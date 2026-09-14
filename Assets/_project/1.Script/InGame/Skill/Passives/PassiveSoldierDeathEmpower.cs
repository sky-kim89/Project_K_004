using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PassiveSoldierDeathEmpower.cs
//  SoldierDeathEmpower 패시브 — 병사 사망 시마다 제너럴 스텟 증가.
//
//  Inspector 설정:
//    TriggerType = OnSoldierDeath
//    StatModifiers: 사망 1명당 증가 수치 목록
//      예) Stat=Attack, Delta=0.02, IsPercent=true → 사망 1명당 공격력 2% 증가
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_SoldierDeathEmpower", menuName = "BattleGame/Passives/SoldierDeathEmpower")]
public class PassiveSoldierDeathEmpower : PassiveSkillData
{
    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        int deathCount = ctx.SoldierDeathCount;
        if (deathCount <= 0) return;

        var em = ctx.EntityManager;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity)) return;

        var stat = em.GetComponentData<StatComponent>(ctx.GeneralEntity);

        foreach (var mod in StatModifiers)
        {
            float perDeathDelta = mod.IsPercent
                ? stat.Base[mod.Stat] * mod.Delta
                : mod.Delta;

            float totalDelta = perDeathDelta * deathCount;
            stat.Base[mod.Stat]  += totalDelta;
            stat.Final[mod.Stat] += totalDelta;
        }

        em.SetComponentData(ctx.GeneralEntity, stat);
    }
}
