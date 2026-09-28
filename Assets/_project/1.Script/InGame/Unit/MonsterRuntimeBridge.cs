using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  MonsterRuntimeBridge.cs
//  소환 몬스터(Monster.prefab) 전용 RuntimeBridge. 진영은 아군이다.
//
//  ■ 이름이 바뀐 이유
//    원작에서는 EnemyRuntimeBridge 였고 Enemy / Elite / Boss 세 계층을 모두 맡았다.
//    이 게임에서 그 프리팹 계열은 **플레이어가 소환하는 몬스터**가 됐다 (Faction.cs).
//
//  ■ Elite / Boss 는 여기서 다루지 않는다 — 용사(적) 전용이다
//    소환 몬스터에는 계층이 없다. 몬스터의 강함은
//      · 품질(UnitGrade, 도감 개체당 영구)
//      · 소환사의 소환력
//    이 정하지 "엘리트냐 보스냐" 가 정하지 않는다.
//
//    엘리트·보스는 용사 쪽 개념으로 옮겼다 → HeroTierSetup.cs
//    (엘리트 스킬 풀 · 보스 패턴 슬롯 · AoE 평타 · 광폭화가 전부 그쪽에 있다)
//
//  ■ 스탯은 두 경로가 있다
//    · MonsterStatComposer — 정식 경로. 종족 기본 스탯 + 소환력 + 품질.
//      소환 시스템이 만든 UnitStat 을 InitializeWithStat 으로 넘긴다.
//    · MonsterStatRoller   — 원작에서 물려받은 이름 시드 롤러.
//      소환 시스템이 붙기 전까지 쓰는 임시 경로다.
//
//  ■ 근접이냐 원거리냐는 종족이 정한다 (MonsterAttackKind)
//    원거리 몬스터에게는 RangedTag · UnitJobComponent · 발사 요청 버퍼 세 개를
//    한꺼번에 붙인다. 셋 다 있어야 RangedAttackJob 쿼리에 걸린다 —
//    하나만 빠지면 원거리에도 근접에도 안 걸려 **아무 공격도 못 하는 유닛**이 된다.
//
//  ■ 패시브 슬롯을 항상 들고 있다
//    카드 레벨로 열린 패시브·개성이 얹힐 자리다. 비어 있어도(슬롯 0개)
//    컴포넌트 자체는 붙여 둔다 — 나중에 붙이려면 구조 변경이 필요한데,
//    그건 전투 중에 하기에 비싼 일이다.
// ============================================================

public class MonsterRuntimeBridge : UnitRuntimeBridge
{
    // ⚠ SpawnEntity() 안에서 읽히는 값들 — Initialize 가 **먼저** 채워야 한다
    //   AddComponents 는 최초 1회, OnEntityReset 은 재사용 때 돌면서 둘 다
    //   이 필드를 본다. 비워 두면 풀에서 나온 개체가 지난 종족의 공격 형태를
    //   그대로 들고 등장한다.
    MonsterAttackKind    _attackKind = MonsterAttackKind.Melee;
    ActiveSkillId        _activeSkill = ActiveSkillId.None;
    MonsterProjectileKind _projectile = MonsterProjectileKind.Arrow;
    float                _projectileSpeed;

    PassiveSkillType _passive0, _passive1, _passive2;
    byte             _passiveCount;

    /// <summary>이 개체가 소환한 종족. 사망 훅(MonsterDeathWatcher)이 읽는다.</summary>
    public MonsterSpeciesData Species { get; private set; }

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// 소환 시스템이 만든 스탯을 그대로 받아 세운다. <b>정식 경로다.</b>
    /// 스탯 합성 규칙은 MonsterStatComposer 한 곳이 소유한다.
    /// </summary>
    /// <param name="unitName">외형·시드용 고유 이름</param>
    /// <param name="stat">MonsterStatComposer.Compose 결과</param>
    /// <param name="species">종족 정의 — 외형·공격 형태가 여기서 갈린다</param>
    /// <param name="passives">카드 레벨·합성으로 열린 패시브 (최대 3개까지 쓴다)</param>
    /// <param name="scaleMultiplier">프리팹 원본 크기에 곱할 배율</param>
    public void InitializeWithStat(string unitName, UnitStat stat,
                                   MonsterSpeciesData species,
                                   System.Collections.Generic.List<PassiveSkillType> passives = null,
                                   float scaleMultiplier = 1f)
    {
        _unitName = unitName;

        Species          = species;
        _attackKind      = species.AttackKind;
        _activeSkill     = species.ActiveSkill;
        _projectile      = species.Projectile;
        _projectileSpeed = species.ProjectileSpeed;

        CachePassives(passives);

        // 도감에서 이 종족에 끼워 둔 장비의 겉모습 (스탯은 이미 Compose 가 얹었다).
        // ⚠ 스폰마다 새로 만든다 — 장착은 도감에서 언제든 바뀐다 (MonsterGearRule 주석)
        MonsterGearVisual gear = MonsterGearRule.BuildVisual(species.Id);

        // 크기는 SpawnEntity() 전에 확정해야 한다
        // — UnitSizeComponent.Radius 가 localScale 에서 나온다.
        //
        // ⚠ 장비 '덩치'(Bulk)가 여기에 곱해진다
        //   그래서 히트박스·분리 반경도 함께 커진다. 그게 맞다 — 커 보이는데
        //   실제로는 안 커지면 라인에서 서로를 통과하는 것처럼 보인다.
        //   대신 MonsterGearData.ScaleBonus 의 상한을 낮게(0.2) 잡아 둔다.
        float scale = Mathf.Max(0.01f, scaleMultiplier) * (1f + gear.ScaleBonus);

        // ⚠ 종족마다 **몸 비율**이 다르다 (사용자 지적, 2026-09-09)
        //   같은 라이브러리를 쓰는 계보(슬라임 넷·늑대 둘 …)는 색조만으로는
        //   전장에서 갈리지 않는다. 색은 원본 그림에 곱해지는 값이라, 바탕이
        //   짙은 종족일수록 차이가 묻힌다. **실루엣**은 그런 사정을 타지 않는다.
        //   독 슬라임은 길쭉하고 힐 슬라임은 납작하다 — 한 눈에 갈린다.
        //   ⚠ 히트박스·분리 반경은 max(x, y) 에서 나온다 (UnitSizeComponent).
        //     비율을 크게 벌리면 그쪽도 함께 커진다.
        Vector2 stretch = species.BodyStretch;

        transform.localScale = Vector3.Scale(
            _baseScale, new Vector3(scale * stretch.x, scale * stretch.y, scale));

        _spawnScale          = transform.localScale;

        _stat = stat;

        // 인간형이면 CharacterBuilder 합성, 비인간형이면 완성 라이브러리 직접 할당.
        GetComponent<MonsterAppearanceBridge>().Apply(species, unitName, gear);

        // 머리 위 표식 — 같은 그림의 계보를 기호로 가른다 (MonsterMarkView).
        // ⚠ 스폰마다 부른다 — None 이면 끈다 (풀 재사용).
        if (!TryGetComponent<MonsterMarkView>(out var mark)) mark = gameObject.AddComponent<MonsterMarkView>();
        mark.Setup(species.Mark);

        // 좋은 장비를 낀 개체만 빛이 돈다 (MonsterGearRule.AuraMinGrade).
        // ⚠ 컴포넌트가 없어도 그냥 넘어간다 — Monster.prefab 을 아직 다시 굽지
        //   않았을 수 있다. 장식이 빠지는 것뿐이라 전투를 막을 이유가 없다.
        if (TryGetComponent<MonsterGearAuraView>(out var aura)) aura.Setup(gear);

        SpawnEntity();
    }

    /// <summary>
    /// 이름 시드로 스탯을 굴려 세운다. 원작에서 물려받은 경로다.
    ///
    /// ⚠ 임시 경로다 — 소환 시스템이 붙으면 InitializeWithStat 으로 갈아탄다.
    ///   이쪽은 종족 기본 스탯도 소환력도 품질도 반영하지 않는다.
    /// </summary>
    public void Initialize(string unitName, EnemyRace race,
                           int level = 1, float statMultiplier = 1f, float stageBias = 0f,
                           float scaleMultiplier = 1f)
    {
        _unitName = unitName;

        transform.localScale = _baseScale * Mathf.Max(0.01f, scaleMultiplier);
        _spawnScale          = transform.localScale;

        _stat = MonsterStatRoller.Roll(unitName, SpawnUnitType.Monster,
                                       level, statMultiplier, stageBias);

        GetComponent<UnitAppearanceBridge>().ApplyEnemy(race, unitName);

        SpawnEntity();
    }

    // ── UnitRuntimeBridge 구현 ───────────────────────────────

    protected override void OnEnable()
    {
        base.OnEnable();

        // ⚠ 반드시 되돌린다 — 안 그러면 풀에서 다시 나온 몬스터가
        //   지난 판에서 부푼 몸집을 그대로 들고 등장한다.
        ResetEnrageGrowth();
    }

    // ── 소속 진영 ────────────────────────────────────────────
    //
    //  ⚠ 클래스 이름이 아니라 진영으로 판단할 것 — 이 브릿지가 붙는 프리팹은
    //    **플레이어가 소환하는 몬스터** 다. 적 전력은 용사가 담당한다.
    TeamType _team = Faction.Monster;

    /// <summary>
    /// 소속 진영을 정한다. <b>Initialize 보다 먼저</b> 불러야 한다 —
    /// Initialize 안에서 엔티티가 만들어지며 Team 이 박히기 때문이다.
    /// </summary>
    public void SetTeam(TeamType team) => _team = team;

    protected override TeamType GetTeam() => _team;

    protected override UnitType GetUnitType() => UnitType.Monster;

    /// <summary>
    /// 나오자마자 달린다.
    ///
    /// 원작 아군은 1초를 기다렸다. 소환 몬스터가 그러면 소환 지점에 쌓이고,
    /// 0.2초마다 뒤이어 나오는 개체와 겹쳐 분리 계산이 튄다.
    /// </summary>
    protected override float SpawnMoveDelay => 0f;

    // ── 공격 형태 · 패시브 ───────────────────────────────────

    /// <summary>
    /// 최초 엔티티 생성 시 한 번. 이후 재사용에서는 OnEntityReset 이 값을 다시 씌운다.
    /// </summary>
    protected override void AddComponents(EntityManager em, Entity entity)
    {
        em.AddComponentData(entity, BuildPassiveSet());

        // 원거리 세트 — 셋을 한꺼번에 붙인다 (파일 머리 주석 참고).
        //
        // ⚠ 근접 몬스터에도 붙여 둔다
        //   구조 변경(AddComponent)은 전투 중에 하면 아키타입이 갈려 비싸고,
        //   풀에서 나온 개체의 종족이 매번 바뀌므로 "필요할 때만 붙인다" 가
        //   결국 매 소환마다 붙였다 뗐다가 된다. 근접일 때는 RangedTag 만
        //   빼면 되므로, 나머지 둘은 항상 들고 있는 편이 싸다.
        em.AddComponentData(entity, new UnitJobComponent
        {
            Job = MonsterProjectile.ToJob(_projectile),
        });
        em.AddBuffer<ProjectileLaunchRequest>(entity);
        em.AddComponentData(entity, new ProjectileSpeedOverride { Speed = ResolvedSpeed });

        if (_attackKind == MonsterAttackKind.Ranged)
            em.AddComponent<RangedTag>(entity);

        // 고유 액티브 스킬 — 종족이 갖고 있으면 슬롯을 붙인다.
        //
        // ⚠ 슬롯은 종족과 무관하게 **항상** 붙인다
        //   풀에서 나온 개체의 종족이 매번 바뀌므로 "필요할 때만 붙인다" 가
        //   결국 매 소환마다 붙였다 뗐다가 된다. 스킬이 없는 종족은
        //   SkillId = None 으로 두면 ActiveSkillAISystem 이 알아서 거른다.
        em.AddComponentData(entity, BuildSkillSlot());

        // ⚠ 실행 이벤트 버퍼도 함께 붙인다 — 없으면 스킬을 쓰는 순간 터진다
        //   ActiveSkillCooldownSystem 이 발동을 확정하면 이 버퍼에 AppendToBuffer
        //   한다. 버퍼가 없으면 ECB 재생 중에
        //   "Buffer does not exist on entity, cannot append element" 로 죽는다.
        //   용사 쪽은 HeroTierSetup.EnsureExecuteBuffer 가 같은 일을 한다 —
        //   몬스터에게 액티브 스킬을 붙이면서 이쪽만 빠져 있었다.
        //
        //   ⚠ 슬롯과 마찬가지로 종족과 무관하게 **항상** 붙인다.
        //     풀에서 나온 개체의 종족이 매번 바뀌므로, 스킬 있는 종족이
        //     물려받을 수 있는 자리는 미리 갖춰 둔다.
        em.AddBuffer<ActiveSkillExecuteEvent>(entity);

        // ── 두 번째 스킬 슬롯 — 권속 소환 (2차 업그레이드, 2026-09-15) ──
        //
        //  ⚠ 슬롯과 마찬가지로 종족과 무관하게 **항상** 붙인다
        //    구조 변경은 전투 중에 비싸고, 풀에서 나온 개체의 종족이 매번 바뀐다.
        //    권속이 없는 종족은 버퍼를 비워 두면 ActiveSkillAISystem 이 그냥 지나간다.
        //  ⚠ 보스 행동 패턴이 쓰는 그 버퍼다 (HeroTierSetup) — 진영·계층을 따지지
        //    않으므로 몬스터가 그대로 쓸 수 있다. 새 시스템을 만들지 않는다.
        em.AddBuffer<ActiveSkillSlot>(entity);
        FillBroodSlot(em.GetBuffer<ActiveSkillSlot>(entity));
    }

    /// <summary>
    /// 풀에서 물려받은 엔티티에 이번 종족의 값을 다시 씌운다.
    ///
    /// ⚠ RangedTag 는 붙였다 떼야 한다
    ///   같은 Monster.prefab 을 모든 종족이 공유하므로, 리치(원거리)가 쓰던
    ///   엔티티를 슬라임(근접)이 물려받는 일이 흔하다. 태그가 남으면
    ///   슬라임이 근접 쿼리에서 빠져 **붙어서 아무것도 안 하는** 유닛이 된다.
    /// </summary>
    protected override void OnEntityReset(EntityManager em, Entity entity)
    {
        em.SetComponentData(entity, BuildPassiveSet());

        em.SetComponentData(entity, new UnitJobComponent
        {
            Job = MonsterProjectile.ToJob(_projectile),
        });
        em.SetComponentData(entity, new ProjectileSpeedOverride { Speed = ResolvedSpeed });
        em.GetBuffer<ProjectileLaunchRequest>(entity).Clear();

        em.SetComponentData(entity, BuildSkillSlot());

        // ⚠ 두 번째 슬롯도 매번 다시 채운다 (풀 재사용)
        //   안 비우면 리치 킹이 쓰던 엔티티를 물려받은 슬라임이 **리치를 소환한다.**
        FillBroodSlot(em.GetBuffer<ActiveSkillSlot>(entity));

        // 지난 종족이 남긴 발동 요청을 비운다 — 안 비우면 풀에서 나오자마자
        // 이전 개체의 스킬이 한 번 터진다.
        em.GetBuffer<ActiveSkillExecuteEvent>(entity).Clear();

        // 쓰지도 못한 채 태그만 남아 있으면 다음 개체가 그걸 물려받는다.
        if (em.HasComponent<UseActiveSkillTag>(entity))
            em.RemoveComponent<UseActiveSkillTag>(entity);

        bool wantsRanged = _attackKind == MonsterAttackKind.Ranged;
        bool hasRanged   = em.HasComponent<RangedTag>(entity);

        if (wantsRanged && !hasRanged) em.AddComponent<RangedTag>(entity);
        if (!wantsRanged && hasRanged) em.RemoveComponent<RangedTag>(entity);

        ClearSpeciesPassiveResidue(em, entity);
    }

    /// <summary>
    /// 지난 개체의 종족 패시브 흔적을 지운다.
    ///
    /// ⚠ 이걸 빠뜨리면 "슬라임인데 독을 뿜는" 유닛이 나온다
    ///   Monster.prefab 하나를 전 종족이 공유하므로, 독 슬라임이 쓰던 엔티티를
    ///   평범한 스켈레톤이 물려받는 일이 흔하다. 종족 패시브는 스폰 시점에
    ///   SpeciesPassiveRuntime 이 **필요한 것만** 다시 붙이므로, 여기서는
    ///   전부 떼는 것이 맞다.
    ///
    ///   증상이 조용하다는 게 특히 나쁘다 — 에러도 로그도 없이 밸런스만 틀어진다.
    /// </summary>
    static void ClearSpeciesPassiveResidue(EntityManager em, Entity entity)
    {
        if (em.HasComponent<RegenComponent>(entity))     em.RemoveComponent<RegenComponent>(entity);

        // 역병 술사 개성 태그 — 남으면 다른 종족이 쓰러뜨려도 좀비가 일어난다.
        if (em.HasComponent<PlagueRiserTag>(entity))     em.RemoveComponent<PlagueRiserTag>(entity);
        if (em.HasComponent<RetaliateComponent>(entity)) em.RemoveComponent<RetaliateComponent>(entity);
        if (em.HasComponent<InflictOnHitComponent>(entity))
            em.RemoveComponent<InflictOnHitComponent>(entity);

        // 연사(고블린 궁수)는 기존 태그를 빌려 쓴다 — 남으면 근접 몬스터가 2연타한다.
        if (em.HasComponent<DoubleStrikeTag>(entity))    em.RemoveComponent<DoubleStrikeTag>(entity);

        // 휩쓸기(고대 트롤) — 남으면 슬라임이 광역 평타를 휘두른다.
        if (em.HasComponent<SplashAttackComponent>(entity))
            em.RemoveComponent<SplashAttackComponent>(entity);

        // ⚠ 넉백 면역도 뗀다 — 야수 금·거상·난공불락·무게추가 붙인다.
        //   한때 떼는 곳이 없어서, 야수 금으로 한 번 선 엔티티는 풀에서 누가
        //   물려받든 영영 밀리지 않았다. 스폰 때 필요한 쪽이 다시 붙인다.
        if (em.HasComponent<KnockbackImmuneTag>(entity)) em.RemoveComponent<KnockbackImmuneTag>(entity);

        // ── 장비 특이 패시브 (MonsterTag.cs) — 새로 만들면 여기에도 한 줄 ──
        Remove<BravadoComponent>(em, entity);
        Remove<RecoilComponent>(em, entity);
        Remove<VitalStrikeTag>(em, entity);
        Remove<ExecuteComponent>(em, entity);
        Remove<DamageTakenMultComponent>(em, entity);
        Remove<PhotosynthesisComponent>(em, entity);
        Remove<VengeanceComponent>(em, entity);
    }

    static void Remove<T>(EntityManager em, Entity entity) where T : unmanaged, IComponentData
    {
        if (em.HasComponent<T>(entity)) em.RemoveComponent<T>(entity);
    }

    /// <summary>
    /// 실제로 쓸 발사체 속도. 종족이 0 으로 두면 발사체 종류의 기본값을 쓴다
    /// (RangedAttackJob 의 ArrowSpeed 15 · MagicBoltSpeed 10 과 같은 값).
    /// </summary>
    float ResolvedSpeed
    {
        get
        {
            if (_projectileSpeed > 0f) return _projectileSpeed;
            return _projectile == MonsterProjectileKind.Arrow ? 15f : 10f;
        }
    }

    /// <summary>
    /// 고유 액티브 스킬 슬롯. 수치는 SO(ActiveSkillData)가 소유하므로
    /// 여기서는 쿨다운만 채운다 — 실행 시점에 실행기가 SO 를 다시 읽는다.
    ///
    /// ⚠ 쿨다운이 0 이면 매 프레임 시전한다
    ///   종족이 스킬을 갖고 있는데 쿨다운을 안 잡아 두면 돌진이 끊이지 않는다.
    ///   SO 를 못 찾으면 넉넉한 기본값으로 막는다.
    /// </summary>
    GeneralActiveSkillComponent BuildSkillSlot()
    {
        if (_activeSkill == ActiveSkillId.None)
            return new GeneralActiveSkillComponent { SkillId = (int)ActiveSkillId.None };

        ActiveSkillData data = ActiveSkillDatabase.Current?.Get(_activeSkill);

        float baseCooldown = data != null ? data.Cooldown : 12f;

        // ⚠ 쿨감을 여기서 반영한다 — 안 하면 술법 시너지가 통째로 무효다 (2026-09-03)
        //   GeneralRuntimeBridge 는 하고 있는데 이쪽만 빠져 있었다. 몬스터의
        //   쿨다운은 SO 값 그대로 박혀서, SkillCooldownReduce 를 아무리 올려도
        //   스킬이 빨라지지 않았다. 술법의 단계 효과가 전부 이 스탯 하나라
        //   동·은·금이 셋 다 아무 일도 안 하는 상태였다.
        float cdr = GeneralRuntimeBridge.ClampCDR(_stat.Get(StatType.SkillCooldownReduce),
                                                  GameplayConfig.CooldownCap);

        float cooldown = baseCooldown * (1f - cdr);

        return new GeneralActiveSkillComponent
        {
            SkillId           = (int)_activeSkill,
            EffectValue       = data != null ? data.EffectValue    : 1f,
            EffectRadius      = data != null ? data.EffectRadius   : 0f,
            EffectDuration    = data != null ? data.EffectDuration : 0f,
            Cooldown          = cooldown,

            // 나오자마자 쓰지 않는다 — 소환 직후 전원이 동시에 돌진하면
            // 라인이 통째로 앞으로 튀어 그림이 무너진다.
            CooldownRemaining = cooldown * 0.5f,
        };
    }

    /// <summary>
    /// 권속 소환 슬롯을 다시 채운다. 권속이 없는 종족이면 <b>비운다</b>.
    ///
    /// ⚠ 언제나 Clear 로 시작한다 — 풀에서 나온 엔티티에 지난 종족의 슬롯이 남아 있다.
    /// ⚠ 쿨다운의 정본은 <b>종족</b>이다 (BroodCooldown). SO 값은 종족이 안 적었을 때의
    ///   기본값일 뿐이다 — 왕마다 부르는 주기가 다른 것이 이 축의 뜻이다.
    /// ⚠ 술법 쿨감은 여기에도 건다 — 안 걸면 "스킬 하나는 빨라지는데 소환만 그대로" 가 된다
    ///   (BuildSkillSlot 이 같은 함정을 이미 한 번 밟았다).
    /// </summary>
    void FillBroodSlot(DynamicBuffer<ActiveSkillSlot> slots)
    {
        slots.Clear();

        if (Species == null || Species.BroodSpecies == null || Species.BroodCount <= 0) return;

        float baseCooldown = Species.BroodBaseCooldown;

        float cdr = GeneralRuntimeBridge.ClampCDR(_stat.Get(StatType.SkillCooldownReduce),
                                                  GameplayConfig.CooldownCap);

        float cooldown = baseCooldown * (1f - cdr);

        // 시너지 왕권 — 권속 소환 쿨다운 (2차만 표식을 갖는다, 2026-09-15)
        if ((Species.Tags & MonsterTag.Royal) != 0)
            cooldown *= 1f - MonsterSynergyRule.RoyalBroodCooldownCut(
                                 MonsterSynergyRule.TierOf(MonsterTag.Royal));

        slots.Add(new ActiveSkillSlot
        {
            SkillId  = (int)ActiveSkillId.SummonBrood,
            Cooldown = cooldown,

            // 나오자마자 부르지 않는다 — 소환되자마자 권속이 함께 서면
            // "왕을 냈다" 가 아니라 "한 장에 열 마리가 나왔다" 로 읽힌다.
            CooldownRemaining = cooldown,
        });
    }

    /// <summary>
    /// 권속 소환 슬롯을 비운다 — 분열체·부활체(1세대 이상)가 쓰는 길이다.
    ///
    /// ⚠ MonsterSpawner.Arm 이 부른다. 세대를 아는 곳이 거기뿐이다.
    /// </summary>
    public void ClearBroodSlot()
    {
        if (!TryGetComponent<EntityLink>(out var link)) return;
        if (link.Entity == Entity.Null)                 return;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        EntityManager em = world.EntityManager;
        if (!em.Exists(link.Entity))                          return;
        if (!em.HasBuffer<ActiveSkillSlot>(link.Entity))      return;

        em.GetBuffer<ActiveSkillSlot>(link.Entity).Clear();
    }

    GeneralPassiveSetComponent BuildPassiveSet() => new()
    {
        Slot0           = _passive0,
        Slot1           = _passive1,
        Slot2           = _passive2,
        ActiveSlotCount = _passiveCount,
    };

    /// <summary>
    /// 패시브 목록을 3칸에 담는다. 넘치면 앞 3개만 쓴다 —
    /// GeneralPassiveSetComponent 가 3슬롯 고정이기 때문이다.
    /// </summary>
    void CachePassives(System.Collections.Generic.List<PassiveSkillType> passives)
    {
        _passive0 = _passive1 = _passive2 = PassiveSkillType.None;
        _passiveCount = 0;

        if (passives == null) return;

        for (int i = 0; i < passives.Count && _passiveCount < 3; i++)
        {
            if (passives[i] == PassiveSkillType.None) continue;

            switch (_passiveCount)
            {
                case 0: _passive0 = passives[i]; break;
                case 1: _passive1 = passives[i]; break;
                default: _passive2 = passives[i]; break;
            }
            _passiveCount++;
        }
    }
}
