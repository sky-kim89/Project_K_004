using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  CardRewardPicker.cs
//  스테이지를 깬 뒤 보여 줄 카드 후보를 고른다. 지급은 하지 않는다.
//
//  ■ 무엇이 후보가 되는가
//    ① 아직 없는 카드  → 새 카드 (칸이 남아 있을 때만)
//    ② 이미 가진 카드  → 중복 = 레벨업
//    둘을 섞어서 낸다. 새 카드만 주면 칸이 금방 차고 레벨업을 못 하며,
//    중복만 주면 판이 넓어지지 않는다.
//
//  ■ 칸이 다 차면 새 카드를 내지 않는다
//    받아도 놓을 자리가 없으면 보상이 아니라 낚시가 된다.
//    (SummonDeckData.Acquire 가 자리가 없으면 0 을 돌려주는 것과 짝이다)
//
//  ■ 친화 카드를 더 자주 낸다
//    소환사의 색깔대로 런이 굴러가야 캐릭터를 고른 의미가 산다.
//    다만 **친화만** 내지는 않는다 — 그러면 캐릭터마다 런이 똑같아진다.
//
//  ■ ⚠ 업그레이드 종족은 **도감에 해금된 뒤에만** 후보가 된다
//    (사용자 확정, 2026-09-07 — 규칙이 바뀌었다)
//
//    힐/독/강철 슬라임 같은 상위 종족을 **처음 얻는 길은 여전히 진화 하나뿐**이다.
//    보상으로 그냥 주워지면 만렙까지 키워 진화시키는 과정이 무의미해진다.
//    그래서 도감 해금이 문 노릇을 한다 —
//      한 번이라도 진화로 손에 넣어 본 종족만 이후 런의 보상에 섞인다.
//
//    이러면 도감을 채우는 일이 **다음 런의 선택지를 넓히는 일**이 된다.
//    (예전에는 통째로 제외라, 진화체는 도감에 있어도 영영 뽑히지 않았다)
//
//  ■ ⚠ 뿌리 종족은 도감과 무관하다
//    해금 여부로 뿌리까지 막으면 첫 런에서 카드가 세 장밖에 안 나온다.
//    거르는 것은 업그레이드 종족뿐이다.
// ============================================================

/// <summary>보상 후보 한 장. 화면이 이 정보만으로 카드를 그릴 수 있어야 한다.</summary>
public readonly struct CardRewardOption
{
    public readonly SummonKind Kind;
    public readonly string     Id;

    /// <summary>이미 가진 카드인가. true 면 이 보상은 레벨업이다.</summary>
    public readonly bool IsDuplicate;

    /// <summary>받고 나면 몇 레벨이 되는가. 표시용.</summary>
    public readonly int ResultLevel;

    /// <summary>지금 몇 레벨인가. 새 카드면 0.</summary>
    public readonly int CurrentLevel;

    /// <summary>
    /// 받고 나면 누적 장수가 몇 장이 되는가.
    ///
    /// 중복이 **항상 레벨을 올리지는 않는다** — 문턱(1,2,4,7,11장)을 넘어야 오른다.
    /// 화면이 "이번에 오르는가, 몇 장 더 모아야 하는가" 를 말하려면 이 값이 필요하다.
    /// </summary>
    public readonly int CopiesAfter;

    /// <summary>이번 획득으로 레벨이 실제로 오르는가.</summary>
    public bool LevelsUp => IsDuplicate && !IsMaxed && ResultLevel > CurrentLevel;

    /// <summary>
    /// 이미 만렙이라 중복이 레벨을 못 올리는가.
    ///
    /// true 면 이 카드를 고르는 것은 "레벨업" 이 아니라 **진화/융합 갈림길**을
    /// 여는 행동이다. 보상 화면이 그렇게 표시해야 플레이어가 헛되이 고르지 않는다.
    /// </summary>
    public readonly bool IsMaxed;

    // ── 시너지 강화 카드 ─────────────────────────────────────
    //
    //  ⚠ SummonKind 를 늘리지 않는다
    //    이 카드는 덱 칸에 들어가지 않는다(런 상태로만 산다). SummonKind 는
    //    "덱 칸에 무엇이 들어 있나" 를 가리키는 열거형이라, 여기에 값을 더하면
    //    카드 바·저장·소환 경로가 전부 없는 칸을 다루게 된다.
    //    후보에만 있는 종류이므로 표식 하나로 갈라낸다.

    /// <summary>강화할 시너지. None 이면 이 후보는 몬스터·스킬 카드다.</summary>
    public readonly MonsterTag BoostTag;

    public bool IsSynergyBoost => BoostTag != MonsterTag.None;

    /// <summary>시너지 강화 후보를 만든다.</summary>
    public static CardRewardOption SynergyBoost(MonsterTag tag)
        => new CardRewardOption(tag);

    CardRewardOption(MonsterTag tag)
    {
        Kind         = SummonKind.Monster;   // 쓰이지 않는다 — IsSynergyBoost 가 먼저 갈린다
        Id           = string.Empty;
        IsDuplicate  = false;
        ResultLevel  = 0;
        IsMaxed      = false;
        CurrentLevel = 0;
        CopiesAfter  = 0;
        BoostTag     = tag;
        EvolveFromId = string.Empty;
    }

    // ── 진화 후보 ────────────────────────────────────────────
    //
    //  ⚠ SummonKind 도 BoostTag 도 늘리지 않는다 (시너지 강화와 같은 이유)
    //    이것은 **덱에 이미 있는 칸을 바꾸는** 후보다. 새 칸을 먹지 않는다.

    /// <summary>
    /// 이 후보가 진화시킬 <b>베이스 카드의 종족 ID</b>. 비어 있으면 보통 카드다.
    ///
    /// 채워져 있으면 <see cref="Id"/> 는 <b>진화 결과</b>의 종족이고, 고르면
    /// 덱의 그 베이스 칸이 이것으로 바뀐다 (SummonDeckData.EvolveTo).
    /// </summary>
    public readonly string EvolveFromId;

    public bool IsEvolve => !string.IsNullOrEmpty(EvolveFromId);

    /// <summary>진화 후보를 만든다. baseLevel 은 지금 베이스 카드의 레벨(표시용)이다.</summary>
    public static CardRewardOption Evolve(string targetId, string baseId, int baseLevel)
        => new CardRewardOption(targetId, baseId, baseLevel);

    CardRewardOption(string targetId, string baseId, int baseLevel)
    {
        Kind        = SummonKind.Monster;
        Id          = targetId;

        // ⚠ 새 카드가 아니다(칸을 안 먹는다). 그렇다고 중복도 아니다(레벨이 안 오른다).
        //   둘 다 false 로 두고 IsEvolve 하나로 갈린다.
        IsDuplicate = false;
        IsMaxed     = false;

        // 진화는 **Lv1 로 되돌아간다** (SummonDeckData.Evolve 와 같은 규칙).
        ResultLevel  = 1;
        CurrentLevel = baseLevel;
        CopiesAfter  = 1;

        BoostTag     = MonsterTag.None;
        EvolveFromId = baseId;
    }

    public CardRewardOption(SummonKind kind, string id, bool isDuplicate,
                            int resultLevel, bool isMaxed = false,
                            int currentLevel = 0, int copiesAfter = 1)
    {
        Kind         = kind;
        Id           = id;
        IsDuplicate  = isDuplicate;
        ResultLevel  = resultLevel;
        IsMaxed      = isMaxed;
        CurrentLevel = currentLevel;
        CopiesAfter  = copiesAfter;
        BoostTag     = MonsterTag.None;
        EvolveFromId = string.Empty;
    }
}

public static class CardRewardPicker
{
    /// <summary>한 번에 보여 줄 후보 수 (기본값).</summary>
    public const int ChoiceCount = 3;

    /// <summary>
    /// 특성까지 반영한 실제 후보 수 — '감식안' 이 한 장 더 준다.
    ///
    /// ⚠ 팝업도 이 값을 봐야 한다. ChoiceCount 를 직접 읽으면 네 번째 카드가
    ///   뽑히기만 하고 화면에 안 뜬다.
    /// </summary>
    /// <summary>
    /// 프리팹이 준비해 둬야 할 칸 수 — <b>특성까지 켜졌을 때의 최대값</b>.
    ///
    /// ⚠ Creator 는 이 값으로 굽는다. ChoiceCount(3)로 구우면 '감식안' 의
    ///   네 번째 카드가 뽑히기만 하고 화면에 안 뜬다.
    ///   남는 칸은 CardSelectPopup.Setup 이 알아서 숨긴다.
    /// </summary>
    public const int MaxChoiceCount = ChoiceCount + AppraisalExtra;

    /// <summary>'감식안' 이 더해 주는 선택지 수. RunPerkRule 과 같은 값이어야 한다.</summary>
    const int AppraisalExtra = 1;

    public static int CurrentChoiceCount
        => Mathf.Min(MaxChoiceCount,
                     ChoiceCount
                     + (RunPerkRule.Has(RunPerk.Appraisal) ? RunPerkRule.AppraisalExtraChoices : 0)
                     // 유물 '안목'
                     + RelicTreeApplier.GetSystemInt(RelicSystemEffect.CardChoiceCount));

    // ── 가중치 ───────────────────────────────────────────────
    //
    //  ⚠ 값을 4배로 잡아 두었다 (2026-09-09)
    //    풀은 "같은 항목을 N번 넣는" 방식이라 가중치가 곧 정수 개수다.
    //    기본이 2 이면 그보다 드물게 만들 자리가 1 하나뿐이라 세밀하게 못 나눈다.
    //    전부 4배로 올려 두면 "8분의 1로 드물게" 같은 조절이 가능해진다.
    //    ⚠ 비율만 뜻이 있다 — 한 줄만 바꾸면 그 카드만 흔해진다.

    /// <summary>친화 카드가 후보 풀에 더 들어가는 횟수 (가중치).</summary>
    const int AffinityWeight = 12;

    /// <summary>
    /// 시너지 강화가 후보 풀에 들어가는 횟수.
    ///
    /// 몬스터(2)보다 드물다 — 런에 한 장뿐인 카드라 매 판 뜨면 "언제 고를까" 가
    /// 아니라 "이번엔 뭘 강화할까" 가 되어 몬스터 선택을 밀어낸다.
    /// </summary>
    const int BoostWeight = 4;

    /// <summary>몬스터 카드 기본 가중치.</summary>
    const int MonsterWeight = 8;

    /// <summary>
    /// 만렙이라 <b>진화·융합만 남은</b> 카드의 가중치.
    ///
    /// 레벨이 오르는 카드보다 드물게 둔다 — 그쪽은 고르면 반드시 숫자가 오르지만
    /// 이쪽은 "지금 진화할까" 라는 무거운 결정을 다시 꺼내는 카드다.
    /// </summary>
    const int MaxedWeight = 4;

    /// <summary>
    /// <b>이미 융합한 적이 있는</b> 만렙 카드의 가중치.
    ///
    /// ■ ⚠ 아주 드물어야 한다 (사용자 지적, 2026-09-09)
    ///   융합을 한 번 한 카드가 계속 후보로 올라와 3택이 매번 같은 카드로 채워졌다.
    ///   그 카드가 할 수 있는 일은 "재료를 하나 더 먹는 것" 뿐이고, 그건 이미
    ///   한 번 한 선택이다. 기본(8)의 8분의 1 로 둔다 — 아예 빼지는 않는다.
    ///   융합 슬롯이 셋이라, 끝까지 채우고 싶은 사람의 길은 남겨 둔다.
    /// </summary>
    const int FusedAgainWeight = 1;

    static readonly List<CardRewardOption> _pool = new(64);

    /// <summary>
    /// 후보 3장을 뽑는다. 뽑을 게 모자라면 그만큼만 낸다 (빈 배열도 정상이다).
    /// </summary>
    public static List<CardRewardOption> Pick(SummonerData summoner, SummonDeckData deck)
        => Pick(summoner, deck, CurrentChoiceCount);

    /// <summary>
    /// 장수를 지정해 뽑는다 — 상점이 쓴다 (RunShopRule.RollCards).
    ///
    /// ⚠ 후보를 고르는 규칙은 3택과 **같아야 한다**
    ///   상점만 다른 규칙을 두면 "3택에는 안 나오는데 상점에는 나오는 카드" 가
    ///   생겨, 도감 해금이 무슨 뜻인지가 화면마다 갈린다. 그래서 수만 받는다.
    /// </summary>
    public static List<CardRewardOption> Pick(SummonerData summoner, SummonDeckData deck, int want)
    {
        var result = new List<CardRewardOption>(want);

        CardCatalog catalog = CardCatalog.Current;
        if (catalog == null)
        {
            Debug.LogError("[CardRewardPicker] Resources/CardCatalog 이 없습니다. " +
                           "Tools > Project K > 데이터 생성 > 카드 목록 을 실행하세요.");
            return result;
        }

        // 도감은 **업그레이드 종족의 문**이다 (위 머리 주석 참고).
        var codex = UserDataManager.Instance?.Get<MonsterCodexData>();

        BuildPool(summoner, deck, catalog, codex);
        if (_pool.Count == 0) return result;

        // 같은 카드가 두 번 뽑히지 않게 ID 로 거른다 —
        // 가중치 때문에 같은 항목이 풀에 여러 번 들어 있기 때문이다.
        var taken = new HashSet<string>();

        int guard = _pool.Count * 4;   // 무한 루프 방지 (전부 중복일 수 있다)
        while (result.Count < want && guard-- > 0)
        {
            CardRewardOption option = _pool[Random.Range(0, _pool.Count)];
            if (!taken.Add(option.Id)) continue;

            result.Add(option);
        }

        return result;
    }

    // ── 내부 ─────────────────────────────────────────────────

    static void BuildPool(SummonerData summoner, SummonDeckData deck, CardCatalog catalog,
                          MonsterCodexData codex)
    {
        _pool.Clear();

        bool hasRoom = deck.HasFreeSlot;

        foreach (MonsterSpeciesData species in catalog.Monsters)
        {
            if (species == null || string.IsNullOrEmpty(species.Id)) continue;

            int at = deck.IndexOf(species.Id);
            bool owned = at >= 0;

            // ── 진화체를 아직 손에 안 들었을 때 — **진화 선택지**로만 낸다 ──
            //
            //  ■ ⚠ 규칙이 또 바뀌었다 (사용자 지시, 2026-09-12)
            //    해금된 진화체가 **그냥 새 카드로** 뜨는 것이 불합리했다.
            //    힐 슬라임을 줍는 것과 슬라임을 힐 슬라임으로 키우는 것은 다른 일인데,
            //    화면에서는 둘이 같은 "새 카드" 로 보였다.
            //
            //  ■ 지금 — 문이 둘이고 순서가 있다
            //      ① 처음 얻는 길은 여전히 **만렙 랜덤 진화** 하나뿐이다.
            //         그걸로 도감이 열린다 (CardEvolveUI.HandleEvolve).
            //      ② 도감에 오른 뒤로는, **베이스를 손에 들고 있을 때만**
            //         "○○ 진화" 카드가 3택에 섞인다. 고르면 그 베이스 칸이 바뀐다.
            //    그래서 도감을 채우는 일이 여전히 다음 런의 선택지를 넓히는 일이다.
            //
            //  ⚠ 칸(hasRoom)을 보지 않는다 — 새 칸을 먹지 않고 있는 칸을 바꾼다.
            //  ⚠ 이미 손에 든 진화체는 이 갈래를 타지 않는다 — 아래 보통 경로로
            //    내려가 **중복(레벨업)** 후보가 된다. 안 그러면 한 번 얻은
            //    진화체를 영영 키울 수 없다.
            if (!owned && species.UpgradeOf != null)
            {
                // 도감이 문이다 (위 ① 참고)
                if (codex == null || !codex.IsUnlocked(species.Id)) continue;

                int baseAt = deck.IndexOf(species.UpgradeOf.Id);

                // ⚠ 판정은 CardEvolution.CanEvolveTo 하나를 쓴다 —
                //   실제로 바꾸는 쪽(SummonDeckData.EvolveTo)과 조건이 갈리면
                //   "골랐는데 아무 일도 안 난다" 가 된다.
                if (!CardEvolution.CanEvolveTo(deck, baseAt, species)) continue;

                var evolve = CardRewardOption.Evolve(species.Id, species.UpgradeOf.Id,
                                                     deck.GetSlot(baseAt).Level);

                // 친화는 **결과 종족**으로 본다 — 얻게 되는 것이 그쪽이다.
                int evolveWeight = MonsterWeight
                                 + (summoner.IsAffinity(species) ? AffinityWeight : 0);

                for (int i = 0; i < evolveWeight; i++) _pool.Add(evolve);
                continue;
            }

            if (!owned && !hasRoom) continue;   // 놓을 자리가 없다

            int copiesNow = owned ? deck.GetSlot(at).Copies : 0;
            int copiesNext = copiesNow + 1;

            int nowLevel = owned ? CardLevelRule.LevelForCopies(copiesNow) : 0;
            int level    = owned ? CardLevelRule.LevelForCopies(copiesNext) : 1;

            bool maxed = owned && deck.GetSlot(at).IsMaxLevel;

            // ⚠ 아무 일도 못 하는 카드는 내지 않는다
            //   만렙이면 레벨이 안 오르고, 진화도 융합도 막혀 있으면 골라도
            //   창이 뜨지 않고 그대로 넘어간다 — 보상 한 번을 통째로 버린다.
            //   진화체가 후보에 들어오면서(2026-09-07) 이 상태가 흔해졌다.
            //   진화한 카드는 다시 진화하지 못하기 때문이다.
            //   ⚠ 판정은 CardEvolution.HasAnyChoice 하나를 쓴다 —
            //     창을 여는 쪽과 조건이 갈리면 "골랐는데 그냥 넘어간다" 가 된다.
            if (maxed && !CardEvolution.HasAnyChoice(deck, at)) continue;

            // ⚠ 만렙 카드는 드물게, 이미 융합한 카드는 아주 드물게 (위 상수 주석)
            //   ⚠ 친화 가중치는 **레벨이 오르는 카드에만** 얹는다
            //     만렙에도 얹으면 친화 종족이 융합을 한 번 한 순간 13(=1+12)이 되어
            //     "아주 드물게" 가 통째로 무효가 된다. 친화는 "이 종족을 키우라" 는
            //     신호지 "다 키운 카드를 또 보여 주라" 는 신호가 아니다.
            int weight = maxed
                       ? (deck.GetSlot(at).HasFused ? FusedAgainWeight : MaxedWeight)
                       : MonsterWeight + (summoner.IsAffinity(species) ? AffinityWeight : 0);

            var option = new CardRewardOption(SummonKind.Monster, species.Id, owned, level, maxed,
                                              nowLevel, copiesNext);
            for (int i = 0; i < weight; i++) _pool.Add(option);
        }

        AddSynergyBoosts();
    }

    // ── 시너지 강화 후보 ─────────────────────────────────────
    //
    //  ⚠ 스킬 카드 후보는 없앴다 (2026-09-04, 사용자 확정)
    //    스킬은 이제 소환사가 **무료로** 쓰는 시그니처 하나뿐이고, 3택으로
    //    줍는 물건이 아니다. 덱 칸이 8뿐인데 스킬 카드가 그 칸을 먹으면
    //    시너지 조합이 그만큼 좁아졌다.
    //    (남은 SkillCardData·SkillCardCaster 는 그 시그니처 스킬 강화 카드
    //     한 장이 쓴다 — 지우지 말 것)
    //
    //  ■ 강화는 **이미 키우고 있는 시너지**만 후보로 낸다
    //    한 장도 안 낸 줄에 카운트를 부어도 아무것도 안 열린다. 그런 후보를
    //    띄우면 "고르면 손해" 인 선택지를 화면에 두는 셈이다.

    static void AddSynergyBoosts()
    {
        if (!MonsterSynergyRule.BoostAvailable) return;   // 런에 한 장뿐이다

        foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
        {
            if (MonsterSynergyRule.CountOf(tag) <= 0) continue;

            var option = CardRewardOption.SynergyBoost(tag);
            for (int i = 0; i < BoostWeight; i++) _pool.Add(option);
        }
    }
}
