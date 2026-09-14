using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  SplashBootstrap.cs
//  앱 최초 진입 씬(Splash)의 오케스트레이터.
//
//  역할:
//    1. 앱 공통 설정 적용 (FPS, 화면, 오디오, 물리)
//    2. 매니저 클래스 초기화 (UserDataManager, GameSession 등)
//    3. Lobby / InGame 씬 동시 비동기 로딩 + 진행률 표시
//    4. 로딩 완료 후 Lobby 씬으로 전환 (페이드)
//
//  확장 방법:
//    SplashBootstrap 을 상속 → RegisterSteps() 오버라이드
//    → AddStep(new CustomStep(...)) 으로 커스텀 스텝 추가
//
//  Inspector 설정:
//    ProgressBarFill  : Image (fillAmount 방식, Fill)
//    StatusText       : TextMeshProUGUI
//    SplashCanvas     : CanvasGroup (루트 캔버스, 페이드아웃용)
//    LobbySceneName   : "Lobby"   (Build Settings 이름과 일치)
//    InGameSceneName  : "InGame"
//
//  씬 구성:
//    - Lobby 씬은 로딩 완료 후 즉시 활성화됨
//    - InGame 씬은 allowSceneActivation=false 로 대기 상태를 유지
//      → LobbyManager.StartBattle() 이 ScenePreloader.ActivateInGame() 으로 전환
//
//  ※ InGame 씬에 ECS Authoring 이 있는 경우,
//    allowSceneActivation=false 상태에서는 Awake/Baker 가 실행되지 않는다.
//    ActivateInGame() 호출 시점에 ECS 월드가 생성되므로 정상 동작한다.
// ============================================================

public class SplashBootstrap : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────

    [Header("씬 이름 (Build Settings 와 일치시킬 것)")]
    [SerializeField] string _lobbySceneName  = "Lobby";
    [SerializeField] string _inGameSceneName = "InGame";

    [Header("UI 레퍼런스")]
    [SerializeField] Image           _progressBarFill;   // Image Type = Filled, Fill Method = Horizontal
    [SerializeField] TextMeshProUGUI _statusText;
    [SerializeField] CanvasGroup     _splashCanvas;      // 루트 CanvasGroup (페이드아웃용)

    [Header("앱 공통 설정")]
    [Tooltip("목표 프레임레이트. vSync 비활성 시 유효.")]
    [SerializeField] int  _targetFps        = 60;
    [Tooltip("VSync 를 비활성화합니다. 모바일 환경 권장.")]
    [SerializeField] bool _disableVSync     = true;
    [Tooltip("화면 자동 꺼짐 방지 (모바일).")]
    [SerializeField] bool _neverSleep       = true;
    [Tooltip("앱이 포커스를 잃어도 계속 실행.")]
    [SerializeField] bool _runInBackground  = true;

    [Header("전환 연출")]
    [Tooltip("스플래시 최소 표시 시간 (초). 로딩이 빨라도 이 시간은 유지됨.")]
    [SerializeField] float _minDisplaySeconds = 1.5f;
    [Tooltip("씬 전환 페이드아웃 시간 (초).")]
    [SerializeField] float _fadeOutDuration   = 0.6f;

    // ── 내부 상태 ─────────────────────────────────────────────

    readonly List<ISplashStep> _steps = new();
    float _totalWeight;
    float _completedWeight;

    // ── Unity 생명주기 ────────────────────────────────────────

    void Awake()
    {
        ApplyAppSettings();
    }

    IEnumerator Start()
    {
        float startTime = Time.realtimeSinceStartup;

        // 스텝 등록 및 가중치 합산
        RegisterSteps();
        foreach (var s in _steps) _totalWeight += s.Weight;

        // 스텝 순차 실행
        foreach (var step in _steps)
        {
            UpdateStatus(step.Label);

            IEnumerator e = step.Execute();
            while (e.MoveNext())
            {
                float innerT          = Mathf.Clamp01(step.Progress);
                float overallProgress = (_completedWeight + step.Weight * innerT) / _totalWeight;
                UpdateProgress(overallProgress);
                yield return e.Current;
            }

            _completedWeight += step.Weight;
            UpdateProgress(_completedWeight / _totalWeight);
        }

        // 최소 표시 시간 보장
        float elapsed = Time.realtimeSinceStartup - startTime;
        if (elapsed < _minDisplaySeconds)
            yield return new WaitForSecondsRealtime(_minDisplaySeconds - elapsed);

        if (!ScenePreloader.IsInGameReady)
            Debug.LogWarning(
                $"[SplashBootstrap] InGame 씬이 준비되지 않았습니다 ('{_inGameSceneName}').\n" +
                "StartBattle 호출 시 일반 로딩으로 폴백합니다.");

        // ── Lobby 씬 활성화 ───────────────────────────────────
        // Splash 페이드아웃 전에 Lobby 를 Additive 로 활성화해 두고,
        // PopupManager(Lobby 씬)가 준비된 뒤 로딩 팝업을 띄운다.

        if (ScenePreloader.IsLobbyReady)
            ScenePreloader.ActivateLobby();
        else
        {
            Debug.LogWarning(
                $"[SplashBootstrap] Lobby 씬이 준비되지 않았습니다 ('{_lobbySceneName}'). 직접 로드합니다.");
            SceneManager.LoadSceneAsync(_lobbySceneName, LoadSceneMode.Additive);
        }

        // Lobby 씬이 완전히 로드될 때까지 대기
        while (true)
        {
            Scene lobby = SceneManager.GetSceneByName(_lobbySceneName);
            if (lobby.IsValid() && lobby.isLoaded) break;
            yield return null;
        }

        // Lobby 를 활성 씬으로 설정 (이후 생성 오브젝트가 Lobby 씬에 귀속)
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(_lobbySceneName));

        // 로딩 팝업 오픈 (Lobby 씬의 PopupManager 가 초기화된 상태)
        if (PopupManager.Instance != null)
            PopupManager.Instance.Open(PopupType.Loading);

        // Splash 캔버스 페이드아웃
        yield return StartCoroutine(FadeOut());

        // 로딩 팝업 닫기 → Lobby UI 노출
        if (PopupManager.Instance != null)
            PopupManager.Instance.Close(PopupType.Loading);

        // Splash 씬 언로드 (이 코루틴은 여기서 중단됨 — 이후 처리 불필요)
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }

    // ── 스텝 등록 (확장 포인트) ───────────────────────────────

    /// <summary>
    /// 실행할 초기화 스텝을 등록한다.
    /// 상속 클래스에서 오버라이드해 커스텀 스텝을 추가하거나 순서를 바꿀 수 있다.
    /// <code>
    ///   protected override void RegisterSteps()
    ///   {
    ///       base.RegisterSteps();
    ///       AddStep(new MyServerAuthStep());
    ///   }
    /// </code>
    /// </summary>
    protected virtual void RegisterSteps()
    {
        AddStep(new AppWarmupStep());
        AddStep(new ManagerInitStep());
        AddStep(new ScenePreloadStep(_lobbySceneName, _inGameSceneName));
    }

    protected void AddStep(ISplashStep step) => _steps.Add(step);

    // ── 앱 설정 ──────────────────────────────────────────────

    void ApplyAppSettings()
    {
        // 프레임레이트
        if (_disableVSync)
            QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = _targetFps;

        // 화면
        Screen.sleepTimeout    = _neverSleep ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
        Application.runInBackground = _runInBackground;

        // 오디오 초기 상태 보장
        AudioListener.pause  = false;
        AudioListener.volume = 1f;

        // 3D 물리 비활성 — 2D 오토배틀 전용 프로젝트
        // 필요하다면 제거할 것
        Physics.autoSimulation = false;

        // Time
        Time.timeScale = 1f;
        Random.InitState(System.DateTime.Now.Millisecond);

#if UNITY_ANDROID || UNITY_IOS
        Screen.orientation = ScreenOrientation.LandscapeLeft;
#endif

        Debug.Log($"[Splash] 앱 설정 완료 — FPS:{_targetFps} vSync:{!_disableVSync}");
    }

    // ── UI 헬퍼 ──────────────────────────────────────────────

    void UpdateProgress(float t)
    {
        if (_progressBarFill != null)
            _progressBarFill.fillAmount = Mathf.Clamp01(t);
    }

    void UpdateStatus(string msg)
    {
        if (_statusText != null)
            _statusText.text = msg;
    }

    IEnumerator FadeOut()
    {
        if (_splashCanvas == null) yield break;

        float elapsed = 0f;
        while (elapsed < _fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _splashCanvas.alpha = 1f - Mathf.Clamp01(elapsed / _fadeOutDuration);
            yield return null;
        }
        _splashCanvas.alpha = 0f;
    }

    // ============================================================
    //  ISplashStep 인터페이스
    //  스플래시에서 실행될 초기화 단계의 계약.
    // ============================================================

    protected interface ISplashStep
    {
        string Label    { get; }   // 로딩 화면 상태 텍스트
        float  Weight   { get; }   // 전체 진행률 내 비중
        float  Progress { get; }   // 0~1 내부 진행률 (Execute 중 갱신)
        IEnumerator Execute();
    }

    // ============================================================
    //  내장 스텝 1 — AppWarmupStep
    //  셰이더 웜업, 엔진 1프레임 초기화 대기.
    // ============================================================

    sealed class AppWarmupStep : ISplashStep
    {
        public string Label    => "엔진 초기화 중...";
        public float  Weight   => 0.05f;
        public float  Progress { get; private set; }

        public IEnumerator Execute()
        {
            Progress = 0f;

            // 엔진이 첫 프레임을 완전히 처리하도록 대기
            yield return null;
            Progress = 0.4f;

            // ShaderVariantCollection 이 있다면 여기서 Warmup
            // shaderCollection?.WarmUp();
            yield return null;
            Progress = 1f;
        }
    }

    // ============================================================
    //  내장 스텝 2 — ManagerInitStep
    //  순수 C# 싱글톤 매니저를 초기화한다.
    //  MonoBehaviour 싱글톤(PoolController 등)은 해당 씬에 배치되어
    //  자체 Awake 에서 초기화되므로 여기서 다루지 않는다.
    // ============================================================

    sealed class ManagerInitStep : ISplashStep
    {
        public string Label    => "데이터 로드 중...";
        public float  Weight   => 0.15f;
        public float  Progress { get; private set; }

        public IEnumerator Execute()
        {
            Progress = 0f;
            yield return null;

            // UserDataManager — PureSingleton, Instance 접근 시 OnInitialize() + LoadAll() 자동 실행
            _ = UserDataManager.Instance;
            Progress = 1f;

            // 추가 매니저가 생기면 아래에 계속 등록
            // _ = SomeOtherManager.Instance;
        }
    }

    // ============================================================
    //  내장 스텝 3 — ScenePreloadStep
    //  InGame 씬을 Additive + allowSceneActivation=false 로 미리 로딩한다.
    //
    //  allowSceneActivation=false 상태의 AsyncOperation 은
    //  이후 LoadScene(Lobby, Single) 의 영향을 받지 않고 메모리에 유지된다.
    //
    //  InGame 씬의 InGameManager 루트 오브젝트는 Inspector 에서
    //  비활성(inactive) 상태로 배치해야 한다.
    //  → ScenePreloader.ActivateInGame() 으로 씬이 활성화된 뒤
    //    ScenePreloader.FindInGameManagerRoot() 로 루트를 찾아 SetActive(true).
    // ============================================================

    sealed class ScenePreloadStep : ISplashStep
    {
        public string Label    => "씬 로딩 중...";
        public float  Weight   => 0.80f;
        public float  Progress { get; private set; }

        readonly string _lobbyName;
        readonly string _inGameName;

        public ScenePreloadStep(string lobbyName, string inGameName)
        {
            _lobbyName  = lobbyName;
            _inGameName = inGameName;
        }

        public IEnumerator Execute()
        {
            Progress = 0f;

            // Lobby + InGame 씬 동시 비동기 로딩 (Additive, allowSceneActivation=false)
            var lobbyOp  = SceneManager.LoadSceneAsync(_lobbyName,  LoadSceneMode.Additive);
            var inGameOp = SceneManager.LoadSceneAsync(_inGameName, LoadSceneMode.Additive);

            if (lobbyOp == null)
            {
                Debug.LogError($"[ScenePreloadStep] Lobby 씬 로드 실패: '{_lobbyName}' — Build Settings 확인");
                Progress = 1f;
                yield break;
            }
            if (inGameOp == null)
            {
                Debug.LogError($"[ScenePreloadStep] InGame 씬 로드 실패: '{_inGameName}' — Build Settings 확인");
                Progress = 1f;
                yield break;
            }

            lobbyOp.allowSceneActivation  = false;
            inGameOp.allowSceneActivation = false;

            // 두 씬 모두 0.9(준비 완료) 에 도달할 때까지 진행률 갱신
            while (lobbyOp.progress < 0.9f || inGameOp.progress < 0.9f)
            {
                Progress = (lobbyOp.progress / 0.9f + inGameOp.progress / 0.9f) * 0.5f;
                yield return null;
            }

            ScenePreloader.SetLobbyOp(lobbyOp);
            ScenePreloader.SetInGameOp(inGameOp);
            Progress = 1f;
        }
    }
}

// ============================================================
//  ScenePreloader  (static 유틸리티)
//  사전 로딩된 InGame 씬 AsyncOperation 을 보관·제공한다.
//
//  흐름:
//    Splash  : SetInGameOp(op)  — allowSceneActivation=false 상태 보관
//    StartBattle: ActivateInGame() → 씬 완전 활성화 대기
//              → FindInGameManagerRoot() → SetActive(true) → 배틀 시작
// ============================================================

public static class ScenePreloader
{
    static AsyncOperation _lobbyOp;
    static AsyncOperation _inGameOp;

    /// <summary>Lobby AsyncOperation 이 준비(90%)되었으면 true.</summary>
    public static bool IsLobbyReady  => _lobbyOp  is { progress: >= 0.9f };

    /// <summary>InGame AsyncOperation 이 준비(90%)되었으면 true.</summary>
    public static bool IsInGameReady => _inGameOp is { progress: >= 0.9f };

    internal static void SetLobbyOp(AsyncOperation op)  => _lobbyOp  = op;
    internal static void SetInGameOp(AsyncOperation op) => _inGameOp = op;

    /// <summary>대기 중인 Lobby 씬을 활성화한다.</summary>
    public static void ActivateLobby()
    {
        if (_lobbyOp == null)
        {
            Debug.LogWarning("[ScenePreloader] Lobby AsyncOperation 이 없습니다.");
            return;
        }
        _lobbyOp.allowSceneActivation = true;
        _lobbyOp = null;
    }

    /// <summary>대기 중인 InGame 씬을 활성화한다. IsInGameReady 가 true 일 때만 호출.</summary>
    public static void ActivateInGame()
    {
        if (_inGameOp == null)
        {
            Debug.LogWarning("[ScenePreloader] InGame AsyncOperation 이 없습니다.");
            return;
        }
        _inGameOp.allowSceneActivation = true;
        _inGameOp = null;
    }
}
