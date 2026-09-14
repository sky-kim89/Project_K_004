using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  RunNode.cs
//  스테이지와 스테이지 **사이**의 갈림길 — 종류·값·후보 뽑기의 정본.
//
//  ■ ⚠ 고른 것이 **다음 스테이지 그 자체**다 (2026-09-04 확정)
//    시설을 고르면 그 판에는 전투가 없다. 스테이지 번호는 그대로 올라가므로
//    **골드 수입 한 판을 포기하고 정비하는** 거래가 된다.
//
//    한때는 매 판 끝에 시설을 하나씩 얹었다. 그러면 갈림길만으로 빌드가
//    완성돼 카드 3택·보스 보상이 곁다리가 되고, 스무 판이면 시설을 스무 번
//    지난다. 지금은 시설을 지날 때마다 전투 하나를 잃는다.
//
//  ■ 후보는 **일곱 중 둘**이다 (사용자 확정, 2026-09-04)
//    전투 둘, 시설 다섯이 같은 표에서 가중치로 뽑힌다. [야영지][제단] 처럼
//    둘 다 시설인 판도 나온다 — 대신 일반 전투의 가중치를 가장 높게 두어
//    그런 판이 드물게 오도록 만든다.
//
//  ■ ⚠ 엘리트가 뽑히면 짝은 **반드시 일반 전투**다
//    "엘리트는 피할 수 있지만 전투는 피할 수 없다" 가 규칙이다.
//    [엘리트][야영지] 가 뜨면 엘리트를 피하려다 전투를 통째로 건너뛰게 되어,
//    험로를 넣은 판이 오히려 가장 편한 판이 된다.
//
//  ■ 보스판에는 갈림길이 없다
//    5의 배수는 피할 수 없는 관문이다 (RunStageKindRule.IsBossStage).
//
//  ■ 골드는 여기서만 나간다
//    전투에는 마나가 있고 갈림길에는 골드가 있다. 두 자원이 서로의 단위로
//    환전되지 않아야 각자 결정을 만든다 — 골드로 마나를 사지 않는 이유다.
//    (강화소가 카드의 **비용**을 깎는 것은 환전이 아니라 규칙 변경이다)
//
//  ■ 허들 뒤에는 갈림길이 없다
//    보스·엘리트 판이 끝나면 특성 보상이 이미 붙는다. 거기에 갈림길까지
//    얹으면 한 판 끝에 고를 것이 셋이 되어 어느 것도 무게가 없어진다.
// ============================================================

public enum RunNodeKind
{
    /// <summary>상점 — 최대 마나를 골드로 산다.</summary>
    Shop = 0,

    /// <summary>이벤트 — 마왕성 체력을 값으로 치른다. 표는 RunEventRule.</summary>
    Event = 1,

    /// <summary>야영지 — 마왕성을 고치거나 더 튼튼하게 만든다.</summary>
    Camp = 2,

    /// <summary>제단 — 카드를 제물로 바쳐 시너지 카운트를 남긴다.</summary>
    Altar = 3,

    /// <summary>강화소 — 카드 하나의 소환 비용을 깎거나 마릿수를 늘린다.</summary>
    Forge = 4,

    // ── 전투 — 고르면 그 판이 전투가 된다 ────────────────────

    /// <summary>일반 전투. 안전한 골드 수입.</summary>
    NormalBattle = 5,

    /// <summary>엘리트 전투. 부대가 한 줄 늘고 엘리트 용사가 섞이지만, 특성 1택 + 골드 ×3.</summary>
    EliteBattle = 6,
}

public static class RunNodeRule
{
    /// <summary>갈림길에 세우는 후보 수.</summary>
    public const int ChoiceCount = 2;

    // ── 야영지 ──────────────────────────────────────────────

    /// <summary>
    /// 야영지 — 마왕성 체력 회복량. <b>야영지는 많이 회복한다</b> (사용자 확정, 2026-09-08).
    ///
    /// ⚠ 이벤트의 회복 갈래와 폭이 뚜렷해야 한다
    ///   마왕성 체력은 소환사마다 18~26 뿐이다(BaseCoreHp 10 + 체력×2). 5 였을 때는
    ///   이벤트 회복(3)과 두 칸 차이라 **둘 다 "조금 회복" 으로 읽혔고**, 야영지를
    ///   고를 이유가 "증축이 있으니까" 뿐이었다. 8 이면 최대치의 3분의 1 이상이라
    ///   "여기서 크게 숨을 돌린다" 가 된다.
    ///   ⚠ 이벤트 쪽은 여기서 파생된다 — RunEventRule.HealAmount 를 함께 볼 것.
    ///
    /// ⚠ 마왕성 체력이 3배가 되면서 함께 3배가 됐다 (2026-09-10)
    ///   비율(최대치의 약 3분의 1)을 지킨 것이다. 위 주석의 근거가 비율이므로
    ///   최대 체력을 건드릴 때는 이 값도 같이 옮겨야 한다.
    /// </summary>
    public const int CampHealAmount = 24;

    /// <summary>야영지 — 최대 체력 증가량. 최대치의 10% 남짓이다.</summary>
    public const int CampMaxAmount = 9;

    /// <summary>야영지 수리 — 마왕성과 함께 채우는 마나 (최대 마나 비율, 사용자 지시 2026-09-12).</summary>
    public const float CampManaRatio = 0.33f;

    /// <summary>야영지 증축 — 마왕성 최대 체력과 함께 늘어나는 최대 마나 (사용자 지시 2026-09-12).</summary>
    public const int CampMaxManaAmount = 6;

    /// <summary>
    /// 야영지 — 최대 체력 증축 값. 영구라 회복(무료)보다 비싸다.
    ///
    /// ⚠ 고정가로 두지 않는다 (2026-09-10) — RunGoldRule.Price 주석 참고.
    /// </summary>
    public static int CampMaxCost(int stageNumber) => RunGoldRule.Price(2.5f, stageNumber);

    // ── 강화소 ──────────────────────────────────────────────

    /// <summary>강화소 — 소환 비용을 깎는 양.</summary>
    public const int ForgeManaCut = 1;

    /// <summary>강화소 — 늘려 주는 소환 마릿수.</summary>
    public const int ForgeExtraSummons = 1;

    /// <summary>
    /// 강화소 — 값. 둘 다 같은 값이다.
    ///
    /// ⚠ 마릿수 쪽을 싸게 두지 말 것
    ///   마릿수는 마나를 안 늘리고 물량을 늘리므로 이 게임에서 가장 값이 센
    ///   축이다(RunPerkRule 의 친화 증원 주석과 같은 이유). 비용 −1 과 값이
    ///   같아야 "무엇을 고를까" 가 성립한다.
    ///
    /// ⚠ 고정가로 두지 않는다 (2026-09-10) — RunGoldRule.Price 주석 참고.
    /// </summary>
    public static int ForgeCost(int stageNumber) => RunGoldRule.Price(2f, stageNumber);

    // ── 제단 ────────────────────────────────────────────────

    // ── 상점 ────────────────────────────────────────────────
    //
    //  ⚠ 값과 재고의 정본은 RunShopRule 이다 — 여기에 두지 않는다 (2026-09-10)
    //    ShopManaAmount / ShopManaCost 가 여기 남아 있었는데, 실제로 읽는 곳은
    //    아무 데도 없고 상점은 RunShopRule.EssenceManaAmount / EssencePrice 를
    //    썼다. 같은 것을 말하는 상수가 둘이면 한쪽만 고쳐지는 날이 온다.

    /// <summary>
    /// 제단 — 골드 값.
    ///
    /// ⚠ 0 이다. 대가는 **카드 한 장**이다
    ///   덱이 6칸(확장 편성으로 8)뿐이라 칸 하나를 내주는 것이 이미 무겁다.
    ///   골드까지 받으면 아무도 안 쓰는 시설이 된다.
    ///   값을 매기고 싶으면 이 상수만 올리면 된다.
    /// </summary>
    public const int AltarCost = 0;

    // ── 표시 ────────────────────────────────────────────────

    /// <summary>전투 노드인가 — 고르면 그 판이 전투가 된다.</summary>
    public static bool IsBattle(this RunNodeKind kind)
        => kind is RunNodeKind.NormalBattle or RunNodeKind.EliteBattle;

    /// <summary>전투 노드가 만들 스테이지 성격.</summary>
    public static RunStageKind ToStageKind(this RunNodeKind kind)
        => kind == RunNodeKind.EliteBattle ? RunStageKind.Elite : RunStageKind.Normal;

    public static string ToKorean(this RunNodeKind kind) => kind switch
    {
        RunNodeKind.Shop         => "상점",
        RunNodeKind.Event        => "이벤트",
        RunNodeKind.Camp         => "야영지",
        RunNodeKind.Altar        => "제단",
        RunNodeKind.Forge        => "강화소",
        RunNodeKind.NormalBattle => "진군",
        RunNodeKind.EliteBattle  => "험로",
        _                        => "",
    };

    /// <summary>
    /// 갈림길 카드에 적히는 한 줄.
    ///
    /// ⚠ 스테이지를 받는다 (2026-09-10) — 값이 스테이지를 따라 오르므로
    ///   (RunGoldRule.Price) 고정 숫자를 적으면 카드가 거짓말을 한다.
    /// </summary>
    public static string Describe(this RunNodeKind kind, int stageNumber) => kind switch
    {
        RunNodeKind.Shop  => "카드·특성·마력의 정수를 골드로 산다",

        // ⚠ 규칙을 적는다 — 이벤트만 골드가 아니라 **마왕성 체력**으로 산다.
        //   그것이 갈림길에서 이벤트를 고르는 이유이므로 카드에 적혀야 한다.
        RunNodeKind.Event => "무엇이 일어날지 모른다. 값은 마왕성 체력으로 치른다",
        RunNodeKind.Camp  => $"마왕성 수리(무료) 또는 증축 {CampMaxCost(stageNumber)} G",
        RunNodeKind.Altar => "카드 하나를 제물로 바쳐 시너지 카운트를 남긴다",
        RunNodeKind.Forge => $"골드 {ForgeCost(stageNumber)} 로 카드 하나를 강화한다",

        RunNodeKind.NormalBattle => "평범한 용사 부대. 잡은 만큼 골드가 들어온다",
        RunNodeKind.EliteBattle  =>
            $"엘리트 용사가 섞이고 부대가 한 줄 는다. 대신 특성 1택 + 처치 골드 ×{RunGoldRule.EliteKillMultiplier}",

        _ => "",
    };

    // ── 후보 뽑기 ───────────────────────────────────────────

    /// <summary>
    /// 갈림길 후보 표 — 일곱 종류와 각자의 가중치.
    ///
    /// ■ 일반 전투가 가장 높다 (사용자 확정)
    ///   나머지를 다 합친 것에는 못 미치되 단일로는 가장 크다. 그래야
    ///   "가끔 정비할 기회가 온다" 로 읽히고, 시설이 흔해지면 전투가 곁다리가 된다.
    ///
    /// ⚠ 아직 안 만든 것은 <see cref="Implemented"/> 가 걸러 낸다
    ///   목록에 띄워 놓고 눌렀을 때 아무 일도 안 일어나면, 플레이어는
    ///   갈림길 하나를 통째로 버린 셈이 된다. 여기서 지우지 말고 그쪽을 고칠 것 —
    ///   가중치는 시설이 다 만들어진 뒤의 값이어야 밸런스를 볼 수 있다.
    /// </summary>
    /// <summary>
    /// 그림 배열의 순서 정본. (MonsterSynergyRule.AllTags 와 같은 계약이다)
    ///
    /// ⚠ 셋이 이 순서 하나를 공유한다 — 갈림길 그림 PNG · Creator 가 박는
    ///   Sprite[] · 런타임 조회(IndexOf). 중간에 끼워 넣으면 "야영지인데
    ///   상점 그림이 뜨는" 상태가 된다. <b>뒤에만 추가한다.</b>
    /// </summary>
    public static readonly RunNodeKind[] AllKinds =
    {
        RunNodeKind.NormalBattle,
        RunNodeKind.EliteBattle,
        RunNodeKind.Camp,
        RunNodeKind.Forge,
        RunNodeKind.Shop,
        RunNodeKind.Altar,
        RunNodeKind.Event,
    };

    /// <summary>
    /// 표식 묶음에서 하나를 무작위로 고른다. 없으면 None. 제단이 쓴다.
    ///
    /// ⚠ 정본 순서(MonsterSynergyRule.AllTags)를 훑는다 —
    ///   비트를 직접 세면 표식이 늘 때 조용히 어긋난다.
    /// </summary>
    public static MonsterTag PickRandomTag(MonsterTag tags)
    {
        var owned = new System.Collections.Generic.List<MonsterTag>(4);

        foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
            if ((tags & tag) != 0) owned.Add(tag);

        return owned.Count == 0
            ? MonsterTag.None
            : owned[UnityEngine.Random.Range(0, owned.Count)];
    }

    /// <summary>AllKinds 안에서의 자리. 없으면 −1 (그림을 숨기라는 뜻이다).</summary>
    public static int IndexOf(RunNodeKind kind)
    {
        for (int i = 0; i < AllKinds.Length; i++)
            if (AllKinds[i] == kind) return i;

        return -1;
    }

    static readonly (RunNodeKind Kind, int Weight)[] Table =
    {
        (RunNodeKind.NormalBattle, 30),
        (RunNodeKind.EliteBattle,  10),
        (RunNodeKind.Camp,         12),
        (RunNodeKind.Forge,        12),
        (RunNodeKind.Shop,         12),
        (RunNodeKind.Altar,        10),
        (RunNodeKind.Event,        14),
    };

    /// <summary>
    /// 그 시설이 실제로 열리는가. 아직 못 만든 것은 후보에서 빠진다.
    ///
    /// ⚠ 지금은 일곱이 다 열린다 (이벤트 구현 2026-09-08).
    ///   새 종류를 표에 적기만 하고 화면을 안 만들었을 때 **여기에 한 줄**을
    ///   두는 것이 규칙이다 — 표에서 지우면 가중치를 다시 잡아야 하고,
    ///   그냥 두면 눌렀을 때 아무 일도 안 일어나 갈림길 한 칸을 버린다.
    /// </summary>
    static bool Implemented(RunNodeKind kind) => true;

    /// <summary>
    /// 갈림길 후보 <see cref="ChoiceCount"/> 개를 뽑는다.
    ///
    ///   · 일곱 종류에서 가중치로 뽑는다. 둘 다 시설인 판도 나온다.
    ///   · <b>[일반][일반] 도 나온다</b> — 그때는 그냥 싸우는 판이다.
    ///   · 그 밖의 중복은 막는다 — [야영지][야영지] 는 고를 것이 없는 통보다.
    ///   · <b>엘리트가 뽑히면 짝은 반드시 일반 전투다.</b>
    ///
    /// ⚠ 마지막 규칙을 빼면 안 된다
    ///   [엘리트][야영지] 가 뜨면 엘리트를 피하는 순간 전투가 통째로 사라져,
    ///   험로를 넣은 판이 오히려 가장 편한 판이 된다.
    ///   엘리트는 피할 수 있어야 하지만 **전투는 피할 수 없어야** 한다.
    /// </summary>
    public static List<RunNodeKind> Pick()
    {
        var pool = new List<(RunNodeKind Kind, int Weight)>(Table.Length);

        foreach (var row in Table)
            if (Implemented(row.Kind)) pool.Add(row);

        var result = new List<RunNodeKind>(ChoiceCount);
        if (pool.Count == 0) return result;

        RunNodeKind first = pool[DrawIndex(pool)].Kind;
        result.Add(first);

        // ⚠ 두 번째는 **일반 전투만** 중복을 허용한다
        //   [일반][일반] 은 "이번 판은 그냥 싸운다" 는 뜻이라 선택지로 성립하지만,
        //   [야영지][야영지] 는 같은 화면이 두 칸을 차지한 것뿐이다.
        if (first != RunNodeKind.NormalBattle)
            pool.RemoveAll(row => row.Kind == first);

        if (pool.Count > 0) result.Add(pool[DrawIndex(pool)].Kind);

        ForceBattlePartner(result);

        return result;
    }

    /// <summary>
    /// 엘리트가 끼어 있으면 짝을 일반 전투로 바꾼다.
    ///
    /// 이미 [엘리트][일반] 이면 아무것도 하지 않는다.
    /// ⚠ 엘리트는 중복이 막혀 있으므로 [엘리트][엘리트] 는 오지 않는다.
    /// </summary>
    static void ForceBattlePartner(List<RunNodeKind> picked)
    {
        int elite = picked.IndexOf(RunNodeKind.EliteBattle);
        if (elite < 0 || picked.Count < 2) return;

        int other = elite == 0 ? 1 : 0;
        if (picked[other] == RunNodeKind.NormalBattle) return;

        picked[other] = RunNodeKind.NormalBattle;
    }

    /// <summary>가중치에 비례해 한 칸을 고른다.</summary>
    static int DrawIndex(List<(RunNodeKind Kind, int Weight)> pool)
    {
        int total = 0;
        foreach (var row in pool) total += row.Weight;

        if (total <= 0) return Random.Range(0, pool.Count);

        int roll = Random.Range(0, total);

        for (int i = 0; i < pool.Count; i++)
        {
            roll -= pool[i].Weight;
            if (roll < 0) return i;
        }

        return pool.Count - 1;   // 부동소수가 아니라 정수라 여기 오지 않는다
    }
}
