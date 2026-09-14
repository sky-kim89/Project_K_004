using System.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PiercingDashRunner.cs
//  관통 돌진(PiercingDash)의 돌진 시퀀스.
//
//    ① 돌진 : EntityLink 동기화를 끄고 transform 을 직접 밀어 낸다
//    ② 관통 : 출발점~도착점 직선 위의 적을 한 번에 전부 때린다
//    ③ 착지 : 도착 지점을 ECS 에 써 넣고 동기화를 되돌린다
//
//  ⚠ 돌진은 실제 이동이다 — 돌아오지 않는다
//    예전엔 동기화만 되돌려서 다음 프레임에 출발점으로 스냅됐다("치고 빠지기").
//    지금은 ECS LocalTransform 까지 도착 지점으로 옮겨 파고든 자리에 그대로 선다.
//    ECS 위치를 안 옮기고 동기화만 켜면 무조건 뒤로 끌려간다.
//
//  ⚠ 판정은 도착 후 한 번만 한다
//    매 프레임 검사하면 같은 적을 여러 번 때린다. 1초 쿨 기술이라
//    중복 타격이 붙으면 순식간에 다른 스킬 피해량을 넘어선다.
//
//  ⚠ 위치 동기화는 반드시 되돌린다
//    안 되돌리면 그 장군은 영원히 제자리에 붙는다 (OnDisable 안전망 포함).
// ============================================================

public class PiercingDashRunner : MonoBehaviour
{
    EntityLink _held;

    void OnDisable()
    {
        if (_held != null) _held.SyncPosition = true;
        _held = null;
    }

    public void Run(EntityManager em, Entity casterEntity, TeamType casterTeam,
                    Transform casterTf, float3 origin, float3 forward,
                    float distance, float width, float damage, float dashSpeed, float knockMult,
                    SkillEffectConfig fx)
    {
        StartCoroutine(Sequence(em, casterEntity, casterTeam, casterTf, origin, forward,
                                distance, width, damage, dashSpeed, knockMult, fx));
    }

    IEnumerator Sequence(EntityManager em, Entity casterEntity, TeamType casterTeam,
                         Transform casterTf, float3 origin, float3 forward,
                         float distance, float width, float damage, float dashSpeed, float knockMult,
                         SkillEffectConfig fx)
    {
        if (casterTf == null) yield break;

        float   angle = math.degrees(math.atan2(forward.y, forward.x));
        var     rot   = Quaternion.Euler(0f, 0f, angle);
        Vector3 start = casterTf.position;
        Vector3 end   = start + new Vector3(forward.x, forward.y, 0f) * distance;

        // 출발 먼지
        SkillEffectHelper.Spawn(fx.BaseEffectKey, start, fx.DespawnDelay, rot);

        // ── ① 돌진 ───────────────────────────────────────────
        var link = casterTf.GetComponent<EntityLink>();
        if (link != null && link.SyncPosition)
        {
            link.SyncPosition = false;
            _held = link;
        }

        while (Vector3.Distance(casterTf.position, end) > 0.05f)
        {
            casterTf.position = Vector3.MoveTowards(casterTf.position, end, dashSpeed * Time.deltaTime);
            yield return null;
        }

        // ── ② 관통 타격 ──────────────────────────────────────
        SkillEffectHelper.Spawn(fx.CasterEffectKey, casterTf.position, fx.DespawnDelay, rot);

        em.CompleteAllTrackedJobs();
        var targets = SkillCrowdControl.CollectEnemiesInLine(
            em, origin, forward, distance, width, casterTeam);

        foreach (var t in targets)
        {
            if (!em.Exists(t)) continue;

            Vector3 tp = SkillCrowdControl.PositionOf(em, t);
            SkillEffectHelper.Spawn(fx.TargetEffectKey, tp, fx.DespawnDelay, rot);
            SkillCrowdControl.DealDamage(em, t, damage, forward, knockMult, casterEntity);
        }

        // ── ③ 착지 ───────────────────────────────────────────
        // 도착 지점을 ECS 에 먼저 써 넣는다. 이 순서를 바꾸면(동기화 먼저)
        // 다음 LateUpdate 가 옛 위치로 덮어써 장군이 출발점으로 튕긴다.
        if (em.Exists(casterEntity) && em.HasComponent<LocalTransform>(casterEntity))
        {
            var ecsTf = em.GetComponentData<LocalTransform>(casterEntity);
            ecsTf.Position = new float3(casterTf.position.x, casterTf.position.y, ecsTf.Position.z);
            em.SetComponentData(casterEntity, ecsTf);
        }

        if (link != null)
        {
            link.SyncPosition = true;
            if (_held == link) _held = null;
        }
    }
}
