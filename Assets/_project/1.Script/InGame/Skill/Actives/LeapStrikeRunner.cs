using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  LeapStrikeRunner.cs — 도약 강타 시퀀스 실행기
//
//  ■ 이펙트 타이밍
//    - BaseEffect  : 도약 시작 시 시전자 위치 (발구름 연출)
//    - CasterEffect: 착지 시 시전자 위치 (AoE 충격파)
//    - TargetEffect: 범위 내 각 피격 적 위치
// ============================================================

public class LeapStrikeRunner : MonoBehaviour
{
    Coroutine _current;

    void OnDisable() { _current = null; }

    public void Run(
        Transform       casterTransform,
        Vector3         targetPos,
        Entity          casterEntity,
        StatComponent   casterStat,
        EntityManager   em,
        TeamType        casterTeam,
        float           damageMultiplier,
        float           aoeRadius,
        float           leapSpeed,
        float           knockbackMult,
        SkillEffectConfig fx)
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(Sequence(
            casterTransform, targetPos, casterEntity, casterStat, em,
            casterTeam, damageMultiplier, aoeRadius, leapSpeed, knockbackMult, fx));
    }

    IEnumerator Sequence(
        Transform       casterTransform,
        Vector3         targetPos,
        Entity          casterEntity,
        StatComponent   casterStat,
        EntityManager   em,
        TeamType        casterTeam,
        float           damageMultiplier,
        float           aoeRadius,
        float           leapSpeed,
        float           knockbackMult,
        SkillEffectConfig fx)
    {
        EntityLink entityLink = casterTransform.GetComponent<EntityLink>();

        // ECS→Transform 동기화 중단 + ECS 이동 동결 (도약 중 ECS가 위치를 덮어쓰지 않도록)
        if (entityLink != null) entityLink.SyncPosition = false;
        em.CompleteAllTrackedJobs();
        if (em.HasComponent<MovementComponent>(casterEntity))
        {
            var mv = em.GetComponentData<MovementComponent>(casterEntity);
            mv.MoveDelay = 999f;
            em.SetComponentData(casterEntity, mv);
        }

        // ── ① 도약 시작 이펙트 ────────────────────────────────
        SkillEffectHelper.Spawn(fx.BaseEffectKey, casterTransform.position, fx.DespawnDelay);

        // ── ② 도약 ────────────────────────────────────────────
        yield return MoveToward(casterTransform, targetPos, leapSpeed, stopDistance: 1.0f);

        // ── ③ 착지 이펙트 + AoE 타격 ─────────────────────────
        Vector3 landPos3D = casterTransform.position;
        float fxScale = aoeRadius / 2f;  // FX_Leap_Land 프리팹 기준 반경 2
        SkillEffectHelper.Spawn(fx.CasterEffectKey, landPos3D, fx.DespawnDelay, scale: fxScale);

        em.CompleteAllTrackedJobs();
        float3 landPos = new float3(landPos3D.x, landPos3D.y, 0f);

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
            if (id.Team == casterTeam) continue;

            float dist = math.distance(landPos, new float3(transforms[i].Position.x, transforms[i].Position.y, 0f));
            if (dist > aoeRadius) continue;

            if (!em.HasBuffer<HitEventBufferElement>(entities[i])) continue;

            SkillEffectHelper.Spawn(fx.TargetEffectKey,
                new Vector3(transforms[i].Position.x, transforms[i].Position.y, transforms[i].Position.z),
                fx.DespawnDelay);

            float  damage   = casterStat.Final[StatType.Attack] * damageMultiplier;
            float3 knockDir = math.normalizesafe(transforms[i].Position - landPos);

            em.GetBuffer<HitEventBufferElement>(entities[i]).Add(new HitEventBufferElement
            {
                Damage         = damage,
                HitDirection   = knockDir * knockbackMult,
                AttackerEntity = casterEntity,
                Type = BattleGame.Units.HitType.Skill,
            });
        }

        entities.Dispose();
        transforms.Dispose();
        query.Dispose();

        // ── ④ 착지 위치에서 ECS 동기화 후 이동 재개 ─────────
        em.CompleteAllTrackedJobs();
        if (em.Exists(casterEntity) && em.HasComponent<LocalTransform>(casterEntity))
        {
            var lt = em.GetComponentData<LocalTransform>(casterEntity);
            lt.Position = new Unity.Mathematics.float3(landPos3D.x, landPos3D.y, landPos3D.z);
            em.SetComponentData(casterEntity, lt);
        }
        if (em.HasComponent<MovementComponent>(casterEntity))
        {
            var mv = em.GetComponentData<MovementComponent>(casterEntity);
            mv.MoveDelay = 0f;
            em.SetComponentData(casterEntity, mv);
        }
        if (entityLink != null) entityLink.SyncPosition = true;

        _current = null;
    }

    IEnumerator MoveToward(Transform t, Vector3 destination, float speed, float stopDistance)
    {
        while (Vector3.Distance(t.position, destination) > stopDistance)
        {
            t.position = Vector3.MoveTowards(t.position, destination, speed * Time.deltaTime);
            yield return null;
        }
    }
}
