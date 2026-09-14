using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveSkillExecuteSystem.cs
//  ActiveSkillExecuteEvent 버퍼를 읽어 스킬 Execute() 를 호출하는 관리형 시스템.
//
//  흐름:
//    ActiveSkillCooldownSystem → ActiveSkillExecuteEvent 버퍼에 추가
//    ActiveSkillExecuteSystem  → Execute(context) 호출
//                             → 버퍼 Clear
//
//  왜 managed 시스템인가:
//    ActiveSkillData.Execute() 는 트윈(DOTween 등) / 애니메이션 / MonoBehaviour 에
//    접근할 수 있어야 하므로 Burst 컴파일 대상이 아니다.
// ============================================================

namespace BattleGame.Units
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ActiveSkillCooldownSystem))]
    public partial class ActiveSkillExecuteSystem : SystemBase
    {
        readonly List<(Entity entity, ActiveSkillExecuteEvent evt)> _pending = new();

        protected override void OnUpdate()
        {
            _pending.Clear();

            // ── ① 실행 이벤트 수집 ──────────────────────────────
            Entities
                .WithoutBurst()
                .WithNone<DeadTag>()
                .ForEach((Entity entity,
                          DynamicBuffer<ActiveSkillExecuteEvent> events) =>
                {
                    for (int i = 0; i < events.Length; i++)
                        _pending.Add((entity, events[i]));
                    events.Clear();
                })
                .Run();

            if (_pending.Count == 0) return;

            var db = ActiveSkillDatabase.Current;
            if (db == null)
            {
                Debug.LogWarning("[ActiveSkillExecuteSystem] ActiveSkillDatabase.Current 가 null입니다.");
                return;
            }

            // ── ② 실행 ──────────────────────────────────────────
            foreach (var (entity, evt) in _pending)
            {
                if (!EntityManager.Exists(entity)) continue;

                var skillData = db.Get((ActiveSkillId)evt.SkillId);
                if (skillData == null)
                {
                    // ⚠ 조용히 넘기지 않는다
                    //   여기서 말없이 continue 하면 화면에는 "스킬을 썼는데
                    //   아무 일도 안 일어남" 으로만 보인다. DB 에 SO 가 빠진
                    //   것이 원인인데 그 사실이 어디에도 드러나지 않는다.
                    //   (ActiveSkillCreator 가 DB 를 Clear 로 시작하므로,
                    //    거기 없는 스킬은 다시 구울 때마다 사라진다)
                    Debug.LogError(
                        $"[ActiveSkillExecuteSystem] ActiveSkillDatabase 에 " +
                        $"'{(ActiveSkillId)evt.SkillId}' 가 없습니다 — " +
                        "Tools > Project K > 데이터 생성 > 액티브 스킬 을 다시 실행하세요.");
                    continue;
                }

                // 컨텍스트 구성
                var context = new ActiveSkillContext
                {
                    CasterEntity   = entity,
                    TargetEntity   = evt.TargetEntity,
                    TargetPosition = new Vector3(evt.TargetPosition.x, evt.TargetPosition.y, evt.TargetPosition.z),
                    EntityManager  = EntityManager,
                };

                // StatComponent 스냅샷
                if (EntityManager.HasComponent<StatComponent>(entity))
                    context.CasterStat = EntityManager.GetComponentData<StatComponent>(entity);

                // UnitPoolLinkComponent 에서 GameObject 참조
                if (EntityManager.HasComponent<UnitPoolLinkComponent>(entity))
                {
                    var link = EntityManager.GetComponentObject<UnitPoolLinkComponent>(entity);
                    if (link?.LinkedObject != null)
                    {
                        context.CasterObject    = link.LinkedObject;
                        context.CasterTransform = link.LinkedObject.transform;
                    }
                }

                Debug.Log($"[ActiveSkill] {skillData.SkillName} ({skillData.SkillId}) 발동 | 시전자: {entity.Index} | 타겟: {(context.HasTarget ? context.TargetEntity.Index.ToString() : "없음")}");
                skillData.Execute(context);

                // ── 장비 특이 패시브 '광합성' — 스킬을 쓸 때마다 최대 체력 비율 회복 ──
                //   ⚠ 여기가 "스킬을 실제로 썼다" 가 확정되는 유일한 자리다.
                //     SkillUseEvent 를 따로 읽으면 그 버퍼를 비우는 쪽(CombatTriggerSystem)과
                //     순서를 다퉈야 한다.
                if (EntityManager.HasComponent<BattleGame.Units.PhotosynthesisComponent>(entity) &&
                    EntityManager.HasComponent<StatComponent>(entity))
                {
                    float ratio = EntityManager.GetComponentData<BattleGame.Units.PhotosynthesisComponent>(entity).Ratio;
                    float maxHp = EntityManager.GetComponentData<StatComponent>(entity).Final[StatType.MaxHp];
                    SkillCrowdControl.Heal(EntityManager, entity, maxHp * ratio, entity);
                }

                // OnSkillUse 패시브 트리거용 이벤트 기록
                if (EntityManager.HasBuffer<SkillUseEvent>(entity))
                    EntityManager.GetBuffer<SkillUseEvent>(entity).Add(default);
            }
        }
    }

    // ══════════════════════════════════════════
    // 액티브 스킬 데이터베이스
    // ══════════════════════════════════════════

    // ActiveSkillDatabase 는 별도 파일로 분리해도 되지만
    // 참조 편의를 위해 같은 파일에 배치.
}
