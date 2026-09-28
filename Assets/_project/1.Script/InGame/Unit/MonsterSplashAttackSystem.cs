using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BattleGame.Units
{
    // ============================================================
    //  MonsterSplashAttackSystem.cs
    //  휩쓸기 — 평타가 주 타겟 **주변까지** 닿게 한다 (SplashAttackComponent).
    //
    //  ■ 왜 UnitAttackSystem 안에서 안 하나
    //    MeleeAttackJob 은 Burst 병렬 잡이고 제 타겟 하나만 안다. 주변을 찾으려면
    //    잡 안에서 전체 유닛을 훑어야 하는데, 그 잡은 **모든 근접 유닛이 매 프레임
    //    지나는 길**이다 — 거기에 O(N) 순회를 얹으면 휩쓸기가 없는 유닛까지 값을 치른다.
    //
    //  ■ BossAttackSystem 과 같은 구조다
    //    "이번 프레임에 때린 유닛" 을 모으고, 후보 배열을 한 번만 떠서 거리로 거른다.
    //    보스는 한 판에 하나뿐이라 매 프레임 후보를 모으지만, 이쪽은 **휩쓸기를 가진
    //    유닛이 실제로 때린 프레임에만** 모은다 (아래 조기 탈출).
    //
    //  ■ ⚠ 주 타겟은 건너뛴다
    //    그 피해는 MeleeAttackJob 이 이미 넣었다. 여기서 또 넣으면 주 대상만
    //    1 + Ratio 배를 맞아 "스플래시가 본체보다 아픈" 평타가 된다.
    //
    //  ■ ⚠ 원거리는 제외한다
    //    AttackedThisFrame 은 원거리 잡도 켠다. 그런데 원거리의 실제 피해는 발사체가
    //    **나중에** 꽂히므로, 쏜 순간 주변을 때리면 화살이 날아가기도 전에 먼저 맞는다.
    //    휩쓸기는 휘두르는 것이라 근접 전용이다.
    //
    //  ■ ⚠ 피해량은 LastDamageDealt 를 쓴다 — 다시 굴리지 않는다
    //    다시 굴리면 치명타가 따로 판정돼 "본체는 안 터졌는데 주변만 치명타" 가 된다.
    //    주변이 받는 것은 **본체가 받은 그 값**의 비율이다.
    // ============================================================

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitAttackSystem))]
    public partial class MonsterSplashAttackSystem : SystemBase
    {
        struct Splasher
        {
            public Entity   Attacker;
            public Entity   MainTarget;
            public float3   Center;
            public float    Damage;
            public float    Radius;
            public TeamType Team;
        }

        struct Candidate
        {
            public Entity   Entity;
            public float3   Position;
            public TeamType Team;
        }

        EntityQuery _candidateQuery;

        protected override void OnCreate()
        {
            _candidateQuery = GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<UnitIdentityComponent>(),
                ComponentType.Exclude<DeadTag>());

            // 휩쓸기를 가진 유닛이 하나도 없으면 이 시스템은 아예 돌지 않는다.
            RequireForUpdate<SplashAttackComponent>();
        }

        protected override void OnUpdate()
        {
            var pending = new NativeList<Splasher>(4, Allocator.Temp);

            // ── ① 이번 프레임에 때린 휩쓸기 유닛을 모은다 ──
            foreach (var (attack, splash, identity, entity)
                     in SystemAPI.Query<
                            RefRO<AttackComponent>,
                            RefRO<SplashAttackComponent>,
                            RefRO<UnitIdentityComponent>>()
                        .WithNone<DeadTag, RangedTag>()
                        .WithEntityAccess())
            {
                if (!attack.ValueRO.AttackedThisFrame)      continue;
                if (attack.ValueRO.LastDamageDealt <= 0f)   continue;
                if (splash.ValueRO.Radius <= 0f)            continue;
                if (splash.ValueRO.Ratio  <= 0f)            continue;

                pending.Add(new Splasher
                {
                    Attacker   = entity,
                    MainTarget = attack.ValueRO.TargetEntity,

                    // ⚠ 중심은 **타겟** 자리다 — 시전자가 아니다
                    //   시전자를 중심으로 잡으면 등 뒤의 적까지 맞는다.
                    //   휘두른 곳이 곧 닿는 곳이라야 화면과 판정이 같은 말을 한다.
                    Center = attack.ValueRO.TargetPosition,
                    Damage = attack.ValueRO.LastDamageDealt * splash.ValueRO.Ratio,
                    Radius = splash.ValueRO.Radius,
                    Team   = identity.ValueRO.Team,
                });
            }

            if (pending.Length == 0) { pending.Dispose(); return; }

            // ── ② 후보를 한 번만 뜬다 ──
            var positions = _candidateQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var ids       = _candidateQuery.ToComponentDataArray<UnitIdentityComponent>(Allocator.Temp);
            var entities  = _candidateQuery.ToEntityArray(Allocator.Temp);

            var candidates = new NativeArray<Candidate>(entities.Length, Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
                candidates[i] = new Candidate
                {
                    Entity   = entities[i],
                    Position = positions[i].Position,
                    Team     = ids[i].Team,
                };

            positions.Dispose();
            ids.Dispose();
            entities.Dispose();

            // ── ③ 반경 안의 적에게 스플래시 ──
            for (int s = 0; s < pending.Length; s++)
            {
                Splasher sp  = pending[s];
                float    sqr = sp.Radius * sp.Radius;

                for (int c = 0; c < candidates.Length; c++)
                {
                    Candidate cand = candidates[c];

                    if (cand.Team == sp.Team)         continue;   // 아군은 안 맞는다
                    if (cand.Entity == sp.MainTarget) continue;   // 주 타겟은 이미 맞았다

                    if (math.distancesq(cand.Position, sp.Center) > sqr) continue;
                    if (!SystemAPI.HasBuffer<HitEventBufferElement>(cand.Entity)) continue;

                    SystemAPI.GetBuffer<HitEventBufferElement>(cand.Entity)
                             .Add(new HitEventBufferElement
                    {
                        Damage         = sp.Damage,
                        HitDirection   = math.normalizesafe(cand.Position - sp.Center),
                        AttackerEntity = sp.Attacker,

                        // ⚠ 평타(Normal)로 넣는다 — 가시·중독 전이 같은 피격 반응이
                        //   본체와 똑같이 돌아야 "같은 한 방" 으로 읽힌다.
                        Type = HitType.Normal,
                    });
                }
            }

            candidates.Dispose();
            pending.Dispose();
        }
    }
}
