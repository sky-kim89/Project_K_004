using System.Collections.Generic;
using Unity.Mathematics;

// ============================================================
//  EnemyAppearanceRoller.cs
//  적군 유닛 외형을 종족(EnemyRace) + unitName 시드로 생성.
//
//  ■ 규칙
//    Body / Head / Eyes / Ears = 종족 이름 (디폴트 색상 고정)
//    무기만 unitName 시드 기반 랜덤
//    그 외 모든 슬롯(아머/헬멧/헤어/마스크 등) = empty
//
//  ■ Lizard / FireLizard 종족
//    CharacterBuilder.BuildLayers() 에서 Lizard 계열 Head 감지 시
//    Hair / Helmet / Mask 를 자동으로 제거하므로 별도 처리 불필요.
//
//  ■ 캐시
//    (race, unitName) 조합은 항상 동일한 외형을 반환하므로 static 캐시로 재사용.
// ============================================================

public static class EnemyAppearanceRoller
{
    // (EnemyRace, unitName, 공격 형태) → UnitAppearanceData 캐시
    // ⚠ 공격 형태도 열쇠다 — 빼면 같은 종족·같은 이름의 근접·원거리 개체가
    //   먼저 만들어진 쪽의 무기를 함께 쓴다 (고블린 궁수가 낫을 든다).
    static readonly Dictionary<(EnemyRace, string, MonsterAttackKind), UnitAppearanceData> _cache = new();

    // ── 무기 풀 — **종족마다 다르다** ─────────────────────────
    //
    //  ⚠ 한때 20자루짜리 공통 풀 하나였다 (2026-09-04에 나눔)
    //    무기를 unitName 시드로만 뽑아서, 고블린과 좀비가 같은 칼을 들고
    //    나왔다. 종족이 달라도 손에 든 것이 같으면 멀리서 구분이 안 된다 —
    //    이 게임은 다섯 라인에 몬스터가 우르르 서므로 실루엣이 곧 정보다.
    //
    //  ⚠ 풀끼리 **겹치지 않는다**
    //    한 자루라도 겹치면 그 자루를 든 순간 두 종족이 같아 보인다.
    //    20자루를 여섯 종족에 나눠 담았고 중복이 없다 — 자루를 옮길 때
    //    반드시 다른 풀에서 빼고 넣을 것.
    //
    //  ⚠ 이름은 벤더 에셋(PixelFantasy)의 슬롯 이름이다
    //    없는 이름을 적으면 무기가 통째로 안 그려진다. 새로 넣으려면
    //    에셋에 그 이름이 실제로 있는지 먼저 확인할 것.

    /// <summary>오크 — 크고 무겁다. 힘으로 내리찍는 것들.</summary>
    static readonly string[] OrcWeapons =
        { "Axe", "BattleAxe", "Greataxe", "GiantBlade" };

    /// <summary>고블린 — 작고 조잡하다. 농기구를 주워 든 꼴.</summary>
    static readonly string[] GoblinWeapons =
        { "Sickle", "Fork", "WoodenClub", "Pitchfork" };

    /// <summary>스켈레톤 — 낫과 녹슨 칼. 죽음의 상징.</summary>
    static readonly string[] SkeletonWeapons =
        { "Scythe", "LargeScythe", "IronSword", "Sword" };

    /// <summary>좀비 — 둔기. 휘두르는 것 말고는 못 한다.</summary>
    static readonly string[] ZombieAWeapons =
        { "SpikedClub", "RoundMace", "Mace" };

    /// <summary>역병 좀비 — 더 무거운 둔기.</summary>
    static readonly string[] ZombieBWeapons =
        { "Hammer", "BattleHammer", "Greatsword" };

    /// <summary>리치 계열 — 죽음의 낫과 거대검.</summary>
    static readonly string[] DemonWeapons =
        { "DeathScythe", "GiantSword" };

    /// <summary>목록에 없는 종족이 오면 쓰는 최후의 풀.</summary>
    static readonly string[] FallbackWeapons =
        { "Sword", "Axe", "Mace" };

    static string[] WeaponsFor(EnemyRace race) => race switch
    {
        EnemyRace.Orc      => OrcWeapons,
        EnemyRace.Goblin   => GoblinWeapons,
        EnemyRace.Skeleton => SkeletonWeapons,
        EnemyRace.ZombieA  => ZombieAWeapons,
        EnemyRace.ZombieB  => ZombieBWeapons,
        EnemyRace.Demon    => DemonWeapons,
        _                  => FallbackWeapons,
    };

    // ── 원거리 무기 풀 — **공격 형태가 갈린다** (사용자 지적, 2026-09-12) ─────
    //
    //  ⚠ 종족만으로는 무기를 정할 수 없다
    //    고블린 궁수가 낫을 들고, 해골 술사가 녹슨 칼을 들고 서 있었다.
    //    Roll 이 공격 형태를 몰랐기 때문이다 — 이제 MonsterAttackKind 를 받는다.
    //    화살을 쏘는데 손에 농기구가 들려 있으면 **무엇을 하는 유닛인지**가
    //    실루엣에서 거짓말을 한다. 이 게임은 다섯 라인에 몬스터가 우르르 서므로
    //    실루엣이 곧 정보다 (위 근접 풀 주석과 같은 이유).
    //
    //  ⚠ 근접 풀과도, 서로끼리도 한 자루도 겹치지 않는다
    //    활·지팡이는 근접 풀 어디에도 없다. 종족끼리도 갈라 두었다.
    //
    //  ⚠ 이름은 벤더 에셋(PixelFantasy)의 Weapon 슬롯 이름이다
    //    없는 이름을 적으면 무기가 통째로 안 그려진다.

    /// <summary>고블린 — 작고 조잡한 활.</summary>
    static readonly string[] GoblinRanged =
        { "ShortBow", "Bow" };

    /// <summary>스켈레톤 — 해골 술사. 활이 아니라 지팡이다.</summary>
    static readonly string[] SkeletonRanged =
        { "NecromancerStaff", "SkullWand" };

    /// <summary>리치 계열 — 더 높은 격의 지팡이.</summary>
    static readonly string[] DemonRanged =
        { "ElderStaff", "StormStaff" };

    /// <summary>오크 — 크고 무거운 활.</summary>
    static readonly string[] OrcRanged =
        { "BattleBow", "LongBow" };

    static readonly string[] ZombieARanged = { "HermitStaff" };
    static readonly string[] ZombieBRanged = { "WingedStaff" };

    /// <summary>목록에 없는 종족이 원거리로 오면 쓰는 최후의 풀.</summary>
    static readonly string[] FallbackRanged =
        { "CurvedBow", "MagicWand" };

    static string[] RangedWeaponsFor(EnemyRace race) => race switch
    {
        EnemyRace.Orc      => OrcRanged,
        EnemyRace.Goblin   => GoblinRanged,
        EnemyRace.Skeleton => SkeletonRanged,
        EnemyRace.ZombieA  => ZombieARanged,
        EnemyRace.ZombieB  => ZombieBRanged,
        EnemyRace.Demon    => DemonRanged,
        _                  => FallbackRanged,
    };

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// 종족으로 신체를 결정하고 unitName 시드로 무기를 결정한다.
    /// 신체 색상은 에셋 기본값(디폴트) 유지.
    /// 동일한 (race, unitName) 조합은 캐시된 인스턴스를 반환한다.
    /// </summary>
    public static UnitAppearanceData Roll(EnemyRace race, string unitName)
        => Roll(race, unitName, MonsterAttackKind.Melee);

    /// <summary>
    /// 공격 형태까지 보고 무기를 고른다 — 원거리면 활·지팡이를 든다.
    ///
    /// ⚠ 용사(General)는 위 2인자 판을 그대로 쓴다 — 그쪽은 직업이 무기를
    ///   정하는 별개 경로(AllyAppearanceRoller)이므로 여기 갈래와 무관하다.
    /// </summary>
    public static UnitAppearanceData Roll(EnemyRace race, string unitName, MonsterAttackKind kind)
    {
        var key = (race, unitName, kind);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        uint seed = ComputeSeed(unitName);
        var  rng  = new Random(seed);

        string raceName = race.ToString();

        var data = new UnitAppearanceData
        {
            Body   = raceName,
            Head   = raceName,
            Ears   = raceName,
            Eyes   = raceName,
            Weapon = PickWeapon(race, kind, ref rng),
            // 나머지 슬롯: 기본값 empty
        };

        _cache[key] = data;
        return data;
    }

    // ── 내부 ─────────────────────────────────────────────────

    /// <summary>그 종족·그 공격 형태의 풀에서 한 자루. 같은 이름이면 늘 같은 자루가 나온다.</summary>
    static string PickWeapon(EnemyRace race, MonsterAttackKind kind, ref Random rng)
    {
        string[] pool = kind == MonsterAttackKind.Ranged
                      ? RangedWeaponsFor(race)
                      : WeaponsFor(race);

        return pool[rng.NextInt(0, pool.Length)];
    }

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
