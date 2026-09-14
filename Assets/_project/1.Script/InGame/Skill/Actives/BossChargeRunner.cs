using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  BossChargeRunner.cs
//  돌진 연출 코루틴. 웅크림 → 돌진 → 착지.
//
//  ■ 같은 적을 두 번 때리지 않는다
//    경로를 프레임마다 훑으므로, 기록하지 않으면 한 적이 여러 프레임에
//    걸쳐 반복 피격된다. 지나친 적을 HashSet 에 담아 1회로 막는다.
//
//  ■ 타겟 엔티티가 아니라 '경로 위 전부' 를 때린다
//    몸통박치기라 지나간 자리에 있던 것은 다 밀려나야 자연스럽다.
//
//  ⚠ EntityLink.SyncPosition 을 꺼야 움직인다
//    평소엔 EntityLink 가 매 프레임 ECS → GameObject 로 위치를 덮어쓴다.
//    끄지 않으면 transform 을 아무리 옮겨도 다음 프레임에 원위치한다.
//    끝날 때 ECS 위치를 최종 좌표로 맞춰 주고 다시 켠다 —
//    안 그러면 보스가 돌진 시작점으로 순간이동한다.
// ============================================================

public class BossChargeRunner : MonoBehaviour
{
    Coroutine  _current;
    EntityLink _held;                       // SyncPosition 을 꺼 둔 링크
    readonly HashSet<Entity> _hit = new();

    // ⚠ 쿼리를 매 프레임 만들지 않는다
    //   돌진 경로 판정은 프레임마다 돈다. CreateEntityQuery 는 매번 잡 동기화를
    //   일으켜 1,000마리 전투에서 그대로 프레임 드랍이 된다. 한 번 만들어 재사용한다.
    EntityQuery _sweepQuery;
    bool        _queryReady;

    // 연출 도중 유닛이 죽거나 풀로 반납되면 코루틴이 끊긴다.
    // 그때 SyncPosition 이 꺼진 채로 남으면 그 유닛은 영영 안 움직인다.
    void OnDisable()
    {
        if (_held != null) _held.SyncPosition = true;
        _held    = null;
        _current = null;
        _hit.Clear();
    }

    public void Run(ActiveBossCharge data, ActiveSkillContext ctx)
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(Sequence(data, ctx));
    }

    IEnumerator Sequence(ActiveBossCharge d, ActiveSkillContext ctx)
    {
        _hit.Clear();

        Transform t  = ctx.CasterTransform;
        var       em = ctx.EntityManager;
        if (t == null) yield break;

        Vector3 start  = t.position;
        Vector3 target = ctx.TargetPosition;
        Vector3 dir    = target - start;
        dir.z = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.left;

        // 돌진 거리 상한 — 시야 밖까지 달려가지 않는다.
        // ⚠ 락 시간이 14유닛 이동을 가정하고 계산된다 (ActiveBossCharge.Execute)
        //   더 멀리 달리면 도착하기 전에 락이 풀려 이동 잡이 궤적을 끌어당긴다.
        //   사거리 판정이 빠진 지금은 침투 적 때문에 목표가 50유닛까지 벌어질 수 있다.
        float dist = dir.magnitude;
        dir /= dist;
        if (dist > UnitGridConstants.SightRange)
            target = start + dir * UnitGridConstants.SightRange;

        Vector3 destination = target + dir * d.OvershootDistance;

        // ECS 위치 덮어쓰기를 잠시 끈다 — 이걸 안 하면 transform 이 안 움직인다
        _held = t.GetComponent<EntityLink>();
        if (_held != null) _held.SyncPosition = false;

        // ── ① 웅크림 — 뒤로 살짝 빼면서 힘을 모은다 ────────────
        SkillEffectHelper.Spawn(d.BaseEffectKey, start, d.EffectDespawnDelay);

        Vector3 back = start - dir * d.WindupBackstep;
        float   e    = 0f;
        while (e < d.WindupTime)
        {
            e += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / d.WindupTime));
            t.position = Vector3.Lerp(start, back, k);
            yield return null;
        }

        // ── ② 돌진 ─────────────────────────────────────────────
        float attack   = ctx.CasterStat.Final[StatType.Attack];
        float damage   = attack * d.DamageMultiplier * Mathf.Max(0.1f, d.EffectValue);
        float trailGap = 0.1f;
        float trailT   = 0f;

        while ((t.position - destination).sqrMagnitude > 0.16f)
        {
            float step = d.ChargeSpeed * Time.deltaTime;
            t.position = Vector3.MoveTowards(t.position, destination, step);

            trailT += Time.deltaTime;
            if (trailT >= trailGap)
            {
                trailT = 0f;
                SkillEffectHelper.Spawn(d.CasterEffectKey, t.position, d.EffectDespawnDelay);
            }

            SweepHit(em, ctx, t.position, dir, damage, d);
            yield return null;
        }

        // ── ③ 착지 ─────────────────────────────────────────────
        SkillEffectHelper.Spawn(d.TargetEffectKey, t.position, d.EffectDespawnDelay);
        CameraShaker.Impulse(0.35f, t.position);   // 화면 밖이면 알아서 약해진다

        // 옮겨 온 위치를 ECS 에 되돌려 준 뒤 동기화를 다시 켠다.
        // 순서를 바꾸면 다음 프레임에 시작 지점으로 튕겨 돌아간다.
        if (em.Exists(ctx.CasterEntity) && em.HasComponent<LocalTransform>(ctx.CasterEntity))
        {
            var lt = em.GetComponentData<LocalTransform>(ctx.CasterEntity);
            lt.Position = t.position;
            em.SetComponentData(ctx.CasterEntity, lt);
        }
        if (_held != null) _held.SyncPosition = true;
        _held = null;

        yield return new WaitForSeconds(d.RecoverTime);
        _current = null;
    }

    // 돌진 경로 주변의 적을 1회씩 타격한다.
    void SweepHit(EntityManager em, ActiveSkillContext ctx,
                  Vector3 pos, Vector3 dir, float damage, ActiveBossCharge d)
    {
        if (!em.Exists(ctx.CasterEntity)) return;
        if (!em.HasComponent<UnitIdentityComponent>(ctx.CasterEntity)) return;

        TeamType myTeam = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity).Team;

        if (!_queryReady)
        {
            _sweepQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<UnitIdentityComponent>(),
                ComponentType.ReadOnly<HitEventBufferElement>(),
                ComponentType.Exclude<DeadTag>());
            _queryReady = true;
        }

        var ents = _sweepQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        var trs  = _sweepQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
        var ids  = _sweepQuery.ToComponentDataArray<UnitIdentityComponent>(Unity.Collections.Allocator.Temp);

        float r2 = d.HitRadius * d.HitRadius;
        float3 p = pos;

        for (int i = 0; i < ents.Length; i++)
        {
            if (ids[i].Team == myTeam)   continue;
            if (_hit.Contains(ents[i]))  continue;
            if (math.distancesq(trs[i].Position, p) > r2) continue;

            _hit.Add(ents[i]);
            em.GetBuffer<HitEventBufferElement>(ents[i]).Add(new HitEventBufferElement
            {
                Damage         = damage,
                HitDirection   = new float3(dir.x, dir.y, 0f) * d.KnockbackMult,
                AttackerEntity = ctx.CasterEntity,
                Type           = HitType.Skill,
            });
        }

        ents.Dispose();
        trs.Dispose();
        ids.Dispose();
    }

    void OnDestroy()
    {
        if (_queryReady) _sweepQuery.Dispose();
    }
}
