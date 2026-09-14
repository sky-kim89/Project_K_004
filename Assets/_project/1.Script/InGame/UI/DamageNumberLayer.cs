using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;

// ============================================================
//  DamageNumberLayer.cs
//  피해 숫자를 화면에 띄운다. **대량**으로 뜨는 것을 전제로 만들었다.
//
//  ■ 왜 UI(Canvas)가 아니라 월드 TMP 인가
//    이 게임은 한 판에 수십 마리가 서로를 때린다. 그 숫자를 Canvas 안에서
//    움직이면 **매 프레임 캔버스가 통째로 다시 조립된다**(레이아웃 리빌드).
//    숫자 하나가 1px 올라갈 때마다 그 비용을 내는 셈이라, 물량이 곧 프레임
//    저하가 된다. 월드 공간 TextMeshPro 는 MeshRenderer 라서 캔버스를
//    거치지 않는다 — 위치를 바꿔도 자기 메시만 다시 그린다.
//
//  ■ 숫자를 아끼는 세 가지 장치
//    ① 합산 — 같은 대상이 MergeWindow 안에 계속 맞으면 **떠 있는 숫자를
//       키운다**. 좀비 여섯 마리가 용사 하나를 두들길 때 숫자 여섯 개가
//       겹쳐 뜨면 읽히지도 않고 비용만 든다.
//    ② 상한 — 동시에 MaxActive 개까지만 존재한다. 넘치면 **가장 오래된 것을
//       뺏어 쓴다**. 새 피해가 안 보이는 것보다 낫다.
//    ③ 풀 — 만든 오브젝트는 절대 버리지 않는다. 꺼 두고 다시 켠다.
//
//  ■ 폰트가 따로 있다 (Resources/DamageNumberFont)
//    숫자·기호 열몇 자만 구운 **정적 아틀라스**다. 기본 폰트를 쓰면 글리프가
//    없을 때 런타임에 아틀라스를 다시 굽는데, 그 순간 프레임이 튄다.
//    만드는 것은 DamageFontCreator (Tools > Project K > 아이콘·텍스처).
//
//  ⚠ 머티리얼을 개체마다 만지지 말 것
//    fontSharedMaterial 을 그대로 쓴다. TMP 는 material 프로퍼티를 건드리는
//    순간 인스턴스를 복제하고, 그러면 숫자 하나하나가 드로우콜이 된다.
//    색은 vertex color(TMP.color)로 바꾼다 — 공짜다.
// ============================================================

public class DamageNumberLayer : MonoBehaviour
{
    // ── 치수 (월드 단위. 카메라 orthographicSize = 12) ────────
    //   유닛 키가 대략 2 유닛이라, 숫자는 그 3분의 1쯤이 읽기 좋다.
    //   TMP 월드 텍스트는 fontSize 10 이 대략 1 월드 유닛이다.

    /// <summary>보통 피해.</summary>
    const float BaseFontSize = 9f;

    /// <summary>마지막 일격 — 한 단계 키워 "죽였다" 를 알린다.</summary>
    const float KillFontSize = 13f;

    /// <summary>숫자가 뜨는 높이 (유닛 발밑 기준).</summary>
    const float SpawnHeight = 1.6f;

    /// <summary>수명 동안 떠오르는 거리.</summary>
    const float RiseDistance = 1.3f;

    /// <summary>좌우 흔들림 폭 — 같은 자리에 겹쳐 뜨는 것을 막는다.</summary>
    const float SpreadX = 0.45f;

    const float Lifetime = 0.75f;

    /// <summary>이 시간 안에 같은 대상이 또 맞으면 새로 띄우지 않고 합산한다.</summary>
    const float MergeWindow = 0.35f;

    /// <summary>동시에 떠 있을 수 있는 최대 개수. 넘치면 가장 오래된 것을 재활용한다.</summary>
    const int MaxActive = 80;

    /// <summary>유닛(100)·공중 이펙트(200)보다 위. UnitSortingSetup 참고.</summary>
    const int SortOrder = 300;

    // ── 색 ───────────────────────────────────────────────────
    //
    //  ⚠ 색이 곧 진영이다 — 누가 맞았는지가 먼저 읽혀야 한다
    //    내가 때린 것(용사가 맞음)과 내가 맞은 것(몬스터·마왕성)이 같은 색이면
    //    난전에서 판이 유리한지 불리한지 알 수 없다.

    /// <summary>용사가 맞았다 — 내가 넣은 피해.</summary>
    static readonly Color DealtColor = new(1.00f, 0.95f, 0.72f);

    /// <summary>마지막 일격 — 더 밝고 크게.</summary>
    static readonly Color KillColor = new(1.00f, 0.78f, 0.24f);

    /// <summary>내 몬스터·마왕성이 맞았다.</summary>
    static readonly Color TakenColor = new(1.00f, 0.42f, 0.38f);

    // ── 싱글턴 ───────────────────────────────────────────────
    //
    //  ⚠ 씬에 배치하지 않는다
    //    부르는 쪽이 ECS 시스템(BattleStatCollectorSystem)이라, 씬에 오브젝트를
    //    두면 "InGame 씬을 다시 구웠더니 숫자가 안 뜬다" 가 된다.
    //    처음 쓸 때 스스로 선다.

    static DamageNumberLayer _instance;

    public static DamageNumberLayer Instance
    {
        get
        {
            if (_instance != null) return _instance;

            var go = new GameObject("DamageNumberLayer");
            _instance = go.AddComponent<DamageNumberLayer>();
            return _instance;
        }
    }

    /// <summary>떠 있는 숫자 하나.</summary>
    class Slot
    {
        public TextMeshPro Text;
        public Transform   Tr;

        public bool    Active;
        public float   Age;
        public Vector3 Origin;
        public float   DriftX;
        public float   Amount;
        public float   FontSize;
        public Color   Color;

        /// <summary>합산 대상. 같은 엔티티가 또 맞으면 이 칸을 키운다.</summary>
        public Entity Victim;
    }

    readonly List<Slot>             _slots    = new(MaxActive);
    readonly Dictionary<Entity, int> _byVictim = new(MaxActive);

    TMP_FontAsset _font;

    void Awake()
    {
        _instance = this;

        // ⚠ 없으면 큰 소리로 알린다 — 조용히 기본 폰트로 흘러가면
        //   런타임 아틀라스 재생성이 도로 살아난다 (이 클래스가 피하려던 것).
        _font = Resources.Load<TMP_FontAsset>(DamageFontNames.ResourceKey);
        if (_font == null)
            Debug.LogError($"[DamageNumberLayer] Resources/{DamageFontNames.ResourceKey} 가 없습니다. " +
                           "Tools > Project K > 아이콘·텍스처 > 데미지 숫자 폰트 를 먼저 실행하세요.");
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ── 표시 ─────────────────────────────────────────────────

    /// <summary>
    /// 피해 숫자 하나를 띄운다.
    ///
    /// 같은 <paramref name="victim"/> 이 MergeWindow 안에 또 맞으면 새 숫자를
    /// 만들지 않고 떠 있는 숫자에 더한다 — 화면도 조용해지고 개수도 줄어든다.
    /// </summary>
    /// <param name="victim">맞은 엔티티. 합산의 열쇠다.</param>
    /// <param name="worldPos">맞은 자리 (유닛 발밑).</param>
    /// <param name="amount">방어 적용 후 실제 피해.</param>
    /// <param name="toHero">용사가 맞았는가 (= 내가 넣은 피해인가).</param>
    /// <param name="isKill">이 타격으로 쓰러졌는가.</param>
    public void Show(Entity victim, Vector3 worldPos, float amount, bool toHero, bool isKill)
    {
        if (amount <= 0f) return;

        // ① 합산 — 떠 있는 같은 대상의 숫자를 키운다
        if (_byVictim.TryGetValue(victim, out int at))
        {
            Slot merged = _slots[at];

            if (merged.Active && merged.Victim == victim && merged.Age < MergeWindow)
            {
                merged.Amount += amount;

                // 마지막 일격은 뒤늦게 와도 승격시킨다 — 죽인 타격이 더 중요하다.
                if (isKill)
                {
                    merged.FontSize = KillFontSize;
                    merged.Color    = KillColor;
                }

                // 다시 튀어 오르게 나이를 조금 되돌린다 (계속 맞고 있다는 신호).
                merged.Age = Mathf.Min(merged.Age, MergeWindow * 0.35f);
                Apply(merged);
                return;
            }

            _byVictim.Remove(victim);
        }

        // ② 빈 칸 확보
        int  index = Take();
        Slot slot  = _slots[index];

        slot.Active   = true;
        slot.Age      = 0f;
        slot.Victim   = victim;
        slot.Amount   = amount;
        slot.Origin   = worldPos + new Vector3(0f, SpawnHeight, 0f);
        slot.DriftX   = Random.Range(-SpreadX, SpreadX);
        slot.FontSize = isKill ? KillFontSize : BaseFontSize;
        slot.Color    = isKill ? KillColor : (toHero ? DealtColor : TakenColor);

        slot.Text.gameObject.SetActive(true);
        Apply(slot);

        _byVictim[victim] = index;
    }

    /// <summary>
    /// 쓸 칸의 번호를 준다.
    ///
    /// 상한에 닿으면 **가장 오래된 칸을 뺏는다.** 새 피해를 못 보여 주는 것보다
    /// 거의 사라져 가던 숫자 하나를 잃는 편이 낫다.
    /// </summary>
    int Take()
    {
        for (int i = 0; i < _slots.Count; i++)
            if (!_slots[i].Active) return i;

        if (_slots.Count < MaxActive)
        {
            _slots.Add(Create());
            return _slots.Count - 1;
        }

        int   oldest    = 0;
        float oldestAge = -1f;

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].Age <= oldestAge) continue;
            oldestAge = _slots[i].Age;
            oldest    = i;
        }

        _byVictim.Remove(_slots[oldest].Victim);
        return oldest;
    }

    Slot Create()
    {
        var go = new GameObject("DamageNumber");
        go.transform.SetParent(transform, worldPositionStays: false);

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.font               = _font;
        tmp.fontSharedMaterial = _font.material;   // ⚠ 공유 머티리얼 — 인스턴스화 금지
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.textWrappingMode   = TextWrappingModes.NoWrap;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.raycastTarget      = false;

        // 자동 크기 조정을 끈다 — 켜져 있으면 글자가 바뀔 때마다 레이아웃을 다시 잰다.
        tmp.enableAutoSizing = false;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sortingOrder      = SortOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        go.SetActive(false);

        return new Slot { Text = tmp, Tr = go.transform };
    }

    // ── 갱신 ─────────────────────────────────────────────────
    //
    //  ⚠ Time.deltaTime 이다 (unscaled 가 아니다)
    //    숫자는 전투의 일부라 배속을 함께 타야 한다. 2배속에서 숫자만
    //    제 속도로 떠오르면 화면이 숫자로 덮인다.

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < _slots.Count; i++)
        {
            Slot slot = _slots[i];
            if (!slot.Active) continue;

            slot.Age += dt;

            if (slot.Age >= Lifetime)
            {
                slot.Active = false;
                slot.Text.gameObject.SetActive(false);

                if (_byVictim.TryGetValue(slot.Victim, out int owner) && owner == i)
                    _byVictim.Remove(slot.Victim);

                continue;
            }

            float t = slot.Age / Lifetime;

            // 위로 떠오르며 옆으로 살짝 흐른다. 뒤로 갈수록 느려진다.
            float rise = RiseDistance * (1f - (1f - t) * (1f - t));

            slot.Tr.localPosition = slot.Origin + new Vector3(slot.DriftX * t, rise, 0f);

            // 뜨는 순간 한 번 부푼다 — 새 피해가 들어왔다는 신호.
            float pop = t < 0.16f ? Mathf.Lerp(1.35f, 1f, t / 0.16f) : 1f;
            slot.Tr.localScale = Vector3.one * pop;

            // 뒤쪽 40% 에서만 사라진다. 처음부터 흐리면 읽을 시간이 없다.
            float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;

            Color c = slot.Color;
            c.a = alpha;
            slot.Text.color = c;
        }
    }

    /// <summary>칸의 값·색·크기를 텍스트에 반영한다. 위치는 LateUpdate 가 잡는다.</summary>
    static void Apply(Slot slot)
    {
        slot.Text.fontSize = slot.FontSize;
        slot.Text.color    = slot.Color;
        slot.Text.SetText(Format(slot.Amount));
    }

    // ── 표기 ─────────────────────────────────────────────────

    /// <summary>
    /// 피해량 표기. 자릿수가 늘어도 폭이 폭발하지 않게 축약한다.
    ///
    /// ⚠ 여기 쓰는 글자는 전부 DamageFontNames.Charset 에 있어야 한다.
    ///   없는 글자는 정적 아틀라스에서 □ 로 나온다 (런타임에 채우지 않는다).
    /// </summary>
    public static string Format(float amount)
    {
        int v = Mathf.Max(1, Mathf.RoundToInt(amount));

        if (v >= 1_000_000) return (v / 1_000_000f).ToString("0.#") + "M";
        if (v >= 10_000)    return (v / 1_000f)    .ToString("0.#") + "K";

        return v.ToString();
    }
}
