using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// ============================================================
//  SkillUsePolicy.cs
//  "지금 이 액티브 스킬을 써도 되는가" 판정의 정본.
//
//  ■ 규칙
//    ⓪ 버프·치유·소환(ActiveSkillId.IsSupport())은 **아무 조건도 보지 않는다.**
//       타겟도 사거리도 따지지 않고 쿨다운만 차면 나간다.
//       ⚠ 예전엔 ② 만 건너뛰고 ① 은 그대로 봤다 — 그래서 적이 시야에서 사라진
//         순간(웨이브 사이·적이 멀리 있을 때) 광전사·치유 오라·소환이 통째로
//         멈췄다. "적이 없으면 버프도 못 건다" 는 규칙이 될 이유가 없다.
//    ① 나머지 스킬은 타겟이 있어야 한다.
//    ② 그 중 공격 스킬은 타겟이 **시전자의 공격 사거리 안** 에 들어와 있어야 한다.
//    ③ 돌진형(ActiveSkillId.IsDash())도 ② 를 건너뛴다.
//       달려가서 때리는 것이 목적인데 사거리 안에서만 나가면 돌진할 거리가
//       남아 있지 않다. ① 의 HasTarget 이 이미 "적이 시야에 있다" 를 뜻하므로
//       (UnitTargetSearchSystem 의 그리드 탐색 반경) 그것으로 충분하다.
//
//    ④ 스킬 사거리는 평타보다 RangeBonus 만큼 넓다.
//       평타와 똑같이 잡았더니 근접 직업이 스킬을 거의 못 썼다 —
//       평타 사거리가 0.7~1.2 라 "붙어 있는 순간"에만 조건이 성립하는데,
//       그 순간은 서로 밀고 밀리느라 오래 유지되지 않는다.
//       쿨타임이 다 찼는데도 발동하지 못하고 흘려보내는 일이 잦았다.
//
//    사거리 판정 방식 자체는 UnitAttackSystem 의 평타 판정과 같다
//    (StatComponent.Final[AttackRange] 기준 거리 제곱 비교).
//    여기서 더해 주는 RangeBonus 는 **의도된 차이**다 — 평타는 못 닿아도
//    스킬은 나간다. 대부분의 스킬이 착탄 지점 기준 범위기라 문제가 없고,
//    돌진형은 애초에 사거리를 보지 않는다.
//
//  ■ 두 진입점이 있는 이유
//    Query   — ActiveSkillAISystem(ECS) 이 매 프레임 자동 발동을 판단할 때.
//              쿼리 순회 중이라 ComponentLookup 으로만 남의 엔티티를 읽는다.
//    Now     — 장수 카드를 눌렀을 때 (GeneralPanelUI, 메인 스레드).
//              EntityManager 를 직접 써도 되는 자리다.
//    같은 규칙을 두 번 적으면 반드시 갈라지므로 판정 본문은 InRange 하나로 모은다.
// ============================================================

namespace BattleGame.Units
{
    public static class SkillUsePolicy
    {
        /// <summary>
        /// 스킬이 평타보다 더 멀리서 나가는 여유 거리.
        ///
        /// ⚠ 배율이 아니라 덧셈이다
        ///   배율로 주면 사거리가 긴 궁수·법사가 훨씬 크게 이득을 본다.
        ///   문제는 근접(0.7~1.2)이 못 쓰는 것이므로, 모두에게 같은 폭을 준다.
        ///   근접은 사실상 두 배가 되고, 원거리는 체감이 거의 없다.
        /// </summary>
        public const float RangeBonus = 1.2f;

        /// <summary>
        /// 이 스킬이 실제로 요구하는 사거리.
        /// ⚠ 두 진입점(Query·Now)이 반드시 이 함수를 거쳐야 한다 —
        ///   한쪽만 고치면 "UI 는 켜지는데 발동은 안 되는" 상태가 된다.
        /// </summary>
        public static float EffectiveRange(float attackRange, ActiveSkillId id)
            => attackRange * id.RangeScale() + RangeBonus;

        /// <summary>ECS 쿼리 순회 중 판정 — 남의 위치는 lookup 으로만 읽는다.</summary>
        public static bool CanUse(int skillId,
                                  in AttackComponent attack,
                                  in StatComponent   stat,
                                  float3             casterPos,
                                  in ComponentLookup<LocalTransform> transformLookup)
        {
            var id = (ActiveSkillId)skillId;

            // 버프·치유·소환은 타겟 유무조차 보지 않는다 (규칙 ①·② 둘 다 건너뜀)
            if (id.IsSupport()) return true;

            if (!attack.HasTarget) return false;

            // 돌진형은 여기서 끝 — HasTarget 이 곧 "적이 시야에 있다" 다.
            if (id.IsDash()) return true;

            if (!transformLookup.TryGetComponent(attack.TargetEntity, out LocalTransform targetT))
                return false;

            float range = EffectiveRange(stat.Final[StatType.AttackRange], id);
            return InRange(casterPos, targetT.Position, range);
        }

        /// <summary>메인 스레드 판정 — 클릭·UI 표시용. 쿨다운·사망·경직까지 함께 본다.</summary>
        public static bool CanUseNow(EntityManager em, Entity general)
        {
            // 전투가 시작되기 전에는 수동 발동도 막는다.
            // ⚠ 자동(ActiveSkillAISystem)만 막으면 규칙이 갈라진다 — 대기 화면에서
            //   장수 카드를 누르면 그것만 나가는 구멍이 생긴다.
            if (BattleManager.Instance == null || !BattleManager.Instance.IsWaveRunning) return false;

            if (!em.Exists(general))                                   return false;
            if (em.HasComponent<DeadTag>(general))                     return false;
            if (em.HasComponent<UseActiveSkillTag>(general))           return false;  // 이미 요청됨
            if (!em.HasComponent<GeneralActiveSkillComponent>(general)) return false;

            if (!em.GetComponentData<GeneralActiveSkillComponent>(general).IsReady) return false;
            if (em.GetComponentData<UnitStateComponent>(general).Current == UnitState.Hit) return false;

            var id = (ActiveSkillId)em.GetComponentData<GeneralActiveSkillComponent>(general).SkillId;
            if (id.IsSupport()) return true;   // 버프·치유·소환 — 타겟도 사거리도 보지 않는다

            var attack = em.GetComponentData<AttackComponent>(general);
            if (!attack.HasTarget) return false;

            if (id.IsDash())    return true;   // 달려가서 때리는 스킬 — 사거리를 보지 않는다

            if (!em.Exists(attack.TargetEntity))                            return false;
            if (em.HasComponent<Disabled>(attack.TargetEntity))             return false;  // 풀에 반납된 유령
            if (!em.HasComponent<LocalTransform>(attack.TargetEntity))      return false;

            float3 casterPos = em.GetComponentData<LocalTransform>(general).Position;
            float3 targetPos = em.GetComponentData<LocalTransform>(attack.TargetEntity).Position;
            float  range     = EffectiveRange(
                em.GetComponentData<StatComponent>(general).Final[StatType.AttackRange], id);

            return InRange(casterPos, targetPos, range);
        }

        /// <summary>
        /// 실행 이벤트에 실어 보낼 타겟. 타겟이 없으면 <b>반드시</b> Entity.Null 이다.
        ///
        /// ⚠ AttackComponent.TargetEntity 를 그대로 쓰면 안 된다
        ///   UnitTargetSearchSystem 은 HasTarget 만 false 로 내리고 TargetEntity 는
        ///   일부러 남겨 둔다 (UnitMovementSystem 이 마지막 목적지로 쓴다).
        ///   버프·소환이 타겟 없이도 나가게 되면서 그 낡은 값이 스킬까지 흘러들었다.
        ///   ActiveSkillContext.HasTarget 은 Null 인지만 보므로, 풀에 반납돼 화면에
        ///   없는 유닛을 '살아 있는 타겟' 으로 알고 그쪽으로 병사를 돌격시킨다.
        /// </summary>
        public static Entity ResolveTarget(in AttackComponent attack)
            => attack.HasTarget ? attack.TargetEntity : Entity.Null;

        static bool InRange(float3 casterPos, float3 targetPos, float range)
            => math.distancesq(casterPos, targetPos) <= range * range;
    }
}
