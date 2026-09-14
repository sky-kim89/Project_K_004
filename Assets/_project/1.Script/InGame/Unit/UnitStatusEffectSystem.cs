using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

// ============================================================
//  UnitStatusEffectSystem.cs
//  버프·디버프 처리 + 스텟 재계산 시스템
//
//  매 프레임 한 번, StatusEffectTickJob 이 다음을 수행한다:
//  1. 버프 타이머 감소 → 만료 시 제거
//  2. Dot(도트 데미지) → HitEventBuffer 에 피해 주입
//  3. StatComponent.Final 재계산:
//     Final = Base, 이후 Add 모드 적용, 마지막 Multiply 모드 적용
//  4. MaxHp 증가 시 CurrentHp 동기화 (ComponentLookup 으로 공통 처리)
//
//  Add 먼저, Multiply 나중 순서 이유:
//    공격력 100 + 50(Add) = 150, × 1.3(Multiply) = 195
//    → 순서를 바꾸면 값이 달라지므로 일관성 유지
// ============================================================

namespace BattleGame.Units
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(UnitHitSystem))]
    public partial struct UnitStatusEffectSystem : ISystem
    {
        // [BurstCompile] — GameplayConfig(관리형 객체) 접근이 필요해 Burst 제외
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime  = SystemAPI.Time.DeltaTime;
            float defenseMax = GameplayConfig.Current != null ? GameplayConfig.Current.DefenseMax : 0.95f;
            new StatusEffectTickJob
            {
                DeltaTime      = deltaTime,
                DefenseMax     = defenseMax,
                HealthLookup   = SystemAPI.GetComponentLookup<HealthComponent>(),
                ReactionLookup = SystemAPI.GetComponentLookup<HitReactionComponent>(),
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    [WithNone(typeof(DeadTag))]
    public partial struct StatusEffectTickJob : IJobEntity
    {
        public float DeltaTime;
        public float DefenseMax;

        // HealthComponent 가 없는 엔티티도 처리 가능하도록 Lookup 으로 주입.
        // 병렬 job 에서 엔티티별로 서로 다른 컴포넌트를 쓰므로 실질적 경쟁 없음.
        [NativeDisableParallelForRestriction]
        public ComponentLookup<HealthComponent> HealthLookup;

        /// <summary>
        /// 도트를 받고 있는지 알려 주는 자리. UnitAnimationSync 가 이걸 보고
        /// 몸을 초록빛으로 물들인다 (HitReactionComponent.IsPoisoned 주석).
        ///
        /// ⚠ HealthLookup 과 같은 이유로 Lookup 이다 — 이 컴포넌트가 없는
        ///   엔티티도 버프 처리는 받아야 하므로 Execute 인자에 넣을 수 없다.
        /// </summary>
        [NativeDisableParallelForRestriction]
        public ComponentLookup<HitReactionComponent> ReactionLookup;

        public void Execute(
            Entity                                       entity,
            ref StatComponent                            stat,
            ref DynamicBuffer<StatusEffectBufferElement> buffs,
            ref DynamicBuffer<HitEventBufferElement>     hitBuffer)
        {
            // ── Step 1. 타이머 감소 & 만료 제거 & 도트 데미지 처리 ──
            bool    hasDot  = false;
            DotKind dotLook = DotKind.Burn;   // 가장 약한 우선순위에서 시작한다

            // ⚠ 도트는 프레임마다 **한 건으로 합쳐** 넣는다 (2026-09-11)
            //   스택마다 한 건씩 넣으면 수백 스택이 쌓인 보스가 프레임마다 수백 건의
            //   피해 기록을 만들고, BattleStatCollectorSystem 이 그걸 관리형 코드로
            //   하나씩 훑느라 100ms 를 썼다. 전과는 가장 센 도트의 시전자가 가져간다.
            float  dotDamage = 0f;
            float  dotTop    = 0f;
            Entity dotSource = Entity.Null;

            for (int i = buffs.Length - 1; i >= 0; i--)
            {
                var buff = buffs[i];

                if (buff.Duration >= 0f) // Duration == -1 이면 영구 버프
                    buff.Remaining -= DeltaTime;

                if (buff.Remaining <= 0f)
                {
                    buffs.RemoveAtSwapBack(i);
                    continue;
                }

                // 도트 데미지 — 매 프레임 HitEvent 로 주입
                if (buff.Mode == EffectMode.Dot)
                {
                    // ⚠ 작은 값이 이긴다 — 독(0)이 화상(1)을 덮는다 (DotKind 주석)
                    if (!hasDot || (byte)buff.Dot < (byte)dotLook) dotLook = buff.Dot;
                    hasDot  = true;

                    dotDamage += buff.Delta * DeltaTime;

                    if (buff.Delta > dotTop)
                    {
                        dotTop    = buff.Delta;
                        dotSource = buff.SourceEntity;
                    }
                }

                buffs[i] = buff;
            }

            // 시전자를 그대로 실어 보낸다 — 도트로 잡은 적도 그 부대의 전과가 된다
            // (UnitDeathDespawnSystem.ResolveKillCredit 이 이 값을 본다)
            // ⚠ Type 은 Dot 이다 — Normal 로 두면 틱마다 반응이 터진다 (HitType.Dot 주석)
            if (dotDamage > 0f)
                hitBuffer.Add(new HitEventBufferElement
                {
                    Damage         = dotDamage,
                    HitDirection   = float3.zero,
                    AttackerEntity = dotSource,
                    Type           = HitType.Dot,
                });

            // ── Step 1-b. 도트 상태를 연출 쪽에 알린다 ──
            //   ⚠ 값이 달라졌을 때만 쓴다 — 매 프레임 SetComponentData 를
            //     때리면 바뀐 것도 없이 청크가 계속 더러워진다.
            if (ReactionLookup.HasComponent(entity))
            {
                var reaction = ReactionLookup[entity];
                if (reaction.IsPoisoned != hasDot || (hasDot && reaction.DotLook != dotLook))
                {
                    reaction.IsPoisoned    = hasDot;
                    reaction.DotLook       = dotLook;
                    ReactionLookup[entity] = reaction;
                }
            }

            // ── Step 2. Final = Base 로 초기화 (MaxHp 변화 감지용 기록) ──
            float prevMaxHp = stat.Final[StatType.MaxHp];
            stat.ResetFinalToBase();

            // ── Step 3. Add 모드 버프 적용 ─────────────────────────
            for (int i = 0; i < buffs.Length; i++)
            {
                var buff = buffs[i];
                if (buff.Mode == EffectMode.Add)
                    stat.Final[buff.Stat] += buff.Delta;
            }

            // ── Step 4. Multiply 모드 버프 적용 ────────────────────
            for (int i = 0; i < buffs.Length; i++)
            {
                var buff = buffs[i];
                if (buff.Mode == EffectMode.Multiply)
                    stat.Final[buff.Stat] *= buff.Delta;
            }

            // ── Step 5. MaxHp 증가분만큼 CurrentHp 동기화 ──────────
            // HealthComponent 가 없는 엔티티(병사 외 오브젝트 등)는 안전하게 건너뜀.
            float newMaxHp = stat.Final[StatType.MaxHp];
            if (newMaxHp > prevMaxHp && HealthLookup.HasComponent(entity))
            {
                var hr = HealthLookup.GetRefRW(entity);
                hr.ValueRW.CurrentHp = math.min(
                    hr.ValueRO.CurrentHp + (newMaxHp - prevMaxHp),
                    newMaxHp);
            }

            // ── Step 6. 스텟 하한선 보정 ────────────────────────────
            float defense  = stat.Final[StatType.Defense];
            float atkSpeed = stat.Final[StatType.AttackSpeed];
            float moveSpeed= stat.Final[StatType.MoveSpeed];
            float attack   = stat.Final[StatType.Attack];

            stat.Final[StatType.Defense]     = math.max(defense, 0f); // 소프트캡은 UnitHitSystem에서 적용, 하한만 보정
            stat.Final[StatType.AttackSpeed] = math.max(atkSpeed,     0.1f);
            stat.Final[StatType.MoveSpeed]   = math.max(moveSpeed,    0.1f);
            stat.Final[StatType.Attack]      = math.max(attack,       0f);
        }
    }
}
