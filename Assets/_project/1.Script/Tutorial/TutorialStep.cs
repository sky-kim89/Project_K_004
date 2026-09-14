using System;
using UnityEngine;

// ============================================================
//  TutorialStep.cs
//  튜토리얼의 한 단계 — "무엇을 가리키고, 뭐라고 말하고, 언제 넘어가나".
//
//  ■ 이 클래스는 데이터일 뿐 화면을 그리지 않는다
//    그리는 쪽은 TutorialOverlay 하나다. 시나리오는 이 데이터만 만들어
//    넘기고, 하이라이트 구멍·말풍선 위치·입력 차단은 전부 오버레이가 한다.
//    그래야 연출을 바꿀 때 시나리오 7개를 다 고치지 않는다.
//
//  ■ 타겟을 Transform 이 아니라 Func 으로 받는 이유
//    스텝은 시나리오를 짤 때 한 번에 다 만들어진다. 그런데 가리킬 버튼은
//    아직 존재하지 않는 경우가 대부분이다 — 팝업이 그때 열리기 때문이다.
//    지연 평가로 두면 "그 스텝이 실제로 재생되는 순간" 찾는다.
//
//  사용:
//    TutorialStep.Say("전투는 자동으로 진행됩니다.")
//    TutorialStep.Point(() => _skillBtn.transform as RectTransform,
//                       "장수 카드를 눌러 스킬을 쓰세요.")
//               .ClickTarget()
// ============================================================

/// <summary>말풍선을 타겟의 어느 쪽에 붙일지.</summary>
public enum TutorialAnchor
{
    Auto   = 0,   // 타겟 위/아래 중 공간이 넓은 쪽 (타겟 없으면 화면 중앙)
    Above  = 1,
    Below  = 2,
    Center = 3,   // 타겟과 무관하게 화면 중앙
}

/// <summary>이 스텝이 언제 끝나는가.</summary>
public enum TutorialAdvance
{
    /// <summary>화면 아무 곳이나 누르면 넘어간다 (설명 전용 스텝).</summary>
    AnyClick   = 0,

    /// <summary>타겟을 실제로 눌러야 넘어간다. 타겟만 입력이 통과된다.</summary>
    ClickTarget = 1,

    /// <summary>조건(WaitUntil)이 참이 될 때까지 기다린다. 입력은 전부 막힌다.</summary>
    Condition  = 2,

    /// <summary>Duration 초가 지나면 저절로 넘어간다.</summary>
    Timed      = 3,
}

public class TutorialStep
{
    /// <summary>가리킬 대상. null 이면 하이라이트 없이 말풍선만 띄운다.</summary>
    public Func<RectTransform> Target;

    /// <summary>말풍선 본문. 비우면 하이라이트만 한다.</summary>
    public string Message = "";

    public TutorialAnchor  Anchor  = TutorialAnchor.Auto;
    public TutorialAdvance Advance = TutorialAdvance.AnyClick;

    /// <summary>Advance == Condition 일 때 이 함수가 true 를 반환하면 넘어간다.</summary>
    public Func<bool> WaitUntil;

    /// <summary>Advance == Timed 일 때의 대기 시간(초). 실시간 기준이다.</summary>
    public float Duration = 1.5f;

    /// <summary>하이라이트 구멍을 타겟보다 이만큼 넓게 판다.</summary>
    public float Padding = 12f;

    /// <summary>
    /// 이 스텝을 보여주는 동안 게임을 멈출 것인가.
    ///
    /// ⚠ 읽는 스텝은 멈춰야 한다
    ///   설명을 읽는 사이에도 전투가 굴러가면 말풍선을 읽다가 스테이지가
    ///   끝나 버린다. 튜토리얼은 원래 "여기서 잠깐" 이라고 말하는 장치다.
    ///
    /// ⚠ 기다리는 스텝은 멈추면 안 된다
    ///   쿨타임·웨이브처럼 게임이 굴러가야 차는 조건을 기다리면서 멈추면
    ///   조건이 영원히 안 찬다. 그래서 Wait/Delay 는 기본이 false 다.
    /// </summary>
    public bool PauseGame = true;

    /// <summary>스텝이 화면에 뜨기 직전에 한 번 호출된다 (상태 준비용).</summary>
    public Action OnEnter;

    /// <summary>스텝이 끝난 직후 한 번 호출된다.</summary>
    public Action OnExit;

    // ── 생성 헬퍼 ────────────────────────────────────────────
    //  시나리오는 한 스텝이 한 줄로 읽혀야 한다. 생성자를 직접 쓰면
    //  필드 이름이 늘어서 "무엇을 가리키는 스텝인지" 가 안 보인다.

    /// <summary>
    /// 설명만 — 아무 데나 누르면 넘어간다.
    ///
    /// ⚠ 이름이 Say 인 이유: 필드 Message 와 겹치면 컴파일이 안 된다 (CS0102)
    ///   C# 은 같은 타입 안에서 필드와 메서드가 이름을 공유하지 못한다.
    ///   Point·Wait·Delay 와 같은 동사 계열로 맞춘다.
    /// </summary>
    public static TutorialStep Say(string message, TutorialAnchor anchor = TutorialAnchor.Center)
        => new() { Message = message, Anchor = anchor, Advance = TutorialAdvance.AnyClick };

    /// <summary>타겟을 가리키며 설명 — 아무 데나 누르면 넘어간다.</summary>
    public static TutorialStep Point(Func<RectTransform> target, string message,
                                     TutorialAnchor anchor = TutorialAnchor.Auto)
        => new() { Target = target, Message = message, Anchor = anchor,
                   Advance = TutorialAdvance.AnyClick };

    /// <summary>
    /// 조건이 찰 때까지 기다린다. 설명은 선택.
    /// 게임을 멈추지 않는다 — 멈추면 게임이 굴러가야 차는 조건이 영원히 안 찬다.
    /// </summary>
    public static TutorialStep Wait(Func<bool> until, string message = "",
                                    Func<RectTransform> target = null)
        => new() { Target = target, Message = message, WaitUntil = until,
                   Advance = TutorialAdvance.Condition, PauseGame = false };

    /// <summary>초를 세고 넘어간다 — 연출이 끝나기를 기다릴 때. 게임은 계속 돈다.</summary>
    public static TutorialStep Delay(float seconds, string message = "")
        => new() { Message = message, Duration = seconds,
                   Advance = TutorialAdvance.Timed, PauseGame = false };

    // ── 체이닝 ───────────────────────────────────────────────

    /// <summary>타겟을 직접 누르게 만든다 (타겟만 입력 통과).</summary>
    public TutorialStep ClickTarget()
    {
        Advance = TutorialAdvance.ClickTarget;
        return this;
    }

    /// <summary>이 스텝 동안 게임을 멈추지 않는다 (연출을 보여줘야 할 때).</summary>
    public TutorialStep KeepRunning()
    {
        PauseGame = false;
        return this;
    }

    /// <summary>이 스텝 동안 게임을 멈춘다.</summary>
    public TutorialStep Freeze()
    {
        PauseGame = true;
        return this;
    }

    /// <summary>
    /// 넘어갈 조건을 직접 지정한다.
    ///
    /// ⚠ ClickTarget 과 함께 쓰는 것이 핵심 용도다
    ///   타겟이 Button 이 아니라 컨테이너면(배치 칸 묶음 등) 눌린 것을
    ///   감지할 방법이 없어 시간 초과까지 멈춰 있는다. "무엇이 벌어지면
    ///   눌린 것인가"(팝업이 열렸다 등)를 여기 적어 두면 그걸로 넘어간다.
    /// </summary>
    public TutorialStep Until(Func<bool> condition)
    {
        WaitUntil = condition;
        return this;
    }

    public TutorialStep At(TutorialAnchor anchor) { Anchor  = anchor;  return this; }
    public TutorialStep Pad(float padding)        { Padding = padding; return this; }
    public TutorialStep Enter(Action onEnter)     { OnEnter = onEnter; return this; }
    public TutorialStep Exit(Action onExit)       { OnExit  = onExit;  return this; }

    // ── 조회 ─────────────────────────────────────────────────

    /// <summary>지금 이 순간의 타겟. 아직 없거나 꺼져 있으면 null.</summary>
    public RectTransform ResolveTarget()
    {
        if (Target == null) return null;
        var rt = Target();
        return rt != null && rt.gameObject.activeInHierarchy ? rt : null;
    }
}
