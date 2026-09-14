using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  RunNodeFlow.cs
//  갈림길과 시설의 **화면 흐름**. ChoicePopup 을 여닫는 일만 한다.
//
//  ■ 흐름
//    스테이지 클리어 → 카드 3택 → (허들이면 특성) → 갈림길 2택
//      → 전투를 골랐으면 곧장 다음 판
//      → 시설을 골랐으면 그 화면들을 지난 뒤 다음 판 (그 판에는 전투가 없다)
//
//  ■ ⚠ 어느 길로 빠지든 반드시 onDone 을 부른다
//    팝업이 없거나, 살 것이 없거나, 그냥 닫아도 마찬가지다.
//    한 갈래라도 빠뜨리면 런이 그 자리에서 멈춘다 — 다음 스테이지로 넘어갈
//    유일한 신호가 이것뿐이기 때문이다.
//
//  ■ ⚠ 골드는 낸 뒤에 물건을 준다
//    순서를 뒤집으면 잔액이 모자란 순간이 공짜가 된다.
//    낼 수 없는 줄은 애초에 흐리게 그려 못 누르게 한다(Entry.Enabled).
//
//  ■ 이벤트만 화면이 **두 단계**다 (2026-09-08)
//    고르는 화면 → 결과 화면. 시설은 결과가 HUD 에 바로 보이지만
//    (체력·골드·마나 숫자가 움직인다) 이벤트는 덱 안쪽을 건드리는 갈래가
//    있어, 무슨 일이 있었는지 말해 주지 않으면 확인할 길이 없다.
//    ⚠ 같은 FacilityPopup 을 두 번 연다 — 강화소가 이미 하는 짜임이다.
// ============================================================

public static class RunNodeFlow
{
    /// <summary>
    /// 갈림길을 띄운다. 고른 것이 <b>다음 스테이지</b>가 된다.
    ///
    /// 전투를 고르면 시설을 열지 않고 곧장 <paramref name="onPicked"/> 로 넘긴다.
    /// 시설을 고르면 그 시설의 화면을 다 지난 뒤에 넘긴다 — 어느 쪽이든
    /// 부르는 쪽(RunBootstrap)이 고른 종류를 받아 다음 판을 세운다.
    /// </summary>
    public static void OpenCrossroad(Action<RunNodeKind> onPicked)
    {
        List<RunNodeKind> nodes = RunNodeRule.Pick();

        // ⚠ 조용히 넘어가지 않는다 — 화면에는 "갈림길이 안 뜬다" 로만 보인다
        if (nodes.Count == 0)
        {
            Debug.LogError("[RunNodeFlow] 갈림길 후보가 하나도 뽑히지 않았습니다 — " +
                           "RunNodeRule.Table / Implemented 를 확인하세요. 일반 전투로 넘어갑니다.");
            onPicked?.Invoke(RunNodeKind.NormalBattle);
            return;
        }

        // ⚠ 팝업이 아니라 **인게임 화면**이다 (사용자 확정, 2026-09-06)
        //   전장 오른쪽 위·아래에 두 갈래를 띄운다 (CrossroadUI 머리 주석 참고).
        //   가운데 목록 창으로 두면 이 선택이 "창을 하나 더 닫는 일" 로 읽힌다.
        var ui = CrossroadUI.Instance;

        if (ui == null)
        {
            // ⚠ 조용히 넘어가지 않는다 — 갈림길이 통째로 사라진 것처럼 보인다
            Debug.LogError("[RunNodeFlow] CrossroadUI 가 씬에 없습니다 — 갈림길을 띄우지 못해 " +
                           "일반 전투로 넘어갑니다. " +
                           "Tools > Project K > UI > 인게임 HUD 를 다시 실행하세요.");
            onPicked?.Invoke(RunNodeKind.NormalBattle);
            return;
        }

        ui.Show(nodes, node =>
        {
            // 전투는 시설 화면이 없다 — 그대로 다음 판이 된다.
            if (node.IsBattle()) { onPicked?.Invoke(node); return; }

            Enter(node, () => onPicked?.Invoke(node));
        });
    }

    static void Enter(RunNodeKind kind, Action onDone)
    {
        // 배경 그림·이야기는 이 값으로 고른다 — 화면마다 인자로 끌고 다니면
        // 강화소처럼 두 단계인 시설에서 한쪽을 빠뜨리기 쉽다.
        _current = kind;

        switch (kind)
        {
            case RunNodeKind.Camp:  OpenCamp(onDone);  break;
            case RunNodeKind.Forge: OpenForge(onDone); break;
            case RunNodeKind.Altar: OpenAltar(onDone); break;
            case RunNodeKind.Shop:  OpenShop(onDone);  break;
            case RunNodeKind.Event: OpenEvent(onDone); break;

            default:
                Debug.LogWarning($"[RunNodeFlow] 아직 만들지 않은 시설: {kind}");
                onDone?.Invoke();
                break;
        }
    }

    // ── 야영지 ──────────────────────────────────────────────

    static void OpenCamp(Action onDone)
    {
        var core = UserDataManager.Instance?.Get<RunCoreData>();
        if (core == null) { onDone?.Invoke(); return; }

        int gold = RunGoldRule.Current;

        // ⚠ 수리는 무료다 (사용자 확정) — 마왕성도 마나도 가득 찼을 때만 못 고른다.
        //   골드가 없어 아무것도 못 하는 야영지가 되면 갈림길 한 칸이 죽는다.
        // ⚠ 수리는 마나도 채운다 · 증축은 최대 마나도 늘린다 (사용자 지시, 2026-09-12)
        var  mana     = UserDataManager.Instance.Get<SummonManaData>();
        int  manaFill = Mathf.FloorToInt(mana.Max * RunNodeRule.CampManaRatio);
        bool canHeal  = core.Current < core.Max || mana.Current < mana.Max;
        int  maxCost  = RunNodeRule.CampMaxCost(StageNumber);
        bool canMax   = gold >= maxCost;

        var entries = new List<ChoicePopup.Entry>(2)
        {
            new("수리   무료",
                !canHeal
                    ? "이미 온전하다"
                    : $"마왕성 체력 +{CampHeal} · 마나 {RunNodeRule.CampManaRatio * 100f:0}% 회복   " +
                      $"(현재 {core.Current}/{core.Max})",
                canHeal),

            new($"증축   {maxCost} G",
                $"마왕성 최대 체력 +{RunNodeRule.CampMaxAmount} · 최대 마나 +{RunNodeRule.CampMaxManaAmount}" +
                "   — 늘어난 만큼 곧바로 채워진다",
                canMax),
        };

        // ⚠ 제목에 지갑을 적지 않는다 — 오른쪽 위 금화 배지가 정본이다 (2026-09-09)
        Open("야영지", entries, blockClose: false, onDone: onDone,
             onPicked: i =>
             {
                 if (i == 0)
                 {
                     core.Heal(CampHeal);
                     RunPerkRule.RestoreMana(manaFill);
                 }
                 else if (RunGoldRule.TrySpend(maxCost))
                 {
                     core.AddMax(RunNodeRule.CampMaxAmount);
                     RunPerkRule.GrowMaxMana(RunNodeRule.CampMaxManaAmount);
                 }

                 UserDataManager.Instance?.RequestSave();
                 onDone?.Invoke();
             });
    }

    // ── 이벤트 ──────────────────────────────────────────────
    //
    //  ■ 시설과 무엇이 다른가 — **지불 수단**이다 (RunEventRule 머리 주석)
    //    시설 넷은 골드로 산다. 이벤트는 마왕성 체력으로 산다. 그래서
    //    지갑이 비었을 때 고를 수 있는 유일한 칸이고, 대신 값이 목숨이다.
    //
    //  ■ 갈래는 셋이다 — 보상 · 골드 · 회복 (사용자 확정, 2026-09-08)
    //    ⚠ 회복은 야영지보다 적다 (RunEventRule.HealAmount) — 같거나 많으면
    //      야영지를 고를 이유가 사라진다.
    //
    //  ■ 무작위는 **어느 이벤트를 만나는가** 뿐이다
    //    고른 뒤의 결과는 확정이다 — 원작 EventChoice.SuccessRate 는 안 쓴다.

    static void OpenEvent(Action onDone)
    {
        var run = UserDataManager.Instance?.Get<SummonRunData>();

        // ⚠ 후보를 다 만났으면 기록을 비우고 다시 돈다
        //   후보가 없다고 건너뛰면 그 런의 이벤트 칸이 조용히 사라진다.
        if (run != null && run.SeenEvents.Count >= RunEventRule.Count)
            run.ClearSeenEvents();

        RunEventId  id  = RunEventRule.Pick(run?.SeenEvents);
        RunEventDef def = RunEventRule.Get(id);

        run?.MarkEventSeen(id);

        // ⚠ FacilityPopup 을 직접 연다 (공용 Open 을 안 쓴다)
        //   같은 창을 결과 화면으로 갈아 끼워야 해서 참조를 들고 있어야 한다.
        //   강화소·제단이 제 팝업을 직접 여는 것과 같은 짜임이다.
        //   (배경 그림은 Setup 에 넘기는 RunNodeKind.Event 가 고른다 — _current 를
        //    쓰지 않으므로 여기서 건드릴 것이 없다. 그 값의 주인은 Enter 하나다)
        var popup = PopupManager.Instance?.Open<FacilityPopup>(PopupType.Facility);

        if (popup == null)
        {
            Debug.LogWarning("[RunNodeFlow] FacilityPopup 을 열지 못했습니다 — 이벤트를 건너뜁니다. " +
                             "Tools > Project K > 프리팹 생성 > 팝업 > 시설 을 실행하고 " +
                             "PopupManager > Load Popup Prefabs 를 누르세요.");
            onDone?.Invoke();
            return;
        }

        int    stage = StageNumber;
        Action done  = Once(onDone);

        // 보상 → 골드 → 회복. ⚠ 순서를 뒤집지 말 것 — 여덟 이벤트가 같은
        // 자리에 같은 성격을 두어야 두 번째부터는 읽지 않고도 안다.
        // (순서의 정본은 RunEventDef.Choices 다)
        RunEventChoice[] choices = def.Choices;

        var entries = new List<ChoicePopup.Entry>(choices.Length);
        foreach (RunEventChoice c in choices)
            entries.Add(new ChoicePopup.Entry(c.Label,
                                              RunEventRule.Describe(c, stage),
                                              RunEventRule.Available(c)));

        // ⚠ 닫을 수 있다 — 셋 다 마음에 안 들면 그냥 지나갈 수 있어야 한다.
        //   보상 갈래가 흐려진 판에서 남은 둘을 강요하면 통보가 된다.
        popup.Setup(RunNodeKind.Event, stage, def.Title, entries,
                    i => ShowEventResult(popup, def, stage,
                                         RunEventRule.Apply(choices[i], stage), done),
                    blockClose: false, flavor: def.Body, keepOpenOnPick: true,
                    eventId: def.Id);

        // ⚠ 반드시 Setup 뒤에 건다 — Setup 은 콜백만 갈아 끼우고 이건 안 건드린다.
        popup.SetOnClose(done);
    }

    /// <summary>
    /// 무슨 일이 있었는지 한 줄로 말하고 넘어간다.
    ///
    /// ⚠ 같은 창을 그 자리에서 갈아 끼운다 — 닫고 다시 열지 않는다
    ///   FacilityPopup._keepOpenOnPick 주석 참고. 닫는 도중 같은 PopupType 을
    ///   다시 열면 인스턴스가 한 벌 더 생긴다.
    ///
    /// ⚠ 줄이 하나뿐이어도 선택지 화면을 쓴다
    ///   결과 전용 팝업을 만들면 PopupType 이 늘고, 이벤트가 아니면 아무도
    ///   안 여는 창이 된다. '계속' 한 줄이면 같은 무대로 충분하다.
    /// </summary>
    static void ShowEventResult(FacilityPopup popup, RunEventDef def, int stage,
                                string result, Action done)
    {
        var entries = new List<ChoicePopup.Entry>(1)
        {
            new("계속", "다음 판으로 넘어간다", true),
        };

        // ⚠ blockClose 다 — 바깥을 눌러 닫히면 무엇을 얻었는지 못 본다.
        // ⚠ 그림도 그대로 둔다 — 결과 화면에서 배경이 바뀌면 다른 곳으로
        //   넘어온 것처럼 보인다. 같은 장면에서 다음 줄이 이어져야 한다.
        popup.Setup(RunNodeKind.Event, stage, def.Title, entries,
                    _ => done?.Invoke(), blockClose: true, flavor: result,
                    eventId: def.Id);

        // ⚠ 여기서 다시 걸어야 한다 — Choose 가 고른 순간 _onClose 를 비운다.
        //   안 걸면 결과 화면을 ✕ 로 닫았을 때 **런이 그 자리에서 멈춘다.**
        popup.SetOnClose(done);
    }

    // ── 상점 ────────────────────────────────────────────────
    //
    //  ■ ⚠ 파는 것은 **최대 마나 +1** 하나뿐이다 (사용자 확정)
    //    카드와 특성을 매 판 끝에 살 수 있으면 갈림길만으로 빌드가 완성돼
    //    카드 3택·보스 보상이 전부 곁다리가 된다. 상점은 "조금씩 두꺼워지는"
    //    자리이지 빌드를 사는 자리가 아니다.

    /// <summary>
    /// 상점 — 카드 · 특성 · 마력의 정수를 런 골드로 산다.
    ///
    /// ⚠ 전체화면 전용 팝업이다 (사용자 요청, 2026-09-07)
    ///   전에는 ChoicePopup 에 "마력의 정수" 한 줄뿐이었다. 상점이 글자 한 줄이면
    ///   갈림길에서 상점을 고를 이유가 없다 — 무엇을 파는지도 안 보인다.
    ///   재고·값의 정본은 RunShopRule 이고, 화면은 ShopPopup 이다.
    /// </summary>
    static void OpenShop(Action onDone)
    {
        var popup = PopupManager.Instance?.Open<ShopPopup>(PopupType.Shop);

        if (popup == null)
        {
            // ⚠ 조용히 넘어가지 않는다 — 상점이 통째로 사라진 것처럼 보인다
            Debug.LogError("[RunNodeFlow] 상점 화면을 열지 못했습니다 — 그냥 넘어갑니다.\n" +
                           "Tools > Project K > 프리팹 생성 > 팝업 > 상점 을 실행하고 " +
                           "PopupManager > Load Popup Prefabs 를 누르세요.");
            onDone?.Invoke();
            return;
        }

        popup.Setup(StageNumber, onDone);
    }

    // ── 강화소 ──────────────────────────────────────────────

    static void OpenForge(Action onDone)
    {
        var deck = UserDataManager.Instance?.Get<SummonDeckData>();
        if (deck == null) { onDone?.Invoke(); return; }

        List<int> slots = CollectForgeSlots(deck);
        if (slots.Count == 0) { onDone?.Invoke(); return; }

        // ⚠ 글자 목록이 아니라 **카드 격자**다 (사용자 지적, 2026-09-06)
        //   카드를 고르는 자리인데 카드가 안 보이면 무엇을 강화하는지 알 수 없다.
        var popup = PopupManager.Instance?.Open<ForgePopup>(PopupType.Forge);

        if (popup == null)
        {
            Debug.LogWarning("[RunNodeFlow] ForgePopup 을 열지 못했습니다 — 건너뜁니다. " +
                             "Tools > Project K > 프리팹 생성 > 팝업 > 강화소 를 실행하고 " +
                             "PopupManager > Load Popup Prefabs 를 누르세요.");
            onDone?.Invoke();
            return;
        }

        popup.Setup(StageNumber, slots, Once(onDone));
        popup.SetOnClose(Once(onDone));
    }

    /// <summary>
    /// 야영지가 회복시키는 마왕성 체력. 유물 '재건' 이 더한다.
    ///
    /// ⚠ 표시와 실제가 같은 값을 봐야 한다 — 그래서 상수를 직접 쓰지 않는다.
    /// </summary>
    static int CampHeal
        => RunNodeRule.CampHealAmount
         + RelicTreeApplier.GetSystemInt(RelicSystemEffect.CampHealBonus);

    // ── 제단 ────────────────────────────────────────────────

    static void OpenAltar(Action onDone)
    {
        var deck = UserDataManager.Instance?.Get<SummonDeckData>();
        if (deck == null) { onDone?.Invoke(); return; }

        List<int> slots = CollectMonsterSlots(deck);

        // ⚠ 마지막 한 장은 못 바친다 — 덱이 비면 아무것도 소환할 수 없다.
        if (slots.Count <= 1) { onDone?.Invoke(); return; }

        var popup = PopupManager.Instance?.Open<AltarPopup>(PopupType.Altar);

        if (popup == null)
        {
            Debug.LogWarning("[RunNodeFlow] AltarPopup 을 열지 못했습니다 — 건너뜁니다. " +
                             "Tools > Project K > 프리팹 생성 > 팝업 > 제단 을 실행하고 " +
                             "PopupManager > Load Popup Prefabs 를 누르세요.");
            onDone?.Invoke();
            return;
        }

        popup.Setup(StageNumber, slots, Once(onDone));
        popup.SetOnClose(Once(onDone));
    }

    /// <summary>
    /// 표식 묶음에서 하나를 무작위로 고른다. 없으면 None.
    ///
    /// ⚠ 정본 순서(AllTags)를 훑는다 — 비트를 직접 세면 표식이 늘 때 어긋난다.
    /// </summary>
    static MonsterTag PickRandomTag(MonsterTag tags)
    {
        var owned = new List<MonsterTag>(4);

        foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
            if ((tags & tag) != 0) owned.Add(tag);

        return owned.Count == 0
            ? MonsterTag.None
            : owned[UnityEngine.Random.Range(0, owned.Count)];
    }

    // ── 공통 ────────────────────────────────────────────────

    /// <summary>
    /// 강화소가 고를 수 있는 카드 — <b>아직 새길 자리가 남은 것만</b>.
    ///
    /// ⚠ 다 새긴 카드는 목록에 올리지 않는다 (사용자 지적, 2026-09-09)
    ///   각인·증식은 카드당 한 번씩이라, 둘 다 새긴 카드는 눌러도 두 버튼이
    ///   전부 잠긴다. 그런 칸이 격자에 섞여 있으면 고르는 시간이 통째로 낭비된다.
    ///   ⚠ 판정을 여기 적지 말 것 — <b>ForgePopup 이 버튼을 잠그는 조건과 같아야
    ///     한다.</b> 둘이 갈리면 "목록에는 있는데 아무것도 못 하는" 칸이 다시 생긴다.
    ///   ⚠ 남은 카드가 하나도 없으면 강화소를 건너뛴다. 아무것도 못 하는 창을
    ///     띄우는 것보다 낫다.
    /// </summary>
    static List<int> CollectForgeSlots(SummonDeckData deck)
    {
        var slots = new List<int>(deck.SlotCount);

        foreach (int at in CollectMonsterSlots(deck))
        {
            SummonDeckSlot slot = deck.GetSlot(at);
            if (slot.ManaDiscount > 0 && slot.ExtraSummons > 0) continue;

            slots.Add(at);
        }

        return slots;
    }

    static List<int> CollectMonsterSlots(SummonDeckData deck)
    {
        var slots = new List<int>(deck.SlotCount);

        for (int i = 0; i < deck.SlotCount; i++)
        {
            SummonDeckSlot slot = deck.GetSlot(i);
            if (!slot.IsEmpty && slot.Kind == SummonKind.Monster) slots.Add(i);
        }

        return slots;
    }

    static List<ChoicePopup.Entry> BuildCardEntries(SummonDeckData deck, List<int> slots,
                                                    bool enabled)
    {
        var entries = new List<ChoicePopup.Entry>(slots.Count);
        CardCatalog catalog = CardCatalog.Current;

        foreach (int at in slots)
        {
            SummonDeckSlot slot = deck.GetSlot(at);
            MonsterSpeciesData sp = catalog?.GetMonster(slot.Id);

            string name = sp != null ? sp.DisplayName : slot.Id;
            float mana  = sp != null ? Mathf.Max(1f, sp.ManaCost - slot.ManaDiscount) : 0f;
            int   count = sp != null ? sp.SummonCount + slot.ExtraSummons : 0;

            string desc = $"마나 {mana:0.#}   {count}마리" +
                          (slot.ManaDiscount > 0 || slot.ExtraSummons > 0
                               ? $"   (강화 −{slot.ManaDiscount} / +{slot.ExtraSummons})"
                               : "");

            entries.Add(new ChoicePopup.Entry(
                $"{name}  Lv.{slot.Level}", desc, enabled,
                sp != null ? MonsterPortraitProvider.Get(sp) : null));
        }

        return entries;
    }

    static List<ChoicePopup.Entry> BuildAltarEntries(SummonDeckData deck, List<int> slots,
                                                     bool enabled)
    {
        var entries = new List<ChoicePopup.Entry>(slots.Count);
        CardCatalog catalog = CardCatalog.Current;

        foreach (int at in slots)
        {
            SummonDeckSlot slot = deck.GetSlot(at);
            MonsterSpeciesData sp = catalog?.GetMonster(slot.Id);

            string name = sp != null ? sp.DisplayName : slot.Id;

            // 어느 표식이 남을 수 있는지 보여 준다 — 무작위지만 후보는 알아야 고른다.
            string tags = sp != null ? DescribeTags(sp.Tags) : "";

            entries.Add(new ChoicePopup.Entry(
                $"{name}  Lv.{slot.Level}",
                tags.Length > 0 ? $"이 카드가 사라지고 [{tags}] 중 하나가 +1" : "남길 표식이 없다",
                enabled && tags.Length > 0,
                sp != null ? MonsterPortraitProvider.Get(sp) : null));
        }

        return entries;
    }

    static string DescribeTags(MonsterTag tags)
    {
        var names = new List<string>(4);

        foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
            if ((tags & tag) != 0) names.Add(MonsterSynergyRule.NameOf(tag));

        return string.Join(" · ", names);
    }

    /// <summary>
    /// ChoicePopup 을 연다. 못 열면 흐름이 멈추지 않게 곧장 onDone 으로 빠진다.
    ///
    /// ⚠ 흐름을 **한 번만** 이어 간다
    ///   닫을 수 있는 화면은 "골랐다" 와 "그냥 닫았다" 가 둘 다 일어난다 —
    ///   ChoicePopup.Choose 가 Close() 한 뒤 콜백을 부르기 때문이다.
    ///   막지 않으면 스테이지가 두 칸 넘어간다.
    /// </summary>
    /// <summary>지금 스테이지 번호 — 이야기 줄을 고르는 시드.</summary>
    static int StageNumber
        => StageLoopDirector.Instance != null ? StageLoopDirector.Instance.StageNumber : 1;

    /// <summary>
    /// 흐름을 <b>한 번만</b> 이어 가는 래퍼.
    ///
    /// ⚠ "골랐다" 와 "그냥 닫았다" 가 둘 다 일어난다
    ///   팝업이 Close() 한 뒤 콜백을 부르므로, 막지 않으면 스테이지가 두 칸 넘어간다.
    /// </summary>
    static Action Once(Action step)
    {
        bool done = false;
        return () =>
        {
            if (done) return;
            done = true;
            step?.Invoke();
        };
    }

    /// <summary>지금 열려 있는 시설. Open 이 배경 그림·이야기를 고르는 데 쓴다.</summary>
    static RunNodeKind _current = RunNodeKind.Camp;

    static void Open(string title, List<ChoicePopup.Entry> entries, bool blockClose,
                     Action onDone, Action<int> onPicked)
    {
        bool done = false;

        void Once(Action step)
        {
            if (done) return;
            done = true;
            step?.Invoke();
        }

        // ⚠ ChoicePopup(글자 목록)이 아니라 시설 화면이다 (사용자 확정, 2026-09-06)
        //   전투가 멈추는 유일한 자리라 배경 그림과 이야기가 붙는다
        //   (FacilityPopup 머리 주석 참고).
        var popup = PopupManager.Instance?.Open<FacilityPopup>(PopupType.Facility);

        if (popup == null)
        {
            Debug.LogWarning("[RunNodeFlow] FacilityPopup 을 열지 못했습니다 — 건너뜁니다. " +
                             "Tools > Project K > 프리팹 생성 > 팝업 > 시설 을 실행하고 " +
                             "PopupManager > Load Popup Prefabs 를 누르세요.");
            Once(onDone);
            return;
        }

        int stage = StageLoopDirector.Instance != null ? StageLoopDirector.Instance.StageNumber : 1;

        popup.Setup(_current, stage, title, entries,
                    i => Once(() => onPicked?.Invoke(i)), blockClose);

        // 닫을 수 있는 화면은 그냥 닫아도 다음 스테이지로 가야 한다.
        if (!blockClose) popup.SetOnClose(() => Once(onDone));
    }
}
