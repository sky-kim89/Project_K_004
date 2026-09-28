using Unity.Mathematics;
using Unity.Transforms;
using BattleGame.Units;

// ============================================================
//  ActiveSkillZoneBase.cs
//  지속 효과 영역(Zone) 스킬의 공통 Execute 로직 추상 베이스.
//
//  ■ 상속 스킬: ActivePoisonZone / ActiveBlizzard / ActiveArrowRain
//
//  ■ 서브클래스 작성 규칙
//    - DefaultRadius   : 기본 반경 (EffectRadius 가 0 일 때 사용)
//    - DefaultDuration : 기본 지속 시간 (EffectDuration 이 0 일 때 사용)
//    - ConfigureDebuffs(ref ZoneConfig) : 디버프 설정 오버라이드 (선택)
// ============================================================

public abstract class ActiveSkillZoneBase : ActiveSkillData
{
    [UnityEngine.Header("존 공통 설정")]
    [UnityEngine.Tooltip("틱 간격 (초)")]
    public float TickInterval = 0.5f;

    protected abstract float DefaultRadius   { get; }
    protected abstract float DefaultDuration { get; }

    /// <summary>디버프 설정. 기본값은 디버프 없음.</summary>
    protected virtual void ConfigureDebuffs(ref SkillZoneRunner.ZoneConfig config) { }

    /// <summary>
    /// 장판이 시전자를 따라다니는가. 기본은 <b>고정</b>이다.
    ///
    /// ⚠ 따라다니면 '어디에 까느냐' 라는 선택이 사라진다 — 그게 이 축의 값이므로
    ///   기본값은 고정이어야 한다. 몸에 두르는 불(화염 오라)만 예외다.
    /// </summary>
    protected virtual bool FollowsCaster => false;

    /// <summary>
    /// 소환사가 시그니처로 깔 때의 틱당 대상 최대 체력 비율 (패기 곱하기 전). 0 이면 비율 피해가 없다.
    /// ⚠ 소환사 시그니처로 쓰는 장판만 채운다 — 용사·몬스터가 깔면 무시된다 (SignatureDamageRule.RatioFor).
    /// </summary>
    protected virtual float SignatureTickRatio => 0f;

    public override void Execute(ActiveSkillContext ctx)
    {
        var em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        // ⚠ 타겟 **엔티티**가 아니라 **위치 스냅샷**을 기준으로 깐다 (2026-09-04)
        //   두 가지가 고쳐진다.
        //     · 타겟이 없으면 통째로 return 하던 것 — 소환사가 준비 시간에
        //       미리 깔아 두려 해도 아무 일이 없고 사용 횟수만 사라졌다
        //     · 스냅샷 이후 타겟이 죽으면 center 가 float3.zero(월드 원점)로
        //       떨어지던 것 — 지대가 화면 밖에 생겼다
        //   TargetPosition 은 발동을 확정한 시점에 이미 굳혀 둔 값이라
        //   (ActiveSkillCooldownSystem·SummonerSkillRule) 둘 다 안전하다.
        float3 center = ctx.TargetPosition;

        // 타겟이 아직 살아 있으면 그 자리가 더 정확하다 — 한 프레임 사이에 움직인다.
        if (ctx.HasTarget && em.HasComponent<LocalTransform>(ctx.TargetEntity))
            center = em.GetComponentData<LocalTransform>(ctx.TargetEntity).Position;

        var identity = em.GetComponentData<UnitIdentityComponent>(ctx.CasterEntity);
        var runner   = ctx.CasterObject.AddComponent<SkillZoneRunner>();

        // 따라다니는 장판은 시전자 발밑에서 시작한다 — 타겟 자리에 깔면
        // 첫 프레임에 제 몸으로 순간이동한 것처럼 보인다.
        if (FollowsCaster)
        {
            UnityEngine.Vector3 here = ctx.CasterTransform.position;
            center = new float3(here.x, here.y, 0f);
        }

        var config = new SkillZoneRunner.ZoneConfig
        {
            Center             = center,
            FollowCaster       = FollowsCaster,
            Radius             = EffectRadius   > 0f ? EffectRadius   : DefaultRadius,
            Duration           = EffectDuration > 0f ? EffectDuration : DefaultDuration,
            TickInterval       = TickInterval,
            DamagePerTick      = ctx.CasterStat.Final[StatType.Attack] * EffectValue,
            MaxHpRatioPerTick  = SignatureDamageRule.RatioFor(em, ctx.CasterEntity, SignatureTickRatio),
            CasterTeam         = identity.Team,
            CasterEntity       = ctx.CasterEntity,
            SourceSkill        = SkillId,       // 디버프 자리를 잡는 열쇠
            BaseEffectKey      = BaseEffectKey,
            EffectDespawnDelay = EffectDespawnDelay,
        };

        ConfigureDebuffs(ref config);
        runner.Setup(config);

#if UNITY_EDITOR
        ActiveSkillDebugOverlay.RegisterZone(
            new UnityEngine.Vector3(center.x, center.y, 0f),
            config.Radius,
            $"{SkillName} r={config.Radius:F1}");
#endif
    }
}
