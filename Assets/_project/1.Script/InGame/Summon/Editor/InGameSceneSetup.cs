using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  InGameSceneSetup.cs  [Editor Only]
//  InGame 씬에 게임플레이 오브젝트를 세운다. UI 는 건드리지 않는다
//  (HUD 는 InGameUIPrefabCreator 담당).
//
//  ■ 세우는 것
//    SummonField      — 5라인 소환 지점 · 소환사 자리
//    Castle           — 마왕성 성벽 스프라이트 (성벽 경계에 맞춰 배치)
//    LaneDividers     — 라인 사이를 가르는 얇은 선 4개
//    HeroSpawner      — 용사 부대 5슬롯 (오른쪽)
//    StageLoopDirector— 스테이지 대기/시작 상태
//    SummonController — 카드 선택 → 라인 탭 → 스폰
//    RunBootstrap     — 마나 지급 · 덱 구성 · 소환사 배치
//
//  ■ 좌표는 원작 씬 스케일에 맞췄다
//    카메라 orthographic size 12, 원작 아군 슬롯이 x≈-12 / y −6~6 이다.
//    그 판을 그대로 쓴다 — 배경·카메라를 다시 잡을 이유가 없다.
//
//      x = -15  성벽 (진입 불가 경계 — 성벽 그림의 바깥면이 여기 맞춰진다)
//      x ≈ -20.2 소환사(마왕성) — 성벽 **안뜰**. 그림이 정한다
//      x = -14  소환 라인 5개 (성문 바로 앞 — 성벽에서 계산한다)
//      x = +26  용사 부대 5슬롯 — 화면 밖 (걸어 들어온다)
//
//  ■ 몇 번을 돌려도 같은 결과다
//    이름으로 찾아 없을 때만 만든다. 이미 손으로 옮겨 둔 지점은 건드리지 않는다.
//
//  사용: Tools > Project K > 씬 셋업 > 인게임 전장
// ============================================================

public static class InGameSceneSetup
{
    const string ScenePath = "Assets/Scenes/InGame.unity";

    const string FieldRootName  = "SummonField";
    const string HeroRootName   = "HeroSpawner";
    const string LoopName       = "StageLoopDirector";
    const string BootName       = "RunBootstrap";
    const string CastleRootName = "Castle";

    // ── 좌표 ─────────────────────────────────────────────────
    //
    //  카메라 orthographic size 12, 16:9 → 화면 가로 절반이 약 21.3 이다.
    //  즉 화면에 보이는 범위가 x ∈ [-21.3, +21.3] 이다.

    /// <summary>
    /// 소환사 자리의 최초 생성값.
    ///
    /// ⚠ 최종 X 는 여기서 정해지지 않는다
    ///   BuildCastle 이 성벽 그림의 안뜰 한가운데로 다시 세운다 —
    ///   "성 안에 서 있다" 를 성립시키는 것은 그림이지 이 숫자가 아니다.
    ///   (PlaceSummonerInCourtyard 참고)
    /// </summary>
    const float SummonerX = -20f;

    /// <summary>
    /// 성벽 — 유닛이 넘어갈 수 없는 왼쪽 한계. 성벽 그림의 바깥면이 여기 맞춰진다.
    ///
    /// 소환사와의 거리는 그대로 공격자의 사거리 보정이 되므로(WallCoverComponent),
    /// 얼마나 띄우든 근접 용사가 소환사를 때리지 못하는 일은 생기지 않는다.
    /// 예전에는 이 거리가 근접 사거리(0.7~1.2)보다 넓으면 패배가 성립하지 않았다.
    ///
    /// ⚠ 이 값 하나가 성의 화면 크기를 정한다 (-17 → -15 로 옮긴 이유)
    ///   성벽 그림은 "바깥면이 이 경계에 닿고 왼쪽 끝은 화면 밖" 두 조건으로
    ///   배율이 결정된다. 즉 성이 화면에서 차지하는 폭 = 이 경계와 화면 왼쪽
    ///   끝 사이의 거리다. -17 일 때 그 폭이 4.3월드(1920 기준 약 195px)뿐이라
    ///   **성 안에 무엇도 놓을 수 없었다** — 소환사는 안뜰 대신 성벽 위로 밀려났고
    ///   라인 대기열은 성 밖 벌판에 그릴 수밖에 없었다.
    ///   -15 로 옮기면 성이 약 285px 이 되어 안뜰에 소환사, 성벽 위에 대기열이
    ///   각자 자리를 갖는다.
    ///
    /// ⚠ -15 → -13 → -12 (사용자 지적, 2026-09-07) — **UI 자리를 성이 만든다**
    ///   왼쪽 가장자리에 시너지 세로 줄이 서면서, 대기열이 성벽에서 왼쪽으로
    ///   177px 자라 그 줄과 부딪혔다. 줄을 좁히거나 대기열을 줄이는 대신
    ///   **성벽을 오른쪽으로 민다** (사용자 확정) — 성이 넓어지면 줄도
    ///   대기열도 제 자리를 갖는다.
    ///     화면 x = (WallX + 21.333) / 42.667 × 1920
    ///       -15 → 285px   ·   -13 → 375px   ·   -12 → 420px
    ///     대기열 = 성벽px − 6 − 171
    ///       -13 → 198~369  ← 시너지 줄이 152 폭일 때(16~168) 30px 여유
    ///       -12 → 243~414  ← 줄이 176 폭이 되며(16~192) 51px 여유
    ///   전장은 36.3 → 33.3 월드로 좁아진다. 용사는 여전히 화면 밖
    ///   (HeroX 26)에서 걸어 들어온다.
    ///
    ///   ⚠ **이 값을 바꾸면 씬을 반드시 다시 구워야 한다**
    ///     (Tools > Project K > 씬 셋업 > 인게임 전장)
    ///     SummonFieldLayout.WallBoundaryX 는 **씬에 직렬화된 값**이라,
    ///     여기 상수만 고치면 게임에서는 아무 일도 일어나지 않는다 —
    ///     실제로 "성벽이 그대로다" 로 한 번 겪었다 (2026-09-07).
    /// </summary>
    const float WallX = -12f;

    /// <summary>
    /// 소환 지점이 성벽에서 떨어지는 거리.
    ///
    /// ⚠ 성문 바로 앞이어야 한다
    ///   멀리 잡으면 몬스터가 벌판 한복판에서 튀어나온 것처럼 보인다.
    ///   성에서 나온 것으로 읽히려면 성벽에 붙어 나와야 한다.
    ///   성벽 그림의 그늘(바깥면에서 오른쪽으로 약 0.8)보다는 밖이어야
    ///   몬스터가 그늘에 묻히지 않는다.
    /// </summary>
    const float LaneOffsetFromWall = 1f;

    /// <summary>소환 지점 X — 성벽에서 계산한다.</summary>
    const float LaneX = WallX + LaneOffsetFromWall;

    /// <summary>
    /// 용사 부대가 서는 자리. <b>화면 밖</b>이어야 한다.
    ///
    /// ⚠ 화면 안에 두면 "등장" 이 성립하지 않는다
    ///   시작을 누른 순간 적이 허공에서 튀어나온 것처럼 보인다.
    ///   밖에 세워야 대열을 갖춘 채 걸어 들어오는 그림이 나온다.
    /// </summary>
    const float HeroX = 24f;

    /// <summary>
    /// 라인 Y 좌표. 카메라 orthographic size 12 라 화면은 y ∈ [-12, 12] 이다.
    ///
    /// 간격 4 — 3 이었을 때 라인끼리 붙어 보여 어느 줄을 눌렀는지 헷갈렸다.
    /// ±8 이면 위아래로 4씩 여유가 남아 유닛이 넘쳐도 화면 밖으로 안 나간다.
    /// </summary>
    static readonly float[] LaneY = { 8f, 4f, 0f, -4f, -8f };

    [MenuItem(ProjectKMenu.Setup + "인게임 전장", priority = ProjectKMenu.SetupPrio)]
    public static void Setup()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        SummonFieldLayout field = BuildField();

        // ⚠ 순서 — 슬롯을 인수한 뒤에 옛 스포너를 지운다.
        //   먼저 지우면 자식이던 Slot_0_General 도 함께 사라진다.
        HeroSpawner hero = BuildHeroSpawner();
        RemoveDeadSpawners();

        BuildCastle(field);
        BuildLaneDividers(field);
        BuildLoopDirector();
        BuildSummonController(field);
        BuildBootstrap(field, hero);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[InGameSceneSetup] 완료 — InGame 씬에 전장 구성을 세웠습니다.\n" +
                  "⚠ 남은 수동 작업: PoolController 에서 'Load Prefabs From Folder' 실행");
    }

    // ── 전장 레이아웃 ────────────────────────────────────────

    static SummonFieldLayout BuildField()
    {
        GameObject root  = FindOrCreate(FieldRootName);
        var        field = GetOrAdd<SummonFieldLayout>(root);

        // 소환사 자리
        if (field.SummonerPoint == null)
            field.SummonerPoint = FindOrCreateChild(root, "SummonerPoint",
                                                    new Vector3(SummonerX, 0f, 0f)).transform;

        // 라인 5개
        if (field.LanePoints == null) field.LanePoints = new List<Transform>();

        field.LanePoints.RemoveAll(t => t == null);

        for (int i = 0; i < SummonFieldLayout.LaneCount; i++)
        {
            string name = $"Lane_{i + 1}";
            Transform lane = FindOrCreateChild(root, name,
                                               new Vector3(LaneX, LaneY[i], 0f)).transform;

            // ⚠ X 는 매번 다시 잡는다 (Y 는 그대로 둔다)
            //   소환 지점이 성문 앞인지는 성벽 위치가 정하므로 손으로 맞춰 둘 값이
            //   아니다. 반면 라인 간격(Y)은 화면을 보며 조정하는 값이라 건드리지 않는다 —
            //   실제로 씬에서 ±8/±4 → ±6/±3 으로 좁혀 둔 상태다.
            Vector3 lanePos = lane.localPosition;
            lanePos.x          = LaneX;
            lane.localPosition = lanePos;

            if (!field.LanePoints.Contains(lane))
            {
                if (i < field.LanePoints.Count) field.LanePoints[i] = lane;
                else                            field.LanePoints.Add(lane);
            }
        }

        field.WallBoundaryX = WallX;

        EditorUtility.SetDirty(field);
        return field;
    }

    // ── 마왕성 성벽 ──────────────────────────────────────────
    //
    //  ■ 이 게임은 위에서 내려다보는 시점이다
    //    배경이 위에서 본 흙 벌판이고, Transparency Sort Axis 가 (0,1,0) 이다.
    //    즉 Y 는 높이가 아니라 깊이다. 그래서 성벽은 화면 왼쪽에 **세로로 뻗은 띠**이고,
    //    성벽 윗면(통로)이 그대로 보인다. 소환사는 그 통로 위 좌표에 선다 —
    //    캐릭터를 위로 띄우는 것이 아니다.
    //
    //  ■ 아래 픽셀 좌표는 FortressWall_Vertical.png 를 실측한 값이다
    //    그림을 다시 그려 끼우면 이 값도 같이 재야 한다. 눈대중으로 맞추면
    //    성벽 그림과 진입 불가 경계가 조용히 어긋난다 — 용사가 벽 앞 허공에서
    //    멈추거나, 반대로 벽을 파고든 것처럼 보인다.

    const string CastleSpritePath = "Assets/_project/3.Textures/FortressWall_Vertical.png";

    const int CastleTexWidth = 224;

    /// <summary>
    /// 성벽 바깥면의 픽셀 X. **여기가 용사가 멈추는 선(WallBoundaryX)이 된다.**
    ///
    /// ⚠ 텍스처 오른쪽 끝(224)이 아니다
    ///   그림의 오른쪽 32px 은 성 밖 그늘이다. 끝을 경계로 잡으면 용사가
    ///   그늘만큼 벽에서 떨어진 자리에서 멈춰 허공을 때리는 것처럼 보인다.
    /// </summary>
    const int CastleWallFacePx = 192;

    /// <summary>
    /// 성벽 상면(통로) 구간. 라인 대기열이 이 위(와 그 오른쪽 흉벽)에 얹힌다.
    /// </summary>
    const int CastleWalkwayStartPx = 90;
    const int CastleWalkwayEndPx   = 149;

    /// <summary>
    /// 안뜰(성 안쪽 판석) 이 끝나고 성벽 몸통이 시작되는 픽셀.
    /// <b>소환사는 이 왼쪽에 선다</b> — 성벽 위가 아니라 성 안이다.
    /// </summary>
    const int CastleCourtyardEndPx = 85;

    /// <summary>스프라이트 임포트 PPU. 배치 계산이 전부 이 값을 전제로 한다.</summary>
    const float CastlePixelsPerUnit = 100f;

    /// <summary>
    /// 성벽 크기를 잡는 기준 가로세로비. 이 게임은 1920×1080 기준이다.
    ///
    /// 에디터의 Game 뷰 비율을 읽지 않는다 — 그때그때 다른 값이 잡히면
    /// 씬에 저장되는 성벽 크기가 사람마다 달라진다.
    ///
    /// ⚠ 실제 기기가 이보다 넓을 수 있다 — 그건 배율이 아니라 뒷판이 해결한다
    ///   비율이 넓어질수록 화면 왼쪽이 더 열리는데, 성벽을 그만큼 키우면
    ///   기기마다 성 크기가 달라지고 21:9 에서는 화면 3분의 1을 성이 먹는다.
    ///   성벽은 이 기준으로 고정하고, 그 왼쪽은 CastleBackfill 이 덮는다.
    /// </summary>
    const float ReferenceAspect = 16f / 9f;

    /// <summary>
    /// 성벽이 기준 화면 왼쪽 끝보다 더 왼쪽까지 덮을 여유(월드).
    /// 0 으로 두면 반올림 한 번에 왼쪽 가장자리로 흙바닥이 비친다.
    /// </summary>
    const float CastleLeftMargin = 0.3f;

    /// <summary>
    /// 뒷판이 덮을 가로세로비 한계. 21:9 울트라와이드까지 감당한다.
    /// 뒷판은 단색 한 장이라 크게 잡아도 비용이 없다.
    /// </summary>
    const float BackfillMaxAspect = 3f;

    /// <summary>성 안쪽 깊은 곳의 색. 성벽 그림의 안뜰 판석 색에서 뽑았다.</summary>
    static readonly Color32 BackfillColor = new Color32(0x22, 0x22, 0x2E, 255);

    const string BackfillSpritePath = "Assets/_project/3.Textures/BG/CastleFill.png";
    const string BackfillName       = "CastleBackfill";

    /// <summary>
    /// 성벽 스프라이트를 세우고, 소환사를 그 통로 위에 올린다.
    ///
    /// ■ 크기를 상수로 박지 않고 계산한다
    ///   성벽 바깥면은 WallBoundaryX 에 닿아야 하고, 동시에 그림 왼쪽 끝은
    ///   화면 왼쪽 밖까지 나가야 한다(안 그러면 성 안뜰 옆에 흙바닥이 비친다).
    ///   이 두 조건이 배율을 하나로 결정한다 — 카메라나 성벽 경계를 옮기면
    ///   저절로 따라온다.
    ///
    /// ■ BG 의 자식으로 넣지 않는다
    ///   BattleScrollManager.LoopBackground 가 BGSprite1/2 를 재배치한다.
    ///   자식으로 두면 성벽이 배경을 따라 끌려간다.
    ///
    /// ■ sortingOrder = -1
    ///   배경(-2) 위, 유닛(SortingGroup 100) 아래다. 그래야 소환사는 성벽 위에
    ///   선 것으로, 용사는 성벽 앞에 붙어 선 것으로 보인다.
    /// </summary>
    static void BuildCastle(SummonFieldLayout field)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CastleSpritePath);
        if (sprite == null)
        {
            Debug.LogError($"[InGameSceneSetup] 성벽 스프라이트가 없습니다: {CastleSpritePath}");
            return;
        }

        EnsureCastleImportSettings();

        Camera cam = FindSceneCamera();

        // ── 배율 — "바깥면은 경계에, 왼쪽 끝은 화면 밖" 두 조건에서 나온다
        float camLeft   = cam.transform.position.x - cam.orthographicSize * ReferenceAspect;
        float leftNeed  = field.WallBoundaryX - camLeft + CastleLeftMargin;
        float scale     = leftNeed / (CastleWallFacePx / CastlePixelsPerUnit);
        float pxToWorld = scale / CastlePixelsPerUnit;

        // ── 위치 — 피벗이 가운데이므로 바깥면 픽셀만큼 왼쪽으로 민다
        float centerX = field.WallBoundaryX
                      - (CastleWallFacePx - CastleTexWidth * 0.5f) * pxToWorld;

        GameObject root = FindOrCreate(CastleRootName);
        var        sr   = GetOrAdd<SpriteRenderer>(root);

        sr.sprite          = sprite;
        sr.sortingOrder    = 0;   // 배경(-2)·뒷판(-1) 위, 유닛(100) 아래
        sr.spriteSortPoint = SpriteSortPoint.Center;

        root.transform.localScale = new Vector3(scale, scale, 1f);
        root.transform.position   = new Vector3(centerX, cam.transform.position.y, 0f);

        // ⚠ 맞는 연출을 받는 것은 이 그림이다 (마왕 본체가 아니라)
        //   캐릭터 HP = 마왕성 HP 라서 피해는 소환사 엔티티로 들어오지만,
        //   번쩍이고 흔들려야 하는 것은 성벽이다. UnitHitSystem 이 본체 플래시를
        //   끄고, BattleStatCollectorSystem 이 같은 피해 기록을 읽어 여기로 넘긴다.
        GetOrAdd<CastleWallView>(root);

        float castleLeft = centerX - CastleTexWidth * 0.5f * pxToWorld;
        BuildCastleBackfill(cam, castleLeft);

        PlaceSummonerInCourtyard(field, centerX, pxToWorld);

        EditorUtility.SetDirty(root);
    }

    /// <summary>
    /// 성벽 왼쪽을 덮는 뒷판.
    ///
    /// ■ 왜 필요한가 — 화면이 넓어지면 성 옆으로 벌판이 비친다
    ///   성벽은 16:9 기준으로 크기가 고정돼 있다. 실제 기기가 더 넓으면
    ///   카메라 왼쪽이 그만큼 더 열리고, 성 안뜰 왼편으로 흙바닥이 드러난다.
    ///   성 안에 벌판이 보이는 셈이라 그림이 통째로 깨진다.
    ///
    /// ■ 왜 성벽을 키우지 않나
    ///   성벽 배율을 화면 비율에 맞춰 늘리면 기기마다 성 크기가 달라지고,
    ///   울트라와이드에서는 성이 화면 3분의 1을 먹는다. 게다가 배율이 바뀌면
    ///   통로 위치도 함께 움직여 소환사가 성 위에서 내려온다.
    ///
    /// ■ 단색 한 장이면 충분하다
    ///   여기는 유닛이 절대 들어올 수 없는 구역이고, 노출되는 것도 화면 맨 끝
    ///   가장자리뿐이다. 안뜰 판석보다 살짝 어두운 색이라 "성 안쪽 깊은 곳" 으로 읽힌다.
    /// </summary>
    static void BuildCastleBackfill(Camera cam, float castleLeft)
    {
        Sprite fill = EnsureBackfillSprite();

        GameObject go = FindOrCreate(BackfillName);
        var        sr = GetOrAdd<SpriteRenderer>(go);

        sr.sprite          = fill;
        sr.color           = BackfillColor;
        sr.sortingOrder    = -1;   // 배경(-2) 위, 성벽(0) 아래
        sr.spriteSortPoint = SpriteSortPoint.Center;

        // 왼쪽 끝은 감당할 수 있는 가장 넓은 화면까지, 오른쪽은 성벽 밑으로 조금 파고든다.
        // (딱 맞추면 반올림 한 줄이 비어 실금처럼 보인다)
        float camX  = cam.transform.position.x;
        float left  = camX - cam.orthographicSize * BackfillMaxAspect;
        float right = castleLeft + 0.5f;

        // 세로는 넉넉히 — 화면이 세로로 길어져도 남는다.
        float height = cam.orthographicSize * 4f;

        float unit = fill.rect.width / fill.pixelsPerUnit;   // 스프라이트 한 장의 월드 크기

        go.transform.position   = new Vector3((left + right) * 0.5f, cam.transform.position.y, 0f);
        go.transform.localScale = new Vector3((right - left) / unit, height / unit, 1f);

        EditorUtility.SetDirty(go);
    }

    /// <summary>
    /// 뒷판에 쓸 단색 스프라이트. 없으면 만든다.
    ///
    /// 색은 SpriteRenderer 가 입히므로 그림 자체는 흰색 한 칸이면 된다 —
    /// 색을 바꾸려고 PNG 를 다시 굽는 일이 없다.
    /// </summary>
    static Sprite EnsureBackfillSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(BackfillSpritePath);
        if (existing != null) return existing;

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px  = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();

        // ⚠ 상대 경로를 그대로 쓰지 않는다 — 작업 디렉터리가 프로젝트 루트라는 보장이 없다
        string full = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(Application.dataPath, "..", BackfillSpritePath));

        System.IO.File.WriteAllBytes(full, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(BackfillSpritePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(BackfillSpritePath);
        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = CastlePixelsPerUnit;
        importer.filterMode          = FilterMode.Point;
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled       = false;
        importer.SaveAndReimport();

        Debug.Log($"[InGameSceneSetup] 뒷판 스프라이트 생성 — {BackfillSpritePath}");

        return AssetDatabase.LoadAssetAtPath<Sprite>(BackfillSpritePath);
    }

    /// <summary>
    /// 안뜰 안에서 소환사가 서는 자리 — 0 이 안뜰 왼쪽 끝, 1 이 성벽 몸통과 닿는 선.
    ///
    /// ⚠ 0.5 → 0.78 (사용자 지적, 2026-09-08) — <b>왼쪽 UI 줄기에 가렸다</b>
    ///   안뜰 한가운데(0.5)는 화면 x≈93px 이다. 화면 왼쪽 가장자리에는
    ///   특성 줄·체력 막대·시너지 줄이 x 16~192 로 서 있어, 소환사가 그
    ///   아래에 통째로 묻혔다. 0.78 이면 x≈145px 로 나와 성벽 쪽으로 한 발
    ///   앞에 선다.
    ///
    /// ⚠ 1 로 밀지 말 것 — 성벽 몸통에 붙으면 다시 "성벽 위에 올라선" 그림이 된다.
    ///   안뜰 판석 위에 온전히 서 있어야 한다.
    ///
    /// ⚠ 이 값을 바꾸면 씬을 다시 구워야 한다 (씬 셋업 > 인게임 전장) —
    ///   소환사 자리는 씬에 직렬화된다.
    /// </summary>
    const float CourtyardBias = 0.78f;

    /// <summary>
    /// 소환사를 성 안뜰에 세운다.
    ///
    /// ■ 성벽 "위" 가 아니라 성벽 "안" 이다
    ///   예전에는 통로(CastleWalkway*) 한가운데에 세웠다. 이 게임은 내려다보는
    ///   시점이라 통로 = 성벽 윗면이고, 그러면 소환사가 **흉벽 사이에 올라선**
    ///   그림이 된다. 마왕성의 주인이 성벽 위에서 망을 보는 모양새라
    ///   "성 안에서 소환한다" 로 읽히지 않는다.
    ///   안뜰(왼쪽 판석 구간)으로 내리면 성 안에 있는 것으로 읽히고,
    ///   덤으로 성벽 위가 비어 라인 대기열이 얹힐 자리가 생긴다.
    ///
    /// ■ X 는 그림이 정한다 (Y 는 건드리지 않는다)
    ///   안뜰이 어디까지인지는 그림만 안다. 좌표를 손으로 박아 두면 그림을
    ///   갈아 끼울 때마다 소환사만 엉뚱한 자리에 남는다.
    ///
    /// ⚠ 이 좌표는 그림 문제만이 아니다
    ///   성벽과 소환사 사이 거리(WallGap)가 그대로 공격자의 사거리 보정이 된다
    ///   (WallCoverComponent). 안뜰로 내리면 그 거리가 1.7 → 5.2 로 늘어난다 —
    ///   근접 용사(사거리 0.7~1.2)가 성벽 너머로 닿기에는 충분하지만,
    ///   더 왼쪽으로 밀면 보정이 과해진다.
    /// </summary>
    static void PlaceSummonerInCourtyard(SummonFieldLayout field, float centerX, float pxToWorld)
    {
        float courtyardMidPx = CastleCourtyardEndPx * CourtyardBias;
        float summonerX      = centerX + (courtyardMidPx - CastleTexWidth * 0.5f) * pxToWorld;

        Transform point = field.SummonerPoint;
        Vector3   pos   = point.position;
        pos.x          = summonerX;
        point.position = pos;

        EditorUtility.SetDirty(point);

        Debug.Log($"[InGameSceneSetup] 소환사를 성 안뜰에 세웠습니다 — x {summonerX:0.00} " +
                  $"(성벽 {field.WallBoundaryX:0.00} 과의 거리 {field.WallBoundaryX - summonerX:0.00})");
    }

    // ── 라인 구분선 ──────────────────────────────────────────
    //
    //  ■ 왜 필요한가 — 라인은 좌표일 뿐 화면에 아무 표시가 없다
    //    소환 지점 5개의 Y 좌표가 라인의 전부다. 벌판에는 아무 것도 그려져 있지
    //    않으니 플레이어는 "지금 누른 곳이 몇 번 라인인가" 를 눈으로 확인할 수
    //    없고, 몬스터가 나온 뒤에야 어느 줄에 걸었는지 안다.
    //
    //  ■ 선은 라인 위가 아니라 라인 **사이**에 긋는다
    //    라인 위에 그으면 그 선이 몬스터 발밑을 가로지른다. 사이에 그어야
    //    "이 띠 안이 3번 라인" 으로 읽히고, 탭 판정(GetNearestLane)이 실제로
    //    나누는 경계와도 일치한다.
    //
    //  ■ 성벽에서 시작한다
    //    성 안까지 그으면 안뜰을 가로질러 소환사 발밑을 지난다.

    const string DividerRootName = "LaneDividers";

    /// <summary>구분선 두께(월드). 1080 세로 기준 약 4px — 있는 줄만 알면 된다.</summary>
    const float DividerThickness = 0.09f;

    /// <summary>구분선이 성벽 바깥면에서 오른쪽으로 뻗는 길이(월드).</summary>
    const float DividerLength = 40f;

    /// <summary>구분선 색. 흙바닥 위에서 겨우 보일 만큼만 밝다.</summary>
    static readonly Color DividerColor = new Color(1f, 0.94f, 0.78f, 0.22f);

    /// <summary>
    /// 라인 사이를 가르는 얇은 선을 놓는다. 라인이 5개면 선은 4개다.
    ///
    /// ⚠ 라인 Y 를 씬에서 옮겼으면 이걸 다시 돌려야 한다
    ///   선 위치는 라인 좌표의 중점에서 계산한다. 라인만 옮기고 이 함수를
    ///   돌리지 않으면 선과 라인이 조용히 어긋난다.
    /// </summary>
    static void BuildLaneDividers(SummonFieldLayout field)
    {
        Sprite fill = EnsureBackfillSprite();   // 흰 사각형 한 장 — 색은 여기서 입힌다
        if (fill == null) return;

        GameObject root = FindOrCreate(DividerRootName);

        float unit   = fill.rect.width / fill.pixelsPerUnit;   // 스프라이트 한 장의 월드 크기
        float startX = field.WallBoundaryX;
        float centerX = startX + DividerLength * 0.5f;

        int count = field.LanePoints.Count - 1;

        for (int i = 0; i < count; i++)
        {
            // 인접한 두 라인의 중점 — 여기가 두 라인을 가르는 경계다.
            float y = (field.LanePoints[i].position.y + field.LanePoints[i + 1].position.y) * 0.5f;

            GameObject go = FindOrCreateChild(root, $"Divider_{i + 1}", Vector3.zero);
            var        sr = GetOrAdd<SpriteRenderer>(go);

            sr.sprite          = fill;
            sr.color           = DividerColor;
            sr.sortingOrder    = 1;   // 배경(-2)·뒷판(-1)·성벽(0) 위, 유닛(100) 아래
            sr.spriteSortPoint = SpriteSortPoint.Center;

            go.transform.position   = new Vector3(centerX, y, 0f);
            go.transform.localScale = new Vector3(DividerLength / unit, DividerThickness / unit, 1f);

            EditorUtility.SetDirty(go);
        }

        // 라인 수를 줄였을 때 남는 선을 치운다.
        for (int i = count; ; i++)
        {
            Transform extra = root.transform.Find($"Divider_{i + 1}");
            if (extra == null) break;
            Undo.DestroyObjectImmediate(extra.gameObject);
        }

        EditorUtility.SetDirty(root);

        Debug.Log($"[InGameSceneSetup] 라인 구분선 {count}개 배치");
    }

    /// <summary>
    /// 성벽 스프라이트의 임포트 설정을 보장한다.
    ///
    /// 배치 계산이 PPU 100 을 전제로 하므로 이 값이 어긋나면 성벽 크기가 통째로
    /// 달라진다. 픽셀아트라 필터도 Point 여야 한다 — Bilinear 이면 돌 이음매가 뭉갠다.
    /// </summary>
    static void EnsureCastleImportSettings()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(CastleSpritePath);

        bool changed = importer.textureType         != TextureImporterType.Sprite
                    || importer.spriteImportMode    != SpriteImportMode.Single
                    || importer.spritePixelsPerUnit != CastlePixelsPerUnit
                    || importer.filterMode          != FilterMode.Point
                    || importer.textureCompression  != TextureImporterCompression.Uncompressed
                    || importer.maxTextureSize      < 2048;

        if (!changed) return;

        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = CastlePixelsPerUnit;
        importer.filterMode          = FilterMode.Point;
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize      = 2048;
        importer.mipmapEnabled       = false;

        importer.SaveAndReimport();
    }

    // ── 용사 스포너 ──────────────────────────────────────────

    /// <summary>
    /// 용사 부대 슬롯을 세운다.
    ///
    /// ■ 원작 아군 배치를 그대로 인수한다
    ///   원작 씬에는 AllySpawner 가 쓰던 Slot_0_General 오브젝트 5개가 있다.
    ///   이 게임의 용사 편성도 **같은 5슬롯 대형**이므로 그 오브젝트를 그대로
    ///   가져다 쓴다 — 새로 만들고 옛것을 남겨 두면 하이어라키에 죽은 짝이 생긴다.
    ///
    ///   달라지는 건 위치뿐이다. 아군 자리(왼쪽 화면 안)에서
    ///   적 자리(오른쪽 화면 밖)로 좌우를 뒤집는다.
    /// </summary>
    static HeroSpawner BuildHeroSpawner()
    {
        GameObject root    = FindOrCreate(HeroRootName);
        var        spawner = GetOrAdd<HeroSpawner>(root);

        if (spawner.SquadPoints == null) spawner.SquadPoints = new List<Transform>();
        spawner.SquadPoints.RemoveAll(t => t == null);

        List<Transform> legacy = FindLegacySlots();

        for (int i = 0; i < HeroDeployment.MaxSquads; i++)
        {
            string name = $"Squad_{i + 1}";
            Transform slot;

            if (i < legacy.Count)
            {
                // 원작 슬롯을 인수 — 이름·부모·위치를 용사 기준으로 다시 잡는다.
                slot = legacy[i];
                slot.name = name;
                slot.SetParent(root.transform, false);
            }
            else
            {
                slot = FindOrCreateChild(root, name, Vector3.zero).transform;
            }

            slot.localPosition = new Vector3(HeroX, LaneY[i], 0f);

            if (!spawner.SquadPoints.Contains(slot))
            {
                if (i < spawner.SquadPoints.Count) spawner.SquadPoints[i] = slot;
                else                               spawner.SquadPoints.Add(slot);
            }
        }

        EditorUtility.SetDirty(spawner);
        return spawner;
    }

    /// <summary>씬에 남아 있는 원작 아군 슬롯(Slot_0_General…)을 찾는다.</summary>
    static List<Transform> FindLegacySlots()
    {
        var found = new List<Transform>();

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name.StartsWith("Slot_0_General")) found.Add(t);
        }

        // 이름 순서가 곧 슬롯 순서다 ("Slot_0_General", "… (1)", "… (2)" …).
        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return found;
    }

    // ── 죽은 오브젝트 정리 ───────────────────────────────────

    /// <summary>
    /// 스크립트가 사라진 원작 스포너 오브젝트를 치운다.
    ///
    /// AllySpawner / EnemySpawner 는 코드째 제거됐다. 씬에 남으면 Missing 컴포넌트로
    /// 뜨고, 무엇이 살아 있는 구성인지 읽기 어려워진다.
    /// </summary>
    static void RemoveDeadSpawners()
    {
        foreach (string name in new[] { "AllySpawner", "EnemySpawner" })
        {
            GameObject go = GameObject.Find(name);
            if (go == null) continue;

            Undo.DestroyObjectImmediate(go);
            Debug.Log($"[InGameSceneSetup] 사용하지 않는 '{name}' 오브젝트를 제거했습니다.");
        }
    }

    // ── 루프 · 컨트롤러 · 부트스트랩 ─────────────────────────

    static void BuildLoopDirector()
    {
        GameObject go = FindOrCreate(LoopName);
        GetOrAdd<StageLoopDirector>(go);
        EditorUtility.SetDirty(go);
    }

    static void BuildSummonController(SummonFieldLayout field)
    {
        // 컨트롤러는 루프 오브젝트에 같이 붙인다 — 둘 다 "소환 조작" 한 덩어리다.
        GameObject go         = FindOrCreate(LoopName);
        var        controller = GetOrAdd<SummonController>(go);

        var so = new SerializedObject(controller);
        so.FindProperty("_field").objectReferenceValue = field;

        // 카메라를 인스펙터에 박아 둔다.
        // 런타임에 Camera.main 으로 찾을 수도 있지만, 씬 전환 직후 한 프레임은
        // 앞 씬 카메라가 잡힐 수 있다. 씬에 있는 것을 직접 걸어 두는 편이 확실하다.
        Camera cam = FindSceneCamera();
        if (cam != null) so.FindProperty("_cam").objectReferenceValue = cam;

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
    }

    static void BuildBootstrap(SummonFieldLayout field, HeroSpawner hero)
    {
        GameObject go   = FindOrCreate(BootName);
        var        boot = GetOrAdd<RunBootstrap>(go);

        var so = new SerializedObject(boot);
        so.FindProperty("_field").objectReferenceValue       = field;
        so.FindProperty("_heroSpawner").objectReferenceValue = hero;

        // ⚠ 카드 3택은 팝업이라 여기서 걸어 줄 것이 없다 (CardSelectPopup).
        //   진화·융합 창은 HUD 안에 남아 있고, 런타임이 씬에서 찾는다.

        // 소환사가 비어 있으면 프로젝트에서 찾아 채운다.
        //
        // ⚠ 시작 로스터를 채우던 코드는 없앴다 (2026-08-27)
        //   덱 편성이 사라지고 시작 카드가 소환사에 붙박이가 되면서
        //   RunBootstrap._startingRoster 필드 자체가 없어졌다.
        //   무엇을 데리고 시작하는지는 이제 SummonerData.StarterMonsters 가 정한다.
        FillIfEmpty(so.FindProperty("_summoner"), FindSummoner());

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(boot);
    }

    // ── 자동 채우기 ──────────────────────────────────────────

    static void FillIfEmpty(SerializedProperty prop, Object value)
    {
        if (prop.objectReferenceValue == null) prop.objectReferenceValue = value;
    }

    /// <summary>
    /// 이 런에 쓸 소환사를 고른다.
    ///
    /// 캐릭터 선택 화면(RunSetup)이 아직 없어서 하나를 자동으로 집는다.
    /// 시작 카드가 비어 있는 소환사는 소환을 아예 못 하므로 건너뛴다 —
    /// 그런 캐릭터가 잡히면 "카드 바가 비어 있다" 로만 보여 원인을 찾기 어렵다.
    /// </summary>
    static SummonerData FindSummoner()
    {
        string[] guids = AssetDatabase.FindAssets("t:SummonerData");

        SummonerData fallback = null;

        foreach (string guid in guids)
        {
            var so = AssetDatabase.LoadAssetAtPath<SummonerData>(
                         AssetDatabase.GUIDToAssetPath(guid));
            if (so == null) continue;

            fallback ??= so;

            if (so.StarterMonsters != null && so.StarterMonsters.Length > 0)
                return so;
        }

        if (fallback == null)
            Debug.LogWarning("[InGameSceneSetup] SummonerData 를 찾지 못했습니다. " +
                             "Tools > Project K > 데이터 생성 > 소환사 를 먼저 실행하세요.");

        return fallback;
    }

    /// <summary>씬의 인게임 카메라를 찾는다. MainCamera 태그를 우선한다.</summary>
    static Camera FindSceneCamera()
    {
        if (Camera.main != null) return Camera.main;

        Camera[] all = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        return all.Length > 0 ? all[0] : null;
    }

    static T FindFirst<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // ── 씬 조작 헬퍼 ─────────────────────────────────────────

    static GameObject FindOrCreate(string name)
    {
        GameObject found = GameObject.Find(name);
        if (found != null) return found;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    static GameObject FindOrCreateChild(GameObject parent, string name, Vector3 localPos)
    {
        Transform found = parent.transform.Find(name);
        if (found != null) return found.gameObject;

        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        return go;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
        => go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
}
