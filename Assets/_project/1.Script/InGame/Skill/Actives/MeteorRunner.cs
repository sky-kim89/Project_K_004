using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  MeteorRunner.cs — 메테오 착탄 처리기
//
//  ■ 이펙트 타이밍
//    - BaseEffect  : 즉시 착탄 예정 위치 (낙하 예고 마커)
//    - TargetEffect: delay 후 착탄 시점 (폭발 이펙트)
// ============================================================

public class MeteorRunner : MonoBehaviour
{
    Coroutine _current;

    void OnDisable() { _current = null; }

    public void Run(
        Vector3         impactPos,
        Entity          casterEntity,
        StatComponent   casterStat,
        EntityManager   em,
        TeamType        casterTeam,
        float           damageMultiplier,
        float           aoeRadius,
        float           delay,
        float           knockbackMult,
        SkillEffectConfig fx)
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(Sequence(
            impactPos, casterEntity, casterStat, em,
            casterTeam, damageMultiplier, aoeRadius, delay, knockbackMult, fx));
    }

    /// <summary>
    /// 떨어지는 운석 본체 이펙트.
    ///
    /// 스킬 SO 의 세 키(Base=예고 원 / Target=착탄 폭발 / Caster)에는 자리가 없다.
    /// 낙하체는 메테오 계열 전부가 같은 것을 쓰므로 여기에 상수로 둔다 —
    /// SO 필드를 늘리면 스킬마다 다시 채워 넣어야 하고, 빠뜨리면 조용히 사라진다.
    /// </summary>
    const string MeteorRockKey = "FX_Meteor_Rock";

    IEnumerator Sequence(
        Vector3         impactPos,
        Entity          casterEntity,
        StatComponent   casterStat,
        EntityManager   em,
        TeamType        casterTeam,
        float           damageMultiplier,
        float           aoeRadius,
        float           delay,
        float           knockbackMult,
        SkillEffectConfig fx)
    {
        float fxScale = aoeRadius / 3f;  // FX_Meteor_Warning / FX_Meteor_Explosion 프리팹 기준 반경 3

        // ── ① 낙하 예고 — 바닥의 표적 원 (즉시) ──────────────
        //   착탄 전까지 떠 있어야 피할 자리를 판단할 수 있다.
        SkillEffectHelper.Spawn(fx.BaseEffectKey, impactPos, delay + fx.DespawnDelay, scale: fxScale);

        // ── ② 하늘에서 떨어지는 운석 본체 ────────────────────
        //  ⚠ 낙하는 착탄보다 먼저 끝나야 한다
        //    예고 원이 떠 있는 동안 실제로 뭔가가 떨어져 내려와야 착탄이
        //    '사건' 이 된다. 예전에는 표적 원 → 폭발뿐이라 갑자기 터졌다.
        //    낙하 시간을 지연보다 살짝 짧게 잡아 폭발 직전에 도달시킨다.
        //
        //  ⚠ 여기에 폭발 프리팹(TargetEffectKey)을 쓰면 안 된다
        //    한때 그렇게 만들어 놓았는데, 폭발이 하늘에서부터 계속 터지면서
        //    내려오는 그림이 됐다. 떨어지는 것은 불붙은 '덩어리' 여야 하고
        //    폭발은 땅에 닿는 순간 한 번이어야 한다. → 전용 FX_Meteor_Rock.
        float fallTime = Mathf.Min(delay * 0.85f, delay - 0.05f);
        if (fallTime > 0.05f)
            SkillEffectHelper.SpawnFalling(MeteorRockKey, impactPos, fallTime,
                                           height: 14f, scale: Mathf.Max(0.55f, fxScale * 0.6f));

        // ── ③ 착탄 대기 ───────────────────────────────────────
        yield return new WaitForSeconds(delay);

        // ── ④ 폭발 이펙트 + AoE 피해 ─────────────────────────
        SkillEffectHelper.Spawn(fx.TargetEffectKey, impactPos, fx.DespawnDelay, scale: fxScale);

        em.CompleteAllTrackedJobs();
        float3 center = new float3(impactPos.x, impactPos.y, 0f);

        var query = em.CreateEntityQuery(new EntityQueryDesc
        {
            All  = new ComponentType[] { ComponentType.ReadOnly<UnitIdentityComponent>(),
                                         ComponentType.ReadOnly<LocalTransform>() },
            None = new ComponentType[] { typeof(DeadTag) },
        });

        NativeArray<Entity>         entities   = query.ToEntityArray(Allocator.Temp);
        NativeArray<LocalTransform> transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            var id = em.GetComponentData<UnitIdentityComponent>(entities[i]);
            if (id.Team == casterTeam) continue;

            float dist = math.distance(center, new float3(transforms[i].Position.x, transforms[i].Position.y, 0f));
            if (dist > aoeRadius) continue;

            if (!em.HasBuffer<HitEventBufferElement>(entities[i])) continue;

            float  damage   = casterStat.Final[StatType.Attack] * damageMultiplier;
            float3 knockDir = dist > 0.01f
                ? math.normalizesafe(new float3(transforms[i].Position.x, transforms[i].Position.y, 0f) - center)
                : new float3(1f, 0f, 0f);

            em.GetBuffer<HitEventBufferElement>(entities[i]).Add(new HitEventBufferElement
            {
                Damage         = damage,
                HitDirection   = knockDir * knockbackMult,
                AttackerEntity = casterEntity,
                Type = BattleGame.Units.HitType.Skill,
            });
        }

        entities.Dispose();
        transforms.Dispose();
        query.Dispose();

        _current = null;
    }
}
