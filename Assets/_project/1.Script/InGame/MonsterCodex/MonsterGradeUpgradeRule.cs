using UnityEngine;

// ============================================================
//  MonsterGradeUpgradeRule.cs
//  몬스터 도감의 **품질 개선** — 비용과 판정의 정본.
//
//  ■ 확정이다 — 확률이 아니다 (사용자 확정, 2026-09-06)
//    도감 해금부터가 "조건부 결정적, 확률 뽑기 아님" 이다(MonsterCodexData).
//    같은 화면 안에서 해금은 확정인데 개선만 도박이면 결이 어긋난다.
//    무게는 확률이 아니라 **비용**이 만든다 — 등급이 오를수록 값이 뛴다.
//
//  ■ 영구 골드로 산다
//    품질은 환생해도 남는 영구 데이터라 런 재화로는 살 수 없다.
//    영구 골드는 런이 끝날 때 **그 런에 번 총액**만큼 들어온다
//    (RunGoldRule.SettleToPermanent) — 쓴 돈까지 포함이라, 런 안에서
//    아끼는 것이 영구 성장에 이득이 되지 않는다.
//
//    ⚠ RunGoldData 로 사지 말 것
//      그쪽은 런이 끝나면 사라진다. 사라지는 돈으로 영원한 것을 사면
//      "마지막 판에 몰아서 지르기" 가 유일한 정답이 된다.
//
//  ■ ⚠ 비용을 화면에 적어 두지 말 것
//    표시·판정·차감이 전부 이 파일의 CostFor 하나를 본다. 화면에 숫자를
//    적으면 밸런스를 고친 날부터 값이 갈린다.
// ============================================================

public static class MonsterGradeUpgradeRule
{
    /// <summary>
    /// 이 등급에서 <b>다음</b> 등급으로 올리는 값. Epic 은 끝이라 값이 없다.
    ///
    /// ■ ⚠ 한 런의 수입에 견줘 잡는다
    ///   영구 골드는 그 런에 <b>번 총액</b>이 그대로 들어온다
    ///   (RunGoldRule.SettleToPermanent). 수입은 판마다 커지므로 —
    ///   5스테이지에서 멈춘 런이 약 <b>700</b>, 30스테이지를 다 돈 런이
    ///   약 <b>28,000</b> 이다. 40배 차이다.
    ///
    ///   옛 표(300/900/2400/6000)는 한 종족을 Epic 까지 9,600 이라
    ///   <b>한 런에 세 종족이 Epic</b> 이 됐다. 영구 성장이 하루치도 못 갔다.
    ///
    /// ■ 첫 칸은 싸고, 무게는 가운데 두 칸이 진다 (사용자 지시, 2026-09-10)
    ///   초반 스테이지는 적 부대가 아직 안 늘어 수입이 적다. 첫 칸이 비싸면
    ///   <b>도감을 처음 만지기까지</b>가 너무 멀다 — 그 화면이 무엇을 하는
    ///   곳인지 알기도 전에 벽을 만난다. 그래서 첫 칸을 5스테이지 런 하나로
    ///   살 수 있게 낮추고, 덜어 낸 몫을 뒤 칸에 얹었다.
    ///
    /// ■ 마지막 칸은 벽이 아니어야 한다 (사용자 지시, 2026-09-10)
    ///   50,000 은 30스테이지 런 두 번이 오롯이 한 칸에 들어가는 값이라,
    ///   Epic 이 "언젠가" 가 아니라 "안 갈 곳" 이 됐다. 40,000 이면
    ///   런 한 번 반이다 — 멀지만 끝이 보인다.
    ///
    /// ■ 단계 배율 6.4× → 4.0× → 2.2×
    ///   ⚠ 뒤로 갈수록 <b>완만해진다</b>. 등급당 스탯은 +10% 로 일정하므로
    ///     값도 완만해지면 마지막 칸이 가장 값어치 있는 칸이 된다 —
    ///     그게 끝까지 올릴 이유다.
    ///
    /// ⚠ 수입 손잡이(RunGoldRule.GoldPerKill · HeroDeployment 의 부대·병사 수)를
    ///   고치면 이 표도 함께 볼 것.
    /// </summary>
    // ⚠ 10% 인상 · 10 단위 (사용자 지시, 2026-09-13)
    //   런 골드(RunGoldRule.CostBump)와 같은 폭으로 함께 올린다 — 한쪽만
    //   올리면 "런에서 쓰는 돈만 비싸고 영구 성장은 그대로" 가 된다.
    //   700 → 770 · 4,500 → 4,950 · 18,000 → 19,800 · 40,000 → 44,000
    //   (한 종족 Epic 까지 63,200 → 69,520)
    public static int CostFor(UnitGrade current) => current switch
    {
        UnitGrade.Normal   =>    770,   // 5스테이지 런 하나로 산다
        UnitGrade.Uncommon =>   4950,
        UnitGrade.Rare     =>  19800,
        UnitGrade.Unique   =>  44000,   // 30스테이지 런 약 1.5회
        _                  =>      0,   // Epic — 더 오를 곳이 없다
    };

    /// <summary>더 오를 수 있는 등급인가.</summary>
    public static bool IsMax(UnitGrade grade) => grade >= UnitGrade.Epic;

    /// <summary>지금 들고 있는 영구 골드.</summary>
    public static int Wallet
        => UserDataManager.Instance?.Get<ItemData>()?.Get(eItem.Gold) ?? 0;

    /// <summary>
    /// 왜 못 올리는가. 버튼 옆에 그대로 띄운다 — 비활성 버튼만 두면
    /// 돈이 모자란 건지 만렙인 건지 알 수 없다.
    /// </summary>
    public enum Blocked
    {
        None = 0,
        NotUnlocked,   // 아직 해금 안 됨
        MaxGrade,      // Epic
        NotEnoughGold, // 돈 부족
        InRun,         // 런이 도는 중 (CodexEditLock)
    }

    public static Blocked Check(string speciesId)
    {
        // ⚠ 가장 먼저 본다 — 런 중에는 해금·품질·지갑을 따질 것도 없다.
        //   이유는 CodexEditLock 머리 주석에 있다.
        if (CodexEditLock.Locked) return Blocked.InRun;

        var codex = UserDataManager.Instance.Get<MonsterCodexData>();

        if (!codex.IsUnlocked(speciesId)) return Blocked.NotUnlocked;

        UnitGrade grade = codex.GetGrade(speciesId);
        if (IsMax(grade)) return Blocked.MaxGrade;

        return Wallet >= CostFor(grade) ? Blocked.None : Blocked.NotEnoughGold;
    }

    /// <summary>
    /// 품질을 한 단계 올린다. 조건이 안 맞으면 <b>아무것도 하지 않고</b> false.
    ///
    /// ⚠ 돈을 먼저 낸다 — 소환 마나와 같은 순서
    ///   올리고 나서 내면, 잔액이 모자란 순간에 공짜로 오른다.
    /// </summary>
    public static bool TryUpgrade(string speciesId)
    {
        if (Check(speciesId) != Blocked.None) return false;

        var codex = UserDataManager.Instance.Get<MonsterCodexData>();
        int cost  = CostFor(codex.GetGrade(speciesId));

        if (!UserDataManager.Instance.Get<ItemData>().Spend(eItem.Gold, cost)) return false;

        bool raised = codex.ImproveGrade(speciesId);

        // ⚠ 돈을 냈는데 안 올랐다면 그건 버그다 — 조용히 넘기면 돈만 사라진다
        if (!raised)
            Debug.LogError($"[MonsterGradeUpgradeRule] '{speciesId}' 의 품질을 올리지 못했는데 " +
                           $"골드 {cost} 를 이미 냈습니다 — Check 와 ImproveGrade 의 판정이 어긋났습니다.");

        UserDataManager.Instance.RequestSave();
        return raised;
    }
}
