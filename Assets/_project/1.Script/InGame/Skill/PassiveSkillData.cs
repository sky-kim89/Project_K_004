using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PassiveSkillData.cs
//  패시브 스킬 베이스 ScriptableObject.
//
//  ■ 일반 패시브 (스폰 시 즉시 스텟 적용)
//    TriggerType = None.
//    StatModifiers 에 수치를 입력하면 스폰 시 자동 적용.
//    서브클래스 없이 PassiveSkillData 를 직접 사용한다.
//
//  ■ 조건부 / 이벤트 반응 패시브
//    TriggerType 에 반응할 이벤트를 지정.
//    PassiveSkillRuntimeSystem 이 해당 이벤트 발생 시 OnTrigger() 를 호출.
//    서브클래스에서 OnTrigger() 를 오버라이드해 구체적인 처리를 구현.
//
//  ■ 추가 패시브 시 작업 범위
//    - 스텟만 변경: SO 생성 → Inspector 에서 StatModifiers 설정 → DB 등록
//    - 이벤트 반응: SO 생성 + 서브클래스 작성 → TriggerType 설정 → DB 등록
//    - 완전히 새로운 TriggerType 필요 시: enum 에 추가 + 시스템에 감지 코드 추가
// ============================================================

/// <summary>
/// 패시브 스킬이 반응하는 게임 이벤트 종류.
/// PassiveSkillData.TriggerType 에 설정한다.
/// </summary>
public enum PassiveTrigger : byte
{
    None           = 0,  // 이벤트 반응 없음 (스폰 시 즉시 적용 패시브)
    OnHit          = 1,  // 제너럴이 피격될 때 (매 피격마다 호출)
    OnAttack       = 2,  // 제너럴이 공격할 때
    OnEnemyKill    = 3,  // 제너럴 또는 소속 병사가 적을 처치할 때
    OnSoldierDeath = 4,  // 소속 병사가 사망할 때
    OnSkillUse     = 5,  // 제너럴이 액티브 스킬을 사용할 때
    OnBattleStart   = 6,  // 전투 시작 시 1회 — BattleManager.FireBattleStartTriggers() 가 호출
    StageClear      = 7,  // 스테이지 클리어 시 — BattleManager 가 승리 시 호출 (전투 외 트리거)
    OnAttackLanded  = 8,  // 장군의 기본 공격이 실제로 타겟에 착탄될 때 (근거리+원거리 공통, 1프레임 지연)
}

[CreateAssetMenu(fileName = "PassiveSkillData", menuName = "BattleGame/PassiveSkillData")]
public class PassiveSkillData : ScriptableObject
{
    // ─────────────────────────────────────────────────────────
    // ■ 식별
    // ─────────────────────────────────────────────────────────

    [Header("식별")]
    [Tooltip("이 데이터가 대응하는 패시브 종류")]
    public PassiveSkillType Type;

    [Tooltip("패시브 이름 (에디터·UI 표시용)")]
    public string SkillName;

    [TextArea(2, 4)]
    [Tooltip("패시브 설명 (UI 표시용)")]
    public string Description;

    // ─────────────────────────────────────────────────────────
    // ■ 스폰 시 즉시 적용 스텟 목록
    // ─────────────────────────────────────────────────────────

    [Header("스폰 시 즉시 적용 스텟 (TriggerType = None 패시브)")]
    [Tooltip("스폰 시 한 번 적용되는 스텟 변경 목록.\n" +
             "이벤트 반응 패시브는 비워두고 TriggerType 과 서브클래스로 처리한다.")]
    public List<StatModifierEntry> StatModifiers = new();

    // ─────────────────────────────────────────────────────────
    // ■ 런타임 이벤트 트리거
    // ─────────────────────────────────────────────────────────

    [Header("런타임 이벤트 트리거")]
    [Tooltip("어떤 게임 이벤트에 반응할지. None 이면 스폰 시 즉시 적용 패시브.")]
    public PassiveTrigger TriggerType = PassiveTrigger.None;

    // ─────────────────────────────────────────────────────────
    // ■ 특수 플래그
    // ─────────────────────────────────────────────────────────

    [Header("아이콘")]
    [Tooltip("버프 슬롯에 표시할 아이콘. 미설정 시 버프 슬롯에 표시되지 않음.")]
    public Sprite Icon;

    [Header("특수 플래그")]
    [Tooltip("TitanGeneral: 제너럴 크기 배율 추가값 (0 = 변화 없음)")]
    public float GeneralScaleBonusAdd = 0f;

    // ─────────────────────────────────────────────────────────
    // ■ 런타임 트리거 콜백 — 서브클래스에서 필요할 때만 오버라이드
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// TriggerType 에 해당하는 게임 이벤트 발생 시 호출된다.
    /// 기본 구현은 아무것도 하지 않음. 서브클래스에서 오버라이드.
    /// </summary>
    public virtual void OnTrigger(PassiveTriggerContext context) { }

    /// <summary>
    /// 로비 스탯 화면용 — 전투 시작 시 붙을 값을 미리 계산해 신고한다.
    ///
    /// ■ 왜 필요한가
    ///   OnBattleStart 패시브는 전투가 시작돼야 계산되므로 로비 스탯에 전혀 안 뜬다.
    ///   "방패의 날: 방어율 10%당 공격력 +4%" 를 달고 방어구를 껴도 공격력 숫자가
    ///   꿈쩍 안 하니 안 먹는 것으로 보였다. 미리 계산해 같은 값을 보여 준다.
    ///
    /// ■ 계약
    ///   current  : 지금까지 합산된 최종 스탯을 돌려준다 (앞 슬롯의 예상치까지 반영).
    ///   baseRoll : 성장(등급·레벨 롤)만의 값. '외부로 오른 증가분' 을 세는 쪽이 쓴다
    ///              (전투의 BaseRollStatComponent 와 같은 기준선).
    ///   outDeltas: 스탯별 **증가량(절대값)** 을 더해 넣는다.
    ///
    /// ⚠ OnTrigger 와 같은 식이어야 한다
    ///   한쪽만 고치면 로비에서 본 숫자와 전투가 갈린다. 공식을 바꿀 때는 반드시 둘 다.
    /// </summary>
    public virtual void CollectPreviewStats(Func<StatType, float> current,
                                            Func<StatType, float> baseRoll,
                                            Dictionary<StatType, float> outDeltas) { }

    // ─────────────────────────────────────────────────────────
    // ■ 단일 스텟 수정자 항목
    // ─────────────────────────────────────────────────────────

    [Serializable]
    public struct StatModifierEntry
    {
        [Tooltip("변경할 스텟 종류")]
        public StatType Stat;

        [Tooltip("수치 (IsPercent=true 이면 기존 스텟의 N% 증감)")]
        public float Delta;

        [Tooltip("true  → 기존 스텟 × Delta 를 더함 (0.2 = +20%)\n" +
                 "false → Delta 를 절대값으로 더함")]
        public bool IsPercent;

        [Tooltip("어디에 적용할지:\n" +
                 "General = 제너럴 UnitStat (SpawnEntity 전)\n" +
                 "Soldier = 병사 StatComponent (Initialize 후)")]
        public PassiveSkillApplier.ApplyTarget Target;
    }
}

// ─────────────────────────────────────────────────────────────
// ■ 트리거 컨텍스트
// ─────────────────────────────────────────────────────────────

/// <summary>
/// OnTrigger() 에 전달되는 이벤트 컨텍스트.
/// 모든 트리거 타입이 공통으로 사용한다.
/// </summary>
public struct PassiveTriggerContext
{
    public Entity         GeneralEntity;
    public EntityManager  EntityManager;
    public EntityQuery    EnemyQuery;          // CombatTriggerSystem 에서 OnCreate 에 캐시된 적 쿼리

    // TriggerType 별 부가 데이터
    public HealthComponent Health;             // OnHit: 피격 시점 체력 스냅샷
    public int             SoldierDeathCount;  // OnSoldierDeath: 이번 프레임 사망 병사 수
    public float           DamageDealt;        // OnAttack: 이번 공격으로 가한 피해 (흡혈 등에 사용)

    // ── 사건이 일어난 자리 ────────────────────────────────────
    //  소환처럼 "그 자리에" 무언가를 세우는 효과가 쓴다.
    //  장군 발밑에서 나오면 무엇이 왜 나왔는지 읽히지 않는다 —
    //  쓰러진 적 위에서 일어나야 "이 녀석이 내 병사가 됐다" 가 보인다.
    public bool            HasKillPosition;
    public float3          KillPosition;          // OnEnemyKill: 마지막으로 쓰러뜨린 적의 자리
    public bool            HasSoldierDeathPosition;
    public float3          SoldierDeathPosition;  // OnSoldierDeath: 병사가 쓰러진 자리
}
