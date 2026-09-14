using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveMeteor.cs — 메테오 (법사)
//
//  타겟 위치에 메테오를 소환한다.
//  EffectDuration 초 후 착탄, EffectRadius 범위 적 전체에 강력한 피해 + 넉백.
//  MeteorRunner 가 딜레이 후 AoE 를 처리한다.
// ============================================================

[CreateAssetMenu(fileName = "Active_Meteor", menuName = "BattleGame/Actives/Meteor")]
public class ActiveMeteor : ActiveSkillData
{
    [Header("메테오 설정")]
    [Tooltip("기본 공격력 배율")]
    public float DamageMultiplier = 5f;

    [Tooltip("넉백 배율")]
    public float KnockbackMult = 8f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (!ctx.HasTarget) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        // 발동 시점에 스냅샷된 위치를 사용 — 타겟이 사망/디스폰되어도 원래 위치에 착탄
        Vector3 targetPos = ctx.TargetPosition;

        var casterIdentity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);

        var runner = ctx.CasterObject.GetComponent<MeteorRunner>();
        if (runner == null) runner = ctx.CasterObject.AddComponent<MeteorRunner>();

        runner.Run(
            impactPos        : targetPos,
            casterEntity     : ctx.CasterEntity,
            casterStat       : ctx.CasterStat,
            em               : em,
            casterTeam       : casterIdentity.Team,
            damageMultiplier : DamageMultiplier * EffectValue,
            aoeRadius        : EffectRadius > 0f ? EffectRadius : 3f,
            delay            : EffectDuration > 0f ? EffectDuration : 1.5f,
            knockbackMult    : KnockbackMult,
            fx               : new SkillEffectConfig
            {
                CasterEffectKey = CasterEffectKey,
                TargetEffectKey = TargetEffectKey,
                BaseEffectKey   = BaseEffectKey,
                DespawnDelay    = EffectDespawnDelay,
            }
        );
    }
}
