#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// ============================================================
//  SpeciesPassiveIconAssets.cs  [Editor Only]
//  종족 패시브 아이콘 PNG 의 경로 정본. SynergyIconAssets 와 같은 역할이다.
//
//  ■ 왜 따로 두는가
//    이 19장을 꽂는 곳이 둘이다.
//      RunPopupCreator        — 카드 3택의 패시브 칩
//      InGameUIPrefabCreator  — 융합 창(CardEvolveUI)의 패시브 칩
//    각자 경로를 들고 있으면 아이콘을 옮기는 순간 한쪽만 고쳐지고
//    나머지가 조용히 빈 그림이 된다.
//
//  ■ 순서가 곧 계약이다
//    Load 는 SpeciesPassiveRule.All 순서로 돌려준다. 런타임(CardSelectPopup ·
//    CardEvolveUI)은 그 인덱스로 그림을 찾으므로, 순서가 어긋나면
//    "재생인데 해골이 뜨는" 상태가 된다. 인덱스의 정본은
//    SpeciesPassiveRule.IndexOf 한 곳이다.
// ============================================================

public static class SpeciesPassiveIconAssets
{
    public const string Dir = "Assets/_project/3.Textures/Icons/SpeciesPassives/";

    public static string PathOf(SpeciesPassive passive) => $"{Dir}passive_{passive}.png";

    /// <summary>
    /// 19장을 <b>All 순서로</b> 읽는다.
    /// 하나라도 없으면 false — 부르는 쪽은 굽기를 중단해야 한다.
    /// 빈 칩을 만들어 두면 프리팹만 보고는 무엇이 빠졌는지 알 수 없다.
    /// </summary>
    public static bool TryLoad(string tag, out Sprite[] icons)
    {
        SpeciesPassive[] all = SpeciesPassiveRule.All;
        icons = new Sprite[all.Length];

        for (int i = 0; i < all.Length; i++)
        {
            string path = PathOf(all[i]);

            UIIconAssets.Configure(path);
            icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (icons[i] != null) continue;

            Debug.LogError($"[{tag}] 종족 패시브 아이콘이 없습니다: {path}\n" +
                           "Tools > Project K > 아이콘·텍스처 > 종족 패시브 아이콘 을 먼저 실행하세요.");
            icons = null;
            return false;
        }

        return true;
    }
}
#endif
