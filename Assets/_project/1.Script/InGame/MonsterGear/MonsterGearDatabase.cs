using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MonsterGearDatabase.cs
//  몬스터 장비 SO 모음. Resources/MonsterGearDatabase.asset 하나가 정본이다.
//
//  ⚠ 굽는 곳은 MonsterGearCreator 하나다
//    그 도구가 Entries 를 통째로 비우고 다시 채운다 (액티브 스킬 DB 와 같은 계약).
//    거기 없는 장비는 다시 구울 때마다 사라진다 — 인스펙터에서 손으로 끼우지 말 것.
//
//  ⚠ Id 는 세이브에 적힌다
//    보유·장착 상태가 Id 문자열로 저장된다 (MonsterGearInventory).
//    이름을 바꾸면 그 장비를 끼고 있던 몬스터가 조용히 맨몸이 된다.
// ============================================================

[CreateAssetMenu(fileName = "MonsterGearDatabase", menuName = "ProjectK/MonsterGearDatabase")]
public class MonsterGearDatabase : ScriptableObject
{
    static MonsterGearDatabase _current;

    public static MonsterGearDatabase Current
        => _current != null ? _current : _current = Resources.Load<MonsterGearDatabase>("MonsterGearDatabase");

    public List<MonsterGearData> Entries = new();

    // Id → SO. 목록을 매번 훑지 않는다 — 칸마다 조회가 일어난다.
    Dictionary<string, MonsterGearData> _byId;

    public MonsterGearData Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (_byId == null || _byId.Count != Entries.Count) Rebuild();

        return _byId.TryGetValue(id, out var found) ? found : null;
    }

    void Rebuild()
    {
        _byId = new Dictionary<string, MonsterGearData>(Entries.Count);
        foreach (var e in Entries)
        {
            if (e == null || string.IsNullOrEmpty(e.Id)) continue;
            _byId[e.Id] = e;
        }
    }

    /// <summary>그 등급·몸 형태에 맞는 장비를 into 에 담는다. 보상 뽑기가 쓴다.</summary>
    public void Collect(UnitGrade grade, MonsterGearBody body, List<MonsterGearData> into)
    {
        foreach (var e in Entries)
        {
            if (e == null) continue;
            if (e.Grade != grade || e.Body != body) continue;
            into.Add(e);
        }
    }
}
