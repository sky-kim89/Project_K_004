using UnityEditor;
using UnityEngine;

// ============================================================
//  MonsterGearIconGenerator.cs  [Editor Only]
//  Tools > Project K > 아이콘·텍스처 > 몬스터 장비 아이콘
//  몬스터 장비 아이콘 8장(부위마다 하나)을 만든다.
//    → Assets/_project/3.Textures/Icons/Gear/gear_<Part>.png
//
//  ■ 왜 필요했나 (사용자 지적, 2026-09-07)
//    MonsterGearData.Icon 필드는 처음부터 있었는데 **채우는 코드가 없었다.**
//    그래서 도감 장비 탭과 몬스터 상세의 장비 칸이 전부 빈 그림이었다.
//    에러는 안 난다 — 그냥 아무것도 안 보인다.
//
//  ■ ⚠ 장비마다 그리지 않는다. **부위마다** 하나다
//    장비는 수십 개인데 부위는 여덟이고, 한 부위는 한 몬스터에 하나뿐이라
//    (MonsterGearRule) 화면에서 갈려야 하는 것은 "어느 자리에 끼우는가" 다.
//    등급은 이름과 스탯이 말한다 — 그림까지 등급별로 나누면 40장을 그려 놓고
//    실제로는 여덟 종류로만 읽힌다.
//
//  ■ ⚠ 아틀라스에 얹지 않는다 — SO 가 스프라이트를 직접 문다
//    MonsterGearData.Icon 은 Sprite 참조라 MonsterGearCreator 가 구울 때
//    꽂아 준다. 그래서 SpriteManagerCreator 에 폴더를 더할 필요가 없다.
//    (키 조회가 필요한 특성·유물과 다른 방식이다 — 그쪽은 무엇이 뜰지
//     런타임에 정해져서 미리 꽂아 둘 수가 없다)
//
//  ■ 인간형 다섯 · 비인간형 셋으로 **색이 갈린다**
//    끼울 수 있는 몸이 다르므로, 목록에서 잘못 고르는 일이 줄어든다.
//
//  ⚠ 부위를 늘리면 아래 표에도 넣고 다시 구울 것 (Verify 가 잡아 준다)
//  ⚠ 구운 뒤 '데이터 생성 > 몬스터 장비' 를 실행해야 SO 에 꽂힌다
// ============================================================

public static class MonsterGearIconGenerator
{
    public const string Dir = "Assets/_project/3.Textures/Icons/Gear";

    /// <summary>부위 → PNG 경로. MonsterGearCreator 가 이 함수로 찾아 꽂는다.</summary>
    public static string PathOf(MonsterGearPart part) => $"{Dir}/gear_{part}.png";

    [MenuItem(ProjectKMenu.Icon + "몬스터 장비 아이콘", priority = ProjectKMenu.IconPrio + 9)]
    public static void Generate()
    {
        IconGenerator.EnsureDir(Dir);

        foreach (Entry e in Table)
            IconGenerator.Save(64, 64, PathOf(e.Part), p => IconArt.Compose(p, e.Style));

        AssetDatabase.Refresh();
        IconGenerator.ApplySpriteImportSettings(Dir, 64);
        AssetDatabase.SaveAssets();

        Verify();

        Debug.Log($"[MonsterGearIconGenerator] 장비 아이콘 {Table.Length}장 생성 → {Dir}\n" +
                  "⚠ 이어서 '데이터 생성 > 몬스터 장비' 를 실행해야 SO 에 꽂힙니다.");
    }

    /// <summary>
    /// 표에 빠진 부위가 없는지 본다.
    ///
    /// ⚠ 빠지면 그 부위의 장비만 빈 그림이 된다 — 목록에서 한 칸만 비어 보여
    ///   원인을 짚기 어렵다.
    /// </summary>
    static void Verify()
    {
        foreach (MonsterGearPart part in System.Enum.GetValues(typeof(MonsterGearPart)))
        {
            bool found = false;
            foreach (Entry e in Table) if (e.Part == part) { found = true; break; }

            if (!found)
                Debug.LogError($"[MonsterGearIconGenerator] '{part}' 가 조합표에 없습니다 — " +
                               "그 부위의 장비가 전부 빈 그림이 됩니다.");
        }
    }

    readonly struct Entry
    {
        public readonly MonsterGearPart Part;
        public readonly IconArt.Style   Style;
        public Entry(MonsterGearPart p, IconArt.Style s) { Part = p; Style = s; }
    }

    static Entry E(MonsterGearPart p, IconArt.Glyph g, string accent,
                   IconArt.Bg bg, IconArt.Frame fr, IconArt.Badge bd = IconArt.Badge.None)
        => new Entry(p, new IconArt.Style(g, IconGenerator.Hex(accent), bg, fr, bd));

    // 인간형은 쇠붙이(강철·청동), 비인간형은 살아 있는 것(초록·자주)으로 갈랐다.
    const string Steel = "9FB4D6", Iron = "7E8CA8", Bronze = "C79438";
    const string Cloth = "C86A6A", Leaf  = "6FC46A", Amber  = "FFB347";
    const string Violet = "9B5CFF";

    static readonly Entry[] Table =
    {
        // ── 인간형 ───────────────────────────────────────────
        E(MonsterGearPart.Armor,  IconArt.Glyph.Fist,   Steel,  IconArt.Bg.Plate,    IconArt.Frame.Rivet),
        E(MonsterGearPart.Helmet, IconArt.Glyph.Crown,  Iron,   IconArt.Bg.Radial,   IconArt.Frame.Double),
        E(MonsterGearPart.Shield, IconArt.Glyph.Shield, Steel,  IconArt.Bg.Split,    IconArt.Frame.Round),
        E(MonsterGearPart.Cape,   IconArt.Glyph.Banner, Cloth,  IconArt.Bg.Diagonal, IconArt.Frame.Cut),
        E(MonsterGearPart.Back,   IconArt.Glyph.Arrows, Bronze, IconArt.Bg.Plate,    IconArt.Frame.Notch),

        // ── 비인간형 ─────────────────────────────────────────
        //   ⚠ 인간형과 색·테두리가 겹치지 않게 둔다 — 목록에서 끼울 수 없는
        //     장비를 고르는 일이 줄어든다.
        E(MonsterGearPart.Hide,   IconArt.Glyph.Aura,   Leaf,   IconArt.Bg.Halo,     IconArt.Frame.Round),
        E(MonsterGearPart.Bulk,   IconArt.Glyph.Pulse,  Amber,  IconArt.Bg.Burst,    IconArt.Frame.Rivet),
        E(MonsterGearPart.Charm,  IconArt.Glyph.Star,   Violet, IconArt.Bg.Halo,     IconArt.Frame.Double),
    };
}
