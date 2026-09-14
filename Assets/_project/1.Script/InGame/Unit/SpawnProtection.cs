using Unity.Burst;
using Unity.Entities;

// ============================================================
//  SpawnProtection.cs
//  "나오는 중" 인 유닛을 잠깐 무적으로 둔다 (소환 연출용).
//
//  ■ 왜 필요한가
//    스켈레톤은 땅에서 일어나는 연출(UnitAnimationSync.PlayRise) 동안
//    웅크린 자세로 서 있다. 그 사이에 맞아 죽으면 "소환되자마자 사라진" 것으로
//    보이고, 반대로 웅크린 채 때리면 자세와 행동이 따로 논다.
//
//  ■ 이동·공격은 SkillCastLock 이 이미 막는다
//    같은 일을 두 번 만들지 않는다. 여기서는 **피격만** 막는다.
//      SkillCastLock   → 이동·평타·추가 스킬 잠금 (기존)
//      SpawnProtection → 피해 무효 (여기)
//
//  ■ InvulnerableTag 를 붙이지 않는다 — 소유권이 겹친다
//    그 태그는 로비 데모 전투가 아군에게 '영구' 로 달아 둔다. 여기서 시간이
//    다 됐다고 떼면 남의 것을 뜯는 셈이라, 데모 아군이 갑자기 죽을 수 있다.
//    대신 피해를 넣는 자리(UnitHitSystem)에서 이 컴포넌트를 함께 본다 —
//    붙인 쪽이 뗄 책임까지 갖는 구조가 된다.
//
//  ■ 시간 기반이다 — 상태 기반이 아니다
//    연출 도중에 유닛이 죽거나 풀로 반납될 수 있다. "연출이 끝나면 푼다" 로 짜면
//    그 경우 무적이 남은 채 재사용돼 **죽지 않는 유닛**이 된다.
//    남은 시간이 0 이 되면 시스템이 스스로 떼어 내므로 그런 구멍이 없다.
//    (SkillCastLock 이 같은 이유로 시간 기반이다)
// ============================================================

namespace BattleGame.Units
{
    /// <summary>남은 무적 시간(초). 0 이 되면 InvulnerableTag 와 함께 제거된다.</summary>
    public struct SpawnProtection : IComponentData
    {
        public float Remaining;
    }

    public static class SpawnProtectionUtil
    {
        /// <summary>seconds 초 동안 피격을 무효로 만든다. 이미 걸려 있으면 더 긴 쪽을 남긴다.</summary>
        public static void Apply(EntityManager em, Entity entity, float seconds)
        {
            if (seconds <= 0f || !em.Exists(entity)) return;

            if (em.HasComponent<SpawnProtection>(entity))
            {
                var cur = em.GetComponentData<SpawnProtection>(entity);
                if (cur.Remaining < seconds)
                {
                    cur.Remaining = seconds;
                    em.SetComponentData(entity, cur);
                }
            }
            else
            {
                em.AddComponentData(entity, new SpawnProtection { Remaining = seconds });
            }

        }
    }

    // ──────────────────────────────────────────────────────────
    //  해제 시스템
    // ──────────────────────────────────────────────────────────

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(UnitHitSystem))]
    public partial struct SpawnProtectionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnProtection>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt  = SystemAPI.Time.DeltaTime;
            var   ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (prot, entity) in SystemAPI.Query<RefRW<SpawnProtection>>().WithEntityAccess())
            {
                prot.ValueRW.Remaining -= dt;
                if (prot.ValueRO.Remaining > 0f) continue;

                ecb.RemoveComponent<SpawnProtection>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
