using Unity.Burst;
using Unity.Entities;

// ============================================================
//  SkillCastLock.cs
//  스킬 연출이 끝날 때까지 시전자를 붙잡아 두는 잠금.
//
//  ■ 왜 필요한가
//    돌진·강타처럼 몸이 움직이는 패턴은 연출에 시간이 걸린다.
//    그 사이에 이동·공격·다른 스킬이 그대로 돌면
//      · 돌진 중에 이동 잡이 유닛을 뒤로 끌어당겨 궤적이 꺾이고
//      · 예고 동작 중에 평타가 나가 "준비 중" 이라는 신호가 깨지고
//      · 강타 예고 중에 돌진이 겹쳐 위치가 꼬인다.
//    연출 시간만큼 잠가 두면 화면에서 한 동작으로 읽힌다.
//
//  ■ 거는 쪽
//    스킬 Runner 가 연출 시작 시 SkillCastLock.Apply(em, caster, 초) 를 부른다.
//    시간이 지나면 SkillCastLockSystem 이 알아서 뗀다 —
//    Runner 가 직접 제거하지 않는 이유는 연출 도중 유닛이 죽거나
//    풀로 반납될 수 있어서다. 시간 기반이면 그런 경우에도 새지 않는다.
//
//  ■ 막는 것 / 안 막는 것
//    막는다   : 이동, 평타, 추가 스킬 슬롯 발동
//    안 막는다: 피격, 사망, 애니메이션, 버프 갱신
//               (무적이 아니다 — 돌진 중에도 맞아야 한다)
// ============================================================

namespace BattleGame.Units
{
    public struct SkillCastLock : IComponentData
    {
        /// <summary>남은 잠금 시간(초).</summary>
        public float Remaining;
    }

    public static class SkillCastLockUtil
    {
        /// <summary>시전자를 seconds 초 동안 잠근다. 이미 잠겨 있으면 더 긴 쪽을 남긴다.</summary>
        public static void Apply(EntityManager em, Entity caster, float seconds)
        {
            if (seconds <= 0f || !em.Exists(caster)) return;

            if (em.HasComponent<SkillCastLock>(caster))
            {
                var cur = em.GetComponentData<SkillCastLock>(caster);
                if (cur.Remaining >= seconds) return;
                cur.Remaining = seconds;
                em.SetComponentData(caster, cur);
                return;
            }
            em.AddComponentData(caster, new SkillCastLock { Remaining = seconds });
        }
    }

    // ──────────────────────────────────────────────────────────
    //  잠금 해제 시스템
    // ──────────────────────────────────────────────────────────

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(UnitMovementSystem))]
    public partial struct SkillCastLockSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SkillCastLock>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt  = SystemAPI.Time.DeltaTime;
            // EntityCommandBuffer 는 Unity.Entities 다 — Unity.Collections 에는 없다.
            var   ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (mLock, entity) in SystemAPI.Query<RefRW<SkillCastLock>>().WithEntityAccess())
            {
                mLock.ValueRW.Remaining -= dt;
                if (mLock.ValueRO.Remaining <= 0f)
                    ecb.RemoveComponent<SkillCastLock>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
