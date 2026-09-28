using UnityEngine;

// ============================================================
//  SpawnPaceRule.cs
//  라인 대기열이 몬스터 한 마리를 뱉는 **속도 등급**의 정본.
//
//  ■ 등급 다섯 — 폭이 크고 값이 딱 떨어진다 (사용자 확정, 2026-09-07)
//    한때 0.25~0.65 였는데 **체감이 없었다.** 트롤과 슬라임의 차이가 0.4초라
//    화면에서는 둘 다 "우르르 나온다" 로 보였다. 지금은 6배 차이다 —
//    트롤은 성문에서 한 기씩 걸어 나오고, 그 그림 자체가 "비싼 것을 냈다" 다.
//
//      등급   힘지수      간격     공/체    무리를 다 세우는 데
//      ────────────────────────────────────────────────────────
//        0    ~140       0.25초    +0%     슬라임 8마리 2.0초
//        1    ~190       0.50초   +20%     좀비 6마리 3.0초
//        2    ~250       0.75초   +40%     오크 4기 3.0초
//        3    ~330       1.00초   +60%     전쟁 오크 4기 4.0초
//        4    그 위      1.50초   +80%     트롤 3기 4.5초
//
//  ■ 느린 만큼 세다 — 손해만 있는 축이 아니다 (사용자 확정, 2026-09-07)
//    늦게 나오는 것은 순수한 벌이다. 대가가 없으면 "빠른 놈만 쓰는" 것이
//    언제나 정답이 되어 등급 자체가 죽는다.
//
//    ⚠ 보너스는 간격과 **한 묶음**이다 — 마나당 값어치를 맞춘 값이다
//      보너스가 없던 시절 트롤은 마나 12 에 체력 780(65/마나)이었고
//      슬라임은 마나 5 에 640(128/마나)이었다 — 느린 데다 마나당 절반이라
//      쓸 이유가 없었다. +80% 면 1404(117/마나)로 슬라임과 나란해지고,
//      공격력은 오히려 앞선다(13.5 대 8). 간격을 늘리면 보너스도 함께 올릴 것.
//
//    ⚠ 보너스는 힘지수에 되먹임되지 않는다
//      PowerOf 는 **SO 원본값**만 본다. 보너스는 그 뒤에 곱해지는 것이라
//      등급이 등급을 올리는 고리가 생기지 않는다. PowerOf 에 합성값을
//      넣지 말 것 — 넣는 순간 트롤이 무한히 느려진다.
//
//    ⚠ 보너스는 **한 곳에서만** 곱한다
//      MonsterStatComposer ②. 도감 표시(MonsterDetailPopup)는 그 값을 다시
//      계산하지 않고 같은 함수를 부른다.
//
//  ■ 기준은 개체의 힘이다 — 마나 비용이 아니다
//    마나는 설계값이라 밸런스를 만질 때마다 배출 속도가 따라 흔들린다.
//    체력·공격력은 그 개체가 화면에서 차지하는 무게 그 자체다.
//
//    ⚠ 마릿수(SummonCount)는 넣지 않는다
//      한 마리당 간격이라 마릿수는 **저절로** 곱해진다. 여기에 또 넣으면
//      물량 종족이 두 번 벌을 받는다.
//      지금은 한 무리를 다 세우는 데 1.75~4.5초다 (숲의 트롤이 9×0.50 = 4.5 로
//      가장 길다). 이 폭이 곧 "무엇을 낼까" 의 무게다.
//
//  ■ 특성은 여기 없다
//    과잉 소환·선발대는 이 값에 **곱해지는** 배율이다
//    (RunPerkRule.DrainMultiplierFor). 두 축을 섞으면 특성을 켰을 때
//    종족 간 순서가 뒤집힌다.
//
//  ⚠ 세 배열은 한 묶음이다 — 길이가 어긋나면 조용히 엉뚱한 칸을 읽는다
//    Thresholds 는 경계라 하나 짧다 (등급 5개 = 경계 4개).
// ============================================================

public static class SpawnPaceRule
{
    // ── 힘 지수 ──────────────────────────────────────────────

    /// <summary>
    /// 공격력 1점을 체력 몇 점으로 칠 것인가.
    ///
    /// ⚠ 6 은 이 로스터의 두 축 폭을 맞춘 값이다
    ///   체력은 42~260(폭 218), 공격력은 5~32(폭 27). 27 × 6 ≈ 162 라
    ///   두 축이 비슷한 무게로 지수에 들어간다. 1 로 두면 공격력이 사실상
    ///   무시돼 리치와 고블린 궁수가 같은 등급이 된다.
    /// </summary>
    const float AttackWeight = 6f;

    /// <summary>등급 경계 — 힘지수가 이 값 <b>미만</b>이면 그 등급이다.</summary>
    static readonly float[] Thresholds = { 140f, 190f, 250f, 330f };

    /// <summary>등급별 배출 간격(초). 맨 위는 슬라임의 여섯 배다 — 체감이 목적이다.</summary>
    static readonly float[] Intervals = { 0.25f, 0.50f, 0.75f, 1.00f, 1.50f };

    /// <summary>등급별 체력·공격력 보너스. 20% 씩 떨어진다 — 늦게 나오는 값이다.</summary>
    static readonly float[] StatBonuses = { 0f, 0.20f, 0.40f, 0.60f, 0.80f };

    /// <summary>등급 수. 화면이 "3 / 5" 처럼 적을 때 쓴다.</summary>
    public static int TierCount => Intervals.Length;

    // ── 조회 ─────────────────────────────────────────────────

    /// <summary>
    /// 그 종족의 힘 지수. 배출 등급 <b>전용</b>이다 —
    /// 품질·소환력·카드 레벨을 곱하지 않는다.
    ///
    /// ⚠ 굴려 나온 값을 쓰지 말 것
    ///   같은 카드가 소환사·품질에 따라 다른 속도로 나오면 "이 종족은 이만큼
    ///   걸린다" 는 감각이 안 생긴다. 그리고 이 등급이 주는 보너스가 다시
    ///   지수를 밀어 올려 되먹임 고리가 생긴다.
    /// </summary>
    public static float PowerOf(MonsterSpeciesData species)
        => species.MaxHp + species.Attack * AttackWeight;

    /// <summary>0(가장 빠름) ~ TierCount-1(가장 느림).</summary>
    public static int TierOf(MonsterSpeciesData species)
    {
        float power = PowerOf(species);

        for (int i = 0; i < Thresholds.Length; i++)
            if (power < Thresholds[i]) return i;

        return Thresholds.Length;
    }

    /// <summary>이 종족 한 마리를 뱉고 다음까지 기다리는 시간(초). 특성 배율은 빠져 있다.</summary>
    public static float IntervalFor(MonsterSpeciesData species)
        => Intervals[TierOf(species)];

    /// <summary>
    /// 늦게 나오는 대가로 붙는 체력·공격력 배율 (1.00 ~ 1.80).
    ///
    /// ⚠ 곱하는 곳은 MonsterStatComposer ② 하나다. 다른 데서 또 곱하지 말 것.
    /// </summary>
    public static float StatMultiplierFor(MonsterSpeciesData species)
        => 1f + StatBonuses[TierOf(species)];

    // ── 초조(Rush) — 판이 길어지면 줄이 빨라진다 (사용자 지시, 2026-09-15) ──
    //
    //  ■ 적 광폭화와 **같은 시계**를 쓴다
    //    보스는 판이 열리고 60초마다 한 스택씩 세진다(HeroTierSetup.EnrageCooldown).
    //    아군도 같은 순간에 한 단계씩 빨라진다 — 간격이 절반이 된다.
    //    두 사건이 같은 시각에 일어나야 "판이 길어지면 서로 몰아친다" 가
    //    화면에서 한 사건으로 읽힌다. 시각이 어긋나면 그냥 둘 다 이유 없이 변한다.
    //
    //  ■ 왜 필요한가 — 대기열이 길면 판이 끝나지 않는다
    //    라인 복귀로 대기열은 판마다 불어나는데 배출 간격은 종족이 정한 고정값이다.
    //    후반에는 줄을 다 뱉기 전에 판이 늘어져, 광폭화한 보스에게 몰살당하는 동안
    //    성문 뒤에 남은 물량이 한 마리씩 걸어 나온다. 교착을 끝내는 장치가
    //    적 쪽에만 있으면 그 시계는 플레이어에게만 불리하게 돈다.
    //
    //  ⚠ 단계 상한이 있다 (MaxRushSteps)
    //    간격이 0 으로 수렴하면 한 프레임에 대기열 전체가 쏟아진다.
    //    1/32 면 가장 느린 트롤(1.50초)도 0.047초 — 사실상 프레임당 한 마리라
    //    그 위로는 더 빨라지지도 않으면서 숫자만 발산한다.
    //
    //  ⚠ 종족 간 순서는 끝까지 유지된다 — 곱하는 배율이라 트롤은 언제나 슬라임보다 느리다.
    //    특성 배율(RunPerkRule.DrainMultiplierFor)과 같은 규칙이다.

    /// <summary>한 단계가 오르는 간격(초). 적 광폭화와 같은 시계다.</summary>
    public static float RushStepSeconds => HeroTierSetup.EnrageCooldown;

    /// <summary>단계 상한. 1/32 위로는 프레임이 벽이라 올려도 의미가 없다.</summary>
    public const int MaxRushSteps = 5;

    /// <summary>판이 열린 뒤 흐른 시간이 만든 가속 단계 (0 ~ MaxRushSteps).</summary>
    public static int RushStepsAt(float secondsSinceStageStart)
        => secondsSinceStageStart < RushStepSeconds
               ? 0
               : Mathf.Min(MaxRushSteps, (int)(secondsSinceStageStart / RushStepSeconds));

    /// <summary>
    /// 배출 간격에 <b>곱할</b> 가속 배율 (1 → 1/2 → 1/4 … 1/32).
    ///
    /// ⚠ 초를 돌려주지 않는다 — 종족별 간격 축을 덮어쓰지 않기 위해서다
    ///   (RunPerkRule.DrainMultiplierFor 와 같은 이유).
    /// </summary>
    public static float RushMultiplierAt(float secondsSinceStageStart)
        => 1f / (1 << RushStepsAt(secondsSinceStageStart));

    /// <summary>
    /// 한 줄 설명 — "소환 0.75초".
    ///
    /// ■ ⚠ 공·체 보너스는 적지 않는다 (사용자 지시, 2026-09-10)
    ///   한때 "소환 0.75초 · 공·체 +40%" 로 함께 적었다. 그런데 이 창의
    ///   체력·공격력 행은 **이미 그 보너스가 곱해진 값**이다
    ///   (MonsterDetailPopup 이 StatMultiplierFor 를 쓴다). 같은 값을 한 번은
    ///   숫자로, 한 번은 비율로 두 번 말하는 셈이라 "+40% 를 또 더해야 하나"
    ///   가 된다. 대가는 위쪽 숫자에 이미 들어가 있으니 여기는 간격만 말한다.
    ///
    /// ⚠ 숫자를 손으로 적지 말 것 — 배열이 정본이다.
    ///   여기에 적어 두면 밸런스를 고친 날부터 화면만 옛말을 한다.
    /// </summary>
    public static string DescribeFor(MonsterSpeciesData species)
        => LocalizationManager.Instance.Format("{0:0.00}초", Intervals[TierOf(species)]);   // 행 이름이 이미 '소환 간격' 이다 (사용자 지시, 2026-09-12)
}

// ============================================================
//  SpeedHpRule.cs
//  빠른 종족의 체력 보너스 (사용자 지시, 2026-09-15).
//
//  ■ 왜 필요한가
//    이동속도는 이 게임에서 강함이 아니다 — 적이 성으로 걸어오므로 먼저 닿아도
//    얻는 것이 없고, 무리보다 앞서 혼자 적진에 들어가 **점사**를 받는다.
//    게다가 빠른 종족은 한 마리당 DPS 가 높아, 하나가 쓰러질 때마다 잃는 딜이 크다.
//    슬라임은 반대다 — 좁은 간격 · 높은 체력 · 낮은 개체 DPS 로 피해가 흩어지고
//    한 마리를 잃어도 손실이 적다. 같은 전투력 지수여도 체감 격차가 컸다.
//
//  ■ 체력에만 준다
//    약점이 "먼저 도착해 녹는다" 이므로 버티는 쪽을 채운다. 공격력을 올리면
//    한 마리가 죽을 때 잃는 딜이 더 커져 문제가 깊어진다.
//
//  ⚠ 작게 둔다 — 속도는 여전히 종족의 성격이지 보상받는 축이 아니다.
//    BaseSpeed 이하는 0, 늑대(4.2)가 +20% 남짓, 상한 MaxBonus.
//
//  ⚠ SO 원본 이동속도로 판정한다 — 레벨·장비로 빨라져도 보너스는 그대로다
//    (SpawnPaceRule.PowerOf 와 같은 규율 — 되먹임 고리를 만들지 않는다).
//
//  ⚠ 곱하는 곳은 둘뿐이다 — MonsterStatComposer ②(전투) · MonsterDetailPopup(도감).
// ============================================================

public static class SpeedHpRule
{
    /// <summary>이 이동속도부터 보너스가 붙기 시작한다.</summary>
    public const float BaseSpeed = 2.5f;

    /// <summary>이동속도 1 당 체력 보너스.</summary>
    public const float PerSpeed = 0.12f;

    /// <summary>보너스 상한.</summary>
    public const float MaxBonus = 0.25f;

    /// <summary>그 종족의 체력 배율 (1 = 보너스 없음).</summary>
    public static float HpMultiplierFor(MonsterSpeciesData species)
        => 1f + Mathf.Clamp((species.MoveSpeed - BaseSpeed) * PerSpeed, 0f, MaxBonus);
}
