using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveManaBurst.cs — 마나 폭발 (대마법사 시그니처, 2026-09-12)
//
//  남은 마나의 BurnRatio 를 태워, 탭한 자리 반경 안의 적 **전부**에게
//  태운 마나 1당 **대상 최대 체력의 2%** 피해를 준다. 보스는 절반(1%)이다.
//
//  ■ 왜 최대 체력 비례인가 (사용자 지시, 2026-09-12)
//    처음엔 "마나 1당 20 + 소환력×4" 고정 피해였다. 용사 체력은 스테이지를 따라
//    수천까지 오르는데 그 값은 거의 안 늘어서, 후반에는 **쓸모가 없어졌다.**
//    비율이면 몇 스테이지에서 쓰든 같은 무게다 — 소환사 평타(SummonerStrikeRule)와 같은 생각이다.
//
//  ■ 방어율을 지나지 않는다 (DefensePierce = 1)
//    파쇄·소환사 평타와 같은 이유 — 비율 피해가 방어율에 깎이면 방패병 용사에게만
//    유독 약해져 "2%" 라는 말이 거짓말이 된다.
//
//  ■ 보스는 반감 (사용자 지시) — 무한 보스(×10)까지 한 번에 지우지 않게.
//    엘리트는 일반과 같다.
//
//  ■ 시그니처는 원래 마나를 안 쓴다 — 이 스킬만 예외다
//    제한은 여전히 **스테이지당 횟수**다 (SummonerSkillRule). 마나는 **연료**다 —
//    많이 쥐고 있을수록 세다. 대마법사의 개성(최대 마나 10당 전군 공/체)과 한 방향을 본다.
//
//  ⚠ 태운 양은 **내림**이다 — 마나는 정수로만 움직인다 (ManaRegenRule 주석).
//  ⚠ 용사 추첨에서는 IsSummonerOnly 가 거른다 — 적이 쓰면 플레이어의 마나가 빠진다.
// ============================================================

[CreateAssetMenu(fileName = "Active_ManaBurst", menuName = "BattleGame/Actives/ManaBurst")]
public class ActiveManaBurst : ActiveSkillData
{
    [Header("마나 폭발")]
    [Tooltip("태우는 비율 — 0.5 = 남은 마나의 절반")]
    public float BurnRatio = 0.5f;

    [Tooltip("태운 마나 1당 대상 최대 체력 비율 — 0.02 = 2%")]
    public float MaxHpRatioPerMana = 0.02f;

    [Tooltip("보스에게 곱하는 배율 — 0.5 = 반감")]
    public float BossMult = 0.5f;

    [Tooltip("넉백 세기")]
    public float KnockbackMult = 4f;

    public override void Execute(ActiveSkillContext ctx)
    {
        var mana = UserDataManager.Instance.Get<SummonManaData>();

        float burned = Mathf.Floor(mana.Current * BurnRatio);

        // 태울 것이 없으면 아무 일도 없다 — 횟수만 쓰였다. 버그가 아니라 규칙이다
        //   (잔량이 1 이면 절반의 내림이 0 이다).
        if (burned <= 0f) return;

        mana.Spend(burned);

        float ratio = burned * MaxHpRatioPerMana * EffectValue;

        EntityManager em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        Vector3  center = ctx.TargetPosition;
        float    radius = EffectRadius > 0f ? EffectRadius : 4f;
        TeamType team   = SkillCrowdControl.TeamOf(em, ctx.CasterEntity);

        // FX_Meteor_Explosion 프리팹의 기준 반경이 3 이다 (MeteorRunner 와 같은 환산)
        SkillEffectHelper.Spawn(TargetEffectKey, center, EffectDespawnDelay, scale: radius / 3f);

        List<Entity> enemies = SkillCrowdControl.CollectEnemiesInRadius(
            em, new float3(center.x, center.y, 0f), radius, team);

        foreach (Entity enemy in enemies)
        {
            if (!em.HasBuffer<HitEventBufferElement>(enemy)) continue;

            float maxHp = em.GetComponentData<StatComponent>(enemy).Final[StatType.MaxHp];
            float mult  = em.HasComponent<BossComponent>(enemy) ? BossMult : 1f;

            Vector3 p   = SkillCrowdControl.PositionOf(em, enemy);
            float3  dir = math.normalizesafe(new float3(p.x - center.x, p.y - center.y, 0f),
                                             new float3(1f, 0f, 0f));

            // ⚠ SkillCrowdControl.DealDamage 를 쓰지 않는다 — 관통을 넘길 자리가 없다
            em.GetBuffer<HitEventBufferElement>(enemy).Add(new HitEventBufferElement
            {
                Damage         = maxHp * ratio * mult,
                HitDirection   = dir * KnockbackMult,
                AttackerEntity = ctx.CasterEntity,
                Type           = HitType.Skill,
                DefensePierce  = 1f,   // 방어율 무시 — 파쇄·소환사 평타와 같은 규칙
            });
        }
    }
}
