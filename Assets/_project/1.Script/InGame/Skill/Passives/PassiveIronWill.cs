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

        // ⚠ 늘어난 최대 체력만큼 **현재 체력도 채운다** (사용자 지적, 2026-09-15)
        //   Base·Final 을 함께 올리므로 UnitStatusEffectSystem 의 "최대 체력이 늘면 현재 체력도" 동기화가
        //   이전·이후 값이 같아 걸리지 않았다 — 체력 50% 이하에서 발동해 **비율만 더 낮아지는** 패시브였다.
        float maxHpGain = stat.Final[StatType.MaxHp] - maxHp;
        if (maxHpGain > 0f && em.HasComponent<HealthComponent>(ctx.GeneralEntity))
        {
            var health = em.GetComponentData<HealthComponent>(ctx.GeneralEntity);
            health.CurrentHp = Mathf.Min(health.CurrentHp + maxHpGain, stat.Final[StatType.MaxHp]);
            em.SetComponentData(ctx.GeneralEntity, health);
        }

        condition.IronWillTriggered = true;
        em.SetComponentData(ctx.GeneralEntity, condition);
    }
}
