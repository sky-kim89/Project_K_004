using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  ForgePopup.cs
//  강화소 — 내 카드 하나를 골라 **각인**(마나 −)하거나 **증식**(마릿수 +)시킨다.
//
//  ■ 화면이 답해야 하는 질문은 "어느 카드를 두껍게 할까" 다
//    그래서 덱을 한 줄로 펼치고, 칸마다 **지금까지 새긴 것**을 아이콘으로 적는다.
//    ⚠ 마나·마릿수·골드는 글자가 아니라 아이콘이다 (UI 규칙 7).
//      "−3 마나" 는 글자 셋을 읽어야 알지만 [물방울]−3 은 그림 하나로 끝난다.
//
//  ■ 카드당 각인 1회 · 증식 1회 (사용자 확정, 2026-09-09)
//    ⚠ 둘 다 새긴 카드는 **목록에 아예 오르지 않는다** — 거르는 곳은
//      RunNodeFlow.CollectForgeSlots 다. 고를 수 있는데 아무것도 못 하는 칸이
//      섞여 있으면 그 칸을 누르는 시간이 통째로 낭비된다.
//    한쪽만 새긴 카드는 목록에 남으므로, 이미 새긴 쪽 버튼은 "완료" 로 잠근다 —
//    무엇을 이미 했는지가 화면에 남아야 한다.
//
//  ■ 두 단계 — 카드 → 무엇을 새길까
//    ⚠ 카드를 고른 것만으로는 골드를 내지 않는다. 값은 행동을 누를 때 낸다.
//    ⚠ **낼 값은 버튼 안**이다 (몬스터 상세의 품질 개선 패널과 같은 규칙).
//      제목 줄에 적어 두면 아무도 안 읽는다 — 지갑은 오른쪽 위 한 자리다.
//
//  ■ ⚠ 제단과 화면을 합치지 않는다 (사용자 지적, 2026-09-06)
//    둘 다 "카드를 고른다" 로 시작하지만 끝이 다르다 — 여기는 카드가 남고
//    거기는 사라진다. 같은 화면에 두면 무엇이 벌어질지가 흐려진다.
//    공유하는 것은 격자뿐이다 (CardPickPopupBase).
// ============================================================

public class ForgePopup : CardPickPopupBase
{
    [Header("행동 줄 — 카드를 고른 뒤에 살아난다")]
    [SerializeField] GameObject      _actionRoot;
    [SerializeField] TextMeshProUGUI _actionHint;

    [Header("각인 — 마나를 깎는다")]
    [SerializeField] Button          _engraveBtn;
    [SerializeField] TextMeshProUGUI _engraveLabel;   // "각인" / "완료"
    [SerializeField] TextMeshProUGUI _engraveValue;   // [마나] −3
    [SerializeField] GameObject      _engraveCostRoot;
    [SerializeField] TextMeshProUGUI _engraveCost;    // [금] 70

    [Header("증식 — 마릿수를 늘린다")]
    [SerializeField] Button          _breedBtn;
    [SerializeField] TextMeshProUGUI _breedLabel;
    [SerializeField] TextMeshProUGUI _breedValue;     // [마릿수] +3
    [SerializeField] GameObject      _breedCostRoot;
    [SerializeField] TextMeshProUGUI _breedCost;

    Action _onDone;

    protected override void Awake()
    {
        base.Awake();

        _engraveBtn.onClick.AddListener(() => Apply(manaCut: RunNodeRule.ForgeManaCut, extra: 0));
        _breedBtn  .onClick.AddListener(() => Apply(manaCut: 0, extra: RunNodeRule.ForgeExtraSummons));
    }

    public ForgePopup Setup(int stageNumber, List<int> slots, Action onDone)
    {
        _onDone = onDone;

        // ⚠ 제목에 값을 적지 않는다 — 지갑은 오른쪽 위, 낼 값은 버튼 안이다.
        Fill(RunNodeKind.Forge, stageNumber, "강화소", slots);

        return this;
    }

    /// <summary>
    /// 칸 아래 줄 — 여기서는 글을 쓰지 않는다. 새긴 것은 아이콘 배지가 말한다.
    /// (아무것도 안 새긴 카드는 그 줄이 통째로 비어 조용하다)
    /// </summary>
    protected override string DescribeCell(in SummonDeckSlot slot, MonsterSpeciesData species)
        => string.Empty;

    /// <summary>칸의 배지 — 새긴 것만 켠다. 없으면 둘 다 꺼진 채로 둔다.</summary>
    protected override void FillBadges(CardCell cell, in SummonDeckSlot slot)
    {
        if (slot.ManaDiscount > 0)
        {
            cell.ManaBadge.SetActive(true);
            cell.ManaValue.text = $"−{slot.ManaDiscount}";
        }

        if (slot.ExtraSummons > 0)
        {
            cell.CountBadge.SetActive(true);
            cell.CountValue.text = $"+{slot.ExtraSummons}";
        }
    }

    protected override void OnSelectionChanged()
    {
        bool picked = SelectedSlot >= 0;

        _actionRoot.SetActive(picked);

        if (!picked)
        {
            _actionHint.text = "새길 카드를 고르세요";
            return;
        }

        var deck = UserDataManager.Instance.Get<SummonDeckData>();
        SummonDeckSlot slot = deck.GetSlot(SelectedSlot);

        MonsterSpeciesData sp = CardCatalog.Current.GetMonster(slot.Id);
        _actionHint.text = $"{(sp != null ? sp.DisplayName : slot.Id)} — 무엇을 새길까";

        // ⚠ 한 카드에 각인·증식은 각각 한 번뿐이다 (파일 머리 주석)
        int  cost   = RunNodeRule.ForgeCost(StageNumber);
        bool afford = RunGoldRule.Current >= cost;

        DrawAction(_engraveBtn, _engraveLabel, _engraveValue, _engraveCostRoot, _engraveCost,
                   "각인", $"−{RunNodeRule.ForgeManaCut}", $"−{slot.ManaDiscount}",
                   open: slot.ManaDiscount <= 0, afford: afford, costGold: cost);

        DrawAction(_breedBtn, _breedLabel, _breedValue, _breedCostRoot, _breedCost,
                   "증식", $"+{RunNodeRule.ForgeExtraSummons}", $"+{slot.ExtraSummons}",
                   open: slot.ExtraSummons <= 0, afford: afford, costGold: cost);
    }

    // ── 버튼 색 ──────────────────────────────────────────────
    //
    //  ⚠ 런타임 파일이라 FacilityStage(에디터 전용)의 색을 못 쓴다.
    //    같은 값을 두 벌 두는 셈이지만, 에디터 어셈블리를 런타임이 참조할 수는 없다.

    static readonly Color DoneC  = new Color(0.52f, 0.56f, 0.68f, 1f);   // 이미 새긴 쪽
    static readonly Color GoldC  = new Color(1.00f, 0.86f, 0.42f, 1f);   // 낼 수 있다
    static readonly Color ShortC = new Color(1.00f, 0.48f, 0.44f, 1f);   // 골드가 모자라다

    /// <summary>
    /// 버튼 한 짝을 그린다.
    ///
    ///   열린 쪽 : [각인] [마나 −3] [금 70]   — 누를 수 있다
    ///   새긴 쪽 : [완료] [마나 −3(흐림)]      — 값 배지를 끄고 잠근다
    ///
    /// ⚠ 새긴 쪽에 값 배지를 남기면 "이미 새겼는데 또 돈을 내야 하나" 로 읽힌다.
    /// ⚠ 못 낼 때는 값 숫자를 붉게 — 버튼이 왜 잠겼는지가 그 자리에서 읽힌다.
    /// </summary>
    static void DrawAction(Button btn, TextMeshProUGUI label, TextMeshProUGUI value,
                           GameObject costRoot, TextMeshProUGUI cost,
                           string name, string gain, string already, bool open, bool afford,
                           int costGold)
    {
        label.text  = open ? name : "완료";
        label.color = open ? Color.white : DoneC;

        value.text  = open ? gain : already;
        value.color = open ? Color.white : DoneC;

        costRoot.SetActive(open);
        cost.text  = $"{costGold:N0}";
        cost.color = afford ? GoldC : ShortC;

        btn.interactable = open && afford;
    }

    /// <summary>⚠ 골드는 여기서 처음 낸다 — 카드를 고른 것만으로는 안 낸다.</summary>
    void Apply(int manaCut, int extra)
    {
        if (SelectedSlot < 0) return;

        if (RunGoldRule.TrySpend(RunNodeRule.ForgeCost(StageNumber)))
            UserDataManager.Instance.Get<SummonDeckData>()
                           .UpgradeCard(SelectedSlot, manaCut, extra);

        UserDataManager.Instance.RequestSave();

        Action done = _onDone;
        _onDone = null;
        Finish(done);
    }
}
