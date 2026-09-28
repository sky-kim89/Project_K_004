using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  CardPickPopupBase.cs
//  "내 카드 중 하나를 고른다" — 강화소·제단이 공유하는 **격자 부분**.
//
//  ■ ⚠ 화면을 합치는 것이 아니다 (사용자 지적, 2026-09-06)
//    한때 강화소·제단·상점을 글자 목록 하나(FacilityPopup)로 퉁쳤다.
//    카드를 고르는 자리인데 카드가 안 보이니 무엇을 바치는지 알 수가 없었다.
//    지금은 화면이 갈라져 있고, 여기 있는 것은 **둘이 정말로 똑같이 하는 일**
//    — "내 덱을 격자로 펼치고 하나를 고른다" — 그 한 가지뿐이다.
//    고른 다음에 무엇을 하는가(각인/증식 · 바치기)는 각자 자기 화면에서 한다.
//
//  ■ 두 단계다
//    ① 카드를 고른다 → 테두리가 켜지고 아래 행동 줄이 살아난다
//    ② 행동을 고른다 → 그때 골드를 낸다
//    ⚠ ①에서 돈을 내지 않는다. 카드를 눌러 본 것만으로 값을 치르면
//      물러날 방법이 없다 (옛 RunNodeFlow.OpenForgeKind 주석과 같은 이유).
//
//  ■ 카드 칸이 보여 주는 것
//    초상화 · 이름 · Lv · 그 카드가 지금 어떤 상태인가(파생 클래스가 채운다).
//    ⚠ 마나·마릿수는 아이콘이다 — 글자로 적지 않는다 (UI 규칙 7).
// ============================================================

public abstract class CardPickPopupBase : PopupBase
{
    [Header("배경 · 글")]
    [SerializeField] protected Image           _art;
    [SerializeField] protected TextMeshProUGUI _titleText;
    [SerializeField] protected TextMeshProUGUI _flavorText;

    [Tooltip("오른쪽 위 보유 골드 숫자. 아이콘은 프리팹이 이미 달고 있다.")]
    [SerializeField] protected TextMeshProUGUI _purseValue;

    [Tooltip("갈림길·시설 그림. ⚠ RunNodeRule.AllKinds 순서 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _nodeArt;

    [Tooltip("시너지 표식 아이콘. ⚠ MonsterSynergyRule.AllTags 순서 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _synergyIcons;

    [Header("카드 격자")]
    [SerializeField] protected CardCell[] _cells;

    [Header("닫기")]
    [SerializeField] Button _closeBtn;

    [Serializable]
    public class CardCell
    {
        public GameObject      Root;
        public Button          Button;
        public Image           Portrait;
        public Image           Frame;       // 고른 칸만 밝아진다
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI LevelText;
        public TextMeshProUGUI StateText;   // 파생 화면이 쓰는 한 줄 (글이 맞는 화면만)

        // ── 아이콘 배지 — 마나·마릿수는 글자로 적지 않는다 (UI 규칙 7) ──
        //   강화소가 "이 카드에 무엇을 새겼나" 를 이걸로 보여 준다.
        //   제단처럼 글이 맞는 화면은 StateText 만 쓰고 이쪽을 꺼 둔다.
        public GameObject      ManaBadge;
        public TextMeshProUGUI ManaValue;
        public GameObject      CountBadge;
        public TextMeshProUGUI CountValue;

        // ── 시너지 표식 — 제단이 쓴다 (UI 규칙 7 · 사용자 요청 2026-09-18) ──
        //   ⚠ 배지·글과 **같은 자리**를 쓴다. 화면마다 하나만 켠다.
        public GameObject      TagRow;
        public GameObject[]    TagRoots;
        public Image[]         TagIcons;
        public SynergyChipUI[] TagChips;
    }

    /// <summary>한 칸이 그릴 수 있는 표식 수. 종족 하나가 둘~셋을 갖는다 (MonsterTag).</summary>
    public const int TagSlots = 3;

    /// <summary>격자가 담는 최대 칸 수 = 덱 칸 상한. 정본은 RunPerkRule.MaxDeckSlots 다.</summary>
    public const int MaxCells = RunPerkRule.MaxDeckSlots;

    /// <summary>지금 고른 칸의 **덱 슬롯 번호**. −1 = 아직 안 골랐다.</summary>
    protected int SelectedSlot { get; private set; } = -1;

    /// <summary>격자에 깔린 덱 슬롯 번호들. 칸 순서와 같다.</summary>
    protected readonly List<int> Slots = new(MaxCells);

    Action _onClose;

    static readonly Color FrameOn  = new Color(1.00f, 0.84f, 0.36f);
    static readonly Color FrameOff = new Color(0.24f, 0.27f, 0.38f);

    protected override void Awake()
    {
        base.Awake();

        for (int i = 0; i < _cells.Length; i++)
        {
            int index = i;   // ⚠ 캡처 — 루프 변수를 넘기면 전부 마지막 값이 된다
            _cells[i].Button.onClick.AddListener(() => Select(index));
        }

        _closeBtn?.onClick.AddListener(() => Close());
    }

    // ── 세우기 ───────────────────────────────────────────────

    /// <summary>
    /// 이 화면이 열린 스테이지. 값 계산(RunGoldRule.Price)이 이걸 쓴다.
    ///
    /// ⚠ Fill 이 채운다 — 파생 화면이 따로 들고 있지 말 것.
    /// </summary>
    protected int StageNumber { get; private set; } = 1;

    /// <summary>
    /// 배경·제목·이야기와 카드 격자를 채운다. 파생 화면이 자기 Setup 에서 부른다.
    /// </summary>
    protected void Fill(RunNodeKind kind, int stageNumber, string title, List<int> slots)
    {
        // 값이 스테이지를 따라 오르므로(RunGoldRule.Price) 파생 화면이 이걸 본다.
        StageNumber = stageNumber;

        _titleText.text  = title;
        _flavorText.text = RunNodeFlavor.Of(kind, stageNumber);

        int index  = RunNodeRule.IndexOf(kind);
        Sprite art = (_nodeArt != null && index >= 0 && index < _nodeArt.Length)
                   ? _nodeArt[index] : null;

        _art.sprite  = art;
        _art.enabled = art != null;

        Slots.Clear();
        Slots.AddRange(slots);

        SelectedSlot = -1;

        var deck    = UserDataManager.Instance.Get<SummonDeckData>();
        var catalog = CardCatalog.Current;

        for (int i = 0; i < _cells.Length; i++)
        {
            bool has = i < Slots.Count;
            _cells[i].Root.SetActive(has);
            if (!has) continue;

            SummonDeckSlot     slot = deck.GetSlot(Slots[i]);
            MonsterSpeciesData sp   = catalog.GetMonster(slot.Id);

            Sprite art2 = sp != null ? MonsterPortraitProvider.Get(sp) : null;
            _cells[i].Portrait.sprite  = art2;
            _cells[i].Portrait.enabled = art2 != null;

            // 친화 종족 표식 — 제단에 무엇을 바칠지·강화소에서 무엇을 키울지 고르는 자리다.
            AffinityTagUI.Set(_cells[i].Portrait, sp);

            _cells[i].NameText.text  = sp != null ? sp.DisplayName : slot.Id;
            _cells[i].LevelText.text = $"Lv.{slot.Level}";
            _cells[i].StateText.text = DescribeCell(slot, sp);
            _cells[i].Frame.color    = FrameOff;

            // 배지는 기본이 '끔' 이다 — 쓰는 화면만 켠다 (FillBadges).
            _cells[i].ManaBadge.SetActive(false);
            _cells[i].CountBadge.SetActive(false);
            FillBadges(_cells[i], slot);

            // 표식도 기본이 '끔' 이다 — 배지·글과 같은 자리라 하나만 켜진다.
            FillTagRow(_cells[i], sp);
        }

        RefreshPurse();
        OnSelectionChanged();
    }

    /// <summary>칸 아래 한 줄 — 이 화면이 그 카드에서 무엇을 보여 줄지 정한다.</summary>
    protected abstract string DescribeCell(in SummonDeckSlot slot, MonsterSpeciesData species);

    /// <summary>
    /// 칸의 아이콘 배지를 채운다. 기본은 아무것도 하지 않는다(글만 쓰는 화면).
    ///
    /// ⚠ 마나·마릿수는 글자가 아니라 아이콘이다 (UI 규칙 7).
    /// </summary>
    protected virtual void FillBadges(CardCell cell, in SummonDeckSlot slot) { }

    /// <summary>
    /// 칸 아래 줄에 <b>시너지 표식</b>을 그리는 화면인가. 제단만 켠다.
    ///
    /// ⚠ 배지(마나·마릿수)·글과 <b>같은 자리</b>다 — 셋 중 하나만 켤 것.
    ///   강화소는 그 줄에 "무엇을 새겼나" 를 적으므로 표식이 들어갈 자리가 없다.
    /// </summary>
    protected virtual bool ShowsTagRow => false;

    /// <summary>
    /// 그 종족이 가진 표식을 아이콘으로 세운다.
    ///
    /// ■ ⚠ 시너지 이름은 글자가 아니라 그림이다 (UI 규칙 7 · CLAUDE.md 시너지 항목)
    ///   화면마다 같은 그림을 쓰므로 순서의 정본은 언제나 MonsterSynergyRule.AllTags 다.
    ///
    /// ■ 이미 열린 표식은 밝게, 아직인 것은 흐리게 (CardSelectPopup.FillSynergy 와 같은 규칙)
    ///   ⚠ 단계 색으로 물들이지 않는다 — 아이콘이 저마다 제 색을 갖고 있어서
    ///     회색으로 물들이면 여덟 개가 다 같아 보인다. 밝기만 낮춘다.
    ///
    /// 자세한 효과는 칩에 올리거나 눌러서 본다 (SynergyChipUI · TooltipLayer).
    /// </summary>
    void FillTagRow(CardCell cell, MonsterSpeciesData species)
    {
        if (cell.TagRow == null) return;

        if (!ShowsTagRow || species == null)
        {
            cell.TagRow.SetActive(false);
            return;
        }

        cell.TagRow.SetActive(true);

        int shown = 0;

        // ⚠ 정본 순서(AllTags)를 훑는다 — 비트를 직접 세면 표식이 늘 때 어긋난다
        foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
        {
            if ((species.Tags & tag) == 0)     continue;
            if (shown >= cell.TagRoots.Length) break;

            int    index = MonsterSynergyRule.IndexOf(tag);
            Sprite icon  = (_synergyIcons != null && index >= 0 && index < _synergyIcons.Length)
                         ? _synergyIcons[index] : null;

            bool lit = MonsterSynergyRule.TierOf(tag) != SynergyTier.None;

            cell.TagRoots[shown].SetActive(true);

            cell.TagIcons[shown].sprite  = icon;
            cell.TagIcons[shown].enabled = icon != null;
            cell.TagIcons[shown].color   = lit ? Color.white : DimTag;

            // ⚠ 칩마다 담는 시너지가 카드에 따라 달라진다 — 주인을 매번 다시 알려 준다
            //   (칸이 재사용된다. 안 부르면 옛 카드의 설명이 뜬다)
            if (cell.TagChips[shown] != null) cell.TagChips[shown].Setup(tag);

            shown++;
        }

        // 남는 칸은 끈다 — 켜 두면 이전 종족의 표식이 그대로 남는다
        for (int i = shown; i < cell.TagRoots.Length; i++)
            cell.TagRoots[i].SetActive(false);

        // ⚠ 표식이 하나도 없으면 줄 자체를 끈다
        //   그 자리는 글("표식 없음")이 대신 쓴다 — 빈 줄을 켜 둔 채로 겹쳐 두지 않는다.
        if (shown == 0) cell.TagRow.SetActive(false);
    }

    /// <summary>아직 안 열린 표식의 밝기. 색조는 건드리지 않는다.</summary>
    static readonly Color DimTag = new(1f, 1f, 1f, 0.42f);

    /// <summary>오른쪽 위 지갑을 다시 그린다. 값을 치른 뒤에도 부른다.</summary>
    protected void RefreshPurse()
    {
        if (_purseValue != null) _purseValue.text = $"{RunGoldRule.Current:N0}";
    }

    /// <summary>고른 칸이 바뀌었다. 파생 화면이 아래 행동 줄을 다시 그린다.</summary>
    protected abstract void OnSelectionChanged();

    void Select(int index)
    {
        if (index >= Slots.Count) return;

        SelectedSlot = Slots[index];

        for (int i = 0; i < _cells.Length; i++)
            if (_cells[i].Root.activeSelf)
                _cells[i].Frame.color = i == index ? FrameOn : FrameOff;

        OnSelectionChanged();
    }

    // ── 닫기 ─────────────────────────────────────────────────

    /// <summary>그냥 닫았을 때 부를 것. 닫아도 다음 스테이지로 가야 한다.</summary>
    public void SetOnClose(Action onClose) => _onClose = onClose;

    /// <summary>행동을 끝냈다 — 화면을 닫고 흐름을 이어 간다.</summary>
    protected void Finish(Action step)
    {
        // ⚠ 콜백보다 먼저 비운다 — 닫기 훅이 한 번 더 흐름을 잇지 않게 한다
        _onClose = null;

        Close();
        step?.Invoke();
    }

    protected override void OnAfterClose()
    {
        Action onClose = _onClose;
        _onClose = null;
        onClose?.Invoke();
    }
}
