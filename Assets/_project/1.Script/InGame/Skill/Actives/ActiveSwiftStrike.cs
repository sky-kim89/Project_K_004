using Unity.Entities;
using Unity.Collections;
using BattleGame.Units;

// ============================================================
//  ActiveSwiftStrike.cs — 신속 연격 (궁수)
//
//  시전자와 소속 병사 전체의 공격 속도를 EffectDuration 초 동안
//  EffectValue 배율로 증가시킨다.
//  (예: EffectValue = 2.0 → 공격속도 2배)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Active_SwiftStrike", menuName = "BattleGame/Actives/SwiftStrike")]
public class ActiveSwiftStrike : ActiveSkillData
{
    public override void Execute(ActiveSkillContext ctx)
    {
        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        float duration = EffectDuration > 0f ? EffectDuration : 6f;

        // 사용자 이펙트 (속도 버프 연출)
        if (ctx.CasterTransform != null)
            SkillEffectHelper.Spawn(CasterEffectKey, ctx.CasterTransform.position, EffectDespawnDelay);

        ApplyBuff(em, ctx.CasterEntity, duration);

        var query    = em.CreateEntityQuery(new EntityQueryDesc
        {
            All  = new ComponentType[] { ComponentType.ReadOnly<SoldierComponent>() },
            None = new ComponentType[] { typeof(DeadTag) },
        });
        NativeArray<Entity>           entities = query.ToEntityArray(Allocator.Temp);
        NativeArray<SoldierComponent> soldiers = query.ToComponentDataArray<SoldierComponent>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
            if (soldiers[i].GeneralEntity == ctx.CasterEntity)
                ApplyBuff(em, entities[i], duration);

        entities.Dispose();
        soldiers.Dispose();
        query.Dispose();
    }

    void ApplyBuff(EntityManager em, Entity entity, float duration)
    {
        if (!em.HasBuffer<StatusEffectBufferElement>(entity)) return;

        em.GetBuffer<StatusEffectBufferElement>(entity).Add(new StatusEffectBufferElement
        {
            Stat       = StatType.AttackSpeed,
            Delta      = EffectValue,
            Mode       = EffectMode.Multiply,
            Duration   = duration,
            Remaining  = duration,
            SourceType = BuffSourceType.ActiveSkill,
            SourceId   = (int)SkillId,
        });
    }
}
