using UnityEngine;

// ============================================================
//  RunPerk.cs
//  런을 도는 동안 주워 모으는 **특성** — 규칙 자체를 바꾸는 것들.
//
//  ■ 왜 별도 축인가
//    · 카드 레벨업 → 그 몬스터 한 종류만 강해진다
//    · 소환사 개성 → 캐릭터를 고르는 순간 정해진다 (런 내내 고정)
//    · **특성**    → 런 도중에 얻고, 판 전체의 규칙을 바꾼다
//    셋이 겹치지 않는다. "첫 카드가 공짜" 는 어느 몬스터의 능력도 아니고
//    캐릭터의 정체성도 아니다 — 이번 런에만 붙은 규칙이다.
//
//  ■ 어빌리티를 대체하지 않는다
//    어빌리티 축은 폐기하고 그 스탯 역할은 **카드 레벨업**이 가져갔다
//    (MonsterLevelBonus). 특성은 그중 "규칙을 바꾸는 것" 만 물려받은 것이고,
//    수가 적고 강해야 한다.
//
//    ⚠ 밋밋한 "공격력 +10%" 를 넣지 말 것 — 레벨업과 역할이 겹쳐 둘 다 밋밋해진다.
//      아래 스탯 계열(소수정예·군세·주공·측면·백귀·원군)은 전부 **조건이 붙어** 있다.
//
//  ■ 런 스코프다
//    환생·패배로 사라진다. 영구 강화는 유물(RelicTree)이 담당한다.
//
//  ⚠ enum 값을 바꾸지 말 것 — 세이브에 정수로 남는다.
//
//  ■ 수치의 정본은 RunPerkRule 하나다
//    특성 값을 다른 파일에 박지 말 것. 훅 지점은 흩어져 있어도 숫자는 여기 있다.
// ============================================================

public enum RunPerk
{
    None = 0,

    /// <summary>첫 소환 무료 — 스테이지마다 첫 카드 1회의 마나가 들지 않는다.</summary>
    FreeFirstSummon = 1,

    // ⚠ 2 번은 비어 있다 — 옛 RefundBonus(잔존 정산).
    //   환수가 폐기되면서(살아남은 몬스터는 마나로 녹지 않고 라인으로 돌아간다)
    //   읽는 곳이 사라져 얻어도 아무 일이 없었다. 2026-09-04 특성표 확정에서
    //   빠졌으므로 지운다. ⚠ 번호는 재사용하지 말 것 — 옛 세이브에 2 가 남아 있다.

    /// <summary>과잉 소환 — 라인이 몬스터를 뱉는 간격이 짧아진다.</summary>
    RapidDrain = 3,

    /// <summary>확장 편성 — 카드 칸 +2 (상한 8).</summary>
    ExtraSlots = 4,

    // ── 마나 ────────────────────────────────────────────────

    /// <summary>친화 할인 — 친화 종족의 소환 비용 −2.</summary>
    CheapAffinity = 5,

    /// <summary>만물 할인 — 모든 몬스터의 소환 비용 −1.</summary>
    CheapAll = 6,

    /// <summary>깊은 그릇 — 최대 마나 +25%.</summary>
    DeepVessel = 7,

    /// <summary>명상 — 스테이지 마나 회복량 +20%.</summary>
    Meditation = 8,

    /// <summary>비축 — 마나를 한 방울도 쓰지 않고 판을 넘기면 최대 마나가 영구히 는다.</summary>
    Hoard = 9,

    /// <summary>저울 — 잔량이 절반 이하면 비용이 싸지고, 넘으면 비싸진다.</summary>
    Scales = 10,

    /// <summary>마력 폭주 — 잔량이 0 에 닿으면 그 판 동안 소환력이 오른다.</summary>
    ManaSurge = 11,

    // ── 대기열 · 소환 ───────────────────────────────────────

    /// <summary>증원 — 카드 한 장이 부르는 마릿수 +1.</summary>
    Reinforce = 12,

    /// <summary>친화 증원 — 친화 종족 카드가 부르는 마릿수 +2.</summary>
    AffinityReinforce = 13,

    /// <summary>선발대 — 각 라인 앞 몇 마리는 거의 즉시 나온다.</summary>
    Vanguard = 14,

    /// <summary>원군 — 판이 열린 뒤 부른 몬스터는 비싸지는 대신 훨씬 강하다.</summary>
    Reinforcements = 15,

    // ── 시너지 ──────────────────────────────────────────────

    /// <summary>공명 — 활성 시너지 하나당 전 몬스터 공/체가 오른다.</summary>
    Resonance = 16,

    /// <summary>극단 — 시너지 종류가 적으면 시너지 스탯 효과가 커진다.</summary>
    Extremity = 17,

    /// <summary>편중 — 카운트가 가장 높은 시너지의 카운트 +1.</summary>
    Weighted = 18,

    // ── 친화 ────────────────────────────────────────────────

    /// <summary>심연 공명 — 친화 배율이 오르고 비친화에 벌점이 붙는다.</summary>
    DeepChannel = 19,

    /// <summary>친화 확장 — 덱의 종족 하나를 친화로 추가 지정한다.</summary>
    AffinityExpand = 20,

    // ── 스탯 · 라인 ─────────────────────────────────────────

    /// <summary>대기만성 — 스테이지를 넘길 때마다 전 몬스터 공/체가 조금씩 오른다.</summary>
    LateBloom = 21,

    /// <summary>소수정예 — 몬스터가 가장 적은 라인이 크게 강해진다.</summary>
    FewButElite = 22,

    /// <summary>군세 — 자기 라인의 몬스터 수만큼 강해진다.</summary>
    Horde = 23,

    /// <summary>주공 — 중앙 라인이 강해진다.</summary>
    CenterPush = 24,

    /// <summary>측면 — 바깥 두 라인이 빨라지고 강해진다.</summary>
    Flank = 25,

    /// <summary>파쇄 — 착탄마다 대상 최대 체력에 비례한 추가 피해.</summary>
    Rend = 26,

    /// <summary>백귀 — 덱에 든 종족 수만큼 전 몬스터가 강해진다.</summary>
    Menagerie = 27,

    // ── 카드 · 덱 ───────────────────────────────────────────

    /// <summary>감식안 — 카드 3택이 4택이 된다.</summary>
    Appraisal = 28,

    /// <summary>속성 — 새로 받는 카드가 2레벨로 들어온다.</summary>
    QuickStudy = 29,

    // ── 소환사 · 사망 ───────────────────────────────────────

    /// <summary>집중 — 시그니처 스킬의 스테이지당 사용 횟수 +1.</summary>
    Focus = 30,

    /// <summary>귀환 — 죽은 몬스터도 확률로 라인 대기열에 돌아간다.</summary>
    Homecoming = 31,

    // ── 대가를 치르는 특성 (2026-09-11, 사용자 요청 "특이한 특성") ──
    //   전부 **무언가를 내주고** 받는다. 공짜 강화가 아니라 판의 모양을 바꾸는 선택이다.

    /// <summary>피의 계약 — 마나가 모자라면 모자란 만큼 마왕성 체력으로 낸다.</summary>
    BloodPact = 32,

    /// <summary>뒤집힌 과부하 — 과부하가 쌓인 카드일수록 그 몬스터가 강하게 나온다.</summary>
    OverloadFrenzy = 33,

    /// <summary>봉인된 칸 — 빈 카드 칸 하나를 없애고 모든 몬스터가 강해진다.</summary>
    SealedSlot = 34,

    /// <summary>기다림의 미학 — 판이 열린 뒤 늦게 나올수록 강하다.</summary>
    Patience = 35,

    /// <summary>유리 성채 — 마왕성 최대 체력을 내주고 소환력을 받는다.</summary>
    GlassKeep = 36,

    /// <summary>저주받은 금화 — 런 골드가 크게 늘고, 판을 넘길 때마다 마왕성이 깎인다.</summary>
    CursedGold = 37,

    /// <summary>쌍둥이 라인 — 카드를 내면 옆 라인에도 절반이 선다. 대신 비싸다.</summary>
    TwinLane = 38,

    /// <summary>한 우물 — 덱 전체가 한 시너지 표식을 공유하면 그 카운트가 오른다.</summary>
    SingleWell = 39,

    /// <summary>매복 — 판 시작 몇 초간 배출을 멈추고, 그 뒤 첫 무리가 강하게 나온다.</summary>
    Ambush = 40,

    // ── 마나를 힘으로 (2026-09-12, 사용자 지시) ──
    //   최대 마나가 넉넉해진 만큼 "마나를 쌓고 아낄 이유" 를 준다.

    /// <summary>넘치는 그릇 — 몬스터가 나오는 순간 보유 마나 10당 그 몬스터 공/체 +5%.</summary>
    Overflow = 41,

    /// <summary>마력 결정화 — 판을 넘길 때 남은 마나의 10%(내림)가 최대 마나로 쌓인다 (최대 +30).</summary>
    Crystallize = 42,

    /// <summary>강적의 정수 — 엘리트·보스 용사를 쓰러뜨릴 때마다 마나 +5.</summary>
    TrophyMana = 43,
}

/// <summary>
/// 특성의 수치·이름 — <b>모든 숫자의 정본</b>. 밸런싱 대상이라 한곳에 모은다.
///
/// ⚠ 중첩되지 않는다
///   같은 특성을 두 번 얻는 일은 없다 (RunPerkData.Add 가 막는다).
///   중첩을 허용하면 "과잉 소환 3중첩" 같은 조합이 라인 배출을 0에 수렴시킨다.
/// </summary>
public static class RunPerkRule
{
    // ══════════════════════════════════════════════════════════
    //  수치
    // ══════════════════════════════════════════════════════════

    // ── 마나 ────────────────────────────────────────────────

    /// <summary>친화 할인 — 친화 종족에서 깎는 마나.</summary>
    public const float CheapAffinityCut = 2f;

    /// <summary>만물 할인 — 모든 몬스터에서 깎는 마나.</summary>
    public const float CheapAllCut = 1f;

    /// <summary>깊은 그릇 — 최대 마나 배율.</summary>
    public const float DeepVesselMult = 1.25f;

    /// <summary>명상 — 스테이지 회복량 배율.</summary>
    public const float MeditationMult = 1.20f;

    /// <summary>비축 — 한 방울도 안 쓰고 판을 넘겼을 때 늘어나는 최대 마나.</summary>
    public const float HoardGain = 10f;

    /// <summary>비축 — 누적 상한. 없으면 아끼기만 하는 판이 최적해가 된다.</summary>
    public const float HoardCeiling = 50f;

    /// <summary>저울 — 잔량이 이 비율 이하면 싸진다.</summary>
    public const float ScalesThreshold = 0.5f;

    /// <summary>저울 — 잔량이 넉넉할 때 붙는 할증.</summary>
    public const float ScalesSurcharge = 1f;

    /// <summary>저울 — 잔량이 바닥일 때 깎이는 마나.</summary>
    public const float ScalesCut = 2f;

    /// <summary>마력 폭주 — 잔량이 0 에 닿은 판의 소환력 배율.</summary>
    public const float ManaSurgeMult = 1.30f;

    // ── 대기열 · 소환 ───────────────────────────────────────

    /// <summary>과잉 소환 — 라인 배출 간격 배율 (0.6 = 40% 단축).</summary>
    public const float DrainIntervalMultiplier = 0.6f;

    /// <summary>선발대 — 이 마릿수까지는 거의 즉시 나온다 (스테이지마다 리셋).</summary>
    public const int VanguardCount = 10;

    /// <summary>선발대 — 그 구간의 배출 간격 배율 (0.1 = 90% 단축).</summary>
    public const float VanguardIntervalMultiplier = 0.1f;

    /// <summary>증원 — 카드 한 번에 늘어나는 마릿수.</summary>
    public const int ReinforceCount = 1;

    /// <summary>친화 증원 — 친화 종족에서 늘어나는 마릿수.</summary>
    public const int AffinityReinforceCount = 2;

    /// <summary>원군 — 판이 열린 뒤 부를 때 붙는 할증.</summary>
    public const float ReinforcementsSurcharge = 2f;

    /// <summary>원군 — 판이 열린 뒤 부른 개체의 공/체 배율.</summary>
    public const float ReinforcementsStatMult = 2f;

    // ── 시너지 ──────────────────────────────────────────────

    /// <summary>공명 — 활성 시너지 하나당 공/체 비율.</summary>
    public const float ResonancePerSynergy = 0.03f;

    /// <summary>극단 — 시너지 종류가 이 수 이하일 때 발동한다.</summary>
    public const int ExtremityMaxSynergies = 3;

    /// <summary>극단 — 시너지 스탯 효과 배율.</summary>
    public const float ExtremityMult = 1.5f;

    /// <summary>편중 — 최고 카운트 시너지에 얹는 카운트.</summary>
    public const int WeightedCount = 1;

    // ── 친화 ────────────────────────────────────────────────

    /// <summary>심연 공명 — 친화 종족 소환력 배율 (기본 1.2 를 대체한다).</summary>
    public const float DeepChannelAffinity = 1.6f;

    /// <summary>심연 공명 — 비친화 종족 소환력 배율 (기본 1.0 을 대체한다).</summary>
    public const float DeepChannelNeutral = 0.8f;

    // ── 스탯 · 라인 ─────────────────────────────────────────

    /// <summary>대기만성 — 스테이지 하나당 공/체 비율.</summary>
    public const float LateBloomPerStage = 0.015f;

    /// <summary>대기만성 — 최대 중첩.</summary>
    public const int LateBloomMaxStacks = 30;

    /// <summary>소수정예 — 가장 적은 라인의 공/체 비율.</summary>
    public const float FewButEliteBonus = 1.0f;

    /// <summary>군세 — 같은 라인 몬스터 1마리당 공/체 비율.</summary>
    public const float HordePerAlly = 0.001f;

    /// <summary>주공 — 중앙 라인의 공/체 비율.</summary>
    public const float CenterPushBonus = 0.35f;

    /// <summary>측면 — 바깥 라인의 이동속도 비율.</summary>
    public const float FlankMoveBonus = 0.30f;

    /// <summary>측면 — 바깥 라인의 공격력 비율.</summary>
    public const float FlankAttackBonus = 0.15f;

    /// <summary>파쇄 — 착탄마다 더해지는 대상 최대 체력 비율.</summary>
    public const float RendMaxHpRatio = 0.02f;

    /// <summary>파쇄 — 추가 피해 상한 (공격력의 몇 배).</summary>
    public const float RendCapAttackMult = 3f;

    /// <summary>백귀 — 덱에 든 종족 1종당 공/체 비율.</summary>
    public const float MenageriePerSpecies = 0.05f;

    // ── 카드 · 덱 · 그 외 ───────────────────────────────────

    /// <summary>확장 편성 — 늘어나는 카드 칸 수. 상한 <see cref="MaxDeckSlots"/> 에서 잘린다.</summary>
    /// <remarks>
    /// ⚠ 기본 칸은 소환사마다 4~6 이고 유물 '전열 확장' 이 최대 +2 를 얹는다 (2026-09-11).
    ///   상한(8)에 가까운 소환사일수록 이 특성의 값이 작다 — 상한이면 후보에서 빠진다.
    ///   7종 계열 시너지의 금 문턱이 7 이므로, 대부분에게 이 특성이 곧 "금까지 갈 수 있는가" 다.
    /// </remarks>
    public const int ExtraSlotCount = 2;

    /// <summary>
    /// 덱 칸의 상한.
    /// ⚠ 화면이 8칸까지만 담는다 — 강화소·제단 격자(CardPickPopupBase.MaxCells) ·
    ///   전황 아군 4×2. 올리려면 그 화면들부터 넓힐 것. 넘치면 조용히 잘린다.
    /// </summary>
    public const int MaxDeckSlots = 8;

    /// <summary>감식안 — 카드 3택에 더해지는 선택지 수.</summary>
    public const int AppraisalExtraChoices = 1;

    /// <summary>속성 — 새 카드가 들어올 때의 장수 (= 레벨).</summary>
    public const int QuickStudyCopies = 2;

    /// <summary>집중 — 시그니처 스킬의 스테이지당 사용 횟수 증가분.</summary>
    public const int FocusExtraUses = 1;

    /// <summary>귀환 — 죽은 몬스터가 대기열로 돌아갈 확률.</summary>
    public const float HomecomingChance = 0.30f;

    // ── 대가를 치르는 특성 (2026-09-11) ─────────────────────

    /// <summary>피의 계약 — 모자란 마나 1 을 마왕성 체력 몇으로 치르나.</summary>
    public const int BloodPactHpPerMana = 1;

    /// <summary>뒤집힌 과부하 — 과부하 1단계당 공/체 비율.</summary>
    public const float OverloadFrenzyPerStack = 0.12f;

    /// <summary>봉인된 칸 — 없애는 칸 수 · 모든 몬스터 공/체 비율.</summary>
    public const int   SealedSlotCount = 1;
    public const float SealedSlotBonus = 0.20f;

    /// <summary>기다림의 미학 — 판이 열린 뒤 1초당 공/체 비율 · 상한.</summary>
    public const float PatiencePerSecond = 0.04f;
    public const float PatienceMax       = 0.40f;

    /// <summary>유리 성채 — 얻는 순간 깎이는 마왕성 최대 체력 비율 · 소환력 배율.</summary>
    public const float GlassKeepCoreCut   = 0.40f;
    public const float GlassKeepPowerMult = 1.50f;

    /// <summary>저주받은 금화 — 런 골드 획득 비율 · 판을 넘길 때 깎이는 마왕성 체력.</summary>
    public const float CursedGoldBonus    = 0.60f;
    public const int   CursedGoldCoreCost = 3;

    /// <summary>쌍둥이 라인 — 붙는 할증.</summary>
    public const float TwinLaneSurcharge = 2f;

    /// <summary>한 우물 — 공유 표식에 얹는 카운트.</summary>
    public const int SingleWellCount = 2;

    /// <summary>매복 — 판 시작 뒤 배출을 멈추는 시간(초) · 라인마다 강해지는 앞 마릿수 · 공/체 비율.</summary>
    public const float AmbushHoldSeconds = 5f;
    public const int   AmbushCount       = 5;
    public const float AmbushBonus       = 0.60f;

    // ── 마나를 힘으로 (2026-09-12) ──────────────────────────

    /// <summary>넘치는 그릇 — 나오는 순간의 보유 마나 10당 공/체 비율.</summary>
    public const float OverflowPer10Mana = 0.05f;

    /// <summary>마력 결정화 — 판을 넘길 때 남은 마나 중 최대 마나로 굳는 비율 · 누적 상한.</summary>
    public const float CrystallizeRatio   = 0.10f;
    public const float CrystallizeCeiling = 30f;

    /// <summary>강적의 정수 — 엘리트·보스 한 기당 돌려받는 마나.</summary>
    public const int TrophyManaAmount = 5;

    // ══════════════════════════════════════════════════════════
    //  조회 — 훅 지점이 부르는 것
    //
    //  ⚠ 여기 함수들은 특성이 없을 때 **아무것도 바꾸지 않은 값**을 돌려준다.
    //    부르는 쪽이 Has() 를 먼저 확인할 필요가 없게 하려는 것이다.
    // ══════════════════════════════════════════════════════════

    /// <summary>지금 런의 특성 목록. 세이브가 없으면 null.</summary>
    public static RunPerkData Data => UserDataManager.Instance?.Get<RunPerkData>();

    public static bool Has(RunPerk perk)
    {
        RunPerkData data = Data;
        return data != null && data.Has(perk);
    }

    // ── 마나 ────────────────────────────────────────────────

    /// <summary>
    /// 특성이 얹은 마나 비용 증감. <b>더할 값</b>을 돌려준다 (음수 = 할인).
    ///
    /// ⚠ 하한은 부르는 쪽이 건다 (SummonerPerkRuntime.ManaCostFor 가 Max(1f) 한다).
    ///   여기서 자르면 개성 할인과 순서가 얽혀 어느 쪽이 하한을 만들었는지 흐려진다.
    /// </summary>
    public static float ManaCostDelta(SummonerData summoner, MonsterSpeciesData species)
    {
        RunPerkData data = Data;
        if (data == null) return 0f;

        float delta = 0f;

        if (data.Has(RunPerk.CheapAll)) delta -= CheapAllCut;

        // 유물 '절약' — 특성 할인과 같은 통에 담는다 (반올림 전에 한 번만 뺀다).
        delta -= RelicTreeApplier.GetSystemValue(RelicSystemEffect.SummonCostCut);

        if (data.Has(RunPerk.CheapAffinity) && IsAffinity(summoner, species))
            delta -= CheapAffinityCut;

        // 저울 — 잔량을 보고 갈린다. 아낄수록 비싸지므로 비축·이자와 정반대 축이다.
        if (data.Has(RunPerk.Scales))
        {
            var mana = UserDataManager.Instance?.Get<SummonManaData>();

            if (mana != null && mana.Max > 0f)
                delta += mana.Current / mana.Max <= ScalesThreshold
                       ? -ScalesCut
                       : ScalesSurcharge;
        }

        // 원군 — 판이 열린 뒤에 부르면 비싸다. 그 대가는 스탯으로 돌려준다.
        if (data.Has(RunPerk.Reinforcements) && IsAfterStageStart())
            delta += ReinforcementsSurcharge;

        // 쌍둥이 라인 — 옆 라인에 절반이 더 서는 값이다 (SummonController.UseMonsterCard).
        if (data.Has(RunPerk.TwinLane)) delta += TwinLaneSurcharge;

        return delta;
    }

    /// <summary>특성까지 반영한 마나 그릇.</summary>
    /// <summary>
    /// ⚠ 유물 '넓은 그릇' 계열이 여기에 함께 곱해진다 (2026-09-07)
    ///   그릇을 묻는 곳은 전부 이 함수를 지난다 — 다른 데서 또 곱하지 말 것.
    /// </summary>
    public static float MaxManaFor(SummonerData summoner)
    {
        if (summoner == null) return 0f;

        RunPerkData data = Data;
        if (data == null) return summoner.MaxMana;

        float max = summoner.MaxMana;

        if (data.Has(RunPerk.DeepVessel)) max *= DeepVesselMult;

        // 유물 '넓은 그릇' 계열 — 특성 배율과 **같은 자리**에서 곱한다.
        // ⚠ 아래 가산(비축·상점) 앞이어야 한다. 뒤에 두면 그 몫까지 부풀린다.
        max *= 1f + RelicTreeApplier.GetSystemValue(RelicSystemEffect.ManaCapacityBonus);

        // 비축·상점으로 쌓인 몫은 배율 **뒤**에 더한다 — 앞에 두면 깊은 그릇이
        // 그 몫까지 부풀려 둘이 곱해진다.
        max += data.HoardBonus;
        max += data.CrystalBonus;   // 특성 '마력 결정화'

        // 상점 정수 · 야영지 증축·이벤트·결정술사가 쌓은 몫 (RunBoonData 가 저장고다)
        var boon = UserDataManager.Instance?.Get<RunBoonData>();
        if (boon != null) max += boon.MaxManaBonus + boon.ExtraMaxMana + boon.CrystalMaxMana;

        // ── 내림 — **그릇도 정수다** (사용자 지적, 2026-09-12) ──
        //
        //  ⚠ 여기가 마나에 소수점이 생기던 마지막 구멍이었다
        //    잔량은 ManaRegenRule.RawFor 가 이미 내림으로 막고 있었는데, 그릇은
        //    위의 **배율들**(깊은 그릇 ×1.25 · 유물 넓은 그릇)을 지나며 103.5 같은
        //    값이 됐다. 화면이 "84 / 103.5" 로 뜬 것이 그것이다.
        //
        //  ⚠ 내림이라야 화면과 실제가 같다 — 올리면 닿을 수 없는 칸이 생긴다
        //    회복은 Max 에서 잘리므로(SummonManaData.RegenForStage), 올림으로
        //    적어 두면 "104 인데 아무리 채워도 103.5 에서 멈추는" 그릇이 된다.
        return Mathf.Floor(max);
    }

    /// <summary>특성까지 반영한 스테이지 회복량.</summary>
    public static float RegenFor(float baseRegen)
        => Has(RunPerk.Meditation) ? baseRegen * MeditationMult : baseRegen;

    // ── 배출 ────────────────────────────────────────────────

    /// <summary>
    /// 배출 간격에 <b>곱할</b> 특성 배율. 1 이면 특성이 없는 것과 같다.
    ///
    /// 선발대는 스테이지마다 앞 <see cref="VanguardCount"/> 마리에만 걸린다 —
    /// 그 뒤로는 과잉 소환이 이어받으므로 둘이 서로를 잡아먹지 않는다.
    ///
    /// ⚠ 간격 자체를 여기서 만들지 않는다 (2026-09-07)
    ///   기본 간격은 이제 <b>종족마다 다르다</b> (SpawnPaceRule). 여기서 초를
    ///   돌려주면 특성을 켠 순간 모든 종족이 같은 속도로 나와 그 축이 통째로
    ///   무효가 된다. 특성은 "몇 배 빠르게" 만 말한다.
    /// </summary>
    public static float DrainMultiplierFor(int spawnedThisStage)
    {
        RunPerkData data = Data;
        if (data == null) return 1f;

        float mult = 1f;

        if (data.Has(RunPerk.RapidDrain)) mult *= DrainIntervalMultiplier;

        if (data.Has(RunPerk.Vanguard) && spawnedThisStage < VanguardCount)
            mult *= VanguardIntervalMultiplier;

        // 유물 '부름의 나팔'·'성문 개방' — 특성 배율 위에 함께 곱한다.
        // ⚠ 0 이하로 내려가면 배출이 한 프레임에 다 쏟아진다.
        mult *= Mathf.Max(0.1f,
                          1f - RelicTreeApplier.GetSystemValue(RelicSystemEffect.DrainSpeedBonus));

        return mult;
    }

    /// <summary>카드 한 번에 줄에 서는 마릿수.</summary>
    public static int SummonCountFor(SummonerData summoner, MonsterSpeciesData species)
    {
        int count = species.SummonCount;

        // 소환사 개성 '뼈의 군단'(스컬 킹) — 친화 종족 카드의 마릿수.
        // ⚠ 여기가 마릿수의 단일 관문이다 — 카드 표시(SummonCardUI)와 실제 소환이 같은 값을 본다.
        // 오크 킹 '총동원'(2026-09-12)도 같은 규칙이다 — 값만 다르다.
        if (summoner != null
            && (summoner.Perk == SummonerPerk.BoneLegion || summoner.Perk == SummonerPerk.Muster)
            && IsAffinity(summoner, species))
            count += Mathf.RoundToInt(summoner.PerkValue);

        RunPerkData data = Data;
        if (data == null) return count;

        if (data.Has(RunPerk.Reinforce)) count += ReinforceCount;

        if (data.Has(RunPerk.AffinityReinforce) && IsAffinity(summoner, species))
            count += AffinityReinforceCount;

        // 유물 '증원의 인장'
        count += RelicTreeApplier.GetSystemInt(RelicSystemEffect.SummonCountBonus);

        return Mathf.Max(1, count);
    }

    // ── 친화 ────────────────────────────────────────────────

    /// <summary>
    /// 특성까지 반영한 친화 판정.
    ///
    /// '친화 확장' 은 덱의 종족 하나를 친화로 끌어올린다 — 그 대상은
    /// RunPerkData 가 들고 있다(획득 시점에 정해져 세이브에 남는다).
    /// </summary>
    public static bool IsAffinity(SummonerData summoner, MonsterSpeciesData species)
    {
        if (summoner == null || species == null) return false;
        if (summoner.IsAffinity(species))        return true;

        RunPerkData data = Data;
        if (data == null || !data.Has(RunPerk.AffinityExpand)) return false;

        string extra = data.ExpandedAffinityId;
        if (string.IsNullOrEmpty(extra)) return false;

        // ⚠ 계보를 따라 올라간다 — 소환사 친화(SummonerAffinity.Matches)와 같은 규칙이다.
        //   업그레이드 종족만 예외가 되면 "진화시켰더니 친화가 풀렸다" 가 된다.
        return species.Id == extra || species.RootSpecies.Id == extra;
    }

    /// <summary>특성까지 반영한 소환력 배율.</summary>
    public static float AffinityMultFor(SummonerData summoner, MonsterSpeciesData species)
    {
        bool affinity = IsAffinity(summoner, species);

        if (Has(RunPerk.DeepChannel))
            return affinity ? DeepChannelAffinity : DeepChannelNeutral;

        return affinity
            ? SummonerAffinityRule.AffinitySummonPower
            : SummonerAffinityRule.NeutralSummonPower;
    }

    // ── 시너지 ──────────────────────────────────────────────

    /// <summary>
    /// 시너지 **스탯** 효과에 곱하는 배율 (극단).
    ///
    /// ⚠ 스탯 항목에만 걸린다
    ///   부활 확률·넉백 면역 같은 비스탯 효과는 단계로만 갈리므로 여기서 못 만진다.
    ///   그쪽까지 키우려면 단계 자체를 올려야 하는데, 그러면 전투력지수가 깨진다.
    /// </summary>
    public static float SynergyStatMult
        => Has(RunPerk.Extremity) &&
           MonsterSynergyRule.ActiveCount <= ExtremityMaxSynergies
            ? ExtremityMult
            : 1f;

    // ── 스탯 ────────────────────────────────────────────────

    /// <summary>대기만성 누적 배율. 스테이지 1 에서는 1 이다.</summary>
    public static float LateBloomMult
    {
        get
        {
            if (!Has(RunPerk.LateBloom)) return 1f;

            var director = StageLoopDirector.Instance;
            int stacks   = director != null ? director.StageNumber - 1 : 0;

            return 1f + LateBloomPerStage * Mathf.Clamp(stacks, 0, LateBloomMaxStacks);
        }
    }

    /// <summary>백귀 — 덱에 든 몬스터 종족 수만큼 붙는 배율.</summary>
    public static float MenagerieMult
    {
        get
        {
            if (!Has(RunPerk.Menagerie)) return 1f;

            var deck = UserDataManager.Instance?.Get<SummonDeckData>();
            if (deck == null) return 1f;

            int species = 0;
            for (int i = 0; i < deck.SlotCount; i++)
            {
                SummonDeckSlot slot = deck.GetSlot(i);
                if (!slot.IsEmpty && slot.Kind == SummonKind.Monster) species++;
            }

            return 1f + MenageriePerSpecies * species;
        }
    }

    /// <summary>
    /// 소환력에 곱하는 특성 배율 — 마력 폭주(이번 판에 잔량이 0 에 닿았나) · 유리 성채.
    /// ⚠ 소환력의 단일 관문은 MonsterStatComposer ⓪ 이다 — 다른 데서 또 곱하지 말 것.
    /// </summary>
    public static float SummonPowerMult
    {
        get
        {
            RunPerkData data = Data;
            if (data == null) return 1f;

            float mult = data.Has(RunPerk.ManaSurge) && data.ManaEmptiedThisStage ? ManaSurgeMult : 1f;

            // 유리 성채 — 마왕성을 내주고 받은 소환력. 성은 얻는 순간 깎였다 (RunPerkData.Add).
            if (data.Has(RunPerk.GlassKeep)) mult *= GlassKeepPowerMult;

            return mult;
        }
    }

    // ── 덱 칸 ───────────────────────────────────────────────

    /// <summary>
    /// 런을 시작할 때의 카드 칸 수 — 소환사 + 유물 '전열 확장'(StartDeckSlots) + 특성.
    ///
    /// ⚠ 새 런과 이어하기(늘리기만)가 이 값을 쓴다 (RunBootstrap.BuildStarterDeck).
    ///   런 도중에 특성을 얻으면 RunPerkData.Add 가 그 자리에서 늘리고 줄인다.
    /// ⚠ 확장 편성은 상한에서 자르고, 봉인된 칸은 그 **뒤에** 뺀다 —
    ///   어느 순서로 얻어도 같은 칸 수가 나온다.
    /// </summary>
    public static int DeckSlotsFor(SummonerData summoner)
    {
        int slots = summoner.StartDeckSlots;

        RunPerkData data = Data;
        if (data != null)
        {
            if (data.Has(RunPerk.ExtraSlots)) slots = Mathf.Min(slots + ExtraSlotCount, MaxDeckSlots);
            if (data.Has(RunPerk.SealedSlot)) slots -= SealedSlotCount;
        }

        return Mathf.Max(1, slots);
    }

    // ── 대가를 치르는 특성 ──────────────────────────────────

    /// <summary>이번 판이 열린 시각 (Time.time). SummonController.HandleStageStart 가 적는다.</summary>
    public static float StageStartedAt { get; private set; } = -1f;

    /// <summary>판이 열렸다 — 매복·기다림의 미학이 여기서부터 잰다.</summary>
    public static void NoteStageStart() => StageStartedAt = Time.time;

    /// <summary>
    /// 판이 열린 뒤 흐른 시간. ⚠ Time.time 이라 배속·일시정지를 그대로 탄다 — 게임 속 시간이다.
    /// </summary>
    static float SecondsSinceStageStart
        => StageStartedAt < 0f ? 0f : Mathf.Max(0f, Time.time - StageStartedAt);

    /// <summary>
    /// 매복 — 배출을 더 멈춰야 하는 시간. 판 시작에서 재므로 판 도중에 새로 연 라인도
    /// 남은 시간만큼만 기다린다.
    /// </summary>
    public static float AmbushHoldRemaining
        => Has(RunPerk.Ambush) && StageStartedAt >= 0f
            ? Mathf.Max(0f, AmbushHoldSeconds - SecondsSinceStageStart)
            : 0f;

    /// <summary>
    /// 배출되는 순간에 정해지는 특성 배율 — 뒤집힌 과부하 · 기다림의 미학 · 매복.
    /// SummonController 가 카드 몬스터를 뱉을 때 계산해 스폰에 넘긴다 (MonsterStatComposer ⑥).
    ///
    /// ⚠ 셋 다 "그 순간" 을 본다 — 필드에 이미 선 개체는 바뀌지 않는다 (나올 때 정해진다).
    /// ⚠ 카드 몬스터만 받는다 — 분열체·스킬 소환은 이 경로를 지나지 않는다.
    /// </summary>
    /// <param name="spawnedThisStage">그 라인이 이번 판에 이미 뱉은 마릿수 (이 개체 앞까지).</param>
    public static float DrainStatMult(MonsterSpeciesData species, int spawnedThisStage)
    {
        RunPerkData data = Data;
        if (data == null) return 1f;

        float mult = 1f;

        // 뒤집힌 과부하 — 도배할수록 비싸지지만 그만큼 세다. 과부하는 카드 ID 로 센다(= 종족 ID).
        if (data.Has(RunPerk.OverloadFrenzy))
            mult *= 1f + OverloadFrenzyPerStack * SummonCostRule.OverloadStacks(species.Id);

        // 기다림의 미학 — 느린 배출·긴 줄이 손해가 아니게 된다.
        if (data.Has(RunPerk.Patience))
            mult *= 1f + Mathf.Min(PatienceMax, PatiencePerSecond * SecondsSinceStageStart);

        // 매복 — 멈춰 있던 뒤 라인마다 앞 몇 마리.
        if (data.Has(RunPerk.Ambush) && spawnedThisStage < AmbushCount)
            mult *= 1f + AmbushBonus;

        // 넘치는 그릇 — 쥐고 있는 마나가 그대로 힘이 된다. "쓸 것인가 쥐고 있을 것인가" 가 선택이 된다.
        if (data.Has(RunPerk.Overflow))
        {
            var mana = UserDataManager.Instance?.Get<SummonManaData>();
            if (mana != null) mult *= 1f + OverflowPer10Mana * Mathf.Floor(mana.Current / 10f);
        }

        return mult;
    }

    /// <summary>
    /// 피의 계약 — 마나가 모자라도 이 값을 낼 수 있는가 (모자란 몫을 마왕성 체력으로).
    ///
    /// ⚠ 마왕성이 1 이상 남을 때만 — "골랐더니 런이 끝났다" 를 만들지 않는다 (RunCoreData.Pay 와 같은 규칙).
    /// ⚠ 카드 바의 '쓸 수 있나'(SummonDeckUI)와 실제 소모(SummonController)가 같은 함수를 본다.
    /// </summary>
    public static bool CanPayWithBlood(float currentMana, int cost)
    {
        if (!Has(RunPerk.BloodPact)) return false;

        var core = UserDataManager.Instance?.Get<RunCoreData>();
        if (core == null) return false;

        int shortfall = cost - Mathf.FloorToInt(currentMana);
        return shortfall > 0 && core.Current - shortfall * BloodPactHpPerMana >= 1;
    }

    /// <summary>피의 계약으로 낸다 — 가진 마나를 전부 내고 모자란 몫을 체력으로. 못 내면 false.</summary>
    public static bool TryPayWithBlood(SummonManaData mana, int cost)
    {
        if (!CanPayWithBlood(mana.Current, cost)) return false;

        int have      = Mathf.FloorToInt(mana.Current);
        int shortfall = cost - have;

        if (have > 0) mana.Spend(have);
        UserDataManager.Instance.Get<RunCoreData>().Pay(shortfall * BloodPactHpPerMana);
        return true;
    }

    // ── 마나를 주는 곳 (2026-09-12) ─────────────────────────
    //
    //  ⚠ 판 도중·시설에서 마나를 주는 길은 전부 이 둘을 지난다
    //    그릇 맞추기(SetMax)와 채우기(Restore)를 따로 부르면 순서를 틀려
    //    "최대 마나는 늘었는데 비어 있다" 가 된다.

    /// <summary>잔량을 채운다 — 그릇을 넘지 않고 정수로만 (SummonManaData.Restore).</summary>
    public static void RestoreMana(float amount)
        => UserDataManager.Instance?.Get<SummonManaData>()?.Restore(amount);

    /// <summary>
    /// 최대 마나를 런 동안 늘리고 **늘어난 만큼 채운다** — 야영지 증축·이벤트가 부른다
    /// (마왕성 최대 체력 증가와 같은 규칙: "늘어난 만큼 곧바로 채워진다").
    /// </summary>
    public static void GrowMaxMana(int amount)
    {
        if (amount <= 0) return;

        UserDataManager.Instance.Get<RunBoonData>().AddExtraMaxMana(amount);
        SyncMaxMana();
        RestoreMana(amount);
    }

    /// <summary>그릇을 지금 규칙대로 다시 맞춘다. 소환사가 아직 없으면 다음 판 경계가 맞춘다.</summary>
    public static void SyncMaxMana()
    {
        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;
        if (summoner == null) return;

        UserDataManager.Instance.Get<SummonManaData>().SetMax(MaxManaFor(summoner));
    }

    /// <summary>
    /// 용사를 쓰러뜨렸다 — 특성 '강적의 정수'.
    /// UnitDeathDespawnSystem 이 골드와 **같은 자리**에서 부른다 (등급을 알 수 있는 마지막 지점).
    /// </summary>
    public static void OnHeroKilled(bool isBoss, bool isElite)
    {
        if (!(isBoss || isElite) || !Has(RunPerk.TrophyMana)) return;
        RestoreMana(TrophyManaAmount);
    }

    /// <summary>
    /// 쌍둥이 라인 — 옆에 설 라인 (오른쪽 끝이면 왼쪽). 특성이 없거나 절반이 0 이면 −1.
    /// ⚠ 절반은 내림이다 — 한 마리짜리 카드(트롤)는 옆에 아무것도 안 선다. 그게 그 카드의 대가다.
    /// </summary>
    public static int TwinLaneOf(int lane, int count)
    {
        if (!Has(RunPerk.TwinLane) || count / 2 < 1) return -1;

        int lanes = SummonFieldLayout.LaneCount;
        return lane + 1 < lanes ? lane + 1 : lane - 1;
    }

    // ── 스테이지 상태 ───────────────────────────────────────

    /// <summary>판이 이미 열렸는가 (대기 시간이 끝나 적이 밀려오는 중).</summary>
    public static bool IsAfterStageStart()
    {
        var director = StageLoopDirector.Instance;
        return director != null && !director.IsStageReady;
    }

    // ══════════════════════════════════════════════════════════
    //  표시
    // ══════════════════════════════════════════════════════════

    public static string ToKorean(this RunPerk perk) => perk switch
    {
        RunPerk.FreeFirstSummon   => "첫 소환 무료",
        RunPerk.RapidDrain        => "과잉 소환",
        RunPerk.ExtraSlots        => "확장 편성",
        RunPerk.CheapAffinity     => "친화 할인",
        RunPerk.CheapAll          => "만물 할인",
        RunPerk.DeepVessel        => "깊은 그릇",
        RunPerk.Meditation        => "명상",
        RunPerk.Hoard             => "비축",
        RunPerk.Scales            => "저울",
        RunPerk.ManaSurge         => "마력 폭주",
        RunPerk.Reinforce         => "증원",
        RunPerk.AffinityReinforce => "친화 증원",
        RunPerk.Vanguard          => "선발대",
        RunPerk.Reinforcements    => "원군",
        RunPerk.Resonance         => "공명",
        RunPerk.Extremity         => "극단",
        RunPerk.Weighted          => "편중",
        RunPerk.DeepChannel       => "심연 공명",
        RunPerk.AffinityExpand    => "친화 확장",
        RunPerk.LateBloom         => "대기만성",
        RunPerk.FewButElite       => "소수정예",
        RunPerk.Horde             => "군세",
        RunPerk.CenterPush        => "주공",
        RunPerk.Flank             => "측면",
        RunPerk.Rend              => "파쇄",
        RunPerk.Menagerie         => "백귀",
        RunPerk.Appraisal         => "감식안",
        RunPerk.QuickStudy        => "속성",
        RunPerk.Focus             => "집중",
        RunPerk.Homecoming        => "귀환",
        RunPerk.BloodPact         => "피의 계약",
        RunPerk.OverloadFrenzy    => "뒤집힌 과부하",
        RunPerk.SealedSlot        => "봉인된 칸",
        RunPerk.Patience          => "기다림의 미학",
        RunPerk.GlassKeep         => "유리 성채",
        RunPerk.CursedGold        => "저주받은 금화",
        RunPerk.TwinLane          => "쌍둥이 라인",
        RunPerk.SingleWell        => "한 우물",
        RunPerk.Ambush            => "매복",
        RunPerk.Overflow          => "넘치는 그릇",
        RunPerk.Crystallize       => "마력 결정화",
        RunPerk.TrophyMana        => "강적의 정수",
        _                         => "",
    };

    public static string Describe(this RunPerk perk) => perk switch
    {
        // ⚠ 설명은 "언제 · 무엇이 · 얼마나" 순으로 적는다 (사용자 지시, 2026-09-11)
        //   조건이 붙는 특성은 조건을 앞에, 값을 뒤에 둔다 — "잔량이 절반 이하면 비용 −2"
        //   처럼 무엇의 절반인지, 무슨 비용인지 흐리면 효과를 거꾸로 읽는다.
        //   ⚠ 소환할 때 굳는 효과(소수정예·군세·마력 폭주)는 그렇다고 적는다 —
        //     이미 필드에 선 몬스터는 안 바뀐다.
        RunPerk.FreeFirstSummon   => "스테이지마다 첫 몬스터 카드 1회는 마나를 쓰지 않는다",
        RunPerk.RapidDrain        => "라인에서 몬스터가 나오는 간격 −40%",
        RunPerk.ExtraSlots        => "카드 칸 +2 (최대 8칸)",
        RunPerk.CheapAffinity     => "친화 종족의 소환 비용 −2",
        RunPerk.CheapAll          => "모든 몬스터의 소환 비용 −1",
        RunPerk.DeepVessel        => "최대 마나 +25%",
        RunPerk.Meditation        => "스테이지마다 돌아오는 마나 +20%",
        RunPerk.Hoard             => "마나를 한 번도 쓰지 않고 스테이지를 넘기면 최대 마나 +10 (최대 +50)",
        RunPerk.Scales            => "보유 마나가 50% 이하면 소환 비용 −2, 50%를 넘으면 +1",
        RunPerk.ManaSurge         => "마나를 0까지 쓰면 그 스테이지 동안 새로 나오는 몬스터의 소환력 +30%",
        RunPerk.Reinforce         => "카드 한 장이 부르는 마릿수 +1",
        RunPerk.AffinityReinforce => "친화 종족 카드가 부르는 마릿수 +2",
        RunPerk.Vanguard          => "스테이지마다 라인별 처음 10마리는 나오는 간격 −90%",
        RunPerk.Reinforcements    => "전투가 시작된 뒤 소환하면 비용 +2, 그 몬스터의 공/체 +100%",
        RunPerk.Resonance         => "켜진 시너지 하나당 모든 몬스터 공/체 +3%",
        RunPerk.Extremity         => "켜진 시너지가 3종 이하면 시너지의 스탯 효과 +50%",
        RunPerk.Weighted          => "카운트가 가장 높은 시너지의 카운트 +1",
        RunPerk.DeepChannel       => "친화 종족 소환력 ×1.2 → ×1.6, 비친화 종족 ×1.0 → ×0.8",
        RunPerk.AffinityExpand    => "덱의 비친화 종족 하나가 친화가 된다",
        RunPerk.LateBloom         => "스테이지를 넘길 때마다 모든 몬스터 공/체 +1.5% (최대 +45%)",
        RunPerk.FewButElite       => "몬스터가 가장 적은 라인에 나오면 공/체 +100% (나올 때 정해짐)",
        RunPerk.Horde             => "나올 때 같은 라인에 선 몬스터 1마리당 공/체 +0.1%",
        RunPerk.CenterPush        => "가운데(3번) 라인 몬스터의 공/체 +35%",
        RunPerk.Flank             => "바깥(1·5번) 라인 몬스터의 이동속도 +30%, 공격력 +15%",
        RunPerk.Rend              => "공격이 맞을 때마다 대상 최대 체력의 2% 추가 피해 (공격력의 3배까지)",
        RunPerk.Menagerie         => "덱에 든 몬스터 종족 1종당 모든 몬스터 공/체 +5%",
        RunPerk.Appraisal         => "카드 보상 선택지 3개 → 4개",
        RunPerk.QuickStudy        => "새로 얻는 카드가 Lv2 로 들어온다",
        RunPerk.Focus             => "시그니처 스킬의 스테이지당 사용 횟수 +1",
        RunPerk.Homecoming        => "카드로 소환한 몬스터가 죽으면 30% 확률로 제 라인 대기열에 돌아간다",
        // ⚠ 아래 아홉은 숫자를 RunPerkRule 에서 뽑는다 — 밸런스를 고치면 문장이 따라온다
        RunPerk.BloodPact         => "마나가 모자라면 모자란 만큼 마왕성 체력으로 낸다 (체력 1 은 남긴다)",
        RunPerk.OverloadFrenzy    => $"과부하 1단계마다 그 카드로 나오는 몬스터 공/체 +{RunPerkRule.OverloadFrenzyPerStack * 100f:0}%",
        RunPerk.SealedSlot        => $"빈 카드 칸 하나를 없앤다. 모든 몬스터 공/체 +{RunPerkRule.SealedSlotBonus * 100f:0}%",
        RunPerk.Patience          => $"판이 열린 뒤 늦게 나올수록 1초당 공/체 +{RunPerkRule.PatiencePerSecond * 100f:0}% (최대 +{RunPerkRule.PatienceMax * 100f:0}%)",
        RunPerk.GlassKeep         => $"얻는 순간 마왕성 최대 체력 −{RunPerkRule.GlassKeepCoreCut * 100f:0}%, 소환력 +{(RunPerkRule.GlassKeepPowerMult - 1f) * 100f:0}%",
        RunPerk.CursedGold        => $"런 골드 획득 +{RunPerkRule.CursedGoldBonus * 100f:0}%, 스테이지를 넘길 때마다 마왕성 체력 −{RunPerkRule.CursedGoldCoreCost}",
        RunPerk.TwinLane          => $"카드를 내면 옆 라인에도 절반(내림)이 선다. 소환 비용 +{RunPerkRule.TwinLaneSurcharge:0}",
        RunPerk.SingleWell        => $"덱의 몬스터가 모두 같은 시너지 표식을 가지면 그 카운트 +{RunPerkRule.SingleWellCount}",
        RunPerk.Ambush            => $"판 시작 {RunPerkRule.AmbushHoldSeconds:0}초간 배출을 멈추고, 라인마다 처음 {RunPerkRule.AmbushCount}마리 공/체 +{RunPerkRule.AmbushBonus * 100f:0}%",
        RunPerk.Overflow          => $"몬스터가 나올 때 보유 마나 10당 그 몬스터 공/체 +{RunPerkRule.OverflowPer10Mana * 100f:0}% (나올 때 정해짐)",
        RunPerk.Crystallize       => $"스테이지를 넘길 때 남은 마나의 {RunPerkRule.CrystallizeRatio * 100f:0}%가 최대 마나로 쌓인다 (최대 +{RunPerkRule.CrystallizeCeiling:0})",
        RunPerk.TrophyMana        => $"엘리트·보스를 쓰러뜨릴 때마다 마나 +{RunPerkRule.TrophyManaAmount}",
        _                         => "",
    };
}
