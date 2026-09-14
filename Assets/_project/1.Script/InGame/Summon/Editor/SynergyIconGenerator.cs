using UnityEditor;
using UnityEngine;

// ============================================================
//  SynergyIconGenerator.cs  [Editor Only]
//  Tools > Project K > 아이콘·텍스처 > 시너지 아이콘
//  시너지 표식 8종의 64×64 PNG 를 만든다.
//    → Assets/_project/3.Textures/Icons/Synergies/synergy_<Tag>.png
//
//  ■ 파일명이 enum 이름과 같아야 한다
//    SynergyIconAssets 가 MonsterTag 이름으로 경로를 만든다.
//    표식을 추가하면 아래 표에도 한 줄 넣고 다시 구울 것 — 빠지면 그 시너지만
//    아이콘이 비어 화면에서 사라진 것처럼 보인다.
//
//  ■ 더미다 — 진짜 그림이 오면 갈아 끼운다
//    지금은 조합형 아트 키트(IconArt)로 굽는다. 시너지를 "이름 대신 그림으로"
//    부르려면 무엇이든 그림이 먼저 있어야 하고, 8장이 서로 구분만 되면
//    그 역할은 한다. 손그림이 준비되면 같은 경로·같은 파일명으로 덮으면
//    코드는 그대로 돌아간다.
//
//  ■ 여덟 장이 서로 안 닮게 — 다섯 축을 전부 다르게 준다
//    글리프만 바꾸면 배경·테두리가 같아 멀리서 한 덩어리로 보인다.
//    IconArt 는 배경 6 × 테두리 5 × 글리프 28 × 뱃지 8 × 색의 조합이라
//    축을 흩뿌리면 작게 줄여도 갈린다. (PassiveIconGenerator 와 같은 방식)
// ============================================================

public static class SynergyIconGenerator
{
    [MenuItem(ProjectKMenu.Icon + "시너지 아이콘", priority = ProjectKMenu.IconPrio + 1)]
    public static void Generate()
    {
        string dir = SynergyIconAssets.Dir.TrimEnd('/');
        IconGenerator.EnsureDir(dir);

        foreach (Entry e in Table)
            IconGenerator.Save(64, 64, SynergyIconAssets.PathOf(e.Tag),
                               p => IconArt.Compose(p, e.Style));

        AssetDatabase.Refresh();
        IconGenerator.ApplySpriteImportSettings(dir, 64);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SynergyIconGenerator] 시너지 아이콘 {Table.Length}장 생성 완료 → {dir}");
    }

    /// <summary>
    /// 이 글리프·색 조합을 시너지가 이미 쓰고 있는가.
    ///
    /// ⚠ 종족 패시브 쪽 검사(SpeciesPassiveIconGenerator.Verify)가 쓴다
    ///   두 축은 화면에서 나란히 뜨는데 그림이 닮으면 무엇이 시너지고
    ///   무엇이 패시브인지 갈리지 않는다. 뜻이 겹치는 짝이 많아서
    ///   (숲↔치유 · 역병↔역병 · 야수↔무리사냥) 가만두면 반드시 닮는다.
    /// </summary>
    public static bool Uses(IconArt.Glyph glyph, Color32 accent)
    {
        foreach (Entry e in Table)
        {
            if (e.Style.G != glyph) continue;

            Color32 a = e.Style.Accent;
            if (a.r == accent.r && a.g == accent.g && a.b == accent.b) return true;
        }

        return false;
    }

    readonly struct Entry
    {
        public readonly MonsterTag    Tag;
        public readonly IconArt.Style Style;
        public Entry(MonsterTag tag, IconArt.Style style) { Tag = tag; Style = style; }
    }

    static Entry E(MonsterTag tag, IconArt.Glyph g, string accent,
                   IconArt.Bg bg, IconArt.Frame fr, IconArt.Badge bd = IconArt.Badge.None)
        => new Entry(tag, new IconArt.Style(g, IconGenerator.Hex(accent), bg, fr, bd));

    const string Green = "4ED96A", Lime = "9EE04A", Teal = "2FC5B5", Cyan = "3FC8FF";
    const string Violet = "9B5CFF", Crimson = "FF3E62", Orange = "FF9130", Steel = "9FB4CC";

    // ── 조합표 ────────────────────────────────────────────────
    //   글리프는 **시너지 이름**을 가리키게 고른다 (효과가 아니라).
    //   화면에서 이 그림이 이름을 대신하므로, 효과 쪽에 맞추면
    //   "체력 시너지" 는 알아도 "숲" 인 줄은 모르게 된다.
    static readonly Entry[] Table =
    {
        E(MonsterTag.Undead,   IconArt.Glyph.Skull,  Violet,  IconArt.Bg.Halo,     IconArt.Frame.Notch),
        E(MonsterTag.Forest,   IconArt.Glyph.Aura,   Green,   IconArt.Bg.Radial,   IconArt.Frame.Round, IconArt.Badge.Plus),
        E(MonsterTag.Beast,    IconArt.Glyph.Boot,   Orange,  IconArt.Bg.Diagonal, IconArt.Frame.Cut,   IconArt.Badge.Bolt),
        E(MonsterTag.Regrowth, IconArt.Glyph.Cross,  Teal,    IconArt.Bg.Halo,     IconArt.Frame.Round, IconArt.Badge.Plus),
        E(MonsterTag.Ferocity, IconArt.Glyph.Sword,  Crimson, IconArt.Bg.Burst,    IconArt.Frame.Notch, IconArt.Badge.Up),
        E(MonsterTag.Steel,    IconArt.Glyph.Shield, Steel,   IconArt.Bg.Plate,    IconArt.Frame.Rivet),
        E(MonsterTag.Plague,   IconArt.Glyph.Spiral, Lime,    IconArt.Bg.Split,    IconArt.Frame.Notch, IconArt.Badge.Minus),
        E(MonsterTag.Sorcery,  IconArt.Glyph.Bolt,   Cyan,    IconArt.Bg.Burst,    IconArt.Frame.Double, IconArt.Badge.Star),
    };
}
