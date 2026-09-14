using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveSlimeMend.cs — 치유 점액 (힐 슬라임 고유)
//
//  시전자 주변(EffectRadius)의 **다친 몬스터 아군**을 체력 비율이 낮은 순으로
//  최대 MaxTargets 마리까지, 각자 최대 체력 × EffectValue 만큼 회복시킨다.
//
//  ■ 왜 새로 만들었나 (사용자 지시, 2026-09-11)
//    힐 슬라임의 회복이 '죽을 때 한 번' 뿐이라 체감이 없었고, 스킬이 없어
//    술법 시너지(쿨감)도 받지 못했다. 원작 치유 두 종(치유 오라·집중 치유)은
//    GeneralComponent 를 쿼리해서 몬스터에게는 아무 일도 하지 않는다.
//
//  ■ 받는 쪽 최대 체력 비례다
//    물량(슬라임)과 거체(트롤)가 섞인 줄에서 고르게 값을 하려면 비율이어야 한다.
//    ⚠ 사망 회복(HealOnDeath)은 **주는 쪽** 비례다 — 둘은 규칙이 다르다 (합치지 말 것).
//
//  ■ 출처는 시전자다 — 통계의 '치유' 가 힐 슬라임 카드에 붙는다 (UnitHealSystem)
//  ■ 다친 아군이 없으면 아무 일도 없다 — 쿨다운은 돈다 (IsSupport 는 조건 없이 쏜다)
// ============================================================

[CreateAssetMenu(fileName = "Active_SlimeMend", menuName = "BattleGame/Actives/SlimeMend")]
public class ActiveSlimeMend : ActiveSkillData
{
    /// <summary>한 번에 치유하는 최대 마릿수. 물량 종족이라 전원을 채우면 너무 세다.</summary>
    const int MaxTargets = 6;

    static readonly List<(Entity entity, float ratio, float maxHp)> _wounded = new(32);

    public override void Execute(ActiveSkillContext ctx)
    {
        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        Vector3 at    = SkillCrowdControl.PositionOf(em, ctx.CasterEntity);
        float   radSq = EffectRadius * EffectRadius;

        _wounded.Clear();

        foreach (Entity ally in SkillCrowdControl.CollectAllies(em, Faction.Monster))
        {
            if ((SkillCrowdControl.PositionOf(em, ally) - at).sqrMagnitude > radSq) continue;
            if (!em.HasComponent<HealthComponent>(ally) || !em.HasComponent<StatComponent>(ally)) continue;

            float maxHp = em.GetComponentData<StatComponent>(ally).Final[StatType.MaxHp];
            float hp    = em.GetComponentData<HealthComponent>(ally).CurrentHp;
            if (maxHp <= 0f || hp >= maxHp - 0.01f) continue;

            _wounded.Add((ally, hp / maxHp, maxHp));
        }

        if (_wounded.Count == 0) return;

        // 가장 위태로운 쪽부터
        _wounded.Sort((a, b) => a.ratio.CompareTo(b.ratio));

        int n = Mathf.Min(MaxTargets, _wounded.Count);
        for (int i = 0; i < n; i++)
        {
            var w = _wounded[i];
            SkillCrowdControl.Heal(em, w.entity, w.maxHp * EffectValue, ctx.CasterEntity);

            if (!string.IsNullOrEmpty(TargetEffectKey))
                SkillEffectHelper.Spawn(TargetEffectKey, SkillCrowdControl.BodyCenterOf(em, w.entity),
                                        EffectDespawnDelay);
        }

        if (!string.IsNullOrEmpty(CasterEffectKey))
            SkillEffectHelper.Spawn(CasterEffectKey, at, EffectDespawnDelay);
    }
}
