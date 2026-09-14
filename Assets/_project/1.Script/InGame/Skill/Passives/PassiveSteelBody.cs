using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  PassiveSteelBody.cs  [OnBattleStart]
//  강철 체력 — 최대체력의 X%를 공격력으로 전환.
//
//  Inspector:
//    TriggerType      = OnBattleStart
//    HpToAttackRatio  : 최대체력 대비 공격력 전환 비율 (0.03 = 3%)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_SteelBody", menuName = "BattleGame/Passives/SteelBody")]
public class PassiveSteelBody : PassiveSkillData
{
    [UnityEngine.Header("강철 체력 설정")]
    [UnityEngine.Tooltip("최대체력 대비 공격력 전환 비율 (0.03 = 체력의 3%를 공격력으로)")]
    [UnityEngine.Range(0f, 0.2f)]
    public float HpToAttackRatio = 0.03f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity)) return;

        var stat = em.GetComponentData<StatComponent>(ctx.GeneralEntity);

        float atkBonus = stat.Base[StatType.MaxHp] * HpToAttackRatio;
        if (atkBonus <= 0f) return;

        stat.Base[StatType.Attack]  += atkBonus;
        stat.Final[StatType.Attack] += atkBonus;
        em.SetComponentData(ctx.GeneralEntity, stat);
    }

    public override void CollectPreviewStats(System.Func<StatType, float> current,
                                             System.Func<StatType, float> baseRoll,
                                             System.Collections.Generic.Dictionary<StatType, float> outDeltas)
    {
        float atkBonus = current(StatType.MaxHp) * HpToAttackRatio;
        if (atkBonus <= 0f) return;
        outDeltas[StatType.Attack] = outDeltas.TryGetValue(StatType.Attack, out var v) ? v + atkBonus : atkBonus;
    }

}
