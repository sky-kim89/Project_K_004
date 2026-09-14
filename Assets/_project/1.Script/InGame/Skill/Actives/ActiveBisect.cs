using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveBisect.cs — 일도양단 (기사 · 희귀)
//
//  전방 직선 위의 적을 베어 넘긴다.
//    ① 납도 자세로 검광 응축 (ChargeTime)
//    ② 참격선 위 적 전원 경직 — 그 자리에서 부들부들 떤다 (TrembleTime)
//    ③ 검선이 지나가며 전원 동시 피격 + 강한 넉백
//
//  EffectRadius : 참격선 길이
//  EffectValue  : 피해 배율에 곱해지는 보정
//
//  실제 시퀀스는 BisectRunner 가 코루틴으로 돌린다 (지연 타격이 필요하므로).
// ============================================================

[CreateAssetMenu(fileName = "Active_Bisect", menuName = "BattleGame/Actives/Bisect")]
public class ActiveBisect : ActiveSkillData
{
    [Header("일도양단 설정")]
    [Tooltip("공격력 배율 (예: 6.0 → 공격력 × 6 피해)")]
    public float DamageMultiplier = 6f;

    [Tooltip("참격선 폭 (월드 단위)")]
    public float LineWidth = 2.2f;

    [Tooltip("검광 응축 시간 (초) — 이 동안은 아무 일도 일어나지 않는다")]
    public float ChargeTime = 0.35f;

    [Tooltip("경직 후 참격까지의 시간 (초). 적이 부들부들 떠는 구간.")]
    public float TrembleTime = 1f;

    [Tooltip("넉백 배율")]
    public float KnockbackMult = 7f;

    [Tooltip("타겟이 EffectRadius 보다 멀 때 참격선을 타겟 뒤까지 늘리는 여유 거리.\n" +
             "이게 없으면 적이 사거리 밖에 서 있을 때 허공만 벤다.")]
    public float ReachMargin = 6f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (ctx.CasterObject == null) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        if (!em.HasComponent<LocalTransform>(ctx.CasterEntity)) return;

        var    casterTf  = em.GetComponentData<LocalTransform>(ctx.CasterEntity);
        float3 casterPos = new float3(casterTf.Position.x, casterTf.Position.y, 0f);

        // 참격 방향: 타겟이 있으면 타겟 쪽, 없으면 진군 방향(오른쪽)
        float3 forward = new float3(1f, 0f, 0f);
        float  length  = EffectRadius > 0f ? EffectRadius : 9f;

        if (ctx.HasTarget && em.Exists(ctx.TargetEntity) && em.HasComponent<LocalTransform>(ctx.TargetEntity))
        {
            var    t = em.GetComponentData<LocalTransform>(ctx.TargetEntity);
            float3 d = new float3(t.Position.x, t.Position.y, 0f) - casterPos;
            float  dist = math.length(d);

            if (dist > 0.001f)
            {
                forward = d / dist;
                // 타겟이 기본 길이보다 멀면 그 뒤까지 벤다 — 허공을 베고 끝나면 안 된다
                length = math.max(length, dist + ReachMargin);
            }
        }

        var identity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);

        var runner = ctx.CasterObject.GetComponent<BisectRunner>();
        if (runner == null) runner = ctx.CasterObject.AddComponent<BisectRunner>();

        runner.Run(
            em          : em,
            casterEntity: ctx.CasterEntity,
            casterTeam  : identity.Team,
            origin      : casterPos,
            forward     : forward,
            length      : length,
            width       : LineWidth,
            damage      : ctx.CasterStat.Final[StatType.Attack] * DamageMultiplier * EffectValue,
            chargeTime  : ChargeTime,
            trembleTime : TrembleTime,
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
