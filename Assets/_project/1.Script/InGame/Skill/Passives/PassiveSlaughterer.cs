using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  PassiveSlaughterer.cs  [OnEnemyKill]
//  도살자 — 처치마다 공격력 X% 누적 (최대 MaxStacks 스택).
//  CombatStackElement 버퍼의 Slaughterer 슬롯 사용.
//  전투 종료 시 CombatStackElement 버퍼 초기화로 자동 리셋됨.
//
//  Inspector:
//    TriggerType       = OnEnemyKill
//    AttackPerKillRatio: 처치당 공격력 증가율 (0.03 = 3%)
//    MaxStacks         : 최대 스택 수 (기본 10 → 최대 ~30%)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_Slaughterer", menuName = "BattleGame/Passives/Slaughterer")]
public class PassiveSlaughterer : PassiveSkillData
{
    [UnityEngine.Header("도살자 설정")]
    [UnityEngine.Tooltip("처치당 현재 기본 공격력 대비 증가율 (0.03 = 3%)")]
    [UnityEngine.Range(0f, 0.2f)]
    public float AttackPerKillRatio = 0.03f;

    [UnityEngine.Tooltip("최대 스택 수 (10스택 × 3% = 최대 ~30%)")]
    public int MaxStacks = 10;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;
        if (!em.HasBuffer<CombatStackElement>(ctx.GeneralEntity)) return;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity)) return;

        var buf = em.GetBuffer<CombatStackElement>(ctx.GeneralEntity);
        int cur = FindOrCreate(buf, PassiveSkillType.Slaughterer, out int idx);
        if (cur >= MaxStacks) return;

        var stat   = em.GetComponentData<StatComponent>(ctx.GeneralEntity);
        float bonus = stat.Base[StatType.Attack] * AttackPerKillRatio;
        stat.Base[StatType.Attack]  += bonus;
        stat.Final[StatType.Attack] += bonus;
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
