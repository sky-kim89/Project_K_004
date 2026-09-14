using Unity.Entities;
using Unity.Collections;
using BattleGame.Units;

// ============================================================
//  ActiveTargetHeal.cs — 집중 치유 (공통)
//
//  아군 장군 중 현재 체력 비율이 가장 낮은 장군 1명을 집중 치유한다.
//  회복량 = 대상 MaxHp × EffectValue (비율).
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Active_TargetHeal", menuName = "BattleGame/Actives/TargetHeal")]
public class ActiveTargetHeal : ActiveSkillData
{
    public override void Execute(ActiveSkillContext ctx)
    {
        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        // ── 아군 장군 중 체력 비율 가장 낮은 1명 탐색 ──────────
        Entity lowestEntity  = Entity.Null;
        float  lowestHpRatio = float.MaxValue;

        var query = em.CreateEntityQuery(new EntityQueryDesc
        {
            All  = new ComponentType[] { ComponentType.ReadOnly<GeneralComponent>(),
                                         ComponentType.ReadOnly<UnitIdentityComponent>() },
            None = new ComponentType[] { typeof(DeadTag) },
        });
        NativeArray<Entity>              entities = query.ToEntityArray(Allocator.Temp);
        NativeArray<UnitIdentityComponent> ids    = query.ToComponentDataArray<UnitIdentityComponent>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if (ids[i].Team != TeamType.Ally) continue;

            float ratio = GetHpRatio(em, entities[i]);
            if (ratio < lowestHpRatio)
            {
                lowestHpRatio = ratio;
                lowestEntity  = entities[i];
            }
        }

        entities.Dispose();
        ids.Dispose();
        query.Dispose();

        if (lowestEntity == Entity.Null) return;

        // ── 집중 치유 ──────────────────────────────────────────
        if (!em.HasComponent<StatComponent>(lowestEntity))        return;
        if (!em.HasBuffer<HealEventBufferElement>(lowestEntity))  return;

        float maxHp  = em.GetComponentData<StatComponent>(lowestEntity).Final[StatType.MaxHp];
        float amount = maxHp * EffectValue;
        em.GetBuffer<HealEventBufferElement>(lowestEntity).Add(
            new HealEventBufferElement { Amount = amount, SourceEntity = ctx.CasterEntity });

        if (em.HasComponent<Unity.Transforms.LocalTransform>(lowestEntity))
        {
            var tf = em.GetComponentData<Unity.Transforms.LocalTransform>(lowestEntity);
            SkillEffectHelper.Spawn(TargetEffectKey,
                new UnityEngine.Vector3(tf.Position.x, tf.Position.y, tf.Position.z),
                EffectDespawnDelay);
        }
    }

    static float GetHpRatio(EntityManager em, Entity entity)
    {
        if (!em.HasComponent<HealthComponent>(entity)) return 1f;
        if (!em.HasComponent<StatComponent>(entity))   return 1f;

        float maxHp = em.GetComponentData<StatComponent>(entity).Final[StatType.MaxHp];
        if (maxHp <= 0f) return 1f;

        return em.GetComponentData<HealthComponent>(entity).CurrentHp / maxHp;
    }
}
