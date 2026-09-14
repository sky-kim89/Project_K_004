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
    }

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
