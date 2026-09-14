using UnityEditor;
using UnityEngine;

// ============================================================
//  DifficultyIconGenerator.cs  [Editor Only]
//  Tools > Project K > 아이콘·텍스처 > 난이도 아이콘
//
//  난이도 등급 5종 + 디버프 4종 아이콘을 48×48 PNG 로 그린다.
//
//  ■ 등급 아이콘은 '같은 형태, 다른 온도' 로 간다
//    다섯 개가 서로 다른 그림이면 어느 쪽이 더 높은 등급인지 안 읽힌다.
//    방패 실루엣을 공통으로 두고 테두리 색만 회색→청록→주황→적색→보라로
//    달군다. 색 온도만으로 순서가 읽히는 게 목적이다.
//    등급 표시용 눈금(칼자국)도 개수로 단계를 알려 준다.
//
//  ■ 디버프 아이콘은 각각 다른 그림이어야 한다
//    등급과 달리 디버프는 '종류' 라서 서로 구분되는 게 우선이다.
//
//  출력: Assets/_project/3.Textures/Icons/Difficulty/
// ============================================================

public static class DifficultyIconGenerator
{
    const string DIR = "Assets/_project/3.Textures/Icons/Difficulty";

    // 등급별 강조색 — 회색에서 시작해 점점 달아오른다
    static readonly (DifficultyTier tier, string edge, string bgA, string bgB)[] TierStyle =
    {
        (DifficultyTier.Easy,  "8A93A6", "141A26", "222B3D"),  // 차분한 강철
        (DifficultyTier.Normal,   "3FC2A8", "07201C", "0E3A32"),  // 청록 — 아직 견딜 만하다
        (DifficultyTier.Hard, "FFA033", "241203", "48240A"),  // 주황 — 경고
        (DifficultyTier.Hell,   "FF4422", "2A0603", "540E06"),  // 적색 — 초열
        (DifficultyTier.Inferno,       "B457FF", "1A0630", "330C55"),  // 보라 — 무간
    };

    [MenuItem(ProjectKMenu.Icon + "난이도 아이콘", priority = ProjectKMenu.IconPrio + 14)]
    public static void Generate()
    {
        EnsureDir();

        foreach (var (tier, edge, bgA, bgB) in TierStyle)
            IconGenerator.Save(48, 48, $"{DIR}/{tier.IconKey()}.png",
                               p => DrawTier(p, (int)tier, edge, bgA, bgB));

        IconGenerator.Save(48, 48, $"{DIR}/{DifficultyDebuff.Ferocity.IconKey()}.png",  DrawFerocity);
        IconGenerator.Save(48, 48, $"{DIR}/{DifficultyDebuff.Horde.IconKey()}.png",     DrawHorde);
        IconGenerator.Save(48, 48, $"{DIR}/{DifficultyDebuff.Awakening.IconKey()}.png", DrawAwakening);
        IconGenerator.Save(48, 48, $"{DIR}/{DifficultyDebuff.Frenzy.IconKey()}.png",    DrawFrenzy);

        DeleteStale();
        AssetDatabase.Refresh();

        // ⚠ 이걸 빼면 아이콘이 화면에 안 나온다
        //   PNG 기본 임포트는 spriteMode = Multiple 이라 서브 스프라이트가 생기고,
        //   LoadAssetAtPath<Sprite>() 가 null 을 돌려준다. 아틀라스도 못 묶는다.
        //   Single 로 강제해야 SpriteManager.Get() 이 찾을 수 있다.
        //   (EventIllustrationGenerator 도 같은 이유로 같은 처리를 한다)
        IconGenerator.ApplySpriteImportSettings(DIR, 48);

        AssetDatabase.SaveAssets();
        Debug.Log($"[DifficultyIconGenerator] 등급 5 + 디버프 4 = 9개 생성 → {DIR}");
    }

    /// <summary>
    /// 지금 쓰지 않는 아이콘 파일을 지운다.
    ///
    /// ⚠ 이름을 바꾸면 옛 파일이 그대로 남는다
    ///   등급 이름을 출정·혈전…에서 쉬움·보통…으로 바꿨을 때
    ///   difficulty_expedition.png 같은 파일이 남아 아틀라스에 쓰레기로 들어갔다.
    ///   현재 enum 이 요구하는 이름만 남기고 나머지는 정리한다.
    /// </summary>
    static void DeleteStale()
    {
        var keep = new System.Collections.Generic.HashSet<string>();
        foreach (DifficultyTier t in System.Enum.GetValues(typeof(DifficultyTier)))
            keep.Add(t.IconKey());
        foreach (DifficultyDebuff d in System.Enum.GetValues(typeof(DifficultyDebuff)))
            if (d != DifficultyDebuff.None) keep.Add(d.IconKey());

        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { DIR }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (keep.Contains(name)) continue;

            AssetDatabase.DeleteAsset(path);
            Debug.Log($"[DifficultyIconGenerator] 옛 아이콘 삭제: {name}.png");
        }
    }

    static void EnsureDir()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_project/3.Textures/Icons"))
            AssetDatabase.CreateFolder("Assets/_project/3.Textures", "Icons");
        if (!AssetDatabase.IsValidFolder(DIR))
            AssetDatabase.CreateFolder("Assets/_project/3.Textures/Icons", "Difficulty");
    }

    // ══════════════════════════════════════════════════════════
    //  등급 — 공통 방패 + 색 온도 + 눈금 개수
    // ══════════════════════════════════════════════════════════

    static void DrawTier(IconGenerator.P p, int level, string edge, string bgA, string bgB)
    {
        Color32 ec = IconGenerator.Hex(edge);
        p.BgGradient(IconGenerator.Hex(bgA), IconGenerator.Hex(bgB));
        p.RoundedBorder(8, 2, ec);

        // 방패 실루엣 — 위는 각지고 아래는 뾰족하게
        var body = new Color32((byte)(ec.r / 3), (byte)(ec.g / 3), (byte)(ec.b / 3), 235);
        p.FillRRect(14, 10, 20, 16, 3, body);
        p.FillTri(14, 26, 34, 26, 24, 40, body);

        // 방패 테두리
        p.DrawLine(14, 10, 34, 10, ec, 2);
        p.DrawLine(14, 10, 14, 26, ec, 2);
        p.DrawLine(34, 10, 34, 26, ec, 2);
        p.DrawLine(14, 26, 24, 40, ec, 2);
        p.DrawLine(34, 26, 24, 40, ec, 2);

        // 단계 눈금 — 방패 위를 가로지르는 칼자국. 개수 = 등급
        // 출정(0단계)은 자국이 없어 '아직 아무 상처도 없는 방패' 가 된다.
        var slash = IconArt.Lighten(ec, 0.45f);
        for (int i = 0; i < level; i++)
        {
            int y = 14 + i * 6;
            p.DrawLine(11, y + 4, 37, y - 2, slash, 2);
        }

        // 최고 등급만 안쪽 발광 — 마지막이라는 신호
        if (level >= 4)
            p.FillCircleAlpha(24, 22, 13, new Color32(ec.r, ec.g, ec.b, 60));
    }

    // ══════════════════════════════════════════════════════════
    //  디버프 — 종류가 구분되는 게 우선
    // ══════════════════════════════════════════════════════════

    // 광포 — 위로 치솟는 화살 + 부풀어 오른 심장
    static void DrawFerocity(IconGenerator.P p)
    {
        p.BgGradient(IconGenerator.Hex("2A0805"), IconGenerator.Hex("55130B"));
        p.RoundedBorder(8, 2, IconGenerator.Hex("FF5533"));

        var core = new Color32(255, 90, 50, 235);
        p.FillCircleAlpha(24, 27, 14, new Color32(255, 70, 30, 55));
        p.FillCircle(19, 26, 7, core);          // 심장 두 덩이
        p.FillCircle(29, 26, 7, core);
        p.FillTri(11, 28, 37, 28, 24, 41, core);

        // 치솟는 화살
        var up = new Color32(255, 200, 120, 240);
        p.DrawLine(24, 22, 24, 7, up, 3);
        p.DrawLine(24, 7, 18, 14, up, 3);
        p.DrawLine(24, 7, 30, 14, up, 3);
    }

    // 물량 — 겹쳐 밀려오는 실루엣 무리
    static void DrawHorde(IconGenerator.P p)
    {
        p.BgGradient(IconGenerator.Hex("120E20"), IconGenerator.Hex("241B3D"));
        p.RoundedBorder(8, 2, IconGenerator.Hex("9A7BFF"));

        // 뒤쪽 무리 — 흐리게, 여러 개
        var far = new Color32(120, 100, 190, 150);
        for (int i = 0; i < 5; i++)
        {
            int x = 8 + i * 8;
            p.FillCircle(x, 16, 3, far);
            p.FillRRect(x - 3, 19, 6, 8, 2, far);
        }

        // 앞쪽 셋 — 진하게
        var near = new Color32(190, 165, 255, 245);
        for (int i = 0; i < 3; i++)
        {
            int x = 13 + i * 11;
            p.FillCircle(x, 27, 4, near);
            p.FillRRect(x - 4, 31, 8, 10, 2, near);
        }
    }

    // 각성 — 눈을 뜬 왕관 (우두머리 강화)
    static void DrawAwakening(IconGenerator.P p)
    {
        p.BgGradient(IconGenerator.Hex("07202A"), IconGenerator.Hex("0D3D4F"));
        p.RoundedBorder(8, 2, IconGenerator.Hex("35D8F0"));

        var gold = new Color32(90, 225, 250, 240);

        // 왕관
        p.FillRRect(11, 26, 26, 6, 2, gold);
        p.FillTri(11, 26, 19, 26, 15,  12, gold);
        p.FillTri(19, 26, 29, 26, 24,   8, gold);
        p.FillTri(29, 26, 37, 26, 33,  12, gold);

        // 뜬 눈 — 각성의 순간
        p.FillEllipse(24, 35, 10, 5, new Color32(230, 250, 255, 245));
        p.FillCircle(24, 35, 3, new Color32(20, 60, 80, 250));
        p.FillCircleAlpha(24, 35, 9, new Color32(90, 225, 250, 70));
    }

    // 폭주 — 뿔 달린 우두머리 + 끊어진 사슬 (제약이 풀려 새 공격을 쏟는다)
    //
    //  ⚠ 각성(왕관+눈)과 확실히 달라야 한다
    //    둘 다 '우두머리 강화' 라 그림이 비슷하면 어느 게 어느 건지 안 읽힌다.
    //    각성은 '깨어남'(정적), 폭주는 '풀려남'(동적)으로 갈랐다.
    static void DrawFrenzy(IconGenerator.P p)
    {
        p.BgGradient(IconGenerator.Hex("2A0614"), IconGenerator.Hex("520C22"));
        p.RoundedBorder(8, 2, IconGenerator.Hex("FF3D6E"));

        // 사방으로 터지는 기운 — 억눌린 것이 풀린 순간
        var burst = new Color32(255, 90, 130, 120);
        p.DrawLine(24, 26,  6, 12, burst, 2);
        p.DrawLine(24, 26, 42, 12, burst, 2);
        p.DrawLine(24, 26,  4, 30, burst, 2);
        p.DrawLine(24, 26, 44, 30, burst, 2);
        p.FillCircleAlpha(24, 26, 16, new Color32(255, 60, 110, 55));

        // 뿔 — 우두머리 표식
        var horn = new Color32(255, 210, 220, 245);
        p.FillTri(14, 24,  9,  4, 20, 18, horn);
        p.FillTri(34, 24, 39,  4, 28, 18, horn);

        // 머리
        var head = new Color32(240, 175, 190, 245);
        p.FillRRect(16, 20, 16, 16, 4, head);
        var eye = new Color32(70, 6, 24, 250);
        p.FillRRect(19, 26, 4, 4, 1, eye);
        p.FillRRect(25, 26, 4, 4, 1, eye);

        // 끊어진 사슬 고리 — 제약이 풀렸다
        var chain = new Color32(255, 200, 150, 235);
        // DrawCircle 은 (cx, cy, r, thick, color) 순서다 — DrawLine 과 다르다
        p.DrawCircle(11, 40, 4, 2, chain);
        p.DrawCircle(37, 40, 4, 2, chain);
        p.DrawLine(17, 40, 21, 40, chain, 2);
        p.DrawLine(27, 40, 31, 40, chain, 2);
    }
}
