#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// ============================================================
//  RunNodeArtAssets.cs  [Editor Only]
//  갈림길·시설 그림 PNG 의 경로 정본. SynergyIconAssets 와 같은 역할이다.
//
//  ■ 왜 따로 두는가
//    이 일곱 장을 꽂는 곳이 둘이다.
//      InGameUIPrefabCreator — 갈림길 두 갈래 카드의 그림
//      FacilityPopupCreator  — 시설 화면의 배경
//    각자 경로를 들고 있으면 그림을 옮기는 순간 한쪽만 고쳐지고
//    나머지가 조용히 빈 그림이 된다.
//
//  ■ 순서가 곧 계약이다
//    Load 는 RunNodeRule.AllKinds 순서로 돌려준다. 런타임(CrossroadUI ·
//    FacilityPopup)은 그 인덱스로 그림을 찾으므로, 순서가 어긋나면
//    "야영지인데 상점 그림이 뜨는" 상태가 된다.
//    인덱스의 정본은 RunNodeRule.IndexOf 한 곳이다.
//
//  ■ ⚠ 지금 들어 있는 것은 더미다
//    RunNodeArtGenerator 가 도형으로 굽는다. 손그림이 준비되면 **같은
//    경로·같은 파일명**으로 덮으면 코드는 그대로 돌아간다.
// ============================================================

public static class RunNodeArtAssets
{
    public const string Dir = "Assets/_project/3.Textures/Icons/RunNodes/";

    public static string PathOf(RunNodeKind kind) => $"{Dir}node_{kind}.png";

    /// <summary>
    /// 일곱 장을 <b>AllKinds 순서로</b> 읽는다.
    /// 하나라도 없으면 false — 부르는 쪽은 굽기를 중단해야 한다.
    /// </summary>
    public static bool TryLoad(string tag, out Sprite[] art)
    {
        RunNodeKind[] all = RunNodeRule.AllKinds;
        art = new Sprite[all.Length];

        for (int i = 0; i < all.Length; i++)
        {
            string path = PathOf(all[i]);

            UIIconAssets.Configure(path);
            art[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (art[i] != null) continue;

            Debug.LogError($"[{tag}] 갈림길 그림이 없습니다: {path}\n" +
                           "Tools > Project K > 아이콘·텍스처 > 갈림길 그림 을 먼저 실행하세요.");
            art = null;
            return false;
        }

        return true;
    }
}
#endif
