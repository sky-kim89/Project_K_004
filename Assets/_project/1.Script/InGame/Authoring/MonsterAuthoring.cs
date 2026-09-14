using Unity.Entities;

// ============================================================
//  MonsterAuthoring.cs
//  일반 적 유닛 전용 Authoring
//
//  - 장군-병사 계층 없이 독립적으로 전투하는 기본 적 유닛
//  - 추가 전용 컴포넌트 없음 (필요 시 MonsterComponent 를 추가할 것)
// ============================================================

namespace BattleGame.Units
{
    public class MonsterAuthoring : UnitAuthoring { }

    public class EnemyBaker : UnitBakerBase<MonsterAuthoring>
    {
        public override void Bake(MonsterAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            BakeCommon(authoring, entity, UnitType.Monster);
        }
    }
}
