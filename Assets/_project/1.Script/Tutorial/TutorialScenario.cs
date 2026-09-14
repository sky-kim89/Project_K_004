using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  TutorialScenario.cs
//  튜토리얼 하나가 어떻게 흘러가는지를 담는 베이스.
//
//  ■ 스텝 하나 = 함수 하나
//    Build() 에서 함수를 순서대로 등록하고, Next() 가 하나씩 실행한다.
//    각 스텝은 코루틴이라 "팝업이 뜰 때까지 기다렸다가" 같은 시퀀스를
//    그 안에서 그대로 적을 수 있다. 튜토리얼은 원래 하드코딩의 영역이라
//    데이터로 빼려 하지 않고 코드로 읽히게 두는 쪽이 고치기 쉽다.
//
//  ■ 화면은 직접 그리지 않는다
//    Show(TutorialStep) 을 부르면 TutorialManager 가 오버레이에 넘긴다.
//    시나리오는 "무엇을 언제" 만 정하고 "어떻게 보이는지" 는 모른다.
//
//  사용:
//    public class LobbyTutorial : TutorialScenario
//    {
//        public override TutorialId Id => TutorialId.Lobby;
//
//        protected override void Build(List<Func<IEnumerator>> steps)
//        {
//            steps.Add(Intro);
//            steps.Add(PointAtHud);
//        }
//
//        IEnumerator Intro()
//        {
//            yield return Show(TutorialStep.Say("전투는 자동으로 진행됩니다."));
//        }
//
//        IEnumerator PointAtHud()
//        {
//            yield return WaitForPopup(PopupType.BattleResult);
//            yield return Show(TutorialStep.Point(() => Find("RewardRoot"), "보상입니다."));
//        }
//    }
// ============================================================

public abstract class TutorialScenario
{
    /// <summary>이 시나리오의 식별자. 저장 기록의 키다.</summary>
    public abstract TutorialId Id { get; }

    /// <summary>
    /// 중단(Abort)됐을 때 완료로 칠 것인가.
    /// 기본 false — 앱이 꺼지거나 씬이 갈려 끊긴 것을 "봤다" 로 치면 안 된다.
    /// 건너뛰기(Skip)는 이 값과 무관하게 항상 완료 처리한다.
    /// </summary>
    public virtual bool CompleteOnAbort => false;

    /// <summary>
    /// 이 시나리오가 자기 무대로 삼는 팝업. 기본 None = 팝업이 하나도 없을 때만 시작한다.
    ///
    /// ⚠ 강제 진행 튜토리얼은 남의 팝업 위에서 시작하면 안 된다
    ///   튜토리얼 오버레이는 sortingOrder 1000 이라 팝업(200)을 통째로 덮고
    ///   입력까지 막는다. 로비·인게임이 동시에 상주하는 구조라 "로비가 떴다" 는
    ///   조건은 어빌리티 선택 팝업이 떠 있는 동안에도 참이 된다 —
    ///   실제로 그 팝업 위에서 로비 튜토리얼이 시작돼 선택을 못 하게 막았다.
    ///   TutorialManager 가 이 값을 보고 화면이 조용해질 때까지 재생을 미룬다.
    /// </summary>
    public virtual PopupType StagePopup => PopupType.None;

    /// <summary>말풍선 오른쪽 아래에 띄울 안내. 스텝 종류에 따라 갈린다.</summary>
    public virtual string HintFor(TutorialStep step) => step.Advance switch
    {
        TutorialAdvance.AnyClick    => "화면을 누르면 계속",
        TutorialAdvance.ClickTarget => "표시된 곳을 누르세요",
        _                           => "",
    };

    // ── 내부 상태 ────────────────────────────────────────────

    readonly List<Func<IEnumerator>> _steps = new();
    bool _built;

    /// <summary>지금 몇 번째 스텝인가 (0-base). 저장·재개에 쓴다.</summary>
    public int CurrentStep { get; private set; }

    /// <summary>전체 스텝 수.</summary>
    public int StepCount { get { EnsureBuilt(); return _steps.Count; } }

    /// <summary>모든 스텝을 소진했는가.</summary>
    public bool IsFinished { get { EnsureBuilt(); return CurrentStep >= _steps.Count; } }

    // TutorialManager 가 꽂아 준다 — 시나리오는 매니저를 직접 찾지 않는다.
    internal TutorialManager Manager;

    // ── 구성 ─────────────────────────────────────────────────

    /// <summary>스텝 함수를 순서대로 등록한다.</summary>
    protected abstract void Build(List<Func<IEnumerator>> steps);

    void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        Build(_steps);
    }

    /// <summary>
    /// 재생 시작 지점을 정한다. 이어 보기(앱 재시작)면 저장된 인덱스부터.
    /// 범위를 벗어난 값은 잘라 낸다 — 시나리오를 고쳐 스텝 수가 줄었을 수 있다.
    /// </summary>
    public void Rewind(int startStep)
    {
        EnsureBuilt();
        CurrentStep = Mathf.Clamp(startStep, 0, _steps.Count);
    }

    // ── 진행 ─────────────────────────────────────────────────

    /// <summary>
    /// 다음 스텝 하나를 실행한다. 더 없으면 아무것도 하지 않고 끝난다.
    /// 실행이 끝나면 CurrentStep 이 1 올라간다 — 매니저가 그 값을 저장한다.
    /// </summary>
    public IEnumerator Next()
    {
        EnsureBuilt();
        if (CurrentStep >= _steps.Count) yield break;

        var step = _steps[CurrentStep];
        yield return step();
        CurrentStep++;
    }

    // ── 스텝 안에서 쓰는 헬퍼 ────────────────────────────────
    //  전부 실시간(unscaled) 기준이다. 튜토리얼은 일시정지·배속과
    //  무관하게 같은 속도로 흘러야 한다 — 특히 Show 가 게임을 멈추므로
    //  여기서 Time.deltaTime 을 쓰면 대기가 영원히 안 끝난다.
    //
    //  ⚠ 아래 대기 헬퍼는 전부 게임을 다시 굴린다
    //    쿨타임·웨이브처럼 시간이 흘러야 차는 조건을 기다리는 자리이므로
    //    멈춘 채로 기다리면 조건이 영영 안 찬다. 멈추는 것은 Show 뿐이다.

    /// <summary>
    /// 스텝 하나를 화면에 띄우고, 넘어갈 조건이 찰 때까지 기다린다.
    /// 읽는 스텝(Say·Point)은 게임을 멈춘 채 띄운다 — TutorialStep.PauseGame 참고.
    /// </summary>
    protected IEnumerator Show(TutorialStep step) => Manager.RunStep(this, step);

    /// <summary>
    /// 입력을 막은 채 초를 센다 (연출 대기).
    /// dim=false 면 어둡게 하지 않는다 — 기다리는 동안 화면을 봐야 할 때.
    /// </summary>
    protected IEnumerator WaitSeconds(float seconds, bool dim = true)
    {
        Manager.BlockOnly(dim);
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    /// <summary>
    /// 조건이 찰 때까지 입력을 막고 기다린다. timeout 초를 넘기면 포기하고 넘어간다.
    /// dim=false 면 어둡게 하지 않는다.
    /// </summary>
    protected IEnumerator WaitUntil(Func<bool> condition, float timeout = 30f, bool dim = true)
    {
        Manager.BlockOnly(dim);
        yield return WaitUntilRaw(condition, timeout);
    }

    /// <summary>
    /// 화면을 덮지 않고 조건만 기다린다.
    /// 플레이어가 직접 눌러야 진행되는 구간에서 쓴다 (입력을 막으면 영원히 안 찬다).
    /// </summary>
    protected IEnumerator WaitUntilFree(Func<bool> condition, float timeout = 30f)
    {
        Manager.Unblock();
        yield return WaitUntilRaw(condition, timeout);
    }

    static IEnumerator WaitUntilRaw(Func<bool> condition, float timeout)
    {
        float end = Time.realtimeSinceStartup + Mathf.Max(0.1f, timeout);
        while (!condition())
        {
            if (Time.realtimeSinceStartup > end)
            {
                // ⚠ 조용히 멈추면 안 된다 — 튜토리얼이 걸리면 게임이 통째로 멈춘 것처럼 보인다.
                //   시간 초과는 시나리오 전제가 깨졌다는 뜻이므로 넘어가고 로그를 남긴다.
                Debug.LogWarning("[Tutorial] 조건 대기 시간 초과 — 다음 스텝으로 넘어갑니다.");
                yield break;
            }
            yield return null;
        }
    }

    /// <summary>팝업이 열릴 때까지 기다린다.</summary>
    protected IEnumerator WaitForPopup(PopupType type, float timeout = 30f)
        => WaitUntilFree(() => PopupManager.Instance != null && PopupManager.Instance.IsOpen(type), timeout);

    /// <summary>팝업이 닫힐 때까지 기다린다.</summary>
    protected IEnumerator WaitForPopupClosed(PopupType type, float timeout = 60f)
        => WaitUntilFree(() => PopupManager.Instance == null || !PopupManager.Instance.IsOpen(type), timeout);

    // ── 타겟 찾기 ────────────────────────────────────────────
    //
    //  ⚠ 스텝을 만들 때가 아니라 재생될 때 찾아야 한다
    //    Build() 는 시나리오 시작 시 한 번에 돈다. 그때는 가리킬 팝업이
    //    아직 없다. 그래서 TutorialStep.Target 은 Func 이고, 아래 헬퍼도
    //    "지금 찾는" 함수를 돌려준다.

    /// <summary>열려 있는 팝업 안에서 이름으로 찾는다.</summary>
    protected static Func<RectTransform> InPopup(PopupType type, string path)
        => () =>
        {
            var popup = PopupManager.Instance?.Get<PopupBase>(type);
            if (popup == null) return null;
            var t = FindDeep(popup.transform, path);
            return t as RectTransform;
        };

    /// <summary>씬 어디든 이름으로 찾는다 (비활성 오브젝트 포함).</summary>
    protected static Func<RectTransform> ByName(string name)
        => () =>
        {
            foreach (var rt in UnityEngine.Object.FindObjectsByType<RectTransform>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (rt.name == name) return rt;
            return null;
        };

    /// <summary>이미 참조를 들고 있을 때 (가장 안전하다 — 이름 오타가 안 난다).</summary>
    protected static Func<RectTransform> Of(Component c)
        => () => c != null ? c.transform as RectTransform : null;

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
