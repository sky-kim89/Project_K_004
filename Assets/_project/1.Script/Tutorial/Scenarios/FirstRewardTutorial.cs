using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  FirstRewardTutorial.cs
//  1스테이지 클리어 직후 — 카드 3택과 갈림길을 한 번 짚어 준다.
//
//  ■ 왜 따로인가
//    인게임 안내는 "대기 → 전투" 화면이고, 이건 판이 끝난 뒤의 보상 화면이다.
//    화면 단위로 시나리오를 끊는다 (TutorialId 머리 주석).
//
//  ■ 무엇을 고를지는 강요하지 않는다
//    카드 선택은 런의 방향을 정한다 — 튜토리얼이 대신 고르면 첫 런의 덱이
//    전부 같아진다. 읽는 스텝만 두고 고르는 것은 플레이어에게 맡긴다.
//
//  ■ 갈림길은 팝업이 아니라 HUD 화면이다 (CrossroadUI)
//    카드를 고른 뒤 뜬다. 1스테이지 다음(2)은 보스·첫 엘리트판이 아니라 늘 열린다.
//    그래도 안 뜨는 경우(진화 창이 끼었다 등)를 위해 시간 초과로 넘어간다.
//
//  트리거: TutorialManager.HandleVictory (1스테이지 클리어 + 아직 안 봤을 때)
// ============================================================

public class FirstRewardTutorial : TutorialScenario
{
    public override TutorialId Id => TutorialId.FirstReward;

    /// <summary>카드 3택 팝업 위에서 시작한다 — 이 팝업이 무대다.</summary>
    public override PopupType StagePopup => PopupType.CardSelect;

    /// <summary>
    /// ⚠ 이어 보지 않는다 — 중간 스텝(갈림길 대기)은 카드 3택을 이미 지났다는 전제다.
    /// 다시 뜨는 것은 1스테이지를 또 깼을 때뿐이고, 그때는 처음부터가 맞다.
    /// </summary>
    public override bool Resumable => false;

    protected override void Build(List<Func<IEnumerator>> steps)
    {
        steps.Add(WaitForCards);
        steps.Add(ExplainCards);
        steps.Add(PointStats);
        steps.Add(LetPick);
        steps.Add(WaitForCrossroad);
        steps.Add(ExplainCrossroad);
    }

    IEnumerator WaitForCards()
    {
        yield return WaitForPopup(PopupType.CardSelect, timeout: 10f);
        yield return WaitSeconds(0.5f, dim: false);
    }

    IEnumerator ExplainCards()
    {
        yield return Show(TutorialStep.Say(
            "판을 이겼습니다. 보상으로 <b>카드 한 장</b>을 고릅니다.\n" +
            "새 카드는 덱의 빈 칸에 들어가고,\n" +
            "이미 가진 카드를 고르면 그 카드의 <b>레벨</b>이 오릅니다."));

        yield return Show(TutorialStep.Say(
            "카드가 <b>최대 레벨</b>에 닿은 뒤 다시 고르면\n" +
            "더 강한 종족으로 <b>진화</b>하거나, 다른 카드를 재료로 <b>융합</b>할 수 있습니다."));
    }

    IEnumerator PointStats()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.CardSelect, "StatsBtn"),
            "<b>전투 통계</b>에서 이번 판에 어떤 카드가 얼마나 싸웠는지 봅니다.\n" +
            "무엇을 키울지 고민될 때 참고하세요.",
            TutorialAnchor.Below));
    }

    IEnumerator LetPick()
    {
        yield return Show(TutorialStep.Say(
            "마음에 드는 카드를 하나 고르세요."));

        // ⚠ 입력을 풀고 기다린다 — 막은 채로 기다리면 고를 수가 없다.
        yield return WaitForPopupClosed(PopupType.CardSelect, timeout: 600f);
    }

    IEnumerator WaitForCrossroad()
    {
        yield return WaitUntilFree(() => CrossroadContent() != null, timeout: 15f);
        yield return WaitSeconds(0.4f, dim: false);
    }

    IEnumerator ExplainCrossroad()
    {
        // 갈림길이 안 떴으면(시간 초과) 설명할 화면이 없다 — 조용히 끝낸다.
        if (CrossroadContent() == null) yield break;

        yield return Show(TutorialStep.Point(
            CrossroadContent,
            "<b>갈림길</b>입니다. 두 길 중 하나가 곧 다음 판이 됩니다.\n" +
            "전투를 고르면 보상을, <b>야영지·상점·강화소</b> 같은 시설을 고르면\n" +
            "그 판은 싸우지 않고 정비합니다. 여기서부터는 자유롭게 나아가세요.",
            TutorialAnchor.Center));
    }

    /// <summary>갈림길 화면의 내용 칸 — 떠 있을 때만 잡힌다.</summary>
    static RectTransform CrossroadContent()
    {
        var ui = CrossroadUI.Instance;
        if (ui == null) return null;
        var content = ui.transform.Find("Content");
        return content != null && content.gameObject.activeInHierarchy ? content as RectTransform : null;
    }
}
