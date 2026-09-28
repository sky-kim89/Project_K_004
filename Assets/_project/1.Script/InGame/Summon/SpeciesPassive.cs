using UnityEngine;

// ============================================================
//  SpeciesPassive.cs
//  종족 고유 패시브 — "그 종족을 그 종족답게 만드는 것".
//
//  ■ PassiveSkillType(40종) 과 무엇이 다른가
//    PassiveSkillType 은 원작 장군용이고, 표현할 수 있는 게 **숫자 조정**뿐이다
//    (공격력 +X%, 처치 시 회복 …). "슬라임은 죽으면 둘로 나뉜다" 처럼
//    유닛을 만들어 내거나 전투 흐름을 바꾸는 것은 그 틀로 표현할 수 없다.
//
//    그래서 종족 패시브는 별도 축이다. 둘은 함께 붙는다 —
//      종족 패시브 : 슬라임이면 언제나 분열한다 (카드 레벨과 무관)
//      카드 패시브 : 카드를 겹쳐 열리는 강화 (LevelPassives)
//
//  ■ 계열 상속 — 업그레이드는 기본 패시브를 반드시 물려받는다
//    슬라임(분열) ─┬─ 힐 슬라임 (분열 + 죽을 때 아군 회복)
//                  ├─ 독 슬라임 (분열 + 피격 시 적 중독)
//                  └─ 강철 슬라임(분열 + 피격 시 반사)
//    `MonsterSpeciesData.UpgradeOf` 로 계보를 잇고, 위로 거슬러 올라가며 모은다.
//    ⚠ 업그레이드에 기본 패시브를 다시 적지 말 것 — 상속으로 자동으로 붙는다.
//      두 번 적으면 분열이 두 번 일어난다.
//
//  ■ ⚠ 무한 증식을 막는 것은 '세대' 다
//    분열로 나온 슬라임이 또 분열하면 한 마리가 화면을 채운다.
//    개체마다 세대(Generation)를 들고 다니고, **유닛을 만들어 내는 패시브는
//    0세대만 발동한다** (SpeciesPassiveRule.MaxReproduceGeneration).
//    "분열체는 분열하지 않는다" 가 이 규칙 하나로 성립한다.
//    회복·중독처럼 개체를 늘리지 않는 패시브는 세대와 무관하게 계속 작동한다.
//
//  ■ 발동 지점은 둘뿐이다
//    ApplyOnSpawn — 스폰 직후. ECS 컴포넌트·상태효과를 붙인다 (지속형)
//    OnDeath      — 죽는 순간. 유닛 생성·범위 효과 (단발형)
//    이 둘로 표현이 안 되는 패시브가 나오면 그때 훅을 늘린다.
// ============================================================

public enum SpeciesPassive
{
    None = 0,

    // ── 기본 종족 패시브 (계열의 뿌리) ───────────────────────

    /// <summary>슬라임 — 죽으면 절반 크기로 둘로 나뉜다. 분열체는 다시 분열하지 않는다.</summary>
    SplitOnDeath = 1,

    /// <summary>스켈레톤 — 죽어도 확률로 그 자리에서 한 번 다시 일어난다.</summary>
    Reassemble = 2,

    /// <summary>
    /// 고블린 — 죽을 때 훔쳐 둔 <b>골드</b>를 떨군다.
    ///
    /// ⚠ 마나가 아니다
    ///   마나는 런 시작에 1회만 주어지는 자원이라, 여기서 늘리면
    ///   "죽을수록 마나가 는다" 가 되어 그 설계가 통째로 무너진다.
    ///   골드는 원래 전투로 버는 자원이므로 떨궈도 규칙을 깨지 않는다.
    /// </summary>
    Loot = 3,

    /// <summary>좀비 — 죽을 때 주변 적을 중독시킨다.</summary>
    PlagueBurst = 4,

    /// <summary>오크 — 처치할 때마다 공격력이 누적된다.</summary>
    Bloodlust = 5,

    /// <summary>늑대 — 처치할 때마다 이동속도가 누적된다.</summary>
    PackHunt = 6,

    /// <summary>멧돼지 — 최대 체력이 늘어난다. (돌진은 패시브가 아니라 액티브 스킬이다)</summary>
    Sturdy = 7,

    /// <summary>리치 — 준 피해의 일부를 회복한다.</summary>
    SoulDrain = 8,

    /// <summary>트롤 — 초당 체력을 재생한다.</summary>
    Regrow = 9,

    // ── 업그레이드 패시브 (기본 위에 얹힌다) ─────────────────

    /// <summary>힐 슬라임 — 죽을 때 주변 아군을 회복시킨다.</summary>
    HealOnDeath = 20,

    /// <summary>독 슬라임 — 자기를 때린 적을 중독시킨다.</summary>
    PoisonOnHit = 21,

    /// <summary>강철 슬라임 — 피격 시 받은 피해의 일부를 되돌려준다.</summary>
    ThornOnHit = 22,

    /// <summary>서리 계열 — 자기를 때린 적을 둔화시킨다.</summary>
    ChillOnHit = 23,

    /// <summary>방패 계열 — 피격 시 방어율이 잠시 오른다.</summary>
    Bulwark = 24,

    /// <summary>지휘 계열 — 죽을 때 주변 아군의 공격력을 올린다.</summary>
    RallyOnDeath = 25,

    /// <summary>폭발 계열 — 죽을 때 주변 적에게 피해를 준다.</summary>
    ExplodeOnDeath = 26,

    /// <summary>연사 계열 — 한 번에 두 발을 쏜다.</summary>
    Volley = 27,

    /// <summary>화염 계열 — 자기가 때린 적을 태운다(지속 피해).</summary>
    BurnOnAttack = 28,

    /// <summary>
    /// 신속 계열 — 공격속도·이동속도가 오른다.
    ///
    /// ⚠ 융합으로 남에게 넘기기 좋은 패시브다
    ///   종족을 가리지 않고 값이 붙는다. "숲의 트롤에게서 신속을 빼앗아
    ///   슬라임에게 준다" 가 융합의 대표적인 쓰임이다.
    /// </summary>
    Swiftness = 29,

    // ══════════════════════════════════════════════════════════
    //  ⚠ 여기부터는 **몬스터 장비**가 주는 것이다 (2026-09-10)
    //    축을 따로 만들지 않고 이 enum 에 얹은 이유 — 실행(SpeciesPassiveRuntime)·
    //    표시(몬스터 상세)·중복·각성 판정이 전부 한 목록을 봐야 하기 때문이다.
    //    출처가 셋(선천·융합·장비)이어도 결과는 한 줄이다 (PassiveResolver).
    //    ⚠ 번호는 뒤에만 — 세이브(융합 칸)와 SO(장비)에 정수로 들어간다.
    // ══════════════════════════════════════════════════════════

    // ── 특이 패시브 (40~) — 혼자선 약하고 짝을 만나면 세다 ──────
    Bravado        = 40,   // 허세 — 체력 50% 이상이면 공격력 +
    Recoil         = 41,   // 반동 — 평타로 적을 밀쳐 낼 때마다 회복
    VitalStrike    = 42,   // 급소 찌르기 — 치명타는 방어율을 무시
    LoneWolf       = 43,   // 외톨이 — 소환될 때 라인이 비어 있으면 공격력 +
    Rampart        = 44,   // 성벽 — 느려지는 대신 받는 피해 −
    Executioner    = 45,   // 처형 — 체력이 낮은 적에게 치명타 확정
    Anchor         = 46,   // 무게추 — 넉백을 받지 않는 대신 조금 느리다
    Photosynthesis = 47,   // 광합성 — 스킬을 쓸 때마다 회복
    BurstBody      = 48,   // 터지는 몸 — 죽을 때 주변 적을 밀쳐 낸다
    Hunger         = 49,   // 굶주림 — 공격 속도 + · 최대 체력 −
    Embers         = 50,   // 잔불 — 죽을 때 주변 적을 태운다
    Vengeance      = 51,   // 복수 — 같은 종족 아군이 죽을 때마다 공격력 +

    // ── 단계 패시브 (60~) — 스탯 % 의 I·II·III ─────────────────
    //   ⚠ I 과 III 은 **다른 패시브**다 (사용자 확정) — 둘 다 적용된다.
    //     실행은 MonsterGearRule.ApplyStats 가 한다 (스탯이라 합성 단계에 들어가야
    //     도감 숫자·스킬 쿨감이 함께 움직인다). GearTierPassive 가 값의 정본이다.
    HpUp1 = 60,       HpUp2 = 61,       HpUp3 = 62,
    AttackUp1 = 63,   AttackUp2 = 64,   AttackUp3 = 65,
    DefenseUp1 = 66,  DefenseUp2 = 67,  DefenseUp3 = 68,
    CritUp1 = 69,     CritUp2 = 70,     CritUp3 = 71,
    CritDmgUp1 = 72,  CritDmgUp2 = 73,  CritDmgUp3 = 74,
    CooldownUp1 = 75, CooldownUp2 = 76, CooldownUp3 = 77,
    PierceUp1 = 78,   PierceUp2 = 79,   PierceUp3 = 80,
    AttackSpeedUp1 = 81, AttackSpeedUp2 = 82, AttackSpeedUp3 = 83,

    // ── 각성 패시브 (100~) — 같은 패시브 둘이 모이면 이것이 된다 ──
    //   ⚠ 직접 얻는 길은 없다. PassiveAwakening 이 둘을 하나로 바꿔 끼운다.
    //   ⚠ 반드시 원래 것의 **상위 호환**이어야 한다 (사용자 확정) —
    //     둘을 모았는데 하나를 잃은 것처럼 느껴지면 각성이 벌이 된다.
    GreatSplit  = 100,   // 분열 ×2
    Undying     = 101,   // 재조립 ×2
    GoldRush    = 102,   // 약탈 ×2
    Pandemic    = 103,   // 역병 ×2
    Berserker   = 104,   // 피의 갈망 ×2
    Alpha       = 105,   // 무리 사냥 ×2
    Colossus    = 106,   // 튼튼함 ×2
    Vampire     = 107,   // 생명 흡수 ×2
    TrollBlood  = 108,   // 재생 ×2
    LifeSeed    = 109,   // 치유의 잔재 ×2
    Venom       = 110,   // 맹독 피부 ×2
    IronThorns  = 111,   // 가시 껍질 ×2
    Frostbite   = 112,   // 냉기 ×2
    Fortress    = 113,   // 방벽 ×2
    WarDrum     = 114,   // 최후의 함성 ×2
    Cataclysm   = 115,   // 자폭 ×2
    Barrage     = 116,   // 연사 ×2
    Hellfire    = 117,   // 화염 ×2
    Gale        = 118,   // 신속 ×2

    // ── 마나 패시브 (130~, 2026-09-12 사용자 지시) — 새 업그레이드 두 종이 갖는다 ──
    ManaResonance = 130,   // 마나 공명 — 최대 마나 1당 공·체 + (비전 리치)
    ManaRelease   = 131,   // 마나 방출 — 죽을 때 마나 + · 한 판 상한 (마력 해골)

    // ── 2차 업그레이드 전용 (사용자 지시, 2026-09-15) ────────
    //  ⚠ 분열(1)·대분열(101)과 다른 물건이다 — 그 둘은 **자기 자신**을 쪼개지만
    //    이쪽은 **권속(BroodSpecies)** 을 쏟아 낸다. 왕이 터지며 부하가 흩어지는 그림이다.
    KingSplit     = 132,   // 왕의 분열 — 죽을 때 권속을 KingSplitCount 마리 남긴다 (슬라임 킹)
    Cleave        = 133,   // 휩쓸기 — 느려지는 대신 평타가 주변까지 닿는다 (고대 트롤)
}

/// <summary>
/// 종족 패시브의 수치·규칙. 밸런싱 대상이라 한곳에 모은다.
///
/// ⚠ 개별 구현에 숫자를 박지 말 것
///   "분열체는 절반" 같은 값이 여러 곳에 흩어지면 밸런싱이 불가능해진다.
/// </summary>
public static class SpeciesPassiveRule
{
    // ── 세대 ─────────────────────────────────────────────────

    /// <summary>
    /// 유닛을 만들어 내는 패시브가 발동할 수 있는 최대 세대.
    ///
    /// 0 = 카드로 직접 소환된 개체만 증식한다. 분열·부활로 나온 개체(1세대)는
    /// 더 이상 늘어나지 않는다. 이 값을 올리면 증식이 기하급수로 터진다 —
    /// 슬라임 8마리가 죽으면 16, 그게 죽으면 32가 된다.
    /// </summary>
    public const int MaxReproduceGeneration = 0;

    // ── 분열 ─────────────────────────────────────────────────

    /// <summary>분열로 나오는 개체 수.</summary>
    public const int SplitCount = 2;

    /// <summary>분열체의 스탯·크기 배율. 둘로 나뉘니 절반이 자연스럽다.</summary>
    public const float SplitScale = 0.5f;

    // ── 재조립 ───────────────────────────────────────────────

    /// <summary>스켈레톤이 다시 일어날 확률.</summary>
    public const float ReassembleChance = 0.4f;

    /// <summary>다시 일어났을 때의 스탯·크기 배율.</summary>
    public const float ReassembleScale = 0.7f;

    // ── 그 외 ────────────────────────────────────────────────

    /// <summary>고블린이 죽을 때 떨구는 골드.</summary>
    public const int LootGold = 3;

    /// <summary>사망 시 범위 효과의 반경(월드).</summary>
    public const float DeathBurstRadius = 3.2f;

    /// <summary>좀비 역병 — 중독 지속 시간(초)과 초당 피해가 공격력에 곱해지는 비율.</summary>
    public const float PlagueDuration = 4f;
    public const float PlagueDpsRatio = 0.35f;

    /// <summary>폭발 — 자기 공격력의 몇 배를 주변에 뿌리는가.</summary>
    public const float ExplodeDamageRatio = 2.5f;

    /// <summary>
    /// 사망 시 회복 — 죽은 개체의 <b>실제</b> 최대 체력의 몇 %를 주변 아군에게.
    ///
    /// ⚠ SO 원본값(species.MaxHp)이 아니라 굴려 나온 값이 기준이다
    ///   카드 레벨·품질·소환력·시너지·분열 배율이 전부 실린 값이라야
    ///   "키운 힐 슬라임이 더 많이 회복시킨다" 가 성립한다. 원본값을 쓰면
    ///   만렙 Epic 이 Lv1 Normal 과 똑같은 양을 회복시킨다.
    /// </summary>
    /// <remarks>
    /// ⚠ 0.10 → 0.20 (사용자 지적, 2026-09-11 "힐이 체감이 안 된다")
    ///   힐 슬라임 최대 체력(약 200~350)의 10% 는 30 남짓 — 용사 평타 한 대 값이라
    ///   화면에서 회복으로 읽히지 않았다. 각성판(LifeSeed)도 함께 올려 상위 호환을 지킨다.
    /// </remarks>
    public const float HealOnDeathRatio = 0.20f;

    /// <summary>마나 공명 — 최대 마나 1당 공격력·최대 체력 비율 (소환되는 순간의 그릇으로 굳는다).</summary>
    public const float ManaResonancePerMana = 0.002f;

    /// <summary>마나 방출 — 죽을 때 돌려주는 마나 · 한 판 상한 (물량 종족이 판을 마나로 채우지 않게).</summary>
    public const int ManaReleaseAmount   = 1;
    public const int ManaReleaseStageCap = 5;

    /// <summary>사망 시 함성 — 주변 아군 공격력 증가율과 지속 시간.</summary>
    public const float RallyBonus   = 0.25f;
    public const float RallyDuration = 6f;

    /// <summary>피격 반응(중독·둔화)의 지속 시간.</summary>
    public const float ReactionDuration = 3f;

    /// <summary>독 슬라임 — 초당 피해가 자기 공격력에 곱해지는 비율.</summary>
    public const float PoisonDpsRatio = 0.1f;

    /// <summary>서리 — 이동속도 감소 비율.</summary>
    public const float ChillSlowRatio = 0.35f;

    /// <summary>가시 — 받은 피해의 몇 %를 되돌려주는가.</summary>
    public const float ThornRatio = 0.3f;

    /// <summary>튼튼함 — 최대 체력 증가율.</summary>
    public const float SturdyHpBonus = 0.10f;

    /// <summary>신속 — 공격속도·이동속도 증가율.</summary>
    public const float SwiftAttackBonus = 0.25f;
    public const float SwiftMoveBonus   = 0.25f;

    /// <summary>화상 — 초당 피해가 공격력에 곱해지는 비율과 지속 시간.</summary>
    public const float BurnDpsRatio = 0.4f;
    public const float BurnDuration = 3f;

    /// <summary>재생 — 최대 체력의 몇 %를 초당 회복하는가.</summary>
    public const float RegrowPerSecond = 0.03f;

    // ── 특이 패시브 (장비) ───────────────────────────────────

    public const float BravadoHpThreshold  = 0.50f;   // 허세 — 이 비율 이상일 때
    public const float BravadoAttackBonus  = 0.12f;
    public const float RecoilHealRatio     = 0.01f;   // 반동 — 밀쳐 낼 때마다 최대 체력의
    public const float LoneWolfAttackBonus = 0.25f;   // 외톨이
    public const float RampartMovePenalty  = 0.30f;   // 성벽
    public const float RampartDamageCut    = 0.15f;
    public const float ExecuteHpThreshold  = 0.30f;   // 처형 — 적 체력이 이 비율 이하
    public const float AnchorMovePenalty   = 0.10f;   // 무게추
    public const float PhotosynthesisHeal  = 0.05f;   // 광합성 — 스킬마다 최대 체력의
    public const float BurstBodyDamageRatio = 0.5f;   // 터지는 몸 — 공격력 배수
    public const float BurstBodyKnockback  = 5f;
    public const float HungerAttackSpeed   = 0.15f;   // 굶주림
    public const float HungerHpPenalty     = 0.10f;
    public const float EmbersDpsRatio      = 0.6f;    // 잔불 — 공격력 대비 초당
    public const float EmbersDuration      = 3f;
    public const float VengeancePerStack   = 0.02f;   // 복수
    public const int   VengeanceMaxStacks  = 10;

    // ── 각성 (원래 것과 나란히 두어 '상위 호환' 인지 한눈에 보이게 한다) ──

    /// <summary>
    /// 왕의 분열 — 죽을 때 남기는 <b>권속</b>의 수 (사용자 지시, 2026-09-15).
    ///
    /// ⚠ 자기 자신이 아니라 MonsterSpeciesData.BroodSpecies 를 낸다
    ///   같은 종족으로 쪼개면 12기가 전부 왕이 되어 **각자 권속 소환 스킬을 갖는다** —
    ///   한 판에 슬라임이 지수로 불어난다. 권속을 내면 그 12기는 평범한 슬라임이라
    ///   더 낳지 않는다 (세대 제한과 별개로 구조적으로 막힌다).
    ///
    /// ⚠ 세대 제한(MaxReproduceGeneration)도 그대로 받는다 — 카드로 낸 왕만 터진다.
    /// </summary>
    // ── 휩쓸기 (사용자 지시, 2026-09-15) ─────────────────────
    //
    //  ■ 느려지는 값으로 범위를 산다 — 순수한 강화가 아니다
    //    공격 속도를 깎지 않으면 "평타가 그냥 넓어진" 것이라 앞줄 몬스터라면
    //    누구나 갖고 싶은 능력이 된다. 느린 대신 넓은 것이라야 축이 갈린다.
    //
    //  ⚠ 셋은 한 묶음이다 — 하나만 만지면 DPS 가 통째로 어긋난다
    //    단일 대상 DPS 는 공속 배율만큼 그대로 줄고(×0.7), 그 손해를 주변 몫이
    //    메운다. 붙어 있는 적이 셋이면 1 + 0.5×2 = 2.0 배 → 실효 ×1.4.
    //    혼자 있는 적에게는 손해라는 것이 이 능력의 값이다.
    //
    //  ⚠ 반경은 제 덩치와 함께 본다 — 고대 트롤은 Size 가 가장 크다.
    //    작은 종족에 그대로 얹으면 팔이 몸의 두 배까지 닿는 그림이 된다.

    /// <summary>휩쓸기 — 주 타겟 주변 이 반경 안의 적도 맞는다.</summary>
    public const float CleaveRadius         = 1.8f;

    /// <summary>휩쓸기 — 주변이 받는 몫. 주 타겟이 받은 피해의 비율이다.</summary>
    public const float CleaveSplashRatio    = 0.5f;

    /// <summary>휩쓸기 — 그 대가로 곱해지는 공격 속도. 1 미만이면 느려진다.</summary>
    public const float CleaveAttackSpeedMult = 0.7f;

    public const int   KingSplitCount        = 12;

    /// <summary>
    /// 왕의 분열로 나온 권속의 스탯·크기 배율.
    ///
    /// ⚠ 합계로 재야 한다 — 분열 2×0.5 = 1.0(본전) · 대분열 4×0.35 = 1.4 ·
    ///   왕의 분열 12×0.15 = <b>1.8</b>. 마릿수가 셋 중 가장 많으니 한 마리는 가장 작다.
    ///   화면에서는 "왕이 터지자 줄이 다시 찬다" 로 읽히면 된다.
    /// </summary>
    public const float KingSplitScale        = 0.15f;

    public const int   GreatSplitCount      = 4;      // 분열 2
    public const float GreatSplitScale      = 0.35f;  // 분열 0.5 — 합계 1.4 vs 1.0
    public const float UndyingScale         = 0.85f;  // 재조립 0.7 (확률 40% → 100%)
    public const int   GoldRushMult         = 3;      // 약탈 ×1
    public const float PandemicPower        = 2f;     // 역병 피해 배율
    public const float PandemicReach        = 1.5f;   //       범위·지속 배율
    public const float BerserkerAttackBonus = 0.15f;
    public const float AlphaSpeedBonus      = 0.15f;
    public const float ColossusHpBonus      = 0.25f;  // 튼튼함 0.10
    public const float TrollBloodPerSecond  = 0.06f;  // 재생 0.03
    public const float TrollBloodLowHpMult  = 2f;
    public const float TrollBloodLowHp      = 0.30f;
    public const float LifeSeedRatio        = 0.40f;  // 치유의 잔재 0.20 (2026-09-11 둘 다 올림)
    public const float LifeSeedReach        = 1.5f;
    public const float VenomPower           = 2.5f;   // 맹독 피부 배율
    public const float VenomThorn           = 0.15f;
    public const float IronThornsRatio      = 0.75f;  // 가시 0.30
    public const float FrostbiteSlowRatio   = 0.60f;  // 냉기 0.35
    public const float FrostbiteDuration    = 5f;     // 냉기 3
    public const float FortressDefense      = 0.10f;
    public const float WarDrumAttackBonus   = 0.50f;  // 함성 0.25
    public const float WarDrumSpeedBonus    = 0.25f;
    public const float WarDrumDuration      = 9f;     // 함성 6
    public const float CataclysmPower       = 2f;     // 자폭 배율
    public const float CataclysmReach       = 1.6f;
    public const float BarrageAttackSpeed   = 0.30f;
    public const float HellfirePower        = 2.2f;   // 화염 배율
    public const float HellfireReach        = 1.5f;   //       지속 배율
    public const float GaleBonus            = 0.50f;  // 신속 0.25

    // ⚠ 방벽(Bulwark)·생명 흡수(SoulDrain)의 수치는 여기 없다
    //   둘은 기존 패시브(DefenseShield · VampiricStrike)에 그대로 얹었다.
    //   수치는 그쪽 PassiveSkillData 에셋이 소유한다 — 여기에 복사해 두면
    //   두 값이 조용히 어긋난다.

    // ── 순서 ─────────────────────────────────────────────────

    /// <summary>
    /// 아이콘 배열의 순서 정본. None 은 빠진다.
    ///
    /// ⚠ 셋이 이 순서 하나를 공유한다 — MonsterSynergyRule.AllTags 와 같은 계약이다
    ///   ① SpeciesPassiveIconGenerator 가 굽는 PNG
    ///   ② Creator 가 프리팹에 박는 Sprite[]
    ///   ③ 런타임 조회(IndexOf)
    ///   중간에 끼워 넣으면 "재생인데 해골이 뜨는" 상태가 된다. <b>뒤에만 추가한다.</b>
    /// </summary>
    public static readonly SpeciesPassive[] All =
    {
        SpeciesPassive.SplitOnDeath,
        SpeciesPassive.Reassemble,
        SpeciesPassive.Loot,
        SpeciesPassive.PlagueBurst,
        SpeciesPassive.Bloodlust,
        SpeciesPassive.PackHunt,
        SpeciesPassive.Sturdy,
        SpeciesPassive.SoulDrain,
        SpeciesPassive.Regrow,
        SpeciesPassive.HealOnDeath,
        SpeciesPassive.PoisonOnHit,
        SpeciesPassive.ThornOnHit,
        SpeciesPassive.ChillOnHit,
        SpeciesPassive.Bulwark,
        SpeciesPassive.RallyOnDeath,
        SpeciesPassive.ExplodeOnDeath,
        SpeciesPassive.Volley,
        SpeciesPassive.BurnOnAttack,
        SpeciesPassive.Swiftness,

        // ── 장비 — 특이 ──
        SpeciesPassive.Bravado,
        SpeciesPassive.Recoil,
        SpeciesPassive.VitalStrike,
        SpeciesPassive.LoneWolf,
        SpeciesPassive.Rampart,
        SpeciesPassive.Executioner,
        SpeciesPassive.Anchor,
        SpeciesPassive.Photosynthesis,
        SpeciesPassive.BurstBody,
        SpeciesPassive.Hunger,
        SpeciesPassive.Embers,
        SpeciesPassive.Vengeance,

        // ── 장비 — 단계 ──
        SpeciesPassive.HpUp1,          SpeciesPassive.HpUp2,          SpeciesPassive.HpUp3,
        SpeciesPassive.AttackUp1,      SpeciesPassive.AttackUp2,      SpeciesPassive.AttackUp3,
        SpeciesPassive.DefenseUp1,     SpeciesPassive.DefenseUp2,     SpeciesPassive.DefenseUp3,
        SpeciesPassive.CritUp1,        SpeciesPassive.CritUp2,        SpeciesPassive.CritUp3,
        SpeciesPassive.CritDmgUp1,     SpeciesPassive.CritDmgUp2,     SpeciesPassive.CritDmgUp3,
        SpeciesPassive.CooldownUp1,    SpeciesPassive.CooldownUp2,    SpeciesPassive.CooldownUp3,
        SpeciesPassive.PierceUp1,      SpeciesPassive.PierceUp2,      SpeciesPassive.PierceUp3,
        SpeciesPassive.AttackSpeedUp1, SpeciesPassive.AttackSpeedUp2, SpeciesPassive.AttackSpeedUp3,

        // ── 각성 ──
        SpeciesPassive.GreatSplit, SpeciesPassive.Undying,    SpeciesPassive.GoldRush,
        SpeciesPassive.Pandemic,   SpeciesPassive.Berserker,  SpeciesPassive.Alpha,
        SpeciesPassive.Colossus,   SpeciesPassive.Vampire,    SpeciesPassive.TrollBlood,
        SpeciesPassive.LifeSeed,   SpeciesPassive.Venom,      SpeciesPassive.IronThorns,
        SpeciesPassive.Frostbite,  SpeciesPassive.Fortress,   SpeciesPassive.WarDrum,
        SpeciesPassive.Cataclysm,  SpeciesPassive.Barrage,    SpeciesPassive.Hellfire,
        SpeciesPassive.Gale,

        // ── 마나 (2026-09-12) ──
        SpeciesPassive.ManaResonance, SpeciesPassive.ManaRelease,
        SpeciesPassive.KingSplit,      SpeciesPassive.Cleave,
    };

    /// <summary>All 안에서의 자리. 없으면 −1 (아이콘을 숨기라는 뜻이다).</summary>
    public static int IndexOf(SpeciesPassive passive)
    {
        for (int i = 0; i < All.Length; i++)
            if (All[i] == passive) return i;

        return -1;
    }
}

public static class SpeciesPassiveNames
{
    public static string ToKorean(this SpeciesPassive passive) => passive switch
    {
        SpeciesPassive.SplitOnDeath   => "분열",
        SpeciesPassive.Reassemble     => "재조립",
        SpeciesPassive.Loot           => "약탈",
        SpeciesPassive.PlagueBurst    => "역병",
        SpeciesPassive.Bloodlust      => "피의 갈망",
        SpeciesPassive.PackHunt       => "무리 사냥",
        SpeciesPassive.Sturdy         => "튼튼함",
        SpeciesPassive.SoulDrain      => "생명 흡수",
        SpeciesPassive.Regrow         => "재생",
        SpeciesPassive.HealOnDeath    => "치유의 잔재",
        SpeciesPassive.PoisonOnHit    => "맹독 피부",
        SpeciesPassive.ThornOnHit     => "가시 껍질",
        SpeciesPassive.ChillOnHit     => "냉기",
        SpeciesPassive.Bulwark        => "방벽",
        SpeciesPassive.RallyOnDeath   => "최후의 함성",
        SpeciesPassive.ExplodeOnDeath => "자폭",
        SpeciesPassive.Volley         => "연사",
        SpeciesPassive.BurnOnAttack   => "화염",
        SpeciesPassive.Swiftness      => "신속",

        SpeciesPassive.Bravado        => "허세",
        SpeciesPassive.Recoil         => "반동",
        SpeciesPassive.VitalStrike    => "급소 찌르기",
        SpeciesPassive.LoneWolf       => "외톨이",
        SpeciesPassive.Rampart        => "성벽",
        SpeciesPassive.Executioner    => "처형",
        SpeciesPassive.Anchor         => "무게추",
        SpeciesPassive.Photosynthesis => "광합성",
        SpeciesPassive.BurstBody      => "터지는 몸",
        SpeciesPassive.Hunger         => "굶주림",
        SpeciesPassive.Embers         => "잔불",
        SpeciesPassive.Vengeance      => "복수",

        SpeciesPassive.GreatSplit     => "대분열",
        SpeciesPassive.Undying        => "불사의 뼈",
        SpeciesPassive.GoldRush       => "황금 약탈",
        SpeciesPassive.Pandemic       => "역병 창궐",
        SpeciesPassive.Berserker      => "광전사",
        SpeciesPassive.Alpha          => "우두머리",
        SpeciesPassive.Colossus       => "거상",
        SpeciesPassive.Vampire        => "흡혈귀",
        SpeciesPassive.TrollBlood     => "트롤의 피",
        SpeciesPassive.LifeSeed       => "생명의 씨앗",
        SpeciesPassive.Venom          => "맹독",
        SpeciesPassive.IronThorns     => "강철 가시",
        SpeciesPassive.Frostbite      => "동결",
        SpeciesPassive.Fortress       => "난공불락",
        SpeciesPassive.WarDrum        => "전쟁의 북",
        SpeciesPassive.Cataclysm      => "대폭발",
        SpeciesPassive.Barrage        => "탄막",
        SpeciesPassive.Hellfire       => "업화",
        SpeciesPassive.Gale           => "질풍",

        SpeciesPassive.ManaResonance  => "마나 공명",
        SpeciesPassive.ManaRelease    => "마나 방출",
        SpeciesPassive.KingSplit      => "왕의 분열",
        SpeciesPassive.Cleave         => "휩쓸기",

        // 단계 패시브 — 이름은 GearTierPassive 가 계열·단계에서 만든다
        _ => GearTierPassive.NameOf(passive),
    };

    /// <summary>도감·카드에 띄울 한 줄 설명.</summary>
    public static string Describe(this SpeciesPassive passive) => passive switch
    {
        SpeciesPassive.SplitOnDeath   => "죽으면 절반 크기로 둘로 나뉜다",
        SpeciesPassive.Reassemble     => "죽어도 일정 확률로 그 자리에서 한 번 다시 일어난다",
        SpeciesPassive.Loot           => "죽을 때 골드를 떨군다",
        SpeciesPassive.PlagueBurst    => "죽을 때 주변 적을 중독시킨다",
        SpeciesPassive.Bloodlust      => "처치할 때마다 공격력이 누적된다",
        SpeciesPassive.PackHunt       => "처치할 때마다 이동속도가 누적된다",
        // ⚠ 수치는 손으로 적지 말 것 — SpeciesPassiveRule 에서 뽑는다
        //   한 번 적어 두면 밸런스를 고쳐도 설명만 옛 숫자를 말한다.
        SpeciesPassive.Sturdy         => F("최대 체력이 {0} 늘어난다", Pct(SpeciesPassiveRule.SturdyHpBonus)),
        SpeciesPassive.SoulDrain      => "준 피해의 일부를 회복한다",
        SpeciesPassive.Regrow         => F("초당 최대 체력의 {0}를 재생한다", Pct(SpeciesPassiveRule.RegrowPerSecond)),
        SpeciesPassive.HealOnDeath    => F("죽을 때 주변 아군을 자기 최대 체력의 {0}만큼 회복시킨다",
                                           Pct(SpeciesPassiveRule.HealOnDeathRatio)),
        SpeciesPassive.PoisonOnHit    => "자기를 때린 적을 중독시킨다",
        SpeciesPassive.ThornOnHit     => "피격 시 받은 피해의 일부를 되돌려준다",
        SpeciesPassive.ChillOnHit     => "자기를 때린 적을 둔화시킨다",
        SpeciesPassive.Bulwark        => "피격 시 방어율이 잠시 오른다",
        SpeciesPassive.RallyOnDeath   => "죽을 때 주변 아군의 공격력을 올린다",
        SpeciesPassive.ExplodeOnDeath => "죽을 때 주변 적에게 피해를 준다",
        SpeciesPassive.Volley         => "한 번에 두 발을 쏜다",
        SpeciesPassive.BurnOnAttack   => "자기가 때린 적을 태운다",
        SpeciesPassive.Swiftness      => F("공격속도와 이동속도가 {0} 오른다", Pct(SpeciesPassiveRule.SwiftAttackBonus)),

        // ── 특이 ──
        SpeciesPassive.Bravado        => F("체력이 {0} 이상이면 공격력 +{1}",
                                           Pct(SpeciesPassiveRule.BravadoHpThreshold),
                                           Pct(SpeciesPassiveRule.BravadoAttackBonus)),
        SpeciesPassive.Recoil         => F("평타로 적을 밀쳐 낼 때마다 최대 체력의 {0}를 회복한다",
                                           Pct(SpeciesPassiveRule.RecoilHealRatio)),
        SpeciesPassive.VitalStrike    => "치명타는 방어율을 무시한다",
        SpeciesPassive.LoneWolf       => F("소환될 때 같은 라인에 다른 아군이 없으면 공격력 +{0}",
                                           Pct(SpeciesPassiveRule.LoneWolfAttackBonus)),
        SpeciesPassive.Rampart        => F("이동 속도 -{0}, 받는 피해 -{1}",
                                           Pct(SpeciesPassiveRule.RampartMovePenalty),
                                           Pct(SpeciesPassiveRule.RampartDamageCut)),
        SpeciesPassive.Executioner    => F("체력이 {0} 이하인 적에게는 치명타가 확정된다",
                                           Pct(SpeciesPassiveRule.ExecuteHpThreshold)),
        SpeciesPassive.Anchor         => F("넉백을 받지 않는다, 이동 속도 -{0}",
                                           Pct(SpeciesPassiveRule.AnchorMovePenalty)),
        SpeciesPassive.Photosynthesis => F("스킬을 쓸 때마다 최대 체력의 {0}를 회복한다",
                                           Pct(SpeciesPassiveRule.PhotosynthesisHeal)),
        SpeciesPassive.BurstBody      => "죽을 때 주변 적을 밀쳐 낸다",
        SpeciesPassive.Hunger         => F("공격 속도 +{0}, 최대 체력 -{1}",
                                           Pct(SpeciesPassiveRule.HungerAttackSpeed),
                                           Pct(SpeciesPassiveRule.HungerHpPenalty)),
        SpeciesPassive.Embers         => F("죽을 때 주변 적을 {0:0}초간 태운다",
                                           SpeciesPassiveRule.EmbersDuration),
        SpeciesPassive.Vengeance      => F("같은 종족 아군이 죽을 때마다 공격력 +{0} (최대 {1}번)",
                                           Pct(SpeciesPassiveRule.VengeancePerStack),
                                           SpeciesPassiveRule.VengeanceMaxStacks),

        // ── 각성 — 원래 것보다 무엇이 더 좋은지를 말한다 ──
        SpeciesPassive.GreatSplit     => F("죽으면 {0}마리로 나뉜다", SpeciesPassiveRule.GreatSplitCount),
        SpeciesPassive.Undying        => "죽으면 반드시 그 자리에서 한 번 다시 일어난다",
        SpeciesPassive.GoldRush       => F("죽을 때 골드를 {0}배 떨군다", SpeciesPassiveRule.GoldRushMult),
        SpeciesPassive.Pandemic       => "죽을 때 더 넓게, 더 오래, 두 배로 중독시킨다",
        SpeciesPassive.Berserker      => F("처치할 때마다 공격력 누적 + 공격력 +{0}",
                                           Pct(SpeciesPassiveRule.BerserkerAttackBonus)),
        SpeciesPassive.Alpha          => F("처치할 때마다 이동속도 누적 + 공격·이동 속도 +{0}",
                                           Pct(SpeciesPassiveRule.AlphaSpeedBonus)),
        SpeciesPassive.Colossus       => F("최대 체력 +{0}, 넉백을 받지 않는다",
                                           Pct(SpeciesPassiveRule.ColossusHpBonus)),
        SpeciesPassive.Vampire        => "준 피해를 흡수하고, 처치할 때마다 체력을 회복한다",
        SpeciesPassive.TrollBlood     => F("초당 최대 체력의 {0}를 재생, 체력이 {1} 이하면 두 배",
                                           Pct(SpeciesPassiveRule.TrollBloodPerSecond),
                                           Pct(SpeciesPassiveRule.TrollBloodLowHp)),
        SpeciesPassive.LifeSeed       => F("죽을 때 넓은 범위의 아군을 최대 체력의 {0}만큼 회복시킨다",
                                           Pct(SpeciesPassiveRule.LifeSeedRatio)),
        SpeciesPassive.Venom          => "때린 적을 강하게 중독시키고 받은 피해 일부를 되돌려준다",
        SpeciesPassive.IronThorns     => F("받은 피해의 {0}를 되돌려준다",
                                           Pct(SpeciesPassiveRule.IronThornsRatio)),
        SpeciesPassive.Frostbite      => F("때린 적을 {0} 둔화시킨다 ({1:0}초)",
                                           Pct(SpeciesPassiveRule.FrostbiteSlowRatio),
                                           SpeciesPassiveRule.FrostbiteDuration),
        SpeciesPassive.Fortress       => F("피격 시 방어율이 오르고, 방어율 +{0}, 넉백을 받지 않는다",
                                           Pct(SpeciesPassiveRule.FortressDefense)),
        SpeciesPassive.WarDrum        => F("죽을 때 주변 아군 공격력 +{0} · 공격 속도 +{1}",
                                           Pct(SpeciesPassiveRule.WarDrumAttackBonus),
                                           Pct(SpeciesPassiveRule.WarDrumSpeedBonus)),
        SpeciesPassive.Cataclysm      => "죽을 때 더 넓게, 두 배로 터진다",
        SpeciesPassive.Barrage        => F("한 번에 두 발을 쏘고 공격 속도 +{0}",
                                           Pct(SpeciesPassiveRule.BarrageAttackSpeed)),
        SpeciesPassive.Hellfire       => "때린 적을 두 배 넘게, 더 오래 태운다",

        // ⚠ Swiftness 와 **같은 문장**이다 — 표에는 한 줄만 있으면 둘 다 받는다
        SpeciesPassive.Gale           => F("공격속도와 이동속도가 {0} 오른다", Pct(SpeciesPassiveRule.GaleBonus)),

        // ── 마나 — 0.2% 라 Pct(정수 반올림)를 쓰지 않는다 ──
        SpeciesPassive.ManaResonance  => F("최대 마나 1당 공격력·최대 체력 +{0:0.#}% (소환될 때 정해짐)",
                                           SpeciesPassiveRule.ManaResonancePerMana * 100f),
        SpeciesPassive.ManaRelease    => F("죽을 때 마나 +{0} (한 판에 최대 {1})",
                                           SpeciesPassiveRule.ManaReleaseAmount,
                                           SpeciesPassiveRule.ManaReleaseStageCap),
        SpeciesPassive.KingSplit      => F("죽을 때 권속 {0}마리로 흩어진다",
                                           SpeciesPassiveRule.KingSplitCount),
        SpeciesPassive.Cleave         => F("공격 속도 {0} 느려지는 대신, 평타가 대상 주변의 적에게도 {1} 피해를 준다",
                                           Pct(1f - SpeciesPassiveRule.CleaveAttackSpeedMult),
                                           Pct(SpeciesPassiveRule.CleaveSplashRatio)),

        _ => GearTierPassive.DescribeOf(passive),
    };

    /// <summary>비율을 사람이 읽는 %로. MonsterSynergyRule.Pct 와 같은 규칙이다.</summary>
    static string Pct(float ratio) => $"{Mathf.RoundToInt(ratio * 100f)}%";

    /// <summary>
    /// 표에서 문장을 찾아 숫자를 끼워 넣는다 — 원본 표가 쓰는 <c>{0}</c> 방식.
    ///
    /// ⚠ <b>수치가 든 설명은 반드시 이걸 쓴다. 보간 문자열($"…{값}…")을 쓰지 말 것</b>
    ///   (2026-09-16). 보간은 실행 시점에 이미 숫자로 바뀌어 있어서 번역표의
    ///   키(코드에 적힌 그대로의 문자열)와 **영원히 일치하지 않는다** — 표에는
    ///   줄이 있는데 화면에는 한국어로 남는다.
    ///
    /// ⚠ 한 문장을 <c>+</c> 로 나눠 쓰지 말 것. 나누면 조각마다 표에 줄이 필요한데
    ///   조각은 언어마다 어순이 달라 옮길 수가 없다. 줄바꿈은 인자 쪽에서 한다.
    /// </summary>
    static string F(string key, params object[] args)
        => LocalizationManager.Instance.Format(key, args);
}
