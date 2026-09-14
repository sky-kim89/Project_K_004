using Unity.Entities;
using Unity.Mathematics;

// ============================================================
//  SkillComponents.cs
//  스킬 관련 ECS 컴포넌트 정의 (장군·엘리트 공용)
//
//  ■ GeneralPassiveSkillComponent
//    - 장군에게 붙음
//    - PassiveSkillAuraSystem 이 주기적으로 읽어
//      CommandRadius 내 소속 병사에게 StatusEffectBufferElement 추가
//    - 버프는 StatType + EffectMode(Add/Multiply) 기반
//
//  ■ GeneralActiveSkillComponent
//    - 장군·엘리트에게 붙음
//    - ActiveSkillCooldownSystem 이 쿨다운을 감소시키고
//      UseActiveSkillTag 가 붙으면 쿨다운 리셋 + 태그 제거
//    - 실제 스킬 실행(트윈·이동·공격 제어)은 별도 스킬 실행기에서 수행
//
//  ■ UseActiveSkillTag
//    - 스킬 발동 요청 시 Entity 에 AddComponent
//    - ActiveSkillCooldownSystem 이 처리 후 즉시 제거
// ============================================================

namespace BattleGame.Units
{
    // ──────────────────────────────────────────
    // 패시브 스킬
    // ──────────────────────────────────────────

    /// <summary>
    /// 장군의 패시브 버프 스킬.
    /// CommandRadius 범위 내 소속 병사의 StatusEffectBuffer 에 버프를 지속 유지한다.
    /// </summary>
    public struct GeneralPassiveSkillComponent : IComponentData
    {
        public StatType   BuffStat;     // 버프할 스텟 종류
        public float      BuffValue;    // 버프 수치
        public EffectMode BuffMode;     // Add(절대값) / Multiply(배율)
        public float      AuraRadius;   // 적용 반경 (0 이면 CommandRadius 전체 사용)
    }

    // ──────────────────────────────────────────
    // 액티브 스킬
    // ──────────────────────────────────────────

    /// <summary>
    /// 장군·엘리트의 액티브 스킬 상태.
    /// CooldownRemaining 이 0 이하면 사용 가능 (UseActiveSkillTag 로 발동).
    /// </summary>
    public struct GeneralActiveSkillComponent : IComponentData
    {
        public int   SkillId;
        public float EffectValue;
        public float EffectRadius;
        public float EffectDuration;
        public float Cooldown;
        public float CooldownRemaining;

        public bool IsReady => CooldownRemaining <= 0f;
    }

    // ──────────────────────────────────────────
    // 발동 요청 태그
    // ──────────────────────────────────────────

    /// <summary>
    /// Entity 에 붙이면 다음 프레임에 **대표 스킬**을 발동한다.
    /// ActiveSkillCooldownSystem 이 처리 후 자동 제거.
    /// </summary>
    public struct UseActiveSkillTag : IComponentData { }

    // ──────────────────────────────────────────
    // 추가 스킬 슬롯 (AI 전용)
    // ──────────────────────────────────────────

    /// <summary>
    /// 대표 스킬 외에 더 들고 있는 액티브 스킬. 개수 제한 없음.
    ///
    /// ■ 왜 대표 스킬과 나눠 두나
    ///   GeneralActiveSkillComponent 는 '플레이어의 스킬' 이다 —
    ///   HUD 카드에 아이콘이 뜨고, 카드를 눌러 수동 발동하며,
    ///   시간 왜곡·마법 집중 같은 어빌리티가 그 쿨다운을 직접 만진다.
    ///   보스 돌진 같은 행동 패턴이 같은 자리를 쓰면
    ///   플레이어가 카드를 눌렀을 때 돌진이 나가 버린다.
    ///
    ///   그래서 이 버퍼는 **AI 만 발동**한다. 패턴도 결국 스킬이라
    ///   쿨다운·타겟·이펙트·Execute() 를 전부 그대로 재사용한다.
    ///
    /// ■ 쓰는 곳
    ///   보스 : 돌진(ActiveSkillId.BossCharge) + 분쇄 강타(BossSlam)
    ///   엘리트: 폭주(무간) 난이도에서 돌진 습득
    ///
    /// ⚠ 슬롯마다 쿨다운이 따로 돈다
    ///   한 슬롯이 쿨다운 중이어도 다른 슬롯은 나갈 수 있다.
    ///   대신 ActiveSkillAISystem 이 한 프레임에 슬롯 하나만 발동시킨다 —
    ///   전부 동시에 터지면 무슨 일이 났는지 화면에서 읽히지 않는다.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct ActiveSkillSlot : IBufferElementData
    {
        public int   SkillId;
        public float EffectValue;
        public float EffectRadius;
        public float EffectDuration;
        public float Cooldown;
        public float CooldownRemaining;

        public bool IsReady => CooldownRemaining <= 0f;
    }

    // ──────────────────────────────────────────
    // 스킬 실행 이벤트 버퍼
    // ──────────────────────────────────────────

    /// <summary>
    /// ActiveSkillCooldownSystem 이 스킬 발동 조건이 충족될 때 이 버퍼에 이벤트를 추가.
    /// ActiveSkillExecuteSystem(managed) 이 같은 프레임에 읽어 Execute() 를 호출 후 Clear.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct ActiveSkillExecuteEvent : IBufferElementData
    {
        public int    SkillId;          // 발동할 스킬 ID (ActiveSkillId enum)
        public Entity TargetEntity;     // 현재 공격 타겟 (없으면 Entity.Null)
        public float3 TargetPosition;   // 스킬 발동 시점의 타겟 위치 스냅샷 (타겟 사망 후에도 유효)
    }
}
