using UnityEngine;
using BattleGame.Units;
using Unity.Entities;

// ============================================================
//  SummonerPerkRuntime.cs
//  소환사 개성(SummonerPerk)의 실행부. 훅 네 개가 전부다.
//
//  ■ 왜 한 파일에 몰아 두나
//    액티브 스킬은 종류마다 연출과 판정이 통째로 달라서 파일을 갈랐다
//    (Actives/Active*.cs). 개성은 대부분 세 줄짜리 분기다 —
//    파일을 12개로 쪼개면 "무엇이 어디에 끼어드는가" 를 읽으려고
//    12개를 다 열어야 한다. 훅이 네 개뿐이니 훅 기준으로 모아 두는 편이 읽힌다.
//
//    ⚠ 한 개성이 열 줄을 넘어가기 시작하면 그때 쪼갤 것.
//
//  ■ 훅이 불리는 자리
//    ManaCostFor     ← SummonController.TryReserve  (마나를 내기 직전)
//    (약탈은 이제 런 골드 배율 — RunGoldRule.Grant)
//    GradeFor        ← SummonController.ResolveGrade
//    OnMonsterSpawned← SummonController.SpawnOne    (스탯이 박힌 직후)
//    OnMonsterDied   ← MonsterDeathWatcher
//
//  ■ ⚠ 여기서 스탯을 직접 만지지 않는다
//    스탯 합성의 정본은 MonsterStatComposer 하나다. 개성이 스탯을 바꿔야 하면
//    Composer 에 인자로 넘겨서 거기서 곱한다 — 두 곳에서 곱하기 시작하면
//    "왜 이 몬스터만 공격력이 이상한가" 를 추적할 수 없게 된다.
//    (DeepChannel·WildSprint 가 Composer 쪽에 있는 이유다)
// ============================================================

namespace BattleGame.Units
{
    /// <summary>
    /// 역병 술사의 친화 종족(카드로 낸 것)에게만 붙는다 — 이 개체가 쓰러뜨린 자리에서 좀비가 일어난다.
    /// ⚠ 풀 재사용 때 떼야 한다 (MonsterRuntimeBridge.ClearSpeciesPassiveResidue).
    /// </summary>
    public struct PlagueRiserTag : IComponentData { }
}

public static class SummonerPerkRuntime
{
    // ── 런 스코프 상태 ───────────────────────────────────────
    //
    //  ⚠ static 이므로 반드시 초기화해 줘야 한다
    //    에디터는 플레이를 멈춰도 static 이 살아 있다. 다음 런이 지난 런의
    //    시체 수를 물려받으면 시작하자마자 거인이 튀어나온다.
    //    RunBootstrap 이 스테이지를 준비할 때마다 ResetRunState 를 부른다.

    /// <summary>도살자 — 이번 스테이지에 쌓인 아군 시체 수.</summary>
    static int _corpseCount;

    /// <summary>슬라임 킹 — 이번 스테이지에 부른 친화 종족 수. N번째마다 원거리로 선다.</summary>
    static int _spitCounter;

    /// <summary>역병 술사 — 이번 프레임에 친화 종족이 쓰러뜨린 자리 (UnitDeathDespawnSystem 이 쌓는다).</summary>
    static readonly System.Collections.Generic.List<Vector3> _plagueKills = new(8);

    /// <summary>스테이지 경계에서 런 스코프 상태를 지운다.</summary>
    public static void ResetRunState()
    {
        _corpseCount = 0;
        _spitCounter = 0;
        _plagueKills.Clear();
    }

    // ── 역병 — 쓰러뜨린 자리에서 좀비가 일어난다 (사용자 지시, 2026-09-11) ──
    //
    //  ■ 왜 큐인가
    //    처치는 UnitDeathDespawnSystem(ECS 시스템) 한가운데서 알 수 있는데, 거기서 곧장
    //    몬스터를 세우면 시스템 도중에 구조 변경이 일어난다. 자리만 적어 두고
    //    SummonController.Update 가 한 번에 세운다.
    //  ■ ⚠ 카드로 낸 좀비만 일으킨다 — 태그(PlagueRiserTag)는 OnMonsterSpawned(카드 경로)만 붙인다.
    //    일어난 좀비가 또 일으키면 판이 좀비로 끝없이 불어난다.

    /// <summary>친화 종족이 적을 쓰러뜨렸다 — 그 자리를 적어 둔다.</summary>
    public static void NotePlagueKill(Vector3 at) => _plagueKills.Add(at);

    /// <summary>
    /// 적어 둔 자리마다 확률(PerkValue)로 좀비를 세운다.
    /// ⚠ 판을 이미 거뒀으면 버린다 — 거둔 뒤에 선 좀비는 다음 판까지 남는다 (MonsterDeathWatcher 와 같은 이유).
    /// </summary>
    public static void FlushPlague(SummonerData summoner, bool waveSwept)
    {
        if (_plagueKills.Count == 0) return;

        if (!waveSwept && summoner != null && summoner.Perk == SummonerPerk.PlagueRise)
            foreach (Vector3 at in _plagueKills)
                if (Random.value < summoner.PerkValue)
                    RaiseFree(summoner, "zombie", at);

        _plagueKills.Clear();
    }

    // ── 마나 비용 ────────────────────────────────────────────

    /// <summary>
    /// 이 카드를 이 소환사가 낼 때 실제로 드는 마나.
    ///
    /// ⚠ 비용은 1 밑으로 내려가지 않는다
    ///   0 이 되면 무한 소환이 성립해 마나라는 자원 자체가 사라진다.
    /// </summary>
    /// <summary>
    /// 카드 칸까지 아는 경로 — 강화소가 깎아 둔 몫이 여기서 빠진다.
    ///
    /// ⚠ 비용을 보여 주는 곳과 실제로 내는 곳이 **둘 다** 이 함수를 지나야 한다.
    ///   한쪽만 지나면 카드에 뜬 숫자와 빠져나가는 마나가 달라진다.
    /// </summary>
    public static float ManaCostFor(SummonerData summoner, MonsterSpeciesData species,
                                    in SummonDeckSlot card)
        => ManaCostFor(summoner, species, species.ManaCost - card.ManaDiscount);

    public static float ManaCostFor(SummonerData summoner, MonsterSpeciesData species,
                                    float baseCost)
    {
        float cost = baseCost;

        if (summoner.Perk == SummonerPerk.CheapAffinity && summoner.IsAffinity(species))
            cost -= summoner.PerkValue;   // PerkValue = 할인액 (마나)

        // 특성(친화 할인·만물 할인·저울·원군)이 얹는 증감.
        // ⚠ 하한은 여기서 딱 한 번 건다 — RunPerkRule 은 델타만 돌려준다.
        cost += RunPerkRule.ManaCostDelta(summoner, species);

        // 몬스터 장비 — 카드 비용 −N (종족에 끼운 장비의 레벨이 연다).
        // ⚠ 여기 한 곳에서 뺀다 — 카드 표시·실제 소모·전황이 전부 이 함수를 지난다.
        cost -= MonsterGearRule.ManaCutFor(species.Id);

        return Mathf.Max(1f, cost);
    }

    // ⚠ '약탈(Plunder)' 의 마나 회복 배율(RegenBonusFor)은 걷었다 (사용자 지시, 2026-09-15)
    //   이름(전리품)과 무관한 옛 환수 규칙의 잔재였다. 지금은 런 골드 배율이다 — RunGoldRule.Grant.

    // ── 마나 개성 (2026-09-12) ──────────────────────────────

    /// <summary>
    /// '자연의 회복'(드루이드) — 판을 넘길 때 **그릇의 일정 비율**이 더 돌아온다.
    /// ⚠ 셈은 ManaRegenRule 한 곳이 한다 (RawFor·Describe 가 이 함수를 부른다).
    /// </summary>
    public static float NatureRestoreFor(SummonerData summoner)
        => summoner != null && summoner.Perk == SummonerPerk.NatureRestore
             ? RunPerkRule.MaxManaFor(summoner) * summoner.PerkValue
             : 0f;

    /// <summary>
    /// '쌍둥이 소집'(군악대장) — 이 카드가 옆 라인에도 세울 마릿수. 개성이 없으면 0.
    ///
    /// ⚠ 공짜 물량이다 — 특성 '쌍둥이 라인'(절반 · 비용 +2)과 달리 값을 받지 않는다.
    ///   대신 **마릿수가 1로 고정**이라 물량 카드일수록 비율이 작다. 이 소환사의 전부다.
    /// ⚠ 옆 라인은 오른쪽, 오른쪽 끝이면 왼쪽이다 (RunPerkRule.TwinLaneOf 와 같은 규칙).
    /// </summary>
    public static int TwinCallCount(SummonerData summoner)
        => summoner != null && summoner.Perk == SummonerPerk.TwinCall
             ? Mathf.Max(1, Mathf.RoundToInt(summoner.PerkValue))
             : 0;

    /// <summary>그 라인의 옆 라인. 라인이 하나뿐이면 −1.</summary>
    public static int NextLaneOf(int lane)
    {
        int lanes = SummonFieldLayout.LaneCount;
        if (lanes <= 1) return -1;

        return lane + 1 < lanes ? lane + 1 : lane - 1;
    }

    /// <summary>'결정화'(결정술사) 가 쌓을 수 있는 최대 마나의 상한 — 아끼기만 하는 판이 끝없이 불어나지 않게.</summary>
    public const float CrystalCeiling = 100f;

    /// <summary>
    /// '결정화' — 판을 넘길 때 남은 마나의 PerkValue 가 최대 마나로 굳는다.
    /// ⚠ RunBootstrap.AdvanceStage 가 **회복·그릇 맞추기 전에** 부른다 (아낀 만큼이다).
    /// </summary>
    public static void SettleCrystal(SummonerData summoner)
    {
        if (summoner == null || summoner.Perk != SummonerPerk.Crystallize) return;

        var user = UserDataManager.Instance;
        var mana = user.Get<SummonManaData>();
        var boon = user.Get<RunBoonData>();

        int room = Mathf.RoundToInt(CrystalCeiling) - boon.CrystalMaxMana;
        int gain = Mathf.Min(Mathf.FloorToInt(mana.Current * summoner.PerkValue), room);

        if (gain > 0) boon.AddCrystalMaxMana(gain);
    }

    // ── 품질 ─────────────────────────────────────────────────

    /// <summary>
    /// 이 소환사가 부르면 품질이 달라지는가.
    /// 스컬 킹처럼 "친화 종족만 한 단계 위로" 를 표현한다.
    /// </summary>
    public static UnitGrade GradeFor(SummonerData summoner, MonsterSpeciesData species,
                                     UnitGrade baseGrade)
    {
        if (summoner.Perk != SummonerPerk.AffinityGrade)  return baseGrade;
        if (!summoner.IsAffinity(species))                return baseGrade;
        if (baseGrade >= UnitGrade.Epic)                  return baseGrade;

        return baseGrade + Mathf.Max(1, Mathf.RoundToInt(summoner.PerkValue));
    }

    // ── 소환 직후 ────────────────────────────────────────────

    /// <summary>
    /// 개체가 세워진 직후. 태그를 얹거나 몸집을 키우는 자리다.
    ///
    /// ⚠ 스탯 숫자는 여기서 만지지 않는다 (파일 머리 주석 참고)
    ///   여기서 하는 일은 "ECS 태그를 하나 더 붙인다" 수준이어야 한다.
    /// </summary>
    public static void OnMonsterSpawned(SummonerData summoner, MonsterSpeciesData species,
                                        GameObject monster, int lane)
    {
        switch (summoner.Perk)
        {
            // 전쟁 함성 — 친화 종족이 여럿 살아 있을수록 강해진다.
            // 실제 수치는 StrengthStack 패시브가 쌓아 준다 (기존 시스템 재활용).
            case SummonerPerk.WarCry when summoner.IsAffinity(species):
                AddPassive(monster, PassiveSkillType.StrengthStack);
                break;

            // 재생 — 트롤 조련사. 피격 시 회복 패시브를 얹는다.
            case SummonerPerk.Regenerate when summoner.IsAffinity(species):
                AddPassive(monster, PassiveSkillType.QuickRecovery);
                break;

            // 무리 결속 — 같은 라인에 겹칠수록 강해진다.
            // 처치 누적으로 표현한다 (라인 추적을 따로 만들지 않기 위해).
            case SummonerPerk.PackBond when summoner.IsAffinity(species):
                AddPassive(monster, PassiveSkillType.KillEmpower);
                break;

            // 증식 — 같은 종족을 연달아 낼수록 그 종족이 커진다.
            // ⚠ 옛 슬라임 킹 개성이다 (2026-09-11 점액 사격으로 교체). 옛 세이브의 소환사 SO 가
            //   다시 굽기 전까지 이 값을 들고 있을 수 있어 남긴다.
            case SummonerPerk.SwellOnRepeat when summoner.IsAffinity(species):
                Swell(monster, species);
                break;

            // 점액 사격 — 친화 종족 N마리 중 1마리가 원거리로 선다 (사용자 지시, 2026-09-11)
            case SummonerPerk.SlimeSpit when summoner.IsAffinity(species):
                if (++_spitCounter % Mathf.Max(2, Mathf.RoundToInt(summoner.PerkValue)) == 0)
                    MakeSpitter(monster);
                break;

            // 역병 — 이 개체가 적을 쓰러뜨리면 그 자리에서 좀비가 일어난다.
            //   판정은 UnitDeathDespawnSystem 이 마지막 일격의 주인에게서 이 태그를 본다.
            case SummonerPerk.PlagueRise when summoner.IsAffinity(species):
                AddTag<PlagueRiserTag>(monster);
                break;
        }
    }

    // ── 점액 사격 ────────────────────────────────────────────

    /// <summary>점액을 뱉는 슬라임의 사거리 (고블린 궁수 6 · 해골 술사 7.5 보다 짧다).</summary>
    const float SpitRange = 5f;

    /// <summary>
    /// 이 개체를 원거리로 바꾼다 — RangedTag · 발사체 종류 · 사거리 셋을 한꺼번에.
    ///
    /// ⚠ 셋 다 있어야 RangedAttackJob 에 걸린다 (MonsterRuntimeBridge 머리 주석).
    /// ⚠ 되돌리는 코드는 따로 없다 — 풀에서 재사용될 때 OnEntityReset 이 종족의
    ///   공격 형태로 RangedTag·발사체를 되돌리고, 사거리는 새 스탯이 덮는다.
    /// </summary>
    static void MakeSpitter(GameObject monster)
    {
        if (!TryEntity(monster, out EntityManager em, out Entity e)) return;

        if (!em.HasComponent<RangedTag>(e)) em.AddComponent<RangedTag>(e);

        em.SetComponentData(e, new UnitJobComponent
        {
            Job = MonsterProjectile.ToJob(MonsterProjectileKind.MagicBolt),
        });
        em.SetComponentData(e, new ProjectileSpeedOverride { Speed = 10f });

        StatComponent stat = em.GetComponentData<StatComponent>(e);
        stat.Base [StatType.AttackRange] = SpitRange;
        stat.Final[StatType.AttackRange] = SpitRange;
        em.SetComponentData(e, stat);
    }

    static void AddTag<T>(GameObject monster) where T : unmanaged, IComponentData
    {
        if (!TryEntity(monster, out EntityManager em, out Entity e)) return;
        if (!em.HasComponent<T>(e)) em.AddComponent<T>(e);
    }

    static bool TryEntity(GameObject monster, out EntityManager em, out Entity e)
    {
        em = default;
        e  = Entity.Null;

        if (!monster.TryGetComponent<EntityLink>(out var link) || link.Entity == Entity.Null) return false;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return false;

        em = world.EntityManager;
        e  = link.Entity;
        return em.Exists(e);
    }

    // ── 몬스터 사망 ──────────────────────────────────────────

    /// <summary>
    /// 아군 몬스터가 죽었다. 부활 계열 개성이 여기에 붙는다.
    ///
    /// ⚠ 여기서 나온 몬스터는 대기열로 돌아가지 않는다 (사용자 확정 규칙)
    ///   공짜로 나온 개체가 줄에 쌓이면 판을 거듭할수록 마나를 내지 않은
    ///   물량이 불어나 소환 경제가 무너진다. 부활·분열로 생긴 개체는
    ///   MonsterSpawner.SpawnDerived 를 지나며 returnable 이 꺼진 채로 선다
    ///   (MonsterLineReturner 의 기본값이 '돌아가지 않는다' 다).
    /// </summary>
    public static void OnMonsterDied(SummonerData summoner, MonsterSpeciesData species,
                                     Vector3 at)
    {
        switch (summoner.Perk)
        {
            case SummonerPerk.RaiseOnDeath:
                RaiseFree(summoner, "skeleton", at);
                break;

            // ⚠ 역병은 여기서 돌지 않는다 — '죽을 때' 가 아니라 '쓰러뜨릴 때' 다 (2026-09-11)
            //   OnMonsterSpawned 가 태그를 붙이고 FlushPlague 가 세운다.

            // 시체 합성 — 시체가 PerkValue 구 쌓일 때마다 거인 한 기.
            //
            // ⚠ 거인은 새 종족이 아니다
            //   트롤을 그대로 쓴다. 전용 종족을 만들면 그림·스탯·도감 항목이
            //   전부 따라와야 하는데, "시체를 뭉쳐 만든 덩어리" 는 트롤의
            //   덩치와 느린 공격으로 충분히 읽힌다.
            case SummonerPerk.FleshGolem:
                _corpseCount++;
                int need = Mathf.Max(2, Mathf.RoundToInt(summoner.PerkValue * 3f));
                if (_corpseCount < need) break;

                _corpseCount = 0;
                RaiseFree(summoner, "troll", at);
                break;
        }
    }

    // ── 내부 ─────────────────────────────────────────────────

    /// <summary>
    /// 공짜 부활. 마나를 내지 않고, 따라서 환수도 하지 않는다.
    ///
    /// ⚠ 부활한 개체는 다시 부활을 일으키지 않는다
    ///   MonsterDeathWatcher 가 '소환으로 나온 개체' 만 사망 훅을 태운다.
    ///   그 구분이 없으면 스켈레톤 하나가 죽을 때마다 스켈레톤이 나와
    ///   전투가 끝나지 않는다.
    /// </summary>
    static void RaiseFree(SummonerData summoner, string speciesId, Vector3 at)
    {
        CardCatalog catalog = CardCatalog.Current;
        if (catalog == null) return;

        MonsterSpeciesData species = catalog.GetMonster(speciesId);
        if (species == null) return;

        MonsterSpawner.SpawnFree(species, summoner, at);
    }

    /// <summary>
    /// 몸집·스탯을 키운다. UnitRuntimeBridge.GrowEnrage 가 이미 하는 일이라
    /// 그대로 빌려 쓴다 — 크기 배율 규칙이 두 벌이 되지 않게.
    /// </summary>
    static void Swell(GameObject monster, MonsterSpeciesData species)
    {
        if (!monster.TryGetComponent<MonsterRuntimeBridge>(out var bridge)) return;

        float radius = bridge.GrowEnrage(0.06f, 8);

        if (!monster.TryGetComponent<EntityLink>(out var link)) return;
        if (link.Entity == Entity.Null) return;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        EntityManager em = world.EntityManager;
        if (!em.Exists(link.Entity)) return;
        if (!em.HasComponent<UnitSizeComponent>(link.Entity)) return;

        UnitSizeComponent size = em.GetComponentData<UnitSizeComponent>(link.Entity);
        size.Radius = radius;
        em.SetComponentData(link.Entity, size);
    }

    /// <summary>
    /// 몬스터에게 패시브 한 칸을 더 붙인다. 슬롯은 3칸뿐이고 꽉 차면 무시한다.
    /// 발동은 기존 PassiveSkillRuntimeSystem 이 그대로 한다 — 새 코드가 없다.
    /// </summary>
    static void AddPassive(GameObject monster, PassiveSkillType passive)
    {
        if (!monster.TryGetComponent<EntityLink>(out var link)) return;
        if (link.Entity == Entity.Null) return;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        EntityManager em = world.EntityManager;
        if (!em.Exists(link.Entity)) return;
        if (!em.HasComponent<GeneralPassiveSetComponent>(link.Entity)) return;

        var set = em.GetComponentData<GeneralPassiveSetComponent>(link.Entity);

        if (set.ActiveSlotCount >= 3) return;

        switch (set.ActiveSlotCount)
        {
            case 0: set.Slot0 = passive; break;
            case 1: set.Slot1 = passive; break;
            default: set.Slot2 = passive; break;
        }
        set.ActiveSlotCount++;

        em.SetComponentData(link.Entity, set);
    }
}
