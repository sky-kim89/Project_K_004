using System.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  BossSlamRunner.cs
//  분쇄 강타 연출. 예고(팔 들기) → 도약 → 내려찍기 → 경직.
//
//  ■ 도약을 y 오프셋으로만 처리한다
//    보스 위치를 실제로 옮기면 SkillCastLock 이 풀린 뒤 이동 잡이
//    공중에 뜬 좌표에서 이어받아 어색해진다. 시작 위치를 기억해 두고
//    y 만 얹었다가 착지 때 정확히 되돌린다.
//
//  ■ 피해는 착지 프레임에 한 번만
//    돌진과 달리 경로가 없어 프레임마다 훑을 이유가 없다.
//
//  ⚠ EntityLink.SyncPosition 을 꺼야 도약이 보인다
//    평소엔 EntityLink 가 매 프레임 ECS → GameObject 로 위치를 덮어쓴다.
//    도약은 ECS 좌표를 바꾸지 않는 '보여주기용 y' 라 반드시 꺼야 한다.
// ============================================================

public class BossSlamRunner : MonoBehaviour
{
    Coroutine  _current;
    EntityLink _held;

    // 연출 중 죽거나 풀로 반납되면 꺼진 채로 남는다 — 여기서 반드시 되돌린다.
    void OnDisable()
    {
        if (_held != null) _held.SyncPosition = true;
        _held    = null;
        _current = null;
    }

    public void Run(ActiveBossSlam data, ActiveSkillContext ctx)
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(Sequence(data, ctx));
    }

    IEnumerator Sequence(ActiveBossSlam d, ActiveSkillContext ctx)
    {
        Transform t  = ctx.CasterTransform;
        var       em = ctx.EntityManager;
        if (t == null) yield break;

        Vector3 ground = t.position;

        // 도약은 ECS 좌표를 건드리지 않는 연출이라 동기화를 꺼야 보인다
        _held = t.GetComponent<EntityLink>();
        if (_held != null) _held.SyncPosition = false;

        // ── ① 예고 — 발밑에 장판, 몸은 살짝 웅크렸다 편다 ──────
        //  ⚠ 반경을 곱해서 띄운다
        //    예고 장판은 "어디까지 맞는가" 를 말하는 그림이다. 프리팹 기본 크기로
        //    띄우면 반경 3 짜리로 보이는데 실제로는 7 까지 때린다 —
        //    피했다고 생각한 자리에서 맞으면 그건 예고가 아니다.
        //    (프리팹 기준 반경 3 — RareSkillEffectGenerator 의 스케일 연동 규칙)
        float fxScale = d.SlamRadius / 3f;
        SkillEffectHelper.Spawn(d.BaseEffectKey, ground, d.WindupTime + d.SlamTime + 0.3f,
                                default, fxScale);

        float e = 0f;
        while (e < d.WindupTime)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / d.WindupTime);
            // 앞부분에서 살짝 가라앉았다가 뒤에서 들어올린다
            float dip = Mathf.Sin(k * Mathf.PI) * -0.35f;
            float rise = Mathf.SmoothStep(0f, d.JumpHeight, Mathf.Clamp01((k - 0.55f) / 0.45f));
            t.position = ground + new Vector3(0f, dip + rise, 0f);
            yield return null;
        }

        // ── ② 내려찍기 ─────────────────────────────────────────
        Vector3 top = t.position;
        e = 0f;
        while (e < d.SlamTime)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / d.SlamTime);
            t.position = Vector3.Lerp(top, ground, k * k);   // 가속하며 떨어진다
            yield return null;
        }
        t.position = ground;

        // ── ③ 착탄 ─────────────────────────────────────────────
        // 착탄도 같은 배율 — 예고와 크기가 다르면 "예고보다 더 넓게 터졌다" 로 읽힌다
        SkillEffectHelper.Spawn(d.CasterEffectKey, ground, d.EffectDespawnDelay,
                                default, fxScale);
        CameraShaker.Impulse(0.6f, ground);

        Explode(em, ctx, ground, d);

        // 제자리 스킬이라 ECS 좌표는 그대로다 — 동기화만 다시 켠다.
        if (_held != null) _held.SyncPosition = true;
        _held = null;

        yield return new WaitForSeconds(d.RecoverTime);
        _current = null;
    }

    void Explode(EntityManager em, ActiveSkillContext ctx, Vector3 center, ActiveBossSlam d)
    {
        if (!em.Exists(ctx.CasterEntity)) return;
        if (!em.HasComponent<UnitIdentityComponent>(ctx.CasterEntity)) return;

        TeamType myTeam = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity).Team;
        float    damage = ctx.CasterStat.Final[StatType.Attack]
                        * d.DamageMultiplier * Mathf.Max(0.1f, d.EffectValue);

        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<UnitIdentityComponent>(),
            ComponentType.ReadOnly<HitEventBufferElement>(),
            ComponentType.Exclude<DeadTag>());

        var ents = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        var trs  = query.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
        var ids  = query.ToComponentDataArray<UnitIdentityComponent>(Unity.Collections.Allocator.Temp);

        float  r2 = d.SlamRadius * d.SlamRadius;
        float3 c  = center;

        for (int i = 0; i < ents.Length; i++)
        {
            if (ids[i].Team == myTeam) continue;

            float3 to     = trs[i].Position - c;
            float  distSq = math.lengthsq(to);
            if (distSq > r2) continue;

            // 중심에서 바깥으로 밀어낸다. 정확히 겹쳐 있으면 임의 방향.
            float3 dir = distSq > 0.0001f ? math.normalize(to) : new float3(1f, 0f, 0f);

            // 가장자리는 피해를 덜 받는다 — 반경이 넓어 전멸을 막는 완충
            float falloff = Mathf.Lerp(1f, 0.55f, Mathf.Sqrt(distSq) / d.SlamRadius);

            SkillEffectHelper.Spawn(d.TargetEffectKey, (Vector3)trs[i].Position, d.EffectDespawnDelay);

            em.GetBuffer<HitEventBufferElement>(ents[i]).Add(new HitEventBufferElement
            {
                Damage         = damage * falloff,
                HitDirection   = dir * d.KnockbackMult,
                AttackerEntity = ctx.CasterEntity,
                Type           = HitType.Skill,
            });
        }

        ents.Dispose();
        trs.Dispose();
        ids.Dispose();
        query.Dispose();
    }
}
