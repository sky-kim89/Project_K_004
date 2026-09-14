using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  PassiveSoldierVigor.cs  [OnEnemyKill]
//  병사 결의 — 처치 시 소속 병사 전체 공격력 T초 버프.
//
//  Inspector:
//    TriggerType = OnEnemyKill
//    AttackBuffDelta: 병사 공격력 증가 절대값
//    BuffDuration: 지속 시간(초)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_SoldierVigor", menuName = "BattleGame/Passives/SoldierVigor")]
public class PassiveSoldierVigor : PassiveSkillData
{
    [UnityEngine.Header("병사 결의 설정")]
    public float AttackBuffDelta = 15f;
    public float BuffDuration    = 4f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
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
