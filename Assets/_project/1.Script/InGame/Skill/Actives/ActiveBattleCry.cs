using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using BattleGame.Units;

// ============================================================
//  ActiveBattleCry.cs — 전투 함성 (전사·방패)
//
//  시전자와 EffectRadius 내 모든 아군 (장군 + 병사) 의 공격력을
//  EffectDuration 초 동안 EffectValue 배율로 증가시킨다.
//  (예: EffectValue = 1.3 → 공격력 1.3배)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Active_BattleCry", menuName = "BattleGame/Actives/BattleCry")]
public class ActiveBattleCry : ActiveSkillData
{
    public override void Execute(ActiveSkillContext ctx)
    {
        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        float duration = EffectDuration > 0f ? EffectDuration : 8f;
        float radius   = EffectRadius   > 0f ? EffectRadius   : 5f;

        // 시전자 팀 확인
        var casterIdentity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);
        var casterTf       = em.GetComponentData<LocalTransform>(ctx.CasterEntity);
        float3 center      = new float3(casterTf.Position.x, casterTf.Position.y, 0f);

        // 걸리는 범위를 바닥에 그린다 — 어디까지가 '주변' 인지 보여야 부대를 세울 수 있다.
        // 색은 올려 주는 스탯(공격력) 색 — 범위 안 병사의 발밑 기둥과 같은 색으로 뜬다.
        ShowRange(ctx, radius, BuffStatPalette.Get(StatType.Attack));

        // 사용자 이펙트 (함성 연출)
        SkillEffectHelper.Spawn(CasterEffectKey,
            new UnityEngine.Vector3(casterTf.Position.x, casterTf.Position.y, casterTf.Position.z),
            EffectDespawnDelay);

        // 범위 내 아군 전체 강화
        var query = em.CreateEntityQuery(new EntityQueryDesc
        {
            All  = new ComponentType[] { ComponentType.ReadOnly<UnitIdentityComponent>(),
                                         ComponentType.ReadOnly<LocalTransform>() },
            None = new ComponentType[] { typeof(DeadTag) },
        });

        NativeArray<Entity>         entities   = query.ToEntityArray(Allocator.Temp);
        NativeArray<LocalTransform> transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            var id = em.GetComponentData<UnitIdentityComponent>(entities[i]);
            if (id.Team != casterIdentity.Team) continue;

            float dist = math.distance(center, new float3(transforms[i].Position.x, transforms[i].Position.y, 0f));
            if (dist > radius) continue;

            if (!em.HasBuffer<StatusEffectBufferElement>(entities[i])) continue;

            em.GetBuffer<StatusEffectBufferElement>(entities[i]).Add(new StatusEffectBufferElement
            {
                Stat       = StatType.Attack,
                Delta      = EffectValue,
                Mode       = EffectMode.Multiply,
                Duration   = duration,
                Remaining  = duration,
                SourceType = BuffSourceType.ActiveSkill,
                SourceId   = (int)SkillId,
            });
        }

        entities.Dispose();
        transforms.Dispose();
        query.Dispose();
    }
}
