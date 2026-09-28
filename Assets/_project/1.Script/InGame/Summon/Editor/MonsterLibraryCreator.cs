using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

// ============================================================
//  MonsterLibraryCreator.cs  [Editor Only]
//  비인간형 몬스터의 SpriteLibraryAsset 을 우리 Animator 규격으로 다시 굽는다.
//
//  ■ 왜 다시 굽나 — 카테고리 이름이 딱 하나 어긋난다
//    합성 캐릭터(CharacterBuilder.Layout)가 쓰는 카테고리:
//      Roll · Death · Block · Fire · Shot · Slash · Jab · Push
//      Jump · Climb · Crawl · Run · Ready · Idle
//    Bonus/Monsters 원본이 가진 카테고리:
//      Idle · Ready · Run · Attack · Death · Jump
//
//    Idle / Ready / Run / Death / Jump 는 이름이 그대로 맞는다.
//    ⚠ Death 는 맞는다 — Animator 의 "Die" 는 파라미터 이름이지 카테고리가 아니다.
//    ⚠ Hit 도 문제가 아니다 — 합성 캐릭터에도 Hit 카테고리는 없다.
//                              피격은 스프라이트 교체가 아니라 색 플래시로 처리한다.
//
//    어긋나는 건 공격 하나뿐이다:
//      원본 Attack  →  우리는 Slash(근접) 또는 Shot(원거리) 을 찾는다.
//
//  ■ 그래서 하는 일
//    원본 SpriteSheet 의 하위 스프라이트(Idle_0, Attack_2 …)를 읽어
//    같은 그림을 Slash / Shot / Jab 이름으로도 등록한 라이브러리를 새로 만든다.
//    원본 에셋은 건드리지 않는다.
//
//    Slash / Shot 을 둘 다 넣는 이유: UnitAnimationSync 가 직업이 궁수면
//    "Shot", 아니면 "Slash" 트리거를 쓴다. 몬스터가 어느 쪽으로 분류되든
//    같은 공격 그림이 나오게 해 두면 분기를 신경 쓸 필요가 없다.
//
//  ■ 애니메이션 클립은 m_SpriteKey(카테고리+라벨 해시)를 굽는다
//    그래서 카테고리 "이름" 이 정확히 맞아야 한다. 해시를 손으로 계산할 수는
//    없으므로 AddCategoryLabel() 에게 맡긴다 — 이게 이 스크립트가 존재하는 이유다.
//
//  사용: Tools > Project K > 데이터 생성 > 비인간형 몬스터 라이브러리
// ============================================================

public static class MonsterLibraryCreator
{
    const string SourceRoot = "Assets/PixelFantasy/PixelHeroes/FantasyHeroes/Bonus/Monsters";
    const string OutputRoot = "Assets/_project/Data/MonsterLibraries";

    /// <summary>
    /// 구울 대상. Barrel / Box / Chest 는 제외한다 —
    /// Death 카테고리밖에 없어 전투 유닛이 될 수 없다 (부서지는 오브젝트용 자산).
    /// </summary>
    static readonly string[] Targets = { "Wolf", "Hog", "Slug", "Troll" };

    /// <summary>
    /// 원본 카테고리 → 우리가 쓸 카테고리들.
    /// 하나를 여러 이름으로 등록할 수 있다 (Attack → Slash + Shot + Jab).
    /// </summary>
    static readonly Dictionary<string, string[]> CategoryMap = new()
    {
        { "Idle",   new[] { "Idle"   } },
        { "Ready",  new[] { "Ready"  } },
        { "Run",    new[] { "Run"    } },
        { "Death",  new[] { "Death"  } },
        { "Jump",   new[] { "Jump"   } },
        { "Attack", new[] { "Slash", "Shot", "Jab" } },
    };

    [MenuItem(ProjectKMenu.Data + "비인간형 몬스터 라이브러리", priority = ProjectKMenu.DataPrio + 21)]
    public static void CreateAll()
    {
        Directory.CreateDirectory(OutputRoot);

        int made = 0;
        foreach (string name in Targets)
        {
            if (Create($"{SourceRoot}/{name}/SpriteSheet.png", $"{name}Library")) made++;
        }

        // ⚠ 색 변형은 원본 라이브러리 **뒤에** 굽는다 — 같은 규칙(CategoryMap)으로 굽는다
        int variants = 0;
        foreach (PaletteVariant v in Variants)
            if (BakeVariant(v)) variants++;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MonsterLibraryCreator] 완료 — {made}/{Targets.Length}종 + 색 변형 {variants}/{Variants.Length}. " +
                  $"경로: {OutputRoot}");
    }

    // ── 색 변형 (팔레트 교체) ────────────────────────────────
    //
    //  ■ 왜 굽나 (사용자 지시, 2026-09-11)
    //    진화체는 뿌리와 같은 그림이라 색조(SpriteRenderer.color)로 갈랐다. 그런데
    //    색조는 원본 색에 **곱해지는** 값이라 초록 슬라임을 밝은 노랑·선명한 파랑으로
    //    만들 수 없고, 애니메이터가 색을 되쓰는 문제(UnitAnimationSync.LateUpdate)도 탄다.
    //    시트 자체의 색을 바꿔 구우면 명암은 그대로, 색상만 원하는 대로 간다.
    //
    //  ■ 슬라임 시트는 **다섯 색**뿐이다 — 검은 외곽선 + 초록 넷(본체·밝음·그늘·하이라이트)
    //    그래서 색상 회전이 아니라 **정확히 네 색을 맞바꾼다.** 외곽선은 건드리지 않는다.
    //    ⚠ 벤더가 시트를 바꾸면 From 과 맞는 픽셀이 0 이 된다 — 그때는 에러로 알린다.
    //
    //  ■ 시트는 AssetDatabase.CopyAsset 으로 복사한다 — .meta 의 자르기 정보(스프라이트 이름·영역)가
    //    그대로 따라와 Idle_0 · Attack_2 같은 이름이 원본과 같다. 그다음 픽셀만 덮어쓴다.
    //    ⚠ 이미 있으면 복사하지 않는다 (GUID 가 바뀌면 라이브러리 참조가 끊긴다).

    const string VariantRoot = "Assets/_project/3.Textures/MonsterSheets";

    struct PaletteVariant
    {
        public string    Monster;   // 원본 폴더 이름 (Slug …)
        public string    Suffix;    // 결과 이름 — SlugLibrary_{Suffix}
        public Color32[] From;      // 원본 색 (본체 · 밝음 · 그늘 · 하이라이트)
        public Color32[] To;        // 바꿀 색 (같은 순서)

        /// <summary>
        /// 범위 색상 이동 — From/To 대신 쓴다 (늑대·멧돼지·트롤).
        /// ⚠ 그 시트들은 10~17색이라 한 색씩 맞바꾸기가 어렵다. 몸 색이 모인 색상·채도 범위만
        ///   골라 **색상만 옮기고 명암(밝기)은 유지**한다. 눈·이빨·옷처럼 범위 밖의 색은 그대로다.
        /// </summary>
        public HueShift? Shift;
    }

    struct HueShift
    {
        public float HueMin, HueMax;   // 대상 색상 범위 (도, 0~360)
        public float SatMin, SatMax;   // 대상 채도 범위 — 외곽선(검정)·흰 이빨처럼 채도 0 인 색을 빼는 문
        public float Hue;              // 옮길 색상 (도)
        public float SatMul, SatAdd;   // 채도 = s × Mul + Add
        public float ValMul;           // 밝기 = v × Mul
    }

    static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

    /// <summary>슬라임 원본 초록 넷 — 본체 · 밝음 · 그늘 · 하이라이트 (시트 분석 결과).</summary>
    static readonly Color32[] SlugGreen =
    {
        C( 95, 229,  74), C(127, 242, 108), C( 93, 169,  64), C(180, 242, 170),
    };

    /// <summary>
    /// 굽을 색 변형. 결과 라이브러리 이름은 {Monster}Library_{Suffix}.
    /// ⚠ 독 슬라임은 원본 초록 그대로다 — 원본 라이브러리(SlugLibrary)를 쓴다 (사용자 지시, 2026-09-11).
    /// </summary>
    static readonly PaletteVariant[] Variants =
    {
        // 기본 슬라임 · 슬라임 킹 — 노랑
        new PaletteVariant { Monster = "Slug", Suffix = "Yellow", From = SlugGreen,
            To = new[] { C(246, 204,  58), C(255, 228, 112), C(196, 148,  36), C(255, 246, 196) } },

        // 힐 슬라임 — 분홍
        new PaletteVariant { Monster = "Slug", Suffix = "Pink", From = SlugGreen,
            To = new[] { C(244, 108, 168), C(255, 150, 200), C(186,  70, 126), C(255, 212, 232) } },

        // 강철 슬라임 — 회청
        new PaletteVariant { Monster = "Slug", Suffix = "Steel", From = SlugGreen,
            To = new[] { C(122, 158, 206), C(160, 190, 232), C( 82, 108, 150), C(216, 230, 250) } },

        // ⚠ 슬라임 킹은 **노란 시트를 함께 쓴다** (사용자 확정, 2026-09-15)
        //   한때 자주(Royal)로 따로 구웠다. 슬라임의 왕은 노랑이라는 것이 확정이라
        //   변형을 걷어냈다 — 굽지 않는 변형을 남겨 두면 아무도 안 쓰는 시트가
        //   빌드에 실린다. 되살리려면 여기 한 줄만 다시 넣으면 된다.
        // ── 범위 색상 이동 (2026-09-11) — 뿌리 종족은 원본 그대로, 진화체만 굽는다 ──
        //   범위는 시트 분석으로 잡았다: 늑대 털 = 색상 ~200 · 채도 0.05~0.18 (회청)
        //   멧돼지 몸 = 색상 11~17 · 채도 0.5~0.87 · 트롤 피부 = 색상 20~21 · 채도 0.56~0.73.
        //   빨간 눈(4)·흰 이빨(채도 0.02)·트롤 파란 옷(209)·주황 눈(33)은 범위 밖이라 남는다.

        // 서리 늑대 — 회색 털을 얼음 청색으로 (채도를 끌어올린다)
        new PaletteVariant { Monster = "Wolf", Suffix = "Frost",
            Shift = new HueShift { HueMin = 190, HueMax = 215, SatMin = 0.03f, SatMax = 0.25f,
                                   Hue = 200, SatMul = 2.6f, SatAdd = 0.12f, ValMul = 1.08f } },

        // 화염 멧돼지 — 갈색을 밝은 불꽃 주황으로
        new PaletteVariant { Monster = "Hog", Suffix = "Flame",
            Shift = new HueShift { HueMin = 8, HueMax = 20, SatMin = 0.45f, SatMax = 1f,
                                   Hue = 32, SatMul = 1.05f, SatAdd = 0.05f, ValMul = 1.22f } },

        // 숲의 트롤 — 갈색 피부를 초록으로
        new PaletteVariant { Monster = "Troll", Suffix = "Forest",
            Shift = new HueShift { HueMin = 17, HueMax = 24, SatMin = 0.5f, SatMax = 0.8f,
                                   Hue = 100, SatMul = 0.85f, SatAdd = 0f, ValMul = 1f } },

        // ── 두 번째 1차 (사용자 지시, 2026-09-15) ────────────────
        //  같은 뿌리의 첫 1차와 **다른 색상 쪽**으로 민다 (서리=청 · 화염=주황 · 숲=초록).

        // 핏빛 늑대 — 회색 털을 검붉게
        new PaletteVariant { Monster = "Wolf", Suffix = "Blood",
            Shift = new HueShift { HueMin = 190, HueMax = 215, SatMin = 0.03f, SatMax = 0.25f,
                                   Hue = 8, SatMul = 2.4f, SatAdd = 0.2f, ValMul = 0.82f } },

        // 철갑 멧돼지 — 갈색을 쇳빛 청회색으로
        new PaletteVariant { Monster = "Hog", Suffix = "Iron",
            Shift = new HueShift { HueMin = 8, HueMax = 20, SatMin = 0.45f, SatMax = 1f,
                                   Hue = 210, SatMul = 0.3f, SatAdd = 0f, ValMul = 0.95f } },

        // 늪 트롤 — 갈색 피부를 탁한 청록으로
        new PaletteVariant { Monster = "Troll", Suffix = "Swamp",
            Shift = new HueShift { HueMin = 17, HueMax = 24, SatMin = 0.5f, SatMax = 0.8f,
                                   Hue = 165, SatMul = 0.55f, SatAdd = 0f, ValMul = 0.78f } },

        // ── 2차 업그레이드 (사용자 지시, 2026-09-15) ──────────────
        //  ⚠ 1차와 **반대쪽으로** 민다 — 한 계보에 뿌리·1차·2차 셋이 서므로
        //    1차가 이미 쓴 방향으로 더 밀면 둘이 같은 색으로 수렴한다.
        //    서리 늑대가 밝은 청색이면 알파는 **어둡게**, 화염 멧돼지가 밝은 주황이면
        //    워 보어는 **검붉게**, 숲의 트롤이 초록이면 고대 트롤은 **돌회색**이다.
        //  ⚠ 채도를 끌어내려 어둡게 잡는다 — 2차는 덩치가 가장 크므로
        //    밝고 진한 색까지 주면 화면에서 혼자 튄다. 크기가 이미 말하고 있다.

        // 알파 울프 — 회색 털을 검은 은빛으로 (채도를 죽이고 어둡게)
        new PaletteVariant { Monster = "Wolf", Suffix = "Alpha",
            Shift = new HueShift { HueMin = 190, HueMax = 215, SatMin = 0.03f, SatMax = 0.25f,
                                   Hue = 205, SatMul = 0.6f, SatAdd = 0f, ValMul = 0.62f } },

        // 워 보어 — 갈색을 검붉게 (화염 멧돼지의 밝은 주황과 정반대)
        new PaletteVariant { Monster = "Hog", Suffix = "War",
            Shift = new HueShift { HueMin = 8, HueMax = 20, SatMin = 0.45f, SatMax = 1f,
                                   Hue = 356, SatMul = 1.15f, SatAdd = 0.05f, ValMul = 0.68f } },

        // 고대 트롤 — 갈색 피부를 돌회색으로 (채도를 거의 걷어낸다)
        new PaletteVariant { Monster = "Troll", Suffix = "Ancient",
            Shift = new HueShift { HueMin = 17, HueMax = 24, SatMin = 0.5f, SatMax = 0.8f,
                                   Hue = 28, SatMul = 0.22f, SatAdd = 0f, ValMul = 0.88f } },
    };

    /// <summary>한 픽셀을 변형 규칙대로 바꾼다. 바꿨으면 true.</summary>
    static bool Recolor(in PaletteVariant v, ref Color32 p)
    {
        if (v.Shift is HueShift s)
        {
            Color.RGBToHSV(p, out float h, out float sat, out float val);
            float deg = h * 360f;

            if (deg < s.HueMin || deg > s.HueMax || sat < s.SatMin || sat > s.SatMax) return false;

            Color c = Color.HSVToRGB(s.Hue / 360f,
                                     Mathf.Clamp01(sat * s.SatMul + s.SatAdd),
                                     Mathf.Clamp01(val * s.ValMul));
            p = new Color32((byte)Mathf.RoundToInt(c.r * 255f), (byte)Mathf.RoundToInt(c.g * 255f),
                            (byte)Mathf.RoundToInt(c.b * 255f), p.a);
            return true;
        }

        for (int k = 0; k < v.From.Length; k++)
        {
            if (p.r != v.From[k].r || p.g != v.From[k].g || p.b != v.From[k].b) continue;

            p = new Color32(v.To[k].r, v.To[k].g, v.To[k].b, p.a);
            return true;
        }
        return false;
    }

    static bool BakeVariant(in PaletteVariant v)
    {
        string src = $"{SourceRoot}/{v.Monster}/SpriteSheet.png";
        string dir = $"{VariantRoot}/{v.Monster}";
        string dst = $"{dir}/SpriteSheet_{v.Suffix}.png";

        Directory.CreateDirectory(dir);

        // ① 자르기 정보째 복사 — 처음 한 번만
        if (!File.Exists(dst) && !AssetDatabase.CopyAsset(src, dst))
        {
            Debug.LogError($"[MonsterLibraryCreator] 시트를 복사하지 못했습니다: {src} → {dst}");
            return false;
        }

        // ② 픽셀만 바꿔 덮어쓴다 — 원본 파일을 바이트로 읽으니 임포트 설정(Read/Write)과 무관하다
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(src));

        Color32[] px = tex.GetPixels32();
        int swapped  = 0;

        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a == 0) continue;
            if (Recolor(v, ref px[i])) swapped++;
        }

        tex.SetPixels32(px);
        File.WriteAllBytes(dst, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        if (swapped == 0)
        {
            Debug.LogError($"[MonsterLibraryCreator] '{v.Monster}_{v.Suffix}' — 바꿀 색이 하나도 없습니다. " +
                           "벤더 시트의 색이 바뀌었는지 보고 From 을 다시 잴 것.");
            return false;
        }

        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);

        // ③ 원본과 같은 규칙으로 라이브러리를 굽는다
        return Create(dst, $"{v.Monster}Library_{v.Suffix}");
    }

    // ── 개별 생성 ────────────────────────────────────────────

    static bool Create(string sheetPath, string libraryName)
    {
        string monsterName = libraryName;

        // 시트 하나가 여러 스프라이트로 잘려 있다. 서브에셋을 전부 긁는다.
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                                        .OfType<Sprite>()
                                        .ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogError($"[MonsterLibraryCreator] 스프라이트를 찾지 못했습니다: {sheetPath}");
            return false;
        }

        var library = ScriptableObject.CreateInstance<SpriteLibraryAsset>();
        int added   = 0;

        foreach (Sprite sprite in sprites)
        {
            // 이름 규칙은 합성 캐릭터와 같다: "카테고리_라벨" (예: Attack_2)
            int underscore = sprite.name.LastIndexOf('_');
            if (underscore < 0)
            {
                Debug.LogWarning($"[MonsterLibraryCreator] 이름 규칙에 맞지 않아 건너뜁니다: {sprite.name}");
                continue;
            }

            string sourceCategory = sprite.name.Substring(0, underscore);
            string label          = sprite.name.Substring(underscore + 1);

            if (!CategoryMap.TryGetValue(sourceCategory, out string[] targets))
            {
                Debug.LogWarning($"[MonsterLibraryCreator] 매핑에 없는 카테고리라 건너뜁니다: {sourceCategory} ({monsterName})");
                continue;
            }

            foreach (string category in targets)
            {
                library.AddCategoryLabel(sprite, category, label);
                added++;
            }
        }

        string outPath = $"{OutputRoot}/{libraryName}.asset";

        // 이미 있으면 내용만 갈아끼운다 —
        // 에셋을 지웠다 다시 만들면 GUID 가 바뀌어 종족 SO 의 참조가 끊어진다.
        var existing = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(outPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(library, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(library);
        }
        else
        {
            AssetDatabase.CreateAsset(library, outPath);
        }

        Debug.Log($"[MonsterLibraryCreator] {monsterName} — 라벨 {added}개 등록");
        return true;
    }
}
