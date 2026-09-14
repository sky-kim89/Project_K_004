using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  DeathSentenceRunner.cs
//  사형 선고(DeathSentence)의 선고 → 처형 연출.
//
//    ① 선고 : 범위에 인장이 찍히고 화면이 크게 흔들린다
//    ② 처형 : **즉발** — 그 프레임에 판정·피해가 전부 끝난다
//             체력 비율 ≤ executeRatio → 즉사, 아니면 큰 피해
//    ③ 표식 : 처형된 적 자리에서 해골이 하나씩 떠오른다
//    ④ 보상 : 처형한 수만큼 시전자 공격력 누적 (전투 내 영구)
//
//  ■ 왜 즉발로 바꿨나
//    예전엔 낙인을 찍고 2초 뒤에 처형했다. 그 2초 동안 낙인은 찍힌 자리에
//    붙박여 있는데 적은 계속 걸어가므로 "누가 선고받았는지" 가 안 보였고,
//    정작 처형 순간에는 이펙트 한 장만 떠서 발동했는지조차 알기 어려웠다.
//    지금은 누른 즉시 끝나고, 대신 해골·화면 흔들림으로 결과를 보여 준다.
//
//  ⚠ 피해는 한 프레임에 전부 넣는다 — 해골만 시차를 준다
//    피해까지 늦추면 "즉발" 이 아니게 되고, 그 사이 적이 다른 스킬에 죽으면
//    처형 수가 어긋난다. 해골은 순수 연출이라 위치만 미리 받아 두면
//    대상이 이미 사라진 뒤에 떠올라도 문제없다.
//
//  ⚠ 즉사를 DeadTag 로 직접 처리하지 않는다
//    그러면 킬 수·전투 통계·처치 트리거(KillEmpower 등)가 전부 누락된다.
//    방어율을 역산해 "확실히 죽는 피해" 를 넣어 UnitHitSystem 이 죽이게 한다.
// ============================================================

public class DeathSentenceRunner : MonoBehaviour
{
    // 해골이 하나씩 떠오르는 간격. 전부 동시에 띄우면 한 덩어리로 뭉개진다.
    const float SkullInterval = 0.07f;

    public void Run(EntityManager em, Entity casterEntity, TeamType casterTeam,
                    float3 center, float radius, float executeRatio,
                    float damage, float attackPerKill, bool executeBosses, float knockMult,
                    string skullEffectKey, float sealLifetime,
                    SkillEffectConfig fx)
    {
        var centerPos = new Vector3(center.x, center.y, 0f);

        // ── ① 선고 ───────────────────────────────────────────
        // 인장은 짧게 찍고 뺀다 — 오래 두면 전장을 덮는 장판처럼 보인다.
        SkillEffectHelper.Spawn(fx.BaseEffectKey, centerPos, sealLifetime,
                                Quaternion.identity, radius / 3f);
        CameraShaker.Impulse(0.5f, centerPos);

        em.CompleteAllTrackedJobs();
        var marked = SkillCrowdControl.CollectEnemiesInRadius(em, center, radius, casterTeam);

        // ── ② 처형 — 판정·피해를 한 프레임에 끝낸다 ──────────
        var skullSpots = new List<Vector3>();
        int executed   = 0;

        foreach (var t in marked)
        {
            if (!em.Exists(t)) continue;
            if (!em.HasComponent<HealthComponent>(t) || !em.HasComponent<StatComponent>(t)) continue;

            var   stat   = em.GetComponentData<StatComponent>(t);
            var   health = em.GetComponentData<HealthComponent>(t);
            float maxHp  = math.max(1f, stat.Final[StatType.MaxHp]);
            float ratio  = health.CurrentHp / maxHp;

            bool isBoss     = em.HasComponent<BossComponent>(t);
            bool canExecute = ratio <= executeRatio && (executeBosses || !isBoss);

            // 낙인·처형 이펙트는 발밑이 아니라 몸통에 찍혀야 "쟤가 걸렸다" 로 읽힌다
            Vector3 body = SkillCrowdControl.BodyCenterOf(em, t);
            float3  dir  = new float3(body.x - center.x, body.y - center.y, 0f);

            SkillEffectHelper.Spawn(fx.CasterEffectKey, body, sealLifetime);

            if (canExecute)
            {
                SkillCrowdControl.DealDamage(em, t, LethalDamage(health.CurrentHp, stat),
                                             dir, knockMult, casterEntity);
                skullSpots.Add(body);
                executed++;
            }
            else
            {
                SkillCrowdControl.DealDamage(em, t, damage, dir, knockMult, casterEntity);
                SkillEffectHelper.Spawn(fx.TargetEffectKey, body, fx.DespawnDelay);
            }
        }

        // ── ③ 처형 보상 ──────────────────────────────────────
        if (executed > 0 && attackPerKill > 0f)
            SkillCrowdControl.AddEffect(em, casterEntity, StatType.Attack,
                                        attackPerKill * executed, EffectMode.Add,
                                        -1f, ActiveSkillId.DeathSentence);   // -1 = 전투 내 영구

        // ── ④ 해골 ───────────────────────────────────────────
        if (skullSpots.Count > 0)
            StartCoroutine(RaiseSkulls(skullSpots, skullEffectKey, fx.DespawnDelay));
    }

    // 처형된 자리마다 해골이 하나씩 떠오른다 — 몇 명이 처형됐는지 세어 보이게 하는 게 목적이다.
    IEnumerator RaiseSkulls(List<Vector3> spots, string skullKey, float despawn)
    {
        foreach (var spot in spots)
        {
            SkillEffectHelper.Spawn(skullKey, spot, despawn);
            CameraShaker.Impulse(0.1f, spot);

            if (SkullInterval > 0f) yield return new WaitForSeconds(SkullInterval);
        }
    }

    /// <summary>
    /// 방어율을 역산해 반드시 죽는 피해량을 구한다.
    /// UnitHitSystem 이 (1 - 방어율) 을 곱하므로 그만큼 부풀려야 정확히 죽는다.
    /// 과하게 넣으면 전투 통계의 총 딜량이 부풀어 오른다.
    /// </summary>
    static float LethalDamage(float currentHp, StatComponent stat)
    {
        float defense = math.clamp(stat.Final[StatType.Defense], 0f, 0.95f);
        return currentHp / math.max(0.05f, 1f - defense) + 1f;
    }
}
