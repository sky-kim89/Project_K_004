using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  FirstCodexTutorial.cs
//  첫 환생 직후 — 유물 안내 다음으로, 도감에서 슬라임 품질을 한 번 올려 보게 한다.
//
//  ■ 왜 슬라임인가
//    첫 실행은 견습 소환사로 들어가므로(LobbyManager.FirstRunSummonerId) 도감에
//    반드시 열려 있는 종족이 슬라임이다. 다른 종족을 가리키면 없는 칸을 찾는다.
//
//  ■ 품질 개선 버튼은 직접 누르게 한다 (ClickTarget)
//    유물과 달리 이쪽은 "영구 골드로 산다" 는 두 번째 성장 축이다.
//    한 번 눌러 숫자가 오르는 것을 봐야 도감이 '기록장' 이 아니라 '강화소' 로 읽힌다.
//
//  ■ ⚠ 모자란 골드는 채워 준다 (한 번뿐)
//    첫 개선 값(770)은 시작 골드(500)보다 비싸서, 첫 런을 일찍 끝낸 사람은
//    누를 수 없는 버튼을 가리키게 된다. 강제 안내가 막히지 않도록 이 시나리오가
//    시작될 때 **모자란 만큼만** 지급한다. 이미 넉넉하면 주지 않는다.
//    (정본 값은 MonsterGradeUpgradeRule.CostFor — 여기에 숫자를 적지 않는다)
//
//  ■ 슬라임이 이미 Normal 이 아니면(치트 등) 누를 것 없이 설명만 한다.
//
//  트리거: TutorialManager.HandleMainPanelShown (환생 1회 이상, FirstRelic 다음)
// ============================================================

public class FirstCodexTutorial : TutorialScenario
{
    public override TutorialId Id => TutorialId.FirstCodex;

    const string SlimeId = "slime";

    protected override void Build(List<Func<IEnumerator>> steps)
    {
        steps.Add(WaitForPanel);
        steps.Add(Intro);
        steps.Add(OpenCodex);
        steps.Add(PointTabs);
        steps.Add(OpenSlime);
        steps.Add(ExplainDetail);
        steps.Add(PressUpgrade);
        steps.Add(Closing);
    }

    IEnumerator WaitForPanel()
    {
        yield return WaitUntilFree(() => ByName("CodexBtn")() != null, timeout: 30f);
        yield return WaitSeconds(0.4f, dim: false);
    }

    IEnumerator Intro()
    {
        EnsureUpgradeGold();

        yield return Show(TutorialStep.Say(
            "유물 말고도 환생해도 남는 성장이 하나 더 있습니다.\n" +
            "여정에서 번 골드는 끝날 때 <b>영구 골드</b>로 쌓이고,\n" +
            "<b>도감</b>에서 몬스터의 <b>품질</b>을 올리는 데 씁니다."));
    }

    IEnumerator OpenCodex()
    {
        yield return Show(TutorialStep.Point(
                ByName("CodexBtn"),
                "<b>도감</b>을 열어 보겠습니다.",
                TutorialAnchor.Auto)
            .ClickTarget()
            .Until(() => IsOpen(PopupType.Codex)));

        yield return WaitForPopup(PopupType.Codex, timeout: 10f);

        // 지난번에 다른 탭을 보고 닫았을 수 있다 — 슬라임 칸이 있는 탭으로 돌린다.
        PopupManager.Instance.Get<CodexPopup>(PopupType.Codex)?.SelectMonsterTab();
        yield return WaitSeconds(0.3f, dim: false);
    }

    IEnumerator PointTabs()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.Codex, "TabBar"),
            "<b>몬스터 · 특성 · 장비</b> 세 갈래로 지금까지 만난 것을 기록합니다.\n" +
            "기록은 환생해도 지워지지 않습니다.",
            TutorialAnchor.Below));
    }

    IEnumerator OpenSlime()
    {
        if (SlimeCell() == null) yield break;   // 칸이 없으면(스크롤 밖 등) 가리킬 수 없다

        yield return Show(TutorialStep.Point(
                SlimeCell,
                "첫 여정을 함께한 <b>슬라임</b>을 눌러 보세요.",
                TutorialAnchor.Auto)
            .ClickTarget()
            .Until(() => IsOpen(PopupType.MonsterDetail)));

        yield return WaitForPopup(PopupType.MonsterDetail, timeout: 10f);
        yield return WaitSeconds(0.3f, dim: false);
    }

    IEnumerator ExplainDetail()
    {
        if (!IsOpen(PopupType.MonsterDetail)) yield break;

        yield return Show(TutorialStep.Say(
            "몬스터의 세부 능력치·패시브·고유 스킬을 보는 창입니다.\n" +
            "<b>품질</b>이 한 단계 오를 때마다 체력·공격력이 함께 오르고,\n" +
            "장비를 끼울 수 있는 칸도 늘어납니다."));
    }

    IEnumerator PressUpgrade()
    {
        if (!IsOpen(PopupType.MonsterDetail)) yield break;

        if (MonsterGradeUpgradeRule.Check(SlimeId) != MonsterGradeUpgradeRule.Blocked.None)
        {
            yield return Show(TutorialStep.Point(
                InPopup(PopupType.MonsterDetail, "UpgradeBtn"),
                "이 버튼으로 <b>품질 개선</b>을 합니다. 버튼 안의 숫자가 드는 영구 골드입니다.",
                TutorialAnchor.Above));
            yield break;
        }

        UnitGrade before = SlimeGrade;

        yield return Show(TutorialStep.Point(
                InPopup(PopupType.MonsterDetail, "UpgradeBtn"),
                "<b>품질 개선</b> 버튼입니다. 버튼 안의 숫자가 드는 영구 골드입니다.\n" +
                "확률이 아니라 <b>확정</b>으로 오릅니다. 눌러 보세요.",
                TutorialAnchor.Above)
            .ClickTarget()
            .KeepRunning()                          // 등급 상승 연출(UIJuice)을 보여 준다
            .Until(() => SlimeGrade != before));

        yield return WaitSeconds(1.0f, dim: false);
    }

    IEnumerator Closing()
    {
        yield return Show(TutorialStep.Say(
            "품질은 환생해도 그대로 남습니다.\n" +
            "여정 중에는 바꿀 수 없으니, 출발 전에 여기서 강화하세요.\n" +
            "창을 닫고 소환사를 골라 새 여정을 시작하세요."));
    }

    // ── 조건 · 타겟 ──────────────────────────────────────────

    static bool IsOpen(PopupType type)
        => PopupManager.Instance != null && PopupManager.Instance.IsOpen(type);

    static UnitGrade SlimeGrade
    {
        get
        {
            var codex = UserDataManager.Instance.Get<MonsterCodexData>();
            return codex.IsUnlocked(SlimeId) ? codex.GetGrade(SlimeId) : UnitGrade.Normal;
        }
    }

    static RectTransform SlimeCell()
    {
        var popup = PopupManager.Instance?.Get<CodexPopup>(PopupType.Codex);
        return popup != null && popup.IsOpen ? popup.CellOf(SlimeId) : null;
    }

    /// <summary>
    /// 첫 개선에 모자란 영구 골드만 채운다 — 위 머리 주석 참고.
    /// ⚠ 슬라임이 Normal 일 때만 준다. 이미 올렸으면 이 안내가 돈을 줄 이유가 없다.
    /// </summary>
    static void EnsureUpgradeGold()
    {
        var codex = UserDataManager.Instance.Get<MonsterCodexData>();
        if (!codex.IsUnlocked(SlimeId) || codex.GetGrade(SlimeId) != UnitGrade.Normal) return;

        int shortfall = MonsterGradeUpgradeRule.CostFor(UnitGrade.Normal) - MonsterGradeUpgradeRule.Wallet;
        if (shortfall <= 0) return;

        UserDataManager.Instance.Get<ItemData>().Add(eItem.Gold, shortfall);
        UserDataManager.Instance.RequestSave();
        Debug.Log($"[Tutorial] 첫 품질 개선 안내 — 모자란 영구 골드 {shortfall} 지급");
    }
}
