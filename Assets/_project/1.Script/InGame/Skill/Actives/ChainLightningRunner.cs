using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ChainLightningRunner.cs
//  연쇄 번개(ChainLightning)의 갈라지는 시퀀스.
//
//    ① 시전자 → 첫 대상으로 번개를 잇는다            (1줄)
//    ② 맞은 적마다 주변 적 splitCount 명에게 갈라진다  (2 → 4 → 8)
//    ③ 파(wave)가 넘어갈 때마다 피해가 누적 증가한다
//
//  ■ 왜 한 줄로 튀지 않고 갈라지나
//    한 줄로 8번 튀면 화면에서는 "번개가 느리게 산책하는" 그림이 된다.
//    파마다 갈라지면 같은 타격 수라도 순식간에 전장을 덮는 그림이 나온다.
//
//  ⚠ 예약한 적은 그 즉시 visited 에 넣는다
//    안 넣으면 같은 파의 두 갈래가 같은 적을 집어 2→4 가 2→2 로 줄어든다.
//    (맞은 적을 다시 후보로 두면 두 적 사이를 왕복해 연쇄로 보이지 않는 문제도 그대로다)
//
//  ⚠ 번개 줄기는 아주 짧게 남긴다
//    LineRenderer 는 페이드가 없어 despawn 까지 **밝기 그대로** 떠 있다.
//    파가 4번이면 최대 15줄이 동시에 남아 화면이 파랗게 덮인다.
//    boltLife 는 "번쩍" 으로 읽히는 0.1~0.2초대로 둘 것.
//
//  ⚠ 진행 중인 시전을 끊지 않는다 (연속 시전·중복 시전 대응)
//    시퀀스마다 자기 방문 목록을 들고 있어 병행 실행해도 간섭하지 않는다.
// ============================================================

public class ChainLightningRunner : MonoBehaviour
{
    // 번개 한 줄기 = 출발점 + 도착 적
    struct Link
    {
        public Vector3 From;
        public Entity  To;
    }

    public void Run(EntityManager em, Entity casterEntity, TeamType casterTeam,
                    Transform casterTf, Entity firstTarget, float jumpRange,
                    float baseDamage, int maxWaves, int splitCount, float damageGrowth,
                    float waveInterval, float boltLife,
                    float slowRatio, float slowDuration, float knockMult,
                    SkillEffectConfig fx)
    {
        StartCoroutine(Sequence(em, casterEntity, casterTeam, casterTf, firstTarget, jumpRange,
                                baseDamage, maxWaves, splitCount, damageGrowth, waveInterval,
                                boltLife, slowRatio, slowDuration, knockMult, fx));
    }

    IEnumerator Sequence(EntityManager em, Entity casterEntity, TeamType casterTeam,
                         Transform casterTf, Entity firstTarget, float jumpRange,
                         float baseDamage, int maxWaves, int splitCount, float damageGrowth,
                         float waveInterval, float boltLife,
                         float slowRatio, float slowDuration, float knockMult,
                         SkillEffectConfig fx)
    {
        var visited = new HashSet<Entity>();

        Vector3 castPos = casterTf != null
            ? casterTf.position
            : SkillCrowdControl.PositionOf(em, casterEntity);

        // 시전자에게 방전 연출
        SkillEffectHelper.Spawn(fx.CasterEffectKey, castPos, fx.DespawnDelay);

        em.CompleteAllTrackedJobs();

        // ── 씨앗: 시전자 → 첫 대상 ───────────────────────────
        var seed = new List<Entity>(1);
        if (em.Exists(firstTarget) && visited.Add(firstTarget))
            seed.Add(firstTarget);
        else
            TakeNearest(em, castPos, jumpRange * 2f, casterTeam, visited, 1, seed);

        var frontier = new List<Link>(seed.Count);
        foreach (var s in seed) frontier.Add(new Link { From = castPos, To = s });

        float damage = baseDamage;
        var   struck = new List<Entity>();
        var   picked = new List<Entity>();

        for (int wave = 0; wave < maxWaves && frontier.Count > 0; wave++)
        {
            // ── 이번 파의 줄기를 전부 동시에 때린다 ──────────
            struck.Clear();

            foreach (var link in frontier)
            {
                if (!em.Exists(link.To)) continue;

                Vector3 to = SkillCrowdControl.BodyCenterOf(em, link.To);

                SkillEffectHelper.Spawn(fx.BaseEffectKey, link.From, to, boltLife);
                SkillEffectHelper.Spawn(fx.TargetEffectKey, to, fx.DespawnDelay);

                float3 dir = new float3(to.x - link.From.x, to.y - link.From.y, 0f);
                SkillCrowdControl.DealDamage(em, link.To, damage, dir, knockMult, casterEntity);

                // 감전 — 번개에 맞은 적은 발이 묶인다
                if (slowRatio > 0f)
                    SkillCrowdControl.AddEffect(em, link.To, StatType.MoveSpeed, 1f - slowRatio,
                                                EffectMode.Multiply, slowDuration,
                                                ActiveSkillId.ChainLightning);

                struck.Add(link.To);
            }

            if (waveInterval > 0f) yield return new WaitForSeconds(waveInterval);

            em.CompleteAllTrackedJobs();

            // ── 다음 파: 맞은 적마다 splitCount 갈래로 갈라진다 ──
            var next = new List<Link>(struck.Count * splitCount);

            foreach (var src in struck)
            {
                if (!em.Exists(src)) continue;

                Vector3 from = SkillCrowdControl.BodyCenterOf(em, src);

                picked.Clear();
                TakeNearest(em, from, jumpRange, casterTeam, visited, splitCount, picked);

                foreach (var p in picked) next.Add(new Link { From = from, To = p });
            }

            frontier = next;
            damage  *= 1f + damageGrowth;
        }
    }

    /// <summary>
    /// origin 반경 안에서 아직 안 맞은 가장 가까운 적을 count 명까지 집어 result 에 담는다.
    /// 집은 적은 그 자리에서 visited 에 등록한다 — 같은 파의 다른 갈래가 겹쳐 집지 않도록.
    /// </summary>
    static void TakeNearest(EntityManager em, Vector3 origin, float range, TeamType casterTeam,
                            HashSet<Entity> visited, int count, List<Entity> result)
    {
        if (count <= 0) return;

        var o          = new float3(origin.x, origin.y, 0f);
        var candidates = SkillCrowdControl.CollectEnemiesInRadius(em, o, range, casterTeam);

        // 거리를 미리 재 둔다 — 정렬 비교자 안에서 ECS 를 조회하면 같은 값을 수십 번 다시 읽는다
        var ranked = new List<(Entity Entity, float DistSq)>(candidates.Count);
        foreach (var e in candidates)
        {
            if (visited.Contains(e)) continue;

            Vector3 p = SkillCrowdControl.PositionOf(em, e);
            ranked.Add((e, math.lengthsq(new float3(p.x - o.x, p.y - o.y, 0f))));
        }

        ranked.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));

        int taken = Mathf.Min(count, ranked.Count);
        for (int i = 0; i < taken; i++)
        {
            visited.Add(ranked[i].Entity);
            result.Add(ranked[i].Entity);
        }
    }
}
