using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActivePiercingDash.cs — 관통 돌진 (근거리 전용 · 희귀)
//
//  쿨타임 1초짜리 "평타형" 스킬.
//    ① 전방으로 짧게 돌진한다
//    ② 지나간 직선 위의 적을 **관통해서** 전부 때린다
//    ③ 넉백은 약하게 — 1초마다 들어가므로 세게 밀면 적이 계속 떠 있는다
//
//  ⚠ 다른 희귀 스킬과 설계 의도가 다르다
//    나머지는 "한 방" 이라 길고 화려하지만, 이건 전투 내내 계속 도는 기술이다.
//    연출은 짧고 가볍게 — 매 초 화면이 번쩍이면 눈이 피로해진다.
//
//  EffectRadius : 돌진 거리 (= 관통 판정 길이)
// ============================================================

[CreateAssetMenu(fileName = "Active_PiercingDash", menuName = "BattleGame/Actives/PiercingDash")]
public class ActivePiercingDash : ActiveSkillData
{
    [Header("관통 돌진 설정")]
    [Tooltip("공격력 배율 — 1초 쿨이라 낮게 잡는다")]
    public float DamageMultiplier = 1.4f;

    [Tooltip("관통 판정 폭")]
    public float LineWidth = 1.6f;

    [Tooltip("돌진 속도 (유닛/초)")]
    public float DashSpeed = 26f;

    [Tooltip("넉백 배율 — 1초마다 들어가므로 약하게")]
    public float KnockbackMult = 1.2f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (ctx.CasterObject == null) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        if (!em.HasComponent<LocalTransform>(ctx.CasterEntity)) return;

        var    casterTf  = em.GetComponentData<LocalTransform>(ctx.CasterEntity);
        float3 casterPos = new float3(casterTf.Position.x, casterTf.Position.y, 0f);

        float3 forward = new float3(1f, 0f, 0f);
        if (ctx.HasTarget && em.Exists(ctx.TargetEntity) && em.HasComponent<LocalTransform>(ctx.TargetEntity))
        {
            var    t = em.GetComponentData<LocalTransform>(ctx.TargetEntity);
            float3 d = new float3(t.Position.x, t.Position.y, 0f) - casterPos;
            if (math.lengthsq(d) > 0.001f) forward = math.normalize(d);
        }

        var identity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);

        var runner = ctx.CasterObject.GetComponent<PiercingDashRunner>();
        if (runner == null) runner = ctx.CasterObject.AddComponent<PiercingDashRunner>();

        runner.Run(
            em          : em,
            casterEntity: ctx.CasterEntity,
            casterTeam  : identity.Team,
            casterTf    : ctx.CasterTransform,
            origin      : casterPos,
            forward     : forward,
            distance    : EffectRadius > 0f ? EffectRadius : 4f,
            width       : LineWidth,
            damage      : ctx.CasterStat.Final[StatType.Attack] * DamageMultiplier * EffectValue,
            dashSpeed   : DashSpeed,
            knockMult   : KnockbackMult,
            fx          : new SkillEffectConfig
            {
                CasterEffectKey = CasterEffectKey,
                TargetEffectKey = TargetEffectKey,
                BaseEffectKey   = BaseEffectKey,
                DespawnDelay    = EffectDespawnDelay,
            });
    }
}
