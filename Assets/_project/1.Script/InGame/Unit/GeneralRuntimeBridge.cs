using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  GeneralRuntimeBridge.cs
//  장군 프리팹 전용 RuntimeBridge.
//
//  병사 스탯 비율:
//    statScaleRatio = 0.2f + CommandPower * 0.01f  (상한 없음)
//    CommandPower 1~30 기준 → 21%~50%, 80 이상이면 100% 를 넘긴다
//    공식은 SoldierRuntimeBridge.StatRatio 가 소유 — 여기서 직접 계산하지 말 것
//
//  병사 진형 (세로 열):
//    전체 높이를 고정(FormationHeight)하고 병사 수로 나눠 간격 산출.
//    병사가 많을수록 자동으로 좁아짐 (최소 0.15, 최대 1.5).
// ============================================================

public class GeneralRuntimeBridge : UnitRuntimeBridge
{
    // ── UI 이벤트 ─────────────────────────────────────────────
    /// <summary>
    /// Initialize() 완료 후 발생. InGameHUD 가 구독해 GeneralPanelUI 를 생성한다.
    /// </summary>
    public static event System.Action<GeneralRuntimeBridge> OnSpawned;

    [Header("병사 설정")]
    [Tooltip("PoolController 에 등록된 병사 풀 키")]
    [SerializeField] string _soldierPoolKey = "Soldier";

    // 병사 진형: 장군 오른쪽에 격자(행 × 열)로 배치
    //   행(Row) — X 축(오른쪽): 장군에서 멀어질수록 뒤열
    //   열(Col) — Y 축(위아래): sqrt(N) 기반 자동 산출, 열 단위 중앙 정렬
    const float ColSpacing = 0.6f; // 병사 간 Y 간격
    const float RowSpacing = 0.7f; // 행 간 X 간격 (오른쪽)

    int       _level;
    UnitGrade _grade;
    UnitJob   _job;
    UnitEntry _unitEntry;   // 등급 조회용

    /// <summary>
    /// 병사 환산의 원본 — 기본 롤 + 장비까지만 담긴다.
    /// 장수의 최종 스탯(_stat)과 일부러 다르다. Initialize 참고.
    /// </summary>
    UnitStat _soldierSourceStat;

    /// <summary>
    /// 성장만으로 얻은 스탯 스냅샷 (등급·레벨 롤). Initialize 맨 앞에서 뜬다.
    /// 전투에서는 BaseRollStatComponent 로 넘어가 '외부 증가분' 의 기준이 된다.
    /// </summary>
    StatBlock _baseRoll;

    // ── 패시브 스킬 슬롯 ─────────────────────────────────────
    PassiveSkillType _passive0;
    PassiveSkillType _passive1;
    PassiveSkillType _passive2;
    byte             _activePassiveCount;

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// AllySpawner 가 스폰 직후 호출. 등급은 가중치 랜덤으로 자동 결정.
    /// unitEntry 를 전달하면 런 장비 스탯이 SpawnEntity() 전에 반영된다.
    /// </summary>
    /// <param name="statMult">
    /// 체력·공격력에 곱해지는 배율. 무한 보스가 쓴다 (EndlessBossRule).
    /// ⚠ 병사 원본에도 함께 걸린다 — 부대장만 세지면 부대가 따로 논다.
    /// </param>
    /// <param name="scaleMult">
    /// 몸집 배율. 분리 반경(UnitSizeComponent.Radius)이 localScale 에서 나오므로
    /// SpawnEntity <b>앞</b>에서 걸어야 히트박스까지 함께 커진다.
    /// </param>
    /// <param name="loneHero">
    /// 휘하 병사를 세우지 않는다. 무한 보스는 한 기로 온다 (EndlessBossRule).
    /// </param>
    public void Initialize(string unitName, int level = 1, UnitEntry unitEntry = null,
                           float statMult = 1f, float scaleMult = 1f, bool loneHero = false)
    {
        _unitName  = unitName;
        _level     = level;
        _unitEntry = unitEntry;
        // 등급 업그레이드 횟수 반영 (unitEntry 없으면 태생 등급 사용)
        _grade     = unitEntry != null ? unitEntry.Grade : UnitJobRoller.GetBirthGrade(unitName);
        _job      = UnitJobRoller.GetJob(unitName);
        // ── 패시브 슬롯 결정 (스탯 적용은 파이프라인이 한다) ──
        (_passive0, _passive1, _passive2) = PassiveSkillRoller.Roll(_unitName);
        _activePassiveCount               = PassiveSkillRoller.GetActiveSlotCount(_grade);

        // ── 스탯 조립 — 규칙은 HeroStatPipeline 하나가 소유한다 ──
        //
        //  ⚠ 여기서 순서를 다시 적지 말 것
        //    예전엔 이 자리에 장비→어빌리티→유물→특성→도감→장수전용 순서가
        //    통째로 적혀 있었고, 로비(HeroStatResolver)에도 같은 순서가 한 번 더
        //    적혀 있었다. 한쪽에 항목을 추가하면 다른 쪽이 조용히 빠져
        //    "화면과 전투가 다른" 버그가 반복해서 났다.
        //
        //  ⚠ previewBattleStart 는 false 다
        //    전투 시작 패시브는 실제로 PassiveSkillRuntimeSystem 이 건다.
        //    여기서 미리 얹으면 같은 보너스가 두 번 들어간다.
        if (unitEntry != null)
        {
            var build          = HeroStatPipeline.Build(unitEntry, previewBattleStart: false);
            _stat              = build.Stat;
            _baseRoll          = StatBlock.FromUnitStat(build.BaseRoll);
            _soldierSourceStat = build.SoldierSource;
        }
        else
        {
            // 에디터 직접 스폰 등 — 세이브가 없어 성장분만 굴린다
            _stat              = GeneralStatRoller.Roll(unitName, _level, _grade);
            _baseRoll          = StatBlock.FromUnitStat(_stat);
            _soldierSourceStat = _stat.CloneWithoutGeneralOnly();
        }

        // ── 유물 '용사 약화' ─────────────────────────────────
        //
        //  ⚠ 이 호출이 없어서 6노드가 통째로 무효였다 (2026-09-07)
        //    RelicTreeApplier.ApplyEnemyWeaken 을 부르는 곳이 아무 데도 없었다.
        //    원작에서는 몬스터가 적이라 MonsterRuntimeBridge 가 불렀는데,
        //    진영이 뒤집히며 그 호출이 사라졌다. 지금 적은 **장수(용사)** 다.
        //
        //  ⚠ 병사 원본에도 함께 건다
        //    병사는 이 값을 환산해 받는다. 장수만 깎으면 병사가 그대로 남아
        //    "체력 −12%" 유물이 부대의 절반에만 걸린다.
        //
        //  ⚠ 자리는 스탯이 다 굴러 나온 **뒤**, SpawnEntity **앞**이다
        //    비율로 깎으므로 앞에 두면 뒤에 붙는 성장분이 깎이지 않는다.
        RelicTreeApplier.ApplyEnemyWeaken(_stat);
        RelicTreeApplier.ApplyEnemyWeaken(_soldierSourceStat);

        // ── 편성이 실은 배율 (무한 보스) ─────────────────────
        //
        //  ⚠ 유물 약화 **뒤**다 — 약화는 비율이라 앞에 두면 배율까지 함께 깎인다.
        //  ⚠ 체력·공격력만 곱한다
        //    사거리·공속·이속까지 곱하면 ×10 한 번에 화면을 가로지르며 연타한다.
        //    무한 보스의 세기는 "얼마나 오래 버티고 얼마나 아픈가" 다.
        if (!Mathf.Approximately(statMult, 1f))
        {
            ScaleCombatStat(_stat,              statMult);
            ScaleCombatStat(_soldierSourceStat, statMult);
        }

        // TitanGeneral 크기 변경 (풀 재사용 시 이전 스케일 누적 방지: 항상 리셋 후 적용)
        transform.localScale = Vector3.one;
        var passiveDb = PassiveSkillDatabase.Current;
        if (passiveDb != null)
        {
            float passiveScale = PassiveSkillApplier.GetGeneralScaleMultiplier(GetActivePassives(), passiveDb);
            if (!Mathf.Approximately(passiveScale, 1f))
                transform.localScale = new Vector3(passiveScale, passiveScale, passiveScale);
        }

        // ── 편성이 실은 몸집 배율 (무한 보스) ────────────────
        //
        //  ⚠ SpawnEntity 앞이다 — 그 안에서 localScale 로 분리 반경을 잡는다.
        //  ⚠ _spawnScale 도 함께 옮긴다
        //    광폭화 성장(GrowEnrage)과 그 초기화(ResetEnrageGrowth)가 이 값을
        //    기준으로 삼는다. 안 옮기면 보스가 첫 광폭화에서 원래 크기로 쪼그라든다.
        if (!Mathf.Approximately(scaleMult, 1f))
        {
            transform.localScale *= scaleMult;
            _spawnScale           = transform.localScale;
        }

        // 외형 적용 (ECS Entity 생성과 독립적으로 실행)
        GetComponent<UnitAppearanceBridge>()?.ApplyAlly(unitName, _job, _grade);

        SpawnEntity();

        // ⚠ 무한 보스는 혼자 온다 (EndlessBossRule) — 병사를 세우지 않는다.
        if (!loneHero) SpawnSoldiers();

        // 배틀 통계 트래커에 장군 등록
        if (TryGetComponent<EntityLink>(out var entityLink) && entityLink.Entity != Unity.Entities.Entity.Null)
            BattleStatsTracker.Instance?.RegisterGeneral(entityLink.Entity, _unitName);

        OnSpawned?.Invoke(this);
    }

    /// <summary>
    /// 거리를 벌며 싸우는 직업인가 — 퇴각 사격(RetreatFireTag) 대상.
    /// </summary>
    static bool IsRangedJob(UnitJob job)
        => job == UnitJob.Archer || job == UnitJob.Mage;

    /// <summary>외부에서 롤된 스탯을 읽을 때 사용.</summary>
    public UnitStat GetRolledStat() => _stat;

    /// <summary>
    /// 표시용 등급. 세이브의 UnitEntry 가 있으면 등급업 반영분까지 그대로,
    /// 없으면(에디터 직접 스폰 등) 이름 시드의 태생 등급으로 떨어진다.
    /// </summary>
    public UnitGrade Grade => _unitEntry != null
        ? _unitEntry.Grade
        : UnitJobRoller.GetBirthGrade(_unitName ?? name);

    // ── UnitRuntimeBridge 구현 ───────────────────────────────

    // ── 소속 진영 ────────────────────────────────────────────
    //
    //  원작에서 장수는 언제나 아군이었다. 이 게임에서는 같은 장수 체계를
    //  적(용사, Hero)이 통째로 물려받는다.
    //
    //  ⚠ 기본값이 Faction.Hero(= Enemy) 다 — 원작과 반대다.
    //    이 게임에 플레이어가 부리는 장수는 없다. General 프리팹을 쓰는 유닛은
    //    전부 용사이므로, 기본값이 적이어야 빠뜨렸을 때 조용히 아군으로 서지 않는다.
    //
    //  ⚠ 원작 NormalMode.GetAllySpawnEntries 는 이제 의미가 없다
    //    플레이어의 장수를 세우던 경로다. 그쪽을 그대로 두면 용사가 아군으로 선다.
    //    아군 전력은 소환 몬스터가 담당한다.
    TeamType _team = Faction.Hero;

    /// <summary>
    /// 소속 진영을 정한다. <b>Initialize 보다 먼저</b> 불러야 한다 —
    /// Initialize 안에서 엔티티가 만들어지며 Team 이 박히기 때문이다.
    /// </summary>
    public void SetTeam(TeamType team) => _team = team;

    protected override TeamType GetTeam()     => _team;
    protected override UnitType GetUnitType() => UnitType.General;

    /// <summary>장군 전용 ECS 컴포넌트 추가 — 직업, 원거리 태그, 패시브/액티브 스킬, 발사 요청 버퍼.</summary>
    protected override void AddComponents(EntityManager em, Entity entity)
    {
        em.AddComponentData(entity, new GeneralComponent { CommandRadius = 15f });
        em.AddComponentData(entity, new UnitJobComponent { Job = _job });

        if (_job == UnitJob.Archer || _job == UnitJob.Mage)
        {
            em.AddComponent<RangedTag>(entity);
            em.AddBuffer<ProjectileLaunchRequest>(entity);
        }

        // 퇴각 사격은 원거리 직업의 기본 행동이다 — 붙어 오는 적에게서 물러나며 쏜다.
        // (예전 TraitType.ArcherRetreatFire 특성이 하던 일)
        if (IsRangedJob(_job))
            em.AddComponent<RetreatFireTag>(entity);


        // ── 성장분 기준선 ────────────────────────────────────
        //   외부 컨텐츠로 얼마나 올랐는지를 세려면 '올리기 전' 값이 있어야 한다.
        em.AddComponentData(entity, new BaseRollStatComponent { Roll = _baseRoll });

        // ── 패시브 슬롯 컴포넌트 ─────────────────────────────
        em.AddComponentData(entity, new GeneralPassiveSetComponent
        {
            Slot0            = _passive0,
            Slot1            = _passive1,
            Slot2            = _passive2,
            ActiveSlotCount  = _activePassiveCount,
        });

        // ── 액티브 스킬: 이름+직업 기반 결정론적 선택 ──────────
        var activeDb  = ActiveSkillDatabase.Current;
        // 희귀 스킬은 직업당 부대에 한 명만 — 중재까지 끝난 결과를 받는다
        var rolledId  = RareSkillArbiter.Resolve(_unitName, _job, activeDb, _grade);
        var skillData = activeDb?.Get(rolledId);

        float baseCooldown = skillData?.Cooldown ?? 15f;
        // _stat.Get 이 이미 곱연산으로 합쳐 준다 (CombineMode.MultiplyResidual)
        float effectiveCdr = ClampCDR(_stat.Get(StatType.SkillCooldownReduce),
                                      GameplayConfig.CooldownCap);
        em.AddComponentData(entity, new GeneralActiveSkillComponent
        {
            SkillId           = (int)rolledId,
            EffectValue       = skillData?.EffectValue    ?? 1f,
            EffectRadius      = skillData?.EffectRadius   ?? 0f,
            EffectDuration    = skillData?.EffectDuration ?? 0f,
            Cooldown          = baseCooldown * (1f - effectiveCdr),
            CooldownRemaining = 0f,  // 첫 발동은 즉시 가능
        });

        // 실행 이벤트 버퍼 추가 (ActiveSkillCooldownSystem 이 여기에 씀)
        em.AddBuffer<ActiveSkillExecuteEvent>(entity);

        // ── 패시브별 런타임 상태 컴포넌트 ───────────────────
        var passives = GetActivePassives();

        if (PassiveSkillApplier.HasPassive(passives, PassiveSkillType.SoldierDeathEmpower))
        {
            em.AddComponentData(entity, new SoldierDeathEmpowerState { DeathCount = 0 });
            em.AddBuffer<SoldierDeathEvent>(entity);
        }

        if (PassiveSkillApplier.HasPassive(passives, PassiveSkillType.BloodPact))
        {
            em.AddComponentData(entity, new BloodPactState { LastBonusRatio = 0f });
            // BloodPact 는 HitEvent 콜백에서 StatusEffectBuffer 를 사용
            if (!em.HasBuffer<StatusEffectBufferElement>(entity))
                em.AddBuffer<StatusEffectBufferElement>(entity);
        }

        bool needsConditionState = PassiveSkillApplier.HasPassive(passives, PassiveSkillType.IronWill)
                                || PassiveSkillApplier.HasPassive(passives, PassiveSkillType.LastStand);
        if (needsConditionState)
        {
            em.AddComponentData(entity, new PassiveConditionState
            {
                IronWillTriggered   = false,
                LastStandTriggered  = false,
                InitialSoldierCount = Mathf.RoundToInt(_stat.Get(StatType.SoldierCount)),
            });
        }

        // ── 새 트리거 패시브용 공용 버퍼 ───────────────────
        bool needsStackBuf =
            PassiveSkillApplier.HasPassive(passives, PassiveSkillType.StrengthStack) ||
            PassiveSkillApplier.HasPassive(passives, PassiveSkillType.KillMomentum)  ||
            PassiveSkillApplier.HasPassive(passives, PassiveSkillType.KillEmpower);
        if (needsStackBuf)
            em.AddBuffer<CombatStackElement>(entity);

        // StatusEffectBuffer — 새 트리거 패시브 다수가 필요
        bool needsStatusBuf =
            PassiveSkillApplier.HasPassive(passives, PassiveSkillType.DefenseShield)   ||
            PassiveSkillApplier.HasPassive(passives, PassiveSkillType.CounterStrike)   ||
            PassiveSkillApplier.HasPassive(passives, PassiveSkillType.SkillAdrenaline);
        if (needsStatusBuf && !em.HasBuffer<StatusEffectBufferElement>(entity))
            em.AddBuffer<StatusEffectBufferElement>(entity);

        // ── 적 처치 / 스킬 사용 / 착탄 이벤트 버퍼 ── 모든 장군에 추가
        em.AddBuffer<EnemyKillEvent>(entity);
        em.AddBuffer<SkillUseEvent>(entity);
        em.AddBuffer<AttackHitEvent>(entity);   // OnAttackLanded 트리거용 — 모든 장군 공통

        // SoldierDeathEvent — OnSoldierDeath 트리거 패시브 또는 기존 SoldierDeathEmpower 가 없어도 추가
        // (패시브 OnSoldierDeath 감지 — 비우는 것은 CombatTriggerSystem)
        if (!em.HasBuffer<SoldierDeathEvent>(entity))
            em.AddBuffer<SoldierDeathEvent>(entity);
    }

    /// <summary>풀 재사용 시 스킬 / 조건 상태 초기화 + 장군별 컴포넌트 갱신.</summary>
    protected override void OnEntityReset(EntityManager em, Entity entity)
    {
        // ⚠ 성장분 기준선은 재사용 때도 새로 써야 한다
        //   AddComponents 는 엔티티를 처음 만들 때만 돈다. 풀에서 꺼낸 엔티티에
        //   그대로 두면 **이전 장수의 롤** 이 기준이 되어 증가분이 엉뚱하게 잡힌다.
        if (em.HasComponent<BaseRollStatComponent>(entity))
            em.SetComponentData(entity, new BaseRollStatComponent { Roll = _baseRoll });
        else
            em.AddComponentData(entity, new BaseRollStatComponent { Roll = _baseRoll });

        // ── 버퍼 초기화 ──────────────────────────────────────────
        if (em.HasBuffer<ProjectileLaunchRequest>(entity))
            em.GetBuffer<ProjectileLaunchRequest>(entity).Clear();

        if (em.HasBuffer<ActiveSkillExecuteEvent>(entity))
            em.GetBuffer<ActiveSkillExecuteEvent>(entity).Clear();

        if (em.HasBuffer<SoldierDeathEvent>(entity))
            em.GetBuffer<SoldierDeathEvent>(entity).Clear();

        if (em.HasBuffer<EnemyKillEvent>(entity))
            em.GetBuffer<EnemyKillEvent>(entity).Clear();

        if (em.HasBuffer<SkillUseEvent>(entity))
            em.GetBuffer<SkillUseEvent>(entity).Clear();

        if (em.HasBuffer<CombatStackElement>(entity))
            em.GetBuffer<CombatStackElement>(entity).Clear();

        if (em.HasBuffer<AttackHitEvent>(entity))
            em.GetBuffer<AttackHitEvent>(entity).Clear();

        // ── 상태 컴포넌트 초기화 ─────────────────────────────────
        if (em.HasComponent<SoldierDeathEmpowerState>(entity))
            em.SetComponentData(entity, new SoldierDeathEmpowerState { DeathCount = 0 });

        if (em.HasComponent<BloodPactState>(entity))
            em.SetComponentData(entity, new BloodPactState { LastBonusRatio = 0f });

        if (em.HasComponent<PassiveConditionState>(entity))
            em.SetComponentData(entity, new PassiveConditionState
            {
                IronWillTriggered   = false,
                LastStandTriggered  = false,
                InitialSoldierCount = Mathf.RoundToInt(_stat.Get(StatType.SoldierCount)),
            });

        // ── 직업 갱신 ────────────────────────────────────────────
        em.SetComponentData(entity, new UnitJobComponent { Job = _job });

        // ── 원거리 태그·버퍼 갱신 ────────────────────────────────
        bool isRanged = _job == UnitJob.Archer || _job == UnitJob.Mage;
        if (isRanged)
        {
            if (!em.HasComponent<RangedTag>(entity))
                em.AddComponent<RangedTag>(entity);
            if (!em.HasBuffer<ProjectileLaunchRequest>(entity))
                em.AddBuffer<ProjectileLaunchRequest>(entity);
        }
        else
        {
            if (em.HasComponent<RangedTag>(entity))
                em.RemoveComponent<RangedTag>(entity);
        }

        // ── 퇴각 사격 갱신 (원거리 기본 행동) ────────────────────
        // ⚠ 붙이는 것만큼 떼는 것도 중요하다
        //   엔티티는 풀에서 재사용되므로 원거리가 쓰던 자리를 근접이 물려받는다.
        //   떼지 않으면 그 기사가 적이 붙을 때마다 뒷걸음질친다.
        bool retreats = IsRangedJob(_job);
        if (retreats && !em.HasComponent<RetreatFireTag>(entity))
            em.AddComponent<RetreatFireTag>(entity);
        else if (!retreats && em.HasComponent<RetreatFireTag>(entity))
            em.RemoveComponent<RetreatFireTag>(entity);

        if (em.HasComponent<TauntTag>(entity)) em.RemoveComponent<TauntTag>(entity);

        // ── 보스·엘리트 승격 되돌리기 ───────────────────────────
        //
        //  ⚠ 이걸 안 해서 **보스가 영원히 보스로 남았다** (사용자 지적, 2026-09-07)
        //    HeroTierSetup 이 붙이는 BossComponent / EliteComponent 를 떼는 코드가
        //    코드베이스에 **하나도 없었다.** 장수 오브젝트는 풀에서 재사용되므로,
        //    5스테이지 보스가 쓰던 엔티티를 6스테이지의 평범한 용사가 물려받으면
        //    그 용사가 그대로 보스다 — AoE 평타, 평타 ×3, 보스 HP 바, 광폭화까지.
        //    허들을 지날 때마다 풀에 보스가 하나씩 늘어나 눈덩이처럼 불어난다.
        //
        //  ⚠ 여기서 떼도 안전하다 — 승격은 Initialize **뒤**에 온다
        //    HeroSpawner.SpawnOne 이 Initialize → ApplyBoss/ApplyElite 순서로 부른다.
        //    이 자리에서 지워야 "이번 판에 승격된 개체만" 보스가 된다.
        if (em.HasComponent<BossComponent>(entity))  em.RemoveComponent<BossComponent>(entity);
        if (em.HasComponent<EliteComponent>(entity)) em.RemoveComponent<EliteComponent>(entity);

        // 보스 패턴(돌진·강타·광폭화)도 같은 이유로 비운다 —
        // 컴포넌트만 떼고 슬롯을 남기면 일반 용사가 보스 기술을 쓴다.
        if (em.HasBuffer<ActiveSkillSlot>(entity)) em.GetBuffer<ActiveSkillSlot>(entity).Clear();

        // 무한 보스가 붙여 둔 넉백 면역도 같은 이유로 뗀다 (HeroSpawner) —
        // 남으면 평범한 용사가 밀리지 않는 채로 선다.
        if (em.HasComponent<KnockbackImmuneTag>(entity))
            em.RemoveComponent<KnockbackImmuneTag>(entity);

        // ── 패시브 슬롯 갱신 ─────────────────────────────────────
        em.SetComponentData(entity, new GeneralPassiveSetComponent
        {
            Slot0           = _passive0,
            Slot1           = _passive1,
            Slot2           = _passive2,
            ActiveSlotCount = _activePassiveCount,
        });

        // ── 액티브 스킬 갱신 ─────────────────────────────────────
        var activeDb  = ActiveSkillDatabase.Current;
        // 희귀 스킬은 직업당 부대에 한 명만 — 중재까지 끝난 결과를 받는다
        var rolledId  = RareSkillArbiter.Resolve(_unitName, _job, activeDb, _grade);
        var skillData = activeDb?.Get(rolledId);
        float effectiveCdr2 = ClampCDR(_stat.Get(StatType.SkillCooldownReduce),
                                       GameplayConfig.CooldownCap);
        em.SetComponentData(entity, new GeneralActiveSkillComponent
        {
            SkillId           = (int)rolledId,
            EffectValue       = skillData?.EffectValue    ?? 1f,
            EffectRadius      = skillData?.EffectRadius   ?? 0f,
            EffectDuration    = skillData?.EffectDuration ?? 0f,
            Cooldown          = (skillData?.Cooldown ?? 15f) * (1f - effectiveCdr2),
            CooldownRemaining = 0f,
        });
    }

    // ── 병사 스폰 ─────────────────────────────────────────────

    /// <summary>
    /// 체력·공격력에만 배율을 건다. 무한 보스가 쓰는 유일한 증폭 지점이다.
    ///
    /// ⚠ 여기에 스탯을 늘리지 말 것 — 사거리·공속·이속은 종류가 다른 손잡이다.
    /// </summary>
    static void ScaleCombatStat(UnitStat stat, float mult)
    {
        stat.Set(StatType.MaxHp,  stat.Get(StatType.MaxHp)  * mult);
        stat.Set(StatType.Attack, stat.Get(StatType.Attack) * mult);
    }

    public void SpawnSoldiers()
    {
        if (string.IsNullOrEmpty(_soldierPoolKey)) return;

        int soldierCount = Mathf.RoundToInt(_stat.Get(StatType.SoldierCount));
        if (soldierCount <= 0) return;

        if (!TryGetComponent<EntityLink>(out var link) || link.Entity == Entity.Null)
        {
            Debug.LogWarning("[GeneralRuntimeBridge] 장군 Entity 없음 — 병사 스폰 취소");
            return;
        }

        // 병사 스탯 비율 — 공식은 SoldierRuntimeBridge 가 소유한다
        // (HeroDetailPopup "용병" 탭도 같은 함수를 쓴다)
        float statScaleRatio = SoldierRuntimeBridge.StatRatio(_stat.Get(StatType.CommandPower));

        // Y축 열 수: sqrt(N) 기반 자동 산출 — 병사 수에 따라 자연스러운 격자
        int colsY = Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(soldierCount)));

        for (int i = 0; i < soldierCount; i++)
        {
            int row           = i / colsY;
            int col           = i % colsY;
            int soldiersInRow = Mathf.Min(colsY, soldierCount - row * colsY);

            // 현재 행 내에서 Y 중앙 정렬
            float yOffset = (col - (soldiersInRow - 1) * 0.5f) * ColSpacing;
            float xOffset = (row + 1) * RowSpacing;  // 장군 오른쪽(+X)

            Vector3 spawnPos = new Vector3(
                transform.position.x + xOffset,
                transform.position.y + yOffset,
                transform.position.z);

            GameObject soldierGO = PoolController.Instance?.Spawn(
                PoolType.Unit, _soldierPoolKey, spawnPos, Quaternion.identity);

            if (soldierGO == null)
            {
                Debug.LogWarning($"[GeneralRuntimeBridge] 병사 스폰 실패 (풀 키: '{_soldierPoolKey}')");
                continue;
            }

            // 병사 생존 카운트 반영 (AliveAllyCount 에 포함되지 않으므로 직접 추가)
            // ⚠ 장수의 진영을 그대로 따른다 — 용사(적) 휘하 병사를 아군으로 세면
            //   전멸 판정이 영원히 성립하지 않는다.
            if (BattleManager.Instance != null)
                BattleManager.Instance.OnUnitSpawned(_team);

            if (soldierGO.TryGetComponent<SoldierRuntimeBridge>(out var soldier))
            {
                // 진영을 먼저 물려준다 — Initialize 안에서 엔티티가 만들어지며
                // Team 이 박히므로 순서를 바꾸면 용사 휘하 병사가 아군으로 선다.
                soldier.SetTeam(_team);

                // ⚠ 장수의 최종 스탯(_stat)이 아니라 병사 전용 원본을 넘긴다
                //   장수만 강해지는 성장(패시브·특성·도감·장수 전용 옵션)이
                //   환산을 타고 병사에게 흘러들지 않게 하는 지점이다.
                soldier.Initialize(_soldierPoolKey, _soldierSourceStat, statScaleRatio,
                                   link.Entity, _job, _unitName, _grade);

                // 병사에게 오는 보너스는 한 곳에서 한 번에 적용한다.
                // ⚠ 출처별로 따로 부르면 안 된다 — 각자 Base 를 읽으며 고쳐서
                //   적용 순서가 결과를 바꾼다 (SoldierStatApplier 주석 참고).
                if (soldierGO.TryGetComponent<EntityLink>(out var soldierLink)
                    && soldierLink.Entity != Entity.Null)
                {
                    var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                    if (world != null)
                        SoldierStatApplier.Apply(
                            soldierLink.Entity, world.EntityManager,
                            GetActivePassives(),        PassiveSkillDatabase.Current);
                }
            }
        }

        var logSkillId   = RareSkillArbiter.Resolve(_unitName, _job, ActiveSkillDatabase.Current, _grade);
        var logSkillData = ActiveSkillDatabase.Current?.Get(logSkillId);
        string skillName = logSkillData?.SkillName ?? logSkillId.ToString();

        Debug.Log($"[GeneralRuntimeBridge] '{_unitName}' 스폰 " +
                  $"| Lv:{_level}  등급:{_grade}  직업:{_job}  " +
                  $"HP:{_stat.Get(StatType.MaxHp):F0}  ATK:{_stat.Get(StatType.Attack):F0}  " +
                  $"병사:{soldierCount}명  스탯비율:{statScaleRatio:P0}  " +
                  $"패시브:[{_passive0},{_passive1},{_passive2}] 활성:{_activePassiveCount}슬롯  " +
                  $"액티브스킬:{skillName}({logSkillId})");
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────

    /// <summary>활성 슬롯 수만큼만 담은 패시브 배열을 반환한다.</summary>
    PassiveSkillType[] GetActivePassives()
    {
        switch (_activePassiveCount)
        {
            case 3: return new[] { _passive0, _passive1, _passive2 };
            case 2: return new[] { _passive0, _passive1 };
            default: return new[] { _passive0 };
        }
    }

    /// <summary>
    /// 쿨다운 감소 최종 보정 — 이제 상한 클램프만 한다.
    ///
    /// 예전에는 여기서 (raw/max)^1.5 체감 공식을 먹였는데,
    /// 그 방식은 합산값 전체에 지수를 걸어서 **출처가 하나뿐일 때도** 깎였다.
    /// "쿨타임 10% 감소" 장비 하나만 껴도 실제로는 3.3% 만 적용돼
    /// 아이템 설명과 실제가 어긋났다.
    ///
    /// 중첩 억제는 이제 합산 단계에서 처리한다 —
    /// UnitStat 의 CombineMode.MultiplyResidual (1 - Π(1-v)).
    /// 10% 하나면 10%, 두 개면 19%. 여기서는 상한만 지킨다.
    /// </summary>
    public static float ClampCDR(float cdr, float maxCDR)
        => Mathf.Clamp(cdr, 0f, maxCDR);
}
