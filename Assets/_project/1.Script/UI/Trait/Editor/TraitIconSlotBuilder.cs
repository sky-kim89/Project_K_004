#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  TraitIconSlotBuilder.cs  [Editor Only]
//  TraitIconUI 슬롯 1칸을 만드는 공용 팩토리.
//
//  예전에는 BattlePanelCreator 안에 있었지만 특성 바가 TopBar 로
//  옮겨가면서 소유자가 바뀌었다. 여러 Creator 가 같은 슬롯을 쓰므로
//  어느 Creator 에도 속하지 않는 자리로 뺐다.
//  (런 상점은 이 슬롯 대신 RewardCard 를 쓴다 — RunShopGoodsSlot 참고)
//
//  구조:
//    TraitSlot_N (TraitIconUI)
//    ├─ IconBtn (Image + Button)
//    │   └─ IconImage
//    └─ Tooltip (InfoTooltipUI — 기본 비활성, 클릭 시 루트 캔버스로 이동)
// ============================================================

public static class TraitIconSlotBuilder
{
    /// <summary>특성 아이콘 슬롯 1칸. 처음엔 비활성 — 표시하는 쪽이 켠다.</summary>
    public static TraitIconUI Build(GameObject parent, int index, float size)
    {
        var go = new GameObject($"TraitSlot_{index}", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);

        var le = go.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = size;

        var ui = go.AddComponent<TraitIconUI>();

        // 아이콘 버튼.
        // ⚠ 배경을 칠하지 않는다 — 상단 UI 는 전장이 그대로 비쳐야 한다.
        //   다만 Image 자체는 남긴다: 이게 없으면 버튼이 클릭을 못 받는다.
        //   거의 투명한 색으로 레이캐스트만 살린다.
        var btnGo = new GameObject("IconBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(go.transform, false);
        EditorUIBuilder.Stretch(btnGo);
        btnGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

        // 아이콘 이미지 (inset)
        var imgGo = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(btnGo.transform, false);
        var imgRt = imgGo.GetComponent<RectTransform>();
        // 배경이 사라졌으니 아이콘이 칸을 꽉 채워도 된다.
        imgRt.anchorMin = Vector2.zero;
        imgRt.anchorMax = Vector2.one;
        imgRt.offsetMin = imgRt.offsetMax = Vector2.zero;
        var iconImg = imgGo.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget  = false;
        iconImg.color = new Color(0.25f, 0.25f, 0.38f);

        // 상세 툴팁 — 보상 카드와 같은 공용 컴포넌트
        var tooltip = InfoTooltipBuilder.Build(go, 300f);

        var tso = new SerializedObject(ui);
        tso.Update();
        SetObj(tso, "_iconImage",   iconImg);
        SetObj(tso, "_iconBtn",     btnGo.GetComponent<Button>());
        SetObj(tso, "_tooltip",     tooltip);
        tso.ApplyModifiedProperties();

        go.SetActive(false);   // 표시할 특성이 정해지면 그때 켠다
        return ui;
    }

    static void SetObj(SerializedObject so, string field, Object obj)
        => EditorUIBuilder.SetObj(so, field, obj, "TraitIconSlotBuilder");
}
#endif
