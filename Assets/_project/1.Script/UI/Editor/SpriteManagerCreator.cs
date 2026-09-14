#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

// ============================================================
//  SpriteManagerCreator.cs  [Editor Only]
//  SpriteAtlas 6종 + SpriteManager.asset 자동 생성 도구.
//
//  사용법:
//    Unity 메뉴 → BattleGame → 데이터 생성 → 스프라이트 매니저 생성
//
//  아틀라스 소스 폴더 → spriteatlas 파일:
//    Items/                          → Atlas_Items.spriteatlas
//    Classes/ + Skills/              → Atlas_General.spriteatlas
//    Difficulty/ + RunPerks/         → Atlas_Traits.spriteatlas (이름만 원작 그대로)
//    RelicTree/                      → Atlas_RelicTree.spriteatlas
//    StageNodes/                     → Atlas_StageNodes.spriteatlas
//    LobbyBtns/                      → Atlas_LobbyBtns.spriteatlas
// ============================================================

public static class SpriteManagerCreator
{
    const string ResourcesDir = "Assets/Resources";
    const string ManagerPath  = "Assets/Resources/SpriteManager.asset";
    const string IconRoot     = "Assets/_project/3.Textures/Icons";

    const string ItemAtlasPath      = IconRoot + "/Atlas_Items.spriteatlas";
    const string GenAtlasPath       = IconRoot + "/Atlas_General.spriteatlas";
    const string TraitAtlasPath     = IconRoot + "/Atlas_Traits.spriteatlas";
    const string RelicTreeAtlasPath = IconRoot + "/Atlas_RelicTree.spriteatlas";
    const string StageNodeAtlasPath = IconRoot + "/Atlas_StageNodes.spriteatlas";
    const string LobbyBtnAtlasPath  = IconRoot + "/Atlas_LobbyBtns.spriteatlas";

    [MenuItem(ProjectKMenu.Data + "SpriteManager + 아틀라스", priority = ProjectKMenu.DataPrio + 20)]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesDir))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // ── 아틀라스 6종 생성 ────────────────────────────────
        var itemAtlas      = CreateAtlas(ItemAtlasPath,      new[] { $"{IconRoot}/Items" });
        var genAtlas       = CreateAtlas(GenAtlasPath,       new[] { $"{IconRoot}/Classes", $"{IconRoot}/Skills" });
        // 난이도 아이콘(등급 5 + 디버프 4)은 특성 아틀라스에 얹는다.
        // 디버프가 특성 바에 같이 표시되므로 같은 아틀라스에 있어야
        // 드로우콜이 안 갈라진다. SpriteManager.Get 은 아틀라스를 전부 훑으므로
        // 키(difficulty_* / debuff_*)만 안 겹치면 된다.
        // ⚠ 런 특성(perk_*)도 여기 얹는다 (2026-09-07)
        //   30종이고 무엇이 뜰지 런타임에 정해져서 Creator 가 Sprite[] 를
        //   미리 박아 둘 수 없다 — SpriteManager 로 키 조회한다
        //   (RunPerkIconKey). 난이도 아이콘이 여기 얹혀 있는 것과 같은 이유다.
        //   ⚠ 이 줄에서 폴더가 빠지면 특성 아이콘이 전부 빈 그림이 된다.
        var traitAtlas     = CreateAtlas(TraitAtlasPath,
                                         new[] { $"{IconRoot}/Difficulty", $"{IconRoot}/RunPerks" });
        // 트리 노드. ⚠ 구 Atlas_Relics 는 폴더째 지웠다 (2026-09-07)
        var relicTreeAtlas = CreateAtlas(RelicTreeAtlasPath, new[] { RelicIconKey.FolderPath });
        var stageNodeAtlas = CreateAtlas(StageNodeAtlasPath, new[] { $"{IconRoot}/StageNodes" });
        var lobbyBtnAtlas  = CreateAtlas(LobbyBtnAtlasPath,  new[] { $"{IconRoot}/LobbyBtns" });

        AssetDatabase.SaveAssets();

        // ── SpriteManager.asset 생성 / 갱신 ─────────────────
        var manager = AssetDatabase.LoadAssetAtPath<SpriteManager>(ManagerPath);
        if (manager == null)
        {
            manager = ScriptableObject.CreateInstance<SpriteManager>();
            AssetDatabase.CreateAsset(manager, ManagerPath);
        }

        var so = new SerializedObject(manager);
        so.Update();
        so.FindProperty("_itemAtlas")      .objectReferenceValue = itemAtlas;
        so.FindProperty("_generalAtlas")   .objectReferenceValue = genAtlas;
        so.FindProperty("_traitAtlas")     .objectReferenceValue = traitAtlas;
        so.FindProperty("_relicTreeAtlas") .objectReferenceValue = relicTreeAtlas;
        so.FindProperty("_stageNodeAtlas") .objectReferenceValue = stageNodeAtlas;
        so.FindProperty("_lobbyBtnAtlas")  .objectReferenceValue = lobbyBtnAtlas;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SpriteManagerCreator] 완료 — 아틀라스 6종, SpriteManager.asset 갱신.");
    }

    // ── 헬퍼 ─────────────────────────────────────────────────

    /// <summary>
    /// UI 아틀라스 패킹 설정. **회전·타이트 패킹을 반드시 끈다.**
    ///
    /// ⚠ enableRotation 이 켜져 있으면 아이콘이 화면에서 돌아간다
    ///   패커는 자리를 아끼려고 스프라이트를 90° 눕혀 넣는다. 스프라이트 자체는
    ///   회전 정보를 갖지만 **UGUI Image(Type.Simple)는 그 회전을 못 읽는다** —
    ///   UV 사각형만 그대로 쓰므로 화면에는 돌아간 그림이 나온다.
    ///   패커가 "넣기 좋을 때만" 눕히기 때문에 몇 장만 증상이 나타나,
    ///   원본 PNG 를 아무리 열어 봐도 멀쩡해서 원인을 찾기 어렵다.
    ///   (2026-08-26: '영웅의 기상'(ability_b11)이 뒤집혀 보인 원인이 이것이다)
    ///
    /// ⚠ enableTightPacking 도 UI 에서는 끈다
    ///   알파를 따라 다각형으로 잘라 넣으면 스프라이트 사각형과 실제 그림 영역이
    ///   어긋난다. Image 는 사각형 기준으로 그리므로 그림이 밀리거나 잘린다.
    ///   텍스처 쪽 Mesh Type = Full Rect 와 짝이다 (IconImportSetup 참고).
    ///
    /// 유니티 문서도 캔버스에서 쓰는 스프라이트는 두 옵션을 끄라고 명시한다.
    /// </summary>
    static void ApplyUIPackingSettings(SpriteAtlas atlas)
    {
        var packing = SpriteAtlasExtensions.GetPackingSettings(atlas);
        packing.enableRotation     = false;
        packing.enableTightPacking = false;
        packing.padding            = 4;
        SpriteAtlasExtensions.SetPackingSettings(atlas, packing);

        // 아이콘은 확대해서 쓰는 그림이라 압축 아티팩트가 그대로 보인다.
        var tex = SpriteAtlasExtensions.GetTextureSettings(atlas);
        tex.filterMode      = FilterMode.Bilinear;
        tex.generateMipMaps = false;
        tex.sRGB            = true;
        SpriteAtlasExtensions.SetTextureSettings(atlas, tex);
    }

    static SpriteAtlas CreateAtlas(string atlasPath, string[] sourceFolders)
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, atlasPath);
        }

        ApplyUIPackingSettings(atlas);

        // 기존 packable 초기화 후 폴더 재등록
        SpriteAtlasExtensions.Remove(atlas, SpriteAtlasExtensions.GetPackables(atlas));

        var packables = new Object[sourceFolders.Length];
        for (int i = 0; i < sourceFolders.Length; i++)
        {
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(sourceFolders[i]);
            if (folder == null)
                Debug.LogWarning($"[SpriteManagerCreator] 폴더 없음(아직 미생성): {sourceFolders[i]}");
            packables[i] = folder;
        }

        SpriteAtlasExtensions.Add(atlas, packables);
        EditorUtility.SetDirty(atlas);
        return atlas;
    }
}
#endif
