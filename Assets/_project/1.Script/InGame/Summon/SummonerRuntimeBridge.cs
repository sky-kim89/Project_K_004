using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  SummonerRuntimeBridge.cs
//  소환사(플레이어 본체) 전용 RuntimeBridge.
//
//  ■ 이 유닛의 HP 가 곧 마왕성 HP 다
//    별도 코어 엔티티가 없다. 소환사가 죽으면 런이 끝난다.
//    그래서 BattleManager 의 패배 판정은 "아군 전멸" 이 아니라
//    "소환사 사망" 을 봐야 한다 (SummonerRuntime.IsAlive).
//
//  ■ 움직이지 않는다
//    왼쪽 성벽 뒤에 서서 넓은 사거리로 쏜다. MoveSpeed 를 0 으로 박아
//    타겟을 쫓아 나가지 않게 한다 — 나가면 성이 비어 버린다.
//
//  ■ 외형은 인간형 합성이다
//    장비를 장착할 수 있어야 하므로 CharacterBuilder 경로를 쓴다.
//    프리팹은 미리 구운 완성본이라 스킨 합성은 하지 않지만,
//    장비 오버레이가 얹힐 자리는 열려 있다.
// ============================================================

public class SummonerRuntimeBridge : UnitRuntimeBridge
{
    /// <summary>지금 전장에 서 있는 소환사. 런당 1기뿐이다.</summary>
    public static SummonerRuntimeBridge Current { get; private set; }

    SummonerData _data;

    /// <summary>이 소환사의 정의. 마나 총량·덱 슬롯 수를 여기서 읽는다.</summary>
    public SummonerData Data => _data;

    /// <summary>프리팹 원래 크기 — 몬스터 모습 배율(AppearanceScale)의 기준. 한 번만 잡는다.</summary>
    Vector3 _authoredScale;
    bool    _hasAuthoredScale;

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// 소환사를 세운다. 런 시작 시 1회만 호출된다.
    /// </summary>
    public void Initialize(SummonerData data)
    {
        _data     = data;
        _unitName = data.Id;

        // ── 몸집 — 몬스터 모습이면 그 배율 (슬라임 킹 = 기본 슬라임의 커다란 판) ──
        //   ⚠ 원래 크기를 **한 번만** 기억하고 거기서 곱한다
        //     인게임 씬은 런 사이에 상주한다. 지금 크기에 곱하면 슬라임 킹을 고를 때마다 1.6배씩 불어난다.
        //   ⚠ SpawnEntity 보다 먼저다 — 히트박스 반경이 localScale 에서 나온다.
        if (!_hasAuthoredScale)
        {
            _authoredScale    = transform.localScale;
            _hasAuthoredScale = true;
        }

        float look = data.AppearanceSpecies != null ? Mathf.Max(0.1f, data.AppearanceScale) : 1f;
        transform.localScale = _authoredScale * look;

        _spawnScale = transform.localScale;

        _stat = BuildStat(data);

        // ⚠ SpawnEntity 보다 먼저 — 외형 조립이 SpriteRenderer 를 갈아 끼운다
        //   SpawnEntity 안의 UnitSortingSetup 이 렌더러 목록을 훑으므로,
        //   순서가 뒤집히면 이미 사라진 렌더러에 정렬 설정을 쓰게 된다.
        ApplyAppearance(data);

        SpawnEntity();

        Current = this;

        // 소환사 평타도 통계에 잡혀야 한다 — 사거리·공격력 투자를 판단하는 근거다.
        if (TryGetComponent<EntityLink>(out var link) && link.Entity != Entity.Null)
            CardStatsTracker.Instance.RegisterOwner(
                link.Entity, CardStatsTracker.SummonerKey, SummonKind.Monster, isSummoner: true);
    }

    /// <summary>
    /// 캐릭터 외형을 조립한다.
    ///
    /// ⚠ 이걸 부르지 않으면 소환사가 화면에 보이지 않는다
    ///   Summoner.prefab 은 General.prefab 복제본이라 스프라이트가 비어 있다.
    ///   장수는 GeneralRuntimeBridge 가 ApplyAlly 로 채우는데, 소환사에는
    ///   그 호출이 없어서 아무것도 안 그려진 채 서 있었다.
    ///   ("성 위에 아무도 없다" 의 정체가 이것이다)
    ///
    /// 입력은 전부 SummonerData 가 갖는다 — 결정적이라 같은 소환사는 언제나
    /// 같은 모습이다. 여기에 직업·등급을 박으면 소환사를 늘릴 때마다 코드를 고쳐야 한다.
    /// </summary>
    void ApplyAppearance(SummonerData data)
    {
        // ── 몬스터 모습의 소환사 (슬라임 킹, 사용자 요청 2026-09-11) ──
        //   소환 몬스터와 **같은 길**(MonsterAppearanceBridge)로 통짜 라이브러리를 꽂는다.
        //   Summoner.prefab 은 General.prefab 복제라 CharacterBuilder·UnitAppearanceBridge 가 있어
        //   그 컴포넌트가 요구하는 것을 이미 갖추고 있다. 머리 위 표식(왕관)도 같은 컴포넌트다.
        if (data.AppearanceSpecies != null)
        {
            if (!TryGetComponent<MonsterAppearanceBridge>(out var look))
                look = gameObject.AddComponent<MonsterAppearanceBridge>();
            look.Apply(data.AppearanceSpecies, data.Id, MonsterGearVisual.None);

            if (!TryGetComponent<MonsterMarkView>(out var markView))
                markView = gameObject.AddComponent<MonsterMarkView>();
            markView.Setup(data.AppearanceMark);
            return;
        }

        // 인간형 — 이 오브젝트가 직전 런에 몬스터 모습이었을 수 있다 (인게임 씬은 런 사이에 상주한다).
        //   합성기를 다시 켜고, 표식을 끄고, 꽂아 둔 라이브러리 기억을 지운다.
        GetComponent<Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts.CharacterBuilder>()
            .enabled = true;
        if (TryGetComponent<MonsterMarkView>(out var oldMark)) oldMark.Setup(MonsterMark.None);
        if (TryGetComponent<MonsterAppearanceBridge>(out var oldLook)) oldLook.ForgetLibrary();

        GetComponent<UnitAppearanceBridge>()
            .ApplyAlly(data.ResolvedAppearanceSeed, data.AppearanceJob, data.AppearanceGrade);
    }

    /// <summary>
    /// 3대 스탯을 실제 전투 스탯으로 환산한다.
    /// 환산 계수는 SummonerData 가 소유한다 — 여기에 숫자를 박지 말 것.
    /// </summary>
    static UnitStat BuildStat(SummonerData data)
    {
        var stat = new UnitStat();

        // 쿨감은 출처끼리 더하지 않고 곱연산으로 겹친다 — 원작 규칙과 동일.
        stat.SetCombineMode(StatType.SkillCooldownReduce, CombineMode.MultiplyResidual);

        stat.Set(StatType.MaxHp,       data.MaxHp);        // 체력 → 마왕성 내구력
        stat.Set(StatType.Attack,      data.Attack);       // 패기 → 소환사 평타 (곁다리)
        stat.Set(StatType.SummonPower, data.SummonPower);  // 지능 → 소환력

        stat.Set(StatType.AttackRange, data.AttackRange);
        stat.Set(StatType.AttackSpeed, data.AttackSpeed);
        stat.Set(StatType.Defense,     data.Defense);

        // ⚠ 이동속도 0 — 성벽 뒤를 떠나지 않는다
        //   타겟을 쫓아 나가면 성이 비고, 적이 그대로 뒤로 돌아 들어간다.
        stat.Set(StatType.MoveSpeed, 0f);

        stat.Set(StatType.CritChance, 0.05f);
        stat.Set(StatType.CritDamage, 1.5f);

        return stat;
    }

    // ── UnitRuntimeBridge 구현 ───────────────────────────────

    /// <summary>
    /// 시그니처 스킬의 쿨다운.
    ///
    /// ⚠ 제한은 쿨다운이 아니라 **스테이지당 횟수**다 (SummonerSkillRule).
    ///   그래도 0 으로 두지 않는다 — 어떤 경로로든 자동 발동(ActiveSkillAISystem)에
    ///   걸리면 매 프레임 스킬이 도는 사고가 난다. 눌러서 쓰는 쪽은
    ///   실행 이벤트를 직접 적으므로 이 값을 타지 않는다.
    /// </summary>
    const float SkillCooldown = 8f;

    protected override TeamType GetTeam()     => Faction.Monster;   // 아군
    protected override UnitType GetUnitType() => UnitType.Summoner;

    /// <summary>
    /// 소환사는 원거리 공격을 한다 — 성벽 뒤에서 쏘려면 필수다.
    ///
    /// ■ WallCoverComponent — 이게 없으면 패배가 성립하지 않는다
    ///   소환사는 성벽 왼쪽에 서고 용사는 성벽에서 멈추므로 둘 사이는 성벽
    ///   두께만큼 절대 좁혀지지 않는다. 그런데 근접 용사의 사거리는 0.7~1.2 로
    ///   굴려지는 값이라(GameplayConfig) 성벽이 그보다 두꺼우면 **영원히 못 때린다.**
    ///   성벽에 붙어 선 채로 서 있기만 하고 마왕성 HP 가 줄지 않는다.
    ///
    ///   그래서 소환사를 노린 공격에 한해 성벽 두께만큼 사거리를 얹어 준다.
    ///   "성벽 너머로 창을 찔러 넣는다" 에 해당하는 보정이다.
    ///   보정값은 씬 배치에서 그대로 나온다 — 성벽이나 소환사를 옮기면 따라온다.
    /// </summary>
    protected override void AddComponents(EntityManager em, Entity entity)
    {
        em.AddComponent<RangedTag>(entity);

        // ⚠ RangedTag 만으로는 한 발도 못 쏜다
        //   RangedAttackJob 은 UnitJobComponent 와 ProjectileLaunchRequest 버퍼를
        //   **필수 쿼리 조건**으로 잡는다. 둘 중 하나만 빠져도 쿼리에 안 걸리고,
        //   RangedTag 때문에 MeleeAttackJob 에서도 제외된다 —
        //   즉 소환사가 성벽 뒤에서 아무것도 하지 않은 채 서 있게 된다.
        //
        //   직업을 Mage 로 박는 이유는 발사체 그림이 여기서 갈리기 때문이다
        //   (ProjectileSpawnSystem.GetPoolKey: Mage → MagicBolt, 그 외 → Arrow).
        //   기획상 소환사는 전원 마법구를 쏜다 — 화살·기타 발사체는 나중에
        //   SummonerData 에 종류 필드를 두고 갈라 붙인다.
        //   소환사에게는 전투 직업 개념이 없으므로 이 값은 외형·발사체 전용이다.
        em.AddComponentData(entity, new UnitJobComponent { Job = UnitJob.Mage });
        em.AddBuffer<ProjectileLaunchRequest>(entity);

        // 사거리가 용사의 두 배를 넘으므로 발사체도 그만큼 빨라야 한다
        // (기본 마법구 10 으로 24 를 날리면 착탄까지 2.4초가 걸린다).
        em.AddComponentData(entity, new ProjectileSpeedOverride
        {
            Speed = _data.ProjectileSpeed,
        });

        em.AddComponentData(entity, new WallCoverComponent
        {
            ExtraReach = SummonFieldLayout.Instance.WallGap,
        });

        // ── 평타는 대상 최대 체력 비례다 (사용자 확정, 2026-09-07) ──
        //   ⚠ 공격력 수치(패기 × 2)로는 후반에 아무 일도 못 한다
        //     8~16 짜리 평타가 체력 수천짜리 용사를 상대로는 없는 것과 같았다.
        //   ⚠ 격에 따라 갈린다 — "소환사가 다 한다" 를 막는 것은 이 세 숫자다
        //     잡병 4대 · 엘리트 20대 · 보스 100대. 보스는 몬스터가 잡아야 한다.
        //   수치의 정본은 SummonerStrikeRule 이다 — 유물이 여기를 올린다.
        em.AddComponentData(entity, SummonerStrikeRule.Build(_data));

        // ⚠ 마왕은 자리를 뜨지 않는다 — 이 두 줄이 그 규칙 전부다
        //   StationaryTag    : 위치를 건드리는 잡(이동·분리·넉백) 쿼리에서 통째로 빠진다.
        //                      "이동속도 0" 으로 흉내 내면 잡마다 따로 막아야 하고,
        //                      실제로 분리가 빠져 있어 겹친 용사에게 조금씩 떠밀렸다.
        //   KnockbackImmune  : 넉백·경직을 안 받는다(기존 방패병 달인 장치를 그대로 쓴다).
        //                      경직이 없으면 UnitState.Hit 도 서지 않으므로,
        //                      맞아도 공격 자세가 끊기지 않는다.
        //
        //   그래서 마왕은 제자리에 서 있다가 사거리에 든 용사를 쏘기만 한다.
        //   "쫓아갈까" 를 판단하는 곳은 UnitAttackSystem 하나뿐이고,
        //   거기서도 이동속도가 0 이면 Chasing 대신 Idle 로 남는다.
        // ── 시그니처 스킬 ────────────────────────────────────
        //
        //  ⚠ 스킬 슬롯과 실행 버퍼가 **둘 다** 있어야 한다
        //    슬롯(GeneralActiveSkillComponent)은 쿨다운·스킬 ID 를 담고,
        //    버퍼(ActiveSkillExecuteEvent)는 "지금 터뜨려라" 를 받는다.
        //    버퍼가 없으면 SummonerSkillRule.Fire 가 조용히 false 를 내고
        //    **버튼을 눌러도 아무 일이 없다** — 횟수만 사라진다.
        //    (몬스터 쪽에서 같은 누락으로 크래시를 겪었다. MonsterRuntimeBridge 참고)
        //
        //  ⚠ 스킬이 없는 소환사여도 붙인다
        //    소환사는 한 종류의 프리팹을 돌려 쓰므로, 있고 없고를 가리면
        //    "스킬 있는 소환사로 바꿨더니 안 나가는" 상태가 생긴다.
        em.AddComponentData(entity, new GeneralActiveSkillComponent
        {
            SkillId           = (int)_data.SignatureSkill,
            Cooldown          = SkillCooldown,
            CooldownRemaining = 0f,
        });
        em.AddBuffer<ActiveSkillExecuteEvent>(entity);

        em.AddComponent<StationaryTag>(entity);
        em.AddComponent<KnockbackImmuneTag>(entity);
    }

    /// <summary>
    /// 엔티티를 풀에서 물려받았을 때 소환사별 값을 다시 씌운다.
    ///
    /// ⚠ AddComponents 는 최초 1회만 돈다
    ///   풀이 GameObject 와 엔티티를 통째로 재사용하므로, 다음 런에서 **다른**
    ///   소환사를 골라도 앞 소환사의 발사체 속도·성벽 보정이 그대로 남는다.
    ///   둘 다 SummonerData / 씬 배치에서 나오는 값이라 여기서 다시 잡아 준다.
    /// </summary>
    protected override void OnEntityReset(EntityManager em, Entity entity)
    {
        em.SetComponentData(entity, new UnitJobComponent { Job = UnitJob.Mage });

        em.SetComponentData(entity, new ProjectileSpeedOverride
        {
            Speed = _data.ProjectileSpeed,
        });

        em.SetComponentData(entity, new WallCoverComponent
        {
            ExtraReach = SummonFieldLayout.Instance.WallGap,
        });
    }

    // ⚠ 풀에 돌아가는 것(OnDisable)도 전장을 떠난 것이다 — 파괴를 기다리면 판을 닫은 뒤에도
    //   '서 있는 소환사' 가 남아, 그걸 보고 몬스터를 세우는 코드가 막히지 않는다.
    void OnDisable()
    {
        if (Current == this) Current = null;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
