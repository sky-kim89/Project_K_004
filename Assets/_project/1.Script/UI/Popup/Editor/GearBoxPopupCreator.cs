using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  GearBoxPopupCreator.cs
//  Tools > Project K > 프리팹 생성 > 팝업 > 보상 상자
//
//  ┌──────────────────────────────┐   ← 가운데 창 (전체 화면 아님)
//  │      스테이지 12 보상          │
//  │         희귀 상자             │   ← 등급색. 열기 전에도 무게는 안다
//  │                              │
//  │         ╔════════╗           │   ← 뚜껑 (열릴 때 날아간다)
//  │         ║        ║           │   ← 상자. 등급색. 누르면 흔들린다
//  │         ╚════════╝           │
//  │        눌러서 열기            │
//  │      [     확인     ]         │   ← 열기 전에는 없다
//  └──────────────────────────────┘
//
//  ■ 전체 화면이 아니다 — 가운데 창이다
//    도감·몬스터 상세와 달리 여기는 **한 가지만** 보여 준다. 화면을 꽉 채우면
//    받은 것이 얼마나 대단한지가 아니라 빈 공간이 먼저 눈에 들어온다.
//
//  ■ 상자와 장비는 같은 자리를 쓴다
//    _boxRoot 와 _gearRoot 가 같은 칸(무대)에 겹쳐 있고 런타임이 하나만 켠다.
//    상자가 있던 자리에서 장비가 나와야 "그 안에서 나왔다" 로 읽힌다.
//
//  ■ ⚠ 무대 안에서 **가운데 정렬**이다 (사용자 지적, 2026-09-09)
//    예전에는 둘 다 무대 맨 위에 붙어 있었다. 그래서 상자가 제목 바로 밑에
//    매달린 것처럼 보였고(빛무리는 제목 위로 넘쳤다), 반대로 장비 쪽은
//    설명 두 줄이 무대를 넘겨 **확인 버튼과 49px 겹쳤다.**
//    지금은 각자 제 높이를 재서 남는 세로를 위아래로 나눈다 —
//    아래 BoxBlockH / GearBlockH 가 그 계산의 정본이고, Verify 가 넘치면 잡는다.
//
//  ■ 연출 부품도 여기서 굽는다 (사용자 요청 — 등급이 높을수록 화려하게)
//    빛무리 · 도는 빛살 16개 · 터질 때 퍼지는 불꽃 24개 · 화면 섬광.
//    ⚠ 세기는 여기서 정하지 않는다 — GearBoxPopup.LookOf 표가 정본이다.
//      여기는 **있을 수 있는 최대치**를 굽는다. 불꽃 수를 줄이면 영웅 상자만
//      조용해진다 (SparkCount 주석 참고).
//
//  ⚠ 상자 그림은 도형으로 그린다 — 전용 아트가 없다
//    사각 면 + 뚜껑 + 가운데 띠. 등급색만 런타임이 갈아 끼운다.
//    (UI 규칙 2 — 장식 기호에 폰트 글리프를 쓰지 않는다)
//
//  ⚠ PopupManager 의 _prefabs 배열에 등록해야 열린다
//    등록 전에는 RunBootstrap 이 경고만 남기고 넘어간다 — 장비는 이미 지급된다.
// ============================================================

public static class GearBoxPopupCreator
{
    const string SavePath = "Assets/_project/2.Prefabs/UI/GearBoxPopup.prefab";
    const string Tag      = "GearBoxPopupCreator";

    const float PanelW = 720f;
    const float PanelH = 760f;
    const float Pad    = 30f;

    const float BoxSize  = 240f;
    const float LidH     = 62f;
    const float GlowSize = 360f;
    const float IconSize = 150f;

    // ── 빛살·불꽃 ────────────────────────────────────────────
    //
    //  ⚠ SparkCount 는 GearBoxPopup.LookOf 의 **최댓값 이상**이어야 한다
    //    런타임은 표가 말하는 수만큼만 켠다. 여기가 적으면 영웅 상자에서
    //    불꽃이 모자라는데 **에러는 나지 않는다** — 그냥 초라해진다.
    const int SparkCount = 24;
    const int RayCount   = 8;      // 막대 하나가 양쪽으로 뻗으니 화면에는 16갈래다

    //  ⚠ 빛살 길이는 무대 안에 들어와야 한다
    //    무대 밖으로 뻗으면 제목·등급 줄 위를 가로지른다 (자르는 마스크가 없다).
    //    상자 한가운데에서 무대 위 끝까지가 약 198 이고, 터질 때 1.25배로 커지므로
    //    340(±170 → 터질 때 ±212)이 상한이다.
    const float RayLen   = 340f;
    const float RayThick = 16f;
    const float SparkSize = 16f;

    // ── 세로 예산 ────────────────────────────────────────────
    //
    //  위 : 여백 30 + 제목 RowLg + 4 + 등급 줄 RowSm + 16
    //  아래: 여백 30 + 확인 버튼 BtnFor(FontMd) + 사이 24
    //  무대 = PanelH − 위 − 아래.  ⚠ Verify 가 두 블록이 들어가는지 검사한다.

    static float CloseH   => UIScale.BtnFor(UIScale.FontMd);
    static float StageTop => Pad + UIScale.RowLg + 4f + UIScale.RowSm + 16f;
    static float StageH   => PanelH - StageTop - (Pad + CloseH + 24f);

    /// <summary>상자 블록의 높이 — 상자 + 사이 + "눌러서 열기".</summary>
    static float BoxBlockH => BoxSize + 22f + UIScale.RowMd;

    /// <summary>장비 블록의 높이 — 틀 + 이름 + 등급 + 스탯 + 설명 두 줄.</summary>
    static float GearBlockH => (IconSize + 8f) + 18f
                             + UIScale.RowLg + 4f
                             + UIScale.RowSm + 12f
                             + UIScale.RowMd + 8f
                             + UIScale.RowSm * 2f;

    static readonly Color ScrimBg   = new Color(0.03f, 0.035f, 0.06f, 0.86f);
    static readonly Color PanelBg   = new Color(0.070f, 0.075f, 0.130f, 1f);
    static readonly Color PanelEdge = new Color(0.24f, 0.30f, 0.52f, 1f);
    static readonly Color SlotBg    = new Color(0.105f, 0.115f, 0.190f, 1f);
    static readonly Color SubText   = new Color(0.72f, 0.76f, 0.90f, 1f);
    static readonly Color BoxDark   = new Color(0f, 0f, 0f, 0.28f);
    static readonly Color BoxLight  = new Color(1f, 1f, 1f, 0.22f);

    [MenuItem(ProjectKMenu.Popup + "보상 상자", priority = ProjectKMenu.PrefabPrio + 48)]
    public static void Run()
    {
        Verify();

        var root = new GameObject("GearBoxPopup", typeof(RectTransform));
        root.AddComponent<CanvasGroup>();
        var popup = root.AddComponent<GearBoxPopup>();

        EditorUIBuilder.Stretch(root);

        var scrim = EditorUIBuilder.Img(root, "Scrim", ScrimBg);
        EditorUIBuilder.Stretch(scrim.gameObject);

        GameObject panel = BuildPanel(root);

        var title = EditorUIBuilder.TMP(panel, "Title", "스테이지 0 보상",
                                        UIScale.FontLg, FontStyles.Bold);
        title.alignment     = TextAlignmentOptions.Center;
        title.raycastTarget = false;
        EditorUIBuilder.AnchorTop(title.rectTransform, Pad, UIScale.RowLg, Pad * 2f);

        // 등급 줄 — 무엇인지는 몰라도 얼마나 좋은지는 열기 전에 안다.
        var gradeLine = EditorUIBuilder.TMP(panel, "GradeLine", "희귀 상자",
                                            UIScale.FontSm, FontStyles.Bold);
        gradeLine.alignment     = TextAlignmentOptions.Center;
        gradeLine.raycastTarget = false;
        EditorUIBuilder.AnchorTop(gradeLine.rectTransform,
                                  Pad + UIScale.RowLg + 4f, UIScale.RowSm, Pad * 2f);

        // ── 무대 — 상자와 장비가 같은 자리를 쓴다 ──
        var stage = EditorUIBuilder.Go("Stage", panel);
        EditorUIBuilder.AnchorTop(stage.GetComponent<RectTransform>(),
                                  StageTop, StageH, Pad * 2f);

        // 상자의 한가운데 — 빛무리·빛살·불꽃이 전부 이 점을 기준으로 놓인다.
        float boxTop      = (StageH - BoxBlockH) * 0.5f;
        float boxCenterY  = -(boxTop + BoxSize * 0.5f);

        BuildRays(stage, boxCenterY, out var rays, out var rayImgs);

        var glow = BuildGlow(stage, boxCenterY);

        BuildBox(stage, boxTop, out var boxRoot, out var boxBtn, out var boxImg,
                 out var lid, out var lidImg, out var hint);

        BuildSparks(stage, boxCenterY, out var sparks, out var sparkImgs);

        BuildGear(stage, (StageH - GearBlockH) * 0.5f,
                  out var gearRoot, out var frame, out var icon,
                  out var gearName, out var gearGrade, out var gearStat, out var gearDesc);

        BuildClose(panel, out var closeBtn, out var closeLabel);

        // ⚠ 섬광은 **맨 마지막 자식**이다 — 창까지 덮어야 터진 것으로 보인다.
        var flash = EditorUIBuilder.Img(root, "Flash", new Color(1f, 1f, 1f, 0f));
        EditorUIBuilder.Stretch(flash.gameObject);
        flash.raycastTarget = false;

        var so = new SerializedObject(popup);
        EditorUIBuilder.SetEnum(so, "_popupType", (int)PopupType.GearBox, Tag);

        EditorUIBuilder.SetObj(so, "_titleText", title,     Tag);
        EditorUIBuilder.SetObj(so, "_gradeLine", gradeLine, Tag);

        EditorUIBuilder.SetObj(so, "_boxRoot",   boxRoot, Tag);
        EditorUIBuilder.SetObj(so, "_boxButton", boxBtn,  Tag);
        EditorUIBuilder.SetObj(so, "_boxImage",  boxImg,  Tag);
        EditorUIBuilder.SetObj(so, "_lid",       lid,     Tag);
        EditorUIBuilder.SetObj(so, "_lidImage",  lidImg,  Tag);
        EditorUIBuilder.SetObj(so, "_hintText",  hint,    Tag);

        EditorUIBuilder.SetObj(so, "_boxGlow", glow,  Tag);
        EditorUIBuilder.SetObj(so, "_rays",    rays,  Tag);
        EditorUIBuilder.SetObj(so, "_flash",   flash, Tag);
        EditorUIBuilder.SetObjArray(so, "_rayImages",   rayImgs,   Tag);
        EditorUIBuilder.SetObjArray(so, "_sparks",      sparks,    Tag);
        EditorUIBuilder.SetObjArray(so, "_sparkImages", sparkImgs, Tag);

        EditorUIBuilder.SetObj(so, "_gearRoot",  gearRoot,  Tag);
        EditorUIBuilder.SetObj(so, "_gearFrame", frame,     Tag);
        EditorUIBuilder.SetObj(so, "_gearIcon",  icon,      Tag);
        EditorUIBuilder.SetObj(so, "_gearName",  gearName,  Tag);
        EditorUIBuilder.SetObj(so, "_gearGrade", gearGrade, Tag);
        EditorUIBuilder.SetObj(so, "_gearStat",  gearStat,  Tag);
        EditorUIBuilder.SetObj(so, "_gearDesc",  gearDesc,  Tag);

        EditorUIBuilder.SetObj(so, "_closeBtn",   closeBtn,   Tag);
        EditorUIBuilder.SetObj(so, "_closeLabel", closeLabel, Tag);

        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[GearBoxPopupCreator] 생성 완료 → " + SavePath +
                  "\nPopupManager 의 _prefabs 배열에 등록해야 열린다.");
    }

    /// <summary>
    /// 두 블록이 무대에 들어가는지 본다.
    ///
    /// ⚠ 넘쳐도 유니티는 아무 말이 없다 — TMP 는 칸을 넘긴 글도 그냥 그린다.
    ///   실제로 설명 두 줄이 확인 버튼 위로 흘러 49px 겹쳐 있었다 (2026-09-09).
    /// </summary>
    static void Verify()
    {
        if (GearBlockH > StageH)
            Debug.LogError($"[{Tag}] 장비 블록({GearBlockH:0})이 무대({StageH:0})보다 큽니다. " +
                           "PanelH 를 키우거나 설명 줄을 줄이세요.");

        if (BoxBlockH > StageH)
            Debug.LogError($"[{Tag}] 상자 블록({BoxBlockH:0})이 무대({StageH:0})보다 큽니다.");

        if (PanelH > UIScale.PopupMaxH)
            Debug.LogError($"[{Tag}] 창 높이 {PanelH} 가 상한 {UIScale.PopupMaxH} 를 넘습니다 (UI 규칙 6).");
    }

    // ── 창 ───────────────────────────────────────────────────

    static GameObject BuildPanel(GameObject root)
    {
        // ⚠ 테두리는 앞 형제로 만들어 뒤에 깐다 (UI 규칙 3)
        //   자식으로 두면 부모 Image 보다 뒤로 갈 수 없어 면을 덮는다.
        var edge = EditorUIBuilder.Img(root, "PanelEdge", PanelEdge);
        EditorUIBuilder.Center(edge.rectTransform, Vector2.zero,
                               new Vector2(PanelW + 6f, PanelH + 6f));
        edge.raycastTarget = false;

        var panel = EditorUIBuilder.Img(root, "Panel", PanelBg).gameObject;
        EditorUIBuilder.Center(panel.GetComponent<RectTransform>(), Vector2.zero,
                               new Vector2(PanelW, PanelH));

        return panel;
    }

    // ── 연출 부품 ────────────────────────────────────────────

    /// <summary>
    /// 도는 빛살. 막대 하나가 중심을 지나 양쪽으로 뻗으므로 8개면 16갈래다.
    /// ⚠ 진하기는 런타임이 등급색으로 갈아 끼운다 — 일반 상자에서는 통째로 꺼진다.
    /// </summary>
    static void BuildRays(GameObject stage, float centerY,
                          out RectTransform rays, out Object[] images)
    {
        var root = EditorUIBuilder.Go("Rays", stage);
        rays = root.GetComponent<RectTransform>();

        // 무대는 위(1)가 기준이라 centerY 를 그대로 쓴다 (음수 = 아래로).
        rays.anchorMin = rays.anchorMax = rays.pivot = new Vector2(0.5f, 1f);
        rays.anchoredPosition = new Vector2(0f, centerY);
        rays.sizeDelta        = new Vector2(RayLen, RayLen);

        images = new Object[RayCount];

        for (int i = 0; i < RayCount; i++)
        {
            var bar = EditorUIBuilder.Bar(root, $"Ray_{i}", RayLen, RayThick,
                                          180f / RayCount * i, Vector2.zero,
                                          new Color(1f, 1f, 1f, 0.1f));
            images[i] = bar.GetComponent<Image>();
        }
    }

    static Image BuildGlow(GameObject stage, float centerY)
    {
        var glow = EditorUIBuilder.Img(stage, "Glow", new Color(1f, 1f, 1f, 0.2f));
        glow.sprite         = EditorUIBuilder.Circle();
        glow.raycastTarget  = false;
        glow.preserveAspect = true;
        {
            var rt = glow.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, centerY + GlowSize * 0.5f);
            rt.sizeDelta        = new Vector2(GlowSize, GlowSize);
        }
        return glow;
    }

    /// <summary>터질 때 퍼지는 불꽃. 전부 상자 한가운데에서 시작해 사방으로 나간다.</summary>
    static void BuildSparks(GameObject stage, float centerY,
                            out Object[] sparks, out Object[] images)
    {
        var root = EditorUIBuilder.Go("Sparks", stage);
        {
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, centerY);
            rt.sizeDelta        = Vector2.zero;
        }

        sparks = new Object[SparkCount];
        images = new Object[SparkCount];

        for (int i = 0; i < SparkCount; i++)
        {
            var img = EditorUIBuilder.Img(root, $"Spark_{i}", Color.white);
            img.sprite         = EditorUIBuilder.Circle();
            img.preserveAspect = true;
            img.raycastTarget  = false;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(SparkSize, SparkSize);

            img.gameObject.SetActive(false);

            sparks[i] = rt;
            images[i] = img;
        }
    }

    // ── 상자 (열기 전) ───────────────────────────────────────

    static void BuildBox(GameObject stage, float yFromTop,
                         out GameObject boxRoot, out Button btn, out Image box,
                         out RectTransform lid, out Image lidImg, out TextMeshProUGUI hint)
    {
        boxRoot = EditorUIBuilder.Go("BoxRoot", stage);
        {
            // ⚠ 흔들 때 anchoredPosition 을 쓰므로 스트레치가 아니라 **고정 크기**다.
            //   스트레치면 흔들림이 sizeDelta 와 뒤섞여 상자가 늘었다 줄었다 한다.
            var rt = boxRoot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -yFromTop);
            rt.sizeDelta        = new Vector2(BoxSize, BoxBlockH);
        }

        // 상자 본체 — 누르는 대상이다.
        // ⚠ 음각 버튼(RaisedBtn)을 쓰지 않는다 (UI 규칙 1 의 예외)
        //   규칙이 말하는 것은 "평평한 사각형이 버튼인지 라벨인지 모른다" 이다.
        //   여기는 화면 가운데 하나뿐인 물건이고 아래에 "눌러서 열기" 가 붙어 있어
        //   무엇을 눌러야 하는지 헷갈릴 자리가 없다. 상자에 음각 그림자를 두르면
        //   상자가 아니라 버튼으로 보인다.
        var boxGo = EditorUIBuilder.Img(boxRoot, "Box", Color.white).gameObject;
        {
            var rt = boxGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(BoxSize, BoxSize);
        }

        box = boxGo.GetComponent<Image>();
        btn = boxGo.AddComponent<Button>();
        btn.targetGraphic = box;
        // 누름 표시는 밝기로 준다 — targetGraphic 색에 곱해진다 (UI 규칙 1)
        EditorUIBuilder.TintTransition(boxGo, Color.white);

        // 잠금 띠 — 상자로 읽히게 하는 최소한의 선.
        var strap = EditorUIBuilder.Img(boxGo, "Strap", BoxDark);
        {
            var rt = strap.rectTransform;
            rt.anchorMin = new Vector2(0.42f, 0f); rt.anchorMax = new Vector2(0.58f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        strap.raycastTarget = false;

        var shine = EditorUIBuilder.Img(boxGo, "Shine", BoxLight);
        {
            var rt = shine.rectTransform;
            rt.anchorMin = new Vector2(0.10f, 0.12f); rt.anchorMax = new Vector2(0.34f, 0.55f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        shine.raycastTarget = false;

        // ⚠ 뚜껑은 상자의 **자식이 아니다** — 따로 날아가야 한다
        //   상자 안에 두면 몸통이 쪼그라들 때 뚜껑도 같이 줄어든다.
        var lidGo = EditorUIBuilder.Img(boxRoot, "Lid", Color.white);
        {
            lid = lidGo.rectTransform;
            lid.anchorMin = lid.anchorMax = lid.pivot = new Vector2(0.5f, 1f);
            lid.anchoredPosition = Vector2.zero;
            lid.sizeDelta        = new Vector2(BoxSize + 16f, LidH);
        }
        lidGo.raycastTarget = false;
        lidImg = lidGo;

        var lidEdge = EditorUIBuilder.Img(lidGo.gameObject, "LidEdge", BoxDark);
        {
            var rt = lidEdge.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 10f);
        }
        lidEdge.raycastTarget = false;

        var lidShine = EditorUIBuilder.Img(lidGo.gameObject, "LidShine", BoxLight);
        {
            var rt = lidShine.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 8f);
        }
        lidShine.raycastTarget = false;

        hint = EditorUIBuilder.TMP(boxRoot, "Hint", "눌러서 열기", UIScale.FontMd, FontStyles.Bold);
        hint.color         = SubText;
        hint.alignment     = TextAlignmentOptions.Center;
        hint.raycastTarget = false;
        {
            var rt = hint.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(BoxSize + 22f));
            rt.sizeDelta        = new Vector2(120f, UIScale.RowMd);   // 상자보다 넓게 — 글이 잘리지 않게
        }
    }

    // ── 장비 (열린 뒤) ───────────────────────────────────────

    static void BuildGear(GameObject stage, float yFromTop,
                          out GameObject gearRoot, out Image frame, out Image icon,
                          out TextMeshProUGUI name, out TextMeshProUGUI grade,
                          out TextMeshProUGUI stat, out TextMeshProUGUI desc)
    {
        gearRoot = EditorUIBuilder.Go("GearRoot", stage);
        {
            // ⚠ 튀어나올 때 localScale 을 만지므로 **가운데 피벗**이다
            //   위 피벗이면 카드가 커질 때 아래로만 자라 '튀어나온' 것으로 안 읽힌다.
            var rt = gearRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -(yFromTop + GearBlockH * 0.5f));
            rt.sizeDelta        = new Vector2(-Pad * 2f, GearBlockH);
        }

        // 아이콘 — 등급색 테두리 안에.
        frame = EditorUIBuilder.Img(gearRoot, "Frame", Color.white);
        frame.raycastTarget = false;
        {
            var rt = frame.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(IconSize + 8f, IconSize + 8f);
        }

        var fill = EditorUIBuilder.Img(frame.gameObject, "Fill", SlotBg);
        EditorUIBuilder.Stretch(fill.gameObject);
        fill.rectTransform.offsetMin = new Vector2(4f, 4f);
        fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
        fill.raycastTarget = false;

        icon = EditorUIBuilder.Img(fill.gameObject, "Icon", Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        EditorUIBuilder.Stretch(icon.gameObject);

        float y = IconSize + 8f + 18f;

        name = EditorUIBuilder.TMP(gearRoot, "Name", "장비 이름", UIScale.FontLg, FontStyles.Bold);
        name.alignment     = TextAlignmentOptions.Center;
        name.raycastTarget = false;
        EditorUIBuilder.AnchorTop(name.rectTransform, y, UIScale.RowLg, 0f);
        y += UIScale.RowLg + 4f;

        grade = EditorUIBuilder.TMP(gearRoot, "Grade", "", UIScale.FontSm, FontStyles.Bold);
        grade.alignment     = TextAlignmentOptions.Center;
        grade.raycastTarget = false;
        EditorUIBuilder.AnchorTop(grade.rectTransform, y, UIScale.RowSm, 0f);
        y += UIScale.RowSm + 12f;

        stat = EditorUIBuilder.TMP(gearRoot, "Stat", "", UIScale.FontMd, FontStyles.Bold);
        stat.color         = new Color(0.55f, 0.90f, 0.65f);   // 스탯 = 초록. 툴팁과 같은 색이다
        stat.alignment     = TextAlignmentOptions.Center;
        stat.raycastTarget = false;
        EditorUIBuilder.AnchorTop(stat.rectTransform, y, UIScale.RowMd, 0f);
        y += UIScale.RowMd + 8f;

        desc = EditorUIBuilder.TMP(gearRoot, "Desc", "", UIScale.FontSm, FontStyles.Normal);
        desc.color         = SubText;
        desc.alignment     = TextAlignmentOptions.Top;
        desc.raycastTarget = false;
        // 긴 설명은 줄여서 담는다 — 넘치면 확인 버튼을 덮는다 (UI 규칙 5)
        desc.enableAutoSizing = true;
        desc.fontSizeMax      = UIScale.FontSm;
        desc.fontSizeMin      = UIScale.FontSm * 0.78f;
        EditorUIBuilder.AnchorTop(desc.rectTransform, y, UIScale.RowSm * 2f, 0f);

        gearRoot.SetActive(false);
    }

    // ── 닫기 ─────────────────────────────────────────────────

    static void BuildClose(GameObject panel, out Button btn, out TextMeshProUGUI label)
    {
        // ⚠ 누를 수 있는 것이므로 음각이다 (UI 규칙 1)
        btn = EditorUIBuilder.RaisedTextBtn(panel, "CloseBtn", "확인",
                                            UIScale.FontMd, new Color(0.24f, 0.40f, 0.74f));
        {
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, Pad);
            rt.sizeDelta        = new Vector2(320f, CloseH);
        }

        label = btn.GetComponentInChildren<TextMeshProUGUI>();

        // 상자를 열기 전에는 없다 — 열지 않고 닫으면 무엇을 받았는지 모른다.
        btn.gameObject.SetActive(false);
    }
}
