using System.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  BulwarkRunner.cs
//  불멸의 방벽(Bulwark)의 보호 → 폭발 시퀀스.
//
//    ① 방벽 전개 : 아군 전원 방어율 버프 + CasterEffect(돔, loop)
//    ② 유지      : duration 동안 대기 (돔이 시전자를 따라다닌다)
//    ③ 붕괴      : TargetEffect(폭발) + 주변 적 피해·넉백 + 아군 회복
//
//  ⚠ 돔은 시전자를 따라다녀야 한다
//    스폰 위치에 고정되면 방패병이 전진했을 때 돔만 뒤에 남는다.
//    풀에서 꺼낸 이펙트라 부모로 붙이지 않고 매 프레임 위치만 맞춘다
//    (부모로 붙이면 풀 반납 시 계층이 꼬인다).
// ============================================================

public class BulwarkRunner : MonoBehaviour
{
    // ⚠ 진행 중인 시전을 끊지 않는다 (연속 시전·중복 시전 대응)
    //   예전엔 새 시전이 들어오면 StopCoroutine 으로 이전 것을 끊었다.
    //   그러면 먼저 친 방벽의 "돔이 시전자를 따라가는 루프" 가 죽어서,
    //   돔 하나가 그 자리에 버려진 채 남고 새 돔만 따라다녔다.
    //   각 시퀀스는 자기 돔만 들고 있으므로 병행 실행해도 서로 간섭하지 않는다.
    public void Run(EntityManager em, Entity casterEntity, TeamType casterTeam,
                    Transform casterTf, float radius, float duration,
                    float defenseBonus, float explodeDamage, float knockMult, float healRatio,
                    SkillEffectConfig fx)
    {
        StartCoroutine(Sequence(em, casterEntity, casterTeam, casterTf, radius,
                                duration, defenseBonus, explodeDamage, knockMult,
                                healRatio, fx));
    }

    IEnumerator Sequence(EntityManager em, Entity casterEntity, TeamType casterTeam,
                         Transform casterTf, float radius, float duration,
                         float defenseBonus, float explodeDamage, float knockMult, float healRatio,
                         SkillEffectConfig fx)
    {
        // ── ① 방벽 전개 ──────────────────────────────────────
        var dome = SkillEffectHelper.Spawn(fx.CasterEffectKey, casterTf.position,
                                           duration + fx.DespawnDelay,
                                           Quaternion.identity, radius / 3f);

        em.CompleteAllTrackedJobs();
        var allies = SkillCrowdControl.CollectAllies(em, casterTeam);
        foreach (var a in allies)
            SkillCrowdControl.AddEffect(em, a, StatType.Defense, defenseBonus,
                                        EffectMode.Add, duration, ActiveSkillId.Bulwark);

        // ── ② 유지 — 돔이 시전자를 따라간다 ──────────────────
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (dome != null && casterTf != null) dome.transform.position = casterTf.position;
            elapsed += Time.deltaTime;
            yield return null;
        }
        // 여기서 코루틴이 끝나면 돔은 자동 반납된다 (스폰 시 duration + delay 로 예약)

        // ── ③ 붕괴 ───────────────────────────────────────────
        Vector3 center = casterTf != null ? casterTf.position : Vector3.zero;

        SkillEffectHelper.Spawn(fx.TargetEffectKey, center, fx.DespawnDelay,
                                Quaternion.identity, radius / 3f);

        em.CompleteAllTrackedJobs();

        var enemies = SkillCrowdControl.CollectEnemiesInRadius(
            em, new float3(center.x, center.y, 0f), radius, casterTeam);

        foreach (var e in enemies)
        {
            if (!em.Exists(e)) continue;

            Vector3 ep      = SkillCrowdControl.PositionOf(em, e);
            float3  outward = new float3(ep.x - center.x, ep.y - center.y, 0f);

            SkillCrowdControl.DealDamage(em, e, explodeDamage, outward, knockMult, casterEntity);
        }

        // 아군 회복 — 방벽이 흩어지며 치유광이 된다
        if (healRatio > 0f)
        {
            em.CompleteAllTrackedJobs();
            foreach (var a in SkillCrowdControl.CollectAllies(em, casterTeam))
            {
                if (!em.Exists(a) || !em.HasComponent<StatComponent>(a)) continue;

                float maxHp = em.GetComponentData<StatComponent>(a).Final[StatType.MaxHp];
                SkillCrowdControl.Heal(em, a, maxHp * healRatio, casterEntity);
            }
        }
    }
}
