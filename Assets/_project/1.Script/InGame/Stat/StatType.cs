using System;
using Unity.Collections;

// ============================================================
//  StatType.cs
//  스텟 시스템 관련 enum / 구조체 정의
// ============================================================

// ── 스텟 종류 ─────────────────────────────────────────────────
public enum StatType
{
    MaxHp        = 0,
    Defense      = 1,   // 방어율 0~1
    Attack       = 2,
    AttackRange  = 3,
    AttackSpeed  = 4,   // 초당 공격 횟수
    MoveSpeed    = 5,
    CritChance    = 6,   // 크리티컬 확률 0~1
    CritDamage    = 7,   // 크리티컬 배율 (기본 1.5)
    SoldierCount         = 8,   // 장군이 지휘하는 병사 수
    CommandPower         = 9,   // 병사 지휘력 — 1포인트당 병사 스텟 1% 증가
    SkillCooldownReduce  = 10,  // 스킬 쿨다운 감소율 (0~1, 예: 0.1 = 10% 감소)
    // 전투 스텟이지만 번호는 15 — 아래 '시스템 전용' 뒤에 있다. DefensePenetration 참고.

    // ── 시스템 전용 스탯 (TraitData.Effects 에서만 사용) ──────
    GeneralSlotBonus     = 11,  // 장수 배치 슬롯 추가 수 (NormalMode 에서 집계)
    AllStatPenalty       = 12,  // 전체 능력치 패널티 비율 (TraitApplier 에서 집계 후 적용)
    EquipSlotBonus       = 13,  // 장비 슬롯 추가 수 (TraitApplier 에서 집계)
    ExpGainBonus         = 14,  // 경험치 획득 비율 가산 (InGameManager 전투 보상에서 집계)

    /// <summary>
    /// 방어율 관통 (0~1). 공격자가 가진 값만큼 <b>대상의 최종 방어율에서 그대로 뺀다.</b>
    ///   대상 방어율 0.70, 관통 0.10 → 실효 방어율 0.60 (피해 0.30 → 0.40)
    ///
    /// ⚠ 곱연산(방어율 × (1-관통))이 아니다
    ///   Defense 는 이 게임에서 "깎이는 비율" 자체다. 곱연산으로 하면 방어율이
    ///   높을수록 관통 1%p 의 값어치가 폭증해(0.9 → 0.81 은 피해 90% 증가) 후반
    ///   보스가 한 스택만 쌓여도 아군이 즉사한다. 뺄셈은 스택 수에 비례해
    ///   선형으로 오르므로 광폭화 스택 설계와 맞는다.
    ///
    /// 실제 적용은 DamageMath.AfterDefense 한 곳뿐이다. 공격자 → HitEvent(발사체면
    /// ProjectileComponent) → 피격 계산 순서로 값이 실려 간다.
    /// </summary>
    DefensePenetration   = 15,

    /// <summary>
    /// 소환력 — 소환사(Summoner)가 소환한 몬스터에게 <b>더해지는</b> 보너스의 크기.
    ///
    /// ⚠ CommandPower(지휘력)와 구조가 다르다. 헷갈리지 말 것.
    ///   지휘력 : 병사 스탯 = 장군 스탯 × (0.2 + 지휘력×0.01)   ← 종속 파생
    ///            병사는 자기 스탯이 없다. 장군이 전부다.
    ///   소환력 : 몬스터 스탯 = 종족 기본 스탯 + 소환력 보너스     ← 가산
    ///            몬스터는 종족별 기본 스탯을 이미 갖고 있다.
    ///
    /// 그래서 소환사를 바꿔도 종족별 스탯 성향(탱커는 여전히 탱커)은 유지되고,
    /// 소환사는 그 위에 얹는 두께만 바꾼다.
    /// 실제 합성은 MonsterStatComposer 한 곳에서만 한다.
    /// </summary>
    SummonPower          = 16,

    // 새 스텟은 여기에 순서대로 추가 — 다른 코드 수정 불필요 (최대 127개)
}

// ── 레이어 간 결합 방식 ────────────────────────────────────────
public enum CombineMode
{
    Add,
    Multiply,
    Max,

    /// <summary>
    /// 감소율 곱연산 — 각 레이어가 "남은 양"을 순서대로 깎는다.
    ///     결과 = 1 - Π(1 - 레이어값)
    /// 쿨타임 감소처럼 여러 출처가 겹치는 감소율에 쓴다.
    ///
    /// 출처가 하나면 값이 그대로 나온다 (10% → 10%). 겹칠 때만 완만해진다
    /// (10% + 10% → 19%). 구조상 100% 를 넘지 않는다.
    /// 음수(페널티) 레이어는 (1-v) > 1 이 되어 총량을 되돌린다 — 의도된 동작.
    /// </summary>
    MultiplyResidual,
}

// ── 단일 스텟 수정자 ───────────────────────────────────────────
[Serializable]
public struct StatModifier
{
    public StatType Type;
    public float    Value;

    [UnityEngine.Tooltip("레이어 키 — 비워두면 base 레이어에 추가됩니다.\n" +
                         "예) equip_sword / buff_rage / skill_passive")]
    public string Key;
}

// ── StatBlock ─────────────────────────────────────────────────
/// <summary>
/// StatType 을 인덱스로 사용하는 가변 길이 float 배열.
/// Burst·ECS 호환 비관리형(unmanaged) 구조체.
///
/// 사용법:
///   float atk = stat.Final[StatType.Attack];
///   stat.Final[StatType.MoveSpeed] *= 1.3f;
///
/// 내부 저장소: FixedList512Bytes&lt;float&gt; = 최대 127 슬롯
/// → StatType 이 수십 개로 늘어나도 코드 수정 불필요
/// → 127개 초과 시 FixedList4096Bytes&lt;float&gt; 로 교체 (최대 1023개)
/// </summary>
public struct StatBlock
{
    // FixedList512Bytes<float> = (512 - 2) / 4 = 127 슬롯
    // unsafe 없이 Burst / ECS 에서 안전하게 사용 가능
    FixedList512Bytes<float> _data;

    // ── 인덱서 ────────────────────────────────────────────────

    public float this[StatType stat]
    {
        // 아직 설정되지 않은 슬롯은 0 반환 (안전 기본값)
        get
        {
            int i = (int)stat;
            return i < _data.Length ? _data[i] : 0f;
        }
        // 슬롯이 부족하면 0으로 자동 확장 후 저장
        set
        {
            int i = (int)stat;
            while (_data.Length <= i)
                _data.Add(0f);
            _data[i] = value;
        }
    }

    // ── 유틸리티 ──────────────────────────────────────────────

    /// <summary>UnitStat 계산 결과를 StatBlock 으로 변환 (Baker / 아웃게임 전용).</summary>
    public static StatBlock FromUnitStat(UnitStat unitStat)
    {
        var block = new StatBlock();
        foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            block[stat] = unitStat.Get(stat);
        return block;
    }
}

/// <summary>
/// 기본값이 0이거나 절대값(pp) 가산이 필요한 스탯 — 비율로 곱하지 않고 그대로 더한다.
/// 유물·몬스터 레벨 보너스·도감 수치가 같은 규칙을 쓴다 (원작 AbilityApplier 에서 옮겨 왔다).
/// </summary>
public static class StatRules
{
    public static bool IsAbsoluteStat(StatType type)
        => type == StatType.SkillCooldownReduce
        || type == StatType.CritChance
        || type == StatType.Defense
        || type == StatType.SoldierCount   // 절대 가산 (+1, +2)
        || type == StatType.CommandPower;  // 절대 가산 (+10, +20)
}
