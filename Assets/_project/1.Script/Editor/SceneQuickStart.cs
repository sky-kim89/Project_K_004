using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

// ============================================================
//  SceneQuickStart.cs
//  에디터 씬 빠른 이동 도구.
//
//  기능 1 — 단축키 (모두 씬 로드, 재생 없음)
//    Ctrl+Shift+Alt+1 : Splash 씬 로드
//    Ctrl+Shift+Alt+2 : Lobby 씬 로드
//    Ctrl+Shift+Alt+3 : InGame 씬 로드
//
//  기능 2 — 상단 툴바 버튼
//    재생 버튼 왼쪽 "▶ S" 버튼 → Splash 씬 재생 (Play 진입)
//
//  씬 검색 순서: Build Settings → 프로젝트 전체 (폴백)
// ============================================================

// ── 단축키 & 씬 실행 로직 ─────────────────────────────────────

public static class SceneQuickStart
{
    const string Splash = "Splash";
    const string Lobby  = "Lobby";
    const string InGame = "InGame";

    [MenuItem(ProjectKMenu.Scene + "Splash 로드    %#&1", priority = ProjectKMenu.ScenePrio)]
    public static void LoadSplash() => Load(Splash);

    [MenuItem(ProjectKMenu.Scene + "Lobby 로드     %#&2", priority = ProjectKMenu.ScenePrio + 1)]
    public static void LoadLobby() => Load(Lobby);

    [MenuItem(ProjectKMenu.Scene + "InGame 로드    %#&3", priority = ProjectKMenu.ScenePrio + 2)]
    public static void LoadInGame() => Load(InGame);

    // ── 내부 ──────────────────────────────────────────────────

    // 씬 로드만 (재생 없음) — 단축키 및 메뉴에서 사용
    internal static void Load(string sceneName)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SceneQuickStart] 재생 중에는 씬 로드를 사용할 수 없습니다.");
            return;
        }

        string path = FindScenePath(sceneName);
        if (path == null)
        {
            Debug.LogError($"[SceneQuickStart] '{sceneName}' 씬을 찾을 수 없습니다.\n" +
                           "Build Settings 에 씬이 추가되어 있는지 확인하세요.");
            return;
        }

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(path);
    }

    // 씬 로드 후 Play 진입 — 툴바 버튼에서만 사용
    internal static void Play(string sceneName)
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        string path = FindScenePath(sceneName);
        if (path == null)
        {
            Debug.LogError($"[SceneQuickStart] '{sceneName}' 씬을 찾을 수 없습니다.\n" +
                           "Build Settings 에 씬이 추가되어 있는지 확인하세요.");
            return;
        }

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(path);
            EditorApplication.isPlaying = true;
        }
    }

    static string FindScenePath(string sceneName)
    {
        // ① Build Settings 에서 탐색
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (Path.GetFileNameWithoutExtension(scene.path) == sceneName)
                return scene.path;
        }
        // ② 프로젝트 전체 탐색 (폴백)
        foreach (var guid in AssetDatabase.FindAssets($"t:Scene {sceneName}"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == sceneName)
                return p;
        }
        return null;
    }
}

// ── 툴바 버튼 주입 (재생 버튼 왼쪽) ─────────────────────────────
// Unity 내부 Toolbar 오브젝트에 UIElements Button 을 삽입한다.
// Unity 2021 ~ 6 에서 동작 확인된 방식.

[InitializeOnLoad]
static class SceneQuickStartToolbar
{
    const string BtnName = "ProjectK_PlaySplash";

    static VisualElement _injectedZone;

    static SceneQuickStartToolbar()
    {
        EditorApplication.update += TryInject;
    }

    static void TryInject()
    {
        // 이미 주입됐으면 버튼이 아직 살아있는지 확인만 한다
        if (_injectedZone != null)
        {
            if (_injectedZone.Q<Button>(BtnName) != null)
                return;
            // 씬 리로드 등으로 제거된 경우 재주입을 위해 리셋
            _injectedZone = null;
        }

        // 내부 Toolbar 오브젝트 탐색
        var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
        if (toolbarType == null) return;

        var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
        if (toolbars.Length == 0) return;

        // m_Root VisualElement 접근
        var rootField = toolbarType.GetField("m_Root",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (rootField == null) return;

        var root = rootField.GetValue(toolbars[0]) as VisualElement;
        if (root == null) return;

        // 재생 버튼 왼쪽 영역 탐색 (Unity 버전별 이름 대응)
        VisualElement zone =
               root.Q("ToolbarZoneLeftAlign")   // Unity 2022~6
            ?? root.Q("AppToolbar")
            ?? root;

        if (zone == null) return;

        // 버튼 생성
        var btn = new Button(() => SceneQuickStart.Play("Splash"))
        {
            name    = BtnName,
            text    = "▶ S",
            tooltip = "Splash 씬 재생",
        };
        btn.style.marginLeft   = 4;
        btn.style.marginRight  = 4;
        btn.style.paddingLeft  = 8;
        btn.style.paddingRight = 8;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;

        // 재생 존 바로 왼쪽에 오도록 끝에 추가
        zone.Add(btn);
        _injectedZone = zone;

        EditorApplication.update -= TryInject;
    }
}
