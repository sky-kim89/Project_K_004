using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MonsterSynergyRule.cs
//  몬스터 시너지의 **정본**. 문턱·수치·현재 단계가 전부 여기 있다.
//
//  ■ 세는 대상은 덱이 아니라 **실제로 낸 종족**이다 (2026-09-03, 사용자 확정)
//    손에 들고만 있어도 시너지가 켜지면 마나를 쓸 이유가 없다. 라인 대기열에
//    올린 순간부터 그 종족이 세어진다.
//
//    ⚠ 세는 시점은 **대기열에 넣을 때 한 번뿐이다**
//      필드의 생존자를 매 프레임 세면 교전 중에 시너지가 켜졌다 꺼졌다 한다.
//      한 마리 죽을 때마다 전군의 스탯이 흔들리는 것은 읽을 수도, 계획할 수도
//      없다. 그래서 죽어도 내려가지 않는다 — 한 번 냈으면 그 런 동안 유지된다.
//
//    ⚠ 라인 복귀(MonsterLineReturner)도 같은 길을 탄다
//      생존자는 대기열로 돌아가므로 다음 판에도 그대로 세어진다.
//
//  ■ 문턱 — 금 = 계열 완주
//    소속 7종이면 3/5/7, 6종이면 2/4/6. 이 규칙 하나로 모든 시너지의
//    금 난이도가 같아진다.
//    ⚠ 예전에는 숲이 9종 중 7장(78%)이면 금인데 야수는 6종 중 6장(100%)이라
//      같은 금인데 난이도가 달랐다.
//
//  ■ 모든 단계는 같은 전투력을 준다 (2026-09-03 재조정)
//    전투력지수 = EHP배율 × DPS배율 로 환산해 맞췄다.
//        동 ×1.12 · 은 ×1.30 · 금 ×1.60
//    ⚠ 이 자를 쓰지 않으면 조용히 어긋난다. 실제로 처음엔
//      숲 동이 체력 +8%(×1.08) 인데 언데드 동이 부활 15%×체력30%(×1.045) 라
//      한쪽이 두 배 가까이 셌다.
//
//    은·금의 **리더(특수 효과)** 는 ×1.03 쯤으로 잡고 수치에서 그만큼 뺀다.
//    은 리더는 금 리더의 **절반짜리 미리보기**다 — 사다리가 눈에 보인다.
//
//  ■ 환산에 쓴 가정 셋 (실측과 다르면 여기만 고치면 된다)
//      · 교전 평균 8초        → 재생의 초당 회복을 EHP 로 환산할 때
//      · 개체 평균 4처치      → 투지의 누적을 평균 배율로 환산할 때
//      · 스킬이 DPS의 40%     → 술법의 쿨감·피해를 전체 DPS 로 환산할 때
// ============================================================

public static class MonsterSynergyRule
{
    /// <summary>단계가 바뀌면 알린다. 카드 바·시너지 표시가 구독한다.</summary>
    public static event Action Changed;

    public static readonly MonsterTag[] AllTags =
    {
        MonsterTag.Undead,   MonsterTag.Forest, MonsterTag.Beast,  MonsterTag.Regrowth,
        MonsterTag.Ferocity, MonsterTag.Steel,  MonsterTag.Plague, MonsterTag.Sorcery,
    };

    /// <summary>
    /// AllTags 안에서의 자리. 아이콘 배열을 찾는 열쇠다.
    ///
    /// ⚠ 화면 쪽 배열(SynergyBarUI._icons · CardSelectPopup._synergyIcons)은
    ///   전부 이 순서로 채워진다. 다른 순서를 쓰면 "숲인데 해골이 뜨는" 상태가 된다.
    /// </summary>
    public static int IndexOf(MonsterTag tag)
    {
        for (int i = 0; i < AllTags.Length; i++)
            if (AllTags[i] == tag) return i;

        return -1;
    }

    // ── 문턱 ─────────────────────────────────────────────────
    //  소속 7종(언데드·숲) → 3/5/7,  소속 6종(나머지) → 2/4/6

    struct Steps { public int Bronze, Silver, Gold; }

    static readonly Steps Wide   = new() { Bronze = 3, Silver = 5, Gold = 7 };
    static readonly Steps Narrow = new() { Bronze = 2, Silver = 4, Gold = 6 };

    static Steps StepsOf(MonsterTag tag)
    {
        Steps s = tag is MonsterTag.Undead or MonsterTag.Forest ? Wide : Narrow;

        // 유물 '조율' — **동 문턱만** 내린다.
        // ⚠ 은·금까지 내리면 7종 계열의 금(7)이 5로 떨어져, 확장 편성 없이도
        //   닿는다. 금 문턱이 덱 칸을 압박하는 설계가 통째로 사라진다.
        int cut = RelicTreeApplier.GetSystemInt(RelicSystemEffect.SynergyStepCut);
        if (cut <= 0) return s;

        s.Bronze = Mathf.Max(1, s.Bronze - cut);
        return s;
    }

    public static int BronzeAt(MonsterTag tag) => StepsOf(tag).Bronze;
    public static int SilverAt(MonsterTag tag) => StepsOf(tag).Silver;
    public static int GoldAt(MonsterTag tag)   => StepsOf(tag).Gold;

    /// <summary>다음 단계에 필요한 장수. 이미 금이면 0.</summary>
    public static int NextStepAt(MonsterTag tag)
    {
        int have = CountOf(tag);
        Steps s  = StepsOf(tag);

        if (have < s.Bronze) return s.Bronze;
        if (have < s.Silver) return s.Silver;
        if (have < s.Gold)   return s.Gold;
        return 0;
    }

    // ── 현재 상태 ────────────────────────────────────────────

    static readonly Dictionary<MonsterTag, int> _counts = new();

    /// <summary>이번 런에서 한 번이라도 대기열에 올린 종족. 이게 집계의 원본이다.</summary>
    static readonly HashSet<string> _summoned = new();

    // ── 시너지 강화 카드 ─────────────────────────────────────
    //
    //  ■ 런에 딱 한 장이다 (사용자 확정, 2026-09-04)
    //    카운트 +1 은 한 단계를 그대로 열 수 있는 강한 효과다. 대신 **키우는
    //    중이 아닌 시너지에 쓰면 독**이다 — 8칸짜리 덱에서 쓸모없는 줄에
    //    카운트를 부어도 아무것도 안 열린다. 그 위험을 지는 대가로 공/체까지
    //    붙여 세게 줬다.
    //
    //  ⚠ 덱 칸을 먹지 않는다
    //    8칸이 이미 빠듯해서 칸을 먹으면 "카운트 +1 을 얻고 칸을 잃는" 제로섬이
    //    된다. 런 상태로만 들고 있는다.

    /// <summary>강화한 시너지. None 이면 아직 안 골랐다.</summary>
    public static MonsterTag BoostedTag { get; private set; }

    /// <summary>강화 카드가 그 시너지 소속 몬스터에게 주는 공격력·체력 비율.</summary>
    public const float BoostStatBonus = 0.15f;

    /// <summary>강화 카드가 얹어 주는 카운트.</summary>
    public const int BoostCount = 1;

    public static bool BoostAvailable => BoostedTag == MonsterTag.None;

    /// <summary>
    /// 시너지 강화 카드를 쓴다. 런에 한 번뿐이다.
    ///
    /// ⚠ 되돌릴 수 없다 — 고른 순간 확정이다.
    ///   되돌릴 수 있으면 "어디에 걸 것인가" 가 선택이 아니게 된다.
    /// </summary>
    public static void ApplyBoost(MonsterTag tag)
    {
        if (!BoostAvailable || tag == MonsterTag.None) return;

        BoostedTag = tag;
        Recount();
    }

    /// <summary>이어하기용 — 저장된 강화 대상을 되돌린다. Recount 는 부르는 쪽이 한다.</summary>
    public static void RestoreBoost(MonsterTag tag) => BoostedTag = tag;

    /// <summary>단계가 하나라도 열려 있는 시너지 수. 중첩 보너스가 이 값을 본다.</summary>
    public static int ActiveCount { get; private set; }

    /// <summary>
    /// 지금 몇 종을 냈나. <b>시너지 강화 카드의 +1 이 여기 포함된다.</b>
    ///
    /// ⚠ 문턱 판정·화면 표시가 전부 이 함수를 지난다 — 강화를 따로 더하는
    ///   코드를 만들지 말 것. 두 곳에서 더하면 카운트가 두 번 오른다.
    /// </summary>
    public static int CountOf(MonsterTag tag)
    {
        int have = RawCountOf(tag);
        if (tag != MonsterTag.None && tag == BoostedTag) have += BoostCount;

        // 런 보너스 — 제단 제물·이벤트가 얹어 둔 몫. 카드가 사라져도 남는다.
        // ⚠ 저장고는 RunBoonData 다. 여기서 값을 들고 있지 않는다.
        if (tag != MonsterTag.None)
            have += UserDataManager.Instance?.Get<RunBoonData>()?.SynergyOf(tag) ?? 0;

        // 특성 '편중' — 카운트가 가장 높은 시너지 하나에만 얹는다.
        // ⚠ 강화 카드와 같은 표식에 걸리면 둘이 함께 붙어 +2 가 된다.
        //   의도한 것이다 — 한 시너지에 모든 것을 건 빌드의 보상이다.
        if (tag != MonsterTag.None && tag == WeightedTag) have += RunPerkRule.WeightedCount;

        // 특성 '한 우물' — 덱의 몬스터가 **전부** 가진 표식 하나에 얹는다.
        if (tag != MonsterTag.None && tag == SingleWellTag) have += RunPerkRule.SingleWellCount;

        return have;
    }

    /// <summary>
    /// 특성 '편중' 이 얹히는 표식 — <b>날것 카운트</b>가 가장 높은 하나.
    ///
    /// ⚠ RawCountOf 로 판정한다
    ///   CountOf 로 재면 자기가 얹은 +1 이 순위를 바꿔 무한 재귀가 되고,
    ///   설령 막아도 매 프레임 대상이 흔들린다.
    /// ⚠ 동점이면 AllTags 순서가 앞선 쪽. 무작위로 고르면 같은 덱에서
    ///   런마다 다른 시너지가 열려 설명할 수 없다.
    /// </summary>
    static MonsterTag WeightedTag
    {
        get
        {
            if (!RunPerkRule.Has(RunPerk.Weighted)) return MonsterTag.None;

            MonsterTag best = MonsterTag.None;
            int        top  = 0;

            for (int i = 0; i < AllTags.Length; i++)
            {
                int n = RawCountOf(AllTags[i]);
                if (n > top) { top = n; best = AllTags[i]; }
            }

            return best;
        }
    }

    /// <summary>
    /// 특성 '한 우물' 이 얹히는 표식 — 덱의 몬스터 카드가 <b>전부</b> 가진 표식 (AllTags 순서로 첫 것).
    ///
    /// ⚠ 몬스터 카드가 둘 이상일 때만 — 한 장이면 무엇이든 "전부 공유" 라 공짜가 된다.
    /// ⚠ 덱에서 매번 다시 잰다 — 다른 계열 카드를 넣는 순간 저절로 풀린다.
    /// ⚠ 동점 규칙은 편중과 같다 (AllTags 순서). 무작위로 고르면 설명할 수 없다.
    /// </summary>
    static MonsterTag SingleWellTag
    {
        get
        {
            if (!RunPerkRule.Has(RunPerk.SingleWell)) return MonsterTag.None;

            var deck    = UserDataManager.Instance?.Get<SummonDeckData>();
            var catalog = CardCatalog.Current;
            if (deck == null || catalog == null) return MonsterTag.None;

            MonsterTag common   = ~MonsterTag.None;
            int        monsters = 0;

            for (int i = 0; i < deck.SlotCount; i++)
            {
                SummonDeckSlot slot = deck.GetSlot(i);
                if (slot.IsEmpty || slot.Kind != SummonKind.Monster) continue;

                MonsterSpeciesData species = catalog.GetMonster(slot.Id);
                if (species == null) continue;

                common &= species.Tags;
                monsters++;
            }

            if (monsters < 2) return MonsterTag.None;

            foreach (MonsterTag t in AllTags)
                if ((common & t) != 0) return t;

            return MonsterTag.None;
        }
    }

    public static SynergyTier TierOf(MonsterTag tag)
    {
        int have = CountOf(tag);
        Steps s  = StepsOf(tag);

        if (have >= s.Gold)   return SynergyTier.Gold;
        if (have >= s.Silver) return SynergyTier.Silver;
        if (have >= s.Bronze) return SynergyTier.Bronze;
        return SynergyTier.None;
    }

    /// <summary>
    /// 덱 변경을 구독하고 한 번 센다. 런이 시작될 때 부른다.
    ///
    /// ⚠ 구독을 먼저 떼고 붙인다
    ///   에디터는 플레이를 멈춰도 static 이벤트가 남는다. 그냥 += 하면
    ///   런을 다시 시작할 때마다 구독이 한 겹씩 쌓여 Recount 가 여러 번 돈다.
    /// </summary>
    public static void Bind()
    {
        _summoned.Clear();
        _alive.Clear();           // 지난 판의 필드 기록이 남아 있으면 안 된다
        _carriedStacks.Clear();   // 투지 금이 물려주던 누적도 런 경계에서 끊는다

        // ⚠ 런 보너스(제단 몫)는 여기서 건드리지 않는다
        //   RunBoonData 가 세이브로 들고 있고, 새 런은 UserDataManager 가
        //   SetDefaults 로 비운다.

        // ⚠ 이어하기를 위해 **대기열에서 다시 읽어 온다**
        //   MarkSummoned 는 카드를 낼 때만 불린다. 앱을 껐다 켜면 그 호출이
        //   다시 일어나지 않으므로, 되살린 대기열(RestoreQueue)에 남아 있는
        //   종족을 여기서 도로 세어 준다. 안 하면 이어하기가 시너지를 통째로
        //   날린다.
        //   ⚠ 그래서 RestoreQueue 보다 **뒤에** 불려야 한다.
        //   ⚠ 카운트 자체는 Recount 가 대기열을 직접 본다 — 여기서 담는 것은
        //     "낸 적 있다" 는 기록뿐이다 (강화 카드 후보가 읽는다).
        SummonReservation reservation = SummonController.Instance?.Reservation;

        if (reservation != null)
            for (int lane = 0; lane < reservation.LaneCount; lane++)
                foreach (MonsterSpeciesData species in reservation.LaneQueue(lane))
                    if (species != null) _summoned.Add(species.Id);

        Recount();
    }

    /// <summary>
    /// 이 종족을 대기열에 올렸다. 시너지는 여기서만 늘어난다.
    ///
    /// ⚠ 부르는 곳은 SummonController.UseMonsterCard 하나뿐이다
    ///   라인 복귀(MonsterLineReturner)는 부를 필요가 없다 — 돌아가는 개체는
    ///   이미 카드로 낸 것이라 그때 세어졌고, 이 함수는 같은 종족을 두 번
    ///   세지 않는다. 분열체·부활체는 애초에 복귀 대상이 아니다(returnable=false).
    /// </summary>
    public static void MarkSummoned(MonsterSpeciesData species)
    {
        if (species == null) return;

        _summoned.Add(species.Id);   // 기록은 남긴다 — 강화 카드 후보가 이걸 본다
        Recount();
    }

    // ── 지금 살아 있는 것 ────────────────────────────────────
    //
    //  ■ ⚠ 전멸하면 카운트에서 빠진다 (사용자 확정, 2026-09-09)
    //    예전 규칙은 "한 번 내면 런 내내 유지" 였다. 그러면 첫 판에 한 번씩
    //    내고 전부 잃어도 시너지가 끝까지 켜져 있어, 화면의 숫자가 전장과
    //    아무 관계가 없어진다.
    //    지금 세는 것은 **지금 존재하는 종족**이다 — 필드에 살아 있거나,
    //    라인 대기열에 남아 있거나.
    //
    //    ⚠ 매 프레임 세지 않는다. 0↔1 을 넘는 순간에만 다시 센다 —
    //      교전 중에 켜졌다 꺼졌다 하는 것을 막으려던 옛 이유는 그대로 유효하다.
    //    ⚠ 대기열도 '존재' 다. 스테이지 사이에는 필드가 비지만 살아남은
    //      것들이 대기열에 있다 — 그걸 안 세면 판이 바뀔 때마다 시너지가 꺼진다.
    //    ⚠ 이어하기가 저절로 맞는다 — 대기열은 저장되므로 다시 켜고 나면
    //      같은 값이 나온다 (예전에는 static 이 날아가 카운트가 통째로 줄었다).

    /// <summary>필드에 서 있는 종족별 마릿수.</summary>
    static readonly Dictionary<string, int> _alive = new();

    /// <summary>존재 판정용 재사용 버퍼.</summary>
    static readonly HashSet<string> _present = new();

    /// <summary>
    /// 한 마리가 서거나(+1) 사라졌다(−1). 부르는 곳은 MonsterLineReturner 하나다
    /// (모든 몬스터가 그 컴포넌트를 달고 나온다 — 분열체·스킬 소환도 포함).
    /// </summary>
    public static void NoteAlive(MonsterSpeciesData species, int delta)
    {
        if (species == null || delta == 0) return;

        string id  = species.Id;
        int    now = (_alive.TryGetValue(id, out int cur) ? cur : 0) + delta;

        bool wasPresent = cur > 0;

        if (now <= 0) _alive.Remove(id);
        else          _alive[id] = now;

        // 0 ↔ 1 을 넘을 때만 다시 센다.
        if (wasPresent != now > 0) Recount();
    }

    /// <summary>런 경계에서 필드 기록을 지운다 (씬을 내리면 OnDisable 이 안 도는 경우가 있다).</summary>
    public static void ClearAlive() => _alive.Clear();

    // ── 런 보너스 (제단·이벤트) ──────────────────────────────
    //
    //  ■ 카드를 잃고 숫자를 얻는다
    //    제물이 된 카드는 덱에서 사라지고, 그 카드가 갖고 있던 표식 중
    //    **하나가 무작위로** +1 로 남는다. 덱 칸이 빠듯한 이 게임에서
    //    "칸을 줄여 문턱을 넘는" 거래다.
    //
    //  ⚠ 값은 RunBoonData 가 들고 있다 — 여기 static 으로 두지 않는다
    //    static 은 저장되지 않아 이어하기에서 통째로 사라지고, 에디터에서는
    //    반대로 런이 바뀌어도 남는다. 세이브 섹션이 두 문제를 다 없앤다.
    //
    //  ⚠ RawCountOf 가 아니라 CountOf 에서 더한다
    //    _counts 는 "실제로 낸 종족 수" 만 담는 날것이어야 한다 (AddTags 주석 참고).
    //    여기 섞으면 종족을 셀 때마다 보너스가 함께 곱해 들어간다.

    /// <summary>런 보너스가 그 표식에 얹어 둔 카운트. 화면 표시용.</summary>
    public static int BoonCountOf(MonsterTag tag)
        => UserDataManager.Instance?.Get<RunBoonData>()?.SynergyOf(tag) ?? 0;

    /// <summary>
    /// <b>지금 존재하는</b> 종족에서 단계를 다시 센다 — 필드 + 대기열.
    ///
    /// ⚠ "낸 적 있는 종족"(_summoned)으로 세지 않는다 (위 NoteAlive 주석)
    ///   그건 강화 카드 후보를 고르는 기록일 뿐이다.
    /// </summary>
    public static void Recount()
    {
        _counts.Clear();

        CardCatalog catalog = CardCatalog.Current;

        if (catalog != null)
        {
            _present.Clear();

            foreach (var pair in _alive) _present.Add(pair.Key);

            SummonReservation reservation = SummonController.Instance?.Reservation;

            if (reservation != null)
                for (int lane = 0; lane < reservation.LaneCount; lane++)
                    foreach (MonsterSpeciesData queued in reservation.LaneQueue(lane))
                        if (queued != null) _present.Add(queued.Id);

            foreach (string id in _present)
            {
                MonsterSpeciesData species = catalog.GetMonster(id);
                if (species == null) continue;

                AddTags(species.Tags);
            }
        }

        int active = 0;
        for (int i = 0; i < AllTags.Length; i++)
            if (TierOf(AllTags[i]) != SynergyTier.None) active++;

        ActiveCount = active;

        Changed?.Invoke();
    }

    static void AddTags(MonsterTag tags)
    {
        for (int i = 0; i < AllTags.Length; i++)
        {
            MonsterTag tag = AllTags[i];
            if ((tags & tag) == 0) continue;

            // ⚠ CountOf 를 쓰지 않는다 — 그건 강화분(+1)을 얹어서 돌려준다
            //   여기서 쓰면 종족을 하나 셀 때마다 강화가 함께 곱해 들어가,
            //   슬라임 한 장 + 숲 강화가 **3** 으로 세어졌다.
            //   _counts 는 "실제로 낸 종족 수" 만 담는 날것이어야 한다.
            _counts[tag] = RawCountOf(tag) + 1;
        }
    }

    /// <summary>강화분을 뺀 날것. 집계 내부에서만 쓴다.</summary>
    static int RawCountOf(MonsterTag tag) => _counts.TryGetValue(tag, out int n) ? n : 0;

    /// <summary>
    /// 이 표식 묶음 중 **하나라도 켜져 있는가.**
    ///
    /// 중첩 보너스가 이걸 본다 — 시너지 효과는 전부 제 표식을 가진 몬스터에만
    /// 가야 하고, 중첩도 예외가 아니다.
    /// </summary>
    public static bool HasAnyActive(MonsterTag tags)
    {
        if (tags == MonsterTag.None) return false;

        for (int i = 0; i < AllTags.Length; i++)
        {
            MonsterTag tag = AllTags[i];
            if ((tags & tag) != 0 && TierOf(tag) != SynergyTier.None) return true;
        }

        return false;
    }

    /// <summary>이 종족을 이미 낸 적이 있나. 카드 3택이 "+1 이 되는가" 를 묻는다.</summary>
    public static bool AlreadySummoned(string speciesId) => _summoned.Contains(speciesId);

    /// <summary>
    /// 이 종족을 내면 열리는 단계. 카드 3택이 "고르면 무엇이 되는가" 를 띄운다.
    /// 이미 낸 종족이면 수가 늘지 않으므로 현재 단계 그대로다.
    /// </summary>
    public static SynergyTier TierIfAdded(MonsterTag tag, bool alreadyCounted)
    {
        int have = CountOf(tag) + (alreadyCounted ? 0 : 1);
        Steps s  = StepsOf(tag);

        if (have >= s.Gold)   return SynergyTier.Gold;
        if (have >= s.Silver) return SynergyTier.Silver;
        if (have >= s.Bronze) return SynergyTier.Bronze;
        return SynergyTier.None;
    }

    // ══════════════════════════════════════════════════════════
    //  수치 — 단계별 효과
    //
    //  ⚠ 아래 값들은 같은 전투력지수에 맞춰져 있다
    //      동 1.12 · 은 1.30 · 금 1.60
    //    한 줄을 고치면 나머지 시너지도 같은 지수로 다시 맞출 것.
    // ══════════════════════════════════════════════════════════

    /// <summary>단계별 목표 전투력지수. 새 시너지를 만들 때 이 값에 맞춘다.</summary>
    public static float TargetIndex(SynergyTier tier) => tier switch
    {
        SynergyTier.Bronze => 1.12f,
        SynergyTier.Silver => 1.30f,
        SynergyTier.Gold   => 1.60f,
        _                  => 1f,
    };

    // ── 숲 — 최대 체력 ───────────────────────────────────────
    public static float ForestHpBonus(SynergyTier t) => t switch
    {
        SynergyTier.Bronze => 0.12f,
        SynergyTier.Silver => 0.26f,
        SynergyTier.Gold   => 0.55f,
        _                  => 0f,
    };

    /// <summary>리더 — 숲 몬스터가 죽으면 주변 숲 아군이 최대 체력의 이만큼 회복한다.</summary>
    public static float ForestDeathHeal(SynergyTier t) => t switch
    {
        SynergyTier.Silver => 0.06f,
        SynergyTier.Gold   => 0.12f,
        _                  => 0f,
    };

    public const float ForestHealRadius = 3.2f;

    /// <summary>
    /// 금 전용 — <b>죽어도 라인 대기열로 돌아간다.</b>
    ///
    /// 보통은 살아남은 개체만 제 라인으로 돌아간다(MonsterLineReturner).
    /// 숲 금은 그 규칙을 깬다 — 쓰러져도 다음 판에 다시 자라 나온다.
    ///
    /// ⚠ 마나를 다시 내지 않는다 — 의도한 예외다
    ///   보통 이런 공짜 물량은 막는다. 계열 일곱 종을 모두 모은 대가로
    ///   숲만 이 규칙을 갖는다. 대신 늘어나지는 않는다 — 죽은 그 한 마리가
    ///   그 자리에 다시 설 뿐이다.
    /// </summary>
    public static bool ForestReturnsOnDeath(SynergyTier t) => t == SynergyTier.Gold;

    // ── 언데드 — 부활 ────────────────────────────────────────
    //   EHP 환산 = 1 + 확률 × 부활체력비
    public static float UndeadReviveChance(SynergyTier t) => t switch
    {
        SynergyTier.Bronze => 0.40f,
        SynergyTier.Silver => 0.55f,
        SynergyTier.Gold   => 0.68f,
        _                  => 0f,
    };

    public static float UndeadReviveHp(SynergyTier t) => t switch
    {
        SynergyTier.Bronze => 0.30f,
        SynergyTier.Silver => 0.47f,
        SynergyTier.Gold   => 0.80f,
        _                  => 0f,
    };

    /// <summary>리더 — 부활 직후 이 비율만큼 피해를 덜 받는다.</summary>
    public static float UndeadReviveGuard(SynergyTier t) => t switch
    {
        SynergyTier.Silver => 0.25f,
        SynergyTier.Gold   => 0.50f,
        _                  => 0f,
    };

    public const float UndeadGuardSeconds = 2f;

    /// <summary>
    /// 금 전용 — <b>부활한 개체가 한 번 더 부활한다.</b>
    ///
    /// 증식·부활은 0세대만 하는 것이 규칙이다(SpeciesPassiveRule.MaxReproduceGeneration).
    /// 한 마리가 화면을 채우는 것을 막는 안전장치인데, 언데드 금은 그 한도를
    /// 한 칸 늘린다 — 두 번까지 일어난다.
    ///
    /// ⚠ 무한이 아니다. 세대는 계속 세므로 세 번째에는 반드시 멈춘다.
    /// </summary>
    public static int UndeadExtraGeneration(SynergyTier t) => t == SynergyTier.Gold ? 1 : 0;

    // ── 재생 — 초당 회복 ─────────────────────────────────────
    //   교전 8초 가정 → EHP 환산 = 1 + 8 × 초당비율
    public static float RegrowthPerSecond(SynergyTier t) => t switch
    {
        SynergyTier.Bronze => 0.015f,
        SynergyTier.Silver => 0.0325f,
        SynergyTier.Gold   => 0.069f,
        _                  => 0f,
    };

    /// <summary>리더 — 체력이 낮을 때 회복량 배율.</summary>
    public static float RegrowthLowHpMult(SynergyTier t) => t switch
    {
        SynergyTier.Silver => 1.5f,
        SynergyTier.Gold   => 2.0f,
        _                  => 1f,
    };

    public const float RegrowthLowHpThreshold = 0.30f;

    /// <summary>
    /// 금 전용 — <b>치명상을 입어도 한 번은 체력 1 로 버틴다.</b>
    ///
    /// 재생의 정체성은 "시간이 갈수록 이득" 이다. 그런데 한 방에 죽으면 그
    /// 시간을 벌지 못한다. 한 번 버티게 해 주면 그 뒤는 재생이 알아서 메운다 —
    /// 숫자가 아니라 **재생이 일할 기회**를 주는 능력이다.
    ///
    /// ⚠ 개체당 한 번이다. 버틴 직후 또 맞으면 그대로 죽는다.
    /// </summary>
    public static bool RegrowthLastStand(SynergyTier t) => t == SynergyTier.Gold;

    // ── 야수 — 공격속도 (이동속도는 덤, 전투력 환산 0) ───────
    public static float BeastAttackSpeed(SynergyTier t) => t switch
    {
        SynergyTier.Bronze => 0.12f,
        SynergyTier.Silver => 0.26f,
        SynergyTier.Gold   => 0.55f,
        _                  => 0f,
    };

    public static float BeastMoveSpeed(SynergyTier t) => t switch
    {
        SynergyTier.Bronze => 0.15f,
        SynergyTier.Silver => 0.25f,
        SynergyTier.Gold   => 0.35f,
        _                  => 0f,
    };

    // ── 야수 리더 ────────────────────────────────────────────
    //
    //  ⚠ 야수만 리더가 없었다 (2026-09-03에 뒤늦게 발견)
    //    처음 설계에는 "처치 시 라인 공속 누적" 이 있었는데, 밸런스를 다시
    //    맞추면서 그 자리를 이동속도 숫자로 바꿔 버렸다. 그래서 여덟 중
    //    야수만 동·은·금이 숫자 두 줄로 똑같았다.
    //
    //  ⚠ 예산은 ×1.03 뿐이다 — 크게 주면 야수만 세진다
    //    금 수치가 이미 공속 +55%(DPS ×1.55) 를 먹었다. 누적 상한 10% 는
    //    교전 중 평균 절반쯤 쌓이므로 ×1.05 안쪽이다.

    /// <summary>처치 하나당 오르는 공격속도.</summary>
    public static float BeastSpeedPerKill(SynergyTier t) => t switch
    {
        SynergyTier.Silver => 0.01f,
        SynergyTier.Gold   => 0.01f,
        _                  => 0f,
    };

    /// <summary>누적 공격속도 상한. 판을 오래 끌어도 여기서 멈춘다.</summary>
    public static float BeastSpeedMax(SynergyTier t) => t switch
    {
        SynergyTier.Silver => 0.05f,
        SynergyTier.Gold   => 0.10f,
        _                  => 0f,
    };

    /// <summary>
    /// 금 전용 — 넉백에 밀리지 않는다.
    ///
    /// 숫자로 안 세는 리더다. 야수는 붙어서 때리는 계열이라 밀려나면 그때마다
    /// 다시 걸어가야 한다 — 공속을 몇 % 올리는 것보다 체감이 크고, 이미 있는
    /// 태그(KnockbackImmuneTag) 하나로 끝난다.
    /// </summary>
    public static bool BeastKnockbackImmune(SynergyTier t) => t == SynergyTier.Gold;

    // ── 투지 — 처치당 공격력 누적 ────────────────────────────
    //   평균 4처치, 누적은 점진 → 평균 배율 ≈ 1 + 2 × 처치당비율
    public static float FerocityPerKill(SynergyTier t) => t switch
    {
        SynergyTier.Bronze => 0.06f,
        SynergyTier.Silver => 0.13f,
        SynergyTier.Gold   => 0.27f,
        _                  => 0f,
    };

    /// <summary>
    /// 누적 상한. 판을 오래 끌어도 끝없이 커지지 않는다.
    ///
    /// ⚠ 금은 상한이 사실상 풀린다 — 그것이 금 전용 능력의 절반이다.
    /// </summary>
    public static int FerocityMaxStacks(SynergyTier t)
        => t == SynergyTier.Gold ? FerocityCarryCeiling : 8;

    /// <summary>
    /// 금 전용 — <b>처치 누적이 스테이지를 넘겨 유지된다.</b>
    ///
    /// 보통 누적은 개체와 함께 사라진다. 라인 복귀로 다시 나온 몬스터는
    /// 처음부터 다시 쌓아야 했다. 금은 그 기록을 **종족 단위로** 물려준다 —
    /// 판을 거듭할수록 그 종족이 강해진다.
    ///
    /// ⚠ 천장은 있다 (FerocityCarryCeiling)
    ///   30스테이지를 도는 게임이라 진짜 무제한이면 후반에 한 종족이
    ///   전부를 대신한다. 상한을 8에서 20으로 올리는 정도다.
    /// </summary>
    public static bool FerocityCarries(SynergyTier t) => t == SynergyTier.Gold;

    /// <summary>물려줄 수 있는 누적의 천장.</summary>
    public const int FerocityCarryCeiling = 20;

    // ── 종족별 누적 보관 ─────────────────────────────────────
    //
    //  ⚠ 엔티티가 아니라 **종족**에 붙는다
    //    라인 복귀는 개체를 despawn 하고 대기열에 종족만 남긴다. 개체에
    //    들고 있으면 그 순간 사라진다.
    //
    //  ⚠ 키를 문자열로 들고 다니지 않는다
    //    누적을 되쓰는 곳이 Burst 없는 시스템이긴 하지만 컴포넌트에는
    //    문자열을 넣을 수 없다. 종족 ID 를 정수 하나로 바꿔 컴포넌트에 심는다.

    static readonly Dictionary<string, int> _speciesKeys  = new();
    static readonly List<string>            _speciesById  = new();
    static readonly Dictionary<int, int>    _carriedStacks = new();

    /// <summary>종족 ID → 안정적인 정수 키. 0 은 "없음" 이라 1부터 센다.</summary>
    public static int KeyOf(string speciesId)
    {
        if (string.IsNullOrEmpty(speciesId)) return 0;
        if (_speciesKeys.TryGetValue(speciesId, out int key)) return key;

        _speciesById.Add(speciesId);
        key = _speciesById.Count;          // 1-based
        _speciesKeys[speciesId] = key;
        return key;
    }

    public static int CarriedStacks(int key)
        => key != 0 && _carriedStacks.TryGetValue(key, out int n) ? n : 0;

    /// <summary>누적을 물려준다. 이미 더 큰 값이 있으면 그대로 둔다.</summary>
    public static void CarryStacks(int key, int stacks)
    {
        if (key == 0) return;

        int capped = Mathf.Min(stacks, FerocityCarryCeiling);
        if (capped <= CarriedStacks(key)) return;

        _carriedStacks[key] = capped;
    }

    /// <summary>리더 — 죽을 때 누적치의 이만큼을 주변 투지 아군에게 물려준다.</summary>
    public static float FerocityInherit(SynergyTier t) => t switch
    {
        SynergyTier.Silver => 0.25f,
        SynergyTier.Gold   => 0.50f,
        _                  => 0f,
    };

    public const float FerocityInheritRadius = 3.2f;

    // ── 강철 — 방어율 (EHP 환산 = 1/(1−방어율)) ──────────────
    public static float SteelDefense(SynergyTier t) => t switch
    {
        SynergyTier.Bronze => 0.11f,
        SynergyTier.Silver => 0.21f,
        SynergyTier.Gold   => 0.35f,
        _                  => 0f,
    };

    /// <summary>
    /// 금 전용 — <b>한 번에 최대 체력의 이 비율을 넘게 잃지 않는다.</b>
    ///
    /// 방어율은 들어오는 피해를 **비율로** 깎는다. 그래서 한 방이 체력을 넘는
    /// 큰 피해(보스 강타·메테오)에는 사실상 무력했다 — 35%p 를 깎아도 죽는다.
    /// 상한은 그 한 방을 나눠 받게 만든다. 방어율을 더 주는 것과 종류가 다른
    /// 방어라, 숫자를 올리는 대신 금에만 둔다.
    ///
    /// ⚠ 0 이면 상한이 없다 (동·은).
    /// </summary>
    public static float SteelDamageCap(SynergyTier t) => t == SynergyTier.Gold ? 0.25f : 0f;

    /// <summary>리더 — 받은 피해의 이만큼을 공격자에게 되돌린다.</summary>
    public static float SteelThorn(SynergyTier t) => t switch
    {
        SynergyTier.Silver => 0.07f,
        SynergyTier.Gold   => 0.15f,
        _                  => 0f,
    };

    // ── 역병 — 공격이 남기는 중독 ────────────────────────────

    /// <summary>공격력 대비 초당 중독 피해.</summary>
    public static float PlagueDps(SynergyTier t) => t switch
    {
        SynergyTier.Bronze => 0.12f,
        SynergyTier.Silver => 0.26f,
        SynergyTier.Gold   => 0.55f,
        _                  => 0f,
    };

    public const float PlagueSeconds = 3f;

    /// <summary>
    /// 금 전용 — <b>역병 몬스터가 죽으면 주변 적에게 역병이 퍼진다.</b>
    ///
    /// 역병은 "시간이 갈수록 이득" 인 계열인데, 정작 그 몬스터가 죽으면
    /// 뿌리던 중독도 함께 끊겼다. 금은 죽음 자체가 전파 수단이 된다 —
    /// 앞줄이 녹아도 독은 남는다.
    /// </summary>
    public static bool PlagueSpreadsOnDeath(SynergyTier t) => t == SynergyTier.Gold;

    /// <summary>사망 전파의 반경·세기. 종족 패시브의 역병 폭발과 같은 규격이다.</summary>
    public const float PlagueBurstRadius = 3.2f;
    public const float PlagueBurstDps    = 0.55f;

    /// <summary>
    /// 리더 — 중독된 적은 이만큼 피해를 더 받는다.
    ///
    /// ⚠ 이 값만 **적 진영 전체**에 곱해진다
    ///   다른 시너지는 내 몬스터에만 붙지만 이건 상대의 받는 피해라, 같은
    ///   지수라도 다른 시너지와 함께 쓸 때 실제 값이 더 크다. 그래서 낮게 잡았다.
    /// </summary>
    public static float PlagueAmplify(SynergyTier t) => t switch
    {
        SynergyTier.Silver => 0.03f,
        SynergyTier.Gold   => 0.07f,
        _                  => 0f,
    };

    // ── 술법 — 스킬 쿨다운 ───────────────────────────────────
    //
    //  ■ 예산 전부를 쿨다운에 싣는다 (2026-09-03, 사용자 확정)
    //    한때 "쿨다운 −x% + 스킬 피해 +y%" 로 나눠 두었다. 스킬 피해를 담을
    //    스탯(SkillPower)이 없어서 절반이 허공에 떠 있었고, 있었더라도
    //    한 시너지가 두 축으로 세지면 환산이 두 배로 복잡해진다.
    //    술법은 **스킬이 자주 나간다** 한 방향이다.
    //
    //  ■ 환산 (스킬이 DPS의 40% 라는 가정)
    //        스킬 배율 s = 1 / (1 − 쿨감)
    //        전투력지수  = 1 + 0.4 × (s − 1)
    //    동 1.12 → s 1.30 → 쿨감 0.23
    //    은 1.26 → s 1.65 → 쿨감 0.39   (나머지 ×1.03 은 리더가 채운다)
    //    금 1.55 → s 2.38 → 쿨감 0.58
    //
    //  ⚠ 상한은 GameplayConfig.CooldownCap(0.9) 이다. 금 0.58 은 그 안이지만,
    //    어빌리티·유물 쿨감과 곱연산으로 겹치므로 상한에 먼저 닿을 수 있다.
    public static float SorceryCooldownReduce(SynergyTier t) => t switch
    {
        SynergyTier.Bronze => 0.23f,
        SynergyTier.Silver => 0.39f,
        SynergyTier.Gold   => 0.58f,
        _                  => 0f,
    };

    /// <summary>
    /// 리더 — 쿨다운이 이만큼 **채워진 채로** 소환된다.
    ///
    /// ⚠ "소환되자마자 스킬을 쓴다" 가 아니다 (2026-09-03 수정)
    ///   그건 아직 적을 만나지도 않은 자리에서 스킬을 허공에 버리는 것이라
    ///   값이 0 이었다. 쿨다운만 채워 두면 **적을 처음 마주친 순간** 첫 스킬이
    ///   나간다 — 노린 것은 그 타이밍이지 소환 순간이 아니다.
    /// </summary>
    public static float SorceryStartCharge(SynergyTier t) => t switch
    {
        SynergyTier.Silver => 0.50f,
        SynergyTier.Gold   => 1.00f,
        _                  => 0f,
    };

    // ── 중첩 보너스 — 켜진 시너지 개수 ───────────────────────
    //
    //  ⚠ 금 하나에 덱의 6~7칸이 잠기므로 금과 중첩은 서로 배타적이다.
    //    그래서 중첩은 얕고 넓게 준다 — 여기서 크게 주면 "아무거나 두 장씩"
    //    이 최적해가 되어 시너지를 고를 이유가 사라진다.

    /// <summary>중첩 문턱 — 켜진 시너지 수. 효과·칩·툴팁이 전부 이 표 하나를 본다.</summary>
    public static readonly int[] StackThresholds = { 3, 5, 7 };

    public static int StackStep(int activeCount)
    {
        int step = 0;
        for (int i = 0; i < StackThresholds.Length; i++)
            if (activeCount >= StackThresholds[i]) step = i + 1;
        return step;
    }

    /// <summary>중첩 보너스 — 공격력·최대 체력에 함께 붙는 비율.</summary>
    public static float StackBonus(int activeCount)
    {
        float b = StackStep(activeCount) switch
        {
            1 => 0.020f,
            2 => 0.045f,
            3 => 0.070f,
            _ => 0f,
        };

        // 유물 '공명의 서' — 중첩이 **하나라도 켜져 있을 때만** 얹는다.
        // ⚠ 켜지지 않은 상태에 더하면 시너지를 안 쓰는 덱도 공짜로 받는다.
        return b > 0f
             ? b + RelicTreeApplier.GetSystemValue(RelicSystemEffect.SynergyStackBonus)
             : 0f;
    }

    /// <summary>중첩 2단계부터 — 소환 직후 잠깐 빨라져 전장에 빨리 붙는다.</summary>
    public const float StackRushMoveBonus = 0.50f;
    public const float StackRushSeconds   = 2f;

    /// <summary>
    /// 중첩 3단계(7개) — 라인 복귀한 몬스터가 <b>한 마리 늘어</b> 돌아온다.
    ///
    /// ⚠ 이건 공짜 물량이다 — 의도한 예외다
    ///   보통은 마나를 내지 않고 나온 개체를 대기열에 넣지 않는다
    ///   (분열체·부활체. MonsterLineReturner 주석 참고). 판을 거듭할수록
    ///   물량이 불어나 소환 경제가 무너지기 때문이다.
    ///   시너지 7개를 동시에 켜는 것은 덱 열 칸을 거의 다 쓰는 일이라,
    ///   그 대가로 딱 한 번 이 규칙을 깬다.
    ///
    ///   ⚠ 살아남은 개체만 는다 — 전멸하면 아무것도 안 돌아온다.
    ///     그래서 무한히 불어나지는 않는다.
    /// </summary>
    public static bool StackDoublesLineReturn(int activeCount) => StackStep(activeCount) >= 3;

    // ── 표시 ─────────────────────────────────────────────────

    public static string NameOf(MonsterTag tag) => tag switch
    {
        MonsterTag.Undead   => "언데드",
        MonsterTag.Forest   => "숲",
        MonsterTag.Beast    => "야수",
        MonsterTag.Regrowth => "재생",
        MonsterTag.Ferocity => "투지",
        MonsterTag.Steel    => "강철",
        MonsterTag.Plague   => "역병",
        MonsterTag.Sorcery  => "술법",
        _                   => "",
    };

    public static string NameOf(SynergyTier tier) => tier switch
    {
        SynergyTier.Bronze => "동",
        SynergyTier.Silver => "은",
        SynergyTier.Gold   => "금",
        _                  => "",
    };

    /// <summary>단계 색 — 동·은·금. 아직 안 열린 것은 흐린 회색이다.</summary>
    /// <summary>
    /// 단계 색.
    ///
    /// ⚠ 은은 <b>푸른 은</b>이다 — 회색이면 '꺼짐' 과 구분이 안 된다
    ///   (사용자 지적, 2026-09-07) 전에는 (0.82, 0.86, 0.92) 라 거의 흰 회색이었다.
    ///   칩 바탕은 이 색을 어둡게 깐 것이라(SynergyBarUI.Fill), 꺼짐
    ///   (0.48, 0.52, 0.62 — 푸른 회색)과 **같은 색조에 밝기만 다른** 상태가 됐다.
    ///   그래서 4종을 모아 은에 닿아도 "색이 안 바뀐다" 로 보였다.
    ///   지금은 채도를 준 하늘빛이라 동(주황)·금(호박)·꺼짐(회색)과 다 갈린다.
    ///
    /// ⚠ 네 색은 한 묶음이다 — 하나를 바꾸면 나머지 셋과 다시 대조할 것.
    ///   특히 은과 꺼짐은 **색조가 달라야** 한다(밝기 차이만으로는 안 읽힌다).
    /// </summary>
    public static Color ColorOf(SynergyTier tier) => tier switch
    {
        SynergyTier.Bronze => new Color(0.90f, 0.55f, 0.28f),   // 주황
        SynergyTier.Silver => new Color(0.55f, 0.80f, 1.00f),   // 하늘빛 은
        SynergyTier.Gold   => new Color(1.00f, 0.82f, 0.30f),   // 호박
        _                  => new Color(0.46f, 0.48f, 0.55f),   // 꺼짐 — 채도 없는 회색
    };

    /// <summary>TMP 리치 텍스트용 16진 색. "숲 2/3" 을 단계 색으로 칠할 때 쓴다.</summary>
    public static string HexOf(SynergyTier tier) => ColorUtility.ToHtmlStringRGB(ColorOf(tier));

    // ── 효과 설명 ────────────────────────────────────────────
    //
    //  ⚠ 글을 손으로 적지 않는다 — 위 수치 함수에서 뽑아 쓴다
    //    한때 밸런스 표와 화면 설명이 따로 놀아, 수치를 고쳐도 툴팁은 옛날
    //    숫자를 말하는 일이 생겼다. 여기서 만들면 그럴 수가 없다.

    static string Pct(float ratio)   => $"{Mathf.RoundToInt(ratio * 100f)}%";
    static string Pp(float ratio)    => $"{Mathf.RoundToInt(ratio * 100f)}%p";
    static string Pct1(float ratio)  => $"{ratio * 100f:0.#}%";

    /// <summary>한 단계가 무엇을 주는지 한 줄로.</summary>
    public static string Describe(MonsterTag tag, SynergyTier tier)
    {
        if (tier == SynergyTier.None) return "";

        switch (tag)
        {
            case MonsterTag.Forest:
            {
                string line = $"최대 체력 +{Pct(ForestHpBonus(tier))}";
                float heal = ForestDeathHeal(tier);
                if (heal > 0f) line += $" · 죽을 때 주변 숲 아군 {Pct(heal)} 회복";
                return line;
            }

            case MonsterTag.Undead:
            {
                string line = $"죽으면 {Pct(UndeadReviveChance(tier))} 확률로 "
                            + $"체력 {Pct(UndeadReviveHp(tier))} 로 부활";
                float guard = UndeadReviveGuard(tier);
                if (guard > 0f)
                    line += $" · 부활 후 {UndeadGuardSeconds:0.#}초간 피해 {Pct(guard)} 감소";
                return line;
            }

            case MonsterTag.Beast:
            {
                string line = $"공격속도 +{Pct(BeastAttackSpeed(tier))} · "
                            + $"이동속도 +{Pct(BeastMoveSpeed(tier))}";

                float cap = BeastSpeedMax(tier);
                if (cap > 0f)
                    line += $" · 처치마다 공격속도 +{Pct(BeastSpeedPerKill(tier))}"
                          + $" (최대 +{Pct(cap)})";

                return line;
            }

            case MonsterTag.Regrowth:
            {
                string line = $"초당 최대 체력 {Pct1(RegrowthPerSecond(tier))} 회복";
                float mult = RegrowthLowHpMult(tier);
                if (mult > 1f)
                    line += $" · 체력 {Pct(RegrowthLowHpThreshold)} 이하에서 {mult:0.#}배";
                return line;
            }

            case MonsterTag.Ferocity:
            {
                string line = $"처치마다 공격력 +{Pct(FerocityPerKill(tier))} "
                            + $"(최대 {FerocityMaxStacks(tier)}회)";
                float share = FerocityInherit(tier);
                if (share > 0f)
                    line += $" · 죽을 때 누적치 {Pct(share)} 를 주변 투지 아군에게";
                return line;
            }

            case MonsterTag.Steel:
            {
                string line = $"방어율 +{Pp(SteelDefense(tier))}";
                float thorn = SteelThorn(tier);
                if (thorn > 0f) line += $" · 받은 피해 {Pct(thorn)} 반사";
                return line;
            }

            case MonsterTag.Plague:
            {
                string line = $"공격에 공격력 {Pct(PlagueDps(tier))} 짜리 중독"
                            + $"({PlagueSeconds:0.#}초)";
                float amp = PlagueAmplify(tier);
                if (amp > 0f) line += $" · 중독된 적이 받는 피해 +{Pct(amp)}";
                return line;
            }

            case MonsterTag.Sorcery:
            {
                string line = $"스킬 쿨다운 −{Pct(SorceryCooldownReduce(tier))}";
                float charge = SorceryStartCharge(tier);
                if (charge > 0f && charge < 1f) line += $" · 쿨다운 {Pct(charge)} 채운 채 소환";
                return line;
            }
        }

        return "";
    }

    /// <summary>
    /// 금 단계에서만 열리는 <b>특별한 능력</b>. 없으면 빈 문자열.
    ///
    /// ■ 왜 따로 뽑나
    ///   금은 "은의 두 배 숫자" 가 아니라 **규칙을 바꾸는 능력**을 하나씩 갖는다.
    ///   숫자 줄에 " · " 로 이어 붙이면 그 줄에 묻혀 읽히지 않는다.
    ///   툴팁도 이 값을 **따로 한 줄** 잡아 그린다 (DescribeAll).
    ///
    /// ⚠ 여덟 시너지 전부에 하나씩 있다. 새 시너지를 만들면 여기도 채울 것 —
    ///   비어 있으면 그 시너지는 금을 향할 이유가 없다.
    /// </summary>
    public static string GoldAbility(MonsterTag tag) => tag switch
    {
        MonsterTag.Forest   => "죽어도 제 라인 대기열로 돌아온다",
        MonsterTag.Undead   => "부활한 개체가 한 번 더 부활한다",
        MonsterTag.Beast    => "넉백에 밀리지 않는다",
        MonsterTag.Regrowth => "치명상을 입어도 한 번은 체력 1 로 버틴다",
        MonsterTag.Ferocity => $"처치 누적 상한이 {FerocityCarryCeiling}회로 풀리고, "
                             + "스테이지를 넘겨 유지된다",
        MonsterTag.Steel    => $"한 번에 최대 체력의 {Pct(SteelDamageCap(SynergyTier.Gold))} 를 "
                             + "넘게 잃지 않는다",
        MonsterTag.Plague   => "죽을 때 주변 적에게 역병이 퍼진다",
        MonsterTag.Sorcery  => "스킬을 준비한 채로 소환된다",
        _                   => "",
    };

    /// <summary>
    /// 세 단계를 한 덩이로. 이미 열린 단계는 그 단계 색, 아직인 것은 흐리게.
    /// 툴팁이 이걸 그대로 띄운다.
    ///
    /// ⚠ 금 전용 능력은 **줄을 바꿔서** 따로 적는다 (사용자 확정, 2026-09-04)
    ///   숫자 줄 뒤에 이어 붙이면 그 줄이 길어져 능력이 묻힌다.
    /// </summary>
    public static string DescribeAll(MonsterTag tag)
    {
        SynergyTier now = TierOf(tag);
        var sb = new System.Text.StringBuilder();

        // ⚠ "지금 N" 줄은 없앴다 (사용자 지적, 2026-09-07)
        //   칩이 이미 현재 개수를 띄우고(StepsLabelOf) 제목도 "숲 3/5" 라고
        //   말한다. 툴팁이 같은 숫자를 세 번째로 되풀이하고 있었다.
        //   제단으로 얹은 몫은 제목(TitleOf)이 이어받는다 — 그 숫자가
        //   어디서 왔는지는 여전히 보여야 한다.

        foreach (SynergyTier tier in new[]
                 { SynergyTier.Bronze, SynergyTier.Silver, SynergyTier.Gold })
        {
            int need = tier switch
            {
                SynergyTier.Bronze => BronzeAt(tag),
                SynergyTier.Silver => SilverAt(tag),
                _                  => GoldAt(tag),
            };

            bool open = now >= tier;

            // 열린 단계만 단계 색으로 칠한다 — 흐린 줄이 곧 "아직 못 받은 것" 이다.
            string hex = open ? HexOf(tier) : ColorUtility.ToHtmlStringRGB(ColorOf(SynergyTier.None));

            if (sb.Length > 0) sb.Append('\n');
            sb.Append("<color=#").Append(hex).Append('>')
              .Append(need).Append("  ").Append(Describe(tag, tier))
              .Append("</color>");
        }

        // ── 금 전용 능력 — 제 줄을 갖는다 ──
        string ability = GoldAbility(tag);

        if (ability.Length > 0)
        {
            // 금에 닿았으면 금색, 아직이면 흐리게. 앞의 '›' 가 "이건 다른 종류" 를 말한다.
            string hex = now >= SynergyTier.Gold
                       ? HexOf(SynergyTier.Gold)
                       : ColorUtility.ToHtmlStringRGB(ColorOf(SynergyTier.None));

            sb.Append('\n')
              .Append("<color=#").Append(hex).Append(">›  <b>")
              .Append(ability)
              .Append("</b></color>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 칩에 붙는 문턱 줄 — "2/4/6". 달성한 문턱만 그 단계 색으로 칠한다.
    ///
    /// ⚠ 지금 장수 하나만 띄우면 "그래서 몇 장이 목표인데" 를 알 수 없다
    ///   한때 "2" 만 띄웠는데, 그게 달성한 문턱인지 그냥 개수인지도
    ///   구분되지 않았다. 세 문턱을 다 보여 주고 도달한 것만 밝히면
    ///   현재 단계와 다음 목표가 한 줄에 같이 들어간다.
    /// </summary>
    public static string StepsLabelOf(MonsterTag tag)
    {
        int have = CountOf(tag);
        Steps s  = StepsOf(tag);

        var sb = new System.Text.StringBuilder();

        // ── 지금 몇인지가 맨 앞이다 (사용자 지적, 2026-09-07) ──
        //   문턱 셋만 띄웠더니 "2/4/6" 이 **목표만** 말하고 현재 개수를
        //   말하지 않았다. 도달한 문턱이 굵어지긴 하지만 그건 "넘었다"
        //   까지고, 다음 문턱까지 몇 장 남았는지는 세어 봐야 했다.
        //
        //   ⚠ 크기를 갈라야 한 덩어리로 안 읽힌다
        //     같은 크기로 "3 2/4/6" 을 붙이면 숫자 넷이 나란해 어느 것이
        //     현재값인지 다시 봐야 한다. 현재값만 제 크기로 두고 문턱을
        //     70% 로 내리면 눈이 먼저 가는 곳이 하나로 정해진다.
        //   ⚠ %(상대 크기)를 쓴다 — 폰트 크기를 박으면 UIScale 단계를
        //     올렸을 때 이 줄만 옛 크기로 남는다.
        sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(ColorOf(TierOf(tag))))
          .Append("><b>").Append(have).Append("</b></color><size=70%> ");

        Step(sb, have, s.Bronze, SynergyTier.Bronze);
        sb.Append("<color=#").Append(DimHex).Append(">/</color>");
        Step(sb, have, s.Silver, SynergyTier.Silver);
        sb.Append("<color=#").Append(DimHex).Append(">/</color>");
        Step(sb, have, s.Gold,   SynergyTier.Gold);

        sb.Append("</size>");
        return sb.ToString();
    }

    static void Step(System.Text.StringBuilder sb, int have, int need, SynergyTier tier)
    {
        bool reached = have >= need;

        sb.Append("<color=#").Append(reached ? HexOf(tier) : DimHex).Append('>');
        if (reached) sb.Append("<b>");
        sb.Append(need);
        if (reached) sb.Append("</b>");
        sb.Append("</color>");
    }

    static string DimHex => ColorUtility.ToHtmlStringRGB(ColorOf(SynergyTier.None));

    /// <summary>툴팁 제목 — "숲  3/5" 또는 이미 금이면 "숲  7".</summary>
    public static string TitleOf(MonsterTag tag)
    {
        int step = NextStepAt(tag);
        int boon = BoonCountOf(tag);

        string head = step > 0
            ? $"{NameOf(tag)}  {CountOf(tag)}/{step}"
            : $"{NameOf(tag)}  {CountOf(tag)}";

        // ⚠ 제단으로 얹은 몫은 반드시 눈에 보여야 한다
        //   카드를 잃고 얻은 숫자라, 어디서 왔는지 안 보이면 제물이
        //   아무 일도 안 한 것처럼 읽힌다. (한때 툴팁 본문의 "지금 N" 줄이
        //   말했는데, 그 줄이 칩·제목과 같은 숫자를 세 번째로 되풀이해 걷어냈다)
        return boon > 0 ? $"{head}  (제단 +{boon})" : head;
    }

    // ── 중첩 표시 ────────────────────────────────────────────
    //
    //  ⚠ 한때 전투에만 걸리고 화면 어디에도 없었다 (사용자 지적, 2026-09-12)
    //    시너지를 셋 켜서 +2% 를 받고 있어도 플레이어는 그 보너스가 있는지조차 몰랐다.
    //    칩은 SynergyBarUI 맨 위 한 칸이다. 글은 위 수치 함수에서 뽑는다 (손으로 적지 않는다).

    /// <summary>중첩 칩의 줄 — "4 3/5/7". 켜진 시너지 수가 앞, 도달한 문턱만 단계 색 (StepsLabelOf 와 같은 짜임).</summary>
    public static string StackStepsLabel()
    {
        int have = ActiveCount;
        var sb   = new System.Text.StringBuilder();

        sb.Append("<color=#").Append(HexOf((SynergyTier)StackStep(have)))
          .Append("><b>").Append(have).Append("</b></color><size=70%> ");

        for (int i = 0; i < StackThresholds.Length; i++)
        {
            if (i > 0) sb.Append("<color=#").Append(DimHex).Append(">/</color>");
            Step(sb, have, StackThresholds[i], (SynergyTier)(i + 1));
        }

        sb.Append("</size>");
        return sb.ToString();
    }

    /// <summary>중첩 툴팁 제목 — "중첩  4/5" · 끝까지 갔으면 "중첩  7".</summary>
    public static string StackTitle()
    {
        int have = ActiveCount;
        foreach (int need in StackThresholds)
            if (have < need) return $"중첩  {have}/{need}";
        return $"중첩  {have}";
    }

    /// <summary>중첩 세 단계 — 열린 단계는 그 단계 색, 아직인 것은 흐리게 (DescribeAll 과 같은 짜임).</summary>
    public static string DescribeStack()
    {
        int step = StackStep(ActiveCount);
        var sb   = new System.Text.StringBuilder();

        for (int i = 0; i < StackThresholds.Length; i++)
        {
            int    need = StackThresholds[i];
            string line = $"공격력·최대 체력 +{Pct1(StackBonus(need))}";
            if (i == 1) line += $" · 소환 직후 {StackRushSeconds:0.#}초간 이동 속도 +{Pct(StackRushMoveBonus)}";
            if (i == 2) line += " · 라인 복귀 때 한 마리 더 (라인·종족마다 한 번)";

            string hex = step > i ? HexOf((SynergyTier)(i + 1)) : DimHex;

            if (sb.Length > 0) sb.Append('\n');
            sb.Append("<color=#").Append(hex).Append('>')
              .Append(need).Append("  ").Append(line)
              .Append("</color>");
        }

        sb.Append('\n').Append("<color=#").Append(DimHex)
          .Append("><size=85%>켜진 시너지 수로 붙는다 · 윗 단계는 아랫 단계 효과를 함께 받는다 · " +
                  "켜진 표식을 가진 몬스터만 받는다</size></color>");
        return sb.ToString();
    }

}
