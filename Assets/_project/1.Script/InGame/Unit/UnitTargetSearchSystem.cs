using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

// ============================================================
//  UnitTargetSearchSystem.cs
//  Grid 기반 타겟 탐색 시스템
//  - 전체 유닛 O(n²) 순회 없이 인접 Grid 셀만 탐색
//  - 적팀 유닛 중 가장 가까운 유닛을 타겟으로 설정
//  - 중앙선(battleCenterX)을 넘은 침투 적은 폴백 탐색으로 항상 감지
//  - 침투 적 추격 임계값 50유닛, 전방 적은 그리드 범위(9유닛) 유지
// ============================================================

namespace BattleGame.Units
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(UnitAttackSystem))]
    public partial struct UnitTargetSearchSystem : ISystem
    {
        NativeParallelMultiHashMap<int2, UnitGridEntry> _gridMap;
        float _battleCenterX;   // 전장 중앙 X — 이 선을 넘은 적만 폴백 탐색 대상

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _gridMap       = new NativeParallelMultiHashMap<int2, UnitGridEntry>(1024, Allocator.Persistent);
            _battleCenterX = 0f;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _gridMap.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _gridMap.Clear();

            int unitCount = SystemAPI.QueryBuilder()
                .WithAll<UnitIdentityComponent, LocalTransform>()
                .WithNone<DeadTag>()
                .Build()
                .CalculateEntityCount();

            if (_gridMap.Capacity < unitCount)
                _gridMap.Capacity = unitCount * 2;

            // ① Grid 맵 빌드
            var gridWriter = _gridMap.AsParallelWriter();
            new BuildGridMapJob
            {
                GridWriter  = gridWriter,
                TauntLookup = SystemAPI.GetComponentLookup<TauntTag>(true),
            }.ScheduleParallel();

            state.Dependency.Complete();

            // ② 선두 아군 x = 화면 중앙 x (카메라 추종 기준)
            //    이 선보다 왼쪽(뒤)에 있는 Enemy = 침투 적 → 폴백 탐색 대상
            var allVals    = _gridMap.GetValueArray(Allocator.Temp);
            float maxAllyX = float.MinValue;
            for (int i = 0; i < allVals.Length; i++)
            {
                var u = allVals[i];
                if (u.Team == TeamType.Ally && u.Position.x > maxAllyX)
                    maxAllyX = u.Position.x;
            }
            if (maxAllyX > float.MinValue)
                _battleCenterX = maxAllyX;

            var infiltrators = new NativeList<UnitGridEntry>(allVals.Length, Allocator.TempJob);
            float cx = _battleCenterX;
            for (int i = 0; i < allVals.Length; i++)
            {
                var u = allVals[i];
                if (u.Team == TeamType.Enemy && u.Position.x < cx && math.all(math.isfinite(u.Position)))
                    infiltrators.Add(u);
            }
            allVals.Dispose();

            // ③ 타겟 탐색
            new FindNearestTargetJob
            {
                GridMap       = _gridMap,
                CellSize      = UnitGridConstants.CellSize,
                AllUnits      = infiltrators.AsArray(),
                BattleCenterX = _battleCenterX,
            }.ScheduleParallel();

            state.Dependency.Complete();
            infiltrators.Dispose();
        }
    }

    /// <summary>Grid 셀에 저장되는 유닛 정보 (최소한의 데이터만)</summary>
    public struct UnitGridEntry
    {
        public Entity   Entity;
        public float3   Position;
        public TeamType Team;
        public bool     HasTaunt; // TauntTag 보유 여부 — 타겟 우선순위에 사용
    }

    public static class UnitGridConstants
    {
        public const float CellSize = 3f;

        /// <summary>
        /// 타겟 탐색 반경 = CellSize × FindNearestTargetJob.SearchRadius(3).
        /// 곧 "유닛이 적을 볼 수 있는 거리" 다.
        ///
        /// ⚠ 돌진형 스킬의 이동 거리 상한으로도 쓴다
        ///   HasTarget 은 침투 적(중앙선 뒤)일 때 최대 50유닛까지 유지된다.
        ///   그 값을 그대로 도약 목적지로 쓰면 장수가 전장을 가로질러 날아가
        ///   적 후방 한가운데에 홀로 떨어진다. 시야 거리로 잘라야 한다.
        /// </summary>
        public const float SightRange = CellSize * 3f;
    }

    // ──────────────────────────────────────────
    // Grid 맵 빌드 Job
    // ──────────────────────────────────────────

    // ⚠ 소환 연출 중(SpawnProtection)인 유닛은 그리드에 넣지 않는다
    //   피해만 막으면 적이 조준하고 달려와 0.45초 동안 허공을 때린다. 그 사이
    //   진짜 위협을 놓치므로, 아예 '보이지 않는' 상태로 두는 편이 자연스럽다.
    //   땅에서 올라오는 중이니 연출과도 맞는다.
    [BurstCompile]
    [WithNone(typeof(DeadTag), typeof(SpawnProtection))]
    public partial struct BuildGridMapJob : IJobEntity
    {
        public NativeParallelMultiHashMap<int2, UnitGridEntry>.ParallelWriter GridWriter;
        [ReadOnly] public ComponentLookup<TauntTag> TauntLookup;

        public void Execute(
            Entity                   entity,
            in LocalTransform        transform,
            in UnitIdentityComponent identity,
            in HealthComponent       health,
            ref GridCellComponent    gridCell)
        {
            int2 cell = WorldToCell(transform.Position);

            gridCell.PrevCell = gridCell.Cell;
            gridCell.Cell     = cell;

            // 날아오는 발사체로 이미 사망이 확정된 유닛은 타겟 후보에서 뺀다.
            // (그리드는 타겟 탐색 전용이므로 여기서 빼면 아무도 새로 조준하지 않는다)
            if (health.IsDoomed) return;

            // ⚠ 소환사(= 마왕성)는 **아무도 조준하지 않는다** (2026-09-04 확정)
            //   용사는 몬스터가 하나라도 있으면 반드시 몬스터를 먼저 친다.
            //   그리드에서 빼면 그 규칙이 우선순위 계산 없이 저절로 성립한다 —
            //   보이지 않으니 후보에 들 수가 없다.
            //   몬스터를 다 뚫은 용사는 타겟이 없어 성벽으로 직진하고(UnitMovementSystem),
            //   성벽선에 닿는 순간 CoreBreachSystem 이 거둬 마왕성을 1 깎는다.
            //   ⚠ 조준당하지 않을 뿐, 소환사 자신의 타겟 탐색은 그대로다
            //     (이 잡은 '후보' 를 담는 곳이고, 찾는 쪽은 그리드를 읽기만 한다).
            if (identity.Type == UnitType.Summoner) return;

            GridWriter.Add(cell, new UnitGridEntry
            {
                Entity   = entity,
                Position = transform.Position,
                Team     = identity.Team,
                HasTaunt = TauntLookup.HasComponent(entity),
            });
        }

        static int2 WorldToCell(float3 pos) => (int2)math.floor(pos.xy / UnitGridConstants.CellSize);
    }

    // ──────────────────────────────────────────
    // 타겟 탐색 Job
    // ──────────────────────────────────────────

    /// <summary>
    /// 자신의 Grid 셀과 인접 셀을 탐색해 가까운 적팀 유닛 중 하나를 타겟으로 설정.
    /// 가장 가까운 3명을 후보로 수집한 뒤, 최근접 거리의 2배 이내인 후보 중 랜덤 선택.
    /// 탐색 범위 = CellSize × (SearchRadius × 2 + 1)
    /// </summary>
    [BurstCompile]
    [WithNone(typeof(DeadTag))]
    public partial struct FindNearestTargetJob : IJobEntity
    {
        [ReadOnly] public NativeParallelMultiHashMap<int2, UnitGridEntry> GridMap;
        [ReadOnly] public NativeArray<UnitGridEntry>                      AllUnits;
        public float CellSize;
        public float BattleCenterX;

        const int SearchRadius = 3;

        public void Execute(
            in  LocalTransform        transform,
            in  UnitIdentityComponent identity,
            in  GridCellComponent     gridCell,
            in  HealthComponent       health,
            ref AttackComponent       attack)
        {
            // ⚠ 사망 확정(IsDoomed)이어도 타겟을 놓지 않는다
            //   예전엔 여기서 HasTarget = false 로 놓았다. 그러면 UnitMovementSystem 의
            //   "적팀 + 타겟 없음 → 전진" 분기로 떨어져, **화살이 아직 날아오는 중인데
            //   멀쩡히 싸우던 놈이 갑자기 아군 쪽으로 달려나갔다.**
            //   IsDoomed 는 오버킬을 막으려고 **남이 나를 조준하지 않게** 하는 표식이다
            //   (그 역할은 위 그리드 등록 Job 이 이미 한다). 맞기 전까지는 평소대로 둔다.

            // 이미 유효한 타겟이 있으면 유지 — 단, 탐색 범위 초과 시 재탐색
            // (넉백·이동 스킬로 타겟이 멀리 날아간 경우 주변 적을 새로 탐색)
            // 타겟 사망 시 MeleeAttackJob / RangedAttackJob 이 HasTarget = false 로 초기화함
            if (attack.HasTarget)
            {
                // 침투 적(중앙선 뒤)은 원거리 추격 허용, 전방 적은 그리드 범위 내만 유지
                float maxChaseDistSq = attack.TargetPosition.x < BattleCenterX
                    ? 50f * 50f
                    : CellSize * SearchRadius * (CellSize * SearchRadius);
                if (math.distancesq(transform.Position, attack.TargetPosition) <= maxChaseDistSq) return;
                attack.HasTarget = false;
            }

            // 서치 반경 내 도발 유닛 추적 + 가장 가까운 3명 후보 수집
            Entity tauntC = Entity.Null;
            float  tauntD = float.MaxValue;
            float3 tauntP = float3.zero;

            Entity c0 = Entity.Null, c1 = Entity.Null, c2 = Entity.Null;
            float  d0 = float.MaxValue, d1 = float.MaxValue, d2 = float.MaxValue;
            float3 p0 = float3.zero,    p1 = float3.zero,    p2 = float3.zero;

            for (int dx = -SearchRadius; dx <= SearchRadius; dx++)
            for (int dy = -SearchRadius; dy <= SearchRadius; dy++)
            {
                int2 checkCell = gridCell.Cell + new int2(dx, dy);

                if (!GridMap.TryGetFirstValue(checkCell, out UnitGridEntry entry, out var it))
                    continue;

                do
                {
                    if (entry.Team == identity.Team) continue;

                    float distSq = math.distancesq(transform.Position, entry.Position);

                    if (entry.HasTaunt && distSq < tauntD)
                    { tauntC = entry.Entity; tauntD = distSq; tauntP = entry.Position; }

                    if (distSq < d0)
                    { c2=c1; d2=d1; p2=p1; c1=c0; d1=d0; p1=p0; c0=entry.Entity; d0=distSq; p0=entry.Position; }
                    else if (distSq < d1)
                    { c2=c1; d2=d1; p2=p1; c1=entry.Entity; d1=distSq; p1=entry.Position; }
                    else if (distSq < d2)
                    { c2=entry.Entity; d2=distSq; p2=entry.Position; }
                }
                while (GridMap.TryGetNextValue(out entry, ref it));
            }

            // 서치 반경 내 도발 유닛 발견 시 즉시 반환
            if (tauntC != Entity.Null)
            {
                attack.TargetEntity   = tauntC;
                attack.TargetPosition = tauntP;
                attack.HasTarget      = true;
                return;
            }

            // 그리드 범위 밖 적 폴백 — 전선을 넘어온 침투 적 대응
            if (c0 == Entity.Null)
            {
                for (int i = 0; i < AllUnits.Length; i++)
                {
                    var u = AllUnits[i];
                    if (u.Team == identity.Team) continue;
                    float distSq = math.distancesq(transform.Position, u.Position);
                    if (u.HasTaunt && distSq < tauntD)
                    { tauntC = u.Entity; tauntD = distSq; tauntP = u.Position; }
                    if (distSq < d0) { c0 = u.Entity; d0 = distSq; p0 = u.Position; }
                }
                if (tauntC != Entity.Null)
                {
                    attack.TargetEntity   = tauntC;
                    attack.TargetPosition = tauntP;
                    attack.HasTarget      = true;
                    return;
                }
            }

            if (c0 == Entity.Null) { attack.HasTarget = false; return; }

            // 최근접 거리 2배 이내 후보만 풀에 포함 (d² 비교: 2배 거리 = 4배 d²)
            float threshold = d0 * 4f;
            int   count     = 1;
            if (c1 != Entity.Null && d1 <= threshold) count++;
            if (c2 != Entity.Null && d2 <= threshold) count++;

            // 후보 중 랜덤 선택 (RandomSeed로 결정론적 랜덤)
            var rng  = new Unity.Mathematics.Random(
                attack.RandomSeed == 0u ? (uint)(c0.Index + 1) : attack.RandomSeed);
            int pick = count > 1 ? rng.NextInt(0, count) : 0;
            attack.RandomSeed = rng.state;

            Entity chosen;
            float3 chosenPos;
            if      (pick == 2) { chosen = c2; chosenPos = p2; }
            else if (pick == 1) { chosen = c1; chosenPos = p1; }
            else                { chosen = c0; chosenPos = p0; }

            attack.TargetEntity   = chosen;
            attack.TargetPosition = chosenPos;
            attack.HasTarget      = true;
        }
    }
}
