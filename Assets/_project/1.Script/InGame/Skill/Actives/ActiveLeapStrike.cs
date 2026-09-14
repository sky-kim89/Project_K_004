using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveLeapStrike.cs — 도약 강타 (방패·전사)
//
//  전방으로 도약하여 착지 반경 내 모든 적을 강타 + 넉백한다.
//  LeapStrikeRunner 가 코루틴으로 이동 시퀀스를 처리한다.
// ============================================================

[CreateAssetMenu(fileName = "Active_LeapStrike", menuName = "BattleGame/Actives/LeapStrike")]
public class ActiveLeapStrike : ActiveSkillData
{
    [Header("도약 강타 설정")]
    [Tooltip("기본 공격력 배율")]
    public float DamageMultiplier = 2.5f;

    [Tooltip("도약 속도 (유닛/초)")]
    public float LeapSpeed = 18f;

    [Tooltip("최대 도약 거리. 시야(9유닛)보다 조금 넉넉하게 잡는다.")]
    public float MaxLeapDistance = UnitGridConstants.SightRange + 3f;

    [Tooltip("넉백 배율")]
    public float KnockbackMult = 4f;

    // ─────────────────────────────────────────────────────────

    public override void Execute(ActiveSkillContext ctx)
    {
        if (!ctx.HasTarget) return;
        if (!ctx.EntityManager.Exists(ctx.TargetEntity)) return;
        if (ctx.CasterObject == null) return;

        Vector3 targetPos = Vector3.zero;
        if (ctx.EntityManager.HasComponent<LocalTransform>(ctx.TargetEntity))
        {
            var lt = ctx.EntityManager.GetComponentData<LocalTransform>(ctx.TargetEntity);
            targetPos = new Vector3(lt.Position.x, lt.Position.y, lt.Position.z);
        }

        // 도약 거리 상한 — 시야 밖으로는 뛰지 않는다.
        // ⚠ 이 스킬은 이제 사거리 판정을 건너뛴다 (ActiveSkillId.IsDash)
        //   HasTarget 은 침투 적일 때 최대 50유닛까지 유지되므로, 자르지 않으면
        //   장수가 적 후방 한가운데에 혼자 떨어져 그대로 녹는다.
        Vector3 casterPos = ctx.CasterTransform.position;
        Vector3 toTarget  = targetPos - casterPos;
        float   leapDist  = toTarget.magnitude;
        if (leapDist > MaxLeapDistance && leapDist > 0.001f)
            targetPos = casterPos + toTarget / leapDist * MaxLeapDistance;

        // 시전자 팀: 반대 팀이 공격 대상
        var casterIdentity = ctx.EntityManager.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);

        var runner = ctx.CasterObject.GetComponent<LeapStrikeRunner>();
        if (runner == null) runner = ctx.CasterObject.AddComponent<LeapStrikeRunner>();

        runner.Run(
            casterTransform  : ctx.CasterTransform,
            targetPos        : targetPos,
            casterEntity     : ctx.CasterEntity,
            casterStat       : ctx.CasterStat,
            em               : ctx.EntityManager,
            casterTeam       : casterIdentity.Team,
            damageMultiplier : DamageMultiplier * EffectValue,
            aoeRadius        : EffectRadius > 0f ? EffectRadius : 2f,
            leapSpeed        : LeapSpeed,
            knockbackMult    : KnockbackMult,
            fx               : new SkillEffectConfig
            {
                CasterEffectKey = CasterEffectKey,
                TargetEffectKey = TargetEffectKey,
                BaseEffectKey   = BaseEffectKey,
                DespawnDelay    = EffectDespawnDelay,
            }
        );
    }
}
