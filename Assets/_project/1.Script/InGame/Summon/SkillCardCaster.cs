using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  SkillCardCaster.cs
//  스킬 카드를 실제로 터뜨린다.
//
//  ■ 새 실행 코드를 쓰지 않는다
//    이미 33종의 액티브 스킬이 ActiveSkillData.Execute(context) 로 돈다.
//    스킬 카드는 그 실행기를 **소환사를 시전자로 삼아** 그대로 부른다.
//    덕분에 스킬 카드를 늘리는 데 필요한 것은 SO 한 장뿐이다.
//
//  ■ 조준은 손이 한다
//    context.TargetPosition 에 플레이어가 탭한 좌표가 들어간다.
//    메테오·독성 지대처럼 "위치에 터지는" 스킬이 카드에 가장 잘 맞는다.
//    타겟 엔티티가 필요한 스킬(집중 치유 등)은 그 자리에서 가장 가까운 적을 잡아 준다.
//
//  ■ 카드 레벨은 위력 배율로 들어간다
//    ⚠ SO 를 직접 수정해 위력을 올리면 안 된다 — SO 는 에셋이라 그 변경이
//      런이 끝나도 남고, 같은 스킬을 쓰는 용사에게까지 번진다.
//      그래서 CasterStat 의 공격력을 배율만큼 부풀린 **사본**을 넘긴다.
//      스킬들은 EffectValue 를 시전자 공격력에 곱해 쓰므로 결과가 같다.
// ============================================================

public static class SkillCardCaster
{
    /// <summary>
    /// 스킬 카드를 그 좌표에 시전한다.
    /// 마나 차감은 부르는 쪽(SummonController)이 이미 했다.
    /// </summary>
    public static void Cast(SkillCardData card, int cardLevel, Vector3 worldPos)
    {
        if (card.Skill == ActiveSkillId.None) return;

        ActiveSkillData data = ActiveSkillDatabase.Current.Get(card.Skill);
        if (data == null)
        {
            Debug.LogError($"[SkillCardCaster] ActiveSkillDatabase 에 없는 스킬: {card.Skill} " +
                           $"(카드 '{card.Id}'). Resources/ActiveSkillDatabase 에 등록하세요.");
            return;
        }

        var summoner = SummonerRuntimeBridge.Current;
        if (summoner == null)
        {
            Debug.LogError("[SkillCardCaster] 소환사가 없습니다 — 시전자가 없어 스킬을 쓸 수 없습니다.");
            return;
        }

        if (!summoner.TryGetComponent<EntityLink>(out var link) || link.Entity == Entity.Null)
        {
            Debug.LogError("[SkillCardCaster] 소환사 엔티티가 아직 없습니다.");
            return;
        }

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        EntityManager em = world.EntityManager;
        em.CompleteAllTrackedJobs();

        if (!em.Exists(link.Entity)) return;

        var context = new ActiveSkillContext
        {
            CasterEntity    = link.Entity,
            TargetEntity    = FindNearestHero(em, worldPos),
            TargetPosition  = worldPos,
            CasterStat      = ScaledStat(em, link.Entity, card.PowerMultiplier(cardLevel)),
            EntityManager   = em,
            CasterObject    = summoner.gameObject,
            CasterTransform = summoner.transform,
        };

        // ⚠ Execute 보다 **먼저** 창을 연다
        //   즉발 스킬은 Execute 안에서 그 자리에 피해를 넣는다. 뒤에 열면
        //   그 피해가 창 밖으로 새어 "누구 것도 아닌 피해" 가 된다.
        //   창 길이는 장판이 다 때릴 때까지 — 지속시간 + 여유 1초.
        CardStatsTracker.Instance.BeginCast(card.Id, data.EffectDuration + 1f);

        data.Execute(context);
    }

    // ── 내부 ─────────────────────────────────────────────────

    /// <summary>
    /// 시전자 스탯의 사본에 카드 레벨 배율을 얹는다.
    /// 원본 엔티티의 스탯은 건드리지 않는다 — 소환사 본인의 평타가 같이 세지면 안 된다.
    /// </summary>
    static StatComponent ScaledStat(EntityManager em, Entity caster, float multiplier)
    {
        var stat = em.GetComponentData<StatComponent>(caster);

        stat.Final[StatType.Attack] *= multiplier;
        return stat;
    }

    /// <summary>
    /// 탭한 자리에서 가장 가까운 용사. 없으면 Entity.Null.
    ///
    /// ⚠ 타겟이 없어도 시전은 진행한다
    ///   범위 스킬은 타겟 없이 좌표만으로 성립한다. 여기서 막으면
    ///   "빈 땅에 미리 깔아 두는" 운용이 아예 불가능해진다.
    /// </summary>
    static Entity FindNearestHero(EntityManager em, Vector3 at)
    {
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<UnitIdentityComponent>(),
            ComponentType.ReadOnly<Unity.Transforms.LocalTransform>(),
            ComponentType.ReadOnly<HealthComponent>());

        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        Entity best     = Entity.Null;
        float  bestDist = float.MaxValue;

        foreach (Entity e in entities)
        {
            if (em.GetComponentData<UnitIdentityComponent>(e).Team != Faction.Hero) continue;
            if (em.GetComponentData<HealthComponent>(e).CurrentHp <= 0f)             continue;

            Vector3 pos = (Vector3)em.GetComponentData<Unity.Transforms.LocalTransform>(e).Position;
            float   d   = (pos - at).sqrMagnitude;

            if (d >= bestDist) continue;

            bestDist = d;
            best     = e;
        }

        return best;
    }
}
