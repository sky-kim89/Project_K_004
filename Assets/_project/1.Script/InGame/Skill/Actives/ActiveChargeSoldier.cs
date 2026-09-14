using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveChargeSoldier.cs — 돌격 병사 소환 (방패)
//
//  장군 후방에 실제 병사 3명을 소환해 전방으로 돌격시킨다.
//  돌진 경로 위 반경(HitRadius) 안의 적에게 데미지 + 넉백.
//  돌진 완료 후 병사는 일반 전투 AI 로 전환해 계속 싸운다.
// ============================================================

[CreateAssetMenu(fileName = "Active_ChargeSoldier", menuName = "BattleGame/Actives/ChargeSoldier")]
public class ActiveChargeSoldier : ActiveSkillData
{
    [Header("돌격 병사 설정")]
    [Tooltip("소환할 병사 풀 키 (PoolController 에 등록된 키)")]
    public string SoldierPoolKey = "Soldier";

    [Tooltip("병사 스텟 비율 (장군 스텟 대비). 예: 0.6 → 60%")]
    [Range(0.1f, 1f)]
    public float StatRatio = 0.6f;

    [Tooltip("돌격 이동 속도 (유닛/초)")]
    public float ChargeSpeed = 18f;

    [Tooltip("피해 판정 반경 (유닛)")]
    public float HitRadius = 0.8f;

    [Tooltip("최대 돌진 거리 (유닛)")]
    public float ChargeDistance = 12f;

    [Tooltip("넉백 힘")]
    public float KnockbackForce = 5f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (string.IsNullOrEmpty(SoldierPoolKey)) return;
        if (PoolController.Instance == null) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        if (!ctx.CasterObject.TryGetComponent<GeneralRuntimeBridge>(out var generalBridge)) return;
        UnitStat generalStat = generalBridge.GetRolledStat();
        if (generalStat == null) return;

        if (!em.HasComponent<UnitJobComponent>(ctx.CasterEntity)) return;
        UnitJob generalJob = em.GetComponentData<UnitJobComponent>(ctx.CasterEntity).Job;

        Vector3 casterPos = ctx.CasterTransform != null ? ctx.CasterTransform.position : Vector3.zero;

        // 돌진 방향: 타겟 방향, 없으면 +X (기본 전방)
        Vector3 chargeDir;
        if (ctx.HasTarget && em.Exists(ctx.TargetEntity)
            && em.HasComponent<LocalTransform>(ctx.TargetEntity))
        {
            var tf  = em.GetComponentData<LocalTransform>(ctx.TargetEntity);
            var raw = new Vector3(tf.Position.x - casterPos.x, tf.Position.y - casterPos.y, 0f);
            chargeDir = raw.sqrMagnitude > 0.01f ? raw.normalized : Vector3.right;
        }
        else
        {
            chargeDir = Vector3.right;
        }

        Vector3 perp = new Vector3(-chargeDir.y, chargeDir.x, 0f);

        var identity  = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);
        float damage  = em.GetComponentData<StatComponent>(ctx.CasterEntity).Final[StatType.Attack] * EffectValue;

        SkillEffectHelper.Spawn(CasterEffectKey, casterPos, EffectDespawnDelay);

        var fx = new SkillEffectConfig
        {
            TargetEffectKey = TargetEffectKey,
            BaseEffectKey   = BaseEffectKey,
            DespawnDelay    = EffectDespawnDelay,
        };

        // 후방 기준점 + 좌·중·우 3명 배치
        Vector3   behindBase = casterPos - chargeDir * 1.5f;
        Vector3[] offsets    = { Vector3.zero, perp * 0.9f, -perp * 0.9f };

        for (int i = 0; i < 3; i++)
        {
            Vector3 spawnPos = behindBase + offsets[i];

            SkillEffectHelper.Spawn(BaseEffectKey, spawnPos, EffectDespawnDelay);

            GameObject go = PoolController.Instance.Spawn(
                PoolType.Unit, SoldierPoolKey, spawnPos, Quaternion.identity);

            if (go == null)
            {
                Debug.LogWarning($"[ActiveChargeSoldier] 풀 스폰 실패: '{SoldierPoolKey}'");
                continue;
            }

            Entity soldierEntity = Entity.Null;
            if (go.TryGetComponent<SoldierRuntimeBridge>(out var bridge))
            {
                // ⚠ 진영은 시전자에게서 온다 — 상수로 박지 말 것
                //   (SkillCrowdControl.TeamOf 주석: 적 수가 조용히 줄어 스테이지가
                //    조기 클리어된 원인이다)
                // ⚠ 순서 고정 — Initialize 안에서 Team 이 박힌다
                TeamType team = SkillCrowdControl.TeamOf(ctx.EntityManager, ctx.CasterEntity);
                bridge.SetTeam(team);

                BattleManager.Instance?.OnUnitSpawned(team);

                bridge.Initialize(SoldierPoolKey, generalStat, StatRatio,
                    ctx.CasterEntity, generalJob, generalBridge.UnitName, UnitGrade.Normal);

                if (go.TryGetComponent<EntityLink>(out var link))
                    soldierEntity = link.Entity;

                if (soldierEntity != Entity.Null)
                    em.AddComponent<SummonedTag>(soldierEntity);
            }

            // 돌격 제어 — 병사 GO 에 런너 컴포넌트를 부착해 돌진 후 일반 전투로 전환
            var runner = go.AddComponent<ChargeSoldierRunner>();
            runner.Launch(
                chargeDir:      chargeDir,
                chargeSpeed:    ChargeSpeed,
                hitRadius:      HitRadius,
                maxDistance:    ChargeDistance,
                damage:         damage,
                knockbackForce: KnockbackForce,
                casterTeam:     identity.Team,
                casterEntity:   ctx.CasterEntity,
                soldierEntity:  soldierEntity,
                em:             em,
                fx:             fx);
        }
    }
}
