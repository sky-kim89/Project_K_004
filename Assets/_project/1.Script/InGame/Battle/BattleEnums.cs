// ============================================================
//  BattleEnums.cs
//  배틀 시스템 전용 enum 모음
// ============================================================

// ── 배틀 모드 ─────────────────────────────────────────────────
public enum BattleMode
{
    Normal         = 0,   // 일반 스테이지
    Elite          = 1,   // 엘리트 스테이지
    GoldDungeon    = 2,   // 골드 던전 (추후 구현)
    SpecialDungeon = 3,   // 특수 던전 (추후 구현)
}

// ── 배틀 상태 ─────────────────────────────────────────────────
/// <summary>
/// 전투 상태.
///
/// ■ 이 게임의 루프
///   StageReady → Preparing → InWave → WaveClear → (보상) → StageReady …
///
///   StageReady 가 이 게임에서 새로 붙은 단계다. 플레이어가 적이 오기 전에
///   몬스터를 미리 세워 둘 시간을 준다 — 실시간 소환만으로는 첫 접전에서
///   손이 모자라 아무것도 못 해 보고 밀린다.
/// </summary>
public enum BattleState
{
    None         = 0,

    /// <summary>
    /// 스테이지 대기 — 적이 아직 없다. 플레이어가 몬스터를 미리 소환해 배치한다.
    /// 플레이어가 "시작" 을 누를 때까지 머문다.
    /// </summary>
    StageReady   = 6,

    Preparing    = 1,   // 웨이브 시작 — 적이 화면 오른쪽 밖에서 진입한다
    InWave       = 2,   // 웨이브 진행 중 (이 동안에도 소환할 수 있다)
    WaveClear    = 3,   // 웨이브 클리어 (보상 창 대기)
    BattleVictory = 4,  // 전체 클리어
    BattleDefeat  = 5,  // 패배 — 소환사(= 마왕성) 사망
}

// ── 스폰 유닛 종류 (스폰 테이블 항목용) ───────────────────────
/// <summary>
/// 스폰 항목의 유닛 종류. PoolController 풀 키로 그대로 쓰인다
/// (SpawnEntry.PoolKey => UnitType.ToString()).
///
/// ⚠ 진영별로 쓰는 값이 갈린다 (Faction.cs 참고)
///   적(용사)  : General · Soldier · Elite · Boss
///   아군(몬스터): Monster 하나뿐
/// </summary>
public enum SpawnUnitType
{
    Soldier = 0,
    General = 1,
    Monster = 2,   // 소환 몬스터 (구 Enemy) — 프리팹 키도 "Monster"
    Elite   = 3,
    Boss    = 4,
    Summoner = 5,  // 소환사 — 런당 1기. 프리팹 키 "Summoner"
}
