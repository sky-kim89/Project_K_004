using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
//  MonsterIconGenerator.cs  [Editor Only]
//  종족 아이콘 9장 + 카테고리 아이콘 3장을 PNG 로 굽고, SO 에 물려 준다.
//
//  ■ 초상화와 다른 물건이다
//    카드 바·보상 화면의 큰 그림은 MonsterPortraitProvider 가 실제 유닛을
//    렌더해 만든다(장비·색까지 반영된다). 여기서 만드는 것은 **작은 식별 표식**
//    이다 — 도감 목록·강화 이벤트·특성 줄처럼 초상화를 띄우기엔 좁은 자리에 쓴다.
//
//  ■ 업그레이드는 뿌리 아이콘을 같이 쓴다
//    힐/독/강철 슬라임이 전부 슬라임 아이콘이다. 계보를 한눈에 묶어 주는 게
//    이 아이콘의 일이고, 개체 구분은 초상화가 이미 하고 있다.
//    ⚠ 업그레이드마다 그림을 따로 만들면 22장이 되고, 그중 절반은
//      작은 크기에서 서로 구분되지 않는다.
//
//  ■ 그림은 도형 조합이다
//    IconGenerator.P 의 픽셀 캔버스를 그대로 쓴다. 손으로 그린 그림이 아니라
//    "원·삼각형 몇 개" 라서, 96px 에서 실루엣만 읽히면 충분하다.
//
//  사용: Tools > Project K > 아이콘·텍스처 > 종족 아이콘
//  ⚠ '데이터 생성 > 몬스터 도감' 을 먼저 돌려야 SO 에 물릴 수 있다.
// ============================================================

public static class MonsterIconGenerator
{
    const string OutputRoot = "Assets/_project/3.Textures/Icons/Species";
    const int    Size       = 96;

    /// <summary>계보 뿌리 종족 ID → 아이콘 파일명.</summary>
    static readonly (string id, string file)[] Species =
    {
        ("slime",    "icon_species_slime"),
        ("skeleton", "icon_species_skeleton"),
        ("goblin",   "icon_species_goblin"),
        ("zombie",   "icon_species_zombie"),
        ("wolf",     "icon_species_wolf"),
        ("orc",      "icon_species_orc"),
        ("hog",      "icon_species_hog"),
        ("lich",     "icon_species_lich"),
        ("troll",    "icon_species_troll"),
    };

    /// <summary>카테고리 아이콘 — 종족이 아닌 축.</summary>
    static readonly string[] Categories =
    {
        "icon_cat_summoner",   // 소환사 — 지팡이
        "icon_cat_skill",      // 스킬 카드 — 룬
        "icon_cat_perk",       // 특성 — 보석
    };

    [MenuItem(ProjectKMenu.Icon + "종족 아이콘", priority = ProjectKMenu.IconPrio)]
    public static void GenerateAll()
    {
        DrawSlime   ($"{OutputRoot}/icon_species_slime.png");
        DrawSkeleton($"{OutputRoot}/icon_species_skeleton.png");
        DrawGoblin  ($"{OutputRoot}/icon_species_goblin.png");
        DrawZombie  ($"{OutputRoot}/icon_species_zombie.png");
        DrawWolf    ($"{OutputRoot}/icon_species_wolf.png");
        DrawOrc     ($"{OutputRoot}/icon_species_orc.png");
        DrawHog     ($"{OutputRoot}/icon_species_hog.png");
        DrawLich    ($"{OutputRoot}/icon_species_lich.png");
        DrawTroll   ($"{OutputRoot}/icon_species_troll.png");

        DrawSummoner($"{OutputRoot}/icon_cat_summoner.png");
        DrawSkill   ($"{OutputRoot}/icon_cat_skill.png");
        DrawPerk    ($"{OutputRoot}/icon_cat_perk.png");

        AssetDatabase.Refresh();

        foreach (var (_, file) in Species) Configure($"{OutputRoot}/{file}.png");
        foreach (string file in Categories) Configure($"{OutputRoot}/{file}.png");

        LinkToSpecies();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MonsterIconGenerator] 완료 — {Species.Length + Categories.Length}장. " +
                  $"경로: {OutputRoot}");
    }

    // ── SO 연결 ──────────────────────────────────────────────

    /// <summary>
    /// 모든 종족 SO 에 **계보 뿌리의 아이콘**을 물린다.
    ///
    /// ⚠ **LineageIcon 에만** 쓴다 — 초상화 자리에는 절대 넣지 않는다 (2026-08-28)
    ///   예전에는 MonsterSpeciesData.Icon 에 채웠는데, 그 칸이 "합성 초상화를
    ///   대신할 그림" 이라 카드 바·대기열·보상 화면의 초상화가 전부 이 납작한
    ///   아이콘으로 바뀌었다. 그 우회로는 아예 없앴고 Icon 필드도 제거했다.
    ///   여기서 만드는 것은 좁은 칸에 쓰는 **식별 표식**이지 초상화가 아니다.
    /// </summary>
    static void LinkToSpecies()
    {
        var iconById = new Dictionary<string, Sprite>(Species.Length);

        foreach (var (id, file) in Species)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{OutputRoot}/{file}.png");
            if (sprite != null) iconById[id] = sprite;
        }

        int linked = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:MonsterSpeciesData"))
        {
            var so = AssetDatabase.LoadAssetAtPath<MonsterSpeciesData>(
                         AssetDatabase.GUIDToAssetPath(guid));
            if (so == null) continue;

            string rootId = so.RootSpecies.Id;
            if (!iconById.TryGetValue(rootId, out Sprite sprite)) continue;

            so.LineageIcon = sprite;
            EditorUtility.SetDirty(so);
            linked++;
        }

        Debug.Log($"[MonsterIconGenerator] 종족 SO {linked}개에 계보 아이콘을 물렸습니다.");
    }

    // ── 종족 그림 ────────────────────────────────────────────
    //
    //  전부 같은 틀이다: 배경 그라데이션 → 테두리 → 실루엣.
    //  96px 에서 **실루엣만** 읽히면 된다 — 세부를 넣을수록 뭉갠다.

    static IconGenerator.P Canvas(string bgDark, string bgMid, string rim)
    {
        var p = new IconGenerator.P(Size, Size);
        p.BgGradient(Hex(bgDark), Hex(bgMid));
        p.RoundedBorder(10, 3, Hex(rim));
        return p;
    }

    /// <summary>슬라임 — 바닥이 넓은 젤리 덩어리 + 하이라이트.</summary>
    static void DrawSlime(string path)
    {
        var p = Canvas("041405", "126022", "3FCF62");

        p.FillEllipse(48, 40, 30, 24, Hex("46D96B"));
        p.FillEllipse(48, 32, 30, 14, Hex("2FA84E"));   // 바닥 그림자
        p.FillEllipse(38, 52, 8, 6, Hex("BFF7CD"));     // 하이라이트
        p.FillCircle(40, 44, 3, Hex("0C2A12"));         // 눈
        p.FillCircle(56, 44, 3, Hex("0C2A12"));

        p.Save(path);
    }

    /// <summary>스켈레톤 — 해골. 눈구멍 두 개가 전부다.</summary>
    static void DrawSkeleton(string path)
    {
        var p = Canvas("0A0A0C", "3A3A42", "C8C8D0");

        p.FillCircle(48, 54, 24, Hex("E4E4EA"));
        p.FillRect(36, 24, 24, 14, Hex("E4E4EA"));      // 턱
        p.FillCircle(40, 56, 6, Hex("101014"));         // 눈구멍
        p.FillCircle(56, 56, 6, Hex("101014"));
        p.FillRect(45, 40, 6, 6, Hex("101014"));        // 코
        p.DrawLine(40, 26, 40, 36, Hex("101014"), 2);   // 이빨 틈
        p.DrawLine(48, 26, 48, 36, Hex("101014"), 2);
        p.DrawLine(56, 26, 56, 36, Hex("101014"), 2);

        p.Save(path);
    }

    /// <summary>고블린 — 뾰족한 귀가 정체성이다.</summary>
    static void DrawGoblin(string path)
    {
        var p = Canvas("0A1404", "3E6E14", "8CD63C");

        p.FillTri(18, 68, 30, 44, 34, 72, Hex("7EC63A"));   // 왼쪽 귀
        p.FillTri(78, 68, 66, 44, 62, 72, Hex("7EC63A"));   // 오른쪽 귀
        p.FillEllipse(48, 48, 20, 22, Hex("8CD63C"));
        p.FillCircle(41, 54, 4, Hex("1A1005"));
        p.FillCircle(55, 54, 4, Hex("1A1005"));
        p.DrawLine(40, 36, 56, 36, Hex("1A1005"), 2);       // 히죽 웃는 입
        p.FillTri(43, 36, 46, 30, 49, 36, Hex("F0F0E0"));   // 이빨

        p.Save(path);
    }

    /// <summary>좀비 — 기울어진 머리와 엇갈린 눈.</summary>
    static void DrawZombie(string path)
    {
        var p = Canvas("0A1008", "3A5424", "7FA84C");

        p.FillEllipse(46, 50, 22, 24, Hex("7FA84C"));
        p.FillEllipse(60, 62, 7, 6, Hex("5C7F32"));         // 썩은 자국
        p.FillCircle(39, 56, 5, Hex("E8E8D0"));             // 흐린 눈
        p.FillCircle(39, 56, 2, Hex("101008"));
        p.DrawLine(51, 60, 59, 54, Hex("101008"), 3);       // 감긴 눈
        p.DrawLine(36, 36, 58, 34, Hex("101008"), 3);       // 벌어진 입
        p.DrawLine(46, 74, 52, 66, Hex("3A5424"), 3);       // 꿰맨 자국

        p.Save(path);
    }

    /// <summary>늑대 — 삼각 귀 둘 + 뾰족한 주둥이.</summary>
    static void DrawWolf(string path)
    {
        var p = Canvas("080A10", "343A4A", "8A93A8");

        p.FillTri(28, 78, 24, 56, 42, 62, Hex("8A93A8"));   // 귀
        p.FillTri(68, 78, 72, 56, 54, 62, Hex("8A93A8"));
        p.FillEllipse(48, 50, 20, 20, Hex("9AA3B8"));
        p.FillTri(34, 40, 62, 40, 48, 20, Hex("8A93A8"));   // 주둥이
        p.FillCircle(48, 26, 4, Hex("14161C"));             // 코
        p.FillCircle(40, 54, 4, Hex("FFCC44"));             // 노란 눈
        p.FillCircle(56, 54, 4, Hex("FFCC44"));

        p.Save(path);
    }

    /// <summary>오크 — 각진 턱 + 위로 솟은 엄니 둘.</summary>
    static void DrawOrc(string path)
    {
        var p = Canvas("0A1208", "2E5A24", "6BB04C");

        p.FillRRect(26, 26, 44, 48, 8, Hex("5F9E42"));
        p.FillCircle(39, 56, 4, Hex("14200C"));
        p.FillCircle(57, 56, 4, Hex("14200C"));
        p.DrawLine(36, 38, 60, 38, Hex("14200C"), 3);
        p.FillTri(38, 38, 42, 38, 40, 50, Hex("F2F2E2"));   // 엄니
        p.FillTri(54, 38, 58, 38, 56, 50, Hex("F2F2E2"));
        p.DrawLine(32, 68, 44, 64, Hex("14200C"), 2);       // 찌푸린 눈썹
        p.DrawLine(64, 68, 52, 64, Hex("14200C"), 2);

        p.Save(path);
    }

    /// <summary>멧돼지 — 큰 코와 콧구멍이 전부다.</summary>
    static void DrawHog(string path)
    {
        var p = Canvas("120A04", "5A3418", "B0713A");

        p.FillTri(26, 76, 30, 58, 42, 66, Hex("8A5628"));   // 귀
        p.FillTri(70, 76, 66, 58, 54, 66, Hex("8A5628"));
        p.FillEllipse(48, 48, 24, 20, Hex("9C6430"));
        p.FillEllipse(48, 38, 14, 10, Hex("C98A50"));       // 주둥이
        p.FillCircle(43, 38, 3, Hex("2A1608"));             // 콧구멍
        p.FillCircle(53, 38, 3, Hex("2A1608"));
        p.FillTri(32, 34, 37, 34, 30, 48, Hex("F0EAD8"));   // 엄니
        p.FillTri(64, 34, 59, 34, 66, 48, Hex("F0EAD8"));
        p.FillCircle(39, 58, 3, Hex("2A1608"));
        p.FillCircle(57, 58, 3, Hex("2A1608"));

        p.Save(path);
    }

    /// <summary>리치 — 후드 그림자 안에서 빛나는 눈 둘.</summary>
    static void DrawLich(string path)
    {
        var p = Canvas("0C0618", "3A1C68", "9A5CE0");

        p.FillTri(48, 82, 22, 24, 74, 24, Hex("5A2E9E"));   // 후드
        p.FillEllipse(48, 46, 15, 18, Hex("120A20"));       // 그림자 속 얼굴
        p.FillCircle(42, 50, 4, Hex("62E8FF"));             // 빛나는 눈
        p.FillCircle(54, 50, 4, Hex("62E8FF"));
        p.FillCircle(42, 50, 2, Hex("E8FBFF"));
        p.FillCircle(54, 50, 2, Hex("E8FBFF"));
        p.DrawLine(22, 24, 74, 24, Hex("7A44C0"), 3);       // 옷깃

        p.Save(path);
    }

    /// <summary>트롤 — 큰 머리, 작은 눈, 아래로 솟은 이빨.</summary>
    static void DrawTroll(string path)
    {
        var p = Canvas("04120E", "1C5C4A", "46B894");

        p.FillRRect(20, 22, 56, 52, 14, Hex("3C9E80"));
        p.FillEllipse(48, 34, 18, 10, Hex("54B896"));       // 턱
        p.FillCircle(38, 56, 3, Hex("0A1E18"));             // 작은 눈
        p.FillCircle(58, 56, 3, Hex("0A1E18"));
        p.DrawLine(38, 40, 58, 40, Hex("0A1E18"), 3);
        p.FillTri(40, 40, 44, 40, 42, 30, Hex("EDF5EE"));   // 아랫니
        p.FillTri(52, 40, 56, 40, 54, 30, Hex("EDF5EE"));
        p.FillEllipse(30, 66, 6, 5, Hex("2C8468"));         // 혹

        p.Save(path);
    }

    // ── 카테고리 그림 ────────────────────────────────────────

    /// <summary>소환사 — 지팡이와 보석.</summary>
    static void DrawSummoner(string path)
    {
        var p = Canvas("14061A", "5A1C6E", "C060E0");

        p.DrawLine(40, 16, 54, 62, Hex("7A4A2A"), 5);       // 자루
        p.FillCircleGrad(58, 70, 14, Hex("FFFFFF"), Hex("D080FF"), Hex("6A20A0"));
        p.DrawCircle(58, 70, 14, 2, Hex("E8B0FF"));
        p.FillCircle(58, 70, 4, Hex("FFFFFF"));

        p.Save(path);
    }

    /// <summary>스킬 카드 — 마름모 룬.</summary>
    static void DrawSkill(string path)
    {
        var p = Canvas("04141A", "12566E", "3CC8E0");

        p.FillTri(48, 82, 20, 48, 76, 48, Hex("2AA8C8"));
        p.FillTri(48, 14, 20, 48, 76, 48, Hex("1E88A8"));
        p.DrawLine(48, 30, 48, 66, Hex("D8F6FF"), 3);       // 룬 획
        p.DrawLine(36, 48, 60, 48, Hex("D8F6FF"), 3);
        p.FillCircle(48, 48, 5, Hex("EAFBFF"));

        p.Save(path);
    }

    /// <summary>특성 — 육각 보석.</summary>
    static void DrawPerk(string path)
    {
        var p = Canvas("1A1204", "6E5210", "E0B83C");

        p.FillTri(48, 80, 22, 58, 74, 58, Hex("F0CC55"));
        p.FillRect(22, 38, 52, 20, Hex("E0B83C"));
        p.FillTri(48, 18, 22, 38, 74, 38, Hex("C89A28"));
        p.DrawLine(22, 58, 74, 58, Hex("FFF0B0"), 2);
        p.DrawLine(48, 80, 48, 18, Hex("FFF0B0"), 2);

        p.Save(path);
    }

    // ── 임포트 설정 ──────────────────────────────────────────

    /// <summary>
    /// 스프라이트로 읽히게 만든다.
    ///
    /// ⚠ Point 필터 · 무압축이 기본이다
    ///   96px 짜리 도형 그림이라 Bilinear 로 뭉개면 이빨·눈이 사라지고,
    ///   압축하면 색 경계에 얼룩이 생긴다.
    /// </summary>
    static void Configure(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[MonsterIconGenerator] 임포터를 찾지 못했습니다: {path}");
            return;
        }

        importer.textureType        = TextureImporterType.Sprite;
        importer.spriteImportMode   = SpriteImportMode.Single;
        importer.filterMode         = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled      = false;
        importer.alphaIsTransparency = true;

        importer.SaveAndReimport();
    }

    static Color32 Hex(string hex)
        => new(System.Convert.ToByte(hex.Substring(0, 2), 16),
               System.Convert.ToByte(hex.Substring(2, 2), 16),
               System.Convert.ToByte(hex.Substring(4, 2), 16),
               255);
}
