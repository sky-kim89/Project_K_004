using System.Text;
using UnityEngine;

// ============================================================
//  ManaRegenRule.cs
//  스테이지를 넘길 때 돌아오는 마나 — **계산과 설명의 정본**.
//
//  ■ 왜 따로 뺐나 (사용자 요청, 2026-09-07)
//    화면이 "다음 판에 얼마 돌아오나" 를 미리 보여 주려면, 실제로 주는 쪽과
//    **똑같은 셈**을 해야 한다. 계산이 RunBootstrap.AdvanceStage 안에 흩어져
//    있으면 화면은 그걸 흉내 낼 수밖에 없고, 둘은 반드시 갈린다.
//    이제 주는 쪽도 보여 주는 쪽도 이 함수 하나를 부른다.
//
//  ■ 셈은 세 겹이다 — 순서가 곧 규칙이다
//      ① 기본   지능 × ManaRegenPerIntelligence + **남은 잔량** × HoldRatio
//      ② 개성   약탈(Plunder) 이면 × PerkValue
//      ③ 특성   명상(Meditation) 이면 × 1.20
//
//    ⚠ ① 이 잔량을 본다 — 그래서 "미리 보기" 가 매 순간 달라진다
//      다 쓴 판과 아낀 판이 같은 양을 받으면 아끼는 이유가 없다.
//      화면은 지금 잔량으로 계산한 값을 띄운다 — 판이 끝날 때 잔량이
//      달라져 있으면 실제 회복도 그만큼 달라진다. 그게 맞다.
//
//    ⚠ Max 를 넘겨 받지는 못한다 (SummonManaData.RegenForStage 가 자른다)
//      미리 보기도 같은 자리를 잘라 줘야 "20 돌아온다더니 3만 늘었다" 가 안 된다.
//
//  ⚠ 여기에 새 보정을 넣으면 AdvanceStage 는 저절로 따라온다 —
//    반대로 AdvanceStage 에만 넣으면 화면이 거짓말을 한다.
// ============================================================

public static class ManaRegenRule
{
    /// <summary>
    /// 이번에 스테이지를 넘기면 실제로 늘어날 마나.
    ///
    /// ⚠ 그릇을 넘는 몫은 빼고 돌려준다 — 화면에 뜨는 숫자가 곧 늘어날 양이다.
    /// </summary>
    public static float PreviewFor(SummonerData summoner, SummonManaData mana)
    {
        if (summoner == null || mana == null) return 0f;

        float raw = RawFor(summoner, mana.Current);

        // 그릇을 넘어서는 받지 못한다.
        return Mathf.Max(0f, Mathf.Min(mana.Max, mana.Current + raw) - mana.Current);
    }

    /// <summary>
    /// 자르기 전의 회복량. <b>실제 지급도 이 값을 쓴다</b> (RunBootstrap.AdvanceStage).
    /// </summary>
    public static float RawFor(SummonerData summoner, float currentMana)
    {
        if (summoner == null) return 0f;

        float regen = summoner.ManaRegenFor(currentMana);

        regen = SummonerPerkRuntime.RegenBonusFor(summoner, regen);
        regen = RunPerkRule.RegenFor(regen);

        // 유물 '흐르는 마력'·'명상의 결정' — 개성·특성 뒤에 곱한다.
        regen *= 1f + RelicTreeApplier.GetSystemValue(RelicSystemEffect.ManaRegenBonus);

        // 개성 '자연의 회복'(드루이드) — 그릇 비례라 배율들 **뒤에** 더한다 (Describe 와 같은 자리).
        regen += SummonerPerkRuntime.NatureRestoreFor(summoner);

        // ── 내림 — **마나는 정수로만 움직인다** (사용자 지시, 2026-09-10) ──
        //
        //  ■ 왜 반올림이 아니라 내림인가
        //    올림·반올림은 없는 마나를 만들어 준다. 아껴서 붙은 이자를 깎는 쪽이
        //    맞다 — 소환 비용이 전부 정수라 0.4 로는 어차피 아무것도 못 산다.
        //
        //  ■ 왜 여기서 자르나 — 두 가지가 함께 고쳐진다
        //    ① **화면에서 0.* 이 사라진다.** 그릇(Max)도 비용도 정수이므로,
        //       회복만 정수면 잔량이 영영 정수로 남는다.
        //    ② **특성 '마력 폭주' 가 발동할 수 있게 된다.** 그 특성은 잔량이
        //       정확히 0 에 닿아야 켜지는데(SummonController), 0.5 같은 찌꺼기가
        //       남으면 그것으로 살 수 있는 카드가 없어 **영영 0 이 되지 않는다.**
        //       가진 특성이 조건상 발동 불가능한 것은 밸런스가 아니라 고장이다.
        //
        //  ⚠ 주는 쪽(RunBootstrap.AdvanceStage)과 보여 주는 쪽(PreviewFor·Describe)이
        //    모두 이 함수를 지난다 — 여기 한 줄이면 셋이 함께 정수가 된다.
        return Mathf.Floor(regen);
    }

    /// <summary>
    /// 툴팁 본문 — 어디서 얼마가 오는지 줄로 편다.
    ///
    /// ⚠ 숫자를 손으로 적지 않는다. 위 함수와 같은 값에서 뽑는다 —
    ///   밸런스를 고친 날 설명만 옛말을 하는 일이 없어야 한다.
    /// </summary>
    public static string Describe(SummonerData summoner, SummonManaData mana)
    {
        if (summoner == null || mana == null) return "";

        var sb = new StringBuilder(220);

        float baseIntel = summoner.Intelligence * summoner.ManaRegenPerIntelligence;
        float hold      = Mathf.Max(0f, mana.Current) * summoner.ManaRegenHoldRatio;

        // ── 줄마다 **제 몫만** 적는다 (사용자 지적, 2026-09-12) ──
        //
        //  ■ 예전에는 줄마다 '거기까지의 누계' 를 적었다
        //    "아껴 둔 마나 10% → 23" 이 지능 몫까지 더한 값이라, 읽는 사람은
        //    그 줄이 10% 의 결과인 줄 알았다가 위 줄과 더해 보고서야 누계임을
        //    알아챈다. 한 줄이 두 가지를 말하면 둘 다 안 읽힌다.
        //
        //  ■ 대신 맨 아래 '합계' 한 줄이 누계를 맡는다
        //    누계를 알고 싶은 것은 마지막에 한 번뿐이다.
        //
        //  ⚠ 그래도 **적힌 것을 더하면 합계와 맞아야 한다**
        //    조각을 그냥 내리면(0.6 + 0.6 → 0 + 0) 합계(1)와 어긋난다.
        //    그래서 Step 이 '누계를 내린 값의 차' 를 돌려준다 — 조각의 합은
        //    언제나 마지막 누계의 내림과 정확히 같다.
        float running = 0f;

        sb.Append($"지능 {summoner.Intelligence:0.#}  →  +{Step(ref running, baseIntel)}");

        sb.Append($"\n아껴 둔 마나 {mana.Current:0} 의 {summoner.ManaRegenHoldRatio * 100f:0}%" +
                  $"  →  +{Step(ref running, baseIntel + hold)}");

        if (summoner.Perk == SummonerPerk.Plunder)
            sb.Append($"\n개성 약탈  ×{summoner.PerkValue:0.##}" +
                      $"  →  +{Step(ref running, running * summoner.PerkValue)}");

        if (RunPerkRule.Has(RunPerk.Meditation))
            sb.Append($"\n특성 명상  ×{RunPerkRule.MeditationMult:0.##}" +
                      $"  →  +{Step(ref running, running * RunPerkRule.MeditationMult)}");

        // 유물 '흐르는 마력'·'명상의 결정' — RawFor 와 **같은 자리·같은 순서**다.
        // ⚠ 한때 여기만 빠져 있었다 (2026-09-07). 위 주석이 "화면이 따로 계산하면
        //   반드시 갈린다" 라고 말해 놓고 정작 이 파일 안에서 갈라져 있었다 —
        //   유물을 찍으면 내역 합계가 실제 회복량보다 작게 나왔다.
        float relic = RelicTreeApplier.GetSystemValue(RelicSystemEffect.ManaRegenBonus);
        if (relic > 0f)
            sb.Append($"\n유물  ×{1f + relic:0.##}" +
                      $"  →  +{Step(ref running, running * (1f + relic))}");

        // 개성 '자연의 회복' — RawFor 와 같은 자리·같은 순서 (배율 뒤 가산)
        float nature = SummonerPerkRuntime.NatureRestoreFor(summoner);
        if (nature > 0f)
            sb.Append($"\n개성 자연의 회복  →  +{Step(ref running, running + nature)}");

        int total  = Mathf.FloorToInt(running);
        int capped = Mathf.FloorToInt(PreviewFor(summoner, mana));

        sb.Append($"\n<color=#9EE04A>합계  +{total}</color>");

        // 그릇에 막히면 그 사실을 말해 준다 — 안 그러면 숫자가 어긋나 보인다.
        // ⚠ 내린 값끼리 견준다 — RawFor 가 내리므로 원본과 비교하면
        //   그릇에 막히지 않았는데도 "만 들어온다" 가 뜬다.
        if (capped < total)
            sb.Append($"\n<color=#FF8A6A>그릇이 {mana.Max:0} 라 {capped} 만 들어온다</color>");

        return sb.ToString();
    }

    /// <summary>
    /// 누계를 <paramref name="newTotal"/> 로 옮기고, <b>그 줄이 실제로 보탠 정수 몫</b>을 돌려준다.
    ///
    /// ⚠ 조각을 따로 내리지 않는다 — 내린 누계의 '차' 다
    ///   그래야 줄에 적힌 값을 전부 더했을 때 마지막 합계와 정확히 맞는다.
    /// </summary>
    static int Step(ref float running, float newTotal)
    {
        int before = Mathf.FloorToInt(running);
        running = newTotal;
        return Mathf.FloorToInt(running) - before;
    }
}
