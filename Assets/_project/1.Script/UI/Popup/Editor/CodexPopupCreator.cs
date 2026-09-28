using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  CodexPopupCreator.cs
//  Tools > Project K > 프리팹 생성 > 팝업 > Codex
//
//  ┌──────────────────────────────────────────────────────────┐  ← 전체 화면
//  │ ◆ 도감   수집 18 / 71              [금] 12,400   [?] [ × ] │
//  ├──────────────────────────────────────────────────────────┤
//  │ [몬스터 12/24] [특성 6/47] [장비 9/36]                     │
//  ├──────────────────────────────────────────────────────────┤
//  │  ┌──────────┐ ┌──────────┐ ┌──────────┐                  │
//  │  │[◈8]  [☠4]│ │          │ │          │                  │
//  │  │   (초상화) │ │   (초상화) │ │    ?     │   (스크롤)      │
//  │  │  힐 슬라임 │ │   고블린   │ │          │                 │
//  │  │    희귀    │ │    일반    │ │          │                 │
//  │  │  ▨ ▨ ▨   │ │  ▨ ▨     │ │          │                 │
//  │  └──────────┘ └──────────┘ └──────────┘                  │
//  └──────────────────────────────────────────────────────────┘
//
//  ■ 탭은 셋이다 (CodexCategory — 몬스터 · 특성 · 장비)
//    어빌리티·장수 탭은 없다. 이 게임에 플레이어의 장수는 없고 어빌리티 축은
//    폐기됐다. 장비 탭은 **몬스터 장비**다 — 원작의 용사 장비가 아니다.
//    ⚠ 탭 이름·개수를 여기에 적지 않는다 — CodexCatalog 가 정본이고
//      아래 BuildTabs 가 enum 을 그대로 돈다. 분류를 늘리면 다시 굽기만 하면 된다.
//
//  ■ 칸이 담는 것이 늘었다 (사용자 요청, 2026-09-06)
//    옛 칸은 계보 아이콘 + 이름 + 등급뿐이라 슬라임 계열 넷이 같은 그림이었고,
//    덱을 짜려면 종족마다 상세 팝업을 열었다 닫아야 했다. 이제 한 칸이
//      [초상화(큼)] [이름] [품질] [마나·마릿수 배지] [시너지 표식]
//    을 함께 그린다 — 격자에서 바로 비교되면 팝업은 확인용으로만 연다.
//
//    ⚠ 초상화는 런타임 합성물이라 Creator 가 넣을 것이 없다
//      칸의 Icon 을 비워 두고 CodexPopup 이 MonsterPortraitProvider 로 채운다.
//      특성 탭은 같은 Icon 에 SO 아이콘을 넣는다.
//
//  ⚠ 셀 구조는 Frame > Fill > (Icon, Name, Sub, BadgeRow, TagRow) 다
//    CodexPopup 이 이 경로로 Find 한다. 이름을 바꾸면 통째로 안 채워진다.
//    Frame(테두리) 안에 Fill(안쪽)을 조금 작게 깔아 테두리를 만든다 —
//    UI 규칙 3 대로 반투명 테두리를 자식으로 얹지 않는다.
//
//  ⚠ 전체 화면이다 — 창으로 띄우면 스크롤만 길어진다.
// ============================================================

public static class CodexPopupCreator
{
    const string SavePath = "Assets/_project/2.Prefabs/UI/CodexPopup.prefab";
    const string Tag      = "CodexPopupCreator";

    const float HeaderH = 118f;
    const float TabH    = 96f;
    const float Pad     = 26f;

    // 닫기 · 도움말(i) 공통 크기 — 둘은 나란히 서므로 **한 상수를 함께 쓴다**
    const float CloseSize = 88f;

    // ── 격자 ──
    //  폭은 7열이 정확히 들어가게 잡았다:
    //    1920 − 좌우 여백 52 − 격자 안쪽 여백 36 = 1832
    //    7 × (248 + 16) − 16 = 1832  ✔
    //  ⚠ 셀 폭을 손대면 이 계산을 다시 할 것 — 어긋나면 마지막 열이 잘린다.
    const float CellW   = 248f;
    const float CellH   = 318f;
    const float CellGap = 16f;
    const float Border  = 3f;     // Frame 이 Fill 밖으로 드러나는 두께 = 테두리

    // 칸 안쪽 자리 (Fill 기준, 위에서부터)
    const float BadgeRowH  = 44f;
    const float BadgeInset = 6f;
    const float BadgeIcon  = 34f;
    const float PortraitY  = 48f;
    const float PortraitSz = 140f;   // 옛 86 → 140. "한눈에 알아보게" 가 목적이다
    const float TagIcon    = 32f;
    const float TagGap     = 8f;
    const float TagBottom  = 8f;

    // 아랫줄(품질). UI 규칙 4 — 폰트는 UIScale 상수에서만 파생시킨다.
    static readonly float SubFont = UIScale.FontSm * 0.82f;
    static readonly float SubH    = UIScale.Line(SubFont);

    static readonly Color CodexC   = new Color(0.27f, 0.87f, 0.80f, 1f);
    static readonly Color GoldC    = new Color(1.00f, 0.83f, 0.30f, 1f);
    static readonly Color ScrimBg  = new Color(0.035f, 0.040f, 0.070f, 0.97f);
    static readonly Color GridBg   = new Color(0.055f, 0.062f, 0.105f, 1f);
    static readonly Color CloseC   = new Color(0.62f, 0.20f, 0.24f, 1f);
    static readonly Color CellFill = new Color(0.125f, 0.140f, 0.215f, 1f);
    static readonly Color CellEdge = new Color(0.32f, 0.36f, 0.52f, 1f);
    static readonly Color CountC   = new Color(1.00f, 0.96f, 0.85f, 1f);

    // ⚠ 이 프로젝트에서 실제로 뜬다 — 올드Tools 가 아니다
    //   MainPanelUI 의 도감 버튼이 연다.
    [MenuItem(ProjectKMenu.Popup + "Codex", priority = ProjectKMenu.PrefabPrio + 50)]
    public static void Run()
    {
        // ⚠ 아이콘이 없으면 굽지 않는다
        //   빈 배지·빈 칩을 구워 두면 프리팹만 보고는 무엇이 빠졌는지 알 수 없다.
        if (!UIIconAssets.TryLoad(Tag, out Sprite manaIcon, out Sprite countIcon)) return;
        if (!UIIconAssets.TryLoadGold(Tag, out Sprite goldIcon))                  return;
        if (!SynergyIconAssets.TryLoad(Tag, out Sprite[] synergyIcons))           return;

        var root = new GameObject("CodexPopup", typeof(RectTransform));
        root.AddComponent<CanvasGroup>();
        var popup = root.AddComponent<CodexPopup>();

        // 전체 화면 — 부모(캔버스)에 꽉 채운다
        EditorUIBuilder.Stretch(root);

        var scrim = EditorUIBuilder.Img(root, "Scrim", ScrimBg);
        EditorUIBuilder.Stretch(scrim.gameObject);

        BuildHeader(root, goldIcon, out var progressTmp, out var goldTmp, out var closeBtn);
        BuildTabs(root, out var tabButtons, out var tabLabels, out var tabBodies);

        var grid    = BuildGrid(root, manaIcon, countIcon);
        var tooltip = BuildTooltip(root);

        var so = new SerializedObject(popup);
        EditorUIBuilder.SetEnum(so, "_popupType", (int)PopupType.Codex, Tag);
        EditorUIBuilder.SetObj(so, "_progressTmp", progressTmp, Tag);
        EditorUIBuilder.SetObj(so, "_goldTmp",     goldTmp,     Tag);
        EditorUIBuilder.SetObj(so, "_closeBtn",    closeBtn,    Tag);
        EditorUIBuilder.SetObj(so, "_grid",        grid,        Tag);
        EditorUIBuilder.SetObj(so, "_tooltip",     tooltip,     Tag);
        EditorUIBuilder.SetObjArray(so, "_tabButtons", tabButtons, Tag);
        EditorUIBuilder.SetObjArray(so, "_tabLabels",  tabLabels,  Tag);
        EditorUIBuilder.SetObjArray(so, "_tabBodies",  tabBodies,  Tag);

        // ⚠ MonsterSynergyRule.AllTags 순서 그대로 — 런타임이 그 인덱스로 그림을 찾는다
        EditorUIBuilder.SetObjArray(so, "_synergyIcons", synergyIcons, Tag);

        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CodexPopupCreator] 생성 완료 → " + SavePath +
                  "\nPopupManager 의 _prefabs 배열에 등록해야 열린다.");
    }

    // ── 헤더 ─────────────────────────────────────────────────

    static void BuildHeader(GameObject root, Sprite goldIcon,
                            out TextMeshProUGUI progress, out TextMeshProUGUI gold,
                            out Button close)
    {
        var header = EditorUIBuilder.Img(root, "Header", EditorUIBuilder.Pop.HeaderBg).gameObject;
        {
            var rt = header.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -HeaderH);
            rt.offsetMax = Vector2.zero;
        }

        // 아래 테두리 — 헤더와 탭을 눈으로 가른다
        var edge = EditorUIBuilder.Img(header, "BottomEdge", EditorUIBuilder.Pop.Divider);
        {
            var rt = edge.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0f, 3f);
        }
        edge.raycastTarget = false;

        // ◆ 마름모는 도형으로 그린다 (UI 규칙 2 — 장식 기호에 글리프를 쓰지 않는다)
        var dia = EditorUIBuilder.Diamond(header, "Diamond", 26f, CodexC);
        {
            var rt = dia.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(Pad + 6f, 0f);
        }

        var title = EditorUIBuilder.TMP(header, "Title", "도감", UIScale.FontLg, FontStyles.Bold);
        title.color     = CodexC;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        {
            var rt = title.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(Pad + 44f, 0f);
            rt.sizeDelta        = new Vector2(280f, UIScale.RowLg);
        }

        progress = EditorUIBuilder.TMP(header, "Progress", "수집 0 / 0", UIScale.FontMd, FontStyles.Bold);
        progress.color     = EditorUIBuilder.Pop.SubText;
        progress.alignment = TextAlignmentOptions.MidlineLeft;
        {
            var rt = progress.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(Pad + 340f, 0f);
            rt.sizeDelta        = new Vector2(420f, UIScale.RowMd);
        }

        // ── 보유 골드 ──
        //  ⚠ 옛 자리에는 "공격력·체력 +42.0%" 가 있었다 — 수집 버프는 폐기됐다.
        //    지갑은 늘 같은 자리(화면 위)에 있고, 낼 값은 누를 것(버튼) 옆에 있다.
        //    두 숫자를 붙여 두면 어느 쪽이 낼 돈인지 매번 다시 읽어야 한다.
        //  ⚠ "골드 12,400" 이라고 글자로 쓰지 않는다 — 재화는 아이콘이다 (UI 규칙 7)
        var walletRoot = EditorUIBuilder.Go("Wallet", header);
        {
            var rt = walletRoot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            // 닫기(88) + 도움말 묶음 왼쪽
            rt.anchoredPosition = new Vector2(-(EditorUIBuilder.HeaderRightBlock(CloseSize, Pad) + 20f), 0f);
            rt.sizeDelta        = new Vector2(280f, 56f);
        }

        var walletIcon = EditorUIBuilder.Img(walletRoot, "GoldIcon", Color.white);
        walletIcon.sprite         = goldIcon;
        walletIcon.preserveAspect = true;
        walletIcon.raycastTarget  = false;
        {
            var rt = walletIcon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(48f, 48f);
        }

        gold = EditorUIBuilder.TMP(walletRoot, "GoldValue", "0", UIScale.FontMd, FontStyles.Bold);
        gold.color         = GoldC;
        gold.alignment     = TextAlignmentOptions.MidlineLeft;
        gold.raycastTarget = false;
        {
            var rt = gold.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(58f, 0f); rt.offsetMax = Vector2.zero;
        }

        // UI 규칙 1 — 누를 수 있는 버튼은 음각. 라벨은 body 아래에 넣는다.
        close = EditorUIBuilder.RaisedBtn(header, "CloseBtn", CloseC, out var closeBody);
        {
            var rt = close.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-Pad, 0f);
            rt.sizeDelta        = new Vector2(CloseSize, CloseSize);
        }
        // 도움말 — 닫기 버튼 왼쪽. **같은 크기·같은 세로 자리**여야 한다 (InfoBtn 주석)
        //  ⚠ 닫기를 88×78 · y+4 로 두었을 때 i 버튼(88×88 · y0)과 크기·높이가 어긋나
        //    머리 줄이 삐뚤어 보였다 (사용자 지적, 2026-09-18). 정사각형 상수 하나로 묶는다.
        EditorUIBuilder.InfoBtn(header, TutorialId.HelpCodex, CloseSize, -Pad);

        var x = EditorUIBuilder.XMark(closeBody, "X", 34f, Color.white);
        {
            var rt = x.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }
    }

    // ── 탭 ───────────────────────────────────────────────────

    static void BuildTabs(GameObject root, out Object[] buttons, out Object[] labels, out Object[] bodies)
    {
        var bar = EditorUIBuilder.Go("TabBar", root);
        {
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(Pad, -(HeaderH + TabH));
            rt.offsetMax = new Vector2(-Pad, -HeaderH);
        }

        var hlg = bar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childControlWidth = true;  hlg.childControlHeight = true;
        // ⚠ 늘려 채우지 않는다
        //   force expand 로 두면 탭 하나가 화면을 분할해 버린다 — 다섯 탭이던
        //   시절의 설정이다. 폭을 고정(340)하고 왼쪽에 붙인다.
        //   셋이면 3×340 + 2×12 = 1044 ≤ 1868. ✔ 탭이 더 늘면 이 검산을 다시 할 것.
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.padding = new RectOffset(0, 0, 12, 10);

        // 배열 순서 = CodexCategory 순서. CodexPopup 이 인덱스로 매핑한다.
        // ⚠ 이름을 손으로 적지 않는다 — CodexCatalog.Label 이 정본이다
        var cats  = (CodexCategory[])System.Enum.GetValues(typeof(CodexCategory));
        var names = new string[cats.Length];
        for (int i = 0; i < cats.Length; i++) names[i] = CodexCatalog.Label(cats[i]);

        var btnArr = new Object[names.Length];
        var lblArr = new Object[names.Length];
        var bodArr = new Object[names.Length];

        for (int i = 0; i < names.Length; i++)
        {
            var go  = EditorUIBuilder.Go($"Tab{i}", bar);
            EditorUIBuilder.LE(go, 340f, 0f);

            var btn = EditorUIBuilder.RaisedBtnOn(go, EditorUIBuilder.Pop.TabInactive, out var body);

            var tmp = EditorUIBuilder.TMP(body, "Label", $"{names[i]}  <size=80%>0/0</size>",
                                          UIScale.FontMd, FontStyles.Bold);
            tmp.alignment = TextAlignmentOptions.Center;
            EditorUIBuilder.Stretch(tmp.gameObject);

            btnArr[i] = btn;
            lblArr[i] = tmp;
            bodArr[i] = body.GetComponent<Image>();
        }

        buttons = btnArr;
        labels  = lblArr;
        bodies  = bodArr;
    }

    // ── 격자 ─────────────────────────────────────────────────

    static RecycleGridScroll BuildGrid(GameObject root, Sprite manaIcon, Sprite countIcon)
    {
        var box = EditorUIBuilder.Img(root, "GridBox", GridBg).gameObject;
        {
            var rt = box.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(Pad, Pad);
            rt.offsetMax = new Vector2(-Pad, -(HeaderH + TabH + 6f));
        }
        box.AddComponent<RectMask2D>();

        var scroll = box.AddComponent<ScrollRect>();
        scroll.horizontal   = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 46f;

        var content = EditorUIBuilder.Go("Content", box);
        {
            var rt = content.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        // ⚠ GridLayoutGroup·ContentSizeFitter 를 붙이지 않는다
        //   위치와 Content 높이는 RecycleGridScroll 이 직접 계산한다.
        //   레이아웃 그룹이 같이 있으면 매 프레임 서로 값을 덮어써 칸이 떨린다.
        scroll.viewport = box.GetComponent<RectTransform>();
        scroll.content  = content.GetComponent<RectTransform>();

        var cellTemplate = BuildCell(content, manaIcon, countIcon);

        var grid = box.AddComponent<RecycleGridScroll>();
        var gso  = new SerializedObject(grid);
        EditorUIBuilder.SetObj(gso, "_scroll",       scroll,                                Tag);
        EditorUIBuilder.SetObj(gso, "_viewport",     box.GetComponent<RectTransform>(),     Tag);
        EditorUIBuilder.SetObj(gso, "_content",      content.GetComponent<RectTransform>(), Tag);
        EditorUIBuilder.SetObj(gso, "_cellTemplate", cellTemplate,                          Tag);
        gso.FindProperty("_cellSize").vector2Value = new Vector2(CellW, CellH);
        gso.FindProperty("_spacing").vector2Value  = new Vector2(CellGap, CellGap);
        gso.FindProperty("_padLeft").floatValue    = 18f;
        gso.FindProperty("_padRight").floatValue   = 18f;
        gso.FindProperty("_padTop").floatValue     = 18f;
        gso.FindProperty("_padBottom").floatValue  = 18f;
        gso.ApplyModifiedProperties();

        return grid;
    }

    // 셀 — Frame(테두리) > Fill(안쪽) > Icon + Name + Sub + BadgeRow + TagRow.
    // Fill 을 Border 만큼 안쪽으로 넣어 Frame 이 가장자리로만 드러나게 한다.
    static GameObject BuildCell(GameObject parent, Sprite manaIcon, Sprite countIcon)
    {
        var cell = EditorUIBuilder.Go("CellTemplate", parent);
        var btn  = cell.AddComponent<Button>();

        var frame = EditorUIBuilder.Img(cell, "Frame", CellEdge);
        EditorUIBuilder.Stretch(frame.gameObject);
        btn.targetGraphic = frame;
        // 누름 표시는 테두리 밝기로 준다 (targetGraphic 색에 곱해진다 — UI 규칙 1)
        EditorUIBuilder.TintTransition(cell, CellEdge);

        var fill = EditorUIBuilder.Img(frame.gameObject, "Fill", CellFill);
        {
            var rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(Border, Border);
            rt.offsetMax = new Vector2(-Border, -Border);
        }
        fill.raycastTarget = false;

        BuildBadgeRow(fill.gameObject, manaIcon, countIcon);

        // 초상화(몬스터) 또는 아이콘(특성). ⚠ 여기서 그림을 넣지 않는다 —
        // 몬스터 초상화는 런타임 합성물이라 CodexPopup 이 채운다.
        var icon = EditorUIBuilder.Img(fill.gameObject, "Icon", Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        {
            var rt = icon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -PortraitY);
            rt.sizeDelta        = new Vector2(PortraitSz, PortraitSz);
        }

        // 이름 — 초상화 바로 아래
        var name = EditorUIBuilder.TMP(fill.gameObject, "Name", "?", UIScale.FontSm, FontStyles.Bold);
        name.alignment     = TextAlignmentOptions.Top;
        name.raycastTarget = false;
        // 이름이 길면 줄여서 담는다 — 두 줄이 되면 아랫줄을 밀어낸다 (UI 규칙 5)
        name.enableAutoSizing = true;
        name.fontSizeMin      = UIScale.FontSm * 0.60f;
        name.fontSizeMax      = UIScale.FontSm;
        {
            var rt = name.rectTransform;
            EditorUIBuilder.AnchorTop(rt, PortraitY + PortraitSz + 4f,
                                      UIScale.Line(UIScale.FontSm), 6f);
        }

        // 품질 — 이름보다 작고, 등급색으로 칠한다 (테두리와 같은 색)
        var sub = EditorUIBuilder.TMP(fill.gameObject, "Sub", "", SubFont, FontStyles.Normal);
        sub.alignment     = TextAlignmentOptions.Top;
        sub.raycastTarget = false;
        sub.enableAutoSizing = true;
        sub.fontSizeMin      = SubFont * 0.62f;
        sub.fontSizeMax      = SubFont;
        {
            var rt = sub.rectTransform;
            EditorUIBuilder.AnchorTop(rt, PortraitY + PortraitSz + 4f + UIScale.Line(UIScale.FontSm),
                                      SubH, 6f);
        }

        BuildTagRow(fill.gameObject);

        cell.SetActive(false);
        return cell;
    }

    /// <summary>
    /// 칸 위쪽 모서리의 [마나][마릿수] 한 쌍.
    ///
    /// ⚠ 글자로 "마나 8" 이라고 쓰지 않는다 (UI 규칙 7)
    /// ⚠ 폭을 검산할 것 — BadgeWidthFor × 2 + inset × 2 ≤ Fill 폭
    ///   34px 아이콘 + FontSm 이면 배지 하나가 79, 둘이면 158 + 12 = 170 ≤ 242. ✔
    /// </summary>
    static void BuildBadgeRow(GameObject fill, Sprite manaIcon, Sprite countIcon)
    {
        var row = EditorUIBuilder.Go("BadgeRow", fill);
        {
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -BadgeRowH);
            rt.offsetMax = Vector2.zero;
        }

        EditorUIBuilder.IconValueBadge(row, "ManaCost", manaIcon, rightSide: false,
                                       BadgeIcon, UIScale.FontSm, BadgeInset, Color.white,
                                       out _);

        // ⚠ 배지 안쪽 순서는 양쪽 다 [아이콘][숫자] 로 같다 (UI 규칙 7)
        //   rightSide 로 자리만 오른쪽으로 옮긴다.
        var countRoot = EditorUIBuilder.Go("CountHolder", row);
        EditorUIBuilder.Stretch(countRoot);

        EditorUIBuilder.IconValueBadge(countRoot, "SummonCount", countIcon, rightSide: true,
                                       BadgeIcon, UIScale.FontSm, BadgeInset, CountC,
                                       out _);
    }

    /// <summary>
    /// 칸 맨 아랫줄의 시너지 표식 — 최대 세 개 (CodexPopup.TagSlots).
    ///
    /// ⚠ 시너지 이름은 글자가 아니라 그림이다
    ///   그림을 꽂는 것은 런타임이다 (표식이 종족마다 다르다). 여기서는
    ///   빈 칸만 굽고 전부 꺼 둔다 — 켜 둔 채로 구우면 흰 사각형이 셋 보인다.
    /// </summary>
    static void BuildTagRow(GameObject fill)
    {
        var row = EditorUIBuilder.Go("TagRow", fill);
        {
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0f, TagBottom);
            rt.offsetMax = new Vector2(0f, TagBottom + TagIcon);
        }

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = TagGap;
        hlg.childAlignment        = TextAnchor.MiddleCenter;
        hlg.childControlWidth     = true;  hlg.childControlHeight     = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        for (int i = 0; i < CodexPopup.TagSlots; i++)
        {
            var img = EditorUIBuilder.Img(row, $"Tag{i}", Color.white);
            img.preserveAspect = true;
            img.raycastTarget  = false;
            EditorUIBuilder.LE(img.gameObject, TagIcon, TagIcon);
            img.gameObject.SetActive(false);
        }
    }

    // ── 정보 툴팁 ────────────────────────────────────────────
    //  특성을 눌렀을 때 뜬다. 몬스터는 MonsterDetailPopup 을 쓴다.
    //
    //  ⚠ 직접 만들지 말고 InfoTooltipBuilder 를 쓴다
    //    특성 아이콘·보상 카드와 같은 모양·같은 동작이어야 한다.
    //
    //  ⚠ 여기서는 팝업 루트에 하나만 만든다
    //    칸마다 붙일 수 없다. 실제 위치는 런타임에 ShowAnchored 가 다시 잡는다.
    //  ⚠ 폭 420 → 600 (사용자 지적, 2026-09-17) — 번역문은 한국어보다 길어 420 이면
    //    짧은 설명도 서너 줄이 됐다. 높이는 ContentSizeFitter 가 글만큼 늘린다(줄 수 제한 없음).
    static InfoTooltipUI BuildTooltip(GameObject root)
        => InfoTooltipBuilder.Build(root, 600f);
}
