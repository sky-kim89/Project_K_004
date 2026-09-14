using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveDeathSentence.cs — 사형 선고 (공통 · 희귀)
//
//  넓은 범위의 적을 **즉시** 선고·처형한다.
//    ① 범위에 인장이 찍히고 화면이 크게 흔들린다
//    ② 그 자리에서 판정 — 체력 비율이 기준 이하인 적은 **즉사**
//    ③ 기준을 넘긴 적은 큰 피해만 받는다
//    ④ 처형된 적 자리에서 해골이 하나씩 떠오른다
//    ⑤ 처형한 수만큼 시전자 공격력이 영구(이번 전투) 누적
//
//  ⚠ 예고(낙인 2초) 를 없앴다
//    낙인은 찍힌 자리에 붙박이는데 적은 계속 걸어가서 누가 걸렸는지 안 보였고,
//    정작 처형 순간에도 발동했는지 알기 어려웠다. 지금은 즉발이고,
//    결과는 해골 개수와 화면 흔들림으로 읽는다.
//
//  ⚠ 즉사도 피해 파이프라인을 거친다
//    DeadTag 를 직접 붙이면 킬 수·전투 통계·처치 트리거가 전부 누락된다.
//    현재 체력을 뚫을 만큼의 피해를 넣어 UnitHitSystem 이 죽이게 한다.
//
//  EffectRadius   : 선고 반경
//  EffectDuration : 인장·낙인이 화면에 남는 시간 (연출 전용 — 처형은 즉발이다)
// ============================================================

[CreateAssetMenu(fileName = "Active_DeathSentence", menuName = "BattleGame/Actives/DeathSentence")]
public class ActiveDeathSentence : ActiveSkillData
{
    [Header("사형 선고 설정")]
    [Tooltip("즉사 기준 체력 비율. 0.35 = 체력 35% 이하면 처형.")]
    [Range(0f, 1f)]
    public float ExecuteHpRatio = 0.35f;

    [Tooltip("처형을 피한 적에게 주는 공격력 배율")]
    public float DamageMultiplier = 4f;

    [Tooltip("처형 1명당 시전자 공격력 증가량 (전투 내 영구 누적)")]
    public float AttackPerExecute = 6f;

    [Tooltip("보스에게도 즉사를 적용할지. 꺼두면 보스는 피해만 받는다.")]
    public bool ExecuteBosses = false;

    [Tooltip("넉백 배율")]
    public float KnockbackMult = 3f;

    [Tooltip("처형된 적 자리에서 떠오르는 해골 이펙트 풀 키")]
    public string SkullEffectKey = "FX_Death_Skull";

    public override void Execute(ActiveSkillContext ctx)
    {
        if (ctx.CasterObject == null) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        Vector3 center = ctx.HasTarget
            ? ctx.TargetPosition
            : ctx.CasterTransform.position + Vector3.right * 4f;

        var identity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);

        var runner = ctx.CasterObject.GetComponent<DeathSentenceRunner>();
        if (runner == null) runner = ctx.CasterObject.AddComponent<DeathSentenceRunner>();

        runner.Run(
            em            : em,
            casterEntity  : ctx.CasterEntity,
            casterTeam    : identity.Team,
            center        : new float3(center.x, center.y, 0f),
            radius        : EffectRadius   > 0f ? EffectRadius   : 6f,
            executeRatio  : ExecuteHpRatio,
            damage        : ctx.CasterStat.Final[StatType.Attack] * DamageMultiplier * EffectValue,
            attackPerKill : AttackPerExecute,
            executeBosses : ExecuteBosses,
            knockMult     : KnockbackMult,
            skullEffectKey: SkullEffectKey,
            sealLifetime  : EffectDuration > 0f ? EffectDuration : 0.8f,
            fx            : new SkillEffectConfig
            {
                CasterEffectKey = CasterEffectKey,
                TargetEffectKey = TargetEffectKey,
                BaseEffectKey   = BaseEffectKey,
                DespawnDelay    = EffectDespawnDelay,
            });
    }
}
