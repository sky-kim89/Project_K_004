using System;
using UnityEngine;

// ============================================================
//  ReincarnationData.cs
//  환생 포인트 + 어빌리티 새로고침 누적 횟수 영구 저장 섹션.
//
//  ■ 환생 포인트 (ReincarnationPoints)
//    RelicTreePopup 에서 노드를 찍을 때 차감.
//    즉시 환생(RelicTreePopup.OnReincarnate) 시 EarnPoints() 로 적립.
//
//  ■ 어빌리티 새로고침 (UsedRefreshCount)
//    AbilitySelectPopup 에서 새로고침 시 UseRefresh() 로 증가.
//    환생 시 ResetOnReincarnation() 으로 초기화 — 환생 전까지 누적 유지.
//
//  ■ 환생 포인트 획득 공식 (구간별 누적)
//    실제 값은 StagePointIncrement 하나가 정본이다 — 여기 숫자를 옮겨 적지 말 것.
//    (리밸런스 전 값 'st30 = 98pt' 가 이 주석에만 남아 한동안 어긋나 있었다)
//
//    ⚠ 첫 환생이 너무 멀면 유물 화면이 "볼 수만 있는 화면" 이 된다
//      예전엔 5스테이지부터였고, 거기 못 가면 한 판을 통째로 날렸다.
//      2스테이지부터 1pt 씩 붙어 실패해도 뭔가 남는다.
// ============================================================

[Serializable]
class ReincarnationJson
{
    public int points;
    public int usedRefreshCount;
    public int totalCount;
    public int bestStage;
}

public class ReincarnationData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.Reincarnation;

    public int ReincarnationPoints { get; private set; }
    public int UsedRefreshCount    { get; private set; }

    /// <summary>
    /// 지금까지 도달한 가장 깊은 스테이지 — **환생을 넘어 남는다.**
    ///
    /// ■ 왜 StageProgressData 를 못 쓰나
    ///   그쪽 ClearedNormalStages 는 환생 때 지워진다
    ///   (UserDataManager.Reincarnate → StageProgressData.SetDefaults).
    ///   그리고 이 게임은 **패배가 곧 환생**이므로, 죽을 때마다 0 으로 돌아간다.
    ///   "스테이지 10 도달" 같은 해금 조건을 그 값으로 걸면 죽는 순간 다시 잠긴다.
    ///
    ///   영구 기록이 필요한 자리는 여기다 — 이 섹션은 환생에도 살아남고
    ///   (ResetOnReincarnation 은 UsedRefreshCount 만 건드린다),
    ///   이미 "얼마나 갔나" 를 다루는 자리다.
    ///
    /// ⚠ 소환사 해금 조건이 이 값을 본다 (SummonerUnlockRule).
    /// </summary>
    public int BestStage { get; private set; }

    /// <summary>지금까지 환생한 횟수. 첫 환생 직후 안내(FirstRelic 튜토리얼)가 본다.</summary>
    public int TotalCount { get; private set; }

    // 환생 가능 최소 스테이지 — StageConfig 에서 읽어 단일 진실 소스 유지
    public static int ReincarnateMinStage => StageConfig.Current?.ReincarnateMinStage ?? 2;

    // ── 환생 포인트 공식 ──────────────────────────────────────

    /// <summary>
    /// 클리어한 일반 스테이지 수에 따른 환생 포인트 (누적).
    /// 구간별 획득량: ~9 +1pt, 10~15 +4pt, 16~20 +8pt, 21~25 +14pt, 26+ +30pt.
    /// 시작 지점은 ReincarnateMinStage(기본 2).
    ///
    /// 예) st30 = 1×8 + 4×6 + 8×5 + 14×5 + 30×5 = 8+24+40+70+150 = 292pt.
    ///
    /// ⚠ 뒤로 갈수록 가팔라야 한다 (2026-08-25 상향)
    ///   예전 곡선은 st30 이 98pt 로, 초반에 죽고 다시 도는 쪽이 이득이었다.
    ///   깊이 들어간 판이 보상받아야 "한 번 더 멀리 가 보자" 가 성립한다.
    ///
    ///   ⚠ 9스테이지까지는 예전 값 그대로다 — 초반 반복은 손대지 않는다.
    ///     상향은 10부터 붙고 배수가 완만하게 자란다:
    ///     st5 ×1.0 · st10 ×1.2 · st15 ×1.5 · st20 ×2.0 · st25 ×2.5 · st30 ×3.0.
    ///   (유물 강화 비용은 그대로다. 이 곡선만으로 후반 보상이 3배가 된다)
    /// </summary>
    public static int CalculateReincarnationPoints(int clearedNormalStage)
    {
        if (clearedNormalStage < ReincarnateMinStage) return 0;
        int total = 0;
        for (int s = ReincarnateMinStage; s <= clearedNormalStage; s++)
            total += StagePointIncrement(s);
        return total;
    }

    static int StagePointIncrement(int stage)
    {
        if (stage < 10) return 1;    // 예전과 동일 — 초반은 건드리지 않는다
        if (stage < 16) return 4;
        if (stage < 21) return 8;
        if (stage < 26) return 14;
        return 30;
    }

    public static bool CanReincarnate(int clearedNormalStage)
        => clearedNormalStage >= ReincarnateMinStage;

    // ── 포인트 조작 ───────────────────────────────────────────

    /// <summary>포인트가 충분하면 차감하고 true 반환, 아니면 false.</summary>
    public bool TrySpendPoints(int cost)
    {
        if (cost <= 0 || ReincarnationPoints < cost) return false;
        ReincarnationPoints -= cost;
        return true;
    }

    /// <summary>스테이지 기반 포인트 자동 계산 후 적립.</summary>
    public void EarnPointsByStage(int clearedNormalStage)
        => EarnPoints(PreviewPoints(clearedNormalStage));

    /// <summary>
    /// 이번 환생으로 받을 포인트 — **화면에 보여 줄 값도 이것을 쓴다.**
    ///
    /// ⚠ CalculateReincarnationPoints 를 직접 부르지 말 것
    ///   그쪽은 난이도 배율이 빠진 원판이다. 예전엔 패배 팝업이 그 값을 띄우고
    ///   그대로 지급해서, 높은 난이도로 죽어도 배율이 통째로 사라졌다.
    /// </summary>
    public static int PreviewPoints(int clearedNormalStage)
    {
        float mul = DifficultyConfig.CurrentTier()?.ReincarnationMultiplier ?? 1f;

        // ⚠ 배율에 하한(Max(1, …))을 두지 않는다 (2026-09-11)
        //   쉬움은 일부러 ×0.8 이다 (DifficultyConfigCreator 머리 주석). 한때 여기서
        //   Max(1, 배율) 로 잘라 두어, 배율을 1 아래로 내리면 **조용히 무효**가 됐다.
        //   음수만 막는다.
        mul = Mathf.Max(0f, mul);

        // 유물 '영혼의 항아리' — 난이도 배율 **뒤**에 곱한다.
        // ⚠ 여기가 표시와 지급을 겸하는 유일한 지점이다 (위 주석 참고).
        //   지급 쪽에만 넣으면 패배 화면이 실제보다 적게 말한다.
        mul *= 1f + RelicTreeApplier.GetSystemValue(RelicSystemEffect.ReincarnPointBonus);

        return Mathf.RoundToInt(CalculateReincarnationPoints(clearedNormalStage) * mul);
    }

    public void EarnPoints(int amount)
        => ReincarnationPoints += Mathf.Max(0, amount);

    // ── 어빌리티 새로고침 ─────────────────────────────────────

    public void UseRefresh() => UsedRefreshCount++;

    /// <summary>환생 시 새로고침 횟수 초기화.</summary>
    public void ResetOnReincarnation() => UsedRefreshCount = 0;

    /// <summary>환생 1회를 센다 — UserDataManager.Reincarnate 만 부른다.</summary>
    public void CountReincarnation() => TotalCount++;

    /// <summary>
    /// 이 스테이지까지 갔다 — 최고 기록만 남긴다.
    /// 부르는 곳은 RunBootstrap.AdvanceStage 하나다.
    /// </summary>
    public void RecordStageReached(int stageNumber)
    {
        if (stageNumber <= BestStage) return;

        BestStage = stageNumber;
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize()
        => JsonUtility.ToJson(new ReincarnationJson
        {
            points           = ReincarnationPoints,
            usedRefreshCount = UsedRefreshCount,
            totalCount       = TotalCount,
            bestStage        = BestStage,
        });

    public void Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var dto = JsonUtility.FromJson<ReincarnationJson>(json);
        if (dto == null) return;
        ReincarnationPoints = dto.points;
        UsedRefreshCount    = dto.usedRefreshCount;
        TotalCount          = dto.totalCount;
        BestStage           = dto.bestStage;
    }

    public void SetDefaults()
    {
        ReincarnationPoints = 0;
        UsedRefreshCount    = 0;
        TotalCount          = 0;
        BestStage           = 0;
    }
}
