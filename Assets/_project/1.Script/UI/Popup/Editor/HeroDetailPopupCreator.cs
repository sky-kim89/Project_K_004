#if UNITY_EDITOR
using System.IO;
using Assets.PixelFantasy.Common.Scripts.CollectionScripts;
using Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  HeroDetailPopupCreator.cs  [Editor Only]
//  Tools > Project K > 프리팹 생성 > 팝업 > HeroDetail
//
//  적(용사) 상세 — 전황에서 적을 누르면 연다 (HeroDetailPopup.SetupHero).
//
//  ■ 레이아웃 (전체 화면 1840×1000, 3단)
//    Header  H=136   ◆ 장 수 | 이름(동적) · 지휘력 안내              [X]
//    Body    H=814   Left 580 | Mid 460 | Right 720   (간격 20)
//      Left   초상화 556 / 직업 · 등급
//      Mid    "스 탯" + [장 수 | 용 병] 토글 + 11행 — 행을 누르면 출처별 분해
//      Right  "스 킬" + 액티브 1 + 패시브 3 — 설명을 FontMd 로 크게
//
//  ⚠ 원작의 성장·장비·해고 칸(플레이어 장수용)은 걷어냈다 — 적을 키울 수는 없다.
// ============================================================

public static class HeroDetailPopupCreator
{
    const string PrefabPath = "Assets/_project/2.Prefabs/UI/HeroDetailPopup.prefab";

    // ── 치수 ─────────────────────────────────────────────────
    const float PW      = 1840f;
    const float PH      = 1000f;
    const float SidePad = 40f;     // 좌우 총 여백 → 콘텐츠 1800

    const float HeaderH = 136f;
    const float BodyTop = 156f;
    const float BodyBtm = 30f;
    const float ColGap  = 20f;

    const float LeftW  = 580f;
    const float MidW   = 460f;
    const float RightW = 720f;     // 580 + 460 + 720 + 20×2 = 1800 ✓

    const float BodyH  = PH - BodyTop - BodyBtm;   // 814

    // 좌측 열 내부 — 초상화가 열 폭을 채운다 (12 + 556 + 12 = 580)
    const float PortraitSize = LeftW - 24f;
    const float DivH         = 36f;

    static readonly float InfoRowH = UIScale.RowMd;              // 53
    static readonly float StatTabH = UIScale.BtnFor(UIScale.FontMd);  // 72 — 장수/용병 토글

    // ── 스탯 목록 높이 배분 ───────────────────────────────────
    //
    //  ⚠ 행 높이를 손으로 적지 않는다
    //    예전엔 StatRowH = 72f 고정에 "행 수를 바꾸면 다시 계산할 것" 이라는
    //    주석만 있었다. 9행 × 72 = 648 로 목록 영역(664)을 거의 꽉 채워 두어
    //    스탯을 하나만 더 넣어도 아래가 잘렸다.
    //    이제 행 수만 고치면 높이가 따라온다.
    //
    //  ⚠ 최소치는 UIScale.RowMd(53) 다 — FontMd 한 줄 (UI 규칙 5)
    //    이 아래로 내려가면 값 글자 아래가 잘린다. 행을 더 늘리려면
    //    목록 영역 자체를 키우거나 폰트를 내려야 한다.
    const  int   StatRowCount = 11;    // 체력·공격·방어율·용병 수·이속·공속
                                       // ·사거리·지휘력·쿨타임·치명확률·치명피해
    static readonly float StatListTop = 12f + DivH + 10f + StatTabH + 10f;   // 140
    static readonly float StatListH   = BodyH - StatListTop - 10f;           // 664
    static readonly float StatRowH    = Mathf.Floor(StatListH / StatRowCount);
    // 스킬 열 세로 배분 (BodyH 814 안에서)
    //   구분선 12..48 / 액티브 60..300 / 패시브 320..802
    //   패시브 3칸 + 간격 12×2 = 482  → 한 칸 152
    // ⚠ ActiveH 를 키우면 패시브 칸이 줄어든다. 둘의 합을 반드시 다시 계산할 것.
    const float ActiveBoxH  = 240f;
    const float PassiveBoxH = 152f;

    // ── 색상 ─────────────────────────────────────────────────
    static readonly Color BgOverlay    = new Color(0f,     0f,     0f,     0.80f);
    static readonly Color PanelBg      = new Color(0.07f,  0.075f, 0.13f,  1f);
    static readonly Color PanelBorder  = new Color(0.26f,  0.44f,  0.72f,  1f);
    static readonly Color HeaderBg     = new Color(0.08f,  0.10f,  0.18f,  1f);
    static readonly Color AccentBlue   = new Color(0.40f,  0.72f,  1.00f,  1f);
    static readonly Color TagColor     = new Color(0.62f,  0.82f,  1.00f,  1f);
    static readonly Color TitleColor   = new Color(1.00f,  0.94f,  0.78f,  1f);
    static readonly Color TitleShadow  = new Color(0.02f,  0.03f,  0.06f,  0.85f);

    static readonly Color ColumnBg     = new Color(0.095f, 0.10f,  0.165f, 1f);
    static readonly Color PitBg        = new Color(0.055f, 0.06f,  0.10f,  1f);
    static readonly Color DividerLine  = new Color(0.26f,  0.28f,  0.40f,  0.85f);
    static readonly Color DividerLabel = new Color(0.70f,  0.74f,  0.88f,  1f);
    static readonly Color LabelColor   = new Color(0.60f,  0.62f,  0.74f,  1f);
    static readonly Color RowLine      = new Color(0.18f,  0.19f,  0.28f,  1f);

    static readonly Color SkillBoxBg   = new Color(0.115f, 0.125f, 0.215f, 1f);

    // ⚠ 스킬 아이콘 홈은 PitBg 를 쓰지 말 것
    //   PitBg 는 초상화 홈(BuildLeftColumn)도 같이 쓴다.
    //   그쪽까지 바꾸면 초상화 배경이 같이 밝아진다.
    //   색의 정본은 SkillIconUI — 로비 카드·인게임과 같은 바탕을 쓴다.
    static readonly Color ActPitBg     = SkillIconUI.ActiveSlotBg;
    static readonly Color PassPitBg    = SkillIconUI.PassiveSlotBg;
    static readonly Color ActiveAccent = new Color(0.45f,  0.65f,  1.00f,  1f);
    static readonly Color PassAccent   = new Color(0.60f,  0.44f,  0.90f,  1f);
    // 이름 = 채도 높은 강조색(액티브 금 / 패시브 보라), 설명 = 채도 낮은 청회색.
    // 종류(금↔보라)와 역할(이름↔설명)이 색만 보고도 갈린다.
    // 예전엔 액티브 설명(0.80,0.82,0.92)과 패시브 이름(0.92,0.88,1.00)이 거의 같은 색이었다.
    static readonly Color ActNameC     = new Color(1.00f,  0.88f,  0.52f,  1f);   // 금
    static readonly Color ActDescC     = new Color(0.66f,  0.72f,  0.86f,  1f);   // 청회
    static readonly Color PassNameC    = new Color(0.80f,  0.66f,  1.00f,  1f);   // 보라
    static readonly Color PassDescC    = new Color(0.60f,  0.63f,  0.76f,  1f);   // 어두운 청회

    // 스킬 아이콘 테두리 — Frame→Pit→Icon 구조.
    // ⚠ 테두리 없이 홈(Pit)에 아이콘만 얹으면 칸으로 안 읽힌다.
    // 색은 이름 색과 같은 정체성(액티브 금 / 패시브 보라)을 쓰되 채도를 낮춘다 —
    // 테두리가 아이콘보다 먼저 눈에 들어오면 안 된다.
    static readonly Color ActFrameC    = new Color(0.60f,  0.48f,  0.22f,  1f);
    static readonly Color PassFrameC   = new Color(0.40f,  0.32f,  0.58f,  1f);

    static readonly Color CloseBtnC    = new Color(0.50f,  0.14f,  0.14f,  1f);
    static readonly Color TabActiveC   = new Color(0.42f,  0.74f,  1.00f,  1f);
    static readonly Color TabIdleC     = new Color(0.58f,  0.60f,  0.72f,  1f);
    // 탭 바탕 — 아래 스탯 목록(ColumnBg)보다 밝게 잡아 "여기부터 목록"이 구분되게 한다
    static readonly Color TabFaceOn    = new Color(0.20f,  0.38f,  0.62f,  1f);
    static readonly Color TabFaceOff   = new Color(0.15f,  0.16f,  0.25f,  1f);

    // ══════════════════════════════════════════════════════════
    //  진입점
    // ══════════════════════════════════════════════════════════

    // ⚠ 올드Tools 에서 이관했다 (사용자 지시, 2026-09-07)
    //   이 게임에서 쓰지 않던 원작 화면이었는데, 전황(BattleInfoPopup)이
    //   적을 눌렀을 때 이 창을 연다. 쓰는 도구는 Tools/Project K 아래에 둔다
    //   (ProjectKMenu 루트 규칙).
    //   ⚠ SetupHero 로 열린다 — 용사는 세이브 기록이 없어 이름 시드로 만든
    //     UnitEntry 를 받는다.
    [MenuItem(ProjectKMenu.Popup + "HeroDetail", priority = ProjectKMenu.PrefabPrio + 55)]
    public static void Create()
    {
        Directory.CreateDirectory("Assets/_project/2.Prefabs/UI");
        AssetDatabase.Refresh();

        var go = BuildPopup();
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.Refresh();
        Debug.Log($"[HeroDetailPopupCreator] 저장: {PrefabPath} — PopupManager > Load Popup Prefabs 실행 필요.");
    }

    static GameObject BuildPopup()
    {
        // 루트 = 전체 화면 어둡게
        var root = CreatePanel(null, "HeroDetailPopup", BgOverlay);
        Stretch(root);
        var popup = root.AddComponent<HeroDetailPopup>();
        var so    = new SerializedObject(popup);
        SetEnum(so, "_popupType", (int)PopupType.HeroDetail);

        // 테두리는 Panel 의 앞 형제 — 자식으로 두면 팝업 전체를 덮는다
        var border = Go("Border", root);
        border.AddComponent<Image>().color = PanelBorder;
        CenterBox(border, PW + 6f, PH + 6f);

        var panel = Go("Panel", root);
        panel.AddComponent<Image>().color = PanelBg;
        CenterBox(panel, PW, PH);

        BuildHeader(panel, so);

        float x = SidePad * 0.5f;
        BuildLeftColumn (panel, so, x);                       x += LeftW + ColGap;
        BuildStatColumn (panel, so, x);                       x += MidW  + ColGap;
        BuildSkillColumn(panel, so, x);

        so.ApplyModifiedProperties();
        return root;
    }

    // ══════════════════════════════════════════════════════════
    //  헤더 — ◆ 장 수 태그 + 이름(동적) + 닫기
    // ══════════════════════════════════════════════════════════

    static void BuildHeader(GameObject panel, SerializedObject so)
    {
        var header = Go("Header", panel);
        header.AddComponent<Image>().color = HeaderBg;
        AnchorTop(header, 0f, HeaderH);

        // ★ 는 폰트에 없다 (□ 로 렌더됨) → 마름모 도형으로 대체
        var tagRoot = Go("HeroTag", header);
        var tagRt = tagRoot.GetComponent<RectTransform>();
        tagRt.anchorMin = tagRt.anchorMax = new Vector2(0f, 1f);
        tagRt.pivot     = new Vector2(0f, 1f);
        tagRt.anchoredPosition = new Vector2(30f, -14f);
        tagRt.sizeDelta        = new Vector2(300f, 34f);

        var diamond = EditorUIBuilder.Diamond(tagRoot, "Mark", 16f, TagColor);
        var dRt = diamond.GetComponent<RectTransform>();
        dRt.anchorMin = dRt.anchorMax = new Vector2(0f, 0.5f);
        dRt.anchoredPosition = new Vector2(10f, 0f);

        var tagTmp = TMP(tagRoot, "Label", "장 수", UIScale.FontSm, FontStyles.Bold);
        tagTmp.color         = TagColor;
        tagTmp.alignment     = TextAlignmentOptions.Left;
        tagTmp.raycastTarget = false;
        var tlRt = tagTmp.rectTransform;
        tlRt.anchorMin = Vector2.zero; tlRt.anchorMax = Vector2.one;
        tlRt.offsetMin = new Vector2(30f, 0f); tlRt.offsetMax = Vector2.zero;

        // 이름 — 그림자 사본을 먼저 깔아 어떤 배경에서도 읽히게 한다
        // ⚠ 그림자도 반드시 연결한다. 안 하면 프리팹 플레이스홀더("영웅 이름")가
        //   실제 이름 옆에 검은 글씨로 그대로 보인다.
        var shadowTmp = MakeTitle(header, "NameShadow", TitleShadow, 3f);
        var nameTmp   = MakeTitle(header, "NameText",   TitleColor,  0f);
        SetObj(so, "_nameText",       nameTmp);
        SetObj(so, "_nameShadowText", shadowTmp);

        // ── 지휘력 안내 (이름 오른쪽 빈 자리) ────────────────
        //
        //  지휘력은 이 화면에서 가장 설명이 필요한 스탯이다. 숫자만 봐서는
        //  34 라는 값이 무엇을 하는지 알 수 없다.
        //
        //  ⚠ 자리 계산 — 이름과 닫기 버튼 사이에만 놓는다
        //    이름  : x=30 부터, FontLg(56). 용사 이름은 한글 3~6자라 450 안쪽이다.
        //    ⚠ 이름 칸은 900 폭으로 잡혀 있지만 NoWrap + 좌측 정렬 + Overflow 라
        //      실제로 그리는 폭은 글자 길이만큼이다. 그 칸을 줄이면 긴 이름이 잘린다.
        //  ⚠ 문구의 수치는 여기에 박지 않는다
        //    GameplayConfig.SoldierRatioPerCommandPower 가 바뀌면 이 글이 거짓말이
        //    된다. 런타임에 HeroDetailPopup 이 채운다.
        const float HintX    = 480f;
        const float MarkSize = 26f;
        var  hintColor = new Color(0.62f, 0.72f, 0.88f);
        float hintW    = PW - HintX - EditorUIBuilder.HeaderRightBlock(76f, 24f) - 32f;

        // 당구장 표시 — ※ 는 폰트에 없어서 도형으로 그린다 (UI 규칙 2)
        var mark = EditorUIBuilder.ReferenceMark(header, "CommandHintMark", MarkSize, hintColor);
        var mkRt = mark.GetComponent<RectTransform>();
        mkRt.anchorMin = mkRt.anchorMax = new Vector2(0f, 1f);
        mkRt.pivot     = new Vector2(0f, 1f);
        mkRt.anchoredPosition = new Vector2(HintX, -58f - (UIScale.RowMd - MarkSize) * 0.5f);

        var cmdHint = TMP(header, "CommandHintText", "", UIScale.FontSm, FontStyles.Normal);
        cmdHint.color            = hintColor;
        cmdHint.alignment        = TextAlignmentOptions.MidlineLeft;
        cmdHint.raycastTarget    = false;
        cmdHint.textWrappingMode = TextWrappingModes.NoWrap;
        cmdHint.overflowMode     = TextOverflowModes.Ellipsis;
        var chRt = cmdHint.rectTransform;
        chRt.anchorMin = chRt.anchorMax = new Vector2(0f, 1f);
        chRt.pivot     = new Vector2(0f, 1f);
        chRt.anchoredPosition = new Vector2(HintX + MarkSize + 8f, -58f);
        chRt.sizeDelta        = new Vector2(hintW - MarkSize - 8f, UIScale.RowMd);
        SetObj(so, "_commandHintText", cmdHint);

        var accent = Go("AccentLine", panel);
        accent.AddComponent<Image>().color = AccentBlue;
        AnchorTop(accent, HeaderH, 3f);

        // 닫기
        var closeBtn = EditorUIBuilder.RaisedBtn(header, "CloseBtn", CloseBtnC, out var body);
        AnchorRight(closeBtn.gameObject, -24f, 76f, 76f);
        Center(EditorUIBuilder.XMark(body, "Mark", UIScale.FontMd, Color.white));
        SetObj(so, "_closeBtn", closeBtn);
    }

    static TextMeshProUGUI MakeTitle(GameObject header, string name, Color color, float dy)
    {
        var tmp = TMP(header, name, "영웅 이름", UIScale.FontLg, FontStyles.Bold);
        tmp.color            = color;
        tmp.alignment        = TextAlignmentOptions.Left;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode     = TextOverflowModes.Overflow;
        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(30f + dy, -52f - dy);
        rt.sizeDelta        = new Vector2(900f, UIScale.RowLg);
        return tmp;
    }

    // ══════════════════════════════════════════════════════════
    //  좌측 열 — 초상화 / 직업 · 등급
    // ══════════════════════════════════════════════════════════

    static void BuildLeftColumn(GameObject panel, SerializedObject so, float x)
    {
        var col = Column(panel, "LeftColumn", x, LeftW);

        // ── 초상화 (좌) ──────────────────────────────────────
        var portraitPit = Go("PortraitPit", col);
        portraitPit.AddComponent<Image>().color = PitBg;
        var ppRt = portraitPit.GetComponent<RectTransform>();
        ppRt.anchorMin = ppRt.anchorMax = new Vector2(0f, 1f);
        ppRt.pivot     = new Vector2(0f, 1f);
        ppRt.anchoredPosition = new Vector2(12f, -12f);
        ppRt.sizeDelta        = new Vector2(PortraitSize, PortraitSize);

        var gradeBorder = Go("GradeBorder", portraitPit);
        gradeBorder.AddComponent<Image>().color = DividerLine;
        Stretch(gradeBorder);
        SetObj(so, "_gradeBorder", gradeBorder.GetComponent<Image>());

        var portraitBg = Go("PortraitBg", portraitPit);
        portraitBg.AddComponent<Image>().color = new Color(0.16f, 0.27f, 0.56f);
        var pbRt = portraitBg.GetComponent<RectTransform>();
        pbRt.anchorMin = Vector2.zero; pbRt.anchorMax = Vector2.one;
        pbRt.offsetMin = new Vector2(4f, 4f); pbRt.offsetMax = new Vector2(-4f, -4f);
        SetObj(so, "_portraitBg", portraitBg.GetComponent<Image>());

        var portraitImg = Go("PortraitImage", portraitPit, typeof(Image)).GetComponent<Image>();
        portraitImg.color          = Color.clear;
        portraitImg.preserveAspect = true;
        portraitImg.raycastTarget  = false;
        var piRt = portraitImg.rectTransform;
        piRt.anchorMin = piRt.anchorMax = new Vector2(0.5f, 0.5f);
        piRt.sizeDelta = new Vector2(PortraitSize - 60f, PortraitSize - 60f);
        SetObj(so, "_portraitImage", portraitImg);

        var preview = Go("PortraitPreview", portraitPit);
        preview.SetActive(false);
        var bridge = preview.AddComponent<UnitAppearanceBridge>();
        if (preview.TryGetComponent<CharacterBuilder>(out var builder))
        {
            var sc = AssetDatabase.LoadAssetAtPath<SpriteCollection>(
                "Assets/PixelFantasy/PixelHeroes/FantasyHeroes/Resources/SpriteCollection.asset");
            if (sc != null) builder.SpriteCollection = sc;
        }
        SetObj(so, "_portraitBridge", bridge);

        // ── 기본 정보 행: 직업 · 등급 ────────────────────────
        float infoY = 12f + PortraitSize + 20f;

        var jobTmp = TMP(col, "JobText", "기사", UIScale.FontMd, FontStyles.Normal);
        jobTmp.alignment     = TextAlignmentOptions.Center;
        jobTmp.color         = Color.white;
        jobTmp.raycastTarget = false;
        AnchorTop(jobTmp.gameObject, infoY, InfoRowH, 24f);

        var gradeBadge = Go("GradeBadge", col);
        gradeBadge.AddComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f);
        var gbRt = gradeBadge.GetComponent<RectTransform>();
        gbRt.anchorMin = gbRt.anchorMax = new Vector2(1f, 1f);
        gbRt.pivot     = new Vector2(1f, 1f);
        gbRt.anchoredPosition = new Vector2(-12f, -infoY);
        gbRt.sizeDelta        = new Vector2(120f, InfoRowH);

        var gradeTmp = TMP(gradeBadge, "Label", "일반", UIScale.FontSm, FontStyles.Bold);
        gradeTmp.alignment     = TextAlignmentOptions.Center;
        gradeTmp.raycastTarget = false;
        Stretch(gradeTmp.gameObject);

        // 배지를 눌러 등급·품질 설명을 본다.
        //
        //  ⚠ UI 규칙 1(음각 RaisedBtn)의 예외다
        //    이 배지는 등급색으로 칠해지는 '정보 표시' 다. RaisedBtn 으로 바꾸면
        //    눌림 색이 targetGraphic 색 기준으로 역산돼 있는데, 런타임에 등급색을
        //    갈아끼우므로 그 계산이 통째로 어긋난다 (칸 배경을 등급색으로 칠하지
        //    말라는 MercenaryPopupCreator 의 경고와 같은 이유).
        var gradeBtn = gradeBadge.AddComponent<Button>();
        gradeBtn.targetGraphic = gradeBadge.GetComponent<Image>();

        // 누를 수 있다는 표시 — 배지 오른쪽 위 모서리에 걸친 작은 ⓘ.
        //
        //  ⚠ 라벨에 '?' 를 이어 붙이면 안 된다
        //    배지는 120×56 인데 "영웅 5 ?" 는 그 폭을 넘겨 두 줄로 접힌다.
        //    등급·품질이 아래위로 갈라져 하나의 값처럼 읽히지 않았다.
        //    표시는 글자 흐름 밖(모서리)으로 빼야 라벨 길이에 영향을 주지 않는다.
        //  ⚠ 배지 밖으로 나가는 양은 오른쪽 여백(12) 안에서 끝낸다
        //    ⓘ 는 raycastTarget = false 라, 눌리는 것은 여전히 배지 전체다.
        const float InfoDotSz = 28f;
        var gradeDot = EditorUIBuilder.InfoDot(gradeBadge, "InfoDot", InfoDotSz,
                                               new Color(0.30f, 0.44f, 0.66f, 0.98f));
        var gdRt = gradeDot.GetComponent<RectTransform>();
        gdRt.anchorMin = gdRt.anchorMax = new Vector2(1f, 1f);
        gdRt.pivot     = new Vector2(0.5f, 0.5f);
        gdRt.anchoredPosition = new Vector2(-4f, 4f);

        // 툴팁 부모는 칼럼 — 배지(120×56) 안에 두면 설명이 통째로 찌그러진다
        var gradeTip = InfoTooltipBuilder.Build(col, 460f);

        SetObj(so, "_jobText",       jobTmp);
        SetObj(so, "_gradeBadge",    gradeBadge.GetComponent<Image>());
        SetObj(so, "_gradeText",     gradeTmp);
        SetObj(so, "_gradeInfoBtn",  gradeBtn);
        SetObj(so, "_gradeTooltip",  gradeTip);
    }
    //
    //  ⚠ Frame(장비 네모)은 아이콘 영역만 차지한다 — 강화 버튼은 그 아래 별개다
    //    빈 칸이면 버튼이 꺼지면서 아래가 비는데, 그 자리를 프레임으로 메우지 말 것.
    //    "장비가 비었다" 를 나타내는 네모가 강화 버튼 자리까지 덮으면
    //    버튼까지 장비 칸의 일부로 읽힌다. 세 칸의 네모 크기는 항상 같다.

    // ══════════════════════════════════════════════════════════
    //  가운데 열 — 스탯 9행
    // ══════════════════════════════════════════════════════════

    static void BuildStatColumn(GameObject panel, SerializedObject so, float x)
    {
        var col = Column(panel, "StatColumn", x, MidW);
        BuildDivider(col, 12f, "스  탯");

        // ── 장수 / 용병 토글 ────────────────────────────────
        //  용병은 장수 스탯 × 배율이라 같은 행을 다시 쓴다 (SoldierRuntimeBridge 공식).
        float tabY = 12f + DivH + 10f;
        var generalTab = BuildStatTab(col, "GeneralTab", "장 수", 0f,    0.5f, tabY, true);
        var soldierTab = BuildStatTab(col, "SoldierTab", "용 병", 0.5f, 1f,   tabY, false);
        SetObj(so, "_generalTabBtn", generalTab);
        SetObj(so, "_soldierTabBtn", soldierTab);

        var list = Go("StatListContainer", col);
        var rt = list.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(10f, 10f);
        rt.offsetMax = new Vector2(-10f, -StatListTop);


        // ⚠ 레이아웃 그룹을 쓰지 않는다.
        //   용병 탭에서 3줄이 빠지면 남은 줄이 재배치돼 "사거리가 어디 갔지" 가 된다.
        //   행마다 index × StatRowH 로 못 박아 두면 어떤 탭에서도 같은 자리에 있다.
        int rowIndex = 0;

        SetObj(so, "_hpText",     Tint(StatRow(list, "HP",   "체력",   rowIndex++), StatColors.Hp));
        SetObj(so, "_atkText",    Tint(StatRow(list, "ATK",  "공격",   rowIndex++), StatColors.Atk));
        SetObj(so, "_defText",    Tint(StatRow(list, "DEF",  "방어율", rowIndex++), StatColors.Def));

        // 용병 수는 네 번째 — 장수를 고르는 기준이 되는 값이라 위쪽에 둔다.
        // ⚠ 장수 전용 행이라 용병 탭에서는 이 자리가 빈다 (아래 _generalOnlyRows 참고)
        var soldierCnt = Tint(StatRow(list, "SOLD", "용병 수",   rowIndex++), StatColors.Soldier);

        SetObj(so, "_spdText",         StatRow(list, "SPD",  "이동속도", rowIndex++));
        SetObj(so, "_atkSpdText",      StatRow(list, "ASPD", "공격속도", rowIndex++));
        SetObj(so, "_rangeText",       StatRow(list, "RNG",  "사거리",   rowIndex++));

        var cmdPwr   = StatRow(list, "CMD", "지휘력", rowIndex++);
        // 라벨은 짧게 — "스킬 쿨타임" 은 라벨 칸을 넘겨 값(3.3%) 위로 밀고 들어왔다
        var cooldown = StatRow(list, "CD",  "쿨타임", rowIndex++);

        // 치명타 2행 — 맨 아래. 용병 탭에서도 보인다.
        //   치확은 환산율이 곱해지고 치피는 그대로다 (SoldierRuntimeBridge.IsUnscaled).
        //   두 값이 한 화면에 붙어 있어야 그 차이가 읽힌다.
        SetObj(so, "_critChanceText", StatRow(list, "CRIT",  "치명확률", rowIndex++));
        SetObj(so, "_critDmgText",    StatRow(list, "CRITD", "치명피해", rowIndex++));

        SetObj(so, "_soldierCountText", soldierCnt);
        SetObj(so, "_cmdPwrText",       cmdPwr);
        SetObj(so, "_cooldownText",     cooldown);
        SetObjArray(so, "_generalOnlyRows", new Object[]
        {
            soldierCnt.transform.parent.gameObject,
            cmdPwr    .transform.parent.gameObject,
            cooldown  .transform.parent.gameObject,
        });

    }

    //  탭 버튼 — 입체(음각)로 만들어 "누를 수 있다"를 먼저 읽히게 한다.
    //  선택 상태는 Body 색 + 밑줄로 표시한다 (HeroDetailPopup.StyleStatTab 이 런타임에 갱신).
    static Button BuildStatTab(GameObject col, string name, string label,
                               float aMinX, float aMaxX, float y, bool active)
    {
        var btn = EditorUIBuilder.RaisedBtn(col, name,
                                            active ? TabFaceOn : TabFaceOff, out var body, 4f);

        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(aMinX, 1f);
        rt.anchorMax = new Vector2(aMaxX, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(aMinX > 0f ? 4f : 10f, -(y + StatTabH));
        rt.offsetMax = new Vector2(aMaxX < 1f ? -4f : -10f, -y);

        var tmp = TMP(body, "Label", label, UIScale.FontMd,
                      active ? FontStyles.Bold : FontStyles.Normal);
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = active ? TabActiveC : TabIdleC;
        tmp.raycastTarget = false;
        Stretch(tmp.gameObject);

        var bar = Go("ActiveBar", body, typeof(Image));
        var barImg = bar.GetComponent<Image>();
        barImg.color         = TabActiveC;
        barImg.raycastTarget = false;
        var bRt = bar.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.14f, 0f); bRt.anchorMax = new Vector2(0.86f, 0f);
        bRt.offsetMin = new Vector2(0f, 4f);    bRt.offsetMax = new Vector2(0f, 8f);
        bar.SetActive(active);

        return btn;
    }

    //  ⚠ 값 TMP 는 Ellipsis 금지 (UI 규칙 5)
    //    칸보다 긴 분해 문자열이 통째로 "..." 으로 바뀌어 숫자가 사라졌다.
    //    AutoSize 로 줄여서 담고, 칸 높이는 FontMd 한 줄보다 넉넉히 잡는다.
    static TextMeshProUGUI StatRow(GameObject parent, string id, string label, int index)
    {
        var row = Go($"Stat_{id}", parent, typeof(Image), typeof(Button));
        AnchorTop(row, index * StatRowH, StatRowH);
        row.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
        var btn = row.GetComponent<Button>();
        btn.targetGraphic = row.GetComponent<Image>();
        btn.transition    = Selectable.Transition.None;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.padding                = new RectOffset(14, 14, 0, 0);
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        var lbl = TMP(row, "Label", label, UIScale.FontMd, FontStyles.Normal);
        lbl.alignment        = TextAlignmentOptions.MidlineLeft;
        lbl.color            = LabelColor;
        lbl.raycastTarget    = false;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        lbl.overflowMode     = TextOverflowModes.Overflow;
        var lblLe = lbl.gameObject.AddComponent<LayoutElement>();
        lblLe.preferredWidth = 190f;
        lblLe.flexibleWidth  = 0f;

        var val = TMP(row, "Value", "—", UIScale.FontMd, FontStyles.Bold);
        val.alignment        = TextAlignmentOptions.MidlineRight;
        val.raycastTarget    = false;
        val.textWrappingMode = TextWrappingModes.NoWrap;
        val.overflowMode     = TextOverflowModes.Overflow;
        val.enableAutoSizing = true;
        val.fontSizeMin      = UIScale.FontSm - 8f;
        val.fontSizeMax      = UIScale.FontMd;
        val.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var line = Go("Divider", row, typeof(Image));
        line.GetComponent<Image>().color = RowLine;
        line.GetComponent<Image>().raycastTarget = false;
        line.AddComponent<LayoutElement>().ignoreLayout = true;
        var lRt = line.GetComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0f, 0f); lRt.anchorMax = new Vector2(1f, 0f);
        lRt.offsetMin = new Vector2(8f, 0f); lRt.offsetMax = new Vector2(-8f, 1f);

        return val;
    }

    // ══════════════════════════════════════════════════════════
    //  우측 열 — 스킬 (액티브 1 + 패시브 3)
    // ══════════════════════════════════════════════════════════

    static void BuildSkillColumn(GameObject panel, SerializedObject so, float x)
    {
        var col = Column(panel, "SkillColumn", x, RightW);
        BuildDivider(col, 12f, "스  킬");

        const float ActiveY = 60f;
        const float ActiveH = ActiveBoxH;
        const float IconSz  = 128f;

        // ── 액티브 스킬 ──────────────────────────────────────
        var box = Go("ActiveBox", col);
        box.AddComponent<Image>().color = SkillBoxBg;
        AnchorTop(box, ActiveY, ActiveH, 24f);

        var accent = Go("AccentBar", box);
        var accImg = accent.AddComponent<Image>();
        accImg.color = ActiveAccent;
        accImg.raycastTarget = false;
        var aRt = accent.GetComponent<RectTransform>();
        aRt.anchorMin = new Vector2(0f, 0f); aRt.anchorMax = new Vector2(0f, 1f);
        aRt.offsetMin = Vector2.zero;        aRt.offsetMax = new Vector2(6f, 0f);

        // 테두리 → 홈 → 아이콘 (장비 타일과 같은 구조)
        var iconFrame = Go("IconFrame", box);
        var ifImg = iconFrame.AddComponent<Image>();
        ifImg.color         = ActFrameC;
        ifImg.raycastTarget = false;
        var ifRt = iconFrame.GetComponent<RectTransform>();
        ifRt.anchorMin = ifRt.anchorMax = new Vector2(0f, 1f);
        ifRt.pivot     = new Vector2(0f, 1f);
        ifRt.anchoredPosition = new Vector2(20f, -20f);
        ifRt.sizeDelta        = new Vector2(IconSz, IconSz);

        var iconPit = Go("IconPit", iconFrame);
        var ipImg = iconPit.AddComponent<Image>();
        ipImg.color         = ActPitBg;
        ipImg.raycastTarget = false;
        var ipRt = iconPit.GetComponent<RectTransform>();
        ipRt.anchorMin = Vector2.zero; ipRt.anchorMax = Vector2.one;
        ipRt.offsetMin = new Vector2(3f, 3f); ipRt.offsetMax = new Vector2(-3f, -3f);

        var skillIcon = Go("ActiveSkillIcon", iconPit, typeof(Image)).GetComponent<Image>();
        skillIcon.color          = new Color(0.28f, 0.32f, 0.52f);
        skillIcon.preserveAspect = true;
        skillIcon.raycastTarget  = false;
        var siRt = skillIcon.rectTransform;
        siRt.anchorMin = Vector2.zero; siRt.anchorMax = Vector2.one;
        siRt.offsetMin = new Vector2(5f, 5f); siRt.offsetMax = new Vector2(-5f, -5f);
        SetObj(so, "_activeSkillIcon", skillIcon);

        float textLeft = 20f + IconSz + 18f;

        var nameTmp = TMP(box, "ActiveSkillText", "—", UIScale.FontLg, FontStyles.Bold);
        nameTmp.color            = ActNameC;
        nameTmp.alignment        = TextAlignmentOptions.MidlineLeft;
        nameTmp.raycastTarget    = false;
        nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
        nameTmp.overflowMode     = TextOverflowModes.Overflow;
        var nRt = nameTmp.rectTransform;
        nRt.anchorMin = new Vector2(0f, 1f); nRt.anchorMax = new Vector2(1f, 1f);
        nRt.pivot     = new Vector2(0.5f, 1f);
        nRt.offsetMin = new Vector2(textLeft, -(18f + UIScale.RowLg));
        nRt.offsetMax = new Vector2(-20f, -18f);
        SetObj(so, "_activeSkillText", nameTmp);

        // 설명 — 남는 칸에 맞춰 줄어든다.
        // ⚠ 예전엔 FontMd(42) + lineSpacing 12 고정이라 한 줄이 64px 였는데
        //   칸은 108px 뿐이라 1.7줄 밖에 못 담았고, Overflow 라서 넘친 줄이
        //   상자 밖(배경 바깥)에 그대로 그려졌다.
        //   AutoSize 를 켜면 넘치는 대신 글자가 작아진다.
        var descTmp = TMP(box, "ActiveSkillDescText", "", UIScale.FontMd, FontStyles.Normal);
        descTmp.color            = ActDescC;
        descTmp.alignment        = TextAlignmentOptions.TopLeft;
        descTmp.raycastTarget    = false;
        descTmp.textWrappingMode = TextWrappingModes.Normal;
        descTmp.overflowMode     = TextOverflowModes.Overflow;
        descTmp.lineSpacing      = 6f;
        descTmp.enableAutoSizing = true;
        descTmp.fontSizeMin      = UIScale.FontSm - 8f;
        descTmp.fontSizeMax      = UIScale.FontMd;
        var dRt = descTmp.rectTransform;
        dRt.anchorMin = new Vector2(0f, 0f); dRt.anchorMax = new Vector2(1f, 1f);
        dRt.offsetMin = new Vector2(textLeft, 16f);
        dRt.offsetMax = new Vector2(-20f, -(18f + UIScale.RowLg + 6f));
        SetObj(so, "_activeSkillDescText", descTmp);

        // ── 패시브 3칸 ───────────────────────────────────────
        float passY = ActiveY + ActiveH + 20f;

        var cont = Go("PassiveContainer", col);
        var cRt = cont.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 0f); cRt.anchorMax = new Vector2(1f, 1f);
        cRt.offsetMin = new Vector2(12f, 12f);
        cRt.offsetMax = new Vector2(-12f, -passY);

        var vlg = cont.AddComponent<VerticalLayoutGroup>();
        vlg.spacing                = 12f;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        // 패시브 칸 수는 등급마다 다르다(1~3). 늘리게 두면 1칸짜리 노멀 등급에서
        // 상자 하나가 열 전체로 부풀어 빈 공간처럼 보인다 — 고정 높이로 둔다.
        vlg.childForceExpandHeight = false;
        vlg.childAlignment         = TextAnchor.UpperLeft;

        var boxes = new Object[3];
        var icons = new Object[3];
        var names = new Object[3];
        var descs = new Object[3];
        for (int i = 0; i < 3; i++)
        {
            var (boxGo, iconI, nameT, descT) = BuildPassiveBox(cont, i);
            boxes[i] = boxGo; icons[i] = iconI; names[i] = nameT; descs[i] = descT;
        }
        SetObjArray(so, "_passiveBoxes",     boxes);
        SetObjArray(so, "_passiveIcons",     icons);
        SetObjArray(so, "_passiveNameTexts", names);
        SetObjArray(so, "_passiveDescTexts", descs);
    }

    //  [액센트바][아이콘 96][이름 / 설명]
    //  아이콘이 붙으면서 글자 폭이 96+여백 만큼 줄었다 —
    //  이름은 NoWrap, 설명은 AutoSize 라 넘치지 않는다.
    static (GameObject box, Image icon, TextMeshProUGUI name, TextMeshProUGUI desc)
        BuildPassiveBox(GameObject cont, int index)
    {
        const float IconSz  = 96f;
        const float IconX   = 20f;
        float textLeft = IconX + IconSz + 18f;   // 134

        var box = Go($"Passive{index}Box", cont);
        box.AddComponent<Image>().color = SkillBoxBg;
        box.AddComponent<LayoutElement>().preferredHeight = PassiveBoxH;

        var accent = Go("AccentBar", box);
        var accImg = accent.AddComponent<Image>();
        accImg.color = PassAccent;
        accImg.raycastTarget = false;
        var aRt = accent.GetComponent<RectTransform>();
        aRt.anchorMin = new Vector2(0f, 0f); aRt.anchorMax = new Vector2(0f, 1f);
        aRt.offsetMin = Vector2.zero;        aRt.offsetMax = new Vector2(6f, 0f);

        // 아이콘 자리 — 액티브와 같은 테두리 → 홈 → 아이콘 구조
        var frame = Go("IconFrame", box);
        var frImg = frame.AddComponent<Image>();
        frImg.color         = PassFrameC;
        frImg.raycastTarget = false;
        var fRt = frame.GetComponent<RectTransform>();
        fRt.anchorMin = fRt.anchorMax = new Vector2(0f, 0.5f);
        fRt.pivot     = new Vector2(0f, 0.5f);
        fRt.anchoredPosition = new Vector2(IconX, 0f);
        fRt.sizeDelta        = new Vector2(IconSz, IconSz);

        var pit = Go("IconPit", frame);
        var pitImg = pit.AddComponent<Image>();
        pitImg.color         = PassPitBg;
        pitImg.raycastTarget = false;
        var pRt = pit.GetComponent<RectTransform>();
        pRt.anchorMin = Vector2.zero; pRt.anchorMax = Vector2.one;
        pRt.offsetMin = new Vector2(3f, 3f); pRt.offsetMax = new Vector2(-3f, -3f);

        var icon = Go("PassiveIcon", pit, typeof(Image)).GetComponent<Image>();
        icon.color          = new Color(0.25f, 0.24f, 0.40f);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        var iRt = icon.rectTransform;
        iRt.anchorMin = Vector2.zero; iRt.anchorMax = Vector2.one;
        iRt.offsetMin = new Vector2(4f, 4f); iRt.offsetMax = new Vector2(-4f, -4f);

        var nameTmp = TMP(box, "NameText", "—", UIScale.FontMd, FontStyles.Bold);
        nameTmp.color            = PassNameC;
        nameTmp.alignment        = TextAlignmentOptions.MidlineLeft;
        nameTmp.raycastTarget    = false;
        nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
        nameTmp.overflowMode     = TextOverflowModes.Overflow;
        var nRt = nameTmp.rectTransform;
        nRt.anchorMin = new Vector2(0f, 1f); nRt.anchorMax = new Vector2(1f, 1f);
        nRt.pivot     = new Vector2(0.5f, 1f);
        nRt.offsetMin = new Vector2(textLeft, -(12f + UIScale.RowMd));
        nRt.offsetMax = new Vector2(-16f, -12f);

        var descTmp = TMP(box, "DescText", "", UIScale.FontSm, FontStyles.Normal);
        descTmp.color            = PassDescC;
        descTmp.alignment        = TextAlignmentOptions.TopLeft;
        descTmp.raycastTarget    = false;
        descTmp.textWrappingMode = TextWrappingModes.Normal;
        descTmp.overflowMode     = TextOverflowModes.Overflow;
        descTmp.lineSpacing      = 4f;
        descTmp.enableAutoSizing = true;
        descTmp.fontSizeMin      = UIScale.FontSm - 12f;
        descTmp.fontSizeMax      = UIScale.FontSm;
        var dRt = descTmp.rectTransform;
        dRt.anchorMin = new Vector2(0f, 0f); dRt.anchorMax = new Vector2(1f, 1f);
        dRt.offsetMin = new Vector2(textLeft, 10f);
        dRt.offsetMax = new Vector2(-16f, -(12f + UIScale.RowMd + 4f));

        return (box, icon, nameTmp, descTmp);
    }

    // ══════════════════════════════════════════════════════════
    //  공통 조각
    // ══════════════════════════════════════════════════════════

    // 본문 3단의 열 하나 — 좌표 x 부터 폭 w, 세로는 본문 전체
    static GameObject Column(GameObject panel, string name, float x, float w)
    {
        var col = Go(name, panel);
        col.AddComponent<Image>().color = ColumnBg;
        var rt = col.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -BodyTop);
        rt.sizeDelta        = new Vector2(w, BodyH);
        return col;
    }

    //  섹션 구분선 — 가운데 글자 + 좌우 라인 (EventPopup 의 "선 택" 과 같은 형태)
    static void BuildDivider(GameObject col, float y, string label)
    {
        var div = Go($"Divider_{label}", col);
        AnchorTop(div, y, DivH, 32f);

        DivLine(div, "LineL", 0f,   0.5f,   0f, -80f);
        DivLine(div, "LineR", 0.5f, 1f,  80f,    0f);

        var tmp = TMP(div, "Label", label, UIScale.FontSm, FontStyles.Bold);
        tmp.color         = DividerLabel;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        Stretch(tmp.gameObject);
    }

    static void DivLine(GameObject parent, string name,
                        float aMinX, float aMaxX, float offMinX, float offMaxX)
    {
        var go = Go(name, parent, typeof(Image));
        var img = go.GetComponent<Image>();
        img.color         = DividerLine;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(aMinX, 0.5f);
        rt.anchorMax = new Vector2(aMaxX, 0.5f);
        rt.offsetMin = new Vector2(offMinX, -1f);
        rt.offsetMax = new Vector2(offMaxX,  1f);
    }

    static TextMeshProUGUI Tint(TextMeshProUGUI tmp, Color c) { tmp.color = c; return tmp; }

    // ── 레이아웃 헬퍼 ────────────────────────────────────────

    static void CenterBox(GameObject go, float w, float h)
        => EditorUIBuilder.Center(go.GetComponent<RectTransform>(), Vector2.zero, new Vector2(w, h));

    static void AnchorTop(GameObject go, float yFromTop, float height, float padH = 0f)
        => EditorUIBuilder.AnchorTop(go.GetComponent<RectTransform>(), yFromTop, height, padH);

    static void AnchorBottom(GameObject go, float yFromBottom, float height, float padH = 0f)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(padH * 0.5f, yFromBottom);
        rt.offsetMax = new Vector2(-padH * 0.5f, yFromBottom + height);
    }

    static void AnchorRight(GameObject go, float rightX, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot     = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(rightX, 0f);
        rt.sizeDelta        = new Vector2(w, h);
    }

    static void Center(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    // ── 잡 헬퍼 ──────────────────────────────────────────────

    static GameObject Go(string name, GameObject parent, params System.Type[] extra)
        => EditorUIBuilder.Go(name, parent, extra);

    static GameObject CreatePanel(GameObject parent, string name, Color color)
        => EditorUIBuilder.Panel(parent, name, color);

    static TextMeshProUGUI TMP(GameObject parent, string name, string text, float size, FontStyles style)
        => EditorUIBuilder.TMP(parent, name, text, size, style);

    static void Stretch(GameObject go) => EditorUIBuilder.Stretch(go);

    static void SetObj(SerializedObject so, string field, Object obj)
        => EditorUIBuilder.SetObj(so, field, obj, "HeroDetailPopupCreator");

    static void SetObjArray(SerializedObject so, string field, Object[] objs)
        => EditorUIBuilder.SetObjArray(so, field, objs, "HeroDetailPopupCreator");

    static void SetEnum(SerializedObject so, string field, int value)
        => EditorUIBuilder.SetEnum(so, field, value, "HeroDetailPopupCreator");
}
#endif
