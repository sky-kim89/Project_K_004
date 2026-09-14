using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  MonsterSynergyRuntime.cs
//  시너지 단계를 실제 몬스터에 얹는 곳. 수치는 전부 MonsterSynergyRule 에 있다.
//
//  ■ SpeciesPassiveRuntime 과 같은 구조다 — 일부러 그렇게 맞췄다
//      ApplyStats   — 스탯 합성 중. 곱해지는 값(체력·공속·방어율·쿨감)
//      ApplyOnSpawn — 스폰 직후. ECS 컴포넌트로 붙는 지속 효과
//      OnDeath      — 죽는 순간. 부활·범위 회복·누적 상속
//    두 파일이 같은 모양이면 "이 효과는 어디서 도나" 를 한 번만 배우면 된다.
//
//  ■ 새 시스템을 만들지 않는다
//      재생   → RegenComponent           (UnitRegenSystem)
//      반사   → RetaliateComponent       (UnitHitSystem 의 피격 루프)
//      중독   → InflictOnHitComponent    (공격 루프)
//      부활   → MonsterSpawner.SpawnDerived (재조립과 같은 길)
//      투지   → SynergyKillStackComponent (이것 하나만 새로 만들었다)
//
//  ■ ⚠ 기존 컴포넌트를 덮어쓰지 않는다
//    종족 패시브가 이미 RetaliateComponent / InflictOnHitComponent 를 쓴다.
//    통째로 SetComponentData 하면 강철 시너지가 독 슬라임의 반격을 지워 버린다.
//    읽어서 **합친 뒤** 다시 넣는다.
// ============================================================

public static class MonsterSynergyRuntime
{
    // ══════════════════════════════════════════════════════════
    //  ① 스탯 — MonsterStatComposer 가 마지막에 부른다
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 곱해지는 시너지 효과를 스탯에 얹는다.
    ///
    /// ⚠ 카드 레벨 보너스보다 **뒤**에 와야 한다
    ///   비율 항목이 '그 시점의 값' 에 곱해 더해지므로, 시너지를 먼저 얹으면
    ///   레벨 보너스가 부풀려진 값을 기준으로 다시 곱한다(이중 계산).
    /// </summary>
    public static void ApplyStats(UnitStat stat, MonsterTag tags)
    {
        if (tags == MonsterTag.None) return;

        // 특성 '극단' — 시너지 종류가 적을 때 **스탯 몫만** 커진다.
        // ⚠ 부활 확률·넉백 면역 같은 비스탯 효과는 단계로만 갈려 여기서 못 만진다.
        //   그쪽까지 키우려면 단계를 올려야 하는데, 그러면 전투력지수가 깨진다.
        float m = RunPerkRule.SynergyStatMult;

        // ── 숲 — 최대 체력 ──
        if (Has(tags, MonsterTag.Forest))
        {
            float hp = MonsterSynergyRule.ForestHpBonus(Tier(MonsterTag.Forest)) * m;
            if (hp > 0f) stat.Set(StatType.MaxHp, stat.Get(StatType.MaxHp) * (1f + hp));
        }

        // ── 야수 — 공격속도·이동속도 ──
        if (Has(tags, MonsterTag.Beast))
        {
            SynergyTier t = Tier(MonsterTag.Beast);

            float atkSpd = MonsterSynergyRule.BeastAttackSpeed(t) * m;
            if (atkSpd > 0f)
                stat.Set(StatType.AttackSpeed, stat.Get(StatType.AttackSpeed) * (1f + atkSpd));

            float move = MonsterSynergyRule.BeastMoveSpeed(t) * m;
            if (move > 0f)
                stat.Set(StatType.MoveSpeed, stat.Get(StatType.MoveSpeed) * (1f + move));
        }

        // ── 강철 — 방어율 ──
        //   ⚠ 방어율은 비율이 아니라 **더하는 %p** 다
        //     곱하면 원래 방어율이 높은 종족만 폭주한다. 상한도 반드시 건다.
        if (Has(tags, MonsterTag.Steel))
        {
            float add = MonsterSynergyRule.SteelDefense(Tier(MonsterTag.Steel)) * m;
            if (add > 0f)
            {
                var   cfg    = GameplayConfig.Current;
                float defMax = cfg != null ? cfg.DefenseMax : 0.95f;

                stat.Set(StatType.Defense,
                         Mathf.Min(stat.Get(StatType.Defense) + add, defMax));
            }
        }

        // ── 술법 — 스킬 쿨다운 ──
        //
        //   술법의 예산은 **전부 쿨다운 하나**다 (사용자 확정, 2026-09-03).
        //   환산은 MonsterSynergyRule.SorceryCooldownReduce 주석 참고.
        //
        //   ⚠ 이 스탯을 실제로 읽는 곳은 MonsterRuntimeBridge.BuildSkillSlot 이다
        //     한때 그쪽이 SO 의 쿨다운을 그대로 박아서, 여기서 아무리 올려도
        //     스킬이 빨라지지 않았다 — 술법 시너지가 통째로 무효였다.
        if (Has(tags, MonsterTag.Sorcery))
        {
            float cdr = MonsterSynergyRule.SorceryCooldownReduce(Tier(MonsterTag.Sorcery)) * m;
            if (cdr > 0f)
                stat.Add(StatType.SkillCooldownReduce, cdr, "synergy");
        }

        // ── 시너지 강화 카드 — 그 시너지 소속에게만 ──
        //   카운트 +1 은 MonsterSynergyRule.CountOf 가 이미 얹었다. 여기서는
        //   공/체만 붙인다.
        if (MonsterSynergyRule.BoostedTag != MonsterTag.None
            && Has(tags, MonsterSynergyRule.BoostedTag))
        {
            float boost = MonsterSynergyRule.BoostStatBonus * m;

            stat.Set(StatType.MaxHp,  stat.Get(StatType.MaxHp)  * (1f + boost));
            stat.Set(StatType.Attack, stat.Get(StatType.Attack) * (1f + boost));
        }

        // ── 중첩 — 켜진 시너지 개수 ──
        //
        //  ⚠ **켜진 시너지를 하나라도 가진 몬스터에만** 붙는다 (사용자 확정, 2026-09-04)
        //    한때 태그를 가리지 않고 전부에 붙였다. 그러면 시너지에 하나도
        //    안 걸린 몬스터가 남의 조합 덕을 그대로 봐서, "이 몬스터가 이
        //    시너지에 얹힌다" 는 규칙이 중첩에서만 깨졌다.
        //    시너지 효과는 전부 제 표식을 가진 몬스터에만 간다 — 예외를 두지 않는다.
        if (MonsterSynergyRule.HasAnyActive(tags))
        {
            float stack = MonsterSynergyRule.StackBonus(MonsterSynergyRule.ActiveCount) * m;
            if (stack > 0f)
            {
                stat.Set(StatType.MaxHp,  stat.Get(StatType.MaxHp)  * (1f + stack));
                stat.Set(StatType.Attack, stat.Get(StatType.Attack) * (1f + stack));
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  ② 스폰 직후 — MonsterSpawner 가 부른다
    // ══════════════════════════════════════════════════════════

    public static void ApplyOnSpawn(GameObject monster, MonsterTag tags)
        => ApplyOnSpawn(monster, tags, 0);

    /// <param name="speciesKey">
    /// 투지 금이 누적을 물려줄 열쇠 (MonsterSynergyRule.KeyOf). 0 이면 물려받지 않는다.
    /// </param>
    public static void ApplyOnSpawn(GameObject monster, MonsterTag tags, int speciesKey)
    {
        if (!TryEntity(monster, out EntityManager em, out Entity e)) return;

        // ⚠ 표식을 먼저 심는다 — 범위 효과가 이걸 보고 대상을 고른다
        //   태그가 None 이어도 심는다. 질의에서 빠지면 "표식 없음" 과
        //   "아직 안 심음" 을 구분할 수 없다.
        var tagComp = new MonsterTagComponent { Tags = tags };
        if (em.HasComponent<MonsterTagComponent>(e)) em.SetComponentData(e, tagComp);
        else                                        em.AddComponentData(e, tagComp);

        // ── 재생 — 초당 회복 ──
        //   ⚠ 종족 재생(트롤)과 **더한다**. 큰 쪽을 고르면 트롤에게 시너지가 무의미해진다.
        if (Has(tags, MonsterTag.Regrowth))
        {
            SynergyTier t = Tier(MonsterTag.Regrowth);

            float rate = MonsterSynergyRule.RegrowthPerSecond(t);
            if (rate > 0f)
                AddRegen(em, e, rate,
                         MonsterSynergyRule.RegrowthLowHpMult(t),
                         MonsterSynergyRule.RegrowthLowHpThreshold);
        }

        // ── 재생 금 — 치명상을 한 번 버틴다 ──
        //   ⚠ 값을 엔티티에 구워 둔다 — 피해 계산은 Burst 잡이라
        //     MonsterSynergyRule(관리형 static)을 읽을 수 없다.
        if (Has(tags, MonsterTag.Regrowth)
            && MonsterSynergyRule.RegrowthLastStand(Tier(MonsterTag.Regrowth)))
            SetOrAdd(em, e, new SynergyLastStandComponent { Used = 0 });

        // ── 강철 — 피해 반사(은부터) · 피해 상한(금) ──
        if (Has(tags, MonsterTag.Steel))
        {
            SynergyTier t = Tier(MonsterTag.Steel);

            float thorn = MonsterSynergyRule.SteelThorn(t);
            if (thorn > 0f) AddThorn(em, e, thorn);

            float cap = MonsterSynergyRule.SteelDamageCap(t);
            if (cap > 0f) SetOrAdd(em, e, new SynergyDamageCapComponent { Fraction = cap });
        }

        // ── 역병 — 내가 때린 적에게 중독 ──
        if (Has(tags, MonsterTag.Plague))
        {
            SynergyTier t = Tier(MonsterTag.Plague);

            float ratio = MonsterSynergyRule.PlagueDps(t);
            if (ratio > 0f)
                AddPoisonOnHit(em, e,
                               MonsterDotRule.Scale(AttackOf(em, e) * ratio),
                               MonsterSynergyRule.PlagueAmplify(t));
        }

        // ── 야수 — 처치 누적 공속(은부터) · 넉백 면역(금) ──
        if (Has(tags, MonsterTag.Beast))
        {
            SynergyTier t = Tier(MonsterTag.Beast);

            if (MonsterSynergyRule.BeastKnockbackImmune(t) && !em.HasComponent<KnockbackImmuneTag>(e))
                em.AddComponent<KnockbackImmuneTag>(e);
        }

        // ── 투지 — 처치당 공격력 누적 ──
        //   ⚠ 야수의 공속 누적과 **같은 컴포넌트**를 쓴다 (처치 수를 공유한다)
        //     늑대처럼 둘을 다 가진 종족은 한 번 처치로 공격력과 공속이 함께 오른다.
        float perKill  = Has(tags, MonsterTag.Ferocity)
                       ? MonsterSynergyRule.FerocityPerKill(Tier(MonsterTag.Ferocity)) : 0f;
        float perSpeed = Has(tags, MonsterTag.Beast)
                       ? MonsterSynergyRule.BeastSpeedPerKill(Tier(MonsterTag.Beast)) : 0f;
        float speedMax = Has(tags, MonsterTag.Beast)
                       ? MonsterSynergyRule.BeastSpeedMax(Tier(MonsterTag.Beast)) : 0f;

        // 투지 금은 누적을 종족 단위로 물려받는다. 그 외에는 0 에서 시작한다.
        bool carries  = Has(tags, MonsterTag.Ferocity)
                     && MonsterSynergyRule.FerocityCarries(Tier(MonsterTag.Ferocity));
        int  carryKey = carries ? speciesKey : 0;

        if (perKill > 0f || perSpeed > 0f)
            SetKillStack(em, e, perKill, perSpeed, speedMax, carryKey);

        // ── 중첩 2단계 — 소환 직후 잠깐 빠르게 ──
        //   전장까지 걸어 나가는 시간을 줄인다. 전투력이 아니라 템포에 붙는 보너스다.
        //   ⚠ 스탯 쪽과 같은 조건이다 — 켜진 시너지를 가진 몬스터만 받는다.
        if (MonsterSynergyRule.HasAnyActive(tags)
            && MonsterSynergyRule.StackStep(MonsterSynergyRule.ActiveCount) >= 2)
            // ⚠ Multiply 의 Delta 는 배율이다 (1 + 비율) — 한때 0.50 을 그대로 넣어
            //   '돌격' 이 이동 속도를 **절반으로** 깎고 있었다 (2026-09-10 수정).
            AddStatus(em, e, StatType.MoveSpeed,
                      1f + MonsterSynergyRule.StackRushMoveBonus, EffectMode.Multiply,
                      MonsterSynergyRule.StackRushSeconds);
    }

    /// <summary>
    /// 술법 리더 — 쿨다운을 채운 채로 내보낸다.
    ///
    /// ⚠ 스킬을 쏘는 게 아니라 **준비만** 시킨다
    ///   소환 지점에는 적이 없다. 여기서 발사하면 허공에 버리는 것이라
    ///   금 단계의 값이 0 이 된다. 쿨다운만 채워 두면 적을 처음 마주치는
    ///   순간 첫 스킬이 나간다.
    /// </summary>
    public static void PrimeSkill(GameObject monster, MonsterTag tags)
    {
        if (!Has(tags, MonsterTag.Sorcery)) return;

        float charge = MonsterSynergyRule.SorceryStartCharge(Tier(MonsterTag.Sorcery));
        if (charge <= 0f) return;

        if (!TryEntity(monster, out EntityManager em, out Entity e)) return;
        if (!em.HasComponent<GeneralActiveSkillComponent>(e)) return;

        var skill = em.GetComponentData<GeneralActiveSkillComponent>(e);
        if (skill.SkillId == (int)ActiveSkillId.None) return;

        // 남은 쿨다운을 charge 비율만큼 미리 지운다. 1 이면 즉시 준비 완료.
        skill.CooldownRemaining *= 1f - Mathf.Clamp01(charge);
        em.SetComponentData(e, skill);
    }

    // ══════════════════════════════════════════════════════════
    //  ③ 사망 — MonsterDeathWatcher 가 부른다
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 죽는 순간 터지는 시너지 효과.
    /// </summary>
    /// <param name="generation">
    /// 이 개체의 세대. 부활은 <b>0세대만</b> 한다 — 안 그러면 언데드 한 마리가
    /// 영원히 일어난다 (종족 재조립과 같은 규칙).
    /// </param>
    /// <param name="origin">
    /// 죽은 개체가 물려주는 것 — 카드 레벨·융합 개성·외형 시드.
    /// ⚠ 부활은 <b>같은 몸이 다시 서는 것</b>이다 (사용자 확정, 2026-09-07).
    ///   Lv5 스켈레톤이 Lv1 로 일어나거나 얼굴이 바뀌면 부활로 읽히지 않는다.
    /// </param>
    public static void OnDeath(MonsterSpeciesData species, SummonerData summoner,
                               Vector3 at, int generation, in MonsterOrigin origin)
    {
        MonsterTag tags = species.Tags;
        if (tags == MonsterTag.None) return;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        EntityManager em = world.EntityManager;

        // ── 언데드 — 부활 ──
        SynergyTier undead = Tier(MonsterTag.Undead);

        // 금은 세대 한도를 한 칸 늘린다 — 부활한 개체가 한 번 더 일어난다.
        int reviveLimit = SpeciesPassiveRule.MaxReproduceGeneration
                        + MonsterSynergyRule.UndeadExtraGeneration(undead);

        if (Has(tags, MonsterTag.Undead) && generation <= reviveLimit)
        {
            SynergyTier t = undead;
            float chance  = MonsterSynergyRule.UndeadReviveChance(t);

            if (chance > 0f && Random.value <= chance)
            {
                // 체력만 깎는다 — 공격력은 그대로다.
                // "체력 X% 로 다시 일어난다" 가 약속이다. 공격력까지 깎으면
                // 전투력 환산(1 + 확률×체력비)이 실제보다 후하게 잡힌다.
                GameObject revived = MonsterSpawner.SpawnDerived(
                    species, summoner, at,
                    MonsterSynergyRule.UndeadReviveHp(t), generation + 1, origin,
                    scaleHpOnly: true);

                ApplyReviveGuard(revived, MonsterSynergyRule.UndeadReviveGuard(t));
            }
        }

        // ── 숲 — 주변 숲 아군 회복 (은부터) ──
        if (Has(tags, MonsterTag.Forest))
        {
            float ratio = MonsterSynergyRule.ForestDeathHeal(Tier(MonsterTag.Forest));
            if (ratio > 0f) HealForestAllies(em, at, ratio);
        }

        // ── 역병 금 — 죽을 때 주변 적에게 역병이 퍼진다 ──
        if (Has(tags, MonsterTag.Plague)
            && MonsterSynergyRule.PlagueSpreadsOnDeath(Tier(MonsterTag.Plague)))
            SpreadPlague(em, at, species);

        // ── 투지 — 누적치를 주변 투지 아군에게 (은부터) ──
        if (Has(tags, MonsterTag.Ferocity))
        {
            float share = MonsterSynergyRule.FerocityInherit(Tier(MonsterTag.Ferocity));
            if (share > 0f) InheritFerocity(em, at, share);
        }
    }

    /// <summary>
    /// 숲 금 — 쓰러진 몬스터를 제 라인 대기열에 도로 세운다.
    ///
    /// ⚠ 마나를 다시 내지 않는다 — 계열을 완주한 대가다
    ///   늘어나지는 않는다. 죽은 그 한 마리가 그 자리에 다시 설 뿐이다.
    ///
    /// ⚠ 대기열이 없으면(전투 밖) 아무것도 하지 않는다.
    /// </summary>
    public static void OnDeathReturnToLine(MonsterSpeciesData species, int lane)
    {
        if (!Has(species.Tags, MonsterTag.Forest))                              return;
        if (!MonsterSynergyRule.ForestReturnsOnDeath(Tier(MonsterTag.Forest)))  return;

        SummonReservation reservation = SummonController.Instance?.Reservation;
        if (reservation == null) return;

        reservation.EnqueueOne(species, lane);
    }

    // ── 사망 효과 세부 ───────────────────────────────────────

    static void ApplyReviveGuard(GameObject revived, float guard)
    {
        if (revived == null || guard <= 0f) return;
        if (!TryEntity(revived, out EntityManager em, out Entity e)) return;

        // 방어율을 잠깐 올려 "일어나는 동안 덜 맞는다" 를 만든다.
        // 새 피해감소 스탯을 만들지 않는다 — 방어율이 이미 그 뜻이다.
        AddStatus(em, e, StatType.Defense, guard, EffectMode.Add,
                  MonsterSynergyRule.UndeadGuardSeconds);
    }

    static void HealForestAllies(EntityManager em, Vector3 at, float ratio)
    {
        float radSq = MonsterSynergyRule.ForestHealRadius * MonsterSynergyRule.ForestHealRadius;

        foreach (Entity ally in SkillCrowdControl.CollectAllies(em, Faction.Monster))
        {
            // ⚠ 숲 아군만이다 — 이 줄이 없으면 그냥 광역 힐이 된다
            //   (모든 몬스터를 회복하면 숲 시너지가 다른 시너지의 몫까지 먹는다)
            if (!HasTag(em, ally, MonsterTag.Forest)) continue;

            if ((SkillCrowdControl.PositionOf(em, ally) - at).sqrMagnitude > radSq) continue;
            if (!em.HasComponent<StatComponent>(ally)) continue;

            // 회복량은 **받는 쪽의** 최대 체력 기준이다. 죽은 쪽 기준으로 하면
            // 트롤 한 마리가 죽을 때 슬라임이 통째로 가득 찬다.
            float max = em.GetComponentData<StatComponent>(ally).Final[StatType.MaxHp];
            SkillCrowdControl.Heal(em, ally, max * ratio, Entity.Null);
        }
    }

    /// <summary>
    /// 죽은 자리 주변 적을 중독시킨다.
    /// 종족 패시브의 역병 폭발(SpeciesPassiveRuntime.PoisonNearbyEnemies)과 같은 길이다.
    /// </summary>
    static void SpreadPlague(EntityManager em, Vector3 at, MonsterSpeciesData species)
    {
        List<Entity> targets = SkillCrowdControl.CollectEnemiesInRadius(
            em, at, MonsterSynergyRule.PlagueBurstRadius, Faction.Monster);

        // 죽은 개체의 공격력이 아니라 **종족 기본 공격력**을 기준으로 삼는다 —
        // 시너지·품질로 부푼 값을 쓰면 사망 전파만 유독 세진다.
        float dps = MonsterDotRule.Scale(species.Attack * MonsterSynergyRule.PlagueBurstDps);

        foreach (Entity target in targets)
            SkillCrowdControl.AddEffect(em, target, StatType.MaxHp, dps, EffectMode.Dot,
                                        MonsterSynergyRule.PlagueSeconds, ActiveSkillId.PoisonZone);
    }

    static void InheritFerocity(EntityManager em, Vector3 at, float share)
    {
        float radSq = MonsterSynergyRule.FerocityInheritRadius
                    * MonsterSynergyRule.FerocityInheritRadius;

        foreach (Entity ally in SkillCrowdControl.CollectAllies(em, Faction.Monster))
        {
            if (!em.HasComponent<SynergyKillStackComponent>(ally)) continue;
            if ((SkillCrowdControl.PositionOf(em, ally) - at).sqrMagnitude > radSq) continue;

            var s = em.GetComponentData<SynergyKillStackComponent>(ally);

            // ⚠ 스택 수가 아니라 **비율**을 넘긴다
            //   스택을 넘기면 상한(MaxStacks)을 우회해 끝없이 불어난다.
            s.Inherited += s.PercentPerKill * s.MaxStacks * share;
            em.SetComponentData(ally, s);
        }
    }

    // ── ECS 헬퍼 ─────────────────────────────────────────────

    static bool Has(MonsterTag tags, MonsterTag one) => (tags & one) != 0;

    /// <summary>필드에 선 이 개체가 그 표식을 갖고 있나. 범위 효과가 대상을 고를 때 쓴다.</summary>
    static bool HasTag(EntityManager em, Entity e, MonsterTag one)
        => em.HasComponent<MonsterTagComponent>(e)
        && (em.GetComponentData<MonsterTagComponent>(e).Tags & one) != 0;

    static SynergyTier Tier(MonsterTag tag) => MonsterSynergyRule.TierOf(tag);

    static bool TryEntity(GameObject go, out EntityManager em, out Entity e)
    {
        em = default;
        e  = Entity.Null;

        if (go == null) return false;
        if (!go.TryGetComponent<EntityLink>(out var link)) return false;
        if (link.Entity == Entity.Null) return false;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        em = world.EntityManager;
        if (!em.Exists(link.Entity)) return false;

        e = link.Entity;
        return true;
    }

    static float AttackOf(EntityManager em, Entity e)
        => em.HasComponent<StatComponent>(e)
         ? em.GetComponentData<StatComponent>(e).Final[StatType.Attack]
         : 0f;

    /// <summary>종족 재생 위에 **더한다**. 덮어쓰면 트롤에게 시너지가 무의미해진다.</summary>
    static void AddRegen(EntityManager em, Entity e, float ratioPerSecond,
                         float lowHpMult, float lowHpThreshold)
    {
        if (em.HasComponent<RegenComponent>(e))
        {
            var cur = em.GetComponentData<RegenComponent>(e);
            cur.RatioPerSecond += ratioPerSecond;

            // 배율은 더하지 않고 센 쪽을 남긴다 — 종족 재생과 시너지가 겹칠 때
            // 곱이 두 번 걸리면 트롤만 회복이 폭주한다.
            if (lowHpMult > cur.LowHpMult)
            {
                cur.LowHpMult      = lowHpMult;
                cur.LowHpThreshold = lowHpThreshold;
            }

            em.SetComponentData(e, cur);
            return;
        }

        em.AddComponentData(e, new RegenComponent
        {
            RatioPerSecond = ratioPerSecond,
            Timer          = 0f,
            LowHpMult      = lowHpMult,
            LowHpThreshold = lowHpThreshold,
        });
    }

    /// <summary>
    /// 반사만 얹는다.
    /// ⚠ 컴포넌트를 새로 만들어 넣으면 종족의 피격 반응(중독·둔화)이 지워진다.
    /// </summary>
    static void AddThorn(EntityManager em, Entity e, float thorn)
    {
        if (em.HasComponent<RetaliateComponent>(e))
        {
            var cur = em.GetComponentData<RetaliateComponent>(e);
            cur.ThornRatio += thorn;
            em.SetComponentData(e, cur);
            return;
        }

        em.AddComponentData(e, new RetaliateComponent { ThornRatio = thorn });
    }

    /// <summary>
    /// 공격에 중독을 싣는다.
    /// ⚠ 화상(BurnOnAttack)과 같은 컴포넌트를 쓴다 — 센 쪽을 남긴다.
    ///   둘을 더하면 화염 멧돼지만 두 배로 아프다.
    /// </summary>
    static void AddPoisonOnHit(EntityManager em, Entity e, float dps, float amplify)
    {
        var next = new InflictOnHitComponent
        {
            EffectStat     = StatType.MaxHp,   // Dot 모드에서는 무시된다
            EffectDelta    = dps,
            EffectMode     = EffectMode.Dot,
            EffectDuration = MonsterSynergyRule.PlagueSeconds,

            // 역병 은·금 — 중독된 적은 피해를 더 받는다.
            //
            // 방어율을 깎아서 표현한다 (새 스탯을 만들지 않는다).
            // 방어율은 이 게임에서 깎이는 비율 자체라, 그만큼 빼면 받는 피해가
            // 그만큼 는다. 방어율 0 인 적에게는 정확히 +N%, 단단한 적에게는
            // 그보다 크게 걸린다 — 단단할수록 역병이 잘 듣는 셈이라 방향도 맞다.
            ExtraStat      = amplify > 0f ? StatType.Defense : StatType.MaxHp,
            ExtraDelta     = -amplify,
            ExtraMode      = EffectMode.Add,
            ExtraDuration  = amplify > 0f ? MonsterSynergyRule.PlagueSeconds : 0f,
        };

        if (em.HasComponent<InflictOnHitComponent>(e))
        {
            var cur = em.GetComponentData<InflictOnHitComponent>(e);
            if (cur.EffectMode == EffectMode.Dot && cur.EffectDelta >= dps) return;

            em.SetComponentData(e, next);
            return;
        }

        em.AddComponentData(e, next);
    }

    static void SetKillStack(EntityManager em, Entity e,
                             float perKill, float perSpeed, float speedMax, int carryKey)
    {
        SynergyTier ferocity = Tier(MonsterTag.Ferocity);

        var value = new SynergyKillStackComponent
        {
            PercentPerKill = perKill,
            MaxStacks      = MonsterSynergyRule.FerocityMaxStacks(ferocity),

            // ⚠ 물려받은 누적을 여기서 심는다 — Applied 는 0 으로 둔다
            //   그러면 다음 프레임에 킬 시스템이 차액을 한 번에 얹는다.
            Stacks         = MonsterSynergyRule.CarriedStacks(carryKey),
            CarryKey       = carryKey,

            Inherited      = 0f,
            Applied        = 0f,
            SpeedPerKill   = perSpeed,
            SpeedMax       = speedMax,
            SpeedApplied   = 0f,
        };

        SetOrAdd(em, e, value);
    }

    /// <summary>있으면 덮고 없으면 붙인다.</summary>
    static void SetOrAdd<T>(EntityManager em, Entity e, T value) where T : unmanaged, IComponentData
    {
        if (em.HasComponent<T>(e)) em.SetComponentData(e, value);
        else                       em.AddComponentData(e, value);
    }

    /// <summary>
    /// 상태효과를 건다. duration 이 음수면 영구다.
    /// ⚠ 영구여도 Remaining 은 0 보다 커야 한다 (UnitStatusEffectSystem 참고).
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
}
