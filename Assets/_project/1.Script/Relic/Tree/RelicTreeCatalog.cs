using System.Collections.Generic;

// ============================================================
//  RelicTreeCatalog.cs
//  유물 테크트리 노드 표 — 이 파일이 트리의 정본이다.
//  ⚠ 개수를 글로 적지 않는다 — 2026-09-07 에 69 → 46 으로 갈아 끼웠는데
//    박아 둔 숫자만 옛말을 했다. 세려면 RelicTreeCatalog.All.Length 다.
//
//  ■ 읽는 법
//    Stat(...) = 스탯 노드, Sys(...) = 시스템 노드.
//    P(스탯, 값) = 비율(%) · A(스탯, 값) = 절대값(%p·명·포인트).
//    좌표는 (x, y), +Y 가 위다. 같은 x 면 dy ≥ 2, 같은 y 면 dx ≥ 2 (칸이 겹친다).
//
//  ■ 비용 (TierCost)
//    tier     0   1   2   3   4   5
//    CostBase 1   2   3   5   8  12    → 레벨업 비용 = CostBase × (레벨+1)
//    첫 레벨만 보면 2 / 3 / 5 / 8 / 12pt — 한 판 벌이로 여러 갈래를 동시에 건드릴 수 있다.
//    만렙: t1 Lv5 = 30 · t2 Lv5 = 45 · t3 Lv5 = 75 · t4 Lv4 = 80 · t5 Lv1 = 12 · 특수 = CostBase
//    전 노드 만렙 총합은 GrandTotalCost() 가 센다 — 여기에 적어 두면 옛말이 된다.
//
//    ⚠ 레벨을 늘리고 레벨당 수치를 낮췄다 (2026-08-25)
//      예전엔 t3 첫 레벨이 10pt, t4 가 20pt 라 한 번의 환생으로 노드 서너 개밖에
//      못 건드렸다. 트리는 "이번엔 어디를 넓힐까" 가 매 판 생겨야 재미가 있다.
//      만렙 총합은 거의 그대로 두고 계단만 잘게 쪼갠 것이다.
//
//  ■ 뿌리는 사방 4갈래뿐이다
//        위     소환수 (날카로운 발톱)
//        아래   마왕성 (두꺼운 성문)
//        왼쪽   마나   (넓은 그릇)
//        오른쪽 통솔   (약탈의 손 → 시간의 고삐 · 부름의 나팔)
//
//  ■ 스탯 노드는 전부 RelicTarget.Unit_Monster 다
//    ⚠ All 로 두면 같은 노드가 장수 경로(ApplyToGeneralStat)에도 걸려
//      **적(용사)까지 강화된다.** 이 게임에서 장수는 적이다.
//      Ensure() 끝의 Verify() 가 이걸 검사해 터뜨린다 — 조용히 새지 않게.
// ============================================================

public static class RelicTreeCatalog
{
    /// <summary>티어별 기본 비용. 인덱스 = Tier.</summary>
    public static readonly int[] TierCost = { 1, 2, 3, 5, 8, 12 };

    static RelicNodeDef[] _all;
    static Dictionary<RelicNodeId, RelicNodeDef>       _byId;
    static Dictionary<RelicNodeId, List<RelicNodeDef>> _children;

    public static RelicNodeDef[] All { get { Ensure(); return _all; } }

    public static RelicNodeDef Get(RelicNodeId id) { Ensure(); return _byId[id]; }

    /// <summary>자식 목록. 없으면 빈 리스트 (트리 말단).</summary>
    public static List<RelicNodeDef> ChildrenOf(RelicNodeId id)
    {
        Ensure();
        return _children.TryGetValue(id, out var list) ? list : Empty;
    }

    static readonly List<RelicNodeDef> Empty = new();

    // ── 해금 · 시야 ───────────────────────────────────────────

    /// <summary>부모를 1레벨 이상 찍었으면 이 노드를 살 수 있다. 뿌리는 항상 열려 있다.</summary>
    public static bool IsUnlocked(RelicNodeId id, IReadOnlyDictionary<RelicNodeId, int> levels)
    {
        var def = Get(id);
        if (def.Parent == RelicNodeId.None) return true;
        return levels.TryGetValue(def.Parent, out int lv) && lv >= 1;
    }

    /// <summary>
    /// 화면에 그릴지 여부. 해금됐거나 이미 찍은 노드만 보인다.
    ///
    /// ⚠ 해금되지 않은 노드는 이름도 효과도 노출하지 않는다
    ///   "다음에 뭐가 나올지 모른다" 가 이 트리의 설계 의도다.
    /// </summary>
    public static bool IsVisible(RelicNodeId id, IReadOnlyDictionary<RelicNodeId, int> levels)
        => IsUnlocked(id, levels) || levels.ContainsKey(id);

    /// <summary>안개 너머 실루엣 — 보이는 노드의 자식만 "뭔가 있다"까지 알려 준다.</summary>
    public static bool IsSilhouette(RelicNodeId id, IReadOnlyDictionary<RelicNodeId, int> levels)
    {
        var def = Get(id);
        if (def.Parent == RelicNodeId.None) return false;
        return !IsVisible(id, levels) && IsVisible(def.Parent, levels);
    }

    /// <summary>전 노드를 만렙까지 올리는 총 환생 포인트.</summary>
    public static int GrandTotalCost()
    {
        Ensure();
        int sum = 0;
        foreach (var d in _all) sum += d.TotalCost;
        return sum;
    }


    // ── 표 ────────────────────────────────────────────────────

    static void Ensure()
    {
        if (_all != null) return;
        var t = new List<RelicNodeDef>();
        _build = t;

        Stat(RelicNodeId.N_Origin, RelicNodeId.None, "마왕의 각인", RelicBranch.Root, 0,
            0, 0, 1, RelicTarget.Unit_Monster, P(StatType.Attack, 0.05f), P(StatType.MaxHp, 0.05f));
        Stat(RelicNodeId.N_Claw, RelicNodeId.N_Origin, "날카로운 발톱", RelicBranch.Brood, 1,
            0, 2, 5, RelicTarget.Unit_Monster, P(StatType.Attack, 0.04f));
        Stat(RelicNodeId.N_Carapace, RelicNodeId.N_Claw, "굳은 껍질", RelicBranch.Brood, 2,
            0, 4, 5, RelicTarget.Unit_Monster, P(StatType.MaxHp, 0.05f));
        Stat(RelicNodeId.N_Fang, RelicNodeId.N_Carapace, "사나운 이빨", RelicBranch.Brood, 3,
            -2, 6, 5, RelicTarget.Unit_Monster, P(StatType.Attack, 0.05f));
        Stat(RelicNodeId.N_ThickHide, RelicNodeId.N_Carapace, "두꺼운 가죽", RelicBranch.Brood, 3,
            2, 6, 5, RelicTarget.Unit_Monster, A(StatType.Defense, 0.02f));
        Stat(RelicNodeId.N_Predator, RelicNodeId.N_Fang, "포식자", RelicBranch.Brood, 4,
            -2, 8, 4, RelicTarget.Unit_Monster, A(StatType.CritChance, 0.03f), P(StatType.CritDamage, 0.12f));
        Stat(RelicNodeId.N_UndyingFlesh, RelicNodeId.N_ThickHide, "불사의 살점", RelicBranch.Brood, 4,
            2, 8, 4, RelicTarget.Unit_Monster, P(StatType.MaxHp, 0.10f));
        Stat(RelicNodeId.N_DemonHorde, RelicNodeId.N_Predator, "마왕의 군세", RelicBranch.Brood, 5,
            0, 10, 1, RelicTarget.Unit_Monster, P(StatType.Attack, 0.12f), P(StatType.MaxHp, 0.12f));
        // 소환수 계열의 **끝** — 기존 최종 노드를 대체하지 않고 그 뒤에 붙인다.
        // ⚠ 마릿수 +1 은 마나를 안 늘리고 물량을 늘리는 유일한 축이다
        //   (CLAUDE.md: "이 게임에서 가장 값이 센 손잡이"). 그래서 트리 전체에서
        //   가장 비싸다 — 30스테이지 런 약 0.7회분.
        Sys(RelicNodeId.N_EndlessSwarm, RelicNodeId.N_DemonHorde, "끝없는 무리", 5,
            0, 12, 1, RelicSystemEffect.SummonCountBonus, 1f, RelicBranch.Brood, cost: 200);
        // 마릿수 +1 의 **싼 판** (사용자 지시, 2026-09-12) — 절약(−1·100pt)과 마지막 한 방울(−1·200pt)의
        //   짝과 같은 모양이다. 둘을 다 찍으면 +2.
        Sys(RelicNodeId.N_SwarmCall, RelicNodeId.N_UndyingFlesh, "무리의 부름", 4,
            2, 10, 1, RelicSystemEffect.SummonCountBonus, 1f, RelicBranch.Brood, cost: 100);
        Sys(RelicNodeId.N_FeralMemory, RelicNodeId.N_Carapace, "야성의 기억", 2,
            -2, 4, 5, RelicSystemEffect.SpeciesPassivePower, 0.10f, RelicBranch.Brood);
        // 상태이상(중독·화상·역병) 피해 — 종족 패시브 줄기 아래가 제 자리다.
        // ⚠ 좌표 (2026-09-12 재배치) — 짝수 격자 · 갈래마다 제 구역 (소환수 위 · 마왕성 아래 ·
        //   마나 왼쪽 y=0 줄 · 통솔 오른쪽 y=0/2/4 세 줄). 같은 x 면 dy ≥ 2, 같은 y 면 dx ≥ 2.
        Sys(RelicNodeId.N_PlagueLore, RelicNodeId.N_FeralMemory, "역병의 지혜", 3,
            -4, 4, 4, RelicSystemEffect.DotDamageBonus, 0.15f, RelicBranch.Brood);
        Sys(RelicNodeId.N_SplitLegacy, RelicNodeId.N_FeralMemory, "분열의 유산", 3,
            -4, 6, 3, RelicSystemEffect.DerivedScaleBonus, 0.07f, RelicBranch.Brood);
        Sys(RelicNodeId.N_DeathToll, RelicNodeId.N_SplitLegacy, "죽음의 대가", 4,
            -4, 8, 4, RelicSystemEffect.DeathTriggerPower, 0.25f, RelicBranch.Brood);
        // ⚠ 값을 티어 밖에서 매긴다 (사용자 지적, 2026-09-10)
        //   런 종료 상자 +1 은 **런마다** 장비가 하나 더 쌓인다는 뜻이라,
        //   5pt 로는 찍지 않을 이유가 없는 노드였다.
        Sys(RelicNodeId.N_ForgeMemory, RelicNodeId.N_ThickHide, "대장간의 기억", 3,
            4, 6, 1, RelicSystemEffect.GearBoxBonus, 1f, RelicBranch.Brood, cost: 35);
        Sys(RelicNodeId.N_Bespoke, RelicNodeId.N_ForgeMemory, "맞춤 제작", 4,
            4, 8, 4, RelicSystemEffect.GearStatBonus, 0.20f, RelicBranch.Brood);
        // ⚠ 마왕성 체력이 18~26 → 54~78 로 3배가 됐다 (2026-09-10)
        //   +1/lv 은 그 위에서 1.5% 짜리라 한 레벨을 찍어도 아무 일이 없어 보였다.
        //   네 노드를 함께 ×2 했다 (합계 +21 → +42, 기본값의 약 60%).
        Sys(RelicNodeId.N_ThickGate, RelicNodeId.N_Origin, "두꺼운 성문", 1,
            0, -2, 5, RelicSystemEffect.CoreHpBonus, 2f, RelicBranch.Keep);
        Sys(RelicNodeId.N_SealedWall, RelicNodeId.N_ThickGate, "봉인된 성벽", 2,
            0, -4, 5, RelicSystemEffect.CoreHpBonus, 2f, RelicBranch.Keep);
        Sys(RelicNodeId.N_DemonMajesty, RelicNodeId.N_SealedWall, "마왕의 위엄", 3,
            -2, -4, 5, RelicSystemEffect.SummonerStrikeBonus, 0.02f, RelicBranch.Keep);
        // ⚠ 옛 효과는 CampHealBonus(야영지 회복 +1/lv)였다 (2026-09-10 교체)
        //   야영지 회복이 8 → 24 로 오르면서 +3 은 12% 짜리 곁다리가 됐고,
        //   무엇보다 **갈림길에서 야영지를 골라야만** 값을 했다.
        //   지금은 판을 넘기기만 하면 붙는다 — 이름 그대로 '재건' 이다.
        //   ⚠ 그릇을 넘지 못한다(RunCoreData.Heal 이 자른다). 저절로 멈추는 사면이다.
        Sys(RelicNodeId.N_Rebuild, RelicNodeId.N_SealedWall, "재건", 3,
            -2, -6, 3, RelicSystemEffect.CoreRegenPerStage, 1f, RelicBranch.Keep);
        Sys(RelicNodeId.N_UnbrokenKeep, RelicNodeId.N_SealedWall, "불락의 성", 4,
            0, -6, 4, RelicSystemEffect.CoreHpBonus, 4f, RelicBranch.Keep);
        Sys(RelicNodeId.N_EternalThrone, RelicNodeId.N_UnbrokenKeep, "영원한 옥좌", 5,
            0, -8, 1, RelicSystemEffect.CoreHpBonus, 6f, RelicBranch.Keep);
        Sys(RelicNodeId.N_TrialBaptism, RelicNodeId.N_ThickGate, "시련의 세례", 2,
            2, -2, 5, RelicSystemEffect.EnemyMaxHpReduction, 0.025f, RelicBranch.Keep);
        Sys(RelicNodeId.N_FearBrand, RelicNodeId.N_TrialBaptism, "공포의 각인", 3,
            4, -2, 5, RelicSystemEffect.EnemyAttackReduction, 0.025f, RelicBranch.Keep);
        Sys(RelicNodeId.N_SlowMarch, RelicNodeId.N_TrialBaptism, "느려진 진군", 3,
            2, -4, 5, RelicSystemEffect.EnemyMoveReduction, 0.05f, RelicBranch.Keep);
        Sys(RelicNodeId.N_WitherCurse, RelicNodeId.N_SlowMarch, "쇠약의 저주", 4,
            2, -6, 4, RelicSystemEffect.EnemyMaxHpReduction, 0.04f, RelicBranch.Keep);
        Sys(RelicNodeId.N_Disarm, RelicNodeId.N_FearBrand, "무력화", 4,
            4, -4, 4, RelicSystemEffect.EnemyAttackReduction, 0.04f, RelicBranch.Keep);
        Sys(RelicNodeId.N_DoomProphecy, RelicNodeId.N_WitherCurse, "몰락의 예언", 5,
            2, -8, 1, RelicSystemEffect.EnemyMaxHpReduction, 0.12f, RelicBranch.Keep);
        Sys(RelicNodeId.N_WideVessel, RelicNodeId.N_Origin, "넓은 그릇", 1,
            -2, 0, 5, RelicSystemEffect.ManaCapacityBonus, 0.04f, RelicBranch.Font);
        Sys(RelicNodeId.N_DeepSpring, RelicNodeId.N_WideVessel, "깊은 샘", 2,
            -4, 0, 5, RelicSystemEffect.ManaCapacityBonus, 0.05f, RelicBranch.Font);
        // ⚠ Lv5 × −0.5 → Lv1 × −1 · 100pt (사용자 지시, 2026-09-12)
        //   카드 원가가 전부 정수라 −0.5 는 반올림(0.5 올림)에 먹혀 **홀수 레벨이 아무 일도 안 했다**.
        //   또 만렙 −2.5(실효 −2)가 45pt 라, 200pt 짜리 '마지막 한 방울'(−1)과 값이 뒤집혀 있었다.
        //   지금은 둘이 −1 씩, 합쳐 −2 다.
        Sys(RelicNodeId.N_Frugality, RelicNodeId.N_WideVessel, "절약", 2,
            -4, -2, 1, RelicSystemEffect.SummonCostCut, 1f, RelicBranch.Font, cost: 100);
        Sys(RelicNodeId.N_FlowingMana, RelicNodeId.N_DeepSpring, "흐르는 마력", 3,
            -6, 0, 5, RelicSystemEffect.ManaRegenBonus, 0.08f, RelicBranch.Font);
        Sys(RelicNodeId.N_Patience, RelicNodeId.N_Frugality, "인내", 3,
            -6, -2, 3, RelicSystemEffect.OverloadRelief, 0.01f, RelicBranch.Font);
        Sys(RelicNodeId.N_EndlessWell, RelicNodeId.N_FlowingMana, "마르지 않는 우물", 4,
            -8, 0, 4, RelicSystemEffect.ManaCapacityBonus, 0.10f, RelicBranch.Font);
        Sys(RelicNodeId.N_MeditationCrystal, RelicNodeId.N_FlowingMana, "명상의 결정", 4,
            -8, 2, 4, RelicSystemEffect.ManaRegenBonus, 0.12f, RelicBranch.Font);
        Sys(RelicNodeId.N_InfiniteVessel, RelicNodeId.N_EndlessWell, "무한의 그릇", 5,
            -10, 0, 1, RelicSystemEffect.ManaCapacityBonus, 0.20f, RelicBranch.Font);
        // 마나 계열의 **끝** — 기존 최종 노드를 대체하지 않고 그 뒤에 붙인다.
        // ⚠ 소환 비용 −1 은 **모든 종족**에 걸리는 절대값이라 싼 카드일수록
        //   비율이 커진다(5마나 −20% · 13마나 −8%). 물량 종족을 여는 열쇠다.
        //   '절약'(−0.5/lv)과 더해지므로 둘을 다 찍으면 −3.5 다.
        Sys(RelicNodeId.N_LastDrop, RelicNodeId.N_InfiniteVessel, "마지막 한 방울", 5,
            -12, 0, 1, RelicSystemEffect.SummonCostCut, 1f, RelicBranch.Font, cost: 200);
        // ⚠ 통솔 계열의 뿌리는 **약탈의 손**이다 (사용자 지시, 2026-09-12)
        //   골드가 먼저, 그 바로 뒤에 시간의 고삐(배속)가 열린다 — 첫 환생 포인트로
        //   "돈을 더 벌고 판을 빨리 넘기는" 편의부터 사게 한다. 부름의 나팔은 약탈의 손 뒤로 물렀다.
        //   티어도 맞바꿨다 (약탈의 손 t2→t1 · 부름의 나팔 t1→t2) — 첫 칸이 싸야 첫 칸이다.
        //   ⚠ 옛 세이브에서 나팔만 찍고 약탈의 손이 0 이면 나팔이 잠긴 채 보인다 — 효과는 그대로 걸린다
        Sys(RelicNodeId.N_CallHorn, RelicNodeId.N_PlunderHand, "부름의 나팔", 2,
            4, 0, 5, RelicSystemEffect.DrainSpeedBonus, 0.05f, RelicBranch.Dominion);
        Sys(RelicNodeId.N_OpenGate, RelicNodeId.N_CallHorn, "성문 개방", 2,
            6, 0, 5, RelicSystemEffect.DrainSpeedBonus, 0.08f, RelicBranch.Dominion);
        // ⚠ 옛 효과는 CardChoiceCount(카드 선택지 +1)였다 (2026-09-10 교체)
        //   특성 '감식안' 과 같은 축인데 CardRewardPicker.MaxChoiceCount(4)가 특성
        //   몫만 계산에 넣어, 둘을 함께 들면 **이 노드가 통째로 무효**였다.
        //   같은 이름("값을 알아보는 눈")으로 축만 옮겼다.
        //   (아이콘 파일명은 **enum ID** 에서 나오므로 표시 이름은 자유롭게 바꿔도 된다)
        Sys(RelicNodeId.N_Insight, RelicNodeId.N_CallHorn, "안목", 2,
            6, 2, 3, RelicSystemEffect.ShopPriceCut, 0.06f, RelicBranch.Dominion);
        // ⚠ '증원의 인장'(마릿수 +1, 8pt)은 지웠다 (사용자 지시, 2026-09-10)
        //   마릿수 +1 은 CLAUDE.md 가 "이 게임에서 가장 값이 센 축" 이라고 못박은
        //   것인데 트리에서 가장 싼 축에 속했다. 효과는 소환수 계열의 **최종
        //   노드**(마왕의 군세)로 옮기고 값을 200pt 로 매겼다.
        //   ⚠ 저장된 레벨은 그냥 무시된다 — RelicTreeApplier 는 카탈로그에 있는
        //     노드만 훑는다. 아이콘 PNG 는 고아로 남으니 '유물 트리 점검' 으로 치울 것.
        Stat(RelicNodeId.N_Sprint, RelicNodeId.N_OpenGate, "질주", RelicBranch.Dominion, 3,
            8, 0, 5, RelicTarget.Unit_Monster, P(StatType.MoveSpeed, 0.08f));
        // ⚠ 옛 효과는 SynergyStepCut(동 문턱 −1/lv)이었다 (2026-09-10 교체)
        //   Lv3 이면 −3 이라 동 문턱(2~3)이 Max(1,…) 에 걸려 **1마리로 동이 켜졌다.**
        //   "계열을 모으면 켜진다" 라는 시너지의 전제를 통째로 없애는 값이었다.
        //
        //   ⚠ 덱 칸(+1)으로 바꿨다가 되돌렸다 (사용자 지적, 2026-09-10)
        //     칸을 영구히 얹으면 특성 '확장 편성'(+2)이 쓸모를 잃는다 —
        //     그 특성의 존재 이유가 통째로 "이미 갖고 있는 것" 이 된다.
        //     유물이 런 특성의 자리를 빼앗으면 안 된다.
        //
        //   지금은 자식(공명의 서)의 약한 판이다. 같은 축의 강화 버전이 위아래로
        //   서는 것은 이 트리의 기본 모양이다 (발톱→이빨, 껍질→살점과 같다).
        Sys(RelicNodeId.N_Attunement, RelicNodeId.N_Insight, "조율", 3,
            8, 2, 3, RelicSystemEffect.SynergyStackBonus, 0.01f, RelicBranch.Dominion);
        Sys(RelicNodeId.N_ResonanceTome, RelicNodeId.N_Attunement, "공명의 서", 4,
            10, 2, 4, RelicSystemEffect.SynergyStackBonus, 0.02f, RelicBranch.Dominion);
        Sys(RelicNodeId.N_LegacyPrice, RelicNodeId.N_Insight, "전승의 대가", 4,
            6, 4, 2, RelicSystemEffect.PerkChoiceCount, 1f, RelicBranch.Dominion);
        // ⚠ 값을 티어 밖에서 매긴다 (사용자 지적, 2026-09-10)
        //   "새 카드가 Lv2 로 들어온다" 는 **카드마다 스테이지 클리어 한 번**을
        //   공짜로 얹어 주는 것과 같다. 8pt 는 그 값이 아니다.
        Sys(RelicNodeId.N_EarlyBloom, RelicNodeId.N_LegacyPrice, "조기 성장", 4,
            8, 4, 1, RelicSystemEffect.NewCardLevel, 1f, RelicBranch.Dominion, cost: 60);
        // ⚠ 덱 칸 노드를 다시 넣었다 (사용자 지시, 2026-09-11)
        //   위 '조율' 주석처럼 한 번 되돌렸던 효과다. 소환사마다 칸이 4~6 으로 갈리면서
        //   좁은 소환사가 영구히 넓어질 길이 필요해졌다 — 대신 **최대 2레벨**이고 값이 비싸다
        //   (Lv1 60 · Lv2 120). 상한 8 (RunPerkRule.MaxDeckSlots)에서 잘린다.
        //   ⚠ 좌표 (10,-2) — 같은 y 의 조기 성장(8,-2)과 dx 2, x=10 은 비어 있다.
        Sys(RelicNodeId.N_WarCamp, RelicNodeId.N_EarlyBloom, "전열 확장", 5,
            10, 4, 2, RelicSystemEffect.DeckSlotBonus, 1f, RelicBranch.Dominion, cost: 60);
        // ⚠ 무작위 특성을 쥐고 시작한다 (사용자 지시, 2026-09-12) — **최대 2레벨**
        //   '전승의 대가'(특성 선택지 +1)와 같은 축이라 그 아래에 붙인다.
        //   ⚠ 값을 티어 밖에서 매긴다 — 특성 한 장은 **런마다** 공짜로 얹히는 축이라
        //     티어 값(12pt)으로는 안 찍을 이유가 없는 노드가 된다.
        //     전열 확장(60/120)보다 조금 위에 둔다 — 70/140, 합 210pt.
        //   ⚠ 좌표 (4,4) — 같은 y 의 찰나의 지배(2,4)·전승의 대가(6,4)와 dx 2,
        //     같은 x 의 부름의 나팔(4,0)·영혼의 항아리(4,2)와 dy 2 이상.
        Sys(RelicNodeId.N_OldPact, RelicNodeId.N_LegacyPrice, "오래된 계약", 5,
            4, 4, 2, RelicSystemEffect.StartingPerkCount, 1f, RelicBranch.Dominion, cost: 70);
        // 통솔 계열의 뿌리 — 위 부름의 나팔 주석 참고 (2026-09-12)
        //   ⚠ 좌표 검산: (2,0) 은 옛 나팔 자리 · 고삐 (2,2) · 찰나 (2,4) · 항아리 (4,4) · 나팔 (4,0) · 안목 (4,-2)
        //     같은 x 는 dy 2, 같은 y 는 dx 2 이상 — 시련의 세례 (2,-2) · 성문 개방 (4,2) 와도 벌어져 있다
        Sys(RelicNodeId.N_PlunderHand, RelicNodeId.N_Origin, "약탈의 손", 1,
            2, 0, 5, RelicSystemEffect.GoldGainBonus, 0.12f, RelicBranch.Dominion);
        Sys(RelicNodeId.N_SoulUrn, RelicNodeId.N_PlunderHand, "영혼의 항아리", 3,
            4, 2, 5, RelicSystemEffect.ReincarnPointBonus, 0.12f, RelicBranch.Dominion);
        Sys(RelicNodeId.N_TimeReins, RelicNodeId.N_PlunderHand, "시간의 고삐", 2,
            2, 2, 1, RelicSystemEffect.BattleSpeedUnlock, 1f, RelicBranch.Dominion);
        // ⚠ 값을 티어 밖에서 매긴다 (사용자 확정, 2026-09-10)
        //   '시간의 고삐'(1.5배)는 싸도 된다 — 판을 빨리 넘기는 편의다.
        //   그 위 단계는 편의를 넘어 **체감 난이도**를 바꾸므로 값이 붙는다.
        Sys(RelicNodeId.N_MomentMastery, RelicNodeId.N_TimeReins, "찰나의 지배", 4,
            2, 4, 1, RelicSystemEffect.BattleSpeedUnlock, 1f, RelicBranch.Dominion, cost: 40);

        _all      = t.ToArray();
        _byId     = new Dictionary<RelicNodeId, RelicNodeDef>(_all.Length);
        _children = new Dictionary<RelicNodeId, List<RelicNodeDef>>();
        foreach (var d in _all)
        {
            _byId[d.Id] = d;
            if (d.Parent == RelicNodeId.None) continue;
            if (!_children.TryGetValue(d.Parent, out var list))
                _children[d.Parent] = list = new List<RelicNodeDef>();
            list.Add(d);
        }
        _build = null;

        Verify();
    }

    /// <summary>
    /// 표가 이 게임의 규칙을 지키는지 검사한다. 어기면 그 자리에서 터진다.
    ///
    /// ■ 왜 검사가 필요한가
    ///   여기서 어긋나면 **에러가 안 난다.** 노드는 화면에 멀쩡히 뜨고
    ///   숫자도 붙는데, 그 몫이 엉뚱한 진영으로 가거나 설명만 빈칸이 된다.
    ///   2026-09-07 트리 교체에서 실제로 46개 중 29개가 설명 빈칸이었다.
    /// </summary>
    static void Verify()
    {
        foreach (var d in _all)
        {
            // ⚠ 스탯 노드는 전부 몬스터 것이다
            //   All 로 두면 같은 노드가 장수 경로(RelicTreeApplier.ApplyToGeneralStat)에도
            //   걸린다. 이 게임에서 장수는 **적(용사)** 이라, 유물을 찍을수록 적이 세진다.
            if (!d.IsSystem && d.Target != RelicTarget.Unit_Monster)
                throw new System.InvalidOperationException(
                    $"[RelicTreeCatalog] '{d.Name}' 의 Target 이 {d.Target} 다. " +
                    "스탯 노드는 반드시 RelicTarget.Unit_Monster — 아니면 적까지 강화된다.");

            // ⚠ 설명 줄이 없으면 팝업에서 효과가 빈칸으로 뜬다
            //   읽는 쪽(GetSystemValue 소비처)을 만들어도 이걸 빠뜨리면
            //   "찍으면 뭐가 되는지" 를 말하지 않는 노드가 된다.
            if (!d.HasEffectText)
                throw new System.InvalidOperationException(
                    $"[RelicTreeCatalog] '{d.Name}'({d.System}) 의 설명 줄이 없다. " +
                    "RelicNodeDef.SystemLine 에 그 효과를 추가할 것.");
        }

        // ⚠ 칸이 겹치면 화면에서 두 노드가 포개진다 (좌표 규칙: 같은 x 면 dy ≥ 2)
        //   노드를 옮기거나 새로 넣을 때 눈으로 검산하던 것을 여기로 옮겼다 —
        //   겹쳐도 에러가 안 나고, 트리에서 한 노드가 통째로 안 보일 뿐이다.
        for (int i = 0; i < _all.Length; i++)
        for (int j = i + 1; j < _all.Length; j++)
        {
            var a = _all[i];
            var b = _all[j];

            bool clash = (a.X == b.X && System.Math.Abs(a.Y - b.Y) < 2)
                      || (a.Y == b.Y && System.Math.Abs(a.X - b.X) < 2);

            if (clash)
                throw new System.InvalidOperationException(
                    $"[RelicTreeCatalog] '{a.Name}'({a.X},{a.Y}) 와 '{b.Name}'({b.X},{b.Y}) 의 " +
                    "칸이 겹친다 — 같은 x 면 dy ≥ 2, 같은 y 면 dx ≥ 2 여야 한다.");
        }
    }

    // ── 표 작성 헬퍼 ──────────────────────────────────────────

    static List<RelicNodeDef> _build;

    /// <summary>비율(%) 스탯 한 줄.</summary>
    static RelicNodeStat P(StatType s, float v) => new() { Stat = s, PerLevel = v, Absolute = false };

    /// <summary>절대값(%p·명·포인트) 스탯 한 줄.</summary>
    static RelicNodeStat A(StatType s, float v) => new() { Stat = s, PerLevel = v, Absolute = true };

    static void Stat(RelicNodeId id, RelicNodeId parent, string name, RelicBranch branch, int tier,
                     int x, int y, int maxLevel, RelicTarget target, params RelicNodeStat[] stats)
        => _build.Add(new RelicNodeDef
        {
            Id = id, Parent = parent, Name = name, Branch = branch, Tier = tier,
            X = x, Y = y, MaxLevel = maxLevel, CostBase = TierCost[tier],
            Target = target, Stats = stats, System = RelicSystemEffect.None,
        });

    /// <summary>
    /// 시스템 노드 (스탯이 아닌 것을 바꾸는 노드).
    ///
    /// ⚠ branch 를 반드시 넘길 것 — 기본값을 없앴다 (2026-09-07)
    ///   원작에는 '유틸' 계열이 있어 기본값이 통했지만, 지금은 네 갈래가
    ///   전부 시스템 노드를 갖는다. 기본값을 두면 색·자리가 엉뚱한 계열로 잡힌다.
    /// </summary>
    /// <param name="cost">
    /// 0 이면 티어 값(TierCost)을 쓴다. 0 보다 크면 <b>그 값이 곧 레벨업 기준가</b>다.
    ///
    /// ■ ⚠ 왜 예외가 필요한가 (사용자 지적, 2026-09-10)
    ///   값을 티어로만 매기면 <b>같은 티어의 노드는 전부 같은 값</b>이 된다.
    ///   그런데 티어는 "트리에서 얼마나 깊은가" 지 "얼마나 센가" 가 아니다.
    ///   실제로 4티어 8pt 짜리 '증원의 인장'(마릿수 +1)이 같은 값의
    ///   '조기 성장'(새 카드 Lv +1)과 나란히 있었는데, 마릿수 +1 은
    ///   CLAUDE.md 가 "이 게임에서 가장 값이 센 축" 이라고 못박은 것이다.
    ///   판을 통째로 바꾸는 한두 노드는 티어 밖에서 값을 매긴다.
    ///
    /// ⚠ 남용하지 말 것 — 예외가 흔해지면 티어 표가 거짓말이 된다.
    ///   지금 값을 따로 매긴 것은 다섯뿐이고, 전부 "한 번 찍으면 판이 달라지는" 것이다.
    /// </param>
    static void Sys(RelicNodeId id, RelicNodeId parent, string name, int tier,
                    int x, int y, int maxLevel, RelicSystemEffect effect, float perLevel,
                    RelicBranch branch, int cost = 0)
        => _build.Add(new RelicNodeDef
        {
            Id = id, Parent = parent, Name = name, Branch = branch, Tier = tier,
            X = x, Y = y, MaxLevel = maxLevel,
            CostBase = cost > 0 ? cost : TierCost[tier],
            Target = RelicTarget.All, Stats = new RelicNodeStat[0],
            System = effect, SystemPerLevel = perLevel,
        });
}
