using UnityEngine;

// ============================================================
//  MonsterGearAuraView.cs
//  좋은 장비를 낀 몬스터 주위를 작은 빛이 맴돈다.
//
//  ■ 왜 필요한가
//    장비는 도감에서 끼우고 전투는 라인에서 벌어진다. 그 둘 사이에 "지금
//    이 녀석이 뭘 걸치고 있나" 를 말해 주는 것이 화면에 없으면, 장비를 낀
//    보람이 숫자로만 남는다. 인간형은 겉모습이 바뀌어 티가 나지만
//    비인간형은 색조·덩치뿐이라 더더욱 필요하다.
//
//  ■ 빛기둥(UnitBuffAuraView)과 일부러 다르게 생겼다
//    그쪽은 발밑에서 **솟는다** — 지금 걸린 버프, 곧 사라질 것.
//    이쪽은 몸 주위를 **돈다** — 늘 붙어 있는 것, 이 개체의 성질.
//    움직임이 다르면 둘이 같이 떠 있어도 무엇이 무엇인지 갈린다.
//
//  ■ 등급이 문턱을 넘은 장비 하나당 빛 하나 (MonsterGearRule.AuraMinGrade)
//    낮은 등급까지 빛나면 "좋은 걸 꼈다" 는 신호가 죽는다.
//    색은 그 장비의 등급색이라, 빛의 개수와 색만 보고 대충의 세기를 안다.
//
//  ■ 드로우콜 — 스프라이트 하나를 전부가 공유한다
//    UnitBuffAuraView 와 같은 규율이다. 런타임에 한 번 만든 텍스처를 모두가
//    함께 쓰고, 구분은 오직 SpriteRenderer.color(정점 색)로 한다.
//    ⚠ material.SetXXX / sharedMaterial 을 건드리지 말 것 — 그 순간 배칭이 깨진다.
//
//  ■ 정렬 — 빛기둥보다 **뒤**다
//    유닛 루트에 SortingGroup 이 있어(UnitSortingSetup) order 는 그 유닛
//    안에서만 의미를 갖는다. 무기(105)보다는 앞, 버프 기둥(110)보다는 뒤에
//    둔다 — 버프는 지금 판단해야 할 정보고, 장비는 늘 있는 장식이다.
//
//  ⚠ 풀에서 재사용된다 — OnEnable 에서 반드시 끈다
//    안 끄면 지난 개체의 빛을 그대로 달고 나온다. 장비를 안 낀 몬스터가
//    빛나는 것보다 나쁜 것은 없다.
// ============================================================

public class MonsterGearAuraView : MonoBehaviour
{
    /// <summary>동시에 돌 수 있는 빛의 수. 장비 칸 수와 같다.</summary>
    const int MaxOrbs = MonsterGearRule.MaxSlots;

    /// <summary>몸 중심에서 빛까지의 거리.</summary>
    const float Radius = 0.42f;

    /// <summary>한 바퀴 도는 데 걸리는 시간(초).</summary>
    const float OrbitCycle = 2.6f;

    /// <summary>궤도를 눕히는 정도 — 1 이면 정원, 작을수록 납작한 타원이 된다.</summary>
    const float OrbitFlatten = 0.42f;

    /// <summary>궤도의 세로 중심 — 몸 가운데쯤.</summary>
    const float CenterY = 0.18f;

    /// <summary>빛 하나의 크기(월드 단위).</summary>
    const float OrbSize = 0.17f;

    /// <summary>뒤로 돌아갔을 때 얼마나 흐려지는가. 0 이면 아예 사라진다.</summary>
    const float BackAlpha = 0.28f;

    /// <summary>SortingGroup 안에서의 자리. 무기(105)보다 앞, 버프 기둥(110)보다 뒤.</summary>
    const int OrbOrder = 108;

    SpriteRenderer[] _orbs;
    int              _activeCount;

    // 유닛마다 다른 시작 위상 — 부대 전체가 같은 자리에서 돌면 기계처럼 보인다
    float _phase;

    // ── 공유 스프라이트 ──────────────────────────────────────

    static Sprite _shared;

    /// <summary>
    /// 가운데가 꽉 차고 가장자리로 갈수록 사라지는 둥근 빛.
    ///
    /// 흰색으로 만들어 두고 색은 런타임에 정점 색으로 입힌다 —
    /// 텍스처를 색깔별로 만들면 그만큼 배치가 갈린다.
    /// </summary>
    static Sprite SharedOrb()
    {
        if (_shared != null) return _shared;

        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            name       = "GearOrb(shared)",
        };

        var   px = new Color32[S * S];
        float c  = (S - 1) * 0.5f;

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;

            // 심지(안쪽 35%)는 꽉 채우고 바깥쪽만 부드럽게 떨군다.
            // 전 구간을 선형으로 떨구면 빛 전체가 뿌옇게만 보인다.
            float a = d >= 1f ? 0f
                    : d <= 0.35f ? 1f
                    : Mathf.Pow(1f - (d - 0.35f) / 0.65f, 1.6f);

            px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);

        _shared = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        _shared.name = "GearOrb(shared)";
        return _shared;
    }

    // ── 생명주기 ─────────────────────────────────────────────

    void Awake() => _phase = Random.value;

    /// <summary>
    /// ⚠ 풀에서 나올 때마다 끈다
    ///   MonsterRuntimeBridge 가 스폰 직후 Setup 을 부른다. 그 전까지는
    ///   지난 개체의 빛이 남아 있으면 안 된다.
    /// </summary>
    void OnEnable() => Clear();

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>장비가 만든 빛을 켠다. 스폰할 때 한 번 부른다.</summary>
    public void Setup(in MonsterGearVisual visual)
    {
        _activeCount = Mathf.Clamp(visual.AuraCount, 0, MaxOrbs);

        if (_activeCount == 0) { Clear(); return; }

        EnsureOrbs();

        for (int i = 0; i < _orbs.Length; i++)
        {
            bool on = i < _activeCount;
            _orbs[i].gameObject.SetActive(on);
            if (on) _orbs[i].color = visual.AuraColors[i];
        }
    }

    public void Clear()
    {
        _activeCount = 0;
        if (_orbs == null) return;

        foreach (var o in _orbs)
            if (o != null) o.gameObject.SetActive(false);
    }

    // ── 내부 ─────────────────────────────────────────────────

    /// <summary>
    /// 빛을 만든다 — 처음 필요해진 순간 딱 한 번.
    ///
    /// ⚠ 미리 만들어 두지 않는다
    ///   장비를 낀 몬스터가 전체의 일부라, 프리팹에 미리 셋을 박아 두면
    ///   대부분의 개체가 쓰지도 않는 SpriteRenderer 셋을 들고 다닌다.
    /// </summary>
    void EnsureOrbs()
    {
        if (_orbs != null) return;

        _orbs = new SpriteRenderer[MaxOrbs];

        for (int i = 0; i < MaxOrbs; i++)
        {
            var go = new GameObject($"GearOrb{i}");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = SharedOrb();
            sr.sortingOrder = OrbOrder;

            go.transform.localScale = Vector3.one * OrbSize;
            go.SetActive(false);

            _orbs[i] = sr;
        }
    }

    /// <summary>
    /// 빛을 돌린다.
    ///
    /// ⚠ 무한 반복이다 — 끝나는 시점이 없다
    ///   장비는 벗기 전까지 붙어 있는 것이라 재생이 끝나면 안 된다.
    ///   그래서 트윈 라이브러리를 쓰지 않고 시간에서 바로 자리를 계산한다 —
    ///   풀에서 껐다 켜도 상태를 되돌릴 것이 없다.
    ///
    /// ⚠ Time.time 이 아니라 unscaledTime 이다
    ///   배속·일시정지에 빛이 멈추면 화면이 얼어붙은 것처럼 보인다.
    ///   전투 판정이 아니라 장식이라 게임 시간을 따를 이유가 없다.
    /// </summary>
    void LateUpdate()
    {
        if (_activeCount == 0 || _orbs == null) return;

        float t = Time.unscaledTime / OrbitCycle + _phase;

        for (int i = 0; i < _activeCount; i++)
        {
            // 빛끼리 같은 간격으로 벌린다 — 겹치면 하나로 보인다.
            float angle = (t + i / (float)_activeCount) * Mathf.PI * 2f;

            float sin = Mathf.Sin(angle);

            _orbs[i].transform.localPosition = new Vector3(
                Mathf.Cos(angle) * Radius,
                CenterY + sin * Radius * OrbitFlatten,
                0f);

            // 뒤로 돌아가면(sin > 0 = 위쪽) 흐려진다 — 몸을 돌아 나가는 것처럼 읽힌다.
            // ⚠ 정렬을 바꾸지 않는다. order 를 매 프레임 만지면 배치가 갈린다.
            Color c = _orbs[i].color;
            c.a = sin > 0f ? Mathf.Lerp(1f, BackAlpha, sin) : 1f;
            _orbs[i].color = c;
        }
    }
}
