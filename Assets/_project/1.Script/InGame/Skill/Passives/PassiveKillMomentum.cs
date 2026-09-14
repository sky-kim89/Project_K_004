using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  PassiveKillMomentum.cs  [OnEnemyKill]
//  처치 가속 — 처치마다 이동속도 N 누적 (최대 MaxStacks 스택).
//  CombatStackElement 버퍼에서 KillMomentum 슬롯 사용.
//
//  Inspector:
//    TriggerType = OnEnemyKill
//    SpeedBonusPerKill: 스택당 이동속도 증가 절대값
//    MaxStacks: 최대 스택 수
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_KillMomentum", menuName = "BattleGame/Passives/KillMomentum")]
public class PassiveKillMomentum : PassiveSkillData
{
    [UnityEngine.Header("처치 가속 설정")]
    public float SpeedBonusPerKill = 0.15f;
    public int   MaxStacks         = 5;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;
        if (!em.HasBuffer<CombatStackElement>(ctx.GeneralEntity)) return;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity)) return;

        var buf = em.GetBuffer<CombatStackElement>(ctx.GeneralEntity);
        int cur = FindOrCreate(buf, PassiveSkillType.KillMomentum, out int idx);
        if (cur >= MaxStacks) return;

        var stat = em.GetComponentData<StatComponent>(ctx.GeneralEntity);
        stat.Base[StatType.MoveSpeed]  += SpeedBonusPerKill;
        stat.Final[StatType.MoveSpeed] += SpeedBonusPerKill;
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
