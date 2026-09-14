using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  FirstRelicTutorial.cs
//  첫 환생 직후 메인 화면 — 유물 전승도를 한 번 열어 보게 만든다.
//
//  ■ 왜 여기인가
//    앱 첫 실행은 메인 화면을 건너뛰고 곧장 전투로 들어간다. 유물 화면을
//    처음 만나는 순간은 '첫 환생 뒤 메인 화면으로 돌아온 지금' 뿐이다.
//    그리고 이때가 처음으로 쓸 포인트를 손에 쥔 시점이다 —
//    설명과 사용이 같은 자리에서 붙어야 기억에 남는다.
//
//  ■ 유물 버튼은 직접 누르게 한다 (ClickTarget)
//    이 화면에서 유물을 안 열면 다음 여정은 지난번과 똑같은 세기로 시작한다.
//    "저기 있습니다" 로 끝내면 대부분 그냥 출전을 누른다.
//
//  ■ 트리는 카드 그리드와 설명할 것이 다르다 (2026-08-25 재구성)
//    구 화면은 유물 카드가 전부 펼쳐져 있어 "버튼으로 올린다" 한 줄이면 끝이었다.
//    트리는 처음 열면 <b>노드 하나와 물음표 넷</b> 뿐이라, 세 가지를 더 말해야 한다:
//      ① 가운데가 시작점이다   ② ▲ 아래 숫자가 값이다   ③ ? 는 앞을 찍어야 열린다
//    이 셋을 빼면 "화면이 비어 있는데 뭘 하라는 거지" 로 끝난다.
//
//  ⚠ 무엇을 찍을지는 강요하지 않는다
//    시작 노드조차 클릭을 강제하지 않는다 — 첫 환생 포인트가 1pt 뿐일 수 있어
//    "누르세요" 로 묶어 두면 포인트가 모자랄 때 튜토리얼이 갇힌다.
//    자리를 가리키는 데까지만 하고 선택은 남겨 둔다.
//
//  ⚠ 전투 시작(출전) 버튼은 가리키지 않는다
//    LobbyTutorial 과 같은 이유다 — 되돌릴 수 없는 행동은 등을 떠밀지 않는다.
//
//  트리거: TutorialManager.HandleMainPanelShown (환생 1회 이상 + 아직 안 봤을 때)
// ============================================================

public class FirstRelicTutorial : TutorialScenario
{
    public override TutorialId Id => TutorialId.FirstRelic;

    protected override void Build(List<Func<IEnumerator>> steps)
    {
        steps.Add(WaitForPanel);
        steps.Add(Welcome);
        steps.Add(OpenRelic);
        steps.Add(PointPoints);
        steps.Add(PointRoot);
        steps.Add(PointBuyBtn);
        steps.Add(PointFog);
        steps.Add(Closing);
    }

    /// <summary>
    /// 메인 화면이 실제로 떠 있을 때까지 기다린다.
    ///
    /// ⚠ 환생 처리가 끝나는 것만으로는 부족하다
    ///   팝업이 닫히고 로비가 메인 패널을 다시 세우는 사이에 말을 걸면
    ///   아직 없는 유물 버튼을 가리키게 된다.
    /// </summary>
    IEnumerator WaitForPanel()
    {
        yield return WaitUntilFree(() => RelicBtn() != null, timeout: 30f);
        yield return WaitSeconds(0.4f, dim: false);
    }

    IEnumerator Welcome()
    {
        yield return Show(TutorialStep.Say(
            "여정이 끝나고 <b>환생</b>했습니다.\n" +
            "장수·장비·특성은 사라졌지만, 그 대가로 <b>환생 포인트</b>를 받았습니다.\n" +
            "이 포인트로 다음 여정의 출발선을 끌어올립니다."));
    }

    IEnumerator OpenRelic()
    {
        yield return Show(TutorialStep.Point(
                RelicBtn,
                "<b>유물</b>을 열어 보겠습니다.\n" +
                "여기서 쓰지 않으면 다음 여정도 방금과 같은 세기로 시작합니다.",
                TutorialAnchor.Above)
            .ClickTarget()
            .Until(() => PopupManager.Instance != null &&
                         PopupManager.Instance.IsOpen(PopupType.Relic)));

        yield return WaitForPopup(PopupType.Relic, timeout: 10f);
        yield return WaitSeconds(0.3f, dim: false);
    }

    IEnumerator PointPoints()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.Relic, "PointGroup"),
            "지금 가진 <b>환생 포인트</b>입니다.\n" +
            "더 멀리 간 여정일수록, 더 높은 난이도일수록 많이 받습니다.",
            TutorialAnchor.Below));
    }

    IEnumerator PointRoot()
    {
        yield return Show(TutorialStep.Point(
            RootNode,
            "유물은 <b>한 그루의 나무</b>로 이어져 있습니다.\n" +
            "가운데 <b>근원의 각인</b>이 시작점이고, 여기서 네 갈래가 뻗습니다 —\n" +
            "위 공격력 · 아래 체력 · 왼쪽 병사 수 · 오른쪽 경험치.",
            TutorialAnchor.Auto));
    }

    IEnumerator PointBuyBtn()
    {
        yield return Show(TutorialStep.Point(
            BuyBtn,
            "노드 아래 <b>▲ 버튼</b>으로 레벨을 올립니다. 옆 숫자가 드는 포인트입니다.\n" +
            "환생해도 사라지지 않으니, 여기 찍은 만큼이 그대로 영구 성장입니다.",
            TutorialAnchor.Below));
    }

    IEnumerator PointFog()
    {
        yield return Show(TutorialStep.Point(
            FogNode,
            "<b>?</b> 는 아직 잠긴 노드입니다.\n" +
            "바로 앞 노드를 찍어야 열리고, 그제서야 무엇인지 보입니다.\n" +
            "화면은 <b>끌어서</b> 움직이고 <b>휠·두 손가락</b>으로 확대할 수 있습니다.",
            TutorialAnchor.Auto));
    }

    IEnumerator Closing()
    {
        yield return Show(TutorialStep.Say(
            "포인트를 다 쓰고 나면 창을 닫고 다시 출전하세요.\n" +
            "이번에는 지난 여정보다 앞에서 출발합니다."));
    }

    // ── 타겟 ─────────────────────────────────────────────────
    //  RelicBtn : MainPanelCreator 가 만드는 버튼 이름 (BuildWideBtn "RelicBtn")
    //  나머지   : RelicTreePopup 이 런타임에 만드는 오브젝트 이름이다
    //             노드 = "Node_{RelicNodeId}", 실루엣 = "Ghost_{RelicNodeId}"
    //
    //  ⚠ 트리 안쪽은 ByName 으로 찾는다 (InPopup 이 아니라)
    //    노드·간선·실루엣 원본 세 개가 팝업 안에 꺼진 채로 들어 있어
    //    InPopup 의 깊이 탐색이 그 원본을 먼저 집을 수 있다.
    //    ByName 은 꺼진 오브젝트를 제외하므로 실제로 깔린 노드가 잡힌다.
    //
    //  ⚠ BuyBtn 은 "지금 보이는 첫 노드" 의 것이 잡힌다
    //    처음 열면 보이는 노드가 근원의 각인 하나뿐이라 의도한 대상이 맞다.
    //    이미 트리를 찍어 둔 상태로 이 튜토리얼이 다시 돌 일은 없다
    //    (첫 환생 직후 1회 — TutorialData 가 기록한다).
    static readonly Func<RectTransform> RelicBtn = ByName("RelicBtn");
    static readonly Func<RectTransform> RootNode = ByName($"Node_{RelicNodeId.N_Origin}");
    static readonly Func<RectTransform> BuyBtn   = ByName("BuyBtn");
    static readonly Func<RectTransform> FogNode  = ByName($"Ghost_{RelicNodeId.N_Claw}");
}
