#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  LegacyCleanupTool.cs  [Editor Only]
//  Tools > Project K > 도구 > 원작 잔재 정리 (씬·프리팹)
//
//  2026-09-11 리팩토링에서 원작 스크립트를 걷어낸 뒤 **한 번** 누른다.
//  코드는 dotnet 빌드로 정리했지만 씬·프리팹은 유니티 안에서만 고칠 수 있다.
//
//  하는 일 (순서대로)
//    ① Lobby 씬   — LobbyCanvas 바로 아래의 원작 패널(TopBar·HeroPanel·BattlePanel·
//                   ProfilePanel·ShopPanel)과 하단 NavBar 를 지우고 MainPanel 을 켠다
//    ② 원작 프리팹 · 이벤트 데이터 · 장비/어빌리티/특성 아이콘을 지운다 (LegacyAssets)
//    ③ 프리팹 폴더 전체 — 지워진 스크립트 자리(Missing Script)를 떼고 다시 저장
//    ④ HeroDetail · Reincarnation 팝업을 다시 굽는다 (필드가 바뀌었다)
//    ⑤ 세 씬의 Missing Script 를 뗀다 — 프리팹 인스턴스는 ③ 에서 이미 고쳐졌다
//    ⑥ Splash 씬 PopupManager 의 프리팹 목록을 다시 채운다
//    ⑦ SpriteManager + 아틀라스를 다시 굽는다 (지운 아틀라스 두 개가 빠진다)
//
//  ⚠ 여러 번 눌러도 안전하다 — 이미 없는 것은 건너뛴다.
// ============================================================

public static class LegacyCleanupTool
{
    const string LobbyScene  = "Assets/Scenes/Lobby.unity";
    const string InGameScene = "Assets/Scenes/InGame.unity";
    const string SplashScene = "Assets/Scenes/Splash.unity";
    const string PrefabRoot  = "Assets/_project/2.Prefabs";

    /// <summary>LobbyCanvas 바로 아래에서 지울 원작 오브젝트.</summary>
    static readonly string[] LobbyLegacyObjects =
        { "TopBar", "HeroPanel", "BattlePanel", "ProfilePanel", "ShopPanel", "NavBar" };

    /// <summary>
    /// 원작 화면의 프리팹과 데이터·그림. 스크립트가 이미 없어 열어도 빈 껍데기다.
    /// 참조하던 곳은 로비 패널(①에서 지운다)과 PopupManager 목록(⑥에서 다시 채운다)뿐이다.
    /// </summary>
    static readonly string[] LegacyAssets =
    {
        // 로비 패널
        "Assets/_project/2.Prefabs/UI/TopBar.prefab",
        "Assets/_project/2.Prefabs/UI/LobbyCanvas.prefab",
        "Assets/_project/2.Prefabs/UI/Lobby/HeroPanel.prefab",
        "Assets/_project/2.Prefabs/UI/Lobby/BattlePanel.prefab",
        "Assets/_project/2.Prefabs/UI/Lobby/ProfilePanel.prefab",
        "Assets/_project/2.Prefabs/UI/Lobby/ShopPanel.prefab",
        "Assets/_project/2.Prefabs/UI/Lobby/HeroCard.prefab",
        "Assets/_project/2.Prefabs/UI/Lobby/EquipCard.prefab",
        "Assets/_project/2.Prefabs/UI/Lobby/StageNodeUI.prefab",
        "Assets/_project/2.Prefabs/UI/Lobby/StageArrowUI.prefab",
        // 팝업 · 칸
        "Assets/_project/2.Prefabs/UI/AbilityListPopup.prefab",
        "Assets/_project/2.Prefabs/UI/AbilitySelectPopup.prefab",
        "Assets/_project/2.Prefabs/UI/BattleResultPopup.prefab",
        "Assets/_project/2.Prefabs/UI/DisassemblePopup.prefab",
        "Assets/_project/2.Prefabs/UI/EquipComparePopup.prefab",
        "Assets/_project/2.Prefabs/UI/EventPopup.prefab",
        "Assets/_project/2.Prefabs/UI/MercenaryShopPopup.prefab",
        "Assets/_project/2.Prefabs/UI/RunShopPopup.prefab",
        "Assets/_project/2.Prefabs/UI/ExpRow.prefab",
        "Assets/_project/2.Prefabs/UI/GeneralPanel.prefab",
        "Assets/_project/2.Prefabs/UI/MercCandidateCard.prefab",
        "Assets/_project/2.Prefabs/UI/MercFullRow.prefab",
        "Assets/_project/2.Prefabs/UI/RewardCard.prefab",
        "Assets/_project/2.Prefabs/UI/RunShopEquipSlot.prefab",
        "Assets/_project/2.Prefabs/UI/RunShopGeneralSlot.prefab",
        "Assets/_project/2.Prefabs/UI/RunShopTraitSlot.prefab",
        // 원작 이벤트
        "Assets/Resources/EventDatabase.asset",
        "Assets/_project/Data/Events",
        "Assets/_project/3.Textures/Events",
        // 원작 장비·어빌리티·특성 아이콘과 그 아틀라스
        "Assets/_project/3.Textures/Icons/Equipments",
        "Assets/_project/3.Textures/Icons/Abilities",
        "Assets/_project/3.Textures/Icons/Traits",
        "Assets/_project/3.Textures/Icons/Atlas_Equipments.spriteatlas",
        "Assets/_project/3.Textures/Icons/Atlas_Abilities.spriteatlas",
        "Assets/_project/3.Textures/Icons/LobbyBtns/btn_disassemble.png",
        "Assets/_project/3.Textures/Icons/LobbyBtns/btn_ability.png",
        "Assets/_project/3.Textures/Icons/LobbyBtns/btn_shop.png",
    };

    [MenuItem(ProjectKMenu.Tool + "원작 잔재 정리 (씬·프리팹)", priority = ProjectKMenu.ToolPrio + 40)]
    public static void Run()
    {
        if (!EditorUtility.DisplayDialog(
                "원작 잔재 정리",
                "Lobby·Splash·InGame 씬과 프리팹에서 원작 잔재를 걷어냅니다.\n" +
                "· 로비의 원작 패널·NavBar 삭제\n· Missing Script 제거\n" +
                "· HeroDetail·Reincarnation 팝업 재굽기\n· PopupManager 목록 갱신\n\n" +
                "씬을 열고 저장합니다.",
                "정리한다", "취소"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string returnTo = SceneManager.GetActiveScene().path;
        var    log      = new System.Text.StringBuilder();

        // ① Lobby — 원작 패널
        var lobby  = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Single);
        var canvas = FindByName(lobby, "LobbyCanvas");
        if (canvas == null)
            throw new System.InvalidOperationException("[원작 잔재 정리] Lobby 씬에 LobbyCanvas 가 없습니다.");

        var doomed = new List<GameObject>();
        foreach (Transform child in canvas)
            if (System.Array.IndexOf(LobbyLegacyObjects, child.name) >= 0) doomed.Add(child.gameObject);
        foreach (var go in doomed)
        {
            log.AppendLine($"Lobby: {go.name} 삭제");
            Object.DestroyImmediate(go);
        }

        var main = canvas.Find("MainPanel");
        if (main != null) main.gameObject.SetActive(true);
        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.SaveScene(lobby);

        // ② 원작 프리팹·데이터·그림
        foreach (string p in LegacyAssets)
            if (AssetDatabase.DeleteAsset(p)) log.AppendLine($"삭제: {p}");

        // ③ 프리팹 Missing Script
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var    root = PrefabUtility.LoadPrefabContents(path);
            int    n    = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                n += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            if (n > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"프리팹 {path}: Missing Script {n}개");
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ④ 필드가 바뀐 팝업
        HeroDetailPopupCreator.Create();
        ReincarnationPopupCreator.Create();

        // ⑤ 씬 Missing Script
        foreach (string path in new[] { LobbyScene, SplashScene, InGameScene })
        {
            var s = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = StripMissing(s);
            log.AppendLine($"{Path.GetFileNameWithoutExtension(path)}: Missing Script {n}개");

            // ⑥ 팝업 목록 — PopupManager 는 Splash 씬에 있다 (상주 오브젝트)
            if (path == SplashScene)
            {
                var pm = Object.FindAnyObjectByType<PopupManager>(FindObjectsInactive.Include);
                if (pm == null)
                    throw new System.InvalidOperationException("[원작 잔재 정리] Splash 씬에 PopupManager 가 없습니다.");
                PopupManagerEditor.LoadPopupPrefabs(pm);
                EditorSceneManager.MarkSceneDirty(s);
            }

            EditorSceneManager.SaveScene(s);
        }

        // ⑦ 지운 아틀라스를 SpriteManager 에서 걷어낸다
        SpriteManagerCreator.Create();

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);

        Debug.Log("[원작 잔재 정리] 끝났습니다.\n" + log);
    }

    static Transform FindByName(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }

    /// <summary>
    /// 씬 오브젝트의 Missing Script 를 뗀다.
    /// ⚠ 프리팹 인스턴스는 건너뛴다 — 인스턴스에서는 뗄 수 없고, 원본은 ③ 에서 이미 고쳤다.
    /// </summary>
    static int StripMissing(Scene scene)
    {
        int n = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (!PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                    n += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        if (n > 0) EditorSceneManager.MarkSceneDirty(scene);
        return n;
    }
}
#endif
