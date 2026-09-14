using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  FacilityPopupCreator.cs
//  Tools > Project K > 프리팹 생성 > 팝업 > 시설
//
//  ┌────────────────────────────────────────────────┐
//  │              (배경 그림 — 위쪽 절반)             │
//  │  ▓▓▓▓▓▓▓▓▓▓ 아래로 갈수록 어두워진다 ▓▓▓▓▓▓▓▓▓▓  │
//  │  야영지                                   [ × ] │
//  │  "무너진 돌을 다시 쌓는다…"                      │
//  ├────────────────────────────────────────────────┤
//  │  수리   무료          마왕성 체력 +5            │
//  │  증축   300 G         최대 체력이 늘어난다      │
//  └────────────────────────────────────────────────┘
//
//  ■ 야영지·강화소·제단·상점이 이 하나를 나눠 쓴다
//    다른 것은 배경 그림과 글 두 줄뿐이다. 넷으로 나누면 한 줄을 고칠 때마다
//    네 번 구워야 한다 (FacilityPopup 머리 주석 참고).
//
//  ⚠ 그림 위에 글을 바로 얹지 않는다 (UI 규칙 8)
//    배경은 종류마다 밝기가 다르다. 글이 놓이는 자리에 **어두운 그라데이션
//    띠**를 깔고 그 위에 밝은 글자를 둔다 — 바탕이 고정되면 글자색도 고정된다.
//    외곽선으로 해결하려 들면 글자가 뭉개지고 머티리얼만 는다.
// ============================================================

public static class FacilityPopupCreator
{
    const string SavePath = "Assets/_project/2.Prefabs/UI/FacilityPopup.prefab";
    const string Tag      = "FacilityPopupCreator";

    const float PanelW = 980f;
    const float PanelH = 940f;
    const float Pad    = 26f;

    /// <summary>
    /// 선택지 기둥의 높이. 아래쪽 어둠(FacilityStage.BottomVeilH) 안에 들어가야 한다.
    ///
    /// ⚠ 배경 그림 높이 상수(ArtH·ScrimH)는 없앴다 — 이제 그림이 화면 전체다.
    /// </summary>
    const float ColumnH = FacilityStage.BottomVeilH - FacilityStage.Margin * 2f;
    const float RowH    = 96f;
    const float RowGap  = 10f;

    static readonly Color PanelBg = new Color(0.075f, 0.082f, 0.135f, 0.99f);
    static readonly Color RowBg   = new Color(0.115f, 0.125f, 0.20f, 1f);
    static readonly Color SubText = new Color(0.70f, 0.75f, 0.88f, 1f);

    [MenuItem(ProjectKMenu.Popup + "시설", priority = ProjectKMenu.PrefabPrio + 45)]
    public static void Run()
    {
        // ⚠ 그림이 없으면 굽지 않는다 — 빈 배경을 구워 두면 프리팹만 보고는
        //   무엇이 빠졌는지 알 수 없다 (RunNodeArtAssets 의 계약과 같다).
        if (!RunNodeArtAssets.TryLoad(Tag, out Sprite[] nodeArt)) return;

        // ⚠ 이벤트 그림도 같은 계약이다 — 없으면 굽지 않는다
        //   빈 배열을 구워 두면 이벤트만 배경이 사라지는데, 프리팹만 보고는
        //   무엇이 빠졌는지 알 수 없다.
        if (!RunEventArtAssets.TryLoad(Tag, out Sprite[] eventArt)) return;
        if (!UIIconAssets.TryLoadGold(Tag, out Sprite goldIcon))    return;

        var root = new GameObject("FacilityPopup", typeof(RectTransform));
        root.AddComponent<CanvasGroup>();
        var popup = root.AddComponent<FacilityPopup>();

        // ⚠ 전체화면 무대다 — 가운데 패널이 아니다 (사용자 요청, 2026-09-07)
        //   그림이 머리에만 붙은 창은 "창" 이지 "장소" 가 아니다.
        //   네 시설이 같은 무대(FacilityStage)를 써야 같은 세계로 읽힌다.
        FacilityStage.Build(root, out Image art, out TextMeshProUGUI title,
                            out TextMeshProUGUI flavor, out GameObject content,
                            out Button closeBtn);

        // ⚠ 지갑은 아이콘 배지다 — 제목 줄에 "보유 6020 G" 를 적지 않는다
        //   (사용자 지적, 2026-09-09 — 한 줄에 숫자를 몰아 두면 아무도 안 읽는다)
        TextMeshProUGUI purse = FacilityStage.PurseBadge(root, goldIcon, "Purse");

        // ── 선택지 기둥 ──
        //   아래쪽 어둠 안에 세운다. 가로를 화면 끝까지 늘이지 않는 이유는
        //   1920 짜리 한 줄이 되면 글이 좌우로 흩어져 읽는 눈이 왕복하기 때문이다.
        var panel = EditorUIBuilder.Go("Column", content);
        {
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, FacilityStage.Margin);
            rt.sizeDelta        = new Vector2(PanelW, ColumnH);
        }

        // ── 선택지 ──
        var rows = new FacilityPopup.RowView[FacilityPopup.MaxRows];
        float y  = 0f;

        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = BuildRow(panel, i, y);
            y += RowH + RowGap;
        }

        var so = new SerializedObject(popup);
        EditorUIBuilder.SetEnum(so, "_popupType", (int)PopupType.Facility, Tag);
        EditorUIBuilder.SetObj(so, "_art",        art,      Tag);
        EditorUIBuilder.SetObj(so, "_titleText",  title,    Tag);
        EditorUIBuilder.SetObj(so, "_flavorText", flavor,   Tag);
        EditorUIBuilder.SetObj(so, "_purseValue", purse,    Tag);
        EditorUIBuilder.SetObj(so, "_closeBtn",   closeBtn, Tag);

        // ⚠ RunNodeRule.AllKinds 순서 그대로 — 런타임이 그 인덱스로 그림을 찾는다
        EditorUIBuilder.SetObjArray(so, "_nodeArt",  nodeArt,  Tag);
        EditorUIBuilder.SetObjArray(so, "_eventArt", eventArt, Tag);

        SerializedProperty arr = so.FindProperty("_rows");
        arr.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Root")    .objectReferenceValue = rows[i].Root;
            e.FindPropertyRelative("Button")  .objectReferenceValue = rows[i].Button;
            e.FindPropertyRelative("NameText").objectReferenceValue = rows[i].NameText;
            e.FindPropertyRelative("DescText").objectReferenceValue = rows[i].DescText;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[FacilityPopupCreator] 생성 완료 → " + SavePath +
                  "\nPopupManager > Load Popup Prefabs 를 눌러야 열린다.");
    }

    /// <summary>선택지 한 줄 — 이름(굵게) 왼쪽, 설명(작게) 오른쪽.</summary>
    static FacilityPopup.RowView BuildRow(GameObject panel, int index, float y)
    {
        var slot = EditorUIBuilder.Go($"Row_{index + 1}", panel);
        {
            var rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta        = new Vector2(-(Pad * 2f), RowH);
        }

        // ⚠ 누를 수 있는 것이므로 음각 버튼이다 (UI 규칙 1)
        Button button = EditorUIBuilder.RaisedBtnOn(slot, RowBg, out GameObject body);

        var name = EditorUIBuilder.TMP(body, "Name", "이름", UIScale.FontMd, FontStyles.Bold);
        {
            var rt = name.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0.42f, 1f);
            rt.offsetMin = new Vector2(22f, 0f); rt.offsetMax = Vector2.zero;
        }
        name.alignment        = TextAlignmentOptions.MidlineLeft;
        name.raycastTarget    = false;
        name.textWrappingMode = TextWrappingModes.NoWrap;

        var desc = EditorUIBuilder.TMP(body, "Desc", "설명", UIScale.FontSm, FontStyles.Normal);
        {
            var rt = desc.rectTransform;
            rt.anchorMin = new Vector2(0.42f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(-22f, 0f);
        }
        desc.alignment     = TextAlignmentOptions.MidlineLeft;
        desc.color         = SubText;
        desc.raycastTarget = false;

        // 긴 설명도 한 줄에 담는다 — 줄이지 말고 줄여 쓴다 (UI 규칙 5).
        desc.enableAutoSizing = true;
        desc.fontSizeMax      = UIScale.FontSm;
        desc.fontSizeMin      = UIScale.FontSm * 0.72f;

        slot.SetActive(false);

        return new FacilityPopup.RowView
        {
            Root     = slot,
            Button   = button,
            NameText = name,
            DescText = desc,
        };
    }
}
