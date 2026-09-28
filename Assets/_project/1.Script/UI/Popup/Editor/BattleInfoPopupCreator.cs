using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  BattleInfoPopupCreator.cs  [Editor Only]
//  Tools > Project K > 프리팹 생성 > 팝업 > 전황
//
//  ■ 얼개 (2026-09-15 다시 짰다 — 사용자 지적 "허접하다 · 글자가 겹친다")
//      ┌ 머리 띠 ─────────────────────────────────────────────┐
//      │ 전황  [스테이지 8]                               [X] │
//      ├──────────────────────┐  ◆VS◆  ┌──────────────────────┤
//      │▌아군 진형    카드 4장 │        │▌적군 진형  부대·병사 │
//      │ 카드 4열 × 2줄        │        │ 라인 1~5 (위→아래)   │
//      └──────────────────────┘        └──────────────────────┘
//
//  ■ 왜 다시 짰나
//    ① 제목("전황 — 스테이지 8")과 진형 라벨이 같은 높이대에 서서 **겹쳐 그려졌다.**
//       제목은 머리 띠 안, 진형 라벨은 각 패널의 머리로 옮겨 서로 다른 칸에 산다.
//    ② 칸들이 배경에 둥둥 떠 있어 어느 쪽 편인지 색으로만 읽혔다. 패널 두 장이
//       좌우를 가르고, 패널 위의 색 띠(파랑/빨강)가 진영을 말한다.
//    ③ "VS" 가 라벨 옆에 붙어 문장처럼 읽혔다 — 두 패널 사이 가운데에 메달로 세운다.
//
//  ⚠ 적은 목록이 아니라 **줄 번호 자리**다 — 위에서 아래가 전장의 1~5 줄이다.
//  ⚠ 상세 패널은 만들지 않는다 — 칸을 누르면 몬스터 상세 · 영웅 상세가 열린다.
//  ⚠ 자리는 전부 상수의 합이다. 손으로 좌표를 적지 말 것 — 겹침이 다시 생긴다.
// ============================================================

public static class BattleInfoPopupCreator
{
    const string SavePath = "Assets/_project/2.Prefabs/UI/BattleInfoPopup.prefab";
    const string Tag      = "BattleInfoPopupCreator";

    // ── 화면 ─────────────────────────────────────────────────
    const float ScreenW = 1920f;
    const float ScreenH = 1080f;
    const float Margin  = 40f;
    const float HeaderH = 104f;

    /// <summary>두 패널 사이 — VS 메달이 선다.</summary>
    const float PanelGap = 112f;

    static float PanelW   => (ScreenW - Margin * 2f - PanelGap) * 0.5f;   // 864
    static float PanelTop => Margin + HeaderH + 16f;                       // 160
    static float PanelH   => ScreenH - PanelTop - Margin;                  // 880

    const float PanelHeadH = 76f;
    const float Inner      = 24f;
    const float AccentH    = 6f;

    // ── 아군 칸 ──────────────────────────────────────────────
    //  ⚠ 높이는 얹는 것들의 합이다 — 배지 30 + 16 + 초상화 132 + 4 + 이름 43 + 6 + 아래배지 43 + 여백 10 = 284
    const int   AllyCols  = 4;
    const float SlotGap   = 16f;
    const float SlotH     = 284f;
    const float PortraitS = 132f;
    const float BadgeIcon = 30f;

    static float SlotW => (PanelW - Inner * 2f - SlotGap * (AllyCols - 1)) / AllyCols;   // 192

    // ── 적 줄 ────────────────────────────────────────────────
    const float LaneGap = 12f;
    static float LaneH => Mathf.Floor((PanelH - PanelHeadH - Inner * 2f - LaneGap * 4f) / 5f);   // 141
    static float LaneW => PanelW - Inner * 2f;

    // ── 색 ───────────────────────────────────────────────────
    static readonly Color Scrim       = new(0.015f, 0.018f, 0.04f, 0.94f);
    static readonly Color HeaderBg    = new(0.055f, 0.063f, 0.105f, 1f);
    static readonly Color HeaderLine  = new(0.26f,  0.30f,  0.46f,  1f);
    static readonly Color PanelBg     = new(0.062f, 0.070f, 0.115f, 0.98f);
    static readonly Color PanelHeadBg = new(0.085f, 0.095f, 0.150f, 1f);
    static readonly Color AllyAccent  = new(0.36f,  0.62f,  1.00f,  1f);
    static readonly Color EnemyAccent = new(1.00f,  0.42f,  0.42f,  1f);
    static readonly Color AllyFace    = new(0.115f, 0.145f, 0.220f, 1f);
    static readonly Color EnemyFace   = new(0.175f, 0.105f, 0.125f, 1f);
    static readonly Color LaneNoBg    = new(0.09f,  0.05f,  0.07f,  1f);
    static readonly Color StageChipBg = new(0.13f,  0.15f,  0.24f,  1f);
    static readonly Color SubText     = new(0.72f,  0.77f,  0.90f,  1f);
    static readonly Color LevelGold   = new(1f,     0.86f,  0.42f,  1f);

    [MenuItem(ProjectKMenu.Popup + "전황", priority = ProjectKMenu.PrefabPrio + 54)]
    public static void Run()
    {
        // ⚠ 마나·마릿수 아이콘이 없으면 굽지 않는다 (UI 규칙 7)
        if (!UIIconAssets.TryLoad(Tag, out Sprite manaIcon, out Sprite countIcon)) return;

        var root  = new GameObject("BattleInfoPopup", typeof(RectTransform));
        root.AddComponent<CanvasGroup>();
        var popup = root.AddComponent<BattleInfoPopup>();
        EditorUIBuilder.Stretch(root);

        var bg = EditorUIBuilder.Img(root, "Scrim", Scrim);
        EditorUIBuilder.Stretch(bg.gameObject);
        bg.raycastTarget = true;   // 뒤쪽 전장이 눌리지 않게 막는다

        BuildHeader(root, out TextMeshProUGUI title, out TextMeshProUGUI stage, out Button close);

        // ── 패널 두 장 ──
        GameObject allyPanel  = BuildPanel(root, "AllyPanel",  Margin,                     AllyAccent,
                                           "아군 진형", out TextMeshProUGUI allySummary);
        GameObject enemyPanel = BuildPanel(root, "EnemyPanel", Margin + PanelW + PanelGap, EnemyAccent,
                                           "적군 진형", out TextMeshProUGUI enemySummary);

        BuildVsMedal(root);

        // ── 아군 격자 (4열 × 2줄) ──
        var allies = new List<BattleInfoPopup.SlotView>(BattleInfoPopup.MaxAllySlots);
        for (int i = 0; i < BattleInfoPopup.MaxAllySlots; i++)
        {
            float x = Inner + (i % AllyCols) * (SlotW + SlotGap);
            float y = -(PanelHeadH + Inner) - (i / AllyCols) * (SlotH + SlotGap);
            allies.Add(BuildSlot(allyPanel, $"Ally{i}", x, y, manaIcon, countIcon));
        }

        // ── 적 줄 (세로 5칸 — 위에서 아래가 곧 전장의 줄 번호다) ──
        var enemies = new List<BattleInfoPopup.SlotView>(BattleInfoPopup.MaxEnemySlots);
        for (int i = 0; i < BattleInfoPopup.MaxEnemySlots; i++)
            enemies.Add(BuildLaneSlot(enemyPanel, $"Enemy{i}", i + 1,
                                      Inner, -(PanelHeadH + Inner) - i * (LaneH + LaneGap)));

        Verify();

        // ── 배선 ──
        var so = new SerializedObject(popup);
        EditorUIBuilder.SetEnum(so, "_popupType", (int)PopupType.BattleInfo, Tag);
        EditorUIBuilder.SetObj(so, "_titleText",    title,        Tag);
        EditorUIBuilder.SetObj(so, "_stageText",    stage,        Tag);
        EditorUIBuilder.SetObj(so, "_allySummary",  allySummary,  Tag);
        EditorUIBuilder.SetObj(so, "_enemySummary", enemySummary, Tag);
        EditorUIBuilder.SetObj(so, "_closeBtn",     close,        Tag);

        WriteSlots(so, "_allySlots",  allies);
        WriteSlots(so, "_enemySlots", enemies);
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);

        Debug.Log($"[{Tag}] 저장: {SavePath}\n" +
                  "⚠ PopupManager 의 [Load Popup Prefabs] 를 눌러야 열립니다.");
    }

    // ── 머리 띠 ──────────────────────────────────────────────

    static void BuildHeader(GameObject root, out TextMeshProUGUI title, out TextMeshProUGUI stage,
                            out Button close)
    {
        var bar = EditorUIBuilder.Img(root, "HeaderBar", HeaderBg);
        {
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(0f, Margin + HeaderH);
        }
        bar.raycastTarget = false;

        var line = EditorUIBuilder.Img(root, "HeaderLine", HeaderLine);
        {
            var rt = line.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(Margin + HeaderH));
            rt.sizeDelta        = new Vector2(0f, 2f);
        }
        line.raycastTarget = false;

        float midY = -(Margin + HeaderH * 0.5f);

        // 제목 — 고정 글자
        title = EditorUIBuilder.TMP(root, "Title", "전황", UIScale.FontXl, FontStyles.Bold);
        {
            var rt = title.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(Margin + 8f, midY);
            rt.sizeDelta        = new Vector2(180f, UIScale.Line(UIScale.FontXl));
        }
        title.alignment        = TextAlignmentOptions.MidlineLeft;
        title.raycastTarget    = false;
        title.textWrappingMode = TextWrappingModes.NoWrap;

        // 스테이지 칩 — 제목과 **다른 칸**이다 (한 줄에 이어 붙였더니 겹쳐 그려졌다)
        const float ChipW = 300f, ChipH = 64f;
        var chip = EditorUIBuilder.Img(root, "StageChip", StageChipBg);
        {
            var rt = chip.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(Margin + 8f + 180f + 20f, midY);
            rt.sizeDelta        = new Vector2(ChipW, ChipH);
        }
        chip.raycastTarget = false;

        stage = EditorUIBuilder.TMP(chip.gameObject, "StageText", "스테이지 1", UIScale.FontMd, FontStyles.Bold);
        EditorUIBuilder.Stretch(stage.gameObject);
        stage.alignment        = TextAlignmentOptions.Midline;
        stage.color            = LevelGold;
        stage.raycastTarget    = false;
        stage.textWrappingMode = TextWrappingModes.NoWrap;

        // 닫기
        close = EditorUIBuilder.RaisedBtn(root, "CloseBtn", new Color(0.22f, 0.16f, 0.24f), out var body);
        {
            var rt = close.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-Margin, midY);
            rt.sizeDelta        = new Vector2(80f, 80f);
        }
        EditorUIBuilder.XMark(body, "X", 34f, new Color(0.90f, 0.92f, 0.97f));

        // 도움말 — 닫기(80) 왼쪽, 같은 세로 줄
        EditorUIBuilder.InfoBtn(root, TutorialId.HelpBattleInfo, 80f, -Margin,
                                size: 80f, anchorY: 1f, y: midY + 40f);   // 피벗이 위(1)라 반 칸 올린다
    }

    // ── 패널 ─────────────────────────────────────────────────

    /// <summary>진영 패널 — 위의 색 띠가 편을 말한다. 머리에 라벨과 요약.</summary>
    static GameObject BuildPanel(GameObject root, string name, float x, Color accent,
                                 string label, out TextMeshProUGUI summary)
    {
        var panel = EditorUIBuilder.Img(root, name, PanelBg);
        {
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -PanelTop);
            rt.sizeDelta        = new Vector2(PanelW, PanelH);
        }
        panel.raycastTarget = false;

        var head = EditorUIBuilder.Img(panel.gameObject, "Head", PanelHeadBg);
        {
            var rt = head.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(0f, PanelHeadH);
        }
        head.raycastTarget = false;

        var strip = EditorUIBuilder.Img(panel.gameObject, "Accent", accent);
        {
            var rt = strip.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(0f, AccentH);
        }
        strip.raycastTarget = false;

        // 라벨 앞 작은 막대 — 같은 색으로 한 번 더 편을 말한다
        var bar = EditorUIBuilder.Img(panel.gameObject, "LabelBar", accent);
        {
            var rt = bar.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(Inner, -(AccentH + (PanelHeadH - AccentH) * 0.5f));
            rt.sizeDelta        = new Vector2(8f, 36f);
        }
        bar.raycastTarget = false;

        float headMid = -(AccentH + (PanelHeadH - AccentH) * 0.5f);

        var text = EditorUIBuilder.TMP(panel.gameObject, "Label", label, UIScale.FontMd, FontStyles.Bold);
        {
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(Inner + 22f, headMid);
            rt.sizeDelta        = new Vector2(320f, UIScale.RowMd);
        }
        text.alignment        = TextAlignmentOptions.MidlineLeft;
        text.color            = Color.white;
        text.raycastTarget    = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        summary = EditorUIBuilder.TMP(panel.gameObject, "Summary", "", UIScale.FontSm, FontStyles.Bold);
        {
            var rt = summary.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-Inner, headMid);
            rt.sizeDelta        = new Vector2(420f, UIScale.RowSm);
        }
        summary.alignment        = TextAlignmentOptions.MidlineRight;
        summary.color            = SubText;
        summary.raycastTarget    = false;
        summary.textWrappingMode = TextWrappingModes.NoWrap;

        return panel.gameObject;
    }

    /// <summary>두 패널 사이 가운데 — 겹친 마름모 위에 "VS".</summary>
    static void BuildVsMedal(GameObject root)
    {
        var holder = EditorUIBuilder.Go("VsMedal", root);
        {
            var rt = holder.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(ScreenW * 0.5f, -(PanelTop + PanelH * 0.5f));
            rt.sizeDelta        = new Vector2(PanelGap, PanelGap);
        }

        // ⚠ 테두리는 **앞 형제**로 깐다 (UI 규칙 3)
        EditorUIBuilder.Diamond(holder, "Rim",  92f, HeaderLine);
        EditorUIBuilder.Diamond(holder, "Face", 80f, HeaderBg);

        var vs = EditorUIBuilder.TMP(holder, "VS", "VS", UIScale.FontMd, FontStyles.Bold);
        EditorUIBuilder.Stretch(vs.gameObject);
        vs.alignment     = TextAlignmentOptions.Midline;
        vs.color         = LevelGold;
        vs.raycastTarget = false;
    }

    // ── 칸 ───────────────────────────────────────────────────

    /// <summary>
    /// 아군 칸 — 하단 소환 카드와 **같은 얼개**로 읽히게 만든다.
    ///     [마나]        [마릿수]     ← 위 모서리 (UI 규칙 7 — 글자가 아니라 배지)
    ///          초상화
    ///          이름
    ///     [Lv]          [품질]       ← 아래 모서리
    /// </summary>
    static BattleInfoPopup.SlotView BuildSlot(GameObject parent, string name, float x, float y,
                                              Sprite manaIcon, Sprite countIcon)
    {
        Button btn = EditorUIBuilder.RaisedBtn(parent, name, AllyFace, out GameObject body);
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(SlotW, SlotH);

        // ⚠ 좌우 한 쌍의 **안쪽 순서는 양쪽 다 같다** (UI 규칙 7)
        EditorUIBuilder.IconValueBadge(body, "ManaBadge", manaIcon, rightSide: false,
                                       BadgeIcon, UIScale.FontSm, 10f, Color.white,
                                       out TextMeshProUGUI manaText);

        EditorUIBuilder.IconValueBadge(body, "CountBadge", countIcon, rightSide: true,
                                       BadgeIcon, UIScale.FontSm, 10f,
                                       new Color(1f, 0.94f, 0.80f),
                                       out TextMeshProUGUI countText);

        var portrait = EditorUIBuilder.Img(body, "Portrait", Color.white);
        portrait.preserveAspect = true;
        portrait.raycastTarget  = false;
        {
            var r = portrait.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot     = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -(BadgeIcon + 16f));
            r.sizeDelta        = new Vector2(PortraitS, PortraitS);
        }

        var nameTmp = EditorUIBuilder.TMP(body, "NameText", "", UIScale.FontSm, FontStyles.Bold);
        {
            var r = nameTmp.rectTransform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot     = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(8f, -(BadgeIcon + 16f + PortraitS + 4f + UIScale.RowSm));
            r.offsetMax = new Vector2(-8f, -(BadgeIcon + 16f + PortraitS + 4f));
        }
        nameTmp.alignment        = TextAlignmentOptions.Midline;
        nameTmp.raycastTarget    = false;
        nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
        // 긴 이름은 줄인다 — 칸 밖으로 넘치면 옆 칸 위에 겹쳐 그려진다
        nameTmp.enableAutoSizing = true;
        nameTmp.fontSizeMax      = UIScale.FontSm;
        nameTmp.fontSizeMin      = UIScale.FontSm * 0.75f;

        TextMeshProUGUI left  = Badge(body, "LeftBadge",  align: true,  fromLeft: 12f, width: SlotW * 0.5f - 12f);
        TextMeshProUGUI right = Badge(body, "RightBadge", align: false, fromLeft: 12f, width: SlotW * 0.5f - 12f);
        left.color = LevelGold;   // 레벨은 금색 — 하단 카드 바의 레벨 배지와 같은 색

        return Pack(btn, portrait, nameTmp, left, right, manaText, countText);
    }

    /// <summary>적 줄 칸 — [줄 번호] [초상화] [이름 / Lv · 병사] 가로형.</summary>
    static BattleInfoPopup.SlotView BuildLaneSlot(GameObject parent, string name, int laneNumber,
                                                  float x, float y)
    {
        float h = LaneH;

        Button btn = EditorUIBuilder.RaisedBtn(parent, name, EnemyFace, out GameObject body);
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(LaneW, h);

        // ⚠ 줄 번호는 고정이다 — 그 줄에 적이 없어도 자리는 남는다.
        const float NoS = 64f;
        var noBg = EditorUIBuilder.Img(body, "LaneNoBg", LaneNoBg);
        {
            var r = noBg.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot     = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(16f, 0f);
            r.sizeDelta        = new Vector2(NoS, NoS);
        }
        noBg.raycastTarget = false;

        var lane = EditorUIBuilder.TMP(noBg.gameObject, "LaneNo", laneNumber.ToString(),
                                       UIScale.FontLg, FontStyles.Bold);
        EditorUIBuilder.Stretch(lane.gameObject);
        lane.alignment     = TextAlignmentOptions.Midline;
        lane.color         = EnemyAccent;
        lane.raycastTarget = false;

        float ps = h - 24f;

        var portrait = EditorUIBuilder.Img(body, "Portrait", Color.white);
        portrait.preserveAspect = true;
        portrait.raycastTarget  = false;
        {
            var r = portrait.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot     = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(16f + NoS + 20f, 0f);
            r.sizeDelta        = new Vector2(ps, ps);
        }

        float textLeft = 16f + NoS + 20f + ps + 20f;

        // 이름 — 위 절반
        var nameTmp = EditorUIBuilder.TMP(body, "NameText", "", UIScale.FontMd, FontStyles.Bold);
        {
            var r = nameTmp.rectTransform;
            r.anchorMin = new Vector2(0f, 0.5f); r.anchorMax = new Vector2(1f, 0.5f);
            r.pivot     = new Vector2(0f, 0f);
            r.offsetMin = new Vector2(textLeft, 2f);
            r.offsetMax = new Vector2(-20f, 2f + UIScale.RowMd);
        }
        nameTmp.alignment        = TextAlignmentOptions.BottomLeft;
        nameTmp.color            = Color.white;
        nameTmp.raycastTarget    = false;
        nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
        nameTmp.enableAutoSizing = true;
        nameTmp.fontSizeMax      = UIScale.FontMd;
        nameTmp.fontSizeMin      = UIScale.FontSm;

        // Lv · 병사 — 아래 절반, 나란히
        TextMeshProUGUI left  = LaneStat(body, "LeftBadge",  textLeft,         LevelGold);
        TextMeshProUGUI right = LaneStat(body, "RightBadge", textLeft + 150f,  SubText);

        return Pack(btn, portrait, nameTmp, left, right);
    }

    // ── 공용 조각 ────────────────────────────────────────────

    /// <summary>아군 칸 아래 모서리의 작은 값 (Lv · 품질).</summary>
    static TextMeshProUGUI Badge(GameObject body, string name, bool align, float fromLeft, float width)
    {
        var tmp = EditorUIBuilder.TMP(body, name, "", UIScale.FontSm, FontStyles.Bold);
        var r   = tmp.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(align ? 0f : 1f, 0f);
        r.pivot     = new Vector2(align ? 0f : 1f, 0f);
        r.anchoredPosition = new Vector2(align ? fromLeft : -fromLeft, 12f);
        r.sizeDelta        = new Vector2(width, UIScale.RowSm);
        tmp.alignment        = align ? TextAlignmentOptions.MidlineLeft
                                     : TextAlignmentOptions.MidlineRight;
        tmp.color            = SubText;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    /// <summary>적 줄 아래 절반의 값 한 칸.</summary>
    static TextMeshProUGUI LaneStat(GameObject body, string name, float x, Color color)
    {
        var tmp = EditorUIBuilder.TMP(body, name, "", UIScale.FontSm, FontStyles.Bold);
        var r   = tmp.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot     = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -4f);
        r.sizeDelta        = new Vector2(140f, UIScale.RowSm);
        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
        tmp.color            = color;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    static BattleInfoPopup.SlotView Pack(Button btn, Image portrait, TextMeshProUGUI name,
                                         TextMeshProUGUI left, TextMeshProUGUI right,
                                         TextMeshProUGUI mana = null,
                                         TextMeshProUGUI count = null)
        => new BattleInfoPopup.SlotView
        {
            Root       = btn.gameObject,
            Button     = btn,
            Portrait   = portrait,
            NameText   = name,
            LeftBadge  = left,
            RightBadge = right,
            ManaText   = mana,
            CountText  = count,
        };

    static void WriteSlots(SerializedObject so, string field,
                           List<BattleInfoPopup.SlotView> list)
    {
        SerializedProperty arr = so.FindProperty(field);
        arr.arraySize = list.Count;

        for (int i = 0; i < list.Count; i++)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Root")      .objectReferenceValue = list[i].Root;
            e.FindPropertyRelative("Button")    .objectReferenceValue = list[i].Button;
            e.FindPropertyRelative("Portrait")  .objectReferenceValue = list[i].Portrait;
            e.FindPropertyRelative("NameText")  .objectReferenceValue = list[i].NameText;
            e.FindPropertyRelative("LeftBadge") .objectReferenceValue = list[i].LeftBadge;
            e.FindPropertyRelative("RightBadge").objectReferenceValue = list[i].RightBadge;
            e.FindPropertyRelative("ManaText")  .objectReferenceValue = list[i].ManaText;
            e.FindPropertyRelative("CountText") .objectReferenceValue = list[i].CountText;
        }
    }

    // ── 검산 ─────────────────────────────────────────────────

    /// <summary>
    /// 칸이 패널 안에 들어가는지 잰다 — 넘쳐도 유니티는 조용히 그린다 (겹침이 그렇게 생겼다).
    /// </summary>
    static void Verify()
    {
        float gridW = SlotW * AllyCols + SlotGap * (AllyCols - 1);
        float gridH = SlotH * 2f + SlotGap;
        float room  = PanelH - PanelHeadH - Inner * 2f;

        if (gridW > PanelW - Inner * 2f + 0.5f || gridH > room)
            Debug.LogError($"[{Tag}] 아군 격자({gridW:0}×{gridH:0})가 패널 안({PanelW - Inner * 2f:0}×{room:0})을 넘습니다.");

        float lanesH = LaneH * 5f + LaneGap * 4f;
        if (lanesH > room)
            Debug.LogError($"[{Tag}] 적 줄({lanesH:0})이 패널 안({room:0})을 넘습니다.");

        if (BadgeWidth() * 2f > SlotW)
            Debug.LogError($"[{Tag}] 마나·마릿수 배지 둘이 칸 폭({SlotW:0})을 넘어 겹칩니다 (UI 규칙 7).");
    }

    static float BadgeWidth() => EditorUIBuilder.BadgeWidthFor(BadgeIcon, UIScale.FontSm) + 10f;
}
