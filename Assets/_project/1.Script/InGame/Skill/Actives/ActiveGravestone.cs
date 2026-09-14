using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveGravestone.cs — 비석 강림 (공통 · 희귀)
//
//  하늘에서 비석이 떨어져 꽂히고, 그 자리에서 망자가 일어난다.
//    ① 낙하 예고 (짧게)
//    ② 착탄 — 반경 내 적에게 피해 + 넉백
//    ③ 비석마다 스켈레톤 1기 소환 (시전자 스탯 비례)
//
//  ⚠ "죽은 병사 부활" 이 아니다
//    사망 위치 버퍼를 읽어 되살리는 안(망자 소집)은 죽은 병사가 없으면
//    아무 일도 안 일어나서 스킬이 조용히 헛돈다. 비석은 언제 써도 그림이 나온다.
//
//  EffectRadius   : 비석 1개의 착탄 반경
//  EffectValue    : 비석 개수
//  EffectDuration : 예고 → 착탄까지의 시간
// ============================================================

[CreateAssetMenu(fileName = "Active_Gravestone", menuName = "BattleGame/Actives/Gravestone")]
public class ActiveGravestone : ActiveSkillData
{
    [Header("비석 강림 설정")]
    [Tooltip("착탄 공격력 배율")]
    public float DamageMultiplier = 3.5f;

    [Tooltip("비석이 떨어지는 범위 반경 (개별 착탄 반경과는 별개)")]
    public float ScatterRadius = 4f;

    [Tooltip("소환할 스켈레톤의 풀 키")]
    public string SkeletonPoolKey = "Soldier";

    [Tooltip("스켈레톤 스텟 비율 (시전자 대비)")]
    [Range(0.1f, 1f)]
    public float StatRatio = 0.45f;

    [Tooltip("넉백 배율")]
    public float KnockbackMult = 5f;

    [Tooltip("비석 사이의 낙하 간격 (초). 순서대로 우수수 떨어진다.")]
    public float DropInterval = 0.12f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (ctx.CasterObject == null) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        Vector3 center = ctx.HasTarget
            ? ctx.TargetPosition
            : ctx.CasterTransform.position + Vector3.right * 4f;

        // ⚠ 시전자를 용사(General)로 한정하지 않는다 (2026-09-04)
        //   소환사도 이 스킬을 쓴다(흑마법사). GeneralRuntimeBridge 로 받으면
        //   소환사에게는 그 컴포넌트가 없어 **여기서 조용히 return** 했다 —
        //   "비석 강림이 발동하지 않는다" 의 원인이었다.
        //   스탯은 모든 유닛이 갖는 것이라 베이스 브리지에서 받는다.
        if (!ctx.CasterObject.TryGetComponent<UnitRuntimeBridge>(out var casterBridge)) return;

        UnitStat casterStat = casterBridge.RolledStat;
        if (casterStat == null) return;

        // 직업은 소환할 스켈레톤의 외형·성향에만 쓰인다. 소환사는 직업이
        // 없으므로 근접 기본값으로 낸다 — 없다고 스킬을 통째로 버리지 않는다.
        UnitJob job = em.HasComponent<UnitJobComponent>(ctx.CasterEntity)
            ? em.GetComponentData<UnitJobComponent>(ctx.CasterEntity).Job
            : UnitJob.Knight;

        var identity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);

        // 소환사가 시전자면 그 소환사의 시그니처 종족을 부른다.
        // 용사가 굴린 경우에는 null 이라 예전 길(SkeletonSpawner)로 떨어진다.
        SummonerRuntimeBridge summonerBridge =
            ctx.CasterObject.GetComponent<SummonerRuntimeBridge>();

        SummonerData       summoner       = summonerBridge != null ? summonerBridge.Data : null;
        MonsterSpeciesData monsterSpecies = summoner != null ? summoner.SignatureSummonSpecies : null;

        var runner = ctx.CasterObject.GetComponent<GravestoneRunner>();
        if (runner == null) runner = ctx.CasterObject.AddComponent<GravestoneRunner>();

        runner.Run(
            em            : em,
            casterEntity  : ctx.CasterEntity,
            casterTeam    : identity.Team,
            center        : new float3(center.x, center.y, 0f),
            stoneCount    : Mathf.Max(1, Mathf.RoundToInt(EffectValue)),
            scatterRadius : ScatterRadius,
            hitRadius     : EffectRadius   > 0f ? EffectRadius   : 2f,
            warningTime   : EffectDuration > 0f ? EffectDuration : 0.5f,
            dropInterval  : DropInterval,
            damage        : ctx.CasterStat.Final[StatType.Attack] * DamageMultiplier,
            knockMult     : KnockbackMult,
            generalStat   : casterStat,
            generalJob    : job,
            statRatio     : StatRatio,
            poolKey       : SkeletonPoolKey,
            fx            : new SkillEffectConfig
            {
                CasterEffectKey = CasterEffectKey,
                TargetEffectKey = TargetEffectKey,
                BaseEffectKey   = BaseEffectKey,
                DespawnDelay    = EffectDespawnDelay,
            },
            monsterSpecies : monsterSpecies,
            summoner       : summoner);
    }
}
