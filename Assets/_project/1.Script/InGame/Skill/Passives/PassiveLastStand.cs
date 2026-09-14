using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PassiveLastStand.cs
//  LastStand 패시브 — 병사 수가 초기 대비 임계값 이하 시 남은 병사 강화 (1회).
//
//  Inspector 설정:
//    TriggerType       = OnSoldierDeath
//    SoldierThreshold  = 0.5 (초기 병사의 50% 이하)
//    StatModifiers: 발동 시 병사에게 적용할 스텟 변경 목록
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_LastStand", menuName = "BattleGame/Passives/LastStand")]
public class PassiveLastStand : PassiveSkillData
{
    [Header("LastStand 설정")]
    [Range(0f, 1f)]
    [Tooltip("발동 병사 비율 임계값 (0.5 = 초기 병사의 50% 이하일 때 발동)")]
    public float SoldierThreshold = 0.5f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;

        // 이미 발동했으면 무시
        if (!em.HasComponent<PassiveConditionState>(ctx.GeneralEntity)) return;
        var condition = em.GetComponentData<PassiveConditionState>(ctx.GeneralEntity);
        if (condition.LastStandTriggered) return;

        int initialCount = condition.InitialSoldierCount;
        if (initialCount <= 0) return;

        // 현재 생존 병사 수 집계
        int aliveCount = CountAliveSoldiers(ctx.GeneralEntity, em);

        float soldierRatio = (float)aliveCount / initialCount;
        if (soldierRatio > SoldierThreshold) return;

        // 생존 병사 전체에 스텟 보너스 적용
        ApplyToAliveSoldiers(ctx.GeneralEntity, em);

        condition.LastStandTriggered = true;
        em.SetComponentData(ctx.GeneralEntity, condition);
    }

    // ── 내부 ─────────────────────────────────────────────────

    int CountAliveSoldiers(Entity generalEntity, EntityManager em)
    {
        using var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<SoldierComponent>(),
            ComponentType.Exclude<DeadTag>());

        int count    = 0;
        var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var e in entities)
        {
            if (em.GetComponentData<SoldierComponent>(e).GeneralEntity == generalEntity)
                count++;
        }
        entities.Dispose();
        return count;
    }

    void ApplyToAliveSoldiers(Entity generalEntity, EntityManager em)
    {
        using var query = em.CreateEntityQuery(
            ComponentType.ReadWrite<StatComponent>(),
            ComponentType.ReadOnly<SoldierComponent>(),
            ComponentType.Exclude<DeadTag>());

        var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var soldierEntity in entities)
        {
            if (em.GetComponentData<SoldierComponent>(soldierEntity).GeneralEntity != generalEntity)
                continue;

            var stat = em.GetComponentData<StatComponent>(soldierEntity);

            foreach (var mod in StatModifiers)
            {
                float delta = mod.IsPercent
                    ? stat.Base[mod.Stat] * mod.Delta
                    : mod.Delta;

                stat.Base[mod.Stat]  += delta;
                stat.Final[mod.Stat] += delta;
            }

            em.SetComponentData(soldierEntity, stat);
        }
        entities.Dispose();
    }
}
