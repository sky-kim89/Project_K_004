using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  BattleInfoPopupCreator.cs  [Editor Only]
//  Tools > Project K > 프리팹 생성 > 팝업 > 전황
//
//  ■ 전체화면 · 가운데를 기준으로 좌우가 갈린다
//      왼쪽  아군 진형 — 덱에 든 카드 (4열 × 2줄)
//      가운데 세로 금 + "VS"
//      오른쪽 적군 진형 — 라인 5줄 (위에서 아래가 전장의 줄 번호다)
//
//  ⚠ 적을 목록으로 늘어놓지 않는다
//    다섯 칸을 세로로 고정해 두고 비는 줄은 끈다. 줄 번호가 자리로 읽혀야
//    "가운데가 무겁다" 같은 판단이 선다 — 그게 이 화면의 존재 이유다.
//
//  ⚠ 아군은 라인이 없다
//    아군은 플레이어가 낼 때 라인을 고르므로 진형이 미리 정해져 있지 않다.
//    그래서 격자로 늘어놓는다 — 좌우가 다른 모양인 것이 맞다.
//
//  ⚠ 상세 패널은 여기서 만들지 않는다 (사용자 지적, 2026-09-07)
//    이미 몬스터 상세(MonsterDetailPopup)와 영웅 상세(HeroDetailPopup)가 있다.
//    같은 것을 두 번 그리면 스탯 줄 하나를 고칠 때마다 두 곳을 고쳐야 하고,
//    반드시 한쪽만 고쳐져 값이 갈린다. 칸을 누르면 그 창들이 열린다.
// ============================================================

public static class BattleInfoPopupCreator
{
    const string SavePath = "Assets/_project/2.Prefabs/UI/BattleInfoPopup.prefab";
    const string Tag      = "BattleInfoPopupCreator";

    const float Margin  = 48f;
    const float HeaderH = 120f;

    // ── 칸 ───────────────────────────────────────────────────
    //
    //  ⚠ 아군 칸 높이는 얹는 것들의 합이다 — 눈대중으로 적지 말 것
    //    배지 30 + 16 + 초상화 132 + 4 + 이름 43 + 6 + 아래배지 43 + 여백 10 = 284.
    //    한 줄이라도 늘리면 여기를 다시 더해야 한다. 224 였을 때 1px 이 모자라
    //    아래 배지가 칸 밖으로 밀렸다.
    const float SlotW     = 190f;
    const float SlotH     = 284f;
    const float SlotGap   = 16f;
    const float PortraitS = 132f;

    /// <summary>적 줄 칸 높이 — 다섯 줄이 세로로 들어가야 한다.</summary>
    const float LaneH     = 139f;

    static readonly Color Scrim     = new(0.02f, 0.02f, 0.05f, 0.93f);
    static readonly Color AllyFace  = new(0.105f, 0.135f, 0.205f, 0.98f);
    static readonly Color EnemyFace = new(0.185f, 0.105f, 0.125f, 0.98f);
    static readonly Color PanelBg   = new(0.075f, 0.082f, 0.135f, 0.99f);
    static readonly Color SubText   = new(0.74f,  0.79f,  0.92f,  1f);
    static readonly Color Divider   = new(0.30f,  0.34f,  0.50f,  0.85f);

    [MenuItem(ProjectKMenu.Popup + "전황", priority = ProjectKMenu.PrefabPrio + 54)]
    public static void Run()
    {
        // ⚠ 마나·마릿수 아이콘이 없으면 굽지 않는다 — 배지가 그림 없이 숫자만
        //   남으면 무슨 값인지 알 수 없다 (UI 규칙 7).
        if (!UIIconAssets.TryLoad(Tag, out Sprite manaIcon, out Sprite countIcon)) return;

        var root  = new GameObject("BattleInfoPopup", typeof(RectTransform));
        root.AddComponent<CanvasGroup>();
        var popup = root.AddComponent<BattleInfoPopup>();
        EditorUIBuilder.Stretch(root);

        var bg = EditorUIBuilder.Img(root, "Scrim", Scrim);
        EditorUIBuilder.Stretch(bg.gameObject);
        bg.raycastTarget = true;   // 뒤쪽 전장이 눌리지 않게 막는다

        // ── 머리 ──
        var title = EditorUIBuilder.TMP(root, "Title", "전황", UIScale.FontXl, FontStyles.Bold);
        {
            var rt = title.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(Margin, -Margin);
            rt.sizeDelta        = new Vector2(-(Margin * 2f + 140f), UIScale.RowLg);
        }
        title.alignment        = TextAlignmentOptions.MidlineLeft;
        title.raycastTarget    = false;
        title.textWrappingMode = TextWrappingModes.NoWrap;

        // ── 가운데 금 ──
        //   ⚠ 좌우를 가르는 것이 이 화면의 뼈대다. 선 하나로 충분하다.
        var line = EditorUIBuilder.Img(root, "Divider", Divider);
        {
            var rt = line.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -(HeaderH * 0.5f));
            rt.sizeDelta        = new Vector2(3f, -(HeaderH + Margin * 2f));
        }
        line.raycastTarget = false;

        var vs = EditorUIBuilder.TMP(root, "VS", "VS", UIScale.FontLg, FontStyles.Bold);
        {
            var rt = vs.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -Margin);
            rt.sizeDelta        = new Vector2(140f, UIScale.RowLg);
        }
        vs.alignment     = TextAlignmentOptions.Midline;
        vs.color         = SubText;
        vs.raycastTarget = false;

        // ⚠ 라벨은 각 진형의 **격자 왼쪽 끝**에 맞춘다 — 화면 가장자리에 붙이면
        //   격자와 멀리 떨어져 어느 쪽을 가리키는 말인지 갈리지 않는다.
        Label(root, "AllyLabel",  "아군 진형",  76f,           -(HeaderH - UIScale.RowMd - 6f));
        Label(root, "EnemyLabel", "적군 진형",  960f + Margin, -(HeaderH - UIScale.RowMd - 6f));

        // ── 아군 격자 (왼쪽 절반, 4열 × 2줄) ──
        //
        //  ⚠ 2열 × 4줄이면 화면 밖으로 넘친다 (사용자 지적, 2026-09-07)
        //    4줄 × (224 + 16) = 960 인데 머리(160)를 빼면 920 만 남는다 —
        //    아래 두 줄이 1080 밖으로 밀려 아예 안 보였다.
        //    왼쪽 절반의 **가로**는 넉넉하다(48 ~ 912, 864). 그쪽으로 눕힌다:
        //      가로 4 × 190 + 3 × 16 = 808  ≤ 864
        //      세로 2 × 224 + 1 × 16 = 464  ≤ 920
        var allies = new List<BattleInfoPopup.SlotView>(BattleInfoPopup.MaxAllySlots);
        {
            const int Cols = 4;

            float gridW = SlotW * Cols + SlotGap * (Cols - 1);

            // 왼쪽 절반(Margin ~ 960-Margin) 안에서 가운데 정렬한다.
            float x0 = Margin + ((960f - Margin * 2f) - gridW) * 0.5f;
            float y0 = -(HeaderH + 40f);

            for (int i = 0; i < BattleInfoPopup.MaxAllySlots; i++)
            {
                float x = x0 + (i % Cols) * (SlotW + SlotGap);
                float y = y0 - (i / Cols) * (SlotH + SlotGap);
                allies.Add(BuildSlot(root, $"Ally{i}", AllyFace, x, y, manaIcon, countIcon));
            }
        }

        // ── 적군 줄 (오른쪽 절반, 세로 5칸) ──
        //   ⚠ 위에서 아래가 곧 전장의 줄 번호 1~5 다.
        var enemies = new List<BattleInfoPopup.SlotView>(BattleInfoPopup.MaxEnemySlots);
        {
            float x  = 960f + Margin;
            float y0 = -(HeaderH + 40f);
            for (int i = 0; i < BattleInfoPopup.MaxEnemySlots; i++)
                enemies.Add(BuildLaneSlot(root, $"Enemy{i}", EnemyFace, i + 1,
                                          x, y0 - i * (LaneH + SlotGap), LaneH));
        }

        // ── 닫기 ──
        Button close = EditorUIBuilder.RaisedBtn(root, "CloseBtn",
                                                 new Color(0.20f, 0.16f, 0.24f), out var body);
        {
            var rt = close.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-Margin, -Margin);
            rt.sizeDelta        = new Vector2(84f, 84f);
        }
        EditorUIBuilder.XMark(body, "X", 36f, new Color(0.90f, 0.92f, 0.97f));

        // ── 배선 ──
        var so = new SerializedObject(popup);
        EditorUIBuilder.SetEnum(so, "_popupType", (int)PopupType.BattleInfo, Tag);
        EditorUIBuilder.SetObj(so, "_titleText",      title,      Tag);
        EditorUIBuilder.SetObj(so, "_closeBtn",       close,      Tag);

        WriteSlots(so, "_allySlots",  allies);
        WriteSlots(so, "_enemySlots", enemies);
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);

        Debug.Log($"[{Tag}] 저장: {SavePath}\n" +
                  "⚠ PopupManager 의 [Load Popup Prefabs] 를 눌러야 열립니다.");
    }

    // ── 칸 ───────────────────────────────────────────────────

    /// <summary>
    /// 아군 칸 — 하단 소환 카드와 **같은 얼개**로 읽히게 만든다.
    ///
    /// ■ ⚠ 왜 카드처럼 만드나 (사용자 지적, 2026-09-07)
    ///   초상화 + 이름 + 작은 글씨 둘만 있던 때는 "덱에 뭐가 있나" 만 알 뿐
    ///   **고를 판단에 필요한 것이 없었다.** 플레이어가 하단 카드 바에서 늘 보는
    ///   것은 [마나][마릿수] 배지와 레벨이다. 같은 배치로 두면 두 화면이
    ///   한 물건으로 읽히고, 전황에서 본 것을 그대로 카드 바에서 찾을 수 있다.
    ///
    ///   ⚠ 마나·마릿수는 **글자가 아니라 배지**다 (UI 규칙 7).
    ///     "마나 5" 라고 적으면 화면마다 표기가 갈려 같은 값을 매번 다시 읽는다.
    ///
    /// ■ 얼개 (위에서 아래로)
    ///     [마나]        [마릿수]     ← 위 모서리 좌우
    ///          초상화
    ///          이름
    ///     [Lv]          [등급]       ← 아래 모서리 좌우
    /// </summary>
    static BattleInfoPopup.SlotView BuildSlot(GameObject parent, string name, Color face,
                                              float x, float y,
                                              Sprite manaIcon, Sprite countIcon)
    {
        Button btn = EditorUIBuilder.RaisedBtn(parent, name, face, out GameObject body);
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(SlotW, SlotH);

        // ── 위 모서리: 마나 · 마릿수 배지 ──
        //   ⚠ 좌우 한 쌍의 **안쪽 순서는 양쪽 다 같다** (UI 규칙 7).
        //     거울상으로 뒤집으면 두 숫자가 가운데에서 마주 봐 헷갈린다.
        const float BadgeIcon = 30f;

        EditorUIBuilder.IconValueBadge(body, "ManaBadge", manaIcon, rightSide: false,
                                       BadgeIcon, UIScale.FontSm, 10f, Color.white,
                                       out TextMeshProUGUI manaText);

        EditorUIBuilder.IconValueBadge(body, "CountBadge", countIcon, rightSide: true,
                                       BadgeIcon, UIScale.FontSm, 10f,
                                       new Color(1f, 0.94f, 0.80f),
                                       out TextMeshProUGUI countText);

        // ── 초상화 ──
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

        // ── 이름 ──
        var nameTmp = EditorUIBuilder.TMP(body, "NameText", "", UIScale.FontSm, FontStyles.Bold);
        {
            var r = nameTmp.rectTransform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot     = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(6f, -(BadgeIcon + 16f + PortraitS + 4f + UIScale.RowSm));
            r.offsetMax = new Vector2(-6f, -(BadgeIcon + 16f + PortraitS + 4f));
        }
        nameTmp.alignment        = TextAlignmentOptions.Midline;
        nameTmp.raycastTarget    = false;
        nameTmp.textWrappingMode = TextWrappingModes.NoWrap;

        // ── 아래 모서리: 레벨 · 품질 ──
        TextMeshProUGUI left  = Badge(body, "LeftBadge",  align: true);
        TextMeshProUGUI right = Badge(body, "RightBadge", align: false);

        // 레벨은 금색으로 — 하단 카드 바의 레벨 배지와 같은 색이다.
        left.color = new Color(1f, 0.86f, 0.42f);

        return Pack(btn, portrait, nameTmp, left, right, manaText, countText);
    }

    /// <summary>적 줄 칸 — 왼쪽에 줄 번호가 붙는 가로형.</summary>
    static BattleInfoPopup.SlotView BuildLaneSlot(GameObject parent, string name, Color face,
                                                  int laneNumber, float x, float y, float h)
    {
        Button btn = EditorUIBuilder.RaisedBtn(parent, name, face, out GameObject body);
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(SlotW * 2f + SlotGap, h);

        // ⚠ 줄 번호는 고정이다 — 그 줄에 적이 없어도 자리는 남는다.
        var lane = EditorUIBuilder.TMP(body, "LaneNo", laneNumber.ToString(),
                                       UIScale.FontLg, FontStyles.Bold);
        {
            var r = lane.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot     = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(14f, 0f);
            r.sizeDelta        = new Vector2(44f, UIScale.RowLg);
        }
        lane.alignment     = TextAlignmentOptions.Midline;
        lane.color         = SubText;
        lane.raycastTarget = false;

        float ps = h - 20f;

        var portrait = EditorUIBuilder.Img(body, "Portrait", Color.white);
        portrait.preserveAspect = true;
        portrait.raycastTarget  = false;
        {
            var r = portrait.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot     = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(64f, 0f);
            r.sizeDelta        = new Vector2(ps, ps);
        }

        float textLeft = 64f + ps + 14f;

        var nameTmp = EditorUIBuilder.TMP(body, "NameText", "", UIScale.FontSm, FontStyles.Bold);
        {
            var r = nameTmp.rectTransform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot     = new Vector2(0f, 1f);
            r.offsetMin = new Vector2(textLeft, -(10f + UIScale.RowSm));
            r.offsetMax = new Vector2(-14f, -10f);
        }
        nameTmp.alignment        = TextAlignmentOptions.MidlineLeft;
        nameTmp.raycastTarget    = false;
        nameTmp.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI left  = Badge(body, "LeftBadge",  align: true,  fromLeft: textLeft);
        TextMeshProUGUI right = Badge(body, "RightBadge", align: false);

        return Pack(btn, portrait, nameTmp, left, right);
    }

    // ── 공용 조각 ────────────────────────────────────────────

    static TextMeshProUGUI Label(GameObject parent, string name, string text, float x, float y)
    {
        var tmp = EditorUIBuilder.TMP(parent, name, text, UIScale.FontMd, FontStyles.Bold);
        var rt  = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(360f, UIScale.RowMd);
        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
        tmp.color            = SubText;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    static TextMeshProUGUI Row(GameObject body, string name, float font, Color color,
                               float yFromTop, bool stretch)
    {
        var tmp = EditorUIBuilder.TMP(body, name, "", font, FontStyles.Bold);
        var r   = tmp.rectTransform;
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
        r.pivot     = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(8f, yFromTop - UIScale.RowSm);
        r.offsetMax = new Vector2(-8f, yFromTop);
        tmp.alignment        = TextAlignmentOptions.Midline;
        tmp.color            = color;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    /// <summary>초상화 아래 왼쪽·오른쪽에 붙는 작은 값 (Lv · 병사 수).</summary>
    static TextMeshProUGUI Badge(GameObject body, string name, bool align, float fromLeft = 10f)
    {
        var tmp = EditorUIBuilder.TMP(body, name, "", UIScale.FontSm, FontStyles.Normal);
        var r   = tmp.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(align ? 0f : 1f, 0f);
        r.pivot     = new Vector2(align ? 0f : 1f, 0f);
        r.anchoredPosition = new Vector2(align ? fromLeft : -12f, 10f);
        r.sizeDelta        = new Vector2(180f, UIScale.RowSm);
        tmp.alignment        = align ? TextAlignmentOptions.MidlineLeft
                                     : TextAlignmentOptions.MidlineRight;
        tmp.color            = SubText;
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
}
