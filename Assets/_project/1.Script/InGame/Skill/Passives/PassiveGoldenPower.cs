using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PassiveGoldenPower.cs  [OnBattleStart]
//  황금의 힘 — 전투 시작 시 보유 골드 N당 공격력·최대체력 +X%.
//
//  Inspector:
//    TriggerType   = OnBattleStart
//    GoldPerBonus  : 보너스 1회당 필요 골드 (기본 300)
//                    골드 수급이 1/3 로 내려가 1000 → 300 으로 재조정됨
//    AttackPercent : 보너스당 공격력 증가율 (0.01 = 1%)
//    HpPercent     : 보너스당 최대체력 증가율 (0.01 = 1%)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_GoldenPower", menuName = "BattleGame/Passives/GoldenPower")]
public class PassiveGoldenPower : PassiveSkillData
{
    [Header("황금의 힘 설정")]
    [Tooltip("골드 N당 보너스 1단계 증가")]
    public int GoldPerBonus = 300;

    [Tooltip("단계당 공격력 증가율 (0.01 = 1%)")]
    [Range(0f, 0.1f)]
    public float AttackPercent = 0.01f;

    [Tooltip("단계당 최대체력 증가율 (0.01 = 1%)")]
    [Range(0f, 0.1f)]
    public float HpPercent = 0.01f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity)) return;

        var itemData = UserDataManager.Instance?.Get<ItemData>();
        if (itemData == null) return;

        int gold = itemData.Get(eItem.Gold);
        if (gold <= 0 || GoldPerBonus <= 0) return;

        int steps = gold / GoldPerBonus;
        if (steps <= 0) return;

        var stat = em.GetComponentData<StatComponent>(ctx.GeneralEntity);

        float atkDelta = stat.Base[StatType.Attack] * AttackPercent * steps;
        float hpDelta  = stat.Base[StatType.MaxHp]  * HpPercent     * steps;

        stat.Base[StatType.Attack]  += atkDelta;
        stat.Final[StatType.Attack] += atkDelta;
        stat.Base[StatType.MaxHp]   += hpDelta;
        stat.Final[StatType.MaxHp]  += hpDelta;
        em.SetComponentData(ctx.GeneralEntity, stat);

        // MaxHp 증가분만큼 현재 체력도 증가 (전투 시작 직후 = 만피 유지)
        if (em.HasComponent<HealthComponent>(ctx.GeneralEntity))
        {
            var health = em.GetComponentData<HealthComponent>(ctx.GeneralEntity);
            health.CurrentHp += hpDelta;
            em.SetComponentData(ctx.GeneralEntity, health);
        }
    }

    public override void CollectPreviewStats(System.Func<StatType, float> current,
                                             System.Func<StatType, float> baseRoll,
                                             System.Collections.Generic.Dictionary<StatType, float> outDeltas)
    {
        var itemData = UserDataManager.Instance?.Get<ItemData>();
        if (itemData == null || GoldPerBonus <= 0) return;

        int steps = itemData.Get(eItem.Gold) / GoldPerBonus;
        if (steps <= 0) return;

        float atkDelta = current(StatType.Attack) * AttackPercent * steps;
        float hpDelta  = current(StatType.MaxHp)  * HpPercent     * steps;

        if (atkDelta != 0f)
            outDeltas[StatType.Attack] = outDeltas.TryGetValue(StatType.Attack, out var a) ? a + atkDelta : atkDelta;
        if (hpDelta != 0f)
            outDeltas[StatType.MaxHp]  = outDeltas.TryGetValue(StatType.MaxHp,  out var h) ? h + hpDelta  : hpDelta;
    }

}
