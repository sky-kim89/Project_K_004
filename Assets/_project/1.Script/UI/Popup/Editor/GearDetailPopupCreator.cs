using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  GearDetailPopupCreator.cs
//  Tools > Project K > 프리팹 생성 > 팝업 > 장비 상세
//
//  ┌──────────────────────────────────────────────────────────────────┐  ← 가운데 창
//  │ ┌────┐ 흑기사의 판금 Lv3        ┌────┐ 착용          [금] 12,400 [×] │
//  │ │ 🛡 │ 유일 · 갑옷 · 인간형      │초상│ 오크 킹                       │
//  │ └────┘                          └────┘                               │
//  ├────────────────────────┬─────────────────────────────────────────┤
//  │ 능 력 치                │ 레 벨                                     │
//  │ ▕체력 ·········· +99 ▏  │ ▌(1) 체력 +66              ← 연 줄: 등급색 띠 │
//  │ ▕체력 ·········· +6% ▏  │ ▌(2) 체력 +33                              │
//  │ ▕패시브 ····· 역병 폭발▏ │ ▌(3) 체력 +6%              ← 다음: 금색 띠   │
//  │  [마나] -1               │  (4) 체력 +33   [패시브]   ← 아직: 흐리게    │
//  │ …설명…                  │  (5) …                                     │
//  │                          │  강화 — Lv5 부터                           │
//  ├────────────────────────┴─────────────────────────────────────────┤
//  │ [ 장착 / 옮겨 장착 / 벗기기 ]         [ 레벨 업  [▨]×2  [금] 900 ]    │
//  └──────────────────────────────────────────────────────────────────┘
//  ⚠ 능력치는 같은 것을 합쳐 [이름 ···· 값] 줄로 적는다 (2026-09-12) — 한 칸 글 뭉치는 안 읽혔다
//
//  ■ 전체 화면이 아니다 — 가운데 창이다
//    몬스터 상세(전체 화면) 위에 겹쳐 뜬다. 뒤에 그 몬스터의 스탯이 비쳐 보여야
//    "이걸 올리면 무엇이 오르나" 를 대조할 수 있다.
//
//  ■ 재료는 **[장비 그림][×N]** 로 센다 (사용자 지시, 2026-09-12 — 그림을 N개 늘어놓던 것을 바꿨다)
//    모자라면 숫자가 붉다. "같은 장비가 N개 더 필요하다" 안내 줄은 없앴다.
//
//  ⚠ 마나는 글자가 아니라 아이콘이다 (UI 규칙 7)
//    카드 비용 옵션은 줄마다 [마나][-1] 배지로 따로 그린다 (GearOptionText.Describe 주석).
//
//  ⚠ PopupManager 의 _prefabs 배열에 등록해야 열린다 ([Load Popup Prefabs])
// ============================================================

public static class GearDetailPopupCreator
{
    const string SavePath = "Assets/_project/2.Prefabs/UI/GearDetailPopup.prefab";
    const string Tag      = "GearDetailPopupCreator";

    // ⚠ 화면을 넉넉히 쓴다 (사용자 지시, 2026-09-11 "텍스트가 너무 작다")
    //   1320×900 일 때 레벨 줄의 능력치 글 칸이 약 290px 뿐이라, "공격력 +2.8 · 방어 관통 +3%"
    //   가 자동 축소로 23px 까지 줄었다 (UI 규칙 4 — FontSm(34) 미만은 안 읽힌다).
    //   지금은 글 칸이 약 650px 이라 FontMd 그대로 들어간다. 높이는 PopupMaxH 와 같다.
    const float PanelW = 1800f;
    const float PanelH = 1000f;
    const float Pad    = 30f;

    const float IconBox  = 140f;
    const float CloseSz  = 84f;

    const float LeftW  = 560f;
    const float ColGap = 40f;
    static float RightW => PanelW - Pad * 2f - LeftW - ColGap;

    const int   Rows    = MonsterGearLevelRule.MaxLevel;

    const float ChipSize   = 52f;
    // 패시브 칩 — [그림 48][이름]. "쿨타임 감소 II" 가 가장 길다 (FontSm 로 8자 ≈ 280px).
    const float PassiveW    = 340f;
    const float PassiveIcon = 48f;
    const float ManaIcon    = 40f;
    const float MatIcon    = 52f;
    /// <summary>재료 칸 폭 — [그림 52][8][×N 숫자 칸 90].</summary>
    static float MatsW => MatIcon + 8f + 90f;
    const float CostIcon   = 46f;
    const float AdvanceW   = 760f;
    const float EquipW     = 320f;

    /// <summary>레벨 줄 하나의 높이 — FontMd 한 줄(RowMd)보다 넉넉한 RowLg.</summary>
    static float RowH => UIScale.RowLg;

    // ── 세로 예산 ────────────────────────────────────────────
    //  위  : 여백 + 아이콘 칸 + 사이 24 = 몸통 시작
    //  아래: 여백 + 버튼 + 12 + 안내 한 줄 + 16 = 몸통 끝
    //  ⚠ Verify 가 두 칸이 몸통에 들어가는지 검사한다.

    static float BtnH      => UIScale.BtnFor(UIScale.FontMd) + 14f;
    static float BodyTop   => Pad + IconBox + 24f;
    static float FooterH   => Pad + BtnH + 12f + UIScale.RowSm + 16f;
    static float BodyH     => PanelH - BodyTop - FooterH;

    static float RowStep   => RowH + 12f;
    static float TitleStep => UIScale.RowMd + 8f;

    // 왼쪽 칸
    // 능력치 줄 — FontSm 한 줄(RowSm) × StatRowCount. 몬스터 상세의 스탯 줄과 같은 짜임이다.
    static float StatRowH  => UIScale.RowSm;
    const  float StatRowGap = 3f;
    static float StatsH => GearDetailPopup.StatRowCount * StatRowH + (GearDetailPopup.StatRowCount - 1) * StatRowGap;
    static float ManaRowH => Mathf.Max(ManaIcon, UIScale.Line(UIScale.FontSm));
    // ⚠ 설명도 FontSm 이다 — 한때 ×0.88 이었는데 규칙 4 아래라 안 읽혔다
    static float DescH    => UIScale.Line(UIScale.FontSm) * 2f;

    static float LeftNeed  => TitleStep + StatsH + 6f + ManaRowH + 12f + DescH;
    static float RightNeed => TitleStep + RowStep * (Rows + 1);

    static readonly Color ScrimBg   = new Color(0.03f, 0.035f, 0.06f, 0.80f);
    static readonly Color PanelBg   = new Color(0.070f, 0.075f, 0.130f, 1f);
    static readonly Color PanelEdge = new Color(0.24f, 0.30f, 0.52f, 1f);
    static readonly Color RowBg     = new Color(0.105f, 0.115f, 0.180f, 1f);
    static readonly Color SubText   = new Color(0.72f, 0.76f, 0.90f, 1f);
    static readonly Color TitleC    = new Color(0.68f, 0.90f, 0.42f, 1f);
    static readonly Color GoldC     = new Color(1.00f, 0.83f, 0.30f, 1f);

    [MenuItem(ProjectKMenu.Popup + "장비 상세", priority = ProjectKMenu.PrefabPrio + 56)]
    public static void Run()
    {
        if (!UIIconAssets.TryLoad(Tag, out Sprite manaIcon, out _)) return;
        if (!UIIconAssets.TryLoadGold(Tag, out Sprite goldIcon))     return;
        if (!SpeciesPassiveIconAssets.TryLoad(Tag, out Sprite[] passiveIcons)) return;

        if (!Verify()) return;

        var root = new GameObject("GearDetailPopup", typeof(RectTransform));
        root.AddComponent<CanvasGroup>();
        var popup = root.AddComponent<GearDetailPopup>();

        EditorUIBuilder.Stretch(root);

        var scrim = EditorUIBuilder.Img(root, "Scrim", ScrimBg);
        EditorUIBuilder.Stretch(scrim.gameObject);

        GameObject panel = BuildPanel(root);

        BuildHeader(panel, goldIcon, out var header);
        BuildLeft(panel, manaIcon, out var left);
        BuildTrack(panel, manaIcon, out var track);
        BuildFooter(panel, goldIcon, out var foot);

        var so = new SerializedObject(popup);
        EditorUIBuilder.SetEnum(so, "_popupType", (int)PopupType.GearDetail, Tag);

        EditorUIBuilder.SetObj(so, "_iconFrame", header.Frame,  Tag);
        EditorUIBuilder.SetObj(so, "_icon",      header.Icon,   Tag);
        EditorUIBuilder.SetObj(so, "_nameText",  header.Name,   Tag);
        EditorUIBuilder.SetObj(so, "_subText",   header.Sub,    Tag);
        EditorUIBuilder.SetObj(so, "_goldText",  header.Gold,   Tag);
        EditorUIBuilder.SetObj(so, "_closeBtn",  header.Close,  Tag);
        EditorUIBuilder.SetObj(so, "_wearerFrame",    header.WearerFrame,    Tag);
        EditorUIBuilder.SetObj(so, "_wearerPortrait", header.WearerPortrait, Tag);
        EditorUIBuilder.SetObj(so, "_wearerName",     header.WearerName,     Tag);

        EditorUIBuilder.SetObjArray(so, "_statRows",   left.StatRows,   Tag);
        EditorUIBuilder.SetObjArray(so, "_statLabels", left.StatLabels, Tag);
        EditorUIBuilder.SetObjArray(so, "_statValues", left.StatValues, Tag);
        EditorUIBuilder.SetObj(so, "_statManaRoot", left.ManaRoot, Tag);
        EditorUIBuilder.SetObj(so, "_statManaText", left.ManaText, Tag);
        EditorUIBuilder.SetObj(so, "_descText",     left.Desc,     Tag);

        EditorUIBuilder.SetObjArray(so, "_rowBgs",       track.Bgs,       Tag);
        EditorUIBuilder.SetObjArray(so, "_rowAccents",   track.Accents,   Tag);
        EditorUIBuilder.SetObjArray(so, "_rowChips",     track.Chips,     Tag);
        EditorUIBuilder.SetObjArray(so, "_rowNums",      track.Nums,      Tag);
        EditorUIBuilder.SetObjArray(so, "_rowTexts",     track.Texts,     Tag);
        EditorUIBuilder.SetObjArray(so, "_rowManaRoots", track.ManaRoots, Tag);
        EditorUIBuilder.SetObjArray(so, "_rowManaTexts", track.ManaTexts, Tag);
        EditorUIBuilder.SetObjArray(so, "_rowPassives",  track.Passives,  Tag);
        EditorUIBuilder.SetObjArray(so, "_rowPassiveChips", track.PassiveChips, Tag);
        EditorUIBuilder.SetObjArray(so, "_rowPassiveIcons", track.PassiveIcons, Tag);
        EditorUIBuilder.SetObjArray(so, "_speciesIcons",    passiveIcons,       Tag);
        EditorUIBuilder.SetObj(so, "_enhanceBg",   track.EnhanceBg,   Tag);
        EditorUIBuilder.SetObj(so, "_enhanceText", track.EnhanceText, Tag);

        EditorUIBuilder.SetObj(so, "_hintText",     foot.Hint,     Tag);
        EditorUIBuilder.SetObj(so, "_advanceBtn",   foot.Advance,  Tag);
        EditorUIBuilder.SetObj(so, "_advanceLabel", foot.Label,    Tag);
        EditorUIBuilder.SetObj(so, "_matRoot",      foot.MatRoot,  Tag);
        EditorUIBuilder.SetObjArray(so, "_matIcons", foot.Mats,    Tag);
        EditorUIBuilder.SetObj(so, "_matCount",     foot.MatCount, Tag);
        EditorUIBuilder.SetObj(so, "_costRoot",     foot.CostRoot, Tag);
        EditorUIBuilder.SetObj(so, "_costText",     foot.CostText, Tag);
        EditorUIBuilder.SetObj(so, "_equipBtn",     foot.Equip,    Tag);
        EditorUIBuilder.SetObj(so, "_equipLabel",   foot.EquipLabel, Tag);

        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[{Tag}] 생성 완료 → {SavePath}\nPopupManager 의 _prefabs 배열에 등록해야 열린다.");
    }

    /// <summary>
    /// 두 칸이 몸통에 들어가는지, 버튼 안이 버튼보다 넓지 않은지 본다.
    /// ⚠ 넘쳐도 유니티는 말이 없다 — TMP 는 칸을 넘긴 글도 그냥 그린다.
    /// </summary>
    static bool Verify()
    {
        bool ok = true;

        if (PanelH > UIScale.PopupMaxH)
        {
            Debug.LogError($"[{Tag}] 창 높이 {PanelH} 가 상한 {UIScale.PopupMaxH} 를 넘습니다 (UI 규칙 6).");
            ok = false;
        }

        if (LeftNeed > BodyH)
        {
            Debug.LogError($"[{Tag}] 왼쪽 칸({LeftNeed:0})이 몸통({BodyH:0})보다 큽니다.");
            ok = false;
        }

        if (RightNeed > BodyH)
        {
            Debug.LogError($"[{Tag}] 레벨 트랙({RightNeed:0})이 몸통({BodyH:0})보다 큽니다.");
            ok = false;
        }

        if (PanelW > UIScale.RefWidth - 60f)
        {
            Debug.LogError($"[{Tag}] 창 폭 {PanelW} 가 화면 폭({UIScale.RefWidth})에 비해 너무 넓습니다.");
            ok = false;
        }

        // 레벨 줄의 능력치 글 칸 — "공격력 +2.8 · 방어 관통 +3%" (17자) 가 FontMd 그대로 들어가야 한다
        float rowTextW = RightW - (10f + ChipSize + 14f)
                       - (12f + PassiveW + 8f + EditorUIBuilder.BadgeWidthFor(ManaIcon, UIScale.FontSm) + 8f);
        if (rowTextW < 17 * UIScale.FontMd * 0.8f)
        {
            Debug.LogError($"[{Tag}] 레벨 줄 글 칸이 {rowTextW:0}px 뿐입니다 — 글이 자동 축소로 작아집니다.");
            ok = false;
        }

        // 버튼 안 — 여백 22 + 라벨 200 + 재료 + 20 + 금화 + 8 + 값 150 + 여백 22
        float inner = 22f + 200f + MatsW + 20f + CostIcon + 8f + 150f + 22f;
        if (inner > AdvanceW)
        {
            Debug.LogError($"[{Tag}] 레벨업 버튼 안({inner:0})이 버튼 폭({AdvanceW:0})보다 넓습니다.");
            ok = false;
        }

        if (EquipW + 24f + AdvanceW > PanelW - Pad * 2f)
        {
            Debug.LogError($"[{Tag}] 아래 두 버튼이 창 폭을 넘습니다.");
            ok = false;
        }

        // 착용 초상화 칸 — 부제(끝 934)와 지갑(시작) 사이에 들어가야 한다
        float subEnd      = Pad + IconBox + 24f + 740f;
        float walletStart = PanelW - (Pad + CloseSz + 24f + 260f);
        if (WearerX < subEnd || WearerX + IconBox + 16f + WearerTextW > walletStart)
        {
            Debug.LogError($"[{Tag}] 착용 초상화 칸이 부제({subEnd:0}) 또는 지갑({walletStart:0})과 겹칩니다.");
            ok = false;
        }


        return ok;
    }

    // ── 자리 도우미 ──────────────────────────────────────────

    /// <summary>창의 왼쪽 위를 원점으로 (x, 위에서 y) 에 w×h 를 놓는다.</summary>
    static void Place(RectTransform rt, float x, float yTop, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -yTop);
        rt.sizeDelta        = new Vector2(w, h);
    }

    static TextMeshProUGUI Text(GameObject parent, string name, string text, float size,
                                FontStyles style, Color color, TextAlignmentOptions align)
    {
        var t = EditorUIBuilder.TMP(parent, name, text, size, style);
        t.color         = color;
        t.alignment     = align;
        t.raycastTarget = false;
        return t;
    }

    static void SectionTitle(GameObject panel, string text, float x, float y, float w)
    {
        var t = Text(panel, "SectionTitle", text, UIScale.FontMd, FontStyles.Bold,
                     TitleC, TextAlignmentOptions.MidlineLeft);
        Place(t.rectTransform, x, y, w, UIScale.RowMd);
    }

    // ── 창 ───────────────────────────────────────────────────

    static GameObject BuildPanel(GameObject root)
    {
        // ⚠ 테두리는 앞 형제로 만들어 뒤에 깐다 (UI 규칙 3)
        var edge = EditorUIBuilder.Img(root, "PanelEdge", PanelEdge);
        EditorUIBuilder.Center(edge.rectTransform, Vector2.zero, new Vector2(PanelW + 6f, PanelH + 6f));
        edge.raycastTarget = false;

        var panel = EditorUIBuilder.Img(root, "Panel", PanelBg).gameObject;
        EditorUIBuilder.Center(panel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(PanelW, PanelH));
        return panel;
    }

    // ── 머리 ─────────────────────────────────────────────────

    struct HeaderRefs
    {
        public Image           Frame, Icon, WearerFrame, WearerPortrait;
        public TextMeshProUGUI Name, Sub, Gold, WearerName;
        public Button          Close;
    }

    // ── 착용 몬스터 (머리 가운데) ──
    //  ⚠ 폭 검산: 이름·부제가 textX(194) + 740 = 934 에서 끝나고, 지갑이 1800 − (30+84+24+260) = 1402 에서 시작한다.
    //    960 + 140 + 16 + WearerTextW(270) = 1386 ≤ 1402 ✔ — Verify 가 다시 잰다.
    const float WearerX     = 960f;
    const float WearerTextW = 270f;

    static void BuildHeader(GameObject panel, Sprite goldIcon, out HeaderRefs refs)
    {
        // 아이콘 칸 — 테두리가 등급색이다. 레벨업 연출(UIJuice)의 자리이기도 하다.
        var frame = EditorUIBuilder.Img(panel, "IconFrame", Color.white);
        frame.raycastTarget = false;
        Place(frame.rectTransform, Pad, Pad, IconBox, IconBox);

        var fill = EditorUIBuilder.Img(frame.gameObject, "Fill", RowBg);
        EditorUIBuilder.Stretch(fill.gameObject);
        fill.rectTransform.offsetMin = new Vector2(4f, 4f);
        fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
        fill.raycastTarget = false;

        var icon = EditorUIBuilder.Img(fill.gameObject, "Icon", Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        EditorUIBuilder.Stretch(icon.gameObject);
        icon.rectTransform.offsetMin = new Vector2(14f, 14f);
        icon.rectTransform.offsetMax = new Vector2(-14f, -14f);

        float textX = Pad + IconBox + 24f;

        var name = Text(panel, "Name", "장비 이름", UIScale.FontLg, FontStyles.Bold,
                        Color.white, TextAlignmentOptions.MidlineLeft);
        // 레벨은 이름 뒤에 작게 이어 붙인다 (런타임 리치 텍스트) — 따로 두면 이름이 짧을 때 사이가 휑하다
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.enableAutoSizing = true;
        name.fontSizeMax      = UIScale.FontLg;
        name.fontSizeMin      = UIScale.FontMd;
        Place(name.rectTransform, textX, Pad + 6f, 740f, UIScale.RowLg);

        var sub = Text(panel, "Sub", "", UIScale.FontSm, FontStyles.Normal,
                       SubText, TextAlignmentOptions.MidlineLeft);
        Place(sub.rectTransform, textX, Pad + 6f + UIScale.RowLg + 4f, 740f, UIScale.RowSm);

        // ── 지갑 — 늘 같은 자리. 낼 값(버튼 안)과 떨어뜨려 둔다 ──
        var wallet = EditorUIBuilder.Go("Wallet", panel);
        {
            var rt = wallet.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-(Pad + CloseSz + 24f), -(Pad + 14f));
            rt.sizeDelta        = new Vector2(260f, 56f);
        }

        var wIcon = EditorUIBuilder.Img(wallet, "GoldIcon", Color.white);
        wIcon.sprite = goldIcon; wIcon.preserveAspect = true; wIcon.raycastTarget = false;
        {
            var rt = wIcon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(48f, 48f);
        }

        var gold = Text(wallet, "GoldValue", "0", UIScale.FontMd, FontStyles.Bold,
                        GoldC, TextAlignmentOptions.MidlineLeft);
        {
            var rt = gold.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(58f, 0f); rt.offsetMax = Vector2.zero;
        }

        // ── 닫기 ──
        var close = EditorUIBuilder.RaisedBtn(panel, "CloseBtn", new Color(0.62f, 0.20f, 0.24f), out var body);
        {
            var rt = close.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-Pad, -Pad);
            rt.sizeDelta        = new Vector2(CloseSz, CloseSz);
        }
        EditorUIBuilder.XMark(body, "X", 34f, new Color(0.92f, 0.94f, 0.98f));

        // ── 착용 몬스터 — [초상화] 착용 / 이름 ──
        //   ⚠ 장착을 누르면 창을 닫지 않고 여기가 그 자리에서 바뀐다 (GearDetailPopup.RefreshWearer)
        var wFrame = EditorUIBuilder.Img(panel, "WearerFrame", Color.white);
        wFrame.raycastTarget = false;
        Place(wFrame.rectTransform, WearerX, Pad, IconBox, IconBox);

        var wFill = EditorUIBuilder.Img(wFrame.gameObject, "Fill", RowBg);
        EditorUIBuilder.Stretch(wFill.gameObject);
        wFill.rectTransform.offsetMin = new Vector2(4f, 4f);
        wFill.rectTransform.offsetMax = new Vector2(-4f, -4f);
        wFill.raycastTarget = false;

        var wPortrait = EditorUIBuilder.Img(wFill.gameObject, "Portrait", Color.white);
        wPortrait.preserveAspect = true;
        wPortrait.raycastTarget  = false;
        EditorUIBuilder.Stretch(wPortrait.gameObject);
        wPortrait.rectTransform.offsetMin = new Vector2(8f, 8f);
        wPortrait.rectTransform.offsetMax = new Vector2(-8f, -8f);

        float wTextX = WearerX + IconBox + 16f;

        var wCaption = Text(panel, "WearerCaption", "착용", UIScale.FontSm, FontStyles.Bold,
                            SubText, TextAlignmentOptions.MidlineLeft);
        Place(wCaption.rectTransform, wTextX, Pad + 20f, WearerTextW, UIScale.RowSm);

        var wName = Text(panel, "WearerName", "없음", UIScale.FontMd, FontStyles.Bold,
                         Color.white, TextAlignmentOptions.MidlineLeft);
        wName.textWrappingMode = TextWrappingModes.NoWrap;
        wName.enableAutoSizing = true;
        wName.fontSizeMax      = UIScale.FontMd;
        wName.fontSizeMin      = UIScale.FontSm;
        Place(wName.rectTransform, wTextX, Pad + 20f + UIScale.RowSm + 4f, WearerTextW, UIScale.RowMd);

        refs = new HeaderRefs
        {
            Frame = frame, Icon = icon, Name = name, Sub = sub, Gold = gold, Close = close,
            WearerFrame = wFrame, WearerPortrait = wPortrait, WearerName = wName,
        };
    }

    // ── 왼쪽 — 지금 능력치 ───────────────────────────────────

    struct LeftRefs
    {
        public TextMeshProUGUI ManaText, Desc;
        public GameObject      ManaRoot;
        public Object[]        StatRows, StatLabels, StatValues;
    }

    static void BuildLeft(GameObject panel, Sprite manaIcon, out LeftRefs refs)
    {
        float x = Pad;
        float y = BodyTop;

        SectionTitle(panel, "능 력 치", x, y, LeftW);
        y += TitleStep;

        // ── [이름 ········ 값] 줄 — 몬스터 상세의 스탯 줄과 같은 짜임 (사용자 지적, 2026-09-12) ──
        //   한 칸에 글을 쏟아 넣었을 때 줄이 늘면 자동 축소로 작아졌고, 이름과 값이 붙어 안 읽혔다.
        //   이름은 흐린 색 왼쪽, 값은 흰 굵은 글 오른쪽 — 눈이 값의 세로줄만 따라 내려가면 된다.
        int n = GearDetailPopup.StatRowCount;
        var statRows = new Object[n]; var statLabels = new Object[n]; var statValues = new Object[n];

        for (int i = 0; i < n; i++)
        {
            var row = EditorUIBuilder.Img(panel, $"StatRow{i}", RowBg);
            row.raycastTarget = false;
            Place(row.rectTransform, x, y + i * (StatRowH + StatRowGap), LeftW, StatRowH);

            var lbl = Text(row.gameObject, "Label", "", UIScale.FontSm, FontStyles.Normal,
                           SubText, TextAlignmentOptions.MidlineLeft);
            lbl.textWrappingMode = TextWrappingModes.NoWrap;
            EditorUIBuilder.Stretch(lbl.gameObject);
            lbl.rectTransform.offsetMin = new Vector2(18f, 0f);
            lbl.rectTransform.offsetMax = new Vector2(-18f, 0f);

            var val = Text(row.gameObject, "Value", "", UIScale.FontSm, FontStyles.Bold,
                           Color.white, TextAlignmentOptions.MidlineRight);
            val.textWrappingMode = TextWrappingModes.NoWrap;
            EditorUIBuilder.Stretch(val.gameObject);
            val.rectTransform.offsetMin = new Vector2(18f, 0f);
            val.rectTransform.offsetMax = new Vector2(-18f, 0f);

            row.gameObject.SetActive(false);
            statRows[i] = row.gameObject; statLabels[i] = lbl; statValues[i] = val;
        }

        y += StatsH + 6f;

        // [마나][-1] — 카드 비용을 깎는 장비만. ⚠ 글자로 적지 않는다 (UI 규칙 7)
        var manaHolder = EditorUIBuilder.Go("ManaRow", panel);
        Place(manaHolder.GetComponent<RectTransform>(), x + 14f, y, LeftW - 14f, ManaRowH);

        var manaRoot = EditorUIBuilder.IconValueBadge(manaHolder, "ManaCut", manaIcon, rightSide: false,
                                                      ManaIcon, UIScale.FontSm, 0f,
                                                      new Color(0.45f, 0.92f, 0.55f), out var manaText);
        manaRoot.SetActive(false);

        y += ManaRowH + 12f;

        var desc = Text(panel, "Desc", "", UIScale.FontSm, FontStyles.Normal,
                        SubText, TextAlignmentOptions.TopLeft);
        desc.enableAutoSizing = true;
        desc.fontSizeMax      = UIScale.FontSm;
        desc.fontSizeMin      = UIScale.FontSm * 0.85f;
        Place(desc.rectTransform, x, y, LeftW, DescH);

        // ⚠ "재료 N" 줄은 없앴다 — 레벨업 버튼의 [그림][×N] 이 말한다 (2026-09-12)
        refs = new LeftRefs
        {
            ManaRoot = manaRoot, ManaText = manaText, Desc = desc,
            StatRows = statRows, StatLabels = statLabels, StatValues = statValues,
        };
    }

    // ── 오른쪽 — 레벨 트랙 ───────────────────────────────────

    struct TrackRefs
    {
        public Object[]        Bgs, Chips, Nums, Texts, ManaRoots, ManaTexts, Passives;
        public Object[]        PassiveChips, PassiveIcons, Accents;
        public Image           EnhanceBg;
        public TextMeshProUGUI EnhanceText;
    }

    static void BuildTrack(GameObject panel, Sprite manaIcon, out TrackRefs refs)
    {
        float x = Pad + LeftW + ColGap;
        float y = BodyTop;
        float w = RightW;

        SectionTitle(panel, "레 벨", x, y, w);
        y += TitleStep;

        var bgs = new Object[Rows]; var chips = new Object[Rows]; var nums = new Object[Rows];
        var texts = new Object[Rows]; var manaRoots = new Object[Rows]; var manaTexts = new Object[Rows];
        var passives = new Object[Rows];
        var passiveChips = new Object[Rows]; var passiveIcons = new Object[Rows];
        var accents = new Object[Rows];

        float manaW = EditorUIBuilder.BadgeWidthFor(ManaIcon, UIScale.FontSm);

        for (int i = 0; i < Rows; i++)
        {
            var bg = EditorUIBuilder.Img(panel, $"Row_Lv{i + 1}", RowBg);
            bg.raycastTarget = false;
            Place(bg.rectTransform, x, y, w, RowH);
            GameObject row = bg.gameObject;

            // 왼쪽 띠 — 연 줄 등급색 · 다음 줄 금색 · 아직 없음 (런타임). 바탕 색만으로는 셋이 안 갈렸다.
            var accent = EditorUIBuilder.Img(row, "Accent", Color.clear);
            accent.raycastTarget = false;
            {
                var rt = accent.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = new Vector2(6f, 0f);
            }
            accents[i] = accent;

            // 레벨 동그라미 — 연 레벨은 등급색 (런타임)
            var chip = EditorUIBuilder.Img(row, "Chip", Color.white);
            chip.sprite        = EditorUIBuilder.Circle();
            chip.raycastTarget = false;
            {
                var rt = chip.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(10f, 0f);
                rt.sizeDelta        = new Vector2(ChipSize, ChipSize);
            }

            var num = Text(chip.gameObject, "Num", (i + 1).ToString(), UIScale.FontSm,
                           FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            EditorUIBuilder.Stretch(num.gameObject);

            float textX = 10f + ChipSize + 14f;

            // 오른쪽 끝부터 — 패시브 칩 · 마나 배지 — 남는 폭이 능력치 글이다
            //   칩 = [그림][이름]. 올리거나 누르면 설명이 뜬다 (SpeciesPassiveChipUI).
            //   ⚠ 그림·이름의 raycastTarget 을 켠다 — 꺼 두면 칩이 포인터를 못 받는다.
            var chipGo = EditorUIBuilder.Go("PassiveChip", row);
            {
                var rt = chipGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-12f, 0f);
                rt.sizeDelta        = new Vector2(PassiveW, 0f);
            }
            var chipUi = chipGo.AddComponent<SpeciesPassiveChipUI>();

            var pIcon = EditorUIBuilder.Img(chipGo, "Icon", Color.white);
            pIcon.preserveAspect = true;
            pIcon.raycastTarget  = true;
            {
                var rt = pIcon.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = new Vector2(PassiveIcon, PassiveIcon);
            }

            var passive = Text(chipGo, "Name", "패시브", UIScale.FontSm, FontStyles.Bold,
                               SubText, TextAlignmentOptions.MidlineLeft);
            passive.raycastTarget    = true;
            passive.textWrappingMode = TextWrappingModes.NoWrap;
            passive.enableAutoSizing = true;
            passive.fontSizeMax      = UIScale.FontSm;
            passive.fontSizeMin      = UIScale.FontSm * 0.85f;
            {
                var rt = passive.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(PassiveIcon + 10f, 0f);
                rt.offsetMax = Vector2.zero;
            }
            chipGo.SetActive(false);

            var manaHolder = EditorUIBuilder.Go("ManaHolder", row);
            {
                var rt = manaHolder.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-(12f + PassiveW + 8f), 0f);
                rt.sizeDelta        = new Vector2(manaW, 0f);
            }
            var manaRoot = EditorUIBuilder.IconValueBadge(manaHolder, "ManaCut", manaIcon, rightSide: true,
                                                          ManaIcon, UIScale.FontSm, 0f, Color.white, out var manaText);
            {
                // 배지는 부모 위쪽 모서리에 붙는다 — 줄 한가운데로 내린다
                var rt = manaRoot.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0f, -(RowH - rt.sizeDelta.y) * 0.5f);
            }
            manaRoot.SetActive(false);

            // ⚠ 자동 축소의 바닥은 FontSm 이다 (UI 규칙 4) — 한때 ×0.66(22px)까지 내려가 안 읽혔다
            var text = Text(row, "Text", "", UIScale.FontMd, FontStyles.Bold,
                            Color.white, TextAlignmentOptions.MidlineLeft);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMax      = UIScale.FontMd;
            text.fontSizeMin      = UIScale.FontSm;
            {
                var rt = text.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(textX, 0f);
                rt.offsetMax = new Vector2(-(12f + PassiveW + 8f + manaW + 8f), 0f);
            }

            bgs[i] = bg; chips[i] = chip; nums[i] = num; texts[i] = text;
            manaRoots[i] = manaRoot; manaTexts[i] = manaText; passives[i] = passive;
            passiveChips[i] = chipUi; passiveIcons[i] = pIcon;

            y += RowStep;
        }

        // ── 강화 줄 — Lv5 뒤에 여는 끝없는 칸 ──
        var enhBg = EditorUIBuilder.Img(panel, "Row_Enhance", RowBg);
        enhBg.raycastTarget = false;
        Place(enhBg.rectTransform, x, y, w, RowH);

        var enhText = Text(enhBg.gameObject, "Text", "강화", UIScale.FontMd, FontStyles.Bold,
                           GoldC, TextAlignmentOptions.MidlineLeft);
        enhText.enableAutoSizing = true;
        enhText.fontSizeMax      = UIScale.FontMd;
        enhText.fontSizeMin      = UIScale.FontSm;
        EditorUIBuilder.Stretch(enhText.gameObject);
        enhText.rectTransform.offsetMin = new Vector2(10f + ChipSize + 14f, 0f);
        enhText.rectTransform.offsetMax = new Vector2(-12f, 0f);

        refs = new TrackRefs
        {
            Bgs = bgs, Chips = chips, Nums = nums, Texts = texts,
            ManaRoots = manaRoots, ManaTexts = manaTexts, Passives = passives,
            PassiveChips = passiveChips, PassiveIcons = passiveIcons, Accents = accents,
            EnhanceBg = enhBg, EnhanceText = enhText,
        };
    }

    // ── 아래 — 안내 · 장착 · 레벨업 ──────────────────────────

    struct FootRefs
    {
        public TextMeshProUGUI Hint, Label, CostText, EquipLabel, MatCount;
        public Button          Advance, Equip;
        public GameObject      MatRoot, CostRoot;
        public Object[]        Mats;
    }

    static void BuildFooter(GameObject panel, Sprite goldIcon, out FootRefs refs)
    {
        // 안내 한 줄 — 버튼 바로 위. 왜 못 누르는지 / 다음에 무엇이 열리는지.
        var hint = Text(panel, "Hint", "", UIScale.FontSm, FontStyles.Bold,
                        SubText, TextAlignmentOptions.Center);
        hint.enableAutoSizing = true;
        hint.fontSizeMax      = UIScale.FontSm;
        hint.fontSizeMin      = UIScale.FontSm * 0.85f;
        {
            var rt = hint.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(Pad, Pad + BtnH + 12f);
            rt.offsetMax = new Vector2(-Pad, Pad + BtnH + 12f + UIScale.RowSm);
        }

        // ── 장착 — 몬스터 상세에서 열었을 때만 런타임이 켠다 ──
        var equip = EditorUIBuilder.RaisedTextBtn(panel, "EquipBtn", "장착", UIScale.FontMd,
                                                  new Color(0.22f, 0.44f, 0.36f));
        {
            var rt = equip.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(Pad, Pad);
            rt.sizeDelta        = new Vector2(EquipW, BtnH);
        }
        equip.gameObject.SetActive(false);

        // ── 레벨업 / 강화 — 낼 것(재료·골드)이 버튼 **안**에 있다 ──
        var advance = EditorUIBuilder.RaisedBtn(panel, "AdvanceBtn", new Color(0.34f, 0.26f, 0.52f),
                                                out var body);
        {
            var rt = advance.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-Pad, Pad);
            rt.sizeDelta        = new Vector2(AdvanceW, BtnH);
        }

        var label = Text(body, "Label", "레벨 업", UIScale.FontMd, FontStyles.Bold,
                         Color.white, TextAlignmentOptions.MidlineLeft);
        {
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(22f, 0f);
            rt.sizeDelta        = new Vector2(200f, 0f);
        }

        // 재료 — [장비 그림][×N] (사용자 지시, 2026-09-12). 모자라면 숫자가 붉다 (런타임).
        //   ⚠ 버튼 면(짙은 보라) 위라 숫자는 밝은 색이다 (UI 규칙 8)
        var matRoot = EditorUIBuilder.Go("Mats", body);
        {
            var rt = matRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(22f + 200f, 0f);
            rt.sizeDelta        = new Vector2(MatsW, 0f);
        }

        var m = EditorUIBuilder.Img(matRoot, "Mat0", Color.white);
        m.preserveAspect = true;
        m.raycastTarget  = false;
        {
            var rt = m.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(MatIcon, MatIcon);
        }
        var mats = new Object[] { m };

        var matCount = Text(matRoot, "Count", "×1", UIScale.FontMd, FontStyles.Bold,
                            Color.white, TextAlignmentOptions.MidlineLeft);
        {
            var rt = matCount.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(MatIcon + 8f, 0f); rt.offsetMax = Vector2.zero;
        }

        // [금][값] — 오른쪽 끝
        var costRoot = EditorUIBuilder.Go("CostRoot", body);
        {
            var rt = costRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-22f, 0f);
            rt.sizeDelta        = new Vector2(CostIcon + 8f + 150f, 0f);
        }

        var cIcon = EditorUIBuilder.Img(costRoot, "GoldIcon", Color.white);
        cIcon.sprite = goldIcon; cIcon.preserveAspect = true; cIcon.raycastTarget = false;
        {
            var rt = cIcon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(CostIcon, CostIcon);
        }

        var cost = Text(costRoot, "CostValue", "0", UIScale.FontMd, FontStyles.Bold,
                        GoldC, TextAlignmentOptions.MidlineRight);
        cost.enableAutoSizing = true;
        cost.fontSizeMax      = UIScale.FontMd;
        cost.fontSizeMin      = UIScale.FontSm * 0.8f;
        {
            var rt = cost.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(CostIcon + 8f, 0f); rt.offsetMax = Vector2.zero;
        }

        refs = new FootRefs
        {
            Hint = hint, Label = label, CostText = cost,
            Advance = advance, Equip = equip,
            // RaisedTextBtn 의 라벨 — "장착" / "옮겨 장착" / "벗기기" 를 런타임이 갈아 끼운다
            EquipLabel = equip.transform.Find("Body/Label").GetComponent<TextMeshProUGUI>(),
            MatRoot = matRoot, CostRoot = costRoot, Mats = mats, MatCount = matCount,
        };
    }
}
