using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  AltarPopup.cs
//  제단 — 내 카드 하나를 **바친다**. 카드는 사라지고 표식 하나가 남는다.
//
//  ■ 화면이 답해야 하는 질문은 "무엇을 잃고 무엇이 남는가" 다
//    그래서 칸마다 그 카드가 가진 **표식(시너지)** 을 적는다. 바치면 그중
//    하나가 남으므로, 무엇이 남을 수 있는지가 고르는 유일한 근거다.
//
//  ■ ⚠ 강화소와 화면을 합치지 않는다 (사용자 지적, 2026-09-06)
//    둘 다 "카드를 고른다" 로 시작하지만, 여기서는 **카드가 사라진다.**
//    되돌릴 수 없는 일이라 확인 한 단계를 따로 둔다 — 카드를 고르면
//    "무엇이 사라지고 무엇이 남는지" 를 아래에 크게 적고 나서 누르게 한다.
//    공유하는 것은 격자뿐이다 (CardPickPopupBase).
//
//  ■ ⚠ 마지막 한 장은 못 바친다
//    덱이 비면 아무것도 소환할 수 없다. 그 판정은 부르는 쪽(RunNodeFlow)이
//    이미 하고 들어온다 — 여기서는 격자에 온 것만 그린다.
// ============================================================

public class AltarPopup : CardPickPopupBase
{
    [Header("바치기 — 카드를 고른 뒤에 살아난다")]
    [SerializeField] GameObject      _actionRoot;
    [SerializeField] TextMeshProUGUI _actionHint;
    [SerializeField] Button          _sacrificeBtn;
    [SerializeField] TextMeshProUGUI _sacrificeLabel;

    [Tooltip("버튼 안의 값 배지 — [금][110]. 낼 값은 누를 것과 한 몸이다.")]
    [SerializeField] GameObject      _sacrificeCostRoot;
    [SerializeField] TextMeshProUGUI _sacrificeCost;

    static readonly Color GoldC  = new Color(1.00f, 0.86f, 0.42f, 1f);
    static readonly Color ShortC = new Color(1.00f, 0.48f, 0.44f, 1f);

    Action _onDone;

    protected override void Awake()
    {
        base.Awake();

        _sacrificeBtn.onClick.AddListener(Sacrifice);
    }

    public AltarPopup Setup(int stageNumber, List<int> slots, Action onDone)
    {
        _onDone = onDone;

        // ⚠ 제목에 값을 적지 않는다 — 지갑은 오른쪽 위, 낼 값은 버튼 안이다
        //   (사용자 지적, 2026-09-09 — 한 줄에 두 숫자를 적으면 둘 다 안 읽힌다).
        Fill(RunNodeKind.Altar, stageNumber, "제단", slots);

        return this;
    }

    /// <summary>칸 아래 줄 — 이 카드가 가진 표식. 바치면 이 중 하나가 남는다.</summary>
    protected override string DescribeCell(in SummonDeckSlot slot, MonsterSpeciesData species)
    {
        if (species == null) return "";

        var sb = new System.Text.StringBuilder(32);

        // ⚠ 정본 순서(AllTags)를 훑는다 — 비트를 직접 세면 표식이 늘 때 어긋난다
        foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
        {
            if ((species.Tags & tag) == 0) continue;

            if (sb.Length > 0) sb.Append(" · ");
            sb.Append(MonsterSynergyRule.NameOf(tag));
        }

        return sb.Length > 0 ? sb.ToString() : "표식 없음";
    }

    protected override void OnSelectionChanged()
    {
        bool picked = SelectedSlot >= 0;

        _actionRoot.SetActive(picked);

        if (!picked)
        {
            _actionHint.text = "바칠 카드를 고르세요";
            return;
        }

        var deck = UserDataManager.Instance.Get<SummonDeckData>();
        SummonDeckSlot slot = deck.GetSlot(SelectedSlot);

        MonsterSpeciesData sp = CardCatalog.Current.GetMonster(slot.Id);
        string name = sp != null ? sp.DisplayName : slot.Id;

        // ⚠ 사라진다는 것을 못 박는다 — 되돌릴 수 없는 일이다
        //   ⚠ 한 줄이다. 두 줄로 늘리면 아래 버튼이 밀려 내려간다.
        _actionHint.text = $"<color=#FF8080>{name}</color> 를 바친다 — 표식 하나가 남는다";

        bool afford = RunGoldRule.Current >= RunNodeRule.AltarCost;

        _sacrificeLabel.text = "바친다";
        _sacrificeCostRoot.SetActive(true);
        _sacrificeCost.text  = $"{RunNodeRule.AltarCost:N0}";
        _sacrificeCost.color = afford ? GoldC : ShortC;

        _sacrificeBtn.interactable = afford;
    }

    /// <summary>⚠ 골드는 여기서 처음 낸다 — 카드를 고른 것만으로는 안 낸다.</summary>
    void Sacrifice()
    {
        if (SelectedSlot < 0) return;

        var deck = UserDataManager.Instance.Get<SummonDeckData>();
        SummonDeckSlot slot = deck.GetSlot(SelectedSlot);
        MonsterSpeciesData sp = CardCatalog.Current.GetMonster(slot.Id);

        if (sp != null && RunGoldRule.TrySpend(RunNodeRule.AltarCost))
        {
            MonsterTag left = RunNodeRule.PickRandomTag(sp.Tags);

            deck.ClearSlot(SelectedSlot);

            if (left != MonsterTag.None)
            {
                UserDataManager.Instance.Get<RunBoonData>().AddSynergy(left);
                Debug.Log($"[AltarPopup] 제물 — {sp.DisplayName} → {left} 카운트 +1");
            }
        }

        UserDataManager.Instance.RequestSave();

        Action done = _onDone;
        _onDone = null;
        Finish(done);
    }
}
