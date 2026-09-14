using Unity.Mathematics;
using UnityEngine;

// ============================================================
//  MonsterStatRoller.cs
//  적 유닛 스텟 결정적(Deterministic) 랜덤 생성기.
//
//  같은 UnitName → 항상 같은 기본 스텟 (FNV-1a 시드 기반).
//  SpawnUnitType 별로 스텟 범위가 다르게 적용된다.
//
//  성장 요소 (level, statMultiplier):
//    level         — 레벨당 LevelMultPerLevel(기본 1%) 스텟 증가 (HP·ATK만 적용)
//    statMultiplier — 스테이지 진행도 기반 배율 (HP·ATK에 곱함, 지수 성장)
//
//  사용:
//    UnitStat stat = MonsterStatRoller.Roll("S5W1E", SpawnUnitType.Monster, level: 4, statMultiplier: 1.2f);
// ============================================================

public static class MonsterStatRoller
{
    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// unitName 을 시드로, unitType 에 맞는 범위의 스텟을 생성해 반환한다.
    /// level 과 statMultiplier 가 HP·ATK에 추가로 곱해진다 (DEF·Range·Speed 제외).
    /// </summary>
    public static UnitStat Roll(string unitName, SpawnUnitType unitType,
                                int level = 1, float statMultiplier = 1f, float stageBias = 0f)
    {
        uint seed = ComputeSeed(unitName);
        var  rng  = new Unity.Mathematics.Random(seed);

        var cfg   = GameplayConfig.Current;
        EnemyGradeStatRange range = cfg != null
            ? cfg.GetEnemyRange(unitType)
            : FallbackRange(unitType);

        // 레벨 배율: 레벨당 1% (GameplayConfig.LevelMultPerLevel)
        float levelMultPerLevel = cfg != null ? cfg.LevelMultPerLevel : 0.01f;
        float levelMult = 1f + Mathf.Max(0, level - 1) * levelMultPerLevel;

        // HP·ATK에만 레벨 배율 + 스테이지 배율을 곱한다
        float hpAtkMult = levelMult * Mathf.Max(0.1f, statMultiplier);

        // ⚠ 난이도 '광포' 를 여기서 곱하지 않는다 (2026-09-11)
        //   이 롤러는 원작의 **적** 롤러였지만, 이 게임에서 이 프리팹 계열은
        //   플레이어의 소환 몬스터다. 여기서 곱하면 난이도가 오를수록 **아군**이 세진다.
        //   적(용사)의 광포는 GeneralStatRoller 가 곱한다.

        var stat = new UnitStat();
        stat.Set(StatType.MaxHp,       range.Hp.Lerp(B(ref rng, stageBias))        * hpAtkMult);
        stat.Set(StatType.Attack,      range.Attack.Lerp(B(ref rng, stageBias))     * hpAtkMult);
        stat.Set(StatType.Defense,     Mathf.Min(range.Defense.Lerp(B(ref rng, stageBias)), 0.85f));
        stat.Set(StatType.AttackRange, range.AttackRange.Lerp(B(ref rng, stageBias)));
        stat.Set(StatType.AttackSpeed, range.AttackSpeed.Lerp(B(ref rng, stageBias)));
        stat.Set(StatType.MoveSpeed,   range.MoveSpeed.Lerp(B(ref rng, stageBias)));
        stat.Set(StatType.CritChance,  range.CritChance);
        stat.Set(StatType.CritDamage,  range.CritDamage);

        return stat;
    }

    // stageBias=0: 균등 랜덤(0~1), stageBias=1: 항상 1.0(최댓값)
    // 공식: t = bias + (1 - bias) × random → 유효 범위 [bias, 1]
    static float B(ref Unity.Mathematics.Random rng, float bias)
        => bias + (1f - bias) * rng.NextFloat();

    // ── 내부 ─────────────────────────────────────────────────

    /// <summary>FNV-1a 32bit 해시 — 실행·플랫폼마다 일치 보장.</summary>
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

    /// <summary>GameplayConfig 미할당 시 하드코딩 폴백 (에디터 테스트 등 예외 상황용).</summary>
    static EnemyGradeStatRange FallbackRange(SpawnUnitType type) => type switch
    {
        SpawnUnitType.Elite => new EnemyGradeStatRange
        {
            Hp = new FloatRange(840f, 2400f), Attack = new FloatRange(77f, 231f),
            Defense = new FloatRange(0.12f, 0.32f), AttackRange = new FloatRange(1.5f, 3.0f),
            AttackSpeed = new FloatRange(0.8f, 2.0f), MoveSpeed = new FloatRange(2.5f, 4.5f),
            CritChance = 0.08f, CritDamage = 1.60f,
        },
        // ⚠ 보스는 '느리고 무겁게' — 공격속도 1/3, 평타 피해 3배 (DPS 는 그대로)
        //   초당 여러 번 깨작거리면 1,000마리 난전 속에서 보스가 안 보인다.
        //   한 대씩 크게 때려야 "보스한테 맞았다" 가 화면과 숫자로 읽힌다.
        //   AoE·넉백까지 이 한 방에 얹히므로 체감 차이가 더 벌어진다.
        //   ×3 은 Attack 스텟이 아니라 BossAttackSystem.BasicAttackMultiplier 가 갖는다 (스킬 피해 제외).
        SpawnUnitType.Boss => new EnemyGradeStatRange
        {
            Hp = new FloatRange(5000f, 14000f), Attack = new FloatRange(220f, 550f),
            Defense = new FloatRange(0.25f, 0.50f), AttackRange = new FloatRange(2.5f, 4.5f),
            AttackSpeed = new FloatRange(0.133f, 0.30f), MoveSpeed = new FloatRange(2.0f, 3.5f),
            CritChance = 0.08f, CritDamage = 1.60f,
        },
        _ => new EnemyGradeStatRange
        {
            Hp = new FloatRange(240f, 720f), Attack = new FloatRange(19.8f, 71.5f),
            Defense = new FloatRange(0.05f, 0.15f), AttackRange = new FloatRange(1.2f, 2.5f),
            AttackSpeed = new FloatRange(0.6f, 1.6f), MoveSpeed = new FloatRange(2.0f, 4.0f),
            CritChance = 0.05f, CritDamage = 1.50f,
        },
    };
}
