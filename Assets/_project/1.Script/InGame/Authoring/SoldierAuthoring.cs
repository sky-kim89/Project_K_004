using Unity.Entities;

// ============================================================
//  SoldierAuthoring.cs
//  병사 유닛 전용 Authoring
//
//  - 장군이 스폰 시 GeneralEntity / StatScaleRatio 를 주입하므로
//    Inspector 의 BaseStats 는 폴백(fallback) 값이다.
//  - 실제 인게임 스탯은 SoldierSpawnSystem 이 장군 스탯 × 비율로 덮어씌운다.
// ============================================================

namespace BattleGame.Units
{
    public class SoldierAuthoring : UnitAuthoring { }

    public class SoldierBaker : UnitBakerBase<SoldierAuthoring>
    {
        public override void Bake(SoldierAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            BakeCommon(authoring, entity, UnitType.Soldier);

            // GeneralEntity / StatScaleRatio 는 SoldierSpawnSystem 이 채워줌
            AddComponent(entity, new SoldierComponent
            {
                GeneralEntity  = Entity.Null,
                StatScaleRatio = 0f,
                IsInitialized  = false,
            });
        }
    }
}
