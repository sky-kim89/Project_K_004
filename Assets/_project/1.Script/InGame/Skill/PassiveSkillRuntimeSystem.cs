using System.Collections.Generic;
using Unity.Entities;

// ============================================================
//  PassiveSkillRuntimeSystem.cs
//  런타임 패시브 이벤트 디스패처 시스템.
//
//  역할:
//    게임 이벤트(피격, 병사 사망)를 감지한 뒤
//    장군이 보유한 패시브 중 TriggerType 이 일치하는 것만 골라 OnTrigger() 를 호출한다.
//
//  ■ 이벤트 발생 측 규약
//    피격:
//      UnitHitSystem 이 HitEventBufferElement 를 버퍼에 추가
//      → 이 시스템이 버퍼 비어있지 않음을 감지 → TriggerType.OnHit 콜백 호출
//    병사 사망:
//      UnitDeathDespawnSystem 이 SoldierDeathEvent 버퍼에 이벤트 추가
//      → 이 시스템이 감지 → TriggerType.OnSoldierDeath 콜백 호출 → 버퍼 Clear
//
//  ■ 새 TriggerType 추가 시 작업 범위
//    1. PassiveTrigger enum 에 값 추가 (PassiveSkillData.cs)
//    2. 이벤트 발생 측에서 적절한 버퍼/컴포넌트 추가
//    3. 이 시스템에 감지 + 디스패치 코드 추가
// ============================================================

namespace BattleGame.Units
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitAttackSystem))]
    [UpdateBefore(typeof(UnitHitSystem))]
    public partial class PassiveSkillRuntimeSystem : SystemBase
    {
        readonly List<(Entity entity, PassiveTrigger trigger, PassiveTriggerContext ctx)> _queue = new();

        protected override void OnUpdate()
        {
            var db = PassiveSkillDatabase.Current;
            if (db == null) return;

            _queue.Clear();

            // ── ① 피격 이벤트 (TriggerType.OnHit) ──────────────
            Entities
                .WithoutBurst()
                .WithAll<GeneralPassiveSetComponent>()
                .WithNone<DeadTag>()
                .ForEach((Entity entity,
                          DynamicBuffer<HitEventBufferElement> hitEvents,
                          in HealthComponent health) =>
                {
                    if (hitEvents.Length > 0)
                    {
                        _queue.Add((entity, PassiveTrigger.OnHit, new PassiveTriggerContext
                        {
                            GeneralEntity     = entity,
                            EntityManager     = EntityManager,
                            Health            = health,
                            SoldierDeathCount = 0,
                            DamageDealt       = 0f,
                        }));
                    }
                })
                .Run();

            // ── ② 병사 사망 이벤트 (TriggerType.OnSoldierDeath) ──
            Entities
                .WithoutBurst()
                .WithAll<GeneralPassiveSetComponent>()
                .WithNone<DeadTag>()
                .ForEach((Entity entity,
                          DynamicBuffer<SoldierDeathEvent> deathEvents,
                          in HealthComponent health) =>
                {
                    if (deathEvents.Length > 0)
                    {
                        _queue.Add((entity, PassiveTrigger.OnSoldierDeath, new PassiveTriggerContext
                        {
                            GeneralEntity     = entity,
                            EntityManager     = EntityManager,
                            Health            = health,
                            SoldierDeathCount = deathEvents.Length,
                            DamageDealt       = 0f,
                        }));
                        // ⚠ 여기서 Clear 하지 않는다 — 이 시스템 뒤에 도는
                        //   CombatTriggerSystem(장비·어빌리티·특성)이 같은 버퍼를 읽는다.
                        //   소비가 끝난 뒤 CombatTriggerSystem 이 Clear 한다.
                    }
                })
                .Run();

            // ── ③ 공격 이벤트 (TriggerType.OnAttack) ─────────────
            Entities
                .WithoutBurst()
                .WithAll<GeneralPassiveSetComponent>()
                .WithNone<DeadTag>()
                .ForEach((Entity entity,
                          in AttackComponent attack,
                          in HealthComponent health) =>
                {
                    if (attack.AttackedThisFrame)
                    {
                        _queue.Add((entity, PassiveTrigger.OnAttack, new PassiveTriggerContext
                        {
                            GeneralEntity     = entity,
                            EntityManager     = EntityManager,
                            Health            = health,
                            SoldierDeathCount = 0,
                            DamageDealt       = attack.LastDamageDealt,
                        }));
                    }
                })
                .Run();

            // ── ④ 적 처치 이벤트 (TriggerType.OnEnemyKill) ───────
            Entities
                .WithoutBurst()
                .WithAll<GeneralPassiveSetComponent>()
                .WithNone<DeadTag>()
                .ForEach((Entity entity,
                          DynamicBuffer<EnemyKillEvent> killEvents,
                          in HealthComponent health) =>
                {
                    if (killEvents.Length > 0)
                    {
                        _queue.Add((entity, PassiveTrigger.OnEnemyKill, new PassiveTriggerContext
                        {
                            GeneralEntity     = entity,
                            EntityManager     = EntityManager,
                            Health            = health,
                            SoldierDeathCount = 0,
                            DamageDealt       = 0f,
                        }));
                        // Clear 는 CombatTriggerSystem 이 담당 (위 ② 주석 참고)
                    }
                })
                .Run();

            // ── ⑤ 스킬 사용 이벤트 (TriggerType.OnSkillUse) ──────
            Entities
                .WithoutBurst()
                .WithAll<GeneralPassiveSetComponent>()
                .WithNone<DeadTag>()
                .ForEach((Entity entity,
                          DynamicBuffer<SkillUseEvent> skillEvents,
                          in HealthComponent health) =>
                {
                    if (skillEvents.Length > 0)
                    {
                        _queue.Add((entity, PassiveTrigger.OnSkillUse, new PassiveTriggerContext
                        {
                            GeneralEntity     = entity,
                            EntityManager     = EntityManager,
                            Health            = health,
                            SoldierDeathCount = 0,
                            DamageDealt       = 0f,
                        }));
                        // Clear 는 CombatTriggerSystem 이 담당 (위 ② 주석 참고)
                    }
                })
                .Run();

            // ── ⑥ 이벤트별 패시브 디스패치 ──────────────────────
            foreach (var (generalEntity, trigger, ctx) in _queue)
            {
                if (!EntityManager.Exists(generalEntity)) continue;
                if (!EntityManager.HasComponent<GeneralPassiveSetComponent>(generalEntity)) continue;

                var passiveSet = EntityManager.GetComponentData<GeneralPassiveSetComponent>(generalEntity);
                DispatchTrigger(ref passiveSet, trigger, ctx, db);
            }
        }

        // ── 내부 유틸리티 ────────────────────────────────────────

        /// <summary>
        /// 장군의 활성 패시브 슬롯 중 TriggerType 이 일치하는 것만 OnTrigger() 호출.
        /// </summary>
        static void DispatchTrigger(
            ref GeneralPassiveSetComponent set,
            PassiveTrigger trigger,
            PassiveTriggerContext ctx,
            PassiveSkillDatabase db)
        {
            TryDispatch(db.Get(set.Slot0), trigger, ctx, set.ActiveSlotCount >= 1);
            TryDispatch(db.Get(set.Slot1), trigger, ctx, set.ActiveSlotCount >= 2);
            TryDispatch(db.Get(set.Slot2), trigger, ctx, set.ActiveSlotCount >= 3);
        }

        static void TryDispatch(PassiveSkillData data, PassiveTrigger trigger, PassiveTriggerContext ctx, bool active)
        {
            if (!active || data == null) return;
            if (data.TriggerType != trigger) return;

#if UNITY_EDITOR
            LogPassiveTrigger(data, trigger, ctx);
#endif
            data.OnTrigger(ctx);
        }

#if UNITY_EDITOR
        static void LogPassiveTrigger(PassiveSkillData data, PassiveTrigger trigger, PassiveTriggerContext ctx)
        {
            if (global::GameplayConfig.Current != null && !global::GameplayConfig.Current.EnablePassiveLog) return;

            var em  = ctx.EntityManager;
            var sb  = new System.Text.StringBuilder();

            // ── 패시브 정보
            string unitName = em.HasComponent<UnitPoolLinkComponent>(ctx.GeneralEntity)
                ? em.GetComponentObject<UnitPoolLinkComponent>(ctx.GeneralEntity).PoolKey
                : ctx.GeneralEntity.ToString();
            sb.AppendLine($"[Passive] ▶ {data.SkillName} ({data.Type}) 발동");
            sb.AppendLine($"  general  : {unitName}");
            sb.AppendLine($"  trigger  : {trigger}");
            sb.AppendLine($"  entity   : {ctx.GeneralEntity}");

            // ── 체력 정보
            float maxHp   = em.HasComponent<StatComponent>(ctx.GeneralEntity)
                ? em.GetComponentData<StatComponent>(ctx.GeneralEntity).Final[StatType.MaxHp]
                : 0f;
            float hpRatio = maxHp > 0f ? ctx.Health.CurrentHp / maxHp : 0f;
            sb.AppendLine($"  HP       : {ctx.Health.CurrentHp:F1} / {maxHp:F1}  ({hpRatio * 100f:F1}%)");

            // ── 스텟 정보 (Base → Final)
            if (em.HasComponent<StatComponent>(ctx.GeneralEntity))
            {
                var stat = em.GetComponentData<StatComponent>(ctx.GeneralEntity);
                sb.AppendLine($"  ATK      : {stat.Base[StatType.Attack]:F1} → {stat.Final[StatType.Attack]:F1}");
                sb.AppendLine($"  DEF      : {stat.Base[StatType.Defense]:F2} → {stat.Final[StatType.Defense]:F2}");
                sb.AppendLine($"  ATK SPD  : {stat.Base[StatType.AttackSpeed]:F2} → {stat.Final[StatType.AttackSpeed]:F2}");
                sb.AppendLine($"  MOV SPD  : {stat.Base[StatType.MoveSpeed]:F2} → {stat.Final[StatType.MoveSpeed]:F2}");
            }

            // ── 조건부 패시브 상태
            if (em.HasComponent<PassiveConditionState>(ctx.GeneralEntity))
            {
                var cond = em.GetComponentData<PassiveConditionState>(ctx.GeneralEntity);
                sb.AppendLine($"  IronWill triggered   : {cond.IronWillTriggered}");
                sb.AppendLine($"  LastStand triggered  : {cond.LastStandTriggered}");
                sb.AppendLine($"  InitialSoldierCount  : {cond.InitialSoldierCount}");
            }

            // ── OnSoldierDeath 전용
            if (trigger == PassiveTrigger.OnSoldierDeath)
                sb.AppendLine($"  SoldierDeathCount    : {ctx.SoldierDeathCount}");

            // ── StatModifiers 목록
            if (data.StatModifiers != null && data.StatModifiers.Count > 0)
            {
                sb.AppendLine("  modifiers:");
                foreach (var mod in data.StatModifiers)
                {
                    string fmt = mod.IsPercent ? $"+{mod.Delta * 100f:F1}%" : $"+{mod.Delta:F1}";
                    sb.AppendLine($"    {mod.Stat} {fmt}  target={mod.Target}");
                }
            }

            UnityEngine.Debug.Log(sb.ToString());
        }
#endif
    }
}
