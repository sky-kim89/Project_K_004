using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  TutorialData.cs
//  튜토리얼 노출 기록 저장 섹션.
//
//  Completed      : 끝까지 본 튜토리얼 목록. 강제 진행 여부를 이 값으로 가른다.
//  InProgress     : 보다가 끊긴 튜토리얼 (없으면 None).
//  InProgressStep : 그 튜토리얼의 몇 번째 스텝까지 봤는지.
//
//  ■ 왜 중간 지점까지 저장하나
//    강제 진행 튜토리얼은 전투·팝업을 가로지른다. 3번째 스텝에서 앱을 껐다
//    켰을 때 처음부터 다시 시키면, 이미 본 설명을 또 보게 되고 그 사이
//    게임 상태(전투 중이었다 등)가 달라져 시나리오가 어긋난다.
//
//  ⚠ 환생으로 초기화하지 않는다
//    런 데이터가 아니라 "이 사람이 이 게임을 배웠는가" 다.
//    UserDataManager.Reincarnate() 의 초기화 목록에 넣지 말 것 —
//    넣으면 환생할 때마다 첫 튜토리얼이 다시 뜬다.
//
//  ⚠ 완료 처리는 마지막 스텝이 끝났을 때만 한다
//    중간에 건너뛰면(Skip) 완료로 친다. 그래야 다시 안 뜬다.
//    반대로 앱이 죽어서 끊긴 것은 완료가 아니므로 InProgress 로만 남는다.
// ============================================================

[Serializable]
class TutorialDataJson
{
    public List<int> completed = new();
    public int       inProgress;
    public int       inProgressStep;
}

public class TutorialData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.Tutorial;

    readonly HashSet<TutorialId> _completed = new();

    TutorialId _inProgress     = TutorialId.None;
    int        _inProgressStep;

    /// <summary>기록이 바뀔 때 발행. 디버그 화면·치트 창이 구독한다.</summary>
    public static event Action OnChanged;

    // ── 조회 ────────────────────────────────────────────────────

    public bool IsCompleted(TutorialId id) => id != TutorialId.None && _completed.Contains(id);

    /// <summary>보다가 끊긴 튜토리얼. 없으면 None.</summary>
    public TutorialId InProgress     => _inProgress;

    /// <summary>끊긴 지점의 스텝 인덱스. 이어 볼 때 여기서부터 재생한다.</summary>
    public int        InProgressStep => _inProgress == TutorialId.None ? 0 : _inProgressStep;

    /// <summary>
    /// 지금 이 튜토리얼을 강제로 띄워도 되는가.
    /// 이미 봤으면 false — 도움말(i 버튼)은 이걸 보지 않고 Replay 로 직접 연다.
    /// </summary>
    public bool ShouldPlay(TutorialId id) => id.IsForced() && !IsCompleted(id);

    /// <summary>이 튜토리얼을 이어 보는 중인가 (앱이 끊겼다 다시 켜진 경우).</summary>
    public bool IsResuming(TutorialId id) => _inProgress == id && _inProgressStep > 0;

    // ── 기록 ────────────────────────────────────────────────────

    /// <summary>시나리오 시작 — 진행 중 표시를 세운다.</summary>
    public void BeginTutorial(TutorialId id)
    {
        if (id == TutorialId.None) return;
        _inProgress     = id;
        _inProgressStep = 0;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 스텝 하나를 마쳤다. step 은 "다음에 재생할 인덱스" 다.
    ///
    /// ⚠ 진행 중인 튜토리얼이 아니면 무시한다
    ///   도움말 재생(Replay)이 강제 진행 기록을 덮어쓰면 안 된다.
    /// </summary>
    public void SetStep(TutorialId id, int step)
    {
        if (_inProgress != id) return;
        if (_inProgressStep == step) return;
        _inProgressStep = Mathf.Max(0, step);
        OnChanged?.Invoke();
    }

    /// <summary>끝까지 봤거나 건너뛰었다 — 다시 강제로 뜨지 않는다.</summary>
    public void MarkCompleted(TutorialId id)
    {
        if (id == TutorialId.None) return;

        bool added = _completed.Add(id);
        bool cleared = false;
        if (_inProgress == id)
        {
            _inProgress     = TutorialId.None;
            _inProgressStep = 0;
            cleared = true;
        }
        if (added || cleared) OnChanged?.Invoke();
    }

    /// <summary>진행 중 표시만 지운다 (완료로 치지 않음). 시나리오가 중단됐을 때.</summary>
    public void ClearInProgress()
    {
        if (_inProgress == TutorialId.None) return;
        _inProgress     = TutorialId.None;
        _inProgressStep = 0;
        OnChanged?.Invoke();
    }

    // ── 디버그·치트 ─────────────────────────────────────────────

    /// <summary>전부 안 본 것으로 되돌린다 — 치트 창에서 튜토리얼을 다시 볼 때.</summary>
    public void ResetAll()
    {
        _completed.Clear();
        _inProgress     = TutorialId.None;
        _inProgressStep = 0;
        OnChanged?.Invoke();
    }

    /// <summary>하나만 안 본 것으로 되돌린다.</summary>
    public void ResetOne(TutorialId id)
    {
        bool removed = _completed.Remove(id);
        if (_inProgress == id) { _inProgress = TutorialId.None; _inProgressStep = 0; removed = true; }
        if (removed) OnChanged?.Invoke();
    }

    /// <summary>강제 진행 튜토리얼을 전부 봤다고 표시 — 개발 중 건너뛰기용.</summary>
    public void CompleteAllForced()
    {
        foreach (TutorialId id in Enum.GetValues(typeof(TutorialId)))
            if (id.IsForced()) _completed.Add(id);
        _inProgress     = TutorialId.None;
        _inProgressStep = 0;
        OnChanged?.Invoke();
    }

    // ── ISaveSection ────────────────────────────────────────────

    public string Serialize()
    {
        var dto = new TutorialDataJson
        {
            inProgress     = (int)_inProgress,
            inProgressStep = _inProgressStep,
        };
        foreach (var id in _completed) dto.completed.Add((int)id);
        return JsonUtility.ToJson(dto);
    }

    public void Deserialize(string json)
    {
        _completed.Clear();
        _inProgress     = TutorialId.None;
        _inProgressStep = 0;

        if (string.IsNullOrEmpty(json)) return;
        var dto = JsonUtility.FromJson<TutorialDataJson>(json);
        if (dto == null) return;

        // ⚠ enum 에서 사라진 번호는 버린다
        //   폐기한 튜토리얼 기록이 남아 있어도 어디서도 못 읽으니 의미가 없고,
        //   나중에 그 번호를 다시 쓰면 "이미 본 것" 으로 오인된다.
        if (dto.completed != null)
            foreach (int raw in dto.completed)
            {
                var id = (TutorialId)raw;
                if (Enum.IsDefined(typeof(TutorialId), id) && id != TutorialId.None)
                    _completed.Add(id);
            }

        var prog = (TutorialId)dto.inProgress;
        if (Enum.IsDefined(typeof(TutorialId), prog) && prog != TutorialId.None && !_completed.Contains(prog))
        {
            _inProgress     = prog;
            _inProgressStep = Mathf.Max(0, dto.inProgressStep);
        }

        OnChanged?.Invoke();
    }

    public void SetDefaults()
    {
        _completed.Clear();
        _inProgress     = TutorialId.None;
        _inProgressStep = 0;
        OnChanged?.Invoke();
    }
}
