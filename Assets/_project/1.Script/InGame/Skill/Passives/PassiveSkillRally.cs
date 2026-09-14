using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  PassiveSkillRally.cs  [OnSkillUse]
//  전투 집결 — 스킬 사용 시 병사 전체 공격력·이동속도 T초 버프.
//
//  Inspector:
//    TriggerType = OnSkillUse
//    AttackBuffDelta: 병사 공격력 증가 절대값
//    SpeedBuffDelta: 병사 이동속도 증가 절대값
//    BuffDuration: 지속 시간(초)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_SkillRally", menuName = "BattleGame/Passives/SkillRally")]
public class PassiveSkillRally : PassiveSkillData
{
    [UnityEngine.Header("전투 집결 설정")]
    public float AttackBuffDelta = 20f;
    public float SpeedBuffDelta  = 0.5f;
    public float BuffDuration    = 6f;

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

            var buf = em.GetBuffer<StatusEffectBufferElement>(soldier);
            buf.Add(new StatusEffectBufferElement
            {
                Stat = StatType.Attack, Delta = AttackBuffDelta,
                Mode = EffectMode.Add, Duration = BuffDuration, Remaining = BuffDuration,
            });
            buf.Add(new StatusEffectBufferElement
            {
                Stat = StatType.MoveSpeed, Delta = SpeedBuffDelta,
                Mode = EffectMode.Add, Duration = BuffDuration, Remaining = BuffDuration,
            });
        }
        query.Dispose();
    }
}
