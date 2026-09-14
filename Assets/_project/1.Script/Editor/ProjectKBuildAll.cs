using System;
using UnityEditor;
using UnityEngine;

// ============================================================
//  ProjectKBuildAll.cs  [Editor Only]
//  Tools > Project K > ▶ 전부 다시 굽기   (+ 그룹별 ▶ 전체 생성)
//
//  ■ 왜 필요했나 (사용자 요청, 2026-09-07)
//    고칠 때마다 눌러야 할 메뉴가 스무 개 가까이 됐다. 게다가 **순서가 있다** —
//    아이콘을 굽기 전에 데이터를 구우면 SO 에 그림이 안 꽂히고, 아틀라스를
//    먼저 갱신하면 방금 구운 그림이 안 들어간다. 순서를 사람이 외우는 것은
//    잘못 눌렀을 때 증상이 "왜인지 그림이 없다" 로만 보여 가장 나쁘다.
//
//  ■ ⚠ 순서가 이 파일의 전부다
//    ① 아이콘·그림   — 다른 것들이 이걸 참조한다
//    ② 데이터(SO)    — 굽는 동안 ①의 PNG 를 찾아 꽂는다 (몬스터 장비가 그렇다)
//    ③ 프리팹·씬     — 굽는 동안 ①②를 참조한다 (HUD 가 패시브 아이콘을 박는다)
//    ④ 아틀라스      — **맨 마지막.** 위에서 만든 그림을 전부 쓸어 담는다
//
//    ⚠ ④를 ①보다 먼저 돌리면 에러 없이 **그림만 전부 빈 칸**이 된다.
//      이 프로젝트에서 가장 자주 밟은 함정이다.
//
//  ■ ⚠ 하나가 실패해도 멈추지 않는다
//    스무 개 중 하나가 터졌다고 나머지를 안 구우면, 다시 눌러야 하는 목록이
//    또 생긴다. 각 단계를 감싸서 실패를 모아 두었다가 **끝에 한꺼번에** 알린다.
//
//  ■ 여기 없는 것 — 일부러 뺐다
//    · 씬 셋업(인게임 전장)  씬을 열고 저장해야 해서 부작용이 크다. 따로 누른다
//    · PopupManager 의 [Load Popup Prefabs]  인스펙터 버튼이라 메뉴가 없다
//    · 올드Tools 아래의 것들  이 게임에서 쓰지 않는다 (ProjectKMenu 규칙)
//
//  ⚠ 도구를 새로 만들면 아래 표에도 넣을 것. 안 넣으면 "전부 다시 굽기" 를
//    눌러도 그것만 옛날 것이 남는다.
// ============================================================

public static class ProjectKBuildAll
{
    // ══════════════════════════════════════════════════════════
    //  ① 아이콘·텍스처
    // ══════════════════════════════════════════════════════════

    [MenuItem(ProjectKMenu.Icon + "▶ 전체 생성", priority = ProjectKMenu.IconPrio - 1)]
    public static void AllIcons()
    {
        var fail = new Fails("아이콘");

        // ── 이 게임의 것 ──
        fail.Run("종족 아이콘",        MonsterIconGenerator.GenerateAll);
        fail.Run("시너지 아이콘",       SynergyIconGenerator.Generate);
        fail.Run("종족 패시브 아이콘",    SpeciesPassiveIconGenerator.Generate);
        fail.Run("갈림길 그림",        RunNodeArtGenerator.Generate);
        fail.Run("이벤트 그림",        RunEventArtGenerator.Generate);
        fail.Run("특성 아이콘",        RunPerkIconGenerator.Generate);
        fail.Run("소환사 개성 아이콘",    SummonerPerkIconGenerator.Generate);
        fail.Run("몬스터 장비 아이콘",    MonsterGearIconGenerator.Generate);
        fail.Run("유물 트리 아이콘",     RelicTreeIconStubs.Sync);

        // ── 원작에서 이관한 것 ──
        fail.Run("직업·스킬 아이콘",     IconGenerator.GenerateAllIcons);
        fail.Run("시그니처 스킬 아이콘",   IconGenerator.GenerateSignatureIcons);
        fail.Run("패시브 아이콘",       PassiveIconGenerator.GeneratePassiveIcons);
        fail.Run("스테이지 노드 아이콘",   IconGenerator.GenerateStageNodeIcons);
        fail.Run("로비 버튼 아이콘",     IconGenerator.GenerateLobbyButtonIcons);
        fail.Run("아이템 아이콘",       ItemIconGenerator.GenerateAll);
        fail.Run("난이도 아이콘",       DifficultyIconGenerator.Generate);
        fail.Run("이펙트 텍스처·머티리얼", EffectTextureGenerator.GenerateAll);
        fail.Run("데미지 숫자 폰트",     DamageFontCreator.Create);

        fail.Report();
    }

    // ══════════════════════════════════════════════════════════
    //  ② 데이터 (ScriptableObject)
    // ══════════════════════════════════════════════════════════
    //
    //  ⚠ 순서가 있다
    //    몬스터 도감 → 스킬 카드 → **카드 목록**. 카드 목록은 앞의 둘을 모아
    //    담는 색인이라 마지막이어야 한다.
    //    몬스터 장비는 장비 아이콘 PNG 를 찾아 꽂으므로 ① 뒤여야 한다.

    [MenuItem(ProjectKMenu.Data + "▶ 전체 생성", priority = ProjectKMenu.DataPrio - 1)]
    public static void AllData()
    {
        var fail = new Fails("데이터");

        fail.Run("액티브 스킬",           ActiveSkillCreator.CreateAllActiveSkills);
        fail.Run("패시브 스킬",           GameAssetCreator.CreateAllPassiveSkills);
        fail.Run("난이도",              DifficultyConfigCreator.Create);
        fail.Run("비인간형 몬스터 라이브러리", MonsterLibraryCreator.CreateAll);
        fail.Run("몬스터 장비",           MonsterGearCreator.Run);
        fail.Run("몬스터 도감",           MonsterCodexCreator.CreateAll);
        fail.Run("소환사",              SummonerCreator.CreateAll);
        fail.Run("스킬 카드",            SkillCardCreator.CreateAll);
        fail.Run("카드 목록",            CardCatalogCreator.CreateAll);

        fail.Report();
    }

    // ══════════════════════════════════════════════════════════
    //  ③ 팝업 프리팹
    // ══════════════════════════════════════════════════════════

    [MenuItem(ProjectKMenu.Popup + "▶ 전체 생성", priority = ProjectKMenu.PrefabPrio + 19)]
    public static void AllPopups()
    {
        var fail = new Fails("팝업");

        // 런 흐름
        fail.Run("런 팝업(카드 선택·전투 통계·선택 목록)", RunPopupCreator.CreateAll);

        // 시설 — 넷이 같은 전체화면 무대를 쓴다 (FacilityStage)
        fail.Run("시설",     FacilityPopupCreator.Run);
        fail.Run("강화소",    CardPickPopupCreator.CreateForge);
        fail.Run("제단",     CardPickPopupCreator.CreateAltar);
        fail.Run("상점",     ShopPopupCreator.Run);

        // 그 밖
        fail.Run("전황",     BattleInfoPopupCreator.Run);
        // ⚠ 전황이 적을 눌렀을 때 여는 창이다 — 둘은 한 묶음이다.
        fail.Run("HeroDetail", HeroDetailPopupCreator.Create);
        fail.Run("진화·융합",  CardEvolvePopupCreator.Create);
        fail.Run("보상 상자",  GearBoxPopupCreator.Run);
        fail.Run("난이도 해금", DifficultyUnlockPopupCreator.Run);
        fail.Run("몬스터 상세", MonsterDetailPopupCreator.Run);
        fail.Run("장비 상세",  GearDetailPopupCreator.Run);
        fail.Run("Codex",   CodexPopupCreator.Run);
        fail.Run("RelicTree", RelicTreePopupCreator.Create);
        fail.Run("Reincarnation", ReincarnationPopupCreator.Create);
        fail.Run("Pause",   PopupPrefabCreator.CreatePausePopup);
        fail.Run("Loading", PopupPrefabCreator.CreateLoadingPopup);

        fail.Report();
        Debug.Log("[전체 생성] ⚠ 팝업을 구운 뒤에는 씬의 PopupManager 에서 " +
                  "[Load Popup Prefabs] 를 눌러야 실제로 열립니다.");
    }

    // ══════════════════════════════════════════════════════════
    //  ④ 전부 — 순서까지 지킨다
    // ══════════════════════════════════════════════════════════

    [MenuItem(ProjectKMenu.Root + "▶ 전부 다시 굽기", priority = -100)]
    public static void Everything()
    {
        if (!EditorUtility.DisplayDialog(
                "전부 다시 굽기",
                "아이콘 → 데이터 → 프리팹 → 아틀라스 순서로 전부 다시 굽습니다.\n" +
                "몇 분 걸릴 수 있습니다.\n\n" +
                "⚠ 씬 셋업(인게임 전장)은 포함되지 않습니다 — 따로 누르세요.",
                "굽는다", "취소"))
            return;

        try
        {
            AllIcons();
            AllData();
            AllPopups();

            var fail = new Fails("마무리");
            fail.Run("인게임 HUD", InGameUIPrefabCreator.CreateAll);

            // ⚠ 아틀라스는 **맨 마지막**이다 (파일 머리 주석 참고)
            //   먼저 돌리면 위에서 만든 그림이 하나도 안 들어간다.
            fail.Run("SpriteManager + 아틀라스", SpriteManagerCreator.Create);
            fail.Report();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log("[전부 다시 굽기] 끝났습니다.\n" +
                  "⚠ 남은 두 가지는 손으로 하세요 —\n" +
                  "  · 씬 셋업 > 인게임 전장 (씬을 열고 저장해야 합니다)\n" +
                  "  · 씬의 PopupManager 에서 [Load Popup Prefabs]");
    }

    // ══════════════════════════════════════════════════════════
    //  실패를 모아 두었다가 끝에 한꺼번에 알린다
    // ══════════════════════════════════════════════════════════
    //
    //  ⚠ 하나가 터졌다고 멈추지 않는다 (파일 머리 주석 참고)
    //    멈추면 "다시 눌러야 하는 목록" 이 또 생긴다. 그게 이 도구를 만든 이유다.

    sealed class Fails
    {
        readonly string                       _group;
        readonly System.Text.StringBuilder    _sb = new();
        int _done, _failed;

        public Fails(string group) => _group = group;

        public void Run(string label, Action step)
        {
            EditorUtility.DisplayProgressBar($"{_group} 굽는 중", label, 0.5f);

            try
            {
                step();
                _done++;
            }
            catch (Exception e)
            {
                _failed++;
                _sb.Append("\n  · ").Append(label).Append(" — ").Append(e.Message);
                Debug.LogException(e);
            }
        }

        public void Report()
        {
            EditorUtility.ClearProgressBar();

            if (_failed == 0)
            {
                Debug.Log($"[전체 생성] {_group} {_done}개 완료.");
                return;
            }

            // ⚠ 경고가 아니라 에러다 — 노란 줄은 로그에 묻힌다.
            Debug.LogError($"[전체 생성] {_group} — 성공 {_done} · 실패 {_failed}{_sb}\n" +
                           "실패한 항목은 위 예외를 보고 개별 메뉴로 다시 실행하세요.");
        }
    }
}
