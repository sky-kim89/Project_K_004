using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

// ============================================================
//  UnitAttackSystem.cs
//  공격 처리 시스템
//  - AttackDamage / AttackRange / AttackSpeed 는 StatComponent.Final 에서 읽음
//  - 타겟 생존 확인: HealthComponent.CurrentHp <= 0 체크
// ============================================================

namespace BattleGame.Units
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct UnitAttackSystem : ISystem
    {
        ComponentLookup<LocalTransform>       _transformLookup;
        ComponentLookup<HealthComponent>      _healthLookup;
        ComponentLookup<DoubleStrikeTag>      _doubleStrikeLookup;
        ComponentLookup<Disabled>              _disabledLookup;
        // 착탄 이벤트를 받을 대상은 "버퍼를 가진 유닛" 으로 판단한다.
        // 예전에는 GeneralComponent 유무로 걸렀는데, 그러면 병사는 아무리
        // 버퍼를 달아 줘도 착탄 이벤트를 못 받아 폭우 사격이 발동하지 않는다.
        // (원거리 경로인 ProjectileSystem 은 이미 버퍼 유무로 판단하고 있었다)
        BufferLookup<AttackHitEvent>          _attackHitLookup;

        /// <summary>성벽 너머 타겟(소환사)에게 붙는 사거리 보정. WallCoverComponent 참고.</summary>
        ComponentLookup<WallCoverComponent>   _wallCoverLookup;

        /// <summary>발사체 속도를 직업 기본값 대신 쓰는 유닛(소환사)용.</summary>
        ComponentLookup<ProjectileSpeedOverride> _projectileSpeedLookup;

        // 장비 특이 패시브 — 급소 찌르기(치명타 방어 무시) · 처형(대상 저체력 치명타 확정)
        ComponentLookup<StatComponent>    _statLookup;
        ComponentLookup<VitalStrikeTag>   _vitalLookup;
        ComponentLookup<ExecuteComponent> _executeLookup;

        // 시너지 2026-09-15 — 사냥(저체력 치명타) · 사격(투사체 하나 더) · 선봉(첫 공격)
        ComponentLookup<HuntCritComponent>        _huntLookup;
        ComponentLookup<ExtraProjectileComponent> _extraShotLookup;
        ComponentLookup<VanguardComponent>        _vanguardLookup;   // ⚠ 쓰기 — 첫 공격 표시

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _huntLookup         = state.GetComponentLookup<HuntCritComponent>(isReadOnly: true);
            _extraShotLookup    = state.GetComponentLookup<ExtraProjectileComponent>(isReadOnly: true);
            _vanguardLookup     = state.GetComponentLookup<VanguardComponent>(isReadOnly: false);
            _statLookup         = state.GetComponentLookup<StatComponent>(isReadOnly: true);
            _vitalLookup        = state.GetComponentLookup<VitalStrikeTag>(isReadOnly: true);
            _executeLookup      = state.GetComponentLookup<ExecuteComponent>(isReadOnly: true);
            _transformLookup    = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _healthLookup       = state.GetComponentLookup<HealthComponent>(isReadOnly: true);
            _doubleStrikeLookup = state.GetComponentLookup<DoubleStrikeTag>(isReadOnly: true);
            _disabledLookup     = state.GetComponentLookup<Disabled>(isReadOnly: true);
            _attackHitLookup    = state.GetBufferLookup<AttackHitEvent>(isReadOnly: true);
            _wallCoverLookup    = state.GetComponentLookup<WallCoverComponent>(isReadOnly: true);
            _projectileSpeedLookup = state.GetComponentLookup<ProjectileSpeedOverride>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            _transformLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _doubleStrikeLookup.Update(ref state);
            _disabledLookup.Update(ref state);
            _attackHitLookup.Update(ref state);
            _wallCoverLookup.Update(ref state);
            _projectileSpeedLookup.Update(ref state);
            // ⚠ 선언 · Update · 잡 대입 셋 다 — 하나만 빠지면 조용히 무효 lookup 이다
            _statLookup.Update(ref state);
            _vitalLookup.Update(ref state);
            _executeLookup.Update(ref state);
            _huntLookup.Update(ref state);
            _extraShotLookup.Update(ref state);
            _vanguardLookup.Update(ref state);

            // ① 쿨다운 감소 (병렬, 근거리 + 원거리)
            new CooldownTickJob { DeltaTime = deltaTime }.ScheduleParallel();

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // ② 근거리 공격 — 타겟 HitEventBuffer + (버퍼 보유 유닛이면) AttackHitEvent 에 직접 추가
            new MeleeAttackJob
            {
                TransformLookup    = _transformLookup,
                HealthLookup       = _healthLookup,
                DoubleStrikeLookup = _doubleStrikeLookup,
                DisabledLookup     = _disabledLookup,
                AttackHitLookup    = _attackHitLookup,
                WallCoverLookup    = _wallCoverLookup,
                StatLookup         = _statLookup,
                VitalLookup        = _vitalLookup,
                ExecuteLookup      = _executeLookup,
                HuntLookup         = _huntLookup,
                VanguardLookup     = _vanguardLookup,
                Ecb                = ecb,
            }.ScheduleParallel();

            // ③ 원거리 공격 — 자신의 ProjectileLaunchRequest 버퍼에 추가
            new RangedAttackJob
            {
                TransformLookup    = _transformLookup,
                HealthLookup       = _healthLookup,
                DoubleStrikeLookup = _doubleStrikeLookup,
                DisabledLookup     = _disabledLookup,
                WallCoverLookup    = _wallCoverLookup,
                SpeedOverrideLookup = _projectileSpeedLookup,
                StatLookup         = _statLookup,
                VitalLookup        = _vitalLookup,
                ExecuteLookup      = _executeLookup,
                HuntLookup         = _huntLookup,
                VanguardLookup     = _vanguardLookup,
                ExtraShotLookup    = _extraShotLookup,
            }.ScheduleParallel();
        }

        // ──────────────────────────────────────────
        // 성벽 보정
        // ──────────────────────────────────────────

        /// <summary>
        /// 타겟이 성벽 뒤에 있으면 그 두께만큼 사거리를 얹어 준다.
        ///
        /// 성벽에 막혀 더 다가갈 수 없는 만큼만 보정하는 것이므로,
        /// 보정을 받아도 "성벽에 붙어야 닿는다" 는 조건은 그대로다.
        /// 성벽 뒤 타겟이 아니면 0 이라 일반 전투에는 아무 영향이 없다.
        /// </summary>
        public static float ReachFor(
            in ComponentLookup<WallCoverComponent> wallCover, Entity target, float baseRange)
        {
            return wallCover.HasComponent(target)
                 ? baseRange + wallCover[target].ExtraReach
                 : baseRange;
        }

        // ──────────────────────────────────────────
        // 치명타 — 근접·원거리가 **같은 함수**를 쓴다
        // ──────────────────────────────────────────
        //
        //  ⚠ 한때 두 잡이 각자 RollDamage 를 들고 있었다. 급소·처형을 얹으면서
        //    한쪽만 고치면 "궁수만 처형이 안 된다" 가 된다 — 여기로 모았다.

        /// <summary>평타 피해를 굴린다. <paramref name="forceCrit"/> 이면 치명타가 확정된다(처형).</summary>
        public static float RollDamage(ref AttackComponent attack, in StatComponent stat,
                                       bool forceCrit, out bool crit)
            => RollDamage(ref attack, in stat, forceCrit, 0f, out crit);

        /// <param name="extraChance">이번 타격에만 더하는 치명타 확률 (사냥 은·금의 저체력 보너스).</param>
        public static float RollDamage(ref AttackComponent attack, in StatComponent stat,
                                       bool forceCrit, float extraChance, out bool crit)
        {
            var   rng   = new Random(attack.RandomSeed == 0u ? 1u : attack.RandomSeed);
            float roll  = rng.NextFloat();
            attack.RandomSeed = rng.state;

            float base_ = stat.Final[StatType.Attack];
            crit = forceCrit || roll < stat.Final[StatType.CritChance] + extraChance;
            return crit ? base_ * stat.Final[StatType.CritDamage] : base_;
        }

        /// <summary>사냥 은·금 — 대상 체력이 문턱 이하면 더할 치명타 확률. 아니면 0.</summary>
        public static float HuntChanceFor(in ComponentLookup<HuntCritComponent> hunt,
                                          in ComponentLookup<StatComponent> stats,
                                          Entity attacker, Entity target, in HealthComponent targetHealth)
        {
            if (!hunt.HasComponent(attacker) || !stats.HasComponent(target)) return 0f;

            HuntCritComponent h     = hunt[attacker];
            float             maxHp = stats[target].Final[StatType.MaxHp];
            return maxHp > 0f && targetHealth.CurrentHp <= maxHp * h.LowHpThreshold ? h.BonusChance : 0f;
        }

        /// <summary>
        /// 선봉 — 첫 공격이면 배율을 돌려주고 "첫 공격 함" 으로 표시한다. 아니면 1.
        /// ⚠ 자기 엔티티에만 쓴다 — 병렬 잡에서 안전한 이유가 그것이다.
        /// </summary>
        public static float VanguardFirstHit(ref ComponentLookup<VanguardComponent> vanguard,
                                             Entity attacker, Entity target)
        {
            if (!vanguard.HasComponent(attacker)) return 1f;

            VanguardComponent v = vanguard[attacker];
            if (v.State != 0) return 1f;

            v.State  = 1;
            v.Target = target;
            vanguard[attacker] = v;
            return v.FirstHitMult;
        }

        /// <summary>
        /// 처형 — 공격자가 가졌고 대상 체력이 문턱 이하면 true.
        /// 대상 최대 체력은 StatComponent.Final 에서 읽는다(HealthComponent 에 최대치가 없다).
        /// </summary>
        public static bool IsExecute(in ComponentLookup<ExecuteComponent> execute,
                                     in ComponentLookup<StatComponent> stats,
                                     Entity attacker, Entity target, in HealthComponent targetHealth)
        {
            if (!execute.HasComponent(attacker) || !stats.HasComponent(target)) return false;

            float maxHp = stats[target].Final[StatType.MaxHp];
            return maxHp > 0f && targetHealth.CurrentHp <= maxHp * execute[attacker].Threshold;
        }

        /// <summary>
        /// 급소 찌르기 — 치명타면 방어율을 통째로 무시한다(관통 1).
        /// ⚠ 관통은 HitEvent·발사체에 값으로 실려 간다 — 예약 피해(ProjectileIncomingDamage)도
        ///   같은 값을 보므로 따로 맞출 것이 없다.
        /// </summary>
        public static float PierceFor(in ComponentLookup<VitalStrikeTag> vital, Entity attacker,
                                      in StatComponent stat, bool crit)
            => crit && vital.HasComponent(attacker) ? 1f : stat.Final[StatType.DefensePenetration];
    }

    // ──────────────────────────────────────────
    // 쿨다운 감소 Job
    // ──────────────────────────────────────────

    [BurstCompile]
    [WithNone(typeof(DeadTag))]
    public partial struct CooldownTickJob : IJobEntity
    {
        public float DeltaTime;

        public void Execute(ref AttackComponent attack)
        {
            if (attack.AttackCooldown > 0f)
                attack.AttackCooldown -= DeltaTime;
            attack.AttackedThisFrame = false;   // 매 프레임 초기화, 이후 AttackJob에서 설정
        }
    }

    // ──────────────────────────────────────────
    // 근거리 공격 Job
    // ──────────────────────────────────────────

    /// <summary>
    /// RangedTag·BossComponent 없는 유닛(Knight, ShieldBearer, 일반 적)만 처리.
    /// 보스는 BossAttackSystem 이 별도로 처리한다 (AoE + 넉백 포함).
    /// 사거리 내 타겟이 있고 쿨다운 0 이면 HitEventBuffer 에 직접 추가.
    /// </summary>
    [BurstCompile]
    [WithNone(typeof(DeadTag), typeof(RangedTag), typeof(BossComponent), typeof(SkillCastLock))]
    public partial struct MeleeAttackJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<LocalTransform>        TransformLookup;
        [ReadOnly] public ComponentLookup<HealthComponent>       HealthLookup;
        [ReadOnly] public ComponentLookup<DoubleStrikeTag>       DoubleStrikeLookup;
        [ReadOnly] public ComponentLookup<Disabled>              DisabledLookup;
        [ReadOnly] public BufferLookup<AttackHitEvent>           AttackHitLookup;
        [ReadOnly] public ComponentLookup<WallCoverComponent>    WallCoverLookup;
        [ReadOnly] public ComponentLookup<StatComponent>         StatLookup;
        [ReadOnly] public ComponentLookup<VitalStrikeTag>        VitalLookup;
        [ReadOnly] public ComponentLookup<ExecuteComponent>      ExecuteLookup;
        [ReadOnly] public ComponentLookup<HuntCritComponent>     HuntLookup;
        [NativeDisableParallelForRestriction]
                   public ComponentLookup<VanguardComponent>     VanguardLookup;   // 자기 엔티티에만 쓴다
        public EntityCommandBuffer.ParallelWriter                 Ecb;

        public void Execute(
            [ChunkIndexInQuery] int chunkIndex,
            Entity                  entity,
            ref AttackComponent     attack,
            ref UnitStateComponent  unitState,
            in  LocalTransform      transform,
            in  StatComponent       stat,
            in  HealthComponent     health)
        {
            if (!attack.HasTarget || attack.AttackCooldown > 0f) return;
            if (unitState.Current == UnitState.Hit)      return;
            // ⚠ 자기 IsDoomed 로는 멈추지 않는다
            //   예약 피해는 "곧 죽을 예정" 일 뿐 아직 맞지도 않았다. 여기서 공격을 끊으면
            //   화살이 날아오는 동안 가만히 서 있다가 죽는다 — 화면에서는 멀쩡한 유닛이
            //   갑자기 굳는 것으로 보인다. IsDoomed 는 **남이 나를 조준하지 않게 하는**
            //   표식이지 내 행동을 멈추는 표식이 아니다 (아래 타겟 쪽 검사만 유지).
            if (!TransformLookup.HasComponent(attack.TargetEntity)) return;
            if (!HealthLookup.HasComponent(attack.TargetEntity))    return;

            // ⚠ 풀에 반납된 유닛은 '존재' 하지만 화면에 없다
            //   EntityLink.OnDisable 은 엔티티를 파괴하지 않고 Disabled 만 붙인다
            //   (재사용을 위해). 그런데 Exists / HasComponent / GetComponentData 는
            //   Disabled 엔티티에도 그대로 성립하므로, 여기서 걸러 내지 않으면
            //   **보이지 않는 유닛을 계속 때린다** — 마지막 위치에 좌표가 굳어 있어
            //   그쪽으로 걸어가 허공을 향해 공격 모션을 반복하게 된다.
            //   (쿼리는 Disabled 를 자동 제외하지만 ComponentLookup 은 하지 않는다)
            if (DisabledLookup.HasComponent(attack.TargetEntity)) { attack.HasTarget = false; return; }

            // 이미 죽었거나(HP 0), 날아오는 발사체로 사망이 확정된 타겟은 놓는다 — 오버킬 방지
            var targetHealth = HealthLookup[attack.TargetEntity];
            if (targetHealth.CurrentHp <= 0f || targetHealth.IsDoomed) { attack.HasTarget = false; return; }

            float3 targetPos   = TransformLookup[attack.TargetEntity].Position;
            attack.TargetPosition = targetPos;

            // 성벽 뒤 타겟(소환사)이면 성벽 두께만큼 사거리를 얹는다 — 근접 용사가
            // 성벽에 막혀 영원히 못 때리는 것을 막는다 (WallCoverComponent 참고).
            float attackRange = UnitAttackSystem.ReachFor(
                                    in WallCoverLookup, attack.TargetEntity,
                                    stat.Final[StatType.AttackRange]);
            float  distSq      = math.distancesq(transform.Position, targetPos);

            if (distSq > attackRange * attackRange)
            {
                // ⚠ 못 움직이는 유닛(소환사)은 추격 상태로 두면 안 된다
                //   UnitAnimationSync 가 Chasing 을 Run 으로 옮기므로 성벽 뒤에
                //   못 박힌 소환사가 제자리에서 달리는 그림이 된다. 게다가 이 시스템은
                //   UnitMovementSystem 보다 **뒤에** 돌아서, 이동 잡이 Idle 로 되돌려도
                //   같은 프레임 안에 다시 Chasing 이 덮인다 — 여기서 갈라야 한다.
                UnitState outOfRange = stat.Final[StatType.MoveSpeed] > 0f
                                     ? UnitState.Chasing
                                     : UnitState.Idle;
                if (unitState.Current != outOfRange) ChangeState(ref unitState, outOfRange);
                return;
            }

            attack.AttackCooldown = 1f / stat.Final[StatType.AttackSpeed];
            ChangeState(ref unitState, UnitState.Attacking);

            bool  forceCrit   = UnitAttackSystem.IsExecute(in ExecuteLookup, in StatLookup,
                                                           entity, attack.TargetEntity, targetHealth);
            float huntChance  = UnitAttackSystem.HuntChanceFor(in HuntLookup, in StatLookup,
                                                               entity, attack.TargetEntity, targetHealth);
            float finalDamage = UnitAttackSystem.RollDamage(ref attack, in stat, forceCrit, huntChance, out bool crit);
            float pierce      = UnitAttackSystem.PierceFor(in VitalLookup, entity, in stat, crit);

            finalDamage *= UnitAttackSystem.VanguardFirstHit(ref VanguardLookup, entity, attack.TargetEntity);

            float3 hitDir = math.normalize(targetPos - transform.Position);

            attack.AttackedThisFrame = true;
            attack.LastDamageDealt   = finalDamage;

            int hitCount = DoubleStrikeLookup.HasComponent(entity) ? 2 : 1;
            for (int h = 0; h < hitCount; h++)
                Ecb.AppendToBuffer(chunkIndex, attack.TargetEntity, new HitEventBufferElement
                {
                    Damage         = finalDamage,
                    HitDirection   = hitDir,
                    AttackerEntity = entity,
                    DefensePierce  = pierce,
                });

            // 근거리 공격 착탄 — OnAttackLanded 트리거용 (ECB → 다음 프레임)
            // 장군 버퍼는 CombatTriggerSystem 이 비운다.
            if (AttackHitLookup.HasBuffer(entity))
                Ecb.AppendToBuffer(chunkIndex, entity, new AttackHitEvent
                {
                    TargetEntity = attack.TargetEntity,
                    TargetPos    = targetPos,
                    Damage       = finalDamage,
                });
        }

        static void ChangeState(ref UnitStateComponent s, UnitState next)
        { s.Previous = s.Current; s.Current = next; s.StateTimer = 0f; }
    }

    // ──────────────────────────────────────────
    // 원거리 공격 Job
    // ──────────────────────────────────────────

    /// <summary>
    /// RangedTag 유닛(Archer, Mage)만 처리.
    /// 사거리 내 타겟이 있고 쿨다운 0 이면 자신의 ProjectileLaunchRequest 버퍼에 추가.
    /// ProjectileSpawnSystem 이 같은 프레임에 버퍼를 읽어 발사체를 스폰한다.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(RangedTag))]
    [WithNone(typeof(DeadTag), typeof(SkillCastLock))]
    public partial struct RangedAttackJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<LocalTransform>  TransformLookup;
        [ReadOnly] public ComponentLookup<HealthComponent> HealthLookup;
        [ReadOnly] public ComponentLookup<DoubleStrikeTag> DoubleStrikeLookup;
        [ReadOnly] public ComponentLookup<Disabled>        DisabledLookup;
        [ReadOnly] public ComponentLookup<WallCoverComponent> WallCoverLookup;
        [ReadOnly] public ComponentLookup<ProjectileSpeedOverride> SpeedOverrideLookup;
        [ReadOnly] public ComponentLookup<StatComponent>         StatLookup;
        [ReadOnly] public ComponentLookup<VitalStrikeTag>        VitalLookup;
        [ReadOnly] public ComponentLookup<ExecuteComponent>      ExecuteLookup;
        [ReadOnly] public ComponentLookup<HuntCritComponent>     HuntLookup;
        [ReadOnly] public ComponentLookup<ExtraProjectileComponent> ExtraShotLookup;
        [NativeDisableParallelForRestriction]
                   public ComponentLookup<VanguardComponent>     VanguardLookup;   // 자기 엔티티에만 쓴다

        const float ArrowSpeed     = 15f;
        const float MagicBoltSpeed = 10f;

        public void Execute(
            Entity                                 entity,
            ref AttackComponent                    attack,
            ref UnitStateComponent                 unitState,
            ref DynamicBuffer<ProjectileLaunchRequest> launchBuffer,
            in  LocalTransform                     transform,
            in  StatComponent                      stat,
            in  UnitIdentityComponent              identity,
            in  UnitJobComponent                   jobComp,
            in  HealthComponent                    health)
        {
            if (!attack.HasTarget || attack.AttackCooldown > 0f) return;
            if (unitState.Current == UnitState.Hit) return;  // 속박/스턴 중 공격 불가
            // 자기 IsDoomed 로는 멈추지 않는다 — MeleeAttackJob 의 같은 자리 주석 참고
            if (!TransformLookup.HasComponent(attack.TargetEntity)) return;
            if (!HealthLookup.HasComponent(attack.TargetEntity))    return;

            // 풀에 반납된 유닛은 놓는다 — MeleeAttackJob 의 같은 자리 주석 참고
            if (DisabledLookup.HasComponent(attack.TargetEntity)) { attack.HasTarget = false; return; }

            // 이미 죽었거나, 날아가는 발사체로 사망이 확정된 타겟은 놓는다.
            // 한 명에게 화살이 몰려 낭비되는 것을 막는 핵심 분기.
            var targetHealth = HealthLookup[attack.TargetEntity];
            if (targetHealth.CurrentHp <= 0f || targetHealth.IsDoomed) { attack.HasTarget = false; return; }

            float3 targetPos   = TransformLookup[attack.TargetEntity].Position;
            attack.TargetPosition = targetPos;  // 이동 시스템에 항상 최신 위치 전달

            // 성벽 뒤 타겟이면 성벽 두께만큼 보정 — 근접과 같은 규칙을 쓴다.
            // 원거리는 사거리가 성벽 두께보다 훨씬 길어 실질적으로 달라지지 않지만,
            // 두 경로가 다른 규칙을 쓰면 나중에 한쪽만 고쳐지는 종류의 버그가 난다.
            float attackRange = UnitAttackSystem.ReachFor(
                                    in WallCoverLookup, attack.TargetEntity,
                                    stat.Final[StatType.AttackRange]);
            float  distSq      = math.distancesq(transform.Position, targetPos);

            if (distSq > attackRange * attackRange)
            {
                // ⚠ 못 움직이는 유닛(소환사)은 추격 상태로 두면 안 된다
                //   UnitAnimationSync 가 Chasing 을 Run 으로 옮기므로 성벽 뒤에
                //   못 박힌 소환사가 제자리에서 달리는 그림이 된다. 게다가 이 시스템은
                //   UnitMovementSystem 보다 **뒤에** 돌아서, 이동 잡이 Idle 로 되돌려도
                //   같은 프레임 안에 다시 Chasing 이 덮인다 — 여기서 갈라야 한다.
                UnitState outOfRange = stat.Final[StatType.MoveSpeed] > 0f
                                     ? UnitState.Chasing
                                     : UnitState.Idle;
                if (unitState.Current != outOfRange) ChangeState(ref unitState, outOfRange);
                return;
            }

            attack.AttackCooldown = 1f / stat.Final[StatType.AttackSpeed];
            ChangeState(ref unitState, UnitState.Attacking);

            bool  forceCrit   = UnitAttackSystem.IsExecute(in ExecuteLookup, in StatLookup,
                                                           entity, attack.TargetEntity, targetHealth);
            float huntChance  = UnitAttackSystem.HuntChanceFor(in HuntLookup, in StatLookup,
                                                               entity, attack.TargetEntity, targetHealth);
            float finalDamage = UnitAttackSystem.RollDamage(ref attack, in stat, forceCrit, huntChance, out bool crit);
            float pierce      = UnitAttackSystem.PierceFor(in VitalLookup, entity, in stat, crit);

            finalDamage *= UnitAttackSystem.VanguardFirstHit(ref VanguardLookup, entity, attack.TargetEntity);

            attack.AttackedThisFrame = true;
            attack.LastDamageDealt   = finalDamage;

            var request = new ProjectileLaunchRequest
            {
                TargetEntity   = attack.TargetEntity,
                AttackerEntity = entity,
                AttackerPos    = transform.Position,
                TargetPos      = targetPos,
                Damage         = finalDamage,
                Speed          = SpeedOverrideLookup.HasComponent(entity)
                                   ? SpeedOverrideLookup[entity].Speed
                                   : (jobComp.Job == UnitJob.Archer ? ArrowSpeed : MagicBoltSpeed),
                Team           = identity.Team,
                DefensePierce  = pierce,
            };

            int launchCount = DoubleStrikeLookup.HasComponent(entity) ? 2 : 1;
            for (int h = 0; h < launchCount; h++)
                launchBuffer.Add(request);

            // 사격 금 — 투사체 하나 더. 같은 대상, 약한 피해. 겹쳐 보이지 않게 조금 위에서 쏜다.
            if (ExtraShotLookup.HasComponent(entity))
            {
                var extra = request;
                extra.Damage      = finalDamage * ExtraShotLookup[entity].Ratio;
                extra.AttackerPos = transform.Position + new float3(0f, 0.35f, 0f);
                launchBuffer.Add(extra);
            }
        }

        static void ChangeState(ref UnitStateComponent s, UnitState next)
        { s.Previous = s.Current; s.Current = next; s.StateTimer = 0f; }
    }
}
