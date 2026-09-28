using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  RunBootstrap.cs
//  인게임 씬 하나만 실행해도 게임이 돌아가게 만드는 진입점 + 런 루프.
//
//  ■ 런 시작에 하는 일 (순서대로)
//    ⓪ 선택 대기     — 로비가 있으면 플레이어가 소환사를 고를 때까지 기다린다
//    ① 소환사 확정   — 고른 캐릭터, 없으면 자동 선택
//    ② 시작 카드 배치 — 소환사에 붙박인 카드만 카드 바에 놓는다
//    ③ 마나 지급     — 소환사의 StartMana 를 런당 1회
//    ④ 소환사 스폰   — 왼쪽 성벽 뒤에 세운다
//    ⑤ 스테이지 대기 — 여기서 플레이어가 몬스터를 미리 소환한다
//
//  ■ ⚠ 덱 편성이 없어졌다 (2026-08-27 설계 변경)
//    예전에는 도감에서 해금된 종족을 골라 덱을 짜고 들어왔다. 지금은
//      · 시작 카드는 **소환사가 정한다** (SummonerData.StarterMonsters/Skills)
//      · 나머지 카드는 **런을 돌며 보상으로 줍는다** (CardRewardPicker)
//      · 같은 카드를 또 주우면 그 카드가 레벨업한다 (CardLevelRule)
//    도감(MonsterCodexData)은 편성 풀이 아니라 순수한 기록으로 돌아갔다.
//    "무엇을 데리고 시작하는가" 가 캐릭터 선택의 무게가 된다.
//
//  ■ 런 루프
//    대기 ─▶ 시작 ─▶ 전투 ─▶ 승리 ─▶ 카드 보상 3택 ─▶ 다음 스테이지 대기
//    승리 감지는 BattleManager.OnVictory 로 받는다.
//
//  ■ 적은 여기서 미리 세우지 않는다
//    스테이지 대기 중에는 적이 없어야 한다. "시작" 을 누른 뒤에
//    HeroSpawner 가 오른쪽 밖에서 걸어 들여보낸다.
// ============================================================

public class RunBootstrap : MonoBehaviour
{
    [Header("데이터")]
    [Tooltip("이 런에 쓸 소환사. 비우면 Resources 에서 첫 번째를 집는다.")]
    [SerializeField] SummonerData _summoner;

    [Header("배치")]
    [Tooltip("전장 레이아웃. 비우면 씬에서 찾는다.")]
    [SerializeField] SummonFieldLayout _field;

    [Tooltip("용사 편성을 세울 스포너. 비우면 씬에서 찾는다.")]
    [SerializeField] HeroSpawner _heroSpawner;

    // ⚠ 카드 3택도 진화·융합도 **팝업이다** — 인스펙터 참조를 두지 않는다
    //   전투 통계를 카드 위에 띄워야 해서 쌓임을 아는 PopupManager 쪽이 맞고,
    //   진화 창도 2026-09-07 에 HUD 자식에서 팝업으로 옮겼다 (사용자 지적).
    //   HUD 자식이던 시절에는 겹침 순서를 손으로 맞춰야 했고, HUD 를 굽다
    //   멈추면 창이 통째로 사라진 채 런타임이 조용히 넘어갔다.

    [Header("스테이지")]
    [Tooltip("런이 시작하는 스테이지 번호. 매 런 여기서 다시 센다.")]
    [Min(1)] [SerializeField] int _startStage = 1;

    [Tooltip("몇 스테이지마다 허들(엘리트)이 오는가. 보스 히어로가 나온다.")]
    [Min(2)] [SerializeField] int _hurdleEvery = 5;

    /// <summary>지금 진행 중인 스테이지 번호. 런이 시작될 때 _startStage 로 되돌아간다.</summary>
    int _stageNumber;

    /// <summary>런이 끝났다 — 대기 루프가 이 신호를 보고 다음 선택을 기다린다.</summary>
    bool _runEnded;

    /// <summary>
    /// 이번 스테이지의 성격 — <b>이 게임에서 "지금 무슨 판인가" 의 정본</b>.
    ///
    /// 보스는 번호가 정하고(5의 배수), 엘리트는 갈림길에서 고른 결과다.
    /// ⚠ 스테이지 번호로 다시 판정하지 말 것 — 엘리트는 번호에 안 적혀 있다.
    /// </summary>
    RunStageKind _stageKind = RunStageKind.Normal;

    // ── 생명주기 ─────────────────────────────────────────────

    IEnumerator Start()
    {
        // 세이브가 준비될 때까지 기다린다 — Splash 를 건너뛰고 인게임만 열면
        // UserDataManager 가 아직 없을 수 있다.
        yield return new WaitUntil(() => UserDataManager.Instance != null);

        // ⚠ 로비가 올라와 있으면 **선택을 기다린다**
        //   Splash 가 Lobby 와 InGame 을 둘 다 additive 로 올리므로, 이 Start 는
        //   플레이어가 캐릭터를 고르기도 전에 돈다. 그대로 진행하면 아무 소환사나
        //   자동으로 세워 두고, 나중에 고른 캐릭터는 반영되지 않는다.
        //   인게임 씬만 열어 확인할 때는 로비가 없으므로 곧바로 지나간다.
        if (RunLaunch.LobbyPresent)
            yield return new WaitUntil(() => RunLaunch.Requested);

        if (_field       == null) _field       = FindAnyObjectByType<SummonFieldLayout>();
        if (_heroSpawner == null) _heroSpawner = FindAnyObjectByType<HeroSpawner>();

        StageLoopDirector.OnStageStart += HandleStageStart;
        BattleManager.OnVictory        += HandleVictory;
        BattleManager.OnDefeat         += HandleDefeat;

        // ⚠ 런 하나로 끝나지 않는다 — 죽으면 다시 고르고 다시 시작한다
        //   예전에는 이 Start 가 한 번 돌고 끝나서, 패배 후 선택 화면으로
        //   돌아가도 **두 번째 런이 영영 시작되지 않았다**. 씬을 다시 로드하지
        //   않는 구조(로비·인게임 동시 상주)라 여기서 돌아야 한다.
        while (true)
        {
            // 로비가 있으면 플레이어가 소환사를 고를 때까지 기다린다.
            // 인게임 씬만 열어 확인할 때는 곧바로 지나간다 (RunLaunch 참고).
            if (RunLaunch.LobbyPresent)
                yield return new WaitUntil(() => RunLaunch.Requested);

            StartRun();

            yield return new WaitUntil(() => _runEnded);
            _runEnded = false;
        }
    }

    /// <summary>
    /// 런 하나를 세운다. 패배로 끝나면 대기 루프가 다시 여기로 온다.
    ///
    /// ■ 이어하기 (앱을 껐다 켠 경우)
    ///   SummonRunData 에 살아 있는 런이 적혀 있으면 그 스테이지부터 시작한다.
    ///   카드·마나·특성은 각자 제 섹션에 이미 저장돼 있으므로 **다시 주지 않는다** —
    ///   마나를 또 주면 죽기 직전에 앱을 껐다 켜는 것이 회복 수단이 되고,
    ///   시작 카드를 또 깔면 키워 놓은 카드를 덮어쓴다.
    ///   (BuildStarterDeck 은 칸이 차 있으면 스스로 물러난다. GrantMana 는
    ///    잔량을 통째로 되돌리므로 여기서 건너뛰어야 한다.)
    /// </summary>
    void StartRun()
    {
        var run = UserDataManager.Instance.Get<SummonRunData>();

        // ⚠ 로비에서 새 런을 요청했으면 그쪽이 이긴다
        //   이어하기 기록이 남아 있어도, 방금 캐릭터를 고른 사람의 뜻이 우선이다.
        //   (정상 흐름에서는 환생이 기록을 지우지만, 인게임 단독 실행 등
        //    기록이 남은 채 새 런이 들어오는 경로가 있다)
        bool resume = run.CanResume && string.IsNullOrEmpty(RunLaunch.SummonerId);

        if (resume)
        {
            _stageNumber = run.StageNumber;
            RunLaunch.Clear();
        }
        else
        {
            _stageNumber = _startStage;
        }

        // 첫 판(또는 이어하기 시작 판)의 성격. 보스가 아니면 일반이다 —
        // 엘리트는 갈림길에서 고른 뒤에야 붙는다.
        _stageKind = RunStageKindRule.Of(_stageNumber);

        // ⚠ 인게임 씬은 런 사이에 상주한다 — 이 컴포넌트도 살아남는다.
        //   비우지 않으면 지난 런에서 5기까지 갔던 무한 보스가 다음 런의
        //   첫 보스부터 ×100000 으로 선다.
        _endlessBossIndex = 0;

        ResolveSummoner(resume ? run.SummonerId : null);
        RunLaunch.Consume();   // 다음 런이 이 요청을 물려받지 않게 즉시 비운다

        // ⚠ 정적 상태라 런 사이에 살아남는다
        //   에디터는 플레이를 멈춰도 static 이 남는다. 초기화하지 않으면
        //   다음 런 첫 판이 지난 런의 과부하를 물려받는다.
        SummonCostRule.Reset();

        BuildStarterDeck();

        // ⚠ 라인 대기열은 카드·마나와 같은 취급이다
        //   예약은 선불이라(SummonReservation) 이어하기에서 되돌려 주지 않으면
        //   낸 마나만 사라진다. 반대로 새 런에서 비우지 않으면 지난 런의
        //   예약분이 공짜 물량으로 따라온다.
        if (resume) SummonController.Instance?.RestoreQueue();
        else        SummonController.Instance?.ClearQueue();

        if (resume)
        {
            // ⚠ 마왕성 체력이 0 이면 이어하기라도 채운다 (2026-09-06)
            //   RunCoreData.Max 가 0 이면 CoreBreachSystem 이 매 프레임 조용히
            //   빠져나가고 IsDown 도 `Max > 0` 조건이라 영영 false 다 — 적이
            //   성벽에 붙어도 아무 일이 없고 런이 끝나지 않는다.
            //   이 기능이 생기기 전에 시작된 런의 세이브가 그 상태다.
            //   ⚠ 0 일 때만이다. 값이 있으면 건드리지 않는다 —
            //     앱을 껐다 켜는 것이 회복 수단이 되면 안 된다 (GrantMana 와 같은 규칙).
            var core = UserDataManager.Instance.Get<RunCoreData>();
            if (core.Max <= 0)
            {
                core.GrantForRun(_summoner.MaxCoreHp);
                Debug.LogWarning($"[RunBootstrap] 마왕성 체력이 비어 있어 다시 채웠습니다 " +
                                 $"— {_summoner.MaxCoreHp}");
            }

            Debug.Log($"[RunBootstrap] 이어하기 — {_summoner.DisplayName} / 스테이지 {_stageNumber}");
        }
        else
        {
            GrantMana();

            // ⚠ 이어하기에서는 부르지 않는다 (GrantMana 와 같은 이유)
            //   지우면 모아 둔 골드가 앱 재시작으로 사라진다.
            UserDataManager.Instance.Get<RunGoldData>().ResetForNewRun();
            UserDataManager.Instance.Get<RunBoonData>().SetDefaults();

            // 마왕성 체력 — 런당 1회. 이어하기는 세이브에 있는 값을 그대로 쓴다.
            UserDataManager.Instance.Get<RunCoreData>()
                           .GrantForRun(_summoner.MaxCoreHp);

            run.Begin(_summoner.Id, _stageNumber);
            UserDataManager.Instance.RequestSave();
        }

        // ⚠ 대기열이 복원된 **뒤**에 센다
        //   시너지는 덱이 아니라 "실제로 낸 종족" 이 정한다. 새 런이면 빈 채로
        //   시작하고, 이어하기면 되살린 대기열에서 도로 세어 온다.
        //   RestoreQueue 앞에서 부르면 이어하기가 시너지를 통째로 잃는다.
        //   이후에는 카드를 낼 때마다 MonsterSynergyRule.MarkSummoned 가 늘린다.
        // ⚠ 강화 대상을 Bind **앞**에 되돌린다 — Bind 안에서 Recount 가 돈다
        //   순서가 뒤집히면 이어하기 첫 판에서 강화 카운트가 빠진 채로 센다.
        MonsterSynergyRule.RestoreBoost(
            resume ? UserDataManager.Instance.Get<SummonRunData>().BoostTag : MonsterTag.None);

        MonsterSynergyRule.Bind();

        // 스테이지 제한형 스킬의 사용 기록을 비운다 (런 제한형은 세이브가 들고 있다).
        SummonerSkillRule.Bind();

        // ⚠ 소환사보다 먼저 — PrepareStage 가 BattleContext 를 새로 만든다.
        //   순서가 뒤집히면 방금 세운 소환사가 생존 카운트에 안 잡힌다.
        PrepareStage();

        SpawnSummoner();

        // ⚠ 소환사가 선 **뒤**에 준다 (유물 '오래된 계약')
        //   RunPerkData.CollectMissing 이 소환사 개성을 보고 겹치는 특성을 걸러내고
        //   (견습 + 친화 할인), '친화 확장' 은 소환사가 없으면 대상을 못 고른다.
        //   BuildStarterDeck·GrantForRun 보다 뒤이기도 해서 '확장 편성'(칸 +2)·
        //   '유리 성채'(성 −40%) 처럼 얻는 순간 값을 건드리는 특성도 제자리를 찾는다.
        if (!resume) GrantStartingPerks();

        EnterStageReady();
    }

    /// <summary>
    /// 유물 '오래된 계약' — 런을 시작할 때 무작위 특성을 쥐고 출발한다.
    ///
    /// ⚠ <b>새 런에서만</b> 부른다 — 이어하기에서 또 주면 앱을 껐다 켜는 것이
    ///   특성 획득 수단이 된다 (GrantMana 와 같은 규칙).
    /// </summary>
    void GrantStartingPerks()
    {
        int count = RelicTreeApplier.GetSystemInt(RelicSystemEffect.StartingPerkCount);
        if (count <= 0) return;

        var data = UserDataManager.Instance.Get<RunPerkData>();

        // ⚠ 한 번에 뽑는다 — 한 장씩 뽑으면 같은 특성이 두 번 나온다
        //   (RunPerkPicker.Pick 은 한 호출 안에서만 중복을 막는다.
        //    이벤트 '뒤집힌 모래시계' 가 특성 2개를 줄 때와 같은 이유다)
        foreach (RunPerk perk in RunPerkPicker.Pick(data, count))
        {
            data.Add(perk);
            Debug.Log($"[RunBootstrap] 오래된 계약 — 시작 특성 '{perk.ToKorean()}'");
        }

        UserDataManager.Instance.RequestSave();
    }

    void OnDestroy()
    {
        StageLoopDirector.OnStageStart -= HandleStageStart;
        BattleManager.OnVictory        -= HandleVictory;
        BattleManager.OnDefeat         -= HandleDefeat;
    }

    /// <summary>
    /// 소환사가 쓰러졌다 — 런이 끝났다.
    ///
    /// ⚠ 손에 든 카드를 통째로 버린다 (사용자 확정 규칙)
    ///   진화·융합으로 얻은 패시브는 **런 안에서만** 산다. 다음 런은
    ///   소환사의 시작 카드에서 기본 패시브만 갖고 다시 시작한다.
    ///   여기서 지우지 않으면 지난 런에서 신속을 먹인 슬라임이 그대로 따라와
    ///   런을 거듭할수록 시작이 세지는 구조가 된다.
    ///
    /// 도감(MonsterCodexData)은 건드리지 않는다 — 그쪽은 영구 기록이다.
    /// </summary>
    /// <summary>
    /// 마왕성이 무너졌다 — **환생 결산**을 띄운다.
    ///
    /// ■ 왜 전용 패배 화면을 만들지 않았나
    ///   런이 끝났을 때 플레이어가 받아야 하는 것은 "다시 도전" 버튼이 아니라
    ///   **환생 포인트**다. 그 화면은 이미 있다 — ReincarnationPopup 이
    ///   딜/탱/힐 탭·세그먼트 바·획득 포인트·환생 버튼을 전부 갖고 있고,
    ///   초기화 목록도 UserDataManager.Reincarnate() 하나가 소유한다
    ///   (SummonDeckData·RunPerkData 도 거기서 함께 비워진다).
    ///
    /// ■ 통계를 **먼저** 실어 준다
    ///   그 화면은 BattleContext.CombatStats 를 읽는다. 이 게임의 통계는
    ///   카드 단위라 원작 장수 집계(BattleStatsTracker)에는 아무것도 없다 —
    ///   여기서 카드 집계를 그 판으로 옮겨 담지 않으면 목록이 통째로 빈다.
    ///
    /// ■ 결산을 먼저 띄우고 카드는 그 뒤에 버린다
    ///   "무엇으로 여기까지 왔는가" 를 보여 주려면 덱이 아직 살아 있어야 한다.
    /// </summary>
    void HandleDefeat()
    {
        int kills = BattleManager.Instance?.EnemyKillCount ?? 0;

        Debug.Log($"[RunBootstrap] 런 종료 — 스테이지 {_stageNumber} 에서 마왕성이 무너졌습니다.");

        BattleContext context = BattleManager.Instance?.Context;

        if (context != null && CardStatsTracker.Instance != null)
        {
            context.CombatStats.Clear();
            context.CombatStats.AddRange(
                CardStatsTracker.Instance.BuildStatEntries(CardCatalog.Current));
        }

        var popup = PopupManager.Instance?.Open<ReincarnationPopup>(PopupType.Reincarnation);

        if (popup == null)
        {
            Debug.LogWarning("[RunBootstrap] ReincarnationPopup 을 열지 못했습니다 — " +
                             "결산 없이 선택 화면으로 돌아갑니다.");
            FinishRun();
            return;
        }

        popup.Setup(context, kills, FinishRun);
    }

    /// <summary>결산을 확인했다 — 런의 흔적을 지우고 선택 화면으로.</summary>
    void FinishRun()
    {
        // ⚠ 런 흔적을 지우기 **전**이다 — Earned 를 읽어야 한다
        //   이번 런에 번 총액만큼 영구 골드가 따라온다. 몬스터 도감의
        //   품질 개선이 그 돈을 쓴다 (RunGoldRule.SettleToPermanent).
        int settled = RunGoldRule.SettleToPermanent();
        if (settled > 0)
            Debug.Log($"[RunBootstrap] 이번 런 수확 — 영구 골드 +{settled}");

        GrantGearBox();

        UserDataManager.Instance.Get<SummonDeckData>().ResetForNewRun();
        UserDataManager.Instance.Get<RunPerkData>().ResetForNewRun();
        UserDataManager.Instance.Get<SummonRunData>().End();
        UserDataManager.Instance.RequestSave();

        ReturnToSelect();

        // 대기 루프를 깨워 다음 선택을 받게 한다.
        _runEnded = true;
    }

    /// <summary>
    /// 런 종료 보상 — 몬스터 장비 하나가 든 상자를 준다.
    ///
    /// ■ ⚠ 뽑기와 지급이 먼저, 연출이 나중이다
    ///   MonsterGearRewardRule.GrantForStage 가 **즉시 인벤토리에 넣고**
    ///   무엇이었는지 돌려준다. 팝업은 그것을 흔들어 보여 줄 뿐이다.
    ///   '안 연 상자' 를 세이브에 두면 연출 도중 앱이 꺼졌을 때 보상이
    ///   사라지는 길이 생기고, 그걸 막으려면 상태 기계를 하나 더 들여야 한다.
    ///
    /// ■ ⚠ 런 흔적을 지우기 **전**에 부른다
    ///   등급을 정하는 것이 도달 스테이지(_stageNumber)다. 아래에서
    ///   SummonRunData.End() 가 돌고 나면 그 값을 다시 알 길이 없다.
    ///
    /// ■ 팝업이 없어도 런은 끝난다
    ///   프리팹을 아직 등록하지 않았을 수 있다(PopupManager._prefabs).
    ///   보상은 이미 들어갔으므로 로그만 남기고 넘어간다 — 연출이 없다고
    ///   선택 화면으로 못 돌아가면 그게 더 나쁘다.
    /// </summary>
    /// <summary>
    /// 이 스테이지부터 상자를 준다 (사용자 지시, 2026-09-15).
    ///
    /// ⚠ 1스테이지는 빈손으로 끝난다 — 의도된 것이다
    ///   첫 판은 **한 번도 안 진 채로** 끝날 수가 없다. 그런데 거기서 끝난 런에도
    ///   상자가 나오면 "시작하자마자 항복 → 상자" 가 성립해, 장비를 모으는 가장 빠른
    ///   길이 게임을 안 하는 것이 된다. 도달 스테이지가 곧 등급인 보상이라
    ///   (MonsterGearRewardRule) 바닥 한 칸은 비워 둔다.
    /// </summary>
    const int GearBoxFromStage = 2;

    void GrantGearBox()
    {
        if (_stageNumber < GearBoxFromStage)
        {
            // ⚠ 난이도 해금 연출은 그래도 열어 준다 — 상자와 별개의 사건이다
            //   (상자가 안 뜨는 다른 경우들과 같은 처리다. 아래 참고)
            DifficultyUnlockPopup.ShowPending();
            return;
        }

        // 상자 수 = 난이도(쉬움 1 ~ 불지옥 5, DifficultyConfig.GearBoxes)
        //        + 유물 '대장간의 기억'.
        // ⚠ 지급은 전부 먼저 하고, 연출은 **한 창이** 맡는다 (GearBoxPopup.SetupMany)
        //   팝업을 상자마다 쌓으면 서로를 덮어 무엇을 받았는지 안 보인다.
        //   하나면 크게 하나, 둘 이상이면 나란히 놓고 한 번에 연다 — 창이 고른다.
        int boxes = (DifficultyConfig.CurrentTier()?.GearBoxes ?? 1)
                  + RelicTreeApplier.GetSystemInt(RelicSystemEffect.GearBoxBonus);

        var gears = new List<MonsterGearData>(boxes);
        for (int i = 0; i < boxes; i++)
        {
            MonsterGearData got = MonsterGearRewardRule.GrantForStage(_stageNumber);
            if (got == null) continue;

            gears.Add(got);
            Debug.Log($"[RunBootstrap] 보상 상자 {i + 1}/{boxes} — {GradeStyle.GetLabel(got.Grade)} {got.DisplayName}");
        }

        // ⚠ 난이도 해금 연출은 **상자를 닫은 뒤**다 (사용자 지시, 2026-09-11)
        //   상자가 안 뜨는 경우(장비 없음·프리팹 없음)에는 곧장 연다.
        if (gears.Count == 0) { DifficultyUnlockPopup.ShowPending(); return; }

        var popup = PopupManager.Instance?.Open<GearBoxPopup>(PopupType.GearBox);
        if (popup == null)
        {
            Debug.LogWarning("[RunBootstrap] GearBoxPopup 을 열지 못했습니다 — " +
                             "장비는 이미 지급됐습니다(도감에서 확인).");
            DifficultyUnlockPopup.ShowPending();
            return;
        }

        popup.SetupMany(gears, _stageNumber);
        popup.SetOnClose(() => DifficultyUnlockPopup.ShowPending());
    }

    /// <summary>
    /// 소환사 선택 화면으로 돌아간다.
    ///
    /// ⚠ 요청을 비워야 한다
    ///   남아 있으면 선택 화면이 뜨자마자 RunBootstrap 이 다시 시작해 버린다.
    ///   (다만 이 컴포넌트의 Start 는 이미 끝났으므로 실제로는 다음 진입 때 문제가 된다)
    ///
    /// 로비가 없으면(인게임 씬만 연 경우) 돌아갈 곳이 없다 — 로그만 남긴다.
    /// </summary>
    void ReturnToSelect()
    {
        RunLaunch.Clear();

        if (!RunLaunch.LobbyPresent)
        {
            Debug.Log("[RunBootstrap] 로비가 없어 선택 화면으로 돌아가지 않습니다 " +
                      "(인게임 씬 단독 실행).");
            return;
        }

        LobbyManager.Instance.ReturnFromSummonRun();
    }

    /// <summary>
    /// 런의 첫 스테이지를 준비한다 — 적은 아직 세우지 않는다.
    ///
    /// ⚠ 필드를 반드시 비운다 (despawnUnits 기본값 true)
    ///   두 번째 런이 시작될 때 지난 런의 소환사·시체가 남아 있으면,
    ///   생존 카운트가 어긋나 승패 판정이 통째로 틀어진다.
    ///   스테이지 **사이**의 준비(AdvanceStage)는 반대로 비우지 않는다 —
    ///   그쪽은 소환사가 계속 서 있어야 한다.
    /// </summary>
    void PrepareStage()
    {
        var bm = BattleManager.Instance;
        if (bm == null)
        {
            Debug.LogError("[RunBootstrap] BattleManager 가 씬에 없습니다.");
            return;
        }

        SummonerPerkRuntime.ResetRunState();
        bm.PrepareStage(new NormalMode(_stageNumber));
    }

    /// <summary>지금 판이 허들(보스·엘리트)인가.</summary>
    bool IsHurdle => RunStageKindRule.IsHurdle(_stageKind);

    // ── ① 소환사 확정 ───────────────────────────────────────

    /// <summary>
    /// 이 런에 쓸 소환사를 정한다. 우선순위가 셋이다.
    ///   ① 선택 화면이 요청한 캐릭터 (RunLaunch)
    ///   ② 인스펙터에 걸어 둔 캐릭터
    ///   ③ 카드 목록의 첫 번째 — 인게임 씬만 열어 확인할 때의 경로
    ///
    /// ⚠ ①이 ②를 덮는다
    ///   씬에 걸린 기본값이 플레이어의 선택을 이기면, 고른 캐릭터가 무시되는데
    ///   화면에는 아무 표시도 없다 — 가장 찾기 어려운 종류의 버그다.
    /// </summary>
    /// <param name="savedId">
    /// 이어하기로 되살릴 소환사 ID. 비우면 선택 화면의 요청(RunLaunch)을 본다.
    /// </param>
    void ResolveSummoner(string savedId = null)
    {
        CardCatalog catalog = CardCatalog.Current;

        // ⚠ 이어하기는 저장된 ID 가 절대적이다
        //   손에 든 카드가 그 캐릭터의 런에서 나온 것이라, 다른 소환사를
        //   세우면 친화 종족과 개성이 통째로 어긋난 판이 된다.
        //   못 찾으면 자동 선택으로 흘리지 않고 소리 내어 실패한다.
        if (!string.IsNullOrEmpty(savedId) && catalog != null)
        {
            SummonerData saved = catalog.GetSummoner(savedId);
            if (saved != null)
            {
                _summoner = saved;
                return;
            }

            Debug.LogError($"[RunBootstrap] 이어하기 대상 소환사 '{savedId}' 를 " +
                           "찾지 못했습니다 — 카드 목록이 바뀐 것 같습니다.");
        }

        string requested = RunLaunch.SummonerId;

        if (!string.IsNullOrEmpty(requested) && catalog != null)
        {
            SummonerData chosen = catalog.GetSummoner(requested);
            if (chosen != null)
            {
                _summoner = chosen;
                Debug.Log($"[RunBootstrap] 소환사 — {_summoner.DisplayName} (선택 화면)");
                return;
            }

            Debug.LogWarning($"[RunBootstrap] 요청된 소환사 '{requested}' 를 " +
                             "카드 목록에서 찾지 못했습니다 — 기본값으로 진행합니다.");
        }

        if (_summoner != null) return;

        if (catalog == null || catalog.Summoners.Count == 0)
        {
            Debug.LogError("[RunBootstrap] 소환사를 찾지 못했습니다. " +
                           "Tools > Project K > 데이터 생성 > 소환사 → 카드 목록 순서로 실행하세요.");
            return;
        }

        _summoner = catalog.Summoners[0];
        Debug.Log($"[RunBootstrap] 소환사 자동 선택 — {_summoner.DisplayName}");
    }

    // ── ② 시작 카드 ─────────────────────────────────────────

    /// <summary>
    /// 카드 바를 소환사의 붙박이 카드로 채운다.
    ///
    /// ⚠ 이미 차 있으면 손대지 않는다
    ///   런 도중에 씬을 다시 열면(에디터) 주워 모은 카드가 살아 있다.
    ///   여기서 덮어쓰면 그걸 조용히 날린다.
    /// </summary>
    void BuildStarterDeck()
    {
        var deck  = UserDataManager.Instance.Get<SummonDeckData>();
        var perks = UserDataManager.Instance.Get<RunPerkData>();

        // 특성 '확장 편성' 이 칸을 늘린다.
        // ⚠ ResizeTo 는 줄어들 때 뒤 칸을 자른다 — 특성을 잃는 경로가 생기면
        //   그때 카드가 사라진다. 지금은 특성이 사라지는 길이 런 종료뿐이라 안전하다.
        // ⚠ 덱 칸은 **특성만** 늘린다 (사용자 확정, 2026-09-10)
        //   유물로 칸을 영구히 얹으면 특성 '확장 편성'(+2)이 쓸모를 잃는다 —
        //   그 특성의 존재 이유가 통째로 "이미 갖고 있는 것" 이 된다.
        // 칸 수의 정본은 RunPerkRule.DeckSlotsFor (소환사 + 유물 '전열 확장' + 특성, 상한 8).
        int slots = RunPerkRule.DeckSlotsFor(_summoner);

        bool inProgress = false;
        for (int i = 0; i < deck.SlotCount; i++)
            if (!deck.GetSlot(i).IsEmpty) { inProgress = true; break; }

        // ⚠ 이어하기는 칸을 **늘리기만** 한다 (2026-09-11)
        //   런 도중의 칸 변화(확장 편성·봉인된 칸)는 RunPerkData.Add 가 그 자리에서 했고
        //   덱 세이브에 그대로 남아 있다. 여기서 다시 줄이면 ResizeTo 가 뒤 칸의 **카드를 자른다.**
        //   늘리는 쪽은 옛 세이브(칸을 런 시작에서만 늘리던 시절)를 위한 것이다.
        if (!inProgress || deck.SlotCount < slots) deck.ResizeTo(slots);

        if (inProgress) return;   // 이미 진행 중인 런이다

        var codex = UserDataManager.Instance.Get<MonsterCodexData>();

        foreach (MonsterSpeciesData species in _summoner.StarterMonsters)
        {
            if (species == null) continue;

            deck.PlaceStarter(SummonKind.Monster, species.Id);

            // 도감은 기록만 한다 — 편성 풀이 아니다. 품질은 여기서 정해진다.
            codex.Unlock(species.Id, UnitGrade.Normal);
        }

        foreach (SkillCardData skill in _summoner.StarterSkills)
        {
            if (skill == null) continue;
            deck.PlaceStarter(SummonKind.Skill, skill.Id);
        }

        Debug.Log($"[RunBootstrap] 시작 카드 배치 — 몬스터 {_summoner.StarterMonsters.Length}종 · " +
                  $"스킬 {_summoner.StarterSkills.Length}종");
    }

    // ── ③ 마나 지급 ─────────────────────────────────────────

    void GrantMana()
    {
        var mana = UserDataManager.Instance.Get<SummonManaData>();
        mana.GrantForRun(RunPerkRule.MaxManaFor(_summoner));

        Debug.Log($"[RunBootstrap] 마나 그릇 {_summoner.MaxMana:0.#} " +
                  $"(지능 {_summoner.Intelligence:0.#}) — 가득 채워 시작합니다. " +
                  $"스테이지마다 지능 {_summoner.Intelligence:0.#} + 잔량의 " +
                  $"{_summoner.ManaRegenHoldRatio:P0} 회복 " +
                  $"(가득 찼을 때 {_summoner.ManaRegenAtFull:0.#})");
    }

    // ── ④ 소환사 스폰 ───────────────────────────────────────

    void SpawnSummoner()
    {
        Vector3 at = _field.SummonerPoint.position;

        GameObject go = PoolController.Instance.Spawn(
            PoolType.Unit, _summoner.PoolKey, at, Quaternion.identity);

        if (go == null)
        {
            Debug.LogError($"[RunBootstrap] 소환사 스폰 실패 — 풀 키 '{_summoner.PoolKey}'.\n" +
                           "PoolController 에서 'Load Prefabs From Folder' 를 눌렀는지 확인하세요.");
            return;
        }

        if (!go.TryGetComponent<SummonerRuntimeBridge>(out var bridge))
        {
            Debug.LogError($"[RunBootstrap] '{_summoner.PoolKey}' 프리팹에 SummonerRuntimeBridge 가 없습니다.");
            return;
        }

        bridge.Initialize(_summoner);

        BattleManager.Instance?.OnUnitSpawned(Faction.Monster);
        BattleManager.Instance?.NotifyAlliesReady();

        Debug.Log($"[RunBootstrap] 소환사 배치 — {_summoner.DisplayName} " +
                  $"(HP {_summoner.MaxHp} · 공격 {_summoner.Attack} · 소환력 {_summoner.SummonPower})");
    }

    // ── ⑤ 스테이지 대기 ─────────────────────────────────────

    void EnterStageReady()
    {
        var director = StageLoopDirector.Instance;
        if (director == null)
        {
            Debug.LogError("[RunBootstrap] StageLoopDirector 가 씬에 없습니다.");
            return;
        }

        // 스테이지 단위 특성 상태를 되돌린다 ('첫 소환 무료' 등).
        //
        // ⚠ '시작' 을 누를 때가 아니라 **대기에 들어설 때** 되돌려야 한다
        //   소환은 대기 중에도 열려 있다. 시작 시점에 되돌리면 대기에서 한 번,
        //   전투에서 또 한 번 — 한 스테이지에 무료 소환이 두 번 나간다.
        UserDataManager.Instance.Get<RunPerkData>().OnStageBegin();

        director.EnterStageReady(_stageNumber, _stageKind);

        Debug.Log($"[RunBootstrap] 스테이지 {_stageNumber} 대기 — 몬스터를 미리 소환하고 시작하세요.");
    }

#if UNITY_EDITOR
    /// <summary>
    /// 치트 — 대기 중인 스테이지 번호를 바꾼다 (CheatEditorWindow). 대기(StageReady) 중에만 된다.
    ///
    /// ⚠ 정상 경로를 그대로 탄다 — 번호·성격(보스)을 다시 정하고 EnterStageReady 를 다시 부른다.
    ///   건너뛴 판의 보상·기록(환생 포인트 근거)은 주지 않는다.
    /// </summary>
    public bool CheatJumpToStage(int stageNumber)
    {
        var director = StageLoopDirector.Instance;
        if (director == null || !director.IsStageReady) return false;

        _stageNumber = Mathf.Max(1, stageNumber);
        _stageKind   = RunStageKindRule.Of(_stageNumber);
        UserDataManager.Instance.Get<SummonRunData>().SetStage(_stageNumber);

        EnterStageReady();
        return true;
    }
#endif

    /// <summary>플레이어가 "시작" 을 눌렀다 — 용사 편성을 오른쪽 밖에서 들여보낸다.</summary>
    void HandleStageStart(int stageNumber)
    {
        float maxStage  = Mathf.Max(1, GameplayConfig.Current.MaxStage);
        float stageBias = Mathf.Clamp01(stageNumber / maxStage);

        List<SpawnEntry> heroes = StageLoopDirector.Instance.BuildHeroDeployment(stageBias);

        _heroSpawner.Spawn(heroes);

        // ⚠ 장수 한 기당 하나만 센다 — 휘하 병사는 스폰한 쪽이 각자 센다
        //   병사만 나오는 편성(SoldiersOnly)에는 장수가 없으므로 여기서 세지 않는다.
        //   세면 잡을 대상이 없는 적이 하나 남아 스테이지가 영영 끝나지 않는다.
        foreach (var entry in heroes)
        {
            if (entry.SoldiersOnly) continue;
            BattleManager.Instance?.OnUnitSpawned(Faction.Hero);
        }

        // ⚠ 상태를 직접 대입하지 않는다 — BeginStage 가 IsWaveRunning·이벤트·
        //   전투 시작 트리거까지 함께 맞물려 돌린다.
        BattleManager.Instance?.BeginStage();

        Debug.Log($"[RunBootstrap] 스테이지 {stageNumber} 시작 — 용사 {heroes.Count}부대 진입");
    }

    // ── 승리 감시 ────────────────────────────────────────────

    /// <summary>
    /// 적이 전멸한 뒤 승리를 선언하기까지 두는 유예(초).
    ///
    /// ⚠ 0 으로 두면 생존자가 대기열로 돌아가지 못한다
    ///   SummonController 는 "InWave 인데 적이 0" 인 프레임을 봐야 살아남은
    ///   몬스터를 거둔다(MonsterLineReturner). 적이 죽은 그 프레임에 곧장 상태를
    ///   BattleVictory 로 바꾸면 그 조건이 영영 성립하지 않는다.
    ///   사망 연출이 끝나는 시간이기도 해서 그림상으로도 이 정도는 필요하다.
    /// </summary>
    const float VictoryGrace = 0.8f;

    float _clearedAt = -1f;

    /// <summary>
    /// 최종 스테이지에서 지금까지 잡은 무한 보스 수 = 다음 보스의 번호.
    ///
    /// ⚠ 저장하지 않는다 — 이어하기로 최종 스테이지에 돌아오면 0번부터다.
    ///   무한 보스는 "이번에 몇 기까지 잡았나" 지 누적 기록이 아니다.
    /// </summary>
    int _endlessBossIndex;

    /// <summary>
    /// 적이 다 죽었는지 지켜본다.
    ///
    /// ⚠ 승리 판정이 BattleManager 안에 없는 이유
    ///   그쪽 EvaluateBattleState 는 유닛이 죽은 **그 순간** 불린다. 유예를
    ///   주려면 시간을 재야 하는데, 그건 이벤트 핸들러가 아니라 루프를 가진
    ///   쪽(여기)의 일이다. 자세한 사정은 EvaluateBattleState 주석 참고.
    /// </summary>
    void Update()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.Context == null) return;

        if (bm.Context.State != BattleState.InWave)
        {
            _clearedAt = -1f;
            return;
        }

        if (bm.Context.AliveEnemyCount > 0)
        {
            _clearedAt = -1f;
            return;
        }

        // ⚠ 아직 아무도 안 나온 판은 '전멸' 이 아니다
        //   용사 편성은 코루틴이 부대마다 한 프레임씩 나눠 세운다. 그 사이는
        //   InWave 인데 적이 0 이다. 이걸 전멸로 읽으면 판이 시작하자마자
        //   끝나 버린다 — 병사만 나오는 초반 스테이지는 장수 한 기를 먼저
        //   세는 보정조차 없어 더 위험하다.
        //   StageEnemyTotal 은 적이 하나라도 서면 올라가므로 그 표식을 쓴다.
        if (bm.Context.StageEnemyTotal <= 0) return;

        if (_clearedAt < 0f)
        {
            _clearedAt = Time.time;
            return;
        }

        if (Time.time - _clearedAt < VictoryGrace) return;

        _clearedAt = -1f;

        // ── 최종 스테이지는 깰 수 없다 — 다음 보스가 온다 ──
        //   정본은 EndlessBossRule. 승리를 선언하지 않으므로 카드 보상도
        //   갈림길도 열리지 않는다. 이 판은 마왕성이 뚫려야 끝난다.
        if (EndlessBossRule.IsEndlessStage(_stageNumber))
        {
            SpawnNextEndlessBoss();
            return;
        }

        bm.DeclareVictory();   // → OnVictory → HandleVictory
    }

    /// <summary>
    /// 다음 무한 보스를 세운다.
    ///
    /// ⚠ 세는 것(OnUnitSpawned)이 **먼저**다
    ///   실제 등장은 EndlessBossRule.RespawnDelay 뒤지만, 그 사이에도
    ///   AliveEnemyCount 는 1 이어야 한다. 0 인 프레임이 남으면
    ///   SummonController 가 그 틈에 생존자를 대기열로 거둬 필드가 빈다.
    /// </summary>
    void SpawnNextEndlessBoss()
    {
        _endlessBossIndex++;

        BattleManager.Instance?.OnUnitSpawned(Faction.Hero);
        _heroSpawner.Spawn(new List<SpawnEntry>
        {
            EndlessBossRule.BuildEntry(_stageNumber, _endlessBossIndex),
        });

        Debug.Log($"[RunBootstrap] 무한 보스 {_endlessBossIndex + 1}기째 — " +
                  $"스텟 ×{EndlessBossRule.StatMultiplierFor(_endlessBossIndex):0.###}");
    }

    // ── 승리 → 카드 보상 → 다음 스테이지 ────────────────────

    /// <summary>
    /// 스테이지를 깼다. 카드 3택을 띄우고, 고르면 다음 스테이지 대기로 넘어간다.
    ///
    /// ⚠ 보상 화면이 없으면 그냥 넘어간다
    ///   HUD 를 다시 굽지 않은 프로젝트에서도 런이 멈추지 않아야 한다.
    ///   (에디터에서 씬만 열어 확인하는 경우가 흔하다)
    /// </summary>
    /// <summary>
    /// 스테이지를 깼다 — 카드 3택 팝업을 띄운다.
    ///
    /// ⚠ 승리 팝업은 띄우지 않는다 (2026-08-27)
    ///   원작은 여기서 결과창을 한 번 보여 준 뒤 어빌리티 선택으로 넘어갔다.
    ///   그 사이 화면이 "확인" 한 번만 받고 사라지는 낭비라, 곧장 카드로 간다.
    ///   방금 판의 기여는 카드 팝업 상단의 '전투 통계' 버튼으로 본다.
    /// </summary>
    void HandleVictory()
    {
        List<CardRewardOption> choices =
            CardRewardPicker.Pick(_summoner, UserDataManager.Instance.Get<SummonDeckData>());

        var popup = choices.Count > 0 && PopupManager.Instance != null
            ? PopupManager.Instance.Open<CardSelectPopup>(PopupType.CardSelect)
            : null;

        if (popup == null)
        {
            if (choices.Count > 0)
                Debug.LogWarning("[RunBootstrap] CardSelectPopup 을 열지 못했습니다 — " +
                                 "보상 없이 다음 스테이지로 넘어갑니다. " +
                                 "Tools > Project K > 프리팹 생성 > 팝업 > 카드 선택 을 실행하세요.");

            // ⚠ 카드를 못 줬다고 갈림길·특성까지 건너뛰지 않는다
            //   AdvanceStage 로 바로 가면 그 판만 다음 판이 저절로 정해진다.
            AfterCardReward();
            return;
        }

        popup.Setup(choices, OnRewardPicked);
    }

    /// <summary>
    /// 보상 카드를 골랐다.
    ///
    /// 세 갈래로 나뉜다.
    ///   신규   → 카드 바에 등록 + 도감 기록
    ///   중복   → 그 카드 레벨업
    ///   만렙   → **진화/융합 갈림길**을 연다 (레벨은 더 오르지 않는다)
    ///
    /// ⚠ 만렙이어도 장수는 올려 둔다
    ///   진화·융합을 고르지 못하는 카드(상위 종족도 재료도 없음)를 골랐을 때
    ///   아무 일도 안 일어나면 보상 한 번을 통째로 버린 셈이 된다.
    ///   장수는 나중에 규칙이 늘 때 쓸 자산이기도 하다.
    /// </summary>
    void OnRewardPicked(CardRewardOption picked)
    {
        // ── 시너지 강화 — 덱 칸을 먹지 않는다 ──
        //   런 상태로만 산다(MonsterSynergyRule.BoostedTag). 8칸이 이미 빠듯해서
        //   칸을 먹으면 "카운트 +1 을 얻고 칸을 잃는" 제로섬이 된다.
        if (picked.IsSynergyBoost)
        {
            MonsterSynergyRule.ApplyBoost(picked.BoostTag);
            UserDataManager.Instance.Get<SummonRunData>().SetBoostTag(picked.BoostTag);

            Debug.Log($"[RunBootstrap] 시너지 강화 — {MonsterSynergyRule.NameOf(picked.BoostTag)} " +
                      $"카운트 +{MonsterSynergyRule.BoostCount} · 공/체 " +
                      $"+{MonsterSynergyRule.BoostStatBonus * 100f:0}%");

            // ⚠ AdvanceStage 로 곧장 가지 않는다 (2026-09-06)
            //   여기서 넘어가면 **갈림길과 특성이 통째로 건너뛰어진다.**
            //   시너지 강화를 고른 판만 다음 판이 저절로 정해져 버렸다.
            //   보상 뒤 흐름의 문은 AfterCardReward 하나다.
            AfterCardReward();
            return;
        }

        var deck  = UserDataManager.Instance.Get<SummonDeckData>();
        var codex = UserDataManager.Instance.Get<MonsterCodexData>();

        // ── 진화 카드 — 새 칸을 먹지 않고 **베이스 카드를 바꾼다** ──
        //   (사용자 지시, 2026-09-12 — CardRewardPicker.BuildPool 의 진화 갈래 주석 참고)
        //   ⚠ Acquire 로 내려보내지 말 것. 그러면 베이스를 남긴 채 진화체가
        //     새 카드로 한 칸 더 들어온다 — 이 규칙을 만든 이유와 정반대다.
        if (picked.IsEvolve)
        {
            int at = deck.IndexOf(picked.EvolveFromId);
            MonsterSpeciesData target = CardCatalog.Current?.GetMonster(picked.Id);

            if (deck.EvolveTo(at, target) != null)
            {
                // ⚠ 도감 해금은 늘 부르는 쪽의 몫이다 (CardEvolveUI.HandleEvolve 와 같은 규칙)
                codex.Unlock(picked.Id, UnitGrade.Normal);
                UserDataManager.Instance.RequestSave();

                Debug.Log($"[RunBootstrap] 진화 — {picked.EvolveFromId} → {picked.Id} (Lv1 로 시작)");
            }
            else
            {
                Debug.LogWarning($"[RunBootstrap] 진화 실패 — {picked.EvolveFromId} → {picked.Id}. " +
                                 "후보를 고를 때와 조건이 갈렸습니다 (CardEvolution.CanEvolveTo).");
            }

            AfterCardReward();
            return;
        }

        int level = deck.Acquire(picked.Kind, picked.Id);

        // 도감은 "만나 봤다" 를 기록한다. 품질은 최초 1회만 정해진다.
        if (picked.Kind == SummonKind.Monster)
            codex.Unlock(picked.Id, UnitGrade.Normal);

        UserDataManager.Instance.RequestSave();

        Debug.Log($"[RunBootstrap] 카드 획득 — {picked.Id} → Lv.{level}" +
                  (picked.IsMaxed     ? " (만렙 · 진화 갈림길)"
                 : picked.IsDuplicate ? " (중복 · 레벨업)"
                                      : " (신규)"));

        if (TryOpenEvolve(picked, deck)) return;

        AfterCardReward();
    }

    /// <summary>
    /// 카드 보상이 끝났다 — 허들을 깼다면 <b>특성</b>이 한 번 더 나온다.
    ///
    ///   일반   → 갈림길 2택 (시설)
    ///   엘리트 → 특성 1택 (무작위로 한 장만 뜬다)
    ///   보스   → 특성 3택
    ///
    /// ■ 왜 허들에서만인가
    ///   카드 3택은 매 스테이지 뜬다. 특성까지 매 판 주면 판이 끝날 때마다
    ///   고를 것이 둘이라 어느 쪽도 무게가 없어진다. 특성은 수가 적고 하나하나가
    ///   강한 축이라(RunPerk.cs), 리듬을 카드와 어긋나게 둔다.
    ///
    /// ⚠ 방금 깬 스테이지 번호로 판정한다
    ///   _stageNumber 는 AdvanceStage 안에서야 올라간다. 여기서 세면
    ///   허들을 깬 판이 정확히 잡힌다.
    /// </summary>
    void AfterCardReward()
    {
        RunStageKind kind = _stageKind;

        // ── 일반 판 — 특성 보상이 없다. 곧장 다음 판을 정한다 ──
        if (kind == RunStageKind.Normal) { ChooseNextStage(); return; }

        var data = UserDataManager.Instance.Get<RunPerkData>();

        // ── 특성 — 엘리트는 1택, 보스는 3택 ──────────────────
        //   같은 화면(ChoicePopup)을 쓰되 **줄 수**로 무게를 가른다.
        //   ⚠ 엘리트 쪽 수를 늘리는 업그레이드가 생기면 이 값만 바꾸면 된다.
        int want = kind == RunStageKind.Elite
                 ? RunPerkPicker.EliteChoiceCount
                 : RunPerkPicker.ChoiceCount;

        // 유물 '전승의 대가' — 엘리트·보스 **양쪽 모두** 늘린다.
        // ⚠ 보스에만 주면 엘리트 1택이 그대로 남아 "유물을 찍었는데 안 변한다"
        //   가 대부분의 판에서 일어난다 (허들의 다수가 엘리트다).
        // ⚠ 줄 수는 ChoicePopup.MaxRows(8) 를 넘길 수 없다.
        want = Mathf.Min(want + RelicTreeApplier.GetSystemInt(RelicSystemEffect.PerkChoiceCount),
                         ChoicePopup.MaxRows);

        List<RunPerk> choices = RunPerkPicker.Pick(data, want);

        // 전부 모았으면 띄울 것이 없다 — 조용히 넘어간다.
        if (choices.Count == 0) { ChooseNextStage(); return; }

        var popup = PopupManager.Instance?.Open<ChoicePopup>(PopupType.Choice);

        if (popup == null)
        {
            Debug.LogWarning("[RunBootstrap] ChoicePopup 을 열지 못했습니다 — " +
                             "특성 없이 다음 스테이지로 넘어갑니다. " +
                             "Tools > Project K > 프리팹 생성 > 팝업 > 선택 목록 을 실행하고 " +
                             "PopupManager > Load Popup Prefabs 를 누르세요.");
            ChooseNextStage();
            return;
        }

        var entries = new List<ChoicePopup.Entry>(choices.Count);
        foreach (RunPerk perk in choices)
            entries.Add(new ChoicePopup.Entry(
                perk.ToKorean(), perk.Describe(), enabled: true,
                // ⚠ 아이콘은 아틀라스에서 키로 찾는다 (RunPerkIconKey)
                //   30종 중 셋이 굴러 나오므로 Creator 가 미리 박아 둘 수 없다.
                //   못 찾으면 null 이고 줄은 글자만으로 뜬다 — 화면이 멈추지는 않는다.
                icon: SpriteManager.Instance?.Get(perk.IconKey())));

        // ⚠ 보상 화면은 못 닫는다 — 닫으면 특성 한 장이 조용히 사라진다.
        popup.Setup(kind == RunStageKind.Boss ? "특성을 하나 고르세요" : "특성을 얻었다",
                    entries,
                    i =>
                    {
                        data.Add(choices[i]);
                        UserDataManager.Instance.RequestSave();
                        ChooseNextStage();
                    },
                    blockClose: true);
    }

    /// <summary>
    /// 다음 판이 무엇이 될지 정한다.
    ///
    ///   보스 차례(5의 배수) → <b>갈림길 없음</b>. 피할 수 없는 관문이다.
    ///   그 외                → 갈림길 2택. 고른 것이 곧 다음 판이다.
    ///
    /// ⚠ 시설을 골라도 스테이지 번호는 올라간다
    ///   그 판에 전투가 없다는 뜻이고, 그래서 골드 수입 한 판을 잃는다.
    ///   그것이 정비의 대가다 (RunNode.cs 머리 주석 참고).
    /// </summary>
    void ChooseNextStage()
    {
        int next = _stageNumber + 1;

        if (RunStageKindRule.IsBossStage(next))
        {
            _stageKind = RunStageKind.Boss;
            AdvanceStage();
            return;
        }

        // ⚠ 첫 엘리트판도 피할 수 없다 (사용자 지시, 2026-09-13)
        //   보스판과 **같은 규칙**으로 갈림길을 열지 않는다. 열어 두면 '험로' 를
        //   고르지 않는 것으로 이 판을 건너뛸 수 있어, 번호로 못 박은 뜻이 사라진다.
        //   (판정의 정본은 RunStageKindRule.IsFirstEliteStage 하나다)
        if (RunStageKindRule.IsFirstEliteStage(next))
        {
            _stageKind = RunStageKind.Elite;
            AdvanceStage();
            return;
        }

        RunNodeFlow.OpenCrossroad(node =>
        {
            // 시설을 골랐으면 그 판은 전투가 없다 — 일반으로 두고 넘어간다.
            // (시설 화면은 RunNodeFlow 가 이미 다 지나온 뒤다)
            _stageKind = node.IsBattle() ? node.ToStageKind() : RunStageKind.Normal;

            if (!node.IsBattle())
                Debug.Log($"[RunBootstrap] {node.ToKorean()} 를 지났다 — " +
                          $"스테이지 {next} 는 전투 없이 넘어간다");

            AdvanceStage();
        });
    }


    /// <summary>
    /// 만렙 카드였다면 갈림길 화면을 연다. 열었으면 true —
    /// 그때는 화면이 닫힌 뒤에 다음 스테이지로 넘어간다.
    /// </summary>
    bool TryOpenEvolve(in CardRewardOption picked, SummonDeckData deck)
    {
        if (!picked.IsMaxed)                   return false;
        if (picked.Kind != SummonKind.Monster) return false;

        int at = deck.IndexOf(picked.Id);
        if (at < 0) return false;

        // ⚠ 팝업이다 — HUD 자식이 아니다 (사용자 지적, 2026-09-07)
        //   바로 앞 화면인 카드 3택(CardSelectPopup)과 같은 길로 연다.
        //   못 열면 **반드시 말한다** — 한때 조용히 넘어가서 "진화를 눌렀는데
        //   아무 일도 안 난다" 가 됐고, 원인이 화면 밖(프리팹)에 있어 짚을 수가 없었다.
        var popup = PopupManager.Instance?.Open<CardEvolveUI>(PopupType.CardEvolve);

        if (popup == null)
        {
            Debug.LogError(
                "[RunBootstrap] 진화·융합 팝업을 열지 못했습니다 — 만렙 카드를 골라도 "
                + "아무 일도 일어나지 않습니다.\n"
                + "아이콘·텍스처 > 종족 패시브 아이콘 → 프리팹 생성 > 팝업 > 진화·융합 → "
                + "PopupManager 의 [Load Popup Prefabs] 순서로 실행하세요.");
            return false;
        }

        // 진화도 융합도 불가능한 카드면 띄울 것이 없다 — 곧장 닫고 흐름을 잇는다.
        if (!popup.Setup(at, AfterCardReward)) { popup.Close(); return false; }

        return true;
    }

    /// <summary>
    /// 다음 스테이지 대기로 넘어간다.
    ///
    /// ⚠ 소환사는 절대 반납하지 않는다
    ///   소환사의 HP 가 곧 마왕성 HP 다. 판을 비우며 같이 치웠다가 다시 세우면
    ///   **매 스테이지마다 성이 완전 회복**되어 런이 성립하지 않는다.
    ///   그래서 BattleArena.Close() 를 쓰지 않는다 — 그건 유닛을 전부 반납한다.
    ///
    /// 살아남은 몬스터는 이미 MonsterLineReturner 가 대기열로 거둬 갔으므로,
    /// 여기서 치울 것은 화면에 남은 이펙트·발사체뿐이다.
    /// </summary>
    void AdvanceStage()
    {
        // ⚠ 깬 스테이지를 남긴다 — 환생 포인트의 유일한 근거다
        //   ReincarnationData.PreviewPoints 도, 실제 지급도
        //   StageProgressData.ClearedNormalStages 를 본다. 여기서 적지 않으면
        //   런을 아무리 길게 끌어도 결산에서 **+0 pt** 가 뜬다.
        //   방금 깬 것은 올리기 전 번호다 — 아래 ++ 보다 먼저 적어야 한다.
        UserDataManager.Instance?.Get<StageProgressData>()
                       ?.RecordClear(_stageNumber);

        // ⚠ 영구 기록은 따로 남긴다
        //   바로 위 StageProgressData 는 환생 때 지워지는데, 이 게임은
        //   패배가 곧 환생이라 죽는 순간 0 이 된다. 소환사 해금 조건이
        //   보는 것은 지워지지 않는 이쪽이다.
        UserDataManager.Instance?.Get<ReincarnationData>()
                       ?.RecordStageReached(_stageNumber);

        // ── 난이도 돌파 — 이 등급에서 20스테이지 이상을 처음 깼으면 다음 등급이 열린다 ──
        //   ⚠ 여기서는 기록만 한다. 해금 연출은 런이 끝나 보상 상자를 닫은 뒤다
        //     (GrantGearBox → DifficultyUnlockPopup.ShowPending).
        var difficulty = UserDataManager.Instance.Get<DifficultyData>();
        if (difficulty.TryBreakthrough(difficulty.SelectedTier, _stageNumber))
        {
            UserDataManager.Instance.RequestSave();
            Debug.Log($"[RunBootstrap] 난이도 돌파 — {difficulty.SelectedTier.Label()} " +
                      $"스테이지 {_stageNumber}. 해금 연출은 런 종료 보상 뒤에 뜬다.");
        }

        _stageNumber++;

        // ── 스테이지를 넘긴 대가 ─────────────────────────────
        //   마나 일부가 돌아오고, 과부하가 풀린다.
        //   둘 다 "한 판을 버텨 냈다" 에 대한 보상이라 같은 자리에 둔다.
        var mana = UserDataManager.Instance?.Get<SummonManaData>();

        // ⚠ 그릇을 매번 다시 맞춘다
        //   이어하기로 되살아난 세이브는 Max 가 옛 값일 수 있다.
        //   소환사가 정본이므로 회복 직전에 한 번 맞춰 준다.
        // ⚠ 순서가 중요하다 — 비축 → 그릇 → 회복
        //   비축은 이번 판에 한 방울도 안 썼는지를 보고 그릇을 키운다.
        //   그릇이 커지기 전에 회복하면 늘어난 칸이 그 판에는 비어 있게 된다.
        var perks = UserDataManager.Instance?.Get<RunPerkData>();
        perks?.SettleStageEnd();

        // 소환사 개성 '결정화'(결정술사) — 회복 **전** 잔량을 본다. 그릇 맞추기(SetMax) 앞이어야 한다.
        SummonerPerkRuntime.SettleCrystal(_summoner);

        // 종족 패시브 '마나 방출' — 한 판 상한을 되돌린다.
        SpeciesPassiveRuntime.ResetStage();

        mana?.SetMax(RunPerkRule.MaxManaFor(_summoner));

        // ⚠ 회복량은 **남은 잔량을 보고** 정해진다 (지능 + 잔량 × 10%)
        //   그래서 SetMax 뒤, 회복을 얹기 전의 잔량으로 계산해야 한다.
        //   Max 를 기준으로 재면 다 쓴 판과 아낀 판이 같은 양을 받는다.
        if (mana != null)
        {
            // ⚠ 셈은 ManaRegenRule 한 곳이 한다 (2026-09-07)
            //   하단 카드 바가 "다음 판에 얼마 돌아오나" 를 미리 띄운다.
            //   여기서 따로 계산하면 화면과 실제가 갈린다.
            mana.RegenForStage(ManaRegenRule.RawFor(_summoner, mana.Current));
        }

        SummonCostRule.Reset();

        // ── 유물 '재건' — 판을 넘길 때마다 성이 조금씩 아문다 ──
        //
        //  ⚠ 야영지 회복과 다른 물건이다 — 그쪽은 갈림길에서 **골라야** 받는다.
        //    이건 고르지 않아도 붙는 사면(赦免)이라, 긴 런에서 조금씩 새어 나간
        //    체력을 메운다. 그릇을 넘지 못하므로(Heal 이 자른다) 저절로 멈춘다.
        //  ⚠ 자리는 마나 회복 옆이다 — 둘 다 "한 판을 버텨 냈다" 의 대가다.
        int coreRegen = RelicTreeApplier.GetSystemInt(RelicSystemEffect.CoreRegenPerStage);
        if (coreRegen > 0)
            UserDataManager.Instance?.Get<RunCoreData>()?.Heal(coreRegen);

        // 특성 '저주받은 금화' — 판을 넘길 때마다 성이 깎인다.
        //   ⚠ Pay 라 1 아래로는 안 내려간다 — 저주로 런이 끝나지 않는다. 재건 회복 **뒤**다.
        if (RunPerkRule.Has(RunPerk.CursedGold))
            UserDataManager.Instance.Get<RunCoreData>().Pay(RunPerkRule.CursedGoldCoreCost);

        // ⚠ 여기가 이어하기의 저장 지점이다
        //   카드 3택을 끝내고 다음 판에 들어서는 순간이라, 카드·마나·특성이
        //   모두 확정돼 있다. 이 시점을 적어 두면 앱이 어디서 죽든
        //   "다음 스테이지 대기" 상태로 정확히 되살아난다.
        var run = UserDataManager.Instance?.Get<SummonRunData>();
        run?.SetStage(_stageNumber);
        UserDataManager.Instance?.RequestSave();

        // 수명 타이머를 기다리면 다음 스테이지에서 터진다 — 지금 회수한다.
        PoolController.Instance?.DespawnAll(PoolType.Effect);
        PoolController.Instance?.DespawnAll(PoolType.Projectile);

        var bm = BattleManager.Instance;
        if (bm != null)
        {
            // despawnUnits:false — 소환사를 그 자리에 그대로 둔다.
            // 카운트는 0 에서 다시 시작하므로 살아 있는 소환사를 다시 세어 준다.
            SummonerPerkRuntime.ResetRunState();
            bm.PrepareStage(new NormalMode(_stageNumber), despawnUnits: false);
            bm.OnUnitSpawned(Faction.Monster);
            bm.NotifyAlliesReady();
        }

        EnterStageReady();
    }
}
