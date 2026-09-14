using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  SuicideSoldierRunner.cs — 자폭 병사 투사·폭발 처리기
//
//  ■ 이펙트 타이밍
//    - BaseEffect  : 투척 시작 시 병사 위치 (발사 연출)
//    - TargetEffect: 착탄 시 폭발 위치 (폭발 이펙트)
// ============================================================

public class SuicideSoldierRunner : MonoBehaviour
{
    Coroutine _current;

    void OnDisable() { _current = null; }

    public void Run(
        Transform       soldierTransform,
        Entity          soldierEntity,
        Vector3         targetPos,
        StatComponent   soldierStat,
        EntityManager   em,
        TeamType        casterTeam,
        float           damageMultiplier,
        float           aoeRadius,
        float           flightDuration,
        float           arcHeight,
        float           knockbackMult,
        SkillEffectConfig fx)
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(Sequence(
            soldierTransform, soldierEntity, targetPos, soldierStat, em,
            casterTeam, damageMultiplier, aoeRadius, flightDuration, arcHeight, knockbackMult, fx));
    }

    IEnumerator Sequence(
        Transform       soldierTransform,
        Entity          soldierEntity,
        Vector3         targetPos,
        StatComponent   soldierStat,
        EntityManager   em,
        TeamType        casterTeam,
        float           damageMultiplier,
        float           aoeRadius,
        float           flightDuration,
        float           arcHeight,
        float           knockbackMult,
        SkillEffectConfig fx)
    {
        Vector3 startPos = soldierTransform.position;
        float   elapsed  = 0f;
        float   duration = flightDuration > 0f ? flightDuration : 0.5f;

        // ── ① 발사 이펙트 ─────────────────────────────────────
        SkillEffectHelper.Spawn(fx.BaseEffectKey, startPos, fx.DespawnDelay);

        // ── ② 포물선 비행 ─────────────────────────────────────
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / duration);

            Vector3 flatPos = Vector3.Lerp(startPos, targetPos, t);
            float   yArc    = arcHeight * Mathf.Sin(Mathf.PI * t);

            soldierTransform.position = new Vector3(flatPos.x, flatPos.y + yArc, flatPos.z);

            if (em.Exists(soldierEntity))
            {
                em.SetComponentData(soldierEntity, LocalTransform.FromPosition(
                    new float3(soldierTransform.position.x,
                               soldierTransform.position.y,
                               soldierTransform.position.z)));
            }

            yield return null;
        }

        soldierTransform.position = targetPos;

        // ── ③ 폭발 이펙트 + AoE ──────────────────────────────
        SkillEffectHelper.Spawn(fx.TargetEffectKey, targetPos, fx.DespawnDelay);

        em.CompleteAllTrackedJobs();
        float3 center = new float3(targetPos.x, targetPos.y, 0f);

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

            float dist = math.distance(center, new float3(transforms[i].Position.x, transforms[i].Position.y, 0f));
            if (dist > aoeRadius) continue;

            if (!em.HasBuffer<HitEventBufferElement>(entities[i])) continue;

            float  damage   = soldierStat.Final[StatType.Attack] * damageMultiplier;
            float3 knockDir = dist > 0.01f
                ? math.normalizesafe(new float3(transforms[i].Position.x, transforms[i].Position.y, 0f) - center)
                : new float3(1f, 0f, 0f);

            em.GetBuffer<HitEventBufferElement>(entities[i]).Add(new HitEventBufferElement
            {
                Damage         = damage,
                HitDirection   = knockDir * knockbackMult,
                AttackerEntity = soldierEntity,
                Type = BattleGame.Units.HitType.Skill,
            });
        }

        entities.Dispose();
        transforms.Dispose();
        query.Dispose();

        // ── ④ 병사 즉사 ───────────────────────────────────────
        if (em.Exists(soldierEntity) && em.HasBuffer<HitEventBufferElement>(soldierEntity))
        {
            em.GetBuffer<HitEventBufferElement>(soldierEntity).Add(new HitEventBufferElement
            {
                Damage         = 999999f,
                HitDirection   = float3.zero,
                AttackerEntity = soldierEntity,
            });
        }

        _current = null;
    }
}
