using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Burst;

// ============================================================
//  DeadSoldierTrackingSystem.cs
//  사망한 병사의 마지막 위치를 소속 장군의 DeadSoldierSpawnPointBuffer 에 기록.
//
//  ■ 동작 흐름
//    1. DeadTag + SoldierComponent 를 가지며
//       SoldierDeathPositionRecorded 가 없는 엔티티를 탐색
//    2. 소속 장군 Entity 의 DeadSoldierSpawnPointBuffer 에 위치 추가
//    3. 버퍼가 최대 용량(MaxPositions)을 초과하면 가장 오래된 항목 제거
//    4. 해당 병사 Entity 에 SoldierDeathPositionRecorded 태그 추가 → 중복 기록 방지
//
//  ■ ActiveSummonSkeleton 과의 연동
//    Execute() 에서 이 버퍼를 읽어 스켈레톤 + 이펙트를 병사 사망 위치에 소환.
//    소환 후 버퍼를 Clear 하여 다음 스킬 사용 시 재활용.
// ============================================================

namespace BattleGame.Units
{
    /// <summary>
    /// 이 태그가 붙으면 DeadSoldierTrackingSystem 이 이미 위치를 기록한 것으로 간주.
    /// </summary>
    public struct SoldierDeathPositionRecorded : IComponentData { }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitHitSystem))]
    public partial struct DeadSoldierTrackingSystem : ISystem
    {
        const int MaxPositions = 8;

        // LookupHandle 은 OnCreate 에서 캐싱, OnUpdate 에서 Update
        BufferLookup<DeadSoldierSpawnPointBuffer> _spawnPointLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            _spawnPointLookup = state.GetBufferLookup<DeadSoldierSpawnPointBuffer>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawnPointLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // DeadTag + SoldierComponent 를 가지며 아직 기록되지 않은 병사 탐색
            foreach (var (soldier, tf, entity) in
                SystemAPI.Query<RefRO<SoldierComponent>, RefRO<LocalTransform>>()
                         .WithAll<DeadTag>()
                         .WithNone<SoldierDeathPositionRecorded>()
                         .WithEntityAccess())
            {
                Entity general = soldier.ValueRO.GeneralEntity;

                if (_spawnPointLookup.HasBuffer(general))
                {
                    var buf = _spawnPointLookup[general];

                    // 버퍼가 가득 차면 가장 오래된 항목(인덱스 0) 제거
                    if (buf.Length >= MaxPositions)
                        buf.RemoveAt(0);

                    buf.Add(new DeadSoldierSpawnPointBuffer
                    {
                        Position = new float3(
                            tf.ValueRO.Position.x,
                            tf.ValueRO.Position.y,
                            tf.ValueRO.Position.z)
                    });
                }

                // 중복 기록 방지 태그 추가
                ecb.AddComponent<SoldierDeathPositionRecorded>(entity);
            }
        }
    }
}
