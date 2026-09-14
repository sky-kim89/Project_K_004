using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveGravityCollapse.cs — 중력 붕괴 (법사 · 희귀)
//
//  타겟 지점에 붕괴점을 만든다.
//    ① 범위 내 적을 중심으로 계속 빨아들이며 발을 묶는다 (지속 시간 동안)
//    ② 빨려 들어가는 동안 지속 피해
//    ③ 종료 시 붕괴 폭발 — 큰 피해 + 바깥으로 강한 넉백
//
//  흡입은 넉백 벡터를 "중심 방향" 으로 주는 것이다 (넉백의 반대 부호).
//  넉백 내성이 있는 보스는 덜 끌려온다 — 의도된 동작.
//
//  EffectRadius   : 붕괴 반경
//  EffectDuration : 흡입 지속 시간
// ============================================================

[CreateAssetMenu(fileName = "Active_GravityCollapse", menuName = "BattleGame/Actives/GravityCollapse")]
public class ActiveGravityCollapse : ActiveSkillData
{
    [Header("중력 붕괴 설정")]
    [Tooltip("흡입 중 초당 피해 배율 (공격력 × 이 값 / 초)")]
    public float DotMultiplier = 0.8f;

    [Tooltip("붕괴 폭발 공격력 배율")]
    public float ExplodeMultiplier = 8f;

    [Tooltip("흡입 판정 간격 (초). 짧을수록 부드럽게 끌려온다.")]
    public float TickInterval = 0.15f;

    [Tooltip("흡입 강도 (넉백 힘의 반대 방향)")]
    public float PullForce = 3.5f;

    [Tooltip("흡입 중 이동속도 감소 비율 (0.9 = 90% 감소)")]
    [Range(0f, 1f)]
    public float RootRatio = 0.9f;

    [Tooltip("폭발 넉백 배율")]
    public float ExplodeKnockback = 8f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (ctx.CasterObject == null) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        Vector3 center = ctx.HasTarget
            ? ctx.TargetPosition
            : ctx.CasterTransform.position + Vector3.right * 5f;

        var identity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);
        float attack = ctx.CasterStat.Final[StatType.Attack];

        var runner = ctx.CasterObject.GetComponent<GravityCollapseRunner>();
        if (runner == null) runner = ctx.CasterObject.AddComponent<GravityCollapseRunner>();

        runner.Run(
            em            : em,
            casterEntity  : ctx.CasterEntity,
            casterTeam    : identity.Team,
            center        : new float3(center.x, center.y, 0f),
            radius        : EffectRadius   > 0f ? EffectRadius   : 4.5f,
            duration      : EffectDuration > 0f ? EffectDuration : 2.5f,
            tickInterval  : Mathf.Max(0.05f, TickInterval),
            dotPerSecond  : attack * DotMultiplier * EffectValue,
            explodeDamage : attack * ExplodeMultiplier * EffectValue,
            pullForce     : PullForce,
            rootRatio     : RootRatio,
            explodeKnock  : ExplodeKnockback,
            fx            : new SkillEffectConfig
            {
                CasterEffectKey = CasterEffectKey,
                TargetEffectKey = TargetEffectKey,
                BaseEffectKey   = BaseEffectKey,
                DespawnDelay    = EffectDespawnDelay,
            });
    }
}
