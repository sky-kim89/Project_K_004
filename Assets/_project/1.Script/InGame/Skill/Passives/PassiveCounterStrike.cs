using BattleGame.Units;

// ============================================================
//  PassiveCounterStrike.cs  [OnHit]
//  피격 반격 강화 — 피격 시 X% 확률로 공격력 +N% T초 버프.
//
//  Inspector:
//    TriggerType = OnHit
//    TriggerChance: 발동 확률 (0.4 = 40%)
//    AttackBonusRatio: 기본 공격력 대비 버프 비율 (0.2 = +20%)
//    BuffDuration: 지속 시간(초)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_CounterStrike", menuName = "BattleGame/Passives/CounterStrike")]
public class PassiveCounterStrike : PassiveSkillData
{
    [UnityEngine.Header("피격 반격 강화 설정")]
    [UnityEngine.Range(0f, 1f)]
    public float TriggerChance      = 0.40f;
    [UnityEngine.Range(0f, 1f)]
    public float AttackBonusRatio   = 0.20f;
    public float BuffDuration       = 5f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        if (UnityEngine.Random.value > TriggerChance) return;
        var em = ctx.EntityManager;
        if (!em.HasBuffer<StatusEffectBufferElement>(ctx.GeneralEntity)) return;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity)) return;

        float bonus = em.GetComponentData<StatComponent>(ctx.GeneralEntity).Base[StatType.Attack] * AttackBonusRatio;

        var buf = em.GetBuffer<StatusEffectBufferElement>(ctx.GeneralEntity);

        // ⚠ 쌓지 않고 시간만 되살린다 (2026-09-07, DefenseShield 와 같은 이유)
        //   맞을 때마다 새 버프를 더하면 앞줄에서 둘러싸인 유닛의 공격력이
        //   상한 없이 불어난다. 이 게임은 아군이 물량이고 적도 부대 단위라
        //   원작보다 피격 횟수가 훨씬 많다. 세기는 AttackBonusRatio 로 조절할 것.
        for (int i = 0; i < buf.Length; i++)
        {
            var b = buf[i];
            if (b.SourceType != BuffSourceType.Passive) continue;
            if (b.SourceId   != (int)Type)              continue;
            if (b.Stat       != StatType.Attack)        continue;

            b.Delta     = bonus;
            b.Duration  = BuffDuration;
            b.Remaining = BuffDuration;
            buf[i]      = b;
            return;
        }

        buf.Add(new StatusEffectBufferElement
        {
            Stat       = StatType.Attack,
            Delta      = bonus,
            Mode       = EffectMode.Add,
            Duration   = BuffDuration,
            Remaining  = BuffDuration,
            SourceType = BuffSourceType.Passive,
            SourceId   = (int)Type,
        });
    }
}
