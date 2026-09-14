using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
//  SceneDirector.cs
//  Lobby 와 InGame 을 동시에 띄워 놓고, "지금 화면을 누가 그리는가" 를 정한다.
//
//  ■ 씬을 언로드하지 않는다
//    예전 흐름은 전투에 들어갈 때 Lobby 를 통째로 언로드하고,
//    돌아올 때 LoadScene 으로 다시 만들었다. 그래서 로비로 돌아올 때마다
//    UI 를 처음부터 다시 세우느라 멈칫했다.
//    이제 두 씬은 앱이 끝날 때까지 그대로 있고, 바뀌는 것은 '표시 상태' 뿐이다.
//
//  ■ 매니저는 원래부터 씬 소속이 아니다
//    Singleton<T> 는 Awake 에서 자기를 루트로 빼고 DontDestroyOnLoad 를 건다.
//    BattleManager(+스포너 자식) · PoolController · PopupManager 는 이미
//    앱 수명 전체를 산다. 그래서 여기서 다루는 것은 씬에 진짜 남아 있는 것 —
//    카메라 · AudioListener · 캔버스 뿐이다.
//
//  ■ 표시 상태 세 가지 (PresentMode)
//    LobbyOnly      로비 카메라 + 로비 UI            — 평소 로비
//    ArenaBehindUI  인게임 카메라 + 로비 UI          — 메인 배경 데모 전투 · 출전 대기 화면
//    Battle         인게임 카메라 + 인게임 HUD       — 실전
//
//    ArenaBehindUI 가 성립하는 이유: 두 씬의 캔버스가 모두 Screen Space-Overlay 라
//    UI 는 어떤 카메라를 쓰든 항상 월드 위에 그려진다. 배경이 비어 있는 패널
//    (MainPanel) 뒤로는 전투가 그대로 비친다.
//
//  ⚠ 카메라와 AudioListener 는 반드시 한 번에 하나만 켠다
//    둘 다 켜 두면 화면이 겹쳐 그려지고 오디오 경고가 매 프레임 뜬다.
// ============================================================

public enum PresentMode
{
    LobbyOnly     = 0,
    ArenaBehindUI = 1,
    Battle        = 2,
}

public class SceneDirector : Singleton<SceneDirector>
{
    [Header("씬 이름")]
    [SerializeField] string _lobbySceneName  = "Lobby";
    [SerializeField] string _inGameSceneName = "InGame";

    [Header("인게임 씬 오브젝트 이름")]
    [Tooltip("인게임 HUD 캔버스 루트 이름 — 로비를 보여 줄 때 꺼 둔다.")]
    [SerializeField] string _inGameCanvasName = "Canvas";

    // ── 캐시 ─────────────────────────────────────────────────
    // 매 전환마다 씬을 훑지 않는다. 씬이 언로드되지 않으므로 한 번 찾으면 끝이다.

    Camera     _lobbyCam;
    Camera     _inGameCam;
    GameObject _inGameCanvas;
    GameObject _lobbyCanvas;

    PresentMode _mode = PresentMode.LobbyOnly;

    Coroutine _loadRoutine;    // 인게임 씬 로드 작업 — SceneDirector 소유 (중복 로드 방지)
    bool      _residentReady;  // 씬 로드 + 카메라·HUD 연결까지 끝났는가

    public PresentMode Mode => _mode;

    /// <summary>InGame 씬이 로드되어 상주 중인가.</summary>
    public bool IsInGameResident
    {
        get
        {
            Scene s = SceneManager.GetSceneByName(_inGameSceneName);
            return s.IsValid() && s.isLoaded;
        }
    }

    // ── 생명주기 ─────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;   // 중복 인스턴스는 곧 파괴된다

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        base.OnDestroy();
    }

    /// <summary>
    /// 인게임 씬이 올라오는 즉시 표시 상태를 강제한다.
    ///
    /// ⚠ 코루틴이 다음 Update 에서 정리하기를 기다리면 한 프레임이 샌다
    ///   씬이 올라오는 순간 인게임 카메라와 HUD 캔버스는 켜진 채다. 그 상태로
    ///   한 번이라도 그려지면 로비 위에 전투 HUD 가 번쩍인다.
    /// </summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != _inGameSceneName) return;
        CacheInGameObjects();
        Apply(_mode);
    }

    /// <summary>씬에 배치하지 않아도 쓸 수 있게 필요할 때 만든다.</summary>
    public static SceneDirector Ensure()
    {
        if (Instance != null) return Instance;
        return new GameObject(nameof(SceneDirector)).AddComponent<SceneDirector>();
    }

    // ── 씬 상주 ──────────────────────────────────────────────

    /// <summary>
    /// InGame 씬을 상주 상태로 만든다. 이미 떠 있으면 즉시 끝난다.
    /// 스플래시가 미리 받아 둔 것이 있으면 그것을 활성화하고, 없으면 Additive 로 얹는다.
    /// </summary>
    public IEnumerator EnsureInGameResident()
    {
        if (_residentReady) yield break;

        // ⚠ 실제 로드는 SceneDirector 가 '자기' 코루틴으로 돌린다
        //   호출한 쪽 코루틴에 얹으면, 그쪽이 도중에 StopCoroutine 으로 취소될 때
        //   (예: MainPanel 이 잠깐 켜졌다 꺼지면서 데모 시작을 취소) 로드가 반쯤
        //   진행된 채 죽는다. 그러면 진행 플래그만 켜진 상태로 남아 이후 호출이
        //   전부 영원히 대기한다 — 아군도 안 서고 전투 시작 버튼도 안 먹는 교착.
        //   여기서는 기다리기만 한다. 취소돼도 로드는 계속 굴러간다.
        if (_loadRoutine == null)
            _loadRoutine = StartCoroutine(LoadInGameRoutine());

        while (!_residentReady) yield return null;
    }

    IEnumerator LoadInGameRoutine()
    {
        if (!IsInGameResident)
        {
            if (ScenePreloader.IsInGameReady)
                ScenePreloader.ActivateInGame();
            else
                SceneManager.LoadSceneAsync(_inGameSceneName, LoadSceneMode.Additive);

            while (!IsInGameResident)
                yield return null;
        }

        // 인게임이 막 올라온 순간에는 카메라·HUD 가 켜져 있다 — 로비를 가리기 전에 끈다.
        CacheInGameObjects();
        Apply(_mode);

        // 씬에 놓인 카메라 자리 = 전투 뷰의 정답. 연출은 여기서부터 계산한다.
        ArenaCameraRig.CaptureHome(InGameCam);

        _residentReady = true;
        _loadRoutine   = null;
    }

    // ── 표시 전환 ────────────────────────────────────────────

    public void Present(PresentMode mode)
    {
        _mode = mode;
        Apply(mode);
    }

    void Apply(PresentMode mode)
    {
        bool lobbyCam  = mode == PresentMode.LobbyOnly;
        bool lobbyUi   = mode != PresentMode.Battle;
        bool inGameHud = mode == PresentMode.Battle;

        SetCamera(LobbyCam,  lobbyCam);
        SetCamera(InGameCam, !lobbyCam);

        if (LobbyCanvas  != null) LobbyCanvas.SetActive(lobbyUi);
        if (lobbyUi) ApplyBackdrops();
        if (InGameCanvas != null) InGameCanvas.SetActive(inGameHud);

        // 팝업은 PopupManager 가 자기 캔버스를 들고 있으므로 여기서 손댈 것이 없다.

        // 새로 생기는 오브젝트가 어느 씬에 붙을지 — 전투 중에는 인게임 쪽이어야
        // 로비를 다시 그릴 때 딸려 나오지 않는다.
        Scene target = SceneManager.GetSceneByName(
            mode == PresentMode.LobbyOnly ? _lobbySceneName : _inGameSceneName);
        if (target.IsValid() && target.isLoaded)
            SceneManager.SetActiveScene(target);
    }

    static void SetCamera(Camera cam, bool on)
    {
        if (cam == null) return;
        cam.enabled = on;

        // AudioListener 는 씬당 하나만 살아 있어야 한다
        if (cam.TryGetComponent<AudioListener>(out var listener))
            listener.enabled = on;
    }

    // ── 로비 배경 가리개 ──────────────────────────────────────
    //
    //  ⚠ 카메라와 HUD 만 정리해서는 전장이 보이지 않는다
    //    로비 캔버스에는 화면을 꽉 채우는 불투명 배경이 깔려 있고(Background),
    //    MainPanel 도 자기 배경 이미지를 하나 더 얹는다(BackgroundImage).
    //    UI 는 항상 월드 위에 그려지므로 이 둘을 내리지 않으면 전투는 영영 가려진다.
    //
    //  ⚠ 켜고 끄기를 '요청 수' 로 센다
    //    패널이 켜지고 꺼지는 순서가 엇갈릴 수 있다.
    //    단순 bool 이면 옛 패널의 OnDisable 이 새 패널의 요청을 덮어쓴다.

    // ⚠ 여기서 다루는 것은 '씬이 소유한' 배경 하나뿐이다
    //   패널 자신의 배경(MainPanel/BackgroundImage · BattlePanel/ActionArea)은
    //   프리팹 쪽에서 이미 투명하게 만들어져 있다 — 그쪽이 정본이다.
    //   Lobby 씬의 Background 만 상황에 따라 켜고 꺼야 한다:
    //   영웅·유물 패널에서는 깔려 있어야 하고, 전장을 비출 때는 내려야 한다.
    [Header("전장을 비출 때 내릴 씬 배경")]
    [SerializeField] string[] _backdropNames = { "Background" };

    readonly List<Graphic> _backdrops = new();
    bool _backdropsFound;
    int  _showArenaRequests;

    /// <summary>전장이 보여야 하는 패널이 켜질 때 true, 꺼질 때 false 로 부른다.</summary>
    public void RequestArenaBackdrop(bool on)
    {
        _showArenaRequests = Mathf.Max(0, _showArenaRequests + (on ? 1 : -1));
        ApplyBackdrops();
    }

    void ApplyBackdrops()
    {
        FindBackdrops();
        bool visible = _showArenaRequests == 0;

        foreach (var g in _backdrops)
            if (g != null) g.enabled = visible;
    }

    /// <summary>
    /// ⚠ 오브젝트가 아니라 Image 컴포넌트를 끈다
    ///   BattlePanel 의 ActionArea 는 배경이면서 동시에 스테이지 노드·전투 시작 버튼을
    ///   담는 칸이다. 오브젝트째 끄면 그 안의 UI 가 통째로 사라진다.
    ///   그릴 것만 멈추면 자식은 그대로 보인다.
    /// </summary>
    void FindBackdrops()
    {
        if (_backdropsFound) return;
        if (LobbyCanvas == null) return;

        foreach (Transform t in LobbyCanvas.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            bool match = false;
            foreach (string name in _backdropNames)
                if (t.name == name) { match = true; break; }
            if (!match) continue;

            var g = t.GetComponent<Graphic>();
            if (g != null) _backdrops.Add(g);
        }

        _backdropsFound = true;
    }

    // ── 오브젝트 탐색 (씬이 언로드되지 않으므로 1회성) ────────

    Camera            LobbyCam  => _lobbyCam  != null ? _lobbyCam  : (_lobbyCam  = FindCamera(_lobbySceneName));
    /// <summary>전투·데모가 쓰는 월드 카메라. 연출(무빙·줌)은 전부 이 카메라를 만진다.</summary>
    public Camera     InGameCam => _inGameCam != null ? _inGameCam : (_inGameCam = FindCamera(_inGameSceneName));
    GameObject InGameCanvas => _inGameCanvas != null ? _inGameCanvas : (_inGameCanvas = FindRoot(_inGameSceneName, _inGameCanvasName));
    GameObject LobbyCanvas  => _lobbyCanvas  != null ? _lobbyCanvas  : (_lobbyCanvas  = FindLobbyCanvas());

    void CacheInGameObjects()
    {
        _inGameCam    = FindCamera(_inGameSceneName);
        _inGameCanvas = FindRoot(_inGameSceneName, _inGameCanvasName);
    }

    static Camera FindCamera(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var cam = root.GetComponentInChildren<Camera>(includeInactive: true);
            if (cam != null) return cam;
        }
        return null;
    }

    static GameObject FindRoot(string sceneName, string rootName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;

        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == rootName) return root;
        return null;
    }

    /// <summary>로비 UI 루트 — 캔버스 컴포넌트를 가진 첫 루트를 쓴다 (이름 규칙에 기대지 않는다).</summary>
    GameObject FindLobbyCanvas()
    {
        Scene scene = SceneManager.GetSceneByName(_lobbySceneName);
        if (!scene.IsValid() || !scene.isLoaded) return null;

        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<Canvas>(includeInactive: true) != null) return root;
        return null;
    }
}
