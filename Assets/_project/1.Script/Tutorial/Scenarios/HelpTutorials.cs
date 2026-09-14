using System;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  HelpTutorials.cs
//  팝업별 도움말 — 헤더의 'i' 버튼으로만 열린다 (강제로 안 뜬다).
//
//  ■ 강제 진행과 규칙이 다르다
//    · 조건 대기가 없다 — 이미 그 팝업이 열려 있을 때만 눌리는 버튼이다
//    · 클릭 유도가 없다 — 도움말을 보러 온 사람에게 조작을 시키지 않는다
//
//  ■ 화면에 있는 것을 짚는다
//    "이 화면이 무엇인가" 만 말하면 이미 열어 본 사람에게는 새 정보가 없다.
//    도움말을 누르는 순간은 대개 "이 버튼이 뭘 하는지 모르겠다" 는 순간이므로,
//    그 화면에만 있는 기능(일괄 분해·중복 표시·교체 소멸 등)을 실제 위치를
//    가리키며 설명한다.
//
//  ■ 되돌릴 수 없는 것은 반드시 경고한다
//    장비 교체(기존 소멸)·분해·이벤트 선택은 취소가 없다.
//    도움말에서 안 짚으면 처음 겪는 사람은 잃고 나서야 안다.
//
//  ■ 한 파일에 모아 둔 이유
//    각자 서너 스텝뿐이라 파일을 쪼개면 찾기만 번거로워진다.
//    스텝이 길어지는 것이 생기면 그때 따로 뺀다 (InGameTutorial 처럼).
//
//  ⚠ 타겟을 가리킬 땐 InPopup 으로 그 팝업 안에서 찾는다
//    ByName 은 씬 전체를 훑어 같은 이름의 다른 UI 를 잡을 수 있다.
//    로비 패널(유물·난이도)은 팝업이 아니라 ByName 을 쓴다.
//
//  ⚠ 타겟 이름은 Creator 가 만드는 이름과 같아야 한다
//    틀리면 하이라이트만 사라지고 말풍선은 화면 중앙에 뜬다 — 튜토리얼이
//    멈추지는 않으므로, 이름을 바꿀 때 여기도 같이 고쳐야 조용히 어긋나지 않는다.
// ============================================================

// ── 유물 · 환생 ─────────────────────────────────────────────

public class RelicHelpTutorial : TutorialScenario
{
    public override TutorialId Id => TutorialId.HelpRelic;

    protected override void Build(List<Func<IEnumerator>> steps)
    {
        steps.Add(What);
        steps.Add(Controls);
        steps.Add(Points);
        steps.Add(Fog);
        steps.Add(Branches);
        steps.Add(Reset);
        steps.Add(Reincarnate);
    }

    IEnumerator What()
    {
        yield return Show(TutorialStep.Say(
            "<b>유물 전승도</b>는 환생해도 사라지지 않는 영구 성장입니다.\n" +
            "장수·장비·특성이 전부 초기화돼도 여기 찍은 것은 남습니다.\n" +
            "여정을 거듭할수록 출발선이 앞당겨지는 부분이 이곳입니다."));
    }

    // ⚠ 조작 안내는 여기에만 있다
    //   헤더에 상시 표기하던 것을 걷어냈다 — 한 번 읽으면 자리만 차지한다.
    //   대신 도움말에서 언제든 다시 꺼내 볼 수 있게 첫머리에 둔다.
    IEnumerator Controls()
    {
        yield return Show(TutorialStep.Say(
            "트리는 화면보다 큽니다.\n" +
            "<b>빈 곳을 끌어</b> 움직이고, <b>휠 또는 두 손가락</b>으로 확대·축소합니다.\n" +
            "노드를 누르면 무슨 효과인지 왼쪽 아래에 뜹니다."));
    }

    IEnumerator Points()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.Relic, "PointGroup"),
            "찍는 데 쓰는 <b>환생 포인트</b>입니다.\n" +
            "환생할 때 도달한 스테이지와 난이도에 따라 받습니다.",
            TutorialAnchor.Below));
    }

    IEnumerator Fog()
    {
        yield return Show(TutorialStep.Say(
            "<b>?</b> 로 보이는 자리는 아직 잠긴 노드입니다.\n" +
            "바로 앞 노드를 1레벨 이상 찍어야 열리고, 그제서야 이름과 효과가 보입니다.\n" +
            "무엇이 나올지 모르는 채로 길을 고르는 것이 이 화면의 규칙입니다."));
    }

    IEnumerator Branches()
    {
        yield return Show(TutorialStep.Say(
            "가운데에서 네 갈래가 뻗습니다 — <b>위 공격력 · 아래 체력 · 왼쪽 병사 수 · 오른쪽 경험치</b>.\n" +
            "각 갈래는 아래로 갈수록 다시 여러 가지로 벌어지고, 멀수록 세지만 비쌉니다.\n" +
            "한 갈래를 깊게 팔지, 네 갈래를 얕게 펼지가 이 화면의 선택입니다."));
    }

    IEnumerator Reset()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.Relic, "ResetBtn"),
            "<b>트리 초기화</b>는 찍은 것을 전부 되돌리고 <b>포인트를 전액 돌려줍니다</b>.\n" +
            "손해가 없으니 길을 잘못 들었다 싶으면 언제든 다시 짜도 됩니다.",
            TutorialAnchor.Above));
    }

    IEnumerator Reincarnate()
    {
        yield return Show(TutorialStep.Say(
            "환생하면 이번 여정의 장수·장비·특성·어빌리티가 모두 사라지고\n" +
            "그 대가로 포인트를 받습니다.\n" +
            "더 나아갈 수 없을 때 다시 시작하는 수단입니다."));
    }
}

// ── 도감 ────────────────────────────────────────────────────

public class CodexHelpTutorial : TutorialScenario
{
    public override TutorialId Id => TutorialId.HelpCodex;

    protected override void Build(List<Func<IEnumerator>> steps)
    {
        steps.Add(What);
        steps.Add(Tabs);
        steps.Add(Upgrade);
    }

    IEnumerator What()
    {
        yield return Show(TutorialStep.Say(
            "<b>도감</b>은 지금까지 한 번이라도 얻어 본 것들의 기록입니다.\n" +
            "잃거나 분해해도 기록은 지워지지 않고,\n" +
            "환생해도 그대로 남습니다."));
    }

    IEnumerator Tabs()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.Codex, "TabBar"),
            "<b>몬스터</b>와 <b>특성</b> 두 갈래입니다.\n" +
            "아직 못 만난 칸은 비어 있으니 무엇이 남았는지 여기서 봅니다.",
            TutorialAnchor.Below));
    }

    // ⚠ 옛 '수집 버프' 설명을 지운 자리다 (2026-09-06)
    //   "1종당 공격력·체력 +0.5%" 는 폐기됐다 — 그 버프가 걸리던 대상은
    //   이 게임에서 적(용사)이었다. 도감의 값어치는 품질 개선이 갖는다.
    IEnumerator Upgrade()
    {
        yield return Show(TutorialStep.Say(
            "몬스터 칸을 누르면 <b>품질</b>을 올릴 수 있습니다.\n" +
            "영구 골드를 내면 확정으로 한 단계 오르고, 체력·공격력이 함께 오릅니다.\n" +
            "환생해도 남는 영구 성장입니다."));
    }
}

// ── 난이도 ──────────────────────────────────────────────────

public class DifficultyHelpTutorial : TutorialScenario
{
    public override TutorialId Id => TutorialId.HelpDifficulty;

    protected override void Build(List<Func<IEnumerator>> steps)
    {
        steps.Add(What);
        steps.Add(Debuff);
        steps.Add(Unlock);
        steps.Add(Locked);
    }

    IEnumerator What()
    {
        yield return Show(TutorialStep.Say(
            "난이도를 올리면 적이 강해지는 대신\n" +
            "환생할 때 받는 <b>환생 포인트</b>가 늘어납니다.\n" +
            "유물을 빨리 모으려면 결국 올려야 하는 값입니다."));
    }

    IEnumerator Debuff()
    {
        yield return Show(TutorialStep.Say(
            "등급마다 붙는 것이 다릅니다.\n" +
            "적이 세지는 <b>광포</b>, 수가 늘어나는 <b>물량</b>,\n" +
            "우두머리가 스킬을 더 자주 쓰는 <b>각성·폭주</b>가 차례로 더해집니다."));
    }

    IEnumerator Unlock()
    {
        yield return Show(TutorialStep.Say(
            "지금 등급으로 <b>20스테이지</b>까지 나아가면\n" +
            "다음 등급이 열립니다. 끝까지 깰 필요는 없습니다."));
    }

    IEnumerator Locked()
    {
        yield return Show(TutorialStep.Say(
            "여정이 시작되면 난이도는 <b>바꿀 수 없습니다.</b>\n" +
            "쉬운 등급으로 앞을 깔고 마지막만 올리는 것을 막기 위해서입니다.\n" +
            "고르는 것은 다음 여정을 시작하기 전입니다."));
    }
}
