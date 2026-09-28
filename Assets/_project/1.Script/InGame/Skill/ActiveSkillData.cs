using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveSkillData.cs
//  액티브 스킬 베이스 ScriptableObject.
//
//  ■ 일반 구조
//    - 쿨다운 / 효과 수치는 여기에 정의
//    - 실제 실행 로직(이동 트윈, 타격, 넉백 연출 등)은 서브클래스에서 Execute() 오버라이드
//
//  ■ 실행 흐름
//    1. AI 또는 입력 → Entity 에 UseActiveSkillTag 추가
//    2. ActiveSkillCooldownSystem → 쿨다운 확인 후 UseActiveSkillTag 제거 + 쿨다운 리셋
//       동시에 ActiveSkillExecuteEvent 버퍼에 이벤트 추가
//    3. ActiveSkillExecuteSystem(managed) → Execute(context) 호출
//    4. Execute() 내부에서 트윈/이동/ECS 이벤트 처리
//
//  ■ 추가 액티브 스킬 시 작업
//    1. ActiveSkillData 를 상속한 클래스 생성 → Execute() 오버라이드
//    2. SO 생성 + GeneralRuntimeBridge 의 ActiveSkill 슬롯에 할당
//    3. ActiveSkillId 에 새 ID 추가
//
//  ■ ActiveSkillContext
//    Execute() 에 전달되는 실행 컨텍스트.
//    ECS Entity/StatComponent 와 Unity GO/Transform 에 동시 접근 가능.
// ============================================================

[CreateAssetMenu(fileName = "ActiveSkillData", menuName = "BattleGame/ActiveSkillData")]
public class ActiveSkillData : ScriptableObject
{
    // ─────────────────────────────────────────────────────────
    // ■ 식별
    // ─────────────────────────────────────────────────────────

    [Header("식별")]
    [Tooltip("스킬 고유 ID. GeneralActiveSkillComponent.SkillId 에 저장된다.")]
    public ActiveSkillId SkillId;

    [Tooltip("스킬 이름 (에디터·UI 표시용)")]
    public string SkillName;

    [TextArea(2, 4)]
    [Tooltip("스킬 설명 (UI 표시용)")]
    public string Description;

    // ─────────────────────────────────────────────────────────
    // ■ 기본 수치
    // ─────────────────────────────────────────────────────────

    [Header("기본 수치")]
    [Min(0f)]
    [Tooltip("쿨다운 (초)")]
    public float Cooldown = 15f;

    [Tooltip("효과 수치 기본값 (데미지 배율, 버프량 등 — 스킬별 해석이 다름)")]
    public float EffectValue = 1f;

    [Tooltip("효과 반경 (0 이면 단일 대상)")]
    public float EffectRadius = 0f;

    [Tooltip("효과 지속 시간 (0 이면 즉발)")]
    public float EffectDuration = 0f;

    // ─────────────────────────────────────────────────────────
    // ■ 직업 제한 (참조용 — 스킬 배정 로직에서 외부에서 활용)
    // ─────────────────────────────────────────────────────────

    [Header("직업 제한 (비워두면 모든 직업 가능)")]
    [Tooltip("이 스킬을 사용할 수 있는 직업 목록. 스킬 배정 시 외부에서 참조.")]
    public UnitJob[] AllowedJobs = new UnitJob[0];

    // ─────────────────────────────────────────────────────────
    // ■ 희귀 스킬
    // ─────────────────────────────────────────────────────────

    [Header("희귀 스킬")]
    [Tooltip("희귀 스킬 여부.\n" +
             "· 주인 영웅 한 명이 이름으로 고정된다 (RareSkillArbiter 할당표)\n" +
             "· 그 영웅은 등급·부대와 무관하게 항상 이 스킬을 쓴다\n" +
             "· 다른 영웅은 추첨으로도 절대 얻을 수 없다")]
    public bool IsRare = false;

    // ─────────────────────────────────────────────────────────
    // ■ 이펙트 풀 키 (PoolType.Effect)
    // ─────────────────────────────────────────────────────────

    [Header("이펙트 풀 키 (PoolType.Effect)")]
    [Tooltip("사용자(시전자) 이펙트 풀 키. 비워두면 미사용.")]
    public string CasterEffectKey = "";

    [Tooltip("피격 대상 이펙트 풀 키. 비워두면 미사용.")]
    public string TargetEffectKey = "";

    [Tooltip("기본/범위 이펙트 풀 키 (존 중심, 낙하 예고 등). 비워두면 미사용.")]
    public string BaseEffectKey = "";

    [Header("범위 표시")]
    [Tooltip("버프·오라의 적용 범위를 바닥 원으로 보여 줄지. 반경(EffectRadius)이 있는 스킬만 의미가 있다.")]
    public bool ShowRangeIndicator = true;

    [Tooltip("범위 원이 떠 있는 시간(초). 0 이면 EffectDespawnDelay 를 쓴다.")]
    public float RangeIndicatorDuration = 0f;

    [Min(0.1f)]
    [Tooltip("이펙트 자동 반납 딜레이 (초). 파티클 재생 시간에 맞게 조절.")]
    public float EffectDespawnDelay = 2f;


    // ─────────────────────────────────────────────────────────
    // ■ 실행 진입점 — 서브클래스에서 오버라이드
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 스킬을 실행한다. ActiveSkillExecuteSystem 이 이벤트 발생 시 호출.
    /// context 를 통해 ECS Entity / StatComponent 와 Unity GO / Transform 에 접근 가능.
    /// </summary>
    /// <summary>
    /// 이 스킬의 적용 범위를 바닥에 원으로 그린다.
    ///
    /// ⚠ 버프는 범위가 보이지 않으면 판단할 수 없다
    ///   "주변 아군 공격력 +30%" 는 어디까지가 주변인지 화면에 아무 단서가 없었다.
    ///   부대를 어디에 세워야 받는지 알 수 없으니, 사실상 운에 맡기는 스킬이었다.
    ///
    /// ⚠ 반경이 없는 스킬은 아무것도 그리지 않는다
    ///   단일 대상 스킬에 원을 그리면 범위가 있는 것처럼 읽힌다.
    /// </summary>
    protected void ShowRange(ActiveSkillContext ctx, float radius)
        => ShowRange(ctx, radius, BuffStatPalette.Unknown);

    /// <summary>
    /// 범위를 버프 색으로 그린다 — 무엇이 오르는 범위인지까지 보여 준다.
    ///
    /// 색은 BuffStatPalette 에서 가져온다. 발밑 빛기둥과 같은 표라
    /// 원 안에 선 병사의 기둥 색과 반드시 일치한다.
    /// </summary>
    protected void ShowRange(ActiveSkillContext ctx, float radius, Color tint)
    {
        if (!ShowRangeIndicator || radius <= 0f) return;
        if (ctx.CasterTransform == null) return;

        float dur = RangeIndicatorDuration > 0f ? RangeIndicatorDuration : EffectDespawnDelay;
        SkillEffectHelper.SpawnRange(SkillEffectHelper.RangeRingKey,
                                     ctx.CasterTransform.position, radius, dur, tint);
    }

    public virtual void Execute(ActiveSkillContext context) { }
}

// ─────────────────────────────────────────────────────────────
// ■ 실행 컨텍스트
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 스킬 실행 시 전달되는 컨텍스트.
/// ECS 데이터와 Unity MonoBehaviour 양쪽에 동시 접근할 수 있다.
/// </summary>
public struct ActiveSkillContext
{
    // ECS
    public Entity         CasterEntity;    // 스킬 사용자(제너럴) Entity
    public Entity         TargetEntity;    // 현재 공격 타겟 Entity (없으면 Entity.Null)
    public Vector3        TargetPosition;  // 스킬 발동 시점의 타겟 위치 스냅샷 (타겟 사망 후에도 유효)
    public StatComponent  CasterStat;      // 사용자 StatComponent 스냅샷
    public EntityManager  EntityManager;

    // Unity
    public GameObject     CasterObject;    // 사용자 GameObject (트윈, 애니메이션 등)
    public UnityEngine.Transform CasterTransform;

    // 편의 프로퍼티
    public bool HasTarget => TargetEntity != Entity.Null;
}

// ─────────────────────────────────────────────────────────────
// ■ 액티브 스킬 ID
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 액티브 스킬 고유 ID.
/// GeneralActiveSkillComponent.SkillId 에 저장되며
/// 스킬별 실행 시스템이 이 값으로 분기한다.
/// </summary>
public enum ActiveSkillId : int
{
    None             = 0,
    HeavyStrike      = 1,   // 강타          — 단일 돌진 타격 + 넉백         (방패·전사)
    VolleyFire       = 2,   // 일제 사격      — 전체 즉시 일반 공격           (궁수·법사)
    LeapStrike       = 3,   // 도약 강타      — 전방 도약 + AoE 타격 + 넉백   (방패·전사)
    HealAura         = 4,   // 치유 오라      — 피해 입은 아군 장군 랜덤 1명+휘하 병사 회복  (공통)
    TargetHeal       = 5,   // 집중 치유      — 아군 장군 중 HP 가장 낮은 1명 집중 회복      (공통)
    ChargeSoldier    = 6,   // 돌격 병사 소환 — 적 밀치며 데미지, 전투 참여   (방패)
    SummonSkeleton   = 7,   // 스켈레톤 소환  — 사망 병사 공·체 일부로 소환   (공통)
    PoisonZone       = 8,   // 독성 지대      — 이속 감소 + 지속 피해 영역    (법사·궁수)
    Meteor           = 9,   // 메테오         — 강력한 AoE 피해 + 넉백        (법사)
    Blizzard         = 10,  // 블리자드       — 공·이속 감소 + 지속 피해 영역  (법사)
    SacrificeSoldier = 11,  // 병사 희생      — 병사 즉사, 그 공·체 일부 흡수  (공통)
    Bind             = 12,  // 속박           — 단일 완전 행동불능 + 지속 피해  (공통)
    SuicideSoldier   = 13,  // 자폭 병사      — 병사가 적에게 달려 폭발        (법사)
    Berserker        = 14,  // 광전사         — 공격속도 대폭 증가 (일시)      (전사)
    IronShield       = 15,  // 철벽 방어      — 방어율 대폭 증가 (일시)        (방패)
    ArrowRain        = 16,  // 화살 비        — 범위 지속 피해                 (궁수)
    BattleCry        = 17,  // 전투 함성      — 주변 아군 공격력 증가 (일시)   (전사·방패)
    Shockwave        = 18,  // 충격파         — 전방 부채꼴 넉백               (전사)
    SwiftStrike      = 19,  // 신속 연격      — 자신·병사 공격속도 대폭 증가   (궁수)
    SummonElite      = 20,  // 정예 소환      — 강화된 병사 분대 소환          (법사)

    // ── 희귀 스킬 (직업당 부대 내 1명만) ─────────────────────────
    Bisect           = 21,  // 일도양단       — 전방 직선 경직 후 참격          (기사)
    ArrowStorm       = 22,  // 화살 폭풍      — 3연타 광역 낙하                 (궁수)
    GravityCollapse  = 23,  // 중력 붕괴      — 흡입 구속 후 붕괴 폭발          (법사)
    Bulwark          = 24,  // 불멸의 방벽    — 아군 보호막 후 폭발 + 치유      (방패)

    // ── 희귀 스킬 (공통 — 직업 무관, 전체에서 1명만) ─────────────
    ChainLightning   = 25,  // 연쇄 번개      — 적 사이를 튀며 피해 누적        (공통)
    DeathSentence    = 26,  // 사형 선고      — 낙인 후 일제 처형(즉사)         (공통)
    BloodPrice       = 27,  // 피의 대가      — 자기 체력을 태워 전방 광역       (공통)
    PiercingDash     = 28,  // 관통 돌진      — 1초 쿨 평타형 돌진 관통          (근거리)
    WarBanner        = 29,  // 군기 강림      — 주변 아군 공·공속·이속 강화      (공통)
    Gravestone       = 30,  // 비석 강림      — 비석 낙하 피해 + 스켈레톤 소환   (공통)

    // ── 우두머리 행동 패턴 (적 전용, 31~) ───────────────────────
    //  패턴도 결국 "쿨다운 돌고 → 타겟 잡고 → 이펙트 내고 → 피해 준다" 라
    //  스킬과 구조가 같다. 별도 패턴 시스템을 두지 않고 스킬로 만들어
    //  쿨다운·타겟팅·이펙트·Execute() 를 전부 재사용한다.
    //  ⚠ 아군 스킬 추첨 풀에 절대 넣지 말 것 — ActiveSkillRoller 가 걸러야 한다.
    BossCharge       = 31,  // 돌진          — 적을 관통하며 몸통박치기      (보스/엘리트)
    BossSlam         = 32,  // 분쇄 강타      — 예고 후 제자리 대반경 강타     (보스)
    BossEnrage       = 33,  // 광폭화         — 1분마다 공격력·방어관통·몸집 영구 중첩 (보스)

    // ── 소환사 시그니처 (34~, 이 프로젝트 신규) ──────────────
    //  ⚠ 31~33 은 보스 전용이라 그 위에서 센다.
    SummonSignature  = 34,  // 대표 종족 소환  — 소환사의 대표 몬스터를 무료로 부른다 (소환사)

    // ── 몬스터 고유 (35~, 이 프로젝트 신규) ──────────────────
    //  ⚠ 원작 치유 두 종(4 치유 오라 · 5 집중 치유)은 **장군만** 찾는다 — 몬스터가 쓰면 아무도 안 낫는다.
    SlimeMend        = 35,  // 치유 점액      — 주변의 다친 몬스터 아군을 제 최대 체력 비례로 회복 (힐 슬라임)

    // ── 소환사 시그니처 (36~, 2026-09-12) ─────────────────────
    ManaBurst        = 36,  // 마나 폭발      — 남은 마나 절반을 태워 비례 광역 피해 (대마법사)

    // ── 2차 업그레이드 전용 (37~, 2026-09-15) ─────────────────
    SummonBrood      = 37,  // 권속 소환      — 제 하위 종족을 주기적으로 불러낸다 (슬라임 킹·리치 킹)

    // ── 몬스터 고유 (38~, 2026-09-15) ────────────────────────
    FlameAura        = 38,  // 화염 오라      — 몸 주위에 불길이 번진다. 따라다니는 장판 (화염 멧돼지)
}

public static class ActiveSkillIdExtensions
{
    public static string IconKey(this ActiveSkillId id) => id switch
    {
        ActiveSkillId.HeavyStrike      => "skill_heavy_strike",
        ActiveSkillId.VolleyFire       => "skill_volley_fire",
        ActiveSkillId.LeapStrike       => "skill_leap_strike",
        ActiveSkillId.HealAura         => "skill_heal_aura",
        ActiveSkillId.TargetHeal       => "skill_target_heal",
        ActiveSkillId.ChargeSoldier    => "skill_charge_soldier",
        ActiveSkillId.SummonSkeleton   => "skill_summon_skeleton",
        ActiveSkillId.PoisonZone       => "skill_poison_zone",
        ActiveSkillId.Meteor           => "skill_meteor",
        ActiveSkillId.Blizzard         => "skill_blizzard",
        ActiveSkillId.SacrificeSoldier => "skill_sacrifice_soldier",
        ActiveSkillId.Bind             => "skill_bind",
        ActiveSkillId.SuicideSoldier   => "skill_suicide_soldier",
        ActiveSkillId.Berserker        => "skill_berserker",
        ActiveSkillId.IronShield       => "skill_iron_shield",
        ActiveSkillId.ArrowRain        => "skill_arrow_rain",
        ActiveSkillId.BattleCry        => "skill_battle_cry",
        ActiveSkillId.Shockwave        => "skill_shockwave",
        ActiveSkillId.SwiftStrike      => "skill_swift_strike",
        ActiveSkillId.SummonElite      => "skill_summon_elite",
        ActiveSkillId.Bisect           => "skill_bisect",
        ActiveSkillId.ArrowStorm       => "skill_arrow_storm",
        ActiveSkillId.GravityCollapse  => "skill_gravity_collapse",
        ActiveSkillId.Bulwark          => "skill_bulwark",
        ActiveSkillId.ChainLightning   => "skill_chain_lightning",
        ActiveSkillId.DeathSentence    => "skill_death_sentence",
        ActiveSkillId.BloodPrice       => "skill_blood_price",
        ActiveSkillId.PiercingDash     => "skill_piercing_dash",
        ActiveSkillId.WarBanner        => "skill_war_banner",
        ActiveSkillId.Gravestone       => "skill_gravestone",
        ActiveSkillId.BossCharge       => "skill_boss_charge",
        ActiveSkillId.BossSlam         => "skill_boss_slam",
        // 광폭화는 전용 아이콘을 따로 그리지 않고 광전사 아이콘을 빌린다 —
        // 같은 뜻의 기호가 이미 있는데 하나 더 만들 이유가 없다.
        ActiveSkillId.BossEnrage       => "skill_berserker",
        ActiveSkillId.SummonSignature  => "skill_summon_signature",
        // 치유 점액은 치유 오라 그림을 빌린다 — 광폭화와 같은 이유 (새 그림을 굽지 않는다)
        ActiveSkillId.SlimeMend        => "skill_heal_aura",
        // 권속 소환 — 시그니처 소환과 **같은 그림**을 쓴다. 하는 일이 같고(무료 소환),
        // 그림을 따로 두면 같은 동작이 두 얼굴을 갖는다 (시그니처 아이콘 주석과 같은 이유).
        ActiveSkillId.SummonBrood      => "skill_summon_signature",
        // 불 그림이라 메테오 것을 빌린다 — 전용 그림을 굽기 전까지.
        ActiveSkillId.FlameAura        => "skill_meteor",
        // 마나 폭발은 중력 붕괴 그림을 빌린다 — 같은 이유 (새 그림을 굽지 않는다)
        ActiveSkillId.ManaBurst        => "skill_gravity_collapse",
        _                              => null,
    };

    /// <summary>
    /// 몬스터 종족만 쓰는 스킬인가 (35~).
    /// ⚠ 적(용사) 추첨 풀에서 뺀다 — IsSummonerOnly 와 같은 자리(ActiveSkillRoller).
    ///   용사가 뽑으면 "몬스터 아군을 치유" 하는 스킬이 적의 손에서 플레이어를 돕는다.
    /// </summary>
    public static bool IsMonsterOnly(this ActiveSkillId id)
        => id == ActiveSkillId.SlimeMend
        // 권속은 **시전자의 종족**에서 나온다 (MonsterSpeciesData.BroodSpecies).
        // 용사가 뽑으면 MonsterRuntimeBridge 가 없어 조용히 아무 일도 안 한다.
        || id == ActiveSkillId.SummonBrood
        // 화염 멧돼지의 서명이다. 용사가 뽑아 쓰면 화면에 같은 불이 양쪽에 생겨
        // "저 불이 누구 것인가" 가 안 갈린다 — 기능이 아니라 정체성으로 막는다.
        || id == ActiveSkillId.FlameAura;

    /// <summary>
    /// 우두머리 전용 패턴 스킬인가.
    /// 아군 스킬 추첨·상점·도감에서 제외하는 기준이다.
    /// </summary>
    public static bool IsBossPattern(this ActiveSkillId id)
        => id == ActiveSkillId.BossCharge
        || id == ActiveSkillId.BossSlam
        || id == ActiveSkillId.BossEnrage;

    /// <summary>
    /// 소환사만 쓰는 스킬인가 (34~).
    ///
    /// ⚠ 적(용사) 추첨 풀에 들어가면 안 된다
    ///   SummonSignature 는 "**소환사**의 대표 종족을 부른다" 는 뜻이라,
    ///   용사가 뽑으면 적의 스킬이 플레이어의 몬스터를 불러낸다.
    ///   보스 패턴(31~33)을 거르는 것과 같은 이유·같은 자리에서 거른다
    ///   (ActiveSkillRoller).
    ///
    /// ⚠ IsRare 로 막을 수 없다 — 그쪽은 희귀 추첨(RareSkillArbiter)이 다시 집는다.
    /// </summary>
    public static bool IsSummonerOnly(this ActiveSkillId id)
        => id == ActiveSkillId.SummonSignature
        || id == ActiveSkillId.ManaBurst;   // 소환사의 마나를 태운다 — 용사가 뽑으면 플레이어 마나가 빠진다

    /// <summary>
    /// 버프·치유·소환 계열인가.
    ///
    /// ■ 왜 나누나
    ///   공격 스킬은 "적이 공격 사거리 안에 들어왔을 때" 만 나간다 (SkillUsePolicy).
    ///   사거리 밖에서 터뜨리면 긴 쿨다운이 허공에 날아간다.
    ///   반대로 버프·치유·소환은 적과의 거리와 상관없는 스킬이라 그 조건에서 빼야 한다.
    ///
    /// ■ 판정 기준
    ///   "시전 순간 시전자가 적 옆에 있어야 하는가" 로만 가른다.
    ///   병사가 대신 달려가는 소환형(돌격병사·자폭병사)은 거리가 필요 없으므로 여기에 넣는다.
    ///   피해가 섞여 있어도 주 효과가 아군 강화면 버프로 본다 (불멸의 방벽).
    /// </summary>
    public static bool IsSupport(this ActiveSkillId id) => id switch
    {
        ActiveSkillId.HealAura         => true,   // 치유 오라
        ActiveSkillId.TargetHeal       => true,   // 집중 치유
        ActiveSkillId.ChargeSoldier    => true,   // 소환 — 병사가 달려간다
        ActiveSkillId.SummonSkeleton   => true,   // 소환
        ActiveSkillId.SacrificeSoldier => true,   // 자기 강화
        ActiveSkillId.SuicideSoldier   => true,   // 소환 — 병사가 달려가 폭발
        ActiveSkillId.Berserker        => true,   // 버프
        ActiveSkillId.IronShield       => true,   // 버프
        ActiveSkillId.BattleCry        => true,   // 버프
        ActiveSkillId.SwiftStrike      => true,   // 버프
        ActiveSkillId.SummonElite      => true,   // 소환
        ActiveSkillId.Bulwark          => true,   // 보호막 + 치유
        ActiveSkillId.WarBanner        => true,   // 버프
        // 광폭화는 자기 강화다. 적이 사거리 안에 있는지 따지면 안 된다 —
        // 아군이 도망치거나 원거리로만 때리는 동안 60초 쿨이 통째로 날아간다.
        ActiveSkillId.BossEnrage       => true,   // 버프 (자기 강화)
        ActiveSkillId.SlimeMend        => true,   // 치유 — 적이 없어도 아군이 다쳤으면 쓴다
        ActiveSkillId.SummonBrood      => true,   // 소환 — 제 발밑에 세운다. 적이 멀어도 미리 불러 둔다
        // 몸에 두르는 불이다 — 타겟이 없어도 두른다. 사거리를 따지면 적이
        // 붙기 전까지 못 켜서, 정작 부딪히는 순간에 쿨다운이 돌고 있다.
        ActiveSkillId.FlameAura        => true,
        _                              => false,
    };

    /// <summary>
    /// 돌진형 — 시전자가 타겟까지 **이동해서** 때리는 스킬.
    ///
    /// ⚠ 사거리 판정을 통째로 건너뛴다
    ///   달려가서 때리는 것이 목적인데 "사거리 안에 들어와야 발동" 이면
    ///   이미 붙어 있을 때만 나간다 — 돌진할 거리가 남아 있지 않다.
    ///   실제로 도약 강타·관통 돌진은 평타 사거리(0.7~1.2)를 그대로 썼고,
    ///   1초 쿨짜리 관통 돌진이 제자리에서 헛돌았다.
    ///
    ///   대신 "적이 시야에 보이면" 을 조건으로 삼는다. AttackComponent.HasTarget
    ///   자체가 이미 그 뜻이다 — UnitTargetSearchSystem 이 그리드 탐색 반경
    ///   (CellSize × SearchRadius) 안에서 찾은 적일 때만 켜진다.
    ///   그래서 별도의 시야 수치를 새로 두지 않는다.
    /// </summary>
    public static bool IsDash(this ActiveSkillId id) => id switch
    {
        ActiveSkillId.LeapStrike   => true,   // 도약 강타 — 전방 도약
        ActiveSkillId.PiercingDash => true,   // 관통 돌진 — 1초 쿨 평타형
        ActiveSkillId.BossCharge   => true,   // 돌진      — 몸통박치기
        _                          => false,
    };

    /// <summary>
    /// 발동에 필요한 최소 사거리 배율. 1 이면 평소 공격 사거리 그대로.
    /// ⚠ 돌진형은 여기 넣지 말 것 — IsDash() 가 사거리 판정 자체를 건너뛴다.
    /// </summary>
    public static float RangeScale(this ActiveSkillId id) => id switch
    {
        ActiveSkillId.BossSlam => 1.5f,   // 근접 직전 — 제자리 강타라 붙어야 한다
        _                      => 1f,
    };
}
