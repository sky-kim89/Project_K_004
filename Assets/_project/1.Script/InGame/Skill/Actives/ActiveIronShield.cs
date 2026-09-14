using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  ActiveIronShield.cs — 철벽 방어 (방패)
//
//  EffectDuration 초 동안:
//    - 시전자의 방어율 +EffectValue (예: 0.3 → +30%)
//    - 시전자에게 도발(TauntTag) 부여 — 적이 우선 타겟으로 삼음
//
//  StatType.Defense 는 0~1 범위, UnitStatusEffectSystem 에서 DefenseMax 로 클램프됨.
//  도발은 TauntTimerSystem 이 매 프레임 차감해 만료 시 자동 해제.
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Active_IronShield", menuName = "BattleGame/Actives/IronShield")]
public class ActiveIronShield : ActiveSkillData
{
    public override void Execute(ActiveSkillContext ctx)
    {
        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        if (!em.HasBuffer<StatusEffectBufferElement>(ctx.CasterEntity)) return;

        float duration = EffectDuration > 0f ? EffectDuration : 8f;

        if (ctx.CasterTransform != null)
            SkillEffectHelper.Spawn(CasterEffectKey, ctx.CasterTransform.position, EffectDespawnDelay);

        // 방어율 버프
        em.GetBuffer<StatusEffectBufferElement>(ctx.CasterEntity).Add(new StatusEffectBufferElement
        {
            Stat       = StatType.Defense,
            Delta      = EffectValue,
            Mode       = EffectMode.Add,
            Duration   = duration,
            Remaining  = duration,
            SourceType = BuffSourceType.ActiveSkill,
            SourceId   = (int)SkillId,
        });

        // 도발 — 이미 도발 중이면 시간 갱신, 아니면 새로 추가
        if (em.HasComponent<TauntTag>(ctx.CasterEntity))
            em.SetComponentData(ctx.CasterEntity, new TauntTag { Remaining = duration });
        else
            em.AddComponentData(ctx.CasterEntity, new TauntTag { Remaining = duration });
    }
}
