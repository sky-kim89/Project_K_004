using Unity.Burst;
using Unity.Entities;

// ============================================================
//  GearPassiveConditionSystem.cs
//  조건이 맞을 때만 켜지는 장비 패시브 — 지금은 '허세' 하나다.
//
//  ■ 왜 상태효과로 안 하나
//    상태효과는 "붙이면 켜지고 시간이 되면 꺼진다" 다. 허세는 체력이 오르내리는
//    대로 **매 프레임** 켜졌다 꺼져야 한다. 버프를 붙였다 떼는 것보다
//    Final 을 곱하는 편이 싸고, 떼는 것을 잊을 길도 없다.
//
//  ■ ⚠ 순서 — UnitStatusEffectSystem **뒤**, UnitHitSystem **앞**
//    Final 은 상태효과 시스템이 매 프레임 Base 로 되돌린 뒤 다시 짠다.
//    그 앞에서 곱하면 바로 지워진다. 공격 시스템은 이 값을 이번 프레임 또는
//    지난 프레임 것으로 읽는다 — 어느 쪽이든 허세가 반영된 값이다.
// ============================================================

namespace BattleGame.Units
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitStatusEffectSystem))]
    [UpdateBefore(typeof(UnitHitSystem))]
    public partial struct GearPassiveConditionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new BravadoJob().ScheduleParallel();
        }
    }

    [BurstCompile]
    [WithNone(typeof(DeadTag))]
    public partial struct BravadoJob : IJobEntity
    {
        public void Execute(ref StatComponent stat, in HealthComponent health, in BravadoComponent bravado)
        {
            if (health.CurrentHp >= stat.Final[StatType.MaxHp] * bravado.Threshold)
                stat.Final[StatType.Attack] *= bravado.AttackMult;
        }
    }
}
