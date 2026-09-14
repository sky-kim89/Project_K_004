using Unity.Entities;

// ============================================================
//  CombatTriggerSystem.cs
//  장군(용사)의 전투 이벤트 버퍼를 매 프레임 비운다.
//
//  ■ 이 시스템이 이벤트 버퍼의 마지막 소비자다
//    PassiveSkillRuntimeSystem(패시브 트리거) · MonsterSynergyKillSystem(투지)이
//    먼저 읽는다. 앞쪽에서 지우면 뒤쪽이 영원히 못 본다.
//    ⚠ 이 시스템을 지우지 말 것 — 비우지 않으면 버퍼가 끝없이 불어난다.
//
//  ⚠ 원작의 장비·어빌리티·특성 트리거 디스패치는 걷어냈다
//    용사에게 그런 것을 붙이는 코드가 없다 (플레이어 장수용 장치였다).
//
//  실행 순서: PassiveSkillRuntimeSystem → CombatTriggerSystem → UnitHitSystem
// ============================================================

namespace BattleGame.Units
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PassiveSkillRuntimeSystem))]
    [UpdateBefore(typeof(UnitHitSystem))]
    public partial class CombatTriggerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (kills, skills, hits, deaths) in
                     SystemAPI.Query<DynamicBuffer<EnemyKillEvent>, DynamicBuffer<SkillUseEvent>,
                                     DynamicBuffer<AttackHitEvent>, DynamicBuffer<SoldierDeathEvent>>()
                              .WithAll<GeneralComponent>())
            {
                kills.Clear();
                skills.Clear();
                hits.Clear();
                deaths.Clear();
            }
        }
    }
}
