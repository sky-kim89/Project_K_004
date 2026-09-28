using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  TutorialManager.cs
//  튜토리얼 총괄. 시나리오를 소유하고, 언제 띄울지 판단하고,
//  진행 중에는 다른 UI 입력을 막는다.
//
//  ■ 세 가지 일만 한다
//    ① 노출 판단  — TutorialData 기록을 보고 "아직 안 본 것" 만 재생
//    ② 진행 저장  — 스텝마다 인덱스를 남겨 앱이 꺼져도 이어 본다
//    ③ 입력 차단  — 전용 UI 팔레트(TutorialOverlay)가 화면 최상단을 덮는다
//
//  ■ UI 팔레트는 매니저 소유다
//    씬의 캔버스를 빌리지 않는다. Lobby·InGame 이 함께 떠 있고 팝업까지
//    얹히는 구조라, 빌려 쓰면 그 캔버스가 꺼지는 순간 튜토리얼이 통째로
//    사라진다. sortingOrder 1000 으로 로비(0)·인게임(10)·팝업(200) 위에 둔다.
//
//  ■ 등록은 코드로 한다
//    RegisterDefaults() 한 곳에 시나리오를 모은다. SO 나 인스펙터 배열로
//    빼면 시나리오를 추가할 때마다 에셋을 손대야 하는데, 튜토리얼은
//    코드와 함께 바뀌므로 같은 자리에 있는 편이 낫다.
//
//  사용:
//    TutorialManager.Instance.TryPlay(TutorialId.InGame);        // 조건부 (안 봤으면)
//    TutorialManager.Instance.Replay(TutorialId.HelpRelic);      // i 버튼 — 항상
//    TutorialManager.Instance.Skip();                            // 건너뛰기 버튼
// ============================================================

public class TutorialManager : Singleton<TutorialManager>
{
    // ── 등록된 시나리오 ──────────────────────────────────────
    readonly Dictionary<TutorialId, TutorialScenario> _scenarios = new();

    TutorialOverlay _overlay;
    Coroutine       _running;

    /// <summary>지금 재생 중인 시나리오. 없으면 null.</summary>
    public TutorialScenario Current { get; private set; }

    /// <summary>튜토리얼이 화면을 잡고 있는가 — 다른 시스템이 끼어들지 판단할 때.</summary>
    public bool IsPlaying => Current != null;

    /// <summary>재생 시작·종료 알림. 자동 전투·데모가 이 신호로 멈추거나 이어 간다.</summary>
    public static event Action<TutorialId> OnStarted;
    public static event Action<TutorialId> OnFinished;

    // ── 생명주기 ─────────────────────────────────────────────

    /// <summary>
    /// 없으면 만들어서 돌려준다.
    ///
    /// ⚠ 씬에 배치하지 않는다
    ///   인스펙터에 물릴 값이 하나도 없고(시나리오는 코드 등록), Lobby·InGame·
    ///   Splash 어디서든 첫 호출에 필요하다. 씬마다 놓으면 중복 인스턴스가 생기고
    ///   어느 씬에서 시작했느냐에 따라 있고 없고가 갈린다.
    /// </summary>
    public static TutorialManager Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject(nameof(TutorialManager));
        return go.AddComponent<TutorialManager>();
    }

    /// <summary>
    /// 씬이 올라오면 알아서 생긴다.
    ///
    /// ⚠ 없으면 트리거를 통째로 놓친다
    ///   노출 시점은 BattleManager 의 static 이벤트로 잡는데, 구독할 인스턴스가
    ///   없으면 이벤트는 그냥 지나간다. "튜토리얼이 안 뜨는" 가장 흔한 원인이
    ///   매니저 미배치이므로 아예 배치를 요구하지 않는다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate() => Ensure();

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;   // 중복 인스턴스는 base 가 지운다

        RegisterDefaults();
    }

    // ── 노출 시점 ────────────────────────────────────────────
    //
    //  ⚠ "언제 띄울지" 는 전부 여기 모은다
    //    각 UI 가 제 자리에서 TryPlay 를 부르게 하면, 튜토리얼 순서를 바꿀 때
    //    어디를 고쳐야 하는지 찾을 수가 없다. 트리거는 매니저가 소유한다.
    //
    //  ⚠ 조건이 안 맞으면 TryPlay 가 알아서 무시한다
    //    이미 봤거나 · 시나리오가 없거나 · 다른 튜토리얼이 도는 중이면 false 를
    //    돌려주고 끝이라, 부르는 쪽에서 따로 검사하지 않는다.

    void OnEnable()
    {
        if (Instance != this) return;
        BattleManager.OnDefeat       += HandleDefeat;
        BattleManager.OnVictory      += HandleVictory;
        StageLoopDirector.OnStageReady += HandleStageReady;
        MainPanelUI.OnShown          += HandleMainPanelShown;
        MainPanelUI.OnHidden         += HandleMainPanelHidden;

        // ⚠ 매니저가 화면보다 늦게 생길 수 있다 (AutoCreate 는 씬 로드 뒤에 돈다)
        //   이미 떠 있는 메인 화면은 OnShown 을 놓친 뒤다. 지금 상태를 직접 본다.
        if (FindAnyObjectByType<MainPanelUI>() != null) HandleMainPanelShown();

        // 같은 이유 — 이미 대기 중인 스테이지는 OnStageReady 를 놓친 뒤다.
        var director = StageLoopDirector.Instance;
        if (director != null && director.IsStageReady) HandleStageReady(director.StageNumber);
    }

    void OnDisable()
    {
        BattleManager.OnDefeat       -= HandleDefeat;
        BattleManager.OnVictory      -= HandleVictory;
        StageLoopDirector.OnStageReady -= HandleStageReady;
        MainPanelUI.OnShown          -= HandleMainPanelShown;
        MainPanelUI.OnHidden         -= HandleMainPanelHidden;

        // 씬이 갈리거나 매니저가 꺼질 때 멈춘 채로 두지 않는다.
        // 이 한 줄이 없으면 "튜토리얼 중 씬 전환 = 게임 영구 정지" 가 된다.
        SetPaused(false);

        // 꺼지면 코루틴도 함께 멈춘다 — 핸들을 비워 둬야 다시 켤 때 큐가 다시 돈다.
        _pump = null;
    }

    /// <summary>
    /// 패배·포기 — 인게임 시나리오의 전제가 통째로 깨졌다.
    ///
    /// ⚠ 이게 없으면 사라진 HUD 를 가리킨 채 타임아웃까지 돈다
    ///   PausePopup 의 '즉시 환생' 도 Surrender() → 패배 처리로 같은 길을 탄다.
    ///   장수 카드·스킬 슬롯이 걷힌 화면에서 "지금 눌러 보세요" 가 45~60초 남는다.
    ///
    /// ⚠ 완료로 치지 않는다 (Abort)
    ///   첫 판에 지고 환생했다고 튜토리얼을 본 것은 아니다. 다음 전투에서 이어 본다.
    /// </summary>
    void HandleDefeat() => Abort();

    /// <summary>
    /// 스테이지 대기 — <b>1스테이지에서만</b> 인게임 안내를 건다.
    ///
    /// ⚠ 1스테이지 뒤로는 강제 안내를 하지 않는다 (사용자 지시, 2026-09-17)
    ///   첫 실행은 LobbyManager 가 곧장 1스테이지로 넣는다. 첫 판에 져서 환생했으면
    ///   다음 런의 1스테이지에서 이어 본다 (완료 기록이 없으므로).
    /// </summary>
    void HandleStageReady(int stage)
    {
        if (stage != 1) return;
        Enqueue(TutorialId.SummonBattle);
    }

    /// <summary>
    /// 1스테이지 클리어 — 카드 3택과 갈림길을 짚는다.
    ///
    /// ⚠ 인게임 안내를 끝까지 본 사람에게만 건다
    ///   인게임 안내가 판 도중에 끊겼거나(패배) 아직 도는 중이면 순서가 뒤집힌다.
    ///   큐는 앞엣것이 끝나야 뒤엣것을 꺼내므로, 도는 중이면 뒤에 붙는다.
    /// </summary>
    void HandleVictory()
    {
        var director = StageLoopDirector.Instance;
        if (director == null || director.StageNumber != 1) return;
        Enqueue(TutorialId.FirstReward);
    }

    /// <summary>
    /// 메인 화면(장수 선택) 진입 — 첫 환생을 마친 사람에게만 유물을 안내한다.
    ///
    /// ⚠ 환생을 한 번도 안 했으면 띄우지 않는다
    ///   쓸 포인트가 0 인 화면에서 "여기서 강화하세요" 는 설명이 아니라 광고다.
    ///   첫 환생 뒤라야 포인트를 손에 쥔 채로 같은 화면을 본다.
    ///
    ///   두 번째부터는 TutorialData 기록이 알아서 거른다 — 여기서 셀 필요가 없다.
    /// </summary>
    void HandleMainPanelShown()
    {
        var reinc = UserDataManager.Instance?.Get<ReincarnationData>();
        if (reinc == null || reinc.TotalCount < 1) return;

        // 유물 → 도감(슬라임 품질 개선) 순서. 둘 다 보상 상자·해금 연출 같은
        // 팝업이 전부 닫힌 뒤에 시작한다 (CanStartNow).
        Enqueue(TutorialId.FirstRelic, TutorialId.FirstCodex);
    }

    /// <summary>
    /// 메인 화면을 떠났다 — 아직 시작 못 한 유물 안내 예약을 버린다.
    ///
    /// ⚠ 예약은 화면을 떠나면 따라가면 안 된다
    ///   수확 팝업이 떠 있는 동안 큐는 대기 상태로 남는다. 그 상태로 출전을 누르면
    ///   전투에 들어가 팝업이 걷히는 순간 시작돼, 없는 유물 버튼을 30초 기다리다
    ///   타임아웃 뒤 전투 화면에서 "여정이 끝나고 환생했습니다" 를 띄웠다.
    ///
    ///   재생 중인 것은 건드리지 않는다 — 튜토리얼이 스스로 팝업을 열어
    ///   패널이 잠시 꺼지는 경우가 있어, 여기서 끊으면 제 튜토리얼이 죽는다.
    /// </summary>
    void HandleMainPanelHidden()
    {
        CancelQueued(TutorialId.FirstRelic);
        CancelQueued(TutorialId.FirstCodex);
    }

    // ── 예약 큐 ──────────────────────────────────────────────
    //
    //  ⚠ 한 트리거에서 여러 개를 띄우려면 큐가 있어야 한다
    //    TryPlay 는 다른 튜토리얼이 도는 중이면 그냥 false 를 돌려준다.
    //    Intro 와 InGame 을 연달아 부르면 뒤엣것이 조용히 버려진다.
    //    큐에 넣어 두고 앞엣것이 끝날 때 다음을 꺼낸다.
    //
    //  ⚠ 이미 본 것은 큐에서 저절로 빠진다
    //    TryPlay 가 완료 기록을 보고 거르므로, 두 번째 승리부터는
    //    아무것도 재생되지 않고 큐만 비워진다.

    readonly Queue<TutorialId> _queue = new();
    Coroutine _pump;

    /// <summary>
    /// 순서대로 재생을 예약한다. 이미 봤거나 시나리오가 없으면 건너뛴다.
    ///
    /// ⚠ 같은 예약을 두 번 넣지 않는다
    ///   화면을 오갈 때마다 트리거가 다시 도는데(MainPanelUI.OnEnable 등),
    ///   앞엣것이 팝업 때문에 대기 중이면 큐에 같은 것이 계속 쌓였다.
    /// </summary>
    public void Enqueue(params TutorialId[] ids)
    {
        foreach (var id in ids)
            if (!_queue.Contains(id)) _queue.Enqueue(id);
        PumpQueue();
    }

    /// <summary>
    /// 아직 시작하지 않은 예약을 큐에서 뺀다. 재생 중인 것은 건드리지 않는다
    /// (중단은 Abort 담당).
    /// </summary>
    public void CancelQueued(TutorialId id)
    {
        if (!_queue.Contains(id)) return;

        var kept = new List<TutorialId>(_queue);
        kept.RemoveAll(q => q == id);
        _queue.Clear();
        foreach (var q in kept) _queue.Enqueue(q);
    }

    void PumpQueue()
    {
        if (_pump == null && _queue.Count > 0 && isActiveAndEnabled)
            _pump = StartCoroutine(PumpRoutine());
    }

    /// <summary>
    /// 큐를 앞에서부터 하나씩 재생한다. 시작할 수 없으면 **버리지 않고 기다린다.**
    ///
    /// ⚠ 예전엔 한 번 훑고 끝이었다
    ///   조건이 아직 안 맞으면 TryPlay 가 false 를 돌려주고 그 시나리오는
    ///   큐에서 사라졌다. 지금은 화면이 조용해질 때까지 기다렸다가 꺼낸다.
    ///
    /// ⚠ 팝업 위에서 시작하지 않는다
    ///   전투가 끝나면 어빌리티 선택 팝업이 먼저 뜨는데, 로비는 그 뒤에 이미
    ///   서 있다(로비·인게임 동시 상주). "로비가 떴다" 만 보고 시작하면
    ///   오버레이(sortingOrder 1000)가 팝업을 덮어 선택을 막아 버린다 —
    ///   실제로 어빌리티 팝업 위에서 로비 튜토리얼이 뜬 적이 있다.
    /// </summary>
    IEnumerator PumpRoutine()
    {
        while (_queue.Count > 0)
        {
            if (IsPlaying || !CanStartNow(_queue.Peek()))
            {
                yield return null;   // timeScale 0 이어도 프레임마다 돈다
                continue;
            }

            TryPlay(_queue.Dequeue());
            yield return null;
        }
        _pump = null;
    }

    /// <summary>
    /// 지금 이 시나리오를 띄워도 되는 화면인가.
    /// 등록되지 않은 시나리오는 true — 큐에서 빠져야 뒤엣것이 진행된다.
    /// </summary>
    bool CanStartNow(TutorialId id)
    {
        if (!_scenarios.TryGetValue(id, out var scenario)) return true;

        var pm = PopupManager.Instance;
        if (pm == null || !pm.HasAnyOpen) return true;

        // 자기 무대 팝업 하나만 떠 있는 경우만 예외 (승리 팝업 위의 BattleResult 등)
        return scenario.StagePopup != PopupType.None
            && pm.OpenCount == 1
            && pm.IsOpen(scenario.StagePopup);
    }

    /// <summary>
    /// 시나리오 등록 지점. 새 튜토리얼은 여기 한 줄만 추가하면 된다.
    ///
    /// ⚠ 아직 시나리오 구현체가 없다 — 베이스만 올린 상태다.
    ///   구현하는 대로 Register(new XxxTutorial()) 를 채운다.
    /// </summary>
    void RegisterDefaults()
    {
        // 강제 진행 — 첫 실행 1스테이지(인게임 → 보상) · 첫 환생 뒤 메인 화면(유물 → 도감)
        Register(new SummonBattleTutorial());
        Register(new FirstRewardTutorial());
        Register(new FirstRelicTutorial());
        Register(new FirstCodexTutorial());

        // 도움말 (팝업 헤더의 i 버튼) — 강제로 뜨지 않는다
        Register(new RelicHelpTutorial());
        Register(new CodexHelpTutorial());
        Register(new DifficultyHelpTutorial());
        Register(new MonsterDetailHelpTutorial());
        Register(new CardSelectHelpTutorial());
        Register(new BattleInfoHelpTutorial());
    }

    public void Register(TutorialScenario scenario)
    {
        if (scenario == null) return;
        if (_scenarios.ContainsKey(scenario.Id))
        {
            Debug.LogWarning($"[Tutorial] {scenario.Id} 시나리오가 이미 등록돼 있습니다 — 나중 것을 무시합니다.");
            return;
        }
        scenario.Manager = this;
        _scenarios[scenario.Id] = scenario;
    }

    public bool Has(TutorialId id) => _scenarios.ContainsKey(id);

    // ── 재생 ─────────────────────────────────────────────────

    /// <summary>
    /// 아직 안 봤으면 재생한다. 강제 진행 튜토리얼의 정식 진입점.
    /// 이미 봤거나, 다른 튜토리얼이 도는 중이거나, 시나리오가 없으면 false.
    /// </summary>
    /// <summary>
    /// 튜토리얼 전역 스위치. false 면 강제 진행·재생이 전부 막힌다.
    /// 이 게임용 시나리오로 갈아 끼운 뒤 다시 켰다 (2026-09-17).
    /// </summary>
    public static bool Enabled = true;

    public bool TryPlay(TutorialId id)
    {
        if (!Enabled) return false;
        if (IsPlaying) return false;

        var data = Data;
        if (data == null || !data.ShouldPlay(id)) return false;
        if (!_scenarios.TryGetValue(id, out var scenario)) return false;

        // 이어 보기 — 앱이 끊긴 지점부터. 처음이면 0.
        int start = data.IsResuming(id) && scenario.Resumable ? data.InProgressStep : 0;
        StartScenario(scenario, start);
        return true;
    }

    /// <summary>
    /// 기록을 무시하고 처음부터 재생한다. 팝업의 i 버튼이 부른다.
    /// ⚠ 완료 기록을 건드리지 않는다 — 도움말을 봤다고 강제 진행이 소멸하면 안 된다.
    /// </summary>
    public bool Replay(TutorialId id)
    {
        if (!Enabled) return false;
        if (IsPlaying) return false;
        if (!_scenarios.TryGetValue(id, out var scenario)) return false;

        StartScenario(scenario, 0, recordProgress: id.IsForced());
        return true;
    }

    /// <summary>
    /// 지금 튜토리얼을 건너뛴다 — 완료로 기록해 다시 뜨지 않게 한다.
    /// 오버레이 오른쪽 위 '건너뛰기' 버튼이 부른다.
    ///
    /// ⚠ 예약된 뒷 시나리오까지 함께 접는다
    ///   "건너뛰기" 는 이 화면 하나가 아니라 안내 자체를 그만 보겠다는 뜻이다.
    ///   큐를 그대로 두면 승리 → 로비 → 장수 상세로 넘어갈 때마다 또 눌러야 해서,
    ///   버튼이 있는데도 빠져나온 기분이 안 든다.
    ///
    /// ⚠ 버리는 것들도 완료로 남긴다
    ///   기록을 안 남기면 다음 전투의 같은 트리거에서 그대로 다시 뜬다.
    ///   건너뛴 사람에게 두 번 묻지 않는다.
    /// </summary>
    public void Skip()
    {
        if (!IsPlaying) return;

        // ⚠ 도움말을 건너뛴 것으로 강제 진행 큐까지 접지 않는다
        //   버튼은 이제 도움말(i 버튼)에만 붙는다. 그걸 닫았다고 아직 보지도 않은
        //   로비·장수 안내를 "봤다" 로 기록하면 영영 안 뜬다.
        if (Current.Id.IsForced()) DropQueuedAsCompleted();

        Stop(complete: true);
    }

    void DropQueuedAsCompleted()
    {
        if (_queue.Count == 0) return;

        var data = Data;
        while (_queue.Count > 0)
        {
            var id = _queue.Dequeue();
            if (id.IsForced()) data?.MarkCompleted(id);
        }
        UserDataManager.Instance?.RequestSave();
    }

    /// <summary>
    /// 진행을 중단한다. 완료로 치지 않으므로 조건이 다시 맞으면 이어 본다.
    /// 씬 전환·전투 강제 종료 등 시나리오 전제가 깨졌을 때 부른다.
    ///
    /// ⚠ 예약된 뒷 시나리오도 함께 버린다
    ///   Stop() 끝에서 PumpQueue 가 도므로, 큐를 비우지 않으면 전제가 깨진 직후
    ///   다음 시나리오가 곧바로 뜬다. 같은 트리거로 예약된 것들은 전제를 공유한다.
    /// </summary>
    public void Abort()
    {
        _queue.Clear();
        if (!IsPlaying) return;
        Stop(complete: Current.CompleteOnAbort);
    }

    // ── 내부 — 재생 루프 ─────────────────────────────────────

    bool _recordProgress;

    void StartScenario(TutorialScenario scenario, int startStep, bool recordProgress = true)
    {
        Current         = scenario;
        _recordProgress = recordProgress;
        scenario.Rewind(startStep);

        if (_recordProgress)
        {
            var data = Data;
            data?.BeginTutorial(scenario.Id);
            data?.SetStep(scenario.Id, startStep);
            UserDataManager.Instance?.RequestSave();
        }

        EnsureOverlay();
        _overlay.BringToFront();

        // ⚠ 강제 진행 튜토리얼에는 건너뛰기 버튼을 두지 않는다
        //   버튼이 화면 오른쪽 위에 상주하는데 거기가 배속·오토 버튼 자리다.
        //   인게임 시나리오는 그 두 버튼을 가리키며 설명하는데, 정작 버튼이
        //   건너뛰기에 가려 보이지 않았다.
        //   도움말(i 버튼)로 다시 볼 때만 남긴다 — 그쪽은 스스로 연 화면이라
        //   빠져나갈 문이 필요하다.
        _overlay.SetSkipVisible(!scenario.Id.IsForced());
        _overlay.ShowBlockOnly();

        OnStarted?.Invoke(scenario.Id);
        _running = StartCoroutine(RunScenario(scenario));
    }

    IEnumerator RunScenario(TutorialScenario scenario)
    {
        while (!scenario.IsFinished)
        {
            yield return scenario.Next();

            // ⚠ 스텝이 끝날 때마다 저장한다
            //   튜토리얼 도중에 앱이 죽는 일은 흔하다. 스텝 단위로 남겨 두지
            //   않으면 처음부터 다시 시켜야 하는데, 그 사이 게임 상태가
            //   달라져 있어서 시나리오 전제가 깨진다.
            if (_recordProgress)
            {
                Data?.SetStep(scenario.Id, scenario.CurrentStep);
                UserDataManager.Instance?.RequestSave();
            }
        }

        Stop(complete: true);
    }

    void Stop(bool complete)
    {
        var id = Current?.Id ?? TutorialId.None;

        // ⚠ 무엇보다 먼저 시간을 되돌린다
        //   아래에서 예외가 나든 코루틴이 끊기든, timeScale 이 0 인 채로 남으면
        //   게임이 통째로 멈춘다. 튜토리얼 버그가 게임 정지로 번지면 안 된다.
        SetPaused(false);

        if (_running != null) { StopCoroutine(_running); _running = null; }

        if (_recordProgress && id != TutorialId.None)
        {
            var data = Data;
            if (complete) data?.MarkCompleted(id);
            else          data?.ClearInProgress();
            UserDataManager.Instance?.RequestSave();
        }

        Current = null;
        _overlay?.Hide();
        OnFinished?.Invoke(id);

        // 예약된 다음 시나리오로 이어 간다 (승리 → 로비 → 장수 상세 등).
        // ⚠ Current 를 비운 뒤에 부른다 — 그 전에 부르면 IsPlaying 이 true 라 막힌다.
        PumpQueue();
    }

    // ── 시나리오가 부르는 창구 ───────────────────────────────

    /// <summary>
    /// 스텝 하나를 띄우고 넘어갈 조건이 찰 때까지 기다린다.
    /// TutorialScenario.Show() 가 이걸 부른다.
    /// </summary>
    internal IEnumerator RunStep(TutorialScenario scenario, TutorialStep step)
    {
        step.OnEnter?.Invoke();

        EnsureOverlay();
        SetPaused(step.PauseGame);

        bool advanced = false;
        Action onClick = step.Advance == TutorialAdvance.AnyClick ? () => advanced = true : null;

        // 타겟은 지금 찾는다 — 스텝을 만들 때는 아직 없었을 수 있다.
        // ClickTarget 인데 타겟이 없으면 영원히 못 넘어가므로 잠시 기다려 본다.
        RectTransform target = step.ResolveTarget();
        if (target == null && step.Advance == TutorialAdvance.ClickTarget)
        {
            float end = Time.realtimeSinceStartup + 5f;
            while (target == null && Time.realtimeSinceStartup < end)
            {
                yield return null;
                target = step.ResolveTarget();
            }
            if (target == null)
                Debug.LogWarning($"[Tutorial] {scenario.Id} — 누를 타겟을 찾지 못해 클릭 대기를 건너뜁니다.");
        }

        _overlay.Show(step, target, scenario.HintFor(step), onClick);

        switch (step.Advance)
        {
            case TutorialAdvance.AnyClick:
                while (!advanced) yield return null;
                break;

            case TutorialAdvance.ClickTarget:
                yield return WaitForTargetClick(step, target);
                break;

            case TutorialAdvance.Condition:
                yield return WaitCondition(step.WaitUntil);
                break;

            case TutorialAdvance.Timed:
                float end = Time.realtimeSinceStartup + step.Duration;
                while (Time.realtimeSinceStartup < end) yield return null;
                break;
        }

        step.OnExit?.Invoke();
    }

    /// <summary>
    /// 아무것도 안 보여주고 입력만 막는다 — 스텝 사이 대기 구간.
    /// dim=false 면 어둡게 하지 않는다 (기다리는 동안 전투가 보여야 할 때).
    /// </summary>
    internal void BlockOnly(bool dim = true)
    {
        EnsureOverlay();
        SetPaused(false);       // 대기 구간은 게임이 굴러가야 조건이 찬다
        _overlay.ShowBlockOnly(dim);
    }

    /// <summary>차단을 푼다 — 플레이어가 직접 조작해야 진행되는 구간.</summary>
    internal void Unblock()
    {
        SetPaused(false);
        _overlay?.Hide();
    }

    // ── 일시 정지 ────────────────────────────────────────────
    //
    //  ⚠ 읽는 동안에는 멈춰야 한다
    //    설명을 읽는 사이에도 전투가 굴러가서, 말풍선을 다 읽고 나면
    //    스테이지가 이미 끝나 있는 일이 생겼다.
    //
    //  ⚠ 원래 값으로 되돌린다 — 1 로 되돌리면 안 된다
    //    배속 토글이 timeScale 을 1/2/3 으로 쓴다 (TopBarUI.ApplySpeed).
    //    끝나고 1 로 못 박으면 3배속으로 놀던 사람이 튜토리얼 한 번 보고
    //    1배속으로 떨어진다. PausePopup 과 같은 규칙이다.
    //
    //  ⚠ 저장은 '멈추기 직전' 한 번만 한다
    //    이미 멈춰 있는데 또 저장하면 0 을 원래 값으로 기억해, 푸는 순간
    //    게임이 멈춘 채로 남는다.

    bool  _paused;
    float _prevTimeScale = 1f;

    void SetPaused(bool on)
    {
        if (_paused == on) return;

        if (on)
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            // 0 을 복원하면 그대로 멈춘 게임이 된다 — 그런 값은 1 로 친다.
            Time.timeScale = _prevTimeScale > 0f ? _prevTimeScale : 1f;
        }

        _paused = on;
    }

    // ── 내부 — 대기 ──────────────────────────────────────────

    /// <summary>
    /// 타겟을 실제로 누를 때까지 기다린다.
    ///
    /// ⚠ 버튼의 onClick 에 리스너를 끼우지 않는다
    ///   대상이 Button 이 아닐 수도 있고, 팝업 코드가 OnBeforeOpen 에서
    ///   RemoveAllListeners() 를 부르는 곳이 여럿이라 조용히 지워진다.
    ///   대신 "눌린 결과" 를 본다 — 타겟이 사라졌거나(팝업이 닫혔거나)
    ///   시나리오가 준 조건이 찼거나.
    /// </summary>
    IEnumerator WaitForTargetClick(TutorialStep step, RectTransform target)
    {
        var btn = target != null ? target.GetComponentInChildren<UnityEngine.UI.Button>() : null;
        bool clicked = false;
        if (btn != null) btn.onClick.AddListener(Mark);

        float end = Time.realtimeSinceStartup + 60f;
        while (!clicked)
        {
            if (step.WaitUntil != null && step.WaitUntil()) break;
            if (target == null || !target.gameObject.activeInHierarchy) break;
            if (Time.realtimeSinceStartup > end)
            {
                Debug.LogWarning("[Tutorial] 타겟 클릭 대기 시간 초과 — 다음 스텝으로 넘어갑니다.");
                break;
            }
            yield return null;
        }

        if (btn != null) btn.onClick.RemoveListener(Mark);
        void Mark() => clicked = true;
    }

    static IEnumerator WaitCondition(Func<bool> condition)
    {
        if (condition == null) yield break;
        float end = Time.realtimeSinceStartup + 60f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > end)
            {
                Debug.LogWarning("[Tutorial] 조건 대기 시간 초과 — 다음 스텝으로 넘어갑니다.");
                yield break;
            }
            yield return null;
        }
    }

    // ── 내부 — 팔레트 ────────────────────────────────────────

    void EnsureOverlay()
    {
        if (_overlay != null) return;
        _overlay = TutorialOverlay.Create(transform, Skip);
    }

    static TutorialData Data => UserDataManager.Instance?.Get<TutorialData>();
}
