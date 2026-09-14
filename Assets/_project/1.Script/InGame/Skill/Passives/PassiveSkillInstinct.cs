using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PassiveSkillInstinct.cs  [OnSkillUse]
//  생존 본능 — 스킬 사용 시 X% 확률로 최대체력의 Y% 즉시 회복.
//
//  Inspector:
//    TriggerType = OnSkillUse
//    TriggerChance: 발동 확률 (0.5 = 50%)
//    HealRatio: 최대체력 대비 회복 비율 (0.08 = 8%)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_SkillInstinct", menuName = "BattleGame/Passives/SkillInstinct")]
public class PassiveSkillInstinct : PassiveSkillData
{
    [Header("생존 본능 설정")]
    [Range(0f, 1f)]
    public float TriggerChance = 0.50f;
    [Range(0f, 0.3f)]
    public float HealRatio     = 0.08f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        if (Random.value > TriggerChance) return;
        var em = ctx.EntityManager;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity))        return;
        if (!em.HasBuffer<HealEventBufferElement>(ctx.GeneralEntity))  return;

        float maxHp = em.GetComponentData<StatComponent>(ctx.GeneralEntity).Final[StatType.MaxHp];
        float heal  = maxHp * HealRatio;
        em.GetBuffer<HealEventBufferElement>(ctx.GeneralEntity).Add(
            new HealEventBufferElement { Amount = heal, SourceEntity = ctx.GeneralEntity });
    }
}
