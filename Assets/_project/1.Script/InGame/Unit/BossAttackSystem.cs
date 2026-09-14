using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

// ============================================================
//  BossAttackSystem.cs
//  보스 전용 공격 시스템 (MeleeAttackJob 에서 제외됨).
//
//  ■ 기능
//    1. 쿨다운 0 + 타겟 있음 → 타겟에 직접 피해 (평타 배율 ×3)
//    2. AoeRadius * 보스 크기 비율 범위 내 적팀 유닛에 스플래시 피해
//    3. 피격 유닛에게 넉백 적용
//
//  ■ 평타 배율 (BasicAttackMultiplier)
//    보스는 공격속도가 일반 적의 1/3 이라 그대로 두면 DPS 가 1/3 로 떨어진다.
//    이 손실은 Attack 스텟이 아니라 여기서 되돌린다 — 스텟을 3배로 올리면
//    Attack 을 계수로 쓰는 보스 스킬(슬램·돌진·사형선고) 피해까지 3배가 되기 때문이다.
//
//  ■ AoE 반경 스케일
//    effectiveAoeRadius = boss.AoeRadius * (bossRadius / ReferenceRadius)
//    ReferenceRadius = 0.5f (기준 크기 — 일반 유닛 반경)
//
//  ■ 실행 순서
//    UnitAttackSystem(CooldownTickJob) → 쿨다운 감소
//    BossAttackSystem                  → 보스 공격 (이후)
//    UnitHitSystem                     → HitEventBuffer 처리
// ============================================================

namespace BattleGame.Units
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitAttackSystem))]
    public partial class BossAttackSystem : SystemBase
    {
        // 일반 유닛 기준 반경 — 보스 크기 배율 계산 기준값
        const float ReferenceRadius = 0.5f;
        // 부채꼴 AoE 반각 (cos 값) — cos(60°) = 0.5 → 총 120° 부채꼴
        const float ConeCosHalfAngle = 0.5f;
        // 평타 피해 배율 — 공격속도 1/3 을 상쇄해 DPS 를 유지한다 (스킬 피해에는 적용 안 됨)
        const float BasicAttackMultiplier = 3f;

        struct BossAttackInfo
        {
            public float3   BossPosition;
            public float3   TargetPosition;
            public float3   FacingDir;            // 보스 → 타겟 방향 (부채꼴 기준 축)
            public Entity   TargetEntity;
            public float    Damage;
            public float    EffectiveAoeRadius;   // 크기 보정된 AoE 반경
            public float    AoeSplashRatio;
            public float    KnockbackForce;
            public float    KnockbackDuration;
            public TeamType AttackerTeam;
            public float    DefensePierce;        // 광폭화 스택으로 오른다
        }

        // AoE 대상 후보 (ForEach 밖에서 미리 수집)
        struct AoeCandidate
        {
            public Entity   Entity;
            public float3   Position;
            public TeamType Team;
        }

        EntityQuery _aoeQuery;

        protected override void OnCreate()
        {
            _aoeQuery = GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<UnitIdentityComponent>(),
                ComponentType.Exclude<DeadTag>());
        }

        protected override void OnUpdate()
        {
            var pendingAttacks = new NativeList<BossAttackInfo>(Allocator.Temp);

            // ── AoE 후보를 ForEach 이전에 미리 수집 ──────────────
            var aoePositions = _aoeQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var aoeIds       = _aoeQuery.ToComponentDataArray<UnitIdentityComponent>(Allocator.Temp);
            var aoeEntities  = _aoeQuery.ToEntityArray(Allocator.Temp);

            var aoeCandidates = new NativeArray<AoeCandidate>(aoePositions.Length, Allocator.Temp);
            for (int i = 0; i < aoePositions.Length; i++)
                aoeCandidates[i] = new AoeCandidate
                {
                    Entity   = aoeEntities[i],
                    Position = aoePositions[i].Position,
                    Team     = aoeIds[i].Team,
                };

            aoePositions.Dispose();
            aoeIds.Dispose();
            aoeEntities.Dispose();

            // ── 보스 공격 이벤트 수집 ────────────────────────────
            Entities
                .WithoutBurst()
                .WithAll<BossComponent>()
                .WithNone<DeadTag, RangedTag>()
                .ForEach((Entity entity,
                          ref AttackComponent    attack,
                          ref UnitStateComponent unitState,
                          in  BossComponent      boss,
                          in  LocalTransform     transform,
                          in  StatComponent      stat,
                          in  UnitSizeComponent  size,
                          in  UnitIdentityComponent identity) =>
                {
                    // ⚠ 쿨다운은 여기서 보지 않는다 — 아래 사거리 판정 뒤로 미룬다
                    //   보스 공격속도는 0.133~0.3 (3~7.5초에 한 번)이다. 예전엔 쿨다운이
                    //   차 있으면 여기서 바로 빠져나가 **TargetPosition 을 갱신하지 못했다.**
                    //   추격 목적지(UnitMovementSystem)는 그 값을 쓰므로, 보스는
                    //   "적이 있던 자리" 로 걸어가 멈춰 섰다 — 아군이 조금만 움직여도
                    //   보스가 영영 따라붙지 못하는 그 증상이다.
                    if (!attack.HasTarget) return;
                    if (!EntityManager.Exists(attack.TargetEntity)) { attack.HasTarget = false; return; }

                    // ⚠ 풀에 반납된 유닛은 '존재' 하지만 화면에 없다
                    //   EntityLink.OnDisable 은 엔티티를 파괴하지 않고 Disabled 만 붙인다
                    //   (재사용을 위해). Exists / HasComponent / GetComponentData 는
                    //   Disabled 엔티티에도 그대로 성립하므로 여기서 직접 걸러야 한다.
                    //   안 그러면 보이지 않는 유닛의 굳은 좌표로 걸어가 **허공을 때린다.**
                    //   (병사 희생·자폭 병사처럼 죽지 않고 사라지는 경로가 이 유령을 만든다)
                    if (EntityManager.HasComponent<Disabled>(attack.TargetEntity))
                    { attack.HasTarget = false; return; }

                    if (!EntityManager.HasComponent<HealthComponent>(attack.TargetEntity)) return;
                    if (EntityManager.GetComponentData<HealthComponent>(attack.TargetEntity).CurrentHp <= 0f)
                    { attack.HasTarget = false; return; }

                    float3 targetPos   = EntityManager.GetComponentData<LocalTransform>(attack.TargetEntity).Position;
                    float  attackRange = stat.Final[StatType.AttackRange];

                    // 매 프레임 갱신 — 근접 보스는 UnitAttackSystem 을 타지 않아
                    // (MeleeAttackJob 이 [WithNone(BossComponent)]) 여기가 유일한 갱신처다.
                    attack.TargetPosition = targetPos;

                    if (math.distancesq(transform.Position, targetPos) > attackRange * attackRange)
                    {
                        if (unitState.Current != UnitState.Chasing)
                        { unitState.Previous = unitState.Current; unitState.Current = UnitState.Chasing; unitState.StateTimer = 0f; }
                        return;
                    }

                    // 사거리 안이다 — 이제 쿨다운을 본다
                    if (attack.AttackCooldown > 0f) return;

                    // 공격 발동 — 쿨다운 리셋
                    attack.AttackCooldown = 1f / stat.Final[StatType.AttackSpeed];
                    unitState.Previous    = unitState.Current;
                    unitState.Current     = UnitState.Attacking;
                    unitState.StateTimer  = 0f;

                    // 크리티컬 판정
                    var   rng    = new Random(attack.RandomSeed == 0u ? 1u : attack.RandomSeed);
                    float roll   = rng.NextFloat();
                    attack.RandomSeed = rng.state;

                    // 돌진 중 공격력 보너스는 없앴다 — 돌진이 스킬(BossCharge)로
                    // 옮겨가면서 피해를 러너가 직접 계산한다. 여기서 또 얹으면 이중 적용이다.
                    // 평타만 ×3 — 스킬은 stat.Final[Attack] 을 그대로 계수로 쓴다
                    float baseAtk = stat.Final[StatType.Attack] * BasicAttackMultiplier;
                    float damage = roll < stat.Final[StatType.CritChance]
                        ? baseAtk * stat.Final[StatType.CritDamage]
                        : baseAtk;

                    // 보스 크기에 비례한 AoE 반경
                    float radiusScale       = math.max(1f, size.Radius / ReferenceRadius);
                    float effectiveAoeRadius = boss.AoeRadius * radiusScale;

                    pendingAttacks.Add(new BossAttackInfo
                    {
                        BossPosition        = transform.Position,
                        TargetPosition      = targetPos,
                        FacingDir           = math.normalizesafe(targetPos - transform.Position, new float3(-1f, 0f, 0f)),
                        TargetEntity        = attack.TargetEntity,
                        Damage              = damage,
                        EffectiveAoeRadius  = effectiveAoeRadius,
                        AoeSplashRatio      = boss.AoeSplashRatio,
                        KnockbackForce      = boss.AttackKnockbackForce,
                        KnockbackDuration   = boss.AttackKnockbackDuration,
                        AttackerTeam        = identity.Team,
                        DefensePierce       = stat.Final[StatType.DefensePenetration],
                    });
                })
                .Run();

            // ── 직접 피해 + AoE 처리 ─────────────────────────────
            for (int i = 0; i < pendingAttacks.Length; i++)
            {
                BossAttackInfo info = pendingAttacks[i];

                // 타겟 직접 피해
                ApplyHit(info.TargetEntity, info.Damage, info.BossPosition, info.TargetPosition,
                         info.KnockbackForce, info.KnockbackDuration, info.DefensePierce);

                // AoE 부채꼴 — 보스 기준 반경 + 전방 120° 이내 적 타격
                if (info.EffectiveAoeRadius > 0f)
                {
                    float sqr       = info.EffectiveAoeRadius * info.EffectiveAoeRadius;
                    float aoeDamage = info.Damage * info.AoeSplashRatio;

                    for (int j = 0; j < aoeCandidates.Length; j++)
                    {
                        AoeCandidate c = aoeCandidates[j];
                        if (c.Team == info.AttackerTeam) continue;
                        if (c.Entity == info.TargetEntity) continue;

                        // 보스 기준 반경 체크
                        if (math.distancesq(c.Position, info.BossPosition) > sqr) continue;

                        // 부채꼴 각도 체크 — 보스 전방 120° (반각 60°, cos=0.5) 이내만 피격
                        float3 toC = math.normalizesafe(c.Position - info.BossPosition, float3.zero);
                        if (math.dot(toC, info.FacingDir) < ConeCosHalfAngle) continue;

                        ApplyHit(c.Entity, aoeDamage, info.BossPosition, c.Position,
                                 info.KnockbackForce * 0.7f, info.KnockbackDuration * 0.7f,
                                 info.DefensePierce);
                    }
                }
            }

            pendingAttacks.Dispose();
            aoeCandidates.Dispose();
        }

        // ── 내부 헬퍼 ─────────────────────────────────────────

        void ApplyHit(Entity target, float damage, float3 attackerPos, float3 targetPos,
                      float knockbackForce, float knockbackDuration, float defensePierce)
        {
            if (!EntityManager.Exists(target)) return;
            if (!EntityManager.HasComponent<HitEventBufferElement>(target)) return;

            float3 dir = math.normalizesafe(targetPos - attackerPos, new float3(-1f, 0f, 0f));

            EntityManager.GetBuffer<HitEventBufferElement>(target).Add(new HitEventBufferElement
            {
                Damage         = damage,
                HitDirection   = dir,
                AttackerEntity = Entity.Null,
                DefensePierce  = defensePierce,
            });

            if (knockbackForce <= 0f) return;
            if (!EntityManager.HasComponent<HitReactionComponent>(target)) return;

            float resistance = 0f;
            if (EntityManager.HasComponent<BossComponent>(target))
                resistance = EntityManager.GetComponentData<BossComponent>(target).KnockbackResistance;
            else if (EntityManager.HasComponent<EliteComponent>(target))
                resistance = EntityManager.GetComponentData<EliteComponent>(target).KnockbackResistance;

            float effectiveDuration = knockbackDuration * (1f - resistance);
            if (effectiveDuration <= 0f) return;

            var reaction = EntityManager.GetComponentData<HitReactionComponent>(target);
            reaction.KnockbackVelocity = dir * knockbackForce * (1f - resistance);
            reaction.StunDuration      = effectiveDuration;
            reaction.StunTimer         = effectiveDuration;
            reaction.IsStunned         = true;
            reaction.NeedsFlash        = true;
            EntityManager.SetComponentData(target, reaction);
        }
    }
}
