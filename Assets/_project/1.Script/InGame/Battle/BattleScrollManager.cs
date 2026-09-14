using UnityEngine;

// ============================================================
//  BattleScrollManager.cs
//  배경 스프라이트(BGSprite1/2)를 무한 루프한다.
//
//  ⚠ 카메라 추종은 제거했다 (원작 기능)
//    원작은 선두 아군을 따라 카메라를 오른쪽으로 밀었다. 이 게임에서는
//    그게 마나 환수를 통째로 막는다 —
//    ScreenClampSystem 이 Camera.main 을 직접 참조해 유닛을 화면 안에 가두므로,
//    카메라가 따라가면 클램프 범위도 함께 밀려 몬스터가 환수 경계를 못 넘는다.
//
//    전장은 고정 화면이다. 몬스터가 오른쪽으로 걸어 나가 사라지는 것이
//    이 게임의 기본 동작이라 카메라가 따라갈 이유도 없다.
//    (선두 위치를 계산하던 LeadAllyTrackerSystem 도 함께 제거했다)
//
//  ■ 배경은 난이도로 갈린다 (Easy→BG5 … Inferno→BG1) — 원작의 **거꾸로**다
//    원작은 평화로운 숲(BG1)에서 마왕성(BG5)으로 쳐들어가는 이야기였다.
//    이 게임은 마왕성이 출발점이라, 난이도가 오를수록 용사들의 땅(숲)으로 나아간다
//    (사용자 지시, 2026-09-11). 순서는 씬의 _bgByTier 배열이 정한다 — 코드는 뒤집지 않는다.
//    난이도는 런 시작 시점에 고정되므로 배경도 판 단위로만 바뀐다.
//    바꿔 끼우는 지점은 ResetBackground 하나다 — Start·StartScroll·
//    ResetBackgroundToStart 가 전부 그리로 모이므로 실전·로비 데모·출전 대기가
//    저절로 같은 배경을 쓴다.
//
//  ⚠ 스프라이트를 갈면 _bgWidth 를 반드시 다시 잰다
//    무한 루프가 이 폭 하나로 돈다. 예전 폭을 그대로 두면 두 장 사이가
//    벌어지거나 겹쳐서 이음매가 화면에 그대로 보인다.
//
//  ⚠ 원본 픽셀 크기가 장마다 다르다 — 세로를 맞춰 스케일을 역산한다
//    BG2 만 2143×734 이고 나머지는 3548×1216 이다 (가로세로비는 같다).
//    씬에 박아 둔 scale 2 를 그대로 쓰면 BG2 는 화면 위아래가 비어 버린다.
//    _bgWorldHeight 에 맞춰 균등 스케일을 계산한다.
//
//  씬 설정:
//    Inspector 에서 _bg1 = BGSprite1, _bg2 = BGSprite2 연결
//    _bgByTier 에 BG5·BG4·BG3·BG2·BG1 을 DifficultyTier 순서대로 꽂는다 (비우면 씬 그대로)
//    두 스프라이트는 초기에 나란히 배치되어야 함 (합산 폭 >= 화면 폭 × 2)
// ============================================================

public class BattleScrollManager : Singleton<BattleScrollManager>
{
    [Header("배경 스프라이트")]
    [SerializeField] Transform _bg1;
    [SerializeField] Transform _bg2;

    [Header("난이도별 배경")]
    [Tooltip("DifficultyTier 순서 — 쉬움·보통·어려움·지옥·불지옥 = BG5·BG4·BG3·BG2·BG1 (마왕성 → 숲). 비워 두면 씬에 깔린 스프라이트를 그대로 쓴다.")]
    [SerializeField] Sprite[] _bgByTier = new Sprite[5];

    [Tooltip("배경이 채울 세로 높이(월드 단위). 0 이면 씬에 깔린 BG 를 재서 그 높이를 쓴다.")]
    [SerializeField] float _bgWorldHeight = 0f;


    Camera         _cam;
    SpriteRenderer _sr1;
    SpriteRenderer _sr2;
    float          _bgWidth;
    float          _initCamX;

    bool           _active;

    // ── Unity 생명주기 ────────────────────────────────────────

    void Start()
    {
        // ⚠ Camera.main 을 쓰지 않는다 — 로비 카메라가 함께 떠 있으면 그쪽이 잡힌다
        _cam      = ResolveCamera();
        _initCamX = _cam.transform.position.x;
        ResetBackground(_initCamX);   // 난이도 배경 적용 + 폭 측정까지 여기서 한다
    }

    void OnEnable()
    {
        // ⚠ OnAlliesReady 가 아니라 OnWavesStarted 다
        //   출전 대기 화면에서도 아군은 스폰된다. 그 신호로 추종을 켜면
        //   대기 화면이 잡아 둔 카메라 위치를 이 스크립트가 곧바로 되돌린다
        //   (카메라 X 가 저절로 0 으로 돌아가던 증상).
        BattleManager.OnWavesStarted += OnBattleStart;
        BattleManager.OnVictory      += OnBattleEnd;
        BattleManager.OnDefeat       += OnBattleEnd;

        // 로비에서 난이도를 고르는 즉시 대기 화면 배경도 바뀐다 — 무엇을 고르는지 눈에 보여야 한다.
        DifficultyData.OnChanged += OnDifficultyChanged;
    }

    void OnDisable()
    {
        BattleManager.OnWavesStarted -= OnBattleStart;
        BattleManager.OnVictory      -= OnBattleEnd;
        BattleManager.OnDefeat       -= OnBattleEnd;
        DifficultyData.OnChanged     -= OnDifficultyChanged;
    }

    // ── 배틀 이벤트 ───────────────────────────────────────────

    void OnBattleStart() => StartScroll();
    void OnBattleEnd()   => StopScroll();

    /// <summary>
    /// 난이도 선택이 바뀌었다 — 배경을 그 자리에서 갈아 끼운다.
    ///
    /// ⚠ 캐시를 직접 버린다 — 구독 순서에 기대면 안 된다
    ///   DifficultyConfig 는 CurrentTier() 를 처음 부를 때 비로소 OnChanged 에
    ///   Invalidate 를 건다. 이 스크립트는 OnEnable(=Start 보다 먼저) 에서 구독하므로
    ///   호출 순서가 [이 핸들러] → [Invalidate] 가 되어, 여기서 읽는 난이도가
    ///   **바뀌기 전 값**이었다. 배경이 한 박자 늦게 바뀌던 원인이 이것이다.
    ///   순서에 의존하지 말고 읽기 직전에 스스로 버린다 (여러 번 불러도 무해하다).
    ///
    /// ⚠ 스크롤 중에도 갈아 끼운다
    ///   예전엔 _active 면 그냥 돌아갔는데, 로비 데모 전투가 늘 돌고 있어서
    ///   (LobbyDemoBattle 이 StartScroll 을 부른다) 사실상 항상 무시됐다.
    ///   난이도를 바꾸는 것은 플레이어의 명시적 조작이므로, 배경이 그 자리에서
    ///   즉시 바뀌는 편이 옳다.
    ///
    /// ⚠ 카메라는 건드리지 않는다
    ///   ResetBackgroundToStart 는 배경을 _initCamX 에 깐다. 전진 중에 그러면
    ///   배경만 뒤로 확 끌려가 화면이 크게 튄다. 지금 카메라가 보는 자리에
    ///   다시 깔면 그림만 바뀌고 위치는 유지된다.
    /// </summary>
    void OnDifficultyChanged()
    {
        DifficultyConfig.Invalidate();

        if (!_active) { ResetBackgroundToStart(); return; }

        _cam = ResolveCamera();
        if (_cam == null) return;
        ResetBackground(_cam.transform.position.x);
    }

    // ── 공개 제어 (실전·로비 데모 공용) ───────────────────────

    /// <summary>
    /// 선두 아군 추종을 시작한다. 카메라를 시작 위치로 되돌리고 배경도 다시 깐다.
    ///
    /// ⚠ 실전 진입뿐 아니라 로비 배경 데모도 이 문으로 들어온다
    ///   데모가 BattleManager 이벤트를 흉내 내면 그쪽 상태 기계가 오염된다.
    /// </summary>
    public void StartScroll()
    {
        _cam = ResolveCamera();
        if (_cam == null) return;

        Vector3 camPos = _cam.transform.position;
        camPos.x                = _initCamX;
        _cam.transform.position = camPos;

        _active     = true;

        ResetBackground(_initCamX);
    }

    public void StopScroll() => _active = false;

    /// <summary>
    /// 배경을 시작 위치(_initCamX)로 되돌린다. BattlePanel 로 돌아올 때만 부른다.
    ///
    /// ⚠ 전투가 끝나도 배경은 전진한 자리에 남는다
    ///   스크롤 중 BGSprite1/2 는 카메라를 따라 계속 오른쪽으로 옮겨진다.
    ///   되돌리지 않으면 대기 화면 카메라가 텅 빈 허공을 잡는다.
    ///
    /// ⚠ 카메라 위치가 아니라 시작 위치에 맞춘다
    ///   전투가 시작되면 StartScroll 이 배경을 _initCamX 에 다시 깐다.
    ///   대기 때 다른 자리에 깔아 두면 그 순간 배경이 한 번 덜컹한다.
    ///   같은 자리에 두면 전투 시작 때 아무것도 움직이지 않는다.
    /// </summary>
    public void ResetBackgroundToStart() => ResetBackground(_initCamX);

    /// <summary>
    /// 스크롤이 쓸 카메라. 로비·인게임 카메라가 동시에 있을 수 있으므로
    /// Camera.main 대신 SceneDirector 가 지목한 인게임 카메라를 쓴다.
    /// </summary>
    static Camera ResolveCamera()
    {
        var director = SceneDirector.Instance;
        Camera cam = director != null ? director.InGameCam : null;
        return cam != null ? cam : Camera.main;
    }

    // ── 메인 루프 ─────────────────────────────────────────────

    /// <summary>
    /// 배경 루프만 돌린다.
    ///
    /// ⚠ 카메라 추종은 제거했다 (선두 아군 따라가기)
    ///   원작은 선두 아군을 따라 카메라를 오른쪽으로 밀었다. 이 게임에서는
    ///   그게 **마나 환수를 통째로 막는다**:
    ///
    ///     ScreenClampSystem 이 Camera.main 을 직접 참조해 유닛을 화면 안에
    ///     가둔다. 카메라가 선두를 따라가면 클램프 범위도 함께 밀려, 전장이
    ///     어디까지인지가 매 프레임 달라진다 — 성벽·소환 지점 같은 고정 좌표와
    ///     화면 경계가 어긋나 무엇이 왜 막혔는지 읽을 수 없게 된다.
    ///
    ///   전장은 고정 화면이다. 몬스터가 오른쪽으로 걸어 나가 사라지는 그림이
    ///   이 게임의 기본 동작이므로 카메라가 따라갈 이유도 없다.
    /// </summary>
    void LateUpdate()
    {
        if (!_active) return;

        LoopBackground();
    }

    // ── 배경 처리 ─────────────────────────────────────────────

    void ResetBackground(float camCenterX)
    {
        ApplyDifficultyBackground();
        if (_bgWidth <= 0f) return;
        SetBgX(_bg1, camCenterX);
        SetBgX(_bg2, camCenterX + _bgWidth);
    }

    // ── 난이도별 배경 ─────────────────────────────────────────

    /// <summary>
    /// 지금 난이도에 맞는 배경을 두 장에 깔고 폭을 다시 잰다.
    ///
    /// 배경을 갈아 끼우는 유일한 지점이다 — ResetBackground 만 이걸 부르고,
    /// Start·StartScroll·ResetBackgroundToStart 가 전부 ResetBackground 를 거친다.
    /// </summary>
    void ApplyDifficultyBackground()
    {
        CacheRenderers();
        if (_sr1 == null) return;

        // ⚠ 높이는 배경을 갈기 '전에' 재 둔다
        //   갈고 나서 재면 새 배경의 높이를 기준으로 삼아, 갈 때마다 크기가 눌어붙는다.
        if (_bgWorldHeight <= 0f) _bgWorldHeight = _sr1.bounds.size.y;

        Sprite bg = BackgroundForCurrentTier();
        if (bg != null && _sr1.sprite != bg)
        {
            Fit(_sr1, bg);
            Fit(_sr2, bg);
        }

        // 스프라이트마다 원본 크기가 다르다 — 폭은 항상 지금 깔린 것에서 다시 잰다.
        _bgWidth = CalcBgWidth();
    }

    /// <summary>선택된 난이도의 배경. 배열이 비었거나 칸이 비면 null (= 씬에 깔린 것을 유지).</summary>
    Sprite BackgroundForCurrentTier()
    {
        if (_bgByTier == null || _bgByTier.Length == 0) return null;

        // DifficultyConfig 가 캐시하는 값을 쓴다 — 현재 난이도의 정본 창구가 하나여야 한다.
        int idx = (int)DifficultyConfig.CurrentTier().Tier;
        return _bgByTier[Mathf.Clamp(idx, 0, _bgByTier.Length - 1)];
    }

    /// <summary>
    /// 배경 한 장을 갈아 끼우고 세로를 화면 높이에 맞춘다.
    ///
    /// ⚠ 씬에 박힌 scale 을 그대로 쓰면 안 된다
    ///   BG2 만 2143×734 이고 나머지는 3548×1216 이다. 같은 scale 2 를 먹이면
    ///   BG2 는 세로가 60% 밖에 안 돼 위아래가 텅 빈다.
    ///   가로세로비는 모두 같으므로 세로만 맞추면 가로도 저절로 맞는다.
    /// </summary>
    void Fit(SpriteRenderer sr, Sprite bg)
    {
        if (sr == null) return;
        sr.sprite = bg;

        float h = bg.bounds.size.y;      // 스케일이 안 섞인 원본 세로 (월드 단위)
        if (h <= 0f) return;

        float s = _bgWorldHeight / h;
        sr.transform.localScale = new Vector3(s, s, 1f);
    }

    void CacheRenderers()
    {
        if (_sr1 == null && _bg1 != null) _bg1.TryGetComponent(out _sr1);
        if (_sr2 == null && _bg2 != null) _bg2.TryGetComponent(out _sr2);
    }

    void LoopBackground()
    {
        if (_bgWidth <= 0f) return;

        float halfW    = _cam.orthographicSize * _cam.aspect;
        float leftEdge = _cam.transform.position.x - halfW;

        // 센터 피벗 기준: right edge = position.x + bgWidth * 0.5f
        if (_bg1.position.x + _bgWidth * 0.5f < leftEdge)
            SetBgX(_bg1, _bg2.position.x + _bgWidth);
        else if (_bg2.position.x + _bgWidth * 0.5f < leftEdge)
            SetBgX(_bg2, _bg1.position.x + _bgWidth);
    }

    // ── 유틸 ─────────────────────────────────────────────────

    static void SetBgX(Transform t, float x)
    {
        Vector3 p = t.position;
        p.x = x;
        t.position = p;
    }

    float CalcBgWidth()
    {
        CacheRenderers();
        if (_sr1 != null) return _sr1.bounds.size.x;

        Debug.LogWarning("[BattleScrollManager] _bg1에 SpriteRenderer가 없습니다. bgWidth = 20f 기본값 사용.");
        return 20f;
    }
}
