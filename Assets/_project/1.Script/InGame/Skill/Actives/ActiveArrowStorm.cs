using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveArrowStorm.cs — 화살 폭풍 (궁수 · 희귀)
//
//  전방으로 산탄을 3연발 퍼붓는다.
//    ① 시위를 당기는 동안 그 자리에 선다 (3발이 끝날 때까지 고정)
//    ② 매 발사마다 부채꼴 범위의 적에게 피해 + **강한 넉백**
//    ③ 마지막 발은 큰 피해 + 경직
//
//  EffectRadius : 산탄 사거리
//  EffectValue  : 발당 피해 배율 보정
//
//  ⚠ 낙하형 광역이 아니다
//    예전엔 지정 지점에 화살비를 쏟는 방식이었는데 메테오·화살비와 그림이 겹쳤다.
//    "쏜다" 는 궁수의 동작이 보이도록 전방 산탄으로 바꿨다.
// ============================================================

[CreateAssetMenu(fileName = "Active_ArrowStorm", menuName = "BattleGame/Actives/ArrowStorm")]
public class ActiveArrowStorm : ActiveSkillData
{
    [Header("화살 폭풍 설정")]
    [Tooltip("1·2발 공격력 배율")]
    public float DamageMultiplier = 2.0f;

    [Tooltip("마지막 발 공격력 배율 (마무리 일격)")]
    public float FinalDamageMultiplier = 4.5f;

    [Tooltip("발사 횟수")]
    public int WaveCount = 3;

    [Tooltip("발사 간격 (초)")]
    public float WaveInterval = 0.45f;

    [Tooltip("첫 발까지의 조준 시간 (초)")]
    public float WarningTime = 0.35f;

    [Tooltip("산탄이 퍼지는 각도 (도). 60 이면 좌우 각 30도.")]
    [Range(10f, 180f)]
    public float ConeAngleDegrees = 60f;

    [Tooltip("넉백 배율 — 산탄이므로 세게 민다")]
    public float KnockbackMult = 9f;

    [Tooltip("타격마다 걸리는 이동속도 감소 비율 (0.4 = 40% 감소). 중첩된다.")]
    [Range(0f, 0.9f)]
    public float SlowRatio = 0.4f;

    [Tooltip("이동속도 감소 지속 시간 (초)")]
    public float SlowDuration = 3f;

    [Tooltip("마지막 발 경직 시간 (초)")]
    public float FinalStunDuration = 1.5f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (ctx.CasterObject == null) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        if (!em.HasComponent<LocalTransform>(ctx.CasterEntity)) return;

        var    casterTf  = em.GetComponentData<LocalTransform>(ctx.CasterEntity);
        float3 casterPos = new float3(casterTf.Position.x, casterTf.Position.y, 0f);

        // 조준 방향: 타겟이 있으면 타겟 쪽, 없으면 진군 방향(오른쪽)
        float3 forward = new float3(1f, 0f, 0f);
        if (ctx.HasTarget && em.Exists(ctx.TargetEntity) && em.HasComponent<LocalTransform>(ctx.TargetEntity))
        {
            var    t = em.GetComponentData<LocalTransform>(ctx.TargetEntity);
            float3 d = new float3(t.Position.x, t.Position.y, 0f) - casterPos;
            if (math.lengthsq(d) > 0.001f) forward = math.normalize(d);
        }

        var identity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);

        var runner = ctx.CasterObject.GetComponent<ArrowStormRunner>();
        if (runner == null) runner = ctx.CasterObject.AddComponent<ArrowStormRunner>();

        runner.Run(
            em           : em,
            casterEntity : ctx.CasterEntity,
            casterTeam   : identity.Team,
            casterTf     : ctx.CasterTransform,
            forward      : forward,
            range        : EffectRadius > 0f ? EffectRadius : 9f,
            halfAngle    : ConeAngleDegrees * 0.5f,
            waveDamage   : ctx.CasterStat.Final[StatType.Attack] * DamageMultiplier      * EffectValue,
            finalDamage  : ctx.CasterStat.Final[StatType.Attack] * FinalDamageMultiplier * EffectValue,
            waveCount    : Mathf.Max(1, WaveCount),
            waveInterval : WaveInterval,
            warningTime  : WarningTime,
            slowRatio    : SlowRatio,
            slowDuration : SlowDuration,
            finalStun    : FinalStunDuration,
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
