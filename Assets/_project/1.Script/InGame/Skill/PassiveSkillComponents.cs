using Unity.Entities;
using Unity.Mathematics;

// ============================================================
//  PassiveSkillComponents.cs
//  패시브 스킬 런타임 상태 ECS 컴포넌트 정의
//
//  ■ GeneralPassiveSetComponent
//    - 장군이 보유한 3개의 패시브 슬롯
//    - ActiveSlotCount: 등급에 따라 1~3 슬롯 활성화
//
//  ■ SoldierDeathEmpowerState
//    - SoldierDeathEmpower 패시브 런타임 카운터
//
//  ■ SoldierDeathEvent
//    - 병사 사망 이벤트 버퍼 (제너럴 Entity 에 붙음)
//    - UnitDeathDespawnSystem 이 병사 사망 감지 시 추가
//    - SoldierDeathEmpowerSystem 이 처리 후 Clear
//
//  ■ PassiveConditionState
//    - IronWill / LastStand 1회성 트리거 추적
//
//  ■ BloodPactState
//    - BloodPact 현재 보너스 추적 (변경 감지용)
// ============================================================

namespace BattleGame.Units
{
    // ── 패시브 슬롯 컴포넌트 ─────────────────────────────────

    /// <summary>
    /// 장군이 보유한 3개의 패시브 슬롯.
    /// ActiveSlotCount 이하의 슬롯(Slot0, Slot1, Slot2)만 실제 적용.
    /// </summary>
    public struct GeneralPassiveSetComponent : IComponentData
    {
        public PassiveSkillType Slot0;
        public PassiveSkillType Slot1;
        public PassiveSkillType Slot2;
        public byte ActiveSlotCount;   // 등급에 따라 1(Normal/Uncommon) ~ 3(Epic)
    }

    // ── 사망 강화 런타임 상태 ─────────────────────────────────

    /// <summary>
    /// SoldierDeathEmpower 패시브 런타임 상태.
    /// 병사 사망 횟수를 누적해 제너럴 스텟 증가에 사용.
    /// </summary>
    public struct SoldierDeathEmpowerState : IComponentData
    {
        public int DeathCount;  // 누적 사망 병사 수
    }

    // ── 병사 사망 이벤트 버퍼 ─────────────────────────────────

    /// <summary>
    /// 병사 사망 이벤트 버퍼 — 제너럴 Entity 에 붙음.
    /// UnitDeathDespawnSystem 이 소속 병사 사망 감지 시 사망 지점과 함께 Add.
    ///
    /// 소비 순서: PassiveSkillRuntimeSystem(패시브) → CombatTriggerSystem 이 Clear. 앞쪽에서 지우면 뒤쪽 트리거가 영원히 못 본다.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SoldierDeathEvent : IBufferElementData
    {
        /// <summary>병사가 쓰러진 지점 — 순교 등 위치 기반 특성이 사용.</summary>
        public float3 Position;
    }

    // ── 조건부 패시브 트리거 추적 ────────────────────────────

    /// <summary>
    /// IronWill / LastStand 1회성 조건부 패시브 트리거 상태.
    /// 한 번 발동하면 해당 플래그를 true 로 고정해 재발동 방지.
    /// </summary>
    public struct PassiveConditionState : IComponentData
    {
        public bool IronWillTriggered;      // IronWill 발동 여부
        public bool LastStandTriggered;     // LastStand 발동 여부
        public int  InitialSoldierCount;    // 스폰 시 초기 병사 수 (LastStand 기준점)
    }

    // ── BloodPact 런타임 상태 ────────────────────────────────

    /// <summary>
    /// BloodPact 패시브 현재 보너스 추적.
    /// HP 비율이 변경될 때마다 StatusEffect 를 갱신.
    /// LastBonusRatio: 마지막으로 적용된 HP 비율 (변경 감지용).
    /// </summary>
    public struct BloodPactState : IComponentData
    {
        public float LastBonusRatio;    // 마지막 적용된 HP 비율 (0~1)
    }

    // ── 스킬 사용 이벤트 버퍼 ────────────────────────────────

    /// <summary>
    /// 스킬 사용 이벤트 버퍼 — 제너럴 Entity 에 붙음.
    /// ActiveSkillExecuteSystem 이 스킬 실행 직후 Add(default).
    /// PassiveSkillRuntimeSystem 이 처리 후 Clear.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct SkillUseEvent : IBufferElementData { }

    // ── 적 처치 이벤트 버퍼 ──────────────────────────────────

    /// <summary>
    /// 적 처치 이벤트 버퍼 — 제너럴 Entity 에 붙음.
    /// UnitDeathDespawnSystem 이 적 사망 감지 시 아군 장군 전체에 Add(default).
    /// PassiveSkillRuntimeSystem 이 처리 후 Clear.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct EnemyKillEvent : IBufferElementData
    {
        /// <summary>쓰러진 적의 자리 — 처치 시 소환이 그 자리에서 일어나도록.</summary>
        public float3 Position;
    }

    // ── 스택형 패시브 공용 버퍼 ───────────────────────────────

    /// <summary>
    /// 스택형 패시브(StrengthStack / KillMomentum / KillEmpower 등) 공용 버퍼.
    /// 각 패시브가 PassiveType 키로 자신의 슬롯을 찾아 스택 수를 관리한다.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CombatStackElement : IBufferElementData
    {
        public PassiveSkillType PassiveType;
        public int              StackCount;
    }

    // ── 스켈레톤 소환용 병사 사망 위치 버퍼 ─────────────────

    /// <summary>
    /// 사망한 소속 병사의 마지막 월드 위치 버퍼 — 제너럴 Entity 에 붙음.
    /// DeadSoldierTrackingSystem 이 병사 사망 시 채우고,
    /// ActiveSummonSkeleton.Execute() 가 소환 위치로 소비한다.
    /// 최대 8개 유지 (오래된 항목부터 제거).
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DeadSoldierSpawnPointBuffer : IBufferElementData
    {
        public float3 Position;
    }
}
