using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// InGameHUD의 상단 정보와 하단 소환 덱을 현재 게임 기준으로 다시 구성한다.
public static class InGameUIPrefabCreator
{
    const string ScenePath = "Assets/Scenes/InGame.unity";
    const string HudPath   = "Assets/_project/2.Prefabs/UI/InGame/InGameHUD.prefab";
    const string CardPath  = "Assets/_project/2.Prefabs/UI/InGame/SummonCard.prefab";
    const string Tag       = "InGameUIPrefabCreator";

    // ── 상단바 오른쪽 배치 (오른쪽 끝에서부터의 여백) ──
    //   말풍선 → 일시정지 → 배속 → 스테이지 순으로 안쪽으로 들어온다.
    // ── 라인 대기열 표시 ──
    const int   LaneCount         = SummonFieldLayout.LaneCount;
    const int   QueueDisplayLimit = SummonReservation.DisplayLimit;
    // ── 라인 대기열 격자 ──
    //   셀 하나 = 초상화 + 숫자. 2칸이 차면 다음 줄로 넘어간다.
    //
    //   ⚠ 세로 2줄 × 가로 4칸에서 세로 3줄 × 가로 2칸으로 바꿨다
    //     대기열이 성벽 안으로 들어가면서 쓸 수 있는 가로폭이 성의 폭(1920 기준
    //     약 285px)으로 묶였다. 4칸(339px)은 성을 넘어 벌판으로 삐져나온다.
    //     2칸이면 171px 이라 성벽 위에 온전히 얹힌다.
    //
    //     세로는 3줄까지만 가능하다 — 라인 간격이 약 135px 이고
    //     3줄(126px)이 거기 겨우 들어간다. 4줄로 늘리면 위아래 라인의
    //     대기열이 서로 겹쳐 어느 줄 것인지 못 읽는다.
    //
    //   ⚠ 숫자 칸은 세 자리("999")가 들어갈 폭이어야 한다
    //     좁게 잡았더니 두 자리부터 초상화 위로 넘쳐 겹쳐 보였다.
    //     물량 종족을 여러 번 걸면 세 자리는 쉽게 나온다.
    const int   QueueColumns  = 2;
    const int   QueueRows     = 3;                          // DisplayLimit(6) ÷ 열 수
    const float QueueSlotSize = 38f;                        // 초상화
    const float QueueNumW     = 46f;                        // 숫자 ("999" 기준)
    const float QueueCellW    = QueueSlotSize + QueueNumW;
    const float QueueCellH    = 40f;
    const float QueueSpacing  = 3f;

    // ── 카드 배지 — 마나(왼쪽 위) · 마릿수(오른쪽 위) ──
    //
    //   [아이콘][숫자] 를 가로로 나란히 놓는다 (2026-09-03, 사용자 확정).
    //   왜 아이콘 위에 얹지 않는지는 EditorUIBuilder.IconValueBadge 주석 참고 —
    //   그림 위에 앉히면 세로 자리·글자색·폰트 상한이 전부 PNG 에 종속된다.
    //
    //   ⚠ 카드 폭(150)에 **두 개가 들어가야 한다** — 이걸 안 재서 겹쳤다
    //     나란히 놓으면 배지가 넓어진다. 검산은 반드시 이 식으로 한다.
    //         BadgeWidthFor(아이콘, 폰트) × 2 + BadgeInset × 2 ≤ CardWidth
    //         (36 + 4 + 41) × 2 + (−6) × 2 = 162 − 12 = 150  ≤ 150   ✔
    //
    //   ⚠ 여백이 **음수**다 — 배지를 카드 밖으로 6px 내민다
    //     카드가 여덟 장 붙어 늘어선 좁은 바라 150px 안에서는 아이콘이
    //     30px 밑으로 내려간다. 카드 사이 간격(14)의 절반씩을 빌려 오면
    //     아이콘을 36 까지 세울 수 있고, 이웃 카드의 배지와는 2px 남는다.
    //     ⚠ 카드 간격(HorizontalLayoutGroup.spacing)을 줄이면 여기가 먼저 깨진다.
    //     세로로도 6px 올라가지만 하단바(172)가 카드(150)보다 높아 바 안에 남는다.
    //
    //   ⚠ 카드 폭 150 은 더 못 늘린다 — 10칸이 1622px 안에 들어가야 한다
    //     (여백 16 + 간격 14×9 를 빼면 칸당 148 뿐이다).
    const float BadgeIconSize = 36f;
    const float BadgeInset    = -6f;

    //   숫자가 카드 면(늘 어두운 남색) 위에 앉으므로 UIScale 단계를 그대로 쓴다.
    //   FontSm 이 읽히는 하한이다 — 이보다 내리려면 아이콘이 아니라 카드를 키워야 한다.
    const float BadgeFontSize = UIScale.FontSm;

    // ⚠ 글자색은 **바탕(카드 면)** 을 보고 정한다 — 아이콘 밝기가 아니다
    //   숫자가 더 이상 그림 위에 앉지 않는다. 어두운 남색 면 위이므로 둘 다 밝은 색이고,
    //   어느 아이콘의 값인지 곁눈으로도 짝지어지도록 각 아이콘의 색조를 따라간다.
    //   ⚠ 마나 숫자 색의 정본은 여기가 아니라 SummonCardUI 다
    //     원가/과부하/할인에 따라 흰·붉은·초록으로 갈리므로 런타임이 매번 덮어쓴다.
    //     여기 값은 프리팹 기본값이고, 런타임의 '원가' 색(NormalCostColor)과 같아야
    //     에디터에서 본 모습과 실제가 어긋나지 않는다.
    static readonly Color ManaNumberColor  = Color.white;
    static readonly Color CountNumberColor = new(0.96f, 0.94f, 0.87f);   // 해골 — 크림색

    // ⚠ 16 → 44 (2026-09-02) — 오른쪽 끝 UI 가 잘려 보이던 것과 같은 이유
    //   일시정지·배속·스테이지가 이 값에서 차례로 안쪽으로 들어온다.
    //   기기 안전영역을 아직 다루지 않으므로 여백으로 버틴다.
    const float PauseRightInset  = 44f;
    const float SpeedRightInset  = PauseRightInset + UIScale.BtnSm + 10f;

    /// <summary>
    /// 전황 버튼 자리 — <b>배속 바로 옆</b> (사용자 지적, 2026-09-08).
    ///
    /// ⚠ 스테이지 표시와 자리를 맞바꿨다
    ///   전에는 띠의 맨 왼쪽 끝(스테이지 바깥)이라 배속·일시정지와 멀찍이
    ///   떨어져 있었다. 셋 다 "언제든 누르는 버튼" 인데 하나만 글자 표시
    ///   건너편에 서 있으면 한 묶음으로 읽히지 않는다.
    ///   ⚠ 스테이지 표시는 버튼이 아니다 — 누르는 것 셋을 붙여 두고
    ///     읽기만 하는 것을 바깥으로 뺀다.
    /// </summary>
    const float InfoRightInset   = SpeedRightInset + 108f + 14f;

    /// <summary>
    /// 스테이지 표시 자리 — 오른쪽 띠의 마지막 칸.
    ///
    /// ⚠ 항목을 더 넣으려면 이 값에서 이어 가되, 상단바 왼쪽의 보유 특성
    ///   줄(x 16~1102)과 부딪히지 않는지 볼 것.
    ///   지금 띠는 오른쪽 끝에서 670 까지 = x 1250 에서 시작한다 (148px 여유).
    /// </summary>
    const float StageRightInset  = InfoRightInset + UIScale.BtnSm + 14f;

    /// <summary>시너지 칸 수 = 표식 종류 수. 전부 켜져도 자리가 모자라지 않는다.</summary>
    static readonly int MonsterSynergySlots = MonsterSynergyRule.AllTags.Length;

    const int MaxDeckSlots = 10;
    // ── 보유 특성 줄 ──
    //
    //  ■ 칸 수에 따라 **두 모습**을 갖는다 (사용자 확정, 2026-09-10)
    //      14개 이하 → 아이콘 72, 한 줄 14열      ← 지금까지와 픽셀 단위로 같다
    //      15개 이상 → 아이콘 48, 두 줄 20열 (40칸)
    //    빽빽할 때만 작아진다. 흔한 판(5~8종)에서는 큰 그림 그대로다.
    //
    //  ⚠ 왜 필요했나 — **14를 넘으면 조용히 잘렸다**
    //    RunPerkBarUI 는 칸이 모자라면 break 한다. 에러도 경고도 없이
    //    15번째부터 사라진다. 담을 수 있어야 하는 최대는
    //    개성 1 + 특성 30 + 제단 표식 8 = **39** 다.
    //
    //  ⚠ 56 → 72 (사용자 지적, 2026-09-07) — 56 은 전장 위에서 안 읽혔다.
    //    시너지 아이콘(48)보다 커야 한다: 시너지는 옆에 숫자가 붙어 있어
    //    그림이 작아도 짚이지만, 특성은 그림 하나가 전부다.
    //    ⚠ 빽빽할 때의 48 은 그 하한과 같은 값이다. 더 줄이지 말 것 —
    //      대신 열을 늘리려면 줄 폭(PerkRowW)을 넓혀야 하는데, 오른쪽
    //      버튼 띠(스테이지·배속·일시정지)가 그 자리를 쓴다.
    //
    //  ⚠ **자리는 언제나 두 줄만큼 비워 둔다** (PerkRowsH)
    //    프리팹은 미리 굽고 칸 수는 런타임에 정해진다. 한 줄일 때만 자리를
    //    적게 잡아 두면, 두 줄이 되는 순간 아래 마왕성 체력 막대를 덮는다.
    //    비워 둔 자리는 한 줄일 때 그냥 빈다 — 전장이 비쳐 티가 안 난다.
    const int   PerkSlotMax  = 40;

    /// <summary>넉넉할 때(≤14) 한 줄에 서는 수. 이 수를 넘으면 빽빽한 모습으로 바뀐다.</summary>
    const int   PerkColumns  = 14;
    const float PerkIconSize = 72f;

    /// <summary>빽빽할 때(≥15) — 두 줄 × 20열 = 40칸.</summary>
    const int   PerkDenseColumns = 20;
    const float PerkDenseIcon    = 48f;

    /// <summary>특성 아이콘 사이. 아이콘이 커서 눈금(10)보다 좁게 붙인다.</summary>
    const float PerkIconGap  = 6f;

    /// <summary>줄 폭 — 넉넉한 모습이 정한다. 빽빽한 모습은 이 안에 들어와야 한다(Verify).</summary>
    const float PerkRowW = PerkIconSize * PerkColumns + PerkIconGap * (PerkColumns - 1);

    /// <summary>
    /// 특성 줄이 차지할 수 있는 <b>최대</b> 세로 — 빽빽할 때의 두 줄.
    /// ⚠ 한 줄일 때도 이만큼 비워 둔다 (위 주석 참고).
    /// </summary>
    const float PerkRowsH = PerkDenseIcon * 2f + PerkIconGap;

    /// <summary>보유 특성 줄의 위 여백.</summary>
    const float PerkRowTop = 10f;

    /// <summary>마왕성 체력 막대의 위 — 특성 줄이 쓸 수 있는 자리 끝에서 한 눈금.</summary>
    const float CoreBarTop = PerkRowTop + PerkRowsH + Gutter;
    // ══════════════════════════════════════════════════════
    //  화면 격자 (사용자 지적으로 다시 짬, 2026-09-07)
    //
    //  ■ 눈금 하나로 맞춘다 — 자리를 눈대중으로 적지 않는다
    //    전에는 줄마다 x·y 를 손으로 적어 서로 2~6px 씩 어긋나 있었다.
    //    왼쪽 여백과 줄 사이를 상수 둘로 묶고, 뒤 줄은 **앞 줄의 끝에서** 잰다.
    //
    //      x = HudMargin(16)   ← 시너지 줄 · 특성 줄 · 체력 막대가 **같은 왼쪽 선**
    //      y  10 ..  82  보유 특성 줄  (아이콘 72)
    //      y  92 .. 138  마왕성 체력   (46)
    //      y 148 .. 201  보스 HP (중앙)
    //      y 148 .. 748  시너지 세로 줄 (칸 176×68, 왼쪽 · 최대 8칸)
    //
    //  ■ 시너지 줄과 보스 HP 는 같은 y 에서 시작하지만 안 부딪힌다
    //    보스 바는 가운데 정렬(760 폭 → x 580..1340)이고 시너지는 x 16..192 다.
    //
    //  ⚠ 가로 검산 (성벽 -13 기준)
    //    특성 14칸  16 + (72×14 + 6×13) = 1102  < 오른쪽 버튼 시작 1600 ✔
    //    시너지 줄  16 + 152 = 168, 라인 대기열 시작 198 → 30px 여유 ✔
    //    ⚠ 대기열은 성벽에서 왼쪽으로 177 자란다. 성벽을 왼쪽으로 되돌리면
    //      (WallX 를 줄이면) 이 여유가 먼저 사라진다 — InGameSceneSetup.WallX 주석 참고.

    /// <summary>화면 가장자리 여백. 왼쪽 줄기가 전부 이 선에서 시작한다.</summary>
    const float HudMargin = 16f;

    /// <summary>줄 사이 간격의 기본 눈금.</summary>
    const float Gutter = 10f;

    /// <summary>
    /// 오른쪽 버튼 띠의 높이.
    ///
    /// ⚠ UIScale.BtnSm(100) 보다 커야 한다 — 버튼이 띠 밖으로 삐져나온다.
    ///   148 → 112 로 줄였다(왼쪽 줄기를 밖으로 뺐으므로). 위아래 6px 여백.
    /// </summary>
    const float TopBarHeight = 112f;
    // 특성 아이콘 — 배경을 걷어낸 만큼 키운다. 상단에서 바로 읽혀야 한다.
    // 하단바 — 전장을 가리는 비율을 줄인다.
    const float BarHeight = 172f;

    // ── 마나 칸 ──
    //   ⚠ 카드 줄 시작점이 여기서 나온다 (ManaPanelLeft + ManaPanelW + ManaGap).
    //     따로 적으면 패널을 넓힌 날 첫 카드가 그 위로 올라탄다. 실제로 그랬다.
    const float ManaPanelLeft = 14f;
    const float ManaPanelW    = 268f;
    // 잔량(FontLg 한 줄) + 다음 판 예고(FontSm×0.9 한 줄) + 위아래 여백.
    // ⚠ 예고 줄을 지우면 여기도 줄일 것 — 안 그러면 칸 아래가 텅 빈다.
    const float ManaPanelH    = 116f;
    const float ManaIconSz    = 52f;

    /// <summary>마나 칸과 첫 카드 사이. 붙어 있으면 한 덩어리로 보인다 (사용자 지적).</summary>
    const float ManaGap = 36f;
    // 카드 — 화면을 덜 가리도록 낮춘다. 초상화는 그대로 크게 쓴다.
    const float CardWidth  = 150f;
    const float CardHeight = 150f;

    [MenuItem(ProjectKMenu.UI + "인게임 HUD", priority = ProjectKMenu.UIPrio)]
    public static void CreateAll()
    {
        // 경로·임포트 설정의 정본은 UIIconAssets 다 (카드 3택 팝업도 같은 아이콘을 쓴다).
        if (!UIIconAssets.TryLoad(Tag, out Sprite manaIcon, out Sprite countIcon)) return;

        // 시너지 아이콘 여덟 장 — 없으면 굽기를 멈춘다. 빈 칩을 만들어 두면
        // 프리팹만 보고는 무엇이 빠졌는지 알 수 없다.
        if (!SynergyIconAssets.TryLoad(Tag, out Sprite[] synergyIcons)) return;

        // 런 골드 칸(2026-09-13) — 시설 화면의 지갑과 **같은 그림**을 쓴다.
        if (!UIIconAssets.TryLoadGold(Tag, out Sprite goldIcon)) return;

        GameObject cardPrefab = CreateCardPrefab(manaIcon, countIcon, synergyIcons);
        if (cardPrefab == null) return;

        InGameHUD sceneHud = UnityEngine.Object.FindObjectsByType<InGameHUD>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene.IsValid());

        if (sceneHud != null)
        {
            PatchHud(sceneHud.gameObject, cardPrefab, manaIcon, countIcon, synergyIcons, goldIcon);
            SaveSceneHud(sceneHud.gameObject);
            FixCanvas(sceneHud.GetComponentInParent<Canvas>());
            EditorSceneManager.MarkSceneDirty(sceneHud.gameObject.scene);
        }
        else
        {
            PatchHudPrefab(cardPrefab, manaIcon, countIcon, synergyIcons, goldIcon);
            Debug.LogWarning($"[{Tag}] 열린 씬에 InGameHUD가 없어 프리팹만 갱신했습니다. " +
                             "InGame 씬을 연 뒤 메뉴를 다시 실행하면 씬에도 적용됩니다.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[{Tag}] 완료 — {HudPath}, {CardPath}");
    }

    // CI/자동 굽기용. 사용자가 보고 있던 씬은 유지하고 InGame만 잠시 연다.
    public static void CreateAndInstallInGameScene()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForBuild = !scene.IsValid() || !scene.isLoaded;
        if (openedForBuild)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(scene);
        CreateAll();
        EditorSceneManager.SaveScene(scene);

        if (previous.IsValid() && previous.isLoaded)
            SceneManager.SetActiveScene(previous);
        if (openedForBuild)
            EditorSceneManager.CloseScene(scene, true);
    }

    /// <summary>
    /// 소환 카드 한 장.
    ///
    /// ■ 구성 — 초상화가 전부다
    ///     ┌──────────────┐
    ///     │(마나)          │   마나 = 왼쪽 위, 아이콘 크게 + 가운데 숫자
    ///     │   초상화  ×N   │   카운트 = 초상화 오른쪽
    ///     └──────────────┘
    ///
    ///   이름 텍스트는 넣지 않는다 — 카드가 작아 이름을 넣으면 초상화가 뭉개지고,
    ///   몬스터는 초상화로 구분한다.
    ///
    ///   쿨다운 표시도 없다. 제약은 마나뿐이다.
    /// </summary>
    /// <summary>
    /// 카드 레벨 배지. 오른쪽 아래에 "Lv.3" 한 줄.
    ///
    /// ⚠ 배경 판을 깐다 — 초상화 위에 글자만 얹으면 안 읽힌다
    ///   몬스터 그림이 밝은 종족(슬라임)에서는 흰 글자가 통째로 묻힌다.
    /// </summary>
    /// <summary>
    /// 친화 종족 표식 — 왼쪽 아래 [친화]. 레벨 배지와 같은 치수의 거울상이다.
    ///
    /// ⚠ 보라 판 위 흰 글자 — 초상화 위에 글자만 얹으면 밝은 종족에서 묻힌다 (레벨 배지와 같은 이유).
    /// </summary>
    static GameObject CreateAffinityBadge(GameObject parent)
    {
        var root = EditorUIBuilder.Go("AffinityBadge", parent);
        var rt   = root.GetComponent<RectTransform>();

        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(6f, 6f);
        rt.sizeDelta        = new Vector2(72f, UIScale.RowSm);

        var plate = EditorUIBuilder.Img(root, "Plate", new Color(0.42f, 0.22f, 0.66f, 0.92f));
        EditorUIBuilder.Stretch(plate.gameObject);
        plate.raycastTarget = false;

        var text = EditorUIBuilder.TMP(root, "Text", "친화", UIScale.FontSm, FontStyles.Bold);
        text.color         = Color.white;
        text.alignment     = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        EditorUIBuilder.Stretch(text.gameObject);

        root.SetActive(false);
        return root;
    }

    static GameObject CreateLevelBadge(GameObject parent, out TextMeshProUGUI levelText)
    {
        var root = EditorUIBuilder.Go("LevelBadge", parent);
        var rt   = root.GetComponent<RectTransform>();

        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-6f, 6f);
        rt.sizeDelta        = new Vector2(72f, UIScale.RowSm);

        var plate = EditorUIBuilder.Img(root, "Plate", new Color(0.06f, 0.05f, 0.12f, 0.82f));
        EditorUIBuilder.Stretch(plate.gameObject);
        plate.raycastTarget = false;

        levelText = EditorUIBuilder.TMP(root, "Text", "Lv.1", UIScale.FontSm, FontStyles.Bold);
        EditorUIBuilder.Stretch(levelText.gameObject);
        levelText.alignment        = TextAlignmentOptions.Center;
        levelText.color            = new Color(1f, 0.86f, 0.42f);   // 금색 — 강화된 카드
        levelText.raycastTarget    = false;
        levelText.textWrappingMode = TextWrappingModes.NoWrap;

        root.SetActive(false);
        return root;
    }

    static GameObject CreateCardPrefab(Sprite manaIcon, Sprite countIcon, Sprite[] synergyIcons)
    {
        var root = new GameObject("SummonCard", typeof(RectTransform));
        EditorUIBuilder.LE(root, CardWidth, CardHeight);

        var button = EditorUIBuilder.RaisedBtnOn(
            root, new Color(0.105f, 0.115f, 0.19f), out var body);
        var ui = root.AddComponent<SummonCardUI>();

        // ── 초상화 — 카드 가운데를 크게 차지한다 ──
        var monsterIcon = EditorUIBuilder.Img(body, "MonsterIcon", Color.white);
        monsterIcon.sprite         = countIcon;   // 기본값(해골). 종족 초상화로 덮인다.
        monsterIcon.preserveAspect = true;
        monsterIcon.raycastTarget  = false;
        // ⚠ 아래를 기준으로 세운다
        //   슬라임·늑대·멧돼지는 납작해서 세로 가운데에 두면 공중에 뜬 것처럼 보인다.
        //   발밑을 카드 아래에 맞추면 인간형·네발짐승이 같은 바닥에 선다.
        var iconRt = monsterIcon.rectTransform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0f);
        iconRt.pivot     = new Vector2(0.5f, 0f);
        iconRt.anchoredPosition = new Vector2(0f, 16f);
        iconRt.sizeDelta        = new Vector2(104f, 104f);

        // ── 마나(왼쪽 위) · 마릿수(오른쪽 위) — 같은 치수의 좌우 대칭 ──
        var costRoot  = CreateBadge(body, "ManaCost",    manaIcon,  rightSide: false,
                                    ManaNumberColor,  out var manaCostText);
        var countRoot = CreateBadge(body, "SummonCount", countIcon, rightSide: true,
                                    CountNumberColor, out var summonCountText);

        // ── 시너지 표식(왼쪽 변) ──
        Image[] synergySlots = CreateCardSynergyColumn(body);

        // ── 카드 레벨(오른쪽 아래) ──
        //   중복 획득으로 오른다. Lv1 일 때는 런타임이 통째로 끈다 —
        //   모든 카드에 "Lv.1" 이 붙어 있으면 정보가 아니라 잡음이다.
        GameObject levelRoot = CreateLevelBadge(body, out var levelText);

        // ── 친화(왼쪽 아래) — 레벨 배지의 거울상 ──
        //   시너지 세로 줄은 레벨 배지 위에서 끝나므로 왼쪽 아래 72×43 은 비어 있다.
        //   런타임이 친화 종족일 때만 켠다 (SummonCardUI.RefreshAffinity).
        GameObject affinityRoot = CreateAffinityBadge(body);

        // ── 선택 강조 ──
        //   ⚠ 초상화·배지보다 **뒤에** 만든다 = 그 위에 그려진다.
        //     앞에 만들면 초상화가 테두리를 덮어 선택이 안 보인다.
        GameObject selected = CreateSelectedAccent(body);

        var disabled = EditorUIBuilder.Img(body, "DisabledOverlay",
                                           new Color(0.02f, 0.025f, 0.055f, 0.62f));
        EditorUIBuilder.Stretch(disabled.gameObject);
        disabled.raycastTarget = false;

        var so = new SerializedObject(ui);
        EditorUIBuilder.SetObj(so, "_button",           button,              Tag);
        EditorUIBuilder.SetObj(so, "_monsterIcon",      monsterIcon,         Tag);
        EditorUIBuilder.SetObj(so, "_costRoot",         costRoot,            Tag);
        EditorUIBuilder.SetObj(so, "_manaCostText",     manaCostText,        Tag);
        EditorUIBuilder.SetObj(so, "_countRoot",        countRoot,           Tag);
        EditorUIBuilder.SetObj(so, "_summonCountText",  summonCountText,     Tag);
        EditorUIBuilder.SetObjArray(so, "_synergyIcons",   synergySlots,  Tag);
        EditorUIBuilder.SetObjArray(so, "_synergyIconSet", synergyIcons,  Tag);
        EditorUIBuilder.SetObj(so, "_levelRoot",        levelRoot,           Tag);
        EditorUIBuilder.SetObj(so, "_levelText",        levelText,           Tag);
        EditorUIBuilder.SetObj(so, "_affinityRoot",     affinityRoot,        Tag);
        EditorUIBuilder.SetObj(so, "_disabledOverlay",  disabled.gameObject, Tag);
        EditorUIBuilder.SetObj(so, "_selectedAccent",   selected,            Tag);
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CardPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    // ── 카드 안 시너지 줄 ────────────────────────────────────
    //
    //  ⚠ 세로다 — 가로로 눕히면 레벨 배지와 부딪힌다
    //    레벨 배지가 오른쪽 아래 72×43 을 쓴다(x 72~144, y 6~49).
    //    가로줄을 깔면 셋 중 마지막 칸이 그 위로 올라탄다.
    //    왼쪽 변은 마나 배지 아래(위에서 44)부터 레벨 배지 위까지 비어 있다.
    //
    //  ⚠ 초상화(104폭, 카드 가운데)와 4px 만 스친다
    //    아이콘 줄이 x 3~27, 초상화가 x 23~127 이다. 그 4px 은 몬스터 그림의
    //    투명한 왼쪽 여백이라 실제로 가려지는 것이 없다.
    //    초상화를 넓히거나 왼쪽으로 옮기면 이 자리를 다시 봐야 한다.

    const float CardSynergySize  = 24f;
    const float CardSynergyGap   =  2f;
    const float CardSynergyLeft  =  3f;
    const float CardSynergyTop   = 44f;   // 마나 배지 아래

    /// <summary>한 종족이 갖는 표식의 최대 수. 카드 3택과 같은 값이다.</summary>
    const int CardSynergySlots = 3;

    static Image[] CreateCardSynergyColumn(GameObject body)
    {
        var root = EditorUIBuilder.Go("SynergyColumn", body);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0f, 1f);
        rootRt.pivot     = new Vector2(0f, 1f);
        rootRt.anchoredPosition = new Vector2(CardSynergyLeft, -CardSynergyTop);
        rootRt.sizeDelta        = new Vector2(
            CardSynergySize,
            CardSynergySize * CardSynergySlots + CardSynergyGap * (CardSynergySlots - 1));

        var layout = root.AddComponent<VerticalLayoutGroup>();
        layout.spacing                = CardSynergyGap;
        layout.childAlignment         = TextAnchor.UpperLeft;
        layout.childControlWidth      = true;
        layout.childControlHeight     = true;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;

        var slots = new Image[CardSynergySlots];

        for (int i = 0; i < slots.Length; i++)
        {
            Image icon = EditorUIBuilder.Img(root, $"Synergy_{i + 1}", Color.white);
            icon.preserveAspect = true;

            // ⚠ 레이캐스트를 끈다 — 카드 자체가 눌려야 한다
            //   여기서 클릭을 먹으면 그 자리를 눌렀을 때 카드가 선택되지 않는다.
            //   설명이 필요하면 상단 시너지 줄에서 본다.
            icon.raycastTarget  = false;

            EditorUIBuilder.LE(icon.gameObject, CardSynergySize, CardSynergySize);
            icon.gameObject.SetActive(false);   // 표식 수만큼만 런타임이 켠다

            slots[i] = icon;
        }

        return slots;
    }

    /// <summary>
    /// 카드 위쪽 배지 한 개 — 마나(왼쪽)와 마릿수(오른쪽)가 <b>같은 함수</b>를 쓴다.
    ///
    /// ⚠ 본문은 EditorUIBuilder.IconValueBadge 가 갖는다 (공용 UI 빌더 규칙)
    ///   카드 3택 팝업(RunPopupCreator)도 초상화 머리 위에 같은 배지를 얹는다.
    ///   두 곳이 각자 그리면 아이콘 크기·숫자 자리가 조용히 갈린다.
    ///   여기서는 이 카드의 치수만 정한다.
    /// </summary>
    static GameObject CreateBadge(GameObject parent, string name, Sprite icon,
                                  bool rightSide, Color numberColor,
                                  out TextMeshProUGUI valueText)
        => EditorUIBuilder.IconValueBadge(parent, name, icon, rightSide,
                                          BadgeIconSize, BadgeFontSize, BadgeInset,
                                          numberColor, out valueText);

    // ── 선택 강조 ────────────────────────────────────────────
    //
    //  ⚠ 아래 7px 짜리 막대 하나로는 안 보인다 (2026-09-02)
    //    카드 여덟 장이 붙어 늘어선 좁은 바에서, 카드 밑변의 얇은 선은
    //    옆 카드의 그림자와 구분되지 않는다. 라인을 탭하기 직전에
    //    "지금 뭘 들고 있나" 를 확인할 수 없으면 엉뚱한 카드를 건다.
    //
    //    그래서 세 겹으로 말한다 — 카드 전체를 두르는 테두리, 옅은 금색
    //    물들임, 카드 위로 삐져나온 삼각 표식. 어느 하나가 옆 카드에 가려도
    //    나머지가 남는다.

    /// <summary>선택 강조 색 — 카드 면(남색)·과부하(붉은색)와 겹치지 않는 금색.</summary>
    static readonly Color SelectColor = new(1.00f, 0.86f, 0.32f);

    const float SelectEdge   = 6f;    // 테두리 두께
    const float SelectMarkSz = 34f;   // 카드 위 삼각 표식

    static GameObject CreateSelectedAccent(GameObject parent)
    {
        var root = EditorUIBuilder.Go("SelectedAccent", parent);
        EditorUIBuilder.Stretch(root);

        // ① 옅은 물들임 — 카드 전체가 "켜졌다" 는 느낌을 만든다
        var fill = EditorUIBuilder.Img(root, "Fill",
                                       new Color(SelectColor.r, SelectColor.g, SelectColor.b, 0.16f));
        EditorUIBuilder.Stretch(fill.gameObject);
        fill.raycastTarget = false;

        // ② 사방 테두리 — 옆 카드와 맞닿아도 경계가 산다
        Edge(root, "EdgeTop",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, SelectEdge));
        Edge(root, "EdgeBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, SelectEdge));
        Edge(root, "EdgeLeft",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(SelectEdge, 0f));
        Edge(root, "EdgeRight",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(SelectEdge, 0f));

        // ③ 카드 위로 삐져나온 삼각 표식 — 카드 바를 훑을 때 가장 먼저 잡힌다
        //   ⚠ '▲' 글리프는 기본 폰트에 없다 (UI 규칙 2). 도형으로 그린다.
        GameObject mark = EditorUIBuilder.TriangleUp(root, "Marker", SelectMarkSz, SelectColor);
        var mRt = mark.GetComponent<RectTransform>();
        mRt.anchorMin = mRt.anchorMax = new Vector2(0.5f, 1f);
        mRt.pivot     = new Vector2(0.5f, 0f);
        mRt.anchoredPosition = new Vector2(0f, 6f);

        root.SetActive(false);
        return root;
    }

    /// <summary>테두리 한 변. 두께가 0 인 축은 부모를 따라 늘어난다.</summary>
    static void Edge(GameObject parent, string name, Vector2 min, Vector2 max, Vector2 size)
    {
        var img = EditorUIBuilder.Img(parent, name, SelectColor);
        img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = size;
    }

    static void PatchHud(GameObject hud, GameObject cardPrefab, Sprite manaIcon, Sprite countIcon,
                         Sprite[] synergyIcons, Sprite goldIcon)
    {
        // ⚠ 옛 잔재를 먼저 치운다 — 안 그러면 저장 자체가 막힌다
        //   Unity 는 스크립트가 사라진 컴포넌트가 하나라도 붙어 있으면
        //   SaveAsPrefabAsset 을 거부한다("missing script" 에러).
        StripDeadObjects(hud);

        BuildTopBar(hud, synergyIcons);
        BuildLaneQueue(hud, countIcon);

        // ⚠ 대기열보다 **뒤에** 깔아야 한다 (아래에서 SetAsFirstSibling)
        //   순서를 바꾸면 라인 띠가 대기열 초상화를 덮는다.
        BuildLaneGuide(hud);
        BuildSynergyBar(hud, synergyIcons);
        // ⚠ 진화·융합 창은 **팝업으로 옮겼다** (사용자 지적, 2026-09-07)
        //   CardEvolvePopupCreator 가 따로 굽는다 — 프리팹 생성 > 팝업 > 진화·융합.
        //   옛 HUD 에 남아 있는 자식은 걷어낸다 (둘이 남으면 하나가 유령이 된다).
        DestroyChild(hud.transform, "CardEvolve");
        BuildCrossroad(hud);
        BuildCoreHpBar(hud);
        BuildRunGoldBar(hud, goldIcon);
        DestroyChild(hud.transform, "SummonDeckBar");
        DestroyChild(hud.transform, "GeneralPanelContainer");

        // ⚠ 배경을 깔지 않는다 — 전장이 그대로 보여야 한다.
        var bar = EditorUIBuilder.Go("SummonDeckBar", hud);
        SetBottom(bar.GetComponent<RectTransform>(), BarHeight);


        // ── 마나 ──
        //
        //  ■ 숫자 하나만 남긴다 (사용자 지적, 2026-09-07)
        //    한때 "소환 마나" 라벨 + "9.2 / 65" + 진행 막대 셋이 한 패널에 있었다.
        //    셋이 같은 값을 세 번 말한다 — 라벨은 아이콘이 이미 말하고
        //    (물방울 그림), 막대는 숫자가 이미 말한다. 그래서 둘 다 걷어냈다.
        //    ⚠ 막대를 다시 넣지 말 것 — 마나는 초 단위로 차오르는 자원이
        //      아니라 **스테이지 경계에서만 움직이는 값**이다. 실시간으로
        //      차오르는 게이지처럼 그리면 기다리면 채워지는 줄 안다.
        //
        //  ⚠ 카드 줄과 띄운다 (사용자 지적)
        //    패널 오른쪽 끝과 첫 카드 사이가 붙어 있어 한 덩어리로 보였다.
        //    ManaPanelW + ManaGap 이 카드 줄의 시작점이다 — 아래 CardRow 참고.
        var manaPanel = EditorUIBuilder.Panel(bar, "ManaPanel", new Color(0.075f, 0.08f, 0.15f, 0.55f));
        var manaPanelRt = manaPanel.GetComponent<RectTransform>();
        manaPanelRt.anchorMin = manaPanelRt.anchorMax = new Vector2(0f, 0.5f);
        manaPanelRt.pivot = new Vector2(0f, 0.5f);
        manaPanelRt.anchoredPosition = new Vector2(ManaPanelLeft, 0f);
        manaPanelRt.sizeDelta = new Vector2(ManaPanelW, ManaPanelH);

        var manaImg = EditorUIBuilder.Img(manaPanel, "ManaIcon", Color.white);
        manaImg.sprite         = manaIcon;
        manaImg.preserveAspect = true;
        manaImg.raycastTarget  = false;
        var manaImgRt = manaImg.rectTransform;
        manaImgRt.anchorMin = manaImgRt.anchorMax = new Vector2(0f, 0.5f);
        manaImgRt.pivot = new Vector2(0f, 0.5f);
        manaImgRt.anchoredPosition = new Vector2(14f, 0f);
        manaImgRt.sizeDelta = new Vector2(ManaIconSz, ManaIconSz);

        // ⚠ 잔량은 이 화면에서 가장 자주 읽는 숫자다 — 한 단계 크게 잡는다
        //   ⚠ 자동 축소를 끄지 말 것 — "100 / 100" 은 FontLg 로 칸을 넘친다.
        var manaText = EditorUIBuilder.TMP(manaPanel, "ManaText", "100 / 100",
                                           UIScale.FontLg, FontStyles.Bold);
        var manaTextRt = manaText.rectTransform;
        manaTextRt.anchorMin = new Vector2(0f, 0f); manaTextRt.anchorMax = new Vector2(1f, 1f);
        manaTextRt.offsetMin = new Vector2(14f + ManaIconSz + 8f, 0f);
        manaTextRt.offsetMax = new Vector2(-12f, 0f);
        manaText.alignment     = TextAlignmentOptions.Midline;
        manaText.raycastTarget = false;

        manaText.enableAutoSizing = true;
        manaText.fontSizeMax      = UIScale.FontLg;
        // ⚠ 바닥은 FontSm 이다 (사용자 지적, 2026-09-12 — "100 자리가 넘어가면 UI 밖으로 삐져나간다")
        //   FontMd 가 바닥이면 세 자리 그릇("100 / 100")이 칸(182px)을 넘고, TMP 는
        //   NoWrap 에서 넘치는 글을 **자르지 않고 그대로 밖에 그린다** — 마나 칸
        //   바깥으로 숫자가 삐져나온다. 한 단계 더 내려갈 여지를 주면 네 자리까지 든다.
        //   ⚠ FontSm 아래로 두지 말 것 (UI 규칙 4) — 그 밑은 읽히지 않는다.
        manaText.fontSizeMin      = UIScale.FontSm;
        manaText.textWrappingMode = TextWrappingModes.NoWrap;

        // ── 다음 판 회복 예고 ──
        //   ⚠ 잔량 아래 한 줄로 붙인다. 셈은 ManaRegenRule 이 한다.
        //     올리면 내역(지능 몫·아껴 둔 몫·개성·특성)이 툴팁으로 펴진다.
        var manaRegen = EditorUIBuilder.TMP(manaPanel, "RegenText", "다음 판 +0",
                                            UIScale.FontSm * 0.9f, FontStyles.Normal);
        {
            var rt = manaRegen.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(12f, 4f);
            rt.offsetMax = new Vector2(-12f, 4f + UIScale.Line(UIScale.FontSm * 0.9f));
        }
        manaRegen.alignment        = TextAlignmentOptions.Center;
        manaRegen.color            = new Color(0.62f, 0.78f, 1f);
        manaRegen.raycastTarget    = false;
        manaRegen.textWrappingMode = TextWrappingModes.NoWrap;

        // ⚠ 레이캐스트 면을 따로 깐다 — 칸의 배경(Panel)은 글자 뒤라 클릭을 못 받는다
        //   거의 투명한 면을 맨 위에 덮어 마우스만 받는다 (TraitIconSlotBuilder 와 같은 수법).
        var manaHit = EditorUIBuilder.Img(manaPanel, "Hover", new Color(0f, 0f, 0f, 0.001f));
        EditorUIBuilder.Stretch(manaHit.gameObject);
        manaHit.raycastTarget = true;
        var manaHover = manaHit.gameObject.AddComponent<ManaRegenHoverUI>();

        // ── 카드 줄 ──
        //
        //  ⚠ 스크롤·마스크를 두지 않는다
        //    덱은 슬롯 수 상한이 정해져 있어 한 줄에 다 들어간다. 스크롤이 필요 없다.
        //    그리고 RectMask2D 가 있으면 **카드 밖으로 내민 것이 잘린다** —
        //    마나 배지를 카드 꼭짓점에 걸치게 두는 디자인이 마스크와 공존할 수 없다.
        var content = EditorUIBuilder.Go("CardRow", bar);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 0.5f);
        contentRt.anchorMax = new Vector2(1f, 0.5f);
        contentRt.pivot     = new Vector2(0f, 0.5f);
        // ⚠ 마나 칸에서 계산한다 — 숫자를 손으로 적지 말 것 (사용자 지적, 2026-09-07)
        //   286 을 박아 뒀더니 마나 패널(14+292=306)과 20px 겹쳤고, 패널을
        //   줄일 때마다 여기를 따라 고쳐야 했다.
        contentRt.offsetMin = new Vector2(ManaPanelLeft + ManaPanelW + ManaGap,
                                          -CardHeight * 0.5f);

        // ⚠ 오른쪽 끝을 시그니처 스킬 칸만큼 비켜선다 (2026-09-04)
        //   그 칸은 카드와 같은 크기·같은 세로 중심으로 앉는다. 여기를 줄이지
        //   않으면 마지막 카드가 스킬 칸 위로 올라탄다.
        contentRt.offsetMax = new Vector2(-SkillSlotRight - CardWidth - 14f,
                                          CardHeight * 0.5f);

        var layout = content.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 0, 0);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var cards = new SummonCardUI[MaxDeckSlots];
        for (int i = 0; i < cards.Length; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, content.transform);
            go.name = $"Slot_{i + 1}";
            cards[i] = go.GetComponent<SummonCardUI>();
            cards[i].ShowEmpty(countIcon);
            go.SetActive(i < 6);
        }

        BuildSkillSlot(bar, countIcon);

        var deckUi = bar.AddComponent<SummonDeckUI>();

        // ⚠ 종족 목록을 인스펙터에 물리지 않는다
        //   런 중에 주운 카드가 그 배열에 없으면 카드 바에 빈 칸으로 떠서
        //   "받았는데 쓸 수 없는" 상태가 됐다. ID → SO 변환은 이제
        //   Resources/CardCatalog 한 곳만 한다 (CardCatalog.cs 참고).
        var so = new SerializedObject(deckUi);
        EditorUIBuilder.SetObj(so, "_manaText", manaText, Tag);
        EditorUIBuilder.SetObj(so, "_manaRegenText", manaRegen, Tag);
        EditorUIBuilder.SetObj(so, "_manaHover", manaHover, Tag);
        EditorUIBuilder.SetObjArray(so, "_cards", cards.Cast<UnityEngine.Object>().ToArray(), Tag);
        EditorUIBuilder.SetObj(so, "_fallbackMonsterIcon", countIcon, Tag);
        var previewSlots = so.FindProperty("_emptyPreviewSlots");
        if (previewSlots != null) previewSlots.intValue = 6;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (CardCatalog.Current == null)
            Debug.LogWarning($"[{Tag}] Resources/CardCatalog 이 없습니다. " +
                             "Tools > Project K > 데이터 생성 > 카드 목록 을 실행하세요.");

        // ⚠ 덮개는 계층의 **맨 뒤**여야 한다 (사용자 지적, 2026-09-07)
        //   갈림길·융합 창은 각자 SetAsLastSibling 을 부르지만, 그건 자기가
        //   만들어질 때 얘기다. 카드 바(SummonDeckBar)는 그 뒤에 만들어지므로
        //   결국 카드 바가 마지막이 되어 **덮개 위로 그려졌다** — 갈림길이 떠
        //   있는데 시그니처 스킬 칸과 카드가 그 위에 얹혀 있었다.
        //   같은 캔버스 안에서는 sortingOrder 가 없다. 순서가 곧 앞뒤다.
        BringOverlaysToFront(hud);

        // ⚠ 같은 이름이 쌓이지 않았는지 마지막에 확인한다 (사용자 지적, 2026-09-07)
        //   HUD 루트에 무언가를 새로 달면서 지우는 줄을 빠뜨리면 구울 때마다
        //   하나씩 쌓이는데, 화면에서는 그저 "겹쳐 보인다" 로만 나타나 원인을
        //   짚기 어렵다. 실제로 PerkBar 가 넷까지 쌓였다.
        VerifyNoDuplicateChildren(hud);
    }

    /// <summary>
    /// 보유 특성 줄 검산 — <b>조용히 잘리는 것</b>을 막는 유일한 장치다.
    ///
    /// ⚠ RunPerkBarUI 는 칸이 모자라면 break 한다. 에러도 경고도 없이 특성이
    ///   화면에서 사라진다 — 실제로 14칸이던 시절에 15번째부터 안 보였다.
    /// </summary>
    static void VerifyPerkBar()
    {
        // 담아야 하는 최대 — 개성 1 + 런 특성 전부 + 제단 표식(표식 종류 수).
        int needed = 1
                   + System.Enum.GetValues(typeof(RunPerk)).Length - 1   // None 제외
                   + MonsterSynergyRule.AllTags.Length;

        if (PerkSlotMax < needed)
            Debug.LogError($"[{Tag}] 특성 칸이 모자랍니다 — {PerkSlotMax} < {needed}. " +
                           "넘치는 특성은 화면에서 조용히 사라집니다 (RunPerkBarUI 가 break). " +
                           "PerkDenseColumns 를 늘리고 PerkSlotMax 를 맞추세요.");

        if (PerkSlotMax > PerkDenseColumns * 2)
            Debug.LogError($"[{Tag}] 특성 칸 {PerkSlotMax}개는 두 줄({PerkDenseColumns * 2})을 " +
                           "넘습니다 — 세 줄째는 마왕성 체력 막대를 덮습니다.");

        float denseW = PerkDenseIcon * PerkDenseColumns
                     + PerkIconGap * (PerkDenseColumns - 1);

        if (denseW > PerkRowW)
            Debug.LogError($"[{Tag}] 빽빽한 모습의 줄 폭이 넘칩니다 ({denseW:0} > {PerkRowW:0}) — " +
                           "PerkDenseIcon 을 줄이거나 열 수를 줄이세요.");

        if (PerkDenseIcon > PerkIconSize)
            Debug.LogError($"[{Tag}] 빽빽한 모습이 넉넉한 모습보다 큽니다 " +
                           $"({PerkDenseIcon} > {PerkIconSize}) — 두 모습이 뒤집혔습니다.");
    }

    /// <summary>HUD 루트에 같은 이름의 자식이 둘 이상이면 이름을 대고 알린다.</summary>
    static void VerifyNoDuplicateChildren(GameObject hud)
    {
        var seen = new System.Collections.Generic.Dictionary<string, int>();

        foreach (Transform child in hud.transform)
        {
            seen.TryGetValue(child.name, out int n);
            seen[child.name] = n + 1;
        }

        foreach (var pair in seen)
        {
            if (pair.Value <= 1) continue;

            Debug.LogError($"[{Tag}] HUD 에 '{pair.Key}' 가 {pair.Value}개 있습니다 — " +
                           "만드는 함수에 DestroyChild 가 빠졌습니다. " +
                           "지금 프리팹에 쌓인 것은 다시 구우면 정리됩니다.");
        }
    }

    /// <summary>
    /// 전장을 덮는 판들을 계층 맨 뒤로 보낸다 — 그려지는 순서가 곧 앞뒤다.
    ///
    /// ⚠ 순서가 뜻을 갖는다
    ///   갈림길이 융합 창보다 뒤(= 더 앞에 그려짐)다. 융합은 갈림길에서
    ///   고른 뒤에 열리는 다음 단계라, 둘이 동시에 뜨는 일은 없지만
    ///   순서를 정해 두면 나중에 겹쳐도 뒤엉키지 않는다.
    /// </summary>
    static void BringOverlaysToFront(GameObject hud)
    {
        foreach (string name in new[] { "Crossroad" })   // 진화 창은 팝업이라 여기 없다
        {
            Transform t = hud.transform.Find(name);
            if (t == null)
            {
                Debug.LogWarning($"[{Tag}] '{name}' 를 찾지 못해 앞으로 못 보냈습니다 — " +
                                 "카드 바가 그 위로 그려집니다.");
                continue;
            }

            t.SetAsLastSibling();
        }
    }

    // ── 보스 광폭화 · 스킬 쿨다운 ────────────────────────────
    //
    //  ■ 왜 다시 넣었나 (사용자 지적, 2026-09-07)
    //    HUD 를 원작 UISetupTool 에서 이쪽으로 옮기며 **HP 바만 옮기고
    //    광폭화 표시와 스킬 4칸을 통째로 빠뜨렸다.** TopBarUI 의 로직
    //    (RefreshBossEnrage · RefreshBossSkills)은 멀쩡히 남아 있었지만,
    //    참조가 비어 있어 둘 다 첫 줄에서 return 하고 있었다 —
    //    화면에는 "보스 HP 바만 덩그러니" 로 보인다.
    //    ⚠ 새 Creator 로 화면을 옮길 때는 **컴포넌트의 SerializeField 를
    //      한 줄씩 훑어 빠진 것이 없는지 확인할 것.** 빠져도 예외가 나지 않는다.
    //
    //  ■ 칸 수는 4다 — 대표 스킬 1 + 패턴 3(돌진·분쇄 강타·광폭화)
    //    ⚠ 가장 높은 난이도 기준으로 잡아야 한다. 낮은 난이도는 2칸만 쓰지만,
    //      그 숫자에 맞춰 두면 다 갖춘 보스에서 마지막 칸이 조용히 잘려 나간다
    //      (TopBarUI 는 넘치면 그냥 버린다).

    const int   BossSkillSlots = 4;
    const float BossSkillSize  = 46f;
    const float BossSkillGap   = 8f;

    static void BuildBossExtras(GameObject hud, GameObject bossRoot,
                                out TextMeshProUGUI enrage,
                                out UnityEngine.Object[] icons,
                                out UnityEngine.Object[] cooldowns,
                                out UnityEngine.Object[] timers,
                                out UnityEngine.Object[] buttons,
                                out InfoTooltipUI tooltip)
    {
        // ── 광폭화 스택 — 바 오른쪽 끝 ──
        //   ⚠ 가운데 HP 숫자와 같은 칸을 쓰지 않는다
        //     BossHpText 는 폭 전체를 먹는다. 여기에 이어 붙이면 스택이 붙는
        //     순간 "1000 / 1000" 이 중앙에서 밀려, 체력이 줄 때마다 숫자가
        //     좌우로 흔들린다.
        enrage = EditorUIBuilder.TMP(bossRoot, "BossEnrageText", "광폭화 × 1",
                                     UIScale.FontSm, FontStyles.Bold);
        {
            var rt = enrage.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-12f, 0f);
            rt.sizeDelta        = new Vector2(190f, UIScale.RowSm);
        }
        enrage.alignment     = TextAlignmentOptions.MidlineRight;
        enrage.color         = new Color(1.00f, 0.62f, 0.20f);   // 달아오른 주황
        enrage.raycastTarget = false;
        enrage.gameObject.SetActive(false);

        // ── 스킬 칸 — HP 바 바로 아래 ──
        //   보스가 뭘 들고 있고 언제 터지는지 보이면 "갑자기 죽었다" 가 줄어든다.
        var iconArr  = new UnityEngine.Object[BossSkillSlots];
        var cdArr    = new UnityEngine.Object[BossSkillSlots];
        var timerArr = new UnityEngine.Object[BossSkillSlots];
        var btnArr   = new UnityEngine.Object[BossSkillSlots];

        float totalW = BossSkillSlots * BossSkillSize + (BossSkillSlots - 1) * BossSkillGap;

        for (int i = 0; i < BossSkillSlots; i++)
        {
            float x = -totalW * 0.5f + BossSkillSize * 0.5f + i * (BossSkillSize + BossSkillGap);

            var slot = EditorUIBuilder.Img(bossRoot, $"BossSkill{i}",
                                           new Color(0.10f, 0.04f, 0.05f, 0.92f));
            {
                var rt = slot.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot     = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(x, -6f);
                rt.sizeDelta        = new Vector2(BossSkillSize, BossSkillSize);
            }

            // ⚠ UI 규칙 1(음각 RaisedBtn)의 예외다
            //   그 규칙은 "평평한 사각형이 버튼인지 라벨인지 구분이 안 된다" 를
            //   막으려는 것이다. 여기는 스킬 아이콘 타일이라 이미 눌러 볼 것처럼
            //   생겼고, 특성 아이콘도 같은 방식으로 툴팁을 연다. 46px 칸에
            //   그림자·모서리를 넣으면 아이콘이 가려져 정보량이 오히려 준다.
            var btn = slot.gameObject.AddComponent<Button>();
            btn.targetGraphic = slot;
            btnArr[i] = btn;

            var icon = EditorUIBuilder.Img(slot.gameObject, "Icon", Color.white);
            EditorUIBuilder.Stretch(icon.gameObject);
            icon.preserveAspect = true;
            // 클릭은 칸(slot)이 받는다 — 자식이 가로채면 Button 이 안 눌린다
            icon.raycastTarget = false;
            iconArr[i] = icon;

            var cd = EditorUIBuilder.Img(slot.gameObject, "Cooldown", new Color(0f, 0f, 0f, 0.78f));
            EditorUIBuilder.Stretch(cd.gameObject);
            cd.raycastTarget = false;
            cd.type          = Image.Type.Filled;
            cd.fillMethod    = Image.FillMethod.Radial360;
            cd.fillClockwise = false;
            cd.fillAmount    = 0f;
            cdArr[i] = cd;

            // 남은 초 — 링 위에 겹쳐 올린다
            var timer = EditorUIBuilder.TMP(slot.gameObject, "Timer", "",
                                            UIScale.FontSm * 0.9f, FontStyles.Bold);
            EditorUIBuilder.Stretch(timer.gameObject);
            timer.alignment     = TextAlignmentOptions.Center;
            timer.raycastTarget = false;
            timerArr[i] = timer;

            slot.gameObject.SetActive(false);
        }

        icons     = iconArr;
        cooldowns = cdArr;
        timers    = timerArr;
        buttons   = btnArr;

        // ── 설명 툴팁 ──
        //   ⚠ 부모는 HUD 루트다 — BossHpRoot 아래에 두면 안 된다
        //     ShowAnchored 는 부모 사각형 안으로 툴팁을 밀어 넣는다. BossHpRoot 는
        //     높이가 60px 남짓이라 설명이 통째로 찌그러진다.
        tooltip = InfoTooltipBuilder.Build(hud, 460f);
    }

    // ── 마왕성 체력 — 성벽에 붙는 세로 기둥 ──────────────────
    //
    //  ■ 상단바가 아니라 **성벽**이다 (사용자 확정, 2026-09-07)
    //    이 게임의 유일한 패배 조건인데 화면 어디에도 없었다. 보스 HP 는
    //    상단바에 있지만 이건 성이 깎이는 것이므로 성에 붙어 있어야
    //    "저기가 뚫리고 있다" 로 읽힌다.
    //
    //  ⚠ **짧은 가로 막대**다 (사용자 지적, 2026-09-07 재조정)
    //    세로 기둥으로 두 번 시도했는데 둘 다 "체력처럼 안 생겼다".
    //    체력은 가로 막대가 관습이고, 세로 막대는 게이지·슬라이더로 읽힌다.
    //    자리는 시너지가 쓰던 왼쪽 위 — 특성 줄 바로 아래다. 시너지는 반대로
    //    왼쪽 가장자리 세로 줄로 내려갔다 (둘을 맞바꿨다).
    //
    //  ⚠ 짧게 둔다 — 길면 보스 HP 바와 헷갈린다
    //    보스 바는 화면 가운데에 760 으로 길다. 이건 왼쪽에 360 이다.
    //    자리·길이·색 셋이 다 달라야 한 화면에 있는 두 막대가 갈린다.

    /// <summary>막대 가로. 보스 HP(760)보다 확실히 짧아야 한다.</summary>
    const float CoreBarW = 360f;

    /// <summary>막대 세로.</summary>
    const float CoreBarH = 46f;

    /// <summary>뚫렸을 때 붉게 번쩍이는 성 쪽 띠의 폭.</summary>
    const float CoreFlashW = 420f;

    // ══════════════════════════════════════════════════════════
    //  런 골드 (사용자 지시, 2026-09-13)
    //
    //  ■ 자리 — 마왕성 체력 막대의 **오른쪽, 같은 줄**
    //    버는 곳이 전장인데 읽을 곳이 갈림길 안뿐이었다. 왼쪽 줄기의 같은
    //    격자에 얹으면 특성·체력·골드가 한 덩어리로 읽힌다.
    //
    //  ⚠ 보스 HP 바(가운데 760 → x 580 부터)를 넘지 않는다
    //    아래 Verify 가 굽는 순간 잰다. 넘치면 숫자 칸을 줄일 것.
    //
    //  ⚠ 글자보다 아이콘이 크다 (UI 규칙 7) — 재화는 그림이 정본 표기다.

    /// <summary>금화 그림 한 변.</summary>
    const float GoldIconSz = 44f;

    /// <summary>숫자 칸 폭. 후반 다섯 자리(12,340)까지 든다.</summary>
    const float GoldNumW = 130f;

    /// <summary>보스 HP 바가 시작하는 x — 골드 칸이 여기를 넘으면 겹친다.</summary>
    const float BossBarLeft = 580f;

    static readonly Color GoldNumColor = new(1f, 0.86f, 0.42f);

    /// <summary>
    /// 런 골드 배지. 값은 <see cref="RunGoldBarUI"/> 가 채운다 —
    /// 여기서는 자리와 그림만 정한다.
    /// </summary>
    static void BuildRunGoldBar(GameObject hud, Sprite goldIcon)
    {
        DestroyChild(hud.transform, "RunGold");

        var root = EditorUIBuilder.Go("RunGold", hud);
        EditorUIBuilder.Stretch(root);

        var ui = root.AddComponent<RunGoldBarUI>();

        // [금화][1,240] — 시설 화면의 지갑과 같은 배지 구조다 (표기가 화면마다 갈리지 않게).
        GameObject badge = FacilityStage.Badge(root, "GoldBadge", goldIcon,
                                               GoldIconSz, UIScale.FontSm, GoldNumW,
                                               GoldNumColor, out TextMeshProUGUI value);

        var rt = badge.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);

        // ⚠ 체력 막대의 끝에서 잰다 — 좌표를 손으로 적지 않는다 (화면 격자 주석).
        rt.anchoredPosition = new Vector2(HudMargin + CoreBarW + Gutter,
                                          -(CoreBarTop + (CoreBarH - rt.sizeDelta.y) * 0.5f));

        var so = new SerializedObject(ui);
        EditorUIBuilder.SetObj(so, "_valueText",  value,  Tag);
        EditorUIBuilder.SetObj(so, "_badge",      rt,     Tag);
        EditorUIBuilder.SetObj(so, "_coinSprite", goldIcon, Tag);
        so.ApplyModifiedPropertiesWithoutUndo();

        // ⚠ 조용한 실패를 시끄럽게 — 겹쳐도 에러가 안 나고 두 숫자가 포개져 보일 뿐이다.
        float right = rt.anchoredPosition.x + rt.sizeDelta.x;
        if (right > BossBarLeft)
            Debug.LogError($"[{Tag}] 런 골드 칸이 보스 HP 바와 겹칩니다 " +
                           $"({right:0} > {BossBarLeft}) — GoldNumW 를 줄이세요.");
    }

    static void BuildCoreHpBar(GameObject hud)
    {
        DestroyChild(hud.transform, "CoreHp");

        var root = EditorUIBuilder.Go("CoreHp", hud);
        EditorUIBuilder.Stretch(root);

        // ⚠ 맨 뒤로 보내지 않는다 — 갈림길·융합 덮개보다 **앞**이어야 한다
        //   덮개가 떠 있는 동안은 성이 안 깎이므로 가려도 상관없다.
        //   반대로 카드 바보다는 뒤라야 카드가 기둥에 가리지 않는다.
        root.transform.SetAsFirstSibling();

        var ui = root.AddComponent<CoreHpBarUI>();

        // ── 뚫림 번쩍임 — 성 쪽 화면 가장자리 ──
        //   평소에는 완전히 투명하다. 알파만 런타임이 올린다.
        var flash = EditorUIBuilder.Img(root, "BreachFlash", new Color(0.95f, 0.12f, 0.16f, 0f));
        {
            var rt = flash.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(CoreFlashW, 0f);
        }
        flash.raycastTarget = false;

        // ── 막대 ──
        //   테두리를 자식으로 얹지 않는다 (UI 규칙 3) — 바깥 판이 곧 테두리다.
        var column = EditorUIBuilder.Img(root, "Bar", new Color(0.06f, 0.05f, 0.11f, 0.92f));
        {
            var rt = column.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(HudMargin, -CoreBarTop);
            rt.sizeDelta        = new Vector2(CoreBarW, CoreBarH);
        }
        column.raycastTarget = false;

        // 채움 — 왼쪽에서 오른쪽으로. 줄면 오른쪽부터 빈다.
        var fill = EditorUIBuilder.Img(column.gameObject, "Fill", new Color(0.62f, 0.34f, 0.98f));
        {
            var rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(3f, 3f);
            rt.offsetMax = new Vector2(-3f, -3f);
        }
        // ⚠ 스프라이트가 없다 — Filled 가 먹으려면 CoreHpBarUI 가 런타임에 FillSprite 를 꽂는다
        fill.type          = Image.Type.Filled;
        fill.fillMethod    = Image.FillMethod.Horizontal;
        fill.fillOrigin    = (int)Image.OriginHorizontal.Left;
        fill.raycastTarget = false;

        // 덧빛 — 위험할 때 맥동하고 뚫릴 때 번쩍인다.
        var glow = EditorUIBuilder.Img(column.gameObject, "Glow", new Color(1f, 0.32f, 0.30f, 0f));
        {
            var rt = glow.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-8f, -8f);
            rt.offsetMax = new Vector2(8f, 8f);
        }
        glow.raycastTarget = false;

        // 남은 수 — 막대 한가운데.
        //   ⚠ 흰색 고정이다 — 뒤가 두 가지(빈 칸의 검정 · 채움의 보라/붉은)인데
        //     둘 다 어두워서 흰 글자가 양쪽에서 읽힌다.
        var text = EditorUIBuilder.TMP(column.gameObject, "Value", "0",
                                       UIScale.FontMd, FontStyles.Bold);
        EditorUIBuilder.Stretch(text.gameObject);
        text.alignment        = TextAlignmentOptions.Center;
        text.color            = Color.white;
        text.raycastTarget    = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        // ⚠ 꺼진 채로 굽는다 — 런이 시작되기 전에는 성이 없다
        //   켜 두면 로딩·준비 화면에서 "0" 짜리 빈 기둥이 먼저 보인다.
        //   RunCoreData.Max 가 채워지면 CoreHpBarUI.Refresh 가 켠다.
        column.gameObject.SetActive(false);

        var so = new SerializedObject(ui);
        EditorUIBuilder.SetObj(so, "_column",      column.rectTransform, Tag);
        EditorUIBuilder.SetObj(so, "_fill",        fill,                 Tag);
        EditorUIBuilder.SetObj(so, "_text",        text,                 Tag);
        EditorUIBuilder.SetObj(so, "_glow",        glow,                 Tag);
        EditorUIBuilder.SetObj(so, "_breachFlash", flash,                Tag);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── 시그니처 스킬 칸 ─────────────────────────────────────
    //
    //  ■ 카드와 같은 격자에 앉는다 (사용자 확정, 2026-09-04)
    //    크기는 카드와 같고(150×150), 세로 중심도 카드 줄과 같다. 하는 일이
    //    카드와 같은 물건이라 그렇게 보여야 한다.
    //
    //  ■ 시작 버튼(말풍선)과 같은 세로선에 세운다
    //    말풍선은 오른쪽 끝에서 StartBtnRightInset(96) 떨어져 있고 지름이
    //    StartBtnSize(132) 다 → 그 중심은 오른쪽 끝에서 96 + 66 = 162.
    //    스킬 칸의 중심을 같은 162 에 두면 두 버튼이 한 줄로 선다.
    //        오른쪽 여백 = 162 − 150/2 = 87
    //
    //  ⚠ 카드 줄의 오른쪽 끝을 함께 줄여야 한다 (위 contentRt.offsetMax)
    //    안 줄이면 마지막 카드가 이 칸 위로 올라탄다.

    /// <summary>스킬 칸이 화면 오른쪽 끝에서 떨어진 거리.</summary>
    const float SkillSlotRight = StartBtnRightInset + StartBtnSize * 0.5f - CardWidth * 0.5f;

    // ── 칸 안쪽 세로 예산 (칸 높이 = CardHeight = 150) ──
    //   위에서부터  여백 10 + 그림 88 + 틈 4 + 횟수 띠 44 + 여백 4 = 150 ✔
    //   ⚠ 그림을 키우려면 그만큼 아래 둘에서 빼 올 것
    //     겹치면 숫자가 그림 위에 앉아, 글자색을 아이콘마다 다시 재야 한다
    //     (UI 규칙 8 — 대비는 외곽선이 아니라 바탕 색으로 만든다).
    const float SkillIconTop     = 10f;
    const float SkillIconSize    = 88f;
    const float SkillCountBottom = 4f;
    const float SkillCountH      = 44f;

    static void BuildSkillSlot(GameObject bar, Sprite fallbackIcon)
    {
        var slot = EditorUIBuilder.Go("SkillSlot", bar);
        var rt   = slot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot     = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-SkillSlotRight, 0f);
        rt.sizeDelta        = new Vector2(CardWidth, CardHeight);

        // ⚠ 뿌리와 내용을 나눈다
        //   SummonerSkillButtonUI 는 뿌리에 붙고, 껐다 켜는 것은 이 자식이다.
        //   뿌리를 끄면 그 컴포넌트의 Update 가 함께 멎어, 소환사가 선 뒤에도
        //   버튼이 영영 다시 켜지지 않는다.
        var content = EditorUIBuilder.Go("Content", slot);
        EditorUIBuilder.Stretch(content);

        // 누를 수 있는 것이므로 음각이다 (UI 규칙 1).
        // 카드 면(남색)과 다른 색으로 두어 "카드가 아니다" 를 색으로 말한다.
        Button button = EditorUIBuilder.RaisedBtnOn(content, new Color(0.16f, 0.13f, 0.26f),
                                                    out GameObject body);

        // ── 스킬 그림 — 이 칸의 본체다 ──
        //  ⚠ 글자를 두지 않는다 (사용자 확정, 2026-09-07)
        //    한때 "권속 소환"(이름)과 "판당"(제한 종류)이 함께 있었다. 150px
        //    짜리 칸에 글자가 둘이면 그림이 76px 로 쪼그라들어, 정작 무엇을
        //    누르는지가 제일 안 보였다. 시그니처는 소환사당 하나뿐이라
        //    이름을 읽어 고를 일이 없다 — 그림 하나로 충분하다.
        //  ⚠ 그림은 런타임이 넣는다 (SummonerSkillButtonUI.Refresh)
        //    소환사마다 시그니처가 달라서 구울 때는 무엇이 올지 알 수 없다.
        //    키의 정본은 ActiveSkillIdExtensions.IconKey → SpriteManager 다.
        var icon = EditorUIBuilder.Img(body, "SkillIcon", Color.white);
        icon.sprite         = null;
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        var iconRt = icon.rectTransform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.pivot     = new Vector2(0.5f, 1f);
        iconRt.anchoredPosition = new Vector2(0f, -SkillIconTop);
        iconRt.sizeDelta        = new Vector2(SkillIconSize, SkillIconSize);

        // ── 남은 횟수 — 칸 아래 띠 ──
        //   ⚠ 숫자는 남긴다. "아이콘만" 이라고 해서 이것까지 지우면 몇 번
        //     남았는지 알 길이 없어진다 — 잠긴 이유를 화면이 말하지 못한다.
        //   ⚠ 그림 **위에** 얹지 않는다 (UI 규칙 8)
        //     바탕이 그림이 되면 글자색을 아이콘마다 다시 재야 한다. 아래
        //     띠에 두면 늘 같은 카드 면(어두운 남색) 위라 밝은 색 하나로 끝난다.
        var count = EditorUIBuilder.TMP(body, "Count", "3", UIScale.FontMd, FontStyles.Bold);
        var countRt = count.rectTransform;
        countRt.anchorMin = new Vector2(0f, 0f); countRt.anchorMax = new Vector2(1f, 0f);
        countRt.pivot     = new Vector2(0.5f, 0f);
        countRt.offsetMin = new Vector2(6f, SkillCountBottom);
        countRt.offsetMax = new Vector2(-6f, SkillCountBottom + SkillCountH);
        count.alignment        = TextAlignmentOptions.Center;
        count.color            = new Color(1f, 0.86f, 0.42f);
        count.raycastTarget    = false;
        count.textWrappingMode = TextWrappingModes.NoWrap;

        var disabled = EditorUIBuilder.Img(body, "DisabledOverlay",
                                           new Color(0.02f, 0.025f, 0.055f, 0.62f));
        EditorUIBuilder.Stretch(disabled.gameObject);
        disabled.raycastTarget = false;

        // ── 겨냥 중 표시 ─────────────────────────────────────
        //   누르면 그 자리에서 나가는 게 아니라 "다음 탭을 기다리는" 상태가
        //   된다. 그 사이 화면이 그대로면 눌린 건지 알 수 없다.
        //   ⚠ 어둡게 덮지 않는다 — 잠김(DisabledOverlay)과 반대 뜻이다.
        //     밝은 테두리를 덧그려 "지금 이게 켜져 있다" 로 읽히게 한다.
        var armed = EditorUIBuilder.Img(body, "ArmedMark", new Color(1f, 0.86f, 0.35f, 0.30f));
        EditorUIBuilder.Stretch(armed.gameObject);
        armed.raycastTarget = false;
        armed.gameObject.SetActive(false);

        var ui = slot.AddComponent<SummonerSkillButtonUI>();
        var so = new SerializedObject(ui);
        EditorUIBuilder.SetObj(so, "_button",          button,              Tag);
        EditorUIBuilder.SetObj(so, "_icon",            icon,                Tag);
        EditorUIBuilder.SetObj(so, "_countText",       count,               Tag);
        EditorUIBuilder.SetObj(so, "_disabledOverlay", disabled.gameObject, Tag);
        EditorUIBuilder.SetObj(so, "_armedMark",       armed.gameObject,    Tag);
        EditorUIBuilder.SetObj(so, "_content",         content,             Tag);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildTopBar(GameObject hud, Sprite[] synergyIcons)
    {
        DestroyChild(hud.transform, "TopBar");
        DestroyChild(hud.transform, "BossHpRoot");
        DestroyChild(hud.transform, "Tooltip"); // 원작 보스 스킬 툴팁

        // ⚠ 보유 특성 줄은 **HUD 루트**의 자식이다 — TopBar 와 함께 안 지워진다
        //   (사용자 지적, 2026-09-07) 상단바 안에 있던 것을 밖으로 빼면서 이 줄을
        //   빠뜨렸다. 구울 때마다 하나씩 쌓여 PerkBar 가 넷이 됐다.
        //   ⚠ HUD 루트에 무언가를 새로 달면 **여기 지우는 줄도 같이 넣을 것.**
        //     DeadHudChildren 목록이 아니라 만드는 함수가 지우는 것이 규칙이다.
        DestroyChild(hud.transform, "PerkBar");
        DestroyChild(hud.transform, "TraitBar");   // 옛 이름 — 원작 TraitBarUI 시절

        // ⚠ 배경을 깔지 않는다 — 전장이 그대로 보여야 한다.
        //   Panel 대신 빈 오브젝트를 쓰고, 배치 기준으로만 쓴다.
        var bar = EditorUIBuilder.Go("TopBar", hud);
        SetTop(bar.GetComponent<RectTransform>(), TopBarHeight);

        // ── 전황 버튼 — 배속 바로 옆 ──
        //
        //  ⚠ "시작"(EnemyInfoButtonUI)과 다른 물건이다
        //    그쪽은 대기 중에만 눌리는 진행 버튼이고, 이건 **읽는 창**이라
        //    전투 중에도 눌린다. 그래서 상단바의 다른 상시 버튼과 한 줄에 둔다.
        Button infoBtn = EditorUIBuilder.RaisedBtn(bar, "BattleInfoBtn",
                                                   new Color(0.16f, 0.20f, 0.32f),
                                                   out GameObject infoBody);
        {
            var rt = infoBtn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-InfoRightInset, 0f);
            rt.sizeDelta        = new Vector2(UIScale.BtnSm, UIScale.BtnSm);
        }
        {
            // ⚠ 글리프(⚔ 같은 것)는 폰트에 없다 (UI 규칙 2). 글자로 적는다.
            var label = EditorUIBuilder.TMP(infoBody, "Label", "전황",
                                            UIScale.FontSm, FontStyles.Bold);
            EditorUIBuilder.Stretch(label.gameObject);
            label.alignment        = TextAlignmentOptions.Midline;
            label.raycastTarget    = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        var infoUi = infoBtn.gameObject.AddComponent<BattleInfoButtonUI>();
        {
            var so2 = new SerializedObject(infoUi);
            EditorUIBuilder.SetObj(so2, "_button", infoBtn, Tag);
            so2.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 스테이지 표시 — 오른쪽. 배경 없이 글자만 얹는다 ──
        var wavePanel = EditorUIBuilder.Go("StagePanel", bar);
        var wavePanelRt = wavePanel.GetComponent<RectTransform>();
        wavePanelRt.anchorMin = new Vector2(1f, 0f);
        wavePanelRt.anchorMax = new Vector2(1f, 1f);
        wavePanelRt.pivot = new Vector2(1f, 0.5f);
        wavePanelRt.anchoredPosition = new Vector2(-StageRightInset, 0f);
        wavePanelRt.sizeDelta = new Vector2(280f, -20f);

        var waveText = EditorUIBuilder.TMP(wavePanel, "WaveText", "스테이지 1",
                                           UIScale.FontMd, FontStyles.Bold);
        var waveTextRt = waveText.rectTransform;
        waveTextRt.anchorMin = Vector2.zero;
        waveTextRt.anchorMax = Vector2.one;
        waveTextRt.offsetMin = new Vector2(0f, 14f);
        waveTextRt.offsetMax = new Vector2(0f, -6f);
        waveText.alignment = TextAlignmentOptions.MidlineRight;
        waveText.raycastTarget = false;
        // 배경이 없으니 테두리로 읽히게 한다.
        waveText.outlineWidth = 0.22f;
        waveText.outlineColor = Color.black;

        var waveBg = EditorUIBuilder.Img(wavePanel, "WaveProgressBg", new Color(0.13f, 0.14f, 0.23f, 0.55f));
        var waveBgRt = waveBg.rectTransform;
        waveBgRt.anchorMin = new Vector2(0f, 0f);
        waveBgRt.anchorMax = new Vector2(1f, 0f);
        waveBgRt.pivot = new Vector2(0.5f, 0f);
        waveBgRt.anchoredPosition = new Vector2(0f, 8f);
        waveBgRt.sizeDelta = new Vector2(0f, 6f);
        waveBg.raycastTarget = false;

        var waveFill = EditorUIBuilder.Img(waveBg.gameObject, "WaveProgressFill",
                                           new Color(0.46f, 0.24f, 1f));
        EditorUIBuilder.Stretch(waveFill.gameObject);
        waveFill.type = Image.Type.Filled;
        waveFill.fillMethod = Image.FillMethod.Horizontal;
        waveFill.raycastTarget = false;

        var speedButton = EditorUIBuilder.RaisedBtn(bar, "SpeedButton",
                                                     new Color(0.18f, 0.30f, 0.46f), out var speedBody);
        SetTopButtonRight(speedButton.GetComponent<RectTransform>(), SpeedRightInset, 108f);
        var speedLabel = EditorUIBuilder.TMP(speedBody, "Label", "1×", UIScale.FontSm, FontStyles.Bold);
        EditorUIBuilder.Stretch(speedLabel.gameObject);
        speedLabel.raycastTarget = false;
        var speedFace = EditorUIBuilder.Img(speedBody, "SpeedAccent", new Color(0.18f, 0.30f, 0.46f));
        SetButtonAccent(speedFace.rectTransform);
        speedFace.raycastTarget = false;
        var speedTooltip = InfoTooltipBuilder.Build(bar, 420f);

        var pauseButton = EditorUIBuilder.RaisedBtn(bar, "PauseButton",
                                                     new Color(0.30f, 0.16f, 0.18f), out var pauseBody);
        SetTopButtonRight(pauseButton.GetComponent<RectTransform>(), PauseRightInset, UIScale.BtnSm);
        EditorUIBuilder.Bar(pauseBody, "BarL", 9f, 38f, 0f, new Vector2(-11f, 0f), Color.white);
        EditorUIBuilder.Bar(pauseBody, "BarR", 9f, 38f, 0f, new Vector2(11f, 0f), Color.white);

        // ── 보유 특성 — 왼쪽. 배경 없이 아이콘만 늘어놓는다 ──
        //
        //  ■ ⚠ 원작 TraitBarUI 를 걷어냈다 (사용자 지적, 2026-09-07)
        //    그 컴포넌트는 **원작의 특성 체계**(RunTraitData · JobSynergyEvaluator)를
        //    읽는다. 이 게임은 그 축을 쓰지 않아 줄이 언제나 비어 있었고,
        //    화면에는 "보유 특성을 볼 곳이 없다" 로 보였다.
        //    지금은 RunPerkBarUI 가 소환사 개성 + 주운 특성(RunPerkData)을 세운다.
        //
        //  ■ 두 모습을 한 격자로 낸다 (위 PerkSlotMax 주석이 정본)
        //    ≤14 → 72px 한 줄 · ≥15 → 48px 두 줄. 바꾸는 것은 런타임
        //    (RunPerkBarUI.ApplyLayout)이고, Creator 는 **네 수치와 자리**만 준다.
        //
        //    ⚠ 자리(높이)는 언제나 두 줄만큼이다 — 프리팹은 미리 굽는데 칸 수는
        //      런타임에 정해진다. 한 줄 높이로 구워 두면 두 줄이 되는 순간
        //      아래 마왕성 체력 막대를 덮는다.
        //    ⚠ 정렬은 UpperLeft 다. MiddleLeft 로 두면 한 줄일 때 두 줄 자리의
        //      가운데로 내려가 15px 어긋난다 — 지금까지의 자리와 달라진다.
        // ⚠ 상단바(bar)가 아니라 HUD 루트에 단다
        //   상단바는 이제 오른쪽 버튼만 담는 얇은 띠라 왼쪽 줄기가 들어갈 자리가 없다.
        var traitRoot = EditorUIBuilder.Go("PerkBar", hud);
        var traitRt = traitRoot.GetComponent<RectTransform>();
        traitRt.anchorMin = traitRt.anchorMax = new Vector2(0f, 1f);
        traitRt.pivot = new Vector2(0f, 1f);
        traitRt.anchoredPosition = new Vector2(HudMargin, -PerkRowTop);
        traitRt.sizeDelta = new Vector2(PerkRowW, PerkRowsH);

        var grid = traitRoot.AddComponent<GridLayoutGroup>();
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.cellSize = new Vector2(PerkIconSize, PerkIconSize);
        grid.spacing = new Vector2(PerkIconGap, PerkIconGap);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = PerkColumns;

        var perkBar = traitRoot.AddComponent<RunPerkBarUI>();

        var perkIcons = new TraitIconUI[PerkSlotMax];
        for (int i = 0; i < perkIcons.Length; i++)
            perkIcons[i] = TraitIconSlotBuilder.Build(traitRoot, i, PerkIconSize);

        var perkSo = new SerializedObject(perkBar);
        EditorUIBuilder.SetObjArray(perkSo, "_slots",
                                    perkIcons.Cast<UnityEngine.Object>().ToArray(), Tag);

        // 두 모습의 수치 — ⚠ 숫자는 Creator 가 정본이다. 런타임은 고르기만 한다.
        EditorUIBuilder.SetObj(perkSo, "_grid", grid, Tag);
        perkSo.FindProperty("_iconSize").floatValue      = PerkIconSize;
        perkSo.FindProperty("_columns").intValue         = PerkColumns;
        perkSo.FindProperty("_denseIconSize").floatValue = PerkDenseIcon;
        perkSo.FindProperty("_denseColumns").intValue    = PerkDenseColumns;

        // 제단이 얹은 표식도 이 줄에 선다 — 상단 시너지 줄과 **같은 그림**을 쓴다.
        // ⚠ MonsterSynergyRule.AllTags 순서 그대로 (런타임이 IndexOf 로 찾는다)
        EditorUIBuilder.SetObjArray(perkSo, "_synergyIcons", synergyIcons, Tag);
        perkSo.ApplyModifiedPropertiesWithoutUndo();

        VerifyPerkBar();

        var bossRoot = EditorUIBuilder.Panel(hud, "BossHpRoot", new Color(0.14f, 0.04f, 0.05f, 0.94f));
        var bossRt = bossRoot.GetComponent<RectTransform>();
        bossRt.anchorMin = bossRt.anchorMax = new Vector2(0.5f, 1f);
        bossRt.pivot = new Vector2(0.5f, 1f);
        // ⚠ 왼쪽 줄기(특성 + 마왕성 체력) 아래다 — 상단바 높이가 아니라 그 끝을 기준으로 잡는다.
        //   상단바만 보고 잡으면 왼쪽 줄기가 보스 바를 덮는다.
        //   가운데 정렬이라 가로로는 왼쪽 줄기(x 110~)와 부딪히지 않는다.
        bossRt.anchoredPosition = new Vector2(0f, -(CoreBarTop + CoreBarH + Gutter));
        bossRt.sizeDelta = new Vector2(760f, UIScale.RowMd);

        var bossFill = EditorUIBuilder.Img(bossRoot, "BossHpFill", new Color(0.90f, 0.20f, 0.18f));
        EditorUIBuilder.Stretch(bossFill.gameObject);
        bossFill.type = Image.Type.Filled;
        bossFill.fillMethod = Image.FillMethod.Horizontal;
        bossFill.raycastTarget = false;
        var bossText = EditorUIBuilder.TMP(bossRoot, "BossHpText", "보스  1000 / 1000",
                                           UIScale.FontSm, FontStyles.Bold);
        EditorUIBuilder.Stretch(bossText.gameObject);
        bossText.outlineWidth = 0.22f;
        bossText.outlineColor = Color.black;
        bossText.raycastTarget = false;

        BuildBossExtras(hud, bossRoot, out var bossEnrage, out var bossSkillIcons,
                        out var bossSkillCds, out var bossSkillTimers,
                        out var bossSkillBtns, out var bossSkillTip);

        bossRoot.SetActive(false);

        // ── 말풍선 버튼 — 화면 오른쪽 끝 한가운데 ──
        //   ⚠ 상단바(bar)가 아니라 HUD 루트(hud)에 단다
        //     상단바에 있으면 배속·일시정지와 한 줄에 섞여 "판을 시작하는 버튼"
        //     으로 읽히지 않았다. 용사가 들어오는 쪽 한복판에 세운다.
        BuildEnemyInfoButton(hud);

        var topBar = bar.AddComponent<TopBarUI>();
        var so = new SerializedObject(topBar);
        EditorUIBuilder.SetObj(so, "_waveText", waveText, Tag);
        EditorUIBuilder.SetObj(so, "_waveProgressFill", waveFill, Tag);
        EditorUIBuilder.SetObj(so, "_bossHpRoot", bossRoot, Tag);
        EditorUIBuilder.SetObj(so, "_bossHpFill", bossFill, Tag);
        EditorUIBuilder.SetObj(so, "_bossHpText", bossText, Tag);
        EditorUIBuilder.SetObj(so, "_bossEnrageText", bossEnrage, Tag);
        EditorUIBuilder.SetObj(so, "_bossSkillTooltip", bossSkillTip, Tag);
        EditorUIBuilder.SetObjArray(so, "_bossSkillIcons",     bossSkillIcons,  Tag);
        EditorUIBuilder.SetObjArray(so, "_bossSkillCooldowns", bossSkillCds,    Tag);
        EditorUIBuilder.SetObjArray(so, "_bossSkillTimers",    bossSkillTimers, Tag);
        EditorUIBuilder.SetObjArray(so, "_bossSkillButtons",   bossSkillBtns,   Tag);
        EditorUIBuilder.SetObj(so, "_speedButton", speedButton, Tag);
        EditorUIBuilder.SetObj(so, "_speedLabel", speedLabel, Tag);
        EditorUIBuilder.SetObj(so, "_speedFace", speedFace, Tag);
        EditorUIBuilder.SetObj(so, "_speedLockTooltip", speedTooltip, Tag);
        EditorUIBuilder.SetObj(so, "_pauseButton", pauseButton, Tag);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ⚠ 카드 3택은 여기서 만들지 않는다 (2026-08-27)
    //   HUD 패널이던 CardRewardUI 를 **팝업**(CardSelectPopup)으로 옮겼다.
    //   전투 통계를 그 위에 띄워야 하는데, 캔버스 두 장의 앞뒤를 손으로
    //   관리하는 것보다 PopupManager 의 쌓임을 쓰는 편이 맞다.
    //   프리팹은 Tools > Project K > 프리팹 생성 > 팝업 > ▶ 런 팝업 이 굽는다.

    // ── 옛 잔재 청소 ─────────────────────────────────────────
    //
    //  ■ 왜 필요한가 (2026-09-02)
    //    카드 3택을 팝업으로 옮기며 CardRewardUI 스크립트를 지웠는데,
    //    HUD 프리팹에는 그 스크립트를 달고 있던 "CardReward" 오브젝트가
    //    그대로 남아 있었다. Unity 는 **스크립트가 사라진 컴포넌트가 붙어
    //    있으면 프리팹 저장을 거부한다** — 그래서 HUD 를 다시 구우려 할
    //    때마다 "missing script" 로 막혔다.
    //
    //  ■ 이 프리팹에서는 지우는 것이 안전하다
    //    HUD 프리팹은 통째로 Creator 산출물이다(손으로 고치지 않는다).
    //    그러니 스크립트가 사라진 컴포넌트는 정의상 옛 설계의 잔재다.
    //    남겨 둘 이유가 없고, 남기면 저장이 영영 안 된다.
    //
    //  ⚠ 이름으로 지우는 목록과 훑어서 지우는 것을 **둘 다** 한다
    //    이름 목록은 "이건 확실히 죽은 것" 을 통째로 치우고,
    //    훑기는 원작에서 넘어오며 이름이 바뀐 것들까지 받아 낸다
    //    (EnemyRuntimeBridge → MonsterRuntimeBridge 같은 개명이 여럿 있었다).

    /// <summary>설계가 바뀌며 죽은 오브젝트 — 이름이 바뀔 일이 없는 것만 적는다.</summary>
    static readonly string[] DeadHudChildren =
    {
        "CardReward",   // 카드 3택 → CardSelectPopup 으로 이전 (2026-08-27)
    };

    static void StripDeadObjects(GameObject hud)
    {
        foreach (string name in DeadHudChildren)
            DestroyChild(hud.transform, name);

        int removed = 0;
        foreach (Transform t in hud.GetComponentsInChildren<Transform>(includeInactive: true))
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

        if (removed > 0)
            Debug.Log($"[{Tag}] 스크립트가 사라진 컴포넌트 {removed}개를 걷어냈습니다. " +
                      "옛 설계의 잔재이며, 남아 있으면 프리팹이 저장되지 않습니다.");
    }

    // ── 진화 / 융합 갈림길 ───────────────────────────────────
    //
    //  ■ 왜 이것만 HUD 에 남았나
    //    카드 3택은 팝업으로 옮겼지만(전투 통계를 위에 얹어야 해서), 이 창은
    //    **카드 바 바로 위**에서 뜨는 편이 읽힌다 — 융합 재료를 고르는 화면이라
    //    지금 손에 든 카드와 나란히 보여야 한다.
    //
    //  ■ 두 단계를 한 판 위에 겹쳐 둔다
    //    1단계(갈림길)와 2단계(재료 선택)를 각각의 루트로 만들어 켜고 끈다.
    //    창을 둘로 나누면 배경 판이 두 벌이 되고, 뒤로 가기가 화면 전환처럼
    //    보여 "같은 결정 안에서 되돌아가는 것" 으로 읽히지 않는다.

    const float EvolvePanelW  = 900f;
    // ⚠ 620 → 804 (2026-09-12) — 갈래가 셋이 되면서 다시 쟀다
    //   ① 1단계: 머리 168 + 버튼 3개(BtnLg 164 × 3 + 간격 24 × 2 = 540) + 바닥 24 = 732
    //   ② 2단계: 머리 168 + 재료 격자(262 × 2 + 12 = 536) + 뒤로 가기(8 + 132 = 140)
    //             + 바닥 24 = 868  ← 이쪽이 더 크다
    //   둘 중 큰 쪽을 쓴다. 620 이던 시절에는 2단계 격자와 뒤로 가기 버튼이
    //   184px 겹쳐 있었다 — 재료 아랫줄이 버튼에 깔렸다.
    //   ⚠ UIScale.PopupMaxH(1000)를 넘기지 말 것.
    const float EvolvePanelH  = 868f;
    const float MaterialCellW = 190f;

    // ⚠ 230 → 262 (2026-09-13) — 패시브 글이 두 줄이 됐다
    //   세로 예산: 여백 12 + 초상화 110 + 이름 43(y 96) + 패시브 두 줄 86(y 8)
    //   초상화 아랫변이 y 140 이라 이름 줄(96~139)과 1px 만 남기고 맞물린다.
    //   줄을 더 늘리려면 여기와 EvolvePanelH 를 함께 올릴 것.
    const float MaterialCellH = 262f;
    const int   MaterialMax   = 8;      // 카드 칸 최대치와 맞춘다

    // ── 융합 창의 패시브 칩 ──────────────────────────────────
    //   계보(최대 4) + 융합으로 배워 온 것까지 오므로 칸은 여섯이다.
    const int   EvolveChipSlots = 6;
    const float EvolveChipSize  = 44f;
    const float EvolveChipGap   = 8f;

    /// <summary>
    /// 종족 패시브 칩 하나 — 레이캐스트 면 + 그림 + 툴팁 훅.
    /// RunPopupCreator.BuildPassiveChip 과 같은 구조다 (칩 크기만 다르다).
    /// </summary>
    static GameObject BuildPassiveChip(GameObject parent, int index,
                                       out Image icon, out SpeciesPassiveChipUI hover)
    {
        var chip = EditorUIBuilder.Go($"Passive_{index + 1}", parent);
        EditorUIBuilder.LE(chip, EvolveChipSize, EvolveChipSize);

        // ⚠ 클릭을 받을 면이 필요하다 — 빈 오브젝트는 레이캐스트를 못 받는다
        var hit = EditorUIBuilder.Img(chip, "Hit", new Color(0f, 0f, 0f, 0.001f));
        EditorUIBuilder.Stretch(hit.gameObject);
        hit.raycastTarget = true;

        icon = EditorUIBuilder.Img(chip, "Icon", Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        EditorUIBuilder.Stretch(icon.gameObject);

        hover = chip.AddComponent<SpeciesPassiveChipUI>();

        chip.SetActive(false);
        return chip;
    }

    // ══════════════════════════════════════════════════════════
    //  갈림길 — 전장 오른쪽에 두 갈래
    //
    //  ⚠ 팝업이 아니다 (사용자 확정, 2026-09-06)
    //    가운데 목록 창으로 두면 "창을 하나 더 닫는 일" 로 읽힌다.
    //    용사가 걸어 들어오는 오른쪽에 길이 갈려 보여야 방향이 맞는다.
    //  ⚠ 전장을 가리지 않는다 — 얇은 막만 깔고 카드는 오른쪽 끝에 붙인다.
    // ══════════════════════════════════════════════════════════

    const float CrossW   = 460f;
    const float CrossH   = 300f;
    const float CrossPad = 40f;

    static void BuildCrossroad(GameObject hud)
    {
        DestroyChild(hud.transform, "Crossroad");

        var root = EditorUIBuilder.Go("Crossroad", hud);
        EditorUIBuilder.Stretch(root);
        root.transform.SetAsLastSibling();

        var ui = root.AddComponent<CrossroadUI>();

        // 고르기 전에는 못 닫는다 — 눌러도 아무 일이 없는 면이다.
        var blocker = EditorUIBuilder.Img(root, "Blocker", new Color(0.02f, 0.02f, 0.05f, 0.40f));
        EditorUIBuilder.Stretch(blocker.gameObject);
        blocker.raycastTarget = true;

        var content = EditorUIBuilder.Go("Content", root);
        EditorUIBuilder.Stretch(content);

        var title = EditorUIBuilder.TMP(content, "Title", "갈림길   —   길을 고르세요",
                                        UIScale.FontLg, FontStyles.Bold);
        {
            var rt = title.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-CrossPad, 0f);
            rt.sizeDelta        = new Vector2(CrossW, UIScale.RowLg);
        }
        title.alignment     = TextAlignmentOptions.MidlineRight;
        title.raycastTarget = false;

        // 위 · 아래 한 칸씩. 가운데 제목을 사이에 두고 갈라진다.
        var views = new CrossroadUI.NodeView[2];
        views[0] = BuildCrossNode(content, 0, upper: true);
        views[1] = BuildCrossNode(content, 1, upper: false);

        var so = new SerializedObject(ui);
        EditorUIBuilder.SetObj(so, "_content", content, Tag);
        EditorUIBuilder.SetObj(so, "_blocker", blocker.gameObject, Tag);

        SerializedProperty arr = so.FindProperty("_nodes");
        arr.arraySize = views.Length;
        for (int i = 0; i < views.Length; i++)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Root")    .objectReferenceValue = views[i].Root;
            e.FindPropertyRelative("Button")  .objectReferenceValue = views[i].Button;
            e.FindPropertyRelative("Art")     .objectReferenceValue = views[i].Art;
            e.FindPropertyRelative("Frame")   .objectReferenceValue = views[i].Frame;
            e.FindPropertyRelative("NameText").objectReferenceValue = views[i].NameText;
            e.FindPropertyRelative("DescText").objectReferenceValue = views[i].DescText;
        }

        // ⚠ RunNodeRule.AllKinds 순서 그대로 — 런타임이 그 인덱스로 그림을 찾는다
        //   시설 화면(FacilityPopup)과 **같은 그림**을 쓴다. 갈림길에서 본 장면이
        //   그대로 배경으로 펼쳐져야 "그 길로 들어왔다" 가 읽힌다.
        if (RunNodeArtAssets.TryLoad(Tag, out Sprite[] nodeArt))
            EditorUIBuilder.SetObjArray(so, "_nodeArt", nodeArt, Tag);

        so.ApplyModifiedPropertiesWithoutUndo();

        content.SetActive(false);
        blocker.gameObject.SetActive(false);
    }

    static CrossroadUI.NodeView BuildCrossNode(GameObject parent, int index, bool upper)
    {
        var slot = EditorUIBuilder.Go($"Node_{index + 1}", parent);
        {
            var rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, upper ? 1f : 0f);
            rt.pivot     = new Vector2(1f, upper ? 1f : 0f);
            rt.anchoredPosition = new Vector2(-CrossPad, upper ? -CrossPad : CrossPad);
            rt.sizeDelta        = new Vector2(CrossW, CrossH);
        }

        // ⚠ 누를 수 있는 것이므로 음각 버튼이다 (UI 규칙 1)
        Button button = EditorUIBuilder.RaisedBtnOn(
            slot, new Color(0.115f, 0.125f, 0.20f), out GameObject body);

        // 종류 색 띠 — 왼쪽 변. 런타임이 전투/시설로 색을 바꾼다.
        var frame = EditorUIBuilder.Img(body, "Frame", Color.white);
        frame.raycastTarget = false;
        {
            var rt = frame.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(10f, 0f);
            rt.sizeDelta        = new Vector2(10f, -20f);
        }

        var art = EditorUIBuilder.Img(body, "Art", Color.white);
        art.preserveAspect = true;
        art.raycastTarget  = false;
        {
            var rt = art.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(40f, 0f);
            rt.sizeDelta        = new Vector2(140f, 140f);
        }

        var name = EditorUIBuilder.TMP(body, "Name", "이름", UIScale.FontLg, FontStyles.Bold);
        {
            var rt = name.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(196f, -46f);
            rt.sizeDelta        = new Vector2(-216f, UIScale.RowLg);
        }
        name.alignment        = TextAlignmentOptions.MidlineLeft;
        name.raycastTarget    = false;
        name.textWrappingMode = TextWrappingModes.NoWrap;

        var desc = EditorUIBuilder.TMP(body, "Desc", "설명", UIScale.FontSm, FontStyles.Normal);
        {
            var rt = desc.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(196f, -46f - UIScale.RowLg);
            rt.sizeDelta        = new Vector2(-216f, UIScale.RowSm * 3f);
        }
        desc.alignment     = TextAlignmentOptions.TopLeft;
        desc.color         = new Color(0.72f, 0.76f, 0.9f);
        desc.raycastTarget = false;

        return new CrossroadUI.NodeView
        {
            Root     = slot,
            Button   = button,
            Art      = art,
            Frame    = frame,
            NameText = name,
            DescText = desc,
        };
    }

    /// <summary>
    /// 진화·융합 창의 내용물을 짓고 루트를 돌려준다. <b>부모에 매달지 않는다.</b>
    ///
    /// ■ ⚠ 이건 팝업이다 — HUD 자식이 아니다 (사용자 지적, 2026-09-07)
    ///   한때 HUD 프리팹 안에 들어 있었다. 팝업 체계가 멀쩡히 있는데 이것만
    ///   HUD 자식이라 셋이 따라왔다:
    ///     ① 겹침 순서를 손으로 맞춰야 했다 (BringOverlaysToFront)
    ///     ② HUD 를 굽다 여기서 멈추면 창이 프리팹에서 통째로 사라졌다
    ///     ③ 사라져도 런타임은 조용히 넘어가 "눌렀는데 아무 일도 없다" 가 됐다
    ///   지금은 CardEvolvePopupCreator 가 이 함수를 불러 팝업 프리팹으로 굽는다.
    ///
    /// ⚠ 그림 조립은 여기 그대로 둔다 — 옮기면 200줄이 두 벌이 될 위험이 있다.
    ///   이 파일은 실제로 한 번 444줄이 중복된 적이 있다.
    ///
    /// 못 지었으면 null 을 돌려준다 (종족 패시브 아이콘이 없을 때).
    /// </summary>
    public static GameObject BuildCardEvolveRoot(GameObject parent, Sprite fallbackIcon)
    {
        // 종족 패시브 아이콘 19장 — 어느 칩에 어느 그림이 갈지는 런타임이 정한다.
        // ⚠ 없으면 짓지 않는다. 빈 칩을 구워 두면 프리팹만 보고는
        //   무엇이 빠졌는지 알 수 없다 (SpeciesPassiveIconAssets 참고).
        if (!SpeciesPassiveIconAssets.TryLoad(Tag, out Sprite[] passiveIcons))
        {
            Debug.LogError(
                "[InGameUIPrefabCreator] 종족 패시브 아이콘을 읽지 못해 진화·융합 창을 "
                + "만들지 못했습니다.\n"
                + "먼저 아이콘·텍스처 > 종족 패시브 아이콘 을 실행하세요.");
            return null;
        }

        var root = EditorUIBuilder.Go("CardEvolvePopup", parent);
        EditorUIBuilder.Stretch(root);

        var ui = root.AddComponent<CardEvolveUI>();

        var dim = EditorUIBuilder.Img(root, "Dim", new Color(0.02f, 0.02f, 0.05f, 0.86f));
        EditorUIBuilder.Stretch(dim.gameObject);

        var panel = EditorUIBuilder.Panel(root, "Panel", new Color(0.09f, 0.095f, 0.16f, 0.98f));
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot     = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta        = new Vector2(EvolvePanelW, EvolvePanelH);

        // ── 대상 카드 (두 단계 내내 위에 남는다) ──
        var targetIcon = EditorUIBuilder.Img(panel, "TargetIcon", Color.white);
        targetIcon.sprite         = fallbackIcon;
        targetIcon.preserveAspect = true;
        targetIcon.raycastTarget  = false;
        var tiRt = targetIcon.rectTransform;
        tiRt.anchorMin = tiRt.anchorMax = new Vector2(0f, 1f);
        tiRt.pivot     = new Vector2(0f, 1f);
        tiRt.anchoredPosition = new Vector2(28f, -24f);
        tiRt.sizeDelta        = new Vector2(120f, 120f);

        var targetName = EditorUIBuilder.TMP(panel, "TargetName", "이름",
                                             UIScale.FontLg, FontStyles.Bold);
        var tnRt = targetName.rectTransform;
        tnRt.anchorMin = new Vector2(0f, 1f);
        tnRt.anchorMax = new Vector2(1f, 1f);
        tnRt.pivot     = new Vector2(0f, 1f);
        tnRt.anchoredPosition = new Vector2(164f, -30f);
        tnRt.sizeDelta        = new Vector2(-192f, UIScale.RowLg);
        targetName.alignment        = TextAlignmentOptions.MidlineLeft;
        targetName.raycastTarget    = false;
        targetName.textWrappingMode = TextWrappingModes.NoWrap;

        var targetDesc = EditorUIBuilder.TMP(panel, "TargetDesc", "", UIScale.FontSm, FontStyles.Normal);
        var tdRt = targetDesc.rectTransform;
        tdRt.anchorMin = new Vector2(0f, 1f);
        tdRt.anchorMax = new Vector2(1f, 1f);
        tdRt.pivot     = new Vector2(0f, 1f);
        tdRt.anchoredPosition = new Vector2(164f, -30f - UIScale.RowLg);
        tdRt.sizeDelta        = new Vector2(-192f, UIScale.RowSm * 2f);
        targetDesc.alignment     = TextAlignmentOptions.TopLeft;
        targetDesc.color         = new Color(0.72f, 0.76f, 0.9f);
        targetDesc.raycastTarget = false;

        // ── 대상이 지금 가진 종족 패시브 — 그림으로 ──
        //
        //   ⚠ TargetDesc 와 같은 자리에 겹쳐 둔다
        //     둘은 동시에 채워지지 않는다. 패시브가 하나라도 있으면 칩이
        //     그리고 글은 비고, 하나도 없으면 글이 "패시브 없음" 만 말한다
        //     (CardEvolveUI.FillTargetPassives 참고).
        var targetPassiveRow = EditorUIBuilder.Go("TargetPassiveRow", panel);
        var tpRt = targetPassiveRow.GetComponent<RectTransform>();
        tpRt.anchorMin = new Vector2(0f, 1f);
        tpRt.anchorMax = new Vector2(0f, 1f);
        tpRt.pivot     = new Vector2(0f, 1f);
        tpRt.anchoredPosition = new Vector2(164f, -30f - UIScale.RowLg);
        tpRt.sizeDelta        = new Vector2(EvolveChipSize * EvolveChipSlots
                                            + EvolveChipGap * (EvolveChipSlots - 1),
                                            EvolveChipSize);

        var tpLayout = targetPassiveRow.AddComponent<HorizontalLayoutGroup>();
        tpLayout.spacing                = EvolveChipGap;
        tpLayout.childAlignment         = TextAnchor.MiddleLeft;
        tpLayout.childControlWidth      = true;
        tpLayout.childControlHeight     = true;
        tpLayout.childForceExpandWidth  = false;
        tpLayout.childForceExpandHeight = false;

        var targetPassiveRoots  = new GameObject[EvolveChipSlots];
        var targetPassiveIcons  = new Image[EvolveChipSlots];
        var targetPassiveHovers = new SpeciesPassiveChipUI[EvolveChipSlots];

        for (int i = 0; i < EvolveChipSlots; i++)
        {
            targetPassiveRoots[i] = BuildPassiveChip(targetPassiveRow, i,
                                                     out targetPassiveIcons[i],
                                                     out targetPassiveHovers[i]);
        }

        // ── 1단계 : 갈림길 ──
        var choiceStep = EditorUIBuilder.Go("ChoiceStep", panel);
        var csRt = choiceStep.GetComponent<RectTransform>();
        csRt.anchorMin = new Vector2(0f, 0f);
        csRt.anchorMax = new Vector2(1f, 1f);
        csRt.offsetMin = new Vector2(24f, 24f);
        csRt.offsetMax = new Vector2(-24f, -168f);

        Button evolveBtn = BuildBranchButton(
            choiceStep, "EvolveBranch", "진화", new Color(0.30f, 0.20f, 0.46f),
            offsetY: 0f, out var evolveDesc);

        Button fuseBtn = BuildBranchButton(
            choiceStep, "FuseBranch", "융합", new Color(0.17f, 0.30f, 0.42f),
            offsetY: -(UIScale.BtnLg + 24f), out var fuseDesc);

        // ⚠ 제3의 갈래 '강화' (사용자 지시, 2026-09-12)
        //   진화가 막힌 카드는 남는 갈래가 융합뿐이라 **재료를 잃는 쪽이 강제**됐다.
        //   이 갈래는 잃는 것이 없는 대신 공·체만 조금 얹는다 — 값은
        //   CardEvolution.EmpowerBonus 가 정본이고 런타임이 글을 만든다.
        //   ⚠ 런타임이 켜진 갈래만 위에서부터 다시 세운다 (CardEvolveUI.StackBranches).
        //     여기 offsetY 셋이 그 자리의 정본이다.
        Button powerBtn = BuildBranchButton(
            choiceStep, "PowerBranch", "강화", new Color(0.34f, 0.26f, 0.14f),
            offsetY: -(UIScale.BtnLg + 24f) * 2f, out var powerDesc);

        // ── 2단계 : 재료 선택 ──
        var materialStep = EditorUIBuilder.Go("MaterialStep", panel);
        var msRt = materialStep.GetComponent<RectTransform>();
        msRt.anchorMin = new Vector2(0f, 0f);
        msRt.anchorMax = new Vector2(1f, 1f);
        msRt.offsetMin = new Vector2(24f, 24f);
        msRt.offsetMax = new Vector2(-24f, -168f);

        var grid = EditorUIBuilder.Go("Grid", materialStep);
        var gridRt = grid.GetComponent<RectTransform>();
        gridRt.anchorMin = new Vector2(0f, 1f);
        gridRt.anchorMax = new Vector2(1f, 1f);
        gridRt.pivot     = new Vector2(0.5f, 1f);
        gridRt.anchoredPosition = Vector2.zero;
        gridRt.sizeDelta        = new Vector2(0f, MaterialCellH * 2f + 12f);

        var layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize        = new Vector2(MaterialCellW, MaterialCellH);
        layout.spacing         = new Vector2(12f, 12f);
        layout.childAlignment  = TextAnchor.UpperCenter;
        layout.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 4;

        var materials = new List<CardEvolveUI.MaterialView>(MaterialMax);
        for (int i = 0; i < MaterialMax; i++)
            materials.Add(BuildMaterialView(grid, i, fallbackIcon));

        // 뒤로 가기 — 재료를 보고 나서 진화로 마음을 바꿀 수 있어야 한다.
        var backBtn = EditorUIBuilder.RaisedTextBtn(
            materialStep, "BackBtn", "뒤로", UIScale.FontMd, new Color(0.16f, 0.17f, 0.26f));
        var backRt = backBtn.GetComponent<RectTransform>();
        backRt.anchorMin = backRt.anchorMax = new Vector2(0.5f, 0f);
        backRt.pivot     = new Vector2(0.5f, 0f);
        backRt.anchoredPosition = new Vector2(0f, 8f);
        backRt.sizeDelta        = new Vector2(280f, UIScale.BtnMd);

        var so = new SerializedObject(ui);
        // ⚠ _root 는 없다 — 팝업으로 옮기면서 지웠다 (2026-09-07)
        //   여닫는 것은 이제 PopupBase 다. 여기에 줄을 남겨 두면
        //   "직렬화 필드 없음: _root" 에러가 구울 때마다 뜬다.
        EditorUIBuilder.SetObj(so, "_targetIcon",   targetIcon,    Tag);
        EditorUIBuilder.SetObj(so, "_targetName",   targetName,    Tag);
        EditorUIBuilder.SetObj(so, "_targetDesc",   targetDesc,    Tag);
        EditorUIBuilder.SetObj(so, "_choiceStep",   choiceStep,    Tag);
        EditorUIBuilder.SetObj(so, "_evolveButton", evolveBtn,     Tag);
        EditorUIBuilder.SetObj(so, "_evolveDesc",   evolveDesc,    Tag);
        EditorUIBuilder.SetObj(so, "_fuseButton",   fuseBtn,       Tag);
        EditorUIBuilder.SetObj(so, "_fuseDesc",     fuseDesc,      Tag);
        EditorUIBuilder.SetObj(so, "_powerButton",  powerBtn,      Tag);
        EditorUIBuilder.SetObj(so, "_powerDesc",    powerDesc,     Tag);
        EditorUIBuilder.SetObj(so, "_materialStep", materialStep,  Tag);
        EditorUIBuilder.SetObj(so, "_backButton",   backBtn,       Tag);
        EditorUIBuilder.SetObj(so, "_fallbackIcon", fallbackIcon,  Tag);

        // ⚠ SpeciesPassiveRule.All 순서 그대로 — 런타임이 그 인덱스로 그림을 찾는다
        EditorUIBuilder.SetObjArray(so, "_speciesIcons", passiveIcons, Tag);

        EditorUIBuilder.SetObjArray(so, "_targetPassiveRoots",  targetPassiveRoots,  Tag);
        EditorUIBuilder.SetObjArray(so, "_targetPassiveIcons",  targetPassiveIcons,  Tag);
        EditorUIBuilder.SetObjArray(so, "_targetPassiveHovers", targetPassiveHovers, Tag);

        SerializedProperty arr = so.FindProperty("_materials");
        arr.arraySize = materials.Count;
        for (int i = 0; i < materials.Count; i++)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Root")       .objectReferenceValue = materials[i].Root;
            e.FindPropertyRelative("Button")     .objectReferenceValue = materials[i].Button;
            e.FindPropertyRelative("Icon")       .objectReferenceValue = materials[i].Icon;
            e.FindPropertyRelative("NameText")   .objectReferenceValue = materials[i].NameText;
            e.FindPropertyRelative("PassiveText").objectReferenceValue = materials[i].PassiveText;
            e.FindPropertyRelative("PassiveIcon").objectReferenceValue = materials[i].PassiveIcon;
            e.FindPropertyRelative("PassiveHover").objectReferenceValue = materials[i].PassiveHover;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    /// <summary>갈림길 버튼 한 줄 — 큰 제목 + 오른쪽에 설명.</summary>
    static Button BuildBranchButton(GameObject parent, string name, string label,
                                    Color face, float offsetY,
                                    out TextMeshProUGUI desc)
    {
        var slot = EditorUIBuilder.Go(name, parent);
        var rt   = slot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, offsetY);
        rt.sizeDelta        = new Vector2(0f, UIScale.BtnLg);

        Button button = EditorUIBuilder.RaisedBtnOn(slot, face, out var body);

        var title = EditorUIBuilder.TMP(body, "Label", label, UIScale.FontLg, FontStyles.Bold);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(0f, 0.5f);
        titleRt.pivot     = new Vector2(0f, 0.5f);
        titleRt.anchoredPosition = new Vector2(28f, 0f);
        titleRt.sizeDelta        = new Vector2(180f, UIScale.RowLg);
        title.alignment        = TextAlignmentOptions.MidlineLeft;
        title.raycastTarget    = false;
        title.textWrappingMode = TextWrappingModes.NoWrap;

        desc = EditorUIBuilder.TMP(body, "Desc", "", UIScale.FontSm, FontStyles.Normal);
        var descRt = desc.rectTransform;
        descRt.anchorMin = new Vector2(0f, 0.5f);
        descRt.anchorMax = new Vector2(1f, 0.5f);
        descRt.pivot     = new Vector2(0f, 0.5f);
        descRt.anchoredPosition = new Vector2(220f, 0f);
        descRt.sizeDelta        = new Vector2(-248f, UIScale.RowSm * 2f);
        desc.alignment     = TextAlignmentOptions.MidlineLeft;
        desc.color         = new Color(0.78f, 0.82f, 0.95f);
        desc.raycastTarget = false;

        return button;
    }

    /// <summary>재료 후보 한 칸 — 초상화 + 이름 + 넘어갈 패시브.</summary>
    static CardEvolveUI.MaterialView BuildMaterialView(GameObject parent, int index,
                                                       Sprite fallbackIcon)
    {
        var slot = EditorUIBuilder.Go($"Material_{index + 1}", parent);

        Button button = EditorUIBuilder.RaisedBtnOn(
            slot, new Color(0.125f, 0.135f, 0.22f), out var body);

        var icon = EditorUIBuilder.Img(body, "Icon", Color.white);
        icon.sprite         = fallbackIcon;
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        var iconRt = icon.rectTransform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.pivot     = new Vector2(0.5f, 1f);
        iconRt.anchoredPosition = new Vector2(0f, -12f);
        iconRt.sizeDelta        = new Vector2(110f, 110f);

        var nameText = EditorUIBuilder.TMP(body, "Name", "이름", UIScale.FontSm, FontStyles.Bold);
        var nameRt = nameText.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 0f);
        nameRt.anchorMax = new Vector2(1f, 0f);
        nameRt.pivot     = new Vector2(0.5f, 0f);
        // ⚠ 패시브 글이 두 줄(86)이라 그 위로 올린다 — 8 + 86 + 2 = 96
        nameRt.anchoredPosition = new Vector2(0f, UIScale.RowSm * 2f + 10f);
        nameRt.sizeDelta        = new Vector2(-12f, UIScale.RowSm);
        nameText.alignment        = TextAlignmentOptions.Center;
        nameText.raycastTarget    = false;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;

        // ── 넘어갈 패시브 — [그림] 위, [이름 / 공·체] 아래 두 줄 ──
        //
        //  ⚠ 칩은 **왼쪽 위**다 (사용자 지적, 2026-09-13)
        //    한때 왼쪽 아래에 두고 글을 칸 전체 가운데 정렬로 깔았다. 글과 칩이
        //    같은 띠를 써서 이름이 조금만 길어도 **아이콘이 글에 덮였다.**
        //    글을 칩 옆으로 밀면 남는 폭이 124 뿐이라 두 줄로도 안 들어간다 —
        //    그래서 칩을 띠 밖(초상화 왼쪽 위)으로 옮기고 글에 칸 폭을 통째로 준다.
        var passiveChip = BuildPassiveChip(body, 0,
                                           out Image passiveIcon,
                                           out SpeciesPassiveChipUI passiveHover);
        passiveChip.SetActive(true);

        var pcRt = passiveChip.GetComponent<RectTransform>();
        pcRt.anchorMin = pcRt.anchorMax = new Vector2(0f, 1f);
        pcRt.pivot     = new Vector2(0f, 1f);
        pcRt.anchoredPosition = new Vector2(4f, -4f);
        pcRt.sizeDelta        = new Vector2(EvolveChipSize, EvolveChipSize);

        var passiveText = EditorUIBuilder.TMP(body, "Passive", "", UIScale.FontSm, FontStyles.Normal);
        var pRt = passiveText.rectTransform;
        pRt.anchorMin = new Vector2(0f, 0f);
        pRt.anchorMax = new Vector2(1f, 0f);
        pRt.pivot     = new Vector2(0.5f, 0f);
        pRt.anchoredPosition = new Vector2(0f, 8f);
        pRt.sizeDelta        = new Vector2(-12f, UIScale.RowSm * 2f);
        passiveText.alignment        = TextAlignmentOptions.Center;
        passiveText.color            = new Color(0.72f, 0.55f, 1f);
        passiveText.raycastTarget    = false;

        // ⚠ NoWrap 을 쓰지 않는다 — 넘치면 칸 밖으로 흘러 옆 재료를 덮는다.
        //   줄바꿈을 켜 두면 최악의 경우에도 칸 안에서 접힌다.
        passiveText.textWrappingMode = TextWrappingModes.Normal;

        return new CardEvolveUI.MaterialView
        {
            Root         = slot,
            Button       = button,
            Icon         = icon,
            NameText     = nameText,
            PassiveText  = passiveText,
            PassiveIcon  = passiveIcon,
            PassiveHover = passiveHover,
        };
    }

    /// <summary>상단바 오른쪽에서부터 버튼을 배치한다.</summary>
    /// <summary>
    /// 라인별 출전 대기 표시.
    ///
    /// 라인마다 격자 한 덩이를 만들고, 거기에 "초상화 ×N" 칸을 6개까지 넣는다.
    /// 덩이의 화면 위치는 런타임에 SummonQueueUI 가 잡는다 —
    /// 오른쪽 끝을 성벽 바깥면에 붙이고 세로만 라인을 따라간다.
    /// 여기서는 구조만 만들고 좌표는 잡지 않는다.
    /// </summary>
    static void BuildLaneQueue(GameObject hud, Sprite fallbackIcon)
    {
        DestroyChild(hud.transform, "LaneQueue");

        var root = EditorUIBuilder.Go("LaneQueue", hud);
        var rootRt = root.GetComponent<RectTransform>();
        EditorUIBuilder.Stretch(root);
        rootRt.SetAsFirstSibling();   // 카드 바·상단바보다 뒤에 깔린다

        var ui = root.AddComponent<SummonQueueUI>();
        var so = new SerializedObject(ui);

        SerializedProperty lanes = so.FindProperty("_lanes");
        lanes.arraySize = LaneCount;

        for (int lane = 0; lane < LaneCount; lane++)
        {
            var column = EditorUIBuilder.Go($"Lane_{lane + 1}", root);
            var colRt  = column.GetComponent<RectTransform>();

            // ⚠ 격자를 쓴다 — 가로 배치(HorizontalLayoutGroup)는 칸이 서로 겹쳤다
            //   칸 안에서 초상화를 앵커로 직접 잡다 보니 레이아웃이 폭을 제대로
            //   못 재고 포개졌다. 격자는 셀 크기를 고정하므로 겹칠 수가 없다.
            //
            //   덤으로 줄바꿈도 공짜로 얻는다 — QueueColumns(4) 칸을 넘으면
            //   자동으로 둘째 줄로 넘어간다.
            colRt.sizeDelta = new Vector2(
                QueueCellW * QueueColumns + QueueSpacing * (QueueColumns - 1),
                QueueCellH * QueueRows    + QueueSpacing * (QueueRows    - 1));

            var grid = column.AddComponent<GridLayoutGroup>();

            // ⚠ 오른쪽 위부터 채운다
            //   줄이 성벽에 붙어 왼쪽(성 안)으로 자라므로, 맨 앞 대기(지금 뽑는
            //   종족)가 성벽에 가장 가까운 칸에 있어야 "다음에 이게 나간다" 로
            //   읽힌다. 기본값(UpperLeft)이면 맨 앞이 성 안쪽 깊은 곳에 놓인다.
            grid.startCorner     = GridLayoutGroup.Corner.UpperRight;

            // ⚠ 세로 정렬은 **가운데**다 (2026-09-03) — 위쪽이 아니다
            //   칸틀은 3줄(126px)짜리로 고정인데 정렬이 UpperRight 였다.
            //   대기가 한 종류뿐이면(대부분 그렇다) 그 한 줄이 틀 맨 위에 붙어,
            //   초상화가 실제 라인보다 **한 줄 높이(약 43px) 위**에 떴다.
            //   라인 띠(LaneGuideUI)를 깔고 나서야 어긋난 게 드러났다 —
            //   띠는 라인 Y 를 정확히 따르고 있었고 대기열이 밀려 있었다.
            //   가운데 정렬이면 줄이 몇 개든 라인 Y 를 가운데에 두고 자란다.
            //   (좌우 정렬은 그대로 오른쪽 = 성벽 쪽이다)
            grid.childAlignment  = TextAnchor.MiddleRight;
            grid.cellSize        = new Vector2(QueueCellW, QueueCellH);
            grid.spacing         = new Vector2(QueueSpacing, QueueSpacing);
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = QueueColumns;

            SerializedProperty laneProp = lanes.GetArrayElementAtIndex(lane);
            laneProp.FindPropertyRelative("Root").objectReferenceValue = colRt;

            SerializedProperty slots = laneProp.FindPropertyRelative("Slots");
            slots.arraySize = QueueDisplayLimit;

            for (int i = 0; i < QueueDisplayLimit; i++)
            {
                GameObject slot = BuildQueueSlot(column, i, fallbackIcon,
                                                 out Image portrait, out Image progress,
                                                 out TextMeshProUGUI count);

                SerializedProperty sp = slots.GetArrayElementAtIndex(i);
                sp.FindPropertyRelative("Root").objectReferenceValue      = slot;
                sp.FindPropertyRelative("Portrait").objectReferenceValue  = portrait;
                sp.FindPropertyRelative("Progress").objectReferenceValue  = progress;
                sp.FindPropertyRelative("CountText").objectReferenceValue = count;
            }

            column.SetActive(false);
        }

        EditorUIBuilder.SetObj(so, "_fallbackIcon", fallbackIcon, Tag);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── 시너지 줄 ────────────────────────────────────────────
    //
    //  ⚠ 자리는 상단바 **아래**다 — 상단바 안이 아니다
    //    상단바에는 이미 특성 아이콘 격자(TraitBar)가 두 줄로 깔려 있어
    //    같은 줄에 넣으면 겹친다. 바로 아래 띠는 비어 있고, 라인 대기열은
    //    성벽 중간 높이라 세로로 부딪히지 않는다.
    //
    //  ⚠ 왼쪽에서 오른쪽으로 자란다
    //    켜진 개수가 0~8 로 변한다. 가운데 정렬이면 시너지가 하나 켜질 때마다
    //    이미 떠 있던 칸이 옆으로 밀려 눈이 다시 읽어야 한다.

    //   칸 = [아이콘 56][2/4/6]. 이름을 글자로 적지 않으므로 폭이 짧다 —
    //   여덟 개가 다 켜져도 (152+8)×8 = 1280px 로 화면 안에 들어간다.
    //
    //   ⚠ 숫자 칸이 "2/4/6" 을 담아야 한다 (2026-09-03)
    //     한 자리만 띄우던 때는 96 으로 충분했다. 문턱 셋을 다 띄우면
    //     FontSm 기준 약 76px 이 필요하다 — 152 − 56(아이콘) − 18(여백) = 78.
    //
    //   ⚠ 40 → 56 (2026-09-03) — 전장 위에서 안 읽힌다는 지적
    //     시너지 이름을 그림으로 바꿨으니 그림이 안 읽히면 무엇이 켜졌는지
    //     알 방법이 없어진다. 배지 아이콘(하단 카드 36)보다 커야 하는 자리다.
    //   ■ 칸 = [아이콘 48][보유][2/4/6] — 셋으로 읽는다 (사용자 확정, 2026-09-03)
    //     6(여백) + 48(아이콘) + 6 + 36(보유) + 6 + 84(문턱) + 6 = 192
    //     여덟 개가 다 켜져도 (192+8)×8 − 8 = 1592px 로 화면 안에 들어간다.
    //
    //     ⚠ 보유 카운터를 따로 둔 이유 — 문턱 줄만 띄우면 굵게 칠해진 숫자가
    //       곧 보유 수인 줄 알기 어렵다. 3장이어도 굵은 것은 2 라 2장으로 읽힌다.
    // ■ 칸 = [아이콘 48][보유][2/4/6] — 셋으로 읽는다 (사용자 확정, 2026-09-03)
    //     6(여백) + 48(아이콘) + 6 + 36(보유) + 6 + 84(문턱) + 6 = 192
    //
    //   ⚠ **칸 안은 건드리지 않는다** (사용자 지적, 2026-09-07)
    //     줄을 세로로 세우면서 칸 내용까지 두 층으로 다시 짰다가 되돌렸다.
    //     옮기라는 것은 **줄의 자리**지 칸의 짜임이 아니다. 문턱("2/4/6")을
    //     빼면 "지금 몇이고 다음 문턱이 얼마인가" 를 칩만 보고는 알 수 없다.
    //
    //   ⚠ 보유 카운터를 따로 둔 이유 — 문턱 줄만 띄우면 굵게 칠해진 숫자가
    //     곧 보유 수인 줄 알기 어렵다. 3장이어도 굵은 것은 2 라 2장으로 읽힌다.
    // 여백 6 + 아이콘 48 + 6 + 문턱 86 + 6 = 152
    /// <summary>
    /// 시너지 칩 가로. <b>이 값이 성벽 위치를 정한다</b> — 아래 주의 참고.
    ///
    /// ⚠ 152 → 176 (사용자 지적, 2026-09-07)
    ///   칩 글자가 문턱 셋(2/4/6)만 띄우고 **지금 몇 장인지를 말하지 않았다.**
    ///   현재 개수를 앞에 세우면서(MonsterSynergyRule.StepsLabelOf) 글자가
    ///   "3 2/4/6" 으로 길어졌다. 문턱을 70% 로 내려도 FontMd(42) 기준
    ///   약 106px 이라, 아이콘(48)+여백(18) 을 빼면 152 로는 모자란다.
    ///
    /// ⚠ 넓히면 성벽도 함께 밀어야 한다
    ///   이 줄은 화면 왼쪽 끝(HudMargin 16)에 서고, 라인 대기열은 성벽에서
    ///   왼쪽으로 177px 자란다. 둘이 부딪히면 **UI 를 줄이지 말고 성벽을
    ///   오른쪽으로 민다** (사용자 확정) — InGameSceneSetup.WallX 다.
    ///     시너지 줄 = 16 ~ 192   ·   대기열 = 성벽px − 177 = 243 ~ 414
    ///     (WallX −12 → 성벽 420px) — 51px 여유
    /// </summary>
    const float SynergyChipW    = 176f;
    // ⚠ 68 → 45 · 간격 8 → 3 · 아이콘 48 → 40 (2026-09-15) — 시너지가 13종이 되어
    //   중첩 1 + 13 = 14칸이 178 + 45×14 + 3×13 = 847 ≤ 전장 끝(908) 에 들어가야 한다.
    //   글자도 FontMd → FontSm (한 줄 43 ≤ 칸 45, UI 규칙 5).
    const float SynergyChipH    = 45f;
    const float SynergyChipGap  = 3f;
    const float SynergyIconSize = 40f;
    const float SynergyChipPad  = 6f;
    // ⚠ 보유 칸(SynergyCountW)·문턱 폭(SynergyStepsW)은 없앴다 —
    //   문턱 줄이 아이콘 오른쪽 남은 자리를 통째로 쓴다.

    /// <summary>
    /// 시너지 줄이 화면 위에서 떨어진 거리 — <b>마왕성 체력 막대 바로 아래</b>.
    ///
    /// ⚠ 가운데 정렬을 걷어냈다 (사용자 지적, 2026-09-08)
    ///   전에는 세로 중심이 전장 한가운데(y≈510)라, 칩이 둘뿐일 때 줄이
    ///   화면 정중앙에 떠서 **성 안뜰의 소환사를 정면으로 덮었다.**
    ///   칩 수는 판마다 달라지므로 가운데 정렬은 "몇 개냐" 에 따라 줄이
    ///   위아래로 움직인다 — 무엇을 가릴지 미리 알 수 없다는 뜻이다.
    ///   위에서부터 쌓으면 자라는 방향이 아래 하나뿐이라 자리가 예측된다.
    ///
    ///   왼쪽 줄기 순서: 특성(10..82) → 체력(92..138) → 시너지(148..).
    ///   여덟 칸이 다 떠도 148 + 68×8 + 8×7 = 748 이라 전장(…908) 안이다.
    /// </summary>
    const float SynergyBarTop = CoreBarTop + CoreBarH + Gutter;

    static void BuildSynergyBar(GameObject hud, Sprite[] synergyIcons)
    {
        DestroyChild(hud.transform, "SynergyBar");

        // ── 왼쪽 가장자리 세로 줄 ──
        //   ⚠ 마왕성 기둥이 있던 자리다. 둘을 맞바꿨다 (사용자 확정, 2026-09-07)
        //     체력은 짧은 가로 막대라야 체력으로 읽히고, 시너지는 개수가
        //     들쭉날쭉해서 세로로 쌓는 쪽이 자리를 덜 먹는다.
        //   ⚠ **바뀐 것은 줄의 방향뿐이다** — 칸 안(아이콘·보유·문턱)은 그대로다.
        var root   = EditorUIBuilder.Go("SynergyBar", hud);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0f, 1f);
        rootRt.pivot     = new Vector2(0f, 1f);
        // ⚠ 위에서부터 쌓는다 — 체력 막대 끝에 이어 붙인다 (SynergyBarTop 주석 참고)
        rootRt.anchoredPosition = new Vector2(HudMargin, -SynergyBarTop);
        // 칸 = 중첩 1 + 시너지 13. 14칸이 다 떠도 178 + 45×14 + 3×13 = 847 ≤ 전장 끝(908)
        int rows = MonsterSynergySlots + 1;
        rootRt.sizeDelta        = new Vector2(
            SynergyChipW,
            SynergyChipH * rows + SynergyChipGap * (rows - 1));

        var layout = root.AddComponent<VerticalLayoutGroup>();
        layout.spacing               = SynergyChipGap;
        // ⚠ 위쪽 정렬이다 — 가운데로 두면 칩 수에 따라 줄이 위아래로 떠다닌다
        layout.childAlignment        = TextAnchor.UpperLeft;
        layout.childControlWidth     = true;
        layout.childControlHeight    = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var ui = root.AddComponent<SynergyBarUI>();
        var so = new SerializedObject(ui);

        // ── 중첩 칩 — 줄 맨 위 (사용자 지시, 2026-09-12) ──
        //   ⚠ 먼저 만든다 — 세로 레이아웃은 형제 순서대로 쌓는다
        //   시너지 그림이 없으므로 아이콘 칸에 겹친 마름모 셋을 그린다 (UI 규칙 2 — 글리프 금지)
        GameObject stack = BuildSynergyChip(root, -1, out Image sFace, out Image sIcon,
                                            out TextMeshProUGUI sLabel, out SynergyChipUI sHover);
        stack.name    = "Chip_Stack";
        sIcon.enabled = false;
        DrawStackIcon(stack);

        SerializedProperty sp = so.FindProperty("_stackChip");
        sp.FindPropertyRelative("Root").objectReferenceValue  = stack;
        sp.FindPropertyRelative("Face").objectReferenceValue  = sFace;
        sp.FindPropertyRelative("Icon").objectReferenceValue  = sIcon;
        sp.FindPropertyRelative("Label").objectReferenceValue = sLabel;
        sp.FindPropertyRelative("Hover").objectReferenceValue = sHover;

        SerializedProperty chips = so.FindProperty("_chips");
        chips.arraySize = MonsterSynergySlots;

        for (int i = 0; i < MonsterSynergySlots; i++)
        {
            GameObject chip = BuildSynergyChip(root, i, out Image face, out Image icon,
                                               out TextMeshProUGUI label,
                                               out SynergyChipUI hover);

            SerializedProperty e = chips.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Root").objectReferenceValue  = chip;
            e.FindPropertyRelative("Face").objectReferenceValue  = face;
            e.FindPropertyRelative("Icon").objectReferenceValue  = icon;
            e.FindPropertyRelative("Label").objectReferenceValue = label;
            e.FindPropertyRelative("Hover").objectReferenceValue = hover;
        }

        // ⚠ AllTags 순서 그대로 넣는다 — 런타임이 그 인덱스로 그림을 찾는다
        EditorUIBuilder.SetObjArray(so, "_icons", synergyIcons, Tag);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>중첩 칩의 그림 — 아래로 갈수록 흐린 마름모 셋을 겹쳐 "쌓인다" 를 그린다.</summary>
    static void DrawStackIcon(GameObject chip)
    {
        var holder = EditorUIBuilder.Go("StackIcon", chip);
        var hrt    = holder.GetComponent<RectTransform>();
        hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 0.5f);
        hrt.pivot     = new Vector2(0f, 0.5f);
        hrt.anchoredPosition = new Vector2(SynergyChipPad, 0f);
        hrt.sizeDelta        = new Vector2(SynergyIconSize, SynergyIconSize);

        float[] alpha = { 0.45f, 0.72f, 1f };
        for (int i = 0; i < alpha.Length; i++)
        {
            var d = EditorUIBuilder.Diamond(holder, $"Layer{i}", 24f, new Color(1f, 1f, 1f, alpha[i]));
            d.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -12f + 12f * i);
        }
    }

    /// <summary>
    /// 칸 하나 — [아이콘][개수]. 그림이 시너지 이름을 대신한다.
    ///
    /// ⚠ 그림은 런타임이 갈아 끼운다 — 칸마다 어느 시너지가 올지 정해져 있지 않다
    ///   켜진 것부터 앞에서 채우므로 1번 칸이 매 판 다른 시너지가 된다.
    ///   그래서 Creator 는 빈 Image 만 놓고, 여덟 장 전부를 SynergyBarUI 에 쥐여 준다.
    ///
    /// 색(바탕·글자)도 런타임이 단계에 맞춰 넣는다.
    /// </summary>
    static GameObject BuildSynergyChip(GameObject parent, int index,
                                       out Image face, out Image icon,
                                       out TextMeshProUGUI label,
                                       out SynergyChipUI hover)
    {
        face = EditorUIBuilder.Img(parent, $"Chip_{index + 1}", Color.white);
        GameObject chip = face.gameObject;

        // ⚠ 레이캐스트를 켠다 — 이게 꺼져 있으면 눌러도 툴팁이 안 뜬다
        //   바탕이 곧 클릭 판정 면이다. 아이콘·글자는 꺼 두어 그 위를 통과시킨다.
        face.raycastTarget = true;

        EditorUIBuilder.LE(chip, SynergyChipW, SynergyChipH);

        icon = EditorUIBuilder.Img(chip, "Icon", Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        var iconRt = icon.rectTransform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot     = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(SynergyChipPad, 0f);
        iconRt.sizeDelta        = new Vector2(SynergyIconSize, SynergyIconSize);

        // 문턱 줄 — 아이콘 오른쪽 전부를 쓴다.
        //   ⚠ 보유 숫자를 따로 두지 않는다 (사용자 지적, 2026-09-07)
        //     문턱 줄이 이미 그 말을 한다 — 도달한 칸이 굵고 단계 색이다.
        //     정확한 수는 올리면 뜨는 툴팁 제목("숲 3/5")이 말해 준다.
        label = EditorUIBuilder.TMP(chip, "Label", "", UIScale.FontSm, FontStyles.Bold);
        var labelRt = label.rectTransform;
        labelRt.anchorMin = new Vector2(0f, 0f); labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = new Vector2(SynergyChipPad * 2f + SynergyIconSize, 0f);
        labelRt.offsetMax = new Vector2(-SynergyChipPad, 0f);
        label.alignment        = TextAlignmentOptions.Midline;
        label.raycastTarget    = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        hover = chip.AddComponent<SynergyChipUI>();

        chip.SetActive(false);
        return chip;
    }

    /// <summary>
    /// 라인 경계 표시 — 라인마다 띠 하나.
    ///
    /// 여기서는 띠를 만들기만 한다. 화면 좌표(성벽부터 오른쪽 끝까지,
    /// 라인 사이 중간이 경계)는 런타임에 LaneGuideUI 가 매 프레임 잡는다.
    /// 카메라가 스크롤하므로 한 번 잡아 두면 어긋난다.
    /// </summary>
    static void BuildLaneGuide(GameObject hud)
    {
        DestroyChild(hud.transform, "LaneGuide");

        var root = EditorUIBuilder.Go("LaneGuide", hud);
        EditorUIBuilder.Stretch(root);
        root.GetComponent<RectTransform>().SetAsFirstSibling();   // 전장 위, 다른 UI 아래

        var bands  = new RectTransform[LaneCount];
        var images = new UnityEngine.Object[LaneCount];

        for (int lane = 0; lane < LaneCount; lane++)
        {
            // 색은 런타임에 LaneGuideUI 가 한 칸 걸러 갈아 끼운다 — 여기서는
            // 두 곳이 각자 색을 들고 있지 않도록 흰색으로만 둔다.
            Image band = EditorUIBuilder.Img(root, $"Band_{lane + 1}", Color.white);
            band.raycastTarget = false;                           // 탭은 전장이 받는다

            var rt = band.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            bands[lane]  = rt;
            images[lane] = band;
        }

        var ui = root.AddComponent<LaneGuideUI>();
        var so = new SerializedObject(ui);
        EditorUIBuilder.SetObjArray(so, "_bands",      bands.Cast<UnityEngine.Object>().ToArray(), Tag);
        EditorUIBuilder.SetObjArray(so, "_bandImages", images,                                     Tag);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>대기열 한 칸 — 초상화 + "×N".</summary>
    static GameObject BuildQueueSlot(GameObject parent, int index, Sprite fallbackIcon,
                                     out Image portrait, out Image progress,
                                     out TextMeshProUGUI countText)
    {
        var slot = EditorUIBuilder.Go($"Slot_{index + 1}", parent);

        // 셀 왼쪽 = 초상화
        portrait = EditorUIBuilder.Img(slot, "Portrait", Color.white);
        portrait.sprite         = fallbackIcon;
        portrait.preserveAspect = true;
        portrait.raycastTarget  = false;
        var pRt = portrait.rectTransform;
        pRt.anchorMin = pRt.anchorMax = new Vector2(0f, 0.5f);
        pRt.pivot     = new Vector2(0f, 0.5f);
        pRt.anchoredPosition = Vector2.zero;
        pRt.sizeDelta        = new Vector2(QueueSlotSize, QueueSlotSize);

        // 진행 표시 — 같은 초상화를 검은 반투명으로 겹치고 360° 로 채운다.
        // 아래 초상화가 드러나는 식이라 남은 시간이 그림 위에서 바로 읽힌다.
        progress = EditorUIBuilder.Img(slot, "Progress", new Color(0f, 0f, 0f, 0.62f));
        progress.sprite         = fallbackIcon;
        progress.preserveAspect = true;
        progress.raycastTarget  = false;
        progress.type           = Image.Type.Filled;
        progress.fillMethod     = Image.FillMethod.Radial360;
        progress.fillOrigin     = (int)Image.Origin360.Top;
        progress.fillClockwise  = false;
        progress.fillAmount     = 0f;
        var gRt = progress.rectTransform;
        gRt.anchorMin = gRt.anchorMax = new Vector2(0f, 0.5f);
        gRt.pivot     = new Vector2(0f, 0.5f);
        gRt.anchoredPosition = Vector2.zero;
        gRt.sizeDelta        = new Vector2(QueueSlotSize, QueueSlotSize);   // 초상화와 정확히 겹친다

        // 셀 오른쪽 = 숫자. 초상화와 겹치지 않는 자리다.
        countText = EditorUIBuilder.TMP(slot, "Count", "6", UIScale.FontSm, FontStyles.Bold);
        var cRt = countText.rectTransform;
        cRt.anchorMin = cRt.anchorMax = new Vector2(1f, 0.5f);
        cRt.pivot     = new Vector2(1f, 0.5f);
        cRt.anchoredPosition = Vector2.zero;
        cRt.sizeDelta        = new Vector2(QueueNumW, QueueSlotSize);
        countText.alignment        = TextAlignmentOptions.MidlineLeft;
        countText.raycastTarget    = false;
        countText.textWrappingMode = TextWrappingModes.NoWrap;

        // ⚠ 넘치면 줄이지 말고 잘라낸다
        //   자동 축소를 켜면 칸마다 글자 크기가 달라져 눈이 어지럽다.
        countText.overflowMode = TextOverflowModes.Overflow;
        countText.margin       = new Vector4(3f, 0f, 0f, 0f);

        // ⚠ 외곽선을 두르지 않는다 (UI 규칙 8)
        //   여기는 성벽 위라 바탕이 늘 어둡다 — 흰 숫자로 충분히 읽힌다.
        //   TMP 의 outlineWidth 는 프리팹에 저장되지도 않으므로 쓸 이유가 없다.

        return slot;
    }

    static void SetTopButtonRight(RectTransform rt, float right, float width)
    {
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot     = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-right, 0f);
        rt.sizeDelta        = new Vector2(width, UIScale.BtnSm);
    }

    // ── 스테이지 시작(말풍선) 버튼 ───────────────────────────

    /// <summary>
    /// 버튼이 화면 오른쪽 끝에서 띄우는 거리(px).
    ///
    /// ⚠ 34 → 96 (2026-09-02) — 화면 끝에 너무 붙어 잘려 보였다
    ///   고리 지름이 178 인데 여백이 34 뿐이었다. 자기 지름의 5분의 1 도
    ///   안 되는 거리라, 아래 두 가지 중 하나만 겹쳐도 오른쪽이 먹힌다.
    ///     · 기기 안전영역(노치·둥근 모서리) — 이 프로젝트는 아직 처리하지 않는다
    ///     · 16:9 보다 넓은 화면 (CanvasScaler match 0.5 라 캔버스 폭이 1920 을 넘는다)
    ///   96 이면 고리 반지름(89)보다 커서 어느 쪽이 먹어도 원이 온전히 남는다.
    ///
    ///   "화면 오른쪽 끝 한가운데" 라는 자리(사용자 확정)는 그대로다 —
    ///   끝에 붙이는 것과 끝에서 잘리는 것은 다르다.
    /// </summary>
    const float StartBtnRightInset = 96f;

    /// <summary>버튼 몸통 크기. 정사각이라 고리가 정원으로 돈다.</summary>
    const float StartBtnSize = 132f;

    /// <summary>고리 바깥 지름. 버튼보다 커야 테두리처럼 둘린다.</summary>
    const float StartRingSize = 178f;

    /// <summary>
    /// 말풍선 버튼. 지금은 스테이지 시작을 겸한다.
    ///
    /// ■ 자리 — 화면 오른쪽 끝 한가운데 (사용자 확정, 2026-08-28)
    ///   용사가 걸어 들어오는 쪽이다. 상단바에 있을 때는 배속·일시정지와
    ///   한 줄에 섞여 "판을 시작하는 버튼" 으로 읽히지 않았다.
    ///
    /// ■ 남은 준비 시간은 360° 고리로 그린다
    ///   버튼 뒤에 원반 두 장을 깐다 — 어두운 트랙(항상 가득)과 그 위의
    ///   밝은 고리(Filled/Radial360, 시계 방향). 시간이 흐르면 밝은 쪽만
    ///   줄어 트랙이 드러난다. 한 바퀴가 다 닳으면 용사가 들어온다.
    ///
    ///   ⚠ 원은 유니티 내장 Knob 스프라이트다 (UI 규칙 2번)
    ///     ○ 같은 글자는 기본 폰트에 없어 □ 로 나온다. 원형 스프라이트를
    ///     Radial360 으로 채우는 것이 가장 싸다.
    ///
    ///   ⚠ 고리는 버튼의 **앞 형제**로 만든다 (UI 규칙 3번)
    ///     Unity UI 는 부모 Graphic → 자식 순으로 그린다. 버튼 자식으로 넣으면
    ///     버튼 면 위에 얹혀 말풍선을 가린다. 앞 형제여야 뒤에 깔린다.
    ///
    /// 말풍선은 둥근 몸통 + 아래 꼬리다. 폰트 글리프를 쓰지 않는다.
    /// </summary>
    static void BuildEnemyInfoButton(GameObject hud)
    {
        DestroyChild(hud.transform, "StageStartRoot");

        var root = EditorUIBuilder.Go("StageStartRoot", hud);
        {
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-StartBtnRightInset, 0f);
            rt.sizeDelta        = new Vector2(StartRingSize, StartRingSize);
        }

        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // ── 트랙 (항상 가득 찬 어두운 원반) ──
        var track = EditorUIBuilder.Img(root, "ReadyTrack", new Color(0.06f, 0.07f, 0.13f, 0.72f));
        CenterCircle(track, knob, StartRingSize);

        // ── 남은 시간 고리 ──
        var fill = EditorUIBuilder.Img(root, "ReadyFill", new Color(0.46f, 0.70f, 1f, 0.95f));
        CenterCircle(fill, knob, StartRingSize);
        fill.type            = Image.Type.Filled;
        fill.fillMethod      = Image.FillMethod.Radial360;
        fill.fillOrigin      = (int)Image.Origin360.Top;
        fill.fillClockwise   = true;
        fill.fillAmount      = 1f;

        // ── 버튼 ──
        //
        //  ⚠ 사각 버튼이 아니라 **원**이다 (2026-09-03)
        //    뒤에 깔린 준비 고리가 원인데 그 위에 정사각 면이 얹혀 있어,
        //    화면에서는 "원 안에 박힌 네모난 박스" 로 보였다. 둘이 한 물건으로
        //    읽히지 않으니 고리가 버튼의 테두리가 아니라 별개의 장식이 된다.
        //    몸통도 원으로 맞추면 고리·몸통·꼬리가 한 덩이로 읽힌다.
        var button = EditorUIBuilder.RoundBtn(root, "EnemyInfoButton",
                                              new Color(0.24f, 0.20f, 0.42f), out var body);
        EditorUIBuilder.SetRoundSize(button, StartBtnSize);
        {
            var rt = button.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        // 몸통 — 원형 스프라이트를 쓴다. 기본 Image 는 각진 흰 사각이라
        // 둥근 버튼 면 위에 "흰 박스" 하나가 더 얹힌 것처럼 보였다.
        //
        // ⚠ 64×46 → 84×62 (2026-09-03) — 숫자가 안 읽힌다는 지적
        //   말풍선이 숫자보다 겨우 클 정도라, 어두운 숫자와 흰 바탕이 붙어
        //   하나의 회색 덩어리로 보였다. 대비는 색으로 이미 최대치라
        //   (흰 바탕 · 거의 검은 글자) 남은 수단은 **바탕을 넓히는 것**이다.
        var bubble = EditorUIBuilder.Img(body, "Bubble", Color.white);
        bubble.sprite = EditorUIBuilder.Circle();
        var bubbleRt = bubble.rectTransform;
        bubbleRt.anchorMin = bubbleRt.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleRt.pivot     = new Vector2(0.5f, 0.5f);
        bubbleRt.anchoredPosition = new Vector2(0f, 10f);
        bubbleRt.sizeDelta        = new Vector2(84f, 62f);
        bubble.raycastTarget = false;

        // 꼬리 — 아래로 뻗은 작은 사각. 회전 막대로 만든다.
        EditorUIBuilder.Bar(body, "Tail", 16f, 18f, 45f, new Vector2(-12f, -28f), Color.white);

        // ── 남은 준비 시간(숫자) ─────────────────────────────
        //   고리가 대략을 보여 주고 숫자가 정확한 초를 받친다.
        //
        //   ⚠ 말풍선 **안**에 넣는다
        //     흰 바탕 위에 어두운 숫자를 얹으면 그대로 읽힌다.
        var countdown = EditorUIBuilder.TMP(body, "Countdown", "20",
                                            UIScale.FontMd, FontStyles.Bold);
        {
            var rt = countdown.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 10f);
            rt.sizeDelta        = new Vector2(84f, UIScale.RowMd);
        }
        countdown.alignment        = TextAlignmentOptions.Center;
        countdown.raycastTarget    = false;
        countdown.textWrappingMode = TextWrappingModes.NoWrap;

        // ⚠ 남색(0.12,0.12,0.22)이 아니라 거의 검정이다 (2026-09-03)
        //   그 남색은 버튼 면(0.24,0.20,0.42)과 같은 계열이라, 흰 말풍선 밖으로
        //   조금이라도 삐져나가면 그대로 묻혔다. 말풍선 위에서만 사는 글자이니
        //   흰 바탕에 대해 가장 진한 값으로 둔다.
        countdown.color = new Color(0.05f, 0.05f, 0.09f);

        // ⚠ 정착 색(EnemyInfoButtonUI.CountdownColor)도 함께 봐야 한다
        //   런타임이 매 프레임 색을 덮어쓴다 — 여기 값은 프리팹 기본값이다.

        // ⚠ 컴포넌트는 **루트**에 붙인다 — 버튼이 아니다 (2026-09-03)
        //   대기가 아닐 때 버튼을 통째로 숨기려면, 숨기는 쪽과 숨겨지는 쪽이
        //   같은 오브젝트일 수 없다. 버튼에 붙여 두고 그 버튼을 끄면 자기
        //   OnDisable 에서 StageLoopDirector 구독을 놓아 버려, 다음 스테이지
        //   대기가 와도 다시 켜 줄 사람이 없다(영영 사라진다).
        //   루트는 항상 켜져 있으므로 구독이 끊기지 않는다.
        var ui = root.AddComponent<EnemyInfoButtonUI>();
        var so = new SerializedObject(ui);
        EditorUIBuilder.SetObj(so, "_button",        button,           Tag);
        EditorUIBuilder.SetObj(so, "_countdownText", countdown,        Tag);
        EditorUIBuilder.SetObj(so, "_readyFill",     fill,             Tag);
        EditorUIBuilder.SetObj(so, "_readyTrack",    track.gameObject, Tag);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>원형 스프라이트를 부모 한가운데에 정사각으로 깐다.</summary>
    static void CenterCircle(Image img, Sprite knob, float size)
    {
        if (knob != null) img.sprite = knob;

        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(size, size);

        // 고리는 표시일 뿐이다 — 버튼보다 넓어서 탭을 가로채면 안 된다.
        img.raycastTarget = false;
    }

    static void SetTopButton(RectTransform rt, float left, float width)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(left, 0f);
        rt.sizeDelta = new Vector2(width, UIScale.BtnSm);
    }

    static void SetButtonAccent(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 3f);
        rt.sizeDelta = new Vector2(-14f, 6f);
    }

    static MonsterSpeciesData[] LoadSpeciesCatalog()
        => AssetDatabase.FindAssets("t:MonsterSpeciesData", new[] { "Assets/_project/Data/Monsters" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<MonsterSpeciesData>)
            .Where(x => x != null)
            .OrderBy(x => x.Id)
            .ToArray();

    static void SaveSceneHud(GameObject hud)
    {
        string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hud);
        if (sourcePath == HudPath)
            PrefabUtility.ApplyPrefabInstance(hud, InteractionMode.AutomatedAction);
        else
            PrefabUtility.SaveAsPrefabAssetAndConnect(hud, HudPath, InteractionMode.AutomatedAction);
    }

    static void PatchHudPrefab(GameObject cardPrefab, Sprite manaIcon, Sprite countIcon,
                               Sprite[] synergyIcons, Sprite goldIcon)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        if (asset == null)
        {
            Debug.LogError($"[{Tag}] 기존 HUD 프리팹이 없습니다: {HudPath}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        PatchHud(root, cardPrefab, manaIcon, countIcon, synergyIcons, goldIcon);
        PrefabUtility.SaveAsPrefabAsset(root, HudPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void FixCanvas(Canvas canvas)
    {
        if (canvas == null) return;
        if (!canvas.TryGetComponent<CanvasScaler>(out var scaler))
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UIScale.RefWidth, UIScale.RefHeight);
        scaler.matchWidthOrHeight = UIScale.Match;

        if (!canvas.TryGetComponent<GraphicRaycaster>(out _))
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, canvas.gameObject.scene);
        }
    }

    /// <summary>
    /// 그 이름의 자식을 <b>전부</b> 지운다.
    ///
    /// ⚠ 하나만 지우면 안 된다 (사용자 지적, 2026-09-07)
    ///   전에는 Find 로 첫 개를 지웠다. 지우는 코드를 빠뜨린 채 몇 번 구우면
    ///   같은 이름이 여러 개 쌓이는데(실제로 PerkBar 가 넷이었다), 그 상태에서
    ///   고쳐도 한 번에 하나씩만 사라져 원인이 남아 있는 것처럼 보인다.
    ///   ⚠ 뒤에서부터 훑는다 — 지우면서 인덱스가 밀린다.
    /// </summary>
    static void DestroyChild(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    static void SetBottom(RectTransform rt, float height, float sideInset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(-sideInset * 2f, height);
    }

    static void SetTop(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, height);
    }

    static void SetTopCenter(RectTransform rt, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = new Vector2(width, height);
    }
}
