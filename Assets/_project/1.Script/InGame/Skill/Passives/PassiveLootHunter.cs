using BattleGame.Units;

// ============================================================
//  PassiveLootHunter.cs  [OnEnemyKill]
//  전리품 사냥 — 적 처치 시 골드 N 획득.
//
//  Inspector:
//    TriggerType = OnEnemyKill
//    GoldPerKill : 처치당 획득 골드 (기본 15)
//                  스테이지 클리어 골드가 500~1950 으로 내려가 50 → 15 로 재조정됨
//                  (처치 수 50~150 기준, 처치 골드 ≒ 스테이지 보상 1회분)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_LootHunter", menuName = "BattleGame/Passives/LootHunter")]
public class PassiveLootHunter : PassiveSkillData
{
    [UnityEngine.Header("전리품 사냥 설정")]
    [UnityEngine.Tooltip("적 1처치당 획득 골드")]
    public int GoldPerKill = 15;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        if (GoldPerKill <= 0) return;
        var itemData = UserDataManager.Instance?.Get<ItemData>();
        itemData?.Add(eItem.Gold, GoldPerKill);
    }
}
