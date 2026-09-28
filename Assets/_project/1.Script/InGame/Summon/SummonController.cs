using System.Collections;
using UnityEngine;

// ============================================================
//  SummonController.cs
//  소환 조작의 실행부. UI(카드 선택) → 예약 → 스폰까지를 잇는다.
//
//  ■ 조작 흐름
//    ① 하단 카드 탭  → 그 카드가 **선택된 채로 유지**된다
//    ② 맵의 라인 탭  → 마나를 내고
//                       · 몬스터 카드 : 그 라인 큐에 줄을 세운다
//                       · 스킬 카드   : 그 자리에서 즉시 터진다 (줄을 서지 않는다)
//    ③ 스테이지 시작 → 라인마다 일정 간격으로 한 마리씩 나간다
//    ④ 적 전멸      → 살아남은 몬스터가 **제 라인 대기열로 돌아간다**
//                       (MonsterLineReturner. 다음 판 시작에 저절로 다시 나간다)
//
//  ■ ⚠ 종족별 소환 시간(SummonTime)은 없앴다 (2026-08-27)
//    예전에는 종족마다 "한 마리 나오는 데 N초" 가 있어서 라인 속도가 갈렸다.
//    무게는 마나 하나로만 표현하기로 하고 걷어냈다. 지금 남은 DrainInterval 은
//    **연출용 고정 간격**이다 — 같은 프레임에 6마리가 겹쳐 나오면 분리 계산이
//    튀고 그림도 뭉친다. 밸런스 축이 아니므로 종족별로 다르게 만들지 말 것.
//
//  ■ 라인은 각자 돈다 — 배출 루프가 라인 수만큼 있다
//    3번 라인에 100마리가 밀려 있어도 1번 라인은 제 속도로 나간다.
//
//  ■ 라인마다 0.05초씩 어긋나게 시작한다
//    다섯 라인이 정확히 같은 순간에 뱉으면 한 덩어리로 뚝뚝 끊겨 보인다.
//
//  ■ 마나는 여기서만, 예약하는 순간 깎는다
//    선불이다. UI 는 "쓸 수 있나" 를 보여 줄 뿐 깎지 않는다.
//    실제 비용은 SummonerPerkRuntime.ManaCostFor 가 정한다 (친화 할인 등).
//
//  ■ 스폰은 여기서 하지 않는다
//    MonsterSpawner 한 곳이 소유한다 — 카드 소환·부활·스킬 소환이 같은 길을
//    지나야 품질·패시브·환수 규칙이 어긋나지 않는다.
// ============================================================

public class SummonController : MonoBehaviour
{
    /// <summary>
    /// 라인 간 시작 어긋남(초). 라인 N 은 N × 이 값만큼 늦게 시작한다.
    /// </summary>
    public const float LaneStagger = 0.05f;

    // ⚠ 배출 간격은 이제 **종족마다 다르다** (사용자 확정, 2026-09-07)
    //   정본은 SpawnPaceRule.IntervalFor 이고, 특성은 거기에 곱해지는 배율이다
    //   (RunPerkRule.DrainMultiplierFor). 여기 고정 상수를 두지 말 것 —
    //   두 곳이 갈리면 화면의 360° 표시와 실제 배출이 어긋난다.

    public static SummonController Instance { get; private set; }

    [Header("연결")]
    [Tooltip("하단 소환 덱 UI. 비우면 씬에서 찾는다.")]
    [SerializeField] SummonDeckUI _deckUI;

    [Tooltip("라인 좌표를 들고 있는 전장 레이아웃. 비우면 씬에서 찾는다.")]
    [SerializeField] SummonFieldLayout _field;

    [Tooltip("탭 좌표를 월드로 바꿀 인게임 카메라.\n" +
             "인스펙터에 직접 걸어 두는 것이 정석이다 — 아래 주의 참고.")]
    [SerializeField] Camera _cam;

    /// <summary>예약 목록. UI 가 읽어 라인별 "초상화 × 마릿수" 를 그린다.</summary>
    public SummonReservation Reservation { get; private set; }

    /// <summary>지금 선택된 카드 칸. 없으면 비어 있다.</summary>
    SummonDeckSlot _selected = SummonDeckSlot.Empty;

    Coroutine[] _drains;
    float[]     _laneTimer;   // 다음 한 마리까지 남은 시간 (UI 의 360° 표시가 읽는다)

    // 지금 기다리고 있는 간격의 전체 길이. 360° 표시의 **분모**다 —
    // 종족마다 간격이 다르므로 남은 시간만으로는 비율을 낼 수 없다.
    float[]     _laneInterval;

    /// <summary>
    /// 라인마다 <b>이번 스테이지</b>에 뱉은 마릿수. 특성 '선발대' 가 이 값을 본다.
    ///
    /// ⚠ 스테이지 경계에서 0 으로 되돌린다 — 안 되돌리면 선발대가 런 전체에서
    ///   딱 한 번만 발동하고, 증상이 조용하다(첫 판에서는 멀쩡히 돈다).
    /// </summary>
    int[]       _laneSpawned;

    /// <summary>
    /// 탭 좌표 변환에 쓸 카메라.
    ///
    /// ⚠ Awake 에서 캐시해 두면 안 된다 — 씬 전환에서 터진다
    ///   씬이 넘어가면 앞 씬 카메라가 파괴되는데, 캐시된 참조는 그 파괴된
    ///   오브젝트를 계속 가리킨다. Unity 의 파괴된 오브젝트는 == null 이
    ///   true 이므로, 이 검사 하나로 "미연결" 과 "파괴됨" 을 함께 걸러 다시 잡는다.
    /// </summary>
    Camera Cam
    {
        get
        {
            if (_cam == null) _cam = Camera.main;
            return _cam;
        }
    }

    // ── 생명주기 ─────────────────────────────────────────────

    void Awake()
    {
        Instance = this;

        if (_deckUI == null) _deckUI = FindAnyObjectByType<SummonDeckUI>();
        if (_field  == null) _field  = FindAnyObjectByType<SummonFieldLayout>();

        int lanes = SummonFieldLayout.LaneCount;
        Reservation = new SummonReservation(lanes);
        _drains       = new Coroutine[lanes];
        _laneTimer    = new float[lanes];
        _laneInterval = new float[lanes];
        _laneSpawned  = new int[lanes];

        // ⚠ 대기 중의 예약도 저장한다 — 아래 HandleReservationChanged 주석 참고.
        //   ⚠ 해지하지 않는다: 이 예약 목록은 이 컴포넌트가 소유하고 함께 죽는다.
        Reservation.Changed += HandleReservationChanged;
    }

    // ── 대기열 저장/복원 ─────────────────────────────────────
    //
    //  ■ 저장은 판의 **경계**에서만 한다 (2026-09-02)
    //      ① 스테이지 시작   — 미리 걸어 둔 예약이 확정되는 순간
    //      ② 적 전멸 직후    — 생존자를 대기열로 거둬들인 직후
    //    이 둘 사이(전투 중)에는 저장하지 않는다.
    //
    //  ⚠ Reservation.Changed 를 구독하지 말 것
    //    한때 예약이 바뀔 때마다 저장했다. 대기열은 배출 루프가 라인마다
    //    0.15초에 하나씩 꺼내므로, 전투 한 판에 저장이 **수백 번** 돌고
    //    콘솔이 저장 로그로 덮였다. 게다가 RequestSave 는 전 섹션을 쓴다 —
    //    몬스터 하나 나갈 때마다 세이브 전체를 다시 쓰는 셈이었다.
    //
    //  ■ 중간에 앱이 죽으면 어떻게 되나
    //    직전 경계 상태로 돌아간다. 마나도 같은 규칙이다 —
    //    SummonManaData.Spend 도 저장을 부르지 않는다. 둘이 같은 시점에만
    //    굳으므로 "마나만 빠지고 몬스터는 없는" 어긋남이 생기지 않는다.
    //    ⚠ 한쪽만 즉시 저장하게 바꾸면 그 어긋남이 되살아난다.

    /// <summary>지금 대기열을 세이브에 받아 적는다. 판의 경계에서만 부른다.</summary>
    void PersistQueue()
    {
        var queue = UserDataManager.Instance?.Get<SummonQueueData>();
        if (queue == null) return;

        queue.Capture(Reservation);
        UserDataManager.Instance.RequestSave();
    }

    /// <summary>
    /// 예약이 바뀌었다 — <b>스테이지 대기 중일 때만</b> 받아 적는다.
    ///
    /// ■ 왜 필요했나 (사용자 지적, 2026-09-12 — "대기열 저장이 안 되는 것 같다")
    ///   저장 지점이 판의 **경계** 둘(시작 직전 · 전멸 직후)뿐이었다. 그런데
    ///   플레이어가 실제로 줄을 세우는 시간은 그 사이의 **대기 구간**이고,
    ///   자동 시작이 꺼져 있어(StageLoopDirector._readySeconds = 0) 그 구간은
    ///   [시작]을 누를 때까지 무한정 이어진다. 거기서 앱을 껐다 켜면 걸어 둔
    ///   예약이 통째로 사라졌다 — "저장이 안 된다" 로 보이는 것이 이것이다.
    ///
    /// ■ ⚠ 그래도 전투 중에는 안 적는다 (원래 주석의 이유가 그대로 살아 있다)
    ///   배출 루프가 라인마다 0.15초에 하나씩 꺼내며 이 사건을 쏜다. 거기까지
    ///   받아 적으면 한 판에 저장이 수백 번 돌고, RequestSave 는 전 섹션을 쓴다.
    ///   대기 중에는 배출이 돌지 않으므로 이 사건은 **플레이어가 카드를 낼 때만**
    ///   온다 — 한 판에 많아야 수십 번이고, 그나마 같은 프레임은 한 번으로 합쳐진다.
    ///
    /// ■ ⚠ 마나와 같은 시점에 굳는다
    ///   SaveAll 이 전 섹션을 함께 쓰므로 마나 잔량도 이때 같이 저장된다.
    ///   "마나만 빠지고 몬스터는 없는" 어긋남이 생기지 않는다.
    /// </summary>
    void HandleReservationChanged()
    {
        var director = StageLoopDirector.Instance;
        if (director == null || !director.IsStageReady) return;

        PersistQueue();
    }

    /// <summary>
    /// 저장된 대기열을 되돌린다 — 이어하기 전용(RunBootstrap.StartRun).
    ///
    /// ⚠ 새 런에서는 부르지 않는다. 그쪽은 ClearQueue 로 비워야 한다 —
    ///   지난 런의 예약분을 물려받으면 공짜 물량이 된다.
    /// </summary>
    public void RestoreQueue()
    {
        var queue = UserDataManager.Instance?.Get<SummonQueueData>();
        if (queue == null || CardCatalog.Current == null) return;

        queue.Restore(Reservation, CardCatalog.Current);
    }

    /// <summary>대기열을 비우고 저장까지 지운다 — 새 런 시작에 부른다.</summary>
    public void ClearQueue()
    {
        Reservation.Clear();
        PersistQueue();   // Clear 가 비어 있던 경우에도 세이브를 확실히 비운다
    }

    void OnEnable()
    {
        if (_deckUI != null) _deckUI.CardSelected += HandleCardSelected;
        SummonerSkillRule.ArmedChanged += HandleArmedChanged;
        StageLoopDirector.OnStageStart += HandleStageStart;
        StageLoopDirector.OnStageReady += HandleStageReady;
        BattleManager.OnDefeat         += Halt;
    }

    void OnDisable()
    {
        if (_deckUI != null) _deckUI.CardSelected -= HandleCardSelected;
        SummonerSkillRule.ArmedChanged -= HandleArmedChanged;
        StageLoopDirector.OnStageStart -= HandleStageStart;
        StageLoopDirector.OnStageReady -= HandleStageReady;
        BattleManager.OnDefeat         -= Halt;
    }

    /// <summary>
    /// 배출·풀 채우기를 전부 멈춘다 — 패배 순간과 판을 닫을 때(BattleArena.Close) 부른다.
    ///
    /// ⚠ 이게 없으면 '즉시 환생' 뒤에도 몬스터가 계속 나왔다 (사용자 지적, 2026-09-22)
    ///   전투 도중 포기하면 대기열이 남은 채 배출 코루틴이 그대로 돈다. 판을 닫아(DespawnAllUnits)
    ///   필드를 비운 **다음에** 나온 개체는 아무도 거두지 않아 로비 뒤에 남고 풀에도 안 돌아갔다.
    ///   소환사가 풀에 돌아가도 SummonerRuntimeBridge.Current 가 남아 있어 SpawnOne 이 막히지 않았다.
    /// </summary>
    public void Halt()
    {
        StopDrains();

        if (_prewarm != null) StopCoroutine(_prewarm);
        _prewarm = null;
    }

    // ── 풀 미리 채우기 ───────────────────────────────────────
    //
    //  ■ 왜 필요한가 (사용자 지적, 2026-09-11 — 프로파일러 313ms)
    //    슬라임 67마리가 한꺼번에 죽자 분열체를 세우느라 Instantiate 가 742번 돌았다.
    //    죽는 개체는 사망 연출이 끝나 OnDisable 이 도는 **그 순간에도 아직 풀에 없어서**
    //    (Release 가 목록에 넣기 전이다) 자기 자리를 물려줄 수 없다. 대기 중인 인스턴스가
    //    0 이면 분열체 하나마다 몬스터 프리팹 한 벌(외형 합성 포함)을 새로 찍는다.
    //
    //  ■ 대기 시간에 조금씩 채운다
    //    판이 열리기 전(StageReady)에 **대기열에 선 수 + 여유분** 만큼 비축해 둔다.
    //    한 프레임에 몰아 채우면 그게 또 끊김이라 프레임당 몇 개씩 나눈다.
    //    풀은 줄지 않으므로 두 번째 판부터는 거의 아무것도 안 만든다.

    /// <summary>대기열 몫 위에 더 비축할 수 — 분열·부활이 한꺼번에 터지는 몫이다.</summary>
    const int DerivedSpare = 64;

    /// <summary>한 프레임에 새로 만드는 수. 몬스터 프리팹 한 벌이 약 2ms 다.</summary>
    const int PrewarmPerFrame = 4;

    /// <summary>몬스터 풀 키 — 종족이 전부 같은 프리팹을 쓴다 (MonsterCodexCreator).</summary>
    static readonly string MonsterPoolKey = SpawnUnitType.Monster.ToString();

    Coroutine _prewarm;

    void HandleStageReady(int stageNumber)
    {
        if (_prewarm != null) StopCoroutine(_prewarm);
        _prewarm = StartCoroutine(PrewarmMonsters());
    }

    IEnumerator PrewarmMonsters()
    {
        var pool = PoolController.Instance;

        int queued = 0;
        for (int lane = 0; lane < Reservation.LaneCount; lane++)
            queued += Reservation.RemainingCount(lane);

        int target = queued + DerivedSpare;

        while (pool.InactiveCount(PoolType.Unit, MonsterPoolKey) < target)
        {
            int have = pool.InactiveCount(PoolType.Unit, MonsterPoolKey);
            pool.Prewarm(PoolType.Unit, MonsterPoolKey, Mathf.Min(target, have + PrewarmPerFrame));
            yield return null;
        }

        _prewarm = null;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        ReadTap();
        CheckWaveCleared();

        // 시너지 선봉(첫 공격 후처리) · 무리(뭉침 판정) — 2026-09-15
        MonsterSynergyRuntime.Tick();

        // 역병 술사 — 이번 프레임에 쓰러뜨린 자리마다 좀비 (거둔 판이면 버린다).
        SummonerPerkRuntime.FlushPlague(SummonerRuntimeBridge.Current?.Data, _swept);
    }

    // ── 판이 끝난 순간 — 생존자를 대기열로 거둔다 ─────────────

    /// <summary>이번 판의 생존자 회수를 이미 했는가. 판마다 한 번만 돈다.</summary>
    bool _swept;

    /// <summary>
    /// 이번 판을 이미 거뒀는가. <b>사망 훅이 읽는다</b> (MonsterDeathWatcher).
    ///
    /// ⚠ 거둔 뒤에 태어나는 개체는 아무도 거두지 않는다
    ///   거두기는 판마다 한 번뿐이고(_swept), 상태가 InWave 에서 내려오는 것은
    ///   그보다 늦다. 그 사이에 분열·부활이 낳은 개체는 다음 스테이지까지 남는다.
    /// </summary>
    public bool WaveSwept => _swept;

    /// <summary>
    /// 적을 모두 잡았는가를 **한 곳에서만** 판정한다.
    ///
    /// ■ 조건은 둘이다 — 웨이브 중이었고, 적이 하나도 남지 않았다
    ///   ⚠ "적이 0" 만 보면 안 된다. 스테이지 대기(StageReady)에도 적은 0 이라,
    ///     상태를 같이 보지 않으면 사전 소환한 몬스터가 세우자마자 거둬진다.
    ///
    /// ■ 왜 개체마다 판정하지 않는가
    ///   예전에는 MonsterLineReturner 가 각자 Update 로 이 조건을 봤다.
    ///   그러면 배출 코루틴이 아직 도는 사이에 개체가 대기열에 들어가
    ///   **넣자마자 다시 나오는** 상태가 됐다(간헐적으로 안 돌아가 보이던 원인).
    ///   배출을 먼저 멈추고 나서 거두려면 판정이 한곳에 있어야 한다.
    /// </summary>
    void CheckWaveCleared()
    {
        var bm = BattleManager.Instance;

        if (bm == null || bm.Context == null ||
            bm.Context.State != BattleState.InWave)
        {
            _swept = false;
            return;
        }

        if (bm.Context.AliveEnemyCount > 0)
        {
            _swept = false;
            return;
        }

        // ⚠ 아직 아무도 안 나온 판은 '전멸' 이 아니다
        //   용사 편성은 코루틴이 부대마다 한 프레임씩 나눠 세운다. 그 사이는
        //   InWave 인데 적이 0 이다. 이걸 전멸로 읽으면 판이 시작하자마자
        //   끝나 버린다 — 병사만 나오는 초반 스테이지는 장수 한 기를 먼저
        //   세는 보정조차 없어 더 위험하다.
        //   StageEnemyTotal 은 적이 하나라도 서면 올라가므로 그 표식을 쓴다.
        if (bm.Context.StageEnemyTotal <= 0) return;

        // ── 최종 스테이지(무한 보스)는 거두지 않는다 ──
        //
        //  ⚠ 이 판은 끝나지 않는다 (EndlessBossRule)
        //    보스를 잡아도 다음 보스가 올 뿐이라 '다음 스테이지' 가 없다.
        //    여기서 거두면 배출이 멈춘 채 생존자가 전부 대기열로 들어가고,
        //    그 대기열을 열어 줄 스테이지 시작이 영영 오지 않아
        //    **필드가 텅 빈 채로 멈춘다.**
        //  ⚠ 잠깐이라도 적이 0 인 프레임이 생긴다 — 보스가 죽고 다음 보스가
        //    걸어 들어오기까지의 사이다. RunBootstrap 이 세는 쪽을 먼저 올리지만
        //    실행 순서가 보장되지 않으므로 여기서도 못 박아 둔다.
        if (EndlessBossRule.IsEndlessStage(bm.Context.StageNumber)) return;

        if (_swept) return;
        _swept = true;

        // 순서를 지킨다 — 배출을 먼저 멈추고 거둔다.
        StopDrains();
        MonsterLineReturner.ReturnAllToQueue(Reservation);

        // ⚠ 저장 지점 ② — 거둔 **뒤에** 적는다
        //   이 판을 버텨 낸 생존자가 다음 판의 대기열이다. 거두기 전에
        //   적으면 살아남은 몬스터가 통째로 사라진 세이브가 된다.
        //   (다음은 카드 3택 → 다음 스테이지라, 여기가 판의 마지막 상태다)
        PersistQueue();
    }

    /// <summary>돌고 있는 배출 루프를 전부 접는다. 라인 타이머도 0 으로 내린다.</summary>
    void StopDrains()
    {
        for (int lane = 0; lane < _drains.Length; lane++)
        {
            if (_drains[lane] != null) StopCoroutine(_drains[lane]);
            _drains[lane]       = null;
            _laneTimer[lane]    = 0f;
            _laneInterval[lane] = 0f;
        }
    }

    // ── UI 조회 ──────────────────────────────────────────────

    /// <summary>
    /// 그 라인의 다음 배출까지 남은 비율 (1 = 방금 뱉음, 0 = 곧 나옴).
    /// 대기열 초상화 위의 360° 표시가 이 값을 쓴다.
    ///
    /// ⚠ 분모는 <b>지금 기다리고 있는 그 간격</b>이다 (2026-09-07)
    ///   종족마다 간격이 달라졌으므로 고정값으로 나누면 트롤 차례에 고리가
    ///   반쯤 돌다 끝나고, 슬라임 차례엔 한 바퀴를 넘겨 되감긴다.
    /// </summary>
    public float GetLaneCooldownRatio(int laneIndex)
    {
        float span = _laneInterval[laneIndex];
        return span > 0f ? Mathf.Clamp01(_laneTimer[laneIndex] / span) : 0f;
    }

    // ── 입력 ─────────────────────────────────────────────────

    /// <summary>
    /// 카드가 눌렸다. UI 가 토글을 이미 처리하고 선택된 칸만 알려 준다.
    /// 선택은 다시 누를 때까지 유지된다 — 여기서 지우지 않는다.
    /// </summary>
    /// ⚠ 카드를 고르면 시그니처 스킬 겨냥은 풀린다
    ///   둘 다 "다음 탭" 을 기다리는 상태라, 겹쳐 두면 한 번의 탭이 어느
    ///   쪽으로 갈지 알 수 없다. 나중에 고른 쪽이 이긴다.
    ///
    /// ⚠ **해제(slot &lt; 0)일 때는 겨냥을 건드리지 않는다** (2026-09-07)
    ///   겨냥을 켜면 그 반대편(HandleArmedChanged)이 카드 선택을 놓는데,
    ///   그때 날아오는 해제 통지까지 Cancel 로 받으면 방금 켠 겨냥이
    ///   그 자리에서 도로 꺼진다. 겨냥을 푸는 것은 '카드를 고른' 순간뿐이다.
    void HandleCardSelected(int slot, SummonDeckSlot card)
    {
        if (slot >= 0) SummonerSkillRule.Cancel();
        _selected = card;
    }

    /// <summary>
    /// 시그니처 스킬 겨냥이 켜졌다 — 고른 카드를 놓는다 (사용자 확정, 2026-09-07).
    ///
    /// 카드를 고르면 겨냥이 풀리는 것의 <b>반대편</b>이다. 한쪽만 있으면
    /// "스킬을 켰는데 카드도 여전히 골라져 있는" 상태가 되어, 다음 탭이
    /// 어느 쪽으로 갈지 화면만 보고는 알 수 없다.
    ///
    /// ⚠ 꺼질 때는 아무것도 하지 않는다 — 겨냥을 푼다고 카드가 돌아올 이유가 없다.
    /// </summary>
    void HandleArmedChanged()
    {
        if (!SummonerSkillRule.IsArmed) return;

        _deckUI?.ClearSelection();
    }

    void ReadTap()
    {
        // ⚠ 시그니처 스킬이 먼저다
        //   겨냥 중이면 카드 선택은 이미 풀려 있다(HandleArmedChanged).
        //   순서를 뒤집으면 직전에 고른 카드가 탭을 가로챈다.
        bool armed = SummonerSkillRule.IsArmed;

        if (!armed && _selected.IsEmpty) return;
        if (!Input.GetMouseButtonDown(0)) return;

        // UI 위를 눌렀으면 무시한다 — 카드 바를 누른 것이지 맵을 누른 게 아니다.
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        Camera cam = Cam;
        if (cam == null)
        {
            Debug.LogError("[SummonController] 카메라를 찾지 못했습니다 — " +
                           "인스펙터의 Cam 슬롯에 인게임 카메라를 연결하세요.");
            return;
        }

        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;

        if (armed) SummonerSkillRule.TryUseAt(world);
        else       TryUseCard(_selected, world);
    }

    // ── 카드 사용 ────────────────────────────────────────────

    /// <summary>
    /// 탭한 위치에 카드를 쓴다. 마나가 모자라면 아무것도 하지 않고 false.
    ///
    /// ⚠ 마나를 먼저 낸다 — 사용이 곧 확정이다 (취소 없음)
    ///   되돌릴 수 있으면 "얼마나 걸 것인가" 가 선택이 아니게 된다.
    /// </summary>
    public bool TryUseCard(in SummonDeckSlot card, Vector3 worldPos)
    {
        if (card.IsEmpty) return false;

        var director = StageLoopDirector.Instance;
        if (director != null && !director.CanSummon) return false;

        return card.Kind == SummonKind.Skill
            ? UseSkillCard(card, worldPos)
            : UseMonsterCard(card, worldPos);
    }

    bool UseMonsterCard(in SummonDeckSlot card, Vector3 worldPos)
    {
        MonsterSpeciesData species = CardCatalog.Current.GetMonster(card.Id);
        if (species == null)
        {
            Debug.LogError($"[SummonController] 카드 목록에 없는 몬스터 ID: '{card.Id}'. " +
                           "Tools > Project K > 데이터 생성 > 카드 목록 을 다시 실행하세요.");
            return false;
        }

        SummonerData summoner = SummonerRuntimeBridge.Current.Data;

        // 개성·특성 할인을 먼저 받고, 그 값 위에 과부하가 얹힌다.
        float adjusted = SummonerPerkRuntime.ManaCostFor(summoner, species, card);
        int   cost     = SummonCostRule.CostFor(card.Id, adjusted);

        var mana = UserDataManager.Instance.Get<SummonManaData>();

        // 특성 '첫 소환 무료' — 스테이지마다 한 번은 마나를 내지 않는다.
        // ⚠ 조회와 소모가 한 몸이다 (RunPerkData.ConsumeFreeSummon 주석 참고)
        // 특성 '피의 계약' — 모자라면 모자란 몫을 마왕성 체력으로 낸다 (체력 1 은 남긴다).
        if (!UserDataManager.Instance.Get<RunPerkData>().ConsumeFreeSummon()
            && !mana.Spend(cost)
            && !RunPerkRule.TryPayWithBlood(mana, cost))
            return false;

        // ⚠ 마나를 실제로 낸 뒤에 센다
        //   위에서 잔량이 모자라 돌아갔는데 세어 두면, 소환도 못 하고
        //   값만 오르는 상태가 된다. (무료 소환도 한 번 쓴 것으로 친다 —
        //   공짜라고 도배가 허용되면 과부하를 우회하는 길이 된다)
        SummonCostRule.MarkUsed(card.Id);

        // 마력 폭주 — 이 소환으로 잔량이 바닥났다면 그 판 내내 소환력이 오른다.
        // ⚠ 마나를 낸 **뒤에** 본다. 내기 전에 보면 영영 0 이 되지 않는다.
        if (mana.Current <= 0f)
            UserDataManager.Instance.Get<RunPerkData>().NoticeManaEmpty();

        int lane = _field.GetNearestLane(worldPos);

        // 마릿수의 정본은 RunPerkRule 이다 — 증원·친화 증원이 여기서 얹힌다.
        int count = RunPerkRule.SummonCountFor(summoner, species) + card.ExtraSummons;
        Reservation.Enqueue(species, lane, count);

        // 특성 '쌍둥이 라인' — 옆 라인에 절반(내림)이 더 선다. 값(+2 마나)은 비용이 이미 받았다.
        int twin = RunPerkRule.TwinLaneOf(lane, count);
        if (twin >= 0)
        {
            Reservation.Enqueue(species, twin, count / 2);
            EnsureDraining(twin);
        }

        // 개성 '쌍둥이 소집'(군악대장) — 옆 라인에 한 마리가 공짜로 더 선다 (2026-09-12).
        //   ⚠ 특성과 겹쳐도 따로 얹힌다 — 둘은 다른 축이다(하나는 비용을 냈고 하나는 개성이다).
        int callCount = SummonerPerkRuntime.TwinCallCount(summoner);
        if (callCount > 0)
        {
            int side = SummonerPerkRuntime.NextLaneOf(lane);
            if (side >= 0)
            {
                Reservation.Enqueue(species, side, callCount);
                EnsureDraining(side);
            }
        }

        // ⚠ 시너지가 켜지는 유일한 지점이다
        //   덱에 들고만 있어서는 안 켜진다 — 마나를 내고 줄을 세워야 센다.
        //   한 번 세면 그 런 동안 내려가지 않는다(죽어도 유지).
        MonsterSynergyRule.MarkSummoned(species);

        EnsureDraining(lane);
        return true;
    }

    /// <summary>
    /// 스킬 카드 — 줄을 서지 않고 그 자리에서 바로 터진다.
    ///
    /// ⚠ 라인 큐에 넣지 않는다
    ///   대기열은 "성에서 나갈 순서" 다. 스킬은 성에서 나가는 게 아니라
    ///   전장에 떨어지는 것이라, 줄을 세우면 시작 버튼을 누르기 전에 걸어 둔
    ///   메테오가 스테이지 시작과 함께 빈 땅에 떨어진다.
    /// </summary>
    bool UseSkillCard(in SummonDeckSlot card, Vector3 worldPos)
    {
        SkillCardData skill = CardCatalog.Current.GetSkill(card.Id);
        if (skill == null)
        {
            Debug.LogError($"[SummonController] 카드 목록에 없는 스킬 ID: '{card.Id}'.");
            return false;
        }

        // 스킬도 같은 규칙을 탄다 — 메테오 도배가 답이 되면 카드가 한 장뿐인 게임이다.
        int cost = SummonCostRule.CostFor(card.Id, skill.ManaCost);

        var mana = UserDataManager.Instance.Get<SummonManaData>();
        if (!mana.Spend(cost)) return false;

        SummonCostRule.MarkUsed(card.Id);

        SkillCardCaster.Cast(skill, card.Level, worldPos);
        return true;
    }

    // ── 배출 ─────────────────────────────────────────────────

    void HandleStageStart(int stageNumber)
    {
        // 지난 판의 회수 표식을 내린다 — 안 내리면 이번 판에 배출이 열리지 않는다.
        _swept = false;

        // 이번 판이 열린 시각 — 특성 '매복'·'기다림의 미학' 이 여기서부터 잰다.
        RunPerkRule.NoteStageStart();

        // ⚠ 저장 지점 ① — 배출을 열기 **전에** 적는다
        //   대기 중에 걸어 둔 예약이 이 순간의 대기열 전부다. 배출이 시작되면
        //   곧바로 줄어들기 시작하므로, 한 마리라도 나간 뒤에 적으면
        //   플레이어가 마나를 내고 산 물량이 세이브에서 빠진다.
        PersistQueue();

        // 선발대 카운터를 판마다 되돌린다 (RunPerkData.OnStageBegin 과 같은 이유).
        for (int lane = 0; lane < _laneSpawned.Length; lane++) _laneSpawned[lane] = 0;

        for (int lane = 0; lane < _drains.Length; lane++)
            EnsureDraining(lane);
    }

    /// <summary>
    /// 그 라인의 배출 루프를 돌린다. 이미 돌고 있으면 아무것도 하지 않는다.
    /// 대기 중에는 돌지 않는다 — 시작 전에 빠져나가면 미리 소환하는 셈이 된다.
    /// </summary>
    /// <summary>
    /// 전투 도중 쓰러진 몬스터를 제 라인 대기열에 도로 세운다 — 숲 금 · 특성 '귀환'.
    ///
    /// ⚠ 넣기만 하면 안 된다 — 배출도 켠다 (2026-09-16 버그)
    ///   그 라인의 배출이 이미 끝났으면(대기열이 비어 코루틴이 멈췄다) 넣은 몬스터가
    ///   **다음 판까지 대기열에 갇혔다.** 보스 성벽 판정(CoreBreachSystem)이 대기열을
    ///   "싸울 아군" 으로 세던 때는 필드가 텅 빈 채 보스가 영원히 붙들렸다.
    /// </summary>
    public void ReturnToLine(MonsterSpeciesData species, int lane)
    {
        Reservation.EnqueueOne(species, lane);
        EnsureDraining(lane);
    }

    /// <summary>
    /// 지금 실제로 나오고 있는 대기열이 있는가 — 배출이 도는(매복 대기 포함) 라인에 몬스터가 남았다.
    /// ⚠ 대기열 수만 보지 않는다 — 판이 거둔 뒤나 배출이 멈춘 라인의 몬스터는 이번 판에 안 나온다.
    /// </summary>
    public bool HasPendingSpawns
    {
        get
        {
            for (int lane = 0; lane < _drains.Length; lane++)
                if (_drains[lane] != null && Reservation.RemainingCount(lane) > 0) return true;
            return false;
        }
    }

    void EnsureDraining(int lane)
    {
        if (_drains[lane] != null) return;

        // 이미 판이 끝났다 — 지금 열면 방금 거둔 생존자가 빈 전장으로 다시 나간다.
        if (_swept) return;

        var director = StageLoopDirector.Instance;
        if (director != null && director.IsStageReady) return;

        _drains[lane] = StartCoroutine(DrainLane(lane));
    }

    IEnumerator DrainLane(int lane)
    {
        // 특성 '매복' — 판이 열리고 몇 초는 아무도 안 나간다. 판 시작 시각에서 재므로
        //   판 도중 새로 연 라인도 남은 시간만큼만 기다린다. 360° 표시도 이 시간을 돈다.
        float hold = RunPerkRule.AmbushHoldRemaining;
        if (hold > 0f) yield return CountDown(lane, hold);

        // 라인별 어긋남 — 0번은 즉시, 1번은 0.05초 뒤…
        float stagger = lane * LaneStagger;
        if (stagger > 0f) yield return CountDown(lane, stagger);

        while (true)
        {
            if (!Reservation.TryDequeue(lane, out MonsterSpeciesData species))
            {
                // 큐가 비었다 — 루프를 접고 다음 예약 때 다시 연다.
                _laneTimer[lane] = 0f;
                _drains[lane]    = null;
                yield break;
            }

            SpawnOne(species, lane, _laneSpawned[lane]);

            // ── 간격 = 종족의 무게 × 특성 배율 ──
            //   ⚠ 방금 **뱉은** 종족을 기준으로 잰다 (다음 대기가 아니다)
            //     트롤이 나오고 나서 슬라임 간격만큼만 쉬면 트롤이 무겁게
            //     나온 그림이 그 자리에서 지워진다.
            //   선발대는 앞 몇 마리에만 걸린다 — 세고 나서 배율을 정한다.
            //   ⚠ 초조(Rush)는 판이 열린 뒤 흐른 시간이 정한다 — 적 광폭화와 같은
            //     60초 시계로 한 단계씩, 단계마다 간격이 절반이 된다.
            //     매 마리마다 다시 재야 판 도중에 단계가 올라도 그 자리에서 빨라진다.
            float interval = SpawnPaceRule.IntervalFor(species)
                           * RunPerkRule.DrainMultiplierFor(_laneSpawned[lane])
                           * SpawnPaceRule.RushMultiplierAt(RunPerkRule.SecondsSinceStageStart);
            _laneSpawned[lane]++;

            yield return CountDown(lane, interval);
        }
    }

    /// <summary>
    /// 라인 타이머를 초 단위로 깎으며 기다린다.
    /// UI 가 매 프레임 이 값을 읽어 360° 표시를 그린다.
    /// </summary>
    IEnumerator CountDown(int lane, float seconds)
    {
        _laneTimer[lane]    = seconds;
        _laneInterval[lane] = seconds;   // 360° 표시의 분모

        while (_laneTimer[lane] > 0f)
        {
            _laneTimer[lane] -= Time.deltaTime;
            yield return null;
        }

        _laneTimer[lane] = 0f;
    }

    /// <summary>
    /// 한 마리를 세운다.
    ///
    /// ⚠ 카드 상태는 **예약 시점이 아니라 여기서** 읽는다
    ///   예약과 배출 사이에 스테이지 보상으로 카드 레벨이 오를 수 있다.
    ///   최신 레벨로 나가는 편이 플레이어의 기대와 맞는다 —
    ///   "방금 레벨업했는데 아직 약한 게 나온다" 는 설명할 수 없다.
    /// </summary>
    void SpawnOne(MonsterSpeciesData species, int lane, int spawnedThisStage)
    {
        var summoner = SummonerRuntimeBridge.Current;
        if (summoner == null)
        {
            Debug.LogError("[SummonController] 소환사가 없습니다 — 소환할 수 없습니다.");
            return;
        }

        var deck = UserDataManager.Instance.Get<SummonDeckData>();
        int at   = deck.IndexOf(species.Id);

        // 카드가 덱에서 사라졌으면(합성 재료로 소모 등) 레벨 1로 취급한다.
        SummonDeckSlot card = at >= 0
            ? deck.GetSlot(at)
            : new SummonDeckSlot(SummonKind.Monster, species.Id);

        Vector3 pos = _field.GetSpawnPosition(lane);

        // 살아남으면 이 라인 대기열로 돌아가 다음 판에 다시 나간다 (MonsterLineReturner).
        // 배출 순간의 특성 배율(뒤집힌 과부하·기다림의 미학·매복)은 여기서 굳는다.
        MonsterSpawner.SpawnFromCard(species, summoner.Data, card, pos, lane,
                                     RunPerkRule.DrainStatMult(species, spawnedThisStage));
    }
}
