// ============================================================
//  RelicEnums.cs
//  유물 트리가 스탯이 아닌 방식으로 건드리는 것들.
//
//  ■ 스탯 노드와 시스템 노드는 다른 길로 간다
//    스탯 노드는 UnitStat 에 얹히고(RelicTreeApplier.ApplyToMonsterStat),
//    시스템 노드는 각 규칙 클래스가 GetSystemValue 로 물어본다.
//    ⚠ 시스템 효과를 새로 만들면 **읽는 쪽도 반드시 함께 만들 것.**
//      안 그러면 화면에는 노드가 뜨는데 사거도 아무 일이 없다 —
//      실패가 조용해서 가장 찾기 어려운 종류의 버그가 된다.
//
//  ■ 값의 뜻
//    비율(0.10 = +10%)인지 절대값(1 = +1개)인지는 효과마다 다르다.
//    각 줄 주석이 정본이고, 읽는 쪽이 그 규칙대로 쓴다.
//
//  ⚠ 1~10 은 원작 잔재다 (2026-09-07)
//    이 게임에는 장수·병사·어빌리티·경험치가 없다. **새 노드에 쓰지 말 것.**
//    남긴 것은 아직 읽는 쪽이 있어서다 —
//      1·2·3 어빌리티 화면(AbilityPicker·AbilitySelectPopup)
//      9       장수 슬롯(RelicTreeApplier.GetTotalActiveGeneralSlots — 원작 상점·출전 화면)
//      4·7·8·10 은 새 트리도 쓴다 (런 골드 · 용사 체력/공격력 · 배속)
//    읽는 쪽이 하나도 없던 5·6·11·12 는 지웠다.
// ============================================================

public enum RelicSystemEffect
{
    None = 0,

    // ── 원작 잔재 (새 트리는 쓰지 않는다) ────────────────────
    AbilityRefreshCount    = 1,
    AbilityChoiceCount     = 2,
    AbilityAdvancedChance  = 3,
    GoldGainBonus          = 4,  // ← 이것만 살아 있다: 런 골드 +N%
    EnemyMaxHpReduction    = 7,  // ← 살아 있다: 용사 최대 체력 −N%
    EnemyAttackReduction   = 8,  // ← 살아 있다: 용사 공격력 −N%
    GeneralSlotBonus       = 9,
    BattleSpeedUnlock      = 10, // ← 살아 있다: 전투 배속 해금 +N단계

    // ══════════════════════════════════════════════════════
    //  이 게임의 효과 (2026-09-07)
    // ══════════════════════════════════════════════════════

    // ── 소환수 ──────────────────────────────────────────────

    /// <summary>종족 패시브 수치 +N% (SpeciesPassiveRule 이 곱한다).</summary>
    SpeciesPassivePower    = 20,

    /// <summary>
    /// 분열·재조립체의 스탯 배율에 **더하는 절대값** (0.07 = +0.07).
    /// ⚠ 비율이 아니다 — 0.5 짜리 분열 배율에 곱하면 체감이 없다.
    /// </summary>
    DerivedScaleBonus      = 21,

    /// <summary>사망 발동 종족 패시브(회복·역병·자폭)의 효과 +N%.</summary>
    DeathTriggerPower      = 22,

    /// <summary>런 종료 몬스터 장비 상자 +N개.</summary>
    GearBoxBonus           = 23,

    /// <summary>몬스터 장비가 주는 스탯 +N%.</summary>
    GearStatBonus          = 24,

    // ── 마왕성 ──────────────────────────────────────────────

    /// <summary>마왕성 최대 체력 +N (절대값).</summary>
    CoreHpBonus            = 30,

    /// <summary>소환사 평타의 최대 체력 비율 +N%p (0.02 = +2%p).</summary>
    SummonerStrikeBonus    = 31,

    /// <summary>야영지가 회복시키는 마왕성 체력 +N.</summary>
    CampHealBonus          = 32,

    /// <summary>용사 이동속도 −N%.</summary>
    EnemyMoveReduction     = 33,

    // ── 마나 ────────────────────────────────────────────────

    /// <summary>최대 마나 +N%.</summary>
    ManaCapacityBonus      = 40,

    /// <summary>소환 비용 −N (절대값, 반올림 전에 뺀다).</summary>
    SummonCostCut          = 41,

    /// <summary>스테이지 마나 회복량 +N%.</summary>
    ManaRegenBonus         = 42,

    /// <summary>과부하 계수 −N (절대값. 기본 0.10 에서 뺀다).</summary>
    OverloadRelief         = 43,

    // ── 통솔 ────────────────────────────────────────────────

    /// <summary>라인 배출 간격 −N%.</summary>
    DrainSpeedBonus        = 50,

    /// <summary>카드 한 장이 부르는 마릿수 +N.</summary>
    SummonCountBonus       = 51,

    /// <summary>
    /// 카드 3택의 선택지 +N개.
    ///
    /// ⚠ **쓰지 않는다** (사용자 확정, 2026-09-10) — 트리에 이 효과를 쓰는 노드가 없다.
    ///   특성 '감식안' 과 같은 축인데 `CardRewardPicker.MaxChoiceCount`(4)가 특성 몫만
    ///   계산에 넣어, 둘을 함께 들면 유물 쪽이 <b>조용히 무효</b>가 됐다.
    ///   같은 대상에 같은 값을 두 번 붙이는 것은 선택지가 아니다
    ///   (CLAUDE.md 'ShadowedByPerk' 와 같은 규칙).
    ///   ⚠ 읽는 쪽(CardRewardPicker)은 남겨 뒀다 — 값이 0 이라 아무 일도 하지 않는다.
    ///     새 노드에 다시 쓰려면 MaxChoiceCount 와 카드 칸 수부터 함께 늘릴 것.
    /// </summary>
    CardChoiceCount        = 52,

    /// <summary>특성 선택지 +N개 (엘리트·보스 보상).</summary>
    PerkChoiceCount        = 53,

    /// <summary>새로 받는 카드의 시작 장수 +N (1 = 2레벨로 들어온다).</summary>
    NewCardLevel           = 54,

    /// <summary>시너지 문턱 −N (동 단계만 내린다).</summary>
    SynergyStepCut         = 55,

    /// <summary>시너지 중첩 보너스 +N (0.02 = +2%p).</summary>
    SynergyStackBonus      = 56,

    /// <summary>환생 포인트 +N%.</summary>
    ReincarnPointBonus     = 57,

    /// <summary>
    /// 지속 피해(중독·화상·역병)의 초당 피해 +N%.
    ///
    /// ⚠ 거는 순간의 세기만 바꾼다 — 이미 걸려 있는 도트는 그대로다.
    ///   단일 관문은 <see cref="MonsterDotRule.Scale"/> 하나다.
    /// </summary>
    DotDamageBonus         = 59,

    /// <summary>
    /// 스테이지를 넘길 때마다 마왕성 체력 +N 회복.
    ///
    /// ⚠ 야영지 회복(CampHealBonus)과 다른 물건이다 — 그쪽은 갈림길에서 골라야
    ///   받고, 이건 판을 넘기기만 하면 저절로 붙는다.
    /// </summary>
    CoreRegenPerStage      = 60,

    /// <summary>
    /// 런 골드로 내는 <b>모든 값</b> −N% (상점·강화소·야영지 증축·리롤).
    ///
    /// ⚠ 값을 깎을 뿐 <b>보상은 안 깎는다</b> — 이벤트의 골드 갈래는
    ///   RunGoldRule.PriceUnit 을 보고, 이 효과는 RunGoldRule.Price 에만 걸린다.
    ///   둘을 한 함수로 합치지 말 것.
    /// ⚠ 영구 골드(품질 개선)에는 안 걸린다 — 그건 런 밖의 재화다.
    /// </summary>
    ShopPriceCut           = 58,

    /// <summary>
    /// 카드 칸 +N (절대값). 런을 시작할 때 소환사 칸에 더해진다 (SummonerData.StartDeckSlots).
    ///
    /// ⚠ 다시 넣었다 (사용자 지시, 2026-09-11) — 한때 "확장 편성의 존재 이유를 뺏는다" 로
    ///   되돌렸지만, 소환사마다 칸이 4~6 으로 갈리면서 좁은 소환사에게 영구 성장의 길이 필요해졌다.
    /// ⚠ 상한은 RunPerkRule.MaxDeckSlots(8) — 화면이 8칸까지만 담는다.
    /// </summary>
    DeckSlotBonus          = 61,

    /// <summary>
    /// 런을 시작할 때 <b>무작위 특성</b> +N개 (절대값).
    ///
    /// ⚠ 소환사가 선 **뒤**에 준다 (RunBootstrap.GrantStartingPerks) —
    ///   `RunPerkData.CollectMissing` 이 소환사 개성과 겹치는 특성을 걸러내고
    ///   (`ShadowedByPerk`), '친화 확장' 은 소환사가 없으면 대상을 못 고른다.
    /// ⚠ 이어하기에서는 주지 않는다 — 앱을 껐다 켜는 것이 특성 획득 수단이 된다.
    /// ⚠ 대가를 치르는 특성(유리 성채·봉인된 칸 등)도 그대로 뽑힌다 —
    ///   무작위라는 것이 이 노드의 값이자 위험이다.
    /// </summary>
    StartingPerkCount      = 62,
}
