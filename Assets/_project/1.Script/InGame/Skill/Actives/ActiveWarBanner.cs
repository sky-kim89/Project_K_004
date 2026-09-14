using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveWarBanner.cs — 군기 강림 (공통 · 희귀)
//
//  시전자 자리에 군기를 세워 **주변 아군**을 강화한다.
//    · 반경 안 아군 전원 공격력·공격속도 대폭 증가 (지속 시간 동안)
//    · 버프는 발동 순간에 범위 안에 있던 대상에게만 걸린다
//
//  ⚠ 부술 수 있는 오브젝트가 아니다
//    처음엔 군기를 실제 유닛으로 세우려 했지만, 그러면 적 타겟팅·생존 카운트·
//    사망 처리에 전부 예외가 생긴다. 지금은 시전자 주변 즉발 버프다.
//
//  ⚠ 군기는 세우는 게 아니라 들고 다니는 것이다
//    예전엔 시전 위치에 8초짜리 이펙트를 박아 뒀는데, 장군은 계속 걸어가므로
//    금세 아무도 없는 자리에서 깃발만 나부꼈다. 지금은 WarBannerRunner 가
//    시전자에게 붙여 따라다니게 한다.
//
//  EffectRadius   : 버프 반경
//  EffectDuration : 버프 지속 시간
// ============================================================

[CreateAssetMenu(fileName = "Active_WarBanner", menuName = "BattleGame/Actives/WarBanner")]
public class ActiveWarBanner : ActiveSkillData
{
    [Header("군기 강림 설정")]
    [Tooltip("공격력 증가 배율 (1.4 = +40%)")]
    public float AttackMultiplier = 1.4f;

    [Tooltip("공격속도 증가 배율 (1.4 = +40%)")]
    public float AttackSpeedMultiplier = 1.4f;

    [Tooltip("이동속도 증가 배율 (1.15 = +15%)")]
    public float MoveSpeedMultiplier = 1.15f;

    [Tooltip("깃발이 펄럭이는 시간 (초). 이후 깃발만 사라지고 반경 표시는 남는다.")]
    public float FlashTime = 1f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (ctx.CasterObject == null) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        if (!em.HasComponent<LocalTransform>(ctx.CasterEntity)) return;

        var    casterTf  = em.GetComponentData<LocalTransform>(ctx.CasterEntity);
        float3 casterPos = new float3(casterTf.Position.x, casterTf.Position.y, 0f);
        var    center    = new Vector3(casterPos.x, casterPos.y, 0f);

        float radius   = EffectRadius   > 0f ? EffectRadius   : 6f;
        float duration = EffectDuration > 0f ? EffectDuration : 8f;

        // 군기 — 시전자를 따라다니며 버프 반경을 그린다.
        // 연속 시전으로 두 번 들어와도 Runner 가 하나로 합친다.
        var runner = ctx.CasterObject.GetComponent<WarBannerRunner>();
        if (runner == null) runner = ctx.CasterObject.AddComponent<WarBannerRunner>();

        runner.Run(
            casterTf : ctx.CasterTransform,
            radius   : radius,
            duration : duration,
            flashTime: FlashTime,
            fx       : new SkillEffectConfig
            {
                BaseEffectKey = BaseEffectKey,
                DespawnDelay  = EffectDespawnDelay,
            });

        SkillEffectHelper.Spawn(CasterEffectKey, center, EffectDespawnDelay);

        var identity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);

        // 반경 안 아군만 — CollectAllies 는 전장 전체라 거리로 다시 거른다
        float radiusSq = radius * radius;
        int   buffed   = 0;

        foreach (var a in SkillCrowdControl.CollectAllies(em, identity.Team))
        {
            if (!em.Exists(a)) continue;

            Vector3 p = SkillCrowdControl.PositionOf(em, a);
            if (math.lengthsq(new float3(p.x - casterPos.x, p.y - casterPos.y, 0f)) > radiusSq) continue;

            SkillCrowdControl.AddEffect(em, a, StatType.Attack, AttackMultiplier,
                                        EffectMode.Multiply, duration, ActiveSkillId.WarBanner);
            SkillCrowdControl.AddEffect(em, a, StatType.AttackSpeed, AttackSpeedMultiplier,
                                        EffectMode.Multiply, duration, ActiveSkillId.WarBanner);
            SkillCrowdControl.AddEffect(em, a, StatType.MoveSpeed, MoveSpeedMultiplier,
                                        EffectMode.Multiply, duration, ActiveSkillId.WarBanner);

            SkillEffectHelper.Spawn(TargetEffectKey, p, EffectDespawnDelay);
            buffed++;
        }

        if (buffed == 0)
            Debug.LogWarning("[WarBanner] 반경 안에 아군이 없다 — 반경이 너무 작지 않은지 확인할 것.");
    }
}
