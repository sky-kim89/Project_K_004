using System;
using System.Linq;
using BattleGame.Units;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

// ============================================================
//  CheatEditorWindow.cs  [Editor Only]
//  Tools > Project K > 도구 > 치트  — 플레이 모드 전용.
//
//  ■ 2026-09-11 이 게임용으로 새로 짰다 (사용자 지시 — "기존 로직 이용해서")
//    원작 창은 어빌리티·장수·용사 장비·원작 특성을 줬다. 이 게임의 축이 아니다.
//    지금 탭은 이 게임의 자원 그대로다:
//      런     마나 · 마왕성 · 런 골드 · 시그니처 · 용사 전멸 · 스테이지 이동
//      카드   종족 카드 받기 · 만렙 · 빈 칸
//      특성   런 특성(RunPerk) 받기
//      재화   영구 골드 · 환생 포인트 · 최고 도달 스테이지 · 소환사 전부 해금
//      도감   몬스터 · 특성 · 장비 채우기 · 품질 영웅 · 장비 만렙
//      유물   전 노드 만렙 · 초기화(환불)
//      난이도 · 튜토리얼  (원작 창에서 그대로 옮겼다)
//
//  ■ ⚠ 값을 직접 쓰지 않는다 — 게임이 쓰는 함수를 그대로 부른다
//    특성은 RunPerkData.Add(얻는 순간의 칸·성 변화까지), 카드는 SummonDeckData.Acquire,
//    용사 전멸은 DeadTag → 기존 사망 파이프라인(CoreBreachSystem 과 같은 길)이다.
//    치트가 따로 계산하면 "치트로는 되는데 실제로는 안 되는" 상태를 시험하게 된다.
// ============================================================

public class CheatEditorWindow : EditorWindow
{
    static readonly string[] Tabs = { "런", "카드", "특성", "재화", "도감", "유물", "난이도", "튜토리얼" };

    int     _tab;
    Vector2 _scroll;

    int _jumpStage  = 10;
    int _bestStage  = 30;
    int _speciesIdx;

    [MenuItem(ProjectKMenu.Tool + "치트", priority = ProjectKMenu.ToolPrio)]
    static void Open() => GetWindow<CheatEditorWindow>("치트");

    void OnGUI()
    {
        if (!Application.isPlaying || UserDataManager.Instance == null)
        {
            EditorGUILayout.HelpBox("플레이 모드에서만 쓸 수 있습니다.", MessageType.Warning);
            return;
        }

        _tab = GUILayout.Toolbar(_tab, Tabs);
        EditorGUILayout.Space(6);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        switch (_tab)
        {
            case 0: DrawRunTab();        break;
            case 1: DrawCardTab();       break;
            case 2: DrawPerkTab();       break;
            case 3: DrawCurrencyTab();   break;
            case 4: DrawCodexTab();      break;
            case 5: DrawRelicTab();      break;
            case 6: DrawDifficultyTab(); break;
            case 7: DrawTutorialTab();   break;
        }
        EditorGUILayout.EndScrollView();
    }

    void OnInspectorUpdate() => Repaint();   // 마나·체력 숫자가 따라 움직이게

    // ══════════════════════════════════════════════════════════
    //  런
    // ══════════════════════════════════════════════════════════

    void DrawRunTab()
    {
        SummonerData summoner = SummonerRuntimeBridge.Current != null ? SummonerRuntimeBridge.Current.Data : null;
        var director = StageLoopDirector.Instance;
        var mana     = Get<SummonManaData>();
        var core     = Get<RunCoreData>();
        var gold     = Get<RunGoldData>();

        Header("상태");
        Row("소환사",   summoner != null ? summoner.DisplayName : "런 밖 (소환사 없음)");
        Row("스테이지", director != null ? $"{director.StageNumber}  ({(director.IsStageReady ? "대기" : "전투")})" : "-");
        Row("마나",     $"{mana.Current:0} / {mana.Max:0}");
        Row("마왕성",   $"{core.Current} / {core.Max}");
        Row("런 골드",  $"{gold.Current:N0}");

        Header("마나");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("가득 채우기")) { mana.RegenForStage(mana.Max - mana.Current); Done("마나 가득"); }

            // 상점 '마력의 정수' 와 같은 통(RunBoonData)에 쌓는다 — 그릇은 MaxManaFor 가 다시 잰다
            if (Button("최대 +10") && summoner != null)
            {
                Get<RunBoonData>().AddMaxMana(10);
                mana.SetMax(RunPerkRule.MaxManaFor(summoner));
                Done("최대 마나 +10");
            }
        }

        Header("마왕성");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("가득 채우기")) { core.Heal(core.Max); Done("마왕성 가득"); }
            if (Button("최대 +20"))    { core.AddMax(20);    Done("마왕성 최대 +20"); }
        }

        Header("런 골드");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("+1,000"))  { gold.Add(1000);  Done("런 골드 +1,000"); }
            if (Button("+10,000")) { gold.Add(10000); Done("런 골드 +10,000"); }
        }

        Header("시그니처 스킬");
        if (Button("스테이지당 횟수 +1 (이번 런)"))
        {
            // 이벤트 '봉인된 지팡이' 와 같은 통이다 (SummonerSkillRule.Remaining 이 읽는다)
            Get<RunBoonData>().AddSignatureUse(1);
            Done("시그니처 스테이지당 +1");
        }

        Header("전투");
        bool inWave = BattleManager.Instance != null && BattleManager.Instance.Context != null &&
                      BattleManager.Instance.Context.State == BattleState.InWave;

        using (new EditorGUI.DisabledScope(!inWave))
        {
            if (Button("용사 전멸 (지금 필드)", 30f))
                Debug.Log($"[치트] 용사 {KillAllHeroes()}기 전멸 — 남은 부대가 없으면 승리로 넘어간다");
        }
        if (!inWave) EditorGUILayout.LabelField("  전투 중에만", EditorStyles.miniLabel);

        bool ready = director != null && director.IsStageReady;
        using (new EditorGUILayout.HorizontalScope())
        using (new EditorGUI.DisabledScope(!ready))
        {
            _jumpStage = EditorGUILayout.IntField("스테이지 이동", Mathf.Max(1, _jumpStage));
            if (GUILayout.Button("이동", GUILayout.Width(60f)))
            {
                var boot = FindAnyObjectByType<RunBootstrap>();
                if (boot != null && boot.CheatJumpToStage(_jumpStage)) Done($"스테이지 {_jumpStage} 대기로 이동");
            }
        }
        if (!ready) EditorGUILayout.LabelField("  스테이지 대기 중에만 — 건너뛴 판의 보상·기록은 없다", EditorStyles.miniLabel);
    }

    /// <summary>
    /// 지금 필드의 용사를 전부 쓰러뜨린다 — DeadTag 를 붙여 기존 사망 파이프라인에 맡긴다
    /// (CoreBreachSystem 이 성벽을 지난 용사를 치우는 것과 같은 길). 판정·반납·집계가 전부 그쪽이다.
    /// </summary>
    static int KillAllHeroes()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return 0;

        var em = world.EntityManager;
        em.CompleteAllTrackedJobs();

        using var query    = em.CreateEntityQuery(ComponentType.ReadOnly<UnitIdentityComponent>(),
                                                  ComponentType.Exclude<DeadTag>());
        using var entities = query.ToEntityArray(Allocator.Temp);

        int killed = 0;
        foreach (Entity e in entities)
        {
            if (em.GetComponentData<UnitIdentityComponent>(e).Team != Faction.Hero) continue;

            em.AddComponent<DeadTag>(e);
            killed++;
        }

        return killed;
    }

    // ══════════════════════════════════════════════════════════
    //  카드
    // ══════════════════════════════════════════════════════════

    void DrawCardTab()
    {
        var deck     = Get<SummonDeckData>();
        var monsters = CardCatalog.Current.Monsters.Where(m => m != null).ToArray();
        if (monsters.Length == 0) { EditorGUILayout.HelpBox("카드 목록이 비었습니다.", MessageType.Warning); return; }

        Header("덱");
        Row("칸", $"{deck.SlotCount}  (빈 칸 {deck.FreeSlotCount})");
        if (Button($"빈 칸 +1 (상한 {RunPerkRule.MaxDeckSlots})"))
        {
            deck.ResizeTo(Mathf.Min(deck.SlotCount + 1, RunPerkRule.MaxDeckSlots));
            Done("빈 칸 +1");
        }
        if (Button("덱의 몬스터 카드 전부 만렙"))
        {
            for (int i = 0; i < deck.SlotCount; i++)
            {
                SummonDeckSlot slot = deck.GetSlot(i);
                if (slot.IsEmpty || slot.Kind != SummonKind.Monster) continue;

                while (deck.GetSlot(i).Level < CardLevelRule.MaxLevel)
                    deck.Acquire(SummonKind.Monster, slot.Id);
            }
            Done("덱 전부 만렙");
        }

        Header("카드 받기");
        string[] labels = monsters.Select(m => $"{m.DisplayName}  ({m.Id})").ToArray();
        _speciesIdx = EditorGUILayout.Popup("종족", Mathf.Clamp(_speciesIdx, 0, monsters.Length - 1), labels);

        MonsterSpeciesData species = monsters[_speciesIdx];
        int at = deck.IndexOf(species.Id);
        Row("지금", at >= 0 ? $"{at + 1}번 칸 · Lv.{deck.GetSlot(at).Level}"
                            : deck.HasFreeSlot ? "덱에 없음" : "덱에 없음 — 빈 칸이 없어 못 받는다");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("한 장 받기")) GiveCard(deck, species, toMax: false);
            if (Button("만렙까지"))   GiveCard(deck, species, toMax: true);
        }
    }

    /// <summary>
    /// 보상 경로(RunBootstrap)와 같은 순서 — Acquire 로 받고, 도감 해금은 **부르는 쪽의 몫**이라 여기서 연다.
    /// </summary>
    static void GiveCard(SummonDeckData deck, MonsterSpeciesData species, bool toMax)
    {
        int level = deck.Acquire(SummonKind.Monster, species.Id);
        if (level == 0) { Debug.LogWarning("[치트] 빈 칸이 없어 카드를 못 받았습니다."); return; }

        Get<MonsterCodexData>().Unlock(species.Id, UnitGrade.Normal);

        while (toMax && level < CardLevelRule.MaxLevel)
            level = deck.Acquire(SummonKind.Monster, species.Id);

        Done($"{species.DisplayName} Lv.{level}");
    }

    // ══════════════════════════════════════════════════════════
    //  특성
    // ══════════════════════════════════════════════════════════

    void DrawPerkTab()
    {
        var perks = Get<RunPerkData>();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("남은 특성 전부 받기"))
            {
                foreach (RunPerk p in Enum.GetValues(typeof(RunPerk)))
                    if (p != RunPerk.None) perks.Add(p);
                Done("특성 전부");
            }
            if (Button("특성 비우기"))
            {
                perks.SetDefaults();
                Done("특성 비움 — 이미 바뀐 칸·성은 그대로다");
            }
        }

        Header($"특성 ({perks.Perks.Count} 보유)");
        foreach (RunPerk p in Enum.GetValues(typeof(RunPerk)))
        {
            if (p == RunPerk.None) continue;

            using (new EditorGUILayout.HorizontalScope())
            {
                bool has = perks.Has(p);
                using (new EditorGUI.DisabledScope(has))
                {
                    // ⚠ RunPerkData.Add 를 지난다 — 확장 편성·봉인된 칸·유리 성채의 '얻는 순간' 효과까지 돈다
                    if (GUILayout.Button(has ? "보유" : "받기", GUILayout.Width(50f)))
                    {
                        perks.Add(p);
                        Done($"특성 {p.ToKorean()}");
                    }
                }
                EditorGUILayout.LabelField(p.ToKorean(), GUILayout.Width(110f));
                EditorGUILayout.LabelField(p.Describe(), EditorStyles.miniLabel);
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  재화 (영구)
    // ══════════════════════════════════════════════════════════

    void DrawCurrencyTab()
    {
        var items = Get<ItemData>();
        var reinc = Get<ReincarnationData>();

        Header("영구 골드 (품질 개선 · 장비 레벨업)");
        Row("보유", $"{items.Get(eItem.Gold):N0}");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("+10,000"))  { items.Add(eItem.Gold, 10000);  Done("영구 골드 +10,000"); }
            if (Button("+100,000")) { items.Add(eItem.Gold, 100000); Done("영구 골드 +100,000"); }
        }

        Header("환생 포인트 (유물 트리)");
        Row("보유", $"{reinc.ReincarnationPoints:N0}");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("+100"))   { reinc.EarnPoints(100);  Done("환생 포인트 +100"); }
            if (Button("+1,000")) { reinc.EarnPoints(1000); Done("환생 포인트 +1,000"); }
        }

        Header("최고 도달 스테이지 (소환사 해금 조건)");
        Row("기록", reinc.BestStage.ToString());
        using (new EditorGUILayout.HorizontalScope())
        {
            _bestStage = EditorGUILayout.IntField("기록", Mathf.Max(1, _bestStage));
            if (GUILayout.Button("올리기", GUILayout.Width(60f)))
            {
                reinc.RecordStageReached(_bestStage);
                Done($"최고 도달 {_bestStage}");
            }
        }

        Header("소환사");
        if (Button("소환사 전부 해금", 30f))
        {
            // 해금 조건(SummonerUnlockRule)이 보는 것을 전부 채운다 — 판정은 건드리지 않는다
            FillCodex(CodexCategory.Monster, silent: true);
            MaxAllGrades();
            reinc.RecordStageReached(GameplayConfig.Current.MaxStage);
            Done("소환사 전부 해금 — 몬스터 도감 전체 · 품질 영웅 · 최고 도달 기록");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  도감
    // ══════════════════════════════════════════════════════════

    void DrawCodexTab()
    {
        Header("현재 수집");
        foreach (CodexCategory c in Enum.GetValues(typeof(CodexCategory)))
        {
            var (owned, total) = CodexCatalog.Progress(c);
            Row(CodexCatalog.Label(c), $"{owned} / {total}");
        }

        Header("채우기");
        foreach (CodexCategory c in Enum.GetValues(typeof(CodexCategory)))
        {
            var cat = c;
            if (Button($"{CodexCatalog.Label(cat)} 도감 완성")) FillCodex(cat);
        }

        Header("몬스터 · 장비");
        if (Button("해금한 종족 품질 전부 영웅")) { MaxAllGrades(); Done("품질 전부 영웅"); }
        if (Button("가진 장비 전부 Lv5"))
        {
            var inv = Get<MonsterGearInventory>();
            foreach (MonsterGearData g in MonsterGearDatabase.Current.Entries)
                if (g != null && inv.OwnedCount(g.Id) > 0) inv.SetLevel(g.Id, MonsterGearLevelRule.MaxLevel);
            Done("장비 전부 Lv5");
        }

        Header("초기화");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("특성·장비 기록 초기화"))  { Get<CodexData>().SetDefaults();        Done("도감 기록 초기화"); }
            if (Button("몬스터 도감 초기화"))     { Get<MonsterCodexData>().SetDefaults(); Done("몬스터 도감 초기화 — 품질도 지운다"); }
        }
    }

    /// <summary>
    /// 한 분류를 전부 수집 상태로 만든다.
    /// ⚠ 목록의 출처는 도감 화면(CodexCatalog.Build)과 같아야 한다 — 다르면 "완성" 을 눌러도 399/400 이 된다.
    /// </summary>
    static void FillCodex(CodexCategory category, bool silent = false)
    {
        int before = CodexCatalog.Progress(category).owned;

        switch (category)
        {
            // 몬스터는 MonsterCodexData — 치트는 언제나 Normal 로 연다 (품질 개선의 기준선)
            case CodexCategory.Monster:
                var mon = Get<MonsterCodexData>();
                foreach (MonsterSpeciesData s in CardCatalog.Current.Monsters)
                    if (s != null) mon.Unlock(s.Id, UnitGrade.Normal);
                break;

            // 특성 탭은 런 특성(RunPerk) 기록이다 (CodexData.RecordPerk)
            case CodexCategory.Trait:
                var codex = Get<CodexData>();
                foreach (RunPerk p in Enum.GetValues(typeof(RunPerk)))
                    codex.AddPerk(p);
                break;

            // 장비는 MonsterGearInventory — 없는 것만 한 개씩 (수를 불리면 레벨업 재료 시험이 흐려진다)
            case CodexCategory.Gear:
                var inv = Get<MonsterGearInventory>();
                foreach (MonsterGearData g in MonsterGearDatabase.Current.Entries)
                    if (g != null && inv.OwnedCount(g.Id) == 0) inv.Add(g.Id);
                break;
        }

        if (!silent)
            Done($"{CodexCatalog.Label(category)} 도감 완성 — {CodexCatalog.Progress(category).owned - before}종 추가");
    }

    /// <summary>해금한 종족을 전부 영웅 품질로 — 품질 개선과 같은 함수(ImproveGrade)를 끝까지 부른다.</summary>
    static void MaxAllGrades()
    {
        var mon = Get<MonsterCodexData>();
        foreach (string id in mon.UnlockedSpeciesIds.ToArray())
            while (mon.ImproveGrade(id)) { }
    }

    // ══════════════════════════════════════════════════════════
    //  유물
    // ══════════════════════════════════════════════════════════

    void DrawRelicTab()
    {
        var tree  = Get<RelicTreeData>();
        var reinc = Get<ReincarnationData>();

        Row("찍은 노드", $"{tree.Levels.Count} / {RelicTreeCatalog.All.Length}");
        Row("환생 포인트", $"{reinc.ReincarnationPoints:N0}");

        if (Button("전 노드 만렙 (포인트를 쓰지 않는다)", 30f))
        {
            foreach (RelicNodeDef def in RelicTreeCatalog.All)
                tree.SetLevel(def.Id, def.MaxLevel);
            Done("유물 전 노드 만렙");
        }

        if (Button("트리 초기화 (투자 포인트 환불)"))
        {
            // 유물 창의 '초기화' 와 같은 짝 — ResetAll 이 돌려준 만큼 적립한다
            reinc.EarnPoints(tree.ResetAll());
            Done("유물 트리 초기화");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  난이도 (원작 창에서 옮겼다)
    //
    //  ⚠ 해금은 '완주' 기록이라 정상 플레이로는 되돌릴 수 없다 — 여기서 연다.
    // ══════════════════════════════════════════════════════════

    void DrawDifficultyTab()
    {
        var data = Get<DifficultyData>();
        var cfg  = DifficultyConfig.Current;

        Header("현재 상태");
        Row("선택된 난이도", data.SelectedTier.Label());
        Row("완주한 최고 등급", data.ClearedTierIndex < 0 ? "없음" : ((DifficultyTier)data.ClearedTierIndex).Label());
        Row("선택 가능 상한", ((DifficultyTier)data.MaxSelectableIndex).Label());

        Header("해금");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("전 난이도 해금"))
            {
                data.RecordClear((DifficultyTier)(Enum.GetValues(typeof(DifficultyTier)).Length - 1));
                Done("전 난이도 해금");
            }
            if (Button("해금 초기화")) { data.SetDefaults(); Done("난이도 해금 초기화"); }
        }

        Header("바로 적용 (해금 무시)");
        foreach (DifficultyTier tier in Enum.GetValues(typeof(DifficultyTier)))
        {
            var entry = cfg.Get(tier);
            using (new EditorGUILayout.HorizontalScope())
            {
                string mark = data.SelectedTier == tier ? "› " : "";
                if (GUILayout.Button(mark + tier.Label(), GUILayout.Width(90f)))
                {
                    // Select 는 해금 검사를 하므로 기록부터 올린다
                    data.RecordClear(tier);
                    data.Select(tier);
                    Done($"난이도 → {tier.Label()}");
                }

                var debuffs = entry.ActiveDebuffs();
                EditorGUILayout.LabelField(debuffs.Length > 0 ? string.Join(" · ", debuffs.Select(d => d.Label()))
                                                              : "제약 없음",
                                           EditorStyles.miniLabel);
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  튜토리얼 (원작 창에서 옮겼다)
    //
    //  ⚠ 튜토리얼은 정상 플레이로 두 번 볼 수 없다 — 연출을 고칠 때 여기서 되돌린다.
    // ══════════════════════════════════════════════════════════

    void DrawTutorialTab()
    {
        var data = Get<TutorialData>();
        var mgr  = TutorialManager.Instance;

        Header("현재 상태");
        Row("재생 중", mgr != null && mgr.IsPlaying
            ? $"{mgr.Current.Id}  ({mgr.Current.CurrentStep}/{mgr.Current.StepCount})" : "없음");
        Row("끊긴 튜토리얼", data.InProgress == TutorialId.None ? "없음" : $"{data.InProgress}  (스텝 {data.InProgressStep})");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (Button("전체 초기화 (다시 보기)")) { data.ResetAll();          Done("튜토리얼 기록 초기화"); }
            if (Button("전부 봤음 처리"))         { data.CompleteAllForced(); Done("강제 튜토리얼 전부 완료"); }
        }

        if (mgr != null && mgr.IsPlaying && Button("지금 것 건너뛰기")) mgr.Skip();

        Header("개별");
        foreach (TutorialId id in Enum.GetValues(typeof(TutorialId)))
        {
            if (id == TutorialId.None) continue;

            using (new EditorGUILayout.HorizontalScope())
            {
                bool done = data.IsCompleted(id);
                EditorGUILayout.LabelField($"{(done ? "[본것]" : "[미시청]")} {id}", GUILayout.Width(220f));
                EditorGUILayout.LabelField(id.IsForced() ? "강제" : "도움말", EditorStyles.miniLabel, GUILayout.Width(50f));

                using (new EditorGUI.DisabledScope(!done))
                    if (GUILayout.Button("초기화", GUILayout.Width(60f))) { data.ResetOne(id); Save(); }

                bool has = mgr != null && mgr.Has(id);
                using (new EditorGUI.DisabledScope(!has || mgr.IsPlaying))
                    if (GUILayout.Button("재생", GUILayout.Width(60f))) mgr.Replay(id);
            }
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────

    static T Get<T>() where T : class, ISaveSection => UserDataManager.Instance.Get<T>();

    static void Save() => UserDataManager.Instance.RequestSave();

    /// <summary>치트 한 번 = 저장 한 번 + 로그 한 줄.</summary>
    static void Done(string what)
    {
        Save();
        Debug.Log($"[치트] {what}");
    }

    static void Header(string text)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
    }

    static void Row(string label, string value) => EditorGUILayout.LabelField("  " + label, value);

    static bool Button(string label, float height = 24f) => GUILayout.Button(label, GUILayout.Height(height));
}
