#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// ============================================================
//  RunEventArtAssets.cs  [Editor Only]
//  이벤트 타이틀 그림 PNG 의 경로 정본. RunNodeArtAssets 와 같은 역할이다.
//
//  ■ 왜 갈림길 그림과 따로 두는가
//    갈림길 그림(node_Event)은 **"이벤트라는 칸"** 의 그림이다 — 갈림길에서
//    두 갈래 중 하나로 보일 때 쓴다. 이건 **"어느 이벤트인가"** 의 그림이다.
//    한 폴더에 섞으면 열다섯 장 중 어느 것이 칸 그림인지 알 수 없고,
//    RunNodeArtAssets.TryLoad 가 폴더를 훑는 것도 아니라 조용히 어긋난다.
//
//  ■ 순서가 곧 계약이다
//    TryLoad 는 <see cref="RunEventId"/> 번호 순서로 돌려준다. 런타임
//    (FacilityPopup._eventArt)은 그 번호로 그림을 찾으므로, 순서가 어긋나면
//    "잊힌 서고인데 갱도 그림이 뜨는" 상태가 된다.
//    ⚠ RunEventId 를 중간에 끼우면 그 뒤가 전부 밀린다 — 뒤에만 추가할 것.
//
//  ■ ⚠ 지금 들어 있는 것은 더미다
//    RunEventArtGenerator 가 도형으로 굽는다. 손그림이 준비되면 **같은
//    경로·같은 파일명**으로 덮으면 코드는 그대로 돌아간다.
// ============================================================

public static class RunEventArtAssets
{
    public const string Dir = "Assets/_project/3.Textures/Icons/RunEvents/";

    public static string PathOf(RunEventId id) => $"{Dir}event_{id}.png";

    /// <summary>
    /// 열다섯 장을 <b>RunEventId 번호 순서로</b> 읽는다.
    /// 하나라도 없으면 false — 부르는 쪽은 굽기를 중단해야 한다.
    /// </summary>
    public static bool TryLoad(string tag, out Sprite[] art)
    {
        int count = RunEventRule.Count;
        art = new Sprite[count];

        for (int i = 0; i < count; i++)
        {
            string path = PathOf((RunEventId)i);

            UIIconAssets.Configure(path);
            art[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (art[i] != null) continue;

            Debug.LogError($"[{tag}] 이벤트 그림이 없습니다: {path}\n" +
                           "Tools > Project K > 아이콘·텍스처 > 이벤트 그림 을 먼저 실행하세요.");
            art = null;
            return false;
        }

        return true;
    }
}
#endif
