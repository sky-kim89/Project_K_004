// ============================================================
//  NormalMode.cs
//  일반 배틀 모드 — RunBootstrap 이 스테이지마다 새로 만든다.
// ============================================================

public class NormalMode : BattleModeBase
{
    readonly int _stageNumber;

    public NormalMode(int stageNumber)
    {
        _stageNumber = stageNumber;
    }

    public override BattleMode Mode        => BattleMode.Normal;
    public override int        StageNumber => _stageNumber;
}
