using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SummonBattleTutorial.cs
//  첫 실행 1스테이지 — 인게임 화면과 한 판의 진행 방법을 통째로 가르친다.
//
//  ■ 흐름 (화면 하나 = 시나리오 하나)
//    ① 대기 구간 — 마왕성 체력 · 마나 · 카드
//    ② 직접 해 보기 — 카드 고르기 → 라인 탭 → 대기열 설명 → [시작]
//    ③ 전투 중 — 시그니처 스킬 직접 쓰기 → 스테이지 · 배속 · 전황 · 일시정지 · 시너지 · 특성 줄
//    ④ 마무리 — 이기면 무엇이 오는가
//
//  ■ 직접 누르게 하는 것 (ClickTarget)
//    카드 → 라인 → 시작 → 시그니처 스킬 → 전장. 플레이어가 손을 대는 자리가
//    이것뿐이라, 읽고 넘기면 [시작] 을 못 찾거나 스킬을 끝까지 안 쓴다.
//
//  ■ 시그니처 스킬은 [시작] 뒤에 가르친다 (사용자 지시, 2026-09-17)
//    대기 중에도 쓸 수는 있지만, 적이 없는 전장에 써 보면 무엇을 했는지 안 보인다.
//
//  ■ 라인·스킬 탭은 월드 입력이다
//    SummonController.ReadTap 은 UI 위를 누르면 무시한다. 가운데 라인 띠
//    (LaneGuide/Band_3, raycastTarget 꺼짐)를 구멍으로 뚫으면 그 자리의 탭이
//    오버레이를 지나 전장에 닿는다. 넘어가는 판정은 결과(대기열·남은 횟수)로 한다.
//
//  ■ 트리거: TutorialManager.HandleStageReady (1스테이지 + 아직 안 봤을 때)
//
//  ⚠ 타겟 이름은 InGameUIPrefabCreator 가 굽는 이름이다
//    (ManaPanel · CardRow · CoreHp/Bar · LaneGuide/Band_N · EnemyInfoButton ·
//     StagePanel · SpeedButton · BattleInfoBtn · PauseButton · SynergyBar · PerkBar)
//    틀리면 하이라이트만 사라지고 말풍선은 가운데에 뜬다 — 진행은 멈추지 않는다.
//  ⚠ 화면 전체로 늘인 뿌리(CoreHp 등)를 가리키면 구멍이 화면 전체라 하이라이트가 안 보인다.
//    그 안의 실제 막대를 가리킬 것.
// ============================================================

public class SummonBattleTutorial : TutorialScenario
{
    public override TutorialId Id => TutorialId.SummonBattle;

    /// <summary>끊기면 처음부터 — 이어하기는 판 대기로 돌아온다 (TutorialScenario.Resumable).</summary>
    public override bool Resumable => false;

    protected override void Build(List<Func<IEnumerator>> steps)
    {
        // ── ① 대기 구간 — 읽기 ─────────────────────────────
        steps.Add(WaitForReady);
        steps.Add(Welcome);
        steps.Add(PointCore);
        steps.Add(PointMana);
        steps.Add(PointCards);

        // ── ② 직접 해 보기 ─────────────────────────────────
        steps.Add(SelectCard);
        steps.Add(TapLane);
        steps.Add(ExplainQueue);
        steps.Add(PressStart);

        // ── ③ 전투 중 ──────────────────────────────────────
        steps.Add(WaitForBattle);
        steps.Add(PressSignature);
        steps.Add(UseSignature);
        steps.Add(PointStage);
        steps.Add(PointSpeed);
        steps.Add(PointBattleInfo);
        steps.Add(PointPause);
        steps.Add(PointSynergy);
        steps.Add(PointPerks);

        // ── ④ 마무리 ───────────────────────────────────────
        steps.Add(Closing);
    }

    // ══════════════════════════════════════════════════════════
    //  ① 대기 구간
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 대기 화면이 다 서고 카드가 깔릴 때까지 기다린다.
    /// ⚠ 소환사·덱은 HUD 보다 늦게 선다 — 카드 칸이 빈 채로 가리키지 않는다.
    /// </summary>
    IEnumerator WaitForReady()
    {
        yield return WaitUntilFree(() => IsStageReady && FirstCard() != null, timeout: 60f);
        yield return WaitSeconds(0.6f, dim: false);
    }

    IEnumerator Welcome()
    {
        yield return Show(TutorialStep.Say(
            "용사들이 <b>마왕성</b>을 노리고 몰려옵니다.\n" +
            "몬스터를 소환해 용사들이 성벽을 넘지 못하게 막아 내세요."));
    }

    IEnumerator PointCore()
    {
        yield return Show(TutorialStep.Point(
            CoreBar,
            "<b>마왕성 체력</b>입니다.\n" +
            "용사가 성벽을 넘을 때마다 줄어들고, 0 이 되면 이번 여정이 끝납니다.",
            TutorialAnchor.Below));
    }

    IEnumerator PointMana()
    {
        yield return Show(TutorialStep.Point(
            ByName("ManaPanel"),
            "소환에 쓰는 <b>마나</b>입니다. 마나는 <b>판이 끝날 때</b>만 회복됩니다.\n" +
            "아래의 '다음 판 +N' 이 회복량이며, 남겨 둔 마나가 많을수록 조금 더 회복됩니다.",
            TutorialAnchor.Above));
    }

    IEnumerator PointCards()
    {
        yield return Show(TutorialStep.Point(
            ByName("CardRow"),
            "<b>소환 카드</b>입니다. 카드마다 필요한 마나와 한 번에 나오는 마릿수가 표시됩니다.\n" +
            "한 판에 같은 카드를 여러 번 내면 비용이 조금씩 오릅니다 (붉은 숫자).",
            TutorialAnchor.Above));
    }

    // ══════════════════════════════════════════════════════════
    //  ② 직접 해 보기
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 첫 카드를 고르게 한다. 카드는 한 번 고르면 선택이 유지된다.
    /// ⚠ 게임을 멈추지 않는다 — 선택 연출이 timeScale 0 에서 얼어붙는다.
    /// </summary>
    IEnumerator SelectCard()
    {
        yield return Show(TutorialStep.Point(
                FirstCard,
                "카드를 눌러 선택하세요.",
                TutorialAnchor.Above)
            .ClickTarget()
            .KeepRunning()
            .Until(() => Deck != null && Deck.SelectedSlot >= 0));
    }

    /// <summary>
    /// 전장의 라인을 탭하게 한다 — 가운데 라인 띠만 입력을 통과시킨다.
    /// ⚠ 카드 선택이 풀렸으면(다른 곳을 눌렀다) 대기열이 안 생긴다. 시간 초과로 넘어간다.
    /// </summary>
    IEnumerator TapLane()
    {
        yield return Show(TutorialStep.Point(
                MiddleLane,
                "소환할 <b>라인</b>을 누릅니다.\n" +
                "가운데 라인을 눌러 보세요.",
                TutorialAnchor.Above)
            .ClickTarget()
            .KeepRunning()
            .Pad(0f)
            .Until(() => Queued > 0));

        yield return WaitSeconds(0.4f, dim: false);
    }

    IEnumerator ExplainQueue()
    {
        yield return Show(TutorialStep.Say(
            "소환한 몬스터는 성 안에서 <b>대기</b>합니다.\n" +
            "판이 시작되면 성문에서 한 마리씩 나가 싸웁니다.\n" +
            "마나만 있으면 전투 중에도 더 소환할 수 있습니다."));

        yield return Show(TutorialStep.Say(
            "판이 끝날 때 살아남은 몬스터는 <b>대기열로 돌아와</b>\n" +
            "다음 판에도 다시 싸웁니다."));
    }

    /// <summary>
    /// [시작] 을 누르게 한다 — 대기 구간은 이 버튼을 누를 때까지 끝나지 않는다.
    /// </summary>
    IEnumerator PressStart()
    {
        yield return Show(TutorialStep.Point(
                ByName("EnemyInfoButton"),
                "준비가 끝나면 이 버튼을 눌러 <b>판을 시작</b>하세요.",
                TutorialAnchor.Auto)
            .ClickTarget()
            .KeepRunning()
            .Until(() => !IsStageReady));
    }

    // ══════════════════════════════════════════════════════════
    //  ③ 전투 중
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 용사가 실제로 들어오고 첫 몬스터가 나갈 때까지 보여 준다.
    /// ⚠ 어둡게 덮지 않는다 — 방금 세운 몬스터가 나가는 모습이 이 구간의 설명이다.
    /// </summary>
    IEnumerator WaitForBattle()
    {
        yield return WaitUntil(() => BattleManager.Instance != null && BattleManager.Instance.IsWaveRunning,
                               timeout: 20f, dim: false);
        yield return WaitSeconds(2.5f, dim: false);
    }

    /// <summary>
    /// 시그니처 스킬 버튼을 누르게 한다 (겨냥이 켜질 때까지).
    /// ⚠ 스킬이 없거나 쓸 수 없으면 건너뛴다 — 누를 수 없는 버튼을 가리키지 않는다.
    /// </summary>
    IEnumerator PressSignature()
    {
        if (!SummonerSkillRule.CanUse) yield break;

        yield return Show(TutorialStep.Point(
                SignatureSlot,
                "<b>시그니처 스킬</b>입니다. 마나 없이 판마다 정해진 횟수만큼 쓸 수 있습니다.\n" +
                "버튼을 눌러 보세요.",
                TutorialAnchor.Above)
            .ClickTarget()
            .KeepRunning()
            .Until(() => SummonerSkillRule.IsArmed));
    }

    /// <summary>
    /// 전장을 탭해 실제로 쓰게 한다 — 남은 횟수가 줄면 넘어간다.
    /// ⚠ 겨냥이 안 켜졌으면(앞 스텝을 건너뛰었다) 쓸 수 없으니 건너뛴다.
    /// </summary>
    IEnumerator UseSignature()
    {
        if (!SummonerSkillRule.IsArmed) yield break;

        int before = SummonerSkillRule.Remaining;

        yield return Show(TutorialStep.Point(
                MiddleLane,
                "스킬을 쓸 곳을 누르세요.\n" +
                "누른 자리에 바로 발동합니다 — 급할 때를 위해 아껴 두는 것도 방법입니다.",
                TutorialAnchor.Above)
            .ClickTarget()
            .KeepRunning()
            .Pad(0f)
            .Until(() => SummonerSkillRule.Remaining < before));

        // ⚠ 겨냥을 풀어 둔다 — 켜진 채면 다음 설명을 읽고 누르는 탭이 또 스킬을 쓴다.
        SummonerSkillRule.Cancel();

        // 스킬이 무엇을 했는지 보여 준다 — 어둡게 덮지 않는다.
        yield return WaitSeconds(1.5f, dim: false);
    }

    IEnumerator PointStage()
    {
        yield return Show(TutorialStep.Point(
            ByName("StagePanel"),
            "현재 <b>스테이지</b>입니다. 용사를 모두 쓰러뜨리면 승리합니다.\n" +
            "5 스테이지마다 <b>보스 용사</b>가 등장합니다.",
            TutorialAnchor.Below));
    }

    IEnumerator PointSpeed()
    {
        yield return Show(TutorialStep.Point(
            ByName("SpeedButton"),
            "전투가 느리게 느껴지면 <b>배속</b>을 올리세요.",
            TutorialAnchor.Below));
    }

    IEnumerator PointBattleInfo()
    {
        yield return Show(TutorialStep.Point(
            ByName("BattleInfoBtn"),
            "<b>전황</b>에서는 내 카드와 이번 판 용사 부대가\n" +
            "어느 라인으로 들어오는지 한눈에 볼 수 있습니다.",
            TutorialAnchor.Below));
    }

    IEnumerator PointPause()
    {
        yield return Show(TutorialStep.Point(
            ByName("PauseButton"),
            "게임을 멈추거나 소리·언어를 설정합니다.",
            TutorialAnchor.Below));
    }

    IEnumerator PointSynergy()
    {
        yield return Show(TutorialStep.Point(
            ByName("SynergyBar"),
            "<b>시너지</b>입니다. 같은 표식을 가진 몬스터가 많을수록\n" +
            "동 → 은 → 금 순으로 효과가 강해집니다.\n" +
            "아이콘에 올리거나 누르면 효과를 볼 수 있습니다.",
            TutorialAnchor.Auto));
    }

    IEnumerator PointPerks()
    {
        yield return Show(TutorialStep.Point(
            ByName("PerkBar"),
            "소환사의 <b>개성</b>과 여정 중 얻은 <b>특성</b>이 표시됩니다.\n" +
            "특성은 엘리트·보스 스테이지를 클리어하면 얻습니다.",
            TutorialAnchor.Below));
    }

    // ══════════════════════════════════════════════════════════
    //  ④ 마무리
    // ══════════════════════════════════════════════════════════

    IEnumerator Closing()
    {
        yield return Show(TutorialStep.Say(
            "판을 이기면 <b>보상 카드</b>를 고르고 다음 길을 정합니다.\n" +
            "이제 몬스터들이 싸우는 모습을 지켜보세요."));
    }

    // ══════════════════════════════════════════════════════════
    //  타겟 · 조건
    // ══════════════════════════════════════════════════════════

    static bool IsStageReady
        => StageLoopDirector.Instance != null && StageLoopDirector.Instance.IsStageReady;

    static int Queued
        => SummonController.Instance != null ? SummonController.Instance.Reservation.TotalCount : 0;

    static SummonDeckUI Deck => UnityEngine.Object.FindAnyObjectByType<SummonDeckUI>();

    static readonly Func<RectTransform> MiddleLane = ByName("Band_3");

    /// <summary>
    /// 첫 카드 칸 — CardRow 의 첫 <b>활성</b> 카드.
    /// ⚠ ByName("SummonCard") 를 쓰지 않는다 — 카드가 여럿이라 어느 것이 잡힐지 모른다.
    /// </summary>
    static RectTransform FirstCard()
    {
        var row = ByName("CardRow")();
        if (row == null) return null;

        for (int i = 0; i < row.childCount; i++)
        {
            var child = row.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;
            if (child.GetComponent<SummonCardUI>() == null) continue;
            return child as RectTransform;
        }
        return null;
    }

    /// <summary>
    /// 마왕성 체력 막대. 뿌리("CoreHp")는 뚫림 번쩍임 때문에 화면 전체로 늘어나 있어
    /// 그걸 가리키면 하이라이트가 화면 전체가 된다 — 안쪽 막대("Bar")를 가리킨다.
    /// </summary>
    static RectTransform CoreBar()
    {
        var ui = UnityEngine.Object.FindAnyObjectByType<CoreHpBarUI>();
        return ui != null ? ui.transform.Find("Bar") as RectTransform : null;
    }

    /// <summary>
    /// 시그니처 스킬 칸. 이름("SkillSlot")은 다른 화면에도 있어 컴포넌트로 찾는다.
    /// </summary>
    static RectTransform SignatureSlot()
    {
        var ui = UnityEngine.Object.FindAnyObjectByType<SummonerSkillButtonUI>();
        return ui != null ? ui.transform as RectTransform : null;
    }
}
