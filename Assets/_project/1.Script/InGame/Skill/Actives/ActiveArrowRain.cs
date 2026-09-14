// ============================================================
//  ActiveArrowRain.cs — 화살 비 (궁수)
//
//  디버프 없이 순수 피해만 가하는 고밀도 화살 비.
//  존 로직은 ActiveSkillZoneBase → SkillZoneRunner 가 처리.
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Active_ArrowRain", menuName = "BattleGame/Actives/ArrowRain")]
public class ActiveArrowRain : ActiveSkillZoneBase
{
    protected override float DefaultRadius   => 2f;
    protected override float DefaultDuration => 5f;

    // 디버프 없음 — ConfigureDebuffs 오버라이드 불필요
}
