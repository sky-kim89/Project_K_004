using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  DifficultyUnlockPopupCreator.cs
//  Tools > Project K > 프리팹 생성 > 팝업 > 난이도 해금
//
//  ┌──────────────────────────────┐   ← 가운데 창 (보상 상자와 같은 크기·결)
//  │         새 난이도 해금          │
//  │            ( 빛 )             │
//  │         ╔════════╗            │   ← 난이도 아이콘 (처음엔 어둡게)
//  │         ║  [자물쇠] ║            │   ← 흔들리다 튀어 오르며 사라진다
//  │         ╚════════╝            │
//  │            어려움               │   ← 난이도 색. 튀어나온다
//  │   적이 더 강해지고 수도 늘어난다.   │
//  │        환생 포인트 ×1.5          │
//  │      [     확인     ]          │   ← 연출이 끝나야 켜진다
//  └──────────────────────────────┘
//
//  ⚠ 자물쇠는 폰트 글리프가 아니라 EditorUIBuilder.PadLock 이다 (UI 규칙 2)
//  ⚠ PopupManager 의 _prefabs 에 등록해야 열린다 ([Load Popup Prefabs])
// ============================================================

public static class DifficultyUnlockPopupCreator
{
    const string SavePath = "Assets/_project/2.Prefabs/UI/DifficultyUnlockPopup.prefab";
    const string Tag      = "DifficultyUnlockPopupCreator";

    const float PanelW = 720f;
    const float PanelH = 720f;
    const float Pad    = 30f;

    const float FrameSize = 220f;
    const float GlowSize  = 400f;
    const float LockSize  = 110f;

    // ── 세로 예산 ── 위에서부터 쌓는다. ⚠ Verify 가 확인 버튼과 겹치는지 본다.
    static float CloseH    => UIScale.BtnFor(UIScale.FontMd);
    static float FrameTop  => Pad + UIScale.RowLg + 24f;
    static float NameTop   => FrameTop + FrameSize + 28f;
    static float NameH     => UIScale.Line(UIScale.FontXl);
    static float SummaryTop => NameTop + NameH + 6f;
    static float RewardTop  => SummaryTop + UIScale.RowSm + 8f;
    static float ContentEnd => RewardTop + UIScale.RowMd;

    static readonly Color ScrimBg   = new Color(0.03f, 0.035f, 0.06f, 0.86f);
    static readonly Color PanelBg   = new Color(0.070f, 0.075f, 0.130f, 1f);
    static readonly Color PanelEdge = new Color(0.24f, 0.30f, 0.52f, 1f);
    static readonly Color SlotBg    = new Color(0.105f, 0.115f, 0.190f, 1f);
    static readonly Color SubText   = new Color(0.72f, 0.76f, 0.90f, 1f);
    static readonly Color LockColor = new Color(0.92f, 0.86f, 0.62f, 1f);

    [MenuItem(ProjectKMenu.Popup + "난이도 해금", priority = ProjectKMenu.PrefabPrio + 57)]
    public static void Run()
    {
        Verify();

        var root = new GameObject("DifficultyUnlockPopup", typeof(RectTransform));
        root.AddComponent<CanvasGroup>();
        var popup = root.AddComponent<DifficultyUnlockPopup>();
        EditorUIBuilder.Stretch(root);

        var scrim = EditorUIBuilder.Img(root, "Scrim", ScrimBg);
        EditorUIBuilder.Stretch(scrim.gameObject);

        // ⚠ 테두리는 앞 형제로 뒤에 깐다 (UI 규칙 3)
        var edge = EditorUIBuilder.Img(root, "PanelEdge", PanelEdge);
        EditorUIBuilder.Center(edge.rectTransform, Vector2.zero, new Vector2(PanelW + 6f, PanelH + 6f));
        edge.raycastTarget = false;

        var panel = EditorUIBuilder.Img(root, "Panel", PanelBg).gameObject;
        EditorUIBuilder.Center(panel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(PanelW, PanelH));

        var title = EditorUIBuilder.TMP(panel, "Title", "새 난이도 해금", UIScale.FontLg, FontStyles.Bold);
        title.alignment     = TextAlignmentOptions.Center;
        title.raycastTarget = false;
        EditorUIBuilder.AnchorTop(title.rectTransform, Pad, UIScale.RowLg, Pad * 2f);

        float frameCenterY = -(FrameTop + FrameSize * 0.5f);

        // 빛무리 — 아이콘 뒤. 먼저 만들어야 뒤에 깔린다.
        var glow = EditorUIBuilder.Img(panel, "Glow", new Color(1f, 1f, 1f, 0f));
        glow.sprite         = EditorUIBuilder.Circle();
        glow.preserveAspect = true;
        glow.raycastTarget  = false;
        TopCenter(glow.rectTransform, frameCenterY, GlowSize, GlowSize);

        // 아이콘 틀 — 튀어 오를 때 localScale 을 쓰므로 가운데 피벗이다
        var frame = EditorUIBuilder.Img(panel, "IconFrame", Color.white);
        frame.raycastTarget = false;
        TopCenter(frame.rectTransform, frameCenterY, FrameSize, FrameSize);

        var fill = EditorUIBuilder.Img(frame.gameObject, "Fill", SlotBg);
        EditorUIBuilder.Stretch(fill.gameObject);
        fill.rectTransform.offsetMin = new Vector2(6f, 6f);
        fill.rectTransform.offsetMax = new Vector2(-6f, -6f);
        fill.raycastTarget = false;

        var icon = EditorUIBuilder.Img(fill.gameObject, "Icon", Color.white);
        EditorUIBuilder.Stretch(icon.gameObject);
        icon.rectTransform.offsetMin = new Vector2(18f, 18f);
        icon.rectTransform.offsetMax = new Vector2(-18f, -18f);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;

        // 자물쇠 — 틀의 자식이 아니다. 틀이 튀어 오를 때 같이 커지지 않게.
        var lockGo = EditorUIBuilder.PadLock(panel, "Lock", LockSize, LockColor);
        TopCenter(lockGo.GetComponent<RectTransform>(), frameCenterY, LockSize, LockSize);
        var lockGraphics = lockGo.GetComponentsInChildren<Graphic>(true);
        foreach (var g in lockGraphics) g.raycastTarget = false;

        var tierName = EditorUIBuilder.TMP(panel, "TierName", "어려움", UIScale.FontXl, FontStyles.Bold);
        tierName.alignment     = TextAlignmentOptions.Center;
        tierName.raycastTarget = false;
        EditorUIBuilder.AnchorTop(tierName.rectTransform, NameTop, NameH, Pad * 2f);
        tierName.rectTransform.pivot = new Vector2(0.5f, 0.5f);   // 튀어나올 때 가운데서 자라게
        tierName.rectTransform.anchoredPosition = new Vector2(0f, -(NameTop + NameH * 0.5f));

        var summary = EditorUIBuilder.TMP(panel, "Summary", "", UIScale.FontSm, FontStyles.Normal);
        summary.color         = SubText;
        summary.alignment     = TextAlignmentOptions.Center;
        summary.raycastTarget = false;
        EditorUIBuilder.AnchorTop(summary.rectTransform, SummaryTop, UIScale.RowSm, Pad);

        var reward = EditorUIBuilder.TMP(panel, "Reward", "", UIScale.FontMd, FontStyles.Bold);
        reward.alignment     = TextAlignmentOptions.Center;
        reward.raycastTarget = false;
        EditorUIBuilder.AnchorTop(reward.rectTransform, RewardTop, UIScale.RowMd, Pad);

        // ⚠ 누를 수 있는 것이므로 음각이다 (UI 규칙 1)
        var closeBtn = EditorUIBuilder.RaisedTextBtn(panel, "CloseBtn", "확인",
                                                     UIScale.FontMd, new Color(0.24f, 0.40f, 0.74f));
        {
            var rt = closeBtn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, Pad);
            rt.sizeDelta        = new Vector2(320f, CloseH);
        }
        closeBtn.gameObject.SetActive(false);

        var so = new SerializedObject(popup);
        EditorUIBuilder.SetEnum(so, "_popupType", (int)PopupType.DifficultyUnlock, Tag);
        EditorUIBuilder.SetObj(so, "_titleText",      title,                   Tag);
        EditorUIBuilder.SetObj(so, "_glow",           glow,                    Tag);
        EditorUIBuilder.SetObj(so, "_iconFrame",      frame.rectTransform,     Tag);
        EditorUIBuilder.SetObj(so, "_iconFrameImage", frame,                   Tag);
        EditorUIBuilder.SetObj(so, "_icon",           icon,                    Tag);
        EditorUIBuilder.SetObj(so, "_lock",           lockGo.GetComponent<RectTransform>(), Tag);
        EditorUIBuilder.SetObjArray(so, "_lockGraphics", lockGraphics.Cast<Object>().ToArray(), Tag);
        EditorUIBuilder.SetObj(so, "_tierName",       tierName,                Tag);
        EditorUIBuilder.SetObj(so, "_summary",        summary,                 Tag);
        EditorUIBuilder.SetObj(so, "_reward",         reward,                  Tag);
        EditorUIBuilder.SetObj(so, "_closeBtn",       closeBtn,                Tag);
        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[DifficultyUnlockPopupCreator] 생성 완료 → " + SavePath +
                  "\nPopupManager 의 [Load Popup Prefabs] 를 눌러야 열린다.");
    }

    /// <summary>창 위에서 잰 중심 y 에 가운데 피벗으로 놓는다.</summary>
    static void TopCenter(RectTransform rt, float centerY, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, centerY);
        rt.sizeDelta        = new Vector2(w, h);
    }

    /// <summary>⚠ 넘쳐도 유니티는 말이 없다 — 내용이 확인 버튼을 덮는지 굽는 순간 잰다.</summary>
    static void Verify()
    {
        float closeTop = PanelH - Pad - CloseH - 16f;
        if (ContentEnd > closeTop)
            Debug.LogError($"[{Tag}] 내용({ContentEnd:0})이 확인 버튼 자리({closeTop:0})를 넘습니다. PanelH 를 키우세요.");

        if (PanelH > UIScale.PopupMaxH)
            Debug.LogError($"[{Tag}] 창 높이 {PanelH} 가 상한 {UIScale.PopupMaxH} 를 넘습니다 (UI 규칙 6).");
    }
}
