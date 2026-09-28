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
//    그 화면에만 있는 기능을 실제 위치를 가리키며 설명한다.
//
//  ■ 되돌릴 수 없는 것은 반드시 경고한다
//    품질 개선(영구 골드 지출)·진화(레벨 초기화)·환생은 취소가 없다.
//
//  ■ 한 파일에 모아 둔 이유
//    각자 서너 스텝뿐이라 파일을 쪼개면 찾기만 번거로워진다.
//
//  ⚠ 타겟을 가리킬 땐 InPopup 으로 그 팝업 안에서 찾는다
//    ByName 은 씬 전체를 훑어 같은 이름의 다른 UI 를 잡을 수 있다.
//
//  ⚠ 타겟 이름은 Creator 가 만드는 이름과 같아야 한다
//    틀리면 하이라이트만 사라지고 말풍선은 화면 중앙에 뜬다.
//
//  ⚠ i 버튼은 Creator 가 굽는다 (EditorUIBuilder.InfoBtn) — 시나리오를 추가했으면
//    그 팝업의 Creator 에도 한 줄 넣고 다시 구울 것. 등록만 하면 버튼이 없다.
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
            "카드·특성·런 골드가 전부 초기화돼도 여기 찍은 것은 남습니다.\n" +
            "여정을 거듭할수록 출발선이 앞당겨지는 부분이 이곳입니다."));
    }

    // ⚠ 조작 안내는 여기에만 있다 — 헤더에 상시 표기하던 것을 걷어냈다.
    IEnumerator Controls()
    {
        yield return Show(TutorialStep.Say(
            "트리는 화면보다 큽니다.\n" +
            "<b>빈 곳을 끌어</b> 움직이고, <b>휠 또는 두 손가락</b>으로 확대·축소합니다.\n" +
            "노드를 누르면 무슨 효과인지 상세 카드가 뜹니다."));
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
            "바로 앞 노드를 1레벨 이상 찍어야 열리고, 그제서야 이름과 효과가 보입니다."));
    }

    IEnumerator Branches()
    {
        yield return Show(TutorialStep.Say(
            "가운데에서 네 갈래가 뻗습니다 —\n" +
            "<b>위 소환수 · 아래 마왕성 · 왼쪽 마나 · 오른쪽 통솔</b>.\n" +
            "멀리 갈수록 세지만 비쌉니다. 한 갈래를 깊게 팔지, 넓게 펼지가 이 화면의 선택입니다."));
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
            "환생하면 이번 여정의 카드·특성·런 골드가 모두 사라지고\n" +
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
        steps.Add(Gear);
    }

    IEnumerator What()
    {
        yield return Show(TutorialStep.Say(
            "<b>도감</b>은 지금까지 한 번이라도 만나 본 것들의 기록입니다.\n" +
            "환생해도 그대로 남습니다."));
    }

    IEnumerator Tabs()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.Codex, "TabBar"),
            "<b>몬스터 · 특성 · 장비</b> 세 갈래입니다.\n" +
            "아직 못 만난 칸은 비어 있으니 무엇이 남았는지 여기서 봅니다.",
            TutorialAnchor.Below));
    }

    // ⚠ 옛 '수집 버프' 설명을 지운 자리다 (2026-09-06) — 도감의 값어치는 품질 개선이 갖는다.
    IEnumerator Upgrade()
    {
        yield return Show(TutorialStep.Say(
            "몬스터 칸을 누르면 <b>품질</b>을 올릴 수 있습니다.\n" +
            "영구 골드를 내면 확정으로 한 단계 오르고, 체력·공격력이 함께 오릅니다.\n" +
            "여정 중에는 바꿀 수 없으니 출발 전에 강화하세요."));
    }

    IEnumerator Gear()
    {
        yield return Show(TutorialStep.Say(
            "<b>장비</b>는 여정이 끝날 때 보상 상자에서 얻습니다.\n" +
            "장비 칸을 누르면 같은 장비를 재료로 <b>레벨업</b>할 수 있고,\n" +
            "끼우는 것은 몬스터 상세 창에서 합니다."));
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
            "환생할 때 받는 <b>환생 포인트</b>와 <b>장비 상자</b>가 늘어납니다.\n" +
            "유물을 빨리 모으려면 결국 올려야 하는 값입니다."));
    }

    IEnumerator Debuff()
    {
        yield return Show(TutorialStep.Say(
            "등급마다 붙는 것이 다릅니다.\n" +
            "적이 세지는 <b>광포</b>, 수가 늘어나는 <b>물량</b>,\n" +
            "우두머리가 스킬을 더 자주 쓰는 <b>각성</b>이 차례로 더해집니다."));
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
            "고르는 것은 다음 여정을 시작하기 전입니다."));
    }
}

// ── 몬스터 상세 ─────────────────────────────────────────────

public class MonsterDetailHelpTutorial : TutorialScenario
{
    public override TutorialId Id => TutorialId.HelpMonsterDetail;

    protected override void Build(List<Func<IEnumerator>> steps)
    {
        steps.Add(Left);
        steps.Add(Upgrade);
        steps.Add(Gear);
        steps.Add(Passives);
    }

    IEnumerator Left()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.MonsterDetail, "Left"),
            "초상화 아래에 <b>소환 마나</b>와 한 번에 나오는 <b>마릿수</b>,\n" +
            "이 몬스터가 가진 <b>시너지 표식</b>이 있습니다.",
            TutorialAnchor.Auto));
    }

    IEnumerator Upgrade()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.MonsterDetail, "UpgradeBtn"),
            "<b>품질 개선</b> — 영구 골드를 내고 확정으로 한 단계 올립니다.\n" +
            "체력·공격력이 오르고 장비·패시브 칸이 늘어납니다. 되돌릴 수 없습니다.\n" +
            "여정 중에는 이 버튼이 사라집니다.",
            TutorialAnchor.Above));
    }

    IEnumerator Gear()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.MonsterDetail, "GearRow"),
            "<b>장비 칸</b>입니다. 칸을 눌러 가진 장비를 끼웁니다.\n" +
            "장비 하나는 한 몬스터만 낄 수 있고, 다른 몬스터가 낀 것을 고르면 옮겨 옵니다.",
            TutorialAnchor.Auto));
    }

    IEnumerator Passives()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.MonsterDetail, "Right"),
            "종족 <b>패시브</b>와 <b>고유 스킬</b>입니다.\n" +
            "같은 패시브가 둘 이상 겹치면 <b>각성</b>해 더 강한 하나가 됩니다.",
            TutorialAnchor.Auto));
    }
}

// ── 카드 3택 ────────────────────────────────────────────────

public class CardSelectHelpTutorial : TutorialScenario
{
    public override TutorialId Id => TutorialId.HelpCardSelect;

    protected override void Build(List<Func<IEnumerator>> steps)
    {
        steps.Add(What);
        steps.Add(Maxed);
        steps.Add(Stats);
    }

    IEnumerator What()
    {
        yield return Show(TutorialStep.Say(
            "스테이지를 깰 때마다 <b>카드 한 장</b>을 고릅니다.\n" +
            "새 카드는 덱의 빈 칸에 들어가고, 가진 카드를 고르면 <b>레벨</b>이 오릅니다.\n" +
            "소환사의 <b>친화</b> 종족은 더 자주 나옵니다."));
    }

    IEnumerator Maxed()
    {
        yield return Show(TutorialStep.Say(
            "최대 레벨 카드를 고르면 <b>진화 · 융합 · 강화</b> 중 하나를 합니다.\n" +
            "진화는 더 강한 종족이 되지만 <b>Lv1 부터 다시</b> 키웁니다.\n" +
            "융합은 다른 카드를 재료로 바쳐 그 개성을 배웁니다."));
    }

    IEnumerator Stats()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.CardSelect, "StatsBtn"),
            "<b>전투 통계</b>에서 카드별 피해·받은 피해·치유를 봅니다.\n" +
            "무엇을 키울지 고민될 때 참고하세요.",
            TutorialAnchor.Below));
    }
}

// ── 전황 ────────────────────────────────────────────────────

public class BattleInfoHelpTutorial : TutorialScenario
{
    public override TutorialId Id => TutorialId.HelpBattleInfo;

    protected override void Build(List<Func<IEnumerator>> steps)
    {
        steps.Add(Ally);
        steps.Add(Enemy);
    }

    IEnumerator Ally()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.BattleInfo, "AllyPanel"),
            "왼쪽은 <b>내 덱</b>입니다. 카드를 누르면 이번 여정의 실제 능력치가 보입니다\n" +
            "(레벨·시너지·특성이 모두 얹힌 값).",
            TutorialAnchor.Auto));
    }

    IEnumerator Enemy()
    {
        yield return Show(TutorialStep.Point(
            InPopup(PopupType.BattleInfo, "EnemyPanel"),
            "오른쪽은 이번 판 <b>용사 부대</b>입니다. 들어오는 <b>라인 그대로</b> 세워 두었습니다.\n" +
            "무거운 라인에 몬스터를 더 세우세요. 용사를 누르면 상세가 뜹니다.",
            TutorialAnchor.Auto));
    }
}
