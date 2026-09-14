using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using BattleGame.Units;      // ProjectileLaunchRequest, RangedTag, UnitJobComponent, HitEventBufferElement
using BattleGame.Projectiles; // ProjectileComponent, ProjectileGoLink, ProjectileDestroyTag

// ============================================================
//  ProjectileSystem.cs
//  발사체 생성 · 이동 · 피격 · 소멸 시스템.
//
//  실행 순서 (SimulationSystemGroup 내):
//    ① ProjectileSpawnSystem   — 발사 요청 버퍼 처리 (non-Burst, managed 접근 필요)
//    ② ProjectileMovementSystem — 이동 + Lifetime 감소 (Burst 병렬)
//    ③ ProjectileHitSystem      — 피격 판정 (Burst 병렬)
//    ④ ProjectileDestroySystem  — GO 반납 (non-Burst, PoolController 접근 필요)
//    ⑤ ProjectileIncomingDamageSystem — 예약 피해 재계산
//
//  피격 흐름:
//    ProjectileHitJob → HitEventBuffer append (ECB) + ProjectileDestroyTag 추가
//    → UnitHitSystem 이 HitEvent 처리 (기존 로직 그대로)
//
//  ■ 예약 피해 (집중공격 낭비 방지)
//    발사체는 날아가는 동안에도 "이미 맞은 것"으로 친다.
//    ProjectileIncomingDamageSystem 이 매 프레임 살아있는 발사체를 훑어
//    타겟의 HealthComponent.IncomingDamage 를 처음부터 다시 채운다.
//    실효 체력(CurrentHp - IncomingDamage)이 0 이하가 된 유닛 = IsDoomed:
//      - 새 공격의 타겟 후보에서 제외 (BuildGridMapJob / 공격 Job)
//      - 스스로도 공격하지 않음 — 이동만 한다
//      - 날아오던 발사체는 그대로 명중하고, 그때 실제 HP 가 깎여 사망 처리된다
//    누적이 아니라 매 프레임 재계산이므로 발사체가 빗나가거나 수명이 다해 사라져도
//    다음 프레임에 자동으로 원복된다 — 환불 로직이 필요 없다.
// ============================================================

namespace BattleGame.Projectiles
{
    // ══════════════════════════════════════════════════════════
    // ① 발사체 스폰 — 발사 요청 버퍼 처리
    // ══════════════════════════════════════════════════════════

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ProjectileMovementSystem))]
    public partial struct ProjectileSpawnSystem : ISystem
    {
        const string ArrowPoolKey     = "Arrow";
        const string MagicBoltPoolKey = "MagicBolt";

        // ── 직업별 풀 키 ──────────────────────────────────────
        static string GetPoolKey(UnitJob job) => job switch
        {
            UnitJob.Mage => MagicBoltPoolKey,
            _            => ArrowPoolKey,
        };

        public readonly void OnUpdate(ref SystemState state)
        {
            if (PoolController.Instance == null) return;

            // 로컬 변수 — ISystem 은 unmanaged struct 이므로 List 필드 불가
            var pending = new List<(string poolKey, ProjectileLaunchRequest req)>();

            // ── 1단계: 반복 중 데이터만 수집 (구조 변경 없음) ────
            foreach (var (launchBuffer, jobComp) in
                SystemAPI.Query<DynamicBuffer<ProjectileLaunchRequest>, RefRO<UnitJobComponent>>()
                         .WithAll<RangedTag>())
            {
                if (launchBuffer.IsEmpty) continue;

                string poolKey = GetPoolKey(jobComp.ValueRO.Job);
                foreach (var req in launchBuffer)
                    pending.Add((poolKey, req));

                launchBuffer.Clear(); // 길이만 0으로 — 구조 변경 아님, 반복 중 안전
            }

            if (pending.Count == 0) return;

            // ── 2단계: 반복 완료 후 스폰 + ECS 구조 변경 ─────────
            state.EntityManager.CompleteAllTrackedJobs();

            foreach (var (poolKey, req) in pending)
            {
                var go = PoolController.Instance.Spawn(
                    PoolType.Projectile, poolKey,
                    new Vector3(req.AttackerPos.x, req.AttackerPos.y, req.AttackerPos.z),
                    Quaternion.identity);

                if (go == null)
                {
                    Debug.LogWarning($"[ProjectileSpawnSystem] 풀 스폰 실패: '{poolKey}'");
                    continue;
                }

                if (go.TryGetComponent<ProjectileView>(out var view))
                {
                    float arcHeight = poolKey == ArrowPoolKey ? 1.5f : 0f;
                    view.Launch(req, arcHeight);
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    // ② 이동 + Lifetime 감소
    // ══════════════════════════════════════════════════════════

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileSpawnSystem))]
    public partial struct ProjectileMovementSystem : ISystem
    {
        ComponentLookup<LocalTransform>  _transformLookup;
        ComponentLookup<HealthComponent> _healthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _healthLookup    = state.GetComponentLookup<HealthComponent>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _healthLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new ProjectileMoveJob
            {
                TransformLookup = _transformLookup,
                HealthLookup    = _healthLookup,
                DeltaTime       = SystemAPI.Time.DeltaTime,
                Ecb             = ecb,
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    [WithNone(typeof(ProjectileDestroyTag))]
    public partial struct ProjectileMoveJob : IJobEntity
    {
        // 자신의 LocalTransform(쓰기)과 타겟의 LocalTransform(읽기)이 다른 entity 임을
        // 안전 시스템이 구분 못하므로 명시적으로 억제 — 실제 aliasing 없음
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public ComponentLookup<LocalTransform>  TransformLookup;
        [ReadOnly] public ComponentLookup<HealthComponent> HealthLookup;
        public float                                       DeltaTime;
        public EntityCommandBuffer.ParallelWriter          Ecb;

        public void Execute(
            [ChunkIndexInQuery] int    chunkIndex,
            Entity                     entity,
            ref ProjectileComponent    proj,
            ref LocalTransform         transform)
        {
            // Lifetime 감소 — 만료 시 소멸 요청
            proj.Lifetime -= DeltaTime;
            if (proj.Lifetime <= 0f)
            {
                Ecb.AddComponent<ProjectileDestroyTag>(chunkIndex, entity);
                return;
            }

            // 타겟 생존 확인 — 사망 즉시 Entity.Null 로 무효화
            // 이유: 유닛은 풀 반납 후 같은 Entity 로 재사용되므로,
            //       무효화하지 않으면 재스폰된 유닛을 계속 추적하는 버그 발생
            if (proj.TargetEntity != Entity.Null)
            {
                bool alive = TransformLookup.HasComponent(proj.TargetEntity) &&
                             HealthLookup.HasComponent(proj.TargetEntity)    &&
                             HealthLookup[proj.TargetEntity].CurrentHp > 0f;

                if (alive)
                {
                    proj.TargetPos = TransformLookup[proj.TargetEntity].Position;
                }
                else
                {
                    // TargetPos 는 이미 마지막 위치로 캐시됨 — Entity 만 무효화
                    proj.TargetEntity = Entity.Null;

                    // Arrow: 남은 거리 기반으로 TotalTime 재계산 (속도 일정하게 유지)
                    if (proj.ArcHeight > 0f)
                    {
                        float remaining = math.distance(transform.Position, proj.TargetPos);
                        proj.TotalTime  = proj.ElapsedTime +
                                          (proj.Speed > 0.0001f ? remaining / proj.Speed : 0.1f);
                    }
                }
            }

            if (proj.ArcHeight > 0f)
            {
                // ── Arrow: 포물선 이동 ─────────────────────────────
                // t=0(발사) → t=1(도달), sin 곡선으로 Y 오프셋 추가
                proj.ElapsedTime += DeltaTime;
                float t = proj.TotalTime > 0.0001f
                    ? math.saturate(proj.ElapsedTime / proj.TotalTime)
                    : 1f;

                float3 basePos = math.lerp(proj.StartPos, proj.TargetPos, t);
                basePos.y += proj.ArcHeight * math.sin(t * math.PI);
                transform.Position = basePos;
            }
            else
            {
                // ── MagicBolt: 직선 이동 ──────────────────────────
                float3 diff = proj.TargetPos - transform.Position;
                float  dist = math.length(diff);
                if (dist < 0.01f) return;

                float step = proj.Speed * DeltaTime;
                transform.Position += math.normalize(diff) * math.min(step, dist);
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    // ③ 피격 판정
    // ══════════════════════════════════════════════════════════

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileMovementSystem))]
    public partial struct ProjectileHitSystem : ISystem
    {
        ComponentLookup<LocalTransform>  _transformLookup;
        ComponentLookup<HealthComponent> _healthLookup;
        BufferLookup<AttackHitEvent>     _attackHitLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup  = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _healthLookup     = state.GetComponentLookup<HealthComponent>(isReadOnly: true);
            _attackHitLookup  = state.GetBufferLookup<AttackHitEvent>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _attackHitLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new ProjectileHitJob
            {
                TransformLookup  = _transformLookup,
                HealthLookup     = _healthLookup,
                AttackHitLookup  = _attackHitLookup,
                Ecb              = ecb,
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    [WithNone(typeof(ProjectileDestroyTag))]
    public partial struct ProjectileHitJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<LocalTransform>  TransformLookup;
        [ReadOnly] public ComponentLookup<HealthComponent> HealthLookup;
        [ReadOnly] public BufferLookup<AttackHitEvent>     AttackHitLookup;
        public EntityCommandBuffer.ParallelWriter          Ecb;

        const float HitRadiusSq = 0.5f * 0.5f;  // 타격 판정 반경

        public void Execute(
            [ChunkIndexInQuery] int  chunkIndex,
            Entity                   entity,
            in  ProjectileComponent  proj,
            in  LocalTransform       transform)
        {
            // 타겟 위치 결정 (생존 시 현재 위치, 사망 시 마지막 캐시)
            float3 targetPos;
            bool   targetAlive = false;

            // Entity.Null 체크 — MoveJob 이 사망 감지 시 이미 무효화했으므로
            // 재스폰된 유닛(같은 Entity ID)을 살아있는 타겟으로 오판하지 않음
            if (proj.TargetEntity != Entity.Null          &&
                TransformLookup.HasComponent(proj.TargetEntity) &&
                HealthLookup.HasComponent(proj.TargetEntity)    &&
                HealthLookup[proj.TargetEntity].CurrentHp > 0f)
            {
                targetPos   = TransformLookup[proj.TargetEntity].Position;
                targetAlive = true;
            }
            else
            {
                targetPos = proj.TargetPos;
            }

            // 도달 체크
            if (math.distancesq(transform.Position, targetPos) > HitRadiusSq) return;

            // 타겟 생존 시 피격 이벤트 등록 (기존 HitSystem 이 처리)
            if (targetAlive)
            {
                float3 hitDir = math.lengthsq(targetPos - proj.StartPos) > 0f
                    ? math.normalize(targetPos - proj.StartPos)
                    : float3.zero;

                Ecb.AppendToBuffer(chunkIndex, proj.TargetEntity, new HitEventBufferElement
                {
                    Damage         = proj.Damage,
                    HitDirection   = hitDir,
                    AttackerEntity = proj.AttackerEntity,
                    DefensePierce  = proj.DefensePierce,
                });
            }

            // 발사체 착탄 — OnAttackLanded 트리거용 (ECB → 다음 프레임 CombatTriggerSystem)
            // 버퍼가 있는 엔티티(장군)에만 append — 병사 원거리 공격은 버퍼 없음
            if (proj.AttackerEntity != Entity.Null && AttackHitLookup.HasBuffer(proj.AttackerEntity))
                Ecb.AppendToBuffer(chunkIndex, proj.AttackerEntity, new AttackHitEvent
                {
                    TargetEntity = proj.TargetEntity,
                    TargetPos    = targetPos,
                    Damage       = proj.Damage,
                });

            // 소멸 요청
            Ecb.AddComponent<ProjectileDestroyTag>(chunkIndex, entity);
        }
    }

    // ══════════════════════════════════════════════════════════
    // ⑤ 예약 피해 재계산 — 비행 중 발사체가 확정지은 피해를 타겟에 반영
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 매 프레임 모든 유닛의 IncomingDamage 를 0 으로 지운 뒤,
    /// 살아있는 발사체의 피해량(방어율 적용 후)을 타겟에 다시 누적한다.
    ///
    /// 누적 필드가 아니라 "재계산" 이므로 발사체가 소멸하든 타겟이 바뀌든
    /// 다음 프레임에 저절로 정합성이 맞는다.
    ///
    /// ⚠ 방어율 환산은 DamageMath.AfterDefense 만 쓴다 — UnitHitSystem 이 실제로
    ///   깎는 값과 어긋나면 예약이 과대평가되어 적이 죽지도 맞지도 않게 된다.
    /// </summary>
    // GameplayConfig(managed) 를 읽으므로 시스템 자체는 Burst 미적용 — Job 만 Burst.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileDestroySystem))]
    public partial struct ProjectileIncomingDamageSystem : ISystem
    {
        ComponentLookup<HealthComponent>  _healthLookup;
        ComponentLookup<StatComponent>    _statLookup;
        ComponentLookup<InvulnerableTag>  _invulnerableLookup;

        public void OnCreate(ref SystemState state)
        {
            _healthLookup       = state.GetComponentLookup<HealthComponent>();
            _statLookup         = state.GetComponentLookup<StatComponent>(isReadOnly: true);
            _invulnerableLookup = state.GetComponentLookup<InvulnerableTag>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var cfg = GameplayConfig.Current;
            if (cfg == null) return;

            _healthLookup.Update(ref state);
            _statLookup.Update(ref state);
            _invulnerableLookup.Update(ref state);

            // ① 초기화 — 병렬 가능 (각 엔티티가 자기 것만 건드린다)
            state.Dependency = new ClearIncomingDamageJob().ScheduleParallel(state.Dependency);

            // ② 누적 — 여러 발사체가 같은 타겟을 쓰므로 단일 스레드로 처리
            state.Dependency = new AccumulateIncomingDamageJob
            {
                HealthLookup        = _healthLookup,
                StatLookup          = _statLookup,
                InvulnerableLookup  = _invulnerableLookup,
                DefenseSoftCap      = cfg.DefenseMax,
                DefenseOverflowRate = cfg.DefenseOverflowRate,
                DefenseEffectiveCap = cfg.DefenseEffectiveCap,
            }.Schedule(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct ClearIncomingDamageJob : IJobEntity
    {
        public void Execute(ref HealthComponent health) => health.IncomingDamage = 0f;
    }

    /// <summary>
    /// ProjectileDestroyTag 가 붙은 발사체도 포함한다 — 명중 판정은 끝났지만
    /// HitEvent 는 다음 프레임 UnitHitSystem 이 처리하므로 아직 HP 가 안 깎였다.
    /// </summary>
    [BurstCompile]
    public partial struct AccumulateIncomingDamageJob : IJobEntity
    {
        [NativeDisableContainerSafetyRestriction]
        public ComponentLookup<HealthComponent>            HealthLookup;
        [ReadOnly] public ComponentLookup<StatComponent>   StatLookup;
        [ReadOnly] public ComponentLookup<InvulnerableTag> InvulnerableLookup;
        public float DefenseSoftCap;
        public float DefenseOverflowRate;
        public float DefenseEffectiveCap;

        public void Execute(in ProjectileComponent proj)
        {
            Entity target = proj.TargetEntity;
            if (target == Entity.Null)                return;  // MoveJob 이 무효화한 발사체
            if (!HealthLookup.HasComponent(target))   return;
            if (!StatLookup.HasComponent(target))     return;

            // 불사 유닛은 예약을 잡지 않는다 — 실효 체력이 0 이하가 되면 IsDoomed 가 서고,
            // 그러면 UnitAttackSystem 이 '이미 죽은 놈' 으로 보고 공격을 멈춘다.
            if (InvulnerableLookup.HasComponent(target)) return;

            var health = HealthLookup[target];
            if (health.CurrentHp <= 0f) return;

            health.IncomingDamage += DamageMath.AfterDefense(
                proj.Damage,
                StatLookup[target].Final[StatType.Defense],
                proj.DefensePierce,
                DefenseSoftCap, DefenseOverflowRate, DefenseEffectiveCap);

            HealthLookup[target] = health;
        }
    }

    // ══════════════════════════════════════════════════════════
    // ④ GO 반납 — ProjectileDestroyTag 처리
    // ══════════════════════════════════════════════════════════

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileHitSystem))]
    public partial struct ProjectileDestroySystem : ISystem
    {
        public readonly void OnUpdate(ref SystemState state)
        {
            if (PoolController.Instance == null) return;

            // ── 1단계: 반복 중 GO 목록만 수집 ────────────────────
            var toDestroy = new List<GameObject>();
            foreach (var goLink in
                SystemAPI.Query<ProjectileGoLink>()
                         .WithAll<ProjectileDestroyTag>())
            {
                if (goLink.Go != null && goLink.Go.activeSelf)
                    toDestroy.Add(goLink.Go);
            }

            if (toDestroy.Count == 0) return;

            // ── 2단계: 반복 완료 후 Despawn (EntityLink.OnDisable → Disabled 추가) ─
            state.EntityManager.CompleteAllTrackedJobs();
            foreach (var go in toDestroy)
                PoolController.Instance.Despawn(go);
        }
    }
}
