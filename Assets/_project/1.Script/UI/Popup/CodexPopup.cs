using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  CodexPopup.cs
//  도감 — 몬스터·특성·장비를 한 번이라도 만나 봤는지 보여 준다.
//
//  ■ 구조
//    탭 3개(몬스터 · 특성 · 장비) + 격자 하나. 전체 화면.
//    칸을 누르면
//      · 몬스터 → MonsterDetailPopup (세부 스탯 + 품질 개선 + 고유 스킬)
//      · 특성·장비 → InfoTooltipUI (이름/설명/스탯)
//
//    ⚠ 장비 탭에서는 **끼울 수 없다** (CodexCategory.Gear 주석 참고)
//      장착은 종족에 하는 일이라 몬스터 상세의 칸이 맡는다. 여기는 목록이다.
//    미보유 칸은 눌러도 아무 일도 없다 — 안 만나 본 것의 정보를 주면 도감이 아니다.
//
//  ■ ⚠ 수집 버프는 없다 (사용자 확정, 2026-09-06)
//    헤더에 "공격력·체력 +42%" 가 떠 있었다. 그 버프가 걸리던 대상은
//    이 게임에서 **적(용사)** 이라 도감을 채울수록 적이 세지고 있었다.
//    그 자리는 이제 **보유 골드**가 쓴다 — 이 화면에서 실제로 쓰는 값이다
//    (몬스터 칸의 품질 개선이 영구 골드로 값을 치른다).
//
//  ■ ⚠ 몬스터 칸은 아이콘이 아니라 초상화다 (사용자 확정, 2026-09-06)
//    옛 칸은 계보 아이콘(LineageIcon) 한 장이라 슬라임 계열 넷이 전부
//    같은 그림이었다. 초상화는 종족마다 다르고 게임 안에서 실제로 보는
//    모습이라 이름을 읽기 전에 무엇인지 알아본다.
//
//  사용법:
//    PopupManager.Instance.Open<CodexPopup>(PopupType.Codex);
// ============================================================

public class CodexPopup : PopupBase
{
    [Header("헤더")]
    [SerializeField] TextMeshProUGUI _progressTmp;   // "수집 84 / 120"
    [SerializeField] TextMeshProUGUI _goldTmp;       // 보유 영구 골드

    [Header("탭 (몬스터 · 특성 · 장비 순 = CodexCategory)")]
    [SerializeField] Button[]          _tabButtons;
    [SerializeField] TextMeshProUGUI[] _tabLabels;
    [SerializeField] Image[]           _tabBodies;   // 선택 표시용 배경

    [Header("격자")]
    [SerializeField] RecycleGridScroll _grid;   // 보이는 만큼만 만들어 돌려 쓴다

    [Header("정보 툴팁 (특성 · 장비)")]
    [SerializeField] InfoTooltipUI _tooltip;

    [Tooltip("시너지 표식 아이콘. ⚠ MonsterSynergyRule.AllTags 순서 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _synergyIcons;

    [Header("닫기")]
    [SerializeField] Button _closeBtn;

    // 셀 자식 이름 — Creator 가 만드는 구조와 반드시 일치해야 한다
    const string FramePath    = "Frame";
    const string FillPath     = "Frame/Fill";
    const string IconPath     = "Frame/Fill/Icon";
    const string NamePath     = "Frame/Fill/Name";
    const string SubPath      = "Frame/Fill/Sub";
    const string BadgeRowPath = "Frame/Fill/BadgeRow";
    const string ManaPath     = "Frame/Fill/BadgeRow/ManaCost/Value";
    const string CountPath    = "Frame/Fill/BadgeRow/CountHolder/SummonCount/Value";
    const string TagRowPath   = "Frame/Fill/TagRow";

    /// <summary>칸에 그릴 수 있는 시너지 표식 수. Creator 가 굽는 칸 수와 같아야 한다.</summary>
    public const int TagSlots = 3;

    static readonly Color LockedAccent = new Color(0.16f, 0.18f, 0.26f);
    static readonly Color LockedFill   = new Color(0.075f, 0.082f, 0.125f);
    static readonly Color OwnedFill    = new Color(0.125f, 0.140f, 0.215f);
    static readonly Color OwnedTextC   = new Color(0.92f, 0.95f, 1.00f);
    static readonly Color LockedTextC  = new Color(0.34f, 0.37f, 0.47f);
    static readonly Color TagTint      = new Color(0.78f, 0.82f, 0.94f);

    CodexCategory _tab = CodexCategory.Monster;

    // 현재 탭의 목록. 셀이 재사용되므로 인덱스로 다시 꺼내 쓴다.
    readonly List<CodexEntry> _entries = new();

    // ── PopupBase 훅 ─────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        _closeBtn?.onClick.AddListener(() => Close());

        if (_tabButtons != null)
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                var cat = (CodexCategory)i;   // 배열 순서 = enum 순서
                if (_tabButtons[i] == null) continue;
                _tabButtons[i].onClick.AddListener(() => SelectTab(cat));
            }
        }
    }

    protected override void OnAfterOpen() => SelectTab(_tab);

    // ── 튜토리얼 창구 (FirstCodexTutorial) ──────────────────

    /// <summary>몬스터 탭으로 연다 — 지난번에 보던 탭이 남아 있을 수 있다.</summary>
    public void SelectMonsterTab() => SelectTab(CodexCategory.Monster);

    /// <summary>
    /// 그 종족이 지금 화면에 깔린 칸. 스크롤 밖이면 null.
    /// ⚠ 칸은 재사용된다 — 이름이 아니라 마지막으로 묶인 항목으로 찾는다.
    /// </summary>
    public RectTransform CellOf(string speciesId)
    {
        foreach (var pair in _boundCells)
        {
            if (pair.Key == null || !pair.Key.activeInHierarchy) continue;
            if (pair.Value < 0 || pair.Value >= _entries.Count) continue;
            if (_entries[pair.Value].Species?.Id != speciesId) continue;
            return pair.Key.transform as RectTransform;
        }
        return null;
    }

    readonly Dictionary<GameObject, int> _boundCells = new();

    protected override void OnAfterClose() => CloseTooltip();

    // ── 탭 ───────────────────────────────────────────────────

    void SelectTab(CodexCategory category)
    {
        _tab = category;

        CloseTooltip();

        RefreshHeader();
        RefreshTabs();
        RefreshGrid();

        // 탭을 바꾸면 항상 맨 위부터 — 중간부터 보이면 당황스럽다
        _grid?.ScrollToTop();
    }

    void RefreshTabs()
    {
        if (_tabLabels == null) return;

        for (int i = 0; i < _tabLabels.Length; i++)
        {
            var cat        = (CodexCategory)i;
            var (owned, t) = CodexCatalog.Progress(cat);
            bool active    = cat == _tab;

            if (_tabLabels[i] != null)
            {
                _tabLabels[i].text  = $"{CodexCatalog.Label(cat)}  <size=80%>{owned}/{t}</size>";
                _tabLabels[i].color = active ? Color.white : LockedTextC;
            }

            if (_tabBodies != null && i < _tabBodies.Length && _tabBodies[i] != null)
                _tabBodies[i].color = active
                    ? CodexTabColors.TabActive
                    : CodexTabColors.TabInactive;
        }
    }

    void RefreshHeader()
    {
        var (owned, total) = CodexCatalog.TotalProgress();

        if (_progressTmp != null)
            _progressTmp.text = LocalizationManager.Instance.Format("수집 <color=#{0}>{1}</color> / {2}",
                                                                   StatBonusColors.Codex, owned, total);

        RefreshGold();
    }

    /// <summary>
    /// 보유 영구 골드. 품질 개선이 여기서 값을 치르므로 도감 화면에 상주한다.
    ///
    /// ⚠ 지갑과 값을 나란히 붙여 두지 않는다
    ///   한때 버튼 바로 위에 "900 (보유 12,400)" 이라고 붙어 있었다. 두 숫자가
    ///   같은 크기로 붙어 있으면 어느 쪽이 낼 돈인지 매번 다시 읽어야 한다.
    ///   지갑은 화면 위(늘 같은 자리), 값은 버튼 위(누를 것 옆)로 갈랐다.
    /// </summary>
    void RefreshGold()
    {
        if (_goldTmp == null) return;
        _goldTmp.text = MonsterGradeUpgradeRule.Wallet.ToString("N0");
    }

    // ── 격자 ─────────────────────────────────────────────────

    void RefreshGrid()
    {
        if (_grid == null) return;

        _entries.Clear();
        _entries.AddRange(CodexCatalog.Build(_tab));

        // 전부 만들지 않는다 — 화면에 걸치는 만큼만 만들고 돌려 쓴다
        _grid.Bind(_entries.Count, BindCell);
    }

    // ⚠ 셀은 재사용된다 — 모든 상태를 매번 덮어써야 한다
    //   "보유일 때만" 칠하는 식으로 두면 그 칸이 미보유 항목으로 넘어갈 때
    //   이전 색·그림·리스너가 그대로 남는다.
    void BindCell(int index, GameObject cell)
    {
        if (index < 0 || index >= _entries.Count) return;

        var entry = _entries[index];
        _boundCells[cell] = index;

        Paint(cell, entry);

        if (!cell.TryGetComponent<Button>(out var btn)) return;

        btn.onClick.RemoveAllListeners();
        // 미보유는 누를 게 없다 — 버튼을 꺼서 눌리는 느낌도 주지 않는다
        btn.interactable = entry.Owned;

        if (!entry.Owned) return;

        var owner = cell.GetComponent<RectTransform>();
        btn.onClick.AddListener(() => OnCellClicked(entry, owner));

        // 특성 칸은 툴팁이다 — PC 는 올려서 연다 (몬스터·장비 칸은 누르면 상세 창이라 올림이 없다)
        bool tooltipCell = entry.Species == null && entry.Gear == null;
        TooltipInput.HookHover(cell,
            tooltipCell ? () => ShowTraitTooltip(entry, owner) : null,
            tooltipCell ? CloseTooltip : null);
    }

    void Paint(GameObject cell, CodexEntry entry)
    {
        var frameTr = cell.transform.Find(FramePath);
        var fillTr  = cell.transform.Find(FillPath);
        var iconTr  = cell.transform.Find(IconPath);
        var nameTr  = cell.transform.Find(NamePath);

        // 테두리 — 칸 사이 간격이 보이려면 이게 있어야 한다.
        // 보유는 등급색, 미보유는 눌러 죽인 회색.
        if (frameTr != null && frameTr.TryGetComponent<Image>(out var frame))
            frame.color = entry.Owned ? entry.Accent : LockedAccent;

        if (fillTr != null && fillTr.TryGetComponent<Image>(out var fill))
            fill.color = entry.Owned ? OwnedFill : LockedFill;

        if (iconTr != null && iconTr.TryGetComponent<Image>(out var icon))
        {
            // 몬스터는 합성 초상화, 특성은 SO 아이콘. 미보유는 그림을 통째로 가린다 —
            // 실루엣만 줘도 못 만난 것의 정답을 말해 준다.
            Sprite art = !entry.Owned ? null
                       : entry.Species != null ? MonsterPortraitProvider.Get(entry.Species)
                       : entry.Icon;

            icon.sprite  = art;
            icon.enabled = art != null;
        }

        if (nameTr != null && nameTr.TryGetComponent<TextMeshProUGUI>(out var tmp))
        {
            // ⚠ 이름 칸 높이는 칸 종류가 정한다 (사용자 지적, 2026-09-17 — 번역된 특성 이름이 칸 밖으로 샜다)
            //   몬스터 칸은 이름 아래에 품질 줄·배지·시너지 표식이 붙어 한 줄뿐이다.
            //   특성·장비처럼 아래가 빈 칸은 **두 줄**을 쓴다 — 긴 이름을 줄이지 않고 감는다.
            //   ⚠ 글을 넣기 전에 높이를 바꾼다 — LocalizedText 가 칸 높이로 한 줄/두 줄을 판단한다.
            bool roomy = entry.Species == null && string.IsNullOrEmpty(entry.SubLabel);
            var  nrt   = tmp.rectTransform;
            nrt.sizeDelta = new Vector2(nrt.sizeDelta.x, UIScale.Line(UIScale.FontSm) * (roomy ? 2f : 1f));

            tmp.text  = entry.Owned ? entry.Name : "?";
            tmp.color = entry.Owned ? OwnedTextC : LockedTextC;
        }

        // 품질 — 등급색을 그대로 쓴다. 테두리와 같은 색이라 눈이 바로 묶어 읽는다.
        var subTr = cell.transform.Find(SubPath);
        if (subTr != null && subTr.TryGetComponent<TextMeshProUGUI>(out var sub))
        {
            // 미보유는 등급까지 가린다 — 뭘 못 만났는지 알려주면 도감이 아니다
            sub.text  = entry.Owned ? (entry.SubLabel ?? "") : "";
            sub.color = entry.Accent;
        }

        PaintMonsterExtras(cell, entry);
    }

    /// <summary>
    /// 몬스터 칸에만 있는 줄 둘 — [마나][마릿수] 배지와 시너지 표식.
    ///
    /// ■ 왜 칸에 넣나 (사용자 요청, 2026-09-06)
    ///   덱을 짜려면 "얼마짜리가 몇 마리 나오고 무슨 시너지인가" 셋을 봐야 하는데,
    ///   옛 칸은 이름과 등급뿐이라 종족마다 상세 팝업을 열었다 닫아야 했다.
    ///   격자에서 바로 비교되면 팝업은 확인용으로만 열게 된다.
    ///
    ///   ⚠ 마나·마릿수는 글자가 아니라 아이콘이다 (UI 규칙 7)
    ///   ⚠ 시너지 이름도 글자가 아니라 그림이다 (MonsterSynergyRule 항목 참고)
    /// </summary>
    void PaintMonsterExtras(GameObject cell, CodexEntry entry)
    {
        var badgeTr = cell.transform.Find(BadgeRowPath);
        var tagTr   = cell.transform.Find(TagRowPath);

        bool show = entry.Owned && entry.Species != null;

        if (badgeTr != null) badgeTr.gameObject.SetActive(show);
        if (tagTr   != null) tagTr.gameObject.SetActive(show);

        if (!show) return;

        var s = entry.Species;

        var manaTr = cell.transform.Find(ManaPath);
        if (manaTr != null && manaTr.TryGetComponent<TextMeshProUGUI>(out var mana))
            mana.text = s.ManaCost.ToString("0.#");

        var countTr = cell.transform.Find(CountPath);
        if (countTr != null && countTr.TryGetComponent<TextMeshProUGUI>(out var count))
            count.text = s.SummonCount.ToString();

        if (tagTr == null) return;

        // ⚠ 표식은 셋까지만 그린다 — 종족 하나가 두세 개를 갖는다 (MonsterTag)
        int slot = 0;
        foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
        {
            if (slot >= TagSlots || slot >= tagTr.childCount) break;
            if ((s.Tags & tag) == 0) continue;

            var child = tagTr.GetChild(slot);
            if (child.TryGetComponent<Image>(out var img))
            {
                int idx = MonsterSynergyRule.IndexOf(tag);
                img.sprite  = (_synergyIcons != null && idx >= 0 && idx < _synergyIcons.Length)
                            ? _synergyIcons[idx] : null;
                img.color   = TagTint;
                img.enabled = img.sprite != null;
            }

            child.gameObject.SetActive(true);
            slot++;
        }

        // 남는 칸은 끈다 — 켜 두면 이전 종족의 표식이 그대로 남는다
        for (int i = slot; i < tagTr.childCount; i++)
            tagTr.GetChild(i).gameObject.SetActive(false);
    }

    // ── 클릭 ─────────────────────────────────────────────────

    void OnCellClicked(CodexEntry entry, RectTransform owner)
    {
        if (!entry.Owned) return;

        // 몬스터 — 세부 스탯 + 고유 스킬 + 품질 개선
        if (entry.Species != null)
        {
            OpenMonsterDetail(entry.Species);
            return;
        }

        // 장비 — 레벨 트랙 + 레벨업 (GearDetailPopup)
        //   ⚠ 툴팁이 아니다 — 여기서 골드를 쓴다. 몬스터 칸과 같은 이유다.
        if (entry.Gear != null)
        {
            OpenGearDetail(entry.Gear);
            return;
        }

        // 특성 — 누른 칸 아래에 띄운다. 읽기만 하는 기록이다.
        //
        // ⚠ Show 가 아니라 ShowAnchored 다
        //   Show 는 툴팁을 누른 칸의 자식으로 옮긴다. 그러면 격자를 다시 그릴 때
        //   칸과 함께 툴팁까지 파괴돼 그 뒤로는 영영 뜨지 않는다.
        //   ShowAnchored 는 부모를 건드리지 않고 위치만 맞춘다.
        //   PC 는 올려서 이미 떠 있다 (TooltipInput) — 누를 때는 모바일만 연다
        if (!TooltipInput.HoverMode) ShowTraitTooltip(entry, owner);
    }

    void ShowTraitTooltip(CodexEntry entry, RectTransform owner)
    {
        if (_tooltip != null)
            _tooltip.ShowAnchored(owner, entry.Name, entry.Desc, entry.StatLine);
    }

    /// <summary>
    /// 세부 스탯과 품질 개선은 전용 팝업이 맡는다 (MonsterDetailPopup).
    ///
    /// ⚠ 툴팁으로는 안 된다 — 여기는 <b>누를 것</b>이 있는 자리다
    ///   특성 탭은 읽기만 하므로 InfoTooltipUI 로 충분하지만, 몬스터 칸은
    ///   돈을 내고 등급을 올린다. 되돌릴 수 없는 지출이라 스탯 전부와
    ///   "무엇이 얼마에 무엇으로 바뀌는가" 를 한 화면에 펴 놓고 눌러야 한다.
    ///
    /// ⚠ 등급이 오르면 격자와 지갑을 함께 다시 그린다
    ///   칸의 테두리색·등급 글자가 그대로면 닫았을 때 옛 등급이 남고,
    ///   헤더의 골드가 그대로면 돈을 안 낸 것처럼 보인다.
    ///   팝업이 도감을 직접 부르지 않고 콜백으로 알린다 — 다른 화면에서도
    ///   열 수 있어야 하기 때문이다.
    /// </summary>
    void OpenMonsterDetail(MonsterSpeciesData species)
    {
        var popup = PopupManager.Instance.Open<MonsterDetailPopup>(PopupType.MonsterDetail);
        if (popup == null) return;

        popup.Setup(species);
        popup.OnUpgraded = () => { RefreshGrid(); RefreshHeader(); };
    }

    /// <summary>
    /// 장비 상세 — 레벨업하면 보유 수(재료로 먹힌다)·레벨·지갑이 함께 바뀐다.
    /// 격자와 헤더를 다시 그려야 닫았을 때 옛 숫자가 안 남는다.
    /// </summary>
    void OpenGearDetail(MonsterGearData gear)
    {
        var popup = PopupManager.Instance.Open<GearDetailPopup>(PopupType.GearDetail);
        if (popup == null) return;

        popup.Setup(gear);
        popup.OnChanged = () => { RefreshGrid(); RefreshHeader(); };
    }

    // 툴팁은 셀 위치를 기준으로 떠 있다 — 목록이 바뀌면 가리키던 칸이 다른 항목이 된다
    void CloseTooltip()
    {
        if (_tooltip != null && _tooltip.IsOpen) _tooltip.Close();
    }
}

// 탭 색 — EditorUIBuilder.Pop 은 Editor 폴더에 있어 런타임에서 참조할 수 없다.
// 값이 달라지면 열었을 때와 탭을 바꿀 때 색이 튀니 같은 값을 유지할 것.
static class CodexTabColors
{
    public static readonly Color TabActive   = new Color(0.24f, 0.40f, 0.74f, 1f);
    public static readonly Color TabInactive = new Color(0.155f, 0.175f, 0.275f, 1f);
}
