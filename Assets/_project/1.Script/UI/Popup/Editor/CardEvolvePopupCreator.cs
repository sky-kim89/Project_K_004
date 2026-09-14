using UnityEditor;
using UnityEngine;

// ============================================================
//  CardEvolvePopupCreator.cs  [Editor Only]
//  Tools > Project K > 프리팹 생성 > 팝업 > 진화·융합
//  만렙 카드를 골랐을 때 뜨는 진화/융합 갈림길 팝업을 굽는다.
//
//  ■ 왜 HUD 에서 팝업으로 옮겼나 (사용자 지적, 2026-09-07)
//    이 창은 원래 InGameHUD 프리팹의 자식이었다. 팝업 체계(PopupManager ·
//    PopupBase · PopupType)가 멀쩡히 있고 바로 앞 화면인 카드 3택도
//    팝업인데, 이것만 HUD 안에 있었다. 그래서 셋이 따라왔다:
//
//      ① 겹침 순서를 손으로 맞춰야 했다
//         HUD 자식은 sortingOrder 가 없어 계층 순서가 곧 그리는 순서다.
//         카드 바가 나중에 만들어지면 덮개 위로 올라타, 그걸 되돌리는
//         BringOverlaysToFront 가 따로 있어야 했다.
//      ② HUD 를 굽다 중간에 멈추면 창이 통째로 사라졌다
//         종족 패시브 아이콘을 못 읽으면 빌더가 빠져나가는데, 그러면
//         HUD 는 창 없이 구워진다. 프리팹만 보고는 무엇이 빠졌는지 모른다.
//      ③ 사라져도 런타임은 조용했다
//         RunBootstrap 이 참조가 null 이면 그냥 넘어가서, 화면에서는
//         "진화를 눌렀는데 아무 일도 안 난다" 로만 보였다.
//
//    팝업은 PopupManager 가 제 프리팹을 따로 들고 열어 주므로 셋 다 없다.
//    못 열면 Open 이 null 을 돌려주고, 부르는 쪽이 그걸 말한다.
//
//  ■ 그림 조립은 옮기지 않았다
//    내용물 200줄은 InGameUIPrefabCreator.BuildCardEvolveRoot 에 그대로 있고
//    여기서는 그 함수를 불러 껍데기만 씌운다. 옮기면 같은 코드가 두 벌이 될
//    위험이 있다 — 그 파일은 실제로 한 번 444줄이 중복된 적이 있다.
//
//  ⚠ 굽기 전에 종족 패시브 아이콘이 있어야 한다
//    없으면 빌더가 null 을 돌려주고 이 도구는 **아무것도 저장하지 않는다**
//    (반쯤 만든 프리팹을 남기지 않는다). 그때는 무엇을 먼저 누르라고 말해 준다.
//
//  ⚠ 구운 뒤 PopupManager 의 [Load Popup Prefabs] 를 눌러야 열린다.
// ============================================================

public static class CardEvolvePopupCreator
{
    const string SaveRoot = "Assets/_project/2.Prefabs/UI";
    const string Tag      = "CardEvolvePopupCreator";
    const string FileName = "CardEvolvePopup";

    [MenuItem(ProjectKMenu.Popup + "진화·융합", priority = ProjectKMenu.PrefabPrio + 49)]
    public static void Create()
    {
        // 카드 아이콘이 없을 때 대신 쓸 그림 — 하단 카드 바와 같은 것을 쓴다.
        // ⚠ 못 읽으면 짓지 않는다. 아이콘 없는 창을 구워 두면 프리팹만 보고는
        //   무엇이 빠졌는지 알 수 없다 (마나·마릿수 아이콘은 UIIconAssets 가 정본).
        if (!UIIconAssets.TryLoad(Tag, out _, out Sprite fallback)) return;

        // ⚠ 임시 부모를 하나 두고 그 밑에 짓는다
        //   빌더가 EditorUIBuilder.Go(name, parent) 로 시작하므로 부모가 필요하다.
        //   저장은 자식(root)만 가져가고, 부모는 곧바로 지운다.
        var holder = new GameObject("~CardEvolveHolder", typeof(RectTransform));

        GameObject root = InGameUIPrefabCreator.BuildCardEvolveRoot(holder, fallback);

        if (root == null)
        {
            Object.DestroyImmediate(holder);
            Debug.LogError($"[{Tag}] 진화·융합 팝업을 만들지 못했습니다.\n" +
                           "Tools > Project K > 아이콘·텍스처 > 종족 패시브 아이콘 을 먼저 실행하세요.");
            return;
        }

        // PopupBase._popupType 은 [SerializeField] 다 — 컴포넌트를 붙인 뒤에 채운다.
        // ⚠ enumValueIndex 가 아니라 intValue 다. PopupType 은 20 번이 비어 있어
        //   자리 번호와 값이 어긋난다 (RunPopupCreator.Save 의 같은 주의).
        var popup = root.GetComponent<CardEvolveUI>();
        var so    = new SerializedObject(popup);
        so.FindProperty("_popupType").intValue = (int)PopupType.CardEvolve;
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = $"{SaveRoot}/{FileName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(holder);

        Debug.Log($"[{Tag}] 저장: {path}\n" +
                  "⚠ PopupManager 의 [Load Popup Prefabs] 를 눌러야 열립니다.");
    }
}
