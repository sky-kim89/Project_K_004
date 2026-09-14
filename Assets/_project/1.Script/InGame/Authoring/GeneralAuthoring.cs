using Unity.Entities;
using UnityEngine;

// ============================================================
//  GeneralAuthoring.cs
//  장군 유닛 전용 Authoring
//
//  - 병사 스폰 설정: SoldierPrefab / SoldierCount / StatScaleRatio
//  - 패시브 스킬: 소속 병사에게 스탯 버프 오라
//  - 액티브 스킬: 쿨다운 후 발동, 스킬 실행기가 직접 제어
// ============================================================

namespace BattleGame.Units
{
    public class GeneralAuthoring : UnitAuthoring
    {
        [Header("장군 — 지휘 설정")]
        [Tooltip("스폰할 병사 프리팹 (SoldierAuthoring 이 붙은 프리팹)")]
        public GameObject SoldierPrefab;

        [Tooltip("스폰할 병사 수")]
        public int        SoldierCount   = 10;

        [Tooltip("지휘력 0 일 때의 병사 스탯 비율. 최종 비율 = 이 값 + 지휘력 × 0.01 (상한 없음)")]
        [Range(0.1f, 1f)]
        public float      StatScaleRatio = 0.2f;

        [Tooltip("지휘 반경 — 이 범위 내 소속 병사에게 패시브 버프 적용")]
        public float      CommandRadius  = 15f;

        [Header("패시브 스킬 (없으면 비워두기)")]
        public SkillData  PassiveSkill;

        [Header("액티브 스킬 (없으면 비워두기)")]
        public SkillData  ActiveSkill;
    }

    public class GeneralBaker : UnitBakerBase<GeneralAuthoring>
    {
        public override void Bake(GeneralAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            BakeCommon(authoring, entity, UnitType.General);

            AddComponent(entity, new GeneralComponent
            {
                CommandRadius = authoring.CommandRadius,
            });

            // 스켈레톤 소환용 병사 사망 위치 버퍼
            AddBuffer<DeadSoldierSpawnPointBuffer>(entity);

            // 병사 스폰 요청 (SoldierSpawnSystem 이 처리)
            if (authoring.SoldierPrefab != null)
            {
                AddComponent(entity, new SpawnSoldiersRequest
                {
                    SoldierPrefab  = GetEntity(authoring.SoldierPrefab, TransformUsageFlags.Dynamic),
                    Count          = authoring.SoldierCount,
                    StatScaleRatio = authoring.StatScaleRatio,
                });
            }

            // 패시브 스킬
            if (authoring.PassiveSkill != null &&
                authoring.PassiveSkill.Category == SkillCategory.Passive)
            {
                AddComponent(entity, new GeneralPassiveSkillComponent
                {
                    BuffStat   = authoring.PassiveSkill.PassiveBuffStat,
                    BuffValue  = authoring.PassiveSkill.PassiveBuffValue,
                    BuffMode   = EffectMode.Add,   // 패시브는 절대값 증가 (필요 시 SO 에 필드 추가)
                    AuraRadius = authoring.PassiveSkill.PassiveAuraRadius,
                });
            }

            // 액티브 스킬
            if (authoring.ActiveSkill != null &&
                authoring.ActiveSkill.Category == SkillCategory.Active)
            {
                AddComponent(entity, new GeneralActiveSkillComponent
                {
                    SkillId           = authoring.ActiveSkill.SkillId,
                    EffectValue       = authoring.ActiveSkill.ActiveEffectValue,
                    EffectRadius      = authoring.ActiveSkill.ActiveEffectRadius,
                    EffectDuration    = authoring.ActiveSkill.ActiveEffectDuration,
                    Cooldown          = authoring.ActiveSkill.ActiveCooldown,
                    CooldownRemaining = 0f,
                });
            }
        }
    }
}
