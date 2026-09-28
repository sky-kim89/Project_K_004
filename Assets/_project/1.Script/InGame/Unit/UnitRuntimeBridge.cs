using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  UnitRuntimeBridge.cs
//  모든 유닛 RuntimeBridge 의 공통 베이스 클래스.
//
//  Pool 은 Instantiate 기반이므로 Baker 가 실행되지 않는다.
//  이 클래스가 Start() 에서 ECS Entity 를 직접 생성하고 EntityLink 에 등록한다.
//
//  파생 클래스 구현 사항:
//    - GetTeam()          : 팀 타입 반환 (Ally / Enemy)
//    - GetUnitType()      : 유닛 타입 반환 (General / Enemy / Elite ...)
//    - AddComponents()    : (선택) 타입 전용 ECS 컴포넌트 추가
//    - Initialize(...)    : 스폰 직후 Spawner 에서 호출 — _unitName, _stat 설정
//
//  라이프사이클:
//    OnEnable  → 상태 초기화 (풀 재사용 대비)
//    Start     → Entity 생성 후 EntityLink 에 등록
//    (EntityLink.OnDisable 이 풀 반납 시 Entity 파괴 처리)
// ============================================================

public abstract class UnitRuntimeBridge : MonoBehaviour
{
    // 아군 스폰 후 이동 시작까지 대기 시간
    const float AllySpawnMoveDelay = 1f;

    /// <summary>
    /// 스폰 후 이동을 시작하기까지의 대기(초).
    ///
    /// 원작 아군은 대형을 갖추고 서 있다가 적이 오면 움직였다. 그래서 1초를 뒀다.
    /// 이 게임의 소환 몬스터는 나오자마자 달려야 한다 — 대기하면 소환 지점에
    /// 쌓이기만 하고, 뒤이어 나오는 개체와 겹친다.
    ///
    /// 기본값은 원작 그대로 두고, 필요한 브리지가 0 으로 덮는다.
    /// </summary>
    protected virtual float SpawnMoveDelay
        => GetTeam() == TeamType.Ally ? AllySpawnMoveDelay : 0f;

    // 파생 클래스가 Initialize() 에서 설정
    protected string   _unitName;
    protected UnitStat _stat;

    /// <summary>
    /// 굴려 나온 최종 스탯. 스킬이 "시전자의 스탯" 을 기준으로 무언가를
    /// 만들 때 쓴다 (비석 강림의 스켈레톤 등).
    ///
    /// ⚠ 진영·계층을 가리지 않는다
    ///   예전에는 GeneralRuntimeBridge.GetRolledStat() 만 있었다. 그래서
    ///   그 스킬을 **소환사**가 쓰면 브리지 타입이 달라 조용히 return 했다
    ///   (비석 강림이 아무 일도 안 하던 이유). 스탯은 모든 유닛이 갖는
    ///   것이므로 베이스에 둔다.
    /// </summary>
    public UnitStat RolledStat => _stat;

    // ── 크기 기준점 ──────────────────────────────────────────
    //
    //  풀에서 재사용되므로 배율이 누적되지 않도록 항상 원본을 기준으로 다시 잡는다.

    /// <summary>프리팹 원본 크기. Awake 에서 한 번만 잡는다.</summary>
    protected Vector3 _baseScale;

    /// <summary>이번 등장에서 확정된 크기 (_baseScale × 스폰 배율). 광폭화 성장의 기준점.</summary>
    protected Vector3 _spawnScale;

    int _enrageStacks;

    protected virtual void Awake()
    {
        _baseScale  = transform.localScale;
        _spawnScale = _baseScale;
    }

    // ── 광폭화 성장 ──────────────────────────────────────────

    /// <summary>
    /// 광폭화 1스택만큼 몸집을 키우고, 새 반경(UnitSizeComponent.Radius 용)을 돌려준다.
    /// 호출자(ActiveBossEnrage)가 그 값을 ECS 에 써 넣는다 —
    /// 브리지는 GameObject 크기만 책임지고 엔티티는 건드리지 않는다.
    ///
    /// ⚠ 베이스에 있는 이유
    ///   원래는 적 전용 브리지에 있었다. 보스가 용사(General 프리팹)로 옮겨가면서
    ///   어느 브리지가 쓸지 알 수 없게 됐다 — 몸집을 키우는 건 유닛 종류와
    ///   무관한 동작이라 베이스가 맞는 자리다.
    ///
    /// ⚠ 누적 곱이 아니라 '등장 크기 기준 가산' 이다
    ///   매번 ×1.1 을 하면 복리라 스택 20 에 6.7배가 된다. 무한 보스(스폰 배율 ×2)
    ///   에서는 화면을 통째로 덮는다. 공격력 스택도 가산이므로 크기만 복리일 이유가 없다.
    ///
    /// ⚠ localScale.x 의 부호는 지킨다
    ///   UnitAnimationSync 가 좌우 반전에 이 부호를 쓴다. 그냥 덮어쓰면
    ///   스택이 오르는 프레임마다 유닛이 한 번씩 홱 뒤집힌다.
    /// </summary>
    public float GrowEnrage(float perStack, int maxStacks)
    {
        _enrageStacks = maxStacks > 0
            ? Mathf.Min(_enrageStacks + 1, maxStacks)
            : _enrageStacks + 1;

        Vector3 s = _spawnScale * (1f + perStack * _enrageStacks);
        if (transform.localScale.x < 0f) s.x = -s.x;
        transform.localScale = s;

        // 아래 반경 계산 공식과 같아야 한다 (Max(x,y) × 0.5)
        return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y)) * 0.5f;
    }

    /// <summary>풀에서 다시 나올 때 광폭화 누적을 지운다.</summary>
    protected void ResetEnrageGrowth()
    {
        _enrageStacks        = 0;
        transform.localScale = _spawnScale;
    }

    // ── 생사 판정 ────────────────────────────────────────────

    /// <summary>
    /// 이 유닛이 아직 살아 있는가.
    ///
    /// 엔티티가 아직 없으면(스폰 전) <b>true</b> 다 — 세워지기도 전에
    /// "죽었다" 로 읽히면 소환사 사망 판정이 시작하자마자 터진다.
    /// </summary>
    public bool IsAlive
    {
        get
        {
            if (!TryGetComponent<EntityLink>(out var link)) return true;
            if (link.Entity == Entity.Null)                 return true;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return true;

            EntityManager em = world.EntityManager;
            if (!em.Exists(link.Entity)) return false;

            if (em.HasComponent<DeadTag>(link.Entity)) return false;

            if (em.HasComponent<HealthComponent>(link.Entity))
                return em.GetComponentData<HealthComponent>(link.Entity).CurrentHp > 0f;

            return true;
        }
    }

    /// <summary>Initialize() 에서 설정된 유닛 이름 (AllySpawner 에서 넘긴 unitName).</summary>
    public string UnitName => _unitName;

    // ── 파생 클래스가 반드시 구현 ────────────────────────────

    protected abstract TeamType GetTeam();
    protected abstract UnitType GetUnitType();

    static float GetSeparationMass(UnitType type) => type switch
    {
        UnitType.Boss    => 20f,  // 병사 20배 — 군중에 밀리지 않음
        UnitType.Elite   => 3f,   // 병사 3배
        UnitType.General => 5f,
        _                => 1f,
    };

    /// <summary>기본 컴포넌트 추가 후 호출 — 타입 전용 컴포넌트를 여기서 추가.</summary>
    protected virtual void AddComponents(EntityManager em, Entity entity) { }

    /// <summary>Entity 재사용 시 호출 — 타입 전용 컴포넌트 값을 리셋할 때 오버라이드.</summary>
    protected virtual void OnEntityReset(EntityManager em, Entity entity) { }

    /// <summary>풀 반납(사망 또는 전투 종료) 직전 호출 — 사망 반응이 필요한 특성에서 오버라이드.</summary>
    public virtual void OnBeforeDespawn() { }

    // ── Unity 생명주기 ────────────────────────────────────────

    protected virtual void OnEnable()
    {
        _unitName = null;
        _stat     = null;
    }

    /// <summary>
    /// 파생 클래스의 Initialize() 마지막에 호출.
    /// Entity 가 없으면 최초 생성, 있으면 상태값만 리셋해서 재사용한다.
    /// </summary>
    protected void SpawnEntity()
    {
        // 앞뒤(그리는 순서)는 Unity 의 Transparency Sort Axis 에 맡긴다.
        // ⚠ 외형 조립(ApplyAlly/ApplyEnemy)이 끝난 뒤라야 한다
        //   CharacterBuilder 가 SpriteRenderer 를 갈아 끼우므로 그 전에 훑으면
        //   이미 사라진 렌더러 목록을 손보게 된다.
        UnitSortingSetup.Apply(gameObject);

        // 피격 플래시가 도는 중에 죽으면 코루틴이 끊긴 채 빨간 색이 굳는다.
        // 풀에서 꺼낸 유닛이 그 색을 물려받지 않게 여기서 원래 색으로 되돌린다.
        if (TryGetComponent<UnitAnimationSync>(out var animSync))
            animSync.ClearTint();

        // 강화 버프 표시 — 프리팹을 고치지 않고 여기서 한 번만 붙인다
        // (UnitSortingSetup 과 같은 방식. 모든 유닛이 지나는 유일한 길목이다)
        if (!TryGetComponent<UnitBuffAuraView>(out _))
            gameObject.AddComponent<UnitBuffAuraView>();

        if (!TryGetComponent<EntityLink>(out var link))
        {
            Debug.LogWarning($"[{GetType().Name}:{_unitName}] EntityLink 없음. 프리팹에 추가하세요.");
            return;
        }

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogWarning($"[{GetType().Name}:{_unitName}] ECS World 없음.");
            return;
        }

        EntityManager em = world.EntityManager;
        em.CompleteAllTrackedJobs();

        if (link.Entity != Entity.Null && em.Exists(link.Entity))
        {
            // ── 재사용: 상태값 리셋 후 Disabled 제거 ─────────
            ResetEntity(em, link.Entity);
            if (em.HasComponent<Disabled>(link.Entity))
                em.RemoveComponent<Disabled>(link.Entity);
        }
        else
        {
            // ── 최초 생성 ─────────────────────────────────────
            Entity entity = CreateBaseEntity(em);
            AddComponents(em, entity);
            link.Entity = entity;
        }

    }

    // ── Entity 재사용 시 상태 리셋 ───────────────────────────

    void ResetEntity(EntityManager em, Entity entity)
    {
        Vector3   pos       = transform.position;
        StatBlock statBlock = StatBlock.FromUnitStat(_stat);

        em.SetComponentData(entity, LocalTransform.FromPosition(
            new float3(pos.x, pos.y, pos.z)));
        em.SetComponentData(entity, new UnitIdentityComponent
        {
            UnitId = 0,
            Team   = GetTeam(),
            Type   = GetUnitType(),
        });
        em.SetComponentData(entity, new StatComponent { Base = statBlock, Final = statBlock });
        em.SetComponentData(entity, new HealthComponent
        {
            CurrentHp = _stat.Get(StatType.MaxHp),
        });
        em.SetComponentData(entity, new MovementComponent
        {
            Velocity         = float3.zero,
            Destination      = float3.zero,
            StoppingDistance = 0.5f,
            MoveDelay        = SpawnMoveDelay,
            IsMoving         = false,
        });
        em.SetComponentData(entity, new FormationSlotComponent
        {
            Row = 0, Column = 0, SlotPosition = float3.zero,
        });
        em.SetComponentData(entity, new AttackComponent
        {
            AttackCooldown = 0f,
            HasTarget      = false,
            RandomSeed     = (uint)UnityEngine.Random.Range(1, int.MaxValue),
        });
        em.SetComponentData(entity, new UnitStateComponent
        {
            Current = UnitState.Idle, Previous = UnitState.Idle, StateTimer = 0f,
        });
        em.SetComponentData(entity, new HitReactionComponent
        {
            KnockbackVelocity = float3.zero, StunDuration = 0f,
            StunTimer = 0f, IsStunned = false,
        });
        em.SetComponentData(entity, new GridCellComponent
        {
            Cell = int2.zero, PrevCell = int2.zero,
        });

        // 유닛 크기 갱신 (풀 재사용 시 스케일이 달라질 수 있음)
        Vector3 scale  = transform.localScale;
        float   radius = Mathf.Max(scale.x, scale.y) * 0.5f;
        float   mass   = GetSeparationMass(GetUnitType());
        em.SetComponentData(entity, new UnitSizeComponent { Radius = radius, Mass = mass });

        // 화면 진입 상태 초기화 (재스폰 시 다시 진입 판정)
        em.SetComponentData(entity, new ScreenStateComponent { HasEnteredScreen = false });

        // DeadTag 제거 (사망 상태로 반납된 경우 대비)
        if (em.HasComponent<DeadTag>(entity))
            em.RemoveComponent<DeadTag>(entity);

        // BreachedTag 제거 — 성벽을 통과해 거둬진 용사 엔티티가 재사용될 때 (2026-09-16 버그)
        //   CoreBreachSystem 은 이 태그가 있으면 판정에서 뺀다. 안 떼면 다음 판에 병사로 다시 선
        //   엔티티가 **처음부터 "이미 통과한 유닛"** 이라, 성벽선에 닿아도 영영 안 거둬지고
        //   머리만 박고 서 있었다(처치해도 골드도 없었다). 판을 거듭할수록 그런 개체가 늘었다.
        if (em.HasComponent<BreachedTag>(entity))
            em.RemoveComponent<BreachedTag>(entity);

        // SummonedTag 제거 (소환 유닛으로 쓰였던 엔티티가 재사용될 때 잔류 방지)
        if (em.HasComponent<SummonedTag>(entity))
            em.RemoveComponent<SummonedTag>(entity);

        // 소환 연출용 잠금·보호 제거
        //
        //  ⚠ '시간 기반이니 알아서 풀린다' 로는 부족하다
        //    남은 시간은 시뮬레이션이 도는 동안에만 줄어든다. 연출 도중 웨이브가
        //    끝나거나 전투가 멈춘 채 반납되면 0.45 가 그대로 남아 있고, 그 엔티티를
        //    물려받은 **다음 유닛이 못 움직이고 조준도 안 되는 상태로** 등장한다.
        //    (다음 스테이지 시작 순간이라 눈에 잘 띈다)
        if (em.HasComponent<SkillCastLock>(entity))
            em.RemoveComponent<SkillCastLock>(entity);

        if (em.HasComponent<SpawnProtection>(entity))
            em.RemoveComponent<SpawnProtection>(entity);

        // 풀 링크 갱신 — 사망 처리 시 UnitDeathDespawnSystem 이 제거하므로 없으면 재추가
        if (em.HasComponent<BattleGame.Units.UnitPoolLinkComponent>(entity))
        {
            var poolLink = em.GetComponentObject<BattleGame.Units.UnitPoolLinkComponent>(entity);
            poolLink.PoolKey      = _unitName;
            poolLink.LinkedObject = gameObject;
        }
        else
        {
            em.AddComponentObject(entity, new BattleGame.Units.UnitPoolLinkComponent
            {
                PoolKey      = _unitName,
                LinkedObject = gameObject,
            });
        }

        // 버퍼 클리어
        em.GetBuffer<HitEventBufferElement>(entity).Clear();
        em.GetBuffer<StatusEffectBufferElement>(entity).Clear();
        em.GetBuffer<DamageResultElement>(entity).Clear();
        em.GetBuffer<HealEventBufferElement>(entity).Clear();

        // 파생 클래스 전용 컴포넌트 리셋
        OnEntityReset(em, entity);
    }

    // ── 공통 Entity 생성 ─────────────────────────────────────

    Entity CreateBaseEntity(EntityManager em)
    {
        Entity entity = em.CreateEntity();

        // ── Transform ─────────────────────────────────────────
        Vector3 pos = transform.position;
        em.AddComponentData(entity, LocalTransform.FromPosition(
            new float3(pos.x, pos.y, pos.z)));

        // ── 식별 ──────────────────────────────────────────────
        em.AddComponentData(entity, new UnitIdentityComponent
        {
            UnitId = 0,
            Team   = GetTeam(),
            Type   = GetUnitType(),
        });

        // ── 스탯 ──────────────────────────────────────────────
        StatBlock statBlock = StatBlock.FromUnitStat(_stat);
        em.AddComponentData(entity, new StatComponent { Base = statBlock, Final = statBlock });

        // ── 체력 ──────────────────────────────────────────────
        em.AddComponentData(entity, new HealthComponent
        {
            CurrentHp = _stat.Get(StatType.MaxHp),
        });

        // ── 이동 ──────────────────────────────────────────────
        em.AddComponentData(entity, new MovementComponent
        {
            Velocity         = float3.zero,
            Destination      = float3.zero,
            StoppingDistance = 0.5f,
            MoveDelay        = SpawnMoveDelay,
            IsMoving         = false,
        });
        em.AddComponentData(entity, new FormationSlotComponent
        {
            Row          = 0,
            Column       = 0,
            SlotPosition = float3.zero,
        });

        // ── 전투 ──────────────────────────────────────────────
        em.AddComponentData(entity, new AttackComponent
        {
            AttackCooldown = 0f,
            HasTarget      = false,
            RandomSeed     = (uint)UnityEngine.Random.Range(1, int.MaxValue),
        });
        em.AddComponentData(entity, new UnitStateComponent
        {
            Current    = UnitState.Idle,
            Previous   = UnitState.Idle,
            StateTimer = 0f,
        });
        em.AddComponentData(entity, new HitReactionComponent
        {
            KnockbackVelocity = float3.zero,
            StunDuration      = 0f,
            StunTimer         = 0f,
            IsStunned         = false,
        });
        em.AddComponentData(entity, new GridCellComponent
        {
            Cell     = int2.zero,
            PrevCell = int2.zero,
        });

        // ── 유닛 크기 (분리 반경 + 질량) ──────────────────────────────
        Vector3 scale  = transform.localScale;
        float   radius = Mathf.Max(scale.x, scale.y) * 0.5f;
        float   mass   = GetSeparationMass(GetUnitType());
        em.AddComponentData(entity, new UnitSizeComponent { Radius = radius, Mass = mass });

        // ── 화면 경계 ─────────────────────────────────────────
        em.AddComponentData(entity, new ScreenStateComponent { HasEnteredScreen = false });

        // ── 동적 버퍼 ─────────────────────────────────────────
        em.AddBuffer<HitEventBufferElement>(entity);
        em.AddBuffer<StatusEffectBufferElement>(entity);
        em.AddBuffer<DamageResultElement>(entity);
        em.AddBuffer<HealEventBufferElement>(entity);

        // ── 풀 반납 링크 (managed component) ──────────────────
        // UnitDeathDespawnSystem 이 DeadTag 감지 후 이 컴포넌트로 풀 반납
        em.AddComponentObject(entity, new BattleGame.Units.UnitPoolLinkComponent
        {
            PoolKey      = _unitName,
            LinkedObject = gameObject,
        });

        return entity;
    }
}
