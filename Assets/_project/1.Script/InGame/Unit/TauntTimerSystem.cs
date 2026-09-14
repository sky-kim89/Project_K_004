using Unity.Burst;
using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  TauntTimerSystem.cs
//  TauntTag.Remaining 을 매 프레임 차감하고 만료 시 컴포넌트를 제거한다.
// ============================================================

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct TauntTimerSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt  = SystemAPI.Time.DeltaTime;
        var   ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                             .CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (taunt, entity) in
            SystemAPI.Query<RefRW<TauntTag>>().WithEntityAccess())
        {
            taunt.ValueRW.Remaining -= dt;
            if (taunt.ValueRO.Remaining <= 0f)
                ecb.RemoveComponent<TauntTag>(entity);
        }
    }
}
