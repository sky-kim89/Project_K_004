using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PassiveIronWill.cs
//  IronWill 패시브 — 제너럴 HP 임계값 이하 시 공체 1회 강화.
//
//  Inspector 설정:
//    TriggerType  = OnHit
//    HpThreshold  = 0.5 (50% 이하일 때 발동)
//    StatModifiers: Runtime 스텟 변경 목록 (Target 은 무시, OnTrigger 에서 직접 처리)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_IronWill", menuName = "BattleGame/Passives/IronWill")]
public class PassiveIronWill : PassiveSkillData
{
    [Header("IronWill 설정")]
    [Range(0f, 1f)]
    [Tooltip("발동 HP 비율 임계값 (0.5 = HP 50% 이하일 때 발동)")]
    public float HpThreshold = 0.5f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;

        // 이미 발동했으면 무시
        if (!em.HasComponent<PassiveConditionState>(ctx.GeneralEntity)) return;
        var condition = em.GetComponentData<PassiveConditionState>(ctx.GeneralEntity);
        if (condition.IronWillTriggered) return;

        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity)) return;
        var stat  = em.GetComponentData<StatComponent>(ctx.GeneralEntity);
        float maxHp = stat.Final[StatType.MaxHp];
        if (maxHp <= 0f) return;

        float hpRatio = ctx.Health.CurrentHp / maxHp;
        if (hpRatio > HpThreshold) return;

        // 스텟 보너스 적용
        foreach (var mod in StatModifiers)
        {
            float delta = mod.IsPercent
                ? stat.Base[mod.Stat] * mod.Delta
                : mod.Delta;

            stat.Base[mod.Stat]  += delta;
            stat.Final[mod.Stat] += delta;
        }

        em.SetComponentData(ctx.GeneralEntity, stat);

        condition.IronWillTriggered = true;
        em.SetComponentData(ctx.GeneralEntity, condition);
    }
}
