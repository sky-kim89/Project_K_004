using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MonsterCodexData.cs
//  몬스터 도감 — 해금 상태와 종족별 영구 품질을 담는 세이브 섹션.
//
//  ■ 이 도감은 "소환 덱 편성 화면" 그 자체다
//    원작 Codex 는 "지금까지 뭘 만나 봤나" 를 세는 기록이었다.
//    이 도감은 거기에 더해 편성 풀 역할을 한다 — 여기 해금된 종족만
//    소환 덱에 넣을 수 있다. 화면을 둘로 나누지 않는다.
//
//  ■ 해금은 조건부 결정적이다 (확률 뽑기 아님)
//    "Hero Warlord N회 처치", "이벤트 선택지 X", "상점에서 소환 정수로 구매" 처럼
//    종족마다 고유 조건이 있다. 조건을 채우면 반드시 열린다.
//
//  ■ 품질은 "도감 개체당 영구" 다 (확정 사양)
//    해금하는 순간 임의로 배정되어 그 종족의 품질로 고정되고,
//    "품질 개선" 시스템으로만 올라간다.
//    그 종족으로 소환되는 모든 개체가 같은 품질을 쓴다.
//
//    ⚠ 소환할 때마다 굴리는 방식이 아니다.
//      개체마다 품질이 흔들리면 "품질 개선" 이 무엇을 올리는지 설명할 수 없고,
//      덱 편성 시점에 전력을 가늠할 수도 없다.
//
//  ■ 환생해도 남는다 (영구)
//    SetDefaults 는 신규 시작에서만 의미가 있다.
//    UserDataManager.Reincarnate 는 이 섹션을 건드리지 않는다.
//
//  ■ ⚠ 수집 버프는 없다 (사용자 확정, 2026-09-06)
//    "몇 종 모았나" 로 공격력·체력이 오르지 않는다. 해금은 **선택지**를 늘리고,
//    세기는 **품질 개선**으로만 산다 (MonsterGradeUpgradeRule).
// ============================================================

[Serializable]
class MonsterCodexEntryJson
{
    public string id;      // MonsterSpeciesData.Id
    public int    grade;   // UnitGrade — 해금 시 배정된 영구 품질
}

[Serializable]
class MonsterCodexJson
{
    public List<MonsterCodexEntryJson> entries = new();

    // 이번 여정에 적용 중인 수집 종수. -1 = 잠기지 않음(실시간 값을 쓴다).
    public int lockedCount = -1;
}

public class MonsterCodexData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.MonsterCodex;

    readonly Dictionary<string, UnitGrade> _unlocked = new();
    int _lockedCount = -1;

    // ── 조회 ─────────────────────────────────────────────────

    public bool IsUnlocked(string speciesId) => _unlocked.ContainsKey(speciesId);

    /// <summary>해금된 종족 수.</summary>
    public int UnlockedCount => _unlocked.Count;

    /// <summary>편성 가능한 종족 ID 목록.</summary>
    public IEnumerable<string> UnlockedSpeciesIds => _unlocked.Keys;

    /// <summary>
    /// 그 종족의 영구 품질. 해금되지 않았으면 예외가 난다 —
    /// 해금 여부를 먼저 확인하는 게 호출 측 책임이다.
    /// </summary>
    public UnitGrade GetGrade(string speciesId) => _unlocked[speciesId];

    // ── 해금 / 품질 개선 ─────────────────────────────────────

    /// <summary>
    /// 종족을 해금하고 품질을 임의 배정한다. 이미 해금돼 있으면 아무것도 하지 않는다
    /// (재해금으로 품질이 다시 굴려지면 안 된다).
    /// </summary>
    public void Unlock(string speciesId, UnitGrade grade)
    {
        if (_unlocked.ContainsKey(speciesId)) return;
        _unlocked[speciesId] = grade;
    }

    /// <summary>
    /// 품질을 한 단계 올린다. 이미 Epic 이면 false 를 반환한다.
    /// </summary>
    public bool ImproveGrade(string speciesId)
    {
        UnitGrade current = _unlocked[speciesId];
        if (current >= UnitGrade.Epic) return false;

        _unlocked[speciesId] = current + 1;
        return true;
    }

    // ⚠ 수집 버프(LockForRun/CollectionBonus)는 폐기됐다 (사용자 확정, 2026-09-06)
    //   도감을 채운다고 공격력·체력이 오르지 않는다. 도감의 값어치는
    //   **품질 개선**(MonsterGradeUpgradeRule)이 전부 갖는다.
    //   lockedCount 필드는 옛 세이브를 읽기 위해 JSON 에만 남아 있다.

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize()
    {
        var json = new MonsterCodexJson { lockedCount = _lockedCount };
        foreach (var (id, grade) in _unlocked)
            json.entries.Add(new MonsterCodexEntryJson { id = id, grade = (int)grade });

        return JsonUtility.ToJson(json);
    }

    public void Deserialize(string json)
    {
        var data = JsonUtility.FromJson<MonsterCodexJson>(json);

        _unlocked.Clear();
        foreach (var e in data.entries)
            _unlocked[e.id] = (UnitGrade)e.grade;

        _lockedCount = data.lockedCount;
    }

    /// <summary>신규 시작 전용. 환생은 이 섹션을 건드리지 않는다 (영구 데이터).</summary>
    public void SetDefaults()
    {
        _unlocked.Clear();
        _lockedCount = -1;
    }
}
