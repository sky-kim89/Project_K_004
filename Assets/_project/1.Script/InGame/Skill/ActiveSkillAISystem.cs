using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// ============================================================
//  ActiveSkillAISystem.cs
//  액티브 스킬 쿨다운이 찼을 때 자동으로 UseActiveSkillTag 를 붙인다.
//
//  ■ 발동 조건
//    - GeneralActiveSkillComponent.IsReady (CooldownRemaining <= 0)
//    - SkillUsePolicy.CanUse — 타겟이 있고, 공격 스킬이면 사거리 안
//    - DeadTag 없음 / 경직(Hit) 아님 / UseActiveSkillTag 중복 아님
//    - **아군 장수는 AUTO 토글이 켜져 있을 때만** (상단바 AUTO 버튼)
//
//  ■ 아군만 토글에 걸린다
//    이 시스템은 적 엘리트도 함께 돈다 (MonsterRuntimeBridge 가 같은 컴포넌트를 붙인다).
//    팀을 안 보고 막으면 AUTO 를 끄는 순간 적 엘리트 스킬까지 멎어
//    전투가 통째로 쉬워진다.
//
//  ■ Burst 를 쓰지 않는다
//    BattleSettingsData.AutoSkillEnabled 는 managed 정적 필드다.
//    대상이 장수·엘리트 몇 기뿐이라 Burst 로 얻을 이득이 없다.
//
//  ■ 흐름
//    이 시스템 → UseActiveSkillTag 추가
//    → ActiveSkillCooldownSystem → ActiveSkillExecuteEvent 버퍼 추가
//    → ActiveSkillExecuteSystem  → Execute(context) 호출
//    (수동 사용은 GeneralPanelUI 가 같은 태그를 직접 붙인다)
// ============================================================

namespace BattleGame.Units
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ActiveSkillCooldownSystem))]
    public partial struct ActiveSkillAISystem : ISystem
    {
        ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GeneralActiveSkillComponent>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            // ⚠ 웨이브가 돌기 전에는 어떤 스킬도 나가지 않는다
            //   Lobby·InGame 두 씬이 동시에 상주하고(SceneDirector) 출전 대기 화면과
            //   로비 배경 데모에도 진짜 유닛이 서 있다. 그래서 이 시스템은 로비에서도
            //   계속 돈다 — 전투가 시작됐는지는 여기서 직접 물어봐야 한다.
            //
            //   예전엔 SkillUsePolicy 의 "타겟이 있어야 한다" 가 우연히 문 노릇을 했다.
            //   버프·소환이 타겟 없이도 나가게 되면서 그 문이 사라졌고, 전투 시작을
            //   누르지도 않은 대기 화면에서 장수들이 쿨마다 버프를 뿌렸다.
            //
            //   IsWaveRunning 은 '적이 나오는 구간' 을 뜻한다 (BattleManager 소유).
            //   웨이브 사이(Preparing·WaveClear)에는 켜진 채라 스킬이 끊기지 않고,
            //   승패가 갈리거나 판을 닫으면 내려간다.
            if (BattleManager.Instance == null || !BattleManager.Instance.IsWaveRunning) return;

            _transformLookup.Update(ref state);

            bool autoAlly = BattleSettingsData.AutoSkillEnabled;
            var  pending  = new NativeList<Entity>(8, Allocator.Temp);

            foreach (var (skill, attack, stat, unitState, transform, identity, entity)
                     in SystemAPI.Query<
                            RefRO<GeneralActiveSkillComponent>,
                            RefRO<AttackComponent>,
                            RefRO<StatComponent>,
                            RefRO<UnitStateComponent>,
                            RefRO<LocalTransform>,
                            RefRO<UnitIdentityComponent>>()
                        .WithNone<DeadTag, UseActiveSkillTag, SkillCastLock>()
                        .WithEntityAccess())
            {
                // 아군 자동 사용이 꺼져 있으면 수동(장수 카드 클릭)으로만 나간다
                //
                // ⚠ 소환 몬스터는 이 설정에서 빼야 한다
                //   그 설정은 "장수 카드를 눌러 직접 쓴다" 를 위한 것인데,
                //   몬스터에는 수동으로 쓸 카드가 없다. 함께 막으면 몬스터의
                //   고유 스킬(멧돼지 돌진 등)이 영영 나가지 않는다.
                if (!autoAlly && identity.ValueRO.Team == TeamType.Ally
                              && identity.ValueRO.Type != UnitType.Monster) continue;

                // ⚠ 스킬 없는 유닛도 이 쿼리에 걸린다
                //   소환 몬스터는 종족에 스킬이 없어도 슬롯을 항상 들고 있다
                //   (풀 재사용 때 구조 변경을 피하려고 그렇게 만들었다).
                //   여기서 거르지 않으면 매 프레임 발동 요청이 들어가 쿨다운만 돌고,
                //   실행기는 없는 SO 를 찾아 헤맨다.
                if (skill.ValueRO.SkillId == (int)ActiveSkillId.None) continue;

                if (!skill.ValueRO.IsReady) continue;                       // 쿨다운 미완료
                if (unitState.ValueRO.Current == UnitState.Hit) continue;   // 속박/스턴 중

                if (!SkillUsePolicy.CanUse(skill.ValueRO.SkillId,
                                           attack.ValueRO,
                                           stat.ValueRO,
                                           transform.ValueRO.Position,
                                           _transformLookup))
                    continue;

                pending.Add(entity);
            }

            // ⚠ 구조 변경(AddComponent)은 반드시 모든 조회가 끝난 뒤에 한다
            //   AddComponent 는 아키타입을 바꾸므로 그 순간 청크가 재배치되고
            //   _transformLookup 을 비롯한 모든 핸들이 무효가 된다.
            //   예전엔 여기서 태그를 붙인 뒤 FireExtraSlots 가 같은 lookup 을
            //   그대로 다시 썼다 — 무효 핸들 접근이라 Burst 잡에서
            //   NullReferenceException 이 터졌다.
            //   그래서 추가 슬롯 판정을 '먼저' 끝내고, 구조 변경을 맨 마지막에 모은다.
            var fired = CollectExtraSlotFires(ref state);

            var em = state.EntityManager;
            for (int i = 0; i < pending.Length; i++)
                em.AddComponent<UseActiveSkillTag>(pending[i]);

            for (int i = 0; i < fired.Length; i++)
            {
                var f = fired[i];
                if (!em.HasBuffer<ActiveSkillExecuteEvent>(f.Caster)) continue;

                em.GetBuffer<ActiveSkillExecuteEvent>(f.Caster).Add(new ActiveSkillExecuteEvent
                {
                    SkillId        = f.SkillId,
                    TargetEntity   = f.Target,
                    TargetPosition = f.TargetPos,
                });
            }

            pending.Dispose();
            fired.Dispose();
        }

        // ── 추가 슬롯 (보스 돌진·분쇄 강타 등) ────────────────────
        //
        //  대표 스킬과 달리 태그를 거치지 않고 여기서 바로 실행 이벤트를 넣는다.
        //  AI 전용이라 수동 발동 경로가 없어 태그를 왕복시킬 이유가 없다.
        //
        //  ⚠ 한 프레임에 슬롯 하나만 발동시킨다
        //    돌진과 강타가 같은 프레임에 터지면 무슨 일이 났는지 안 읽히고,
        //    돌진 이동 중에 강타가 겹쳐 위치가 꼬인다.
        //  ⚠ 여기서는 '무엇을 쏠지' 만 모은다 — 쓰기는 호출한 쪽에서 한다
        //    SystemAPI.Query foreach 안에서 EntityManager.GetBuffer 를 부르면
        //    그 타입의 잡 의존성이 완료되면서 순회 중인 핸들이 무효화될 수 있다.
        //    (같은 이유로 CompleteAllTrackedJobs / CreateEntityQuery 도 금지)
        //
        //  쿨다운 갱신만 여기서 한다 — 순회 대상인 버퍼라 안전하다.
        NativeList<PendingFire> CollectExtraSlotFires(ref SystemState state)
        {
            var fired = new NativeList<PendingFire>(8, Allocator.Temp);

            foreach (var (slotsRO, attack, stat, unitState, transform, entity)
                     in SystemAPI.Query<
                            DynamicBuffer<ActiveSkillSlot>,
                            RefRO<AttackComponent>,
                            RefRO<StatComponent>,
                            RefRO<UnitStateComponent>,
                            RefRO<LocalTransform>>()
                        .WithNone<DeadTag, SkillCastLock>()
                        .WithEntityAccess())
            {
                // ⚠ foreach 분해 변수에는 쓸 수 없다 (CS1654)
                //   DynamicBuffer 는 내부 포인터를 공유하므로 복사해도 같은 메모리다.
                var slots = slotsRO;

                if (slots.Length == 0) continue;
                if (unitState.ValueRO.Current == UnitState.Hit) continue;

                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i].IsReady) continue;

                    if (!SkillUsePolicy.CanUse(slots[i].SkillId,
                                               attack.ValueRO,
                                               stat.ValueRO,
                                               transform.ValueRO.Position,
                                               _transformLookup))
                        continue;

                    // ⚠ TargetEntity 날것 금지 (SkillUsePolicy.ResolveTarget 주석 참고)
                    Entity target    = SkillUsePolicy.ResolveTarget(attack.ValueRO);
                    float3 targetPos = _transformLookup.TryGetComponent(target, out LocalTransform lt)
                        ? lt.Position
                        : transform.ValueRO.Position;

                    // 쿨다운은 버퍼(순회 대상)라 여기서 바로 써도 안전하다
                    var s = slots[i];
                    s.CooldownRemaining = s.Cooldown;
                    slots[i] = s;

                    fired.Add(new PendingFire
                    {
                        Caster    = entity,
                        SkillId   = s.SkillId,
                        Target    = target,
                        TargetPos = targetPos,
                    });
                    break;                       // 이 프레임은 여기까지
                }
            }

            return fired;
        }

        struct PendingFire
        {
            public Entity Caster;
            public int    SkillId;
            public Entity Target;
            public float3 TargetPos;
        }
    }
}
