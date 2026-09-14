using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;


// ============================================================
//  UnitHitSystem.cs
//  피격 처리 시스템
//  - HitEventBuffer 에 쌓인 이벤트를 읽어 HP 차감
//  - Defense 는 StatComponent.Final[StatType.Defense] 에서 읽음
//  - 사망 시 DeadTag 부착 (HealthComponent.IsDead 필드 제거됨)
// ============================================================

namespace BattleGame.Units
{
    /// <summary>
    /// 방어율 → 실제 피해 환산 공식. Burst 안에서도 쓸 수 있도록 순수 static.
    ///
    /// ⚠ 예약 피해(ProjectileIncomingDamageSystem)와 실제 피해(ProjectileHitJob)가
    ///   반드시 같은 값을 내야 한다 — 예약이 실제보다 크면 죽지 않은 적이 영영
    ///   타겟에서 제외되어 전투가 멈춘다. 두 곳 모두 이 함수만 쓸 것.
    /// </summary>
    public static class DamageMath
    {
        /// <param name="pierce">
        /// 공격자의 방어율 관통 (0~1). 소프트캡·상한을 모두 적용한 <b>최종</b> 방어율에서
        /// 뺀다 — 관통이 소프트캡 공식을 거꾸로 타고 증폭되지 않게 하려는 것이다.
        /// </param>
        public static float AfterDefense(float rawDamage, float rawDefense, float pierce,
                                         float softCap, float overflowRate, float effectiveCap)
        {
            float eff = rawDefense <= softCap
                ? rawDefense
                : softCap + (rawDefense - softCap) * overflowRate;
            float defense = math.min(eff, effectiveCap);
            defense = math.max(0f, defense - math.saturate(pierce));
            return math.max(rawDamage * (1f - defense), 1f);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitAttackSystem))]
    public partial struct UnitHitSystem : ISystem
    {
        ComponentLookup<BossComponent>        _bossLookup;
        ComponentLookup<EliteComponent>       _eliteLookup;
        ComponentLookup<RetaliateComponent>   _retaliateLookup;
        ComponentLookup<InflictOnHitComponent> _inflictLookup;
        ComponentLookup<KnockbackImmuneTag>   _knockbackImmuneLookup;
        ComponentLookup<KnockbackPowerComponent> _knockbackPowerLookup;
        ComponentLookup<SummonerStrikeComponent> _summonerStrikeLookup;
        ComponentLookup<WallCoverComponent>   _wallCoverLookup;
        ComponentLookup<InvulnerableTag>      _invulnerableLookup;
        ComponentLookup<SpawnProtection>      _spawnProtectionLookup;
        // 몬스터 시너지 금 — 강철(피해 상한) · 재생(치명상 버티기)
        ComponentLookup<SynergyDamageCapComponent> _damageCapLookup;
        ComponentLookup<RendComponent>             _rendLookup;
        ComponentLookup<SynergyLastStandComponent> _lastStandLookup;
        // 장비 특이 패시브 — 반동(때린 쪽 회복) · 성벽(받는 피해 배율)
        ComponentLookup<RecoilComponent>           _recoilLookup;
        ComponentLookup<DamageTakenMultComponent>  _damageTakenLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _recoilLookup          = state.GetComponentLookup<RecoilComponent>(isReadOnly: true);
            _damageTakenLookup     = state.GetComponentLookup<DamageTakenMultComponent>(isReadOnly: true);
            _bossLookup            = state.GetComponentLookup<BossComponent>(isReadOnly: true);
            _eliteLookup           = state.GetComponentLookup<EliteComponent>(isReadOnly: true);
            _retaliateLookup       = state.GetComponentLookup<RetaliateComponent>(isReadOnly: true);
            _inflictLookup         = state.GetComponentLookup<InflictOnHitComponent>(isReadOnly: true);
            _knockbackImmuneLookup = state.GetComponentLookup<KnockbackImmuneTag>(isReadOnly: true);
            _knockbackPowerLookup  = state.GetComponentLookup<KnockbackPowerComponent>(isReadOnly: true);
            _summonerStrikeLookup  = state.GetComponentLookup<SummonerStrikeComponent>(isReadOnly: true);
            _wallCoverLookup       = state.GetComponentLookup<WallCoverComponent>(isReadOnly: true);
            _invulnerableLookup    = state.GetComponentLookup<InvulnerableTag>(isReadOnly: true);
            _spawnProtectionLookup = state.GetComponentLookup<SpawnProtection>(isReadOnly: true);
            _damageCapLookup       = state.GetComponentLookup<SynergyDamageCapComponent>(isReadOnly: true);
            _rendLookup            = state.GetComponentLookup<RendComponent>(isReadOnly: true);
            _lastStandLookup       = state.GetComponentLookup<SynergyLastStandComponent>(isReadOnly: true);
        }

        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _bossLookup.Update(ref state);
            _eliteLookup.Update(ref state);
            _retaliateLookup.Update(ref state);
            _inflictLookup.Update(ref state);
            _knockbackImmuneLookup.Update(ref state);
            _knockbackPowerLookup.Update(ref state);
            _summonerStrikeLookup.Update(ref state);
            _wallCoverLookup.Update(ref state);
            _invulnerableLookup.Update(ref state);
            _spawnProtectionLookup.Update(ref state);
            _damageCapLookup.Update(ref state);
            _rendLookup.Update(ref state);
            _lastStandLookup.Update(ref state);
            // ⚠ 선언 · Update · 잡 대입 셋 다 해야 한다 — 하나만 빠지면 조용히 무효 lookup 이다
            _recoilLookup.Update(ref state);
            _damageTakenLookup.Update(ref state);

            var cfg = GameplayConfig.Current;
            if (cfg == null) return;

            float softCap      = cfg.DefenseMax;
            float overflowRate = cfg.DefenseOverflowRate;
            float effectiveCap = cfg.DefenseEffectiveCap;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new ProcessHitEventsJob
            {
                Ecb                   = ecb,
                BossLookup            = _bossLookup,
                EliteLookup           = _eliteLookup,
                RetaliateLookup       = _retaliateLookup,
                InflictLookup         = _inflictLookup,
                KnockbackImmuneLookup = _knockbackImmuneLookup,
                KnockbackPowerLookup  = _knockbackPowerLookup,
                SummonerStrikeLookup  = _summonerStrikeLookup,
                WallCoverLookup       = _wallCoverLookup,
                InvulnerableLookup    = _invulnerableLookup,
                SpawnProtectionLookup = _spawnProtectionLookup,
                DamageCapLookup       = _damageCapLookup,
                RendLookup            = _rendLookup,
                LastStandLookup       = _lastStandLookup,
                RecoilLookup          = _recoilLookup,
                DamageTakenLookup     = _damageTakenLookup,
                DefenseSoftCap        = softCap,
                DefenseOverflowRate   = overflowRate,
                DefenseEffectiveCap   = effectiveCap,
            }.ScheduleParallel();
        }
    }

    // ──────────────────────────────────────────
    // 피격 이벤트 처리 Job
    // ──────────────────────────────────────────

    [BurstCompile]
    [WithNone(typeof(DeadTag))]
    public partial struct ProcessHitEventsJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter               Ecb;
        [ReadOnly] public ComponentLookup<BossComponent>        BossLookup;
        [ReadOnly] public ComponentLookup<EliteComponent>       EliteLookup;
        // 종족 패시브 — 피격 시 때린 상대에게 되돌려주는 반응 (독 슬라임·서리 늑대·가시)
        [ReadOnly] public ComponentLookup<RetaliateComponent>   RetaliateLookup;
        // 종족 패시브 — 때린 쪽이 맞은 쪽에게 거는 상태효과 (화상 등)
        [ReadOnly] public ComponentLookup<InflictOnHitComponent> InflictLookup;
        [ReadOnly] public ComponentLookup<KnockbackImmuneTag>   KnockbackImmuneLookup;

        // 소환사 '패기' — 때린 쪽에 구워져 있는 평타 넉백 배율 (SummonerVigorRule).
        //  ⚠ 여기서 소환사를 조회할 수 없다 (Burst 잡). 스폰 때 MonsterSpawner 가 박아 준다.
        [ReadOnly] public ComponentLookup<KnockbackPowerComponent> KnockbackPowerLookup;

        // 소환사 평타 — 대상 최대 체력 비례 (SummonerStrikeComponent 머리 주석).
        [ReadOnly] public ComponentLookup<SummonerStrikeComponent> SummonerStrikeLookup;
        // 성벽 뒤에 선 마왕 — 피격 연출을 본체가 아니라 성벽이 받는다
        [ReadOnly] public ComponentLookup<WallCoverComponent>   WallCoverLookup;
        [ReadOnly] public ComponentLookup<InvulnerableTag>      InvulnerableLookup;
        // 소환 연출 중(땅에서 일어나는 중)인 유닛 — 잠깐 피해를 받지 않는다
        [ReadOnly] public ComponentLookup<SpawnProtection>      SpawnProtectionLookup;

        // 몬스터 시너지 금 — 값이 컴포넌트에 구워져 있다.
        //  ⚠ 여기서 MonsterSynergyRule 을 읽을 수 없다 (Burst 잡이라 관리형 static 불가).
        //    스폰 때 MonsterSynergyRuntime 이 단계를 계산해 컴포넌트로 박아 준다.
        [ReadOnly] public ComponentLookup<SynergyDamageCapComponent> DamageCapLookup;
        [ReadOnly] public ComponentLookup<RendComponent>             RendLookup;
        [ReadOnly] public ComponentLookup<SynergyLastStandComponent> LastStandLookup;

        // 장비 특이 패시브 — 값이 스폰 때 구워져 있다 (SpeciesPassiveRuntime)
        [ReadOnly] public ComponentLookup<RecoilComponent>          RecoilLookup;
        [ReadOnly] public ComponentLookup<DamageTakenMultComponent> DamageTakenLookup;
        public float DefenseSoftCap;
        public float DefenseOverflowRate;
        public float DefenseEffectiveCap;

        // ── 일반 공격 넉백 ────────────────────────────────────
        /// <summary>최대 체력 100% 를 한 방에 깎았을 때의 넉백 세기.</summary>
        const float KnockbackPerHpRatio = 10f;
        /// <summary>단일 타격 넉백 상한 (여러 타격 합산 상한은 MaxKnockbackMag).</summary>
        const float MaxSingleKnockback  = 6f;

        /// <summary>
        /// 경직 발생 문턱 — 한 방에 최대 체력의 이 비율 이상을 날린 타격만 경직을 만든다.
        /// 이 아래는 <b>경직 시간을 계산조차 하지 않고 그냥 무시</b>한다.
        /// </summary>
        const float StunHpRatioThreshold = 0.02f;

        public void Execute(
            [ChunkIndexInQuery] int                    chunkIndex,
            Entity                                     entity,
            ref HealthComponent                        health,
            in  StatComponent                          stat,       // Defense → StatFinal
            ref HitReactionComponent                   hitReaction,
            ref UnitStateComponent                     unitState,
            ref DynamicBuffer<HitEventBufferElement>   hitBuffer,
            ref DynamicBuffer<DamageResultElement>     resultBuffer)
        {
            if (hitBuffer.Length == 0) return;

            float  totalDamage      = 0f;
            float3 totalKnockback   = float3.zero;
            float  maxStun          = 0f;
            float  maxNormalKbMag   = 0f;   // 일반 공격: 프레임 내 최대 단일 타격 넉백
            float3 maxNormalKbVec   = float3.zero;

            float rawDef      = stat.Final[StatType.Defense];

            // 넉백은 "절대 피해량" 이 아니라 "최대 체력 대비 비율" 로 정한다.
            //   100 체력에 50 피해  → 0.5   → 크게 밀린다
            //   10000 체력에 50 피해 → 0.005 → 거의 밀리지 않는다
            // 절대값 기준이면 스테이지가 올라가 공격력이 커질수록 체력 만 단위 보스도
            // 잡몹처럼 날아가고, 반대로 초반 저공격력은 종잇장 적조차 못 밀어낸다.
            float maxHp = math.max(1f, stat.Final[StatType.MaxHp]);

            bool               hasRetaliate = RetaliateLookup.HasComponent(entity);
            RetaliateComponent retaliate    = hasRetaliate
                                            ? RetaliateLookup[entity]
                                            : default;

            bool hasSkillKnockback = false;

            for (int i = 0; i < hitBuffer.Length; i++)
            {
                HitEventBufferElement hit = hitBuffer[i];

                // 반응(파쇄·반사·가시·중독 전이)을 일으키는 타격인가.
                // ⚠ 반사와 도트는 아니다 — 반사는 되받아치기 무한 루프, 도트는 틱마다
                //   반응이 터져 스택이 지수로 분다 (HitType.Dot 주석).
                bool reacts = hit.Type == HitType.Normal || hit.Type == HitType.Skill;
                bool isDot  = hit.Type == HitType.Dot;

                // 방어율 적용 (공식은 DamageMath 가 소유 — 예약 피해 계산과 반드시 동일)
                // ⚠ 도트는 방어율을 지나지 않는다 — 하한(1)이 프레임마다 붙으면
                //   초당 피해가 프레임 수만큼 부푼다. 값은 초당 피해 그대로다.
                float rawDamage    = hit.Damage;
                float actualDamage = isDot ? rawDamage : DamageMath.AfterDefense(
                    rawDamage, rawDef, hit.DefensePierce,
                    DefenseSoftCap, DefenseOverflowRate, DefenseEffectiveCap);
                float absorbed     = rawDamage - actualDamage;

                // ── 소환사 평타 — 대상 최대 체력 비례로 **갈아 끼운다** ──
                //   ⚠ 방어율을 지나지 않는다 (파쇄와 같은 이유)
                //   ⚠ 더하지 않고 대신한다 — 소환사의 공격력 수치는 여기서 무의미해진다.
                //   ⚠ 스킬·반사에는 안 붙는다. 평타만이다 —
                //     시그니처 스킬까지 비율이 되면 횟수 제한의 값이 폭발한다.
                if (hit.Type == HitType.Normal &&
                    hit.AttackerEntity != Entity.Null &&
                    SummonerStrikeLookup.HasComponent(hit.AttackerEntity))
                {
                    SummonerStrikeComponent strike = SummonerStrikeLookup[hit.AttackerEntity];

                    float ratio = BossLookup.HasComponent(entity)  ? strike.BossRatio
                                : EliteLookup.HasComponent(entity) ? strike.EliteRatio
                                                                   : strike.NormalRatio;

                    actualDamage = maxHp * ratio;
                    absorbed     = 0f;
                }

                // ── 특성 '파쇄' — 때린 쪽이 갖고 있으면 최대 체력 비례 추가 피해 ──
                //   ⚠ 방어율을 지나지 않는다. 방패병 용사의 해답이 존재 이유라
                //     방어율을 태우면 목적 자체가 사라진다.
                //   ⚠ 반사 피해에는 안 붙는다 — 붙이면 두 파쇄가 서로를 되쏘며
                //     한 번의 교전이 무한히 커진다.
                if (reacts &&
                    hit.AttackerEntity != Entity.Null &&
                    RendLookup.HasComponent(hit.AttackerEntity))
                {
                    RendComponent rend = RendLookup[hit.AttackerEntity];
                    actualDamage += math.min(maxHp * rend.MaxHpRatio, rend.Cap);
                }

                totalDamage       += actualDamage;

                if (hit.Type == HitType.Skill)
                {
                    totalKnockback += hit.HitDirection;
                    if (math.lengthsq(hit.HitDirection) > 0f)
                        hasSkillKnockback = true;
                }
                else if (!isDot)   // 도트는 밀지 않는다
                {
                    // 체력의 100% 를 한 방에 날리면 KnockbackPerHpRatio 만큼 밀린다.
                    //
                    // ⚠ 소환사 '패기' 가 여기에 곱해진다 (사용자 확정, 2026-09-07)
                    //   패기 4 → ×0.8 · 패기 8 → ×1.6. 라인 디펜스에서 적을 뒤로 미는
                    //   것은 성벽 도달을 늦추는 실제 값어치라, 공격력·체력과
                    //   겹치지 않는 축이 된다.
                    // ⚠ 상한은 배율 **뒤에** 건다 — 앞에 걸면 패기가 상한에 먹힌다.
                    float kbPower = KnockbackPowerLookup.HasComponent(hit.AttackerEntity)
                                  ? KnockbackPowerLookup[hit.AttackerEntity].Mult
                                  : 1f;

                    float kbMag = math.min(actualDamage / maxHp * KnockbackPerHpRatio * kbPower,
                                           MaxSingleKnockback);
                    if (kbMag > maxNormalKbMag)
                    {
                        maxNormalKbMag = kbMag;
                        maxNormalKbVec = hit.HitDirection * kbMag;
                    }
                }

                float stunTime = isDot ? 0f : CalculateStunDuration(actualDamage, maxHp);
                maxStun        = math.max(maxStun, stunTime);

                // ── 반동 — 평타로 **실제로** 밀쳐 냈을 때 때린 쪽이 회복한다 ──
                //   ⚠ 경직(stunTime)이 곧 넉백이 적용되는 조건이다 — 잔매는 밀리지 않는다.
                //     넉백 면역 대상도 밀리지 않으므로 세지 않는다.
                //   ⚠ 회복은 ECB 로 때린 쪽 버퍼에 넣는다 (병렬 잡에서 남의 체력을 못 쓴다).
                if (hit.Type == HitType.Normal && stunTime > 0f &&
                    hit.AttackerEntity != Entity.Null &&
                    !KnockbackImmuneLookup.HasComponent(entity) &&
                    RecoilLookup.HasComponent(hit.AttackerEntity))
                {
                    Ecb.AppendToBuffer(chunkIndex, hit.AttackerEntity, new HealEventBufferElement
                    {
                        Amount       = RecoilLookup[hit.AttackerEntity].HealAmount,
                        SourceEntity = hit.AttackerEntity,
                    });
                }

                // ── 종족 패시브 — 때린 쪽이 나에게 거는 것 (화상) ──
                //
                //   위와 반대 방향이다. 여기서는 공격자의 컴포넌트를 읽어
                //   **나 자신에게** 상태효과를 건다.
                if (reacts &&
                    hit.AttackerEntity != Entity.Null &&
                    InflictLookup.HasComponent(hit.AttackerEntity))
                {
                    InflictOnHitComponent inflict = InflictLookup[hit.AttackerEntity];

                    if (inflict.EffectDuration > 0f)
                        Ecb.AppendToBuffer(chunkIndex, entity, new StatusEffectBufferElement
                        {
                            Stat         = inflict.EffectStat,
                            Delta        = inflict.EffectDelta,
                            Mode         = inflict.EffectMode,
                            Dot          = inflict.EffectDot,
                            Duration     = inflict.EffectDuration,
                            Remaining    = inflict.EffectDuration,
                            SourceType   = BuffSourceType.Passive,
                            SourceId     = 0,
                            SourceEntity = hit.AttackerEntity,
                        });

                    // 두 번째 효과 — 역병 시너지가 중독과 함께 거는 취약 표식.
                    if (inflict.ExtraDuration > 0f)
                        Ecb.AppendToBuffer(chunkIndex, entity, new StatusEffectBufferElement
                        {
                            Stat         = inflict.ExtraStat,
                            Delta        = inflict.ExtraDelta,
                            Mode         = inflict.ExtraMode,
                            Dot          = inflict.ExtraDot,
                            Duration     = inflict.ExtraDuration,
                            Remaining    = inflict.ExtraDuration,
                            SourceType   = BuffSourceType.Passive,
                            SourceId     = 0,
                            SourceEntity = hit.AttackerEntity,
                        });
                }

                // ── 종족 패시브 반응 — 때린 상대에게 되돌려준다 ──
                //
                // ⚠ 반사 피해에는 반응하지 않는다
                //   가시 vs 가시가 만나면 서로를 무한히 되받아친다.
                //   거울 방어가 Reflected 를 거르는 것과 같은 이유다.
                if (hasRetaliate && reacts &&
                    hit.AttackerEntity != Entity.Null)
                {
                    if (retaliate.ThornRatio > 0f)
                        Ecb.AppendToBuffer(chunkIndex, hit.AttackerEntity, new HitEventBufferElement
                        {
                            Damage         = actualDamage * retaliate.ThornRatio,
                            AttackerEntity = entity,
                            Type           = HitType.Reflected,
                        });

                    if (retaliate.EffectDuration > 0f)
                        Ecb.AppendToBuffer(chunkIndex, hit.AttackerEntity,
                            new StatusEffectBufferElement
                            {
                                Stat         = retaliate.EffectStat,
                                Delta        = retaliate.EffectDelta,
                                Mode         = retaliate.EffectMode,
                                Dot          = retaliate.EffectDot,
                                Duration     = retaliate.EffectDuration,
                                Remaining    = retaliate.EffectDuration,
                                SourceType   = BuffSourceType.Passive,
                                SourceId     = 0,
                                // ⚠ 출처를 비우면 도트로 잡은 적이 아무의 전과도 아니게 된다
                                //   (StatusEffectBufferElement.SourceEntity 주석 참고)
                                SourceEntity = entity,
                            });
                }

                resultBuffer.Add(new DamageResultElement
                {
                    AttackerEntity = hit.AttackerEntity,
                    ActualDamage   = actualDamage,
                    AbsorbedDamage = absorbed,
                    IsKill         = false,
                    Type           = hit.Type,
                });
            }

            totalKnockback   += maxNormalKbVec;

            // 스킬 넉백이 있으면 KnockbackJob이 적용할 수 있도록 최소 경직 시간 보장
            // (KnockbackJob은 IsStunned=true 일 때만 KnockbackVelocity를 적용함)
            if (hasSkillKnockback)
                maxStun = math.max(maxStun, 0.3f);

            // ── 무적 ──
            //  피격 연출(플래시·넉백·경직)과 반사·피해 기록은 그대로 두고 체력만 지킨다.
            //  ⚠ 반드시 여기서 막는다 — 죽음은 이 자리에서 확정되므로
            //    밖에서 체력을 주기적으로 채우는 방식은 한 틱 안에 통이 비는
            //    병사를 못 살린다 (로비 데모에서 장군만 살아남던 이유).
            // ⚠ 연출·무적은 '피해만' 막는다
            //   플래시·넉백·경직은 위에서 이미 처리됐다. 체력을 지키는 것이 전부다.
            // ── 강철 금 — 한 방 피해 상한 ──
            //
            //  ⚠ 방어율이 못 막는 종류의 피해를 막는다
            //    방어율은 들어오는 피해를 비율로 깎으므로, 한 방이 체력을 넘는
            //    큰 피해(보스 강타·메테오)에는 무력했다. 상한은 그 한 방을
            //    나눠 받게 만든다.
            //
            //  ⚠ 프레임 합산분(totalDamage)에 건다
            //    타격마다 걸면 여러 발이 같은 프레임에 들어올 때 상한이 발수만큼
            //    늘어난다. "한 번에" 는 한 프레임이다.
            //
            //  ⚠ 반사·피해 기록은 깎기 전 값을 그대로 쓴다
            //    위에서 이미 기록됐다. 여기서는 체력에 들어가는 양만 줄인다 —
            //    무적 처리와 같은 자리·같은 이유다.
            // ── 성벽 — 받는 피해 배율 ──
            //   ⚠ 강철 금(상한)보다 **앞**이다 — 줄인 뒤에 상한을 걸어야 둘이 겹칠 때
            //     상한이 더 늦게 걸린다(성벽이 상한을 헛되게 만들지 않는다).
            //   반사·피해 기록은 이미 위에서 원래 값으로 끝났다 — 무적·상한과 같은 자리.
            if (DamageTakenLookup.HasComponent(entity))
                totalDamage *= DamageTakenLookup[entity].Mult;

            if (DamageCapLookup.HasComponent(entity))
            {
                float capped = stat.Final[StatType.MaxHp] * DamageCapLookup[entity].Fraction;
                totalDamage  = math.min(totalDamage, capped);
            }

            if (!InvulnerableLookup.HasComponent(entity) &&
                !SpawnProtectionLookup.HasComponent(entity))
                health.CurrentHp -= totalDamage;

            hitBuffer.Clear();

            // ⚠ 마왕은 제 몸으로 맞지 않는다 — 성벽이 맞는다
            //   캐릭터 HP 가 곧 마왕성 HP 라서 피해는 이 엔티티로 들어오지만,
            //   화면에서 흔들리고 번쩍여야 하는 것은 **성벽**이다. 본체가
            //   번쩍이면 "성벽 뒤에 숨은 마왕" 이라는 그림이 무너진다.
            //   성벽 연출은 BattleStatCollectorSystem 이 같은 피해 기록을 읽어
            //   CastleWallView 에 넘긴다 (넉백·경직은 KnockbackImmuneTag 담당).
            hitReaction.NeedsFlash = !WallCoverLookup.HasComponent(entity);

            // ── 재생 금 — 치명상을 한 번 버틴다 ──
            //
            //  ⚠ 사망 판정 **앞**이어야 한다
            //    죽음은 아래에서 확정된다. 뒤에 두면 DeadTag 가 이미 붙어
            //    되살릴 수 없다 (무적 처리가 같은 이유로 위에 있다).
            //
            //  ⚠ 표식은 ECB 로 남긴다
            //    병렬 잡이라 남의 컴포넌트를 직접 쓸 수 없다. 이번 프레임에
            //    두 번 버티는 일은 없다 — 이 잡은 엔티티당 한 번만 돈다.
            if (health.CurrentHp <= 0f
                && LastStandLookup.HasComponent(entity)
                && LastStandLookup[entity].Used == 0)
            {
                health.CurrentHp = 1f;
                Ecb.SetComponent(chunkIndex, entity, new SynergyLastStandComponent { Used = 1 });
            }

            // ── 사망 판정 ──
            if (health.CurrentHp <= 0f)
            {
                health.CurrentHp = 0f;
                ChangeState(ref unitState, UnitState.Dead);
                Ecb.AddComponent<DeadTag>(chunkIndex, entity);

                // 마지막 히트를 날린 공격자에게 킬 표시 (버퍼 마지막 항목 기준)
                if (resultBuffer.Length > 0)
                {
                    int last = resultBuffer.Length - 1;
                    var r = resultBuffer[last];
                    r.IsKill = true;
                    resultBuffer[last] = r;
                }
                return;
            }

            // ── 방패병 달인: 넉백 완전 무시 ──
            if (KnockbackImmuneLookup.HasComponent(entity))
            {
                totalKnockback = float3.zero;
                maxStun        = 0f;
            }
            else
            {
                // ── 내성 적용 ──
                if (BossLookup.HasComponent(entity))
                {
                    var boss = BossLookup[entity];
                    totalKnockback *= (1f - boss.KnockbackResistance);
                    maxStun        *= (1f - boss.CCResistance);
                }
                else if (EliteLookup.HasComponent(entity))
                {
                    totalKnockback *= (1f - EliteLookup[entity].KnockbackResistance);
                }
            }

            // ── 넉백 / 경직 적용 (누적 상한 8) ──
            const float MaxKnockbackMag = 8f;
            float kbMagSq = math.lengthsq(totalKnockback);
            if (kbMagSq > MaxKnockbackMag * MaxKnockbackMag)
                totalKnockback = math.normalize(totalKnockback) * MaxKnockbackMag;

            hitReaction.KnockbackVelocity = totalKnockback;

            hitReaction.StunDuration = math.max(hitReaction.StunDuration, maxStun);
            hitReaction.StunTimer    = math.max(hitReaction.StunTimer,    maxStun);
            hitReaction.IsStunned    = hitReaction.IsStunned || maxStun > 0f;

            if (maxStun > 0f)
                ChangeState(ref unitState, UnitState.Hit);
        }

        /// <summary>
        /// 경직 시간 — 넉백과 같은 기준(최대 체력 대비 비율)으로 정하고,
        /// <b>문턱 아래의 타격은 경직을 아예 만들지 않는다.</b>
        ///
        /// ⚠ 절대 피해량 기준이던 것을 비율 + 문턱으로 바꿨다
        ///   예전 표는 "피해 20 이상이면 무조건 경직" 이었다. 스테이지가 오르면
        ///   잡몹 평타도 수백 피해라 <b>모든 타격이 경직</b>이 됐다. 그런데 경직은
        ///   매 프레임 max() 로 갱신되고 공격 Job 은 UnitState.Hit 이면 그냥 return 한다.
        ///   적 대여섯이 붙으면 프레임마다 새 경직이 덮여 타이머가 0 에 닿질 못했고,
        ///   유닛은 계속 밀리기만 하며 평생 공격을 못 했다 — 그게 '무한 넉백' 증상이다.
        ///   넉백 상한(MaxKnockbackMag)은 한 프레임의 세기만 막을 뿐 이 누적을 못 막는다.
        ///
        ///   지금은 한 방에 체력을 크게 날린 타격만 경직이다. 잡몹의 잔매는 몇이 붙든
        ///   경직도 넉백도 아니다 (넉백은 IsStunned 일 때만 적용되므로 함께 사라진다).
        /// </summary>
        static float CalculateStunDuration(float damage, float maxHp)
        {
            float ratio = damage / maxHp;
            if (ratio < StunHpRatioThreshold) return 0f;   // 문턱 아래 = 무시
            if (ratio >= 0.40f) return 0.5f;
            if (ratio >= 0.25f) return 0.35f;
            return 0.2f;
        }

        static void ChangeState(ref UnitStateComponent s, UnitState next)
        {
            s.Previous   = s.Current;
            s.Current    = next;
            s.StateTimer = 0f;
        }
    }

    // ──────────────────────────────────────────
    // 상태 타이머 갱신 Job
    // ──────────────────────────────────────────

    [BurstCompile]
    public partial struct StateTimerJob : IJobEntity
    {
        public readonly float DeltaTime;

        public void Execute(ref UnitStateComponent unitState)
        {
            unitState.StateTimer += DeltaTime;
        }
    }
}
