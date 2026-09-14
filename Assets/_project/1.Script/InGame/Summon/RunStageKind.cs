// ============================================================
//  RunStageKind.cs
//  스테이지의 성격 — **번호로 성격을 정하는 유일한 곳**.
//
//  ■ 왜 한곳에 모으나
//    편성(HeroDeployment) · 보상(RunBootstrap) · 화면(StageLoopDirector) 셋이
//    같은 질문을 한다: "이번 판은 뭔가?" 각자 `% 5 == 0` 을 적으면 주기를
//    바꿀 때 한 군데를 빠뜨리고, 그러면 **보스가 나오는데 보상은 일반**인
//    상태가 조용히 생긴다.
//
//  ■ 주기 (2026-09-04 재확정)
//      보스   5의 배수 →  5 · 10 · 15 · 20 …   **피할 수 없다**
//      엘리트 주기가 없다 — 갈림길에서 **플레이어가 고른다** (RunNodeKind)
//
//    ⚠ 엘리트를 주기로 두지 않는 이유
//      갈림길이 "다음 판을 무엇으로 할까" 를 묻는데 주기가 그걸 미리 정해
//      버리면 선택이 통보가 된다. 보스만 강제다 — 피할 수 없어야 관문이다.
//
//  ■ 보상은 골드와 특성뿐이다 (사용자 확정)
//      엘리트 → 특성 **무작위 1개**를 그냥 받는다 · 처치 골드 ×3
//      보스   → 특성 **3택** · 처치 골드 ×10
//    같은 화폐(특성)로 갚되 **고를 수 있는가**로 무게를 가른다.
//    수치는 RunGoldRule 과 RunBootstrap.AfterCardReward 가 소유한다.
// ============================================================

public enum RunStageKind
{
    /// <summary>일반 전투. 보상은 카드 3택뿐이다.</summary>
    Normal = 0,

    /// <summary>엘리트 용사 1기가 섞인다. 특성 무작위 1개.</summary>
    Elite = 1,

    /// <summary>보스 용사 1기가 선다. 특성 3택.</summary>
    Boss = 2,
}

public static class RunStageKindRule
{
    /// <summary>보스가 서는 주기. 이 배수마다 보스 스테이지다.</summary>
    public const int BossEvery = 5;

    /// <summary>
    /// 첫 엘리트가 반드시 서는 스테이지 (사용자 지시, 2026-09-13).
    ///
    /// ■ 왜 번호로 못 박는가
    ///   엘리트는 원래 갈림길에서 '험로' 를 <b>고른 사람만</b> 만난다. 그래서 첫 보스
    ///   (5스테이지)까지 우두머리를 한 번도 안 보고 갈 수 있었고, 그러면 보스가
    ///   그 런에서 처음 만나는 우두머리가 된다 — 장수가 처음 서는 판과 보스가
    ///   처음 서는 판이 겹친다(HeroDeployment.GeneralFromStage 주석의 걱정 그대로).
    ///   한 판이라도 겪어 본 뒤에 보스를 만나게 한다.
    ///
    /// ⚠ 반드시 <see cref="BossEvery"/> 보다 앞이어야 한다
    ///   3 이면 [1 병사][2 병사][3 엘리트][4 병사][5 보스] 가 된다.
    /// ⚠ 이 판은 <b>장수가 처음 서는 자리이기도 하다</b> — HeroDeployment.Build 의
    ///   'soldiersOnly' 는 일반 부대에만 걸리고 엘리트·보스 자리는 예외다.
    /// ⚠ 부대는 늘지 않는다 — GetSquadCount 의 허들 +1 은 5스테이지 뒤부터다.
    /// </summary>
    public const int FirstEliteStage = 3;

    /// <summary>이 스테이지가 보스판인가 — <b>갈림길이 열리지 않는 조건 중 하나</b>.</summary>
    public static bool IsBossStage(int stageNumber)
        => stageNumber % BossEvery == 0;

    /// <summary>
    /// 번호로 못 박힌 첫 엘리트판인가.
    ///
    /// ⚠ 보스판과 마찬가지로 <b>갈림길이 열리지 않는다</b>
    ///   (RunBootstrap.ChooseNextStage). 갈림길을 열어 두면 '험로' 를 고르지 않는
    ///   것으로 이 판을 피할 수 있어 못 박은 의미가 사라진다.
    /// </summary>
    public static bool IsFirstEliteStage(int stageNumber)
        => stageNumber == FirstEliteStage;

    /// <summary>
    /// 번호만으로 정해지는 성격 — 보스판과 <b>첫 엘리트판</b>이 여기서 갈린다.
    /// 그 밖의 판을 엘리트로 올릴지는 갈림길에서 고른 결과가 정한다.
    ///
    /// ⚠ 이 함수만 보고 "엘리트인가" 를 판단하지 말 것 —
    ///   실제 성격은 RunBootstrap 이 들고 있는 현재 스테이지 종류다.
    /// </summary>
    public static RunStageKind Of(int stageNumber)
        => IsBossStage(stageNumber)      ? RunStageKind.Boss
         : IsFirstEliteStage(stageNumber) ? RunStageKind.Elite
                                          : RunStageKind.Normal;

    /// <summary>
    /// 편성·난이도가 '허들' 로 취급하는가 — 보스와 엘리트 둘 다다.
    ///
    /// 부대 수 +1(HeroDeployment.GetSquadCount)과 BattleMode.Elite 가 이 값을 본다.
    /// </summary>
    public static bool IsHurdle(RunStageKind kind) => kind != RunStageKind.Normal;
}
