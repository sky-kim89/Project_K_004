using BattleGame.Units;
using Unity.Entities;
using UnityEngine;

// ============================================================
//  UnitBuffAuraView.cs
//  강화 버프가 걸린 유닛의 발밑에서 작은 빛기둥이 솟는다.
//
//  ■ 왜 필요한가
//    전투 함성·광전사·철벽방어는 숫자만 조용히 오른다. 1,000마리 난전에서
//    "지금 내 부대가 버프를 받고 있나" 를 알 방법이 화면에 없었다.
//    범위 원(ShowRange)은 시전 순간만 보여 주므로, 지속 시간 동안은 이쪽이 답한다.
//
//  ■ 왜 링이 아니라 기둥인가
//    처음엔 발밑에 얇은 링을 깔았는데 눈에 띄지 않았다. 바닥 장식은 유닛·그림자·
//    장판 이펙트와 같은 평면에서 경쟁하느라 묻힌다.
//    위로 솟는 빛은 그 평면을 벗어나므로, 작아도 움직임 때문에 읽힌다.
//
//  ■ 드로우콜 — 스프라이트 하나를 전부가 공유한다
//    런타임에 한 번 만든 텍스처를 모든 유닛이 함께 쓰고, 구분은 오직
//    SpriteRenderer.color 로 한다. 색은 정점 색이라 머티리얼이 갈리지 않는다.
//    기둥이 몇 개든, 유닛이 몇이든 한 배치로 그려진다.
//
//    ⚠ 여기서 절대 하면 안 되는 것: material.SetXXX / sharedMaterial 수정
//      머티리얼 속성을 유닛마다 바꾸는 순간 인스턴스가 갈라져 배칭이 깨진다.
//      그래서 상승 애니메이션도 셰이더가 아니라 Transform 과 정점 색으로 만든다.
//
//  ■ 버프 하나 = 줄기 셋
//    줄기 하나짜리 첫 판은 너무 가늘어서 난전에서 보이지 않았다.
//    같은 색 줄기를 셋 묶어 서로 다른 박자로 올리면 '타오르는 빛' 이 되어
//    크기를 크게 키우지 않고도 눈에 들어온다.
//
//  ■ 버프가 여럿이면 묶음도 여럿이다 (최대 MaxBuffs)
//    공격력(붉은) + 공격속도(황금) 처럼 동시에 걸리는 경우가 흔하다.
//    묶음끼리 좌우로 벌려 세운다 — 겹치면 색이 섞여 무슨 버프인지 못 읽는다.
//
//    ⚠ 이펙트 풀에서 꺼내 쓰지 않는다 — 배칭이 아니라 수명 때문이다
//      풀은 '스폰 → 정해진 시간 뒤 반납'(EffectAutoReturn) 모델이다.
//      버프는 갱신·중첩·조기 해제로 끝나는 시점이 계속 바뀌어서, 풀로 하면
//      매번 반납 시간을 다시 잡거나 껐다 켜기를 반복하게 된다.
//      자식 스프라이트는 유닛과 수명이 같아 그 관리가 통째로 사라진다.
//
//  ■ 정렬 — '제 유닛 앞' 이지 '모두의 앞' 이 아니다
//    유닛 루트에 SortingGroup 이 있어(UnitSortingSetup) 자식들의 order 는
//    **그 유닛 안에서만** 의미를 갖는다. 무기(105)보다 큰 값을 주면
//      · 제 몸·무기보다는 앞          (버프가 보인다)
//      · 다른 유닛과의 앞뒤는 그룹이 결정  (남의 몸을 덮지 않는다)
//    두 조건이 저절로 성립한다 — 매 프레임 order 를 쫓아다닐 필요가 없다.
//
//  ■ 버프 목록은 매 프레임 훑지 않는다
//    DynamicBuffer 읽기는 비용이 있다. 목록 확인은 0.2초마다,
//    기둥이 솟는 애니메이션만 매 프레임 돈다(Transform 3개 + 색 3개).
// ============================================================

[RequireComponent(typeof(EntityLink))]
public class UnitBuffAuraView : MonoBehaviour
{
    /// <summary>버프 목록 확인 주기(초). 매 프레임 볼 이유가 없다.</summary>
    const float PollInterval = 0.2f;

    /// <summary>한 유닛에 동시에 보여 줄 버프 수. 넘치면 약한 것부터 밀려난다.</summary>
    const int MaxBuffs = 3;

    /// <summary>버프 하나를 이루는 줄기 수. 하나뿐이면 가늘어서 묻힌다.</summary>
    const int StreaksPerBuff = 3;

    /// <summary>실제로 만드는 스프라이트 수. 전부 같은 텍스처라 배치는 여전히 하나다.</summary>
    const int TotalStreaks = MaxBuffs * StreaksPerBuff;

    /// <summary>기둥이 솟기 시작하는 높이 — 유닛 중심보다 아래(발밑).</summary>
    const float FootOffsetY = -0.34f;

    /// <summary>한 번에 솟아오르는 높이.</summary>
    const float RiseHeight = 1.25f;

    /// <summary>한 번 솟는 데 걸리는 시간(초).</summary>
    const float RiseCycle = 0.85f;

    /// <summary>줄기 하나의 가로 폭.</summary>
    const float StreakWidth = 0.2f;

    /// <summary>줄기 하나의 세로 길이.</summary>
    const float StreakHeight = 0.75f;

    /// <summary>한 묶음 안에서 줄기끼리 벌리는 간격.</summary>
    const float StreakGap = 0.13f;

    /// <summary>버프 묶음끼리 벌리는 간격.</summary>
    const float BuffGap = 0.46f;

    EntityLink       _link;
    SpriteRenderer[] _streaks;      // [버프 i * StreaksPerBuff + 줄기 k]
    float            _nextPoll;
    int              _activeCount;  // 지금 보여 주는 '버프' 수 (줄기 수가 아니다)

    /// <summary>
    /// 그룹 안에서 줄기가 설 자리. 무기(105)보다 위면 된다.
    ///
    /// SortingGroup 덕분에 이 값은 다른 유닛과 아무 관계가 없다 —
    /// 유닛끼리의 앞뒤는 그룹이 통째로 정한다.
    /// </summary>
    const int StreakOrder = 110;

    // 유닛마다 다른 박자로 솟게 하는 시작 위상 — 부대 전체가 같이 깜빡이면 기계처럼 보인다
    float _phase;

    // 이번 판정에서 고른 색 — 매 프레임 new 하지 않으려고 들고 있는다
    readonly Color[]    _pickColors = new Color[MaxBuffs];
    readonly float[]    _pickWeight = new float[MaxBuffs];
    readonly StatType[] _pickStat   = new StatType[MaxBuffs];
    int                 _pickCount;

    // ── 공유 스프라이트 ──────────────────────────────────────
    //  모든 유닛이 같은 텍스처를 본다 → 머티리얼 하나 → 한 번에 그려진다.
    static Sprite _shared;

    /// <summary>
    /// 아래가 밝고 위로 갈수록 사라지는 세로 빛줄기.
    ///
    /// ⚠ 텍스처의 알파는 '모양' 이지 '흐림' 이 아니다
    ///   첫 판은 전체를 230/255 로 깔아 두어 어디를 봐도 반투명했다.
    ///   심지(가운데)는 꽉 찬 255 로 두고, 가장자리와 꼭대기에서만 알파를
    ///   떨어뜨려야 흐릿하지 않으면서 기둥 모양이 나온다.
    ///
    /// 흰색으로 만들어 두고 색은 런타임에 정점 색으로 입힌다 —
    /// 텍스처를 색깔별로 만들면 그만큼 배치가 갈린다.
    /// </summary>
    static Sprite SharedPillar()
    {
        if (_shared != null) return _shared;

        const int W = 16, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            name       = "BuffPillar(shared)",
        };

        var px = new Color32[W * H];
        float cx = (W - 1) * 0.5f;
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)(H - 1);           // 0 = 바닥, 1 = 꼭대기

            // 아래 2/3 는 꽉 찬 채로 두고 꼭대기 부근에서만 사라진다.
            // 전 구간을 선형으로 떨구면 기둥 전체가 반투명해 보인다.
            float vertical = Mathf.Clamp01((1f - v) * 3f) * Mathf.Clamp01(v * 8f);

            // 위로 갈수록 좁아진다 — 곧게 뻗은 사각형은 기둥이 아니라 막대로 보인다
            float halfW = Mathf.Lerp(cx, cx * 0.35f, v);

            for (int x = 0; x < W; x++)
            {
                float h = 1f - Mathf.Clamp01(Mathf.Abs(x - cx) / Mathf.Max(halfW, 0.001f));

                // 가운데 절반은 통째로 꽉 채우고, 바깥쪽만 부드럽게 떨군다.
                // (h*h 로 전체를 깎으면 심지까지 옅어져 흐릿해진다)
                float core = Mathf.Clamp01(h * 2.2f);

                byte a = (byte)(Mathf.Clamp01(vertical * core) * 255f);
                px[y * W + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);

        // pivot 을 바닥 가운데로 — 발밑에서 자라 올라가는 것처럼 보이게 한다
        _shared = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), H);
        _shared.name = "BuffStreak(shared)";
        return _shared;
    }

    // ── 생명주기 ─────────────────────────────────────────────

    void Awake()
    {
        _link = GetComponent<EntityLink>();
        // 유닛마다 다른 시작 위상 — 같은 프레임에 스폰돼도 함께 깜빡이지 않는다
        _phase = Random.value;
    }

    void OnEnable()
    {
        _nextPoll    = 0f;
        _activeCount = 0;

        Hide();
    }

    void LateUpdate()
    {
        RefreshPicks();
        Animate();
    }

    /// <summary>0.2초마다 버프 목록을 훑어 보여 줄 색을 정한다.</summary>
    void RefreshPicks()
    {
        if (Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + PollInterval;

        if (_link == null || _link.Entity == Entity.Null) { Deactivate(); return; }

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) { Deactivate(); return; }

        var em = world.EntityManager;
        if (!em.Exists(_link.Entity) || !em.HasBuffer<StatusEffectBufferElement>(_link.Entity))
        {
            Deactivate();
            return;
        }

        var buffs = em.GetBuffer<StatusEffectBufferElement>(_link.Entity);

        // 걸려 있는 강화를 스탯별로 모은다 — 같은 스탯이 여러 줄이면 센 쪽만 남긴다
        _pickCount = 0;

        for (int i = 0; i < buffs.Length; i++)
        {
            var b = buffs[i];
            if (b.Mode == EffectMode.Dot) continue;

            // 강화만 — 약화(둔화·저주)는 여기서 표시하지 않는다.
            // 블리자드 장판 안에서 100마리가 전부 기둥을 세우면 아무것도 안 보인다.
            bool isBuff = b.Mode == EffectMode.Multiply ? b.Delta > 1.001f : b.Delta > 0.001f;
            if (!isBuff) continue;

            // 색표는 BuffStatPalette 하나뿐이다 — 스킬 범위 원도 같은 표를 쓰므로
            // 범위 안에 선 병사의 기둥 색과 원 색이 어긋날 수 없다.
            if (!BuffStatPalette.TryGet(b.Stat, out var c)) continue;

            // 세기 — 비율은 초과분, 절대값은 그대로
            float weight = b.Mode == EffectMode.Multiply ? b.Delta - 1f : b.Delta;
            AddPick(b.Stat, c, weight);
        }

        SortByStat();
        Activate(_pickCount);
    }

    /// <summary>후보에 넣는다. 같은 스탯은 센 쪽만 남고, 자리가 없으면 약한 것이 밀려난다.</summary>
    void AddPick(StatType stat, Color color, float weight)
    {
        for (int i = 0; i < _pickCount; i++)
        {
            if (_pickStat[i] != stat) continue;
            if (weight > _pickWeight[i]) _pickWeight[i] = weight;
            return;
        }

        if (_pickCount < MaxBuffs)
        {
            _pickStat[_pickCount]   = stat;
            _pickColors[_pickCount] = color;
            _pickWeight[_pickCount] = weight;
            _pickCount++;
            return;
        }

        int weakest = 0;
        for (int i = 1; i < _pickCount; i++)
            if (_pickWeight[i] < _pickWeight[weakest]) weakest = i;

        if (weight <= _pickWeight[weakest]) return;
        _pickStat[weakest]   = stat;
        _pickColors[weakest] = color;
        _pickWeight[weakest] = weight;
    }

    /// <summary>
    /// 기둥 자리를 StatType 으로 고정한다.
    ///
    /// ⚠ 발견 순서대로 두면 기둥 색이 매번 자리를 바꾼다
    ///   버프 버퍼는 만료·추가로 순서가 계속 바뀐다. 그대로 쓰면 0.2초마다
    ///   빨강과 노랑이 좌우를 오가며 튄다. 같은 스탯은 항상 같은 자리에 온다.
    /// </summary>
    void SortByStat()
    {
        for (int i = 1; i < _pickCount; i++)
        for (int j = i; j > 0 && _pickStat[j] < _pickStat[j - 1]; j--)
        {
            (_pickStat[j],   _pickStat[j - 1])   = (_pickStat[j - 1],   _pickStat[j]);
            (_pickColors[j], _pickColors[j - 1]) = (_pickColors[j - 1], _pickColors[j]);
            (_pickWeight[j], _pickWeight[j - 1]) = (_pickWeight[j - 1], _pickWeight[j]);
        }
    }

    // ── 표시 ─────────────────────────────────────────────────

    void Activate(int count)
    {
        if (count <= 0) { Deactivate(); return; }
        if (_streaks == null) Build();

        _activeCount = count;

        // 켜고 끄기만 한다 — 자리는 Animate 가 매 프레임 잡는다
        for (int i = 0; i < MaxBuffs; i++)
        {
            bool on = i < count;
            for (int k = 0; k < StreaksPerBuff; k++)
            {
                var go = _streaks[i * StreaksPerBuff + k].gameObject;
                if (go.activeSelf != on) go.SetActive(on);
            }
        }
    }

    void Deactivate()
    {
        _activeCount = 0;
        Hide();
    }

    void Hide()
    {
        if (_streaks == null) return;
        for (int i = 0; i < _streaks.Length; i++)
            if (_streaks[i] != null && _streaks[i].gameObject.activeSelf)
                _streaks[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// 줄기를 아래에서 위로 밀어 올린다.
    ///
    /// ⚠ 셰이더가 아니라 Transform 으로 움직인다
    ///   머티리얼 속성을 유닛마다 만지면 인스턴스가 갈라져 배칭이 깨진다.
    ///   위치·스케일·정점 색만 쓰면 머티리얼이 하나로 유지된다.
    ///
    /// ⚠ 투명도로 등장·퇴장을 만들지 않는다
    ///   첫 판은 올라가는 내내 알파를 오르내리게 해서, 밝은 순간이 짧고
    ///   대부분의 시간을 흐릿하게 보냈다. 지금은 **끝의 15%에서만** 사라진다.
    ///   나머지 구간은 색 그대로 꽉 찬 상태다.
    ///
    /// 시간은 게임 시간(Time.time)을 쓴다 — 배속을 올리면 함께 빨라져야
    /// 전투 속도와 따로 노는 느낌이 나지 않는다.
    /// </summary>
    void Animate()
    {
        if (_activeCount <= 0 || _streaks == null) return;

        float buffStartX = -(_activeCount - 1) * 0.5f * BuffGap;
        float now        = Time.time;

        for (int i = 0; i < _activeCount; i++)
        {
            float slotX = buffStartX + BuffGap * i;
            var   color = _pickColors[i];

            for (int k = 0; k < StreaksPerBuff; k++)
            {
                var sr = _streaks[i * StreaksPerBuff + k];
                if (sr == null || !sr.gameObject.activeSelf) continue;

                // 줄기마다 박자를 어긋나게 — 셋이 나란히 오르면 막대그래프가 된다
                float t = Mathf.Repeat(now / RiseCycle + _phase + i * 0.37f + k * 0.31f, 1f);

                // 감속 — 위로 갈수록 힘이 빠지는 느낌
                float ease = 1f - (1f - t) * (1f - t);

                // 가운데 줄기가 가장 높이, 바깥 줄기는 낮게 — 불꽃처럼 뭉쳐 보인다
                float centerBias = 1f - Mathf.Abs(k - (StreaksPerBuff - 1) * 0.5f)
                                        / Mathf.Max(1f, (StreaksPerBuff - 1) * 0.5f);
                float height = RiseHeight * Mathf.Lerp(0.62f, 1f, centerBias);
                float x      = slotX + (k - (StreaksPerBuff - 1) * 0.5f) * StreakGap;

                sr.transform.localPosition = new Vector3(x, FootOffsetY + height * ease, 0f);

                float stretch = Mathf.Lerp(0.7f, 1.2f, Mathf.Sin(t * Mathf.PI));
                sr.transform.localScale = new Vector3(
                    StreakWidth,
                    StreakHeight * stretch * Mathf.Lerp(0.75f, 1f, centerBias),
                    1f);

                // 끝에서만 사라진다 — 나머지 구간은 꽉 찬 색이다
                color.a  = Mathf.Clamp01((1f - t) / 0.15f);
                sr.color = color;
            }
        }
    }

    void Build()
    {
        _streaks = new SpriteRenderer[TotalStreaks];

        var sprite = SharedPillar();

        for (int i = 0; i < MaxBuffs; i++)
        for (int k = 0; k < StreaksPerBuff; k++)
        {
            var go = new GameObject("BuffStreak_" + i + "_" + k);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, FootOffsetY, 0f);
            go.transform.localScale    = new Vector3(StreakWidth, StreakHeight, 1f);
            go.SetActive(false);

            var sr    = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;


            // 그룹 안이라 값이 고정이다 — 유닛끼리의 앞뒤는 SortingGroup 이 정한다
            sr.sortingOrder = StreakOrder;

            _streaks[i * StreaksPerBuff + k] = sr;
        }
    }
}
