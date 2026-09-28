using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

// ============================================================
//  UnitMovementSystem.cs
//  이동 + 유닛 간 분리(Separation) 처리 시스템
//
//  분리 대상: 팀 무관 — 아군/적군 모두 포함 (공격 중 겹침 방지)
//
//  실행 순서 (매 프레임):
//    ① BuildSepGridJob          — 전체 유닛 위치를 셀 맵에 등록 (병렬)
//       Complete()              — 맵 완성 보장
//    ② SeparationJob           — 겹친 유닛끼리 서로 밀어냄 (병렬)
//    ③ MoveToDestinationJob    — 목적지로 이동 (병렬)
//    ④ KnockbackJob            — 넉백 처리 (병렬)
//
//  분리 범위: 원이 아니라 **발밑 타원**이다 (SeparationJob.FootSquash)
//
//  분리 성능:
//    셀 크기 1.0f, 3×3 인접 셀 탐색 → 유닛당 평균 비교 4~8회
//    Burst 병렬 처리 → 200유닛 기준 무시 가능한 오버헤드
// ============================================================

namespace BattleGame.Units
{
    public struct SeparationEntry
    {
        public Entity Entity;
        public float3 Position;
        public float  Radius;   // GameObject.transform.localScale 기반 반경
        public float  Mass;     // 분리 질량 (General = 5, 나머지 = 1)
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitTargetSearchSystem))]
    [UpdateBefore(typeof(UnitAttackSystem))]
    public partial struct UnitMovementSystem : ISystem
    {
        NativeParallelMultiHashMap<int2, SeparationEntry> _sepGrid;

        const float SepCellSize     = 1.0f;  // 그리드 셀 크기
        const float SepStrength     = 3.3f;  // 밀어내는 힘 (2026-09-19 사용자 지시: 3.0 → +10%)

        public void OnCreate(ref SystemState state)
        {
            _sepGrid = new NativeParallelMultiHashMap<int2, SeparationEntry>(1024, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            _sepGrid.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime     = SystemAPI.Time.DeltaTime;
            bool  enemyDefeated = BattleManager.Instance != null && BattleManager.Instance.IsEnemyDefeated;

            // ⚠ "적이 화면에 들어왔나" 집계는 걷어냈다 (2026-09-07)
            //   소환수는 이제 낸 즉시 달려 나간다 — 아래 MoveToDestinationJob 참고.
            //   매 프레임 전 유닛을 도는 쿼리였으므로 함께 지운다.

            // ① 분리 그리드 빌드 (아군 + 적군 전체) ─────────────
            _sepGrid.Clear();

            int unitCount = SystemAPI.QueryBuilder()
                .WithAll<LocalTransform>()
                .WithNone<DeadTag>()
                .Build()
                .CalculateEntityCount();

            if (_sepGrid.Capacity < unitCount * 2)
                _sepGrid.Capacity = unitCount * 2;

            new BuildSepGridJob
            {
                GridWriter = _sepGrid.AsParallelWriter(),
                CellSize   = SepCellSize,
            }.ScheduleParallel();

            state.Dependency.Complete(); // 그리드 완성 대기

            // ② 분리 (팀 무관 — 공격 중 겹침 포함) ──────────────
            new SeparationJob
            {
                Grid      = _sepGrid,
                DeltaTime = deltaTime,
                CellSize  = SepCellSize,
                Strength  = SepStrength,
            }.ScheduleParallel();

            // ③ 목적지 이동 ────────────────────────────────────────
            var retreatFireLookup  = SystemAPI.GetComponentLookup<RetreatFireTag>(isReadOnly: true);
            new MoveToDestinationJob
            {
                DeltaTime          = deltaTime,
                EnemyDefeated      = enemyDefeated,
                RetreatFireLookup  = retreatFireLookup,
            }.ScheduleParallel();

            // ④ 넉백 ─────────────────────────────────────────────
            new KnockbackJob { DeltaTime = deltaTime }.ScheduleParallel();
        }
    }

    // ──────────────────────────────────────────
    // ① 분리 그리드 빌드 Job
    // ──────────────────────────────────────────

    [BurstCompile]
    [WithNone(typeof(DeadTag))]
    public partial struct BuildSepGridJob : IJobEntity
    {
        public NativeParallelMultiHashMap<int2, SeparationEntry>.ParallelWriter GridWriter;
        public float CellSize;

        public void Execute(Entity entity, in LocalTransform transform, in UnitSizeComponent size)
        {
            int2 cell = (int2)math.floor(transform.Position.xy / CellSize);
            GridWriter.Add(cell, new SeparationEntry
            {
                Entity   = entity,
                Position = transform.Position,
                Radius   = size.Radius,
                Mass     = size.Mass,
            });
        }
    }

    // ──────────────────────────────────────────
    // ② 분리 Job
    // ──────────────────────────────────────────

    [BurstCompile]
    [WithNone(typeof(DeadTag), typeof(SkillCastLock))]
    // ⚠ 마왕(StationaryTag)은 이 잡의 대상이 아니다 — 위치를 건드리는 잡 전부에서 뺀다.
    // 하나라도 빠지면 그 경로로 조금씩 떠밀린다. UnitComponents.StationaryTag 참고.
    [WithNone(typeof(StationaryTag))]
    public partial struct SeparationJob : IJobEntity
    {
        [ReadOnly] public NativeParallelMultiHashMap<int2, SeparationEntry> Grid;
        public float DeltaTime;
        public float CellSize;
        public float Strength;

        // 공격 중 밀림 감쇠 — 대규모 전투에서 어택 무빙 방지
        const float AttackingSepScale = 0.1f;

        /// <summary>
        /// 발밑 타원의 납작 — 세로(깊이) 반지름 ÷ 가로 반지름.
        ///
        /// ⚠ 분리 범위는 몸통이 아니라 **발밑 그림자** 다 (사용자 지시, 2026-09-19).
        ///   원이면 뒤에 설 유닛의 **발**이 앞 유닛의 **머리** 자리에 들어오면서
        ///   빈 자리가 있는데도 서로를 밀어낸다 — 그림과 판정이 어긋난다.
        ///   유닛 원점이 이미 발밑이므로(UnitSortingSetup 참고) 자리는 그대로 두고
        ///   세로만 눌러 타원으로 만든다.
        ///
        /// ⚠ 가로는 건드리지 않는다 — 좌우로 겹치면 그림이 그대로 포개진다.
        /// ⚠ 공격 사거리와 무관하다 — 사거리는 중심 사이 거리를 본다(UnitAttackSystem).
        ///   이 값을 내리면 유닛이 세로로 더 빽빽하게 서므로 라인 밀도만 올라간다.
        /// </summary>
        const float FootSquash = 0.4f;

        /// <summary>
        /// 밀어내는 범위 배율 — 반지름 합에 곱한다 (사용자 지시, 2026-09-19: +10%).
        ///
        /// ⚠ 두 축에 함께 걸린다 — 타원의 모양(FootSquash)은 그대로고 크기만 커진다.
        /// ⚠ UnitSizeComponent.Radius 를 키우지 않는다 — 그 값은 보스 AoE·광폭화도 읽는다.
        /// </summary>
        const float SepRangeMult = 1.1f;

        public void Execute(Entity entity, ref LocalTransform transform,
                            in UnitSizeComponent size, in UnitStateComponent unitState)
        {
            float  myRadius = size.Radius;
            float  myMass   = math.max(size.Mass, 0.01f);
            int2   myCell   = (int2)math.floor(transform.Position.xy / CellSize);
            float3 push     = float3.zero;

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int2 cell = myCell + new int2(dx, dy);
                if (!Grid.TryGetFirstValue(cell, out SeparationEntry entry, out var it))
                    continue;

                do
                {
                    if (entry.Entity == entity) continue;

                    float  pushDist = (myRadius + entry.Radius) * SepRangeMult;

                    // 발밑 타원 — 세로를 FootSquash 로 나눠 **원 문제로 환산**하여 푸는 것이다.
                    //   뒤에서 밀어낼 때는 그만큼 더 가까워져야 겹친다.
                    float3 diff     = transform.Position - entry.Position;
                    float2 s        = new float2(diff.x, diff.y / FootSquash);
                    float  distSq   = math.lengthsq(s);

                    if (distSq > 0.0001f && distSq < pushDist * pushDist)
                    {
                        float dist    = math.sqrt(distSq);
                        float overlap = pushDist - dist;

                        // 질량 기반 분리: 상대 질량이 클수록 나는 더 많이 밀림
                        // push 비율 = otherMass / (myMass + otherMass)
                        float otherMass  = math.max(entry.Mass, 0.01f);
                        float massRatio  = otherMass / (myMass + otherMass);

                        // 환산한 공간에서 밀고 세로를 다시 곱해 원래 공간으로 돌린다 —
                        // 안 돌리면 위아래로 겹쳤을 때 타원 높이의 1/FootSquash 배만큼 튀어나간다.
                        float2 dir = s / dist;
                        push += new float3(dir.x, dir.y * FootSquash, 0f)
                                * (overlap * Strength * massRatio);
                    }
                }
                while (Grid.TryGetNextValue(out entry, ref it));
            }

            if (math.lengthsq(push) > 0f)
            {
                // 공격 중 밀림 감쇠
                bool reduceSep = unitState.Current == UnitState.Attacking;
                float scale = reduceSep ? AttackingSepScale : 1f;
                transform.Position += push * (DeltaTime * scale);
            }
        }
    }

    // ──────────────────────────────────────────
    // ③ 목적지 이동 Job
    // ──────────────────────────────────────────

    [BurstCompile]
    [WithNone(typeof(DeadTag), typeof(SkillCastLock))]
    // ⚠ 마왕(StationaryTag)은 이 잡의 대상이 아니다 — 위치를 건드리는 잡 전부에서 뺀다.
    // 하나라도 빠지면 그 경로로 조금씩 떠밀린다. UnitComponents.StationaryTag 참고.
    [WithNone(typeof(StationaryTag))]
    public partial struct MoveToDestinationJob : IJobEntity
    {
        // ── 퇴각 사격의 값 ───────────────────────────────────
        //
        //  ⚠ 둘은 한 묶음이다 — 하나만 만지면 균형이 깨진다
        //    묶이는 시간을 늘리면 물러나는 속도는 올려도 되고, 반대도 같다.
        //    둘 다 후하게 주면 옛날(공짜 무빙샷)로 돌아간다.

        /// <summary>쏘면서 물러날 때의 이동속도 배율.</summary>
        const float RetreatSpeedMult = 0.5f;

        /// <summary>
        /// 발이 묶이는 구간 — 공격 간격의 앞 이만큼은 한 발짝도 못 뗀다.
        ///
        /// 쿨다운은 가득 찬 값에서 0 으로 줄어드므로, "방금 쐈다" 는
        /// 남은 쿨다운이 간격의 (1 − 이 값) 보다 큰 상태다.
        /// </summary>
        const float ShotRecoveryRatio = 0.35f;

        /// <summary>방금 쏴서 아직 발이 묶여 있는가.</summary>
        static bool InShotRecovery(float cooldownRemaining, in StatComponent stat)
        {
            // ⚠ 간격을 공속에서 되계산한다 — 남은 시간만으로는 비율을 낼 수 없다
            //   공속이 오르면 묶이는 절대 시간도 함께 줄어야 한다.
            float speed    = math.max(0.01f, stat.Final[StatType.AttackSpeed]);
            float interval = 1f / speed;

            return cooldownRemaining > interval * (1f - ShotRecoveryRatio);
        }

        public float DeltaTime;
        public bool  EnemyDefeated;
        [ReadOnly] public ComponentLookup<RetreatFireTag> RetreatFireLookup;

        public void Execute(
            Entity                     entity,
            ref LocalTransform         transform,
            ref MovementComponent      movement,
            ref UnitStateComponent     unitState,
            in  FormationSlotComponent slot,
            in  AttackComponent        attack,
            in  UnitIdentityComponent  identity,
            in  StatComponent          stat)
        {
            // 스폰 대기 중 — 이동 억제
            if (movement.MoveDelay > 0f)
            {
                movement.MoveDelay -= DeltaTime;
                movement.Velocity   = float3.zero;
                movement.IsMoving   = false;
                return;
            }

            if (unitState.Current == UnitState.Hit  ||
                unitState.Current == UnitState.Dead)
            {
                movement.Velocity = float3.zero;
                movement.IsMoving = false;
                return;
            }

            // ⚠ 보스 돌진은 여기서 처리하지 않는다
            //   돌진이 ActiveSkillId.BossCharge 스킬로 옮겨가면서
            //   BossChargeRunner 가 transform 을 직접 제어한다.
            //   시전 중에는 SkillCastLock 이 붙어 이 잡 자체가 안 돈다.

            // 공격 상태 전환 처리
            if (unitState.Current == UnitState.Attacking)
            {
                if (attack.HasTarget)
                {
                    if (attack.AttackCooldown > 0f)
                    {
                        // ── 퇴각 사격(궁수 기본) ──
                        //   타겟이 사거리 절반 이내로 붙으면 쏘면서 물러난다.
                        //
                        //   ⚠ 공짜 무빙샷이 아니다 (사용자 지적, 2026-09-07)
                        //     한때 **제 이속 그대로** 물러나며 쐈다. 근접이 영영
                        //     못 따라잡아 궁수가 혼자 라인을 갈아 버렸다.
                        //     지금은 둘로 값을 치른다:
                        //       ① 쏜 직후 ShotRecoveryRatio 만큼은 발이 묶인다
                        //       ② 그 뒤에도 RetreatSpeedMult 로만 물러난다
                        //     "쏘거나, 움직이거나" 가 되어 근접이 붙을 틈이 생긴다.
                        if (RetreatFireLookup.HasComponent(entity))
                        {
                            float r     = stat.Final[StatType.AttackRange];
                            float halfSq = r * r * 0.25f;
                            if (math.distancesq(transform.Position, attack.TargetPosition) < halfSq
                                && !InShotRecovery(attack.AttackCooldown, stat))
                            {
                                float3 retreatDir   = math.normalizesafe(transform.Position - attack.TargetPosition);
                                movement.Velocity   = retreatDir * stat.Final[StatType.MoveSpeed] * RetreatSpeedMult;
                                transform.Position += movement.Velocity * DeltaTime;
                                movement.IsMoving   = true;
                                return;
                            }
                        }
                        movement.Velocity = float3.zero;
                        movement.IsMoving = false;
                        return;
                    }
                    // 쿨다운 만료: 사거리 밖이면 즉시 추격 전환 (타겟 사망 후 새 타겟 배정 시 멈춤 방지)
                    float atkRng      = stat.Final[StatType.AttackRange];
                    float toTargetSq  = math.distancesq(transform.Position, attack.TargetPosition);
                    if (toTargetSq > atkRng * atkRng)
                        ChangeState(ref unitState, UnitState.Chasing);
                    else
                    {
                        movement.Velocity = float3.zero;
                        movement.IsMoving = false;
                        return;
                    }
                }
                else
                {
                    ChangeState(ref unitState, UnitState.Idle);
                }
            }

            float moveSpeed = stat.Final[StatType.MoveSpeed];

            // 아군 + 적 전멸 → 제자리 정지 (승리 후 몰림 방지)
            if (identity.Team == TeamType.Ally && EnemyDefeated)
            {
                movement.Velocity = float3.zero;
                movement.IsMoving = false;
                if (unitState.Current != UnitState.Idle)
                    ChangeState(ref unitState, UnitState.Idle);
                return;
            }

            // 아군(몬스터) + 타겟 없음 → **바로 +X 로 전진한다**
            //
            //  ⚠ "적이 화면에 들어올 때까지 기다린다" 는 규칙을 걷어냈다
            //    (사용자 지적, 2026-09-07)
            //    원작에서는 아군이 진형을 짜고 서 있다가 적이 오면 맞붙었다.
            //    이 게임의 아군은 **플레이어가 마나를 내고 부른 소환수**다.
            //    성문에서 나온 몬스터가 그 자리에 멈춰 서 있으면
            //      · 소환한 순간과 움직이는 순간이 갈려 "먹통" 으로 읽히고,
            //      · 성벽 앞에 몬스터가 쌓여 라인이 무슨 의미인지 사라지며,
            //      · 미리 소환해 두는 대기 시간이 통째로 죽은 시간이 된다.
            //    낸 즉시 달려 나가야 "내보냈다" 가 성립한다.
            //
            //  ⚠ 전멸 뒤 정지는 위에서 이미 처리했다 (EnemyDefeated) —
            //    승리 후 오른쪽으로 몰려 나가는 일은 여기서 생기지 않는다.
            if (identity.Team == TeamType.Ally && !attack.HasTarget)
            {
                movement.Velocity  = new float3(1f, 0f, 0f) * moveSpeed;
                transform.Position += movement.Velocity * DeltaTime;
                movement.IsMoving  = true;
                if (unitState.Current != UnitState.Moving)
                    ChangeState(ref unitState, UnitState.Moving);
                return;
            }

            // 적팀(용사) + 타겟 없음 → 성벽을 향해 계속 진군한다
            //
            // ⚠ "아군 전멸이면 제자리 정지" 를 걷어냈다 (2026-08-28)
            //   원작에서는 아군 전멸이 곧 패배였으니 적이 멈춰도 됐다.
            //   이 게임의 패배는 **소환사 사망**이다 — 몬스터를 다 잡은 용사는
            //   성벽까지 걸어와 소환사를 때려야 판이 끝난다.
            //   그 분기가 남아 있으면 몬스터가 전멸한 순간 용사가 벌판에 멎어
            //   아무 일도 일어나지 않는 교착이 된다.
            //   (생존 카운트가 잠깐 어긋나기만 해도 걸리던 분기라 더 위험했다)
            if (identity.Team == TeamType.Enemy && !attack.HasTarget)
            {
                movement.Velocity  = new float3(-1f, 0f, 0f) * moveSpeed;
                transform.Position += movement.Velocity * DeltaTime;
                movement.IsMoving  = true;

                if (unitState.Current != UnitState.Moving)
                    ChangeState(ref unitState, UnitState.Moving);
                return;
            }

            bool isChasing = unitState.Current == UnitState.Chasing && attack.HasTarget;

            // 타겟이 있지만 추격 상태가 아닌 경우 (피격 후 Idle 복귀 직후 등)
            // SlotPosition(0,0,0) 쪽으로 이동하면 Velocity.x < 0 이 되어
            // UnitAnimationSync 의 _lastFacingX 가 뒤집히므로 제자리 대기한다.
            if (attack.HasTarget && !isChasing)
            {
                movement.Velocity = float3.zero;
                movement.IsMoving = false;
                return;
            }

            float3 destination = isChasing ? attack.TargetPosition : slot.SlotPosition;

            // 추격 중에는 공격 사거리를 정지 거리로 사용 (Archer·Mage 근접 방지)
            float stoppingDist    = isChasing ? stat.Final[StatType.AttackRange] : movement.StoppingDistance;
            float3 toDestination  = destination - transform.Position;
            float  distSq         = math.lengthsq(toDestination);
            float  stoppingDistSq = stoppingDist * stoppingDist;

            if (distSq <= stoppingDistSq)
            {
                movement.Velocity = float3.zero;
                movement.IsMoving = false;

                if (unitState.Current == UnitState.Moving)
                    ChangeState(ref unitState, UnitState.Idle);
                return;
            }

            float3 direction  = math.normalize(toDestination);
            movement.Velocity = direction * moveSpeed;
            transform.Position += movement.Velocity * DeltaTime;
            movement.IsMoving   = true;

            if (math.lengthsq(movement.Velocity) > 0.001f)
            {
                float angle = math.atan2(movement.Velocity.y, movement.Velocity.x);
                transform.Rotation = quaternion.RotateZ(angle);
            }

            if (unitState.Current == UnitState.Idle)
                ChangeState(ref unitState, UnitState.Moving);
        }

        static void ChangeState(ref UnitStateComponent s, UnitState next)
        {
            s.Previous   = s.Current;
            s.Current    = next;
            s.StateTimer = 0f;
        }
    }

    // ──────────────────────────────────────────
    // ④ 넉백 처리 Job
    // ──────────────────────────────────────────

    [BurstCompile]
    [WithNone(typeof(DeadTag))]
    // ⚠ 마왕(StationaryTag)은 이 잡의 대상이 아니다 — 위치를 건드리는 잡 전부에서 뺀다.
    // 하나라도 빠지면 그 경로로 조금씩 떠밀린다. UnitComponents.StationaryTag 참고.
    [WithNone(typeof(StationaryTag))]
    public partial struct KnockbackJob : IJobEntity
    {
        public float DeltaTime;

        public readonly void Execute(
            ref LocalTransform       transform,
            ref HitReactionComponent hitReaction,
            ref UnitStateComponent   unitState)
        {
            if (!hitReaction.IsStunned) return;

            hitReaction.StunTimer -= DeltaTime;

            if (hitReaction.StunTimer <= 0f)
            {
                hitReaction.IsStunned         = false;
                hitReaction.KnockbackVelocity = float3.zero;
                ChangeState(ref unitState, UnitState.Idle);
                return;
            }

            const float KnockbackDrag = 8f;
            hitReaction.KnockbackVelocity = math.lerp(
                hitReaction.KnockbackVelocity, float3.zero, DeltaTime * KnockbackDrag);

            transform.Position += hitReaction.KnockbackVelocity * DeltaTime;
        }

        static void ChangeState(ref UnitStateComponent s, UnitState next)
        {
            s.Previous   = s.Current;
            s.Current    = next;
            s.StateTimer = 0f;
        }
    }

    // ──────────────────────────────────────────
    // ⑤ 화면 경계 클램프 System + Job
    // ──────────────────────────────────────────

    /// <summary>
    /// 이동·분리·넉백이 모두 끝난 뒤 실행.
    /// 유닛이 화면에 한 번이라도 진입하면 이후로는 화면 밖으로 밀리지 않는다.
    /// 미진입 상태로 화면 외곽 밖에 일정 시간 머물면 사망 처리한다 (걸어 들어오는 중은 봐준다).
    /// Camera.main 이 없거나 Perspective 카메라면 동작하지 않는다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitMovementSystem))]
    public partial struct ScreenClampSystem : ISystem
    {
        /// <summary>위쪽 여백 비율 (화면 절반 높이 대비). 상단바가 덮는 만큼.</summary>
        const float TopInsetRatio = 0.10f;

        /// <summary>
        /// 아래쪽 여백 비율. 소환 카드 바가 덮으므로 위보다 넉넉히 잡는다 —
        /// UI 뒤에서 싸우면 무슨 일이 벌어지는지 볼 수가 없다.
        /// </summary>
        const float BottomInsetRatio = 0.22f;

        public void OnUpdate(ref SystemState state)
        {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null || !cam.orthographic) return;

            float h    = cam.orthographicSize;
            float w    = h * cam.aspect;
            float camX = cam.transform.position.x;
            float camY = cam.transform.position.y;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // ── 세로 여백 ────────────────────────────────────────
            //
            //  화면 끝까지 허용하면 유닛이 위아래로 반쯤 잘려 나간다.
            //  아래쪽은 소환 카드 바가 덮고 있어 더 많이 비워야 한다 —
            //  UI 뒤에서 싸우면 무슨 일이 벌어지는지 볼 수가 없다.
            float topInset    = h * TopInsetRatio;
            float bottomInset = h * BottomInsetRatio;

            // ⚠ 몬스터의 오른쪽 이탈 예외는 걷어냈다 (예전 환수 방식의 잔재)
            //   예전에는 적을 정리한 뒤 몬스터가 오른쪽 화면 밖으로 걸어 나가야
            //   마나가 환수됐다. 지금은 적을 모두 잡은 그 순간 제자리에서 거둬져
            //   대기열로 돌아간다(MonsterLineReturner). 나갈 일이 없으므로 모두 화면 안에 가둔다.
            //
            //   예외를 남겨 두면 오히려 해롭다 — 그 조건이 "적 0" 이라
            //   **스테이지 대기 중에도 오른쪽이 열려** 사전 소환한 몬스터가
            //   화면 밖으로 흘러나갈 수 있었다.

            // ── 성벽 ─────────────────────────────────────────────
            //
            //  왼쪽은 성이다. 유닛이 넘어가면 소환사 뒤로 돌아 들어가 버린다.
            //  화면 왼쪽 끝보다 이쪽이 더 안쪽이면 이 값이 한계가 된다.
            float wallX = SummonFieldLayout.WallX;

            new ScreenClampJob
            {
                Min = new float2(math.max(camX - w, wallX), camY - h + bottomInset),
                Max = new float2(camX + w, camY + h - topInset),
                Ecb = ecb,

                // ⚠ 화면 밖 사망 판정은 웨이브가 도는 동안에만 켠다
                //   출전 대기 화면은 카메라를 옆으로 밀어 두므로 스폰 지점이 화면 밖으로
                //   빠질 수 있다. 그 상태에서 이 규칙이 살아 있으면 방금 세운 부대가
                //   미진입(HasEnteredScreen=false) 상태로 걸려 즉시 사망 → 1초 뒤 풀 반납된다.
                //   이 규칙의 목적은 '전투 중 넉백으로 화면 밖에 영구 방치되는 유닛 정리' 다.
                KillOutOfBounds = BattleManager.Instance != null
                               && BattleManager.Instance.IsWaveRunning,

                // 유예 시간을 재는 잣대. 배속을 그대로 탄다 — 2배속이면 유예도 2배 빨리 흐르고,
                // 걸어 들어오는 속도도 2배라 결과가 같다.
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    [WithNone(typeof(DeadTag))]
    public partial struct ScreenClampJob : IJobEntity
    {
        public float2 Min;
        public float2 Max;
        public EntityCommandBuffer.ParallelWriter Ecb;

        /// <summary>화면 밖 사망 판정 사용 여부. 웨이브 진행 중에만 true.</summary>
        public bool KillOutOfBounds;

        /// <summary>이번 프레임 경과 시간. 유예 시간을 쌓는 데 쓴다.</summary>
        public float DeltaTime;

        /// <summary>
        /// 화면에 한 번도 안 들어온 유닛이 이만큼 벗어나 있으면 즉시 사망 처리한다.
        /// 대형 넉백으로 타겟 탐색 범위 밖에 영구 방치되는 것을 막는 안전망이다.
        ///
        /// ⚠ **용사 스폰 자리와 한 묶음이다** (사용자 지적, 2026-09-07)
        ///   용사는 일부러 화면 밖(InGameSceneSetup.HeroX)에서 걸어 들어온다.
        ///   그 자리가 이 반경 밖이면 **웨이브가 시작되는 순간 부대 전체가
        ///   즉사한다** — 스폰 로그는 멀쩡히 찍히고 스킬까지 발동하는데
        ///   화면에는 아무도 안 나온다. 실제로 그렇게 됐다:
        ///     카메라 orthographicSize 12 · 16:9 → 화면 반폭 21.33
        ///     허용 = 21.33 + 4 = 25.33  <  HeroX 26   ← 전멸
        ///   지금은 HeroX 24 · 반경 6 이라 허용 27.33 으로 3.3 여유가 있다.
        ///
        ///   ⚠ 셋 중 하나라도 손대면 나머지를 다시 계산할 것 —
        ///     카메라 orthographicSize · HeroX · 이 값.
        ///
        ///   ⚠ 거리만으로는 못 막는다 — 부대가 스테이지를 따라 **커진다**
        ///     (사용자 지적, 2026-09-15 — "25스테이지쯤부터 적이 생기자마자 죽는다")
        ///     병사는 스폰 자리에서 오른쪽으로 격자를 이루며 선다
        ///     (HeroSpawner.SoldierRowSpacing 0.7 · GeneralRuntimeBridge.RowSpacing 0.7).
        ///     병사 수는 스테이지마다 부대당 +1 이라(LevelFlatSoldierCountPerLevel)
        ///     25스테이지에는 한 부대가 30기 남짓, 격자가 6열 6행이 된다 —
        ///     뒷줄이 x = 24 + 6×0.7 = 28.2 로 허용(27.33)을 넘어 **세워지자마자 즉사**했다.
        ///     거리를 넓혀도 다음 스테이지에 다시 넘는다. 그래서 유예 시간으로 바꿨다.
        const float OutOfBoundsKillDist = 6f;

        /// <summary>
        /// 허용 범위 밖에 <b>연속으로</b> 이만큼 머물면 그때 사망 처리한다.
        ///
        /// ■ 왜 시간인가 — 이 규칙이 잡으려는 것은 거리가 아니라 '방치' 다
        ///   목적은 "대형 넉백으로 화면 밖에 영구히 남은 유닛 정리" 다. 걸어 들어오는
        ///   중인 유닛과 영영 안 돌아오는 유닛은 **거리로는 같고 시간으로는 다르다.**
        ///   스폰 자리가 화면 밖인 한 거리 기준은 부대가 커질 때마다 다시 터진다.
        ///
        /// ⚠ 4초면 넉넉하다 — 유예가 끝나는 조건은 '화면 진입' 이 아니라
        ///   '허용 범위 복귀' 다. 뒷줄 병사가 되돌아와야 하는 거리는 1~2 남짓이라
        ///   보통 1초 안에 풀린다. 진짜로 방치된 유닛만 4초를 채운다.
        const float OutOfBoundsGraceSeconds = 4f;

        public void Execute(
            [ChunkIndexInQuery] int    chunkIndex,
            Entity                     entity,
            ref LocalTransform         transform,
            ref ScreenStateComponent   screen,
            ref HealthComponent        health,
            in  UnitIdentityComponent  identity)
        {
            float x = transform.Position.x;
            float y = transform.Position.y;

            // 화면 진입 감지
            if (!screen.HasEnteredScreen &&
                x >= Min.x && x <= Max.x &&
                y >= Min.y && y <= Max.y)
            {
                screen.HasEnteredScreen = true;
            }

            // ⚠ 소환사는 성 안(성벽 왼쪽)에 선다 — 클램프 대상이 아니다
            //   가두면 자기 자리에서 성벽 밖으로 밀려 나온다.
            if (identity.Type == UnitType.Summoner) return;

            if (screen.HasEnteredScreen)
            {
                // 세로는 언제나 가둔다 — 위아래로 나갈 이유가 없다.
                transform.Position.y = math.clamp(y, Min.y, Max.y);

                // 가로도 언제나 가둔다 — 화면 밖으로 나갈 유닛은 이제 없다.
                transform.Position.x = math.clamp(x, Min.x, Max.x);
            }
            else if (KillOutOfBounds)
            {
                // 미진입 상태에서 허용 범위를 벗어나 있으면 시간을 쌓는다 (웨이브 중에만).
                bool outX = x < Min.x - OutOfBoundsKillDist || x > Max.x + OutOfBoundsKillDist;
                bool outY = y < Min.y - OutOfBoundsKillDist || y > Max.y + OutOfBoundsKillDist;

                if (outX || outY)
                {
                    screen.OutOfBoundsSeconds += DeltaTime;

                    // 유예를 다 쓴 것 — 스스로 돌아올 생각이 없는 개체다.
                    if (screen.OutOfBoundsSeconds >= OutOfBoundsGraceSeconds)
                    {
                        health.CurrentHp = 0f;
                        Ecb.AddComponent<DeadTag>(chunkIndex, entity);
                    }
                }
                else
                {
                    // 범위 안으로 돌아왔다 — 처음부터 다시 잰다 (누적이 아니다).
                    screen.OutOfBoundsSeconds = 0f;
                }
            }
        }
    }
}
