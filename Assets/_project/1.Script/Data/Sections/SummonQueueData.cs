using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SummonQueueData.cs
//  라인별 소환 대기열 — 런 스코프 세이브 섹션.
//
//  ■ 왜 저장해야 하나
//    마나는 **예약하는 순간** 빠진다(SummonReservation 참고). 취소도 없다.
//    그런데 대기열 자체는 메모리에만 있어서, 스테이지 대기 중에 앱을 껐다
//    켜면 마나만 사라지고 몬스터는 오지 않았다. 이어하기가 곧 손실이 되는
//    구조라 반드시 함께 저장해야 한다.
//
//  ■ 판이 끝난 뒤의 생존자도 여기 들어온다
//    MonsterLineReturner 가 살아남은 개체를 라인 대기열로 되돌린다.
//    그 결과가 저장되지 않으면 "돌아온 몬스터" 도 재시작으로 증발한다.
//
//  ■ 저장 형태 — 평평한 (라인, 종족ID) 목록
//    JsonUtility 는 중첩 리스트(List&lt;List&lt;string&gt;&gt;)를 직렬화하지 못한다.
//    한 줄에 라인 번호를 함께 적고 **넣은 순서 그대로** 늘어놓는다.
//    복원할 때 앞에서부터 다시 넣으면 라인별 순서가 그대로 살아난다.
//
//  ■ ⚠ 개체 단위다 — 카드 한 장이 아니다
//    슬라임(6마리) 한 장을 걸면 여섯 줄이 쌓인다. 복원도 개체 단위로 한다
//    (SummonReservation.EnqueueOne). Enqueue 로 되돌리면 한 마리가
//    여섯 마리로 불어난다.
//
//  ■ 환생으로 사라진다 (런 스코프)
// ============================================================

[Serializable]
class SummonQueueEntryJson
{
    public int    lane;
    public string id = string.Empty;
}

[Serializable]
class SummonQueueJson
{
    public List<SummonQueueEntryJson> entries = new();
}

public class SummonQueueData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.SummonQueue;

    SummonQueueJson _raw = new();

    /// <summary>저장된 대기 개체가 하나라도 있는가.</summary>
    public bool HasAny => _raw.entries.Count > 0;

    // ── 기록 ─────────────────────────────────────────────────

    /// <summary>
    /// 지금 대기열을 통째로 받아 적는다. 예약이 바뀔 때마다 부른다
    /// (SummonController 가 SummonReservation.Changed 를 받아 부른다).
    ///
    /// 증분으로 관리하지 않는 이유 — 넣기·꺼내기·되돌리기가 세 곳에서 일어난다.
    /// 통째로 다시 적는 편이 어긋날 여지가 없고, 대기열은 길어야 수십 개다.
    /// </summary>
    public void Capture(SummonReservation reservation)
    {
        _raw.entries.Clear();

        for (int lane = 0; lane < reservation.LaneCount; lane++)
        {
            IReadOnlyList<MonsterSpeciesData> queue = reservation.LaneQueue(lane);

            for (int i = 0; i < queue.Count; i++)
                _raw.entries.Add(new SummonQueueEntryJson { lane = lane, id = queue[i].Id });
        }
    }

    /// <summary>
    /// 저장된 대기열을 예약 목록에 되돌린다. 이어하기에서 한 번 부른다.
    ///
    /// ⚠ 반드시 **사본**을 먼저 뜬다 — Capture() 가 쓰는 목록과 같은 객체다
    ///   되돌리는 중에 Capture() 가 한 번이라도 끼면 _raw.entries 가 Clear()
    ///   되면서 순회가 깨진다("Collection was modified"). 실제로 예약 변경마다
    ///   저장하던 시절에 이 예외가 났다. 지금은 저장을 판의 경계에서만 하지만,
    ///   두 함수가 같은 리스트를 읽고 쓴다는 사실은 그대로다 — 사본이 그 아귀를 끊는다.
    ///
    /// ⚠ 예약을 먼저 비운다 — 두 번 부르면 대기열이 두 배가 된다.
    /// 카탈로그에 없는 ID(종족이 삭제된 옛 세이브)는 조용히 버린다.
    ///
    /// ■ ⚠ 저장된 수를 <b>한 마리도 빠짐없이</b> 되돌린다 (사용자 지적, 2026-09-12)
    ///   한때 <c>EnqueueOne</c> 의 옛 상한(MaxPerLane 40)에 걸려, 라인에 40마리를
    ///   넘겨 쌓아 둔 사람이 앱을 껐다 켜면 <b>넘치는 몫이 통째로 사라졌다.</b>
    ///   실제로 48·47·46 마리였던 세 라인이 전부 정확히 40 으로 잘렸다 —
    ///   40 이하였던 라인만 멀쩡해서 "가끔 대기열이 준다" 로 보였다.
    ///   그 상한은 <b>지금 없다</b> (SummonReservation 파일 머리 주석).
    ///
    ///   ⚠ 여기에 어떤 상한도 다시 걸지 말 것. 걸면 "한 판을 잘 버텨 물량을 쌓을수록
    ///     이어하기로 더 많이 잃는" 구조가 된다 — 잘한 사람만 손해를 본다.
    /// </summary>
    public void Restore(SummonReservation reservation, CardCatalog catalog)
    {
        var saved = _raw.entries.ToArray();

        reservation.Clear();

        foreach (SummonQueueEntryJson e in saved)
        {
            if (e.lane < 0 || e.lane >= reservation.LaneCount) continue;

            MonsterSpeciesData species = catalog.GetMonster(e.id);
            if (species == null) continue;

            reservation.EnqueueOne(species, e.lane);
        }
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize() => JsonUtility.ToJson(_raw);

    public void Deserialize(string json)
    {
        _raw = string.IsNullOrEmpty(json)
             ? new SummonQueueJson()
             : JsonUtility.FromJson<SummonQueueJson>(json) ?? new SummonQueueJson();

        _raw.entries ??= new List<SummonQueueEntryJson>();
    }

    public void SetDefaults() => _raw = new SummonQueueJson();

    public void ResetForNewRun() => SetDefaults();
}
