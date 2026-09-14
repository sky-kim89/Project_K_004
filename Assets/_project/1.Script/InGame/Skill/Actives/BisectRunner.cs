using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  BisectRunner.cs
//  일도양단(Bisect)의 3단 시퀀스를 돌리는 MonoBehaviour.
//
//    ① 응축 : 시전자에게 CasterEffect (검광이 모인다) + 시전자 정지
//    ② 경직 : 참격선 위 적을 스턴 + 그 자리에서 떨게 만들고, 참격 띠를 예고로 깐다
//    ③ 참격 : 띠를 실제 크기로 다시 깔고 전원 동시 피격
//
//  ⚠ 진행 중인 시전을 끊지 않는다 (연속 시전·중복 시전 대응)
//    예전엔 새 시전이 들어오면 StopCoroutine 으로 이전 것을 끊었다.
//    그래서 "연속 시전" 특성이 붙으면 원본이 취소되고 에코만 나갔다.
//    지금은 두 번 시전하면 참격도 두 번 나간다.
//
//  ⚠ 떨림은 EntityLink.SyncPosition 을 꺼야 보인다
//    LateUpdate 가 매 프레임 ECS 위치로 transform 을 덮어쓰기 때문이다.
//    되돌리는 책임은 **떨림 코루틴 자신**에게 있다 — 시퀀스가 공유 목록을 비우면
//    동시에 돌던 다른 시퀀스가 붙잡고 있던 적까지 놓아버린다.
//    _held 는 오브젝트가 꺼질 때의 비상 복구용으로만 남긴다
//    (안 되돌리면 그 유닛이 제자리에 붙는다).
// ============================================================

public class BisectRunner : MonoBehaviour
{
    readonly HashSet<EntityLink> _held = new();

    void OnDisable() => ReleaseAll();

    public void Run(EntityManager em, Entity casterEntity, TeamType casterTeam,
                    float3 origin, float3 forward, float length, float width,
                    float damage, float chargeTime, float trembleTime, float knockMult,
                    SkillEffectConfig fx)
    {
        StartCoroutine(Sequence(em, casterEntity, casterTeam, origin, forward,
                                length, width, damage, chargeTime, trembleTime,
                                knockMult, fx));
    }

    IEnumerator Sequence(EntityManager em, Entity casterEntity, TeamType casterTeam,
                         float3 origin, float3 forward, float length, float width,
                         float damage, float chargeTime, float trembleTime, float knockMult,
                         SkillEffectConfig fx)
    {
        // ── ① 검광 응축 ──────────────────────────────────────
        var casterPos = new Vector3(origin.x, origin.y, 0f);
        float slashAngle = math.degrees(math.atan2(forward.y, forward.x));

        SkillEffectHelper.Spawn(fx.CasterEffectKey, casterPos, fx.DespawnDelay,
                                Quaternion.Euler(0f, 0f, slashAngle));

        // 시전 내내 시전자는 그 자리에 선다 — 납도 자세로 버티는 장면이다.
        // (안 묶으면 베는 순간 시전자가 이미 딴 데 가 있어 참격선과 따로 논다)
        SkillCrowdControl.AddEffect(em, casterEntity, StatType.MoveSpeed, 0f,
                                    EffectMode.Multiply, chargeTime + trembleTime,
                                    ActiveSkillId.Bisect);

        if (chargeTime > 0f) yield return new WaitForSeconds(chargeTime);

        // ── ② 참격선 위의 적을 붙잡는다 ──────────────────────
        em.CompleteAllTrackedJobs();
        var targets = SkillCrowdControl.CollectEnemiesInLine(
            em, origin, forward, length, width, casterTeam);

        foreach (var t in targets)
        {
            // 보스는 CC 내성으로 스턴이 씹힌다 — 의도된 동작이다
            SkillCrowdControl.Stun(em, t, trembleTime);

            // 발도 표식 — 누가 참격선에 걸렸는지 베기 전에 보여 준다
            SkillEffectHelper.Spawn(fx.CasterEffectKey, SkillCrowdControl.PositionOf(em, t),
                                    trembleTime + 0.3f, Quaternion.Euler(0f, 0f, slashAngle), 0.45f);

            var go = SkillCrowdControl.ObjectOf(em, t);
            if (go != null && go.TryGetComponent<EntityLink>(out var link) && link.SyncPosition)
            {
                link.SyncPosition = false;
                _held.Add(link);
                StartCoroutine(Tremble(link, trembleTime));
            }
        }

        // 참격 띠 예고 — 어디까지·얼마나 넓게 베이는지 경직 동안 보여 준다
        Vector3 mid = new Vector3(origin.x + forward.x * length * 0.5f,
                                  origin.y + forward.y * length * 0.5f, 0f);
        SpawnBand(fx.BaseEffectKey, mid, slashAngle, length, width,
                  trembleTime + 0.2f, 0.45f);

        if (trembleTime > 0f) yield return new WaitForSeconds(trembleTime);

        // ── ③ 참격 ───────────────────────────────────────────
        SpawnBand(fx.BaseEffectKey, mid, slashAngle, length, width, fx.DespawnDelay, 1f);

        em.CompleteAllTrackedJobs();
        foreach (var t in targets)
        {
            if (!em.Exists(t)) continue;

            Vector3 tp = SkillCrowdControl.PositionOf(em, t);
            SkillEffectHelper.Spawn(fx.TargetEffectKey, tp, fx.DespawnDelay,
                                    Quaternion.Euler(0f, 0f, slashAngle));

            SkillCrowdControl.DealDamage(em, t, damage, forward, knockMult, casterEntity);
        }
    }

    // 참격 띠를 길이·폭에 맞춰 눕힌다.
    //
    //  프리팹 기준: 길이 6 · 폭 1 (FX_Bisect_Slash)
    //  가로세로를 따로 늘려야 "폭이 이만큼"이 눈에 보인다 —
    //  균등 배율로 키우면 길어질수록 띠도 같이 굵어져 실제 판정 폭과 어긋난다.
    static void SpawnBand(string key, Vector3 center, float angle,
                          float length, float width, float despawn, float intensity)
    {
        var go = SkillEffectHelper.Spawn(key, center, despawn, Quaternion.Euler(0f, 0f, angle));
        if (go == null) return;

        go.transform.localScale = new Vector3(length / 6f, width * intensity, 1f);
    }

    // 제자리에서 잘게 떤다 — 베이기 직전의 정지 화면.
    // 위치 동기화 복구까지 여기서 책임진다 (동시 시전 간섭 방지).
    IEnumerator Tremble(EntityLink link, float duration)
    {
        Transform t      = link.transform;
        Vector3   origin = t.position;
        float     elapsed = 0f;

        while (elapsed < duration && link != null && t != null)
        {
            elapsed += Time.deltaTime;

            // 뒤로 갈수록 크게 떨린다 (베이기 직전의 긴장)
            float amp = Mathf.Lerp(0.06f, 0.26f, elapsed / Mathf.Max(0.01f, duration));
            // Unity.Mathematics.Random 과 이름이 겹친다 — 반드시 전체 이름으로 쓴다
            t.position = origin + new Vector3(UnityEngine.Random.Range(-amp, amp),
                                              UnityEngine.Random.Range(-amp, amp), 0f);
            yield return null;
        }

        if (t != null) t.position = origin;
        if (link != null)
        {
            link.SyncPosition = true;
            _held.Remove(link);
        }
    }

    void ReleaseAll()
    {
        foreach (var link in _held)
            if (link != null) link.SyncPosition = true;
        _held.Clear();
    }
}
