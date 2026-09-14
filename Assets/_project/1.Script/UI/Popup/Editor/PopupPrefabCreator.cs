using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  PopupPrefabCreator.cs  [Editor Only]
//  Tools > Project K > 프리팹 생성 > 팝업 > Pause · Loading
//
//  ■ 2026-09-11 정리 — 이 게임에서 뜨는 두 팝업만 남겼다
//    원작 묶음(▶ 팝업 전체 · BattleResult · ExpRow · 어빌리티 선택/목록)은
//    이 게임에서 열리지 않아 지웠다. 런 종료 결산은 Reincarnation, 스테이지
//    클리어는 카드 3택(CardSelect)이 맡는다.
//  크기·폰트는 UIScale 상수를 참조한다.
// ============================================================

public static class PopupPrefabCreator
{
    const string SavePath = "Assets/_project/2.Prefabs/UI";

    static void StretchRT(GameObject go) => EditorUIBuilder.Stretch(go);

    // ── PausePopup ────────────────────────────────────────────

    //  로비 팝업(EventPopup·HeroDetail)과 같은 톤으로 맞췄다:
    //    전체화면 오버레이 → 테두리(패널 앞 형제) → 패널 → ◆ 태그 헤더 + 강조선 → 입체 버튼.
    //  ⚠ 이 팝업만 인게임 캔버스(1080×1920 세로) 위에 뜬다 — 가로 여유가 1080 뿐이다.
    // ⚠ 이 프로젝트에서 실제로 뜬다 — 올드Tools 가 아니다 (2026-08-28 이관)
    //   인게임 상단바(TopBarUI)가 연다.
    //   진영 개념이 없는 화면이라 원작 산출물이 그대로 맞는다.
    [MenuItem(ProjectKMenu.Popup + "Pause", priority = ProjectKMenu.PrefabPrio + 32)]
    public static void CreatePausePopup()
    {
        const float PW      = 840f;
        const float HeaderH = 136f;
        const float SidePad =  48f;
        const float BtnGap  =  24f;
        const float Outset  =   6f;   // 테두리가 패널 밖으로 드러나는 두께 (PausePopup 과 동일)

        float btnH = UIScale.BtnFor(UIScale.FontMd) + 20f;   // 92 — 인게임은 손가락으로 누른다
        // 사운드 토글은 설명 줄이 없다 — 두 줄짜리 선택지보다 낮게 잡아 목록을 압축한다.
        float togH = UIScale.BtnFor(UIScale.FontMd);

        // 행 순서: 계속하기 → 효과음 → 배경음악 → 즉시 환생하기
        //   되돌릴 수 없는 항목을 맨 아래에 둔다. 마침 로비에서 접는 행도 이것이라
        //   접었을 때 목록 중간에 구멍이 나지 않는다.
        float yResume = HeaderH + 43f;
        float ySfx    = yResume + btnH + BtnGap;
        float yBgm    = ySfx    + togH + BtnGap;
        float yReinc  = yBgm    + togH + BtnGap;

        float surrenderRowH = BtnGap + btnH;                  // 로비에서 접는 높이
        float popupH        = yReinc + btnH + 48f;

        // 루트는 전체화면 오버레이 — 뒤 전투 화면을 어둡게 깐다
        var root = new GameObject("PausePopup", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        StretchRT(root);
        var popup = root.AddComponent<PausePopup>();

        // 테두리는 Panel 의 **앞 형제** — 자식으로 두면 팝업 전체를 덮는다 (UI 규칙 3)
        var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(root.transform, false);
        border.GetComponent<Image>().color = new Color(0.26f, 0.44f, 0.72f, 1f);
        SetRect(border.GetComponent<RectTransform>(), Vector2.zero, new Vector2(PW + Outset, popupH + Outset));

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        panel.GetComponent<Image>().color = new Color(0.07f, 0.075f, 0.13f, 1f);
        SetRect(panel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(PW, popupH));

        // ── 헤더 ──────────────────────────────────────────────
        var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(panel.transform, false);
        header.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.18f, 1f);
        EditorUIBuilder.AnchorTop(header.GetComponent<RectTransform>(), 0f, HeaderH);

        // ★ 는 폰트에 없다 (□ 로 렌더됨) → 마름모 도형 (UI 규칙 2)
        var tagRoot = EditorUIBuilder.Go("PauseTag", header);
        var tagRt = tagRoot.GetComponent<RectTransform>();
        tagRt.anchorMin = tagRt.anchorMax = new Vector2(0f, 1f);
        tagRt.pivot     = new Vector2(0f, 1f);
        tagRt.anchoredPosition = new Vector2(30f, -14f);
        tagRt.sizeDelta        = new Vector2(300f, 34f);

        var diamond = EditorUIBuilder.Diamond(tagRoot, "Mark", 16f, new Color(0.62f, 0.82f, 1.00f));
        var dRt = diamond.GetComponent<RectTransform>();
        dRt.anchorMin = dRt.anchorMax = new Vector2(0f, 0.5f);
        dRt.anchoredPosition = new Vector2(10f, 0f);

        var tagTmp = AddTMP(tagRoot, "Label", "전 투", UIScale.FontSm, FontStyles.Bold);
        tagTmp.color         = new Color(0.62f, 0.82f, 1.00f);
        tagTmp.alignment     = TextAlignmentOptions.Left;
        tagTmp.raycastTarget = false;
        var tlRt = tagTmp.rectTransform;
        tlRt.anchorMin = Vector2.zero; tlRt.anchorMax = Vector2.one;
        tlRt.offsetMin = new Vector2(30f, 0f); tlRt.offsetMax = Vector2.zero;

        // 타이틀 — 그림자 사본을 먼저 깔아 어떤 배경에서도 읽히게 한다
        MakePauseTitle(header, "TitleShadow", new Color(0.02f, 0.03f, 0.06f, 0.85f), 3f);
        MakePauseTitle(header, "TitleText",   new Color(1.00f, 0.94f, 0.78f, 1f),    0f);

        var accent = new GameObject("AccentLine", typeof(RectTransform), typeof(Image));
        accent.transform.SetParent(panel.transform, false);
        accent.GetComponent<Image>().color = new Color(0.40f, 0.72f, 1.00f, 1f);
        EditorUIBuilder.AnchorTop(accent.GetComponent<RectTransform>(), HeaderH, 3f);

        // ── 선택지 ────────────────────────────────────────────
        //  "즉시 환생하기" 는 되돌릴 수 없다 — 붉은 계열로 구분한다.
        var resumeBtn = MakePauseChoice(panel, "ResumeButton", "계 속 하 기",
                                        "전투로 돌아간다",
                                        new Color(0.13f, 0.52f, 0.38f, 1f),
                                        yResume, btnH, SidePad);

        var sfxBtn = MakeSoundToggle(panel, "SfxButton", "효 과 음",
                                     ySfx, togH, SidePad,
                                     out var sfxPill, out var sfxState);

        var bgmBtn = MakeSoundToggle(panel, "BgmButton", "배 경 음 악",
                                     yBgm, togH, SidePad,
                                     out var bgmPill, out var bgmState);

        var reincBtn  = MakePauseChoice(panel, "ReincarnateButton", "즉시 환생하기",
                                        "이번 런을 포기하고 환생한다",
                                        new Color(0.50f, 0.16f, 0.18f, 1f),
                                        yReinc, btnH, SidePad);

        var so = new SerializedObject(popup);
        SetEnum(so, "_popupType",          (int)PopupType.Pause);
        SetObj (so, "_resumeButton",       resumeBtn);
        SetObj (so, "_reincarnateButton",  reincBtn);
        SetObj (so, "_sfxButton",          sfxBtn);
        SetObj (so, "_sfxPill",            sfxPill);
        SetObj (so, "_sfxState",           sfxState);
        SetObj (so, "_bgmButton",          bgmBtn);
        SetObj (so, "_bgmPill",            bgmPill);
        SetObj (so, "_bgmState",           bgmState);
        SetObj (so, "_panelRect",          panel.GetComponent<RectTransform>());
        SetObj (so, "_borderRect",         border.GetComponent<RectTransform>());
        so.FindProperty("_panelFullH").floatValue    = popupH;
        so.FindProperty("_surrenderRowH").floatValue = surrenderRowH;
        so.ApplyModifiedProperties();

        Save(root, "PausePopup");
    }

    /// <summary>
    /// [라벨] ────── [상태 알약] 한 줄짜리 사운드 토글 버튼.
    ///
    /// ⚠ 상태를 버튼 본체 색으로 나타내지 않는다
    ///   Body 는 Button.targetGraphic 이라 눌림 색이 그 색에 곱해진다 (UI 규칙 1).
    ///   런타임에 Body 를 물들이면 TintFor 로 역산해 둔 눌림 색이 어긋나므로,
    ///   상태는 그 위에 얹은 알약(Image + TMP)이 맡는다.
    /// </summary>
    static Button MakeSoundToggle(GameObject panel, string name, string label,
                                  float yFromTop, float h, float sidePad,
                                  out Image pill, out TextMeshProUGUI state)
    {
        var btn = EditorUIBuilder.RaisedBtn(panel, name, new Color(0.19f, 0.24f, 0.38f, 1f), out var body);
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -yFromTop);
        rt.sizeDelta        = new Vector2(-sidePad * 2f, h);

        const float PillW = 170f;

        var lbl = AddTMP(body, "Label", label, UIScale.FontMd, FontStyles.Bold);
        lbl.color            = Color.white;
        lbl.alignment        = TextAlignmentOptions.MidlineLeft;
        lbl.raycastTarget    = false;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        var lRt = lbl.rectTransform;
        lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
        lRt.offsetMin = new Vector2(28f, 0f);
        lRt.offsetMax = new Vector2(-(PillW + 40f), 0f);

        // 알약 — 우측. 높이는 UIScale.RowSm (글자가 잘리지 않는 최소 한 줄, UI 규칙 5)
        pill = EditorUIBuilder.Img(body, "StatePill", new Color(0.16f, 0.50f, 0.34f, 1f));
        var pRt = pill.rectTransform;
        pRt.anchorMin = pRt.anchorMax = new Vector2(1f, 0.5f);
        pRt.pivot     = new Vector2(1f, 0.5f);
        pRt.anchoredPosition = new Vector2(-28f, 0f);
        pRt.sizeDelta        = new Vector2(PillW, UIScale.RowSm);

        state = AddTMP(pill.gameObject, "State", "켜짐", UIScale.FontSm, FontStyles.Bold);
        state.color            = Color.white;
        state.alignment        = TextAlignmentOptions.Center;
        state.raycastTarget    = false;
        state.textWrappingMode = TextWrappingModes.NoWrap;
        StretchRT(state.gameObject);

        return btn;
    }

    static void MakePauseTitle(GameObject header, string name, Color color, float dy)
    {
        var tmp = AddTMP(header, name, "일시 정지", UIScale.FontLg, FontStyles.Bold);
        tmp.color            = color;
        tmp.alignment        = TextAlignmentOptions.Left;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode     = TextOverflowModes.Overflow;
        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(30f + dy, -52f - dy);
        rt.sizeDelta        = new Vector2(700f, UIScale.RowLg);
    }

    /// <summary>[라벨(FontMd)] 위 / [설명(FontSm)] 아래 2줄짜리 입체 선택 버튼.</summary>
    static Button MakePauseChoice(GameObject panel, string name, string label, string hint,
                                  Color face, float yFromTop, float h, float sidePad)
    {
        // UI 규칙 1 — 누를 수 있는 버튼은 음각. 내용은 반드시 body 아래.
        var btn = EditorUIBuilder.RaisedBtn(panel, name, face, out var body);
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(sidePad,  0f);
        rt.offsetMax = new Vector2(-sidePad, 0f);
        rt.anchoredPosition = new Vector2(0f, -yFromTop);
        rt.sizeDelta        = new Vector2(-sidePad * 2f, h);

        var lbl = AddTMP(body, "Label", label, UIScale.FontMd, FontStyles.Bold);
        lbl.color            = Color.white;
        lbl.alignment        = TextAlignmentOptions.Center;
        lbl.raycastTarget    = false;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        var lRt = lbl.rectTransform;
        lRt.anchorMin = new Vector2(0f, 0.5f); lRt.anchorMax = new Vector2(1f, 1f);
        lRt.offsetMin = new Vector2(16f, 0f);  lRt.offsetMax = new Vector2(-16f, -6f);

        var hintTmp = AddTMP(body, "Hint", hint, UIScale.FontSm, FontStyles.Normal);
        hintTmp.color            = new Color(1f, 1f, 1f, 0.72f);
        hintTmp.alignment        = TextAlignmentOptions.Center;
        hintTmp.raycastTarget    = false;
        hintTmp.textWrappingMode = TextWrappingModes.NoWrap;
        var hRt = hintTmp.rectTransform;
        hRt.anchorMin = new Vector2(0f, 0f);   hRt.anchorMax = new Vector2(1f, 0.5f);
        hRt.offsetMin = new Vector2(16f, 8f);  hRt.offsetMax = new Vector2(-16f, 0f);

        return btn;
    }

    // ── LoadingPopup ──────────────────────────────────────────

    // ⚠ 이 프로젝트에서 실제로 뜬다 — 올드Tools 가 아니다 (2026-08-28 이관)
    //   스플래시(SplashBootstrap) · 로비↔인게임 전환(LobbyManager) · 인게임
    //   준비(InGameManager)가 PopupType.Loading 으로 연다. 글자 한 줄짜리
    //   가림막이라 진영과 무관하다.
    [MenuItem(ProjectKMenu.Popup + "Loading", priority = ProjectKMenu.PrefabPrio + 33)]
    public static void CreateLoadingPopup()
    {
        var root = new GameObject("LoadingPopup", typeof(RectTransform));
        root.AddComponent<CanvasGroup>();
        var popup = root.AddComponent<LoadingPopup>();

        // 전체 화면 스트레치
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        AddBgPanel(root, new Color(0.05f, 0.05f, 0.08f, 1f));

        var titleText  = AddTMP(root, "TitleText",  "배틀 준비 중",    UIScale.FontLg, FontStyles.Bold);
        var statusText = AddTMP(root, "StatusText", "장군 소환 중...", UIScale.FontMd, FontStyles.Normal);
        statusText.color = new Color(0.75f, 0.75f, 0.75f);

        SetRect(titleText .rectTransform, new Vector2(0,  50), new Vector2(800, 80));
        SetRect(statusText.rectTransform, new Vector2(0, -50), new Vector2(700, 60));

        var so = new SerializedObject(popup);
        SetEnum(so, "_popupType",   (int)PopupType.Loading);
        SetObj (so, "_titleText",   titleText);
        SetObj (so, "_statusText",  statusText);
        so.ApplyModifiedProperties();

        Save(root, "LoadingPopup");
    }

    // ── 헬퍼 ─────────────────────────────────────────────────

    static void AddBgPanel(GameObject parent, Color color)
        => EditorUIBuilder.BgPanel(parent, color);

    static TextMeshProUGUI AddTMP(GameObject parent, string name, string text, float size, FontStyles style)
        => EditorUIBuilder.TMP(parent, name, text, size, style);

    static void SetRect(RectTransform rt, Vector2 pos, Vector2 size)
        => EditorUIBuilder.Center(rt, pos, size);

    static void SetEnum(SerializedObject so, string field, int value)
        => EditorUIBuilder.SetEnum(so, field, value, "PopupPrefabCreator");

    static void SetObj(SerializedObject so, string field, Object obj)
        => EditorUIBuilder.SetObj(so, field, obj, "PopupPrefabCreator");

    static void Save(GameObject root, string fileName)
    {
        string path = $"{SavePath}/{fileName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log($"[PopupPrefabCreator] 저장: {path}");
    }
}
