using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  RunEvent.cs
//  갈림길 '이벤트' 의 **정본** — 표 · 값 · 판정 · 적용이 전부 여기 하나에 있다.
//
//  ■ 이벤트만의 축은 **지불 수단**이다 (2026-09-08 설계)
//    시설 넷은 전부 골드로 산다 — 야영지 증축 90G · 강화소 70G · 상점 45G~.
//    이벤트는 **골드로 사지 않는다.** 그래서 "지갑이 비어도 살 수 있는 자리" 이고,
//    대신 내는 것이 목숨이거나 손에 든 카드다.
//
//    ⚠ 이벤트가 골드로 사기 시작하면 시설과 같은 물건이 된다
//      그러면 갈림길에 이벤트를 넣을 이유가 "가끔 싸게 파는 상점" 뿐이다.
//
//    대가는 셋이다 (RunEventCost) — 전부 골드가 아니다.
//      Core       마왕성 체력 N. 값이 정해져 있다
//      HalfCore   지금 체력의 절반. **상태를 보고 정하는 대가**다
//      LowestCard 덱에서 가장 약한 카드 한 장
//
//    ⚠ 상태 의존 대가(HalfCore)는 화면에 **지금 얼마인지**를 적는다
//      "절반" 이라고만 적으면 고르기 전에 계산을 시킨다. Describe 가
//      그 자리에서 실제 숫자를 뽑는다 — 그게 이 갈래의 재미다.
//
//  ■ 무작위는 **어느 이벤트를 만나는가** 뿐이다
//    고른 뒤의 결과는 확정이다. 이 프로젝트가 품질 개선을 "확정이다.
//    확률이 아니다" 로 정한 것과 같은 규칙 (CLAUDE.md 몬스터 도감 항목).
//    ⚠ 원작 EventChoice.SuccessRate 를 쓰지 않는 이유가 이것이다 —
//      결과까지 확률이면 "골랐는데 꽝" 이 되어 고른 무게가 사라진다.
//
//  ■ 갈래는 언제나 셋이다 — **보상 · 골드 · 회복** (사용자 확정, 2026-09-08)
//      보상 — 마왕성 체력을 내고 **구조적인 것**을 받는다 (덱·시너지·성벽)
//      골드 — 공짜. 이번 판의 돈
//      회복 — 공짜. 마왕성 체력을 **조금** 되찾는다 (야영지의 3분의 1쯤)
//    자리와 순서가 모든 이벤트에서 같아야 두 번째부터는 읽지 않고도
//    무엇을 묻는지 안다. 다른 것은 보상 갈래가 주는 물건뿐이고,
//    결정은 거기서 난다.
//
//    ■ 셋이 서로 다른 것을 묻는다
//      보상은 "지금 체력을 걸어 나중을 살 것인가",
//      골드와 회복은 "돈이 급한가 목숨이 급한가" 다.
//      공짜 갈래가 둘이라 체력이 바닥나도 이벤트 칸이 손해가 아니다.
//
//    ⚠ **야영지는 많이, 이벤트는 조금** (사용자 확정, 2026-09-08)
//      야영지 8 · 이벤트 3 이다. 야영지는 회복이 존재 이유인 자리이므로
//      폭이 뚜렷해야 한다 — 두 칸 차이였을 때는 둘 다 "조금 회복" 으로 읽혀
//      야영지를 고를 이유가 "증축이 있으니까" 뿐이었다.
//      그래서 값을 직접 적지 않고 야영지에서 **비율로** 만든다
//      (RunEventRule.HealAmount) — 야영지를 올리면 이쪽도 따라 오르되
//      구조적으로 앞지르지 못한다.
//
//  ■ ⚠ 마나는 건드리지 않는다 (GameDesign.md 5.0절)
//    "패시브 재생·이벤트 보상·3택지 보상 어디에도 마나 회복 옵션을 넣지
//    않는다." 최대 마나를 늘리는 것은 상점의 '마력의 정수' 하나로 남긴다 —
//    상점만의 물건이 있어야 갈림길에서 상점을 고를 이유가 된다.
//
//  ■ ⚠ 원작 EventData / EventRewardHandler / EventPopup 은 쓰지 않는다
//    EventRewardType 13종이 전부 원작 축이다 — 특성(TraitType) · 병사 수 ·
//    어빌리티 선택 · 용병 고용 · 장수 HP%. 이 게임에는 병사도 어빌리티도
//    용병도 없다. 남겨 둔 이유는 원작 화면이 아직 참조하기 때문이다.
//
//  ■ ⚠ 화면을 새로 만들지 않는다
//    FacilityPopup(시설 화면)을 그대로 쓴다 — 배경 그림 + 이야기 한 줄 +
//    선택지 줄이 이미 이벤트가 필요한 전부다. PopupType 을 늘리지 않는다.
//    선택 → 결과 두 단계인 것은 강화소가 이미 하고 있는 짜임이다.
//
//  ■ ⚠ 만난 이벤트는 그 런에 다시 나오지 않는다
//    SummonRunData 가 기억한다 (런 스코프 · 이어하기에도 남는다).
//    후보가 다 떨어지면 그때부터 다시 돈다 — 갈림길이 비면 안 되기 때문이다.
// ============================================================

/// <summary>
/// 이벤트 종류. ⚠ <b>뒤에만</b> 추가한다 — 만난 기록을 번호로 저장한다.
/// 중간에 끼우면 옛 세이브의 "만났다" 가 다른 이벤트로 옮겨 간다.
/// </summary>
public enum RunEventId
{
    MinerShaft   = 0,   // 광부의 갱도
    LostLibrary  = 1,   // 잊힌 서고
    WanderingSoul= 2,   // 떠도는 혼
    StrayMonster = 3,   // 길 잃은 몬스터
    OfferingMark = 4,   // 제물의 흔적
    BrokenCircle = 5,   // 깨진 소환진
    SupplyCart   = 6,   // 버려진 보급 수레
    RoamingSmith = 7,   // 떠돌이 대장장이

    // ── 무거운 판 (체력 −15) ─────────────────────────────────
    SealedStaff  = 8,   // 봉인된 지팡이
    HeroGraves   = 9,   // 용사들의 무덤
    BloodMoon    = 10,  // 핏빛 달
    BrokenPens   = 11,  // 버려진 사육장

    // ── 대가가 체력이 아닌 판 ────────────────────────────────
    BrokenNest   = 12,  // 버려진 둥지    — 빈 칸 셋을 요구한다
    HungryIdol   = 13,  // 굶주린 우상    — 카드 한 장을 먹는다
    InvertedGlass= 14,  // 뒤집힌 모래시계 — 지금 체력의 절반
}

/// <summary>
/// 갈래가 주는 것. <b>여기 없는 보상은 이벤트가 줄 수 없다.</b>
///
/// ⚠ 새 종류를 만들면 <see cref="RunEventRule.Describe"/> · <see cref="RunEventRule.Available"/> ·
///   <see cref="RunEventRule.Apply"/> 셋을 **전부** 채울 것. 하나만 빠지면 조용히 실패한다 —
///   설명이 빈 줄이 되거나, 못 하는 갈래가 눌리거나, 눌러도 아무 일이 없다.
/// </summary>
public enum RunEventGain
{
    /// <summary>런 골드. Amount 는 <see cref="RunEventRule.GoldUnit"/> 의 배수다.</summary>
    Gold = 0,

    /// <summary>런 특성 1개 (무작위). 카드 3택·상점과 같은 뽑기를 쓴다.</summary>
    Perk = 1,

    /// <summary>덱에서 <b>레벨이 가장 낮은</b> 몬스터 카드의 레벨 +Amount.</summary>
    CardLevel = 2,

    /// <summary>
    /// 새 카드 <b>Amount 장</b>. 후보 규칙은 카드 3택과 같다 (도감 해금이 문이다).
    ///
    /// ⚠ 빈 칸이 Amount 개 있어야 고를 수 있다 (Available) — 모자란 채로 주면
    ///   넘치는 장이 조용히 사라지는데 대가는 이미 치른 뒤다.
    /// </summary>
    NewCard = 3,

    /// <summary>덱에 <b>가장 많은 표식</b>의 시너지 카운트 +Amount.</summary>
    SynergyCount = 4,

    /// <summary>덱에서 <b>가장 비싼</b> 카드의 소환 마릿수 +Amount.</summary>
    ExtraSummons = 5,

    /// <summary>덱에서 <b>가장 비싼</b> 카드의 소환 비용 −Amount.</summary>
    ManaCut = 6,

    /// <summary>마왕성 <b>최대</b> 체력 +Amount. 늘어난 만큼 즉시 채워진다.</summary>
    CoreMax = 7,

    /// <summary>
    /// 마왕성 체력 회복.
    ///
    /// ⚠ Amount 를 보지 않는다 — 양의 정본은 <see cref="RunEventRule.HealAmount"/> 하나다.
    ///   갈래마다 다른 값을 적으면 그중 하나가 야영지를 앞지르는 것을 못 막는다.
    /// </summary>
    CoreHeal = 8,

    /// <summary>
    /// 시그니처 스킬의 <b>스테이지당</b> 사용 횟수 +Amount (런 영구).
    ///
    /// 저장고는 RunBoonData.SignatureBonus, 읽는 곳은 SummonerSkillRule.Remaining 이다
    /// (특성 '집중' 과 같은 자리).
    /// </summary>
    SignatureUse = 9,

    /// <summary>
    /// 덱에서 <b>가장 비싼</b> 몬스터 카드의 레벨 +Amount.
    ///
    /// ⚠ <see cref="CardLevel"/>(가장 낮은 카드)과 반대다 — 둘을 합치지 말 것.
    ///   "약한 것을 먹여 강한 것을 키운다" 는 이야기가 성립하려면 대상이 반대여야 한다.
    /// </summary>
    TopCardLevel = 10,
}

/// <summary>
/// 갈래가 요구하는 대가. <b>골드는 없다</b> — 그건 시설의 것이다 (머리 주석 참고).
///
/// ⚠ 새 종류를 만들면 <see cref="RunEventRule.DescribeCost"/> ·
///   <see cref="RunEventRule.CanPay"/> · <see cref="RunEventRule.Pay"/> 셋을
///   **전부** 채울 것. 하나만 빠지면 조용히 실패한다 — 대가가 안 적히거나,
///   못 낼 것을 고르게 되거나, 공짜가 된다.
/// </summary>
public enum RunEventCost
{
    /// <summary>공짜. 골드·회복 갈래가 쓴다.</summary>
    None = 0,

    /// <summary>마왕성 체력 Amount. 값이 표에 적혀 있다.</summary>
    Core = 1,

    /// <summary>
    /// 지금 마왕성 체력의 <b>절반</b>(내림). Amount 를 보지 않는다.
    ///
    /// 체력이 많을수록 비싸고 적을수록 싸다 — 대신 적을 때는 그 절반이 더 아프다.
    /// 표에 값이 없는 유일한 대가라, 고를 때마다 무게가 달라진다.
    /// </summary>
    HalfCore = 2,

    /// <summary>
    /// 덱에서 <b>레벨이 가장 낮은</b> 몬스터 카드 한 장. Amount 를 보지 않는다.
    ///
    /// ⚠ 마지막 한 장은 못 낸다 — 덱이 비면 아무것도 소환할 수 없다
    ///   (제단이 같은 이유로 막는다).
    /// </summary>
    LowestCard = 3,
}

/// <summary>갈래 하나 — 무엇을 내고 무엇을 받나.</summary>
public readonly struct RunEventChoice
{
    /// <summary>버튼에 적히는 말. ⚠ 값은 여기 적지 않는다 — Describe 가 만든다.</summary>
    public readonly string Label;

    public readonly RunEventGain Gain;
    public readonly int          Amount;

    /// <summary>무엇으로 내는가. None 이면 공짜 갈래다.</summary>
    public readonly RunEventCost Cost;

    /// <summary>대가의 양. <see cref="RunEventCost.Core"/> 만 쓴다 — 나머지는 상태가 정한다.</summary>
    public readonly int CostAmount;

    public RunEventChoice(string label, RunEventGain gain, int amount,
                          RunEventCost cost = RunEventCost.None, int costAmount = 0)
    {
        Label      = label;
        Gain       = gain;
        Amount     = amount;
        Cost       = cost;
        CostAmount = costAmount;
    }
}

/// <summary>이벤트 하나 — 제목 · 이야기 · 세 갈래.</summary>
public readonly struct RunEventDef
{
    public readonly RunEventId     Id;
    public readonly string         Title;

    /// <summary>이야기 한 토막. ⚠ 규칙을 설명하지 않는다 (RunNodeFlavor 와 같은 규칙).</summary>
    public readonly string         Body;

    /// <summary>체력을 내고 받는 구조적인 것. 이벤트마다 다른 유일한 갈래다.</summary>
    public readonly RunEventChoice Reward;

    /// <summary>공짜 — 골드.</summary>
    public readonly RunEventChoice Gold;

    /// <summary>공짜 — 마왕성 체력 회복. ⚠ 야영지보다 적다.</summary>
    public readonly RunEventChoice Heal;

    /// <summary>화면에 세우는 순서 그대로. ⚠ 뒤집지 말 것 (머리 주석 참고).</summary>
    public RunEventChoice[] Choices => new[] { Reward, Gold, Heal };

    public RunEventDef(RunEventId id, string title, string body,
                       RunEventChoice reward, RunEventChoice gold, RunEventChoice heal)
    {
        Id     = id;
        Title  = title;
        Body   = body;
        Reward = reward;
        Gold   = gold;
        Heal   = heal;
    }
}

public static class RunEventRule
{
    // ── 값 ───────────────────────────────────────────────────

    /// <summary>
    /// 골드 한 단위. 갈래의 Amount 가 이 값의 배수다.
    ///
    /// ⚠ 정본은 <see cref="RunGoldRule.PriceUnit"/> 다 — 여기서 다시 적지 말 것
    ///   (2026-09-10). 주는 쪽(이벤트)과 쓰는 쪽(상점·시설)이 같은 자를 써야
    ///   "이 골드가 얼마짜리인가" 가 성립한다. 한때 이 함수만 스테이지를 따라
    ///   컸고 소비처는 전부 고정가라, 후반에 돈이 남아돌았다.
    /// </summary>
    public static int GoldUnit(int stageNumber) => RunGoldRule.PriceUnit(stageNumber);

    /// <summary>
    /// 회복 갈래가 되찾아 주는 마왕성 체력. <b>야영지보다 뚜렷하게 적다.</b>
    ///
    /// ⚠ 숫자를 직접 적지 않는다 — 야영지에서 **비율로** 만든다
    ///   둘을 따로 적으면 야영지 값을 올린 날 이벤트가 조용히 따라붙는다.
    ///   그러면 "무료로 수리하는 자리" 가 둘이 되어 야영지가 죽는다.
    ///
    /// ⚠ 뺄셈이 아니라 비율인 이유
    ///   전에는 −2 였다. 야영지를 5 에서 8 로 올리자 이벤트가 6 이 되어
    ///   폭이 그대로였다. 비율이면 야영지를 얼마로 올리든 3분의 1쯤에 남는다.
    ///
    /// ⚠ 마지막 줄(Min)이 안전장치다 — 비율을 1 에 가깝게 잘못 적어도
    ///   야영지와 같아지지는 않는다.
    ///
    /// ⚠ 유물 '재건'(CampHealBonus)은 더하지 않는다 — 그건 야영지의 보너스다.
    ///   여기까지 얹으면 유물 하나가 두 자리를 동시에 키운다.
    /// </summary>
    public static int HealAmount
    {
        get
        {
            int camp = RunNodeRule.CampHealAmount;
            int heal = Mathf.RoundToInt(camp * HealRatio);

            return Mathf.Clamp(heal, 1, Mathf.Max(1, camp - 1));
        }
    }

    /// <summary>야영지 회복량의 몇 할인가. 24 → 8.</summary>
    const float HealRatio = 0.35f;

    // ── 표 ───────────────────────────────────────────────────
    //
    //  ⚠ 보상 갈래의 체력 값은 **셋뿐이다** (3 · 4 · 5)
    //    값을 잘게 나누면 "이건 4고 저건 6" 을 외워야 한다. 셋이면
    //    가볍다/보통/무겁다 로 읽힌다. 무거울수록 받는 것이 크다.

    /// <summary>회복 갈래 — 여덟이 값은 같고 말만 다르다. 양은 HealAmount 하나가 정본이다.</summary>
    static RunEventChoice Rest(string label)
        => new(label, RunEventGain.CoreHeal, 0);

    static readonly RunEventDef[] Table =
    {
        // ⚠ 광산만 보상 갈래가 골드다 — 돈이 나오는 곳이라 그게 맞다
        //   대신 골드 갈래를 여덟 중 가장 작게(×1) 두어 [큰돈 / 푼돈 / 회복]
        //   세 칸이 서로 다른 크기로 읽히게 한다. 나머지 일곱은 전부
        //   구조적인 것(덱·시너지·성벽)을 준다.
        new(RunEventId.MinerShaft, "광부의 갱도",
            "버려진 갱도가 어둠 속으로 이어진다. 아래쪽에서 쇳내가 올라온다.",
            new RunEventChoice("갱도 끝까지 파고든다", RunEventGain.Gold, 4, RunEventCost.Core, 15),
            new RunEventChoice("입구만 훑는다", RunEventGain.Gold, 1),
            Rest("갱도 안에서 하룻밤 눕는다")),

        new(RunEventId.LostLibrary, "잊힌 서고",
            "곰팡내 나는 서가가 줄지어 섰다. 펼치면 무언가 읽는 쪽도 읽는다.",
            new RunEventChoice("끝까지 읽는다", RunEventGain.Perk, 1, RunEventCost.Core, 9),
            new RunEventChoice("값나가는 것만 챙긴다", RunEventGain.Gold, 2),
            Rest("서가 사이에서 눈을 붙인다")),

        new(RunEventId.WanderingSoul, "떠도는 혼",
            "형체 없는 것이 성벽 주위를 돈다. 들어오고 싶어 하는 눈치다.",
            new RunEventChoice("몸을 내어 준다", RunEventGain.CardLevel, 1, RunEventCost.Core, 12),
            new RunEventChoice("공물을 놓고 돌려보낸다", RunEventGain.Gold, 2),
            Rest("성벽 틈을 메우게 시킨다")),

        new(RunEventId.StrayMonster, "길 잃은 몬스터",
            "무리에서 떨어진 것이 성문 앞에 웅크려 있다. 굶었다.",
            new RunEventChoice("피를 먹여 거둔다", RunEventGain.NewCard, 1, RunEventCost.Core, 9),
            new RunEventChoice("가진 것을 빼앗는다", RunEventGain.Gold, 2),
            Rest("먹인 값으로 돌을 나르게 한다")),

        new(RunEventId.OfferingMark, "제물의 흔적",
            "누군가 먼저 다녀갔다. 마른 자국이 아직 표식의 모양을 하고 있다.",
            new RunEventChoice("표식을 잇는다", RunEventGain.SynergyCount, 1, RunEventCost.Core, 15),
            new RunEventChoice("남은 것을 긁어모은다", RunEventGain.Gold, 2),
            Rest("자국을 지우고 하루를 쉰다")),

        new(RunEventId.BrokenCircle, "깨진 소환진",
            "금 간 마법진이 아직 희미하게 돈다. 한 획만 다시 그으면 될 것 같다.",
            new RunEventChoice("피로 획을 잇는다", RunEventGain.ExtraSummons, 1, RunEventCost.Core, 12),
            new RunEventChoice("남은 마력을 팔아넘긴다", RunEventGain.Gold, 2),
            Rest("남은 마력을 성벽으로 돌린다")),

        new(RunEventId.SupplyCart, "버려진 보급 수레",
            "용사들이 두고 간 수레가 옆으로 넘어져 있다. 바퀴가 아직 성하다.",
            new RunEventChoice("헐어 성벽에 덧댄다", RunEventGain.CoreMax, 12, RunEventCost.Core, 9),
            new RunEventChoice("쓸 만한 것만 판다", RunEventGain.Gold, 2),
            Rest("실린 식량을 나눠 먹는다")),

        new(RunEventId.RoamingSmith, "떠돌이 대장장이",
            "화덕도 없이 망치만 든 자가 앉아 있다. \"값은 쇠로 안 받습니다.\"",
            new RunEventChoice("손목을 내어 준다", RunEventGain.ManaCut, 1, RunEventCost.Core, 12),
            new RunEventChoice("연장을 사들인다", RunEventGain.Gold, 2),
            Rest("성문 경첩을 고쳐 달게 한다")),

        // ── 무거운 판 넷 (2026-09-08 추가) ────────────────────
        //
        //  ⚠ 앞의 여덟과 값이 다르다 — 전부 체력 −15 다
        //    같은 물건을 더 크게 주되 더 비싸다. 만나는 것이 무작위라
        //    "이번 이벤트는 크다" 가 그 판의 성격이 된다.
        //    ⚠ 앞의 것을 그대로 두고 이쪽만 키운 이유 — 값이 하나뿐이면
        //      체력이 넉넉한 판과 빠듯한 판이 같은 무게로 읽힌다.

        new(RunEventId.SealedStaff, "봉인된 지팡이",
            "돌무더기 아래 지팡이 한 자루가 박혀 있다. 뽑으면 대가를 요구할 눈치다.",
            new RunEventChoice("뽑아 든다", RunEventGain.SignatureUse, 1, RunEventCost.Core, 15),
            new RunEventChoice("장식만 뜯어낸다", RunEventGain.Gold, 2),
            Rest("돌무더기 그늘에 앉는다")),

        new(RunEventId.HeroGraves, "용사들의 무덤",
            "비석도 없는 흙더미가 줄지어 있다. 아직 갑옷 냄새가 난다.",
            new RunEventChoice("파내어 성벽에 쌓는다", RunEventGain.CoreMax, 21, RunEventCost.Core, 15),
            new RunEventChoice("부장품만 챙긴다", RunEventGain.Gold, 2),
            Rest("흙더미 사이에서 숨을 고른다")),

        new(RunEventId.BloodMoon, "핏빛 달",
            "달이 붉다. 성벽 아래 것들이 평소보다 크게 운다.",
            new RunEventChoice("달빛 아래 피를 뿌린다", RunEventGain.CardLevel, 2, RunEventCost.Core, 15),
            new RunEventChoice("문을 걸고 지켜본다", RunEventGain.Gold, 2),
            Rest("붉은 밤이 지나가길 기다린다")),

        new(RunEventId.BrokenPens, "버려진 사육장",
            "부서진 우리가 늘어서 있다. 바닥에 발자국이 겹겹이 남았다.",
            new RunEventChoice("우리를 다시 세운다", RunEventGain.ExtraSummons, 2, RunEventCost.Core, 15),
            new RunEventChoice("쇠창살을 뜯어 판다", RunEventGain.Gold, 2),
            Rest("빈 우리에 짚을 깔고 눕는다")),

        // ── 대가가 체력이 아닌 판 셋 (2026-09-08 추가) ────────
        //
        //  ■ 앞의 열둘은 전부 "체력 얼마" 라는 같은 질문이었다
        //    값만 다르고 묻는 것이 같으면 열두 판이 한 판처럼 읽힌다.
        //    이 셋은 **무엇을 내는가**가 다르다 — 빈 칸 · 손에 든 카드 ·
        //    지금 상태. 그래서 덱을 보고 고르게 된다.
        //
        //  ⚠ 셋 다 못 고르는 판이 흔하다 (빈 칸이 없다 · 카드가 한 장뿐이다 ·
        //    체력이 빠듯하다). 그래도 골드·회복 갈래가 늘 살아 있어 칸이 죽지 않는다.

        new(RunEventId.BrokenNest, "버려진 둥지",
            "우리 세 개가 나란히 놓여 있다. 아직 온기가 남았다.",
            new RunEventChoice("셋 다 품는다", RunEventGain.NewCard, 3, RunEventCost.Core, 15),
            new RunEventChoice("둥지를 헐어 판다", RunEventGain.Gold, 2),
            Rest("둥지 옆에 자리를 편다")),

        new(RunEventId.HungryIdol, "굶주린 우상",
            "입을 벌린 돌 우상이 서 있다. 무엇이든 삼킬 것처럼 생겼다.",
            new RunEventChoice("가장 약한 것을 바친다", RunEventGain.TopCardLevel, 2,
                               RunEventCost.LowestCard),
            new RunEventChoice("우상의 눈을 파낸다", RunEventGain.Gold, 2),
            Rest("우상 발치에서 쉬어 간다")),

        new(RunEventId.InvertedGlass, "뒤집힌 모래시계",
            "모래가 위로 흐른다. 들여다보면 성벽이 조금씩 얇아지는 것이 보인다.",
            new RunEventChoice("모래를 되돌린다", RunEventGain.Perk, 2, RunEventCost.HalfCore),
            new RunEventChoice("유리를 깨어 판다", RunEventGain.Gold, 2),
            Rest("모래가 다 흐를 때까지 눈을 감는다")),
    };

    public static int Count => Table.Length;

    /// <summary>
    /// 번호로 이벤트를 찾는다. <b>번호가 곧 표의 자리다.</b>
    ///
    /// ⚠ 그 전제가 깨지면 조용히 엉뚱한 이벤트가 뜬다 — 만난 기록도 어긋난다.
    ///   그래서 처음 쓰일 때 한 번 검사한다 (<see cref="Verify"/>).
    /// </summary>
    public static RunEventDef Get(RunEventId id)
    {
        Verify();
        return Table[(int)id];
    }

    static bool _verified;

    /// <summary>
    /// 표의 자리와 <see cref="RunEventId"/> 번호가 맞는지 본다.
    ///
    /// ⚠ 조용한 실패를 시끄러운 실패로 바꾸는 자리다
    ///   표 중간에 한 줄을 끼우면 그 뒤가 전부 한 칸씩 밀린다. 화면에는
    ///   "제목과 선택지가 안 어울린다" 로만 보이고, 저장된 '만난 기록'
    ///   (번호)도 다른 이벤트를 가리키게 된다.
    /// </summary>
    static void Verify()
    {
        if (_verified) return;
        _verified = true;

        int ids = System.Enum.GetValues(typeof(RunEventId)).Length;

        if (Table.Length != ids)
            Debug.LogError($"[RunEventRule] 표({Table.Length})와 RunEventId({ids})의 수가 다릅니다 " +
                           "— 둘 다 채우세요.");

        for (int i = 0; i < Table.Length; i++)
            if ((int)Table[i].Id != i)
                Debug.LogError($"[RunEventRule] 표 {i}번 자리에 {Table[i].Id}({(int)Table[i].Id}) 가 " +
                               "있습니다 — RunEventId 번호와 표의 순서가 같아야 합니다. " +
                               "⚠ 중간에 끼우지 말고 뒤에만 추가할 것.");
    }

    // ── 만날 이벤트 고르기 ───────────────────────────────────

    /// <summary>
    /// 이번 판에 만날 이벤트. <paramref name="seen"/> 에 없는 것 중에서 고른다.
    ///
    /// ⚠ 다 만났으면 **기록을 비우고 다시 돈다**
    ///   후보가 없다고 갈림길을 건너뛰면, 이벤트를 여덟 번 본 런은 그 뒤로
    ///   갈림길 한 칸이 조용히 사라진다. 비우는 것은 부르는 쪽의 몫이다 —
    ///   여기서는 "비었을 때도 반드시 하나를 돌려준다" 만 지킨다.
    /// </summary>
    public static RunEventId Pick(IReadOnlyList<int> seen)
    {
        var pool = new List<RunEventId>(Table.Length);

        foreach (RunEventDef def in Table)
            if (!Seen(seen, def.Id)) pool.Add(def.Id);

        if (pool.Count == 0)
            return Table[Random.Range(0, Table.Length)].Id;

        return pool[Random.Range(0, pool.Count)];
    }

    /// <summary>이미 만났나. ⚠ LINQ 를 쓰지 않는다 — 갈림길마다 도는 자리다.</summary>
    static bool Seen(IReadOnlyList<int> seen, RunEventId id)
    {
        if (seen == null) return false;

        for (int i = 0; i < seen.Count; i++)
            if (seen[i] == (int)id) return true;

        return false;
    }

    // ── 설명 ─────────────────────────────────────────────────

    /// <summary>
    /// 갈래 한 줄의 설명 — <b>[내는 것] → [받는 것]</b>.
    ///
    /// ⚠ 수치를 손으로 적지 말 것. 값은 전부 이 함수가 표에서 뽑는다 —
    ///   표를 고쳤는데 설명만 옛 숫자를 말하는 상태를 막는 유일한 방법이다.
    /// </summary>
    public static string Describe(in RunEventChoice choice, int stageNumber)
    {
        string gain = choice.Gain switch
        {
            RunEventGain.Gold         => F("골드 +{0:N0}", GoldUnit(stageNumber) * choice.Amount),
            RunEventGain.Perk         => F("특성 {0}개를 얻는다", choice.Amount),
            RunEventGain.CardLevel    => F("덱에서 레벨이 가장 낮은 카드 Lv +{0}", choice.Amount),
            RunEventGain.NewCard      => choice.Amount > 1
                                       ? F("몬스터 카드 {0}장을 얻는다", choice.Amount)
                                       : F("몬스터 카드 1장을 얻는다"),
            RunEventGain.SynergyCount => F("덱에 가장 많은 표식의 카운트 +{0}", choice.Amount),
            RunEventGain.ExtraSummons => F("가장 비싼 카드의 소환 마릿수 +{0}", choice.Amount),
            RunEventGain.ManaCut      => F("가장 비싼 카드의 소환 비용 −{0}", choice.Amount),
            RunEventGain.CoreMax      => F("마왕성 최대 체력 +{0} · 최대 마나 +{1}",
                                           choice.Amount, ManaForCoreMax(choice.Amount)),

            // ⚠ choice.Amount 가 아니라 HealAmount 다 — 양의 정본은 하나다.
            RunEventGain.CoreHeal     => F("마왕성 체력 +{0}", HealAmount),

            RunEventGain.SignatureUse => F("시그니처 스킬 스테이지당 사용 +{0}", choice.Amount),
            RunEventGain.TopCardLevel => F("덱에서 가장 비싼 카드 Lv +{0}", choice.Amount),

            _                         => "",
        };

        string cost = DescribeCost(choice);

        return string.IsNullOrEmpty(cost) ? gain : $"{cost}   →   {gain}";
    }

    /// <summary>
    /// 대가 한 토막. 공짜면 빈 글자다.
    ///
    /// ⚠ 상태가 정하는 대가는 **지금 얼마인지**를 적는다
    ///   "절반" · "가장 약한 카드" 로만 적으면 고르기 전에 계산과 확인을 시킨다.
    ///   여기서 실제 숫자와 이름을 뽑아 주는 것이 그 갈래의 재미다.
    /// </summary>
    public static string DescribeCost(in RunEventChoice choice)
    {
        var user = UserDataManager.Instance;

        switch (choice.Cost)
        {
            case RunEventCost.Core:
                return F("마왕성 체력 −{0}", choice.CostAmount);

            case RunEventCost.HalfCore:
                return F("마왕성 체력 −{0} (지금의 절반)", HalfCoreCost(user));

            case RunEventCost.LowestCard:
            {
                int at = LowestLevelSlotForSacrifice(user.Get<SummonDeckData>());
                return at < 0
                     ? F("바칠 카드가 없다")
                     : F("[{0}] 카드를 잃는다", NameOf(user.Get<SummonDeckData>().GetSlot(at).Id));
            }

            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// 표에서 문장을 찾아 숫자·이름을 끼워 넣는다 — 원본 표가 쓰는 <c>{0}</c> 방식.
    ///
    /// ⚠ <b>수치가 든 설명은 반드시 이걸 쓴다. 보간 문자열($"…{값}…")을 쓰지 말 것</b>
    ///   (2026-09-16). 보간은 실행 시점에 이미 숫자로 바뀌어 있어서 번역표의
    ///   키(코드에 적힌 그대로의 문자열)와 **영원히 일치하지 않는다** — 표에는
    ///   줄이 있는데 화면에는 한국어로 남는다.
    ///
    /// ⚠ 키를 고치면 LocalizationTable.txt 의 같은 문장도 함께 고칠 것.
    /// </summary>
    static string F(string key, params object[] args)
        => LocalizationManager.Instance.Format(key, args);

    /// <summary>지금 체력의 절반(내림). ⚠ 최소 1 — 0 이면 공짜 갈래가 된다.</summary>
    static int HalfCoreCost(UserDataManager user)
        => Mathf.Max(1, user.Get<RunCoreData>().Current / 2);

    // ── 고를 수 있는가 ───────────────────────────────────────

    /// <summary>
    /// 이 갈래를 지금 고를 수 있는가.
    ///
    /// ⚠ 체력은 <b>남아야</b> 한다 — 내고 나서 0 이 되면 안 된다
    ///   "골랐더니 런이 끝났다" 는 어떤 이유로도 만들지 않는다.
    ///   못 고르는 갈래는 흐려질 뿐이고, 안전 갈래(골드)는 언제나 고를 수 있어
    ///   이벤트 칸이 막다른 길이 되지 않는다.
    ///
    /// ⚠ 받을 자리가 없어도 못 고른다 — 덱이 꽉 찼는데 "새 카드" 를 고르면
    ///   체력만 내고 아무것도 못 받는다 (Acquire 가 0 을 돌려준다).
    /// </summary>
    public static bool Available(in RunEventChoice choice)
    {
        var user = UserDataManager.Instance;
        if (user == null) return false;

        RunCoreData    core = user.Get<RunCoreData>();
        SummonDeckData deck = user.Get<SummonDeckData>();

        if (!CanPay(choice, user, core, deck)) return false;

        return choice.Gain switch
        {
            RunEventGain.Gold         => true,
            RunEventGain.CoreMax      => true,

            // ⚠ 시그니처가 없는 소환사가 있다 — 그때는 아무 일도 안 일어난다.
            RunEventGain.SignatureUse => SummonerRuntimeBridge.Current?.Data?.HasSignatureSkill
                                      ?? false,

            // ⚠ 가득 찼으면 못 고른다 — 야영지의 '수리' 와 같은 규칙이다.
            //   그냥 두면 아무 일도 안 일어나는 줄을 고르게 된다.
            RunEventGain.CoreHeal     => core.Current < core.Max,
            // ⚠ 개수만큼 남아 있어야 한다 — 모자라면 낸 값에 못 미치게 받는다.
            RunEventGain.Perk         => user.Get<RunPerkData>().CollectMissing().Count
                                       >= choice.Amount,
            // ⚠ 빈 칸이 장수만큼 있어야 한다 (사용자 지적, 2026-09-08)
            //   모자란 채로 주면 넘치는 장이 조용히 사라지는데 대가는 이미 냈다.
            RunEventGain.NewCard      => deck.FreeSlotCount >= choice.Amount
                                      && HasCardCandidate(deck),

            RunEventGain.CardLevel    => LowestLevelSlot(deck) >= 0,
            RunEventGain.TopCardLevel => PriciestSlot(deck) >= 0,
            RunEventGain.SynergyCount => DominantTag(deck) != MonsterTag.None,
            RunEventGain.ExtraSummons => PriciestSlot(deck) >= 0,
            RunEventGain.ManaCut      => PriciestSlot(deck) >= 0,
            _                         => false,
        };
    }

    /// <summary>
    /// 대가를 지금 낼 수 있는가.
    ///
    /// ⚠ 체력은 내고 나서도 **남아야** 한다 — "골랐더니 런이 끝났다" 를
    ///   어떤 이유로도 만들지 않는다 (머리 주석 참고).
    /// ⚠ 마지막 카드는 못 바친다 — 덱이 비면 아무것도 소환할 수 없다.
    /// </summary>
    static bool CanPay(in RunEventChoice choice, UserDataManager user,
                       RunCoreData core, SummonDeckData deck)
        => choice.Cost switch
        {
            RunEventCost.None       => true,
            RunEventCost.Core       => core.Current - choice.CostAmount >= 1,
            RunEventCost.HalfCore   => core.Current - HalfCoreCost(user) >= 1,
            RunEventCost.LowestCard => LowestLevelSlotForSacrifice(deck) >= 0,
            _                       => false,
        };

    // ── 적용 ─────────────────────────────────────────────────

    /// <summary>
    /// 갈래를 실제로 치른다. 돌려주는 것은 <b>결과 화면에 뜰 한 줄</b>이다.
    ///
    /// ⚠ 값을 먼저 내고 물건을 나중에 준다 (RunNodeFlow 머리 주석과 같은 규칙).
    /// ⚠ 무엇을 받았는지 **이름을 대야 한다** — "카드 하나가 좋아졌다" 로는
    ///   덱을 다시 열어 보기 전까지 무슨 일이 있었는지 알 수 없다.
    /// </summary>
    public static string Apply(in RunEventChoice choice, int stageNumber)
    {
        var user = UserDataManager.Instance;

        // ⚠ 값을 먼저 낸다. 낸 것이 무엇인지도 여기서 문장으로 받아 둔다 —
        //   카드를 잃는 대가는 지운 뒤에는 이름을 알 수 없다.
        string paid = Pay(choice, user);

        SummonDeckData deck   = user.Get<SummonDeckData>();
        string         result = choice.Gain switch
        {
            RunEventGain.Gold         => GiveGold(stageNumber, choice.Amount),
            RunEventGain.Perk         => GivePerk(user, choice.Amount),
            RunEventGain.CardLevel    => GiveCardLevel(deck, choice.Amount),
            RunEventGain.TopCardLevel => GiveTopCardLevel(deck, choice.Amount),
            RunEventGain.NewCard      => GiveNewCard(user, deck, choice.Amount),
            RunEventGain.SynergyCount => GiveSynergy(user, deck, choice.Amount),
            RunEventGain.ExtraSummons => UpgradeCard(deck, cut: 0, extra: choice.Amount),
            RunEventGain.ManaCut      => UpgradeCard(deck, cut: choice.Amount, extra: 0),
            RunEventGain.CoreMax      => GiveCoreMax(user, choice.Amount),
            RunEventGain.CoreHeal     => GiveCoreHeal(user),
            RunEventGain.SignatureUse => GiveSignatureUse(user, choice.Amount),
            _                         => "",
        };

        user.RequestSave();

        return string.IsNullOrEmpty(paid) ? result : $"{paid}   {result}";
    }

    /// <summary>
    /// 대가를 실제로 치른다. 돌려주는 것은 <b>결과 화면에 뜰 앞 토막</b>이다.
    ///
    /// ⚠ 카드를 잃는 대가는 지우기 **전에** 이름을 읽어 둔다 —
    ///   지운 뒤에는 무엇을 바쳤는지 화면이 말할 수 없다.
    /// </summary>
    static string Pay(in RunEventChoice choice, UserDataManager user)
    {
        switch (choice.Cost)
        {
            case RunEventCost.Core:
                user.Get<RunCoreData>().Pay(choice.CostAmount);
                return F("마왕성 체력을 {0} 내주었다.", choice.CostAmount);

            case RunEventCost.HalfCore:
            {
                int amount = HalfCoreCost(user);
                user.Get<RunCoreData>().Pay(amount);
                return F("마왕성 체력을 {0} 내주었다.", amount);
            }

            case RunEventCost.LowestCard:
            {
                SummonDeckData deck = user.Get<SummonDeckData>();
                int            at   = LowestLevelSlotForSacrifice(deck);

                if (at < 0) return string.Empty;

                string name = NameOf(deck.GetSlot(at).Id);
                deck.ClearSlot(at);

                return F("[{0}] 이(가) 삼켜졌다.", name);
            }

            default:
                return string.Empty;
        }
    }

    // ── 각 보상 ──────────────────────────────────────────────

    static string GiveGold(int stageNumber, int units)
    {
        int amount = GoldUnit(stageNumber) * units;

        // ⚠ RunGoldRule 을 지난다 — 유물 '약탈의 손' 이 거기서 얹힌다.
        //   RunGoldData 에 직접 넣으면 그 보너스가 조용히 빠진다.
        RunGoldRule.Grant(amount);

        return F("골드를 {0:N0} 얻었다.", amount);
    }

    static string GivePerk(UserDataManager user, int count)
    {
        RunPerkData   data  = user.Get<RunPerkData>();

        // ⚠ 한 번에 뽑는다 — 한 장씩 뽑으면 같은 특성이 두 번 나온다
        //   (Pick 은 한 번의 호출 안에서만 중복을 막는다).
        List<RunPerk> picks = RunPerkPicker.Pick(data, count);

        if (picks.Count == 0) return "얻을 것이 남아 있지 않았다.";

        var names = new List<string>(picks.Count);
        foreach (RunPerk perk in picks)
        {
            data.Add(perk);
            names.Add(perk.ToKorean());
        }

        return F("특성 [{0}] 을(를) 얻었다.", string.Join("] [", names));
    }

    static string GiveCardLevel(SummonDeckData deck, int amount)
    {
        int at = LowestLevelSlot(deck);
        if (at < 0) return "레벨을 올릴 카드가 없었다.";

        SummonDeckSlot slot = deck.GetSlot(at);
        string         name = NameOf(slot.Id);
        int            was  = slot.Level;

        // ⚠ Acquire 가 곧 레벨업이다 — 이미 가진 카드는 장수를 올린다.
        //   레벨을 직접 쓰는 경로를 새로 만들면 '속성'·'조기 성장' 이 갈린다.
        // ⚠ 같은 카드에 거듭 얹는다 — 매번 가장 낮은 것을 다시 찾으면
        //   +2 가 서로 다른 두 카드에 흩어져 "한 장이 크게 자란다" 가 아니게 된다.
        for (int i = 0; i < amount; i++)
            deck.Acquire(SummonKind.Monster, slot.Id);

        return F("[{0}] 이(가) Lv.{1} → Lv.{2} 이 되었다.", name, was, deck.GetSlot(at).Level);
    }

    static string GiveNewCard(UserDataManager user, SummonDeckData deck, int count)
    {
        // ⚠ 후보 규칙은 카드 3택·상점과 **같은 것을 쓴다**
        //   이벤트만 다른 규칙을 두면 도감 해금이 무슨 뜻인지가 화면마다 갈린다.
        //
        // ⚠ 한 번에 뽑지 않고 **한 장씩 다시 뽑는다**
        //   CardRewardPicker 는 덱을 보고 후보를 만든다. 한 장 넣을 때마다
        //   덱이 달라지므로(중복이 되거나 칸이 차거나) 다시 물어야 맞다.
        //   한 번에 셋을 받아 두면 두 번째 장이 "이미 넣은 것" 과 겹칠 수 있다.
        var names = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            // ⚠ 칸이 없으면 멈춘다 — Acquire 가 0 을 돌려주고 그 장은 사라진다.
            //   Available 이 미리 막지만, 여기가 마지막 방벽이다.
            if (!deck.HasFreeSlot) break;

            List<CardRewardOption> picks = RollCards(deck);
            if (picks.Count == 0) break;

            CardRewardOption opt = picks[0];
            deck.Acquire(opt.Kind, opt.Id);

            // ⚠ 도감 해금은 언제나 부르는 쪽의 몫이다 (CLAUDE.md 확정 규칙)
            if (opt.Kind == SummonKind.Monster)
                user.Get<MonsterCodexData>().Unlock(opt.Id, UnitGrade.Normal);

            names.Add(NameOf(opt.Id));
        }

        if (names.Count == 0) return "따라올 만한 것이 없었다.";

        return F("[{0}] 이(가) 따라왔다.", string.Join("] [", names));
    }

    static string GiveTopCardLevel(SummonDeckData deck, int amount)
    {
        int at = PriciestSlot(deck);
        if (at < 0) return "살찌울 카드가 없었다.";

        SummonDeckSlot slot = deck.GetSlot(at);
        string         name = NameOf(slot.Id);
        int            was  = slot.Level;

        // ⚠ 같은 카드에 거듭 얹는다 (GiveCardLevel 과 같은 이유)
        for (int i = 0; i < amount; i++)
            deck.Acquire(SummonKind.Monster, slot.Id);

        return F("[{0}] 이(가) Lv.{1} → Lv.{2} 이 되었다.", name, was, deck.GetSlot(at).Level);
    }

    static string GiveSynergy(UserDataManager user, SummonDeckData deck, int amount)
    {
        MonsterTag tag = DominantTag(deck);
        if (tag == MonsterTag.None) return "이을 표식이 없었다.";

        // 저장고는 RunBoonData 다 — 제단 제물과 같은 자리에 쌓인다.
        user.Get<RunBoonData>().AddSynergy(tag, amount);

        return F("[{0}] 카운트가 {1} 올랐다.", MonsterSynergyRule.NameOf(tag), amount);
    }

    static string UpgradeCard(SummonDeckData deck, int cut, int extra)
    {
        int at = PriciestSlot(deck);
        if (at < 0) return "새길 카드가 없었다.";

        string name = NameOf(deck.GetSlot(at).Id);
        deck.UpgradeCard(at, cut, extra);

        return cut > 0
             ? F("[{0}] 의 소환 비용이 {1} 줄었다.", name, cut)
             : F("[{0}] 이(가) {1}마리 더 나온다.", name, extra);
    }

    /// <summary>
    /// 마왕성 최대 체력 보상에 함께 붙는 최대 마나 — 체력 몫의 2/3 (사용자 지시, 2026-09-12).
    /// 야영지 증축(체력 9 · 마나 6)과 같은 비율이다. 표시·지급이 이 함수 하나를 본다.
    /// </summary>
    public static int ManaForCoreMax(int coreMax) => Mathf.RoundToInt(coreMax * 2f / 3f);

    static string GiveCoreMax(UserDataManager user, int amount)
    {
        user.Get<RunCoreData>().AddMax(amount);

        int mana = ManaForCoreMax(amount);
        RunPerkRule.GrowMaxMana(mana);

        return F("마왕성 최대 체력이 {0}, 최대 마나가 {1} 늘었다.", amount, mana);
    }

    static string GiveSignatureUse(UserDataManager user, int amount)
    {
        user.Get<RunBoonData>().AddSignatureUse(amount);
        return F("시그니처 스킬을 판마다 {0}번 더 쓸 수 있게 되었다.", amount);
    }

    static string GiveCoreHeal(UserDataManager user)
    {
        RunCoreData core = user.Get<RunCoreData>();
        int         was  = core.Current;

        core.Heal(HealAmount);

        // ⚠ 실제로 오른 만큼 적는다 — 상한에 걸리면 HealAmount 보다 적다.
        //   표를 그대로 읊으면 "3 회복" 이라 적고 1 만 오르는 줄이 생긴다.
        return F("마왕성 체력을 {0} 회복했다.", core.Current - was);
    }

    // ── 덱에서 대상 고르기 ───────────────────────────────────
    //
    //  ⚠ 전부 **결정적**이다 — 같은 덱이면 같은 카드가 걸린다
    //    무작위로 고르면 결과 화면을 읽고도 왜 그 카드인지 설명할 수 없다.
    //    동점이면 덱의 앞 칸이 이긴다.

    /// <summary>레벨이 가장 낮은 몬스터 카드. 전부 만렙이면 −1.</summary>
    static int LowestLevelSlot(SummonDeckData deck)
    {
        int best  = -1;
        int lowest = int.MaxValue;

        for (int i = 0; i < deck.SlotCount; i++)
        {
            SummonDeckSlot slot = deck.GetSlot(i);
            if (slot.IsEmpty || slot.Kind != SummonKind.Monster) continue;
            if (slot.IsMaxLevel) continue;

            if (slot.Level >= lowest) continue;

            lowest = slot.Level;
            best   = i;
        }

        return best;
    }

    /// <summary>
    /// 소환 비용이 가장 비싼 몬스터 카드.
    ///
    /// ⚠ 마릿수 갈래도 이 카드에 건다 — 두 갈래가 서로 다른 기준으로 고르면
    ///   "어느 카드에 걸리나" 를 갈래마다 따로 외워야 한다. 규칙은 하나다:
    ///   <b>가장 비싼 카드가 좋아진다.</b> 비싼 카드일수록 −1 도 +1마리도 값이 크다.
    /// </summary>
    static int PriciestSlot(SummonDeckData deck)
    {
        CardCatalog catalog = CardCatalog.Current;
        if (catalog == null) return -1;

        int   best = -1;
        float top  = float.MinValue;

        for (int i = 0; i < deck.SlotCount; i++)
        {
            SummonDeckSlot slot = deck.GetSlot(i);
            if (slot.IsEmpty || slot.Kind != SummonKind.Monster) continue;

            MonsterSpeciesData sp = catalog.GetMonster(slot.Id);
            if (sp == null) continue;

            float mana = Mathf.Max(1f, sp.ManaCost - slot.ManaDiscount);
            if (mana <= top) continue;

            top  = mana;
            best = i;
        }

        return best;
    }

    /// <summary>
    /// 덱에 가장 많이 든 표식. 없으면 None.
    ///
    /// ⚠ 실제로 부른 수(MonsterSynergyRule.CountOf)가 아니라 <b>덱 구성</b>으로 센다
    ///   갈림길은 전투가 끝난 뒤라 그 판에 무엇을 냈는지에 따라 대상이 흔들린다.
    ///   덱으로 세면 화면을 보고 어느 표식에 걸릴지 미리 안다.
    /// ⚠ 동점이면 AllTags 순서가 앞선 쪽 — 무작위로 고르면 설명할 수 없다.
    /// </summary>
    static MonsterTag DominantTag(SummonDeckData deck)
    {
        CardCatalog catalog = CardCatalog.Current;
        if (catalog == null) return MonsterTag.None;

        MonsterTag[] all    = MonsterSynergyRule.AllTags;
        var          counts = new int[all.Length];

        for (int i = 0; i < deck.SlotCount; i++)
        {
            SummonDeckSlot slot = deck.GetSlot(i);
            if (slot.IsEmpty || slot.Kind != SummonKind.Monster) continue;

            MonsterSpeciesData sp = catalog.GetMonster(slot.Id);
            if (sp == null) continue;

            for (int t = 0; t < all.Length; t++)
                if ((sp.Tags & all[t]) != 0) counts[t]++;
        }

        MonsterTag best = MonsterTag.None;
        int        top  = 0;

        for (int t = 0; t < all.Length; t++)
            if (counts[t] > top) { top = counts[t]; best = all[t]; }

        return best;
    }

    /// <summary>
    /// 제물로 바칠 카드 — 레벨이 가장 낮은 몬스터 카드.
    ///
    /// ⚠ <see cref="LowestLevelSlot"/> 와 다른 물건이다
    ///   그쪽은 "레벨을 올릴 카드" 라 만렙을 건너뛴다. 제물은 만렙이어도
    ///   바칠 수 있어야 한다 — 만렙 카드만 남은 덱에서 갈래가 통째로 죽는다.
    ///
    /// ⚠ 마지막 한 장은 못 바친다 — 덱이 비면 아무것도 소환할 수 없다
    ///   (제단이 같은 이유로 막는다). 그래서 두 장 이상일 때만 자리를 돌려준다.
    /// ⚠ 동점이면 앞 칸. 무작위로 고르면 무엇을 잃을지 미리 알 수 없다.
    /// </summary>
    static int LowestLevelSlotForSacrifice(SummonDeckData deck)
    {
        int best   = -1;
        int lowest = int.MaxValue;
        int owned  = 0;

        for (int i = 0; i < deck.SlotCount; i++)
        {
            SummonDeckSlot slot = deck.GetSlot(i);
            if (slot.IsEmpty || slot.Kind != SummonKind.Monster) continue;

            owned++;

            if (slot.Level >= lowest) continue;

            lowest = slot.Level;
            best   = i;
        }

        return owned >= 2 ? best : -1;
    }

    /// <summary>새 카드를 하나라도 뽑을 수 있는가. 못 뽑으면 갈래가 흐려진다.</summary>
    static bool HasCardCandidate(SummonDeckData deck) => RollCards(deck).Count > 0;

    /// <summary>
    /// 이벤트가 줄 수 있는 카드 후보.
    ///
    /// ⚠ 한 장만 뽑지 않는다 — 그 한 장이 시너지 강화면 걸러진 뒤 빈손이 된다
    ///   (상점과 같은 이유로 시너지 강화는 제외한다: 런에 한 장뿐인 물건이라
    ///   여러 경로로 나오면 무게가 어긋난다). 넉넉히 뽑아 거르면 그 구멍이 없다.
    ///
    /// ⚠ 흐리게 그릴 때와 실제로 줄 때가 **같은 함수**를 써야 한다
    ///   갈라지면 "고를 수 있었는데 눌러 보니 빈손" 이 된다 — 체력은 이미 냈다.
    /// </summary>
    static List<CardRewardOption> RollCards(SummonDeckData deck)
    {
        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;
        if (summoner == null) return new List<CardRewardOption>();

        List<CardRewardOption> picks = CardRewardPicker.Pick(summoner, deck, CardRollWant);
        picks.RemoveAll(o => o.IsSynergyBoost);

        return picks;
    }

    /// <summary>후보를 몇 장 뽑아 놓고 거를 것인가. 시너지 강화 한 장을 빼도 남을 만큼.</summary>
    const int CardRollWant = 3;

    static string NameOf(string id)
    {
        MonsterSpeciesData sp = CardCatalog.Current?.GetMonster(id);
        return sp != null ? sp.DisplayName : id;
    }
}
