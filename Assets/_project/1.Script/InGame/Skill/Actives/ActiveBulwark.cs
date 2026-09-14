using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveBulwark.cs — 불멸의 방벽 (방패병 · 희귀)
//
//  방패를 땅에 꽂아 아군 전체를 덮는 방벽을 세운다.
//    ① 지속 시간 동안 아군 전원 방어율 대폭 상승
//    ② 종료 시 방벽이 터지며 주변 적에게 공격력 비례 폭발 피해 + 넉백
//    ③ 동시에 아군 전원 체력 회복
//
//  EffectRadius   : 폭발 반경
//  EffectDuration : 방벽 유지 시간
// ============================================================

[CreateAssetMenu(fileName = "Active_Bulwark", menuName = "BattleGame/Actives/Bulwark")]
public class ActiveBulwark : ActiveSkillData
{
    [Header("불멸의 방벽 설정")]
    [Tooltip("방벽 유지 중 아군 방어율 가산치 (0.45 = +45%p)")]
    [Range(0f, 0.9f)]
    public float DefenseBonus = 0.45f;

    [Tooltip("방벽 폭발 공격력 배율 (시전자 공격력 × 이 값)")]
    public float ExplodeMultiplier = 7f;

    [Tooltip("폭발 넉백 배율")]
    public float KnockbackMult = 6f;

    [Tooltip("종료 시 아군 회복량 (최대 체력 비율. 0.25 = 25%)")]
    [Range(0f, 1f)]
    public float HealRatio = 0.25f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (ctx.CasterObject == null) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        var identity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);

        var runner = ctx.CasterObject.GetComponent<BulwarkRunner>();
        if (runner == null) runner = ctx.CasterObject.AddComponent<BulwarkRunner>();

        runner.Run(
            em            : em,
            casterEntity  : ctx.CasterEntity,
            casterTeam    : identity.Team,
            casterTf      : ctx.CasterTransform,
            radius        : EffectRadius   > 0f ? EffectRadius   : 4f,
            duration      : EffectDuration > 0f ? EffectDuration : 5f,
            defenseBonus  : DefenseBonus,
            explodeDamage : ctx.CasterStat.Final[StatType.Attack] * ExplodeMultiplier * EffectValue,
            knockMult     : KnockbackMult,
            healRatio     : HealRatio,
            fx            : new SkillEffectConfig
            {
                CasterEffectKey = CasterEffectKey,
                TargetEffectKey = TargetEffectKey,
                BaseEffectKey   = BaseEffectKey,
                DespawnDelay    = EffectDespawnDelay,
            });
    }
}
