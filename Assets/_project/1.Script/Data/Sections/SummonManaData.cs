using System;
using UnityEngine;

// ============================================================
//  SummonManaData.cs
//  소환 마나 — 런 스코프 세이브 섹션.
//
//  ■ 그릇이 있고, 스테이지마다 일부가 찬다 (2026-08-28 개편)
//    Max      — 소환사의 지능이 정하는 상한. 런 시작에는 가득 차 있다.
//    Regen    — 스테이지를 넘길 때마다 **지능 + 잔량의 10%** 가 돌아온다
//               (공식의 정본은 SummonerData.ManaRegenFor).
//    회복은 절대 Max 를 넘지 않는다.
//
//    ⚠ 회복량이 잔량에 의존하므로 회복 순서가 중요하다 — 잔량을 건드리는
//      다른 정산(특성 환급 등)을 회복보다 **뒤에** 두면 그만큼 이자가 덜 붙는다.
//
//  ■ ⚠ 환수(Refund)는 폐기됐다
//    옛 규칙은 "적을 다 잡으면 살아남은 몬스터가 제 몫을 돌려주고 물러난다"
//    였다. 그러면 마나 손실이 곧 **사망률**이 되고, 사망률을 낮추는 최선책이
//    "더 많이 소환하기" 라서 최적해가 언제나 '전부 낸다' 로 굳었다.
//
//    지금 살아남은 몬스터는 마나로 녹지 않고 **제 라인으로 돌아가 다음 판에
//    다시 싸운다** (MonsterLineReturner). 아낀 보상이 마나가 아니라 전력이다.
//
//  ■ 마나 0 은 패배가 아니다
//    신규 소환이 막힐 뿐이다. 런은 소환사(= 마왕성)의 HP 가 0이 될 때 끝난다.
//
//  ■ 환생 시 초기화된다 (SetDefaults)
// ============================================================

[Serializable]
class SummonManaJson
{
    public float max;        // 마나 그릇 (소환사 지능이 정한다)
    public float granted;    // 이번 런에 들어온 총량 (시작 + 회복 누적)
    public float current;    // 현재 잔량
    public float spent;      // 누적 소모량 (통계·UI용)
    public float regenerated;// 누적 회복량 (통계·UI용)
}

public class SummonManaData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.SummonMana;

    /// <summary>마나 그릇. 회복은 이 값을 넘지 못한다.</summary>
    public float Max { get; private set; }

    /// <summary>이번 런에 지금까지 들어온 마나 총량 (시작 + 회복 누적). 통계용.</summary>
    public float Granted { get; private set; }

    /// <summary>현재 잔량.</summary>
    public float Current { get; private set; }

    /// <summary>누적 소모량 (통계·UI용).</summary>
    public float Spent { get; private set; }

    /// <summary>누적 회복량 (통계·UI용).</summary>
    public float Regenerated { get; private set; }

    /// <summary>0~1. 게이지가 이 값으로 찬다.</summary>
    public float Fill => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;

    /// <summary>잔량이 바뀔 때마다 발생 — 마나 게이지 갱신용.</summary>
    public event Action OnManaChanged;

    // ── 런 시작 ──────────────────────────────────────────────

    /// <summary>
    /// 런 시작 — 그릇 크기를 정하고 가득 채운다. 런당 1회만 호출된다.
    ///
    /// ⚠ 이어하기에서는 부르지 않는다
    ///   잔량을 통째로 되돌리므로, 앱을 껐다 켜는 것이 회복 수단이 된다.
    ///   (RunBootstrap.StartRun 의 resume 분기 참고)
    /// </summary>
    public void GrantForRun(float maxMana)
    {
        Max         = maxMana;
        Granted     = maxMana;
        Current     = maxMana;
        Spent       = 0f;
        Regenerated = 0f;
        OnManaChanged?.Invoke();
    }

    /// <summary>
    /// 그릇 크기를 다시 맞춘다.
    ///
    /// ⚠ 잔량은 건드리지 않는다
    ///   이어하기로 되살아난 세이브의 Max 를 소환사 값으로 되맞추는 용도다.
    ///   여기서 Current 까지 채우면 앱을 껐다 켜는 것이 회복 수단이 된다.
    ///   다만 그릇이 줄었다면 잔량도 따라 줄여야 게이지가 넘치지 않는다.
    /// </summary>
    public void SetMax(float maxMana)
    {
        if (Mathf.Approximately(Max, maxMana)) return;

        Max     = maxMana;
        Current = Mathf.Min(Current, Max);

        OnManaChanged?.Invoke();
    }

    /// <summary>
    /// 스테이지를 넘겼다 — 일부가 돌아온다.
    ///
    /// ⚠ 그릇을 넘지 않는다
    ///   넘치게 두면 판을 오래 끌수록 마나가 쌓여, "이번 판에 얼마를 걸까" 가
    ///   다시 무의미해진다. 회복은 소모한 만큼을 메우는 것이지 저축이 아니다.
    /// </summary>
    public void RegenForStage(float amount)
    {
        if (amount <= 0f || Current >= Max) return;

        float before = Current;

        Current      = Mathf.Min(Current + amount, Max);
        Regenerated += Current - before;
        Granted     += Current - before;

        OnManaChanged?.Invoke();
    }

    /// <summary>
    /// 판 도중·시설에서 채운다 — 야영지 수리·강적의 정수·마나 방출 (2026-09-12).
    /// ⚠ 그릇을 넘지 않고 **정수로만** 채운다 (마나는 정수로 움직인다 — ManaRegenRule 주석).
    /// ⚠ 직접 부르기보다 RunPerkRule.RestoreMana 를 지날 것.
    /// </summary>
    public void Restore(float amount)
    {
        amount = Mathf.Floor(amount);
        if (amount <= 0f || Current >= Max) return;

        float before = Current;

        Current      = Mathf.Min(Current + amount, Max);
        Regenerated += Current - before;
        Granted     += Current - before;

        OnManaChanged?.Invoke();
    }

    // ── 소모 ────────────────────────────────────────────────

    /// <summary>소환 가능한지 확인한다. 잔량이 모자라면 false.</summary>
    public bool CanSpend(float cost) => Current >= cost;

    /// <summary>
    /// 마나를 소모한다. 잔량이 모자라면 아무것도 하지 않고 false 를 반환한다.
    /// 호출 측은 반환값을 반드시 확인하고, false 면 소환을 중단해야 한다.
    /// </summary>
    public bool Spend(float cost)
    {
        if (Current < cost) return false;

        Current -= cost;
        Spent   += cost;
        OnManaChanged?.Invoke();
        return true;
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize() => JsonUtility.ToJson(new SummonManaJson
    {
        max         = Max,
        granted     = Granted,
        current     = Current,
        spent       = Spent,
        regenerated = Regenerated,
    });

    public void Deserialize(string json)
    {
        var data = JsonUtility.FromJson<SummonManaJson>(json);

        Max         = data.max;
        Granted     = data.granted;
        Current     = data.current;
        Spent       = data.spent;
        Regenerated = data.regenerated;

        // ⚠ 옛 세이브에는 max 가 없다 — 0 이면 게이지가 영원히 비어 보인다
        //   잔량만큼이라도 그릇을 잡아 두면 화면이 거짓말을 하지 않는다.
        //   다음 스테이지 회복에서 소환사 값으로 다시 맞춰진다.
        if (Max <= 0f) Max = Mathf.Max(Current, Granted);

        // ⚠ 옛 세이브에는 0.5 같은 찌꺼기가 남아 있다 (2026-09-10)
        //   회복은 이제 정수로만 들어오지만(ManaRegenRule.RawFor), 그 전에 저장된
        //   런은 소수점을 들고 이어진다. 그대로 두면 그 런만 잔량이 영영 0 에
        //   닿지 못해 특성 '마력 폭주' 가 죽는다. 이어받는 자리에서 한 번 턴다.
        Current = Mathf.Floor(Current);
    }

    /// <summary>환생·신규 시작 시 초기화. 실제 부여는 GrantForRun 이 한다.</summary>
    public void SetDefaults()
    {
        Max         = 0f;
        Granted     = 0f;
        Current     = 0f;
        Spent       = 0f;
        Regenerated = 0f;
    }
}
