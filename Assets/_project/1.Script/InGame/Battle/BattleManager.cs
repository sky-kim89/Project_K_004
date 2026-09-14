using System.Collections;
using System.Collections.Generic;
using Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts;
using Unity.Entities;
using UnityEngine;

// ============================================================
//  BattleManager.cs
//  배틀 전체 흐름 관리 Singleton.
//
//  담당 역할 — 상태·집계·판정만 한다. 진행은 StageLoopDirector 가 주도한다.
//  - 아군/적군 생존 카운트 추적
//  - 패배 판정 (소환사 사망)
//  - 전투 이벤트 발행 (OnVictory / OnDefeat / OnUnitKilled …)
//
//  ⚠ 원작에서 걷어낸 것 — 웨이브 코루틴과 스포너 참조
//    원작은 이 클래스가 AllySpawner / EnemySpawner 를 들고 WaveSetupData 의
//    웨이브를 순서대로 굴렸다. 이 게임은 그 전제가 없다 —
//    아군은 플레이어가 실시간으로 소환하고, 적은 스테이지 편성으로 한 번에 선다.
//
//  외부에서의 사용:
//    BattleManager.Instance.PrepareStage(mode);   ← 스테이지 준비 (StageLoopDirector)
//    BattleManager.Instance.BeginStage();         ← 적 등장 시작
//    BattleManager.Instance.DeclareVictory();     ← 적 정리 완료
//    BattleManager.Instance.OnUnitDead(team);     ← UnitDeathDespawnSystem 호출
// ============================================================

public class BattleManager : Singleton<BattleManager>
{
    // ── 내부 상태 ─────────────────────────────────────────────
    //
    //  ⚠ 스포너 참조가 없다 (원작에서 제거)
    //    원작은 AllySpawner / EnemySpawner 를 직접 들고 웨이브를 굴렸다.
    //    이 게임에서 아군은 플레이어가 실시간으로 소환하고(SummonController),
    //    적은 스테이지 편성으로 한 번에 선다(HeroSpawner). 진행 주도권은
    //    StageLoopDirector 에 있고, 이 클래스는 그 결과를 집계·판정만 한다.

    BattleContext  _context;
    BattleModeBase _mode;

    // ── UI 이벤트 ─────────────────────────────────────────────

    /// <summary>유닛 사망 시 팀 정보를 전달하는 이벤트. TopBarUI 킬 카운터 등에서 구독.</summary>
    public static event System.Action<TeamType> OnUnitKilled;

    /// <summary>웨이브 1 아군(장군) 스폰 완료. InGameManager 가 로딩 팝업 닫기에 사용.</summary>
    public static event System.Action OnAlliesReady;

    /// <summary>
    /// 웨이브가 실제로 시작될 때 발행.
    /// ⚠ OnAlliesReady 와 구분해야 한다 — 출전 대기 화면에서도 아군은 서므로
    ///   그 신호로 전투 연출(카메라 추종 등)을 켜면 대기 중에 카메라를 빼앗긴다.
    /// </summary>
    public static event System.Action OnWavesStarted;

    /// <summary>
    /// 새 전투 준비 직전에 발행 — 아군이 스폰되기 전이다.
    /// ⚠ 씬이 상주하면서 필요해졌다
    ///   예전엔 전투가 끝날 때마다 InGame 씬이 통째로 내려가 HUD 도 새로 만들어졌다.
    ///   지금은 씬이 계속 살아 있으므로, 지난 전투의 흔적을 여기서 스스로 지워야 한다.
    /// </summary>
    public static event System.Action OnBattlePrepared;

    /// <summary>전체 웨이브 클리어(승리). InGameManager 가 결과 팝업 오픈에 사용.</summary>
    public static event System.Action OnVictory;

    /// <summary>아군 전멸(패배). InGameManager 가 결과 팝업 오픈에 사용.</summary>
    public static event System.Action OnDefeat;

    // ── 킬 카운트 ─────────────────────────────────────────────
    int _enemyKillCount;

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>현재 배틀 컨텍스트. TopBarUI 등 UI 에서 웨이브/상태 정보를 읽을 때 사용.</summary>
    public BattleContext Context => _context;

    /// <summary>이번 배틀에서 처치한 적 수.</summary>
    public int EnemyKillCount => _enemyKillCount;

    /// <summary>아군이 전멸했는지 여부. ECS 시스템에서 프레임마다 읽는다.</summary>
    public bool IsAllyDefeated => _context?.IsAllyDefeated ?? false;

    /// <summary>
    /// 웨이브가 실제로 돌고 있는가 (적이 나오는 구간).
    ///
    /// ⚠ 출전 대기와 전투를 가르는 값이다
    ///   대기 중에는 아군만 서 있고 카메라도 옆으로 밀려 있다. 전투용 규칙
    ///   (화면 밖 사망 판정 등)을 그대로 적용하면 방금 세운 부대가 정리돼 버린다.
    /// </summary>
    public bool IsWaveRunning { get; private set; }

    /// <summary>적군이 전멸했는지 여부(웨이브 클리어). ECS 시스템에서 프레임마다 읽는다.</summary>
    public bool IsEnemyDefeated => _context?.IsEnemyClear ?? false;

    // ── 스테이지 API ──────────────────────────────────────────
    //
    //  ⚠ 진행은 StageLoopDirector 가 주도한다
    //    원작은 이 클래스가 웨이브 코루틴을 직접 돌렸다(StartBattle → BattleRoutine
    //    → RunWave). 그 구조는 "WaveSetupData 에 적힌 웨이브를 순서대로 스폰한다" 는
    //    전제 위에 있었는데, 이 게임은 그 전제가 없다 —
    //    아군은 플레이어가 실시간으로 세우고, 적은 스테이지 편성으로 한 번에 선다.
    //
    //    그래서 코루틴을 통째로 걷어내고, 바깥에서 부르는 얇은 API 만 남겼다.
    //    상태를 밖에서 직접 대입(Context.State = ...)하지 말고 이 메서드들을 쓸 것 —
    //    그래야 IsWaveRunning·이벤트·통계가 함께 맞물려 돈다.

    /// <summary>
    /// 새 스테이지를 준비한다. 적은 아직 없고, 플레이어가 몬스터를 미리 세우는 시점이다.
    /// </summary>
    /// <param name="mode">이번 스테이지의 모드</param>
    /// <param name="despawnUnits">
    /// 필드를 비울 것인가.
    ///
    /// ⚠ 스테이지 사이에는 false 여야 한다
    ///   소환사는 런 내내 같은 개체로 서 있어야 한다 — 그 HP 가 곧 마왕성 HP 라
    ///   여기서 반납했다 다시 세우면 **매 스테이지마다 성이 완전 회복된다.**
    ///   런 시작(첫 준비)에서만 true 로 부른다.
    ///   false 로 부르면 생존 카운트가 0 에서 다시 시작하므로,
    ///   부르는 쪽이 살아 있는 유닛만큼 OnUnitSpawned 를 다시 세어 줘야 한다.
    /// </param>
    public void PrepareStage(BattleModeBase mode, bool despawnUnits = true)
    {
        if (_context != null && despawnUnits) DespawnAllUnits();

        // ⚠ 스테이지 번호를 반드시 함께 옮긴다
        //   컨텍스트를 새로 만들면 StageNumber 는 기본값 1 로 돌아간다.
        //   여기서 모드의 번호를 옮기지 않으면 상단 표시가 런 내내 1 에 멈춘다.
        _context        = new BattleContext
        {
            Mode        = mode.Mode,
            StageNumber = mode.StageNumber,
        };
        _mode           = mode;
        _enemyKillCount = 0;

        BattleStatsTracker.Instance?.Reset();

        // ⚠ 카드 통계는 **스테이지 단위**다
        //   스테이지를 깰 때마다 그 판의 기여를 보는 화면이라, 여기서 비운다.
        //   런 누적을 보고 싶어지면 합계를 따로 들어야 한다.
        CardStatsTracker.Instance?.Reset();

        _context.State = BattleState.StageReady;
        IsWaveRunning  = false;

        OnBattlePrepared?.Invoke();
    }

    /// <summary>
    /// 스테이지를 시작한다 — 적이 들어오기 시작한다.
    /// StageLoopDirector 가 용사 편성을 세운 직후에 부른다.
    /// </summary>
    public void BeginStage()
    {
        if (_context == null)
        {
            Debug.LogWarning("[BattleManager] 준비된 스테이지가 없습니다 — PrepareStage 를 먼저 부르세요.");
            return;
        }

        _context.State = BattleState.InWave;
        IsWaveRunning  = true;

        FireBattleStartTriggers();
        OnWavesStarted?.Invoke();
    }

    /// <summary>
    /// 적을 모두 정리했다. 보상 단계로 넘어간다.
    /// </summary>
    public void DeclareVictory()
    {
        if (_context == null) return;
        if (_context.State != BattleState.InWave) return;

        _context.State = BattleState.BattleVictory;
        IsWaveRunning  = false;

        LogBattleStats("승리");
        _mode?.OnBattleVictory();
        OnVictory?.Invoke();
    }

    /// <summary>아군(소환사)이 세워졌음을 알린다 — 로딩 가림막을 걷는 신호.</summary>
    public void NotifyAlliesReady() => OnAlliesReady?.Invoke();

    /// <summary>
    /// 병사처럼 스포너 외부에서 추가 스폰되는 유닛을 카운트에 반영한다.
    /// GeneralRuntimeBridge.SpawnSoldiers() 에서 병사 스폰 성공 시 호출.
    /// </summary>
    public void OnUnitSpawned(TeamType team)
    {
        if (_context == null) return;

        if (team == TeamType.Ally)
        {
            _context.AliveAllyCount++;
        }
        else
        {
            _context.AliveEnemyCount++;

            // 진행도 분모 — 스폰될 때마다 최대치를 따라 올린다.
            // 용사가 휘하 병사를 나중에 끌고 나오므로 한 번에 확정할 수 없다.
            if (_context.AliveEnemyCount > _context.StageEnemyTotal)
                _context.StageEnemyTotal = _context.AliveEnemyCount;
        }
    }

    /// <summary>
    /// UnitDeathDespawnSystem 이 유닛 사망 시 호출.
    /// 생존 카운트를 갱신하고 웨이브 클리어 / 패배 여부를 확인한다.
    /// </summary>
    public void OnUnitDead(TeamType team)
    {
        if (_context == null) return;

        if (team == TeamType.Enemy)
        {
            _context.AliveEnemyCount = Mathf.Max(0, _context.AliveEnemyCount - 1);
            _enemyKillCount++;
        }
        else
        {
            _context.AliveAllyCount  = Mathf.Max(0, _context.AliveAllyCount  - 1);
        }

        OnUnitKilled?.Invoke(team);
        EvaluateBattleState();
    }

    // ── 내부 ─────────────────────────────────────────────────

    // OnBattleStart 트리거를 보유한 모든 장군의 패시브를 일괄 발동한다.
    // 특정 타입을 직접 참조하지 않으므로 새 OnBattleStart 스킬 추가 시 자동 지원.
    void FireBattleStartTriggers()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        var em    = world.EntityManager;
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<BattleGame.Units.GeneralComponent>());

        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entities)
            FireBattleStartFor(em, entity);

        query.Dispose();
    }

    /// <summary>
    /// 한 유닛의 OnBattleStart 패시브를 발동한다.
    ///
    /// ⚠ 소환 몬스터는 "전투 시작" 을 놓친다 — 그래서 이게 public 이다
    ///   위의 일괄 발동은 BeginStage 시점에 **그때 필드에 있던** 유닛만 훑는다.
    ///   소환 몬스터는 전투 도중에 나오므로 그 배열에 없다. 몬스터에게
    ///   "전투 시작" 은 곧 **자기가 필드에 선 순간**이라, MonsterSpawner 가
    ///   스폰 직후 이 메서드를 직접 부른다.
    ///
    ///   이걸 빠뜨리면 OnBattleStart 계열 패시브(SteelBody·ShieldEdge·
    ///   FocusedFire·SwiftAssault …)가 몬스터에게서 **영영 발동하지 않는다.**
    ///   카드 레벨로 열리는 패시브 상당수가 이 계열이라 조용히 죽으면
    ///   "레벨을 올렸는데 아무 일도 안 일어난다" 가 된다.
    /// </summary>
    public static void FireBattleStartFor(EntityManager em, Entity entity)
    {
        if (!em.Exists(entity)) return;

        var ctx = new PassiveTriggerContext { GeneralEntity = entity, EntityManager = em };

        // ── 패시브 스킬 (GeneralPassiveSetComponent) ────────
        if (!em.HasComponent<BattleGame.Units.GeneralPassiveSetComponent>(entity)) return;

        var ps = em.GetComponentData<BattleGame.Units.GeneralPassiveSetComponent>(entity);
        var db = PassiveSkillDatabase.Current;
        if (db == null) return;

        TryFirePassiveOnBattleStart(db.Get(ps.Slot0), ctx, ps.ActiveSlotCount >= 1);
        TryFirePassiveOnBattleStart(db.Get(ps.Slot1), ctx, ps.ActiveSlotCount >= 2);
        TryFirePassiveOnBattleStart(db.Get(ps.Slot2), ctx, ps.ActiveSlotCount >= 3);
    }

    static void TryFirePassiveOnBattleStart(PassiveSkillData data, PassiveTriggerContext ctx, bool active)
    {
        if (!active || data == null) return;
        if (data.TriggerType != PassiveTrigger.OnBattleStart) return;
        data.OnTrigger(ctx);
    }

    /// <summary>
    /// 즉시 패배 처리 — 일시 정지 팝업의 "즉시 환생하기" 가 부른다.
    /// 아군 전멸과 같은 경로를 타므로 결과 팝업·통계·보상 처리가 전부 동일하다.
    /// 이미 승패가 갈렸으면 무시한다 (결과 팝업이 두 번 뜨는 것을 막는다).
    /// </summary>
    public void Surrender()
    {
        if (_context == null) return;
        if (_context.State == BattleState.BattleDefeat ||
            _context.State == BattleState.BattleVictory) return;

        _context.State = BattleState.BattleDefeat;
        Debug.Log($"[BattleManager] 포기 — 스테이지 {_context.StageNumber}");
        LogBattleStats("포기");
        _mode?.OnBattleDefeat();
        OnDefeat?.Invoke();
    }

    /// <summary>패배를 판정한다. 승리 판정은 StageLoopDirector 가 한다.</summary>
    /// <summary>
    /// 패배 판정.
    ///
    /// ⚠ 이 게임의 패배는 "아군 전멸" 이 아니라 "마왕성 함락" 이다
    ///   소환한 몬스터가 전부 죽어도 런은 안 끝난다 — 마나가 남아 있으면 다시
    ///   세우면 되고, 없어도 소환사 본인이 성벽에서 버틴다. 몬스터를 다 뚫은
    ///   용사가 **성벽까지 걸어 들어온 횟수**가 쌓이는 것이 정상적인 패배 경로다.
    ///
    ///   원작 규칙(IsAllyDefeated)을 그대로 두면 몬스터가 잠깐 비는 순간마다
    ///   패배가 떠서 게임이 성립하지 않는다.
    /// </summary>
    void EvaluateBattleState()
    {
        if (_context.State != BattleState.InWave) return;

        // ⚠ 승리는 여기서 선언하지 않는다
        //   적이 0 이 된 그 프레임에 곧장 상태를 바꾸면, 살아남은 몬스터를
        //   대기열로 거둘 틈이 없다 — SummonController 는 "InWave 인데
        //   적이 0" 인 프레임을 봐야 생존자를 회수한다(MonsterLineReturner).
        //   승리 선언은 그 틈을 준 뒤 RunBootstrap 이 DeclareVictory() 로 한다.

        if (!IsCoreDown()) return;

        _context.State = BattleState.BattleDefeat;
        IsWaveRunning  = false;
        Debug.Log($"[BattleManager] 패배 — 마왕성 함락 (스테이지 {_context.StageNumber})" +
                  $"  적군 잔존: {_context.AliveEnemyCount}");
        LogBattleStats("패배");
        _mode.OnBattleDefeat();
        OnDefeat?.Invoke();
    }

    /// <summary>
    /// 마왕성이 무너졌는가 — <b>런 종료 판정의 정본</b>.
    ///
    /// ⚠ 소환사의 HP 가 아니다 (2026-09-04 개편)
    ///   용사는 이제 소환사를 조준하지 않는다(UnitTargetSearchSystem 에서
    ///   그리드 제외). 성벽에 닿은 용사를 CoreBreachSystem 이 거두며
    ///   RunCoreData 를 1 씩 깎고, 그 값이 0 이 되는 것이 패배다.
    ///
    /// 아직 지급 전(Max=0)이면 false 다 — 런이 시작되기 전에 패배가 뜨면 안 된다.
    /// </summary>
    static bool IsCoreDown()
        => UserDataManager.Instance?.Get<RunCoreData>()?.IsDown ?? false;

    // ── 전투 통계 로그 ────────────────────────────────────────

    static void LogBattleStats(string result)
    {
        var tracker = BattleStatsTracker.Instance;
        if (tracker == null) return;

        var entries = tracker.GetAllEntries();
        if (entries == null || entries.Count == 0) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[BattleStats] ===== 전투 결과 [{result}] =====");

        foreach (var e in entries)
        {
            sb.AppendLine($"  ▶ {e.GeneralName}");
            sb.AppendLine($"    딜량   총:{e.TotalDamageDealt:F0}" +
                          $"  장군:{e.GeneralDamageDealt:F0}" +
                          $"  병사:{e.SoldierDamageDealt:F0}" +
                          $"  스킬:{e.SkillDamageDealt:F0}");
            sb.AppendLine($"    피해량 장군:{e.DamageTaken:F0}" +
                          $"  병사:{e.SoldierDamageTaken:F0}" +
                          $"  방어감소:{e.DamageAbsorbed:F0}");
            sb.AppendLine($"    처치:{e.KillCount}  힐(가한):{e.HealingDone:F0}  힐(받은):{e.HealingReceived:F0}");
        }

        sb.Append("=========================================");
        Debug.Log(sb.ToString());
    }

    // ── 유닛 전체 정리 ────────────────────────────────────────

    /// <summary>
    /// 진행 중인 배틀을 즉시 종료하고 모든 유닛을 풀로 반납한 뒤 ECS 엔티티를 파괴한다.
    /// 로비 복귀 시 LobbyManager.ReturnToLobby() 에서 호출.
    /// </summary>
    public void DespawnAllUnits()
    {
        // 유닛을 다 치우는 시점 = 웨이브는 끝났다.
        // 승리·패배·판 닫기 어느 경로로 왔든 여기를 지나므로 한 곳에서 내린다.
        IsWaveRunning = false;

        StopAllCoroutines();
        _context = null;
        _mode    = null;

        // ① ECS UnitPoolLinkComponent 를 통해 활성 유닛 GO 풀 반납
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            var em    = world.EntityManager;
            em.CompleteAllTrackedJobs();  // 실행 중인 Burst Job 완료 대기
            var query = em.CreateEntityQuery(
                new ComponentType[] { ComponentType.ReadOnly<BattleGame.Units.UnitPoolLinkComponent>() });

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                var link = em.GetComponentObject<BattleGame.Units.UnitPoolLinkComponent>(entity);
                if (link?.LinkedObject != null)
                    PoolController.Instance?.Despawn(link.LinkedObject);
            }
            query.Dispose();

            // ② 모든 유닛 ECS 엔티티 파괴 (UnitIdentityComponent 기준)
            var unitQuery = em.CreateEntityQuery(
                new ComponentType[] { ComponentType.ReadOnly<BattleGame.Units.UnitIdentityComponent>() });
            em.DestroyEntity(unitQuery);
            unitQuery.Dispose();
        }

        // ③ 풀에서 놓친 유닛 브릿지 — 안전망으로 활성 브릿지 전부 회수
        foreach (var bridge in Object.FindObjectsByType<UnitRuntimeBridge>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            PoolController.Instance?.Despawn(bridge.gameObject);
        }
    }



}
