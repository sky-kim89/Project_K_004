using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  PassiveKillEmpower.cs  [OnEnemyKill]
//  처치 강화 — 처치마다 공격력 N 누적 (최대 MaxStacks 스택).
//  CombatStackElement 버퍼에서 KillEmpower 슬롯 사용.
//
//  Inspector:
//    TriggerType = OnEnemyKill
//    AttackBonusPerKill: 스택당 공격력 증가 절대값
//    MaxStacks: 최대 스택 수
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_KillEmpower", menuName = "BattleGame/Passives/KillEmpower")]
public class PassiveKillEmpower : PassiveSkillData
{
    [UnityEngine.Header("처치 강화 설정")]
    public float AttackBonusPerKill = 10f;
    public int   MaxStacks          = 5;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;
        if (!em.HasBuffer<CombatStackElement>(ctx.GeneralEntity)) return;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity)) return;

        var buf = em.GetBuffer<CombatStackElement>(ctx.GeneralEntity);
        int cur = FindOrCreate(buf, PassiveSkillType.KillEmpower, out int idx);
        if (cur >= MaxStacks) return;

        var stat = em.GetComponentData<StatComponent>(ctx.GeneralEntity);
        stat.Base[StatType.Attack]  += AttackBonusPerKill;
        stat.Final[StatType.Attack] += AttackBonusPerKill;
        em.SetComponentData(ctx.GeneralEntity, stat);

        var elem = buf[idx]; elem.StackCount++; buf[idx] = elem;
    }

    static int FindOrCreate(DynamicBuffer<CombatStackElement> buf, PassiveSkillType type, out int idx)
    {
        for (int i = 0; i < buf.Length; i++)
        { if (buf[i].PassiveType == type) { idx = i; return buf[i].StackCount; } }
        buf.Add(new CombatStackElement { PassiveType = type, StackCount = 0 });
        idx = buf.Length - 1; return 0;
    }
}
