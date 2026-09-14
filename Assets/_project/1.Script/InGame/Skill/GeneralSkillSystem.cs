using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// ============================================================
//  GeneralSkillSystem.cs
//  장군 관련 시스템 3종
//
//  ① SoldierSpawnSystem          [InitializationSystemGroup]
//     - SpawnSoldiersRequest 가 붙은 장군 Entity 를 찾아
//       지정 프리팹을 Count 만큼 인스턴스화
//     - 장군 StatComponent.Final 을 기반으로 병사 StatComponent.Base 를 결정
//       (비율 적용: 병사 스텟 = 장군 스텟 × StatScaleRatio)
//     - 처리 완료 후 SpawnSoldiersRequest 제거
//
//  ② PassiveSkillAuraSystem       [SimulationSystemGroup]
//     - 1초마다 실행
//     - GeneralPassiveSkillComponent 를 가진 장군의 CommandRadius 내
//       소속 병사에게 StatusEffectBufferElement 갱신
//     - 버프 Duration = 2초, 1초마다 갱신 → 만료 없이 유지
//       장군 사망·범위 이탈 시 2초 후 자연 만료
//
//  ③ ActiveSkillCooldownSystem    [SimulationSystemGroup]
//     - 매 프레임 쿨다운 감소
//     - UseActiveSkillTag 발동 시: 쿨다운 리셋 + 태그 제거
//       실제 스킬 실행(트윈·이동·공격 제어)은 별도 스킬 실행기에서 수행
// ============================================================

namespace BattleGame.Units
{
    // ══════════════════════════════════════════
    // ① 병사 스폰 시스템
    // ══════════════════════════════════════════

    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SoldierSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnSoldiersRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, identity, stat, generalEntity)
                     in SystemAPI.Query<
                            RefRO<SpawnSoldiersRequest>,
                            RefRO<UnitIdentityComponent>,
                            RefRO<StatComponent>>()
                        .WithEntityAccess())
            {
                // SoldierCount — Authoring 기준값 + 어빌리티 가산분 (A16/B13 등)
                StatBlock generalStat  = stat.ValueRO.Final;
                float     statCount    = generalStat[StatType.SoldierCount];
                int       count        = request.ValueRO.Count + (int)math.max(0f, statCount);

                // CommandPower — 1포인트당 병사 스텟 +1%p 가산 (상한 없음).
                // ⚠ 공식은 SoldierRuntimeBridge.StatRatio 와 같은 형태를 유지할 것
                //   (여기는 Burst 라 GameplayConfig 를 읽지 못해 Authoring 값을 기준으로 쓴다)
                float commandPower = generalStat[StatType.CommandPower];
                float ratio        = math.max(0f, request.ValueRO.StatScaleRatio + commandPower * 0.01f);

                Entity   prefab = request.ValueRO.SoldierPrefab;
                TeamType team   = identity.ValueRO.Team;

                for (int i = 0; i < count; i++)
                {
                    Entity soldier = ecb.Instantiate(prefab);

                    // 소속 정보 갱신
                    ecb.SetComponent(soldier, new UnitIdentityComponent
                    {
                        UnitId = i,
                        Team   = team,
                        Type   = UnitType.Soldier,
                    });

                    // 장군 스텟 × 비율로 병사 Base 스텟 결정
                    // 사거리(AttackRange)는 비율 적용 제외 — 진형 설계상 동일 사거리 사용
                    var soldierBase = new StatBlock();
                    soldierBase[StatType.MaxHp]       = generalStat[StatType.MaxHp]       * ratio;
                    soldierBase[StatType.Defense]     = generalStat[StatType.Defense]     * ratio;
                    soldierBase[StatType.Attack]      = generalStat[StatType.Attack]      * ratio;
                    soldierBase[StatType.AttackRange] = generalStat[StatType.AttackRange];
                    soldierBase[StatType.AttackSpeed] = generalStat[StatType.AttackSpeed] * ratio;
                    soldierBase[StatType.MoveSpeed]   = generalStat[StatType.MoveSpeed]   * ratio;
                    soldierBase[StatType.CritChance]  = generalStat[StatType.CritChance];
                    soldierBase[StatType.CritDamage]  = generalStat[StatType.CritDamage];

                    ecb.SetComponent(soldier, new StatComponent
                    {
                        Base  = soldierBase,
                        Final = soldierBase,  // 첫 버프 계산 전까지 Final = Base
                    });

                    // 체력 초기화
                    ecb.SetComponent(soldier, new HealthComponent
                    {
                        CurrentHp = soldierBase[StatType.MaxHp],
                    });

                    // 소속 장군 링크
                    ecb.SetComponent(soldier, new SoldierComponent
                    {
                        GeneralEntity  = generalEntity,
                        StatScaleRatio = ratio,
                        IsInitialized  = true,
                    });

                    // 병사별 고유 랜덤 시드 (크리티컬 판정용)
                    ecb.SetComponent(soldier, new AttackComponent
                    {
                        AttackCooldown = 0f,
                        HasTarget      = false,
                        RandomSeed     = (uint)(i + 1) * 2654435761u,
                    });
                }

                // 요청 제거 (1회성)
                ecb.RemoveComponent<SpawnSoldiersRequest>(generalEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    // ══════════════════════════════════════════
    // ② 패시브 버프 오라 시스템
    // ══════════════════════════════════════════

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PassiveSkillAuraSystem : ISystem
    {
        // 1초마다 버프를 갱신하고, 버프 지속시간은 2초로 설정한다.
        // → 갱신이 한 번 누락되면 2초 후 자연 만료 (장군 사망·범위 이탈 처리)
        const float RefreshInterval = 1f;
        const float BuffDuration    = 2f;

        float _timer;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GeneralPassiveSkillComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _timer += SystemAPI.Time.DeltaTime;
            if (_timer < RefreshInterval) return;
            _timer = 0f;

            // ── Step 1. 살아 있는 장군의 오라 정보를 수집 ────────────
            var auraList = new NativeList<AuraInfo>(8, Allocator.Temp);

            foreach (var (identity, passive, general, transform)
                     in SystemAPI.Query<
                            RefRO<UnitIdentityComponent>,
                            RefRO<GeneralPassiveSkillComponent>,
                            RefRO<GeneralComponent>,
                            RefRO<LocalTransform>>()
                        .WithNone<DeadTag>())
            {
                // AuraRadius 가 0 이면 지휘 반경(CommandRadius) 전체를 적용
                float radius = passive.ValueRO.AuraRadius > 0f
                    ? passive.ValueRO.AuraRadius
                    : general.ValueRO.CommandRadius;

                auraList.Add(new AuraInfo
                {
                    Team      = identity.ValueRO.Team,
                    Position  = transform.ValueRO.Position,
                    Radius    = radius,
                    Stat      = passive.ValueRO.BuffStat,
                    Delta     = passive.ValueRO.BuffValue,
                    Mode      = EffectMode.Add,
                });
            }

            if (auraList.Length == 0)
            {
                auraList.Dispose();
                return;
            }

            // ── Step 2. 살아 있는 병사마다 오라 범위 안에 있는지 확인 ──
            foreach (var (identity, transform, buffersRO)
                     in SystemAPI.Query<
                            RefRO<UnitIdentityComponent>,
                            RefRO<LocalTransform>,
                            DynamicBuffer<StatusEffectBufferElement>>()
                        .WithAll<SoldierComponent>()
                        .WithNone<DeadTag>())
            {
                // DynamicBuffer 는 내부 포인터를 공유하므로 복사해도 같은 메모리를 가리킨다.
                // foreach 분해식 변수는 C# 컴파일러가 쓰기를 막으므로 로컬 변수로 받는다.
                var activeBuffs = buffersRO;

                for (int i = 0; i < auraList.Length; i++)
                {
                    AuraInfo aura = auraList[i];

                    // 다른 팀 장군의 오라는 무시
                    if (aura.Team != identity.ValueRO.Team)
                        continue;

                    // 오라 반경 밖에 있으면 무시
                    float distSq = math.distancesq(transform.ValueRO.Position, aura.Position);
                    if (aura.Radius > 0f && distSq > aura.Radius * aura.Radius)
                        continue;

                    // ── Step 3. 이미 같은 버프가 있으면 지속시간만 갱신,
                    //           없으면 새로 추가 ──────────────────────────
                    bool alreadyHasBuff = false;

                    for (int j = 0; j < activeBuffs.Length; j++)
                    {
                        StatusEffectBufferElement existingBuff = activeBuffs[j];

                        bool sameStat  = existingBuff.Stat == aura.Stat;
                        bool sameMode  = existingBuff.Mode == aura.Mode;
                        bool sameDelta = math.abs(existingBuff.Delta - aura.Delta) < 0.01f;

                        if (sameStat && sameMode && sameDelta)
                        {
                            existingBuff.Remaining = BuffDuration;
                            existingBuff.Duration  = BuffDuration;
                            activeBuffs[j]         = existingBuff;
                            alreadyHasBuff         = true;
                            break;
                        }
                    }

                    if (!alreadyHasBuff)
                    {
                        activeBuffs.Add(new StatusEffectBufferElement
                        {
                            Stat      = aura.Stat,
                            Delta     = aura.Delta,
                            Mode      = aura.Mode,
                            Duration  = BuffDuration,
                            Remaining = BuffDuration,
                        });
                    }
                }
            }

            auraList.Dispose();
        }
    }

    internal struct AuraInfo
    {
        public TeamType   Team;
        public float3     Position;
        public float      Radius;
        public StatType   Stat;    // 버프 대상 스텟
        public float      Delta;   // 버프 수치
        public EffectMode Mode;    // Add / Multiply
    }

    // ══════════════════════════════════════════
    // ③ 액티브 스킬 쿨다운 시스템
    // ══════════════════════════════════════════

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ActiveSkillCooldownSystem : ISystem
    {
        ComponentLookup<LocalTransform>            _transformLookup;
        BufferLookup<ActiveSkillExecuteEvent>      _executeLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GeneralActiveSkillComponent>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _executeLookup   = state.GetBufferLookup<ActiveSkillExecuteEvent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _executeLookup.Update(ref state);
            float dt = SystemAPI.Time.DeltaTime;

            // 쿨다운 감소 — 대표 스킬
            foreach (var skill in SystemAPI.Query<RefRW<GeneralActiveSkillComponent>>()
                                           .WithNone<DeadTag>())
            {
                if (skill.ValueRO.CooldownRemaining > 0f)
                    skill.ValueRW.CooldownRemaining -= dt;
            }

            // 쿨다운 감소 — 추가 슬롯(패턴 등). 슬롯마다 따로 돈다.
            //
            // ⚠ foreach 분해 변수에는 쓸 수 없다 (CS1654)
            //   DynamicBuffer 는 내부 포인터를 공유하므로 로컬 변수로 받아도
            //   같은 메모리를 가리킨다. 로컬로 옮겨야 컴파일러가 쓰기를 허용한다.
            //   (PassiveSkillAuraSystem 이 같은 이유로 같은 처리를 한다)
            foreach (var slotsRO in SystemAPI.Query<DynamicBuffer<ActiveSkillSlot>>()
                                             .WithNone<DeadTag>())
            {
                var slots = slotsRO;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].CooldownRemaining <= 0f) continue;
                    var s = slots[i];
                    s.CooldownRemaining -= dt;
                    slots[i] = s;
                }
            }

            // UseActiveSkillTag 처리 — 쿨다운 확인 + 실행 이벤트 버퍼에 추가
            // 실제 스킬 실행(트윈·이동·공격 제어)은 ActiveSkillExecuteSystem(managed) 에서 수행
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (skill, attack, entity)
                     in SystemAPI.Query<
                            RefRW<GeneralActiveSkillComponent>,
                            RefRO<AttackComponent>>()
                        .WithAll<UseActiveSkillTag>()
                        .WithNone<DeadTag>()
                        .WithEntityAccess())
            {
                ecb.RemoveComponent<UseActiveSkillTag>(entity);

                if (!skill.ValueRO.IsReady)
                    continue;  // 쿨다운 미완료 — 요청 무시

                skill.ValueRW.CooldownRemaining = skill.ValueRO.Cooldown;

                // 타겟 위치를 이 시점에 스냅샷 — 이후 타겟이 사망해도 착탄 위치가 유지됨
                // ⚠ TargetEntity 를 날것으로 쓰지 않는다 (SkillUsePolicy.ResolveTarget 주석 참고)
                // ⚠ 타겟이 없을 때 float3.zero 로 두지 않는다 (2026-09-04)
                //   원점은 전장 한복판이 아니라 **월드 (0,0,0)** 이다. 타겟이
                //   없는 채로 발동하면 메테오·비석이 화면 밖 엉뚱한 자리에
                //   떨어져 "스킬이 안 나갔다" 로 보인다.
                //   AI 경로(ActiveSkillAISystem)는 원래부터 시전자 자리로
                //   떨어뜨리고 있었다 — 두 경로가 달랐던 것이 버그였다.
                Entity targetEnt = SkillUsePolicy.ResolveTarget(attack.ValueRO);

                float3 casterPos = _transformLookup.TryGetComponent(entity, out LocalTransform self)
                    ? self.Position
                    : float3.zero;

                float3 targetPos = _transformLookup.TryGetComponent(targetEnt, out LocalTransform lt)
                    ? lt.Position
                    : casterPos;

                // ⚠ 버퍼가 없으면 **적지 않는다** — AppendToBuffer 는 그냥 죽는다
                //   ECB 재생 중에 "Buffer does not exist on entity, cannot append
                //   element" 로 터지고, 스택은 Playback 만 가리켜 어느 엔티티가
                //   범인인지 알 수 없다. (실제로 몬스터에게 액티브 스킬을 붙이면서
                //   버퍼를 안 붙여 이 사고가 났다 — MonsterRuntimeBridge 참고)
                //   AI 경로(ActiveSkillAISystem)는 원래 이 검사를 하고 있었다.
                if (!_executeLookup.HasBuffer(entity)) continue;

                // 실행 이벤트 버퍼에 추가 → ActiveSkillExecuteSystem 이 다음에 처리
                ecb.AppendToBuffer(entity, new ActiveSkillExecuteEvent
                {
                    SkillId        = skill.ValueRO.SkillId,
                    TargetEntity   = targetEnt,
                    TargetPosition = targetPos,
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
