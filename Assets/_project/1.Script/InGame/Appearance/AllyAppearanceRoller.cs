using System.Collections.Generic;
using Unity.Mathematics;

// ============================================================
//  AllyAppearanceRoller.cs
//  아군 유닛 외형을 unitName 시드 기반으로 결정적(Deterministic)으로 생성.
//
//  같은 unitName → 항상 같은 외형 (FNV-1a 해시 → Unity.Mathematics.Random)
//
//  ■ 등급별 장비 구성 (시각적 등급 구분)
//    Normal   — 아머 없음, 헬멧 없음, 마스크 없음 / 무채색 계열
//    Uncommon — 아머만 있음 (헬멧 없음)              / 무채색 계열
//    Rare     — 아머 + 헬멧, 마스크 15%              / 일반 색상
//    Unique   — 아머 + 헬멧, 마스크 40%              / 선명한 색상
//    Epic     — 아머 + 헬멧, 마스크 70%              / 금/프리미엄 색상
//
//  ■ 공통 규칙
//    종족:   Human / Elf (50/50, 시드 결정)
//    피부:   살색 계열 팔레트 색상 (종족별 차별화)
//    헤어:   항상 있음 — 랜덤 스타일 + 랜덤 색상
//    케이프: 항상 empty
//    무기:   직업별 풀에서 랜덤
//    Crossbow: Archer 20% 확률로 Firearm 슬롯에 추가
//
//  ■ 캐시
//    (unitName, job, grade) 조합은 항상 동일한 외형을 반환하므로 static 캐시로 재사용.
// ============================================================

public static class AllyAppearanceRoller
{
    // (unitName, job, grade) → UnitAppearanceData 캐시
    static readonly Dictionary<(string, UnitJob, UnitGrade), UnitAppearanceData> _cache = new();
    // ── 종족 ────────────────────────────────────────────────

    static readonly string[] AllyRaces = { "Human", "Elf" };

    // ── 피부색 (SpriteCollection.Palette 기준 정확한 색상값) ─

    static readonly string[] HumanSkins = { "F6CA9F", "E69C69", "BF6F4A", "8A4836" };
    static readonly string[] ElfSkins   = { "F9E6CF", "F6CA9F", "C7CFDD" };

    // ── 헤어 ────────────────────────────────────────────────

    static readonly string[] HairTextures =
    {
        "Hair1", "Hair2", "Hair3", "Hair4", "Hair5", "Hair6", "Hair7",
        "Hair8", "Hair9", "Hair10", "Hair11", "Hair12", "Hair13", "Hair14", "Hair15",
        "Rambo",
    };

    static readonly string[] HairColors =
    {
        "3D3D3D", "5D5D5D", "858585",
        "5D2C28", "8A4836", "BF6F4A", "E69C69",
        "C64524", "E07438", "FFA214",
        "891E2B", "C42430",
    };

    // ── 등급별 장비 틴트 색상 풀 ────────────────────────────
    // Normal / Uncommon — 칙칙한 무채색 계열
    static readonly string[] DullColors =
    {
        "888888", "AAAAAA", "666655", "554433", "335544",
    };

    // Rare — 일반 색상
    static readonly string[] NormalColors =
    {
        "CCCCCC", "AAAAAA", "AA3322", "2244AA",
        "226622", "552299", "884411", "333333",
    };

    // Unique — 선명하고 채도 높은 색상
    static readonly string[] VibrantColors =
    {
        "DD2211", "1133DD", "118833", "661199", "CC8800",
    };

    // Epic — 금/프리미엄 색상
    static readonly string[] PremiumColors =
    {
        "DDAA00", "FFCC00", "CCCCCC", "EE4411", "1155EE", "BB00BB",
    };

    // ── 아머 풀 ─────────────────────────────────────────────

    static readonly string[] Armors =
    {
        "ConeKnightArmor", "TravelerTunic",   "NinjaTunic",      "LegionaryArmor",
        "CaptainArmor",    "BanditTunic",     "CavalrymanArmor", "ArcherTunic",
        "IronKnight",      "CrossKnight",     "HeavyKnightArmor","Gladiator",
        "ThiefTunic",      "MilitiamanArmor", "TournamentArmor", "DwarfTunic",
        "GuardianTunic",   "BlueWizardTunic", "DruidRobe",       "ClericRobe",
        "Chief",           "Griffin",         "DarkKnight",      "BlueKnight",
    };

    // ── 헬멧 풀 ─────────────────────────────────────────────

    static readonly string[] Helmets =
    {
        "ArcherHood",        "Gladiator",          "CrossKnight",
        "LegionaryHelmet",   "TournamentHelmet",    "CaptainHelmet",
        "VikingHelmet",      "IronKnightHelmet",    "GuardianHelmet",
        "HeavyKnightHelmet", "Spartan",             "DruidHelmet",
        "DwarfHelm",         "ThiefHood",           "BanditBandana",
        "MilitiamanHelmet",  "ConeKnightHelm",       "BlueKnightHelmet",
        "ExecutionerHood",   "Griffin",             "DarkKnight",
        "SamuraiHelmet",
        // [ShowEars] 포함 헬멧
        "PirateBandana [ShowEars]", "MusketeerHat [ShowEars]",
        "BanditPatch [ShowEars]",   "ChiefHat [ShowEars]",
    };

    // ── 마스크 풀 ────────────────────────────────────────────

    static readonly string[] Masks =
    {
        "BlackMask", "DarkMask", "IronMask", "Shadow", "BanditMask",
    };

    // ── Back 아이템 풀 ───────────────────────────────────────

    static readonly string[] BackItems = { "BackSword", "LargeBackpack", "SmallBackpack" };

    // ── 직업별 웨펀 풀 ───────────────────────────────────────

    static readonly string[] KnightWeapons =
    {
        "Longsword", "Sword",      "IronSword",  "BastardSword", "Saber",
        "Cutlass",   "Greatsword", "Epee",       "BattleAxe",    "Axe",
        "RoundMace", "Mace",       "Hammer",     "BattleHammer", "Halberd", "Blade",
    };

    static readonly string[] ArcherWeapons =
    {
        "Bow", "ShortBow", "LongBow", "BattleBow", "CurvedBow",
    };

    static readonly string[] MageWeapons =
    {
        "BishopStaff", "HermitStaff", "FlameStaff",  "StormStaff",
        "ElderStaff",  "ArchStaff",   "MagicWand",   "CrystalWand",
        "WaterWand",   "FireWand",    "BlueWand",     "GreenWand",
        "NatureWand",  "SkullWand",   "MasterWand",
    };

    static readonly string[] ShieldBearerWeapons =
    {
        "Mace", "RoundMace", "Hammer", "Sword", "IronSword", "Longsword", "BattleHammer",
    };

    static readonly string[] Shields =
    {
        "AncientGreatShield", "LegionShield",     "Dreadnought",      "GuardianShield",
        "WoodenBuckler",      "IronBuckler",      "RoyalGreatShield", "SteelShield",
        "BlueShield",         "KnightShield",     "GoldenEagle",      "BrassRoundShield",
        "TowerShield",        "CrusaderShield",
    };

    // Crossbow 파일명 — 에셋 원본 파일이 키릴 문자 'С'(\u0421)로 시작함
    const string CrossbowFirearm = "\u0421rossbow";

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// unitName 시드로 외형 데이터를 결정적으로 생성한다.
    /// 같은 unitName + job + grade 는 항상 동일한 외형을 반환.
    /// 동일한 조합은 캐시된 인스턴스를 반환한다.
    /// </summary>
    public static UnitAppearanceData Roll(string unitName, UnitJob job, UnitGrade grade)
    {
        var key = (unitName, job, grade);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        uint seed = ComputeSeed(unitName);
        var  rng  = new Random(seed);

        var data = new UnitAppearanceData();

        // ── 종족 + 피부색 ────────────────────────────────────
        string   race      = AllyRaces[rng.NextInt(0, AllyRaces.Length)];
        string[] skins     = race == "Elf" ? ElfSkins : HumanSkins;
        string   skin      = skins[rng.NextInt(0, skins.Length)];
        string   raceColor = $"{race}#{skin}";

        data.Body = raceColor;
        data.Head = raceColor;
        data.Ears = raceColor;
        data.Eyes = race;          // 눈에는 피부색 미적용

        // ── 헤어 (항상 있음) ─────────────────────────────────
        data.Hair =
            $"{HairTextures[rng.NextInt(0, HairTextures.Length)]}#{HairColors[rng.NextInt(0, HairColors.Length)]}";

        // ── 등급별 색상 풀 결정 ──────────────────────────────
        string[] colorPool = grade switch
        {
            UnitGrade.Epic   => PremiumColors,
            UnitGrade.Unique => VibrantColors,
            UnitGrade.Rare   => NormalColors,
            _                => DullColors,
        };

        // ── 아머 — Uncommon 이상 ─────────────────────────────
        if (grade >= UnitGrade.Uncommon)
        {
            data.Armor =
                $"{Armors[rng.NextInt(0, Armors.Length)]}#{colorPool[rng.NextInt(0, colorPool.Length)]}";
        }
        else
        {
            // 시드 소비 — 등급이 달라져도 이하 결과값 변동 없음
            rng.NextInt(0, Armors.Length);
            rng.NextInt(0, colorPool.Length);
        }

        // ── 헬멧 — Rare 이상 ─────────────────────────────────
        if (grade >= UnitGrade.Rare)
        {
            data.Helmet =
                $"{Helmets[rng.NextInt(0, Helmets.Length)]}#{colorPool[rng.NextInt(0, colorPool.Length)]}";
        }
        else
        {
            rng.NextInt(0, Helmets.Length);
            rng.NextInt(0, colorPool.Length);
        }

        // ── 마스크 — 등급별 확률 ────────────────────────────
        int maskThreshold = grade switch
        {
            UnitGrade.Epic   => 30,   // 70% 확률
            UnitGrade.Unique => 60,   // 40% 확률
            UnitGrade.Rare   => 85,   // 15% 확률
            _                => 100,  // 0%
        };
        if (rng.NextInt(0, 100) >= maskThreshold)
            data.Mask = Masks[rng.NextInt(0, Masks.Length)];
        else
            rng.NextInt(0, Masks.Length);  // 시드 소비

        // ── 케이프: 항상 empty ───────────────────────────────
        data.Cape = "";

        // ── Back — 등급별 확률 ───────────────────────────────
        int backChance = grade switch
        {
            UnitGrade.Epic   => 30,
            UnitGrade.Unique => 20,
            UnitGrade.Rare   => 15,
            UnitGrade.Uncommon => 10,
            _                => 5,
        };

        // ── 직업별 무기 + 직업 특수 처리 ─────────────────────
        switch (job)
        {
            case UnitJob.Knight:
                data.Weapon = KnightWeapons[rng.NextInt(0, KnightWeapons.Length)];
                if (rng.NextInt(0, 100) < backChance)
                    data.Back = BackItems[rng.NextInt(0, BackItems.Length)];
                break;

            case UnitJob.Archer:
                data.Weapon  = ArcherWeapons[rng.NextInt(0, ArcherWeapons.Length)];
                data.Back    = "LeatherQuiver"; // 화살통 고정
                // 20% 확률로 Crossbow (Firearm 슬롯)
                if (rng.NextInt(0, 100) < 20)
                    data.Firearm = CrossbowFirearm;
                break;

            case UnitJob.Mage:
                data.Weapon = MageWeapons[rng.NextInt(0, MageWeapons.Length)];
                if (rng.NextInt(0, 100) < backChance)
                    data.Back = BackItems[rng.NextInt(0, BackItems.Length)];
                break;

            case UnitJob.ShieldBearer:
                data.Weapon = ShieldBearerWeapons[rng.NextInt(0, ShieldBearerWeapons.Length)];
                data.Shield = Shields[rng.NextInt(0, Shields.Length)];
                if (rng.NextInt(0, 100) < backChance)
                    data.Back = BackItems[rng.NextInt(0, BackItems.Length)];
                break;
        }

        _cache[key] = data;
        return data;
    }

    // ── 내부 ─────────────────────────────────────────────────

    /// <summary>FNV-1a 32bit 해시 — UnitJobRoller 와 동일한 방식.</summary>
    static uint ComputeSeed(string name)
    {
        uint hash = 2166136261u;
        foreach (char c in name)
        {
            hash ^= (byte)c;
            hash *= 16777619u;
        }
        return hash == 0u ? 1u : hash;
    }
}
