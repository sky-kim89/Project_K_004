using UnityEngine;

// ============================================================
//  RunGoldRule.cs
//  인게임 골드의 **모든 수치와 지급 규칙**이 있는 곳.
//
//  ■ ⚠ 런이 끝나면 사라지는 재화다 (사용자 확정, 2026-09-04)
//    지갑은 RunGoldData 이고, 환생·패배로 초기화된다. 남은 골드를 영구
//    재화나 환생 포인트로 바꿔 주지 않는다 — 그러면 "안 쓰고 모으기" 가
//    최적해가 되어 갈림길 선택이 무의미해진다.
//
//    ⚠ ItemData 의 eItem.Gold 와 헷갈리지 말 것. 그쪽은 원작에서 넘어온
//      **영구 재화**(용병 고용·장비 강화·환생)다. 이름만 같다.
//
//  ■ 버는 길은 하나다 — 용사를 잡는다
//    스테이지 클리어 보너스를 따로 주지 않는다. 이 게임은 적을 전멸시켜야
//    판이 끝나므로 "빨리 깨는 것" 과 "많이 잡는 것" 이 사실상 같은 말이다.
//    규칙이 하나면 화면에 설명할 것도 하나다.
//
//  ■ ⚠ 병사도 한 마리로 센다
//    초반 스테이지는 장수 없이 병사만 나온다(HeroDeployment 의 SoldiersOnly).
//    병사를 빼면 그 구간에서 골드가 아예 안 들어온다.
//    한 판의 수입 ≈ GoldPerKill × (부대 수 × 부대원 수) 이므로,
//    단가를 조절할 때 HeroDeployment 의 부대 수를 함께 볼 것.
//
//  ■ 엘리트·보스는 값이 다르다 (사용자 확정, 2026-09-04)
//    그 판의 보상은 **골드와 특성뿐**이다. 그 외 보상이 없으므로 골드 쪽에
//    무게가 실려야 "허들을 넘었다" 는 감각이 생긴다.
//
//  ■ 구독 지점은 RunBootstrap 하나다
//    BattleManager.OnUnitKilled 는 아군 사망에도 발생한다. 진영을 여기서
//    한 번만 가려 내면 갈라지는 자리가 한 곳으로 남는다.
// ============================================================

public static class RunGoldRule
{
    /// <summary>일반 용사 하나를 잡을 때 들어오는 골드.</summary>
    public const int GoldPerKill = 10;

    // ══════════════════════════════════════════════════════════
    //  ■ 값 단위 — **모든 골드 소비처가 이것의 배수다**
    //
    //  ⚠ 왜 필요한가 (사용자 지적, 2026-09-10)
    //    수입은 스테이지마다 커진다. 적 총량이 부대 수(10판에 5로 상한)와
    //    부대당 병사 수(스테이지마다 +1)의 곱이라, 30스테이지 한 판이
    //    170기쯤 = 1,700 골드다. 1스테이지는 40 남짓이다 — **40배** 차이다.
    //    그런데 소비처는 전부 고정가(40·70·90)였다. 그래서 중반부터 살 것을
    //    다 사고도 돈이 남고, 유일하게 반복 구매가 가능한 **마력의 정수만
    //    끝없이 사는** 상태가 됐다. 돈이 남는 것은 값이 싼 것이 아니라
    //    **값이 수입을 안 따라간 것**이다.
    //
    //  ⚠ 여기에 새 소비처의 값을 직접 적지 말 것
    //    Price(배수, 스테이지) 로 적는다. 그래야 수입 곡선을 고칠 때
    //    소비처 전부가 함께 따라온다 — 한 곳만 고쳐서 어긋나는 일이 없다.
    // ══════════════════════════════════════════════════════════

    /// <summary>1스테이지의 값 단위.</summary>
    public const int PriceUnitBase = 40;

    /// <summary>스테이지 1당 값 단위 상승분. 수입 증가분(스테이지당 약 +50)에 맞춘 값이다.</summary>
    public const int PriceUnitPerStage = 10;

    /// <summary>
    /// 이 스테이지의 값 한 단위. 대략 그 판 수입의 <b>5분의 1</b>이다.
    ///
    /// ⚠ 이벤트의 골드 갈래도 이 단위를 쓴다 (RunEventRule.GoldUnit 이 위임) —
    ///   주는 쪽과 받는 쪽이 같은 자를 써야 "이 골드가 얼마짜리인가" 가 성립한다.
    /// </summary>
    public static int PriceUnit(int stageNumber)
        => PriceUnitBase + PriceUnitPerStage * Mathf.Max(0, stageNumber - 1);

    /// <summary>
    /// 값 단위의 배수. <b>소비처의 값은 전부 이 함수로 적는다.</b>
    ///
    /// ⚠ 5 단위로 끊는다 — 화면에 뜨는 숫자라 337 보다 335 가 읽기 쉽다.
    /// </summary>
    // ── 물가·표기 단위 (사용자 지시, 2026-09-13) ──────────────
    //
    //  ⚠ 골드로 내는 값은 **전부** 이 둘을 지난다 (Price · Flat).
    //    한 곳에서 곱하고 한 곳에서 끊어야 "10% 올렸는데 여기만 그대로" 가 없다.

    /// <summary>모든 골드 값에 곱하는 물가. 1.10 = 10% 비싸다.</summary>
    public const float CostBump = 1.10f;

    /// <summary>값의 표기 단위. 화면에 뜨는 숫자라 10 단위로 딱 떨어져야 한다.</summary>
    public const int PriceStep = 10;

    /// <summary>10 단위 반올림 + 하한. 값을 만드는 함수는 전부 여기로 끝낸다.</summary>
    static int Round(float gold)
        => Mathf.Max(PriceStep, Mathf.RoundToInt(gold / PriceStep) * PriceStep);

    public static int Price(float multiple, int stageNumber)
    {
        // 유물 '안목' — 내는 값만 깎는다.
        //
        // ⚠ PriceUnit 이 아니라 여기서 곱한다
        //   이벤트의 골드 **보상**은 PriceUnit 을 본다. 단위 쪽에 걸면
        //   값을 깎는 유물이 받는 돈까지 깎아 아무 일도 안 한 것이 된다.
        // ⚠ 하한을 둔다 — 여러 단계를 찍어도 공짜가 되지는 않는다.
        float cut = 1f - RelicTreeApplier.GetSystemValue(RelicSystemEffect.ShopPriceCut);

        return Round(PriceUnit(stageNumber) * multiple * CostBump * Mathf.Clamp(cut, 0.4f, 1f));
    }

    /// <summary>
    /// <b>스테이지를 타지 않는</b> 고정 값. 유물 '안목' 할인과 5 단위 반올림은
    /// <see cref="Price"/> 와 똑같이 먹는다.
    ///
    /// ⚠ 이것은 예외다 — 소비처의 값은 원칙적으로 Price(배수, 스테이지) 로 적는다.
    ///   수입 곡선을 고치면 소비처가 함께 따라와야 하기 때문이다.
    ///   값이 <b>산 횟수로만</b> 올라야 하는 물건에만 쓴다
    ///   (상점 '전쟁 자금' — 사용자 지시, 2026-09-13).
    ///
    /// ⚠ 할인을 여기서 다시 짜지 않는다 — Price 와 같은 식이라야 "이 유물이
    ///   어떤 값에 걸리는가" 가 화면마다 갈리지 않는다.
    /// </summary>
    public static int Flat(int gold)
    {
        float cut = 1f - RelicTreeApplier.GetSystemValue(RelicSystemEffect.ShopPriceCut);

        return Round(gold * CostBump * Mathf.Clamp(cut, 0.4f, 1f));
    }

    /// <summary>엘리트 용사 하나. 일반의 몇 배인가.</summary>
    public const int EliteKillMultiplier = 3;

    /// <summary>보스 용사 하나. 일반의 몇 배인가.</summary>
    public const int BossKillMultiplier = 10;

    /// <summary>
    /// 유닛이 쓰러졌다 — 용사였다면 골드가 들어온다.
    /// <see cref="BattleManager.OnUnitKilled"/> 에 물린다.
    /// </summary>
    /// <param name="team">쓰러진 유닛의 진영.</param>
    public static void HandleUnitKilled(TeamType team)
    {
        // ⚠ 진영을 TeamType 으로 직접 비교하지 않는다 (CLAUDE.md 확정 규칙)
        //   이 게임은 원작과 진영이 뒤집혀 있어 TeamType.Enemy 가 '용사' 다.
        if (team != Faction.Hero) return;

        Grant(GoldPerKill);
    }

    /// <summary>
    /// 등급이 있는 용사가 쓰러졌다 — 엘리트·보스는 값이 다르다.
    ///
    /// ⚠ <see cref="HandleUnitKilled"/> 와 <b>둘 중 하나만</b> 부른다.
    ///   둘 다 부르면 한 마리가 두 번 값을 치른다.
    /// </summary>
    /// <returns>실제로 들어온 골드. 처치 자리에 띄우는 연출(GoldPickupLayer)이 쓴다.</returns>
    public static int GrantForHeroKill(bool isBoss, bool isElite)
    {
        int mult = isBoss  ? BossKillMultiplier
                 : isElite ? EliteKillMultiplier
                           : 1;

        return Grant(GoldPerKill * mult);
    }

    /// <summary>
    /// 고블린 '약탈' 이 떨구는 골드. <b>런 골드</b>다 (사용자 확정, 2026-09-06).
    ///
    /// ⚠ 한때 영구 골드(ItemData)에 바로 넣었다
    ///   그러면 몬스터를 많이 죽일수록 영구 재화가 느는데, 정작 이번 런의
    ///   갈림길에서는 쓸 수가 없어 "죽는 게 이득" 처럼 읽혔다.
    ///   지금은 런 골드로 들어오고, 번 총액이 런이 끝날 때 영구 골드로도
    ///   따라온다 (SettleToPermanent). 쓰고도 남는 구조다.
    /// </summary>
    public static void GrantLoot(int amount) => Grant(amount);

    /// <summary>
    /// 실제로 지갑에 넣는 유일한 지점. 유물 '약탈의 손' 이 여기서 얹힌다.
    ///
    /// ⚠ 얻는 길이 여럿이라(처치·약탈·이벤트) 반드시 이 한 곳에서 곱한다.
    ///   부르는 쪽마다 곱하면 어떤 길은 보너스를 못 받는다.
    /// </summary>
    /// <returns>보너스까지 얹어 실제로 지갑에 들어간 값. 연출이 그 숫자를 띄운다.</returns>
    public static int Grant(int amount)
    {
        float mult = 1f + RelicTreeApplier.GetSystemValue(RelicSystemEffect.GoldGainBonus);

        // 특성 '저주받은 금화' — 대가(판마다 성 −3)는 RunBootstrap.AdvanceStage 가 받는다.
        if (RunPerkRule.Has(RunPerk.CursedGold)) mult += RunPerkRule.CursedGoldBonus;

        int granted = Mathf.RoundToInt(amount * mult);
        UserDataManager.Instance?.Get<RunGoldData>()?.Add(granted);

        return granted;
    }

    /// <summary>
    /// 런이 끝났다 — 이번 런에 <b>번 총액</b>만큼 영구 골드를 함께 준다.
    /// 몬스터 도감의 품질 개선이 그 영구 골드를 쓴다.
    ///
    /// ■ 왜 '남은 잔액' 이 아니라 '번 총액' 인가 (사용자 확정, 2026-09-06)
    ///   잔액을 주면 "안 쓰고 모으기" 가 최적해가 되어 갈림길 선택이 죽는다.
    ///   번 총액은 쓰든 안 쓰든 같으므로, 런 안에서는 마음껏 쓰고 영구 성장은
    ///   **얼마나 잘 싸웠나**만 본다. 두 자원이 서로를 갉아먹지 않는다.
    ///
    /// ⚠ 런 종료에서 한 번만 부른다 (RunBootstrap.FinishRun)
    ///   Earned 는 다음 런 시작에서야 0 이 되므로, 두 번 부르면 두 번 준다.
    /// </summary>
    /// <returns>지급한 영구 골드. 결산 화면에 띄울 값이다.</returns>
    public static int SettleToPermanent()
    {
        var run = UserDataManager.Instance?.Get<RunGoldData>();
        if (run == null || run.Earned <= 0)
        {
            // ⚠ 진단 로그 (사용자 지적, 2026-09-11 "환생해도 500 고정") — 원인을 잡으면 지울 것
            Debug.Log($"[RunGoldRule] 영구 골드 결산 — 번 골드 없음 (Earned {run?.Earned ?? -1})");
            return 0;
        }

        int earned = run.Earned;
        var items  = UserDataManager.Instance.Get<ItemData>();
        items.Add(eItem.Gold, earned);

        Debug.Log($"[RunGoldRule] 영구 골드 결산 +{earned} → 보유 {items.Get(eItem.Gold)}");

        // ⚠ 결산한 지갑은 비운다 — 두 번 불려도 한 번만 준다 (2026-09-11)
        //   환생(UserDataManager.Reincarnate)과 런 종료(FinishRun)가 둘 다 부른다.
        run.SetDefaults();

        return earned;
    }

    /// <summary>지금 들고 있는 골드. 없으면 0.</summary>
    public static int Current
        => UserDataManager.Instance?.Get<RunGoldData>()?.Current ?? 0;

    /// <summary>
    /// 골드를 낸다. 모자라면 false — 부르는 쪽은 아무것도 하지 않는다.
    ///
    /// ⚠ 낸 뒤에 물건을 준다. 순서를 뒤집으면 잔액이 모자란 순간이 공짜가 된다.
    /// </summary>
    public static bool TrySpend(int cost)
        => UserDataManager.Instance?.Get<RunGoldData>()?.Spend(cost) ?? false;
}
