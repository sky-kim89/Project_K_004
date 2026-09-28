using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  MainPanelCreator.cs
//  Tools > Project K > 프리팹 생성 > 로비 > MainPanel
//
//  ┌ 사이드 380 ┐┌──── 소환사 격자 740 ────┐┌──── 오른쪽 칸 656 ────┐
//  │ [타이틀]    ││ 소 환 사    눌러서 살펴보기 ││ ① 소환사 정보 (836)      │
//  │ [▣ 유물 ]   ││ [카드][카드][카드]        ││                          │
//  │ [▣ 도감 ]   ││ [카드][카드][카드]        ││ [       선택하기       ] │
//  │ [▣ 기타 ]   ││ [카드][카드][카드]        ││ ─ 누르면 ② 로 바뀐다 ─   │
//  │             ││ [카드][카드][카드]        ││ ② 난이도 선택 (836)      │
//  │             ││  236×200 · 3열 × 4줄      ││ [뒤로][    게임 시작   ] │
//  └─────────────┘└──────────────────────────┘└──────────────────────────┘
//
//  ■ 2026-09-11 다시 짰다 (사용자 요청 — "너무 보기 어렵다")
//    · 소환사 한 명을 큰 카드로 띄우고 ◀ ▶ 로 넘기던 것 → **전원을 카드 격자로**.
//      누가 있는지 · 누가 잠겼는지가 한눈에 보인다.
//    · 오른쪽 조작 칸(옛 난이도 자리)에 **소환사 정보**를 정리해 둔다.
//    · [선택하기] 를 누르면 그 자리가 **난이도 선택 칸**으로 바뀐다
//      (‹ › 로 넘기던 것을 다섯 줄로 펼쳤다 — DifficultySelectorUI).
//
//  ⚠ 세로 예산을 눈대중으로 잡지 않는다
//    두 칸은 위에서부터 y 를 쌓아 가며 짓고, 끝에서 VerifyFits 가 칸 높이를
//    넘었는지 잰다. TAF 는 넘쳐도 아무 경고 없이 칸 밖에 그린다.
// ============================================================

public static class MainPanelCreator
{
    const string SavePath     = "Assets/_project/2.Prefabs/UI/Lobby/MainPanel.prefab";
    const string TitleImgPath = "Assets/_project/3.Textures/UI/Lobby/title_pixel_general.png";
    const string TitleKoPath  = "Assets/Resources/Title/title_logo_ko.png";
    const string TitleEnPath  = "Assets/Resources/Title/title_logo_en.png";

    // ── 레이아웃 상수 ─────────────────────────────────────────
    const float SideW     = 380f;
    const float SidePad   =  24f;
    const float SideBtnW  = SideW - SidePad * 2f;    // 332
    const float TitleH    = 300f;                    // 타이틀 PNG 원본 높이와 동일

    public const float CardW = 620f;
    const int   Lp        =  16;    // 카드 내부 좌 여백 (GradeW 포함)
    const int   Rp        =  16;

    const float GradeW    =   6f;

    public const float ArrGap  = 18f;
    const float ArrInset = 10f;

    const float SkRH      =  96f;

    // ── 3칸 배치: [사이드] [소환사 격자] [정보 / 난이도] ──────────
    //
    //  ⚠ 칸 사이 간격은 EdgeGap 하나다 — 제각각 적으면 어긋나 보인다.
    //  ⚠ 두 칸의 세로는 같다 (FrameH). 남는 세로는 위아래로 똑같이 나눈다 —
    //    로비 화면은 이것 하나뿐이라 상단바가 없다.
    //  ⚠ FrameBottomY 는 FrameH 뒤에 선언해야 한다 (static 초기화는 적힌 순서대로 돈다).
    const float EdgeGap = 48f;
    const float FrameH  = 952f;
    static readonly float FrameBottomY = Mathf.Round((UIScale.LobbyCanvasH - FrameH) * 0.5f);   // 64

    // 소환사 격자 — 15칸(3×5)이 한 화면에 선다. 소환사가 늘면 ListRows 를 늘리고
    // FrameH 안에 들어가는지 볼 것 (칸이 모자라면 MainPanelUI 가 에러를 낸다).
    // ⚠ 4 → 5줄 (2026-09-12, 소환사 14명) — 세로 검산: 82 + 5×164 + 4×12 = 950 ≤ FrameH 952
    //   카드를 낮춘 만큼 초상화도 줄였다 (136 → 102). 더 늘리려면 열(ListCols)을 볼 것 —
    //   그러면 오른쪽 정보 칸(DetailW)이 좁아진다.
    const int   ListCols = 3;
    const int   ListRows = 5;
    const int   ListMax  = ListCols * ListRows;
    const float LCardW   = 236f;   // "비스트마스터"(6자)가 FontSm 그대로 들어가는 폭
    const float LCardH   = 164f;
    const float LCardGap = 12f;
    const float LPortH   = 102f;   // ⚠ 입체 버튼의 몸통은 BtnLift(6)만큼 낮다 — 8+102+4+43 ≤ 158
    const float ListW    = ListCols * LCardW + (ListCols - 1) * LCardGap;   // 740
    static readonly float ListGridTop = UIScale.RowLg + 12f;                // 82

    // 오른쪽 칸 — 남는 폭을 그대로 쓴다 (앵커 스트레치). 폭 숫자는 반쪽 계산에만 쓴다.
    const float DetailW   = UIScale.RefWidth - SideW - EdgeGap * 3f - ListW;   // 656
    const float ActionH   = 100f;   // ≥ UIScale.BtnFor(FontLg) = 96
    const float ActionGap = 16f;
    const float PanelH    = FrameH - ActionH - ActionGap;                    // 836
    const float BackW     = 220f;
    const int   IPad      = 20;

    // ── 색상 ──────────────────────────────────────────────────
    static readonly Color SideBg   = new Color(0.05f, 0.06f, 0.11f, 0.92f);
    static readonly Color CardBg   = new Color(0.07f, 0.08f, 0.14f, 0.94f);
    static readonly Color PortBg   = new Color(0.03f, 0.04f, 0.08f, 1.00f);
    static readonly Color StatRowC = new Color(0.10f, 0.11f, 0.19f, 1.00f);
    static readonly Color StartC   = new Color(0.11f, 0.72f, 0.58f, 1.00f);
    static readonly Color RelicC   = new Color(0.35f, 0.18f, 0.50f, 1.00f);
    static readonly Color CodexC   = new Color(0.10f, 0.36f, 0.42f, 1.00f);   // 도감 — 청록 (StatBonusColors.Codex 계열)
    static readonly Color DetailC  = new Color(0.18f, 0.25f, 0.42f, 1.00f);
    static readonly Color SlotC    = new Color(0.07f, 0.08f, 0.16f, 1.00f);
    static readonly Color Muted    = new Color(0.55f, 0.57f, 0.72f);
    static readonly Color DivC     = new Color(0.22f, 0.24f, 0.34f, 0.70f);
    static readonly Color SideDivC = new Color(0.15f, 0.17f, 0.26f, 1.00f);
    static readonly Color LockedC  = new Color(0.09f, 0.10f, 0.17f, 0.90f);
    static readonly Color SettingsC = new Color(0.26f, 0.30f, 0.42f, 1f);

    // =========================================================

    // ⚠ 올드Tools 에서 올라왔다 (2026-08-27)
    //   원작 장수 선택 화면이었지만, 이 게임의 **소환사 선택 화면**으로
    //   내용을 갈아 끼우고 실제로 굴려 확인했다.
    //   (Tools 루트 규칙은 CLAUDE.md '에디터 툴' 항목 참고)
    [MenuItem(ProjectKMenu.Lobby + "MainPanel", priority = ProjectKMenu.PrefabPrio + 12)]
    public static void Run()
    {
        var canvas = new GameObject("_TempCanvas", typeof(RectTransform));
        canvas.GetComponent<RectTransform>().sizeDelta =
            new Vector2(UIScale.RefWidth, UIScale.RefHeight);
        try
        {
            var panel = Build(canvas);
            PrefabUtility.SaveAsPrefabAsset(panel, SavePath);
        }
        finally
        {
            // Build 가 굽기를 멈춰도(아이콘 없음) 임시 캔버스가 씬에 남지 않게 한다
            Object.DestroyImmediate(canvas);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MainPanelCreator] 생성 완료 → " + SavePath);
    }

    public static GameObject Build(GameObject parent)
    {
        // ⚠ 마나는 글자가 아니라 아이콘이다 (UI 규칙 7) — 없으면 굽기를 멈춘다.
        //   빈 칸으로 구워 두면 프리팹만 보고 무엇이 빠졌는지 알 수 없다.
        if (!UIIconAssets.TryLoad("MainPanelCreator", out Sprite manaIcon, out _))
            throw new System.InvalidOperationException("[MainPanelCreator] 마나 아이콘이 없어 굽기를 멈춥니다.");

        // 루트 (투명 — 배경 투과)
        var root = new GameObject("MainPanel", typeof(RectTransform));
        root.transform.SetParent(parent.transform, false);
        var ui = root.AddComponent<MainPanelUI>();
        Stretch(root.GetComponent<RectTransform>());

        // 배경 이미지 (최하위 — 모든 UI 뒤)
        var bgImg = new GameObject("BackgroundImage", typeof(RectTransform), typeof(Image));
        bgImg.transform.SetParent(root.transform, false);
        Stretch(bgImg.GetComponent<RectTransform>());
        var bgImgComp = bgImg.GetComponent<Image>();
        // ⚠ 투명하게 둔다 — 이 뒤에서 배경 데모 전투가 돈다
        //   오브젝트와 컴포넌트는 남겨 둔다 (MainPanelUI._backgroundImage 참조 유지).
        bgImgComp.color          = new Color(1f, 1f, 1f, 0f);
        bgImgComp.preserveAspect = false;
        bgImgComp.raycastTarget  = false;

        // 좌측 사이드
        var (relicBtn, codexBtn, settingsBtn) = BuildSide(root);

        // 우측 영역 (투명)
        var right = new GameObject("RightArea", typeof(RectTransform));
        right.transform.SetParent(root.transform, false);
        {
            var rt = right.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(SideW, 0f);
            rt.offsetMax = Vector2.zero;
        }

        // ── 가운데: 소환사 격자 ───────────────────────────────
        var listCol = EditorUIBuilder.Go("SummonerList", right);
        {
            var rt = listCol.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot     = Vector2.zero;
            rt.anchoredPosition = new Vector2(EdgeGap, FrameBottomY);
            rt.sizeDelta        = new Vector2(ListW, FrameH);
        }

        var listHead = MakeTMP(listCol, "Head", "소 환 사", UIScale.FontLg, FontStyles.Bold);
        listHead.alignment     = TextAlignmentOptions.MidlineLeft;
        listHead.raycastTarget = false;
        NoWrap(listHead);
        TAF(listHead.rectTransform, 0f, UIScale.RowLg);

        var listHint = MakeTMP(listCol, "Hint", "눌러서 살펴보기", UIScale.FontSm, FontStyles.Normal);
        listHint.color         = Muted;
        listHint.alignment     = TextAlignmentOptions.MidlineRight;
        listHint.raycastTarget = false;
        NoWrap(listHint);
        TAF(listHint.rectTransform, 0f, UIScale.RowLg);

        var listCards = new SummonerListCardUI[ListMax];
        for (int i = 0; i < ListMax; i++)
        {
            int col = i % ListCols, row = i / ListCols;
            listCards[i] = BuildListCard(listCol, i,
                new Vector2(col * (LCardW + LCardGap), -(ListGridTop + row * (LCardH + LCardGap))));
        }
        VerifyFits("소환사 격자", ListGridTop + ListRows * LCardH + (ListRows - 1) * LCardGap, FrameH);

        // ── 오른쪽: ① 정보 / ② 난이도 — 같은 자리를 번갈아 쓴다 ─────
        var detail = EditorUIBuilder.Go("DetailColumn", right);
        {
            // 좌우 스트레치 — 격자 오른쪽 끝에서 EdgeGap, 화면 오른쪽에서 EdgeGap.
            var rt = detail.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.offsetMin = new Vector2(EdgeGap + ListW + EdgeGap, FrameBottomY);
            rt.offsetMax = new Vector2(-EdgeGap, FrameBottomY + FrameH);
        }

        // ① 정보 단계 — 정보 칸 + [선택하기]
        var infoPhase = EditorUIBuilder.Go("InfoPhase", detail);
        Stretch(infoPhase.GetComponent<RectTransform>());

        var info = BuildInfoPanel(infoPhase, manaIcon);

        var selectBtn = MakeBtn(infoPhase, "SelectBtn", "선택하기", StartC, UIScale.FontLg);
        PlaceAction(selectBtn, 0f, 0f, stretch: true);
        var selectLabel = selectBtn.GetComponentInChildren<TextMeshProUGUI>();
        NoWrap(selectLabel);

        // ② 난이도 단계 — 난이도 칸 + [뒤로][게임 시작]
        var diffPhase = EditorUIBuilder.Go("DifficultyPhase", detail);
        Stretch(diffPhase.GetComponent<RectTransform>());

        var chosenText = BuildDifficultyPanel(diffPhase);

        var backBtn = MakeBtn(diffPhase, "BackBtn", "뒤로", DetailC, UIScale.FontLg);
        PlaceAction(backBtn, 0f, BackW, stretch: false);
        NoWrap(backBtn.GetComponentInChildren<TextMeshProUGUI>());

        var startBtn = MakeBtn(diffPhase, "StartBtn", "게임 시작", StartC, UIScale.FontLg);
        PlaceAction(startBtn, BackW + ActionGap, 0f, stretch: true);
        NoWrap(startBtn.GetComponentInChildren<TextMeshProUGUI>());

        diffPhase.SetActive(false);   // 처음엔 ① 이다 — 런타임이 번갈아 켠다

        // MainPanelUI 연결
        var so = new SerializedObject(ui);
        so.Update();
        SetRef(so, "_backgroundImage", bgImgComp);
        SetRef(so, "_infoPhase",       infoPhase);
        SetRef(so, "_info",            info);
        SetRef(so, "_selectBtn",       selectBtn.GetComponent<Button>());
        SetRef(so, "_selectLabel",     selectLabel);
        SetRef(so, "_difficultyPhase", diffPhase);
        SetRef(so, "_chosenText",      chosenText);
        SetRef(so, "_backBtn",         backBtn.GetComponent<Button>());
        SetRef(so, "_startBtn",        startBtn.GetComponent<Button>());
        SetRef(so, "_relicBtn",        relicBtn.GetComponent<Button>());
        SetRef(so, "_codexBtn",        codexBtn.GetComponent<Button>());
        SetRef(so, "_settingsBtn",     settingsBtn.GetComponent<Button>());
        SetObjArrayLocal(so, "_listCards", listCards);
        so.ApplyModifiedProperties();
        return root;
    }

    /// <summary>
    /// 오른쪽 칸 바닥의 버튼 자리. stretch 면 x 부터 오른쪽 끝까지, 아니면 폭 width.
    /// </summary>
    static void PlaceAction(GameObject btn, float x, float width, bool stretch)
    {
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(stretch ? 1f : 0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.offsetMin = new Vector2(x, 0f);
        rt.offsetMax = new Vector2(stretch ? 0f : x + width, ActionH);
    }

    /// <summary>오른쪽 칸의 판 — 위에 붙고 PanelH 만큼. 바닥은 버튼 자리다.</summary>
    static GameObject MakeDetailPanel(string name, GameObject parent)
    {
        var panel = MakeImg(name, parent, CardBg);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, -PanelH);
        rt.offsetMax = Vector2.zero;

        // 상단 강조선 — 로비 팝업 헤더와 같은 언어
        var accent = MakeImg("Accent", panel, new Color(0.40f, 0.72f, 1.00f, 0.55f));
        var art = accent.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0f, 1f); art.anchorMax = new Vector2(1f, 1f);
        art.pivot     = new Vector2(0.5f, 1f);
        art.anchoredPosition = Vector2.zero;
        art.sizeDelta = new Vector2(0f, 3f);
        accent.GetComponent<Image>().raycastTarget = false;

        return panel;
    }

    /// <summary>쌓아 올린 세로가 칸을 넘었는가 — TAF 는 넘쳐도 조용히 칸 밖에 그린다.</summary>
    static void VerifyFits(string what, float used, float limit)
    {
        if (used > limit)
            Debug.LogError($"[MainPanelCreator] {what} 이(가) 칸을 넘칩니다: {used:0} > {limit:0}px. " +
                           "상수를 다시 잡으세요 — 아래 줄이 화면 밖으로 밀립니다.");
    }

    // ══════════════════════════════════════════════════════════
    //  소환사 격자 카드 — 초상화 · 이름 · 잠금 (SummonerListCardUI)
    //
    //  ┌── 236 ──┐   고른 카드만 테두리가 금색으로 켜진다.
    //  │[초상화]  │   잠긴 카드는 어둡게 덮고 자물쇠를 얹는다 —
    //  │  이 름   │   그래도 눌린다 (오른쪽에 해금 조건이 뜬다).
    //  └──────────┘
    // ══════════════════════════════════════════════════════════

    static readonly Color ListCardC    = new Color(0.12f, 0.14f, 0.22f, 1.00f);
    static readonly Color ListFrameOff = new Color(0.16f, 0.18f, 0.27f, 1.00f);

    static SummonerListCardUI BuildListCard(GameObject parent, int index, Vector2 pos)
    {
        var slot = EditorUIBuilder.Go($"Card_{index + 1}", parent);
        {
            var rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta        = new Vector2(LCardW, LCardH);
        }
        var cardUI = slot.AddComponent<SummonerListCardUI>();

        // 테두리 — 버튼의 **앞 형제**라 뒤에 깔린다 (UI 규칙 3).
        var frame = MakeImg("Frame", slot, ListFrameOff);
        {
            var rt = frame.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-5f, -5f);
            rt.offsetMax = new Vector2( 5f,  5f);
        }
        frame.GetComponent<Image>().raycastTarget = false;

        // 누르는 카드다 — 입체 버튼 (UI 규칙 1). 내용은 전부 body 아래.
        var btn = EditorUIBuilder.RaisedBtn(slot, "Button", ListCardC, out var body);
        Stretch(btn.GetComponent<RectTransform>());

        var portrait = MakeImg("Portrait", body, PortBg);
        TAF(portrait.GetComponent<RectTransform>(), 8f, LPortH, 8, 8);
        portrait.GetComponent<Image>().raycastTarget = false;

        var portImg = MakeImg("PortraitImage", portrait, Color.white);
        Stretch(portImg.GetComponent<RectTransform>());
        var pImg = portImg.GetComponent<Image>();
        pImg.preserveAspect = true;
        pImg.raycastTarget  = false;

        // 외형 합성 다리 — 인간형 소환사의 초상화를 UnitPortraitHelper 가 여기서 굽는다.
        var bridgeGo = new GameObject("PortraitBridge");
        bridgeGo.transform.SetParent(portrait.transform, false);
        var bridge = bridgeGo.AddComponent<UnitAppearanceBridge>();

        var nameTmp = MakeTMP(body, "NameText", "이름", UIScale.FontSm, FontStyles.Bold);
        nameTmp.color         = Color.white;
        nameTmp.alignment     = TextAlignmentOptions.Center;
        nameTmp.raycastTarget = false;
        NoWrap(nameTmp);
        TAF(nameTmp.rectTransform, 8f + LPortH + 4f, UIScale.RowSm, 6, 6);

        // 잠금 — 덮개는 반투명이다. 누가 잠겼는지 얼굴은 보여야 목표가 된다.
        var lockRoot = EditorUIBuilder.Go("LockRoot", body);
        Stretch(lockRoot.GetComponent<RectTransform>());
        var veil = lockRoot.AddComponent<Image>();
        veil.color         = new Color(0.03f, 0.04f, 0.08f, 0.62f);
        veil.raycastTarget = false;   // 잠겨도 눌린다 — 조건을 보려면 눌러야 한다

        // ⚠ 자물쇠는 글리프가 아니라 도형이다 (UI 규칙 2)
        GameObject padlock = EditorUIBuilder.PadLock(lockRoot, "LockIcon", 56f,
                                                     new Color(0.86f, 0.90f, 1.00f, 0.92f));
        // 초상화 한가운데 (lockRoot 는 몸통 크기 = 카드 − BtnLift)
        padlock.GetComponent<RectTransform>().anchoredPosition =
            new Vector2(0f, (LCardH - 6f) * 0.5f - (8f + LPortH * 0.5f));
        lockRoot.SetActive(false);

        var so = new SerializedObject(cardUI);
        so.Update();
        SetRef(so, "_button",         btn);
        SetRef(so, "_frame",          frame.GetComponent<Image>());
        SetRef(so, "_portraitBg",     portrait.GetComponent<Image>());
        SetRef(so, "_portraitImage",  pImg);
        SetRef(so, "_portraitBridge", bridge);
        SetRef(so, "_nameText",       nameTmp);
        SetRef(so, "_lockRoot",       lockRoot);
        so.ApplyModifiedProperties();

        return cardUI;
    }

    // ══════════════════════════════════════════════════════════
    //  ① 소환사 정보 칸 (SummonerCandidateCardUI)
    //
    //  ┌──────────────── 656 ────────────────┐
    //  │ [초상화 184]  이름                    │
    //  │               패기 ■■■■■□□□□□ 5       │  ← 배분 (눈금 3줄은 초상화 옆)
    //  │               체력 ■■■■■■■□□□ 7       │
    //  │               지능 ■■■■■■■□□□ 7       │
    //  │ [마왕성      60] [◆          52]      │  ← 배분이 무엇이 되는가 (◆ = 마나 아이콘)
    //  │ [소환력       7] [카드 칸      5]      │     카드 칸은 기준(5)보다 많으면 초록 · 적으면 주황
    //  │ 설명 (네 줄 자리 · 자동 축소)           │
    //  │ ─────────────────────────────       │
    //  │ [개성] 이름 / 설명 두 줄               │
    //  │ [스킬] 이름               스테이지당 N회│  ← 이름·그림이 소환사마다 다르다 (SignatureSkillDisplay)
    //  │ 친화 종족         │ 시작 카드          │
    //  │ ●●●●●             │ ●●●●               │
    //  └─────────────────────────────────────┘
    //
    //  ⚠ 스킬 줄에 설명·소환 줄을 덧붙이지 않는다 (사용자 지시, 2026-09-11)
    //    "무엇을 부르는가" 는 이름과 그림이 말한다 — 메테오처럼 소환이 아닌 스킬은
    //    덧붙일 말이 없어 빈 줄만 남는다.
    // ══════════════════════════════════════════════════════════

    const float IPortSz     = 184f;   // = 이름 한 줄(70) + 눈금 3줄(36×3 + 3×2)
    const float StatChipH   = 46f;
    const float StatChipGap = 6f;
    const float DescH       = 146f;   // 설명은 두 문장 = 최대 네 줄 (FontSm×0.85 에서 145)
    const float PerkRowH    = 140f;
    const float SkillRowH   = 72f;

    // 머리의 눈금 줄 (BuildPipRow) — 폭 416 = [이름표 76][눈금 10칸][값 44]
    const float SPipsH     = 36f;
    const float SPipsGap   = 3f;
    const float SPipSize   = 24f;
    const float SPipGap    = 3f;
    const float SPipLabelW = 76f;

    // 친화·시작 카드 줄 (BuildIconStrip) — 반쪽 폭(≈300)에 5칸이 들어가는 크기
    const float SIconSz   = 52f;
    const int   SAffMax   = 5;
    const int   SStartMax = 4;

    static readonly Color PerkC    = new Color(0.72f, 0.55f, 1.00f);
    static readonly Color SkillC   = new Color(1.00f, 0.82f, 0.42f);
    static readonly Color DescC    = new Color(0.74f, 0.78f, 0.90f);
    static readonly Color ChipVitC = new Color(0.45f, 0.88f, 0.55f);   // SummonerCandidateCardUI.VitColor 와 같은 색
    static readonly Color ChipIntC = new Color(0.62f, 0.66f, 1.00f);   // 〃 IntColor

    static SummonerCandidateCardUI BuildInfoPanel(GameObject parent, Sprite manaIcon)
    {
        const int lp = IPad, rp = IPad;

        var panel = MakeDetailPanel("InfoPanel", parent);
        var cUI   = panel.AddComponent<SummonerCandidateCardUI>();

        float y = 16f;

        // ── 머리: 초상화 + 이름 + 눈금 3줄 ──
        var portrait = MakeImg("Portrait", panel, PortBg);
        {
            var rt = portrait.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(IPad, -y);
            rt.sizeDelta        = new Vector2(IPortSz, IPortSz);
        }
        portrait.GetComponent<Image>().raycastTarget = false;

        var portImg = MakeImg("PortraitImage", portrait, Color.white);
        Stretch(portImg.GetComponent<RectTransform>());
        var pImg = portImg.GetComponent<Image>();
        pImg.preserveAspect = true;
        pImg.raycastTarget  = false;

        var bridgeGo = new GameObject("PortraitBridge");
        bridgeGo.transform.SetParent(portrait.transform, false);
        var bridge = bridgeGo.AddComponent<UnitAppearanceBridge>();

        const float RightX = IPad + IPortSz + 16f;   // 220

        var nameTmp = MakeTMP(panel, "NameText", "이름", UIScale.FontLg, FontStyles.Bold);
        nameTmp.color         = Color.white;
        nameTmp.alignment     = TextAlignmentOptions.MidlineLeft;
        nameTmp.raycastTarget = false;
        AutoFit(nameTmp, UIScale.FontMd, UIScale.FontLg);
        {
            var rt = nameTmp.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(RightX, -(y + UIScale.RowLg));
            rt.offsetMax = new Vector2(-IPad, -y);
        }

        float py = y + UIScale.RowLg;
        StatPipsUI str = BuildPipRow(panel, "VigorRow",        py, RightX, IPad); py += SPipsH + SPipsGap;
        StatPipsUI vit = BuildPipRow(panel, "VitalityRow",     py, RightX, IPad); py += SPipsH + SPipsGap;
        StatPipsUI itl = BuildPipRow(panel, "IntelligenceRow", py, RightX, IPad); py += SPipsH;
        VerifyFits("정보 칸 머리(이름 + 눈금 3줄)", py - y, IPortSz);

        y += IPortSz + 12f;

        // ── 환산 수치 칩 2×2 — 판 폭을 다 쓴다 ──
        const float ChipsH = StatChipH * 2f + StatChipGap;
        var chips = EditorUIBuilder.Go("StatChips", panel);
        TAF(chips.GetComponent<RectTransform>(), y, ChipsH, lp, rp);

        var coreHp = BuildStatChip(chips, "CoreHpChip", 0, 0, "마왕성", null,     ChipVitC);
        var mana   = BuildStatChip(chips, "ManaChip",   1, 0, null,     manaIcon, Color.white);
        var power  = BuildStatChip(chips, "PowerChip",  0, 1, "소환력", null,     ChipIntC);
        var slots  = BuildStatChip(chips, "SlotChip",   1, 1, "카드 칸", null,     Color.white);

        y += ChipsH + 10f;

        // ── 설명 — 두 문장이다. 줄바꿈하고 넘치면 조금 줄어든다 ──
        var descTmp = MakeTMP(panel, "DescText", "", UIScale.FontSm, FontStyles.Normal);
        descTmp.color            = DescC;
        descTmp.alignment        = TextAlignmentOptions.TopLeft;
        descTmp.raycastTarget    = false;
        descTmp.textWrappingMode = TextWrappingModes.Normal;
        descTmp.enableAutoSizing = true;
        descTmp.fontSizeMin      = UIScale.FontSm * 0.85f;
        descTmp.fontSizeMax      = UIScale.FontSm;
        TAF(descTmp.rectTransform, y, DescH, lp, rp);
        y += DescH + 4f;

        y = Divider(panel, "Div1", y, lp, rp);

        // ── 개성 ──
        var perkRow = MakeImg("PerkRow", panel, StatRowC);
        TAF(perkRow.GetComponent<RectTransform>(), y, PerkRowH, lp, rp);
        perkRow.GetComponent<Image>().raycastTarget = false;

        var perkIcon = MakeImg("PerkIcon", perkRow, Color.white);
        {
            var rt = perkIcon.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(14f, 0f);
            rt.sizeDelta        = new Vector2(64f, 64f);
        }
        var perkIconImg = perkIcon.GetComponent<Image>();
        perkIconImg.preserveAspect = true;
        perkIconImg.raycastTarget  = false;

        var perkName = MakeTMP(perkRow, "PerkName", "개성", UIScale.FontMd, FontStyles.Bold);
        perkName.color         = PerkC;
        perkName.alignment     = TextAlignmentOptions.MidlineLeft;
        perkName.raycastTarget = false;
        NoWrap(perkName);
        {
            var rt = perkName.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(92f, -(8f + UIScale.RowMd));
            rt.offsetMax = new Vector2(-14f, -8f);
        }

        // 개성 설명은 긴 것이 한 줄 반쯤 된다 — 두 줄 자리에 자동 축소
        var perkDesc = MakeTMP(perkRow, "PerkDesc", "", UIScale.FontSm, FontStyles.Normal);
        perkDesc.color            = Muted;
        perkDesc.alignment        = TextAlignmentOptions.TopLeft;
        perkDesc.raycastTarget    = false;
        perkDesc.textWrappingMode = TextWrappingModes.Normal;
        perkDesc.enableAutoSizing = true;
        perkDesc.fontSizeMin      = UIScale.FontSm * 0.85f;
        perkDesc.fontSizeMax      = UIScale.FontSm;
        {
            var rt = perkDesc.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(92f, 6f);
            rt.offsetMax = new Vector2(-14f, -(8f + UIScale.RowMd));
        }

        y += PerkRowH + 8f;

        // ── 시그니처 스킬 — [그림] 이름 ········ 스테이지당 N회 ──
        var skillRow = MakeImg("SkillRow", panel, StatRowC);
        TAF(skillRow.GetComponent<RectTransform>(), y, SkillRowH, lp, rp);
        skillRow.GetComponent<Image>().raycastTarget = false;

        var skillIcon = MakeImg("SkillIcon", skillRow, Color.white);
        {
            var rt = skillIcon.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(14f, 0f);
            rt.sizeDelta        = new Vector2(56f, 56f);
        }
        var skillIconImg = skillIcon.GetComponent<Image>();
        skillIconImg.preserveAspect = true;
        skillIconImg.raycastTarget  = false;

        const float UsesW = 200f;

        var skillName = MakeTMP(skillRow, "SkillName", "", UIScale.FontMd, FontStyles.Bold);
        skillName.color         = SkillC;
        skillName.alignment     = TextAlignmentOptions.MidlineLeft;
        skillName.raycastTarget = false;
        AutoFit(skillName, UIScale.FontSm, UIScale.FontMd);
        {
            var rt = skillName.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(84f, 0f);
            rt.offsetMax = new Vector2(-(UsesW + 20f), 0f);
        }

        var skillUses = MakeTMP(skillRow, "SkillUses", "", UIScale.FontSm, FontStyles.Normal);
        skillUses.color         = Muted;
        skillUses.alignment     = TextAlignmentOptions.MidlineRight;
        skillUses.raycastTarget = false;
        NoWrap(skillUses);
        {
            var rt = skillUses.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(-(UsesW + 14f), 0f);
            rt.offsetMax = new Vector2(-14f, 0f);
        }

        y += SkillRowH + 10f;

        // ── 친화 종족 │ 시작 카드 — 한 줄을 반씩 ──
        int half = Mathf.RoundToInt(DetailW * 0.5f);

        Image[] affinity = BuildIconStrip(panel, "AffinityRow", "친화 종족", y, IPad,      half + 6, SAffMax);
        Image[] starters = BuildIconStrip(panel, "StarterRow",  "시작 카드", y, half + 10, IPad,     SStartMax);

        var groupDiv = MakeImg("StripDivider", panel, DivC);
        groupDiv.GetComponent<Image>().raycastTarget = false;
        {
            var rt = groupDiv.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(half, -y);
            rt.sizeDelta        = new Vector2(2f, UIScale.RowSm + SIconSz);
        }

        y += UIScale.RowSm + SIconSz;
        VerifyFits("소환사 정보 칸", y + 8f, PanelH);

        // ── 잠금 덮개 ──────────────────────────────────────
        //  ⚠ 반투명이다 — 스탯·개성·친화가 비쳐야 "무엇을 향해 가는지" 가 읽힌다.
        //  ⚠ 자물쇠는 글리프가 아니라 도형이다 (UI 규칙 2).
        var lockRoot = EditorUIBuilder.Go("LockRoot", panel);
        Stretch(lockRoot.GetComponent<RectTransform>());
        var lockVeil = lockRoot.AddComponent<Image>();
        lockVeil.color = new Color(0.04f, 0.05f, 0.10f, 0.78f);

        GameObject padlock = EditorUIBuilder.PadLock(
            lockRoot, "LockIcon", 78f, new Color(0.86f, 0.90f, 1.00f, 0.92f));
        padlock.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 132f);

        var lockTitle = MakeTMP(lockRoot, "LockTitle", "잠김", UIScale.FontLg, FontStyles.Bold);
        lockTitle.alignment     = TextAlignmentOptions.Center;
        lockTitle.color         = new Color(0.92f, 0.94f, 1.00f);
        lockTitle.raycastTarget = false;
        {
            var rt = lockTitle.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 40f);
            rt.sizeDelta        = new Vector2(DetailW - 60f, UIScale.RowLg);
        }

        // 조건 문구 — 여러 줄이 올 수 있다 (트롤 조련사는 종족 + 스테이지 두 줄).
        var lockText = MakeTMP(lockRoot, "LockText", "", UIScale.FontSm, FontStyles.Normal);
        lockText.alignment     = TextAlignmentOptions.Top;
        lockText.color         = new Color(1.00f, 0.86f, 0.52f);
        lockText.raycastTarget = false;
        {
            var rt = lockText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(DetailW - 72f, UIScale.RowSm * 4f);
        }

        lockRoot.SetActive(false);   // 실제 표시는 런타임 판정이 정한다

        // ── 연결 ──
        var so = new SerializedObject(cUI);
        so.Update();
        SetRef(so, "_portraitBg",       portrait.GetComponent<Image>());
        SetRef(so, "_portraitImage",    pImg);
        SetRef(so, "_portraitBridge",   bridge);
        SetRef(so, "_nameText",         nameTmp);
        SetRef(so, "_descText",         descTmp);
        SetRef(so, "_coreHpText",       coreHp);
        SetRef(so, "_manaText",         mana);
        SetRef(so, "_powerText",        power);
        SetRef(so, "_slotText",         slots);
        SetRef(so, "_strengthPips",     str);
        SetRef(so, "_vitalityPips",     vit);
        SetRef(so, "_intelligencePips", itl);
        SetRef(so, "_perkIcon",         perkIconImg);
        SetRef(so, "_perkName",         perkName);
        SetRef(so, "_perkDesc",         perkDesc);
        SetRef(so, "_skillIcon",        skillIconImg);
        SetRef(so, "_skillName",        skillName);
        SetRef(so, "_skillUses",        skillUses);
        SetRef(so, "_lockRoot",         lockRoot);
        SetRef(so, "_lockText",         lockText);
        SetRef(so, "_fallbackIcon",     LoadPerkIcon());
        SetImgArray(so, "_affinityIcons", affinity);
        SetImgArray(so, "_starterIcons",  starters);
        so.ApplyModifiedProperties();

        return cUI;
    }

    /// <summary>
    /// 환산 수치 칩 한 칸 — [이름표 ····· 값]. icon 이 있으면 이름표 자리에 그림이 앉는다.
    /// ⚠ 마나는 반드시 icon 으로 넘긴다 (UI 규칙 7).
    /// </summary>
    static TextMeshProUGUI BuildStatChip(GameObject parent, string name, int col, int row,
                                         string label, Sprite icon, Color valueColor)
    {
        var chip = MakeImg(name, parent, StatRowC);
        chip.GetComponent<Image>().raycastTarget = false;
        {
            float top = row * (StatChipH + StatChipGap);
            var rt = chip.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(col * 0.5f,       1f);
            rt.anchorMax = new Vector2((col + 1) * 0.5f, 1f);
            rt.offsetMin = new Vector2(col == 0 ? 0f : StatChipGap * 0.5f, -(top + StatChipH));
            rt.offsetMax = new Vector2(col == 0 ? -StatChipGap * 0.5f : 0f, -top);
        }

        if (icon != null)
        {
            var ic = MakeImg("Icon", chip, Color.white);
            var rt = ic.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(12f, 0f);
            rt.sizeDelta        = new Vector2(38f, 38f);
            var img = ic.GetComponent<Image>();
            img.sprite         = icon;
            img.preserveAspect = true;
            img.raycastTarget  = false;
        }
        else
        {
            var lt = MakeTMP(chip, "Label", label, UIScale.FontSm, FontStyles.Normal);
            lt.color         = Muted;
            lt.alignment     = TextAlignmentOptions.MidlineLeft;
            lt.raycastTarget = false;
            NoWrap(lt);
            var rt = lt.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(12f, 0f);
            rt.offsetMax = new Vector2(12f + 130f, 0f);
        }

        var vt = MakeTMP(chip, "Value", "0", UIScale.FontMd, FontStyles.Bold);
        vt.color         = valueColor;
        vt.alignment     = TextAlignmentOptions.MidlineRight;
        vt.raycastTarget = false;
        NoWrap(vt);
        {
            var rt = vt.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(60f, 0f);
            rt.offsetMax = new Vector2(-12f, 0f);
        }
        return vt;
    }

    /// <summary>구분선 한 줄을 놓고 다음 y 를 돌려준다.</summary>
    static float Divider(GameObject card, string name, float y, int lp, int rp)
    {
        var d = MakeImg(name, card, DivC);
        d.GetComponent<Image>().raycastTarget = false;
        TAF(d.GetComponent<RectTransform>(), y, 2f, lp, rp);

        return y + 2f + 10f;
    }

    /// <summary>
    /// "패기 ■■■■■□□□□□ 5" 한 줄 — 정보 칸 머리의 초상화 옆에 선다.
    ///
    /// ⚠ 칸을 레이아웃 그룹으로 만들지 않는다
    ///   칸 수가 10 으로 고정이라 좌표 계산이 더 짧고, 런타임에 폭이 흔들리지 않는다.
    /// ⚠ 폭 = 이름표 + 눈금 10칸 + 값 — 머리 오른쪽 폭(416)에 맞춘 값이다 (VerifyFits 가 잰다).
    /// </summary>
    static StatPipsUI BuildPipRow(GameObject parent, string name, float y, float left, float right)
    {
        const float ValueW = 44f;

        var row = MakeImg(name, parent, StatRowC);
        row.GetComponent<Image>().raycastTarget = false;
        {
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, -(y + SPipsH));
            rt.offsetMax = new Vector2(-right, -y);
        }

        var pips = row.AddComponent<StatPipsUI>();

        var label = MakeTMP(row, "Label", "패기", UIScale.FontSm, FontStyles.Bold);
        label.color         = Color.white;
        label.alignment     = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        NoWrap(label);
        {
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(10f, 0f);
            rt.sizeDelta        = new Vector2(SPipLabelW - 10f, UIScale.RowSm);
        }

        var value = MakeTMP(row, "Value", "0", UIScale.FontSm, FontStyles.Bold);
        value.color         = Color.white;
        value.alignment     = TextAlignmentOptions.MidlineRight;
        value.raycastTarget = false;
        NoWrap(value);
        {
            var rt = value.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-10f, 0f);
            rt.sizeDelta        = new Vector2(ValueW, UIScale.RowSm);
        }

        // 눈금 10칸 — 이름표 오른쪽에서 시작해 값 왼쪽에서 끝난다.
        var   pipImages = new Image[StatPipsUI.MaxPips];
        float startX    = SPipLabelW + 6f;

        for (int i = 0; i < pipImages.Length; i++)
        {
            var pip = MakeImg($"Pip_{i + 1}", row, SlotC);
            var rt  = pip.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(startX + i * (SPipSize + SPipGap), 0f);
            rt.sizeDelta        = new Vector2(SPipSize, SPipSize * 0.62f);

            var img = pip.GetComponent<Image>();
            img.raycastTarget = false;
            pipImages[i] = img;
        }

        VerifyFits($"눈금 줄 {name}",
                   startX + StatPipsUI.MaxPips * (SPipSize + SPipGap) - SPipGap + 6f + ValueW + 10f,
                   DetailW - left - right);

        var pso = new SerializedObject(pips);
        pso.Update();
        SetRef(pso, "_label",     label);
        SetRef(pso, "_valueText", value);
        SetImgArray(pso, "_pips", pipImages);
        pso.ApplyModifiedProperties();

        return pips;
    }

    /// <summary>
    /// [이름표] 한 줄 + 그 아래 아이콘 가로 줄.
    /// left/right 는 판 좌우에서의 안쪽 여백이라, 반쪽 폭만 쓰려면 반대쪽을 크게 준다.
    /// </summary>
    static Image[] BuildIconStrip(GameObject parent, string name, string title,
                                  float y, float left, float right, int count)
    {
        var head = MakeTMP(parent, name + "Label", title, UIScale.FontSm, FontStyles.Bold);
        head.color         = Muted;
        head.alignment     = TextAlignmentOptions.MidlineLeft;
        head.raycastTarget = false;
        NoWrap(head);
        {
            var rt = head.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, -(y + UIScale.RowSm));
            rt.offsetMax = new Vector2(-right, -y);
        }

        var row = EditorUIBuilder.Go(name, parent);
        {
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, -(y + UIScale.RowSm + SIconSz));
            rt.offsetMax = new Vector2(-right, -(y + UIScale.RowSm));
        }

        var icons = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var go = MakeImg($"Icon_{i + 1}", row, Color.white);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(i * (SIconSz + 6f), 0f);
            rt.sizeDelta        = new Vector2(SIconSz, SIconSz);

            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget  = false;
            icons[i] = img;
        }

        VerifyFits(name, count * (SIconSz + 6f) - 6f, DetailW - left - right);
        return icons;
    }

    static void SetImgArray(SerializedObject so, string field, Image[] images)
    {
        var prop = so.FindProperty(field);
        if (prop == null) return;

        prop.arraySize = images.Length;
        for (int i = 0; i < images.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
    }

    /// <summary>
    /// 개성·빈 칸에 쓸 대체 아이콘 (특성 아이콘 = 육각 보석).
    /// 없으면 null — 카드가 알아서 아이콘을 끈다.
    /// </summary>
    static Sprite LoadPerkIcon()
        => AssetDatabase.LoadAssetAtPath<Sprite>(
               "Assets/_project/3.Textures/Icons/Species/icon_cat_perk.png");

    // ── 초상화 위 직업·등급 배지 ─────────────────────────────

    // ── 그룹 라벨 (특성 / 스킬) ──────────────────────────────

    // ── 사이드바 ─────────────────────────────────────────────

    static (GameObject relic, GameObject codex, GameObject settings) BuildSide(GameObject parent)
    {
        var side = MakeImg("SideColumn", parent, SideBg);
        {
            var rt = side.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(SideW, 0f);
        }

        var border = MakeImg("Border", side, SideDivC);
        {
            var rt = border.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-2f, 0f); rt.offsetMax = Vector2.zero;
        }
        border.GetComponent<Image>().raycastTarget = false;

        // 타이틀 이미지
        var ta = MakeImg("TitleArea", side, Color.clear);
        {
            var rt = ta.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(0f, -TitleH); rt.offsetMax = Vector2.zero;
        }
        var ko = AssetDatabase.LoadAssetAtPath<Sprite>(TitleKoPath);
        var en = AssetDatabase.LoadAssetAtPath<Sprite>(TitleEnPath);
        if (ko == null || en == null)
            throw new System.InvalidOperationException("[MainPanelCreator] Localized title sprites are missing.");

        var img = ta.GetComponent<Image>();
        img.sprite = en; img.type = Image.Type.Simple;
        img.color = Color.white; img.preserveAspect = true; img.raycastTarget = false;

        var localizedTitle = ta.AddComponent<LocalizedTitleSprite>();
        var titleSo = new SerializedObject(localizedTitle);
        titleSo.FindProperty("_korean").objectReferenceValue = ko;
        titleSo.FindProperty("_english").objectReferenceValue = en;
        titleSo.ApplyModifiedPropertiesWithoutUndo();

        // 구분선
        var tDiv = MakeImg("TitleDiv", side, DivC);
        { var rt = tDiv.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.offsetMin = new Vector2(20f, -(TitleH + 2f)); rt.offsetMax = new Vector2(-20f, -TitleH); }
        tDiv.GetComponent<Image>().raycastTarget = false;

        // 버튼 영역 — 가로형 행 (정사각형이면 무엇을 누르는 건지 덜 읽힌다)
        var btnArea = new GameObject("BtnArea", typeof(RectTransform));
        btnArea.transform.SetParent(side.transform, false);
        { var rt = btnArea.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(1f, 1f); rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0f, -(TitleH + 8f)); }
        var vlg = btnArea.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        // ⚠ childControl* 를 끄면 LayoutElement 의 preferred 크기를 레이아웃이 아예 보지 않는다.
        //   그러면 버튼은 RectTransform 기본값 100×100 정사각형으로 남는다 —
        //   "가로로 길게" 가 반영되지 않던 원인이 이것이었다.
        vlg.childControlWidth = true;  vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
        vlg.spacing = 16f; vlg.padding = new RectOffset((int)SidePad, (int)SidePad, 24, 24);

        var relicBtn = BuildWideBtn(btnArea, "RelicBtn", "유물", RelicC,
            "Assets/_project/3.Textures/Icons/LobbyBtns/btn_relic.png");
        var rle = relicBtn.AddComponent<LayoutElement>();
        rle.preferredWidth = SideBtnW; rle.preferredHeight = 116f;

        var codexBtn = BuildWideBtn(btnArea, "CodexBtn", "도감", CodexC,
            "Assets/_project/3.Textures/Icons/LobbyBtns/btn_codex.png");
        var dle = codexBtn.AddComponent<LayoutElement>();
        dle.preferredWidth = SideBtnW; dle.preferredHeight = 116f;

        // 설정 — 옛 '기타' 잠금 칸 자리 (사용자 지시, 2026-09-17). 여는 창은 PausePopup 이다.
        //   아이콘 PNG 가 아직 없다 — BuildWideBtn 이 그림 칸을 끄고 글만 둔다.
        var settingsBtn = BuildWideBtn(btnArea, "SettingsBtn", "설정", SettingsC,
            "Assets/_project/3.Textures/Icons/LobbyBtns/btn_settings.png");
        var sle = settingsBtn.AddComponent<LayoutElement>();
        sle.preferredWidth = SideBtnW; sle.preferredHeight = 100f;

        return (relicBtn, codexBtn, settingsBtn);
    }

    // ── 화살표 ────────────────────────────────────────────────

    // ◀ ▶ 글리프는 폰트에 없다 (□ 로 렌더됨) → 꺾쇠 도형으로 그린다.
    // dirDeg: 0 = 오른쪽, 180 = 왼쪽
    // 예전엔 56×56 에 검정 45% 라 흙 배경에 그대로 묻혔다 →
    // 88×88 청색 입체 + 밝은 꺾쇠 + 뒤에 어두운 판을 깔아 대비를 만든다.

    // ── 사이드 버튼 (가로형) ──────────────────────────────────
    //  [아이콘][라벨 ─────────] › 형태. 정사각형보다 무엇을 누르는지 잘 읽힌다.

    static GameObject BuildWideBtn(GameObject parent, string name, string label, Color bg, string iconPath)
    {
        // UI 규칙 1: 누를 수 있는 버튼은 음각 처리 (내용은 body 아래로)
        var go = EditorUIBuilder.Go(name, parent);
        EditorUIBuilder.RaisedBtnOn(go, bg, out var body);

        var iGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iGo.transform.SetParent(body.transform, false);
        {
            var rt = iGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(14f, 12f);
            rt.offsetMax = new Vector2(14f + 76f, -12f);
        }
        var ii = iGo.GetComponent<Image>(); ii.preserveAspect = true; ii.raycastTarget = false;
        // ⚠ 그림이 없으면 칸을 끈다 — sprite 없는 Image 는 흰 사각형으로 그려진다
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        if (sp != null) ii.sprite = sp;
        else            ii.enabled = false;

        var lt = MakeTMP(body, "Label", label, UIScale.FontMd, FontStyles.Bold);
        lt.alignment = TextAlignmentOptions.MidlineLeft; lt.color = Color.white; lt.raycastTarget = false;
        NoWrap(lt);
        {
            var rt = lt.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(104f, 0f);
            rt.offsetMax = new Vector2(-46f, 0f);
        }

        // › 표식 — 눌러서 들어가는 화면이라는 신호
        var chev = EditorUIBuilder.Chevron(body, "Chevron", 26f, 0f, new Color(1f, 1f, 1f, 0.65f));
        {
            var rt = chev.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-18f, 0f);
        }
        return go;
    }

    static GameObject BuildLockedBtn(GameObject parent, string name, string label)
    {
        var go = MakeImg(name, parent, LockedC);
        var dim = new Color(0.30f, 0.32f, 0.48f);

        // 🔒 이모지는 폰트에 없다 (□ 로 렌더됨) → 자물쇠 도형으로 그린다
        var lockGo = EditorUIBuilder.PadLock(go, "Lock", 52f, dim);
        {
            var rt = lockGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(28f, 0f);
        }

        var tmp = MakeTMP(go, "Label", label, UIScale.FontMd, FontStyles.Bold);
        tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.color = dim; tmp.raycastTarget = false;
        NoWrap(tmp);
        {
            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(104f, 0f);
            rt.offsetMax = new Vector2(-46f, 0f);
        }

        var soon = MakeTMP(go, "SoonLabel", "준비 중", UIScale.FontSm, FontStyles.Normal);
        soon.alignment = TextAlignmentOptions.MidlineRight;
        soon.color = new Color(0.24f, 0.26f, 0.40f); soon.raycastTarget = false;
        NoWrap(soon);
        {
            var rt = soon.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(104f, 0f);
            rt.offsetMax = new Vector2(-20f, 0f);
        }
        return go;
    }

    // ── 특성 아이콘 UI ────────────────────────────────────────

    // ── 스킬 아이콘 (액티브 1 + 패시브 3) ────────────────────
    //  테두리 색으로 액티브(금)/패시브(파랑)/잠김(회색)을 구분한다.
    //  SkillIconUI 가 런타임에 _frame 색을 갈아 끼운다.

    //  아이콘 칸 공통 — 누를 수 있는 판 + 그 안의 아이콘 이미지

    //  아이콘 줄(높이 SkRH) 안에서 왼쪽 x 부터 size 정사각, 세로 중앙

    // ── 스탯 행 (앵커 기반 — LayoutGroup 미사용) ──────────────
    //  인게임·상점 용병 카드와 같은 표기로 맞춘다:
    //    [라벨 좌측][값 우측], 값은 StatColors, 둘 다 NoWrap 고정 크기.
    //  예전엔 라벨 우측정렬 + 값 좌측정렬에 값 칸이 120px 뿐이라
    //  "1,988" 이 두 줄로 접혔다.

    // ── 타이틀 이미지 생성 ────────────────────────────────────

    static Sprite GetOrCreateTitleSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(TitleImgPath);
        if (existing != null) return existing;

        const int W = 380, H = 300;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float fx = (float)x / W - 0.5f;
            float fy = (float)y / H;
            float glow  = Mathf.Max(0f, 1f - (fx * fx * 3.5f + (fy - 0.55f) * (fy - 0.55f) * 5f) * 4.5f) * 0.24f;
            float alpha = Mathf.Clamp01(fy * 1.4f);
            tex.SetPixel(x, y, new Color(0.04f + glow * 0.18f, 0.05f + glow * 0.32f, 0.12f + glow * 0.88f, alpha * 0.90f));
        }
        for (int x = 0; x < W; x++)
        {
            tex.SetPixel(x, H - 1, new Color(0.30f, 0.55f, 1f, 1f));
            tex.SetPixel(x, H - 2, new Color(0.22f, 0.44f, 0.88f, 0.8f));
            tex.SetPixel(x, H - 3, new Color(0.16f, 0.34f, 0.72f, 0.5f));
        }
        for (int y = 20; y < H - 20; y++)
        {
            float a = Mathf.Sin((float)(y - 20) / (H - 40) * Mathf.PI) * 0.55f;
            tex.SetPixel(0, y, new Color(0.28f, 0.52f, 1f, a));
            tex.SetPixel(1, y, new Color(0.22f, 0.44f, 0.85f, a * 0.5f));
        }
        tex.Apply();

        var dir = System.IO.Path.GetDirectoryName(
            System.IO.Path.Combine(Application.dataPath, "..", TitleImgPath));
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllBytes(
            System.IO.Path.Combine(Application.dataPath, "..", TitleImgPath),
            tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(TitleImgPath);
        var imp = AssetDatabase.LoadAssetAtPath<TextureImporter>(TitleImgPath);
        if (imp != null)
        {
            imp.textureType        = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.spritePivot        = new Vector2(0.5f, 0.5f);
            imp.filterMode         = FilterMode.Bilinear;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(TitleImgPath);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────

    static void Stretch(RectTransform rt) => EditorUIBuilder.Stretch(rt);

    /// <summary>
    /// 줄바꿈 금지 + 넘침 허용.
    /// ⚠ 칸보다 한 줄이 크면 Ellipsis/Truncate 는 그 줄을 통째로 버린다 —
    ///   접히거나 사라지는 사고는 전부 이 두 줄로 막는다.
    /// </summary>
    static void NoWrap(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode     = TextOverflowModes.Overflow;
    }

    /// <summary>NoWrap + 칸에 맞춰 축소 (넘치면 잘리는 대신 작아진다).</summary>
    static void AutoFit(TextMeshProUGUI tmp, float min, float max)
    {
        NoWrap(tmp);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin      = min;
        tmp.fontSizeMax      = max;
    }

    /// Top-Anchor Fill: card 상단 기준으로 y 위치, h 높이, 좌우 패딩
    static void TAF(RectTransform rt, float y, float h, int lp = 0, int rp = 0)
    {
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2( lp, -(y + h));
        rt.offsetMax = new Vector2(-rp,  -y);
    }

    static void SetRef(SerializedObject so, string field, Object obj)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = obj;
    }

    static GameObject MakeImg(string name, GameObject parent, Color color)
        => EditorUIBuilder.Panel(parent, name, color);

    // ══════════════════════════════════════════════════════════
    //  ② 난이도 선택 칸 (DifficultySelectorUI) — [선택하기] 뒤에 ① 자리에 선다
    //
    //  ┌──────────────── 656 ────────────────┐
    //  │ 난이도 선택                       (i) │
    //  │ 견습 소환사  —  출정 준비              │
    //  │ ┏[아이콘] 쉬움        환생 포인트 ×1.0┓│  ← 고른 줄만 등급 색 테두리
    //  │ ┃[아이콘] 보통        환생 포인트 ×1.2┃│
    //  │  [자물쇠] 어려움      보통 완주 시 해금 │  ← 잠긴 줄은 흐리게
    //  │  …  (5줄)                            │
    //  │ 적이 더 강해지고 수도 늘어난다.         │
    //  │ 적용 중인 제약  [광포][물량]            │  ← 눌러서 수치 툴팁
    //  │ (전투 중에는 바꿀 수 없습니다)          │
    //  └─────────────────────────────────────┘
    //
    //  ■ ‹ › 로 한 등급씩 넘기던 것을 다섯 줄로 펼쳤다 (2026-09-11)
    //    넘겨 보기 전에는 위에 무엇이 더 있는지 · 얼마나 더 주는지를 몰랐다.
    //    다섯 줄이 나란히 서면 보상 배율과 해금 조건을 한 번에 견준다.
    //  ■ 줄 하나가 곧 버튼이다 — 입체 버튼 (UI 규칙 1).
    // ══════════════════════════════════════════════════════════

    const float TierRowH   = 74f;
    const float TierRowGap = 8f;
    const float TierIconSz = 56f;
    const float DebuffSz   = 72f;
    const int   DebuffMax  = 4;
    const int   TierCount  = 5;   // DifficultyTier 개수

    static readonly Color TierFaceC = new Color(0.13f, 0.15f, 0.22f, 1.00f);

    struct TierRowParts
    {
        public Button          Button;
        public Image           Frame;
        public Image           Icon;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Note;
        public GameObject      Lock;
    }

    /// <summary>난이도 칸을 짓고, 머리의 "누구로 출정하나" 글을 돌려준다 (MainPanelUI 가 채운다).</summary>
    static TextMeshProUGUI BuildDifficultyPanel(GameObject parent)
    {
        const int lp = IPad, rp = IPad;

        var panel = MakeDetailPanel("DifficultyPanel", parent);
        var ui    = panel.AddComponent<DifficultySelectorUI>();

        float y = 16f;

        const float InfoSz = 52f;

        var head = MakeTMP(panel, "Head", "난이도 선택", UIScale.FontLg, FontStyles.Bold);
        head.color         = Color.white;
        head.alignment     = TextAlignmentOptions.MidlineLeft;
        head.raycastTarget = false;
        NoWrap(head);
        TAF(head.rectTransform, y, UIScale.RowLg, lp, rp + (int)InfoSz + 12);

        // 도움말 — 머리 줄 오른쪽 끝. 이 칸엔 닫기가 없어 비켜 놓을 거리는 0 이다.
        // ⚠ 기본은 세로 중앙 정렬이다 — 긴 칸 한복판에 뜨지 않게 머리 줄에 맞춘다.
        EditorUIBuilder.InfoBtn(panel, TutorialId.HelpDifficulty,
                                closeSize: 0f, closeRight: -IPad, gap: 0f, size: InfoSz,
                                anchorY: 1f, y: -(y + (UIScale.RowLg - InfoSz) * 0.5f));
        y += UIScale.RowLg;

        var chosen = MakeTMP(panel, "ChosenText", "", UIScale.FontSm, FontStyles.Normal);
        chosen.color         = Muted;
        chosen.alignment     = TextAlignmentOptions.MidlineLeft;
        chosen.raycastTarget = false;
        NoWrap(chosen);
        TAF(chosen.rectTransform, y, UIScale.RowSm, lp, rp);
        y += UIScale.RowSm + 12f;

        // ── 등급 다섯 줄 ──
        var rows = new TierRowParts[TierCount];
        for (int i = 0; i < TierCount; i++)
        {
            rows[i] = BuildTierRow(panel, i, y);
            y += TierRowH + TierRowGap;
        }
        y += 12f - TierRowGap;

        // ── 고른 등급의 요약 ──
        var summary = MakeTMP(panel, "SummaryLabel", "", UIScale.FontSm, FontStyles.Normal);
        summary.color            = new Color(0.78f, 0.82f, 0.95f);
        summary.alignment        = TextAlignmentOptions.TopLeft;
        summary.raycastTarget    = false;
        summary.textWrappingMode = TextWrappingModes.Normal;
        TAF(summary.rectTransform, y, UIScale.RowSm * 2f, lp, rp);
        y += UIScale.RowSm * 2f + 8f;

        // ── 적용 중인 제약 ──
        var dHead = MakeTMP(panel, "DebuffHead", "적용 중인 제약", UIScale.FontSm, FontStyles.Bold);
        dHead.color         = Muted;
        dHead.alignment     = TextAlignmentOptions.MidlineLeft;
        dHead.raycastTarget = false;
        NoWrap(dHead);
        TAF(dHead.rectTransform, y, UIScale.RowSm, lp, rp);
        y += UIScale.RowSm + 6f;

        var debuffRow = EditorUIBuilder.Go("DebuffRow", panel);
        TAF(debuffRow.GetComponent<RectTransform>(), y, DebuffSz, lp, rp);
        var hlg = debuffRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

        var debuffIcons = new TraitIconUI[DebuffMax];
        for (int i = 0; i < DebuffMax; i++)
            debuffIcons[i] = TraitIconSlotBuilder.Build(debuffRow, i, DebuffSz);

        // 제약이 없을 때 자리를 채우는 문구 (아이콘 0개면 빈칸만 남는다)
        var noneLbl = MakeTMP(panel, "NoDebuffLabel", "없음", UIScale.FontSm, FontStyles.Normal);
        noneLbl.color         = new Color(0.42f, 0.45f, 0.55f);
        noneLbl.alignment     = TextAlignmentOptions.MidlineLeft;
        noneLbl.raycastTarget = false;
        NoWrap(noneLbl);
        TAF(noneLbl.rectTransform, y + (DebuffSz - UIScale.RowSm) * 0.5f, UIScale.RowSm, lp + 4, rp);

        y += DebuffSz;

        // ── 바닥: 런 중 잠금 안내 ──
        const float LockBottom = 12f;
        var lockLbl = MakeTMP(panel, "LockLabel", "", UIScale.FontSm, FontStyles.Normal);
        lockLbl.color         = new Color(0.88f, 0.66f, 0.32f);
        lockLbl.alignment     = TextAlignmentOptions.MidlineLeft;
        lockLbl.raycastTarget = false;
        NoWrap(lockLbl);
        PlaceBottom(lockLbl.rectTransform, IPad, LockBottom, UIScale.RowSm);

        VerifyFits("난이도 칸", y + 4f, PanelH - LockBottom - UIScale.RowSm);

        var so = new SerializedObject(ui);
        so.Update();
        var rowsProp = so.FindProperty("_rows");
        rowsProp.arraySize = TierCount;
        for (int i = 0; i < TierCount; i++)
        {
            var e = rowsProp.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Button").objectReferenceValue = rows[i].Button;
            e.FindPropertyRelative("Frame") .objectReferenceValue = rows[i].Frame;
            e.FindPropertyRelative("Icon")  .objectReferenceValue = rows[i].Icon;
            e.FindPropertyRelative("Label") .objectReferenceValue = rows[i].Label;
            e.FindPropertyRelative("Note")  .objectReferenceValue = rows[i].Note;
            e.FindPropertyRelative("Lock")  .objectReferenceValue = rows[i].Lock;
        }
        SetRef(so, "_summaryLabel",  summary);
        SetRef(so, "_lockLabel",     lockLbl);
        SetRef(so, "_noDebuffLabel", noneLbl);
        SetObjArrayLocal(so, "_debuffIcons", debuffIcons);
        so.ApplyModifiedProperties();

        return chosen;
    }

    /// <summary>
    /// 등급 한 줄 — [아이콘] 이름 ········ 보상 배율 / 해금 조건.
    /// 테두리는 버튼의 **앞 형제**다 (UI 규칙 3). 색은 런타임이 고른 줄에만 켠다.
    /// </summary>
    static TierRowParts BuildTierRow(GameObject panel, int index, float y)
    {
        var tier = (DifficultyTier)index;
        Color tierColor = DifficultySelectorUI.TierColors[index];

        var root = EditorUIBuilder.Go($"Tier_{index}", panel);
        TAF(root.GetComponent<RectTransform>(), y, TierRowH, IPad, IPad);

        var frame = MakeImg("Frame", root, new Color(0f, 0f, 0f, 0f));
        {
            var rt = frame.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-4f, -4f);
            rt.offsetMax = new Vector2( 4f,  4f);
        }
        frame.GetComponent<Image>().raycastTarget = false;

        var btn = EditorUIBuilder.RaisedBtn(root, "Button", TierFaceC, out var body);
        Stretch(btn.GetComponent<RectTransform>());

        var icon = MakeImg("Icon", body, Color.white);
        {
            var rt = icon.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(12f, 0f);
            rt.sizeDelta        = new Vector2(TierIconSz, TierIconSz);
        }
        var iconImg = icon.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget  = false;

        // 잠긴 줄은 흐린 아이콘 위에 자물쇠 (UI 규칙 2 — 글리프 대신 도형)
        var lockGo = EditorUIBuilder.PadLock(body, "Lock", 34f, new Color(0.80f, 0.84f, 0.95f, 0.92f));
        {
            var rt = lockGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(12f + TierIconSz * 0.5f, 0f);
        }
        lockGo.SetActive(false);

        const float LabelX = 84f;
        const float NoteX  = 270f;

        var label = MakeTMP(body, "TierLabel", tier.Label(), UIScale.FontMd, FontStyles.Bold);
        label.color         = tierColor;
        label.alignment     = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        NoWrap(label);
        {
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(LabelX, 0f);
            rt.offsetMax = new Vector2(NoteX - 8f, 0f);
        }

        var note = MakeTMP(body, "Note", "", UIScale.FontSm, FontStyles.Normal);
        note.color         = Muted;
        note.alignment     = TextAlignmentOptions.MidlineRight;
        note.raycastTarget = false;
        NoWrap(note);
        {
            var rt = note.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(NoteX, 0f);
            rt.offsetMax = new Vector2(-16f, 0f);
        }

        return new TierRowParts
        {
            Button = btn,
            Frame  = frame.GetComponent<Image>(),
            Icon   = iconImg,
            Label  = label,
            Note   = note,
            Lock   = lockGo,
        };
    }

    /// <summary>패널 하단 기준 배치 — 좌우 스트레치.</summary>
    static void PlaceBottom(RectTransform rt, float padX, float y, float h)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.offsetMin = new Vector2(padX, y);
        rt.offsetMax = new Vector2(-padX, y + h);
    }

    static void SetObjArrayLocal(SerializedObject so, string field, Object[] items)
    {
        var p = so.FindProperty(field);
        if (p == null) return;
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    // UI 규칙: 누를 수 있는 버튼은 음각 처리 (EditorUIBuilder.RaisedTextBtn)
    static GameObject MakeBtn(GameObject parent, string name, string label, Color bg, float fontSize)
        => EditorUIBuilder.RaisedTextBtn(parent, name, label, fontSize, bg).gameObject;

    static TextMeshProUGUI MakeTMP(GameObject parent, string name, string text, float size, FontStyles style)
        => EditorUIBuilder.TMP(parent, name, text, size, style);

}
