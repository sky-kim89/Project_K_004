using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SummonReservation.cs
//  "어느 라인에 무엇을 소환할지" 예약 목록. 실제 스폰은 하지 않는다.
//
//  ■ 라인마다 따로 줄을 선다
//    라인별로 독립된 큐다. 3번 라인이 밀려도 1번 라인은 제 속도로 나간다 —
//    한 줄로 관리하면 앞쪽 라인이 다 빠질 때까지 뒤 라인이 멎는다.
//
//  ■ 큐에는 개체 단위로 들어간다
//    슬라임(6마리) 카드를 한 번 걸면 그 라인 큐에 **6개**가 쌓인다.
//    그래서 화면의 "×N" 이 곧 남은 마릿수다 — 카드 사용 횟수가 아니다.
//
//  ■ 왜 예약인가 — 소환은 탭한 순간이 아니라 스테이지가 시작될 때 일어난다
//    대기 중에 라인을 탭하면 그 자리에 몬스터가 서는 게 아니라 줄을 선다.
//    미리 세워 두면 적이 오기도 전에 필드를 돌아다니다 오른쪽으로 빠져나간다.
//
//  ■ 마나는 예약하는 순간 빠진다
//    선불이다. 취소 기능은 없다 — 되돌릴 수 있으면 "얼마나 걸 것인가" 가
//    선택이 아니게 된다.
//
//  ■ ⚠ 줄은 **덱 순서**로 선다 — 누른 순서가 아니다 (사용자 확정, 2026-09-07)
//    하단 카드 바를 끌어 옮겨 순서를 바꾸면(SummonDeckUI 드래그) 그 순서가
//    곧 전장에 나가는 순서다. 앞 칸에 방패를, 뒤 칸에 원거리를 두면 늘
//    방패가 먼저 걸어 나간다 — 급할 때 누른 순서에 진형이 끌려다니지 않는다.
//
//    ⚠ 꺼낼 때 고르지 않고 **넣을 때 자리를 잡는다**
//      TryDequeue 에서 제일 앞 덱 칸을 골라 꺼내면 화면의 대기열(BuildGroups)은
//      여전히 넣은 순서로 보인다 — 보이는 줄과 나오는 줄이 달라진다.
//      목록 자체를 정렬해 두면 둘이 저절로 같아진다.
//
//    ⚠ 덱에 없는 종족은 맨 뒤다
//      진화·융합으로 카드가 사라져도 이미 걸어 둔 예약은 남는다. 그것들이
//      순서를 비집고 앞으로 오면 안 된다.
// ============================================================

/// <summary>화면에 뿌릴 한 줄 — "초상화 × 남은 마릿수".</summary>
public readonly struct SummonQueueGroup
{
    public readonly MonsterSpeciesData Species;
    public readonly int                Count;

    public SummonQueueGroup(MonsterSpeciesData species, int count)
    {
        Species = species;
        Count   = count;
    }
}

public class SummonReservation
{
    /// <summary>한 라인에 보여 줄 최대 줄 수.</summary>
    public const int DisplayLimit = 6;

    // ══════════════════════════════════════════════════════════
    //  ⚠ 대기열에는 **상한이 없다** (사용자 지시, 2026-09-12)
    // ══════════════════════════════════════════════════════════
    //
    //  ■ 옛 MaxPerLane(40)을 통째로 지웠다
    //    이 게임의 정체성은 **물량**이다. 라인에 쌓인 몬스터는 전부 플레이어가
    //    마나를 내고 샀거나, 한 판을 버텨 내고 살아 돌아온 것이다. 그걸 수치
    //    하나로 조용히 지우는 장치는 브레이크가 아니라 데이터 삭제기다.
    //
    //  ■ 상한이 막으려던 진짜 원인은 이미 따로 고쳐졌다
    //    27스테이지 200마리 사건은 중첩 보너스가 **개체마다** 붙어 판마다 두 배가
    //    되던 버그였다(지수). 2026-09-09 에 `(라인, 종족)마다 한 번` 으로 바뀌면서
    //    그 지수 항이 사라졌다 — 지금 남은 증식 경로는 전부 **선형**이다
    //    (귀향 · 숲 금 복귀 · 중첩 보너스).
    //    근본 원인이 고쳐진 뒤로 이 상한은 아무 버그도 막지 않으면서
    //    **정상적으로 쌓은 물량만** 잘라 먹었다.
    //
    //  ■ 실제로 두 번 물어뜯었다 — 둘 다 "조용히 사라진다" 였다
    //      2026-09-11  판이 배출 도중에 끝나면 생존자가 안 돌아왔다
    //      2026-09-12  앱을 껐다 켜면 40을 넘는 몫이 통째로 사라졌다
    //                  (48·47·46 → 전부 40. 40 이하 라인만 멀쩡해 원인이 안 보였다)
    //
    //  ⚠ 어떤 경로에도 상한을 다시 걸지 말 것. 불어나는 것이 문제라면
    //    **불어나게 만든 규칙**을 고칠 것 — 결과를 잘라 내지 말고.

    /// <summary>
    /// 이 수를 넘으면 라인마다 <b>한 번만</b> 경고를 찍는다.
    ///
    /// ⚠ 옛 상한이 하던 일 중 <b>알려 주는 것만</b> 남긴 자리다. 아무것도 버리지 않는다.
    ///   증식 버그가 다시 생기면 조용히 굴러가는 대신 로그로 드러나야 한다.
    /// </summary>
    const int WarnPerLane = 300;

    // 라인별 큐. 각 항목이 몬스터 한 마리다.
    readonly List<MonsterSpeciesData>[] _lanes;

    /// <summary>그 라인에 경고를 이미 찍었는가 (매번 찍으면 로그가 잠긴다).</summary>
    readonly bool[] _warned;

    /// <summary>예약이 바뀔 때마다 발생 — UI 갱신용.</summary>
    public event Action Changed;

    public SummonReservation(int laneCount)
    {
        _lanes   = new List<MonsterSpeciesData>[laneCount];
        _warned  = new bool[laneCount];
        for (int i = 0; i < laneCount; i++)
            _lanes[i] = new List<MonsterSpeciesData>();
    }

    /// <summary>
    /// 라인이 비정상적으로 불어났는지 본다. <b>아무것도 버리지 않는다.</b>
    /// 넣는 문(Enqueue · EnqueueOne)이 전부 지난다.
    /// </summary>
    void NoteSize(int laneIndex)
    {
        if (_warned[laneIndex] || _lanes[laneIndex].Count < WarnPerLane) return;

        _warned[laneIndex] = true;
        Debug.LogWarning(
            $"[SummonReservation] {laneIndex + 1}번 라인 대기열이 {_lanes[laneIndex].Count}마리입니다 — " +
            "불어나는 규칙(귀향·숲 금·중첩 보너스)에 버그가 없는지 보세요. " +
            "⚠ 버리지 않습니다. 물량은 이 게임의 정체성입니다.");
    }

    public int LaneCount => _lanes.Length;

    // ── 예약 ─────────────────────────────────────────────────

    /// <summary>
    /// 카드 한 번 사용분을 그 라인 큐 뒤에 붙인다.
    /// SummonCount 마리이므로 그 수만큼 항목이 쌓인다.
    /// </summary>
    /// <param name="count">
    /// 실제로 세울 마릿수. 특성(증원·친화 증원)이 얹은 뒤의 값이라
    /// <see cref="MonsterSpeciesData.SummonCount"/> 보다 클 수 있다 —
    /// 정본은 <see cref="RunPerkRule.SummonCountFor"/> 이고 부르는 쪽이 계산해 넘긴다.
    /// </param>
    public void Enqueue(MonsterSpeciesData species, int laneIndex, int count)
    {
        List<MonsterSpeciesData> lane = _lanes[laneIndex];
        int at = InsertIndex(lane, species);

        // ⚠ 상한은 없다 (파일 머리 주석) — 한때 여기에 걸었더니 라인이 찬 뒤로
        //   "카드에는 8마리라고 적혀 있는데 6마리만 선다" 가 되고, 가득 차 있으면
        //   **마나만 내고 아무것도 안 나왔다.**
        for (int i = 0; i < Mathf.Max(1, count); i++)
            lane.Insert(at++, species);

        NoteSize(laneIndex);
        Changed?.Invoke();
    }

    /// <summary>종족 정의 그대로의 마릿수로 세운다 (특성 미반영 — 이어하기 복원용).</summary>
    public void Enqueue(MonsterSpeciesData species, int laneIndex)
        => Enqueue(species, laneIndex, species.SummonCount);

    /// <summary>
    /// 딱 한 마리만 줄 뒤에 붙인다.
    ///
    /// ■ 전투에서 살아남아 돌아온 개체가 쓰는 문이다 (MonsterLineReturner)
    ///   Enqueue 는 카드 한 번 사용분(SummonCount 마리)을 통째로 넣는다.
    ///   살아 돌아온 것은 '한 마리' 지 '카드 한 장' 이 아니므로,
    ///   그쪽을 부르면 슬라임 하나가 살아 돌아올 때마다 여섯 마리로 불어난다.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>상한 인자(capped)는 없앴다</b> (사용자 지시, 2026-09-12 — 파일 머리 주석).
    ///   기본값이 "자른다" 였던 탓에 부르는 쪽이 잊을 때마다 몬스터가 조용히 사라졌다.
    ///   두 번 겪고 나서 인자 자체를 지웠다 — 잊을 수 있는 안전장치는 안전장치가 아니다.
    /// </remarks>
    public void EnqueueOne(MonsterSpeciesData species, int laneIndex)
    {
        List<MonsterSpeciesData> lane = _lanes[laneIndex];

        lane.Insert(InsertIndex(lane, species), species);

        NoteSize(laneIndex);
        Changed?.Invoke();
    }

    // ── 덱 순서 ──────────────────────────────────────────────

    /// <summary>
    /// 덱에서 그 종족이 앉은 칸 번호. 덱에 없으면 <see cref="int.MaxValue"/> — 맨 뒤로 간다.
    ///
    /// ⚠ 정본은 SummonDeckData 다. 여기에 순서를 따로 들고 있지 않는다 —
    ///   두 벌이 되면 카드를 끌어 옮긴 순간 둘이 갈린다.
    /// </summary>
    static int DeckOrderOf(MonsterSpeciesData species)
    {
        if (species == null) return int.MaxValue;

        var deck = UserDataManager.Instance?.Get<SummonDeckData>();
        if (deck == null) return int.MaxValue;

        int index = deck.IndexOf(species.Id);
        return index >= 0 ? index : int.MaxValue;
    }

    /// <summary>
    /// 덱 순서를 지키면서 이 종족이 들어갈 자리.
    ///
    /// ⚠ 같은 칸 번호끼리는 <b>뒤에</b> 붙인다 (안정 정렬)
    ///   같은 종족을 두 번 걸었을 때 나중 것이 앞으로 새치기하면,
    ///   화면의 대기열 묶음이 이유 없이 다시 그려진다.
    /// </summary>
    static int InsertIndex(List<MonsterSpeciesData> lane, MonsterSpeciesData species)
    {
        int order = DeckOrderOf(species);

        for (int i = 0; i < lane.Count; i++)
            if (DeckOrderOf(lane[i]) > order) return i;

        return lane.Count;
    }

    /// <summary>
    /// 이미 서 있는 줄을 지금 덱 순서로 다시 세운다.
    ///
    /// ■ 카드를 끌어 옮기면 부른다 (SummonDeckUI)
    ///   판 중에 순서를 바꿨는데 이미 걸어 둔 예약이 옛 순서로 나가면,
    ///   "바꿨는데 왜 그대로지" 가 된다. 마나를 낸 것은 그대로 두고 순서만 고친다.
    ///
    /// ⚠ 안정 정렬이어야 한다 — List.Sort 는 아니다
    ///   그쪽은 불안정이라 같은 종족 묶음의 내부 순서가 뒤섞인다. 눈에 보이는
    ///   차이는 없지만 세이브(LaneQueue)와 화면이 매번 달라져 디버깅이 어렵다.
    /// </summary>
    public void Resort()
    {
        bool changed = false;

        foreach (List<MonsterSpeciesData> lane in _lanes)
        {
            if (lane.Count < 2) continue;

            _sortBuffer.Clear();
            _sortBuffer.AddRange(lane);
            lane.Clear();

            foreach (MonsterSpeciesData s in _sortBuffer)
                lane.Insert(InsertIndex(lane, s), s);

            changed = true;
        }

        if (changed) Changed?.Invoke();
    }

    /// <summary>Resort 전용 재사용 버퍼 (메인 스레드 전용).</summary>
    static readonly List<MonsterSpeciesData> _sortBuffer = new(32);

    /// <summary>
    /// 그 라인의 맨 앞 한 마리를 꺼낸다. 비어 있으면 false.
    /// 라인별 배출 루프가 한 마리 간격마다 부른다 (SpawnPaceRule).
    ///
    /// ⚠ 여기서 순서를 고르지 않는다 — 줄은 이미 덱 순서로 서 있다
    ///   (파일 머리 주석). 맨 앞을 그대로 꺼내야 화면의 대기열과 같아진다.
    /// </summary>
    public bool TryDequeue(int laneIndex, out MonsterSpeciesData species)
    {
        List<MonsterSpeciesData> lane = _lanes[laneIndex];

        if (lane.Count == 0)
        {
            species = null;
            return false;
        }

        species = lane[0];
        lane.RemoveAt(0);
        Changed?.Invoke();
        return true;
    }

    public bool HasAny(int laneIndex) => _lanes[laneIndex].Count > 0;

    /// <summary>
    /// 그 라인의 대기 목록을 <b>넣은 순서 그대로</b> 읽는다.
    /// 세이브(SummonQueueData.Capture)가 쓰는 창구다 — 표시용 묶음(BuildGroups)은
    /// 종족끼리 합쳐 버려서 순서를 되살릴 수 없다.
    /// </summary>
    public IReadOnlyList<MonsterSpeciesData> LaneQueue(int laneIndex) => _lanes[laneIndex];

    /// <summary>그 라인에 남은 총 마릿수.</summary>
    public int RemainingCount(int laneIndex) => _lanes[laneIndex].Count;

    public void Clear()
    {
        bool had = false;
        foreach (List<MonsterSpeciesData> lane in _lanes)
        {
            if (lane.Count == 0) continue;
            lane.Clear();
            had = true;
        }

        // ⚠ 경고 기록도 함께 지운다 — 안 지우면 한 런에서 한 번 찍힌 뒤
        //   다음 런·이어하기 복원에서는 정말로 불어나도 조용하다.
        Array.Clear(_warned, 0, _warned.Length);

        if (had) Changed?.Invoke();
    }

    // ── 표시용 묶음 ──────────────────────────────────────────

    /// <summary>
    /// 한 라인의 대기 목록을 종족끼리 묶어 "초상화 × 남은 마릿수" 로 만든다.
    ///
    /// 입력 순서(그 종족이 그 라인에 처음 걸린 순서)를 지키고
    /// DisplayLimit 줄까지만 낸다.
    ///
    /// ⚠ 연속된 것만 묶지 않는다
    ///   슬라임 → 늑대 → 슬라임 순으로 걸어도 슬라임은 한 줄(×합계)이다.
    ///   화면에 필요한 건 "무엇이 몇 마리 남았나" 지 배열 순서가 아니다.
    /// </summary>
    public List<SummonQueueGroup> BuildGroups(int laneIndex)
    {
        var order  = new List<MonsterSpeciesData>(DisplayLimit);
        var counts = new Dictionary<MonsterSpeciesData, int>();

        foreach (MonsterSpeciesData species in _lanes[laneIndex])
        {
            if (counts.TryGetValue(species, out int n))
            {
                counts[species] = n + 1;
                continue;
            }

            // 새 종족 — 표시 한도를 넘으면 더 담지 않는다.
            if (order.Count >= DisplayLimit) continue;

            order.Add(species);
            counts[species] = 1;
        }

        var groups = new List<SummonQueueGroup>(order.Count);
        foreach (MonsterSpeciesData species in order)
            groups.Add(new SummonQueueGroup(species, counts[species]));

        return groups;
    }
}
