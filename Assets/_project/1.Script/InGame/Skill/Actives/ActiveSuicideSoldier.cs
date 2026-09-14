using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveSuicideSoldier.cs — 자폭 병사 (법사)
//
//  제너럴이 병사를 포물선 궤도로 타겟 방향에 던진다.
//  착탄 시 EffectRadius 범위 내 모든 적에게 EffectValue 배율 피해 + 넉백.
//  병사는 착탄 후 즉사.
//  SuicideSoldierRunner 가 투사 + 폭발 시퀀스를 처리한다.
// ============================================================

[CreateAssetMenu(fileName = "Active_SuicideSoldier", menuName = "BattleGame/Actives/SuicideSoldier")]
public class ActiveSuicideSoldier : ActiveSkillData
{
    [Header("자폭 병사 설정")]
    [Tooltip("투사 소요 시간 (초)")]
    public float FlightDuration = 0.5f;

    [Tooltip("포물선 최대 높이 (유닛)")]
    public float ArcHeight = 2f;

    [Tooltip("폭발 넉백 배율")]
    public float KnockbackMult = 7f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (!ctx.HasTarget) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        if (!em.Exists(ctx.TargetEntity)) return;

        // ── 소속 병사 중 체력이 가장 낮은 병사 선택 ─────────────
        Entity pickedSoldier = Entity.Null;
        float  lowestHp      = float.MaxValue;
        GameObject soldierGO = null;

        var query    = em.CreateEntityQuery(new EntityQueryDesc
        {
            All  = new ComponentType[] { ComponentType.ReadOnly<SoldierComponent>() },
            None = new ComponentType[] { typeof(DeadTag) },
        });
        NativeArray<Entity>           entities = query.ToEntityArray(Allocator.Temp);
        NativeArray<SoldierComponent> soldiers = query.ToComponentDataArray<SoldierComponent>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if (soldiers[i].GeneralEntity != ctx.CasterEntity) continue;
            if (!em.HasComponent<HealthComponent>(entities[i])) continue;

            float hp = em.GetComponentData<HealthComponent>(entities[i]).CurrentHp;
            if (hp < lowestHp) { lowestHp = hp; pickedSoldier = entities[i]; }
        }

        entities.Dispose();
        soldiers.Dispose();
        query.Dispose();

        if (pickedSoldier == Entity.Null) return;

        if (em.HasComponent<UnitPoolLinkComponent>(pickedSoldier))
            soldierGO = em.GetComponentObject<UnitPoolLinkComponent>(pickedSoldier).LinkedObject;

        if (soldierGO == null) return;

        var targetTf       = em.GetComponentData<LocalTransform>(ctx.TargetEntity);
        Vector3 targetPos  = new Vector3(targetTf.Position.x, targetTf.Position.y, targetTf.Position.z);
        var soldierStat    = em.GetComponentData<StatComponent>(pickedSoldier);
        var casterIdentity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);

        var runner = soldierGO.GetComponent<SuicideSoldierRunner>();
        if (runner == null) runner = soldierGO.AddComponent<SuicideSoldierRunner>();

        runner.Run(
            soldierTransform : soldierGO.transform,
            soldierEntity    : pickedSoldier,
            targetPos        : targetPos,
            soldierStat      : soldierStat,
            em               : em,
            casterTeam       : casterIdentity.Team,
            damageMultiplier : EffectValue,
            aoeRadius        : EffectRadius > 0f ? EffectRadius : 2.5f,
            flightDuration   : FlightDuration,
            arcHeight        : ArcHeight,
            knockbackMult    : KnockbackMult,
            fx               : new SkillEffectConfig
            {
                CasterEffectKey = CasterEffectKey,
                TargetEffectKey = TargetEffectKey,
                BaseEffectKey   = BaseEffectKey,
                DespawnDelay    = EffectDespawnDelay,
            }
        );
    }
}
