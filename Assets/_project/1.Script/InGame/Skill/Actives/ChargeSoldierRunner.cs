using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ChargeSoldierRunner.cs — 돌격 병사 이동·충돌 처리기
//
//  실제 병사 GO 에 AddComponent 로 부착되어 돌격 시퀀스를 제어한다.
//
//  ■ 동작 순서
//    1. EntityLink.SyncPosition = false — 돌진 중 MonoBehaviour 가 위치 직접 제어
//    2. chargeDir 방향으로 chargeSpeed 로 이동
//    3. 매 프레임 ECS LocalTransform 을 GO 위치로 동기화 (UnitMovementSystem 덮어씀)
//    4. hitRadius 내 적에게 데미지 + 넉백 (적 1체당 1회)
//    5. maxDistance 도달 시 ECS 위치 최종 동기화 → SyncPosition 복원
//       → 병사는 일반 전투 AI 로 전환, 이 컴포넌트만 제거
// ============================================================

public class ChargeSoldierRunner : MonoBehaviour
{
    EntityQuery _query;
    Entity      _soldierEntity;

    void OnDestroy()
    {
        if (_query != default(EntityQuery))
            _query.Dispose();
    }

    public void Launch(
        Vector3       chargeDir,
        float         chargeSpeed,
        float         hitRadius,
        float         maxDistance,
        float         damage,
        float         knockbackForce,
        TeamType      casterTeam,
        Entity        casterEntity,
        Entity        soldierEntity,
        EntityManager em,
        SkillEffectConfig fx)
    {
        _soldierEntity = soldierEntity;

        _query = em.CreateEntityQuery(new EntityQueryDesc
        {
            All  = new ComponentType[]
            {
                ComponentType.ReadOnly<UnitIdentityComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
            },
            None = new ComponentType[] { typeof(DeadTag) },
        });

        // 돌진 중 EntityLink 가 ECS 위치를 GO 에 덮어쓰지 않도록 중단
        if (TryGetComponent<EntityLink>(out var link))
            link.SyncPosition = false;

        StartCoroutine(Sequence(
            chargeDir, chargeSpeed, hitRadius, maxDistance,
            damage, knockbackForce, casterTeam, casterEntity, em, fx));
    }

    IEnumerator Sequence(
        Vector3       chargeDir,
        float         chargeSpeed,
        float         hitRadius,
        float         maxDistance,
        float         damage,
        float         knockbackForce,
        TeamType      casterTeam,
        Entity        casterEntity,
        EntityManager em,
        SkillEffectConfig fx)
    {
        var   hitSet    = new HashSet<Entity>();
        float traveled  = 0f;
        float radiusSq  = hitRadius * hitRadius;
        var   knockDir3 = new float3(chargeDir.x, chargeDir.y, 0f);

        while (traveled < maxDistance)
        {
            float step = chargeSpeed * Time.deltaTime;
            transform.position += chargeDir * step;
            traveled           += step;

            em.CompleteAllTrackedJobs();

            // ECS LocalTransform 을 GO 위치로 동기화 — UnitMovementSystem 결과를 덮어씀
            if (_soldierEntity != Entity.Null && em.Exists(_soldierEntity))
            {
                em.SetComponentData(_soldierEntity, LocalTransform.FromPosition(
                    new float3(transform.position.x, transform.position.y, 0f)));
            }

            // 반경 내 적 탐색
            var entities   = _query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var transforms = _query.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
            var identities = _query.ToComponentDataArray<UnitIdentityComponent>(Unity.Collections.Allocator.Temp);

            Vector3 myPos = transform.position;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];

                if (identities[i].Team == casterTeam) continue;  // 아군 제외
                if (hitSet.Contains(e))                continue;  // 이미 피격

                float dx = transforms[i].Position.x - myPos.x;
                float dy = transforms[i].Position.y - myPos.y;
                if (dx * dx + dy * dy > radiusSq)     continue;

                if (!em.HasBuffer<HitEventBufferElement>(e)) continue;

                em.GetBuffer<HitEventBufferElement>(e).Add(new HitEventBufferElement
                {
                    Damage         = damage,
                    HitDirection   = knockDir3 * knockbackForce,
                    AttackerEntity = casterEntity,
                    Type           = HitType.Skill,
                });

                SkillEffectHelper.Spawn(
                    fx.TargetEffectKey,
                    new Vector3(transforms[i].Position.x, transforms[i].Position.y, 0f),
                    fx.DespawnDelay);

                hitSet.Add(e);
            }

            entities.Dispose();
            transforms.Dispose();
            identities.Dispose();

            yield return null;
        }

        // 돌격 종료 — ECS 위치 최종 동기화 후 일반 전투 AI 에 제어권 반환
        em.CompleteAllTrackedJobs();
        if (_soldierEntity != Entity.Null && em.Exists(_soldierEntity))
        {
            em.SetComponentData(_soldierEntity, LocalTransform.FromPosition(
                new float3(transform.position.x, transform.position.y, 0f)));
        }

        if (TryGetComponent<EntityLink>(out var link))
            link.SyncPosition = true;

        // 병사 GO 는 유지하고 이 컴포넌트만 제거
        Destroy(this);
    }
}
