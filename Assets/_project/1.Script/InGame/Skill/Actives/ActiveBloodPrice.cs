using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveBloodPrice.cs — 피의 대가 (공통 · 희귀)
//
//  자기 체력을 태워 전방을 쓸어버린다.
//    ① 시전자 현재 체력의 일정 비율을 즉시 잃는다 (죽지는 않는다)
//    ② 잃은 체력 × 배율 만큼을 전방 부채꼴 광역 피해로 전환
//    ③ 강한 넉백
//
//  체력이 많을수록 세진다 — "지금 지르느냐" 판단이 생기는 위험 감수형.
//
//  ⚠ 대가를 치르는 스킬은 눈에 크게 보여야 한다
//    체력을 40% 태우고도 화면에서 한 번 번쩍이고 마는 게 예전 문제였다.
//    연출(응축 → 파도 → 거리순 착탄 + 화면 흔들림)은 BloodPriceRunner 가 돌린다.
//    여기서는 대가 정산과 피해량 계산까지만 한다.
//
//  EffectRadius : 부채꼴 사거리
// ============================================================

[CreateAssetMenu(fileName = "Active_BloodPrice", menuName = "BattleGame/Actives/BloodPrice")]
public class ActiveBloodPrice : ActiveSkillData
{
    [Header("피의 대가 설정")]
    [Tooltip("소모할 현재 체력 비율 (0.4 = 40%)")]
    [Range(0.05f, 0.9f)]
    public float HpCostRatio = 0.4f;

    [Tooltip("잃은 체력 1당 피해 배율 (2.5 = 잃은 체력의 250%)")]
    public float DamagePerHp = 2.5f;

    [Tooltip("공격력도 함께 더한다 — 체력이 낮을 때 완전히 무의미해지지 않게")]
    public float AttackMultiplier = 2f;

    [Tooltip("부채꼴 각도 (도)")]
    [Range(30f, 360f)]
    public float ConeAngleDegrees = 160f;

    [Tooltip("넉백 배율")]
    public float KnockbackMult = 7f;

    [Tooltip("응축 시간 (초) — 이 동안 시전자는 발이 묶인다")]
    public float ChargeTime = 0.3f;

    public override void Execute(ActiveSkillContext ctx)
    {
        if (ctx.CasterObject == null) return;

        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        if (!em.HasComponent<HealthComponent>(ctx.CasterEntity)) return;
        if (!em.HasComponent<LocalTransform>(ctx.CasterEntity)) return;

        var    casterTf  = em.GetComponentData<LocalTransform>(ctx.CasterEntity);
        float3 casterPos = new float3(casterTf.Position.x, casterTf.Position.y, 0f);

        // ── 대가: 현재 체력의 일부를 태운다 ──────────────────
        // ⚠ 피해 파이프라인을 타지 않는다
        //   히트 이벤트로 넣으면 방어율이 적용되고 "누가 때렸나" 가 남아
        //   반사·흡혈 같은 처리까지 끌려온다. 대가는 대가일 뿐이다.
        var   health = em.GetComponentData<HealthComponent>(ctx.CasterEntity);
        float cost   = health.CurrentHp * HpCostRatio;

        // 자해로 죽지는 않는다 — 체력 1 은 남긴다
        cost = math.min(cost, health.CurrentHp - 1f);
        if (cost <= 0f) return;

        health.CurrentHp -= cost;
        em.SetComponentData(ctx.CasterEntity, health);

        // ── 전방 방향 ────────────────────────────────────────
        float3 forward = new float3(1f, 0f, 0f);
        if (ctx.HasTarget && em.Exists(ctx.TargetEntity) && em.HasComponent<LocalTransform>(ctx.TargetEntity))
        {
            var    t = em.GetComponentData<LocalTransform>(ctx.TargetEntity);
            float3 d = new float3(t.Position.x, t.Position.y, 0f) - casterPos;
            if (math.lengthsq(d) > 0.001f) forward = math.normalize(d);
        }

        float damage = cost * DamagePerHp
                     + ctx.CasterStat.Final[StatType.Attack] * AttackMultiplier * EffectValue;

        var identity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);

        var runner = ctx.CasterObject.GetComponent<BloodPriceRunner>();
        if (runner == null) runner = ctx.CasterObject.AddComponent<BloodPriceRunner>();

        runner.Run(
            em          : em,
            casterEntity: ctx.CasterEntity,
            casterTeam  : identity.Team,
            origin      : casterPos,
            forward     : forward,
            range       : EffectRadius > 0f ? EffectRadius : 9f,
            halfAngleDeg: ConeAngleDegrees * 0.5f,
            damage      : damage,
            knockMult   : KnockbackMult,
            chargeTime  : ChargeTime,
            fx          : new SkillEffectConfig
            {
                CasterEffectKey = CasterEffectKey,
                TargetEffectKey = TargetEffectKey,
                BaseEffectKey   = BaseEffectKey,
                DespawnDelay    = EffectDespawnDelay,
            });
    }
}
