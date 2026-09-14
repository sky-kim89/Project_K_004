using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using BattleGame.Units;

// ============================================================
//  ActiveVolleyFire.cs — 일제 사격 (궁수·법사)
//
//  제너럴과 소속 병사 전체가 현재 타겟에 즉시 공격을 발동한다.
//  원거리 유닛(Archer/Mage) → ProjectileLaunchRequest 버퍼에 추가 (화살/마법탄 발사)
//  근접 유닛 → HitEventBuffer 에 즉시 추가
// ============================================================

[UnityEngine.CreateAssetMenu(
    fileName = "Active_VolleyFire",
    menuName = "BattleGame/Actives/VolleyFire")]
public class ActiveVolleyFire : ActiveSkillData
{
    const float ArrowSpeed     = 15f;
    const float MagicBoltSpeed = 10f;

    public override void Execute(ActiveSkillContext ctx)
    {
        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        if (ctx.CasterTransform != null)
            SkillEffectHelper.Spawn(CasterEffectKey, ctx.CasterTransform.position, EffectDespawnDelay);

        ForceAttack(em, ctx.CasterEntity, EffectValue);

        var query = em.CreateEntityQuery(new EntityQueryDesc
        {
            All  = new ComponentType[] { ComponentType.ReadOnly<SoldierComponent>() },
            None = new ComponentType[] { typeof(DeadTag) },
        });
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        var soldiers = query.ToComponentDataArray<SoldierComponent>(Unity.Collections.Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
            if (soldiers[i].GeneralEntity == ctx.CasterEntity)
                ForceAttack(em, entities[i], EffectValue);

        entities.Dispose();
        soldiers.Dispose();
        query.Dispose();
    }

    void ForceAttack(EntityManager em, Entity attacker, float damageMult)
    {
        if (!em.HasComponent<AttackComponent>(attacker)) return;
        var atk = em.GetComponentData<AttackComponent>(attacker);
        if (!atk.HasTarget || !em.Exists(atk.TargetEntity)) return;

        var    stat     = em.GetComponentData<StatComponent>(attacker);
        var    selfTf   = em.GetComponentData<LocalTransform>(attacker);
        var    targetTf = em.GetComponentData<LocalTransform>(atk.TargetEntity);
        float  damage   = stat.Final[StatType.Attack] * damageMult;

        if (em.HasComponent<RangedTag>(attacker))
        {
            // 원거리 유닛 → 발사체 생성 (ProjectileSpawnSystem 이 다음 프레임 처리)
            var   identity = em.GetComponentData<UnitIdentityComponent>(attacker);
            var   jobComp  = em.GetComponentData<UnitJobComponent>(attacker);
            float speed    = jobComp.Job == UnitJob.Archer ? ArrowSpeed : MagicBoltSpeed;

            em.GetBuffer<ProjectileLaunchRequest>(attacker).Add(new ProjectileLaunchRequest
            {
                TargetEntity   = atk.TargetEntity,
                AttackerEntity = attacker,
                AttackerPos    = selfTf.Position,
                TargetPos      = targetTf.Position,
                Damage         = damage,
                Speed          = speed,
                Team           = identity.Team,
                DefensePierce  = stat.Final[StatType.DefensePenetration],
            });
        }
        else
        {
            // 근접 유닛 → 즉시 피해 적용
            if (!em.HasBuffer<HitEventBufferElement>(atk.TargetEntity)) return;

            em.GetBuffer<HitEventBufferElement>(atk.TargetEntity).Add(new HitEventBufferElement
            {
                Damage         = damage,
                HitDirection   = float3.zero,
                AttackerEntity = attacker,
                Type           = BattleGame.Units.HitType.Skill,
                DefensePierce  = stat.Final[StatType.DefensePenetration],
            });
        }

        atk.AttackCooldown = 0f;
        em.SetComponentData(attacker, atk);
    }
}
