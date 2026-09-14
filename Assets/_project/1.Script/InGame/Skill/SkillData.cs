using UnityEngine;

// ============================================================
//  SkillData.cs
//  스킬 정의 ScriptableObject (아웃게임 / Authoring 전용)
//
//  사용법:
//  - Assets 우클릭 → Create → BattleGame → SkillData 로 생성
//  - GeneralAuthoring의 Passive/ActiveSkill 슬롯에 할당
//
//  ■ 패시브 스킬: 장군 지휘 범위 내 아군 병사에게 스탯 버프
//  ■ 액티브 스킬: 쿨다운 후 발동, 범위/단일 효과
// ============================================================

// ── 스킬 카테고리 ─────────────────────────────────────────────
public enum SkillCategory : byte
{
    Active  = 0,
    Passive = 1,
}

// ── 액티브 스킬 효과 종류 ──────────────────────────────────────
public enum ActiveSkillEffectType : byte
{
    AreaAttack     = 0,   // 범위 공격 데미지
    AllyAttackBuff = 1,   // 아군 공격력 일시 강화
    AllyShield     = 2,   // 아군 방어력 일시 강화
    Charge         = 3,   // 돌격 (이동속도 + 공격력 상승)
}

[CreateAssetMenu(fileName = "SkillData", menuName = "BattleGame/SkillData")]
public class SkillData : ScriptableObject
{
    [Header("공통")]
    public int           SkillId;
    public string        SkillName;
    public SkillCategory Category;

    [Header("패시브 설정 (Category = Passive 일 때만 사용)")]
    [Tooltip("버프할 스탯 종류")]
    public StatType PassiveBuffStat  = StatType.Attack;

    [Tooltip("버프 수치 (고정값 — 장군 스탯과 무관한 절대 수치)")]
    public float    PassiveBuffValue = 10f;

    [Tooltip("적용 반경. 0 이면 같은 팀 전체에 적용")]
    public float    PassiveAuraRadius = 0f;

    [Header("액티브 설정 (Category = Active 일 때만 사용)")]
    public ActiveSkillEffectType ActiveEffectType;

    [Tooltip("쿨다운 (초)")]
    public float ActiveCooldown   = 20f;

    [Tooltip("효과 수치 (데미지 / 버프량)")]
    public float ActiveEffectValue = 50f;

    [Tooltip("효과 반경. 0 이면 단일 대상")]
    public float ActiveEffectRadius = 5f;

    [Tooltip("지속 시간 (초). 즉발 효과면 0")]
    public float ActiveEffectDuration = 5f;
}
