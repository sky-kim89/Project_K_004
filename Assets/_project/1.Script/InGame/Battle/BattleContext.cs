//  BattleContext.cs
//  현재 배틀의 진행 상태를 담는 순수 데이터 클래스.
//  BattleModeBase 와 BattleManager 가 공유해서 읽고 쓴다.
// ============================================================

public class BattleContext
{
    // ── 스테이지 ──────────────────────────────────────────────
    //
    //  ⚠ 웨이브 개념이 없다 (원작에서 제거)
    //    원작은 한 스테이지 안에서 웨이브를 여러 번 돌렸다.
    //    이 게임은 **1스테이지 = 1전투**다. 최대 5개 용사 부대가 한 번에 들어오고,
    //    그것을 정리하면 스테이지가 끝난다.
    //    그래서 TotalWaves / CurrentWave / IsLastWave / EndlessBossIndex 를 없앴다.

    /// <summary>현재 스테이지 번호 (1부터).</summary>
    public int StageNumber { get; set; } = 1;

    /// <summary>
    /// 이번 스테이지에 등장한 적의 총원. 진행도 표시의 분모다.
    ///
    /// 웨이브가 없으므로 "몇 번째 웨이브" 대신 "적을 얼마나 정리했나" 로 읽는다.
    /// 용사가 휘하 병사를 데려오므로 스폰이 끝난 뒤에 확정된다.
    /// </summary>
    public int StageEnemyTotal { get; set; }

    // ── 진행 상태 ─────────────────────────────────────────────
    public BattleState State       { get; set; } = BattleState.None;
    public BattleMode  Mode        { get; set; } = BattleMode.Normal;

    // ── 생존 카운트 (스테이지 클리어·패배 판정용) ─────────────
    public int AliveEnemyCount { get; set; }
    public int AliveAllyCount  { get; set; }

    // ── 전투 통계 스냅샷 (결과 팝업 통계 탭용) ───────────────
    public System.Collections.Generic.List<GeneralStatEntry> CombatStats { get; } = new();

    // ── 전투 경과 시간 (환생 팝업 DPS 계산용) ────────────────
    public float BattleElapsedSeconds { get; set; }

    // ── 편의 프로퍼티 ─────────────────────────────────────────
    public bool IsEnemyClear    => AliveEnemyCount <= 0;
    public bool IsAllyDefeated  => AliveAllyCount  <= 0;
}
