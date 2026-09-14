using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
//  RunPerkIconGenerator.cs  [Editor Only]
//  Tools > Project K > 아이콘·텍스처 > 특성 아이콘
//  런 특성 30종의 64×64 PNG 를 만든다.
//    → Assets/_project/3.Textures/Icons/RunPerks/perk_<Enum>.png
//
//  ■ 왜 필요했나 (사용자 지적, 2026-09-07)
//    특성 3택이 **글자 목록**이었다. "확장 편성 / 카드 칸 +2" 가 줄줄이
//    쌓인 화면은 이 게임의 다른 선택 화면(카드 3택)과 격이 다르고,
//    무엇보다 **한눈에 갈리지 않는다.** 셋 중 하나를 고르는 자리인데
//    세 줄을 다 읽어야 한다.
//    그리고 이미 가진 특성을 볼 곳도 없었다 — 아이콘이 있어야 상단에
//    줄로 세울 수 있다 (RunPerkBarUI).
//
//  ■ 다섯 축을 흩뿌린다 — 30장이 서로 안 닮게
//    글리프만 바꾸면 배경·테두리가 같아 멀리서 한 덩어리로 보인다.
//    (SpeciesPassiveIconGenerator · SynergyIconGenerator 와 같은 방식)
//
//  ■ ⚠ 테두리로 축을 묶지 않는다 — 여기만 예외다
//    종족 패시브는 Rivet 하나로 묶여 있다. 그건 카드 3택에서 **시너지 줄과
//    나란히** 뜨기 때문이다. 특성은 그 둘과 같은 화면에 서지 않는다
//    (특성 3택 · 상단 보유 줄뿐). 대신 30장을 갈라 보이게 하는 쪽이 값이 크다.
//
//  ■ ⚠ 뜻이 겹치는 짝을 조심할 것
//    특성에는 종족 패시브·시너지와 뜻이 같은 것이 여럿 있다
//    (증원↔물량, 파쇄↔가시…). 같은 화면에 서지 않으므로 강제하지는 않지만,
//    색이라도 비켜 두면 나중에 한 화면에 모여도 갈린다.
//
//  ■ 더미다 — 진짜 그림이 오면 같은 경로·같은 파일명으로 덮으면 된다.
//
//  ⚠ 특성을 추가하면 아래 표에도 넣고 다시 구울 것
//    빠지면 Verify 가 잡아 준다. 그냥 두면 그 특성만 빈 그림이 된다.
// ============================================================

public static class RunPerkIconGenerator
{
    [MenuItem(ProjectKMenu.Icon + "특성 아이콘", priority = ProjectKMenu.IconPrio + 4)]
    public static void Generate()
    {
        string dir = RunPerkIconAssets.Dir.TrimEnd('/');
        IconGenerator.EnsureDir(dir);

        foreach (Entry e in Table)
            IconGenerator.Save(64, 64, RunPerkIconAssets.PathOf(e.Perk),
                               p => IconArt.Compose(p, e.Style));

        AssetDatabase.Refresh();
        IconGenerator.ApplySpriteImportSettings(dir, 64);
        AssetDatabase.SaveAssets();

        Verify();

        Debug.Log($"[RunPerkIconGenerator] 특성 아이콘 {Table.Length}장 생성 완료 → {dir}\n" +
                  "⚠ 아틀라스에 넣으려면 데이터 생성 > SpriteManager + 아틀라스 도 실행하세요.");
    }

    /// <summary>
    /// 표에 빠진 특성이 없는지 본다.
    ///
    /// ⚠ 이 검사가 없으면 실패가 조용하다
    ///   특성을 새로 만들고 표에 안 넣으면 PNG 가 안 구워지고, 화면에는
    ///   "그 특성만 그림이 없네" 로만 보여 원인을 짚을 수 없다.
    /// </summary>
    static void Verify()
    {
        foreach (RunPerk perk in RunPerkIconAssets.All())
        {
            bool found = false;
            foreach (Entry e in Table) if (e.Perk == perk) { found = true; break; }

            if (!found)
                Debug.LogError($"[RunPerkIconGenerator] '{perk}' 가 조합표에 없습니다 — " +
                               "아이콘이 비어 화면에서 사라진 것처럼 보입니다.");
        }

        // 같은 조합이 둘 있으면 두 특성이 똑같이 생긴다.
        var seen = new HashSet<(IconArt.Glyph, IconArt.Bg, uint)>();

        foreach (Entry e in Table)
        {
            Color32 c   = e.Style.Accent;
            uint    key = (uint)((c.r << 16) | (c.g << 8) | c.b);

            if (!seen.Add((e.Style.G, e.Style.Back, key)))
                Debug.LogError($"[RunPerkIconGenerator] '{e.Perk}' 가 다른 특성과 " +
                               "글리프·배경·색이 모두 같습니다 — 화면에서 구분되지 않습니다.");
        }
    }

    readonly struct Entry
    {
        public readonly RunPerk       Perk;
        public readonly IconArt.Style Style;
        public Entry(RunPerk p, IconArt.Style style) { Perk = p; Style = style; }
    }

    static Entry E(RunPerk p, IconArt.Glyph g, string accent,
                   IconArt.Bg bg, IconArt.Frame fr, IconArt.Badge bd = IconArt.Badge.None)
        => new Entry(p, new IconArt.Style(g, IconGenerator.Hex(accent), bg, fr, bd));

    const string Violet = "9B5CFF", Indigo = "6C6CFF", Sky = "6FA8FF", Cyan = "3FC8FF";
    const string Teal   = "2FC5B5", Green  = "4ED96A", Lime = "9EE04A", Gold = "FFD34A";
    const string Amber  = "FFB428", Orange = "FF9130", Red  = "FF5535", Crimson = "FF3E62";
    const string Pink   = "FF6FD0", Steel  = "9FB4CC", Bronze = "C98A4B", White = "E8EEFF";

    // ── 조합표 ────────────────────────────────────────────────
    //   묶음마다 색을 몰아 준다 — 마나는 보라·파랑, 물량은 초록, 전투는 붉은색.
    //   같은 묶음 안에서는 글리프·배경·테두리로 가른다.
    static readonly Entry[] Table =
    {
        // ── 소환 · 편성 ──────────────────────────────────────
        E(RunPerk.FreeFirstSummon,  IconArt.Glyph.Star,     Gold,    IconArt.Bg.Burst,    IconArt.Frame.Double, IconArt.Badge.Star),
        E(RunPerk.RapidDrain,       IconArt.Glyph.Boot,     Cyan,    IconArt.Bg.Diagonal, IconArt.Frame.Cut,    IconArt.Badge.Up),
        E(RunPerk.ExtraSlots,       IconArt.Glyph.Banner,   Sky,     IconArt.Bg.Plate,    IconArt.Frame.Round,  IconArt.Badge.Plus),

        // ── 마나 ─────────────────────────────────────────────
        E(RunPerk.CheapAffinity,    IconArt.Glyph.Drop,     Violet,  IconArt.Bg.Halo,     IconArt.Frame.Round,  IconArt.Badge.Minus),
        E(RunPerk.CheapAll,         IconArt.Glyph.Drop,     Indigo,  IconArt.Bg.Radial,   IconArt.Frame.Cut,    IconArt.Badge.Minus),
        E(RunPerk.DeepVessel,       IconArt.Glyph.Potion,   Violet,  IconArt.Bg.Radial,   IconArt.Frame.Double, IconArt.Badge.Plus),
        E(RunPerk.Meditation,       IconArt.Glyph.Aura,     Indigo,  IconArt.Bg.Halo,     IconArt.Frame.Notch,  IconArt.Badge.Plus),
        E(RunPerk.Hoard,            IconArt.Glyph.Coin,     Violet,  IconArt.Bg.Plate,    IconArt.Frame.Rivet,  IconArt.Badge.Up),
        E(RunPerk.Scales,           IconArt.Glyph.Scales,   Sky,     IconArt.Bg.Split,    IconArt.Frame.Round),
        E(RunPerk.ManaSurge,        IconArt.Glyph.Bolt,     Violet,  IconArt.Bg.Burst,    IconArt.Frame.Notch,  IconArt.Badge.Bolt),

        // ── 대기열 · 물량 ────────────────────────────────────
        E(RunPerk.Reinforce,        IconArt.Glyph.Soldiers, Green,   IconArt.Bg.Plate,    IconArt.Frame.Round,  IconArt.Badge.Plus),
        E(RunPerk.AffinityReinforce,IconArt.Glyph.Soldiers, Lime,    IconArt.Bg.Halo,     IconArt.Frame.Double, IconArt.Badge.Plus),
        E(RunPerk.Vanguard,         IconArt.Glyph.Horn,     Amber,   IconArt.Bg.Diagonal, IconArt.Frame.Cut,    IconArt.Badge.Up),
        E(RunPerk.Reinforcements,   IconArt.Glyph.Banner,   Green,   IconArt.Bg.Burst,    IconArt.Frame.Notch,  IconArt.Badge.Plus),

        // ── 소환력 ───────────────────────────────────────────
        E(RunPerk.Resonance,        IconArt.Glyph.Spiral,   Teal,    IconArt.Bg.Radial,   IconArt.Frame.Round,  IconArt.Badge.Up),
        E(RunPerk.Extremity,        IconArt.Glyph.Pulse,    Pink,    IconArt.Bg.Split,    IconArt.Frame.Cut),
        E(RunPerk.Weighted,         IconArt.Glyph.Anvil,    Steel,   IconArt.Bg.Plate,    IconArt.Frame.Rivet,  IconArt.Badge.Up),
        E(RunPerk.DeepChannel,      IconArt.Glyph.Eye,      Violet,  IconArt.Bg.Halo,     IconArt.Frame.Notch,  IconArt.Badge.Star),
        E(RunPerk.AffinityExpand,   IconArt.Glyph.Chain,    Teal,    IconArt.Bg.Diagonal, IconArt.Frame.Double, IconArt.Badge.Plus),

        // ── 라인 ─────────────────────────────────────────────
        E(RunPerk.LateBloom,        IconArt.Glyph.Hourglass,Bronze,  IconArt.Bg.Radial,   IconArt.Frame.Cut,    IconArt.Badge.Clock),
        E(RunPerk.FewButElite,      IconArt.Glyph.Crown,    Gold,    IconArt.Bg.Halo,     IconArt.Frame.Double, IconArt.Badge.Up),
        E(RunPerk.Horde,            IconArt.Glyph.Soldiers, Orange,  IconArt.Bg.Burst,    IconArt.Frame.Notch,  IconArt.Badge.Up),
        E(RunPerk.CenterPush,       IconArt.Glyph.Arrows,   Red,     IconArt.Bg.Split,    IconArt.Frame.Cut,    IconArt.Badge.Up),
        E(RunPerk.Flank,            IconArt.Glyph.Arrows,   Amber,   IconArt.Bg.Diagonal, IconArt.Frame.Round,  IconArt.Badge.Up),

        // ── 전투 ─────────────────────────────────────────────
        E(RunPerk.Rend,             IconArt.Glyph.Sword,    Crimson, IconArt.Bg.Split,    IconArt.Frame.Rivet,  IconArt.Badge.Percent),
        E(RunPerk.Menagerie,        IconArt.Glyph.Fist,     Lime,    IconArt.Bg.Plate,    IconArt.Frame.Notch,  IconArt.Badge.Up),

        // ── 보상 · 성장 ──────────────────────────────────────
        E(RunPerk.Appraisal,        IconArt.Glyph.Eye,      Gold,    IconArt.Bg.Diagonal, IconArt.Frame.Round,  IconArt.Badge.Plus),
        E(RunPerk.QuickStudy,       IconArt.Glyph.Gear,     White,   IconArt.Bg.Radial,   IconArt.Frame.Notch,  IconArt.Badge.Up),
        E(RunPerk.Focus,            IconArt.Glyph.Flame,    Orange,  IconArt.Bg.Halo,     IconArt.Frame.Rivet,  IconArt.Badge.Bolt),
        E(RunPerk.Homecoming,       IconArt.Glyph.Cross,    Teal,    IconArt.Bg.Burst,    IconArt.Frame.Double, IconArt.Badge.Plus),

        // ── 대가를 치르는 특성 (2026-09-11) — 전부 무언가를 내주고 받는다 ──
        E(RunPerk.BloodPact,        IconArt.Glyph.Heart,    Crimson, IconArt.Bg.Split,    IconArt.Frame.Notch,  IconArt.Badge.Minus),
        E(RunPerk.OverloadFrenzy,   IconArt.Glyph.Bolt,     Red,     IconArt.Bg.Diagonal, IconArt.Frame.Cut,    IconArt.Badge.Up),
        E(RunPerk.SealedSlot,       IconArt.Glyph.Chain,    Steel,   IconArt.Bg.Plate,    IconArt.Frame.Rivet,  IconArt.Badge.Minus),
        E(RunPerk.Patience,         IconArt.Glyph.Hourglass,Teal,    IconArt.Bg.Halo,     IconArt.Frame.Round,  IconArt.Badge.Up),
        E(RunPerk.GlassKeep,        IconArt.Glyph.Shield,   Cyan,    IconArt.Bg.Split,    IconArt.Frame.Double, IconArt.Badge.Minus),
        E(RunPerk.CursedGold,       IconArt.Glyph.Coin,     Crimson, IconArt.Bg.Burst,    IconArt.Frame.Notch,  IconArt.Badge.Plus),
        E(RunPerk.TwinLane,         IconArt.Glyph.Arrows,   Sky,     IconArt.Bg.Plate,    IconArt.Frame.Double, IconArt.Badge.Plus),
        E(RunPerk.SingleWell,       IconArt.Glyph.Drop,     Teal,    IconArt.Bg.Radial,   IconArt.Frame.Rivet,  IconArt.Badge.Up),
        E(RunPerk.Ambush,           IconArt.Glyph.Bow,      Amber,   IconArt.Bg.Halo,     IconArt.Frame.Cut,    IconArt.Badge.Clock),

        // ── 마나를 힘으로 (2026-09-12) ──
        E(RunPerk.Overflow,         IconArt.Glyph.Potion,   Violet,  IconArt.Bg.Burst,    IconArt.Frame.Double, IconArt.Badge.Up),
        E(RunPerk.Crystallize,      IconArt.Glyph.Star,     Cyan,    IconArt.Bg.Plate,    IconArt.Frame.Rivet,  IconArt.Badge.Plus),
        E(RunPerk.TrophyMana,       IconArt.Glyph.Crown,    Violet,  IconArt.Bg.Radial,   IconArt.Frame.Notch,  IconArt.Badge.Plus),
    };
}
