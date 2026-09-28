using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  SpeciesPassiveRuntime.cs
//  종족 고유 패시브(SpeciesPassive)의 실행부.
//  2026-09-10 — 장비가 주는 특이·각성 패시브도 여기서 돈다.
//
//  ■ 목록은 PassiveResolver 가 만든다
//    선천·융합·장비를 합치고 겹친 것을 지우고 각성을 끼운 **최종 목록**을 받는다.
//    여기서 다시 거르지 않는다 — 같은 패시브가 두 번 들어오는 일이 없다는 전제다.
//
//  ■ 훅은 셋이다
//    ApplyOnSpawn    — 스폰 직후. **지속형**은 여기서 ECS 컴포넌트로 붙인다.
//    OnDeath         — 죽는 순간. **단발형** 범위 효과·증식.
//    NotifyAllyDied  — 아무 몬스터나 죽을 때. '복수' 가 쌓인다.
//
//  ■ 가능한 한 기존 시스템에 얹는다
//      재생        → RegenComponent (UnitRegenSystem)
//      피격 반응    → RetaliateComponent (UnitHitSystem 의 피격 루프)
//      공격 반응    → InflictOnHitComponent
//      돌진·연사    → StatusEffectBufferElement / DoubleStrikeTag (둘 다 기존)
//      처치 누적    → PassiveSkillType (기존 40종)
//      범위 피해·회복 → SkillCrowdControl (기존 스킬 헬퍼)
//      특이 패시브  → MonsterTag.cs 의 전용 컴포넌트 (값을 스폰 때 굽는다)
//
//  ■ ⚠ Multiply 상태효과의 Delta 는 **곱할 배율 그 자체**다 (1.25 = +25%)
//    UnitStatusEffectSystem 이 Final *= Delta 로 곱한다. 한때 이 파일이 비율을
//    그대로(0.10) 넣어 **튼튼함이 체력을 10% 로 · 신속이 속도를 25% 로 ·
//    최후의 함성이 아군 공격력을 25% 로 · 냉기가 적을 사실상 정지**시키고 있었다
//    (2026-09-10 수정). 반드시 Mult(ratio) = 1 + ratio 를 거칠 것.
//
//  ■ ⚠ 증식은 0세대만 한다
//    분열체가 또 분열하면 슬라임 한 마리가 화면을 채운다.
//    SpeciesPassiveRule.MaxReproduceGeneration 이 그 한계이고,
//    개체의 세대는 MonsterDeathWatcher 가 들고 있다. 각성판도 같다.
// ============================================================

public static class SpeciesPassiveRuntime
{
    // ── 유물 보정 ────────────────────────────────────────────
    //
    //  ⚠ 세 함수 모두 **읽는 자리에서** 곱한다 (SpeciesPassiveRule 을 고치지 않는다)

    /// <summary>분열·재조립체의 스탯 배율. 유물 '분열의 유산' 이 더한다.</summary>
    static float DerivedScale(float baseScale)
        => Mathf.Clamp(
            baseScale + RelicTreeApplier.GetSystemValue(RelicSystemEffect.DerivedScaleBonus),
            0.1f, 1f);

    /// <summary>
    /// 재생·회복량 배율 — 유물 '치유의 기억' (옛 이름 '야성의 기억', 2026-09-16 에 범위를 넓히며 바꿨다).
    /// 거는 곳: 재생 · 트롤의 피(초당 회복) · 치유의 잔재 · 생명의 씨앗 · 광합성 · 힐 슬라임 치유 스킬(ActiveSlimeMend)
    ///         · 원작 회복 패시브 셋 — 흡혈 타격 · 긴급 회복 · 처치 회복 (HealPowerFor, 몬스터만).
    ///         생명 흡수·흡혈귀는 흡혈 타격·처치 회복 슬롯을 붙이는 것이라 저절로 따라온다.
    /// ⚠ 회복이 아닌 종족 패시브(반격·약탈 등)에는 걸지 않는다 — 이름이 '회복' 이다.
    /// </summary>
    public static float PassivePower
        => 1f + RelicTreeApplier.GetSystemValue(RelicSystemEffect.SpeciesPassivePower);

    /// <summary>
    /// 원작 회복 패시브(흡혈 타격·긴급 회복·처치 회복)가 쓰는 배율 — <b>회복하는 쪽이 몬스터일 때만</b> 유물 값.
    /// ⚠ 용사도 같은 패시브를 쓴다. 진영을 안 보면 유물이 적을 강화한다 (RelicTarget.Unit_Monster 와 같은 이유).
    /// </summary>
    public static float HealPowerFor(EntityManager em, Entity healer)
        => em.HasComponent<UnitIdentityComponent>(healer)
           && em.GetComponentData<UnitIdentityComponent>(healer).Team == Faction.Monster
            ? PassivePower
            : 1f;

    /// <summary>사망 발동 효과 배율. 유물 '죽음의 대가'.</summary>
    static float DeathPower
        => 1f + RelicTreeApplier.GetSystemValue(RelicSystemEffect.DeathTriggerPower);

    // ── 마나 방출 — 한 판 상한 ───────────────────────────────

    static int _manaReleased;

    /// <summary>판을 넘겼다 — 마나 방출 상한을 되돌린다 (RunBootstrap.AdvanceStage).</summary>
    public static void ResetStage() => _manaReleased = 0;

    /// <summary>비율 → Multiply 상태효과의 배율. 파일 머리 주석 참고.</summary>
    static float Mult(float ratio) => 1f + ratio;

    // ── 스폰 직후 ────────────────────────────────────────────

    /// <summary>
    /// 지속형 패시브를 붙인다. 스폰 직후 한 번.
    /// 목록은 PassiveResolver 가 만든 최종 목록이다.
    /// </summary>
    public static void ApplyOnSpawn(GameObject monster, MonsterSpeciesData species,
                                    List<SpeciesPassive> passives)
    {
        if (passives.Count == 0) return;

        if (!monster.TryGetComponent<EntityLink>(out var link)) return;
        if (link.Entity == Entity.Null) return;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        EntityManager em = world.EntityManager;
        if (!em.Exists(link.Entity)) return;

        Entity e = link.Entity;

        for (int i = 0; i < passives.Count; i++)
            ApplyOne(em, e, monster, species, passives[i]);
    }

    static void ApplyOne(EntityManager em, Entity e, GameObject monster,
                         MonsterSpeciesData species, SpeciesPassive passive)
    {
        switch (passive)
        {
            // ── 지속 회복 ────────────────────────────────────
            case SpeciesPassive.Regrow:
                SetRegen(em, e, SpeciesPassiveRule.RegrowPerSecond * PassivePower, 0f, 0f);
                break;

            case SpeciesPassive.TrollBlood:
                SetRegen(em, e, SpeciesPassiveRule.TrollBloodPerSecond * PassivePower,
                         SpeciesPassiveRule.TrollBloodLowHpMult, SpeciesPassiveRule.TrollBloodLowHp);
                break;

            // ── 피격 반응 — 합친다 (가시 + 냉기가 서로를 지우지 않게) ──
            case SpeciesPassive.PoisonOnHit:
                MergeRetaliate(em, e, PoisonRetaliate(em, e, 1f), 0f);
                break;

            case SpeciesPassive.Venom:
                MergeRetaliate(em, e, PoisonRetaliate(em, e, SpeciesPassiveRule.VenomPower),
                               SpeciesPassiveRule.VenomThorn);
                break;

            case SpeciesPassive.ChillOnHit:
                MergeRetaliate(em, e, ChillRetaliate(SpeciesPassiveRule.ChillSlowRatio,
                                                     SpeciesPassiveRule.ReactionDuration), 0f);
                break;

            case SpeciesPassive.Frostbite:
                MergeRetaliate(em, e, ChillRetaliate(SpeciesPassiveRule.FrostbiteSlowRatio,
                                                     SpeciesPassiveRule.FrostbiteDuration), 0f);
                break;

            case SpeciesPassive.ThornOnHit:
                MergeRetaliate(em, e, default, SpeciesPassiveRule.ThornRatio);
                break;

            case SpeciesPassive.IronThorns:
                MergeRetaliate(em, e, default, SpeciesPassiveRule.IronThornsRatio);
                break;

            // ── 공격 반응 — 내가 때린 쪽에게 건다 ────────────
            case SpeciesPassive.BurnOnAttack:
                SetBurn(em, e, 1f, 1f);
                break;

            case SpeciesPassive.Hellfire:
                SetBurn(em, e, SpeciesPassiveRule.HellfirePower, SpeciesPassiveRule.HellfireReach);
                break;

            // ── 영구 스탯 보정 ───────────────────────────────
            //   ⚠ Duration = -1 이 영구다 (UnitStatusEffectSystem 참고)
            case SpeciesPassive.Sturdy:
                AddMult(em, e, StatType.MaxHp, SpeciesPassiveRule.SturdyHpBonus);
                break;

            case SpeciesPassive.Colossus:
                AddMult(em, e, StatType.MaxHp, SpeciesPassiveRule.ColossusHpBonus);
                AddTag<KnockbackImmuneTag>(em, e);
                break;

            case SpeciesPassive.Swiftness:
                AddMult(em, e, StatType.AttackSpeed, SpeciesPassiveRule.SwiftAttackBonus);
                AddMult(em, e, StatType.MoveSpeed,   SpeciesPassiveRule.SwiftMoveBonus);
                break;

            case SpeciesPassive.Gale:
                AddMult(em, e, StatType.AttackSpeed, SpeciesPassiveRule.GaleBonus);
                AddMult(em, e, StatType.MoveSpeed,   SpeciesPassiveRule.GaleBonus);
                break;

            // 마나 공명 — 소환되는 순간의 그릇으로 굳는다 (필드에 선 개체는 안 바뀐다).
            //   ⚠ 여기서 건다 — 선천·융합·장비 어느 쪽으로 얻어도 같은 길을 지난다 (튼튼함과 같은 이유)
            case SpeciesPassive.ManaResonance:
            {
                var   mana  = UserDataManager.Instance?.Get<SummonManaData>();
                float bonus = mana != null ? mana.Max * SpeciesPassiveRule.ManaResonancePerMana : 0f;
                if (bonus <= 0f) break;

                AddMult(em, e, StatType.MaxHp,  bonus);
                AddMult(em, e, StatType.Attack, bonus);
                break;
            }

            // ── 기존 태그·패시브로 그대로 표현되는 것 ────────
            case SpeciesPassive.Volley:
                AddTag<DoubleStrikeTag>(em, e);
                break;

            case SpeciesPassive.Barrage:
                AddTag<DoubleStrikeTag>(em, e);
                AddMult(em, e, StatType.AttackSpeed, SpeciesPassiveRule.BarrageAttackSpeed);
                break;

            case SpeciesPassive.Bloodlust:
                AddPassiveSlot(em, e, PassiveSkillType.KillEmpower);
                break;

            case SpeciesPassive.Berserker:
                AddPassiveSlot(em, e, PassiveSkillType.KillEmpower);
                AddMult(em, e, StatType.Attack, SpeciesPassiveRule.BerserkerAttackBonus);
                break;

            case SpeciesPassive.PackHunt:
                AddPassiveSlot(em, e, PassiveSkillType.KillMomentum);
                break;

            case SpeciesPassive.Alpha:
                AddPassiveSlot(em, e, PassiveSkillType.KillMomentum);
                AddMult(em, e, StatType.AttackSpeed, SpeciesPassiveRule.AlphaSpeedBonus);
                AddMult(em, e, StatType.MoveSpeed,   SpeciesPassiveRule.AlphaSpeedBonus);
                break;

            case SpeciesPassive.SoulDrain:
                AddPassiveSlot(em, e, PassiveSkillType.VampiricStrike);
                break;

            case SpeciesPassive.Vampire:
                AddPassiveSlot(em, e, PassiveSkillType.VampiricStrike);
                AddPassiveSlot(em, e, PassiveSkillType.KillHeal);
                break;

            case SpeciesPassive.Bulwark:
                AddPassiveSlot(em, e, PassiveSkillType.DefenseShield);
                break;

            case SpeciesPassive.Fortress:
                AddPassiveSlot(em, e, PassiveSkillType.DefenseShield);
                AddStatus(em, e, StatType.Defense, SpeciesPassiveRule.FortressDefense, EffectMode.Add, -1f);
                AddTag<KnockbackImmuneTag>(em, e);
                break;

            // ── 특이 패시브 (장비) ───────────────────────────
            case SpeciesPassive.Bravado:
                Set(em, e, new BravadoComponent
                {
                    Threshold  = SpeciesPassiveRule.BravadoHpThreshold,
                    AttackMult = Mult(SpeciesPassiveRule.BravadoAttackBonus),
                });
                break;

            case SpeciesPassive.Recoil:
                Set(em, e, new RecoilComponent
                {
                    HealAmount = MaxHpOf(em, e) * SpeciesPassiveRule.RecoilHealRatio,
                });
                break;

            case SpeciesPassive.VitalStrike:
                AddTag<VitalStrikeTag>(em, e);
                break;

            case SpeciesPassive.LoneWolf:
                // ⚠ 소환되는 순간 한 번만 본다 — 특성 '소수정예' 와 같은 규칙.
                //   매 프레임 다시 재면 교전 중 라인이 붐비고 비는 대로 세기가 흔들린다.
                //   ⚠ 자기 자신은 이미 세어져 있다 (MonsterLineReturner.Setup 이 먼저 돈다).
                //   라인이 없는 개체(분열체·스킬 소환)는 조건을 볼 수 없어 받지 않는다.
                if (monster.TryGetComponent<MonsterLineReturner>(out var line) && line.Lane >= 0 &&
                    MonsterLineReturner.AliveInLane(line.Lane) <= 1)
                    AddMult(em, e, StatType.Attack, SpeciesPassiveRule.LoneWolfAttackBonus);
                break;

            case SpeciesPassive.Rampart:
                AddMult(em, e, StatType.MoveSpeed, -SpeciesPassiveRule.RampartMovePenalty);
                MulDamageTaken(em, e, 1f - SpeciesPassiveRule.RampartDamageCut);
                break;

            case SpeciesPassive.Executioner:
                Set(em, e, new ExecuteComponent { Threshold = SpeciesPassiveRule.ExecuteHpThreshold });
                break;

            // 휩쓸기 — 느려지는 값으로 범위를 산다 (성벽·무게추와 같은 문법)
            //  ⚠ 공속 페널티를 빼지 말 것. 순수 강화가 되면 앞줄이라면 누구나
            //    갖고 싶은 능력이 되어 '느린 대신 넓다' 는 축이 통째로 사라진다.
            case SpeciesPassive.Cleave:
                Set(em, e, new SplashAttackComponent
                {
                    Radius = SpeciesPassiveRule.CleaveRadius,
                    Ratio  = SpeciesPassiveRule.CleaveSplashRatio,
                });
                AddMult(em, e, StatType.AttackSpeed,
                        SpeciesPassiveRule.CleaveAttackSpeedMult - 1f);   // 음수 = 느려진다
                break;

            case SpeciesPassive.Anchor:
                AddTag<KnockbackImmuneTag>(em, e);
                AddMult(em, e, StatType.MoveSpeed, -SpeciesPassiveRule.AnchorMovePenalty);
                break;

            case SpeciesPassive.Photosynthesis:
                // 유물 '치유의 기억' — 소환 때 굽는다 (회복은 ActiveSkillExecuteSystem 이 이 비율로 건다)
                Set(em, e, new PhotosynthesisComponent { Ratio = SpeciesPassiveRule.PhotosynthesisHeal * PassivePower });
                break;

            case SpeciesPassive.Hunger:
                AddMult(em, e, StatType.AttackSpeed, SpeciesPassiveRule.HungerAttackSpeed);
                AddMult(em, e, StatType.MaxHp,      -SpeciesPassiveRule.HungerHpPenalty);
                // ⚠ 최대 체력이 **줄어드는** 효과는 현재 체력을 따라 내려 주지 않는다
                //   (UnitStatusEffectSystem 은 늘어날 때만 맞춘다). 스폰 직후라 가득 찬
                //   상태이므로 같은 비율로 깎아 둔다 — 안 그러면 체력 바가 100% 를 넘는다.
                ScaleCurrentHp(em, e, 1f - SpeciesPassiveRule.HungerHpPenalty);
                break;

            case SpeciesPassive.Vengeance:
                Set(em, e, new VengeanceComponent
                {
                    SpeciesKey = MonsterSynergyRule.KeyOf(species.Id),
                    Stacks     = 0,
                });
                break;

            // ── 여기서는 아무것도 하지 않는 것 ───────────────
            //   사망 발동은 OnDeath 에서, 단계 패시브는 MonsterGearRule.ApplyStats 에서.
            default:
                break;
        }
    }

    // ── 사망 ─────────────────────────────────────────────────

    /// <summary>
    /// 단발형 패시브를 터뜨린다. 개체가 죽는 순간 한 번.
    /// </summary>
    /// <param name="maxHp">죽은 개체의 <b>실제</b> 최대 체력 (UnitRuntimeBridge.RolledStat).</param>
    /// <param name="attack">죽은 개체의 <b>실제</b> 공격력 — 새 사망 효과(잔불·터지는 몸)의 기준.</param>
    /// <param name="generation">이 개체의 세대. 증식형은 0세대만 발동한다.</param>
    /// <param name="origin">죽은 개체가 물려주는 것 — 카드 레벨·융합 개성·외형 시드.</param>
    public static void OnDeath(MonsterSpeciesData species, SummonerData summoner,
                               Vector3 at, float maxHp, float attack, int generation,
                               in MonsterOrigin origin,
                               List<SpeciesPassive> passives)
    {
        if (passives.Count == 0) return;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        EntityManager em = world.EntityManager;

        bool canReproduce = generation <= SpeciesPassiveRule.MaxReproduceGeneration;

        for (int i = 0; i < passives.Count; i++)
        {
            switch (passives[i])
            {
                // ── 증식 — 세대 제한을 받는다 ────────────────
                case SpeciesPassive.SplitOnDeath when canReproduce:
                    Split(species, summoner, at, generation, origin,
                          SpeciesPassiveRule.SplitCount, SpeciesPassiveRule.SplitScale);
                    break;

                case SpeciesPassive.GreatSplit when canReproduce:
                    Split(species, summoner, at, generation, origin,
                          SpeciesPassiveRule.GreatSplitCount, SpeciesPassiveRule.GreatSplitScale);
                    break;

                // ── 왕의 분열 — 자기가 아니라 **권속**이 쏟아진다 (2026-09-15) ──
                //
                //  ⚠ 위 둘과 스폰 경로가 다르다
                //    분열·대분열은 SpawnDerived(자기 종족 · 왕의 카드 정보를 물려받음)지만,
                //    이쪽은 SpawnFree(권속 종족 · 그 종족의 덱 카드를 찾아 쓴다)다.
                //    권속에게 왕의 카드 레벨을 물려주면 **다른 종족의 레벨 표**가 얹혀
                //    슬라임이 슬라임 킹의 성장분으로 굴러 나온다.
                //
                //  ⚠ 크기 배율(KingSplitScale)은 SpawnFree 가 받지 않는다 — 권속은
                //    제 크기 그대로 선다. 쪼개진 조각이 아니라 "안에 있던 것들" 이라 그게 맞다.
                //    합계는 마릿수(12)가 정한다.
                case SpeciesPassive.KingSplit when canReproduce:
                    SpawnBrood(species, summoner, at, SpeciesPassiveRule.KingSplitCount);
                    break;

                case SpeciesPassive.Reassemble when canReproduce:
                    if (Random.value > SpeciesPassiveRule.ReassembleChance) break;
                    MonsterSpawner.SpawnDerived(species, summoner, at,
                        DerivedScale(SpeciesPassiveRule.ReassembleScale), generation + 1, origin);
                    break;

                case SpeciesPassive.Undying when canReproduce:
                    // 확률 없이 반드시 — 재조립(40%)의 상위 호환
                    MonsterSpawner.SpawnDerived(species, summoner, at,
                        DerivedScale(SpeciesPassiveRule.UndyingScale), generation + 1, origin);
                    break;

                // ── 골드 — 런 골드다 (마나가 아니다) ─────────
                case SpeciesPassive.Loot:
                    RunGoldRule.GrantLoot(Mathf.RoundToInt(SpeciesPassiveRule.LootGold * DeathPower));
                    break;

                case SpeciesPassive.GoldRush:
                    RunGoldRule.GrantLoot(Mathf.RoundToInt(
                        SpeciesPassiveRule.LootGold * SpeciesPassiveRule.GoldRushMult * DeathPower));
                    break;

                // ── 범위 효과 — 세대와 무관하다 ──────────────
                case SpeciesPassive.PlagueBurst:
                    PoisonNearbyEnemies(em, at, species, 1f, 1f);
                    break;

                case SpeciesPassive.Pandemic:
                    PoisonNearbyEnemies(em, at, species, SpeciesPassiveRule.PandemicPower,
                                        SpeciesPassiveRule.PandemicReach);
                    break;

                case SpeciesPassive.ExplodeOnDeath:
                    ExplodeNearbyEnemies(em, at, species, 1f, 1f);
                    break;

                case SpeciesPassive.Cataclysm:
                    ExplodeNearbyEnemies(em, at, species, SpeciesPassiveRule.CataclysmPower,
                                         SpeciesPassiveRule.CataclysmReach);
                    break;

                case SpeciesPassive.HealOnDeath:
                    HealNearbyAllies(em, species, at, maxHp, SpeciesPassiveRule.HealOnDeathRatio * PassivePower, 1f);
                    break;

                case SpeciesPassive.LifeSeed:
                    HealNearbyAllies(em, species, at, maxHp, SpeciesPassiveRule.LifeSeedRatio * PassivePower,
                                     SpeciesPassiveRule.LifeSeedReach);
                    break;

                case SpeciesPassive.RallyOnDeath:
                    RallyNearbyAllies(em, at, SpeciesPassiveRule.RallyBonus, 0f,
                                      SpeciesPassiveRule.RallyDuration);
                    break;

                case SpeciesPassive.WarDrum:
                    RallyNearbyAllies(em, at, SpeciesPassiveRule.WarDrumAttackBonus,
                                      SpeciesPassiveRule.WarDrumSpeedBonus,
                                      SpeciesPassiveRule.WarDrumDuration);
                    break;

                // ── 특이 (장비) ──────────────────────────────
                case SpeciesPassive.BurstBody:
                    KnockNearbyEnemies(em, at, attack);
                    break;

                case SpeciesPassive.Embers:
                    BurnNearbyEnemies(em, at, attack);
                    break;

                // ── 마나 (2026-09-12) — 한 판 상한까지만 ─────
                case SpeciesPassive.ManaRelease:
                    if (_manaReleased >= SpeciesPassiveRule.ManaReleaseStageCap) break;

                    _manaReleased++;
                    RunPerkRule.RestoreMana(SpeciesPassiveRule.ManaReleaseAmount);
                    break;
            }
        }
    }

    static void Split(MonsterSpeciesData species, SummonerData summoner, Vector3 at,
                      int generation, in MonsterOrigin origin, int count, float scale)
    {
        for (int n = 0; n < count; n++)
            MonsterSpawner.SpawnDerived(species, summoner, ScatterAround(at, n),
                                        DerivedScale(scale), generation + 1, origin);
    }

    /// <summary>
    /// 왕이 터지며 권속을 남긴다.
    ///
    /// ⚠ 1세대로 낸다 — 권속이 또 무언가를 낳지 않게
    ///   MaxReproduceGeneration 이 0 이라 여기서 나온 개체의 사망 증식은 전부 막힌다.
    ///   0세대로 내면 슬라임 12마리가 각자 둘로 분열해 24마리가 된다.
    ///
    /// ⚠ 권속이 비어 있으면 아무 일도 하지 않는다 — 에러를 내지 않는다
    ///   도감을 다시 굽기 전의 옛 에셋이 그 상태다. 전투를 멈출 이유가 없다.
    ///   비어 있는지는 MonsterCodexCreator 의 검산이 굽는 순간 잡는다.
    /// </summary>
    static void SpawnBrood(MonsterSpeciesData species, SummonerData summoner,
                           Vector3 at, int count)
    {
        if (species.BroodSpecies == null) return;

        for (int n = 0; n < count; n++)
            MonsterSpawner.SpawnFree(species.BroodSpecies, summoner,
                                     ScatterAround(at, n), generation: 1);
    }

    // ── 복수 ─────────────────────────────────────────────────

    static EntityQuery _vengeanceQuery;
    static World       _vengeanceWorld;

    /// <summary>
    /// 몬스터가 죽었다 — 같은 종족 아군 중 '복수' 를 가진 개체에게 한 겹 쌓는다.
    /// MonsterDeathWatcher 가 <b>모든</b> 몬스터 사망에서 부른다.
    ///
    /// ⚠ 쿼리를 도는 도중에 EntityManager 로 쓰지 않는다 (CLAUDE.md 투지 누적 크래시)
    ///   배열로 먼저 떠 온 뒤 그 배열을 돈다.
    /// ⚠ 판마다 초기화 — 살아남은 개체는 대기열로 돌아갔다가 새로 스폰되며 0 부터 쌓는다.
    /// </summary>
    public static void NotifyAllyDied(MonsterSpeciesData species)
    {
        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        EntityManager em = world.EntityManager;

        if (_vengeanceWorld != world)
        {
            _vengeanceWorld = world;
            _vengeanceQuery = em.CreateEntityQuery(new EntityQueryDesc
            {
                All  = new ComponentType[] { ComponentType.ReadOnly<VengeanceComponent>() },
                None = new ComponentType[] { typeof(DeadTag) },
            });
        }

        if (_vengeanceQuery.IsEmpty) return;

        int key = MonsterSynergyRule.KeyOf(species.Id);

        using NativeArray<Entity> targets = _vengeanceQuery.ToEntityArray(Allocator.Temp);
        foreach (Entity t in targets)
        {
            VengeanceComponent v = em.GetComponentData<VengeanceComponent>(t);
            if (v.SpeciesKey != key || v.Stacks >= SpeciesPassiveRule.VengeanceMaxStacks) continue;

            v.Stacks++;
            em.SetComponentData(t, v);
            AddMult(em, t, StatType.Attack, SpeciesPassiveRule.VengeancePerStack);
        }
    }

    // ── 범위 효과 구현 ───────────────────────────────────────

    static void PoisonNearbyEnemies(EntityManager em, Vector3 at, MonsterSpeciesData species,
                                    float power, float reach)
    {
        List<Entity> targets = SkillCrowdControl.CollectEnemiesInRadius(
            em, at, SpeciesPassiveRule.DeathBurstRadius * reach, Faction.Monster);

        float dps = MonsterDotRule.Scale(species.Attack * SpeciesPassiveRule.PlagueDpsRatio * power);

        foreach (Entity target in targets)
            SkillCrowdControl.AddEffect(em, target, StatType.MaxHp, dps, EffectMode.Dot,
                                        SpeciesPassiveRule.PlagueDuration * reach,
                                        ActiveSkillId.PoisonZone);
    }

    static void ExplodeNearbyEnemies(EntityManager em, Vector3 at, MonsterSpeciesData species,
                                     float power, float reach)
    {
        List<Entity> targets = SkillCrowdControl.CollectEnemiesInRadius(
            em, at, SpeciesPassiveRule.DeathBurstRadius * reach, Faction.Monster);

        float damage = species.Attack * SpeciesPassiveRule.ExplodeDamageRatio * power;

        foreach (Entity target in targets)
            SkillCrowdControl.DealDamage(em, target, damage,
                                         SkillCrowdControl.PositionOf(em, target) - at,
                                         2f, Entity.Null);

        SkillEffectHelper.Spawn("FX_Explosion", at, 1f);
    }

    /// <summary>
    /// 주변 아군 회복. 회복량은 <b>죽은 개체의 실제 최대 체력</b> 기준이다.
    /// ⚠ 받는 쪽 기준이 아니다 — 숲 시너지와 규칙이 다르다 (합치지 말 것).
    /// </summary>
    /// <param name="species">
    /// 죽은 개체의 종족 = 치유를 달 카드. ⚠ 시전자가 이미 죽어 엔티티로는 못 찾는다 —
    /// 한때 출처를 비워(Entity.Null) 넣어 통계의 '치유' 가 늘 0 이었다 (2026-09-11).
    /// </param>
    static void HealNearbyAllies(EntityManager em, MonsterSpeciesData species, Vector3 at,
                                 float maxHp, float ratio, float reach)
    {
        float amount = maxHp * ratio * DeathPower;
        if (amount <= 0f) return;

        float r     = SpeciesPassiveRule.DeathBurstRadius * reach;
        float radSq = r * r;

        // 통계는 **실제로 찬 양**만 센다 — 가득 찬 아군에게 부은 몫은 기여가 아니다.
        float healed = 0f;

        foreach (Entity ally in SkillCrowdControl.CollectAllies(em, Faction.Monster))
        {
            if ((SkillCrowdControl.PositionOf(em, ally) - at).sqrMagnitude > radSq) continue;

            if (em.HasComponent<HealthComponent>(ally) && em.HasComponent<StatComponent>(ally))
            {
                float missing = em.GetComponentData<StatComponent>(ally).Final[StatType.MaxHp]
                              - em.GetComponentData<HealthComponent>(ally).CurrentHp;
                healed += Mathf.Clamp(missing, 0f, amount);
            }

            // ⚠ 출처는 비운다 — UnitHealSystem 이 엔티티로 카드를 찾으면 여기와 두 번 센다
            SkillCrowdControl.Heal(em, ally, amount, Entity.Null);
            SkillEffectHelper.Spawn("FX_Heal_Target", SkillCrowdControl.BodyCenterOf(em, ally), 1f);
        }

        CardStatsTracker.Instance?.RecordHealingForCard(species.Id, healed);

        SkillEffectHelper.Spawn("FX_Heal_Aura", at, 1.2f);
    }

    static void RallyNearbyAllies(EntityManager em, Vector3 at, float attackBonus,
                                  float speedBonus, float duration)
    {
        float radSq = SpeciesPassiveRule.DeathBurstRadius * SpeciesPassiveRule.DeathBurstRadius;

        foreach (Entity ally in SkillCrowdControl.CollectAllies(em, Faction.Monster))
        {
            if ((SkillCrowdControl.PositionOf(em, ally) - at).sqrMagnitude > radSq) continue;

            // ⚠ 배율이다 — 1 + 비율 (파일 머리 주석). 한때 0.25 를 그대로 넣어
            //   함성이 아군 공격력을 25% 로 **깎고** 있었다.
            SkillCrowdControl.AddEffect(em, ally, StatType.Attack, Mult(attackBonus),
                                        EffectMode.Multiply, duration, ActiveSkillId.BattleCry);

            if (speedBonus > 0f)
                SkillCrowdControl.AddEffect(em, ally, StatType.AttackSpeed, Mult(speedBonus),
                                            EffectMode.Multiply, duration, ActiveSkillId.BattleCry);
        }

        SkillEffectHelper.Spawn("FX_Battle_Cry", at, 1f);
    }

    /// <summary>터지는 몸 — 주변 적을 밀쳐 낸다. 피해는 덤이다(넉백을 태우려면 한 방이 필요하다).</summary>
    static void KnockNearbyEnemies(EntityManager em, Vector3 at, float attack)
    {
        List<Entity> targets = SkillCrowdControl.CollectEnemiesInRadius(
            em, at, SpeciesPassiveRule.DeathBurstRadius, Faction.Monster);

        float damage = attack * SpeciesPassiveRule.BurstBodyDamageRatio * DeathPower;

        foreach (Entity target in targets)
            SkillCrowdControl.DealDamage(em, target, damage,
                                         SkillCrowdControl.PositionOf(em, target) - at,
                                         SpeciesPassiveRule.BurstBodyKnockback, Entity.Null);

        SkillEffectHelper.Spawn("FX_Explosion", at, 0.6f);
    }

    /// <summary>잔불 — 주변 적에게 화상. 불이라 붉게 물든다(DotKind.Burn).</summary>
    static void BurnNearbyEnemies(EntityManager em, Vector3 at, float attack)
    {
        List<Entity> targets = SkillCrowdControl.CollectEnemiesInRadius(
            em, at, SpeciesPassiveRule.DeathBurstRadius, Faction.Monster);

        float dps = MonsterDotRule.Scale(attack * SpeciesPassiveRule.EmbersDpsRatio * DeathPower);

        foreach (Entity target in targets)
        {
            if (!em.HasBuffer<StatusEffectBufferElement>(target)) continue;

            em.GetBuffer<StatusEffectBufferElement>(target).Add(new StatusEffectBufferElement
            {
                Stat       = StatType.MaxHp,
                Delta      = dps,
                Mode       = EffectMode.Dot,
                Dot        = DotKind.Burn,
                Duration   = SpeciesPassiveRule.EmbersDuration,
                Remaining  = SpeciesPassiveRule.EmbersDuration,
                SourceType = BuffSourceType.Passive,
                SourceId   = 0,
            });
        }

        SkillEffectHelper.Spawn("FX_Explosion", at, 0.6f);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────

    /// <summary>분열체가 겹치지 않게 살짝 흩뜨린다 (황금각 — 몇 개든 고르게 퍼진다).</summary>
    static Vector3 ScatterAround(Vector3 at, int index)
    {
        float angle = (index * 137.5f) * Mathf.Deg2Rad;
        return at + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.6f;
    }

    static float AttackOf(EntityManager em, Entity e)
        => em.HasComponent<StatComponent>(e)
            ? em.GetComponentData<StatComponent>(e).Final[StatType.Attack]
            : 0f;

    static float MaxHpOf(EntityManager em, Entity e)
        => em.HasComponent<StatComponent>(e)
            ? em.GetComponentData<StatComponent>(e).Final[StatType.MaxHp]
            : 0f;

    static void ScaleCurrentHp(EntityManager em, Entity e, float mult)
    {
        if (!em.HasComponent<HealthComponent>(e)) return;

        var hp = em.GetComponentData<HealthComponent>(e);
        hp.CurrentHp *= mult;
        em.SetComponentData(e, hp);
    }

    static void SetRegen(EntityManager em, Entity e, float ratioPerSecond, float lowMult, float lowThreshold)
    {
        var regen = new RegenComponent
        {
            RatioPerSecond = ratioPerSecond,
            Timer          = 0f,
            LowHpMult      = lowMult,
            LowHpThreshold = lowThreshold,
        };

        if (em.HasComponent<RegenComponent>(e)) em.SetComponentData(e, regen);
        else                                    em.AddComponentData(e, regen);
    }

    static RetaliateComponent PoisonRetaliate(EntityManager em, Entity e, float power) => new()
    {
        EffectStat     = StatType.MaxHp,   // Dot 모드에서는 무시된다
        EffectDelta    = MonsterDotRule.Scale(AttackOf(em, e) * SpeciesPassiveRule.PoisonDpsRatio * power),
        EffectMode     = EffectMode.Dot,
        EffectDuration = SpeciesPassiveRule.ReactionDuration,
    };

    /// <summary>둔화 — ⚠ Delta 는 곱할 배율이다 (1 − 비율). 한때 −0.35 를 넣어 사실상 정지였다.</summary>
    static RetaliateComponent ChillRetaliate(float slowRatio, float duration) => new()
    {
        EffectStat     = StatType.MoveSpeed,
        EffectDelta    = 1f - slowRatio,
        EffectMode     = EffectMode.Multiply,
        EffectDuration = duration,
    };

    /// <summary>
    /// 피격 반응을 <b>합쳐서</b> 붙인다 — 반사(가시)와 상태효과(중독·둔화)가 따로 산다.
    ///
    /// ⚠ 한때 통째로 덮어써서 "나중 것이 이긴다" 였다. 선천 가시 + 장비 냉기처럼
    ///   출처가 셋이 되며 겹치는 일이 흔해졌다. 반사는 큰 쪽, 상태효과는 새것이 이긴다.
    /// </summary>
    static void MergeRetaliate(EntityManager em, Entity e, RetaliateComponent effect, float thorn)
    {
        RetaliateComponent cur = em.HasComponent<RetaliateComponent>(e)
                               ? em.GetComponentData<RetaliateComponent>(e)
                               : default;

        if (effect.EffectDuration > 0f)
        {
            cur.EffectStat     = effect.EffectStat;
            cur.EffectDelta    = effect.EffectDelta;
            cur.EffectMode     = effect.EffectMode;
            cur.EffectDuration = effect.EffectDuration;
            cur.EffectDot      = effect.EffectDot;
        }

        cur.ThornRatio = Mathf.Max(cur.ThornRatio, thorn);

        Set(em, e, cur);
    }

    static void SetBurn(EntityManager em, Entity e, float power, float reach)
    {
        var value = new InflictOnHitComponent
        {
            EffectStat     = StatType.MaxHp,   // Dot 모드에서는 무시된다
            EffectDelta    = MonsterDotRule.Scale(AttackOf(em, e) * SpeciesPassiveRule.BurnDpsRatio * power),
            EffectMode     = EffectMode.Dot,
            EffectDot      = DotKind.Burn,     // 불이다 — 붉게 물든다
            EffectDuration = SpeciesPassiveRule.BurnDuration * reach,
        };

        Set(em, e, value);
    }

    static void MulDamageTaken(EntityManager em, Entity e, float mult)
    {
        float cur = em.HasComponent<DamageTakenMultComponent>(e)
                  ? em.GetComponentData<DamageTakenMultComponent>(e).Mult
                  : 1f;

        Set(em, e, new DamageTakenMultComponent { Mult = cur * mult });
    }

    static void Set<T>(EntityManager em, Entity e, T value) where T : unmanaged, IComponentData
    {
        if (em.HasComponent<T>(e)) em.SetComponentData(e, value);
        else                       em.AddComponentData(e, value);
    }

    static void AddTag<T>(EntityManager em, Entity e) where T : unmanaged, IComponentData
    {
        if (!em.HasComponent<T>(e)) em.AddComponent<T>(e);
    }

    /// <summary>영구 배율 상태효과 — ratio 가 음수면 깎는다 (−0.1 = ×0.9).</summary>
    static void AddMult(EntityManager em, Entity e, StatType stat, float ratio)
        => AddStatus(em, e, stat, Mult(ratio), EffectMode.Multiply, -1f);

    /// <summary>
    /// 상태효과를 건다. duration 이 음수면 <b>영구</b>다.
    ///
    /// ⚠ 영구 효과의 Remaining 은 0 보다 커야 한다
    ///   UnitStatusEffectSystem 은 Remaining &lt;= 0 인 항목을 종류를 가리지 않고 지운다.
    /// ⚠ Multiply 면 delta 는 배율이다 — 비율을 넘길 때는 AddMult 를 쓸 것.
    /// </summary>
    static void AddStatus(EntityManager em, Entity e, StatType stat, float delta,
                          EffectMode mode, float duration)
    {
        if (!em.HasBuffer<StatusEffectBufferElement>(e)) return;

        em.GetBuffer<StatusEffectBufferElement>(e).Add(new StatusEffectBufferElement
        {
            Stat       = stat,
            Delta      = delta,
            Mode       = mode,
            Duration   = duration,
            Remaining  = duration >= 0f ? duration : 1f,
            SourceType = BuffSourceType.Passive,
            SourceId   = 0,
        });
    }

    /// <summary>
    /// 패시브 슬롯에 한 칸 더 붙인다. 3칸이 꽉 차면 무시한다.
    /// ⚠ 카드 레벨 패시브가 먼저 자리를 잡는다 — 카드를 키운 대가로 얻는 것이 그 자리다.
    /// </summary>
    static void AddPassiveSlot(EntityManager em, Entity e, PassiveSkillType passive)
    {
        if (!em.HasComponent<GeneralPassiveSetComponent>(e)) return;

        var set = em.GetComponentData<GeneralPassiveSetComponent>(e);
        if (set.ActiveSlotCount >= 3) return;

        switch (set.ActiveSlotCount)
        {
            case 0:  set.Slot0 = passive; break;
            case 1:  set.Slot1 = passive; break;
            default: set.Slot2 = passive; break;
        }
        set.ActiveSlotCount++;

        em.SetComponentData(e, set);
    }
}
