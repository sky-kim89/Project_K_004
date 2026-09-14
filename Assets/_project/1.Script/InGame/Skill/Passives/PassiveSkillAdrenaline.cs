using BattleGame.Units;

// ============================================================
//  PassiveSkillAdrenaline.cs  [OnSkillUse]
//  스킬 아드레날린 — 스킬 사용 시 공격력·공격속도 T초 버프.
//
//  Inspector:
//    TriggerType = OnSkillUse
//    AttackBuff: 공격력 증가 절대값
//    AtkSpeedBuff: 공격속도 증가 절대값
//    BuffDuration: 지속 시간(초)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_SkillAdrenaline", menuName = "BattleGame/Passives/SkillAdrenaline")]
public class PassiveSkillAdrenaline : PassiveSkillData
{
    [UnityEngine.Header("스킬 아드레날린 설정")]
    public float AttackBuff   = 20f;
    public float AtkSpeedBuff = 0.3f;
    public float BuffDuration = 5f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;
        if (!em.HasBuffer<StatusEffectBufferElement>(ctx.GeneralEntity)) return;

        var buf = em.GetBuffer<StatusEffectBufferElement>(ctx.GeneralEntity);
        buf.Add(new StatusEffectBufferElement
        {
            Stat = StatType.Attack, Delta = AttackBuff,
            Mode = EffectMode.Add, Duration = BuffDuration, Remaining = BuffDuration,
            SourceType = BuffSourceType.Passive, SourceId = (int)Type,
        });
        buf.Add(new StatusEffectBufferElement
        {
            Stat = StatType.AttackSpeed, Delta = AtkSpeedBuff,
            Mode = EffectMode.Add, Duration = BuffDuration, Remaining = BuffDuration,
            SourceType = BuffSourceType.Passive, SourceId = (int)Type,
        });
    }
}
