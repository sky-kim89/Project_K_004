using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveSummonElite.cs — 정예 소환 (법사)
//
//  시전자 위치 근처에 강화된 병사 분대를 소환한다.
//  소환 수 = Mathf.FloorToInt(EffectValue) (최소 1).
//  정예 병사 능력치 = 시전자 스텟 × StatRatio.
//
//  ElitePoolKey: PoolController 에 등록된 풀 키 (Inspector 설정).
//  정예 프리팹에는 SoldierRuntimeBridge 가 붙어 있어야 한다.
// ============================================================

[CreateAssetMenu(fileName = "Active_SummonElite", menuName = "BattleGame/Actives/SummonElite")]
public class ActiveSummonElite : ActiveSkillData
{
    [Header("정예 소환 설정")]
    [Tooltip("PoolController 에 등록된 정예 병사 풀 키")]
    public string ElitePoolKey = "Soldier";

    [Tooltip("정예 병사 스텟 비율 (시전자 스텟 대비). 예: 0.7 → 70%")]
    [Range(0.1f, 1.5f)]
    public float StatRatio = 0.7f;

    [Tooltip("소환 스폰 오프셋 반경")]
    public float SpawnRadius = 1.5f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (string.IsNullOrEmpty(ElitePoolKey)) return;
        if (PoolController.Instance == null) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        Vector3 casterPos = ctx.CasterTransform != null
            ? ctx.CasterTransform.position
            : Vector3.zero;

        if (!ctx.CasterObject.TryGetComponent<GeneralRuntimeBridge>(out var generalBridge)) return;
        UnitStat generalStat = generalBridge.GetRolledStat();
        if (generalStat == null) return;

        if (!em.HasComponent<UnitJobComponent>(ctx.CasterEntity)) return;
        UnitJob generalJob = em.GetComponentData<UnitJobComponent>(ctx.CasterEntity).Job;

        int count = Mathf.Max(1, Mathf.FloorToInt(EffectValue));

        for (int i = 0; i < count; i++)
        {
            float angle      = (360f / count) * i * Mathf.Deg2Rad;
            Vector3 offset   = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * SpawnRadius;
            Vector3 spawnPos = casterPos + offset;

            GameObject go = PoolController.Instance.Spawn(
                PoolType.Unit, ElitePoolKey, spawnPos, Quaternion.identity);

            if (go == null)
            {
                Debug.LogWarning($"[ActiveSummonElite] 풀 스폰 실패: '{ElitePoolKey}'");
                continue;
            }

            // 소환 위치 이펙트
            SkillEffectHelper.Spawn(BaseEffectKey, spawnPos, EffectDespawnDelay);

            if (go.TryGetComponent<SoldierRuntimeBridge>(out var bridge))
            {
                // ⚠ 진영은 시전자에게서 온다 — 상수로 박지 말 것
                //   (SkillCrowdControl.TeamOf 주석: 적 수가 조용히 줄어 스테이지가
                //    조기 클리어된 원인이다)
                // ⚠ 순서 고정 — Initialize 안에서 Team 이 박힌다
                TeamType team = SkillCrowdControl.TeamOf(ctx.EntityManager, ctx.CasterEntity);
                bridge.SetTeam(team);

                BattleManager.Instance?.OnUnitSpawned(team);

                bridge.Initialize(ElitePoolKey, generalStat, StatRatio, ctx.CasterEntity,
                    generalJob, "Elite", UnitGrade.Rare);

                // 소환 유닛 마킹 — 딜을 SkillDamageDealt 로 귀속
                if (go.TryGetComponent<EntityLink>(out var link) && link.Entity != Entity.Null)
                    em.AddComponent<SummonedTag>(link.Entity);
            }
        }
    }
}
