using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  BloodPriceRunner.cs
//  피의 대가(BloodPrice)의 응축 → 분출 → 착탄 시퀀스.
//
//    ① 응축 : 시전자가 발을 붙이고 피를 끌어모은다 (짧게)
//    ② 분출 : 부채꼴을 따라 핏빛 파도가 세 겹으로 밀려 나간다 + 화면이 크게 흔들린다
//    ③ 착탄 : 파도가 닿는 순서(= 시전자와의 거리순)대로 하나씩 터진다
//
//  ⚠ 체력 대가는 여기서 받지 않는다
//    시전 즉시 ActiveBloodPrice.Execute 가 깎는다. 응축 중에 회복되거나 맞아도
//    "지불한 만큼" 나가야 지를지 말지의 판단이 성립한다.
//
//  ⚠ 거리순 착탄은 피해 시점을 뒤로 민다
//    가장 먼 적은 WaveTime 만큼 늦게 맞는다. 그 사이 죽거나 이동할 수 있으므로
//    타격 직전에 다시 em.Exists / 위치를 확인한다.
// ============================================================

public class BloodPriceRunner : MonoBehaviour
{
    // 가장 먼 적까지 파도가 도달하는 시간. 더 늘리면 "한 방" 이 아니라 장판처럼 보인다.
    const float WaveTime = 0.18f;

    public void Run(EntityManager em, Entity casterEntity, TeamType casterTeam,
                    float3 origin, float3 forward, float range, float halfAngleDeg,
                    float damage, float knockMult, float chargeTime,
                    SkillEffectConfig fx)
    {
        StartCoroutine(Sequence(em, casterEntity, casterTeam, origin, forward, range,
                                halfAngleDeg, damage, knockMult, chargeTime, fx));
    }

    IEnumerator Sequence(EntityManager em, Entity casterEntity, TeamType casterTeam,
                         float3 origin, float3 forward, float range, float halfAngleDeg,
                         float damage, float knockMult, float chargeTime,
                         SkillEffectConfig fx)
    {
        float   angle = math.degrees(math.atan2(forward.y, forward.x));
        var     rot   = Quaternion.Euler(0f, 0f, angle);
        Vector3 o     = new Vector3(origin.x, origin.y, 0f);
        Vector3 dir   = new Vector3(forward.x, forward.y, 0f);

        // ── ① 응축 ───────────────────────────────────────────
        // 시전 내내 시전자는 그 자리에 버틴다 — 안 묶으면 터지는 순간
        // 이미 딴 데 가 있어 부채꼴이 시전자와 따로 논다.
        SkillEffectHelper.Spawn(fx.CasterEffectKey, o, chargeTime + fx.DespawnDelay, rot, 1.6f);
        SkillCrowdControl.AddEffect(em, casterEntity, StatType.MoveSpeed, 0f,
                                    EffectMode.Multiply, chargeTime, ActiveSkillId.BloodPrice);
        CameraShaker.Impulse(0.14f, o);

        if (chargeTime > 0f) yield return new WaitForSeconds(chargeTime);

        // ── ② 분출 ───────────────────────────────────────────
        CameraShaker.Impulse(0.6f, o);
        StartCoroutine(Wave(fx.BaseEffectKey, o, dir, range, rot, fx.DespawnDelay));

        // ── ③ 착탄 ───────────────────────────────────────────
        em.CompleteAllTrackedJobs();
        var targets = SkillCrowdControl.CollectEnemiesInCone(
            em, origin, forward, range, halfAngleDeg, casterTeam);

        // 거리 → 착탄 지연. 파도가 앞으로 뻗어 나가는 동안 순서대로 터진다.
        var order = new List<(Entity Entity, float Delay)>(targets.Count);
        foreach (var t in targets)
        {
            float d = Vector3.Distance(o, SkillCrowdControl.PositionOf(em, t));
            order.Add((t, Mathf.Clamp01(d / Mathf.Max(0.01f, range)) * WaveTime));
        }
        order.Sort((a, b) => a.Delay.CompareTo(b.Delay));

        float elapsed = 0f;
        foreach (var hit in order)
        {
            while (elapsed < hit.Delay)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!em.Exists(hit.Entity)) continue;

            Vector3 tp = SkillCrowdControl.PositionOf(em, hit.Entity);
            SkillEffectHelper.Spawn(fx.TargetEffectKey, tp, fx.DespawnDelay, rot, 1.3f);
            SkillCrowdControl.DealDamage(em, hit.Entity, damage, forward, knockMult, casterEntity);
        }
    }

    // 부채꼴을 따라 세 겹으로 번지는 핏빛 파도.
    //
    //  한 장만 크게 띄우면 "펑" 하고 끝나 범위가 얼마인지 눈에 안 남는다.
    //  앞으로 밀면서 키운 세 장이 사거리 끝까지 훑어야 넓어진 게 보인다.
    //  스케일 규칙: 원형 이펙트는 프리팹 기준 반경 3 (RareSkillEffectGenerator 주석)
    IEnumerator Wave(string key, Vector3 origin, Vector3 dir, float range,
                     Quaternion rot, float despawn)
    {
        for (int i = 0; i < 3; i++)
        {
            float t = (i + 1) / 3f;
            SkillEffectHelper.Spawn(key, origin + dir * range * t * 0.7f, despawn, rot,
                                    range / 3f * (0.5f + 0.28f * i));
            yield return new WaitForSeconds(0.05f);
        }
    }
}
