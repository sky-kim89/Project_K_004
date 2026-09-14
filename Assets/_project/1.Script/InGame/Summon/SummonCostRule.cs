using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SummonCostRule.cs
//  **과부하(Overload)** — 같은 카드를 거듭 낼수록 비싸진다.
//  한 스테이지 안에서만 쌓이고, 판이 바뀌면 풀린다.
//
//  ■ 왜 필요한가
//    카드가 여덟 장이어도 가장 효율 좋은 한 장만 도배하는 것이 언제나
//    최선이면, 나머지 일곱 장은 장식이다. 거듭 낼수록 값이 오르면
//    "이 카드를 한 번 더 낼까, 다른 카드를 낼까" 가 매번 갈린다.
//    덱을 넓게 쓰는 쪽이 싸지므로 카드 3택의 선택도 함께 살아난다.
//
//  ■ 규칙 — 원가의 10%씩 **가산**
//      n 번째 배치 비용 = 원가 × (1 + 0.1 × n)      (n = 이번 스테이지 배치 횟수)
//
//    원가 2 짜리 카드라면:
//      1번째  2.0 → 2        4번째  2.6 → 3
//      2번째  2.2 → 2        5번째  2.8 → 3
//      3번째  2.4 → 2        6번째  3.0 → 3
//
//    ⚠ 곱셈 누적(×1.1ⁿ)이 아니다
//      곱하면 열 번째쯤 원가의 2.6배가 되어 물량 종족이 성립하지 않는다.
//      이 게임은 다수를 뽑는 것이 기본 축이므로, 도배를 막되 물량 자체를
//      벌하지는 않는 선형 가산이 맞다.
//
//  ■ ⚠ 안은 소수점, 밖은 반올림
//    누적은 실수로 셈하고 **표시와 소모만** 정수로 반올림한다.
//    정수로 깎아 가며 누적하면 2 → 2 → 2 … 로 영원히 오르지 않는다
//    (2.2 를 2 로 만든 뒤 거기에 10%를 얹어도 다시 2.2 다).
//    그래서 언제나 원가에서 다시 계산한다.
//
//  ■ ⚠ '연속'이 아니라 '누적'이다
//    다른 카드를 사이에 끼우면 초기화되는 방식도 생각할 수 있지만,
//    그러면 A─B─A─B 로 간단히 피해 가므로 벌칙이 성립하지 않는다.
//    한 스테이지 동안 그 카드를 몇 번 냈는가만 센다.
//
//  ■ 스테이지가 바뀌면 0 으로 돌아간다
//    한 판을 버텨 낸 대가다. 세이브에 남기지 않는다 — 저장 지점이
//    스테이지 경계(RunBootstrap.AdvanceStage)뿐이라 언제나 0 이다.
// ============================================================

/// <summary>카드에 붙은 마나 값이 원가와 어떻게 다른가 — 카드 바가 색으로 알린다.</summary>
public enum ManaCostState
{
    /// <summary>원가 그대로.</summary>
    Normal = 0,

    /// <summary>과부하 — 거듭 내서 값이 올랐다. 붉게 표시한다.</summary>
    Overloaded = 1,

    /// <summary>할인 — 개성 등으로 값이 내렸다. 초록으로 표시한다.</summary>
    Discounted = 2,
}

public static class SummonCostRule
{
    /// <summary>
    /// 과부하 1단계당 원가에 얹히는 <b>기본</b> 비율.
    ///
    /// ⚠ 실제 값은 소환사의 <b>패기</b>가 깎는다 (사용자 확정, 2026-09-07)
    ///   정본은 SummonerVigorRule.OverloadStepFor. 이 상수를 직접 쓰지 말 것 —
    ///   패기 8 짜리 소환사는 0.07 로 도배가 덜 아프다.
    /// </summary>
    public const float OverloadStep = 0.10f;

    /// <summary>
    /// 지금 소환사 기준의 과부하 계수.
    ///
    /// ⚠ 소환사가 아직 안 섰으면 기본값이다 (RunBootstrap 순서 — 카드가 먼저 놓인다).
    /// </summary>
    static float Step
        => SummonerVigorRule.OverloadStepFor(SummonerRuntimeBridge.Current?.Data);

    /// <summary>카드 ID → 이번 스테이지에 낸 횟수 = 과부하 단계.</summary>
    static readonly Dictionary<string, int> _used = new();

    /// <summary>비용이 바뀌었다 — 카드 바가 숫자와 색을 다시 그린다.</summary>
    public static event System.Action Changed;

    // ── 조회 ─────────────────────────────────────────────────

    /// <summary>이번 스테이지에 이 카드를 몇 번 냈는가 = 과부하 단계.</summary>
    public static int OverloadStacks(string cardId)
        => !string.IsNullOrEmpty(cardId) && _used.TryGetValue(cardId, out int n) ? n : 0;

    /// <summary>과부하가 걸려 있는가.</summary>
    public static bool IsOverloaded(string cardId) => OverloadStacks(cardId) > 0;

    /// <summary>
    /// 지금 이 카드를 내면 실제로 빠져나갈 마나.
    /// </summary>
    /// <param name="adjustedBase">
    /// 개성·특성 할인까지 반영된 원가 (SummonerPerkRuntime.ManaCostFor 의 결과).
    /// 과부하는 <b>할인된 값 위에</b> 얹힌다 — 할인을 먼저 받고 그만큼에 대해
    /// 과부하가 붙어야, 싸게 부르는 캐릭터가 도배에도 유리한 채로 남는다.
    /// </param>
    public static int CostFor(string cardId, float adjustedBase)
        => Round(RawCostFor(cardId, adjustedBase));

    /// <summary>반올림 전 실제 비용. 툴팁·디버그용.</summary>
    public static float RawCostFor(string cardId, float adjustedBase)
        => adjustedBase * (1f + Step * OverloadStacks(cardId));

    /// <summary>
    /// 카드에 뜰 마나 숫자를 어떤 색으로 그릴 것인가.
    ///
    /// ⚠ 원가와 **반올림한 뒤** 비교한다
    ///   화면에 뜨는 것은 정수다. 2 → 2.2 는 안쪽으로는 올랐지만 화면에는
    ///   둘 다 2 로 뜬다. 그때 붉게 칠하면 "왜 빨간데 숫자가 같지" 가 된다.
    ///   눈에 보이는 값이 실제로 달라졌을 때만 색을 바꾼다.
    /// </summary>
    public static ManaCostState StateOf(string cardId, float rawBase, float adjustedBase)
    {
        int shown  = CostFor(cardId, adjustedBase);
        int origin = Round(rawBase);

        if (shown > origin) return ManaCostState.Overloaded;
        if (shown < origin) return ManaCostState.Discounted;

        return ManaCostState.Normal;
    }

    // ── 기록 ─────────────────────────────────────────────────

    /// <summary>
    /// 카드를 냈다 — 다음 배치부터 값이 오른다.
    ///
    /// ⚠ 마나를 실제로 낸 뒤에만 부른다
    ///   잔량이 모자라 소환이 무산됐는데 세어 두면, 아무것도 못 하고
    ///   값만 오르는 상태가 된다.
    /// </summary>
    public static void MarkUsed(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        _used[cardId] = OverloadStacks(cardId) + 1;
        Changed?.Invoke();
    }

    /// <summary>스테이지가 새로 시작됐다 — 과부하를 모두 푼다.</summary>
    public static void Reset()
    {
        if (_used.Count == 0) return;

        _used.Clear();
        Changed?.Invoke();
    }

    // ── 반올림 ───────────────────────────────────────────────

    /// <summary>
    /// 통상적인 반올림 — 0.5 는 올린다.
    ///
    /// ⚠ Mathf.RoundToInt 를 쓰지 않는다
    ///   그쪽은 짝수 쪽으로 붙이는 방식이라(banker's rounding) 2.5 가 2 가 된다.
    ///   화면에 뜨는 값이라 사람이 아는 반올림이어야 한다.
    /// </summary>
    public static int Round(float value) => Mathf.FloorToInt(value + 0.5f);
}
