using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  PassiveFocusedFire.cs  [OnBattleStart]
//  집중 사격 — 공격속도 0.1당 치명타 확률 +CritPerAtkSpd.
//
//  Inspector:
//    TriggerType   = OnBattleStart
//    CritPerAtkSpd : 공격속도 1.0당 치명타 확률 증가량 (0.05 = 5%, 즉 0.1공속당 +0.5%)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_FocusedFire", menuName = "BattleGame/Passives/FocusedFire")]
public class PassiveFocusedFire : PassiveSkillData
{
    [UnityEngine.Header("집중 사격 설정")]
    [UnityEngine.Tooltip("공격속도 1.0당 치명타 확률 증가 (0.05 = 공속 1당 +5%, 공속 0.1당 +0.5%)")]
    [UnityEngine.Range(0f, 0.5f)]
    public float CritPerAtkSpd = 0.05f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity)) return;

        var stat = em.GetComponentData<StatComponent>(ctx.GeneralEntity);

        float atkSpd    = stat.Final[StatType.AttackSpeed];
        float critBonus = atkSpd * CritPerAtkSpd;
        if (critBonus <= 0f) return;

        stat.Base[StatType.CritChance]  += critBonus;
        stat.Final[StatType.CritChance] += critBonus;
        em.SetComponentData(ctx.GeneralEntity, stat);
    }

    public override void CollectPreviewStats(System.Func<StatType, float> current,
                                             System.Func<StatType, float> baseRoll,
                                             System.Collections.Generic.Dictionary<StatType, float> outDeltas)
    {
        float critBonus = current(StatType.AttackSpeed) * CritPerAtkSpd;
        if (critBonus <= 0f) return;
        outDeltas[StatType.CritChance] = outDeltas.TryGetValue(StatType.CritChance, out var v) ? v + critBonus : critBonus;
    }

}
