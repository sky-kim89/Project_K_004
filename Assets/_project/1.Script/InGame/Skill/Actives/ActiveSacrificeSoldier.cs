using Unity.Entities;
using Unity.Collections;
using BattleGame.Units;

// ============================================================
//  ActiveSacrificeSoldier.cs — 병사 희생 (공통)
//
//  소속 병사 중 체력이 가장 낮은 병사 하나를 즉사시키고,
//  그 병사의 Attack × EffectValue 만큼 시전자 공격력을 강화한다.
//  버프 지속시간 = EffectDuration (초).
//
//  ⚠ EffectDuration 은 999 로 잡혀 있다 (사실상 스테이지 내내 유지).
//    버프는 매번 새 항목으로 Add 되므로 중첩 상한이 없다 — 병사를 계속
//    태워 장수를 무한히 키우는 것이 이 스킬의 설계 의도다.
//    "너무 세다" 싶으면 지속시간이 아니라 병사 소모 비용으로 조절할 것.
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Active_SacrificeSoldier", menuName = "BattleGame/Actives/SacrificeSoldier")]
public class ActiveSacrificeSoldier : ActiveSkillData
{
    public override void Execute(ActiveSkillContext ctx)
    {
        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        // ── 체력 가장 낮은 병사 탐색 ─────────────────────────────
        Entity sacrifice     = Entity.Null;
        float  lowestHp      = float.MaxValue;

        var query    = em.CreateEntityQuery(new EntityQueryDesc
        {
            All  = new ComponentType[] { ComponentType.ReadOnly<SoldierComponent>() },
            None = new ComponentType[] { typeof(DeadTag) },
        });
        NativeArray<Entity>           entities = query.ToEntityArray(Allocator.Temp);
        NativeArray<SoldierComponent> soldiers = query.ToComponentDataArray<SoldierComponent>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if (soldiers[i].GeneralEntity != ctx.CasterEntity) continue;
            if (!em.HasComponent<HealthComponent>(entities[i])) continue;

            float hp = em.GetComponentData<HealthComponent>(entities[i]).CurrentHp;
            if (hp < lowestHp)
            {
                lowestHp   = hp;
                sacrifice  = entities[i];
            }
        }

        entities.Dispose();
        soldiers.Dispose();
        query.Dispose();

        if (sacrifice == Entity.Null || !em.Exists(sacrifice)) return;

        // 희생 병사 위치에 이펙트
        if (em.HasComponent<Unity.Transforms.LocalTransform>(sacrifice))
        {
            var tf = em.GetComponentData<Unity.Transforms.LocalTransform>(sacrifice);
            SkillEffectHelper.Spawn(TargetEffectKey,
                new UnityEngine.Vector3(tf.Position.x, tf.Position.y, tf.Position.z),
                EffectDespawnDelay);
        }

        // 시전자 흡수 이펙트
        if (ctx.CasterTransform != null)
            SkillEffectHelper.Spawn(CasterEffectKey, ctx.CasterTransform.position, EffectDespawnDelay);

        // ── 병사 공격력 흡수 (시전자에게 버프) ─────────────────
        float soldierAtk = em.GetComponentData<StatComponent>(sacrifice).Final[StatType.Attack];
        float buffAmount = soldierAtk * EffectValue;

        if (em.HasBuffer<StatusEffectBufferElement>(ctx.CasterEntity))
        {
            float duration = EffectDuration > 0f ? EffectDuration : 10f;
            em.GetBuffer<StatusEffectBufferElement>(ctx.CasterEntity).Add(new StatusEffectBufferElement
            {
                Stat      = StatType.Attack,
                Delta     = buffAmount,
                Mode      = EffectMode.Add,
                Duration  = duration,
                Remaining = duration,
            });
        }

        // ── 병사 즉사 (치명적 피해 주입) ────────────────────────
        if (em.HasBuffer<HitEventBufferElement>(sacrifice))
        {
            em.GetBuffer<HitEventBufferElement>(sacrifice).Add(new HitEventBufferElement
            {
                Damage         = 999999f,
                HitDirection   = Unity.Mathematics.float3.zero,
                AttackerEntity = ctx.CasterEntity,
            });
        }
    }
}
