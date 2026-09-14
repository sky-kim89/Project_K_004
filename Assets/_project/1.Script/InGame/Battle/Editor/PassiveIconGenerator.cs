using System;
using UnityEditor;
using UnityEngine;
using P = IconGenerator.P;

// ============================================================
//  PassiveIconGenerator.cs
//  Tools > Project K > 아이콘·텍스처 > 패시브 아이콘
//  PassiveSkillType 40종의 48×48 PNG 를 만든다.
//    → Assets/_project/3.Textures/Icons/Passives/passive_<Type>.png
//  파일명이 enum 이름과 같아야 GameAssetCreator 가 SO 에 자동 연결한다.
//
//  ■ "다 비슷해 보이는" 문제를 어떻게 피했나
//    아이콘 하나 = 배경 6종 × 테두리 5종 × 글리프 28종 × 뱃지 8종 × 색.
//    같은 글리프를 써야 하는 스킬(방패 계열 등)도 배경·테두리·뱃지가 달라
//    한눈에 구분된다. 조합표는 파일 하단 Table 참조.
//
//  좌표계: 좌상단 (0,0), y 증가 = 아래. 아이콘 중심 (24,24).
// ============================================================

public static class PassiveIconGenerator
{
    const string PASSIVE_PATH = "Assets/_project/3.Textures/Icons/Passives";

    [MenuItem(ProjectKMenu.Icon + "패시브 아이콘", priority = ProjectKMenu.IconPrio + 7)]
    public static void GeneratePassiveIcons()
    {
        IconGenerator.EnsureDir(PASSIVE_PATH);

        foreach (var e in Table)
            IconGenerator.Save(48, 48, $"{PASSIVE_PATH}/passive_{e.Name}.png", p => IconArt.Compose(p, e.Style));

        AssetDatabase.Refresh();
        IconGenerator.ApplySpriteImportSettings(PASSIVE_PATH, 48);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PassiveIconGenerator] 패시브 아이콘 {Table.Length}장 생성 완료 → {PASSIVE_PATH}");
    }

    readonly struct Entry
    {
        public readonly string        Name;
        public readonly IconArt.Style Style;
        public Entry(string name, IconArt.Style style) { Name = name; Style = style; }
    }

    static Entry E(string name, IconArt.Glyph g, string accent,
                   IconArt.Bg bg, IconArt.Frame fr, IconArt.Badge bd = IconArt.Badge.None)
        => new Entry(name, new IconArt.Style(g, IconGenerator.Hex(accent), bg, fr, bd));

    // 색 — 계열이 겹치지 않게 넓게 흩뿌린다
    const string Green = "4ED96A", Lime = "9EE04A", Teal = "2FC5B5", Cyan = "3FC8FF";
    const string Sky   = "6FA8FF", Blue = "3D7BF5", Violet = "9B5CFF", Purple = "C25CFF";
    const string Crimson = "FF3E62", Red = "FF5535", Orange = "FF9130", Amber = "FFB428";
    const string Gold  = "FFD34A", Steel = "9FB4CC", Bronze = "C98A4B", Rose = "FF7FA8";

    // ── 조합표 ────────────────────────────────────────────────
    static readonly Entry[] Table =
    {
        // 병사 강화
        E("ExtraSoldiers",            IconArt.Glyph.Soldiers, Green,   IconArt.Bg.Radial,   IconArt.Frame.Round,  IconArt.Badge.Plus),
        E("SoldierCombatBoost",       IconArt.Glyph.Soldiers, Orange,  IconArt.Bg.Burst,    IconArt.Frame.Rivet,  IconArt.Badge.Up),
        E("SoldierHorde",             IconArt.Glyph.Soldiers, Lime,    IconArt.Bg.Diagonal, IconArt.Frame.Cut,    IconArt.Badge.Percent),
        E("VanguardAura",             IconArt.Glyph.Shield,   Teal,    IconArt.Bg.Halo,     IconArt.Frame.Round,  IconArt.Badge.Up),

        // 교환
        E("WeakGeneralStrongSoldier", IconArt.Glyph.Scales,   Orange,  IconArt.Bg.Split,    IconArt.Frame.Double, IconArt.Badge.None),
        E("StrongGeneralWeakSoldier", IconArt.Glyph.Scales,   Gold,    IconArt.Bg.Plate,    IconArt.Frame.Double, IconArt.Badge.None),
        E("WeakGeneralMoreSoldiers",  IconArt.Glyph.Scales,   Green,   IconArt.Bg.Split,    IconArt.Frame.Cut,    IconArt.Badge.Plus),
        E("BerserkerPact",            IconArt.Glyph.Flame,    Red,     IconArt.Bg.Burst,    IconArt.Frame.Notch,  IconArt.Badge.Up),

        // 제너럴 강화
        E("GeneralCombatBoost",       IconArt.Glyph.Sword,    Gold,    IconArt.Bg.Radial,   IconArt.Frame.Round,  IconArt.Badge.Up),
        E("TitanGeneral",             IconArt.Glyph.Fist,     Bronze,  IconArt.Bg.Plate,    IconArt.Frame.Rivet,  IconArt.Badge.Up),
        E("CommanderFury",            IconArt.Glyph.Crown,    Crimson, IconArt.Bg.Burst,    IconArt.Frame.Double, IconArt.Badge.Star),

        // 시너지
        E("SoldierEmpowerGeneral",    IconArt.Glyph.Aura,     Teal,    IconArt.Bg.Halo,     IconArt.Frame.Round,  IconArt.Badge.Up),
        E("UnityStrength",            IconArt.Glyph.Aura,     Blue,    IconArt.Bg.Radial,   IconArt.Frame.Cut,    IconArt.Badge.Plus),
        E("SoldierDeathEmpower",      IconArt.Glyph.Skull,    Purple,  IconArt.Bg.Diagonal, IconArt.Frame.Notch,  IconArt.Badge.Up),
        E("SacrificeRitual",          IconArt.Glyph.Drop,     Crimson, IconArt.Bg.Halo,     IconArt.Frame.Double, IconArt.Badge.Minus),

        // 조건부
        E("BloodPact",                IconArt.Glyph.Drop,     Red,     IconArt.Bg.Burst,    IconArt.Frame.Round,  IconArt.Badge.Up),
        E("IronWill",                 IconArt.Glyph.Shield,   Steel,   IconArt.Bg.Plate,    IconArt.Frame.Rivet,  IconArt.Badge.Star),
        E("LastStand",                IconArt.Glyph.Banner,   Gold,    IconArt.Bg.Diagonal, IconArt.Frame.Notch,  IconArt.Badge.Star),

        // OnAttack
        E("VampiricStrike",           IconArt.Glyph.Drop,     Purple,  IconArt.Bg.Radial,   IconArt.Frame.Cut,    IconArt.Badge.Plus),
        E("StrengthStack",            IconArt.Glyph.Anvil,    Orange,  IconArt.Bg.Plate,    IconArt.Frame.Round,  IconArt.Badge.Up),
        E("SoldierMorale",            IconArt.Glyph.Horn,     Lime,    IconArt.Bg.Burst,    IconArt.Frame.Cut,    IconArt.Badge.Clock),

        // OnHit
        E("DefenseShield",            IconArt.Glyph.Shield,   Blue,    IconArt.Bg.Halo,     IconArt.Frame.Double, IconArt.Badge.Clock),
        E("QuickRecovery",            IconArt.Glyph.Cross,    Green,   IconArt.Bg.Radial,   IconArt.Frame.Round,  IconArt.Badge.Plus),
        E("CounterStrike",            IconArt.Glyph.Arrows,   Crimson, IconArt.Bg.Diagonal, IconArt.Frame.Rivet,  IconArt.Badge.Up),

        // OnEnemyKill
        E("KillMomentum",             IconArt.Glyph.Boot,     Cyan,    IconArt.Bg.Diagonal, IconArt.Frame.Round,  IconArt.Badge.Up),
        E("KillEmpower",              IconArt.Glyph.Sword,    Red,     IconArt.Bg.Burst,    IconArt.Frame.Cut,    IconArt.Badge.Up),
        E("KillHeal",                 IconArt.Glyph.Heart,    Green,   IconArt.Bg.Halo,     IconArt.Frame.Round,  IconArt.Badge.Plus),
        E("SoldierVigor",             IconArt.Glyph.Horn,     Gold,    IconArt.Bg.Radial,   IconArt.Frame.Double, IconArt.Badge.Up),

        // OnSoldierDeath
        E("SacrificeAbsorb",          IconArt.Glyph.Skull,    Teal,    IconArt.Bg.Halo,     IconArt.Frame.Cut,    IconArt.Badge.Plus),

        // OnSkillUse
        E("SkillAdrenaline",          IconArt.Glyph.Bolt,     Amber,   IconArt.Bg.Burst,    IconArt.Frame.Round,  IconArt.Badge.Up),
        E("SkillInstinct",            IconArt.Glyph.Eye,      Violet,  IconArt.Bg.Halo,     IconArt.Frame.Double, IconArt.Badge.Star),
        E("SkillRally",               IconArt.Glyph.Banner,   Lime,    IconArt.Bg.Burst,    IconArt.Frame.Rivet,  IconArt.Badge.Up),

        // OnBattleStart
        E("GoldenPower",              IconArt.Glyph.Coin,     Gold,    IconArt.Bg.Plate,    IconArt.Frame.Double, IconArt.Badge.Up),
        E("SwiftAssault",             IconArt.Glyph.Boot,     Sky,     IconArt.Bg.Diagonal, IconArt.Frame.Cut,    IconArt.Badge.Bolt),
        E("SteelBody",                IconArt.Glyph.Anvil,    Steel,   IconArt.Bg.Plate,    IconArt.Frame.Notch,  IconArt.Badge.Up),
        E("ShieldEdge",               IconArt.Glyph.Shield,   Orange,  IconArt.Bg.Plate,    IconArt.Frame.Cut,    IconArt.Badge.Bolt),
        E("FocusedFire",              IconArt.Glyph.Eye,      Red,     IconArt.Bg.Burst,    IconArt.Frame.Notch,  IconArt.Badge.Percent),

        // 즉시
        E("WideRange",                IconArt.Glyph.Bow,      Cyan,    IconArt.Bg.Radial,   IconArt.Frame.Round,  IconArt.Badge.Plus),

        // OnEnemyKill (추가)
        E("LootHunter",               IconArt.Glyph.Coin,     Rose,    IconArt.Bg.Diagonal, IconArt.Frame.Rivet,  IconArt.Badge.Plus),
        E("Slaughterer",              IconArt.Glyph.Skull,    Crimson, IconArt.Bg.Burst,    IconArt.Frame.Notch,  IconArt.Badge.Up),
    };
}
