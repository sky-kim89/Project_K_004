using System;
using UnityEngine;

// ============================================================
//  DifficultyData.cs
//  난이도 선택 상태 저장 섹션.
//
//  SelectedTier  : 지금 고른 등급. 런 시작 시점에 고정된다.
//  ClearedTier   : 돌파한 최고 등급. 이 값 +1 까지 선택할 수 있다.
//                  기록은 TryBreakthrough 하나 — 그 등급에서 UnlockStage(20) 이상을
//                  **처음** 깬 순간이다 (RunBootstrap.AdvanceStage 가 부른다).
//  PendingUnlock : 방금 열린 등급 — 아직 해금 연출을 안 보여 줬다.
//
//  ■ 해금 연출은 **런 끝 보상 상자를 닫은 뒤**다 (사용자 지시, 2026-09-11)
//    돌파는 런 도중에 일어나지만 그 자리에서는 기록만 한다. 런이 끝나
//    보상 상자(GearBoxPopup)를 닫으면 DifficultyUnlockPopup 이 뜬다.
//    ⚠ 세이브에 둔다 — 그 사이 앱이 꺼지면 다음 실행 때 로비에서 보여 준다.
//    ⚠ 마지막 등급은 열 것이 없어 연출도 없다.
//
//  ⚠ 환생으로 초기화하지 않는다
//    런 데이터가 아니라 계정 진행도다. 해금 기록이 날아가면
//    다시 처음 등급부터 완주해야 한다.
//
//  ⚠ 런 도중에는 바꿀 수 없다
//    30스테이지를 쉬운 등급으로 깔고 마지막만 올리는 악용을 막는다.
//    StageProgressData.RunInProgress 를 보고 UI 에서 잠근다.
// ============================================================

public class DifficultyData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.Difficulty;

    RawData _raw = new();

    /// <summary>선택 변경 알림 — 상단 배지·난이도 팝업이 구독한다.</summary>
    public static event Action OnChanged;

    // ── 읽기 ────────────────────────────────────────────────────

    public DifficultyTier SelectedTier => (DifficultyTier)_raw.SelectedTier;

    /// <summary>완주해 본 최고 등급. 아직 하나도 못 깼으면 -1.</summary>
    public int ClearedTierIndex => _raw.ClearedTier;

    /// <summary>선택 가능한 최고 등급 인덱스 — 완주한 다음 등급까지.</summary>
    public int MaxSelectableIndex =>
        Mathf.Min(_raw.ClearedTier + 1, Enum.GetValues(typeof(DifficultyTier)).Length - 1);

    public bool IsUnlocked(DifficultyTier tier) => (int)tier <= MaxSelectableIndex;

    /// <summary>이 스테이지 이상을 깨면 다음 등급이 열린다.</summary>
    public const int UnlockStage = 20;

    /// <summary>한 칸 위 등급. 마지막 등급이면 false.</summary>
    public static bool TryNext(DifficultyTier tier, out DifficultyTier next)
    {
        next = tier + 1;
        return (int)next < Enum.GetValues(typeof(DifficultyTier)).Length;
    }

    // ── 쓰기 ────────────────────────────────────────────────────

    public void Select(DifficultyTier tier)
    {
        if (!IsUnlocked(tier)) return;
        if (_raw.SelectedTier == (int)tier) return;

        _raw.SelectedTier = (int)tier;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 기록만 올린다 — 보상·연출 없이. 치트 도구가 쓴다.
    /// 실제 게임은 <see cref="TryBreakthrough"/> 를 지난다.
    /// </summary>
    public void RecordClear(DifficultyTier tier)
    {
        if ((int)tier <= _raw.ClearedTier) return;
        _raw.ClearedTier = (int)tier;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 스테이지를 깼다 — 이 등급을 <b>처음</b> 돌파한 순간이면 기록을 올리고 true.
    /// 다시 깨도 두 번 오르지 않는다.
    /// </summary>
    public bool TryBreakthrough(DifficultyTier tier, int clearedStage)
    {
        if (clearedStage < UnlockStage)        return false;
        if ((int)tier <= _raw.ClearedTier)     return false;

        _raw.ClearedTier = (int)tier;
        if (TryNext(tier, out var next)) _raw.PendingUnlock = (int)next;

        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 아직 보여 주지 않은 해금이 있나. 꺼내기만 하고 지우지 않는다 —
    /// 창을 실제로 연 뒤에 <see cref="ClearPendingUnlock"/> 로 지운다.
    /// </summary>
    public bool PeekPendingUnlock(out DifficultyTier tier)
    {
        tier = (DifficultyTier)Mathf.Max(0, _raw.PendingUnlock);
        return _raw.PendingUnlock >= 0;
    }

    public void ClearPendingUnlock() => _raw.PendingUnlock = -1;

    // ── ISaveSection ────────────────────────────────────────────

    public string Serialize() => JsonUtility.ToJson(_raw);

    public void Deserialize(string json)
    {
        _raw = JsonUtility.FromJson<RawData>(json) ?? new RawData();
        OnChanged?.Invoke();
    }

    public void SetDefaults()
    {
        _raw = new RawData();
        OnChanged?.Invoke();
    }

    [Serializable]
    class RawData
    {
        public int SelectedTier = 0;    // 출정
        public int ClearedTier  = -1;   // 아직 아무것도 돌파하지 않음
        public int PendingUnlock = -1;  // 보여 줄 해금 연출 없음 (옛 세이브도 -1 로 읽힌다)
    }
}
