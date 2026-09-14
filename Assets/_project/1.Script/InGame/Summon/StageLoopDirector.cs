using System;
using UnityEngine;

// ============================================================
//  StageLoopDirector.cs
//  스테이지 루프의 축. "지금 무엇을 할 수 있는 시점인가" 를 여기가 정한다.
//
//  ■ 루프
//    스테이지 대기 ──▶ 시작 ──▶ 전투 ──▶ 승리 ──▶ 보상 ──▶ 스테이지 대기
//      (사전 소환)          (적 등장)  (소환 계속)     (어빌리티/이벤트)
//
//  ■ StageReady 가 왜 필요한가
//    실시간 소환만 있으면 첫 접전에서 손이 모자라 아무것도 못 해 보고 밀린다.
//    적이 오기 전에 판을 짤 시간을 줘야 "무엇을 얼마나 세울까" 가 선택이 된다.
//    이 단계에서 쓴 마나도 똑같이 소모된다 — 공짜 배치가 아니라 선불이다.
//
//  ■ 소환은 두 시점 모두 열려 있다
//    StageReady(사전) · InWave(전투 중) 둘 다 CanSummon 이 true 다.
//    WaveClear·보상 팝업 중에는 막는다 — 팝업 위에서 잘못 눌러 마나가
//    날아가면 되돌릴 방법이 없다 (UI 규칙 7번).
//
//  ■ 이 클래스는 판단만 한다
//    실제 스폰은 소환 시스템이, 웨이브 진행은 BattleManager 가 한다.
//    여기에 스폰 코드를 넣지 말 것 — 상태 판단이 두 곳으로 갈리는 순간
//    "소환이 되는데 마나는 안 닳는" 류의 버그가 난다.
// ============================================================

public class StageLoopDirector : MonoBehaviour
{
    public static StageLoopDirector Instance { get; private set; }

    // ── 상태 ─────────────────────────────────────────────────

    /// <summary>현재 스테이지 번호 (1부터).</summary>
    public int StageNumber { get; private set; } = 1;

    /// <summary>이번 스테이지가 허들(엘리트)인가. 보스 히어로가 나온다.</summary>
    public bool IsHurdle => RunStageKindRule.IsHurdle(Kind);

    /// <summary>
    /// 이번 판의 성격. 보스는 번호가, 엘리트는 갈림길 선택이 정한다.
    /// 편성(HeroDeployment)이 이 값으로 보스·엘리트를 세운다.
    /// </summary>
    public RunStageKind Kind { get; private set; } = RunStageKind.Normal;

    /// <summary>스테이지 대기 중인가 — 적이 아직 없고 사전 소환을 받는 시점.</summary>
    public bool IsStageReady { get; private set; }

    // ── 자동 시작 ────────────────────────────────────────────
    //
    //  ■ 왜 자동으로 시작하는가 (2026-08-28)
    //    예전에는 [시작]을 누를 때까지 무한정 기다렸다. 그래서 판을 짜는 데
    //    시간 압박이 0 이었고, 실시간 게임인데 **실시간 결정이 없었다** —
    //    느긋하게 최적 배치를 완성한 뒤 버튼을 누르면 끝이었다.
    //
    //    제한 시간을 두면 "지금 이걸 낼까, 아껴서 전투 중에 낼까" 가 생기고
    //    준비 단계 자체가 플레이가 된다.
    //
    //  ⚠ 스테이지당 용사 편성은 그대로 1부대다 (사용자 확정)
    //    시간이 지나도 적이 더 들어오지는 않는다. 자동 시작은 **시작 시점만**
    //    강제할 뿐 물량을 건드리지 않는다 — HeroDeployment 는 손대지 말 것.

    /// <summary>
    /// 준비 시간(초). 다 지나면 저절로 전투가 시작된다.
    ///
    /// ⚠ 기본값은 <b>0 = 자동 시작 없음</b>이다 (사용자 확정, 2026-09-06)
    ///   이 판에서 정할 것이 많다 — 라인마다 무엇을 몇 마리 세울지, 갈림길에서
    ///   어디로 갈지. 시계가 돌면 그 판단이 "빨리 눌러야 하는 일" 이 된다.
    ///   시작은 플레이어가 [시작]을 눌러야만 일어난다.
    ///   ⚠ 0 이 아니면 카운트다운이 되살아난다 — 씬의 값도 함께 볼 것.
    /// </summary>
    [Tooltip("스테이지 대기 시간(초). 0 이면 자동 시작하지 않는다(수동 전용).")]
    [Min(0f)]
    [SerializeField] float _readySeconds = 0f;

    /// <summary>남은 준비 시간. HUD 가 매 프레임 읽어 그린다.</summary>
    public float ReadyRemaining { get; private set; }

    /// <summary>준비 시간 전체 길이 — 게이지 분모.</summary>
    public float ReadyDuration => _readySeconds;

    /// <summary>0~1. 준비 시간이 얼마나 남았는가.</summary>
    public float ReadyFill => _readySeconds > 0f
        ? Mathf.Clamp01(ReadyRemaining / _readySeconds)
        : 0f;

    /// <summary>자동 시작이 걸려 있는가 — 남은 시간을 띄울지 판단한다.</summary>
    public bool HasReadyTimer => IsStageReady && _readySeconds > 0f;

    // ── 이벤트 ───────────────────────────────────────────────

    /// <summary>스테이지 대기에 들어섰다. UI 가 "시작" 버튼을 띄운다.</summary>
    public static event Action<int> OnStageReady;

    /// <summary>플레이어가 시작을 눌렀다. 적이 등장하기 시작한다.</summary>
    public static event Action<int> OnStageStart;

    // ── 생명주기 ─────────────────────────────────────────────

    void Awake()  => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (!HasReadyTimer) return;

        // ⚠ deltaTime 이다 — 배속·일시정지를 그대로 따른다
        //   준비 시간만 실시간으로 흐르면 일시정지가 곧 무한 준비가 된다.
        ReadyRemaining -= Time.deltaTime;

        if (ReadyRemaining > 0f) return;

        ReadyRemaining = 0f;
        StartStage();
    }

    // ── 소환 가능 시점 판단 ──────────────────────────────────

    /// <summary>
    /// 지금 소환할 수 있는가.
    ///
    /// ⚠ 소환 가능 여부의 정본이다. 다른 곳에서 상태를 다시 판단하지 말 것.
    ///
    /// ⚠ HUD 가 먼저 켜지고 판은 나중에 준비된다 — Context 가 없는 창이 있다
    ///   화면 전환(Present(Battle))은 SetFlow 한 번에 끝나지만, BattleContext 를
    ///   만드는 것은 RunBootstrap 의 코루틴이라 몇 프레임 뒤다.
    ///   그 사이 SummonDeckUI.Update 가 매 프레임 여기를 부르므로,
    ///   Context 를 확인하지 않으면 NullReference 가 프레임마다 쏟아진다.
    ///
    ///   원작 흐름(Preparing)은 판을 먼저 준비하고 HUD 를 켰기에 이 창이 없었다.
    ///   이 게임은 순서가 반대라 여기서 막아야 한다.
    /// </summary>
    public bool CanSummon
    {
        get
        {
            // 대기 중 — 사전 소환
            if (IsStageReady) return true;

            var bm = BattleManager.Instance;
            if (bm == null || bm.Context == null) return false;

            // 전투 중 — 실시간 소환
            return bm.Context.State == BattleState.InWave;
        }
    }

    // ── 루프 진행 ────────────────────────────────────────────

    /// <summary>
    /// 스테이지 대기에 들어선다. 적은 아직 세우지 않는다.
    /// 플레이어가 StartStage() 를 부를 때까지 여기 머문다.
    /// </summary>
    public void EnterStageReady(int stageNumber, RunStageKind kind)
    {
        StageNumber    = stageNumber;
        Kind           = kind;
        IsStageReady   = true;
        ReadyRemaining = _readySeconds;

        // 스테이지 제한형 시그니처 스킬이 여기서 차오른다.
        SummonerSkillRule.RefillForStage();

        OnStageReady?.Invoke(stageNumber);
    }

    /// <summary>
    /// 전투를 시작한다 — 용사 편성이 화면 오른쪽 밖에서 걸어 들어온다.
    ///
    /// 부르는 곳은 둘이다: 플레이어가 [시작]을 누르거나, 준비 시간이 다 되거나.
    /// [시작]은 남은 시간을 건너뛰는 것이지 다른 일을 하는 게 아니다.
    /// </summary>
    public void StartStage()
    {
        if (!IsStageReady)
        {
            Debug.LogWarning("[StageLoopDirector] 대기 상태가 아닙니다 — 시작 요청을 무시합니다.");
            return;
        }

        IsStageReady   = false;
        ReadyRemaining = 0f;
        OnStageStart?.Invoke(StageNumber);
    }

    /// <summary>
    /// 이번 스테이지의 용사 편성을 만든다.
    /// 보상 처리가 끝난 뒤 HeroSpawner 에 넘긴다.
    /// </summary>
    public System.Collections.Generic.List<SpawnEntry> BuildHeroDeployment(float stageBias)
        => HeroDeployment.Build(StageNumber, Kind, stageBias);
}
