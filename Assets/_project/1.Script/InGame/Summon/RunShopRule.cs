using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  RunShopRule.cs
//  상점의 **재고와 값**을 정하는 단 한 곳.
//
//  ■ 상점은 "조금씩 두꺼워지는" 자리다 (RunNodeFlow 머리 주석과 같은 규칙)
//    빌드를 사는 자리가 아니다. 여기서 판을 뒤집을 물건을 팔면 카드 3택과
//    보스 보상이 전부 곁다리가 된다. 그래서
//      · 카드는 **이미 열린 것**만 (도감 규칙을 그대로 따른다)
//      · 특성은 **한 판에 두 개까지**, 값이 비싸다
//      · 마력의 정수는 사면 살수록 비싸진다
//
//  ■ ⚠ 재고는 판마다 새로 굴린다. 저장하지 않는다
//    저장하면 "마음에 안 들면 껐다 켜서 다시 굴리기" 가 생긴다. 다만
//    **화면을 여는 동안에는 고정**이라, 팝업이 재고를 들고 있는다.
//
//  ■ ⚠ 값은 런 골드다 (RunGoldData). 영구 골드가 아니다
//    영구 골드는 품질 개선의 것이다 — 섞으면 "마지막 판에 몰아 지르기" 가
//    최적해가 된다 (CLAUDE.md 골드 두 갈래 항목).
// ============================================================

public static class RunShopRule
{
    // ── 재고 수 ──────────────────────────────────────────────

    /// <summary>한 번에 늘어놓는 몬스터 카드 수.</summary>
    public const int CardStock = 4;

    /// <summary>한 번에 늘어놓는 특성 수.</summary>
    public const int PerkStock = 2;

    // ── 값 ───────────────────────────────────────────────────
    //
    //  ⚠ 값은 전부 RunGoldRule.Price(배수, 스테이지) 다 (2026-09-10)
    //    한때 CardBasePrice 45 + 스테이지당 8 이었다. 수입은 스테이지당 약
    //    +50 씩 느는데 값은 +8 만 올라서, 후반에는 재고를 통째로 사고도 돈이
    //    남았다. 그 남는 돈이 갈 곳이 **마력의 정수뿐**이라 정수만 계속 샀다.
    //    배수로 적으면 수입 곡선을 고칠 때 소비처가 저절로 따라온다.

    // ⚠ 둘을 가운데로 모았다 (사용자 지시, 2026-09-10)
    //   ×1.5 와 ×3 은 폭이 두 배라, 특성이 "가끔 지르는 사치품" 이 되고 카드는
    //   "늘 사는 소모품" 이 됐다. 상점에서 무엇을 살지 저울질하려면 둘이
    //   비슷한 값이어야 한다 — 가운데(×2.25) 언저리에 놓고, 특성만 한 칸 위다.

    /// <summary>카드 한 장 — 값 단위의 배수.</summary>
    public const float CardPriceMult = 2f;

    /// <summary>특성 하나 — 카드보다 <b>조금</b> 비싸다. 수가 적고 하나하나가 세다.</summary>
    public const float PerkPriceMult = 2.5f;

    /// <summary>
    /// 상점에서 특성을 하나 살 때마다 배수가 이만큼 오른다 (사용자 지시, 2026-09-11).
    ///
    /// ⚠ 값이 스테이지만 따라가자 28스테이지 즈음 수입이 남아 특성을 쓸어 담았다.
    ///   정수와 같은 브레이크다 — 30스테이지 기준 825 → 1,240 → 1,650 → 2,065 …
    ///   ⚠ 런 단위로 센다(RunPerkData.ShopPerkBuys). 방문마다 되돌리면 상점을
    ///     만날 때마다 첫 값으로 두 개씩 사는 것이 그대로 남는다.
    /// </summary>
    public const float PerkStepMult = 1.25f;

    /// <summary>마력의 정수 첫 값 — 값 단위의 배수.</summary>
    public const float EssenceBaseMult = 2f;

    /// <summary>
    /// 정수를 하나 살 때마다 배수가 이만큼 오른다.
    ///
    /// ⚠ 스테이지와 <b>산 횟수</b> 둘 다에 걸린다 (2026-09-10)
    ///   예전에는 +30 골드씩만 올랐다. 후반 한 판 수입이 1,700 이라 그 정도는
    ///   브레이크가 아니었다 — 상점을 만날 때마다 정수를 쓸어 담았다.
    ///   30스테이지 기준 660 → 907 → 1,155 … 으로 오른다.
    ///
    /// ⚠ 1.5 → 0.75 로 반을 깎았다 (사용자 지적, 2026-09-15)
    ///   1.5 일 때는 세 번째부터 카드·특성 두 장 값이라, 최대 마나 +1 이
    ///   다른 선택지에 비해 늘 손해였다. 산 횟수 브레이크는 남긴다.
    /// </summary>
    public const float EssenceStepMult = 0.75f;

    /// <summary>정수 하나가 올려 주는 최대 마나.</summary>
    public const int EssenceManaAmount = 1;

    // ── 상시 판매 '전쟁 자금' — 재고가 마르지 않는 자리 (사용자 확정, 2026-09-13) ──
    //
    //  ■ 왜 있나
    //    후반에 골드가 남는 것은 값이 싸서가 아니라 **살 것이 동나서**다.
    //    카드 재고는 4장인데 덱은 8칸이 상한이고(만렙 카드는 후보에서 빠진다),
    //    특성도 30종으로 유한하다. 무한 소비처가 리롤과 정수뿐이라 후반
    //    상점이 "정수 자판기" 가 됐다. 끝이 없는 자리를 하나 더 둔다.
    //
    //  ■ ⚠ 값이 **스테이지를 타지 않는다** (사용자 지시, 2026-09-13)
    //    다른 소비처는 전부 Price(배수, 스테이지)라 수입 곡선을 따라 오른다.
    //    이것만 예외로 **산 횟수만** 본다 — 스테이지까지 곱하면 같은 한 스택을
    //    사는데 늦게 살수록 비싸져서, "얼마나 쌓았나" 를 "언제 샀나" 가 덮는다.
    //    쌓을수록 비싸지는 축 하나만 남긴다.
    //    ⚠ 그래서 값을 골드로 **직접** 적는다. 대신 RunGoldRule.Flat 을 지나
    //      유물 '안목' 할인과 5 단위 반올림은 다른 소비처와 똑같이 먹는다.
    //
    //  ⚠ 산 횟수는 **런 단위**로 저장한다 (RunBoonData) — 방문마다 되돌리면
    //    상점을 만날 때마다 첫 값으로 다시 쓸어 담는 것이 그대로 남는다.

    /// <summary>전쟁 자금 첫 값(골드).</summary>
    public const int WarFundBasePrice = 300;

    /// <summary>
    /// 하나 살 때마다 오르는 값(골드) — 300 → 550 → 800 → 1,050 …
    ///
    /// 열 번째가 2,550 이고 열 개를 다 사면 14,250 이다. 30스테이지 한 판 수입이
    /// 약 1,700 이니 열 판을 오롯이 부어야 +20% 가 된다 — 살 수는 있지만
    /// 그것만 할 수는 없다.
    /// </summary>
    public const int WarFundStepPrice = 250;

    /// <summary>
    /// 전쟁 자금 하나가 올리는 <b>전 몬스터</b> 공격력·체력 비율.
    ///
    /// ⚠ 곱하는 곳은 MonsterStatComposer ⑥-c <b>한 곳뿐</b>이다.
    ///   여기 수치를 올릴 때는 융합이 주는 몫(CardEvolution.FuseInheritShare)과
    ///   견줄 것 — 전군에 붙는 값이 카드 한 장을 먹여 얻는 값을 넘으면 융합이 죽는다.
    /// </summary>
    public const float WarFundStatBonus = 0.02f;

    /// <summary>전쟁 자금 아이콘 키. 굽는 곳은 ItemIconGenerator (Icons/Items).</summary>
    public const string WarFundIconKey = "item_war_fund";

    /// <summary>
    /// 전쟁 자금이 얹은 전 몬스터 공/체 배율. 산 적이 없으면 1 이다.
    ///
    /// ⚠ 읽는 곳은 MonsterStatComposer ⑥-c 하나다 — 다른 데서 또 곱하지 말 것.
    /// </summary>
    public static float WarFundStatMult
    {
        get
        {
            var boon = UserDataManager.Instance?.Get<RunBoonData>();
            return boon == null ? 1f : 1f + WarFundStatBonus * boon.WarFundStacks;
        }
    }

    // ── 상시 판매 '소집의 북' — 배출 간격 (사용자 지시, 2026-09-15) ──
    //
    //  ■ 전쟁 자금과 같은 틀이다 — 끝이 없고, 값은 **산 횟수만** 본다.
    //    전쟁 자금이 "얼마나 세게" 라면 이건 "얼마나 빨리" 줄을 세우는가다.
    //
    //  ⚠ 비율을 **곱한다** (0.95^n). 빼면(1 − 0.05n) 스무 번째에 간격이 0 이 되어
    //    대기열이 한 프레임에 쏟아진다. 곱이면 끝없이 사도 0 에 닿지 않는다.
    //  ⚠ 곱하는 곳은 RunPerkRule.DrainMultiplierFor 하나다 (특성·유물과 같은 관문).

    /// <summary>소집의 북 첫 값(골드).</summary>
    public const int DrumBasePrice = 300;

    /// <summary>하나 살 때마다 오르는 값(골드) — 300 → 550 → 800 …</summary>
    public const int DrumStepPrice = 250;

    /// <summary>하나당 배출 간격에 곱하는 값 — 0.95 = 간격 −5%.</summary>
    public const float DrumIntervalStep = 0.95f;

    /// <summary>소집의 북 아이콘 키. 굽는 곳은 ItemIconGenerator (Icons/Items).</summary>
    public const string DrumIconKey = "item_war_drum";

    public static int DrumPrice(int alreadyBought)
        => RunGoldRule.Flat(DrumBasePrice + DrumStepPrice * Mathf.Max(0, alreadyBought));

    // ── 마나 회복 포션 (사용자 지시, 2026-09-16) ──────────────
    //
    //  ■ 지금 마나를 최대 마나의 PotionRestoreRatio 만큼 채운다 — 그릇은 안 키운다 (그건 정수의 몫)
    //  ■ **방문당 한 병** (사용자 지시, 2026-09-16) — 사면 칸이 "품절" 로 덮인다 (ShopPopup)
    //    마나가 이 게임의 핵심 제약이다. 후반에 남는 골드로 한 번에 몇 병씩 들이켜
    //    다음 판을 가득 채우는 것이 막히지 않으면, 마나 그릇·회복 설계가 통째로 무의미해진다.
    //    ⚠ 한때 산 횟수로 값을 올렸다 — 한 병만 팔므로 그 몫은 걷었다. 방문 수는 저장하지 않는다.
    //  ⚠ 가득 차 있으면 못 산다 (ShopPopup) — 돈만 사라지는 구매를 만들지 않는다.
    //  ⚠ 채우는 곳은 RunPerkRule.RestoreMana 하나다 (정수 내림 · 그릇 상한이 거기 있다).

    /// <summary>한 병이 채우는 몫 — 최대 마나 대비.</summary>
    public const float PotionRestoreRatio = 0.3f;

    /// <summary>한 병 값 배수 (스테이지 값 단위 × 이 값).</summary>
    public const float PotionPriceMult = 1.5f;

    public const string PotionIconKey = "item_mana_potion";

    /// <summary>한 병이 채우는 마나 — 정수, 최소 1.</summary>
    public static int PotionAmount(float maxMana)
        => Mathf.Max(1, Mathf.FloorToInt(maxMana * PotionRestoreRatio));

    public static int PotionPrice(int stageNumber)
        => RunGoldRule.Price(PotionPriceMult, stageNumber);

    /// <summary>소집의 북이 얹은 배출 간격 배율. 산 적이 없으면 1 이다.</summary>
    public static float DrumIntervalMult
    {
        get
        {
            var boon = UserDataManager.Instance?.Get<RunBoonData>();
            return boon == null ? 1f : Mathf.Pow(DrumIntervalStep, boon.DrumStacks);
        }
    }

    // ── 재고 다시 굴리기 ─────────────────────────────────────
    //
    //  ■ 왜 있나 (사용자 지시, 2026-09-10)
    //    상점의 재고는 카드 4 + 특성 2 로 <b>유한하다</b>. 다 사고 나면 남는
    //    돈이 갈 곳이 마력의 정수뿐이라, 후반 상점이 "정수 자판기" 가 됐다.
    //    리롤은 **끝이 없는** 소비처다 — 남는 돈이 언제나 갈 곳이 생긴다.
    //
    //  ■ ⚠ 굴린 횟수는 이 상점 방문 안에서만 센다. 저장하지 않는다
    //    저장하면 "껐다 켜서 리롤 값 되돌리기" 가 생긴다. 상점을 나가면
    //    재고 자체가 사라지므로(RollCards 는 열 때 한 번) 값도 함께 잊는다.
    //
    //  ■ ⚠ 정수는 다시 굴리지 않는다
    //    그건 재고가 아니라 상시 판매품이고, 값이 '산 횟수' 로 오른다.
    //    리롤로 그 값을 되돌릴 수 있으면 브레이크가 통째로 풀린다.

    /// <summary>첫 리롤 값 — 값 단위의 배수. 카드 한 장보다 확실히 싸다.</summary>
    public const float RerollBaseMult = 0.75f;

    /// <summary>한 번 굴릴 때마다 배수가 이만큼 오른다. 무한 리롤을 막는 유일한 장치다.</summary>
    public const float RerollStepMult = 0.75f;

    /// <summary>
    /// 이 상점에서 <paramref name="alreadyRerolled"/> 번 굴린 뒤의 다음 리롤 값.
    ///
    /// 30스테이지 기준 250 → 495 → 745 … 로 오른다. 카드 한 장(660)보다
    /// 싸게 시작하지만 세 번째부터는 카드를 사는 편이 낫다 — 그 교차점이
    /// "이쯤에서 그만" 을 스스로 정하게 만든다.
    /// </summary>
    public static int RerollPrice(int stageNumber, int alreadyRerolled)
        => RunGoldRule.Price(RerollBaseMult + RerollStepMult * Mathf.Max(0, alreadyRerolled),
                             stageNumber);

    /// <summary>
    /// 마력의 정수 아이콘 키 (SpriteManager 조회용).
    ///
    /// ⚠ 그림은 Icons/Items/ 에 있다 — 아이템 아틀라스가 이미 그 폴더를 물고
    ///   있어서 SpriteManagerCreator 에 줄을 더할 필요가 없다.
    ///   굽는 곳은 ItemIconGenerator 다.
    /// </summary>
    public const string EssenceIconKey = "item_mana_essence";

    // ── 계산 ─────────────────────────────────────────────────

    public static int CardPrice(int stageNumber)
        => RunGoldRule.Price(CardPriceMult, stageNumber);

    /// <summary>이번 런에 상점에서 <paramref name="alreadyBought"/> 개를 산 뒤의 특성 값.</summary>
    public static int PerkPrice(int stageNumber, int alreadyBought)
        => RunGoldRule.Price(PerkPriceMult + PerkStepMult * Mathf.Max(0, alreadyBought),
                             stageNumber);

    /// <summary>
    /// 마력의 정수 값. <b>스테이지와 이미 산 횟수 둘 다로 오른다.</b>
    ///
    /// ⚠ 둘 다여야 한다 — 산 횟수만 보면 후반의 큰 수입 앞에서 무의미해지고,
    ///   스테이지만 보면 "한 상점에서 몰아 사기" 가 그대로 남는다.
    /// </summary>
    public static int EssencePrice(int stageNumber, int alreadyBought)
        => RunGoldRule.Price(EssenceBaseMult + EssenceStepMult * Mathf.Max(0, alreadyBought),
                             stageNumber);

    /// <summary>
    /// 전쟁 자금 값. <b>스테이지를 보지 않는다</b> — 산 횟수만 본다 (위 주석).
    /// ⚠ 스테이지를 인자로 되돌리지 말 것. 그러면 이 물건을 만든 이유가 사라진다.
    /// </summary>
    public static int WarFundPrice(int alreadyBought)
        => RunGoldRule.Flat(WarFundBasePrice + WarFundStepPrice * Mathf.Max(0, alreadyBought));

    // ── 재고 굴리기 ──────────────────────────────────────────

    /// <summary>
    /// 이 상점이 팔 몬스터 카드를 고른다.
    ///
    /// ⚠ 후보 규칙은 카드 3택과 **같은 것을 쓴다** (CardRewardPicker)
    ///   상점만 다른 규칙을 두면 "3택에는 안 나오는데 상점에는 나오는 카드" 가
    ///   생겨, 도감 해금이 무슨 뜻인지가 화면마다 갈린다.
    /// </summary>
    public static List<CardRewardOption> RollCards(SummonerData summoner, SummonDeckData deck)
    {
        List<CardRewardOption> all = CardRewardPicker.Pick(summoner, deck, CardStock);

        // 시너지 강화는 상점에서 팔지 않는다 — 런에 한 장뿐인 물건이라
        // 값을 매기면 "돈으로 사는 유일한 한 장" 이 되어 무게가 어긋난다.
        //
        // ⚠ 진화 후보도 뺀다 (2026-09-12)
        //   상점은 "사서 **칸에 넣는**" 곳이고, 진화는 **있는 칸을 바꾸는** 것이라
        //   문법이 다르다. 무엇보다 ShopPopup.BuyCard 는 Acquire 로 새 칸에 꽂으므로,
        //   그대로 두면 베이스를 남긴 채 진화체가 새 카드로 들어온다 —
        //   "진화체가 그냥 새 카드로 나오는 것" 을 없애려고 만든 규칙과 정반대가 된다.
        //
        // ⚠ **만렙 카드도 뺀다** (사용자 지시, 2026-09-13)
        //   3택에서 만렙 카드를 고르면 RunBootstrap.TryOpenEvolve 가 진화·융합
        //   갈림길을 연다. 상점의 BuyCard 에는 그 경로가 없다 — Acquire 가
        //   장수만 올리고 끝나므로 **골드만 나가고 아무 일도 일어나지 않는다.**
        //   "아무 일도 못 하는 카드는 내지 않는다"(CardRewardPicker.BuildPool)를
        //   상점에도 그대로 적용한다.
        //   ⚠ 진화·융합을 상점에서 열어 주는 쪽으로 고치지 말 것 — 상점 팝업 위에
        //     팝업이 겹치고, 창이 닫힌 뒤의 흐름(AfterCardReward)이 상점 밖에 있다.
        all.RemoveAll(o => o.IsSynergyBoost || o.IsEvolve || o.IsMaxed);
        return all;
    }

    /// <summary>이 상점이 팔 특성을 고른다. 3택과 같은 뽑기를 쓴다.</summary>
    public static List<RunPerk> RollPerks(RunPerkData data)
        => RunPerkPicker.Pick(data, PerkStock);
}
