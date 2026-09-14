using System.Collections;
using UnityEngine;

// ============================================================
//  LobbyManager.cs
//  로비(소환사 선택) ↔ 소환 런 전환을 소유하는 Singleton.
//
//  Inspector 설정:
//    StageConfig : StageConfig SO (GameplayConfig.MaxStage 가 읽는다)
// ============================================================

/// <summary>
/// 로비 ↔ 런 흐름의 상태. 진입 판단은 전부 이 값 하나로 한다.
///
///   Boot ─(첫 패널 결정)→ Idle ─(게임 시작)→ SummonRun ─(런 종료)→ Returning → Idle
/// </summary>
public enum LobbyFlow
{
    Boot      = 0,   // 첫 패널이 아직 안 정해졌다 — 아무것도 준비하지 않는다
    Idle      = 1,   // 로비(소환사 선택)에 있다

    // ⚠ 웨이브를 여기서 시작하지 않는다
    //   이 게임은 스테이지 대기(사전 소환)에서 플레이어가 몬스터를 세운 뒤
    //   직접 시작을 누른다. 진행 주도권은 RunBootstrap / StageLoopDirector 에 있다.
    //   여기서 하는 일은 화면을 인게임으로 돌리는 것뿐이다.
    SummonRun = 2,   // 소환 런 진행 중 (RunBootstrap 이 주도)
    Returning = 3,   // 런 종료 후 전장 청소 중
}

public class LobbyManager : Singleton<LobbyManager>
{
    [Header("스테이지 설정")]
    [SerializeField] StageConfig _stageConfig;

    LobbyFlow _flow = LobbyFlow.Boot;

    /// <summary>지금 흐름 상태. 모든 진입 판단은 이 값 하나로 한다.</summary>
    public LobbyFlow Flow => _flow;

    /// <summary>
    /// 지금 <b>포기할 런이 있는가</b> — 일시정지 창의 '즉시 환생하기' 와 도감 편집 잠금이 본다.
    /// ⚠ 판정을 화면마다 적지 말 것 — 상태를 늘리면 여기만 고친다.
    /// </summary>
    public bool IsInRun => _flow == LobbyFlow.SummonRun;

    // ── Unity 생명주기 ────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        if (_stageConfig == null)
        {
            Debug.LogError("[LobbyManager] StageConfig 가 할당되지 않았습니다.");
            return;
        }
        StageConfig.Current = _stageConfig;
    }

    /// <summary>
    /// 씬의 다른 Start 가 다 돈 뒤에 화면을 고른다 (한 프레임 뒤).
    /// </summary>
    IEnumerator Start()
    {
        yield return null;
        SelectInitialPanel();

        // 난이도 해금 연출은 원래 런 끝 보상 상자 뒤에 뜬다. 그걸 보기 전에
        // 앱이 꺼졌다면 여기서 보여 준다 (⚠ 런에서 돌아올 때는 부르지 않는다 —
        // 그때는 보상 상자가 아직 떠 있고, 상자를 닫으면 RunBootstrap 이 연다).
        if (_flow == LobbyFlow.Idle) DifficultyUnlockPopup.ShowPending();
    }

    /// <summary>
    /// 진행 중인 소환 런이 있으면 곧장 그 스테이지로, 없으면 소환사 선택 화면으로.
    ///
    /// ⚠ 이어하기를 맨 앞에서 가른다
    ///   카드·마나·특성이 전부 살아 있는데 선택 화면부터 보여 주면
    ///   "이 판을 이어서 하려면 무엇을 눌러야 하나" 가 어디에도 없다.
    ///   이 기록은 환생으로만 지워진다 (UserDataManager.Reincarnate).
    /// </summary>
    void SelectInitialPanel()
    {
        var summonRun = UserDataManager.Instance?.Get<SummonRunData>();
        if (summonRun != null && summonRun.CanResume)
        {
            Debug.Log($"[LobbyManager] 진행 중인 런을 이어서 시작합니다 — 스테이지 {summonRun.StageNumber}");
            RunLaunch.RequestResume();
            EnterSummonRun();
            return;
        }

        SetFlow(LobbyFlow.Idle);

        // 로비 화면은 MainPanel(소환사 선택) 하나뿐이다 — 원작 탭(NavBar)은 걷어냈다.
        FindAnyObjectByType<MainPanelUI>(FindObjectsInactive.Include).gameObject.SetActive(true);
    }

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// 소환사 선택을 마쳤다 — 인게임 씬을 세우고 화면을 넘긴다.
    ///
    /// ⚠ 곧바로 Present(Battle) 로 넘기면 아무 일도 일어나지 않는다
    ///   스플래시는 InGame 씬을 allowSceneActivation = false 로 받아만 둔다(ScenePreloader).
    ///   씬을 실제로 올리는 곳은 SceneDirector.EnsureInGameResident 하나뿐이다.
    ///
    /// ⚠ 전투를 시작하지는 않는다 — 스테이지 대기를 세우는 것은 RunBootstrap 이다.
    /// </summary>
    public void EnterSummonRun() => StartCoroutine(EnterSummonRunRoutine());

    IEnumerator EnterSummonRunRoutine()
    {
        // ⚠ 여기서 로딩 팝업을 띄우지 않는다
        //   로딩 팝업 프리팹은 InGame 씬의 PopupManager 가 들고 있어 상주 전에는 못 연다.
        //   전환은 상주가 끝난 뒤 SetFlow 한 번으로 일어나므로 빈 화면이 드러나는 구간이 없다.
        yield return SceneDirector.Ensure().EnsureInGameResident();

        // 전장을 '실전' 으로 연다 — 런이 끝나고 돌아올 때 Clean() 이 이걸 보고 치운다.
        BattleArena.Ensure().Open(ArenaKind.Real);

        SetFlow(LobbyFlow.SummonRun);
    }

    /// <summary>
    /// 런이 끝났다 — 전장을 치우고 로비(소환사 선택)로 돌아간다.
    ///
    /// ⚠ 바로 Idle 로 가면 시체가 남는다 — Returning 이 청소를 소유한 유일한 상태다.
    /// </summary>
    public void ReturnFromSummonRun() => SetFlow(LobbyFlow.Returning);

    // ── 흐름 ─────────────────────────────────────────────────

    void SetFlow(LobbyFlow next)
    {
        if (_flow == next) return;
        Debug.Log($"[LobbyFlow] {_flow} → {next}");
        _flow = next;
        OnEnterFlow(next);
    }

    void OnEnterFlow(LobbyFlow flow)
    {
        var director = SceneDirector.Instance;

        // 배경음이 바뀌는 지점은 "로비 ↔ 런" 하나뿐이다 — 화면마다 PlayBgm 을
        // 뿌리면 로비 안에서 곡이 계속 처음부터 다시 돈다.
        AudioManager.Instance?.PlayBgm(flow == LobbyFlow.SummonRun ? BgmKey.InGame : BgmKey.Lobby);

        switch (flow)
        {
            case LobbyFlow.Idle:
                director?.Present(PresentMode.LobbyOnly);
                break;

            case LobbyFlow.SummonRun:
                director?.Present(PresentMode.Battle);
                break;

            case LobbyFlow.Returning:
                director?.Present(PresentMode.ArenaBehindUI);
                Clean();
                break;
        }
    }

    /// <summary>
    /// Returning — 전장 청소. 유닛·발사체·이펙트를 지우는 곳은 여기 하나뿐이다.
    ///
    /// ⚠ 코루틴이 아니라 동기다 — 디스폰은 그 자리에서 끝난다. 프레임을 넘기면
    ///   그 사이 들어온 요청이 Returning 에 막혀 사라진다.
    /// </summary>
    void Clean()
    {
        BattleArena.Ensure().Close();
        SelectInitialPanel();
    }
}
