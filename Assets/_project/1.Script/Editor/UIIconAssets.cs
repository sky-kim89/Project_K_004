#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// ============================================================
//  UIIconAssets.cs  [Editor Only]
//  마나·마릿수 아이콘 PNG 의 경로 정본.
//
//  ■ 왜 따로 두는가
//    이 두 장을 붙이는 곳이 둘이다.
//      InGameUIPrefabCreator  — 하단 카드 바의 배지
//      RunPopupCreator        — 카드 3택 초상화 머리 위 배지
//    각자 경로 문자열을 들고 있으면 아이콘을 옮기는 순간 한쪽만 고쳐지고
//    나머지가 조용히 빈 그림이 된다.
//
//  ■ ⚠ 아이콘을 '생성' 하는 도구는 없다 — 만들지도 말 것
//    PNG 두 장이 이미 있고, Image.sprite 에 그대로 꽂으면 그려진다.
//    한때 TMP 스프라이트 태그로 글 안에 박겠다고 아틀라스를 굽는 도구를
//    뒀는데, 굽기 전에는 화면에 ❓ 가 뜨는 단계를 하나 더 만든 것뿐이었다.
//
//  ⚠ 스프라이트 임포트 설정도 여기서 맞춘다
//    Texture Type 이 Default 인 채로는 Image.sprite 에 넣을 수 없다.
//    아이콘을 새로 그려 넣었을 때 "왜 안 보이지" 의 대부분이 이것이다.
// ============================================================

public static class UIIconAssets
{
    const string Dir = "Assets/_project/3.Textures/Icons/InGame/";

    public const string ManaPath  = Dir + "icon_mana.png";
    public const string CountPath = Dir + "icon_summon_count.png";

    /// <summary>
    /// 골드 아이콘. 품질 개선(MonsterGradeUpgradeRule)이 영구 골드로 값을 치르므로
    /// 도감·몬스터 상세가 지갑과 값을 이 그림으로 그린다.
    ///
    /// ⚠ 위 둘과 폴더가 다르다 — 이건 <b>재화</b> 아이콘이라 Icons/Items 에 산다
    ///   (eItem.IconKey 가 런타임에서 같은 PNG 를 SpriteManager 로 찾는다).
    /// </summary>
    public const string GoldPath = "Assets/_project/3.Textures/Icons/Items/item_gold.png";

    /// <summary>
    /// 두 아이콘을 스프라이트로 읽는다. 하나라도 없으면 false —
    /// 부르는 쪽은 <b>굽기를 중단</b>해야 한다. 빈 배지를 만들어 두면
    /// 프리팹만 보고는 무엇이 빠졌는지 알 수 없다.
    /// </summary>
    public static bool TryLoad(string tag, out Sprite mana, out Sprite count)
    {
        Configure(ManaPath);
        Configure(CountPath);

        mana  = AssetDatabase.LoadAssetAtPath<Sprite>(ManaPath);
        count = AssetDatabase.LoadAssetAtPath<Sprite>(CountPath);

        if (mana != null && count != null) return true;

        Debug.LogError($"[{tag}] UI 아이콘을 불러오지 못했습니다.\n{ManaPath}\n{CountPath}");
        return false;
    }

    /// <summary>
    /// 골드 아이콘 한 장. 없으면 false — 부르는 쪽은 굽기를 중단해야 한다.
    ///
    /// ⚠ Configure 를 부르지 않는다
    ///   이 PNG 는 Atlas_Items 가 이미 굽고 있다. 여기서 임포트 설정을 덮어쓰면
    ///   아틀라스에 들어가는 다른 재화 아이콘과 규격이 갈린다.
    /// </summary>
    public static bool TryLoadGold(string tag, out Sprite gold)
    {
        gold = AssetDatabase.LoadAssetAtPath<Sprite>(GoldPath);
        if (gold != null) return true;

        Debug.LogError($"[{tag}] 골드 아이콘을 불러오지 못했습니다: {GoldPath}");
        return false;
    }

    /// <summary>
    /// UI 에서 쓸 수 있도록 스프라이트로 임포트한다.
    ///
    /// 픽셀 아트라 Point 필터·무압축이다 — 압축하면 64px 짜리 아이콘의
    /// 가장자리에 색 번짐이 남고, Bilinear 면 도트가 뭉개진다.
    /// </summary>
    public static void Configure(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.mipmapEnabled       = false;
        importer.alphaIsTransparency = true;
        importer.filterMode          = FilterMode.Point;
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize      = 64;
        importer.SaveAndReimport();
    }
}
#endif
