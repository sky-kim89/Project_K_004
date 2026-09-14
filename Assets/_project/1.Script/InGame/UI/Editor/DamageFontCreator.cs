#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// ============================================================
//  DamageFontCreator.cs  [Editor Only]
//  피해 숫자 전용 TMP 폰트 에셋을 굽는다.
//
//  사용: Tools > Project K > 아이콘·텍스처 > 데미지 숫자 폰트
//  산출: Assets/Resources/DamageNumberFont.asset (+ 아틀라스·머티리얼 서브에셋)
//
//  ══════════════════════════════════════════════════════════
//  ■ 왜 전용 폰트를 굽는가 — 숫자가 대량으로 뜨기 때문이다
//  ══════════════════════════════════════════════════════════
//
//    ① 정적 아틀라스 (AtlasPopulationMode.Static)
//       기본 폰트(LiberationSans SDF)나 동적 폰트를 그대로 쓰면, 처음 보는
//       글자가 나올 때마다 TMP 가 **런타임에 글리프를 렌더링해 아틀라스
//       텍스처를 갱신**한다. 텍스처 업로드가 끼는 순간 프레임이 튄다.
//       한 판에 수백 번 숫자가 뜨는 게임에서 그 위험을 남길 이유가 없다.
//       필요한 글자를 미리 전부 구워 두고 문을 닫는다.
//
//    ② 문자 열여덟 자뿐 (DamageFontNames.Charset)
//       한글·라틴 전체를 담은 폰트는 아틀라스가 크고, 큰 아틀라스는
//       텍스처 캐시를 잡아먹는다. 숫자와 축약 기호만 있으면 512×512 한 장에
//       넉넉히 들어간다.
//
//    ③ 아틀라스 한 장만 (enableMultiAtlasSupport = false)
//       아틀라스가 여러 장이면 머티리얼도 여러 벌이 되어 **드로우콜이 갈린다.**
//       한 장으로 묶어야 숫자 여든 개가 한 번에 그려진다.
//
//    ④ 외곽선을 공유 머티리얼에 굽는다
//       숫자마다 outlineWidth 를 만지면 TMP 가 머티리얼을 복제해
//       개체 수만큼 드로우콜이 생긴다. 여기서 한 번 구워 두면
//       DamageNumberLayer 는 fontSharedMaterial 을 그대로 쓰기만 하면 된다.
//       (색은 vertex color 라 공유 머티리얼을 깨지 않는다)
//
//  ══════════════════════════════════════════════════════════
//  ■ 원본 글꼴 — TDS_RPG_2
//    이 게임의 아트가 픽셀이라 본문 폰트와 결이 맞는다. 프로젝트 안에 있어
//    패키지 갱신으로 사라지지도 않는다.
//    ⚠ 바꾸려면 SourceFont 만 갈고 다시 실행하면 된다. 굽는 순서는 그대로다.
//
//  ⚠ 실행한 뒤 아무것도 안 바뀐 것처럼 보이면 폰트가 이미 최신이라는 뜻이다.
//    이 도구는 매번 에셋을 통째로 다시 만든다 (덮어쓰기가 아니라 재생성).
// ============================================================

public static class DamageFontCreator
{
    const string SourceFont = "Assets/PixelFantasy/Common/Fonts/TDS_RPG_2.ttf";
    const string OutputPath = "Assets/Resources/" + DamageFontNames.ResourceKey + ".asset";
    const string Tag        = "DamageFontCreator";

    // ── 굽기 설정 ────────────────────────────────────────────
    //
    //  ⚠ Padding 이 외곽선의 상한이다
    //    SDF 는 글자 주위 여백에 거리장을 담는다. 외곽선 폭은 그 여백을
    //    나눠 쓰는 값이라, Padding 이 좁으면 굵은 외곽선이 잘려 각져 보인다.
    //    9 는 SamplingSize 78 기준으로 0.3 두께까지 견딘다.
    const int SamplingSize = 78;
    const int Padding      = 9;
    const int AtlasSize    = 512;

    /// <summary>외곽선 두께 (0~1, 거리장 기준). 어두운 전장에서 숫자가 뜨려면 굵어야 한다.</summary>
    const float OutlineWidth = 0.22f;

    [MenuItem(ProjectKMenu.Icon + "데미지 숫자 폰트", priority = ProjectKMenu.IconPrio + 11)]
    public static void Create()
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
        if (source == null)
        {
            Debug.LogError($"[{Tag}] 원본 글꼴을 찾지 못했습니다 — {SourceFont}");
            return;
        }

        // ⚠ 먼저 동적으로 만들고 글자를 채운 뒤 정적으로 잠근다
        //   정적으로 시작하면 글리프를 추가할 수 없어 빈 아틀라스가 나온다.
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
            source, SamplingSize, Padding, GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize,
            AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);

        if (font == null)
        {
            Debug.LogError($"[{Tag}] 폰트 에셋 생성에 실패했습니다 — {SourceFont} 를 읽을 수 없습니다.");
            return;
        }

        font.name = DamageFontNames.ResourceKey;

        if (!font.TryAddCharacters(DamageFontNames.Charset, out string missing))
            Debug.LogWarning($"[{Tag}] 원본 글꼴에 없는 글자가 있습니다 — \"{missing}\". " +
                             "그 글자는 화면에서 □ 로 나옵니다.");

        // 문을 닫는다 — 이후 런타임에 아틀라스를 다시 굽지 않는다.
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        font.ReadFontAssetDefinition();

        BakeOutline(font.material);
        Save(font);

        Debug.Log($"[{Tag}] 완료 — {OutputPath} " +
                  $"(글자 {font.characterTable.Count}자 / 아틀라스 {AtlasSize}×{AtlasSize} 1장)");
    }

    /// <summary>
    /// 외곽선과 그림자를 <b>공유</b> 머티리얼에 굽는다.
    ///
    /// 숫자 하나하나가 이 값을 만지면 TMP 가 머티리얼을 복제해 드로우콜이
    /// 개수만큼 늘어난다. 여기서 한 번 정해 두면 런타임은 손댈 일이 없다.
    /// </summary>
    static void BakeOutline(Material mat)
    {
        mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.03f, 0.06f, 1f));
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);

        // 살짝 아래로 깔리는 그림자 — 밝은 배경에서도 숫자가 뜬다.
        mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.65f));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX,  0.6f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate,   0.1f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);
    }

    /// <summary>
    /// 에셋으로 굳힌다. 아틀라스 텍스처와 머티리얼은 <b>서브에셋</b>으로 붙인다 —
    /// 따로 두면 폰트만 옮겼을 때 조용히 분홍색(머티리얼 없음)이 된다.
    /// </summary>
    static void Save(TMP_FontAsset font)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

        // 통째로 다시 만든다 — 옛 서브에셋이 남아 아틀라스가 두 장이 되는 것을 막는다.
        if (File.Exists(OutputPath)) AssetDatabase.DeleteAsset(OutputPath);

        AssetDatabase.CreateAsset(font, OutputPath);

        for (int i = 0; i < font.atlasTextures.Length; i++)
        {
            Texture2D atlas = font.atlasTextures[i];
            if (atlas == null) continue;

            atlas.name = font.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, font);
        }

        font.material.name = font.name + " Material";
        AssetDatabase.AddObjectToAsset(font.material, font);

        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif
