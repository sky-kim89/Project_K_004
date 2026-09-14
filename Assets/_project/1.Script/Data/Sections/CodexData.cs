using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  CodexData.cs
//  도감 — 한 번이라도 만나 본 런 특성(RunPerk)을 기록하는 세이브 섹션.
//
//  ■ 런 초기화 없음 — 영구 저장 (RelicTreeData 와 같은 성격)
//    "이번 런에 뭘 들고 있나" 가 아니라 "지금까지 뭘 만나 봤나" 를 센다.
//
//  ■ ⚠ 수집 버프는 폐기됐다 (사용자 확정, 2026-09-06)
//    그 버프가 걸리던 대상(General)은 이 게임에서 적(용사)이었다.
//    도감의 값어치는 몬스터 탭의 **품질 개선**이 갖는다 (MonsterCodexData).
//
//  ⚠ 원작 장비·어빌리티·특성·장수 기록은 걷어냈다 — 옛 세이브의 그 필드는 읽을 때 무시된다.
// ============================================================

[Serializable]
class CodexJson
{
    /// <summary>만나 본 런 특성(RunPerk) — 도감 '특성' 탭이 읽는다.</summary>
    public List<int> perks = new();
}

public class CodexData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.Codex;

    /// <summary>
    /// 한 번이라도 얻은 런 특성 (RunPerk) — 도감 '특성' 탭의 정본이다.
    /// 기록은 RunPerkData.Add 가 한다 (지금 런에 쥔 특성도 본 것으로 친다).
    /// </summary>
    readonly HashSet<RunPerk> _perks = new();

    public bool HasPerk(RunPerk p) => _perks.Contains(p);

    /// <summary>특성을 얻었다 — RunPerkData.Add 가 부른다.</summary>
    public static void RecordPerk(RunPerk p) => Current?.AddPerk(p);

    public void AddPerk(RunPerk p) { if (p != RunPerk.None && _perks.Add(p)) Changed(); }

    /// <summary>새 항목이 등록될 때 발행. 도감 화면이 구독한다.</summary>
    public static event Action OnCodexChanged;

    /// <summary>세이브가 아직 없는 시점(스플래시 등)에는 null 이다.</summary>
    public static CodexData Current => UserDataManager.Instance?.Get<CodexData>();

    void Changed()
    {
        UserDataManager.Instance?.RequestSave();
        OnCodexChanged?.Invoke();
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize()
    {
        var json = new CodexJson();
        foreach (var p in _perks) json.perks.Add((int)p);
        return JsonUtility.ToJson(json);
    }

    public void Deserialize(string jsonStr)
    {
        SetDefaults();
        if (string.IsNullOrEmpty(jsonStr)) return;

        var json = JsonUtility.FromJson<CodexJson>(jsonStr);

        // ⚠ 옛 세이브에는 perks 가 없다 — JsonUtility 가 null 로 둘 수 있다
        if (json?.perks != null)
            foreach (var p in json.perks) _perks.Add((RunPerk)p);
    }

    public void SetDefaults() => _perks.Clear();
}
