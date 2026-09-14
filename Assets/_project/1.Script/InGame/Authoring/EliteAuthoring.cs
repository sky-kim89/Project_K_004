using Unity.Entities;

// ============================================================
//  EliteAuthoring.cs
//  엘리트 유닛 전용 Authoring
//
//  - 일반 적보다 강화된 독립 유닛
//  - 액티브 스킬 보유 가능 (GeneralActiveSkillComponent 공유)
// ============================================================

namespace BattleGame.Units
{
    public class EliteAuthoring : UnitAuthoring
    {
        [UnityEngine.Header("엘리트 — 스킬 설정 (없으면 비워두기)")]
        public SkillData ActiveSkill;
    }

    public class EliteBaker : UnitBakerBase<EliteAuthoring>
    {
        public override void Bake(EliteAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            BakeCommon(authoring, entity, UnitType.Elite);

            bool hasSkill = authoring.ActiveSkill != null &&
                            authoring.ActiveSkill.Category == SkillCategory.Active;

            AddComponent(entity, new EliteComponent { HasSkill = hasSkill, KnockbackResistance = 0.5f });

            if (hasSkill)
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
