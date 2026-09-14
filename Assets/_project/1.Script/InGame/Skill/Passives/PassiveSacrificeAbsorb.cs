using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PassiveSacrificeAbsorb.cs  [OnSoldierDeath]
//  희생 흡수 — 병사 사망 시 사망 수 × N 만큼 즉시 체력 회복.
//
//  Inspector:
//    TriggerType = OnSoldierDeath
//    HealPerDeath: 병사 1명 사망당 회복량
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_SacrificeAbsorb", menuName = "BattleGame/Passives/SacrificeAbsorb")]
public class PassiveSacrificeAbsorb : PassiveSkillData
{
    [Header("희생 흡수 설정")]
    public float HealPerDeath = 30f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        if (ctx.SoldierDeathCount <= 0) return;
        var em = ctx.EntityManager;
        if (!em.HasBuffer<HealEventBufferElement>(ctx.GeneralEntity)) return;

        float heal = HealPerDeath * ctx.SoldierDeathCount;
        em.GetBuffer<HealEventBufferElement>(ctx.GeneralEntity).Add(
            new HealEventBufferElement { Amount = heal, SourceEntity = ctx.GeneralEntity });
    }
}
