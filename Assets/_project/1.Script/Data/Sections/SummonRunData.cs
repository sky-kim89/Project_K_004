using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SummonRunData.cs
//  진행 중인 런의 **자리표** — 런 스코프 세이브 섹션.
//
//  ■ 여기에 담는 것은 셋뿐이다
//      · 어떤 소환사로 시작했나
//      · 몇 스테이지까지 왔나
//      · 지금 런이 살아 있나
//
//    카드·마나·특성은 여기 없다. 각자 제 섹션이 이미 저장하고 있다 —
//      SummonDeckData  손에 든 카드 (레벨·진화·융합까지)
//      SummonManaData  남은 마나
//      RunPerkData     런 중 얻은 특성
//    그 셋을 다시 적으면 두 벌이 생기고, 언젠가 반드시 어긋난다.
//    이 섹션은 "그 조각들을 어느 판에 다시 세울 것인가" 만 말한다.
//
//  ■ 왜 StageProgressData 를 안 쓰나
//    그쪽 RunInProgress/CurrentRunStage 는 원작 런 구조(상점·이벤트 노드가
//    섞인 고정 시퀀스)의 인덱스다. 이 게임의 런은 끝없이 이어지는 스테이지
//    번호라 의미가 다르다. 같은 칸에 다른 뜻을 담으면 잠금 해제 규칙
//    (ClearedNormal 기반)까지 함께 흔들린다.
//
//  ■ 앱을 껐다 켜도 이어진다
//    LobbyManager 가 시작할 때 InProgress 를 보고 곧장 인게임으로 들어간다.
//    RunBootstrap 은 저장된 스테이지·소환사로 판을 다시 세우되
//    **마나 지급과 시작 카드 배치는 건너뛴다** — 이미 준 것을 또 주면
//    죽을 때까지 앱을 껐다 켜는 것이 회복 수단이 된다.
//
//  ■ 환생으로 사라진다
//    UserDataManager.Reincarnate() 가 지운다. 패배 → 환생이 유일한
//    런 종료 경로이므로, 지워진 뒤에는 소환사 선택 화면부터 다시 시작한다.
// ============================================================

[Serializable]
class SummonRunJson
{
    public string summonerId = string.Empty;
    public int    stage      = 1;
    public bool   inProgress;

    /// <summary>시너지 강화 카드를 어디에 걸었나 (MonsterTag 비트값). 0 = 안 걸었다.</summary>
    public int    boostTag;

    /// <summary>
    /// 이번 런에 이미 만난 이벤트 (RunEventId 번호).
    ///
    /// ⚠ 카드·마나와 달리 여기 담는 것이 맞다 — 제 섹션이 없는 값이고
    ///   런이 끝나면 통째로 사라져야 한다. 이어하기에는 남아야 한다
    ///   (앱을 껐다 켜서 같은 이벤트를 다시 뽑는 길을 막는다).
    /// </summary>
    public List<int> seenEvents = new();
}

public class SummonRunData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.SummonRun;

    SummonRunJson _raw = new();

    // ── 조회 ─────────────────────────────────────────────────

    /// <summary>이어서 시작할 런이 있는가.</summary>
    public bool InProgress => _raw.inProgress;

    /// <summary>진행 중인 런의 소환사 카드 ID.</summary>
    public string SummonerId => _raw.summonerId;

    /// <summary>진행 중인 스테이지 번호 (1부터).</summary>
    public int StageNumber => Mathf.Max(1, _raw.stage);

    /// <summary>
    /// 이어하기가 실제로 성립하는가.
    ///
    /// ⚠ 소환사 ID 가 비었으면 이어하지 않는다
    ///   그 상태로 진행하면 RunBootstrap 이 아무 소환사나 자동으로 집어
    ///   **지난 런의 카드를 엉뚱한 캐릭터가 들고 있는** 판이 된다.
    ///   차라리 선택 화면부터 다시 시작하는 편이 낫다.
    /// </summary>
    public bool CanResume => _raw.inProgress && !string.IsNullOrEmpty(_raw.summonerId);

    // ── 기록 ─────────────────────────────────────────────────

    /// <summary>런을 시작했다.</summary>
    public void Begin(string summonerId, int stageNumber)
    {
        _raw.summonerId = summonerId;
        _raw.stage      = Mathf.Max(1, stageNumber);
        _raw.inProgress = true;
    }

    /// <summary>스테이지를 넘겼다. 여기가 다음에 이어붙일 자리가 된다.</summary>
    public void SetStage(int stageNumber)
    {
        _raw.stage = Mathf.Max(1, stageNumber);
    }

    /// <summary>런이 끝났다 — 이어하기 대상이 아니다.</summary>
    public void End() => _raw.inProgress = false;

    // ── 시너지 강화 ──────────────────────────────────────────

    /// <summary>강화 대상. 0 이면 아직 안 골랐다.</summary>
    public MonsterTag BoostTag => (MonsterTag)_raw.boostTag;

    public void SetBoostTag(MonsterTag tag)
    {
        _raw.boostTag = (int)tag;
        UserDataManager.Instance.RequestSave();
    }

    // ── 만난 이벤트 ──────────────────────────────────────────

    /// <summary>이번 런에 이미 만난 이벤트 번호들. 후보를 거를 때 본다.</summary>
    public IReadOnlyList<int> SeenEvents => _raw.seenEvents;

    /// <summary>이벤트 하나를 만났다고 적는다.</summary>
    public void MarkEventSeen(RunEventId id)
    {
        if (_raw.seenEvents.Contains((int)id)) return;

        _raw.seenEvents.Add((int)id);
        UserDataManager.Instance.RequestSave();
    }

    /// <summary>
    /// 기록을 비운다 — <b>여덟을 다 만났을 때만</b> 부른다.
    ///
    /// ⚠ 후보가 없다고 갈림길을 건너뛰면 그 뒤로 이벤트 칸이 조용히 사라진다.
    ///   다시 돌게 만드는 편이 낫다 (RunEventRule.Pick 머리 주석 참고).
    /// </summary>
    public void ClearSeenEvents()
    {
        if (_raw.seenEvents.Count == 0) return;

        _raw.seenEvents.Clear();
        UserDataManager.Instance.RequestSave();
    }



    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize() => JsonUtility.ToJson(_raw);

    public void Deserialize(string json)
    {
        _raw = string.IsNullOrEmpty(json)
             ? new SummonRunJson()
             : JsonUtility.FromJson<SummonRunJson>(json) ?? new SummonRunJson();

        // ⚠ 옛 세이브에는 이 칸이 없다 — JsonUtility 는 없는 List 를 null 로 둔다.
        _raw.seenEvents ??= new List<int>();
    }

    public void SetDefaults() => _raw = new SummonRunJson();

    public void ResetForNewRun() => SetDefaults();
}
