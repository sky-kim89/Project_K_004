using BattleGame.Units;

// ============================================================
//  ActiveFlameAura.cs — 화염 오라 (화염 멧돼지 고유)
//
//  ■ 몸에 두르는 불이다 — 깔아 두는 장판이 아니다
//    독성 지대·블리자드는 **자리를 고르는 것**이 값이라 한자리에 고정된다.
//    이건 반대다 — 멧돼지가 있는 곳이 곧 타는 곳이다. 고정하면 돌진한 뒤
//    아무도 없는 자리에서 불만 타고, 정작 부딪힌 곳은 멀쩡하다.
//    (SkillZoneRunner.ZoneConfig.FollowCaster)
//
//  ■ 왜 화염 멧돼지인가
//    이 종족의 개성이 이미 '때린 자리마다 불이 붙는다'(BurnOnAttack)다.
//    한 마리씩 물어야 붙던 불이 **몸 주위로 번지는 것**이 그 계보의 다음 칸이다.
//    표식도 역병(도트 축)이라 축이 어긋나지 않는다.
//
//    ⚠ 뿌리 멧돼지의 돌진(HeavyStrike)을 물려 쓰던 자리를 대신한다
//      전에는 둘이 같은 스킬이라 진화해도 하는 짓이 똑같았다. 진화체는
//      **다른 것을 해야** 진화로 읽힌다.
//
//  ■ ⚠ 상시 오라가 아니라 쿨다운 스킬이다
//    한 장에 넷이 나오는 종족이다(Count 4). 상시로 두면 넷이 각자 틱마다
//    전체 유닛 쿼리를 돌린다 — SkillZoneRunner.ApplyTick 은 매 틱
//    CompleteAllTrackedJobs() 를 부르는 동기화 지점이라 그대로 프레임을 먹는다.
//    쿨다운 12초 · 지속 4초면 평균 동시 장판이 4 × 4/12 ≈ 1.3 개로,
//    지금 용사 법사가 까는 것과 같은 수준에 머문다.
//    ⚠ 지속을 늘리거나 쿨다운을 줄이려면 그 곱(마릿수 × 지속 / 쿨다운)을 먼저 볼 것.
//
//  ■ 피해는 틱마다 직접 들어간다 (HitType.Skill)
//    도트 상태효과로 걸지 않는다 — SkillZoneRunner 의 디버프 경로는
//    SourceEntity 와 DotKind 를 싣지 않아서, 그대로 쓰면 ① 도트로 잡은 적이
//    아무의 전과도 아니게 되고 ② 불인데 몸이 초록으로 물든다(기본값이 독).
//    불타는 그림은 이펙트(FX_Flame_Zone)가 맡는다.
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Active_FlameAura", menuName = "BattleGame/Actives/FlameAura")]
public class ActiveFlameAura : ActiveSkillZoneBase
{
    protected override float DefaultRadius   => 2.2f;
    protected override float DefaultDuration => 4f;

    /// <summary>몸에 두르는 불이라 시전자를 따라간다.</summary>
    protected override bool FollowsCaster => true;
}
