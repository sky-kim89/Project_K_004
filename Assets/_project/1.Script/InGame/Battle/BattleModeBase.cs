// ============================================================
//  BattleModeBase.cs
//  배틀 모드 베이스 — BattleManager 가 이 클래스를 통해 모드와 통신한다.
//
//  ⚠ 원작의 웨이브·아군 편성·클리어 보상 훅은 걷어냈다
//    이 게임은 적 편성을 HeroDeployment 가 스테이지 번호로 만들고,
//    아군은 플레이어가 소환하고, 보상은 RunBootstrap 이 카드 3택으로 준다.
// ============================================================

public abstract class BattleModeBase
{
    /// <summary>이 모드의 BattleMode 값. BattleContext.Mode 설정에 사용된다.</summary>
    public abstract BattleMode Mode { get; }

    /// <summary>
    /// 이 모드가 세우는 스테이지 번호. BattleContext.StageNumber 의 출처다.
    ///
    /// ⚠ 여기가 없으면 화면의 스테이지 표시가 영원히 1 에 멈춘다
    ///   BattleManager.PrepareStage 는 매 스테이지 BattleContext 를 새로 만든다.
    ///   새 컨텍스트의 StageNumber 기본값은 1 이라, 모드가 들고 있는 번호를
    ///   옮겨 주지 않으면 TopBarUI 는 "스테이지 1" 만 그린다.
    /// </summary>
    public virtual int StageNumber => 1;

    /// <summary>배틀 승리 시 호출.</summary>
    public virtual void OnBattleVictory() { }

    /// <summary>배틀 패배 시 호출.</summary>
    public virtual void OnBattleDefeat() { }
}
