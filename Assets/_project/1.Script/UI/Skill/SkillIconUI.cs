using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  SkillIconUI.cs
//  스킬(액티브·패시브) 아이콘 1칸을 표시하는 재사용 컴포넌트.
//  TraitIconUI 와 같은 구조 — 아이콘 + 누르면 InfoTooltipUI.
//
//  ■ 재사용 위치
//    - MainPanel 장수 선택 카드 (GeneralCandidateCardUI)
//    - 앞으로 스킬 아이콘이 필요한 모든 UI
//
//  ■ 아이콘 출처가 둘로 갈린다
//    액티브 : ActiveSkillData 에 Sprite 필드가 없다 →
//             ActiveSkillId.IconKey() 로 SpriteManager 에서 꺼낸다.
//    패시브 : PassiveSkillData.Icon 을 그대로 쓴다.
//
//  ■ 잠긴 칸은 숨기지 않는다
//    등급이 낮아 아직 못 쓰는 패시브 칸은 SetLocked() 로 흐리게 남긴다.
//    칸을 아예 없애면 "등급을 올리면 늘어난다" 는 정보가 사라진다.
// ============================================================

public class SkillIconUI : MonoBehaviour
{
    [SerializeField] Image         _frame;       // 테두리 — 액티브 / 패시브 / 잠김 구분
    [SerializeField] Image         _slotBg;      // 아이콘 뒤 판 — 밝게 깔아 어두운 그림을 살린다
    [SerializeField] Image         _iconImage;
    [SerializeField] Button        _iconBtn;
    [SerializeField] InfoTooltipUI _tooltip;

    public static readonly Color ActiveFrame  = new(0.94f, 0.70f, 0.26f, 1f);   // 금빛 = 액티브
    public static readonly Color PassiveFrame = new(0.34f, 0.56f, 0.92f, 1f);   // 파랑 = 패시브
    public static readonly Color LockedFrame  = new(0.16f, 0.17f, 0.26f, 1f);

    // ── 아이콘 판 색 ──────────────────────────────────────────
    //
    //  ⚠ 흰색으로 깔지 말 것 (2026-08-26 에 한 번 해 보고 되돌렸다)
    //    스킬 그림은 어두운 외곽선 **과** 크림색 하이라이트를 같이 갖는다.
    //    흰 판 위에서는 외곽선만 살고 하이라이트가 통째로 묻힌다.
    //    반대로 원래의 짙은 남색 판에서는 하이라이트만 살고 형태가 안 읽혔다.
    //    → 판은 **중간 톤**이어야 양쪽이 다 산다 (휘도 약 0.40).
    //
    //  ⚠ 계열 색을 태워 둔다
    //    테두리가 이미 액티브=금 / 패시브=파랑으로 갈려 있다. 판까지 같은 계열로
    //    맞추면 한 덩어리로 읽히고, 카드에 액티브 1 + 패시브 3 이 나란히 설 때
    //    무엇이 무엇인지 색으로 먼저 구분된다.
    //
    //  ⚠ 이 색은 세 화면이 같이 쓴다 — 로비 카드 · 장수 상세 · 인게임 스킬 슬롯.
    //    한 곳만 바꾸면 같은 아이콘이 화면마다 다른 바탕에 앉는다.
    public static readonly Color ActiveSlotBg  = new(0.46f, 0.40f, 0.30f, 1f);   // 따뜻한 돌색
    public static readonly Color PassiveSlotBg = new(0.34f, 0.40f, 0.52f, 1f);   // 서늘한 청회색

    // 잠긴 칸은 어둡게 둔다 —
    // 밝게 깔면 '아직 못 쓰는 칸' 이 화면에서 가장 눈에 띄는 자리가 된다.
    public static readonly Color LockedSlotBg  = new(0.07f, 0.08f, 0.16f, 1f);

    string _title, _desc, _stat;

    // ── 공개 API ─────────────────────────────────────────────

    public void SetActiveSkill(ActiveSkillId id, ActiveSkillData data)
    {
        string title = data != null && !string.IsNullOrEmpty(data.SkillName)
            ? data.SkillName
            : LocalizationManager.Instance.Get(id.ToString());

        Bind(SpriteManager.Instance?.Get(id.IconKey()), ActiveFrame, ActiveSlotBg,
             title, data != null ? data.Description : "",
             data != null ? $"쿨타임 {data.Cooldown:0.#}초" : "");
    }

    public void SetPassiveSkill(PassiveSkillData data)
    {
        if (data == null) { SetLocked(); return; }
        Bind(data.Icon, PassiveFrame, PassiveSlotBg, data.SkillName, data.Description, "");
    }

    /// <summary>등급이 모자라 아직 열리지 않은 칸.</summary>
    public void SetLocked()
    {
        CloseTooltip();
        _title = _desc = _stat = null;

        if (_frame     != null) _frame.color   = LockedFrame;
        if (_slotBg    != null) _slotBg.color  = LockedSlotBg;

        // ⚠ 색으로 흐리게 하지 않고 아예 끈다
        //   예전엔 어두운 색을 칠해 뒀는데, 판이 밝아지면 그 색이 '어두운 사각형' 으로
        //   또렷하게 보인다. 빈 칸은 판만 남아야 빈 칸으로 읽힌다.
        if (_iconImage != null) { _iconImage.sprite = null; _iconImage.enabled = false; }
        if (_iconBtn   != null)
        {
            _iconBtn.onClick.RemoveAllListeners();
            _iconBtn.interactable = false;
        }
    }

    public void CloseTooltip()
    {
        if (_tooltip != null) _tooltip.Close();
    }

    void OnDisable() => CloseTooltip();

    // ── 내부 ─────────────────────────────────────────────────

    void Bind(Sprite icon, Color frame, Color slotBg, string title, string desc, string stat)
    {
        CloseTooltip();

        _title = title;
        _desc  = desc;
        _stat  = stat;

        if (_frame  != null) _frame.color  = frame;
        if (_slotBg != null) _slotBg.color = icon != null ? slotBg : LockedSlotBg;

        if (_iconImage != null)
        {
            _iconImage.sprite  = icon;
            _iconImage.color   = Color.white;
            _iconImage.enabled = icon != null;
        }

        if (_iconBtn != null)
        {
            _iconBtn.onClick.RemoveAllListeners();
            _iconBtn.interactable = true;
            _iconBtn.onClick.AddListener(OpenTooltip);
        }
    }

    void OpenTooltip() => _tooltip.Show(_title, _desc, _stat);
}
