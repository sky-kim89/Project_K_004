using System.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  GravityCollapseRunner.cs
//  중력 붕괴(GravityCollapse)의 흡입 → 폭발 시퀀스.
//
//    ① 붕괴점 생성 : BaseEffect(소용돌이)를 반경에 맞춰 깐다
//    ② 매 tick     : 범위 내 적을 중심으로 당기고 지속 피해
//    ③ 종료        : TargetEffect(붕괴 폭발) + 큰 피해 + 바깥 넉백
//
//  ⚠ 흡입 = 넉백의 방향만 뒤집은 것
//    HitEventBufferElement.HitDirection 을 중심 쪽으로 주면 그대로 빨려온다.
//    UnitHitSystem 이 넉백 내성을 곱하므로 보스는 덜 끌려온다.
//
//  ⚠ 이동속도 감소는 매 tick 갱신한다
//    한 번만 걸면 tick 사이에 만료돼 도중에 걸어 나간다.
// ============================================================

public class GravityCollapseRunner : MonoBehaviour
{
    // ⚠ 진행 중인 시전을 끊지 않는다 (연속 시전·중복 시전 대응)
    //   끊으면 먼저 만든 붕괴점이 폭발하지 못하고 소용돌이만 남는다.
    //   시퀀스마다 자기 중심점을 들고 있어 병행 실행해도 안전하다.
    public void Run(EntityManager em, Entity casterEntity, TeamType casterTeam,
                    float3 center, float radius, float duration, float tickInterval,
                    float dotPerSecond, float explodeDamage,
                    float pullForce, float rootRatio, float explodeKnock,
                    SkillEffectConfig fx)
    {
        StartCoroutine(Sequence(em, casterEntity, casterTeam, center, radius,
                                duration, tickInterval, dotPerSecond, explodeDamage,
                                pullForce, rootRatio, explodeKnock, fx));
    }

    IEnumerator Sequence(EntityManager em, Entity casterEntity, TeamType casterTeam,
                         float3 center, float radius, float duration, float tickInterval,
                         float dotPerSecond, float explodeDamage,
                         float pullForce, float rootRatio, float explodeKnock,
                         SkillEffectConfig fx)
    {
        var centerPos = new Vector3(center.x, center.y, 0f);

        // ── ① 붕괴점 생성 (프리팹 기준 반경 3) ────────────────
        SkillEffectHelper.Spawn(fx.BaseEffectKey, centerPos, fx.DespawnDelay,
                                Quaternion.identity, radius / 3f);

        // 붕괴점을 붙잡고 있는 동안 시전자는 멈춘다 — 일도양단과 같은 규칙
        SkillCrowdControl.AddEffect(em, casterEntity, StatType.MoveSpeed, 0f,
                                    EffectMode.Multiply, duration, ActiveSkillId.GravityCollapse);

        // ── ② 흡입 ───────────────────────────────────────────
        float elapsed   = 0f;
        float tickDamage = dotPerSecond * tickInterval;

        while (elapsed < duration)
        {
            em.CompleteAllTrackedJobs();
            var targets = SkillCrowdControl.CollectEnemiesInRadius(em, center, radius, casterTeam);

            foreach (var t in targets)
            {
                if (!em.Exists(t)) continue;

                Vector3 tp = SkillCrowdControl.PositionOf(em, t);

                // 중심 방향 = 흡입. 가까울수록 약하게 당겨 한 점에 뭉치지 않게 한다.
                float3 toCenter = new float3(center.x - tp.x, center.y - tp.y, 0f);
                float  dist     = math.length(toCenter);
                float  strength = pullForce * math.clamp(dist / math.max(0.01f, radius), 0.25f, 1f);

                SkillCrowdControl.DealDamage(em, t, tickDamage, toCenter, strength, casterEntity);

                // 발 묶기 — tick 마다 갱신해야 끊기지 않는다
                SkillCrowdControl.AddEffect(em, t, StatType.MoveSpeed, 1f - rootRatio,
                                            EffectMode.Multiply, tickInterval * 2f,
                                            ActiveSkillId.GravityCollapse);
            }

            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }

        // ── ③ 붕괴 폭발 ──────────────────────────────────────
        SkillEffectHelper.Spawn(fx.TargetEffectKey, centerPos, fx.DespawnDelay,
                                Quaternion.identity, radius / 3f);

        em.CompleteAllTrackedJobs();
        var finalTargets = SkillCrowdControl.CollectEnemiesInRadius(em, center, radius, casterTeam);

        foreach (var t in finalTargets)
        {
            if (!em.Exists(t)) continue;

            Vector3 tp      = SkillCrowdControl.PositionOf(em, t);
            float3  outward = new float3(tp.x - center.x, tp.y - center.y, 0f);

            SkillCrowdControl.DealDamage(em, t, explodeDamage, outward, explodeKnock, casterEntity);
        }
    }
}
