#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  InfoTooltipBuilder.cs  [Editor Only]
//  "이름 / 설명 / 스탯" 공용 툴팁(InfoTooltipUI) 패널을 만드는 팩토리.
//
//  쓰는 곳: TraitIconSlotBuilder · MainPanelCreator · UISetupTool(RewardCard)
//  → 특성 아이콘과 보상 카드의 툴팁이 같은 모양·같은 동작을 갖는다.
//
//  구조:
//    Tooltip (Image, 기본 비활성, InfoTooltipUI)
//    ├─ TooltipName  이름 (Bold)
//    ├─ TooltipDesc  설명
//    └─ TooltipStat  스탯 (없으면 런타임에 숨김)
//
//  부모의 좌하단에 붙어 아래로 펼쳐진다. 높이는 ContentSizeFitter 가 잡는다.
// ============================================================

public static class InfoTooltipBuilder
{
    static readonly Color PanelBg  = new(0.05f, 0.06f, 0.12f, 0.96f);
    static readonly Color DescGray = new(0.55f, 0.57f, 0.72f);
    static readonly Color StatMint = new(0.55f, 0.90f, 0.65f);

    public static InfoTooltipUI Build(GameObject parent, float width)
    {
        var go = new GameObject("Tooltip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        go.GetComponent<Image>().color = PanelBg;
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = new Vector2(0f, 0f);   // 부모 좌하단
            rt.pivot            = new Vector2(0f, 1f);                  // 아래로 펼침
            rt.anchoredPosition = new Vector2(0f, -4f);
            rt.sizeDelta        = new Vector2(width, 0f);               // 높이는 CSF 가 결정
        }

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset(12, 12, 12, 12);
        vlg.spacing                = 6f;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        var nameTmp = Line(go, "TooltipName", FontStyles.Bold,   Color.white);
        var descTmp = Line(go, "TooltipDesc", FontStyles.Normal, DescGray);
        var statTmp = Line(go, "TooltipStat", FontStyles.Normal, StatMint);
        statTmp.gameObject.SetActive(false);

        var ui = go.AddComponent<InfoTooltipUI>();
        var so = new SerializedObject(ui);
        so.Update();
        EditorUIBuilder.SetObj(so, "_nameText", nameTmp, "InfoTooltipBuilder");
        EditorUIBuilder.SetObj(so, "_descText", descTmp, "InfoTooltipBuilder");
        EditorUIBuilder.SetObj(so, "_statText", statTmp, "InfoTooltipBuilder");
        so.ApplyModifiedProperties();

        go.SetActive(false);
        return ui;
    }

    static TextMeshProUGUI Line(GameObject parent, string name, FontStyles style, Color color)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent.transform, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text             = "";
        tmp.fontSize         = UIScale.FontSm;
        tmp.fontStyle        = style;
        tmp.color            = color;
        tmp.alignment        = TextAlignmentOptions.Left;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }
}
#endif
