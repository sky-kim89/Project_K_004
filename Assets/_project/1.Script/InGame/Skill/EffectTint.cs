using UnityEngine;

// ============================================================
//  EffectTint.cs
//  이펙트 프리팹 하나를 색만 바꿔 여러 용도로 쓰기 위한 장치.
//
//  ■ 왜 필요한가
//    "공격력 버프 범위" 와 "방어 버프 범위" 는 같은 원이고 색만 다르다.
//    색깔 수만큼 프리팹을 만들면 그만큼 풀도 늘고 머티리얼도 갈린다.
//    프리팹은 흰색 한 벌만 두고, 꺼낼 때 색을 입힌다.
//
//  ■ 색은 파티클의 startColor 로만 바꾼다
//    머티리얼을 건드리면 인스턴스가 갈라져 배칭이 깨진다.
//    startColor 는 파티클 정점 색이라 같은 머티리얼을 그대로 쓴다.
//
//  ■ 풀에 돌아갈 때 반드시 원래 색으로 되돌린다
//    이 프리팹은 풀에서 재사용된다. 되돌리지 않으면 다음에 꺼낸 범위 원이
//    앞사람이 쓰던 색으로 나온다 — 방어 버프인데 빨갛게 뜨는 식이다.
//    OnDisable 에서 되돌리므로 풀 반납(SetActive(false))이 곧 복구다.
// ============================================================

[DisallowMultipleComponent]
public class EffectTint : MonoBehaviour
{
    ParticleSystem[]                    _systems;
    ParticleSystem.MinMaxGradient[]     _original;
    bool                                _tinted;

    /// <summary>
    /// 이 이펙트 전체를 한 색으로 물들인다.
    ///
    /// 밝기 차이(자식마다 다른 startColor)는 알파로만 보존한다 —
    /// 색상까지 각자 유지하면 "무슨 버프인가" 를 색으로 읽을 수 없다.
    /// </summary>
    public void Apply(Color color)
    {
        Cache();

        for (int i = 0; i < _systems.Length; i++)
        {
            var ps = _systems[i];
            if (ps == null) continue;

            var main = ps.main;

            // 원래 알파를 살려 둔다 — 안개처럼 옅게 깔리던 것이 불투명해지면 화면을 덮는다
            float alpha = _original[i].mode == ParticleSystemGradientMode.Color
                ? _original[i].color.a
                : _original[i].colorMax.a;

            var c = color;
            c.a = alpha;
            main.startColor = new ParticleSystem.MinMaxGradient(c);
        }

        _tinted = true;
    }

    void OnDisable()
    {
        if (!_tinted || _systems == null) return;

        for (int i = 0; i < _systems.Length; i++)
        {
            if (_systems[i] == null) continue;
            var main = _systems[i].main;
            main.startColor = _original[i];
        }

        _tinted = false;
    }

    void Cache()
    {
        if (_systems != null) return;

        _systems  = GetComponentsInChildren<ParticleSystem>(true);
        _original = new ParticleSystem.MinMaxGradient[_systems.Length];
        for (int i = 0; i < _systems.Length; i++)
            _original[i] = _systems[i].main.startColor;
    }
}
