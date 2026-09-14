using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// ============================================================
//  UnitAuthoring.cs
//  모든 유닛 Authoring 의 공통 베이스 클래스 + 공통 Baker
//
//  파생 클래스 (Authoring/ 폴더):
//    GeneralAuthoring  — 장군 (병사 스폰, 스킬)
//    SoldierAuthoring  — 병사 (스탯은 스폰 시 장군 기반으로 확정)
//    MonsterAuthoring    — 일반 적 (독립 전투)
//    EliteAuthoring    — 엘리트 (독립 강화, 스킬)
//    BossAuthoring     — 보스 (페이즈, 스킬 목록)
//
//  스탯 흐름:
//    BaseStats(Inspector) → UnitStat → StatBlock → StatComponent.Base
//    인게임 버프/디버프 → StatComponent.Final (UnitStatusEffectSystem 이 매 프레임 재계산)
// ============================================================

namespace BattleGame.Units
{
    public abstract class UnitAuthoring : MonoBehaviour
    {
        [Header("소속")]
        public int      UnitId = 0;
        public TeamType Team   = TeamType.Ally;

        [Header("이동 설정")]
        public float StoppingDistance = 0.5f;

        [Header("진형 슬롯")]
        public int Row    = 0;
        public int Column = 0;

        [Header("기본 스텟 (Key 비우면 base 레이어)")]
        public List<StatModifier> BaseStats = new()
        {
            new StatModifier { Type = StatType.MaxHp,       Value = 100f },
            new StatModifier { Type = StatType.Defense,     Value = 0.1f },
            new StatModifier { Type = StatType.Attack,      Value = 15f  },
            new StatModifier { Type = StatType.AttackRange, Value = 1.5f },
            new StatModifier { Type = StatType.AttackSpeed, Value = 1f   },
            new StatModifier { Type = StatType.MoveSpeed,   Value = 3f   },
        };

        public UnitStat BuildStat()
        {
            var stat = new UnitStat();
            stat.Apply(BaseStats);
            return stat;
        }
    }

    // ──────────────────────────────────────────
    // 공통 Baker 베이스
    // ──────────────────────────────────────────

    /// <summary>
    /// 모든 유닛 Baker 의 공통 베이스.
    /// BakeCommon() 으로 공통 ECS 컴포넌트를 일괄 추가한다.
    /// </summary>
    public abstract class UnitBakerBase<T> : Baker<T> where T : UnitAuthoring
    {
        protected void BakeCommon(T authoring, Entity entity, UnitType unitType)
        {
            UnitStat unitStat = authoring.BuildStat();

            // ── 식별 정보 ──────────────────────────────────────
            AddComponent(entity, new UnitIdentityComponent
            {
                UnitId = authoring.UnitId,
                Team   = authoring.Team,
                Type   = unitType,
            });

            // ── 스텟 컴포넌트 ──────────────────────────────────
            // UnitStat 계산 결과를 StatBlock 으로 변환해 Base 에 저장.
            // Final 은 UnitStatusEffectSystem 이 첫 프레임에 Base 로 초기화.
            var statBase = StatBlock.FromUnitStat(unitStat);
            AddComponent(entity, new StatComponent
            {
                Base  = statBase,
                Final = statBase,   // 첫 프레임 전까지 Final = Base 로 초기화
            });

            // ── 이동 ───────────────────────────────────────────
            // MoveSpeed 는 StatComponent 에 있으므로 MovementComponent 에는 없음
            AddComponent(entity, new MovementComponent
            {
                Velocity         = float3.zero,
                Destination      = float3.zero,
                StoppingDistance = authoring.StoppingDistance,
                IsMoving         = false,
            });
            AddComponent(entity, new FormationSlotComponent
            {
                Row          = authoring.Row,
                Column       = authoring.Column,
                SlotPosition = float3.zero,
            });

            // ── 체력 (CurrentHp 만 — MaxHp / Defense → StatComponent) ──
            AddComponent(entity, new HealthComponent
            {
                CurrentHp = unitStat.Get(StatType.MaxHp),
            });

            // ── 공격 (쿨다운·타겟만 — 수치 → StatComponent) ───
            AddComponent(entity, new AttackComponent
            {
                AttackCooldown = 0f,
                HasTarget      = false,
                RandomSeed     = (uint)math.abs(authoring.UnitId) * 2654435761u + 1u,
            });

            // ── 상태 / 피격 / Grid ────────────────────────────
            AddComponent(entity, new UnitStateComponent
            {
                Current    = UnitState.Idle,
                Previous   = UnitState.Idle,
                StateTimer = 0f,
            });
            AddComponent(entity, new HitReactionComponent
            {
                KnockbackVelocity = float3.zero,
                StunDuration      = 0f,
                StunTimer         = 0f,
                IsStunned         = false,
            });
            AddComponent(entity, new GridCellComponent
            {
                Cell     = int2.zero,
                PrevCell = int2.zero,
            });

            // ── 동적 버퍼 ─────────────────────────────────────
            AddBuffer<HitEventBufferElement>(entity);
            AddBuffer<StatusEffectBufferElement>(entity);
        }
    }
}
