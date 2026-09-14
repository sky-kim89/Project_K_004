#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// ============================================================
//  IconImportSetup.cs  [Editor Only]
//  3.Textures/Icons/ 아래 모든 PNG 의 임포트 설정을 자동으로 맞춘다.
//
//  ■ 왜 필요한가
//    아이콘은 두 경로로 들어온다 —
//      · IconGenerator 계열이 코드로 굽는 것 (설정을 같이 넣는다)
//      · 밖에서 만들어 **덮어씌우는 것** (설정을 넣을 자리가 없다)
//    두 번째 경로가 늘면서 폴더마다 설정이 제각각이 됐다.
//    실제로 아이콘 270장이 Mesh Type = Tight 로 들어와 있었다 (2026-08-26).
//
//  ■ ⚠ Mesh Type 은 반드시 Full Rect 여야 한다
//    Tight 는 알파를 따라 다각형으로 잘라 스프라이트 사각형과 그림 영역을 어긋나게 한다.
//    UGUI Image(Type.Simple)는 사각형 기준으로 그리므로 그림이 밀리거나 잘린다.
//    회전이 얹히는 노드(유물 트리의 마름모)에서는 모서리가 통째로 날아간다.
//    아틀라스 쪽 enableTightPacking = false 와 짝이다 (SpriteManagerCreator 참고).
//
//  ■ 폴더별 예외
//    RelicTree 만 압축을 끄고 상한을 256 으로 둔다 — 트리에서 확대해 보는 그림이라
//    압축 아티팩트가 그대로 읽힌다. 나머지는 기존 설정을 존중한다.
//
//  ⚠ 이미 임포트된 파일에는 소급되지 않는다
//    OnPreprocessTexture 는 (재)임포트 시점에만 돈다. 폴더째 다시 적용하려면
//    Tools > Project K > 아이콘·텍스처 > 아이콘 임포트 설정 전체 재적용 을 쓸 것.
// ============================================================

public class IconImportSetup : AssetPostprocessor
{
    public const string IconRoot = "Assets/_project/3.Textures/Icons";

    void OnPreprocessTexture()
    {
        if (!IsIcon(assetPath)) return;
        Apply((TextureImporter)assetImporter, assetPath);
    }

    /// <summary>아이콘 폴더 안의 PNG 인가.</summary>
    public static bool IsIcon(string path)
    {
        path = path.Replace('\\', '/');
        return path.StartsWith(IconRoot + "/") &&
               path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>UI 스프라이트로 쓸 수 있는 설정을 얹는다. 재적용 도구도 이 함수를 쓴다.</summary>
    public static void Apply(TextureImporter ti, string path)
    {
        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled       = false;
        ti.sRGBTexture         = true;
        ti.wrapMode            = TextureWrapMode.Clamp;
        ti.filterMode          = FilterMode.Bilinear;

        // ⚠ 여기가 핵심이다 — 파일 상단 주석 참고
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude  = 1;
        ti.SetTextureSettings(settings);

        // 유물 트리만 확대해 보는 그림이라 압축을 끈다.
        if (path.Replace('\\', '/').StartsWith(RelicIconKey.FolderPath + "/"))
        {
            ti.maxTextureSize     = 256;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }

    // ── 전체 재적용 ──────────────────────────────────────────

    [MenuItem(ProjectKMenu.Icon + "아이콘 임포트 설정 전체 재적용",
              priority = ProjectKMenu.IconPrio + 70)]
    public static void ReapplyAll()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { IconRoot });
        int n = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsIcon(path)) continue;

                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                Apply(ti, path);
                ti.SaveAndReimport();
                n++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        Debug.Log($"[IconImportSetup] 임포트 설정 재적용 — {n}장.\n" +
                  "이어서 '데이터 생성 > SpriteManager + 아틀라스' 를 실행해 아틀라스를 다시 구울 것.");
    }
}
#endif
