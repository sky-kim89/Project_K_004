using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  ActiveBind.cs — 속박 (공통)
//
//  현재 타겟을 EffectDuration 초 동안 완전 행동불능(스턴) 상태로 만들고
//  초당 EffectValue 데미지의 도트 피해를 함께 가한다.
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Active_Bind", menuName = "BattleGame/Actives/Bind")]
public class ActiveBind : ActiveSkillData
{
    public override void Execute(ActiveSkillContext ctx)
    {
        if (!ctx.HasTarget) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        Entity target = ctx.TargetEntity;
        if (!em.Exists(target)) return;

        float duration = EffectDuration > 0f ? EffectDuration : 3f;

        // 피격 대상 이펙트 (속박 연출)
        if (em.HasComponent<Unity.Transforms.LocalTransform>(target))
        {
            var tf = em.GetComponentData<Unity.Transforms.LocalTransform>(target);
            SkillEffectHelper.Spawn(TargetEffectKey,
                new UnityEngine.Vector3(tf.Position.x, tf.Position.y, tf.Position.z),
                EffectDespawnDelay);
        }

        // ── 스턴 적용 ─────────────────────────────────────────
        if (em.HasComponent<HitReactionComponent>(target))
        {
            var reaction = em.GetComponentData<HitReactionComponent>(target);
            reaction.IsStunned     = true;
            reaction.StunDuration  = duration;
            reaction.StunTimer     = duration;
            em.SetComponentData(target, reaction);
        }

        if (em.HasComponent<UnitStateComponent>(target))
        {
            var state = em.GetComponentData<UnitStateComponent>(target);
            state.Previous   = state.Current;
            state.Current    = UnitState.Hit;
            state.StateTimer = 0f;
            em.SetComponentData(target, state);
        }

        // ── 도트 피해 ─────────────────────────────────────────
        if (EffectValue > 0f && em.HasBuffer<StatusEffectBufferElement>(target))
        {
            em.GetBuffer<StatusEffectBufferElement>(target).Add(new StatusEffectBufferElement
            {
                Stat         = StatType.MaxHp,  // Dot 모드에서는 Stat 필드 무시됨
                Delta        = ctx.CasterStat.Final[StatType.Attack] * EffectValue,
                Mode         = EffectMode.Dot,
                Duration     = duration,
                Remaining    = duration,
                // 도트로 잡은 적도 이 장군의 전과로 잡히게 한다
                SourceEntity = ctx.CasterEntity,
            });
        }
    }
}
