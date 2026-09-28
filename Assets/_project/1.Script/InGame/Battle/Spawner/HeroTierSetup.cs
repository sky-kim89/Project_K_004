using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  HeroTierSetup.cs
//  일반 용사를 엘리트 / 보스 계층으로 승격시킨다.
//
//  ■ 엘리트·보스는 용사(적) 전용 개념이다
//    원작에서는 EnemyRuntimeBridge 가 Enemy/Elite/Boss 세 계층을 모두 맡았다.
//    이 게임에서 그 프리팹 계열은 플레이어의 소환 몬스터가 됐고,
//    몬스터에는 계층이 없다 — 강함은 품질과 소환력이 정한다.
//    그래서 계층 개념 전체가 용사 쪽으로 넘어왔다.
//
//  ■ 새 시스템을 만들지 않는다 — 기존 장치에 얹기만 한다
//    BossComponent / EliteComponent 만 붙이면 나머지는 원작 코드가 알아서 한다.
//
//      · UnitAttackSystem 의 근접 공격 잡은
//        [WithNone(typeof(BossComponent))] 이라 보스를 자동으로 놓아준다.
//      · BossAttackSystem 이 대신 잡아 AoE 평타 + 넉백 + 평타 ×3 을 돌린다.
//        (BasicAttackMultiplier = 3f — "범위 공격은 기본 공격의 300%")
//      · TopBarUI 의 보스 HP 바는 BossComponent 엔티티를 쿼리로 찾는다.
//        진영을 따지지 않으므로 용사 보스도 그대로 잡힌다.
//
//    즉 보스 UI·공격 패턴 모두 원작 것을 손대지 않고 그대로 쓴다.
//
//  ■ 공속만은 직접 깎아야 한다
//    원작 보스는 스탯 범위 자체가 느리게(BossRange.AttackSpeed 0.133~0.30) 잡혀
//    있었다. 용사는 직업 스탯 범위(0.9~1.9)에서 굴러 나오므로 그대로 두면
//    "빠르게 연타하는 보스" 가 된다. 여기서 1/3 로 나눈다.
//
//    ⚠ 공격력은 건드리지 않는다
//      DPS 손실은 BossAttackSystem 의 평타 ×3 이 되돌린다. 여기서 Attack 까지
//      3배로 올리면 스킬 피해까지 3배가 된다 — 원작이 이 함정을 주석으로
//      남겨 뒀다(GameplayConfig BossRange).
// ============================================================

public static class HeroTierSetup
{
    // ── 스킬 풀 (원작 EnemyRuntimeBridge 에서 이관) ───────────

    /// <summary>엘리트가 쓰는 스킬 — 단순 공격계만.</summary>
    static readonly int[] EliteSkillPool =
    {
        (int)ActiveSkillId.HeavyStrike,   // 1  강타
        (int)ActiveSkillId.LeapStrike,    // 3  도약강타
        (int)ActiveSkillId.Bind,          // 12 속박
        (int)ActiveSkillId.Berserker,     // 14 광전사
        (int)ActiveSkillId.IronShield,    // 15 철벽방어
        (int)ActiveSkillId.Shockwave,     // 18 충격파
    };

    /// <summary>
    /// 보스가 쓰는 스킬 — 엘리트보다 판이 크고 광역 위주다.
    ///
    /// ⚠ 소환·치유 계열은 넣지 않는다
    ///   보스전이 늘어지기만 하고, 플레이어가 손쓸 수단이 없는 오토배틀에서는
    ///   "언제 끝나나" 만 남는다. 보스는 짧고 아프게 끝나야 한다.
    /// </summary>
    static readonly int[] BossSkillPool =
    {
        (int)ActiveSkillId.Meteor,        //  9 메테오
        (int)ActiveSkillId.Shockwave,     // 18 충격파
        (int)ActiveSkillId.Blizzard,      // 10 블리자드
        (int)ActiveSkillId.ArrowRain,     // 16 화살비
        (int)ActiveSkillId.Bind,          // 12 속박
    };

    // ── 상수 ─────────────────────────────────────────────────

    /// <summary>보스 공속 배율. 원작 보스가 일반 적의 1/3 인 것과 같다.</summary>
    public const float AttackSpeedScale = 1f / 3f;

    // ── 몸집 — 계층이 눈으로 보여야 한다 (사용자 지시, 2026-09-11) ──
    //
    //  일반 1 · 엘리트 1.2 · 보스 1.4. 한 부대 안에서 누가 우두머리인지가
    //  HP 바를 보기 전에 실루엣으로 읽혀야 한다.
    //  ⚠ 여기서 곱하지 않는다 — 크기는 엔티티가 만들어지기 **전**에 정해져야
    //    분리 반경(UnitSizeComponent.Radius)이 따라온다. HeroSpawner 가 이 값을
    //    GeneralRuntimeBridge.Initialize(scaleMult) 로 넘긴다.
    //  ⚠ 무한 보스는 이미 ×2 라 큰 쪽을 쓴다 (EndlessBossRule) — 곱하지 않는다.
    public const float EliteScale = 1.2f;
    public const float BossScale  = 1.4f;

    /// <summary>계층이 정하는 몸집. 보스가 이긴다 (둘 다 켜진 편성은 보스로 붙는다).</summary>
    public static float ScaleFor(bool isBoss, bool isElite)
        => isBoss ? BossScale : isElite ? EliteScale : 1f;

    /// <summary>보스 AoE 반경.</summary>
    public const float AoeRadius = 2.5f;

    /// <summary>보스 AoE 스플래시 비율. 직격 대상 주변은 이 비율만큼 받는다.</summary>
    public const float AoeSplashRatio = 0.6f;

    /// <summary>
    /// 광폭화 쿨다운 (초) — 1분에 1스택.
    ///
    /// ⚠ 아군 배출 가속(SpawnPaceRule.RushStepSeconds)이 이 값을 읽는다
    ///   둘은 같은 시계여야 한다 — "적이 세지는 그 순간 우리도 빨라진다" 가
    ///   화면에서 한 사건으로 읽혀야 하기 때문이다. 여기만 고치면 양쪽이 함께 움직인다.
    ///
    /// ⚠ 난이도 쿨감(CooldownScale)을 곱하지 않는다
    ///   광폭화는 연출이 아니라 교착을 끝내는 시계다.
    /// </summary>
    public const float EnrageCooldown = 60f;

    static bool FrenzyEnabled => DifficultyConfig.CurrentTier()?.FrenzyPatterns ?? false;

    /// <summary>난이도 '각성' 의 엘리트·보스 스킬 쿨감. 원작과 같은 공식이다.</summary>
    static float CooldownScale =>
        Mathf.Max(0.45f, 1f - (DifficultyConfig.CurrentTier()?.BossCooldownCut ?? 0f));

    // ── 보스 승격 ────────────────────────────────────────────

    /// <summary>
    /// 이미 초기화가 끝난 용사 엔티티를 보스로 승격시킨다.
    /// GeneralRuntimeBridge.Initialize 가 끝난 뒤에 부를 것.
    /// </summary>
    public static void ApplyBoss(Entity entity, string unitName)
    {
        EntityManager em = GetManager(entity, "ApplyBoss");
        if (em == default) return;

        // ── ① 보스 표식 — 이것 하나로 공격 시스템과 UI 가 갈린다 ──
        var boss = new BossComponent
        {
            PhaseCount              = 1,
            CurrentPhase            = 1,
            Phase2HpRatio           = 0.5f,
            Phase3HpRatio           = 0.25f,
            CCResistance            = 1f,
            KnockbackResistance     = 0.8f,
            AoeRadius               = AoeRadius,
            AoeSplashRatio          = AoeSplashRatio,
            AttackKnockbackForce    = 4.0f,
            AttackKnockbackDuration = 0.25f,
        };

        SetOrAdd(em, entity, boss);

        // ── ② 대표 스킬 — '폭주'(불지옥) 난이도에서만 ──
        //
        //  BossSkillPool 은 판 전체를 덮는 광역기다. 오토배틀이라 플레이어가 피할
        //  수단이 없어서, 낮은 난이도에서는 이 한 방이 승패를 통째로 정했다.
        //  아래 난이도의 보스는 AoE 평타 + 돌진으로 싸운다.
        if (FrenzyEnabled)
        {
            SetOrAdd(em, entity, new GeneralActiveSkillComponent
            {
                // ⚠ GetHashCode 가 아니라 안정 해시다
                //   문자열 해시 시드는 프로세스마다 바뀐다 — 앱을 다시 켤 때마다
                //   같은 보스의 스킬이 갈리면 안 된다.
                SkillId           = BossSkillPool[
                                        (int)(UnitJobRoller.StableHash(unitName) % (uint)BossSkillPool.Length)],
                EffectValue       = 2.5f,   // 엘리트(1.5)보다 크게
                EffectRadius      = 3.5f,
                EffectDuration    = 4.0f,
                Cooldown          = 16f * CooldownScale,
                CooldownRemaining = 8f,     // 등장 직후 바로 터지면 대응할 여지가 없다
            });
        }

        EnsureExecuteBuffer(em, entity);

        // ── ③ 행동 패턴 슬롯 ──
        //
        //  패턴도 스킬이다. 대표 스킬과 달리 AI 만 발동하는 슬롯에 꽂는다.
        var slots = GetOrAddSlots(em, entity);
        slots.Clear();
        // 돌진은 항상 — 보스를 보스로 보이게 하는 동작이고, 직선 한 번이라
        // 광역 스킬처럼 판을 쓸어버리지 않는다.
        slots.Add(PatternSlot(ActiveSkillId.BossCharge, cooldown: 9f, first: 5f));
        if (FrenzyEnabled)
            slots.Add(PatternSlot(ActiveSkillId.BossSlam, cooldown: 13f, first: 9f));
        // 광폭화 — 난이도와 무관하게 항상. 교착을 끝내는 장치라 빼면 안 된다.
        slots.Add(PatternSlot(ActiveSkillId.BossEnrage, cooldown: EnrageCooldown,
                                                        first:    EnrageCooldown));

        // ── ④ 공속 1/3 — "느리고 무겁게" ──
        ScaleAttackSpeed(em, entity, AttackSpeedScale);

        // ── ⑤ 원거리 태그 제거 ──
        //
        //  보스 평타는 AoE 근접 판정이다. 궁수·법사 용사가 보스로 뽑히면
        //  RangedTag 가 붙어 있는데, 그대로 두면 발사체 잡과 보스 잡이 동시에
        //  돌아 한 번 공격에 두 번 때린다.
        if (em.HasComponent<RangedTag>(entity))
            em.RemoveComponent<RangedTag>(entity);
    }

    // ── 엘리트 승격 ──────────────────────────────────────────

    /// <summary>
    /// 이미 초기화가 끝난 용사 엔티티를 엘리트로 승격시킨다.
    /// 보스와 달리 공격 방식은 그대로다 — 스킬 하나가 붙을 뿐이다.
    /// </summary>
    public static void ApplyElite(Entity entity, string unitName)
    {
        EntityManager em = GetManager(entity, "ApplyElite");
        if (em == default) return;

        SetOrAdd(em, entity, new EliteComponent
        {
            HasSkill            = true,
            KnockbackResistance = 0.5f,
        });

        // 이름 시드로 스킬 결정 — 같은 이름은 항상 같은 스킬.
        int skillId = EliteSkillPool[
                          (int)(UnitJobRoller.StableHash(unitName) % (uint)EliteSkillPool.Length)];

        SetOrAdd(em, entity, new GeneralActiveSkillComponent
        {
            SkillId           = skillId,
            EffectValue       = 1.5f,
            EffectRadius      = 2.0f,
            EffectDuration    = 3.0f,
            Cooldown          = 12f * CooldownScale,
            CooldownRemaining = 5f,     // 처음 5초 후 첫 발동
        });

        EnsureExecuteBuffer(em, entity);

        // 폭주(무간) 난이도에서 엘리트가 보스의 돌진을 배운다.
        // 슬롯이 따로라 원래 갖고 있던 스킬은 그대로 쓴다.
        var slots = GetOrAddSlots(em, entity);
        slots.Clear();
        if (FrenzyEnabled)
            slots.Add(PatternSlot(ActiveSkillId.BossCharge, cooldown: 11f, first: 7f));
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────

    static EntityManager GetManager(Entity entity, string caller)
    {
        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return default;

        EntityManager em = world.EntityManager;
        if (!em.Exists(entity))
        {
            Debug.LogError($"[HeroTierSetup.{caller}] 엔티티가 없습니다. Initialize 뒤에 불러야 합니다.");
            return default;
        }
        return em;
    }

    static void SetOrAdd<T>(EntityManager em, Entity entity, T data)
        where T : unmanaged, IComponentData
    {
        if (em.HasComponent<T>(entity)) em.SetComponentData(entity, data);
        else                            em.AddComponentData(entity, data);
    }

    /// <summary>
    /// ActiveSkillCooldownSystem 이 AppendToBuffer 로 접근한다.
    /// 스킬 유무와 상관없이 반드시 있어야 한다 — 없는 쪽에서 터진다.
    /// </summary>
    static void EnsureExecuteBuffer(EntityManager em, Entity entity)
    {
        if (!em.HasBuffer<ActiveSkillExecuteEvent>(entity))
            em.AddBuffer<ActiveSkillExecuteEvent>(entity);
    }

    static DynamicBuffer<ActiveSkillSlot> GetOrAddSlots(EntityManager em, Entity entity)
        => em.HasBuffer<ActiveSkillSlot>(entity)
               ? em.GetBuffer<ActiveSkillSlot>(entity)
               : em.AddBuffer<ActiveSkillSlot>(entity);

    static ActiveSkillSlot PatternSlot(ActiveSkillId id, float cooldown, float first) => new()
    {
        SkillId           = (int)id,
        Cooldown          = cooldown,
        CooldownRemaining = first,
    };

    /// <summary>
    /// Base 와 Final 을 함께 조정한다.
    /// Base 만 내리면 UnitStatusEffectSystem 이 다음 프레임에 Final 을 다시
    /// 계산하기 전까지 한 박자 빠르게 때린다.
    /// </summary>
    static void ScaleAttackSpeed(EntityManager em, Entity entity, float scale)
    {
        if (!em.HasComponent<StatComponent>(entity)) return;

        var stat = em.GetComponentData<StatComponent>(entity);
        stat.Base [StatType.AttackSpeed] *= scale;
        stat.Final[StatType.AttackSpeed] *= scale;
        em.SetComponentData(entity, stat);
    }
}
