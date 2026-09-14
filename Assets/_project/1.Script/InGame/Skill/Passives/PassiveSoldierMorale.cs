using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PassiveSoldierMorale.cs  [OnAttack]
//  병사 고무 — 장군 공격 시 X% 확률로 소속 병사 전체 공격력 T초 버프.
//
//  Inspector:
//    TriggerType = OnAttack
//    TriggerChance: 발동 확률 (0.3 = 30%)
//    AttackBuffDelta: 병사 공격력 증가 절대값
//    BuffDuration: 버프 지속 시간(초)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_SoldierMorale", menuName = "BattleGame/Passives/SoldierMorale")]
public class PassiveSoldierMorale : PassiveSkillData
{
    [Header("병사 고무 설정")]
    [Range(0f, 1f)]
    public float TriggerChance   = 0.30f;
    public float AttackBuffDelta = 12f;
    public float BuffDuration    = 3f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        if (Random.value > TriggerChance) return;

        var em    = ctx.EntityManager;
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<SoldierComponent>(),
            ComponentType.ReadWrite<StatusEffectBufferElement>());

        using var soldiers = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var soldier in soldiers)
        {
            if (!em.Exists(soldier)) continue;
            if (em.GetComponentData<SoldierComponent>(soldier).GeneralEntity != ctx.GeneralEntity) continue;

            em.GetBuffer<StatusEffectBufferElement>(soldier).Add(new StatusEffectBufferElement
            {
                Stat      = StatType.Attack,
                Delta     = AttackBuffDelta,
                Mode      = EffectMode.Add,
                Duration  = BuffDuration,
                Remaining = BuffDuration,
            });
        }
        query.Dispose();
    }
}
