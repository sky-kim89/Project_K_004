using System.Collections;
using UnityEngine;

// ============================================================
//  SkillEffectHelper.cs
//  액티브 스킬 이펙트 풀 스폰 유틸리티.
//
//  ■ SkillEffectHelper (static)
//    - Spawn(key, position, ...)   : PoolType.Effect 풀에서 이펙트를 꺼내 위치에 배치
//    - Spawn(key, from, to, ...)   : LineRenderer 포함 이펙트를 from→to 사이에 배치
//    - 스폰된 GO 에 EffectAutoReturn 을 붙여 지정 시간 후 자동 반납
//
//  ■ EffectAutoReturn (MonoBehaviour)
//    - 스폰된 이펙트 GO 에 동적으로 부착
//    - delay 초 후 PoolController.Despawn() 으로 풀에 반납
//    - OnDisable 에서 코루틴 중단 → 풀 재사용 시 안전
//
//  ■ SkillEffectConfig (struct)
//    - Runner 에 이펙트 설정을 한 번에 넘기기 위한 편의 구조체
//    - TargetEffectKey  : 피격 대상 이펙트
//    - BaseEffectKey    : 기본/범위 이펙트
//    - DespawnDelay     : 자동 반납 딜레이 (초)
// ============================================================

public static class SkillEffectHelper
{
    /// <summary>
    /// 범위 표시 전용 바닥 원.
    ///
    /// ⚠ 한때 소환진(FX_Summon_Circle)을 빌려 썼다 — 잘못된 선택이었다
    ///   소환진은 "여기서 스켈레톤이 나온다" 는 뜻을 이미 갖고 있다.
    ///   전투 함성 범위에 같은 원이 뜨면 플레이어는 소환을 기다린다.
    ///   이펙트는 곧 문법이라, 뜻이 있는 기호를 다른 뜻으로 재사용하면 안 된다.
    ///
    /// 흰색으로 만들어 두고 꺼낼 때 버프 색을 입힌다(EffectTint) —
    /// 색깔 수만큼 프리팹을 만들면 풀도 머티리얼도 그만큼 갈린다.
    /// </summary>
    public const string RangeRingKey = "FX_Buff_Range";

    /// <summary>
    /// PoolType.Effect 에서 key 에 해당하는 이펙트를 스폰한다.
    /// rotation=default 이면 Quaternion.identity 사용.
    /// scale=1 이면 원본 크기.
    /// </summary>
    public static GameObject Spawn(string key, Vector3 position, float despawnDelay,
        Quaternion rotation = default, float scale = 1f)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (PoolController.Instance == null) return null;

        Quaternion rot = rotation.Equals(default) ? Quaternion.identity : rotation;
        var go = PoolController.Instance.Spawn(PoolType.Effect, key, position, rot);
        if (go == null) return null;

        go.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

        if (!go.TryGetComponent<EffectAutoReturn>(out var ret))
            ret = go.AddComponent<EffectAutoReturn>();
        ret.StartReturn(despawnDelay);

        return go;
    }

    /// <summary>
    /// 하늘에서 떨어지는 이펙트 — 시작 지점에서 착탄 지점까지 낙하시킨다.
    ///
    /// ⚠ 착탄 이펙트와 낙하체는 다른 물건이다
    ///   예전 메테오는 "예고 마커" 와 "폭발" 만 있어서, 하늘에서 뭔가 떨어졌다는
    ///   느낌이 전혀 없었다. 떨어지는 덩어리가 눈에 보여야 착탄이 사건이 된다.
    ///
    /// ⚠ 낙하 시간은 시전 지연보다 짧아야 한다
    ///   폭발은 delay 초 뒤에 터진다. 낙하가 그보다 길면 아직 공중에 있는데
    ///   땅에서 먼저 터진다. 부르는 쪽이 delay 를 그대로 넘기게 두지 말 것.
    /// </summary>
    public static GameObject SpawnFalling(string key, Vector3 impactPos, float fallTime,
        float height = 12f, float scale = 1f, float spinSpeed = 220f)
    {
        if (fallTime <= 0f) return null;

        Vector3 start = impactPos + new Vector3(height * 0.35f, height, 0f);
        var go = Spawn(key, start, fallTime, scale: scale);
        if (go == null) return null;

        if (!go.TryGetComponent<EffectFallMotion>(out var fall))
            fall = go.AddComponent<EffectFallMotion>();
        fall.Begin(start, impactPos, fallTime, spinSpeed);

        return go;
    }

    /// <summary>
    /// 효과 범위를 바닥에 그려 준다 — 버프·오라처럼 "어디까지 걸리는가" 가
    /// 보이지 않으면 플레이어가 배치를 판단할 수 없다.
    ///
    /// ⚠ 프리팹 반경을 1 로 보고 지름으로 키운다
    ///   이펙트 프리팹은 대부분 반경 1 짜리 원이라 지름(=반경×2)이 곧 배율이다.
    ///   프리팹이 다르면 baseRadius 로 보정한다.
    /// </summary>
    public static GameObject SpawnRange(string key, Vector3 center, float radius,
        float duration, float baseRadius = 1f)
    {
        if (radius <= 0f || baseRadius <= 0f) return null;
        return Spawn(key, center, duration, scale: radius / baseRadius);
    }

    /// <summary>
    /// 범위를 그리되 버프 색을 입힌다 — "무엇이 오르는 범위인가" 까지 한 번에 답한다.
    ///
    /// 색은 BuffStatPalette 하나에서 온다. 발밑 빛기둥과 같은 표를 쓰므로
    /// 범위 안에 선 병사의 기둥 색과 원 색이 항상 일치한다.
    /// </summary>
    public static GameObject SpawnRange(string key, Vector3 center, float radius,
        float duration, Color tint, float baseRadius = 1f)
    {
        var go = SpawnRange(key, center, radius, duration, baseRadius);
        if (go == null) return null;

        if (!go.TryGetComponent<EffectTint>(out var tinter))
            tinter = go.AddComponent<EffectTint>();
        tinter.Apply(tint);

        return go;
    }

    /// <summary>
    /// from 위치에 스폰한 뒤 LineRenderer 를 from→to 로 설정한다.
    /// "ImpactSparks", "RedGlow" 이름의 자식 GO 는 to 위치로 이동한다.
    /// </summary>
    public static GameObject Spawn(string key, Vector3 from, Vector3 to, float despawnDelay)
    {
        var go = Spawn(key, from, despawnDelay);
        if (go == null) return null;

        if (go.TryGetComponent<LineRenderer>(out var lr))
        {
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
        }

        var sparks = go.transform.Find("ImpactSparks");
        if (sparks != null) sparks.position = to;

        var glow = go.transform.Find("RedGlow");
        if (glow != null) glow.position = to;

        return go;
    }
}

// ─────────────────────────────────────────────────────────────
// ■ 이펙트 자동 반납 컴포넌트
// ─────────────────────────────────────────────────────────────

public class EffectAutoReturn : MonoBehaviour
{
    Coroutine _cr;

    void OnDisable()
    {
        if (_cr != null) { StopCoroutine(_cr); _cr = null; }
    }

    public void StartReturn(float delay)
    {
        if (_cr != null) StopCoroutine(_cr);
        _cr = StartCoroutine(ReturnAfter(delay));
    }

    IEnumerator ReturnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        _cr = null;

        if (PoolController.Instance != null)
            PoolController.Instance.Despawn(gameObject);
        else
            gameObject.SetActive(false);
    }
}

// ─────────────────────────────────────────────────────────────
// ■ 이펙트 설정 구조체 (Runner 전달용)
// ─────────────────────────────────────────────────────────────

public struct SkillEffectConfig
{
    public string CasterEffectKey;   // 시전자 이펙트 풀 키
    public string TargetEffectKey;   // 피격 대상 이펙트 풀 키
    public string BaseEffectKey;     // 기본/범위 이펙트 풀 키
    public float  DespawnDelay;      // 자동 반납 딜레이 (초)
}

// ============================================================
//  EffectFallMotion
//  스폰된 이펙트를 시작점 → 착탄점으로 떨어뜨린다.
//
//  ⚠ 실시간이 아니라 게임 시간으로 움직인다
//    낙하는 연출이 아니라 '착탄까지 남은 시간' 을 보여 주는 예고다.
//    배속을 켜면 전투가 빨라지는 만큼 운석도 빨리 떨어져야 예고가 맞는다.
// ============================================================
public class EffectFallMotion : MonoBehaviour
{
    Vector3 _from, _to;
    float   _duration, _elapsed, _spin;
    bool    _running;

    public void Begin(Vector3 from, Vector3 to, float duration, float spinSpeed)
    {
        _from = from; _to = to;
        _duration = Mathf.Max(0.01f, duration);
        _spin = spinSpeed;
        _elapsed = 0f;
        _running = true;
        transform.position = from;
    }

    void OnDisable() => _running = false;

    void Update()
    {
        if (!_running) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        // 가속 낙하 — 등속으로 떨어지면 무게가 안 느껴진다
        transform.position = Vector3.Lerp(_from, _to, t * t);
        transform.Rotate(0f, 0f, _spin * Time.deltaTime);

        if (t >= 1f) _running = false;
    }
}
