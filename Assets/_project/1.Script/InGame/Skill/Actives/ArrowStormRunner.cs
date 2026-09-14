using System.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ArrowStormRunner.cs
//  화살 폭풍(ArrowStorm) 의 전방 산탄 3연발을 돌리는 MonoBehaviour.
//
//    ① 조준 : 시전자를 그 자리에 묶는다 (3발이 끝날 때까지)
//    ② 발사 : 매 발마다 시전자 위치에서 CasterEffect(산탄)를 조준 방향으로 쏜다
//    ③ 명중 : 부채꼴 안의 적에게 피해 + 강한 넉백 + 이동속도 감소
//    ④ 마지막 발 : 큰 피해 + 경직
//
//  ⚠ 발사마다 대상을 다시 모은다
//    처음 한 번만 모으면 밀려난 적을 계속 때리고, 새로 들어온 적은 안 맞는다.
//    산탄은 넉백이 세서 적 배치가 발마다 크게 바뀐다.
//
//  ⚠ 방향은 첫 조준에서 고정한다
//    발마다 다시 조준하면 넉백으로 밀려난 적을 따라다니며 각도가 흔들린다.
//    "겨눈 방향으로 세 번 퍼붓는다" 가 이 스킬의 그림이다.
// ============================================================

public class ArrowStormRunner : MonoBehaviour
{
    // ⚠ 진행 중인 시전을 끊지 않는다 (연속 시전·중복 시전 대응)
    //   끊으면 먼저 시작한 3연발이 1발만 나가고 사라진다.
    //   시퀀스끼리 공유하는 상태가 없어 병행 실행해도 안전하다.
    public void Run(EntityManager em, Entity casterEntity, TeamType casterTeam,
                    Transform casterTf, float3 forward, float range, float halfAngle,
                    float waveDamage, float finalDamage,
                    int waveCount, float waveInterval, float warningTime,
                    float slowRatio, float slowDuration, float finalStun, float knockMult,
                    SkillEffectConfig fx)
    {
        StartCoroutine(Sequence(em, casterEntity, casterTeam, casterTf, forward,
                                range, halfAngle, waveDamage, finalDamage,
                                waveCount, waveInterval, warningTime,
                                slowRatio, slowDuration, finalStun, knockMult, fx));
    }

    IEnumerator Sequence(EntityManager em, Entity casterEntity, TeamType casterTeam,
                         Transform casterTf, float3 forward, float range, float halfAngle,
                         float waveDamage, float finalDamage,
                         int waveCount, float waveInterval, float warningTime,
                         float slowRatio, float slowDuration, float finalStun, float knockMult,
                         SkillEffectConfig fx)
    {
        float aimAngle = math.degrees(math.atan2(forward.y, forward.x));
        var   aimRot   = Quaternion.Euler(0f, 0f, aimAngle);

        // ── ① 조준 — 3발이 끝날 때까지 그 자리에 선다 ────────
        float castTime = warningTime + waveInterval * (waveCount - 1) + 0.2f;
        SkillCrowdControl.AddEffect(em, casterEntity, StatType.MoveSpeed, 0f,
                                    EffectMode.Multiply, castTime, ActiveSkillId.ArrowStorm);

        if (warningTime > 0f) yield return new WaitForSeconds(warningTime);

        // ── ② 산탄 3연발 ─────────────────────────────────────
        for (int wave = 0; wave < waveCount; wave++)
        {
            bool  isFinal = wave == waveCount - 1;
            float damage  = isFinal ? finalDamage : waveDamage;

            // 발사 연출 — 시전자 위치에서 조준 방향으로. 사거리에 맞춰 늘린다
            // (프리팹 기준 사거리 6)
            Vector3 muzzle = casterTf != null
                ? casterTf.position
                : SkillCrowdControl.PositionOf(em, casterEntity);

            SkillEffectHelper.Spawn(fx.CasterEffectKey, muzzle, fx.DespawnDelay,
                                    aimRot, range / 6f);

            em.CompleteAllTrackedJobs();
            var origin  = new float3(muzzle.x, muzzle.y, 0f);
            var targets = SkillCrowdControl.CollectEnemiesInCone(
                em, origin, forward, range, halfAngle, casterTeam);

            foreach (var t in targets)
            {
                if (!em.Exists(t)) continue;

                // 산탄은 맞은 방향 그대로 민다 — 정면일수록 세게 밀려난다
                float kb = isFinal ? knockMult * 1.4f : knockMult;
                SkillCrowdControl.DealDamage(em, t, damage, forward, kb, casterEntity);

                if (slowRatio > 0f)
                    SkillCrowdControl.AddEffect(em, t, StatType.MoveSpeed, 1f - slowRatio,
                                                EffectMode.Multiply, slowDuration,
                                                ActiveSkillId.ArrowStorm);

                Vector3 tp = SkillCrowdControl.PositionOf(em, t);
                SkillEffectHelper.Spawn(fx.TargetEffectKey, tp, fx.DespawnDelay,
                                        aimRot, isFinal ? 1.5f : 1f);

                if (isFinal) SkillCrowdControl.Stun(em, t, finalStun);
            }

            if (!isFinal && waveInterval > 0f)
                yield return new WaitForSeconds(waveInterval);
        }
    }
}
