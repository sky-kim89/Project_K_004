using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveChainLightning.cs — 연쇄 번개 (공통 · 희귀)
//
//  대상에 꽂힌 뒤 맞은 적마다 갈라지며 번져 나간다.
//    · 파(wave)마다 갈래가 배로 늘어난다 — 1 → 2 → 4 → 8
//    · 파가 넘어갈 때마다 피해가 누적 증가한다 (마지막이 가장 아프다)
//    · 맞은 적은 짧게 감전 — 이동속도 격감
//    · 한 번 맞은 적은 다시 맞지 않는다 (두 적 사이를 왕복하면 그림이 죽는다)
//    · 주변에 적이 없으면 그 갈래는 그냥 끊긴다 (억지로 되돌아가지 않는다)
//
//  EffectRadius   : 다음 대상을 찾는 반경 (튀는 거리)
//  EffectValue    : 피해 배율 보정
// ============================================================

[CreateAssetMenu(fileName = "Active_ChainLightning", menuName = "BattleGame/Actives/ChainLightning")]
public class ActiveChainLightning : ActiveSkillData
{
    [Header("연쇄 번개 설정")]
    [Tooltip("첫 타격 공격력 배율")]
    public float DamageMultiplier = 2.2f;

    [Tooltip("번져 나가는 단계 수. 4 = 1 → 2 → 4 → 8 (최대 15명)")]
    public int MaxWaves = 4;

    [Tooltip("한 적이 갈라뜨리는 갈래 수. 2 면 파마다 배로 늘어난다.")]
    public int SplitCount = 2;

    [Tooltip("한 파 넘어갈 때마다 피해 증가율 (0.15 = +15% 누적)")]
    public float DamageGrowth = 0.15f;

    [Tooltip("파 사이 간격 (초). 0 이면 전부 같은 프레임에 터진다.")]
    public float ChainInterval = 0.09f;

    [Tooltip("번개 줄기가 남아 있는 시간 (초).\n" +
             "LineRenderer 는 페이드가 없어 이 시간 내내 밝기 그대로 떠 있다 — 길면 잔상이 화면을 덮는다.")]
    public float BoltLifetime = 0.14f;

    [Tooltip("감전 — 이동속도 감소 비율 (0.7 = 70% 감소)")]
    [Range(0f, 1f)]
    public float ShockSlowRatio = 0.7f;

    [Tooltip("감전 지속 시간 (초)")]
    public float ShockDuration = 2f;

    [Tooltip("넉백 배율 (감전이라 약하게)")]
    public float KnockbackMult = 1.5f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (ctx.CasterObject == null) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        if (!em.HasComponent<LocalTransform>(ctx.CasterEntity)) return;

        var identity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);

        // 첫 대상: 타겟이 있으면 그 적, 없으면 시전자 주변에서 가장 가까운 적
        Entity first = ctx.HasTarget && em.Exists(ctx.TargetEntity) ? ctx.TargetEntity : Entity.Null;

        var runner = ctx.CasterObject.GetComponent<ChainLightningRunner>();
        if (runner == null) runner = ctx.CasterObject.AddComponent<ChainLightningRunner>();

        runner.Run(
            em           : em,
            casterEntity : ctx.CasterEntity,
            casterTeam   : identity.Team,
            casterTf     : ctx.CasterTransform,
            firstTarget  : first,
            jumpRange    : EffectRadius > 0f ? EffectRadius : 5f,
            baseDamage   : ctx.CasterStat.Final[StatType.Attack] * DamageMultiplier * EffectValue,
            maxWaves     : Mathf.Max(1, MaxWaves),
            splitCount   : Mathf.Max(1, SplitCount),
            damageGrowth : DamageGrowth,
            waveInterval : ChainInterval,
            boltLife     : BoltLifetime,
            slowRatio    : ShockSlowRatio,
            slowDuration : ShockDuration,
            knockMult    : KnockbackMult,
            fx           : new SkillEffectConfig
            {
                CasterEffectKey = CasterEffectKey,
                TargetEffectKey = TargetEffectKey,
                BaseEffectKey   = BaseEffectKey,
                DespawnDelay    = EffectDespawnDelay,
            });
    }
}
