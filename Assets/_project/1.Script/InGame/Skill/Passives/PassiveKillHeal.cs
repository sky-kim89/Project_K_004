using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PassiveKillHeal.cs  [OnEnemyKill]
//  처치 회복 — 적 처치 시 최대체력의 X% 즉시 회복.
//
//  Inspector:
//    TriggerType = OnEnemyKill
//    HealRatio: 최대체력 대비 회복 비율 (0.05 = 5%)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_KillHeal", menuName = "BattleGame/Passives/KillHeal")]
public class PassiveKillHeal : PassiveSkillData
{
    [Header("처치 회복 설정")]
    [Range(0f, 0.3f)]
    public float HealRatio = 0.05f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity))        return;
        if (!em.HasBuffer<HealEventBufferElement>(ctx.GeneralEntity))  return;

        float maxHp = em.GetComponentData<StatComponent>(ctx.GeneralEntity).Final[StatType.MaxHp];
        // 유물 '치유의 기억' — 몬스터만 받는다 (용사도 이 패시브를 쓴다)
        float heal  = maxHp * HealRatio * SpeciesPassiveRuntime.HealPowerFor(em, ctx.GeneralEntity);
        em.GetBuffer<HealEventBufferElement>(ctx.GeneralEntity).Add(
            new HealEventBufferElement { Amount = heal, SourceEntity = ctx.GeneralEntity });
    }
}
