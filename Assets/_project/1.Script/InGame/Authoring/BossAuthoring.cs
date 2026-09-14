using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

// ============================================================
//  BossAuthoring.cs
//  보스 유닛 전용 Authoring
//
//  - 페이즈 시스템: 체력 비율에 따라 페이즈 전환
//    (BossPhaseSystem 이 BossComponent.CurrentPhase 를 갱신)
//  - 스킬 목록: 페이즈별로 다른 스킬을 사용할 수 있도록 최대 3개 슬롯
//    (스킬 발동 / 전환 로직은 BossSkillSystem 에서 구현)
// ============================================================

namespace BattleGame.Units
{
    public class BossAuthoring : UnitAuthoring
    {
        [Header("보스 — 페이즈 설정")]
        [Tooltip("총 페이즈 수 (1 ~ 3)")]
        [Range(1, 3)]
        public int   PhaseCount    = 2;

        [Tooltip("2페이즈 전환 체력 비율 (예: 0.5 = 50%)")]
        [Range(0.01f, 0.99f)]
        public float Phase2HpRatio = 0.5f;

        [Tooltip("3페이즈 전환 체력 비율 (PhaseCount < 3 이면 무시)")]
        [Range(0.01f, 0.99f)]
        public float Phase3HpRatio = 0.25f;

        [Header("보스 — 내성")]
        [Tooltip("행동불능(스턴) 내성 (0 = 없음, 1 = 완전 면역)")]
        [Range(0f, 1f)]
        public float CCResistance = 1f;

        [Tooltip("넉백 내성 (0 = 없음, 1 = 완전 면역)")]
        [Range(0f, 1f)]
        public float KnockbackResistance = 0.8f;

        [Header("보스 — AoE 기본 공격")]
        [Tooltip("AoE 공격 반경 (0이면 단일 공격)")]
        public float AoeRadius = 2.5f;

        [Tooltip("AoE 범위 피해 비율 (1.0 = 100%)")]
        [Range(0f, 1f)]
        public float AoeSplashRatio = 0.6f;

        [Tooltip("공격 시 넉백 강도")]
        public float AttackKnockbackForce = 4.0f;

        [Tooltip("넉백 지속 시간 (초)")]
        public float AttackKnockbackDuration = 0.25f;

        [Header("보스 — 스킬 목록 (페이즈별 최대 3개, 없으면 비워두기)")]
        public SkillData SkillPhase1;
        public SkillData SkillPhase2;
        public SkillData SkillPhase3;
    }

    public class BossBaker : UnitBakerBase<BossAuthoring>
    {
        public override void Bake(BossAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            BakeCommon(authoring, entity, UnitType.Boss);

            AddComponent(entity, new BossComponent
            {
                PhaseCount              = authoring.PhaseCount,
                CurrentPhase            = 1,
                Phase2HpRatio           = authoring.Phase2HpRatio,
                Phase3HpRatio           = authoring.Phase3HpRatio,
                CCResistance            = authoring.CCResistance,
                KnockbackResistance     = authoring.KnockbackResistance,
                AoeRadius               = authoring.AoeRadius,
                AoeSplashRatio          = authoring.AoeSplashRatio,
                AttackKnockbackForce    = authoring.AttackKnockbackForce,
                AttackKnockbackDuration = authoring.AttackKnockbackDuration,
            });

            // 1페이즈 스킬을 초기 액티브 스킬로 설정
            if (authoring.SkillPhase1 != null &&
                authoring.SkillPhase1.Category == SkillCategory.Active)
            {
                AddComponent(entity, new GeneralActiveSkillComponent
                {
                    SkillId           = authoring.SkillPhase1.SkillId,
                    EffectValue       = authoring.SkillPhase1.ActiveEffectValue,
                    EffectRadius      = authoring.SkillPhase1.ActiveEffectRadius,
                    EffectDuration    = authoring.SkillPhase1.ActiveEffectDuration,
                    Cooldown          = authoring.SkillPhase1.ActiveCooldown,
                    CooldownRemaining = 0f,
                });
            }
        }
    }
}
