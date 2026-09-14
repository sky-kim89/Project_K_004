using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  ReincarnationPopupCreator.cs
//  Tools > Project K > 프리팹 생성 > 팝업 > Reincarnation
//
//  BattleResultPopup 과 같은 시각 언어로 맞췄다.
//    · 헤더 밴드 + 다이아 배지 + 그림자 깔린 타이틀
//    · 강조선 → 섹션 라벨(좌우 라인) → 내용 카드
//    · 탭·환생 버튼은 음각 입체 버튼 (UI 규칙 1)
//
//  옛 레이아웃은 1080 세로 기준으로 좌표를 박아 놨는데 루트는
//  PopupMaxH(1000) 였다 — 타이틀은 위로 28px 잘리고 환생 버튼은
//  아래로 30px 삐져나갔다. 전부 상단 밴드 기준으로 다시 잡았다.
//
//  레이아웃 (1240 × 1000 / 위에서 아래로)
//    Header        Y=  0  H=200  배지 + "패  배" + 웨이브·처치(좌)/총피해·DPS(우)
//    AccentLine    Y=200  H=  3
//    Section       Y=218  H= 43  "전투 기록"
//    TabBar        Y=266  H= 92  딜 / 탱 / 힐
//    GeneralBox    Y=368  → 포인트 패널 위까지 (세로 스크롤)
//    PointsPanel   하단 132  H= 64
//    ReincarnateBtn 하단 22  400×BtnSm
// ============================================================

public static class ReincarnationPopupCreator
{
    const string SavePath = "Assets/_project/2.Prefabs/UI";

    // 환생 팝업 고유 강조색 — 구조색은 EditorUIBuilder.Pop 공용을 쓴다.
    static readonly Color RcRuin    = new Color(0.88f, 0.34f, 0.36f, 1f);  // 패배 = 붉은 계열
    static readonly Color RcGain    = new Color(0.98f, 0.82f, 0.28f, 1f);  // 획득 포인트
    static readonly Color RcAfter   = new Color(0.55f, 0.90f, 0.62f, 1f);  // 환생 후 합계
    static readonly Color RcButton  = new Color(0.46f, 0.20f, 0.72f, 1f);  // 환생 버튼

    public static void CreateGeneralStatRowPrefab()
    {
        const float rowH   = 92f;
        const float portSz = 72f;
        const float nameX  = 90f;   // portrait 우측 끝(78) + 12 gap
        // 좌측 고정 영역 끝 (portrait + 이름)
        const float leftEnd   = 268f;
        // 우측 고정 영역 — 위 총량 / 아래 DPS 가 세로로 쌓인다.
        // ⚠ 110 으로는 "DPS 4,154" 가 안 들어가 막대 위로 삐져나왔다.
        //   가장 긴 문자열을 기준으로 잡는다.
        // ⚠ 186 도 모자랐다 — "DPS 4,154" 가 FontSm 에서 167px 이라
        //   여백(24)을 빼면 162px 뿐이라 **두 줄로 접혔다**. 폭을 늘리고
        //   아래에서 줄바꿈 자체를 끈다(둘 다 해야 한다 — 폭만 늘리면
        //   자릿수가 하나 더 늘었을 때 같은 일이 다시 난다).
        const float rightSize = 214f;
        float stretchX = (leftEnd - rightSize) / 2f;

        var root    = new GameObject("GeneralStatRow", typeof(RectTransform));
        var rowComp = root.AddComponent<GeneralStatRowUI>();
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(860f, rowH);
        root.AddComponent<Image>().color = new Color(0.11f, 0.12f, 0.19f, 0.90f);

        // ── 초상화 ──────────────────────────────────────────────
        var portBg = new GameObject("PortraitBg", typeof(RectTransform), typeof(Image));
        portBg.transform.SetParent(root.transform, false);
        portBg.GetComponent<Image>().color = new Color(0.20f, 0.22f, 0.35f, 1f);
        var portRt = portBg.GetComponent<RectTransform>();
        portRt.anchorMin = new Vector2(0f, 0.5f); portRt.anchorMax = new Vector2(0f, 0.5f);
        portRt.pivot = new Vector2(0f, 0.5f);
        portRt.anchoredPosition = new Vector2(6f, 0f);
        portRt.sizeDelta = new Vector2(portSz, portSz);

        var portImg = new GameObject("PortraitImage", typeof(RectTransform), typeof(Image));
        portImg.transform.SetParent(portBg.transform, false);
        portImg.GetComponent<Image>().color = Color.white;
        StretchRT(portImg);

        var bridgeGo = new GameObject("PortraitBridge", typeof(RectTransform));
        bridgeGo.transform.SetParent(portBg.transform, false);
        bridgeGo.SetActive(false);
        bridgeGo.AddComponent<UnitAppearanceBridge>();
        bridgeGo.AddComponent<Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts.CharacterBuilder>();

        // ── 이름 텍스트 ─────────────────────────────────────────
        var nameText = AddTMP(root, "NameText", "장수 이름", UIScale.FontMd, FontStyles.Bold);
        nameText.alignment    = TextAlignmentOptions.MidlineLeft;
        // 칸 높이(44) < FontMd 한 줄(53) 이라 Ellipsis 면 줄이 통째로 사라진다.
        // Overflow + AutoSize — 긴 이름은 폰트를 줄여 대응한다.
        nameText.overflowMode     = TextOverflowModes.Overflow;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin      = UIScale.FontSm;
        nameText.fontSizeMax      = UIScale.FontMd;
        var nameRt = nameText.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 0.5f); nameRt.anchorMax = new Vector2(0f, 0.5f);
        nameRt.pivot = new Vector2(0f, 0.5f);
        nameRt.anchoredPosition = new Vector2(nameX, 10f);
        // 좌측 열 안에서 끝나야 한다 — AutoSize 라 넘치면 폰트가 줄어들 뿐,
        // 폭을 안 주면 긴 이름이 막대 위로 흘러간다.
        nameRt.sizeDelta = new Vector2(leftEnd - nameX - 12f, 44f);

        // ── StatBar (수평 stretch) ──────────────────────────────
        var barGo = new GameObject("StatBar", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(root.transform, false);
        barGo.GetComponent<Image>().color = new Color(0.18f, 0.20f, 0.32f, 1f);
        var barRt = barGo.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 0.5f); barRt.anchorMax = new Vector2(1f, 0.5f);
        barRt.pivot = new Vector2(0.5f, 0.5f);
        barRt.anchoredPosition = new Vector2(stretchX, 18f);
        barRt.sizeDelta = new Vector2(-(leftEnd + rightSize), 22f);

        var statBarComp = barGo.AddComponent<StatBarUI>();
        var segs = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var seg = new GameObject($"Seg{i}", typeof(RectTransform), typeof(Image));
            seg.transform.SetParent(barGo.transform, false);
            var segRt = seg.GetComponent<RectTransform>();
            segRt.anchorMin = new Vector2(0f, 0f); segRt.anchorMax = new Vector2(0f, 1f);
            segRt.offsetMin = Vector2.zero; segRt.offsetMax = Vector2.zero;
            segs[i] = seg.GetComponent<Image>();
            segs[i].color = Color.clear;
            seg.SetActive(false);
        }
        var sbSo = new SerializedObject(statBarComp);
        sbSo.FindProperty("_barBg").objectReferenceValue = barGo.GetComponent<RectTransform>();
        var segsProp = sbSo.FindProperty("_segments");
        segsProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
            segsProp.GetArrayElementAtIndex(i).objectReferenceValue = segs[i];
        sbSo.ApplyModifiedProperties();

        // ── 범례 텍스트 ─────────────────────────────────────────
        var legendText = AddTMP(root, "LegendText",
            "<color=#4D8CF2>■</color> 장군  <color=#59CC74>■</color> 병사  <color=#F28C33>■</color> 스킬",
            30f, FontStyles.Normal);
        legendText.alignment = TextAlignmentOptions.MidlineLeft;
        legendText.color     = new Color(0.60f, 0.62f, 0.72f);
        var legendRt = legendText.rectTransform;
        legendRt.anchorMin = new Vector2(0f, 0.5f); legendRt.anchorMax = new Vector2(1f, 0.5f);
        legendRt.pivot = new Vector2(0.5f, 0.5f);
        legendRt.anchoredPosition = new Vector2(stretchX, -14f);
        legendRt.sizeDelta = new Vector2(-(leftEnd + rightSize), 30f);

        // ── TotalText (우측 고정) ───────────────────────────────
        var totalText = AddTMP(root, "TotalText", "", UIScale.FontSm, FontStyles.Normal);
        totalText.alignment        = TextAlignmentOptions.MidlineRight;
        totalText.color            = new Color(0.75f, 0.80f, 1.00f, 1f);
        totalText.textWrappingMode = TextWrappingModes.NoWrap;
        totalText.overflowMode     = TextOverflowModes.Overflow;
        var totalRt = totalText.rectTransform;
        totalRt.anchorMin = new Vector2(1f, 0.5f); totalRt.anchorMax = new Vector2(1f, 0.5f);
        totalRt.pivot = new Vector2(1f, 0.5f);
        totalRt.anchoredPosition = new Vector2(-14f, 10f);
        totalRt.sizeDelta = new Vector2(rightSize - 24f, 26f);

        // ── DPS 텍스트 (TotalText 아래, 우측 고정) ──────────────
        var dpsText = AddTMP(root, "DPSText", "", UIScale.FontSm, FontStyles.Normal);
        dpsText.alignment = TextAlignmentOptions.MidlineRight;
        dpsText.color     = new Color(0.55f, 0.65f, 0.85f);
        // ⚠ "DPS 4,154" 는 한 덩어리다 — 접히면 "DPS" 와 숫자가 위아래로 갈린다
        dpsText.textWrappingMode = TextWrappingModes.NoWrap;
        dpsText.overflowMode     = TextOverflowModes.Overflow;
        var dpsRt = dpsText.rectTransform;
        dpsRt.anchorMin = new Vector2(1f, 0.5f); dpsRt.anchorMax = new Vector2(1f, 0.5f);
        dpsRt.pivot = new Vector2(1f, 0.5f);
        dpsRt.anchoredPosition = new Vector2(-14f, -16f);
        dpsRt.sizeDelta = new Vector2(rightSize - 24f, 26f);

        // ── 필드 연결 ────────────────────────────────────────────
        var so = new SerializedObject(rowComp);
        SetObj(so, "_portraitBg",     portBg.GetComponent<Image>());
        SetObj(so, "_portraitImage",  portImg.GetComponent<Image>());
        SetObj(so, "_portraitBridge", bridgeGo.GetComponent<UnitAppearanceBridge>());
        SetObj(so, "_nameText",       nameText);
        SetObj(so, "_statBar",        statBarComp);
        SetObj(so, "_totalText",      totalText);
        SetObj(so, "_legendText",     legendText);
        SetObj(so, "_dpsText",        dpsText);
        so.ApplyModifiedProperties();

        Save(root, "GeneralStatRow");
    }

    // ── ReincarnationPopup ────────────────────────────────────

    // ⚠ 이 프로젝트의 **런 종료 화면**이다 — 올드Tools 가 아니다 (2026-08-28 이관)
    //   RunBootstrap.HandleDefeat 가 이 팝업을 연다. 전용 패배 화면을 따로
    //   만들지 않고 이쪽을 쓴다 — 딜/탱/힐 탭·세그먼트 바·환생 포인트가
    //   이미 갖춰져 있고, 초기화 목록도 UserDataManager.Reincarnate() 가 소유한다.
    //
    //   함께 굽는 GeneralStatRow.prefab 은 **전투 통계 팝업도 쓴다**
    //   (RunPopupCreator.CreateBattleStats). 둘이 같은 행을 공유한다.
    [MenuItem(ProjectKMenu.Popup + "Reincarnation (+ GeneralStatRow)", priority = ProjectKMenu.PrefabPrio + 52)]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>($"{SavePath}/GeneralStatRow.prefab") == null)
            CreateGeneralStatRowPrefab();

        // ⚠ BattleResultPopup 과 같은 치수를 쓴다
        //   승리/패배는 같은 자리에 번갈아 뜨는 한 쌍이다. 크기가 다르면
        //   전투가 끝날 때마다 창이 커졌다 작아졌다 한다.
        const float PW       = 1240f;
        const float PH       = UIScale.PopupMaxH;
        const float SidePad  = 40f;
        const float ContentW = PW - SidePad * 2f;

        const float HeaderH     = 200f;
        const float AccentH     = 3f;
        const float StatY       = 218f;
        const float TabY        = 266f;
        const float TabH        = 92f;
        const float ListY       = 368f;

        // 하단 고정 스택 — 버튼 → 포인트 패널 → 목록 바닥 순으로 쌓아 올린다
        // ⚠ 버튼 크기를 줄인 만큼 위 목록이 통째로 내려온다
        //   예전엔 600×132 버튼이 하단을 먹어 장수 목록이 몇 줄 못 보였다.
        const float BtnW       = 400f;
        const float BtnH       = UIScale.BtnSm;
        const float BtnBottom  = 22f;
        const float PtsH       = 64f;
        const float PtsBottom  = BtnBottom + BtnH + 10f;             // 132
        const float ListBottom = PtsBottom + PtsH + 12f;             // 208

        var root  = CreateRoot<ReincarnationPopup>("ReincarnationPopup", PW, PH);
        var popup = root.GetComponent<ReincarnationPopup>();

        // 테두리는 패널의 "앞 형제" 로 둔다 (UI 규칙 3 — 자식으로 두면 위에 덮인다)
        var border = MakeGo("Border", root);
        border.AddComponent<Image>().color = EditorUIBuilder.Pop.PanelBorder;
        StretchWith(border, -3f);

        var panel = MakeGo("Panel", root);
        panel.AddComponent<Image>().color = EditorUIBuilder.Pop.PanelBg;
        StretchWith(panel, 0f);

        // ── 헤더 밴드 ────────────────────────────────────────
        var header = MakeGo("Header", panel);
        header.AddComponent<Image>().color = EditorUIBuilder.Pop.HeaderBg;
        AnchorTopBand(header, 0f, HeaderH);

        var badge = EditorUIBuilder.Diamond(header, "Badge", 30f, RcRuin);
        {
            var rt = badge.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -30f);
        }

        // 타이틀 — 그림자 사본을 먼저 깔고 그 위에 본문
        // 헤더 중심(=위에서 100) 기준 오프셋으로 잡는다. 타이틀 줄 45~140 → 중심 92.5
        var titleShadow = AddTMP(header, "TitleShadow", "패  배", UIScale.FontXl, FontStyles.Bold);
        titleShadow.color         = EditorUIBuilder.Pop.TitleShadow;
        titleShadow.raycastTarget = false;
        SetRect(titleShadow.rectTransform, new Vector2(3f, 4.5f), new Vector2(ContentW, 95f));

        var titleText = AddTMP(header, "TitleText", "패  배", UIScale.FontXl, FontStyles.Bold);
        titleText.color         = RcRuin;
        titleText.raycastTarget = false;
        SetRect(titleText.rectTransform, new Vector2(0f, 7.5f), new Vector2(ContentW, 95f));

        // 웨이브·처치(좌) / 총 피해·DPS(우) — 같은 줄 양끝. 줄 148~191 → 중심 169.5
        var subText = AddTMP(header, "SubText", "도달 스테이지 0  ·  웨이브 0 / 0  ·  처치 0명",
                             UIScale.FontSm, FontStyles.Normal);
        subText.alignment     = TextAlignmentOptions.MidlineLeft;
        subText.color         = EditorUIBuilder.Pop.SubText;
        subText.raycastTarget = false;
        // 우측 StatsText 와 같은 줄을 나눠 쓴다 — 접히면 아래로 흘러 겹친다
        subText.textWrappingMode = TextWrappingModes.NoWrap;
        subText.overflowMode     = TextOverflowModes.Overflow;
        SetRect(subText.rectTransform, new Vector2(0f, -69.5f), new Vector2(ContentW, UIScale.RowSm));

        var statsText = AddTMP(header, "StatsText", "총 피해  0  |  DPS  0", UIScale.FontSm, FontStyles.Bold);
        statsText.alignment        = TextAlignmentOptions.MidlineRight;
        statsText.color            = new Color(0.85f, 0.88f, 1f);
        statsText.raycastTarget    = false;
        statsText.textWrappingMode = TextWrappingModes.NoWrap;
        statsText.overflowMode     = TextOverflowModes.Overflow;
        SetRect(statsText.rectTransform, new Vector2(0f, -69.5f), new Vector2(ContentW, UIScale.RowSm));

        // 헤더 아래 강조선
        var accentLine = MakeGo("AccentLine", panel);
        var accentImg  = accentLine.AddComponent<Image>();
        accentImg.color         = RcRuin;
        accentImg.raycastTarget = false;
        AnchorTopBand(accentLine, HeaderH, AccentH);

        // ── "전투 기록" 섹션 ─────────────────────────────────
        EditorUIBuilder.SectionLabel(panel, "전투 기록", StatY, ContentW, SidePad);

        var tabBar = MakeGo("TabBar", panel);
        // 패널 폭 전체로 늘리면 탭 한 칸이 500 을 넘는다 — 가운데 900 으로 묶는다
        // (BattleResultPopup 과 같은 규칙)
        AnchorTopBand(tabBar, TabY, TabH, (PW - 900f) * 0.5f);

        var tabHlg = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabHlg.spacing        = 10f;
        tabHlg.childAlignment = TextAnchor.MiddleCenter;
        tabHlg.childControlWidth  = true; tabHlg.childControlHeight  = true;
        tabHlg.childForceExpandWidth = true; tabHlg.childForceExpandHeight = true;

        string[] tabLabels = { "딜", "탱", "힐" };
        var tabButtons   = new Button[3];
        var tabButtonBgs = new Image[3];

        for (int i = 0; i < 3; i++)
        {
            // UI 규칙 1 — 누를 수 있는 요소는 음각 입체 버튼으로.
            // 세 탭 모두 같은 면 색이고, 활성 표시는 하단 강조바(ActiveBar)로 한다.
            // (Body 색만 런타임에 바꾸면 구워 넣은 모서리 색이 따라오지 않아 어긋난다)
            var tabGo = MakeGo($"TabBtn{i}", tabBar);
            var btn   = EditorUIBuilder.RaisedBtnOn(tabGo, EditorUIBuilder.Pop.TabInactive, out var body);

            var label = AddTMP(body, "Label", tabLabels[i], UIScale.FontMd, FontStyles.Bold);
            label.color         = Color.white;
            label.raycastTarget = false;
            EditorUIBuilder.Stretch(label.gameObject);

            var bar    = MakeGo("ActiveBar", body);
            var barImg = bar.AddComponent<Image>();
            barImg.color         = i == 0 ? EditorUIBuilder.Pop.TabActive : Color.clear;
            barImg.raycastTarget = false;
            {
                var rt = bar.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0.08f, 0f);
                rt.anchorMax        = new Vector2(0.92f, 0f);
                rt.pivot            = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 5f);   // BottomEdge(4px) 바로 위
                rt.sizeDelta        = new Vector2(0f, 5f);
            }

            tabButtons[i]   = btn;
            tabButtonBgs[i] = barImg;
        }

        // ── 장수 통계 목록 (세로 스크롤) ──────────────────────
        var listBox = MakeGo("GeneralBox", panel);
        {
            var rt = listBox.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(SidePad, ListBottom);
            rt.offsetMax = new Vector2(-SidePad, -ListY);
        }
        var listScroll = listBox.AddComponent<ScrollRect>();

        var listVp = MakeGo("Viewport", listBox);
        listVp.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        listVp.AddComponent<Mask>().showMaskGraphic = false;
        var lvpRt = listVp.GetComponent<RectTransform>();
        lvpRt.anchorMin = Vector2.zero; lvpRt.anchorMax = Vector2.one;
        lvpRt.offsetMin = Vector2.zero; lvpRt.offsetMax = Vector2.zero;

        var generalContent = MakeGo("GeneralContent", listVp);
        var gcRt = generalContent.GetComponent<RectTransform>();
        gcRt.anchorMin = new Vector2(0f, 1f); gcRt.anchorMax = new Vector2(1f, 1f);
        gcRt.pivot     = new Vector2(0.5f, 1f);
        gcRt.anchoredPosition = Vector2.zero; gcRt.sizeDelta = Vector2.zero;
        var gcVlg = generalContent.AddComponent<VerticalLayoutGroup>();
        gcVlg.spacing = 6f;
        gcVlg.padding = new RectOffset(0, 0, 0, 0);
        gcVlg.childControlWidth  = true; gcVlg.childControlHeight  = false;
        gcVlg.childForceExpandWidth = true; gcVlg.childForceExpandHeight = false;
        gcVlg.childAlignment = TextAnchor.UpperCenter;
        generalContent.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        listScroll.horizontal   = false;
        listScroll.vertical     = true;
        listScroll.movementType = ScrollRect.MovementType.Elastic;
        listScroll.viewport     = lvpRt;
        listScroll.content      = gcRt;

        // ── 포인트 패널 ───────────────────────────────────────
        var (currentPtsTmp, earnPtsTmp, totalPtsTmp) =
            BuildPointsPanel(panel, ContentW, PtsH, PtsBottom);

        // ── 환생 버튼 ─────────────────────────────────────────
        var reincBtn = EditorUIBuilder.RaisedTextBtn(panel, "ReincarnateButton", "환  생",
            UIScale.FontMd, RcButton);
        {
            var rt = reincBtn.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, BtnBottom);
            rt.sizeDelta        = new Vector2(BtnW, BtnH);
        }

        // ── 장수 Row 프리팹 로드 ─────────────────────────────
        var generalRowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{SavePath}/GeneralStatRow.prefab");

        // ── SerializedObject 연결 ────────────────────────────
        var so = new SerializedObject(popup);
        SetEnum(so, "_popupType",          (int)PopupType.Reincarnation);
        SetObj (so, "_subText",            subText);
        SetObj (so, "_statsText",          statsText);
        SetObj (so, "_generalArea",        generalContent.transform);
        SetObj (so, "_currentPtsText",     currentPtsTmp);
        SetObj (so, "_earnPtsText",        earnPtsTmp);
        SetObj (so, "_totalPtsText",       totalPtsTmp);
        SetObj (so, "_reincarnateBtn",     reincBtn);

        if (generalRowPrefab != null)
            SetObj(so, "_generalRowTemplate", generalRowPrefab.GetComponent<GeneralStatRowUI>());
        else
            Debug.LogWarning("[ReincarnationPopupCreator] GeneralStatRow.prefab 이 없습니다.");

        SetObjArray(so, "_tabButtons",   System.Array.ConvertAll(tabButtons,   b => (Object)b));
        SetObjArray(so, "_tabButtonBgs", System.Array.ConvertAll(tabButtonBgs, b => (Object)b));

        so.ApplyModifiedProperties();

        Save(root, "ReincarnationPopup");
    }

    // ── 포인트 패널 빌드 ──────────────────────────────────────
    //  [보유 12 pt]  ›  [+8 pt]  ›  [환생 후 20 pt]
    //  꺾쇠로 "지금 → 환생 후" 흐름을 읽히게 한다 (▶ 는 폰트에 없다 — UI 규칙 2).

    static (TextMeshProUGUI current, TextMeshProUGUI earn, TextMeshProUGUI total)
        BuildPointsPanel(GameObject parent, float width, float height, float yFromBottom)
    {
        var panel = new GameObject("PointsPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent.transform, false);
        panel.GetComponent<Image>().color = new Color(0.135f, 0.100f, 0.215f, 1f);
        {
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, yFromBottom);
            rt.sizeDelta        = new Vector2(width, height);
        }

        float cellW = width * 0.30f;
        float cellX = width * 0.32f;

        var currentTmp = AddTMP(panel, "CurrentPtsText", "보유  0 pt", UIScale.FontSm, FontStyles.Normal);
        currentTmp.alignment = TextAlignmentOptions.MidlineLeft;
        currentTmp.color     = EditorUIBuilder.Pop.SubText;
        SetRect(currentTmp.rectTransform, new Vector2(-cellX, 0f), new Vector2(cellW, UIScale.RowSm));

        var earnTmp = AddTMP(panel, "EarnPtsText", "+0 pt", UIScale.FontMd, FontStyles.Bold);
        earnTmp.color = RcGain;
        SetRect(earnTmp.rectTransform, Vector2.zero, new Vector2(cellW, UIScale.Line(UIScale.FontMd)));

        var totalTmp = AddTMP(panel, "TotalPtsText", "환생 후  0 pt", UIScale.FontSm, FontStyles.Bold);
        totalTmp.alignment = TextAlignmentOptions.MidlineRight;
        totalTmp.color     = RcAfter;
        SetRect(totalTmp.rectTransform, new Vector2(cellX, 0f), new Vector2(cellW, UIScale.RowSm));

        for (int side = 0; side < 2; side++)
        {
            var chev = EditorUIBuilder.Chevron(panel, side == 0 ? "ChevL" : "ChevR", 22f, 0f,
                new Color(0.42f, 0.40f, 0.55f));
            var rt = chev.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(side == 0 ? -cellW * 0.62f : cellW * 0.62f, 0f);
        }

        return (currentTmp, earnTmp, totalTmp);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────

    static GameObject CreateRoot<T>(string name, float w, float h) where T : PopupBase
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.AddComponent<CanvasGroup>();
        go.AddComponent<T>();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(w, h);
        return go;
    }

    static GameObject MakeGo(string name, GameObject parent)
        => EditorUIBuilder.Go(name, parent);

    static TextMeshProUGUI AddTMP(GameObject parent, string name, string text, float size, FontStyles style)
        => EditorUIBuilder.TMP(parent, name, text, size, style);

    static void SetRect(RectTransform rt, Vector2 pos, Vector2 size)
        => EditorUIBuilder.Center(rt, pos, size);

    static void StretchRT(GameObject go) => EditorUIBuilder.Stretch(go);

    // 상단에서 yFromTop 만큼 내려온 높이 h 의 가로 밴드
    static void AnchorTopBand(GameObject go, float yFromTop, float h, float sidePad = 0f)
        => EditorUIBuilder.AnchorTop(go.GetComponent<RectTransform>(), yFromTop, h, sidePad * 2f);

    static void StretchWith(GameObject go, float outset)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(outset, outset);
        rt.offsetMax = new Vector2(-outset, -outset);
    }

    static void SetEnum(SerializedObject so, string field, int value)
        => EditorUIBuilder.SetEnum(so, field, value, "ReincarnationPopupCreator");

    static void SetObj(SerializedObject so, string field, Object obj)
        => EditorUIBuilder.SetObj(so, field, obj, "ReincarnationPopupCreator");

    static void SetObjArray(SerializedObject so, string field, Object[] objs)
        => EditorUIBuilder.SetObjArray(so, field, objs, "ReincarnationPopupCreator");

    static void Save(GameObject root, string fileName)
    {
        string path = $"{SavePath}/{fileName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ReincarnationPopupCreator] 저장 완료 → {path}");
    }
}
