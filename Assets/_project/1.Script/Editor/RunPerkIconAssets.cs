#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
//  RunPerkIconAssets.cs  [Editor Only]
//  런 특성 아이콘 PNG 의 경로 정본. SpeciesPassiveIconAssets 와 같은 역할이다.
//
//  ■ ⚠ 이쪽은 **런타임이 직접 읽는다** — 다른 아이콘들과 다르다
//    종족 패시브·시너지는 Creator 가 굽는 시점에 Sprite[] 를 프리팹에 박아
//    두고, 런타임은 인덱스로 꺼낸다. 특성은 그럴 수 없다:
//      · 30종이라 배열이 길고,
//      · 쓰는 곳(특성 3택 · 상단 보유 줄)이 **무엇이 뜰지 런타임에 정해진다**.
//    그래서 아틀라스에 얹고 SpriteManager 로 키 조회한다
//    (난이도 아이콘 difficulty_* 가 특성 아틀라스에 얹혀 있는 것과 같은 방식).
//
//    ⚠ 그래서 SpriteManagerCreator 의 특성 아틀라스 폴더 목록에
//      이 Dir 이 들어 있어야 한다. 빠지면 아이콘이 전부 빈 그림이 된다.
//
//  ■ 키는 RunPerkIconKey.Of 하나가 만든다 (런타임·에디터 공용)
//    여기서 문자열을 또 조립하면 파일명과 조회 키가 갈릴 수 있다.
// ============================================================

public static class RunPerkIconAssets
{
    public const string Dir = "Assets/_project/3.Textures/Icons/RunPerks/";

    public static string PathOf(RunPerk perk) => $"{Dir}{RunPerkIconKey.Of(perk)}.png";

    /// <summary>None 을 뺀 전체 특성. 굽기·검사가 함께 쓴다.</summary>
    public static List<RunPerk> All()
    {
        var list = new List<RunPerk>(32);

        foreach (RunPerk p in System.Enum.GetValues(typeof(RunPerk)))
            if (p != RunPerk.None) list.Add(p);

        return list;
    }

    /// <summary>
    /// 한 장을 읽는다. 없으면 null — 부르는 쪽이 굽기를 멈춰야 한다.
    /// </summary>
    public static Sprite Load(string tag, RunPerk perk)
    {
        string path = PathOf(perk);

        UIIconAssets.Configure(path);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite == null)
            Debug.LogError($"[{tag}] 특성 아이콘이 없습니다: {path}\n" +
                           "Tools > Project K > 아이콘·텍스처 > 특성 아이콘 을 먼저 실행하세요.");

        return sprite;
    }
}
#endif
