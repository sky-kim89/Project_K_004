using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveHeavyStrike.cs
//  강타(HeavyStrike) — 사정거리 내 적을 즉시 강타.
//  이동 없음. 공격력 × DamageMultiplier 피해 + 강한 넉백.
// ============================================================

[CreateAssetMenu(fileName = "Active_HeavyStrike", menuName = "BattleGame/Actives/HeavyStrike")]
public class ActiveHeavyStrike : ActiveSkillData
{
    [Header("강타 설정")]
    [Tooltip("공격력 배율 (예: 3.0 → 공격력 × 3 데미지)")]
    public float DamageMultiplier = 3f;

    [Tooltip("넉백 배율")]
    public float KnockbackMult = 5f;

    public override void Execute(ActiveSkillContext context)
    {
        if (!context.HasTarget) return;

        var em = context.EntityManager;
        if (!em.Exists(context.TargetEntity)) return;

        Vector3 casterPos = context.CasterTransform != null
            ? context.CasterTransform.position
            : Vector3.zero;
        Vector3 targetPos = context.TargetPosition;

        SkillEffectHelper.Spawn(CasterEffectKey, casterPos, EffectDespawnDelay);
        SkillEffectHelper.Spawn(TargetEffectKey, targetPos, EffectDespawnDelay);

        em.CompleteAllTrackedJobs();
        if (em.HasBuffer<HitEventBufferElement>(context.TargetEntity))
        {
            float  damage = context.CasterStat.Final[StatType.Attack] * DamageMultiplier * EffectValue;
            float3 hitDir = CalcHitDir(casterPos, targetPos);
            em.GetBuffer<HitEventBufferElement>(context.TargetEntity).Add(new HitEventBufferElement
            {
                Damage         = damage,
                HitDirection   = hitDir * KnockbackMult,
                AttackerEntity = context.CasterEntity,
                Type           = HitType.Skill,
            });
        }
    }

    static float3 CalcHitDir(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float   mag = dir.magnitude;
        if (mag < 0.001f) return new float3(1f, 0f, 0f);
        return new float3(dir.x / mag, dir.y / mag, dir.z / mag);
    }
}
