using UnityEditor;
using UnityEngine;

// ============================================================
//  SpeciesPassiveIconGenerator.cs  [Editor Only]
//  Tools > Project K > 아이콘·텍스처 > 종족 패시브 아이콘
//  종족 패시브 19종의 64×64 PNG 를 만든다.
//    → Assets/_project/3.Textures/Icons/SpeciesPassives/passive_<Enum>.png
//
//  ■ 왜 글자가 아니라 그림인가
//    종족 패시브는 "이 몬스터가 무엇을 하는 놈인가" 그 자체다. 카드 3택에서
//    세 장을 훑고 빌드를 정하는 근거인데, 이름을 글자로만 늘어놓으면
//    카드마다 서너 줄이 되어 아무도 읽지 않는다. 그림이면 한눈에 갈린다.
//    (설명은 칩에 올리거나 눌러서 본다 — SpeciesPassiveChipUI)
//
//  ■ 파일명이 enum 이름과 같아야 한다
//    SpeciesPassiveIconAssets 가 SpeciesPassive 이름으로 경로를 만든다.
//    패시브를 추가하면 SpeciesPassiveRule.All 과 아래 표에 **둘 다** 넣고
//    다시 구울 것 — 빠지면 그 패시브만 아이콘이 비어 사라진 것처럼 보인다.
//
//  ■ 더미다 — 진짜 그림이 오면 갈아 끼운다
//    조합형 아트 키트(IconArt)로 굽는다. 손그림이 준비되면 같은 경로·같은
//    파일명으로 덮으면 코드는 그대로 돌아간다.
//
//  ■ 19장이 서로 안 닮게 — 다섯 축을 전부 다르게 준다
//    글리프만 바꾸면 배경·테두리가 같아 멀리서 한 덩어리로 보인다.
//    IconArt 는 배경 6 × 테두리 5 × 글리프 28 × 뱃지 8 × 색의 조합이라
//    축을 흩뿌리면 작게 줄여도 갈린다. (SynergyIconGenerator 와 같은 방식)
//
//  ■ ⚠ 글리프는 **효과**를 가리키게 고른다 (시너지와 반대다)
//    시너지 아이콘은 '숲·언데드' 같은 이름을 대신하지만, 종족 패시브는
//    이름 자체가 효과의 별명("치유의 잔재")이라 그림이 효과를 말해야 한다.
// ============================================================

public static class SpeciesPassiveIconGenerator
{
    [MenuItem(ProjectKMenu.Icon + "종족 패시브 아이콘", priority = ProjectKMenu.IconPrio + 2)]
    public static void Generate()
    {
        string dir = SpeciesPassiveIconAssets.Dir.TrimEnd('/');
        IconGenerator.EnsureDir(dir);

        foreach (Entry e in Table)
            IconGenerator.Save(64, 64, SpeciesPassiveIconAssets.PathOf(e.Passive),
                               p => IconArt.Compose(p, e.Style));

        AssetDatabase.Refresh();
        IconGenerator.ApplySpriteImportSettings(dir, 64);
        AssetDatabase.SaveAssets();

        Verify();

        Debug.Log($"[SpeciesPassiveIconGenerator] 종족 패시브 아이콘 {Table.Length}장 생성 완료 → {dir}");
    }

    /// <summary>
    /// 표와 순서 정본이 어긋나지 않았는지 본다.
    ///
    /// ⚠ 이 검사가 없으면 실패가 조용하다
    ///   패시브를 새로 만들고 All 에만 넣으면 PNG 가 안 구워지고, 표에만
    ///   넣으면 런타임이 그 그림을 영영 못 찾는다. 둘 다 화면에서는
    ///   "그냥 아이콘이 없네" 로만 보여 원인을 짚을 수 없다.
    /// </summary>
    static void Verify()
    {
        foreach (SpeciesPassive p in SpeciesPassiveRule.All)
        {
            bool found = false;
            foreach (Entry e in Table) if (e.Passive == p) { found = true; break; }

            if (!found)
                Debug.LogError($"[SpeciesPassiveIconGenerator] '{p}' 가 조합표에 없습니다 — " +
                               "아이콘이 비어 화면에서 사라진 것처럼 보입니다.");
        }

        // ── 시너지와 닮지 않았는지 ──
        foreach (Entry e in Table)
        {
            if (e.Style.Fr != FamilyFrame)
                Debug.LogError($"[SpeciesPassiveIconGenerator] '{e.Passive}' 의 테두리가 " +
                               $"{FamilyFrame} 가 아닙니다 — 종족 패시브는 테두리 하나로 묶인다.");

            if (SynergyIconGenerator.Uses(e.Style.G, e.Style.Accent))
                Debug.LogError($"[SpeciesPassiveIconGenerator] '{e.Passive}' 가 시너지 아이콘과 " +
                               "같은 글리프·색입니다 — 둘이 한 화면에 뜨면 구분되지 않습니다.");
        }
    }

    readonly struct Entry
    {
        public readonly SpeciesPassive Passive;
        public readonly IconArt.Style  Style;
        public Entry(SpeciesPassive p, IconArt.Style style) { Passive = p; Style = style; }
    }

    /// <summary>
    /// 종족 패시브 <b>전체가 공유하는 테두리</b>. 이게 이 축의 표식이다.
    ///
    /// ⚠ 시너지 표에서 Rivet 은 '강철' 한 장뿐이고, 그건 방패 글리프다
    ///   그래서 방패만 피하면 테두리가 통째로 이쪽 것이 된다. 아이콘 하나하나를
    ///   비교하지 않아도 "리벳이면 패시브" 라는 규칙 하나로 두 줄이 갈린다.
    ///   ⚠ 여기를 바꾸면 시너지 표와 다시 대조해야 한다 (Verify 가 잡아 준다).
    /// </summary>
    const IconArt.Frame FamilyFrame = IconArt.Frame.Rivet;

    static Entry E(SpeciesPassive p, IconArt.Glyph g, string accent,
                   IconArt.Bg bg, IconArt.Badge bd = IconArt.Badge.None)
        => new Entry(p, new IconArt.Style(g, IconGenerator.Hex(accent), bg, FamilyFrame, bd));

    const string Green = "4ED96A", Lime = "9EE04A", Teal = "2FC5B5", Cyan = "3FC8FF";
    const string Sky   = "6FA8FF", Violet = "9B5CFF", Crimson = "FF3E62", Red = "FF5535";
    const string Orange = "FF9130", Amber = "FFB428", Gold = "FFD34A";
    const string Steel = "9FB4CC", Bronze = "C98A4B";

    // ── 조합표 ────────────────────────────────────────────────
    //   ⚠ 순서는 SpeciesPassiveRule.All 과 같게 유지한다
    //     굽는 순서 자체는 상관없지만, 나란히 두면 빠진 줄이 눈에 띈다.
    //
    //   ⚠ 시너지가 쓰는 글리프·색 조합을 피한다 (Verify 가 대조한다)
    //     뜻이 겹치는 짝이 많다 — 숲↔치유의 잔재, 역병↔역병, 야수↔무리 사냥,
    //     투지↔피의 갈망, 언데드↔재조립, 재생↔재생. 뜻만 보고 고르면
    //     반드시 같은 그림이 나온다. 겹치는 자리는 **글리프나 색을 비켜** 준다.
    static readonly Entry[] Table =
    {
        // ── 기본 종족 패시브 ─────────────────────────────────
        E(SpeciesPassive.SplitOnDeath,   IconArt.Glyph.Arrows,    Lime,    IconArt.Bg.Split,    IconArt.Badge.Plus),
        E(SpeciesPassive.Reassemble,     IconArt.Glyph.Skull,     Bronze,  IconArt.Bg.Halo,     IconArt.Badge.Plus),   // 언데드(Skull+Violet) 회피
        E(SpeciesPassive.Loot,           IconArt.Glyph.Coin,      Gold,    IconArt.Bg.Diagonal),
        E(SpeciesPassive.PlagueBurst,    IconArt.Glyph.Pulse,     Lime,    IconArt.Bg.Burst,    IconArt.Badge.Minus),  // 역병(Spiral) 회피
        E(SpeciesPassive.Bloodlust,      IconArt.Glyph.Fist,      Crimson, IconArt.Bg.Burst,    IconArt.Badge.Up),     // 투지(Sword) 회피
        E(SpeciesPassive.PackHunt,       IconArt.Glyph.Boot,      Amber,   IconArt.Bg.Diagonal, IconArt.Badge.Up),     // 야수(Boot+Orange) 회피
        E(SpeciesPassive.Sturdy,         IconArt.Glyph.Heart,     Bronze,  IconArt.Bg.Plate,    IconArt.Badge.Plus),
        E(SpeciesPassive.SoulDrain,      IconArt.Glyph.Drop,      Violet,  IconArt.Bg.Halo,     IconArt.Badge.Plus),
        E(SpeciesPassive.Regrow,         IconArt.Glyph.Hourglass, Teal,    IconArt.Bg.Radial,   IconArt.Badge.Clock),  // 재생(Cross+Teal) 회피

        // ── 업그레이드 패시브 ────────────────────────────────
        E(SpeciesPassive.HealOnDeath,    IconArt.Glyph.Cross,     Green,   IconArt.Bg.Burst,    IconArt.Badge.Plus),   // 숲(Aura+Green) 회피
        E(SpeciesPassive.PoisonOnHit,    IconArt.Glyph.Potion,    Lime,    IconArt.Bg.Split,    IconArt.Badge.Minus),
        E(SpeciesPassive.ThornOnHit,     IconArt.Glyph.Anvil,     Steel,   IconArt.Bg.Plate,    IconArt.Badge.Bolt),   // 강철(Shield+Steel) 회피
        E(SpeciesPassive.ChillOnHit,     IconArt.Glyph.Star,      Cyan,    IconArt.Bg.Radial,   IconArt.Badge.Down),
        E(SpeciesPassive.Bulwark,        IconArt.Glyph.Banner,    Sky,     IconArt.Bg.Plate,    IconArt.Badge.Plus),
        E(SpeciesPassive.RallyOnDeath,   IconArt.Glyph.Horn,      Amber,   IconArt.Bg.Burst,    IconArt.Badge.Up),
        E(SpeciesPassive.ExplodeOnDeath, IconArt.Glyph.Flame,     Red,     IconArt.Bg.Burst,    IconArt.Badge.Bolt),
        E(SpeciesPassive.Volley,         IconArt.Glyph.Bow,       Sky,     IconArt.Bg.Diagonal, IconArt.Badge.Star),
        E(SpeciesPassive.BurnOnAttack,   IconArt.Glyph.Spiral,    Orange,  IconArt.Bg.Radial,   IconArt.Badge.Percent),// 역병(Spiral+Lime) 회피
        E(SpeciesPassive.Swiftness,      IconArt.Glyph.Bolt,      Amber,   IconArt.Bg.Diagonal, IconArt.Badge.Up),     // 술법(Bolt+Cyan) 회피

        // ── 장비 — 특이 패시브 ───────────────────────────────
        //   시너지 조합(Skull+Violet · Aura+Green · Boot+Orange · Cross+Teal · Sword+Crimson ·
        //   Shield+Steel · Spiral+Lime · Bolt+Cyan)을 피한다
        E(SpeciesPassive.Bravado,        IconArt.Glyph.Crown,     Amber,   IconArt.Bg.Burst,    IconArt.Badge.Up),
        E(SpeciesPassive.Recoil,         IconArt.Glyph.Arrows,    Sky,     IconArt.Bg.Plate,    IconArt.Badge.Plus),
        E(SpeciesPassive.VitalStrike,    IconArt.Glyph.Eye,       Crimson, IconArt.Bg.Diagonal, IconArt.Badge.Bolt),
        E(SpeciesPassive.LoneWolf,       IconArt.Glyph.Fist,      Violet,  IconArt.Bg.Halo,     IconArt.Badge.Up),
        E(SpeciesPassive.Rampart,        IconArt.Glyph.Shield,    Bronze,  IconArt.Bg.Plate,    IconArt.Badge.Down),
        E(SpeciesPassive.Executioner,    IconArt.Glyph.Sword,     Red,     IconArt.Bg.Split,    IconArt.Badge.Percent),
        E(SpeciesPassive.Anchor,         IconArt.Glyph.Anvil,     Bronze,  IconArt.Bg.Plate,    IconArt.Badge.Down),
        E(SpeciesPassive.Photosynthesis, IconArt.Glyph.Aura,      Lime,    IconArt.Bg.Radial,   IconArt.Badge.Plus),
        E(SpeciesPassive.BurstBody,      IconArt.Glyph.Pulse,     Orange,  IconArt.Bg.Burst,    IconArt.Badge.Bolt),
        E(SpeciesPassive.Hunger,         IconArt.Glyph.Drop,      Crimson, IconArt.Bg.Split,    IconArt.Badge.Up),
        E(SpeciesPassive.Embers,         IconArt.Glyph.Flame,     Orange,  IconArt.Bg.Radial,   IconArt.Badge.Clock),
        E(SpeciesPassive.Vengeance,      IconArt.Glyph.Skull,     Crimson, IconArt.Bg.Burst,    IconArt.Badge.Up),

        // ── 장비 — 단계 패시브 (계열 = 글리프·색, 단계 = 배경·뱃지) ──
        //   I = Radial/없음 · II = Diagonal/Plus · III = Halo/Star
        T(SpeciesPassive.HpUp1,          IconArt.Glyph.Heart,     Green,  1),
        T(SpeciesPassive.HpUp2,          IconArt.Glyph.Heart,     Green,  2),
        T(SpeciesPassive.HpUp3,          IconArt.Glyph.Heart,     Green,  3),
        T(SpeciesPassive.AttackUp1,      IconArt.Glyph.Sword,     Orange, 1),
        T(SpeciesPassive.AttackUp2,      IconArt.Glyph.Sword,     Orange, 2),
        T(SpeciesPassive.AttackUp3,      IconArt.Glyph.Sword,     Orange, 3),
        T(SpeciesPassive.DefenseUp1,     IconArt.Glyph.Shield,    Sky,    1),
        T(SpeciesPassive.DefenseUp2,     IconArt.Glyph.Shield,    Sky,    2),
        T(SpeciesPassive.DefenseUp3,     IconArt.Glyph.Shield,    Sky,    3),
        T(SpeciesPassive.CritUp1,        IconArt.Glyph.Eye,       Gold,   1),
        T(SpeciesPassive.CritUp2,        IconArt.Glyph.Eye,       Gold,   2),
        T(SpeciesPassive.CritUp3,        IconArt.Glyph.Eye,       Gold,   3),
        T(SpeciesPassive.CritDmgUp1,     IconArt.Glyph.Star,      Red,    1),
        T(SpeciesPassive.CritDmgUp2,     IconArt.Glyph.Star,      Red,    2),
        T(SpeciesPassive.CritDmgUp3,     IconArt.Glyph.Star,      Red,    3),
        T(SpeciesPassive.CooldownUp1,    IconArt.Glyph.Hourglass, Cyan,   1),
        T(SpeciesPassive.CooldownUp2,    IconArt.Glyph.Hourglass, Cyan,   2),
        T(SpeciesPassive.CooldownUp3,    IconArt.Glyph.Hourglass, Cyan,   3),
        T(SpeciesPassive.PierceUp1,      IconArt.Glyph.Chain,     Violet, 1),
        T(SpeciesPassive.PierceUp2,      IconArt.Glyph.Chain,     Violet, 2),
        T(SpeciesPassive.PierceUp3,      IconArt.Glyph.Chain,     Violet, 3),
        T(SpeciesPassive.AttackSpeedUp1, IconArt.Glyph.Gear,      Amber,  1),
        T(SpeciesPassive.AttackSpeedUp2, IconArt.Glyph.Gear,      Amber,  2),
        T(SpeciesPassive.AttackSpeedUp3, IconArt.Glyph.Gear,      Amber,  3),

        // ── 각성 — 원래 것과 **같은 글리프**, 금빛 + 후광 + 별 ──
        //   원래 것을 알아보게 하되 한눈에 "더 좋은 판" 으로 읽히게.
        A(SpeciesPassive.GreatSplit,  IconArt.Glyph.Arrows),
        A(SpeciesPassive.Undying,     IconArt.Glyph.Skull),
        A(SpeciesPassive.GoldRush,    IconArt.Glyph.Coin),
        A(SpeciesPassive.Pandemic,    IconArt.Glyph.Pulse),
        A(SpeciesPassive.Berserker,   IconArt.Glyph.Fist),
        A(SpeciesPassive.Alpha,       IconArt.Glyph.Boot),
        A(SpeciesPassive.Colossus,    IconArt.Glyph.Heart),
        A(SpeciesPassive.Vampire,     IconArt.Glyph.Drop),
        A(SpeciesPassive.TrollBlood,  IconArt.Glyph.Hourglass),
        A(SpeciesPassive.LifeSeed,    IconArt.Glyph.Cross),
        A(SpeciesPassive.Venom,       IconArt.Glyph.Potion),
        A(SpeciesPassive.IronThorns,  IconArt.Glyph.Anvil),
        A(SpeciesPassive.Frostbite,   IconArt.Glyph.Star),
        A(SpeciesPassive.Fortress,    IconArt.Glyph.Banner),
        A(SpeciesPassive.WarDrum,     IconArt.Glyph.Horn),
        A(SpeciesPassive.Cataclysm,   IconArt.Glyph.Flame),
        A(SpeciesPassive.Barrage,     IconArt.Glyph.Bow),
        A(SpeciesPassive.Hellfire,    IconArt.Glyph.Spiral),
        A(SpeciesPassive.Gale,        IconArt.Glyph.Bolt),

        // ── 마나 패시브 (2026-09-12) — 시너지 조합(Spiral+Lime 등)을 피한다 ──
        E(SpeciesPassive.ManaResonance, IconArt.Glyph.Spiral, Violet, IconArt.Bg.Halo,  IconArt.Badge.Up),
        E(SpeciesPassive.ManaRelease,   IconArt.Glyph.Drop,   Sky,    IconArt.Bg.Burst, IconArt.Badge.Plus),
    };

    /// <summary>단계 패시브 한 장 — 계열은 글리프·색, 단계(1~3)는 배경·뱃지가 가른다.</summary>
    static Entry T(SpeciesPassive p, IconArt.Glyph g, string accent, int tier) => tier switch
    {
        1 => E(p, g, accent, IconArt.Bg.Radial),
        2 => E(p, g, accent, IconArt.Bg.Diagonal, IconArt.Badge.Plus),
        _ => E(p, g, accent, IconArt.Bg.Halo,     IconArt.Badge.Star),
    };

    /// <summary>각성판 — 금빛 · 후광 · 별.</summary>
    static Entry A(SpeciesPassive p, IconArt.Glyph g) => E(p, g, Gold, IconArt.Bg.Halo, IconArt.Badge.Star);
}
