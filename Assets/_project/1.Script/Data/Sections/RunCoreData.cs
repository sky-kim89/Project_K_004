using System;
using UnityEngine;

// ============================================================
//  RunCoreData.cs
//  마왕성 체력 — **유저의 목숨**. 이 값이 0 이면 런이 끝난다.
//
//  ■ ⚠ 소환사의 HP 와 다른 축이다 (2026-09-04 개편)
//    예전에는 소환사가 곧 마왕성이라, 용사가 성벽까지 걸어와 소환사를 **때려서**
//    HP 를 깎았다. 그 구조에는 치명적인 문제가 있었다 —
//    적이 세지면 **한 방에 죽는다**. 스테이지가 오를수록 용사의 공격력이
//    올라가므로, 어느 순간부터 "한 마리만 새어 나가도 즉사" 가 된다.
//    막아 낸 정도와 결과 사이에 아무 관계가 없어진다.
//
//    지금은 **몇 마리를 통과시켰는가**를 센다.
//      성벽에 닿은 용사 1기 = 1 감소
//    공격력이 아무리 세도 한 마리는 1 이다. 20 을 잃으려면 20 마리를 놓쳐야
//    하고, 그 사이에 플레이어는 라인을 다시 세울 수 있다.
//
//  ■ 용사는 소환사를 노리지 않는다
//    타겟 그리드에서 소환사를 뺐다(UnitTargetSearchSystem.BuildGridMapJob).
//    몬스터가 하나라도 있으면 반드시 몬스터를 먼저 치고, 아무도 없을 때만
//    성벽으로 직진한다. 마왕성은 **가장 마지막 표적**이다.
//
//  ■ 런이 끝나면 초기화된다
//    ⚠ 스테이지를 넘긴다고 회복되지 않는다. 회복은 갈림길(야영지)이 판다.
// ============================================================

[Serializable]
class RunCoreJson
{
    public int max;
    public int current;
    public int breached;   // 이번 런에 통과당한 총 마릿수 (통계용)
}

public class RunCoreData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.RunCore;

    /// <summary>마왕성 최대 체력. 야영지가 이 값을 올린다.</summary>
    public int Max { get; private set; }

    /// <summary>남은 체력. 0 이면 런 종료.</summary>
    public int Current { get; private set; }

    /// <summary>이번 런에 성벽을 통과당한 총 마릿수. 통계용.</summary>
    public int Breached { get; private set; }

    /// <summary>0~1. 체력 바가 이 값으로 찬다.</summary>
    public float Fill => Max > 0 ? Mathf.Clamp01((float)Current / Max) : 0f;

    /// <summary>무너졌는가 — 런 종료 판정의 정본.</summary>
    public bool IsDown => Max > 0 && Current <= 0;

    /// <summary>값이 바뀔 때마다 발생 — 상단바 체력 표시가 구독한다.</summary>
    public event Action OnCoreChanged;

    // ── 런 시작 ──────────────────────────────────────────────

    /// <summary>
    /// 런 시작 — 최대치를 정하고 가득 채운다. 런당 1회다.
    ///
    /// ⚠ 이어하기에서는 부르지 않는다
    ///   앱을 껐다 켜는 것이 회복 수단이 된다 (마나 GrantForRun 과 같은 이유).
    /// </summary>
    public void GrantForRun(int max)
    {
        Max      = Mathf.Max(1, max);
        Current  = Max;
        Breached = 0;
        OnCoreChanged?.Invoke();
    }

    // ── 피해 ─────────────────────────────────────────────────

    /// <summary>
    /// 용사 <paramref name="count"/> 기가 성벽을 통과했다 — 그만큼 깎인다.
    ///
    /// ⚠ 공격력을 보지 않는다. 한 마리는 언제나 1 이다 (파일 머리 주석 참고).
    /// </summary>
    public void Breach(int count = 1)
    {
        if (count <= 0 || Current <= 0) return;

        Current   = Mathf.Max(0, Current - count);
        Breached += count;
        OnCoreChanged?.Invoke();
    }

    // ── 회복·강화 (갈림길 '야영지') ──────────────────────────

    /// <summary>체력을 회복한다. 최대치를 넘지 않는다.</summary>
    public void Heal(int amount)
    {
        if (amount <= 0 || Current >= Max) return;

        Current = Mathf.Min(Max, Current + amount);
        OnCoreChanged?.Invoke();
    }

    /// <summary>
    /// <b>스스로 치른 값</b>만큼 체력이 깎인다 — 갈림길 '이벤트' 가 쓴다.
    ///
    /// ⚠ Breach 와 다른 물건이다
    ///   그쪽은 용사가 성벽을 통과한 횟수를 함께 센다(Breached, 통계용).
    ///   이벤트로 낸 체력을 거기 섞으면 "이번 런에 몇 기를 놓쳤나" 가 거짓말이 된다.
    ///
    /// ⚠ 0 으로 만들지 않는다 — 최소 1 은 남는다
    ///   "골랐더니 런이 끝났다" 를 어떤 이유로도 만들지 않는다. 애초에
    ///   RunEventRule.Available 이 낼 수 없는 갈래를 흐리게 만들지만,
    ///   마지막 방벽은 값을 깎는 이 자리에 둔다.
    /// </summary>
    public void Pay(int amount)
    {
        if (amount <= 0 || Current <= 1) return;

        Current = Mathf.Max(1, Current - amount);
        OnCoreChanged?.Invoke();
    }

    /// <summary>
    /// 최대 체력을 올린다. <b>늘어난 만큼 즉시 채워진다.</b>
    ///
    /// 채우지 않으면 "최대치 +5" 를 골랐는데 화면의 숫자가 그대로라
    /// 아무 일도 안 일어난 것처럼 보인다.
    /// </summary>
    public void AddMax(int amount)
    {
        if (amount <= 0) return;

        Max     += amount;
        Current += amount;
        OnCoreChanged?.Invoke();
    }

    /// <summary>
    /// 최대 체력을 비율로 줄인다 — 특성 '유리 성채'. 지금 체력도 새 최대치를 넘지 않게 자른다.
    /// ⚠ 0 으로 만들지 않는다 — 최소 1 ("골랐더니 런이 끝났다" 금지).
    /// </summary>
    public void ScaleMax(float ratio)
    {
        Max     = Mathf.Max(1, Mathf.RoundToInt(Max * ratio));
        Current = Mathf.Clamp(Current, 1, Max);
        OnCoreChanged?.Invoke();
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize() => JsonUtility.ToJson(new RunCoreJson
    {
        max      = Max,
        current  = Current,
        breached = Breached,
    });

    public void Deserialize(string json)
    {
        var data = JsonUtility.FromJson<RunCoreJson>(json);

        Max      = data?.max      ?? 0;
        Current  = data?.current  ?? 0;
        Breached = data?.breached ?? 0;

        OnCoreChanged?.Invoke();
    }

    public void SetDefaults()
    {
        Max      = 0;
        Current  = 0;
        Breached = 0;
        OnCoreChanged?.Invoke();
    }
}
