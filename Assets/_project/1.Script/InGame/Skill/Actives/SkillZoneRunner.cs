using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  SkillZoneRunner.cs — 지속 효과 영역 실행기 (공용)
//
//  PoisonZone / Blizzard / ArrowRain 이 공유하는 MonoBehaviour.
//  설정된 중심 · 반경 · 지속 시간 동안 매 틱마다:
//    - 범위 내 적에게 직접 피해 (HitEventBufferElement)
//    - 선택적 디버프 1·2 (StatusEffectBufferElement, 틱마다 갱신)
//
//  디버프 갱신 방식:
//    기존에 같은 Stat+Mode+Delta 의 버프가 있으면 Remaining 을 연장한다.
//    없으면 새로 추가. → 영역을 벗어나면 자연히 만료.
//
//  OnDisable 에서 코루틴 정리 → 풀 재사용 시 안전.
// ============================================================

public class SkillZoneRunner : MonoBehaviour
{
    Coroutine _current;
    ZoneConfig _activeConfig;
    bool       _isRunning;

    void OnDisable()
    {
        _current   = null;
        _isRunning = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!_isRunning) return;

        Vector3 center = new Vector3(_activeConfig.Center.x, _activeConfig.Center.y, 0f);

        // 존 범위 (초록 원)
        UnityEditor.Handles.color = new Color(0.1f, 1f, 0.1f, 0.25f);
        UnityEditor.Handles.DrawSolidDisc(center, Vector3.forward, _activeConfig.Radius);
        UnityEditor.Handles.color = new Color(0.1f, 1f, 0.1f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(center, Vector3.forward, _activeConfig.Radius);

        // 존 중심점 (노란 점)
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(center, 0.12f);

        // 라벨
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(
            center + new Vector3(0f, _activeConfig.Radius + 0.2f, 0f),
            $"Zone r={_activeConfig.Radius:F1}  dmg={_activeConfig.DamagePerTick:F0}/tick");
    }
#endif

    // ── 구성 ─────────────────────────────────────────────────

    public struct ZoneConfig
    {
        public float3     Center;
        public float      Radius;
        public float      Duration;
        public float      TickInterval;  // 틱 간격 (초)
        public float      DamagePerTick; // 틱당 직접 피해 (0이면 피해 없음)
        public TeamType   CasterTeam;    // 적 팀 = 반대 팀
        public Entity     CasterEntity;

        /// <summary>어느 스킬이 깐 장판인가 — 디버프 중첩 판정의 열쇠다.</summary>
        public ActiveSkillId SourceSkill;

        // 디버프 1 (선택)
        public bool       HasDebuff1;
        public StatType   Debuff1Stat;
        public float      Debuff1Delta;
        public EffectMode Debuff1Mode;

        // 디버프 2 (선택)
        public bool       HasDebuff2;
        public StatType   Debuff2Stat;
        public float      Debuff2Delta;
        public EffectMode Debuff2Mode;

        // 이펙트 (선택) — BaseEffectKey 를 존 중심에 존 지속 시간만큼 유지
        public string     BaseEffectKey;
        public float      EffectDespawnDelay;
    }

    // ── 공개 API ─────────────────────────────────────────────

    public void Setup(ZoneConfig config)
    {
        if (_current != null) StopCoroutine(_current);
        _activeConfig = config;
        _isRunning    = true;
        _current      = StartCoroutine(Run(config));
    }

    // ── 내부 ─────────────────────────────────────────────────

    IEnumerator Run(ZoneConfig cfg)
    {
        float elapsed      = 0f;
        float tickInterval = cfg.TickInterval > 0f ? cfg.TickInterval : 0.5f;
        float tickTimer    = 0f;

        // ── 존 이펙트 시작 (존 지속 시간 + 여유 딜레이 후 반납) ──
        Vector3 zoneCenter = new Vector3(cfg.Center.x, cfg.Center.y, cfg.Center.z);
        GameObject zoneEffect = SkillEffectHelper.Spawn(
            cfg.BaseEffectKey,
            zoneCenter,
            cfg.Duration + cfg.EffectDespawnDelay);

        while (elapsed < cfg.Duration)
        {
            elapsed   += Time.deltaTime;
            tickTimer += Time.deltaTime;

            if (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                ApplyTick(cfg, tickInterval);
            }

            yield return null;
        }

        // 존 종료 시 이펙트 방출 즉시 중단 — 기존 파티클만 자연 페이드아웃
        // (데미지가 끝난 뒤에도 이펙트가 남으면 버그처럼 보이므로)
        if (zoneEffect != null)
        {
            foreach (var ps in zoneEffect.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        _current   = null;
        _isRunning = false;

        // 완료 후 컴포넌트 자체 제거 — 동시에 여러 Zone 이 독립 실행 가능하도록
        Destroy(this);
    }

    static void ApplyTick(ZoneConfig cfg, float tickInterval)
    {
        var em = Unity.Entities.World.DefaultGameObjectInjectionWorld?.EntityManager;
        if (em == null) return;

        em.Value.CompleteAllTrackedJobs();

        var query = em.Value.CreateEntityQuery(new EntityQueryDesc
        {
            All  = new ComponentType[] { ComponentType.ReadOnly<UnitIdentityComponent>(),
                                         ComponentType.ReadOnly<LocalTransform>() },
            None = new ComponentType[] { typeof(DeadTag) },
        });

        NativeArray<Entity>         entities   = query.ToEntityArray(Allocator.Temp);
        NativeArray<LocalTransform> transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        float refreshTime = tickInterval * 2f;  // 다음 틱까지 여유 있게 유지
        int   hitCount    = 0;

        for (int i = 0; i < entities.Length; i++)
        {
            var id = em.Value.GetComponentData<UnitIdentityComponent>(entities[i]);
            if (id.Team == cfg.CasterTeam) continue;

            float dist = math.distance(
                new float3(transforms[i].Position.x, transforms[i].Position.y, 0f),
                new float3(cfg.Center.x, cfg.Center.y, 0f));
            if (dist > cfg.Radius) continue;

            // 직접 피해
            bool hasHitBuf = em.Value.HasBuffer<HitEventBufferElement>(entities[i]);
            if (cfg.DamagePerTick > 0f && hasHitBuf)
            {
                em.Value.GetBuffer<HitEventBufferElement>(entities[i]).Add(new HitEventBufferElement
                {
                    Damage         = cfg.DamagePerTick,
                    HitDirection   = float3.zero,
                    AttackerEntity = cfg.CasterEntity,
                    Type = BattleGame.Units.HitType.Skill,
                });
                hitCount++;
            }
#if UNITY_EDITOR
            else if (cfg.DamagePerTick > 0f && !hasHitBuf)
                Debug.LogWarning($"[SkillZone] Entity {entities[i].Index} 범위 내 있지만 HitEventBufferElement 없음");
#endif

            // 디버프 적용 (버퍼에 이미 있으면 갱신, 없으면 추가)
            if (em.Value.HasBuffer<StatusEffectBufferElement>(entities[i]))
            {
                var buff = em.Value.GetBuffer<StatusEffectBufferElement>(entities[i]);

                if (cfg.HasDebuff1)
                    RefreshOrAddDebuff(buff, cfg.Debuff1Stat, cfg.Debuff1Delta, cfg.Debuff1Mode,
                                       refreshTime, cfg.SourceSkill);
                if (cfg.HasDebuff2)
                    RefreshOrAddDebuff(buff, cfg.Debuff2Stat, cfg.Debuff2Delta, cfg.Debuff2Mode,
                                       refreshTime, cfg.SourceSkill);
            }
        }

#if UNITY_EDITOR
        Debug.Log($"[SkillZone] ApplyTick — 검색된 유닛: {entities.Length}  범위 내 피격: {hitCount}  center=({cfg.Center.x:F1},{cfg.Center.y:F1})  r={cfg.Radius:F1}  dmg={cfg.DamagePerTick:F0}");
#endif

        entities.Dispose();
        transforms.Dispose();
        query.Dispose();
    }

    /// <summary>
    /// 존 디버프를 건다 — **같은 스킬이면 갱신, 다른 스킬이면 따로 쌓인다.**
    ///
    /// ⚠ 자기 자신과는 절대 겹치면 안 된다
    ///   장판은 0.5초마다 틱을 돈다. 틱마다 새 줄이 생기면 세 틱 만에
    ///   이동속도가 ×0.25³ = 0.015 가 되어 **가만히 서서 맞기만 하는 유닛**이 된다.
    ///   장판 안에 오래 있을수록 느려지는 것이 아니라, 들어온 순간의 세기가 유지돼야 한다.
    ///
    /// ⚠ 다른 스킬과는 겹쳐야 한다
    ///   블리자드 위에 독성 지대를 겹쳐 까는 것은 플레이어의 선택이고 값을 치른 결과다.
    ///   그래서 '스탯' 이 아니라 (스탯 · 모드 · **출처 스킬**) 로 자리를 잡는다.
    ///
    /// ⚠ 자리를 스킬로 잡으므로 시전자가 둘이어도 하나다
    ///   법사 둘이 같은 블리자드를 깔아도 한 자리를 나눠 쓴다 — 겹쳐 깔아 얼리는
    ///   전술이 성립하지 않게 하려는 의도다 (같은 마법을 두 번 쓴 것뿐이다).
    /// </summary>
    static void RefreshOrAddDebuff(
        DynamicBuffer<StatusEffectBufferElement> buff,
        StatType stat, float delta, EffectMode mode, float refreshTime,
        ActiveSkillId sourceSkill)
    {
        int sourceId = (int)sourceSkill;

        for (int j = 0; j < buff.Length; j++)
        {
            var b = buff[j];
            if (b.Stat != stat || b.Mode != mode) continue;
            if (b.SourceType != BuffSourceType.ActiveSkill || b.SourceId != sourceId) continue;

            // 같은 스킬의 다음 틱 — 새로 쌓지 않고 시간만 되살린다
            b.Delta     = delta;
            b.Remaining = math.max(b.Remaining, refreshTime);
            b.Duration  = math.max(b.Duration,  refreshTime);
            buff[j]     = b;
            return;
        }

        buff.Add(new StatusEffectBufferElement
        {
            Stat       = stat,
            Delta      = delta,
            Mode       = mode,
            Duration   = refreshTime,
            Remaining  = refreshTime,
            SourceType = BuffSourceType.ActiveSkill,
            SourceId   = sourceId,
        });
    }
}
