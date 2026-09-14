using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// ============================================================
//  UnitDeathDespawnSystem.cs
//  DeadTag 가 붙은 Entity 를 감지해 풀로 반납하는 관리형 시스템.
//
//  흐름:
//    ProcessHitEventsJob (Burst) → ECB 로 DeadTag 추가
//    → UnitDeathDespawnSystem (managed) → PoolController.Despawn() 호출
//                                       → BattleManager.OnUnitDead() 호출
//                                       → UnitPoolLinkComponent 제거 (중복 처리 방지)
//
//  왜 Burst 가 아닌 managed 시스템인가:
//    PoolController / BattleManager 는 MonoBehaviour(managed) 이므로
//    Burst Job 내부에서 직접 호출할 수 없다.
//    ProcessHitEventsJob 이 ECB 로 DeadTag 를 추가한 직후 이 시스템이 처리한다.
//
//  UnitPoolLinkComponent:
//    - 스포너가 풀에서 유닛을 꺼낸 뒤 EntityManager 로 추가
//    - PoolKey: 풀 반납 시 사용하는 문자열 키
//    - LinkedObject: 반납할 GameObject 참조
// ============================================================

namespace BattleGame.Units
{
    // ── 유닛 GO ↔ Entity 연결 컴포넌트 (managed) ─────────────
    /// <summary>
    /// 풀에서 꺼낸 유닛 GameObject 와 Entity 를 연결한다.
    /// 스포너가 풀 스폰 직후 EntityManager.AddComponentObject 로 추가.
    /// </summary>
    public class UnitPoolLinkComponent : IComponentData
    {
        public string     PoolKey;       // PoolController 에 등록된 풀 키
        public GameObject LinkedObject;  // 반납할 GameObject
    }

    // ── 사망 감지 + 디스폰 시스템 ────────────────────────────

    // ⚠ 통계 시스템보다 먼저 돌아야 한다
    //   처치자를 알아내려면 피격자의 DamageResultElement(IsKill 이 찍힌 항목)를 읽어야 하는데,
    //   BattleStatCollectorSystem 이 그 버퍼를 다 읽고 **비운다.** 순서를 못 박지 않으면
    //   어떤 프레임엔 처치자가 잡히고 어떤 프레임엔 안 잡히는 식으로 갈린다.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitHitSystem))]
    [UpdateBefore(typeof(BattleStatCollectorSystem))]
    public partial class UnitDeathDespawnSystem : SystemBase
    {
        // GO 반납 목록 — ForEach 외부에서 처리하기 위해 캐싱
        // generalEntity: 병사 사망 시 소속 장군 알림용 (병사 아니면 Entity.Null)
        // deathPos    : 쓰러진 지점 — 순교 등 위치 기반 특성이 SoldierDeathEvent 로 받는다
        readonly System.Collections.Generic.List<(GameObject obj, TeamType team, Entity generalEntity, float3 deathPos)> _pending = new();

        // 이번 프레임에 쓰러진 적 — 순회가 끝난 뒤 처치자를 되짚는다
        //  ⚠ ForEach 람다 안에서 시스템 메서드를 부르지 않는다 (this 캡처 제약)
        readonly System.Collections.Generic.List<(Entity Victim, float3 Position)> _deadEnemies = new();

        protected override void OnUpdate()
        {
            _pending.Clear();
            _deadEnemies.Clear();

            // ── ① 사망 유닛 수집 + 링크 컴포넌트 제거 예약 ─────
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            Entities
                .WithAll<DeadTag>()
                .WithoutBurst()
                .ForEach((Entity entity,
                          UnitPoolLinkComponent link,
                          in UnitIdentityComponent identity,
                          in LocalTransform xform) =>
                {
                    // ForEach 안에서는 GO 반납 금지 (SetActive → EntityLink.OnDisable → AddComponent 구조적 변경 오류)
                    // 대신 목록에 담아 두고 ForEach 완료 후 처리

                    // 병사 사망 시 소속 장군 Entity 캡처
                    Entity generalEntity = Entity.Null;
                    if (identity.Type == UnitType.Soldier
                        && EntityManager.HasComponent<SoldierComponent>(entity))
                    {
                        generalEntity = EntityManager
                            .GetComponentData<SoldierComponent>(entity).GeneralEntity;
                    }

                    _pending.Add((link.LinkedObject, identity.Team, generalEntity, xform.Position));
                    ecb.RemoveComponent<UnitPoolLinkComponent>(entity);

                    if (identity.Team == TeamType.Enemy)
                        _deadEnemies.Add((entity, xform.Position));

                    // ── 골드 — 용사를 잡았다 ─────────────────────
                    //   ⚠ 여기서 주는 이유: 등급(보스·엘리트)을 알 수 있는 마지막 지점이다.
                    //     BattleManager.OnUnitKilled 은 TeamType 하나만 넘겨 주므로
                    //     거기서는 보스와 잡병을 구분할 수 없다.
                    //   ⚠ 이 호출은 한 곳뿐이어야 한다 — 두 곳에서 주면 한 마리가
                    //     두 번 값을 치른다 (RunGoldRule 주석 참고).
                    //   ForEach 가 WithoutBurst().Run() 이라 관리형 호출이 안전하다.
                    // ⚠ 성벽을 통과해 거둬진 용사는 제외한다 (BreachedTag)
                    //   처치가 아니다. 돈까지 주면 "일부러 흘려보내기" 가 이득이 된다.
                    if (identity.Team == Faction.Hero &&
                        !EntityManager.HasComponent<BreachedTag>(entity))
                    {
                        bool isBoss  = EntityManager.HasComponent<BossComponent>(entity);
                        bool isElite = EntityManager.HasComponent<EliteComponent>(entity);

                        int granted = RunGoldRule.GrantForHeroKill(isBoss: isBoss, isElite: isElite);

                        // ⚠ 지급이 먼저, 연출이 나중이다 (장비 상자와 같은 규칙)
                        //   동전은 이미 들어온 골드를 보여 줄 뿐이다. 도착할 때
                        //   지갑에 넣으면 연출 도중 판이 끝났을 때 골드가 사라진다.
                        RunGoldBarUI.Drop(xform.Position, granted);

                        // 특성 '강적의 정수' — 골드와 같은 이유로 여기서 (등급을 아는 마지막 지점)
                        RunPerkRule.OnHeroKilled(isBoss, isElite);
                    }
                })
                .Run();

            ecb.Playback(EntityManager);
            ecb.Dispose();

            // ── ② 적 처치 이벤트 → 잡은 부대의 장군에게만 ─────────
            //
            //  ⚠ 예전엔 아군 장군 전원에게 뿌렸다
            //    적이 죽기만 하면 누가 잡았든 전부에게 이벤트가 갔다. 그래서
            //    "처치 시" 장비(처형자의 낙인·사냥꾼의 장갑)가 남의 부대 전과로 터졌고,
            //    처치 스택 패시브도 자기 부대가 한 일이 아닌 것까지 셌다.
            //    지금은 마지막 일격을 넣은 유닛의 주인 부대에만 들어간다
            //    (장군 본인이 벴든, 그 장군의 병사·소환수가 벴든 그 부대의 전과다).
            foreach (var (victim, victimPos) in _deadEnemies)
            {
                // 역병 술사 — 친화 종족(카드로 낸 좀비 계열)이 마지막 일격이면 그 자리를 적는다.
                //   ⚠ 여기서 곧장 세우지 않는다 — 시스템 도중의 스폰은 구조 변경이다 (SummonerPerkRuntime 주석).
                if (LastHitterHas<PlagueRiserTag>(victim))
                    SummonerPerkRuntime.NotePlagueKill(
                        new UnityEngine.Vector3(victimPos.x, victimPos.y, victimPos.z));

                Entity general = ResolveKillCredit(victim);
                if (general == Entity.Null) continue;
                if (!EntityManager.Exists(general)) continue;
                if (!EntityManager.HasBuffer<EnemyKillEvent>(general)) continue;
                if (EntityManager.HasComponent<DeadTag>(general)) continue;

                // 쓰러진 자리를 같이 넘긴다 — '처형자의 낙인' 같은 처치 소환이
                // 장군 발밑이 아니라 시체 위에서 일어나야 무엇이 일어났는지 보인다.
                EntityManager.GetBuffer<EnemyKillEvent>(general)
                    .Add(new EnemyKillEvent { Position = victimPos });
            }

            // ── ③ 병사 사망 이벤트 → 소속 장군에게 알림 ─────────
            // SoldierDeathEvent 버퍼가 있는 장군에게 사망 이벤트를 추가한다.
            // PassiveSkillRuntimeSystem 이 다음 프레임에 이 버퍼를 처리한다.
            foreach (var (_, _, generalEntity, deathPos) in _pending)
            {
                if (generalEntity == Entity.Null) continue;
                if (!EntityManager.Exists(generalEntity)) continue;
                if (!EntityManager.HasBuffer<SoldierDeathEvent>(generalEntity)) continue;

                EntityManager.GetBuffer<SoldierDeathEvent>(generalEntity)
                    .Add(new SoldierDeathEvent { Position = deathPos });
            }

            // ── ④ ForEach 완료 후 GO 반납 (이 시점은 Entity 순회 밖이므로 안전) ──
            foreach (var (obj, team, generalEntity, _) in _pending)
            {
                // ⚠ 예전엔 여기서 런 누적 사망자 수를 셌다 (혼령 집결이 읽던 값)
                //   혼령 집결이 '전투 내 누적' 으로 바뀌면서 읽는 곳이 없어졌다.
                //   병사 사망은 바로 아래 SoldierDeathEvent 로 이미 알려지고 있으니
                //   (OnSoldierDeath 트리거) 세이브에 카운터를 따로 둘 이유가 없다.

                // 생존 카운트 즉시 갱신 (승패 판정은 연출과 무관하게 바로 처리)
                BattleManager.Instance?.OnUnitDead(team);

                if (obj == null) continue;

                // 디스폰 직전 특성 사망 반응 처리 (K12 영웅의 귀환 등)
                if (obj.TryGetComponent<UnitRuntimeBridge>(out var bridge))
                    bridge.OnBeforeDespawn();

                // UnitAnimationSync 가 있으면 사망 연출(날아가기 + 대기) 후 자체 디스폰.
                // 없으면 즉시 디스폰.
                // activeInHierarchy 체크: SacrificeSoldier 등에서 이미 비활성화된 GO 방어
                var animSync = obj.GetComponent<UnitAnimationSync>();
                if (animSync != null && obj.activeInHierarchy)
                    animSync.TriggerDeath();
                else
                    PoolController.Instance?.Despawn(obj);
            }
        }

        // ── 처치 귀속 ────────────────────────────────────────────

        /// <summary>
        /// 이 적을 잡은 부대의 장군을 돌려준다. 알 수 없으면 Entity.Null.
        ///
        /// 마지막 일격은 UnitHitSystem 이 피격자의 DamageResultElement 에 IsKill 로 찍어 둔다.
        /// 그 공격자가 병사·소환수면 소속 장군으로 올라간다 — 부대 단위 전과다.
        ///
        /// ⚠ 출처 없는 피해는 아무에게도 안 간다
        ///   독 장판처럼 공격자 엔티티가 비어 오는 피해로 죽으면 귀속할 대상이 없다.
        ///   전군에 뿌리던 예전 방식으로 되돌리는 것보다, 그냥 세지 않는 편이 낫다
        ///   ("내가 잡았을 때" 라고 적힌 장비가 남의 전과로 터지는 것이 더 큰 거짓말이다).
        /// </summary>
        /// <summary>마지막 일격을 넣은 공격자가 이 컴포넌트를 갖고 있는가.</summary>
        bool LastHitterHas<T>(Entity victim) where T : unmanaged, IComponentData
        {
            if (!EntityManager.HasBuffer<DamageResultElement>(victim)) return false;

            var results = EntityManager.GetBuffer<DamageResultElement>(victim);
            for (int i = results.Length - 1; i >= 0; i--)
            {
                if (!results[i].IsKill) continue;

                Entity attacker = results[i].AttackerEntity;
                return attacker != Entity.Null
                    && EntityManager.Exists(attacker)
                    && EntityManager.HasComponent<T>(attacker);
            }
            return false;
        }

        Entity ResolveKillCredit(Entity victim)
        {
            if (!EntityManager.HasBuffer<DamageResultElement>(victim)) return Entity.Null;

            var results = EntityManager.GetBuffer<DamageResultElement>(victim);
            for (int i = results.Length - 1; i >= 0; i--)
            {
                if (!results[i].IsKill) continue;

                Entity attacker = results[i].AttackerEntity;
                if (attacker == Entity.Null || !EntityManager.Exists(attacker)) return Entity.Null;

                if (EntityManager.HasComponent<GeneralComponent>(attacker)) return attacker;
                if (EntityManager.HasComponent<SoldierComponent>(attacker))
                    return EntityManager.GetComponentData<SoldierComponent>(attacker).GeneralEntity;

                return Entity.Null;
            }
            return Entity.Null;
        }
    }
}
