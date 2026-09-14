using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  ShopPopup.cs
//  갈림길 '상점' 화면 — 카드 줄 · 특성 줄 · 마력의 정수.
//
//  ■ ⚠ 전체화면이다 (사용자 요청, 2026-09-07)
//    전에는 글자 목록 한 줄(마력의 정수)뿐인 ChoicePopup 이었다.
//    지금은 배경 그림이 화면을 덮고 그 위에 물건이 늘어선다 —
//    "창을 하나 열었다" 가 아니라 "상점에 들렀다" 로 읽혀야 한다.
//    무대는 FacilityStage 가 짓는다 (네 시설이 같은 무대를 쓴다).
//
//  ■ 파는 것 셋
//      카드   — 3택과 같은 후보 규칙 (도감 해금이 문이다)
//      특성   — 3택과 같은 뽑기. 값이 비싸다
//      정수   — 최대 마나 +1. 살수록 비싸진다 (RunShopRule)
//
//  ■ ⚠ 재고는 화면을 여는 순간 한 번만 굴린다
//    매 프레임 다시 굴리면 살 것을 고르는 동안 물건이 바뀐다.
//    저장하지도 않는다 — 저장하면 껐다 켜서 다시 굴리기가 생긴다.
//
//  ■ ⚠ 산 물건은 지우지 않고 **끈다**
//    칸이 사라지면 줄이 밀려서, 방금 무엇을 샀는지가 화면에서 지워진다.
//    자리는 그대로 두고 흐리게 만든다.
//    ⚠ 산 것은 **배열에 기록한다** — 겉모습만 덮으면 다음 Refresh 가 지운다
//      (아래 _cardSold/_perkSold). 실제로 그랬다: SoldOut 이 덮개를 켠 직후
//      Refresh 가 Bind 로 도로 껐다 — 같은 카드를 몇 번이고 살 수 있었다.
//
//  ■ ⚠ 누르는 것은 칸이 아니라 **칸 안의 구매 버튼**이다 (사용자 지적, 2026-09-08)
//    전에는 칸 전체가 버튼이었다. 그러면 ① 어디를 눌러야 사는지가 안 보이고
//    ② 골드가 모자라 못 누르는 상태와 그냥 어두운 면이 구분되지 않는다.
//    지금은 칸 아래에 값이 적힌 버튼이 따로 있다 — 낼 값과 누를 것이 한 몸이다
//    (몬스터 상세의 품질 개선 버튼과 같은 규칙).
//
//  ■ ⚠ 클릭 콜백에 for 의 i 를 그대로 넣지 말 것
//    C# 의 for 변수는 **반복마다 새로 만들어지지 않는다.** 람다가 전부 같은
//    변수를 붙잡아, 루프가 끝난 뒤 i 는 칸 수(4)로 굳는다 → BuyCard(4) 가 되어
//    `index >= _cards.Count` 에서 조용히 return 했다. 화면에서는 "눌러도 아무
//    일이 없다" 로만 보인다. 반드시 안에서 지역 변수로 복사한다
//    (foreach 는 반복마다 새 변수라 안전하지만 for 는 캡처가 공유된다).
//
//  ⚠ 값은 런 골드다 (RunGoldRule). 영구 골드가 아니다 —
//    섞으면 "마지막 판에 몰아 지르기" 가 최적해가 된다.
// ============================================================

public class ShopPopup : PopupBase
{
    // ── 무대 ─────────────────────────────────────────────────

    [SerializeField] Image           _art;
    [SerializeField] Sprite[]        _nodeArt;
    [SerializeField] TextMeshProUGUI _titleText;
    [SerializeField] TextMeshProUGUI _flavorText;
    [SerializeField] TextMeshProUGUI _purseText;
    [SerializeField] Button          _closeBtn;

    // ── 물건 칸 ──────────────────────────────────────────────

    [Serializable]
    public class StallView
    {
        [Tooltip("칸 전체. ⚠ 이건 버튼이 아니다 — 누르는 것은 Button 이다.")]
        public GameObject      Root;

        [Tooltip("칸 안의 **구매 버튼**. 값(PriceText)이 이 안에 들어 있다.")]
        public Button          Button;

        public Image           Icon;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI DescText;

        [Tooltip("구매 버튼 안의 값 줄. 살 수 있으면 금색, 모자라면 붉은색.")]
        public TextMeshProUGUI PriceText;

        /// <summary>이미 산 칸을 덮는 판. 자리는 남기고 흐리게 만든다.</summary>
        public GameObject SoldOverlay;
    }

    [SerializeField] StallView[] _cardStalls;
    [SerializeField] StallView[] _perkStalls;

    [Tooltip("상시 판매 칸. 순서는 BoonEssence / BoonWarFund — Creator 가 같은 순서로 굽는다.")]
    [SerializeField] StallView[] _boonStalls;

    [Tooltip("카드 아이콘을 못 찾았을 때 대신 쓸 그림.")]
    [SerializeField] Sprite _fallbackIcon;

    // ── 재고 교체 ────────────────────────────────────────────

    [Header("재고 교체")]
    [SerializeField] Button          _rerollBtn;
    [SerializeField] TextMeshProUGUI _rerollPrice;

    // ── 카드 줄 감추기 (사용자 지시, 2026-09-13) ─────────────
    //
    //  살 수 있는 몬스터가 하나도 없을 때가 있다 — 덱 칸이 다 찼고 남은 카드가
    //  전부 만렙이면 RunShopRule.RollCards 가 빈 목록을 돌려준다. 그때 "몬스터"
    //  라벨과 빈 칸만 남으면 화면이 "여기 뭔가 있었는데 사라졌다" 로 읽힌다.
    //
    //  ⚠ 재고 교체 버튼은 **감추지 않고 옮긴다**
    //    카드가 없어도 특성 두 칸은 계속 굴릴 수 있어야 한다 — 후반에 남는
    //    골드가 갈 곳이 그것뿐인 상황이 바로 이 상황이다. 버튼이 카드 줄에
    //    앵커돼 있으므로 자리만 특성 줄로 올린다.
    //  ⚠ 두 자리의 정본은 Creator 다 (ShopPopupCreator) — 좌표를 런타임에
    //    적으면 칸 크기를 바꾼 날 여기만 옛 자리에 남는다.

    [Tooltip("\"몬스터\" 줄 이름표. 살 수 있는 카드가 없으면 카드 칸과 함께 감춘다.")]
    [SerializeField] TextMeshProUGUI _cardLabel;

    [Tooltip("카드 줄이 있을 때 재고 교체 버튼의 자리. Creator 가 굽는다.")]
    [SerializeField] Vector2 _rerollPosWithCards;

    [Tooltip("카드 줄이 감춰졌을 때 재고 교체 버튼이 옮겨 갈 자리 (특성 줄 오른쪽 끝).")]
    [SerializeField] Vector2 _rerollPosNoCards;

    /// <summary>Creator 가 굽는 칸 수. 재고보다 넉넉해야 한다.</summary>
    public const int MaxCardStalls = 4;
    public const int MaxPerkStalls = 2;

    // ── 상시 판매 (2026-09-13) ───────────────────────────────
    //
    //  ⚠ 배열 순서가 곧 화면의 위아래다. Creator 와 여기가 같은 상수를 본다 —
    //    순서를 바꾸면 "정수를 샀는데 성벽이 오르는" 상태가 된다.

    public const int BoonEssence = 0;
    public const int BoonWarFund = 1;

    /// <summary>상시 판매 칸 수. Creator 가 이 수만큼 굽는다.</summary>
    public const int BoonStalls = 2;

    // ── 상태 ─────────────────────────────────────────────────

    readonly List<CardRewardOption> _cards = new(MaxCardStalls);
    readonly List<RunPerk>          _perks = new(MaxPerkStalls);

    /// <summary>이미 산 칸. ⚠ 덮개가 아니라 <b>여기</b>가 정본이다 (머리 주석 참고).</summary>
    readonly bool[] _cardSold = new bool[MaxCardStalls];
    readonly bool[] _perkSold = new bool[MaxPerkStalls];

    int    _stageNumber;
    Action _onDone;

    /// <summary>
    /// 이 상점에서 재고를 몇 번 굴렸는가. 다음 리롤 값이 이 수로 오른다.
    ///
    /// ⚠ 저장하지 않는다 — 상점을 나가면 재고 자체가 사라진다 (RunShopRule 주석).
    /// </summary>
    int _rerolls;

    // ── 열기 ─────────────────────────────────────────────────

    public ShopPopup Setup(int stageNumber, Action onDone)
    {
        _stageNumber = stageNumber;
        _onDone      = onDone;

        // ⚠ 재고는 열 때 한 번 굴린다 (파일 머리 주석). 그 뒤로는 리롤만 다시 굴린다.
        _rerolls = 0;
        RollStock();

        _titleText.text  = "상점";
        _flavorText.text = RunNodeFlavor.Of(RunNodeKind.Shop, stageNumber);

        int art = (int)RunNodeKind.Shop;
        if (_nodeArt != null && art >= 0 && art < _nodeArt.Length)
            _art.sprite = _nodeArt[art];

        Refresh();
        return this;
    }

    protected override void Awake()
    {
        base.Awake();
        _closeBtn.onClick.AddListener(Leave);
        _rerollBtn?.onClick.AddListener(Reroll);
    }

    // ── 재고 굴리기 ──────────────────────────────────────────

    /// <summary>
    /// 카드·특성을 새로 뽑고 '이미 삼' 표식을 지운다.
    ///
    /// ⚠ 여는 쪽(Setup)과 리롤이 <b>같은 함수</b>를 쓴다 — 갈라지면
    ///   "리롤한 재고만 규칙이 다른" 상태가 된다.
    /// ⚠ 마력의 정수는 굴리지 않는다 — 재고가 아니라 상시 판매품이고,
    ///   값이 '산 횟수' 로 오른다 (RunShopRule 주석).
    /// </summary>
    void RollStock()
    {
        var user     = UserDataManager.Instance;
        var deck     = user.Get<SummonDeckData>();
        var perkData = user.Get<RunPerkData>();

        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;

        _cards.Clear();

        if (summoner == null)
        {
            // ⚠ 조용히 넘어가지 않는다 — 화면에는 "몬스터 칸이 통째로 없다" 로만 보인다
            Debug.LogError("[ShopPopup] 소환사가 없습니다 — 몬스터 카드를 뽑지 못했습니다. " +
                           "특성·정수만 팝니다.");
        }
        else
        {
            _cards.AddRange(RunShopRule.RollCards(summoner, deck));
        }

        _perks.Clear();
        _perks.AddRange(RunShopRule.RollPerks(perkData));

        Array.Clear(_cardSold, 0, _cardSold.Length);
        Array.Clear(_perkSold, 0, _perkSold.Length);
    }

    /// <summary>
    /// 값을 치르고 재고를 갈아 치운다.
    ///
    /// ⚠ 돈을 먼저 낸다 — 굴리고 나서 내면 잔액이 모자란 순간이 공짜가 된다.
    /// ⚠ 산 것을 되돌리지 않는다 — 이미 손에 든 카드·특성은 그대로다.
    ///   지워지는 것은 <b>진열</b>뿐이다.
    /// </summary>
    void Reroll()
    {
        if (!RunGoldRule.TrySpend(RunShopRule.RerollPrice(_stageNumber, _rerolls))) return;

        _rerolls++;
        RollStock();
        Refresh();
    }

    // ── 그리기 ───────────────────────────────────────────────

    void Refresh()
    {
        int gold = RunGoldRule.Current;
        _purseText.text = $"{gold:N0}";

        int cardPrice = RunShopRule.CardPrice(_stageNumber);
        int perkPrice = RunShopRule.PerkPrice(_stageNumber,
                            UserDataManager.Instance.Get<RunPerkData>().ShopPerkBuys);

        // ── 카드 ──
        //
        //  ⚠ 살 수 있는 카드가 없으면 줄을 통째로 감춘다 (위 필드 주석 참고).
        //    칸은 아래 루프가 has=false 로 끄고, 여기서는 이름표와 재고 교체
        //    버튼의 자리를 함께 옮긴다.
        bool anyCard = _cards.Count > 0;

        _cardLabel.gameObject.SetActive(anyCard);

        if (_rerollBtn != null)
            ((RectTransform)_rerollBtn.transform).anchoredPosition =
                anyCard ? _rerollPosWithCards : _rerollPosNoCards;

        for (int i = 0; i < _cardStalls.Length; i++)
        {
            bool has = i < _cards.Count;
            _cardStalls[i].Root.SetActive(has);
            if (!has) continue;

            // ⚠ 반드시 지역 변수로 복사한다 — for 의 i 는 람다가 공유한다 (머리 주석)
            int slot = i;

            CardRewardOption   opt = _cards[slot];
            MonsterSpeciesData sp  = CardCatalog.Current?.GetMonster(opt.Id);

            string name = sp != null ? sp.DisplayName : opt.Id;

            // 무엇이 달라지는지를 적는다 — "새 카드" 만으로는 살 이유가 안 보인다.
            string desc = opt.IsDuplicate
                        ? $"Lv.{opt.CurrentLevel} → Lv.{opt.ResultLevel}"
                        : "덱에 새로 들어온다";

            Sprite icon = sp != null ? MonsterPortraitProvider.Get(sp) : null;

            Bind(_cardStalls[slot], icon, name, desc, cardPrice, gold,
                 _cardSold[slot], () => BuyCard(slot));

            // 친화 종족 표식 — 칸이 재사용되니 매번 켜고 끈다 (AffinityTagUI).
            AffinityTagUI.Set(_cardStalls[slot].Icon, sp);
        }

        // ── 특성 ──
        for (int i = 0; i < _perkStalls.Length; i++)
        {
            bool has = i < _perks.Count;
            _perkStalls[i].Root.SetActive(has);
            if (!has) continue;

            int     slot = i;                      // ⚠ 위와 같은 이유
            RunPerk perk = _perks[slot];

            Bind(_perkStalls[slot],
                 SpriteManager.Instance?.Get(perk.IconKey()),
                 perk.ToKorean(), perk.Describe(), perkPrice, gold,
                 _perkSold[slot], () => BuyPerk(slot));
        }

        // ── 상시 판매 셋 — 마나 · 공격 · 방어 ──
        //
        //  ⚠ 팔려 나가지 않는다 (sold: false) — 값이 오를 뿐 계속 살 수 있다.
        //    후반에 골드가 남는 것을 받아 내는 자리라, **끝이 없는 것**이 요점이다
        //    (RunShopRule 의 '상시 판매' 주석).
        //  ⚠ 산 횟수는 이름 옆에 붙인다 — 칸이 납작해 설명 줄에 넣으면 두 줄이 된다.
        //  ⚠ 정수만 산 횟수를 따로 세지 않는다 — MaxManaBonus 가 곧 그 수다
        //    (정수 하나가 +1 이므로). 세이브에 수를 하나 더 두면 둘이 갈린다.
        var boon = UserDataManager.Instance.Get<RunBoonData>();

        Bind(_boonStalls[BoonEssence],
             SpriteManager.Instance?.Get(RunShopRule.EssenceIconKey),
             Stacked("마력의 정수", boon.MaxManaBonus),
             $"최대 마나 +{RunShopRule.EssenceManaAmount}",
             RunShopRule.EssencePrice(_stageNumber, boon.MaxManaBonus),
             gold, false, BuyEssence);

        // ⚠ 전쟁 자금만 스테이지를 안 받는다 — 값이 **산 횟수**로만 오른다
        //   (RunShopRule 의 '전쟁 자금' 주석). 스테이지를 넘기며 비싸지지 않는다.
        Bind(_boonStalls[BoonWarFund],
             SpriteManager.Instance?.Get(RunShopRule.WarFundIconKey),
             Stacked("전쟁 자금", boon.WarFundStacks),
             $"전 몬스터 공·체 +{RunShopRule.WarFundStatBonus * 100f:0.#}%",
             RunShopRule.WarFundPrice(boon.WarFundStacks),
             gold, false, BuyWarFund);

        RefreshReroll(gold);
    }

    /// <summary>"전쟁 자금  ×3" — 지금까지 몇 개 샀는지를 이름 옆에 붙인다. 0 이면 안 붙인다.</summary>
    static string Stacked(string name, int stacks)
        => stacks > 0 ? $"{name}  <color=#8FA0C0>×{stacks}</color>" : name;

    /// <summary>
    /// 재고 교체 버튼 — 값과 잠김 상태.
    ///
    /// ⚠ 다른 칸과 같은 규칙으로 그린다 (2026-09-08 확정) — 모자라면 값이 붉고
    ///   버튼이 잠긴다. 여기만 다르게 그리면 왜 안 눌리는지가 화면마다 갈린다.
    /// </summary>
    void RefreshReroll(int gold)
    {
        if (_rerollBtn == null) return;   // Inspector 선택 연결 (코딩 규칙 예외)

        int  cost   = RunShopRule.RerollPrice(_stageNumber, _rerolls);
        bool afford = gold >= cost;

        _rerollPrice.text  = $"{cost:N0}";
        _rerollPrice.color = afford ? FacilityColors.Gold : FacilityColors.Short;

        _rerollBtn.interactable = afford;
    }

    /// <summary>
    /// 칸 하나를 채운다.
    ///
    /// ⚠ 세 상태를 **전부 다르게** 그린다 (사용자 지적, 2026-09-08)
    ///   셋이 비슷하면 화면이 "왜 안 눌리는지" 를 말하지 못한다.
    ///     살 수 있음 — 금색 값, 눌리는 버튼
    ///     모자람     — 붉은 값 + "골드 부족", 버튼이 잠긴다
    ///     이미 삼    — 칸 전체에 덮개 + "구입함"
    /// </summary>
    void Bind(StallView v, Sprite icon, string name, string desc,
              int price, int gold, bool sold, Action onBuy)
    {
        v.Root.SetActive(true);

        v.Icon.sprite  = icon != null ? icon : _fallbackIcon;
        v.Icon.enabled = v.Icon.sprite != null;

        v.NameText.text = name;
        v.DescText.text = desc;

        bool afford = gold >= price;

        // ⚠ 값은 **버튼 안**에 적는다 (UI 규칙 7 과 같은 이유)
        //   낼 값과 누를 것이 떨어져 있으면 눈이 매번 왕복한다.
        //   못 사는 것은 **붉은 숫자**가 말한다 — 이미 산 칸은 덮개가 따로 덮는다.
        // ⚠ 글자 "G" 를 붙이지 않는다 — 배지에 금화 아이콘이 이미 있다 (UI 규칙 7).
        //   못 사는 값은 **붉은 숫자**가 말한다. "부족" 두 글자는 숫자를 밀어냈다.
        v.PriceText.text  = $"{price:N0}";
        v.PriceText.color = afford ? FacilityColors.Gold : FacilityColors.Short;

        v.Button.interactable = afford && !sold;
        v.Button.onClick.RemoveAllListeners();
        v.Button.onClick.AddListener(() => onBuy());

        v.SoldOverlay.SetActive(sold);
    }

    // ── 사기 ─────────────────────────────────────────────────

    void BuyCard(int index)
    {
        if (index >= _cards.Count) return;

        if (!RunGoldRule.TrySpend(RunShopRule.CardPrice(_stageNumber))) return;

        CardRewardOption opt  = _cards[index];
        var              user = UserDataManager.Instance;

        user.Get<SummonDeckData>().Acquire(opt.Kind, opt.Id);

        // ⚠ 도감도 함께 연다 — 해금은 늘 부르는 쪽의 몫이다
        //   (RunBootstrap 의 카드 보상·CardEvolveUI 의 진화와 같은 규칙)
        if (opt.Kind == SummonKind.Monster)
            user.Get<MonsterCodexData>().Unlock(opt.Id, UnitGrade.Normal);

        user.RequestSave();

        _cardSold[index] = true;
        Refresh();
    }

    void BuyPerk(int index)
    {
        if (index >= _perks.Count) return;

        var perkData = UserDataManager.Instance.Get<RunPerkData>();

        if (!RunGoldRule.TrySpend(RunShopRule.PerkPrice(_stageNumber, perkData.ShopPerkBuys))) return;

        perkData.Add(_perks[index]);
        perkData.NoteShopPerkBought();   // 다음 특성 값이 오른다 (RunShopRule.PerkStepMult)
        UserDataManager.Instance.RequestSave();

        _perkSold[index] = true;
        Refresh();
    }

    void BuyEssence()
    {
        var boon = UserDataManager.Instance.Get<RunBoonData>();

        if (!RunGoldRule.TrySpend(RunShopRule.EssencePrice(_stageNumber, boon.MaxManaBonus))) return;

        boon.AddMaxMana(RunShopRule.EssenceManaAmount);
        UserDataManager.Instance.RequestSave();

        // ⚠ 정수는 끄지 않는다 — 값이 오를 뿐 계속 살 수 있다.
        Refresh();
    }

    /// <summary>
    /// 전쟁 자금 — 전 몬스터 공/체가 영구히(런 안에서) 오른다.
    ///
    /// ⚠ 스탯을 여기서 건드리지 않는다 — 스택만 쌓고, 실제로 곱하는 곳은
    ///   MonsterStatComposer ⑥-c 하나다. 이미 필드에 서 있는 몬스터는 안 바뀐다
    ///   (스탯은 소환되는 순간 굳는다 — 도감 잠금과 같은 이유).
    /// </summary>
    void BuyWarFund()
    {
        var boon = UserDataManager.Instance.Get<RunBoonData>();

        if (!RunGoldRule.TrySpend(RunShopRule.WarFundPrice(boon.WarFundStacks))) return;

        boon.AddWarFund();
        UserDataManager.Instance.RequestSave();

        Refresh();
    }

    // ── 나가기 ───────────────────────────────────────────────

    void Leave()
    {
        Action done = _onDone;
        _onDone = null;
        Close();
        done?.Invoke();
    }
}

/// <summary>시설 화면이 함께 쓰는 글자색.</summary>
public static class FacilityColors
{
    public static readonly Color Gold  = new(1f, 0.86f, 0.42f);
    public static readonly Color Short = new(1f, 0.48f, 0.45f);
    public static readonly Color Sub   = new(0.74f, 0.79f, 0.92f);
}
