using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  CardPickPopupCreator.cs
//  Tools > Project K > 프리팹 생성 > 팝업 > 강화소 / 제단
//
//  ┌────────────────────────────────────────────────────────────┐
//  │ 강화소                                  [금]6,020   [ × ]  │  ← 지갑은 아이콘
//  │ 화덕이 아직 식지 않았다…                                    │  ← 작게·흐리게 한 줄
//  │                     (배경 그림)                             │
//  ├────────────────────────────────────────────────────────────┤
//  │ ┌───┐┌───┐┌───┐┌───┐┌───┐┌───┐┌───┐┌───┐  ← 덱 8칸이 **한 줄**│
//  │ │초 ││초 ││초 ││초 ││초 ││초 ││초 ││초 │                     │
//  │ │이름││   ││   ││   ││   ││   ││   ││   │                     │
//  │ │Lv5 ││   ││   ││   ││   ││   ││   ││   │                     │
//  │ │[마]−3 [수]+3                          │  ← 새긴 것도 아이콘 │
//  │ └───┘└───┘└───┘└───┘└───┘└───┘└───┘└───┘                     │
//  │            독 슬라임 — 무엇을 새길까                          │
//  │   [ 각인   [마]−3      [금]70 ]   [ 증식   [수]+3   [금]70 ]  │
//  └────────────────────────────────────────────────────────────┘
//
//  ■ ⚠ 전면 재설계 (사용자 지적, 2026-09-09)
//    고친 것과 그 이유:
//      · **8칸이 한 줄** — 4열이라 다섯 번째부터 아래로 접혀 보이지 않았다.
//        (격자 높이 254 에 300짜리 칸을 넣어 둘째 줄이 통째로 잘려 있었다)
//      · **글자 설명을 걷어냈다** — 버튼 안의 "소환 비용이 영구히 줄어든다" 는
//        칸을 넘겨 삐져나왔고, 정작 읽어야 할 숫자를 밀어냈다.
//      · **마나·마릿수·골드는 전부 아이콘** (UI 규칙 7). "−3 마나" 는 글자 셋을
//        읽어야 알지만 [물방울]−3 은 그림 하나로 끝난다.
//      · **낼 값은 버튼 안** — 제목 줄의 "70 G" 는 아무도 안 읽었다.
//        지갑은 오른쪽 위 한 자리, 값은 누를 것과 한 몸
//        (몬스터 상세의 품질 개선 패널과 같은 규칙).
//
//  ■ 두 화면이 격자를 나눠 쓴다 — 화면 자체는 갈라져 있다
//    강화소는 카드가 남고 제단은 사라진다. 끝이 다르므로 아래 행동 줄이
//    다르고, 클래스도 다르다 (ForgePopup · AltarPopup).
//    같은 것은 "내 덱을 펼치고 하나를 고른다" 뿐이라 그 부분만 함께 굽는다.
//
//  ⚠ 그림 위에 글을 바로 얹지 않는다 (UI 규칙 8)
//    배경 아래에 어두운 띠를 깔고 그 위에 글을 둔다.
// ============================================================

public static class CardPickPopupCreator
{
    const string Tag = "CardPickPopupCreator";

    // ── 카드 격자 — 덱 8칸이 **한 줄**에 들어간다 ──
    //
    //  ⚠ 폭 검산: 8 × 208 + 7 × 14 = 1762 ≤ 1824 (화면 1920 − 여백 48×2)
    //    칸을 키우거나 여백을 넓히면 이 식부터 다시 볼 것. 넘치면 GridLayoutGroup
    //    이 조용히 다음 줄로 접고, 그 줄은 격자 높이를 넘겨 **보이지 않는다.**
    const int   Cols    = CardPickPopupBase.MaxCells;
    const float CellW   = 208f;
    const float CellH   = 286f;
    const float CellGap = 14f;

    // ── 아래 행동 줄 ──
    //  ⚠ 세로 검산: 행동 200 + 사이 16 + 카드 286 = 502 ≤ BottomVeilH(560)
    //    남는 58 이 위쪽 여백이다. 어둠 판 높이는 네 시설이 함께 쓰므로 못 늘린다.
    const float ActionH  = 200f;
    const float ActionBtnW = 560f;
    const float ActionBtnH = 116f;

    static readonly Color PanelBg  = new Color(0.075f, 0.082f, 0.135f, 0.99f);
    static readonly Color CellBg   = new Color(0.115f, 0.125f, 0.20f, 1f);
    static readonly Color ActionBg = new Color(0.055f, 0.062f, 0.105f, 1f);
    static readonly Color SubText  = new Color(0.70f, 0.75f, 0.88f, 1f);

    // ══════════════════════════════════════════════════════════
    //  강화소
    // ══════════════════════════════════════════════════════════

    [MenuItem(ProjectKMenu.Popup + "강화소", priority = ProjectKMenu.PrefabPrio + 46)]
    public static void CreateForge()
    {
        if (!RunNodeArtAssets.TryLoad(Tag, out Sprite[] nodeArt))                  return;
        if (!UIIconAssets.TryLoad(Tag, out Sprite manaIcon, out Sprite countIcon)) return;
        if (!UIIconAssets.TryLoadGold(Tag, out Sprite goldIcon))                   return;

        GameObject root = MakeShell("ForgePopup", goldIcon,
                                    out GameObject panel, out Image art,
                                    out TextMeshProUGUI title, out TextMeshProUGUI flavor,
                                    out TextMeshProUGUI purse, out Button closeBtn);
        var ui = root.AddComponent<ForgePopup>();

        BuildCommon(panel, manaIcon, countIcon, out CardPickPopupBase.CardCell[] cells);

        GameObject bar = ActionBar(panel, out TextMeshProUGUI hint);

        // ⚠ 두 버튼의 면 색이 달라야 한다 — 같은 색이면 어느 쪽을 눌렀는지
        //   손이 기억하지 못한다. 각인 = 보랏빛(마나) · 증식 = 풀빛(마릿수).
        Button engrave = ActionButton(bar, "EngraveBtn", -1, new Color(0.30f, 0.24f, 0.46f),
                                      "각인", manaIcon, goldIcon,
                                      out var engLabel, out var engValue,
                                      out var engCostRoot, out var engCost);

        Button breed = ActionButton(bar, "BreedBtn", +1, new Color(0.22f, 0.36f, 0.30f),
                                    "증식", countIcon, goldIcon,
                                    out var brdLabel, out var brdValue,
                                    out var brdCostRoot, out var brdCost);

        var so = new SerializedObject(ui);
        WriteCommon(so, PopupType.Forge, art, title, flavor, purse, closeBtn, cells, nodeArt);

        EditorUIBuilder.SetObj(so, "_actionRoot",      bar,         Tag);
        EditorUIBuilder.SetObj(so, "_actionHint",      hint,        Tag);

        EditorUIBuilder.SetObj(so, "_engraveBtn",      engrave,     Tag);
        EditorUIBuilder.SetObj(so, "_engraveLabel",    engLabel,    Tag);
        EditorUIBuilder.SetObj(so, "_engraveValue",    engValue,    Tag);
        EditorUIBuilder.SetObj(so, "_engraveCostRoot", engCostRoot, Tag);
        EditorUIBuilder.SetObj(so, "_engraveCost",     engCost,     Tag);

        EditorUIBuilder.SetObj(so, "_breedBtn",        breed,       Tag);
        EditorUIBuilder.SetObj(so, "_breedLabel",      brdLabel,    Tag);
        EditorUIBuilder.SetObj(so, "_breedValue",      brdValue,    Tag);
        EditorUIBuilder.SetObj(so, "_breedCostRoot",   brdCostRoot, Tag);
        EditorUIBuilder.SetObj(so, "_breedCost",       brdCost,     Tag);
        so.ApplyModifiedPropertiesWithoutUndo();

        Save(root, "ForgePopup");
    }

    // ══════════════════════════════════════════════════════════
    //  제단
    // ══════════════════════════════════════════════════════════

    [MenuItem(ProjectKMenu.Popup + "제단", priority = ProjectKMenu.PrefabPrio + 47)]
    public static void CreateAltar()
    {
        if (!RunNodeArtAssets.TryLoad(Tag, out Sprite[] nodeArt))                  return;
        if (!UIIconAssets.TryLoad(Tag, out Sprite manaIcon, out Sprite countIcon)) return;
        if (!UIIconAssets.TryLoadGold(Tag, out Sprite goldIcon))                   return;

        GameObject root = MakeShell("AltarPopup", goldIcon,
                                    out GameObject panel, out Image art,
                                    out TextMeshProUGUI title, out TextMeshProUGUI flavor,
                                    out TextMeshProUGUI purse, out Button closeBtn);
        var ui = root.AddComponent<AltarPopup>();

        BuildCommon(panel, manaIcon, countIcon, out CardPickPopupBase.CardCell[] cells);

        GameObject bar = ActionBar(panel, out TextMeshProUGUI hint);

        // ── 되돌릴 수 없는 한 갈래 ──
        //   ⚠ 붉은 면이다. 강화소의 두 버튼과 색으로 갈려야 손이 멈춘다.
        Button sacrifice = ActionButton(bar, "SacrificeBtn", 0, new Color(0.48f, 0.16f, 0.20f),
                                        "바친다", null, goldIcon,
                                        out var sacLabel, out _,
                                        out var sacCostRoot, out var sacCost);

        var so = new SerializedObject(ui);
        WriteCommon(so, PopupType.Altar, art, title, flavor, purse, closeBtn, cells, nodeArt);

        EditorUIBuilder.SetObj(so, "_actionRoot",        bar,         Tag);
        EditorUIBuilder.SetObj(so, "_actionHint",        hint,        Tag);
        EditorUIBuilder.SetObj(so, "_sacrificeBtn",      sacrifice,   Tag);
        EditorUIBuilder.SetObj(so, "_sacrificeLabel",    sacLabel,    Tag);
        EditorUIBuilder.SetObj(so, "_sacrificeCostRoot", sacCostRoot, Tag);
        EditorUIBuilder.SetObj(so, "_sacrificeCost",     sacCost,     Tag);
        so.ApplyModifiedPropertiesWithoutUndo();

        Save(root, "AltarPopup");
    }

    // ══════════════════════════════════════════════════════════
    //  공통 — 껍데기 · 격자 · 행동 줄
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 전체화면 무대를 세우고, 카드가 들어갈 칸을 돌려준다.
    ///
    /// ⚠ 가운데 패널이 아니다 (사용자 요청, 2026-09-07)
    ///   그림이 머리에만 붙은 창은 "창" 이지 "장소" 가 아니다.
    ///   야영지·상점과 같은 무대(FacilityStage)를 써야 같은 세계로 읽힌다.
    /// </summary>
    static GameObject MakeShell(string name, Sprite goldIcon,
                                out GameObject panel, out Image art,
                                out TextMeshProUGUI title, out TextMeshProUGUI flavor,
                                out TextMeshProUGUI purse, out Button closeBtn)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.AddComponent<CanvasGroup>();

        FacilityStage.Build(root, out art, out title, out flavor,
                            out GameObject content, out closeBtn);

        // 지갑 — 오른쪽 위 한 자리. 제목 줄에 골드를 적지 않는다 (머리 주석).
        purse = FacilityStage.PurseBadge(root, goldIcon, "Purse");

        panel = content;
        return root;
    }

    /// <summary>카드 격자를 짓는다. 제목·이야기·닫기는 무대가 이미 만들었다.</summary>
    static void BuildCommon(GameObject panel, Sprite manaIcon, Sprite countIcon,
                            out CardPickPopupBase.CardCell[] cells)
    {
        var grid = EditorUIBuilder.Go("CardGrid", panel);
        {
            var rt = grid.GetComponent<RectTransform>();
            // ⚠ 아래쪽 어둠 안에 넣는다 — 그림 위에 카드를 흩뿌리면
            //   무엇이 배경이고 무엇이 고를 것인지가 갈리지 않는다.
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, ActionH + 16f);
            rt.sizeDelta        = new Vector2(Cols * CellW + (Cols - 1) * CellGap, CellH);
        }

        var layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize        = new Vector2(CellW, CellH);
        layout.spacing         = new Vector2(CellGap, CellGap);
        layout.childAlignment  = TextAnchor.UpperCenter;
        layout.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Cols;

        cells = new CardPickPopupBase.CardCell[CardPickPopupBase.MaxCells];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = BuildCell(grid, i, manaIcon, countIcon);
    }

    /// <summary>카드 한 칸 — 테두리 · 초상화 · 이름 · Lv · (배지 둘 또는 글 한 줄).</summary>
    static CardPickPopupBase.CardCell BuildCell(GameObject grid, int index,
                                                Sprite manaIcon, Sprite countIcon)
    {
        var slot = EditorUIBuilder.Go($"Cell_{index + 1}", grid);

        // ⚠ 테두리를 자식으로 두지 않는다 (UI 규칙 3)
        //   Frame 을 먼저 만들고 그 위에 버튼 면을 조금 작게 깐다.
        var frame = EditorUIBuilder.Img(slot, "Frame", new Color(0.24f, 0.27f, 0.38f));
        EditorUIBuilder.Stretch(frame.gameObject);
        frame.raycastTarget = false;

        Button button = EditorUIBuilder.RaisedBtnOn(slot, CellBg, out GameObject body);
        {
            var rt = body.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(4f, 4f);
            rt.offsetMax = new Vector2(-4f, -4f);
        }

        var portrait = EditorUIBuilder.Img(body, "Portrait", Color.white);
        portrait.preserveAspect = true;
        portrait.raycastTarget  = false;
        {
            var rt = portrait.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -12f);
            rt.sizeDelta        = new Vector2(132f, 132f);
        }

        // 세로 예산: 12 + 132 + 4 + 이름 43 + Lv 43 + 아래 줄 43 = 277 ≤ 286 − 8 ✔
        float y = 12f + 132f + 4f;

        var name = EditorUIBuilder.TMP(body, "Name", "이름", UIScale.FontSm, FontStyles.Bold);
        Row(name, -y, UIScale.RowSm);
        name.enableAutoSizing = true;
        name.fontSizeMax      = UIScale.FontSm;
        name.fontSizeMin      = UIScale.FontSm * 0.7f;
        y += UIScale.RowSm;

        var level = EditorUIBuilder.TMP(body, "Level", "Lv.1", UIScale.FontSm * 0.85f,
                                        FontStyles.Bold);
        Row(level, -y, UIScale.RowSm);
        level.color = new Color(1f, 0.85f, 0.45f);
        y += UIScale.RowSm;

        // ── 아래 한 줄 — 배지 둘 또는 글 한 줄 ──
        //   ⚠ 둘이 같은 자리를 쓴다. 화면마다 하나만 켠다 (CardPickPopupBase).
        var state = EditorUIBuilder.TMP(body, "State", "", UIScale.FontSm * 0.8f,
                                        FontStyles.Normal);
        Row(state, -y, UIScale.RowSm);
        state.color            = SubText;
        state.enableAutoSizing = true;
        state.fontSizeMax      = UIScale.FontSm * 0.8f;
        state.fontSizeMin      = UIScale.FontSm * 0.6f;

        // 배지 둘을 나란히 — 칸 폭 208 안에서 검산한다.
        //   ⚠ 아이콘이 작으면 아무 소용이 없다 (사용자 지적, 2026-09-09)
        //     28px 짜리 물방울·해골은 무슨 그림인지 알아볼 수가 없었다.
        //     글자를 대신하는 그림이므로 **글자보다 커야** 한다.
        //   폭 검산: 배지 하나 = 36 + 6 + 46 = 88, 중심을 ±46 에 두면
        //     좌우 끝이 −90 … +90 = 180 ≤ 208 − 16 ✔
        GameObject manaBadge = FacilityStage.Badge(body, "ManaBadge", manaIcon, 36f,
                                                   UIScale.FontSm, 46f,
                                                   new Color(0.72f, 0.86f, 1f),
                                                   out TextMeshProUGUI manaValue);
        PlaceBadge(manaBadge, -y - 1f, -46f);

        GameObject countBadge = FacilityStage.Badge(body, "CountBadge", countIcon, 36f,
                                                    UIScale.FontSm, 46f,
                                                    new Color(1f, 0.92f, 0.76f),
                                                    out TextMeshProUGUI countValue);
        PlaceBadge(countBadge, -y - 1f, 46f);

        slot.SetActive(false);

        return new CardPickPopupBase.CardCell
        {
            Root       = slot,
            Button     = button,
            Portrait   = portrait,
            Frame      = frame,
            NameText   = name,
            LevelText  = level,
            StateText  = state,
            ManaBadge  = manaBadge,
            ManaValue  = manaValue,
            CountBadge = countBadge,
            CountValue = countValue,
        };
    }

    /// <summary>칸 안의 배지 자리 — 가운데를 기준으로 좌우로 벌린다.</summary>
    static void PlaceBadge(GameObject badge, float yFromTop, float xFromCenter)
    {
        var rt = badge.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(xFromCenter, yFromTop);
    }

    static void Row(TextMeshProUGUI tmp, float y, float h)
    {
        var rt = tmp.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta        = new Vector2(-12f, h);

        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    /// <summary>아래 행동 줄 — 카드를 고른 뒤에만 켜진다.</summary>
    static GameObject ActionBar(GameObject panel, out TextMeshProUGUI hint)
    {
        var bar = EditorUIBuilder.Img(panel, "ActionBar", ActionBg).gameObject;
        {
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 8f);
            rt.sizeDelta        = new Vector2(-(FacilityStage.Margin * 2f), ActionH);
        }

        // 한 줄이다 — 무엇을 고르는 중인지만 말한다. 규칙 설명을 넣지 않는다.
        hint = EditorUIBuilder.TMP(bar, "Hint", "", UIScale.FontMd, FontStyles.Bold);
        {
            var rt = hint.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -12f);
            rt.sizeDelta        = new Vector2(-32f, UIScale.RowMd);
        }
        hint.alignment        = TextAlignmentOptions.Center;
        hint.raycastTarget    = false;
        hint.textWrappingMode = TextWrappingModes.NoWrap;
        hint.overflowMode     = TextOverflowModes.Ellipsis;

        bar.SetActive(false);
        return bar;
    }

    /// <summary>
    /// 행동 버튼 하나 — <b>[이름]   [효과 배지]   [값 배지]</b>. 설명 글은 없다.
    ///
    /// ⚠ 글로 적지 않는 이유 (사용자 지적, 2026-09-09)
    ///   "소환 비용이 영구히 줄어든다" 같은 줄은 버튼을 넘겨 삐져나왔고,
    ///   정작 읽어야 할 숫자를 밀어냈다. 무엇이 오르는지는 아이콘이 말한다.
    ///
    /// ⚠ 아이콘은 글자보다 크다 (사용자 지적, 2026-09-09) — 글자를 대신하는 그림이라
    ///   작으면 무슨 그림인지 못 알아본다. 효과 56 · 값 46 이 지금 값이다.
    /// ⚠ 가로 검산: 여백 26 + 이름 150 + 효과 배지 (56+10+76) + 값 배지 (46+8+104) + 여백 22
    ///   = 498 ≤ 560 ✔  이름을 늘리려면 이 식부터 다시 볼 것.
    /// </summary>
    /// <param name="side">−1 = 왼쪽, +1 = 오른쪽, 0 = 가운데 하나.</param>
    static Button ActionButton(GameObject bar, string name, int side, Color face,
                               string label, Sprite effectIcon, Sprite goldIcon,
                               out TextMeshProUGUI labelTmp,
                               out TextMeshProUGUI effectValue,
                               out GameObject costRoot, out TextMeshProUGUI costValue)
    {
        var slot = EditorUIBuilder.Go(name, bar);
        {
            var rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(side * (ActionBtnW * 0.5f + 20f), 20f);
            rt.sizeDelta        = new Vector2(ActionBtnW, ActionBtnH);
        }

        Button button = EditorUIBuilder.RaisedBtnOn(slot, face, out GameObject body);

        labelTmp = EditorUIBuilder.TMP(body, "Label", label, UIScale.FontLg, FontStyles.Bold);
        {
            var rt = labelTmp.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(26f, 0f);
            rt.sizeDelta        = new Vector2(150f, 0f);
        }
        labelTmp.alignment        = TextAlignmentOptions.MidlineLeft;
        labelTmp.raycastTarget    = false;
        labelTmp.textWrappingMode = TextWrappingModes.NoWrap;

        // 효과 배지 — 가운데. 제단처럼 효과가 숫자가 아닌 화면은 아이콘이 없다.
        effectValue = null;
        if (effectIcon != null)
        {
            GameObject fx = FacilityStage.Badge(body, "EffectBadge", effectIcon, 56f,
                                                UIScale.FontLg, 76f, Color.white,
                                                out effectValue);
            var rt = fx.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-6f, 0f);
        }

        // 값 배지 — 오른쪽. **낼 값은 누를 것과 한 몸이다.**
        costRoot = FacilityStage.Badge(body, "CostBadge", goldIcon, 46f,
                                       UIScale.FontMd, 104f, FacilityStage.GoldText,
                                       out costValue);
        {
            var rt = costRoot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-22f, 0f);
        }

        return button;
    }

    static void WriteCommon(SerializedObject so, PopupType type,
                            Image art, TextMeshProUGUI title, TextMeshProUGUI flavor,
                            TextMeshProUGUI purse,
                            Button closeBtn, CardPickPopupBase.CardCell[] cells,
                            Sprite[] nodeArt)
    {
        EditorUIBuilder.SetEnum(so, "_popupType", (int)type, Tag);
        EditorUIBuilder.SetObj(so, "_art",        art,       Tag);
        EditorUIBuilder.SetObj(so, "_titleText",  title,     Tag);
        EditorUIBuilder.SetObj(so, "_flavorText", flavor,    Tag);
        EditorUIBuilder.SetObj(so, "_purseValue", purse,     Tag);
        EditorUIBuilder.SetObj(so, "_closeBtn",   closeBtn,  Tag);

        // ⚠ RunNodeRule.AllKinds 순서 그대로 — 런타임이 그 인덱스로 그림을 찾는다
        EditorUIBuilder.SetObjArray(so, "_nodeArt", nodeArt, Tag);

        SerializedProperty arr = so.FindProperty("_cells");
        arr.arraySize = cells.Length;
        for (int i = 0; i < cells.Length; i++)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Root")     .objectReferenceValue = cells[i].Root;
            e.FindPropertyRelative("Button")   .objectReferenceValue = cells[i].Button;
            e.FindPropertyRelative("Portrait") .objectReferenceValue = cells[i].Portrait;
            e.FindPropertyRelative("Frame")    .objectReferenceValue = cells[i].Frame;
            e.FindPropertyRelative("NameText") .objectReferenceValue = cells[i].NameText;
            e.FindPropertyRelative("LevelText").objectReferenceValue = cells[i].LevelText;
            e.FindPropertyRelative("StateText") .objectReferenceValue = cells[i].StateText;
            e.FindPropertyRelative("ManaBadge") .objectReferenceValue = cells[i].ManaBadge;
            e.FindPropertyRelative("ManaValue") .objectReferenceValue = cells[i].ManaValue;
            e.FindPropertyRelative("CountBadge").objectReferenceValue = cells[i].CountBadge;
            e.FindPropertyRelative("CountValue").objectReferenceValue = cells[i].CountValue;
        }
    }

    static void Save(GameObject root, string fileName)
    {
        string path = $"Assets/_project/2.Prefabs/UI/{fileName}.prefab";

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[{Tag}] 생성 완료 → {path}\nPopupManager > Load Popup Prefabs 를 눌러야 열린다.");
    }
}
