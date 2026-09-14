// ============================================================
//  PassiveSkillType.cs
//  장군 패시브 스킬 종류 열거형
//
//  등급별 활성 슬롯 수 (PassiveSkillRoller.GetActiveSlotCount):
//    Normal / Uncommon = 1슬롯
//    Rare   / Unique   = 2슬롯
//    Epic              = 3슬롯
// ============================================================

public enum PassiveSkillType : byte
{
    None = 0,

    // ── 병사 강화 ──────────────────────────────────────────────
    ExtraSoldiers            = 1,   // 병사 수 +N명
    SoldierCombatBoost       = 2,   // 병사 공격력·이동속도 +X%
    SoldierHorde             = 3,   // 병사 수 +N명, 병사 공체 -X%
    VanguardAura             = 4,   // 병사 방어율 +X%

    // ── 교환 ──────────────────────────────────────────────────
    WeakGeneralStrongSoldier = 5,   // 제너럴 공체 -X%, 병사 공체 +Y%
    StrongGeneralWeakSoldier = 6,   // 병사 공체 -X%, 제너럴 공체 +Y%
    WeakGeneralMoreSoldiers  = 7,   // 제너럴 공체 -X%, 병사 수 +N명
    BerserkerPact            = 8,   // 전체 공이속 +X%, 방어율 -Y%

    // ── 제너럴 강화 ────────────────────────────────────────────
    GeneralCombatBoost       = 9,   // 제너럴 공격력·이동속도 +X%
    TitanGeneral             = 10,  // 제너럴 크기·공체 +X%, 공이속 -Y%
    CommanderFury            = 11,  // 제너럴 크리티컬 확률 +X%, 배율 +Y%

    // ── 시너지 ────────────────────────────────────────────────
    SoldierEmpowerGeneral    = 12,  // 병사 수 × X% → 제너럴 공체 증가
    UnityStrength            = 13,  // 병사 수 × X% → 제너럴 공체 추가 (SoldierEmpower와 수치 다름)
    SoldierDeathEmpower      = 14,  // 병사 사망 시 제너럴 공체 +X%, 공이속 +Y%
    SacrificeRitual          = 15,  // 병사 N명 희생 → 제너럴 공체 +X%

    // ── 조건부 ────────────────────────────────────────────────
    BloodPact                = 16,  // 제너럴 체력 낮을수록 공격력 최대 +X%
    IronWill                 = 17,  // 제너럴 체력 Y% 이하 시 공체 +X% (1회)
    LastStand                = 18,  // 병사 수 초기의 Y% 이하 시 남은 병사 공체 +X% (1회)

    // ── OnAttack 트리거 ─────────────────────────────────────
    VampiricStrike           = 19,  // 공격 시 준 피해의 X% 즉시 체력 회복
    StrengthStack            = 20,  // 연속 공격마다 공격력 N 누적 (최대 5스택)
    SoldierMorale            = 21,  // 공격 시 X% 확률로 병사 공격력 T초 버프

    // ── OnHit 트리거 ────────────────────────────────────────
    DefenseShield            = 22,  // 피격 시 방어율 +N% T초 버프
    QuickRecovery            = 23,  // 피격 시 최대체력의 X% 즉시 회복
    CounterStrike            = 24,  // 피격 시 X% 확률로 공격력 +N% T초 버프

    // ── OnEnemyKill 트리거 ───────────────────────────────────
    KillMomentum             = 25,  // 처치마다 이동속도 N 누적 (최대 5스택)
    KillEmpower              = 26,  // 처치마다 공격력 N 누적 (최대 5스택)
    KillHeal                 = 27,  // 처치 시 최대체력의 X% 즉시 회복
    SoldierVigor             = 28,  // 처치 시 병사 공격력 T초 버프

    // ── OnSoldierDeath 트리거 ────────────────────────────────
    SacrificeAbsorb          = 29,  // 병사 사망 시 사망 수 × N만큼 즉시 체력 회복

    // ── OnSkillUse 트리거 ────────────────────────────────────
    SkillAdrenaline          = 30,  // 스킬 사용 시 공격력·공격속도 T초 버프
    SkillInstinct            = 31,  // 스킬 사용 시 X% 확률로 즉시 최대체력의 Y% 회복
    SkillRally               = 32,  // 스킬 사용 시 병사 전체 공격력·이동속도 T초 버프

    // ── OnBattleStart 트리거 ─────────────────────────────────
    GoldenPower              = 33,  // 보유 골드 N당 공격력·최대체력 +X%
    SwiftAssault             = 34,  // 이동속도 증가분만큼 공격속도도 증가
    SteelBody                = 35,  // 최대체력의 X%를 공격력으로 전환
    ShieldEdge               = 36,  // 방어율 10%당 공격력 +Y%
    FocusedFire              = 37,  // 공격속도 0.1당 치명타 확률 +Z%

    // ── 즉시 적용 (None 트리거) ──────────────────────────────
    WideRange                = 38,  // 사정거리 +10% (StatModifier만 사용, 서브클래스 없음)

    // ── OnEnemyKill 트리거 ───────────────────────────────────
    LootHunter               = 39,  // 적 처치 시 골드 N 획득
    Slaughterer              = 40,  // 처치 시 공격력 X% 누적 (최대 MaxStacks 스택)
}
