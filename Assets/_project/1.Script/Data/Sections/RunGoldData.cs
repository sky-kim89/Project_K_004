using System;
using UnityEngine;

// ============================================================
//  RunGoldData.cs
//  런 스코프 **골드** — 용사를 잡아 모으고, 스테이지 사이의 갈림길에서 쓴다.
//
//  ■ 런이 끝나면 사라진다 (사용자 확정, 2026-09-04)
//    환생·패배로 초기화된다. 남은 골드를 환생 포인트나 영구 재화로 바꿔 주지
//    않는다 — 그러면 "안 쓰고 모으기" 가 최적해가 되어 갈림길 선택이 무의미해진다.
//    골드는 **이번 런 안에서 다 쓰라고** 있는 자원이다.
//
//  ■ ⚠ ItemData 의 eItem.Gold 와 다른 재화다
//    그쪽은 원작에서 넘어온 **영구 재화**로 용병 고용·장비 강화·환생에 쓰인다.
//    이름이 같다고 같은 것이 아니다. 같은 지갑을 쓰면
//      · 한 판 잘 굴린 것이 영구 성장으로 새어 나가고
//      · 반대로 메타에서 모은 돈으로 런 안의 선택을 사 버릴 수 있다
//    (CLAUDE.md '재화별 저장 위치를 먼저 볼 것' 과 같은 주의다)
//
//  ■ 마나와도 다른 축이다
//    마나 : 이번 **판 안에서** 무엇을 몇 마리 낼까 — 전투 자원
//    골드 : 판과 판 **사이에서** 어디로 갈까 — 경로 자원
//    둘을 한 재화로 묶으면 "소환을 아껴 상점에 간다" 가 되어 전투가 늘 손해가 된다.
// ============================================================

[Serializable]
class RunGoldJson
{
    public int current;
    public int earned;   // 이번 런에 번 총액 (통계·결산용)
    public int spent;    // 이번 런에 쓴 총액 (통계용)
}

public class RunGoldData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.RunGold;

    /// <summary>지금 들고 있는 골드.</summary>
    public int Current { get; private set; }

    /// <summary>이번 런에 번 총액. 통계·결산용.</summary>
    public int Earned { get; private set; }

    /// <summary>이번 런에 쓴 총액. 통계용.</summary>
    public int Spent { get; private set; }

    /// <summary>잔액이 바뀔 때마다 발생 — 상단바 표시가 구독한다.</summary>
    public event Action OnGoldChanged;

    // ── 획득 ─────────────────────────────────────────────────

    /// <summary>골드를 번다. 0 이하는 무시한다.</summary>
    public void Add(int amount)
    {
        if (amount <= 0) return;

        Current += amount;
        Earned  += amount;
        OnGoldChanged?.Invoke();
    }

    // ── 소모 ─────────────────────────────────────────────────

    public bool CanSpend(int cost) => cost >= 0 && Current >= cost;

    /// <summary>
    /// 골드를 낸다. 모자라면 아무것도 하지 않고 false 를 돌려준다.
    ///
    /// ⚠ 부르는 쪽은 반환값을 반드시 확인한다
    ///   마나(SummonManaData.Spend)와 같은 계약이다 — false 인데 물건을
    ///   내주면 공짜가 된다.
    /// </summary>
    public bool Spend(int cost)
    {
        if (!CanSpend(cost)) return false;

        Current -= cost;
        Spent   += cost;
        OnGoldChanged?.Invoke();
        return true;
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize() => JsonUtility.ToJson(new RunGoldJson
    {
        current = Current,
        earned  = Earned,
        spent   = Spent,
    });

    public void Deserialize(string json)
    {
        var data = JsonUtility.FromJson<RunGoldJson>(json);

        Current = data?.current ?? 0;
        Earned  = data?.earned  ?? 0;
        Spent   = data?.spent   ?? 0;

        OnGoldChanged?.Invoke();
    }

    /// <summary>환생·신규 런 시작 시 초기화.</summary>
    public void SetDefaults()
    {
        Current = 0;
        Earned  = 0;
        Spent   = 0;
        OnGoldChanged?.Invoke();
    }

    /// <summary>런이 끝났다 — 남은 골드는 사라진다. 카드·특성과 같은 수명이다.</summary>
    public void ResetForNewRun() => SetDefaults();
}
