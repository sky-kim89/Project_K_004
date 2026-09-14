using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PassiveStrengthStack.cs  [OnAttack]
//  연격 스택 — 연속 공격마다 공격력 N 누적 (최대 MaxStacks 스택).
//  CombatStackElement 버퍼 슬롯 0 을 사용.
//
//  Inspector:
//    TriggerType = OnAttack
//    AttackBonusPerStack: 스택당 공격력 증가 절대값
//    MaxStacks: 최대 스택 수
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_StrengthStack", menuName = "BattleGame/Passives/StrengthStack")]
public class PassiveStrengthStack : PassiveSkillData
{
    [Header("연격 스택 설정")]
    public float AttackBonusPerStack = 8f;
    public int   MaxStacks           = 5;

    const int SlotIndex = 0;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;
        if (!em.HasBuffer<CombatStackElement>(ctx.GeneralEntity)) return;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity)) return;

        var buf = em.GetBuffer<CombatStackElement>(ctx.GeneralEntity);

        int existing = FindOrCreate(buf, PassiveSkillType.StrengthStack, out int idx);
        if (existing >= MaxStacks) return;

        var stat = em.GetComponentData<StatComponent>(ctx.GeneralEntity);
        stat.Base[StatType.Attack]  += AttackBonusPerStack;
        stat.Final[StatType.Attack] += AttackBonusPerStack;
        em.SetComponentData(ctx.GeneralEntity, stat);

        var elem = buf[idx];
        elem.StackCount++;
        buf[idx] = elem;
    }

    static int FindOrCreate(DynamicBuffer<CombatStackElement> buf, PassiveSkillType type, out int idx)
    {
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i].PassiveType == type) { idx = i; return buf[i].StackCount; }
        }
        buf.Add(new CombatStackElement { PassiveType = type, StackCount = 0 });
        idx = buf.Length - 1;
        return 0;
    }
}
