using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using BattleGame.Units;

// ============================================================
//  ActiveShockwave.cs — 충격파 (전사)
//
//  시전자 전방 부채꼴 범위의 모든 적에게 넉백 + 데미지를 가한다.
//  EffectRadius : 부채꼴 반경
//  EffectValue  : 데미지 배율 (공격력 × EffectValue)
//  ConeAngle    : 부채꼴 각도 (Inspector 설정)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Active_Shockwave", menuName = "BattleGame/Actives/Shockwave")]
public class ActiveShockwave : ActiveSkillData
{
    [UnityEngine.Header("충격파 설정")]
    [UnityEngine.Tooltip("부채꼴 각도 (도). 예: 120 → 좌우 각 60도 범위")]
    [UnityEngine.Range(30f, 360f)]
    public float ConeAngleDegrees = 120f;

    [UnityEngine.Tooltip("넉백 배율")]
    public float KnockbackMult = 6f;

    public override void Execute(ActiveSkillContext ctx)
    {
        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        if (!em.HasComponent<LocalTransform>(ctx.CasterEntity)) return;

        var casterTf       = em.GetComponentData<LocalTransform>(ctx.CasterEntity);
        var casterIdentity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);
        float3 casterPos   = new float3(casterTf.Position.x, casterTf.Position.y, 0f);

        // 전방 방향: 타겟이 있으면 타겟 방향, 없으면 오른쪽
        float3 forward = new float3(1f, 0f, 0f);
        if (ctx.HasTarget && em.Exists(ctx.TargetEntity) && em.HasComponent<LocalTransform>(ctx.TargetEntity))
        {
            var targetTf = em.GetComponentData<LocalTransform>(ctx.TargetEntity);
            float3 dir   = new float3(targetTf.Position.x, targetTf.Position.y, 0f) - casterPos;
            if (math.lengthsq(dir) > 0.001f)
                forward = math.normalize(dir);
        }

        float  radius      = EffectRadius > 0f ? EffectRadius : 3f;
        float  halfConeRad = math.radians(ConeAngleDegrees * 0.5f);
        float  damage      = ctx.CasterStat.Final[StatType.Attack] * EffectValue;

        // 부채꼴 방향에 맞춰 이펙트 회전 (Circle arc 의 0° = local +X 기준 → arc 중앙이 forward 를 향하도록 보정)
        float fwdAngle = math.degrees(math.atan2(forward.y, forward.x));
        float halfArc  = ConeAngleDegrees * 0.5f;
        var   fxRot    = UnityEngine.Quaternion.Euler(0f, 0f, fwdAngle - halfArc);
        float fxScale  = (radius / 3f);   // 프리팹 기준 반경 3

        // 사용자 이펙트 (충격파 발사 연출 — 방향·크기 연동)
        SkillEffectHelper.Spawn(CasterEffectKey,
            new UnityEngine.Vector3(casterPos.x, casterPos.y, casterPos.z),
            EffectDespawnDelay, fxRot, fxScale);

        var query = em.CreateEntityQuery(new EntityQueryDesc
        {
            All  = new ComponentType[] { ComponentType.ReadOnly<UnitIdentityComponent>(),
                                         ComponentType.ReadOnly<LocalTransform>() },
            None = new ComponentType[] { typeof(DeadTag) },
        });

        NativeArray<Entity>         entities   = query.ToEntityArray(Allocator.Temp);
        NativeArray<LocalTransform> transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            var id = em.GetComponentData<UnitIdentityComponent>(entities[i]);
            if (id.Team == casterIdentity.Team) continue;

            float3 toTarget = new float3(transforms[i].Position.x, transforms[i].Position.y, 0f) - casterPos;
            float  dist     = math.length(toTarget);
            if (dist > radius) continue;

            // 부채꼴 안에 있는지 확인
            float3 toDir  = toTarget / dist;
            float  dot    = math.dot(forward, toDir);
            float  angle  = math.acos(math.clamp(dot, -1f, 1f));
            if (angle > halfConeRad) continue;

            if (!em.HasBuffer<HitEventBufferElement>(entities[i])) continue;

            em.GetBuffer<HitEventBufferElement>(entities[i]).Add(new HitEventBufferElement
            {
                Damage         = damage,
                HitDirection   = toDir * KnockbackMult,
                AttackerEntity = ctx.CasterEntity,
                Type = BattleGame.Units.HitType.Skill,
            });
        }

        entities.Dispose();
        transforms.Dispose();
        query.Dispose();
    }
}
