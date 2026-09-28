using UnityEngine;

// ============================================================
//  SummonerVigorRule.cs
//  소환사 **패기(Vigor)** 스탯이 하는 일의 정본.
//
//  ■ 왜 새로 짰나 (사용자 지적, 2026-09-07)
//    옛 이름은 '힘(Strength)' 이었고 죽은 스탯이었다. 하는 일이 둘뿐이었는데 둘 다 값이 없었다:
//      · 소환사 평타       Str×2 → 8~16, 공속 0.8 이라 DPS 6~13.
//        20스테이지 용사가 체력 수천이라 아무 의미가 없다.
//      · 몬스터 공격력     +Str×0.1 → +0.4~0.8. 4% 미만.
//    뿌리는 **지능이 "많이" 와 "세게" 를 둘 다 먹고 있다**는 것이다.
//    소환력이 이미 공격력을 올리므로 힘은 소환력의 열화판이었다.
//    그래서 패기에게는 <b>지능이 못 하는 것</b>을 준다.
//
//  ■ 패기가 하는 일 둘 (사용자 확정, 2026-09-07)
//    ① 밀어내기 — 내 몬스터의 평타 넉백이 세진다.
//       라인 디펜스에서 "적을 뒤로 미는 것" 은 성벽 도달을 늦추는 실제 값어치다.
//       공격력·체력 어느 쪽과도 겹치지 않고, 화면에서 바로 보인다.
//    ② 과부하 저항 — 같은 카드를 거듭 내도 값이 덜 오른다.
//       지능이 '그릇' 이라면 패기는 '밀어붙이는 지구력' 이다.
//
//    ⚠ 두 효과 모두 <b>기준 패기(5)</b> 에서 0 이다
//      로스터의 패기는 4~8 이라 기준을 가운데 두어야 "힘이 낮은 소환사" 도
//      성립한다. 최솟값을 기준으로 잡으면 전원이 보너스만 받아 축이 안 갈린다.
//
//  ■ 소환사 평타는 그대로 둔다
//    SummonerData.Attack = Vigor × AttackPerVigor. 없애지는 않았다 —
//    성벽 뒤에서 깨작대는 그림이 캐릭터를 살아 있게 한다. 다만 그건
//    **곁다리**고, 패기의 본 역할은 위 둘이다.
//
//  ⚠ 옛 이름 'Strength' 의 세이브를 물려받는다
//    소환사 SO 12개에 Strength 로 직렬화돼 있어서, 필드에
//    [FormerlySerializedAs("Strength")] 가 달려 있다. 그 줄을 지우면 기존
//    에셋의 값이 전부 0 이 된다 — SummonerCreator 로 다시 굽기 전까지는.
// ============================================================

public static class SummonerVigorRule
{
    /// <summary>
    /// 효과가 0 이 되는 기준 패기. 로스터가 4~8 이라 가운데다.
    /// </summary>
    const float BaseVigor = 5f;

    // ── ① 밀어내기 ───────────────────────────────────────────

    /// <summary>
    /// 패기 1당 평타 넉백 배율에 더해지는 값.
    ///
    /// 패기 4 → ×0.80 · 패기 5 → ×1.00 · 패기 8 → ×1.60.
    /// 두 배 차이라 라인에서 눈에 보인다.
    /// </summary>
    const float KnockbackPerVigor = 0.20f;

    /// <summary>넉백 배율의 하한. 0 이 되면 밀어내기가 통째로 사라진다.</summary>
    const float MinKnockbackMult = 0.5f;

    /// <summary>
    /// 이 소환사가 부린 몬스터의 <b>평타</b> 넉백 배율.
    ///
    /// ⚠ 스킬 넉백에는 안 걸린다
    ///   스킬은 자기 KnockbackMult 를 갖고 있고 그 값으로 밸런스가 잡혀 있다.
    ///   거기까지 곱하면 메테오 한 방이 전열을 화면 밖으로 날린다.
    ///
    /// ⚠ 실제로 곱하는 곳은 UnitHitSystem 하나다
    ///   스폰 때 KnockbackPowerComponent 에 구워 둔다 — 피해 계산이 Burst 잡이라
    ///   그 안에서 소환사를 조회할 수 없다 (RendComponent 와 같은 이유).
    /// </summary>
    public static float KnockbackMultFor(SummonerData summoner)
        => summoner == null
            ? 1f
            : Mathf.Max(MinKnockbackMult,
                        1f + (summoner.Vigor - BaseVigor) * KnockbackPerVigor);

    // ── ② 과부하 저항 ────────────────────────────────────────

    /// <summary>
    /// 패기 1당 과부하 계수에서 깎이는 양.
    ///
    /// 기본 계수가 0.10 이므로 패기 8 → 0.07 (30% 완화), 패기 4 → 0.11.
    /// </summary>
    const float OverloadReliefPerVigor = 0.01f;

    /// <summary>
    /// 과부하 계수의 하한.
    ///
    /// ⚠ 0 으로 내려가면 안 된다 — 도배를 막는 장치가 통째로 사라진다.
    ///   과부하의 목적은 "덱을 넓게 쓰게 하는 것" 이고, 패기는 그 벌을
    ///   <b>덜어 줄</b> 뿐 없애 주지는 않는다.
    /// </summary>
    const float MinOverloadStep = 0.04f;

    /// <summary>
    /// 이 소환사의 과부하 1단계당 가산 비율. 기본은 SummonCostRule.OverloadStep.
    ///
    /// ⚠ 비용을 다른 데서 다시 계산하지 말 것 — SummonCostRule 이 이 함수를 부른다.
    /// </summary>
    public static float OverloadStepFor(SummonerData summoner)
    {
        if (summoner == null) return SummonCostRule.OverloadStep;

        float step = SummonCostRule.OverloadStep
                   - (summoner.Vigor - BaseVigor) * OverloadReliefPerVigor;

        // 유물 '인내' — 패기와 같은 축이라 같은 자리에서 뺀다.
        step -= RelicTreeApplier.GetSystemValue(RelicSystemEffect.OverloadRelief);

        return Mathf.Max(MinOverloadStep, step);
    }

    // ── ③ 평타 세기 (사용자 지시, 2026-09-11) ───────────────
    //
    //  ■ 왜 더했나
    //    패기 높은 소환사(오크 킹·도살자·트롤 조련사)는 지능이 낮아 그릇이 작다.
    //    그 대가로 받는 것이 넉백·과부하뿐이라 하위권에 머물렀다.
    //    소환사 평타는 대상 최대 체력 비례라(SummonerStrikeRule) 비율을 키우면
    //    몸으로 막아서는 캐릭터가 성벽 뒤에서 실제로 적을 지운다.

    /// <summary>패기 1당 평타 비율(잡병 25% · 엘리트 5% · 보스 1%)에 곱해지는 증가분.</summary>
    const float StrikePerVigor = 0.12f;

    /// <summary>평타 배율 하한 — 패기가 아주 낮아도 평타가 사라지지는 않는다.</summary>
    const float MinStrikeMult = 0.5f;

    /// <summary>
    /// 소환사 평타 비율에 곱하는 배율. 패기 2 → ×0.64 · 5 → ×1.00 · 10 → ×1.60.
    /// ⚠ 곱하는 곳은 SummonerStrikeRule.Build 하나다 (스폰 때 엔티티에 굽는다).
    /// </summary>
    public static float StrikeMultFor(SummonerData summoner)
        => summoner == null
            ? 1f
            : Mathf.Max(MinStrikeMult, 1f + (summoner.Vigor - BaseVigor) * StrikePerVigor);

    // ── 화면 표시 ────────────────────────────────────────────

    /// <summary>
    /// 소환사 선택 화면에 적을 한 줄 — "밀어내기 ×1.6  ·  평타 ×1.36  ·  과부하 7%".
    ///
    /// ⚠ 숫자를 손으로 적지 말 것 — 위 상수가 정본이다.
    /// </summary>
    public static string DescribeFor(SummonerData summoner)
        => LocalizationManager.Instance.Format(
               "밀어내기 ×{0:0.0#}  ·  평타 ×{1:0.0#}  ·  과부하 {2:0.#}%",
               KnockbackMultFor(summoner), StrikeMultFor(summoner),
               OverloadStepFor(summoner) * 100f);
}
