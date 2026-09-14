using System.Collections;
using UnityEngine;

// ============================================================
//  WarBannerRunner.cs
//  군기 강림(WarBanner)의 군기 연출을 시전자에게 붙여 두는 MonoBehaviour.
//
//    ① 추종 : 군기가 시전자를 따라다닌다 (그 자리에 박아 두면 장군만 걸어 나간다)
//    ② 번쩍 : 깃발은 FlashTime 동안만 펄럭이고, 그 뒤 알파가 빠지며 사라진다
//    ③ 표시 : 링·티끌은 버프가 끝날 때까지 **버프 반경 그대로** 깔린다
//
//  ⚠ 연속 시전(특성)으로 두 번 들어와도 군기는 하나다
//    두 번 스폰하면 같은 자리에 겹쳐 두 배로 밝아지고 링이 두 겹으로 보인다.
//    이미 떠 있으면 재사용하고 지속 시간·번쩍만 갱신한다.
//    (버프는 ActiveWarBanner.Execute 가 시전마다 따로 건다 — 여기는 연출 전담이다)
//
//  ⚠ 첫 깃발·첫 링은 Emit 으로 직접 뽑는다
//    프리팹 emitter 는 rateOverTime 0.8 / 1.4 라 그냥 두면 첫 입자가
//    1.25초·0.7초 뒤에나 나온다. "시전 순간 번쩍" 이 성립하지 않는다.
//
//  ⚠ 반경은 링·티끌에만 먹인다
//    루트까지 키우면 깃발이 반경에 비례해 커져 장군보다 커진다.
//    자식 파티클은 scalingMode=Local 이라 루트 스케일을 어차피 무시한다 —
//    자식 transform 을 직접 키워야 한다. (예전에 반경이 안 맞던 진짜 원인)
//
//  ⚠ 시간 축이 셋이다 — 헷갈리면 링이 허공에 멈춘다
//    _flashUntil : 깃발 방출 종료      (시전 + FlashTime)
//    _buffEnd    : 링·티끌 방출 종료    (시전 + 버프 지속)
//    _expire     : 풀 반납 = 추종 종료  (_buffEnd + DespawnDelay, 잔여 입자가 사라질 시간)
// ============================================================

public class WarBannerRunner : MonoBehaviour
{
    // 링 텍스처(TX_FX_Ring)는 쿼드 반폭의 72% 지점에 원이 그려진다.
    // (EffectTextureGenerator.GenRing — Gauss(d, 0.72f, 0.11f))
    // 보이는 원을 버프 반경에 맞추려면 그만큼 되키워야 한다.
    const float RingTexRadiusRatio = 0.72f;

    // 프리팹 기준 치수 — 바꾸면 RareSkillEffectGenerator.BuildBannerAura 도 같이 고칠 것
    const float RingBaseRadius  = 3f;     // Ring  startSize 6 → 반경 3
    const float MotesBaseRadius = 2.9f;   // Motes shape.radius

    GameObject     _banner;      // 떠 있는 군기 인스턴스 (연속 시전 시 재사용)
    ParticleSystem _flag;        // 루트 = 펄럭이는 깃발
    ParticleSystem _ring;        // 버프 반경 표시
    ParticleSystem _motes;

    float _flashUntil;
    float _buffEnd;
    float _expire;
    bool  _flagOn;
    bool  _auraOn;

    Coroutine _cr;

    void OnDisable()
    {
        // 시전자가 풀에 돌아가면 군기와의 인연도 끊는다.
        // 안 끊으면 재스폰된 장군이 남의 이펙트를 붙잡고 흔든다.
        Forget();
    }

    public void Run(Transform casterTf, float radius, float duration, float flashTime,
                    SkillEffectConfig fx)
    {
        float now = Time.time;

        bool reuse = _banner != null && _banner.activeInHierarchy && now < _expire;
        if (!reuse)
        {
            _banner = SkillEffectHelper.Spawn(fx.BaseEffectKey, casterTf.position,
                                              duration + fx.DespawnDelay);
            if (_banner == null) return;

            _flag  = _banner.GetComponent<ParticleSystem>();
            _ring  = FindPS(_banner, "Ring");
            _motes = FindPS(_banner, "Motes");
        }

        _flashUntil = now + flashTime;
        _buffEnd    = Mathf.Max(_buffEnd, now + duration);
        _expire     = _buffEnd + fx.DespawnDelay;

        // 반납 시각을 늘 다시 잡는다 — 재사용이든 신규든 _expire 에 맞춘다
        if (_banner.TryGetComponent<EffectAutoReturn>(out var ret))
            ret.StartReturn(_expire - now);

        // 반경 표시 — 재시전마다 다시 먹인다 (풀에서 나온 인스턴스는 옛 크기를 물고 있다)
        if (_ring  != null) _ring.transform.localScale  = Vector3.one * (radius / (RingBaseRadius * RingTexRadiusRatio));
        if (_motes != null) _motes.transform.localScale = Vector3.one * (radius / MotesBaseRadius);

        // 번쩍 — 시전(재시전)마다 깃발을 다시 펄럭이게 한다.
        //
        //  ⚠ 수명을 FlashTime 으로 덮어써야 FlashTime 이 의미를 갖는다
        //    프리팹 기본 수명은 1.6초다. 방출만 끊으면 이미 떠 있는 깃발은
        //    제 수명대로 1.6초를 채우고 사라져 "1초 번쩍" 이 성립하지 않는다.
        //    수명 = FlashTime 으로 두면 colorOverLifetime 이 딱 그 시간에 걸쳐
        //    알파를 빼주므로 정확히 그만큼 번쩍이고 투명해진다.
        var flagMain = _flag.main;   // 모듈은 struct 반환 — 지역 변수로 받아야 한다 (CS1612)
        flagMain.startLifetime = flashTime;

        _flag.Play(false);           // withChildren:false — 링·티끌은 따로 관리한다
        _flag.Emit(1);
        _flagOn = true;

        PlayAura();
        if (_ring != null) _ring.Emit(1);

        _banner.transform.position = casterTf.position;

        if (_cr == null) _cr = StartCoroutine(Follow(casterTf));
    }

    IEnumerator Follow(Transform casterTf)
    {
        while (_banner != null && _banner.activeInHierarchy && casterTf != null
               && Time.time < _expire)
        {
            _banner.transform.position = casterTf.position;

            // 깃발 방출만 멈춘다. 남아 있던 깃발은 제 수명대로 알파가 빠지며 사라진다
            // — 툭 끊기지 않고 "번쩍했다가 투명해지는" 그림이 된다.
            if (_flagOn && Time.time >= _flashUntil)
            {
                _flagOn = false;
                _flag.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            // 버프가 끝나면 반경 표시도 방출을 끊는다.
            // 남은 입자가 사라질 때까지는 계속 따라다닌다 — 여기서 손을 놓으면
            // 마지막 링이 허공에 멈춘 채 사라져 원래 버그가 그대로 재현된다.
            if (_auraOn && Time.time >= _buffEnd)
            {
                _auraOn = false;
                StopAura();
            }

            yield return null;
        }

        Forget();
    }

    void PlayAura()
    {
        if (_ring  != null) _ring.Play(false);
        if (_motes != null) _motes.Play(false);
        _auraOn = true;
    }

    void StopAura()
    {
        if (_ring  != null) _ring.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        if (_motes != null) _motes.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    void Forget()
    {
        _banner = null;
        _flag   = null;
        _ring   = null;
        _motes  = null;
        _cr     = null;
        _flagOn = false;
        _auraOn = false;
        _buffEnd = 0f;
        _expire  = 0f;
    }

    static ParticleSystem FindPS(GameObject root, string childName)
    {
        var t = root.transform.Find(childName);
        return t != null ? t.GetComponent<ParticleSystem>() : null;
    }
}
