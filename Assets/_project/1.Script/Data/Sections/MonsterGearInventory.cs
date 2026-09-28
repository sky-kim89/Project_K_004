using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MonsterGearInventory.cs
//  몬스터 장비의 보유·장착 상태. 세이브 섹션.
//
//  ■ ⚠ 영구 데이터다 — 환생해도 남는다 (사용자 확정, 2026-09-06)
//    얻는 시점이 **런 종료**라 이미 메타 경계다. 런 스코프로 두면 런이
//    끝나는 순간 받은 보상이 다음 런 시작과 함께 사라져, "언제 쓰라는 건가" 가 된다.
//    MonsterCodexData 와 같은 성격이므로 UserDataManager.Reincarnate 의
//    초기화 목록에 **넣지 말 것**.
//
//  ■ ⚠ 장비 하나는 **한 마리만** 낀다 (사용자 확정, 2026-09-10)
//    같은 장비를 또 얻으면 **개수만 오르고, 늘어난 수는 전부 강화 재료다.**
//    그래서 "보유 목록" 이 아니라 **Id → 개수**다 — 개수 1 이 장비 본체, 나머지가 여분.
//    ⚠ 다른 종족이 낀 장비를 고르면 그 종족에게서 **벗겨 옮긴다** (TryEquip).
//      막아 두면 "왜 안 끼워지지" 를 매번 찾으러 가야 한다.
//
//  ■ 레벨·강화도 여기 저장한다 (규칙은 MonsterGearLevelRule)
//    재료로 쓸 수 있는 수 = SpareCount = 보유 수 − 1 (본체는 안 먹는다).
//
//  ■ 장착은 종족 단위다
//    필드의 개체가 아니라 **도감의 종족**에 끼운다. 슬라임 카드로 몇 마리를
//    부르든 전부 같은 장비를 걸치고 나온다 — 개체마다 다르면 라인에 선
//    같은 종족이 제각각 보여 무엇이 장비 효과인지 읽을 수 없다.
//
//  ■ 부위는 겹쳐 낄 수 있다 (사용자 확정, 2026-09-15)
//    투구 둘·갑옷 셋처럼 같은 부위를 칸이 허락하는 만큼 함께 낀다 — 능력치는 전부 받는다.
//    겉모습은 그 부위의 **앞 칸 장비**가 정한다 (MonsterGearRule.BuildVisual).
//    ⚠ 예전 규칙("같은 부위를 또 끼우면 먼저 낀 것이 벗겨진다")은 폐기했다.
//
//  ⚠ 칸 수는 여기서 정하지 않는다 — MonsterGearRule.SlotsOf 가 정본이다
//    품질이 오르면 칸이 늘고, 이 섹션은 그때 이미 들고 있던 목록을 그대로 쓴다.
//    (품질은 내려가지 않으므로 넘치는 일이 없다)
// ============================================================

[Serializable]
class MonsterGearOwnedJson
{
    public string id;
    public int    count;

    // ⚠ 옛 세이브에는 없다 — 0 으로 읽힌다. level 0 은 Lv1 로 받는다 (Deserialize).
    public int    level;
    public int    enhance;
}

[Serializable]
class MonsterGearEquipJson
{
    public string       speciesId;
    public List<string> gearIds = new();
}

[Serializable]
class MonsterGearJson
{
    public List<MonsterGearOwnedJson> owned    = new();
    public List<MonsterGearEquipJson> equipped = new();
}

public class MonsterGearInventory : ISaveSection
{
    public SaveKey SaveKey => SaveKey.MonsterGear;

    readonly Dictionary<string, int>          _owned    = new();
    readonly Dictionary<string, List<string>> _equipped = new();

    // 없으면 Lv1 · +0 이다. 기본값은 저장하지 않는다.
    readonly Dictionary<string, int>          _levels   = new();
    readonly Dictionary<string, int>          _enhance  = new();

    static readonly List<string> Empty = new();

    /// <summary>보유·장착이 바뀌었다. 열려 있는 화면이 다시 그리도록 구독한다.</summary>
    public static event Action OnChanged;

    // ── 보유 ─────────────────────────────────────────────────

    /// <summary>장비 하나를 얻는다. 보상 상자만 부른다.</summary>
    public void Add(string gearId)
    {
        if (string.IsNullOrEmpty(gearId)) return;

        _owned[gearId] = OwnedCount(gearId) + 1;
        Changed();
    }

    public int OwnedCount(string gearId)
        => !string.IsNullOrEmpty(gearId) && _owned.TryGetValue(gearId, out int n) ? n : 0;

    /// <summary>이 장비를 끼고 있는 종족. 아무도 안 끼었으면 null. 장비 하나는 한 마리만 낀다.</summary>
    public string WearerOf(string gearId)
    {
        foreach (var (speciesId, list) in _equipped)
            for (int i = 0; i < list.Count; i++)
                if (list[i] == gearId) return speciesId;
        return null;
    }

    /// <summary>보유한 장비 Id 전부 (개수와 무관하게 한 번씩).</summary>
    public IEnumerable<string> OwnedIds => _owned.Keys;

    /// <summary>하나라도 갖고 있나. 목록이 비었는지 확인할 때 쓴다.</summary>
    public bool HasAny => _owned.Count > 0;

    // ── 레벨 · 강화 ──────────────────────────────────────────

    /// <summary>그 장비 종류의 레벨. 없으면 1.</summary>
    public int LevelOf(string gearId)
        => !string.IsNullOrEmpty(gearId) && _levels.TryGetValue(gearId, out int lv) ? lv : 1;

    /// <summary>그 장비 종류의 강화 수치 (Lv5 뒤). 없으면 0.</summary>
    public int EnhanceOf(string gearId)
        => !string.IsNullOrEmpty(gearId) && _enhance.TryGetValue(gearId, out int n) ? n : 0;

    /// <summary>
    /// 레벨업·강화 재료로 먹일 수 있는 수 = 보유 − 1.
    /// ⚠ 마지막 한 개(본체)는 안 먹는다 — 먹으면 장비가 통째로 사라진다.
    /// </summary>
    public int SpareCount(string gearId) => Mathf.Max(0, OwnedCount(gearId) - 1);

    /// <summary>여분을 먹는다. 모자라면 아무것도 안 하고 false. MonsterGearLevelRule 만 부른다.</summary>
    public bool ConsumeSpares(string gearId, int count)
    {
        if (count <= 0)                   return true;
        if (SpareCount(gearId) < count)   return false;

        _owned[gearId] = OwnedCount(gearId) - count;
        Changed();
        return true;
    }

    /// <summary>MonsterGearLevelRule 만 부른다.</summary>
    public void SetLevel(string gearId, int level)
    {
        if (level <= 1) _levels.Remove(gearId);
        else            _levels[gearId] = level;
        Changed();
    }

    /// <summary>MonsterGearLevelRule 만 부른다.</summary>
    public void SetEnhance(string gearId, int enhance)
    {
        if (enhance <= 0) _enhance.Remove(gearId);
        else              _enhance[gearId] = enhance;
        Changed();
    }

    // ── 장착 ─────────────────────────────────────────────────

    /// <summary>그 종족이 끼고 있는 장비 Id 목록 (칸 순서).</summary>
    public IReadOnlyList<string> EquippedOn(string speciesId)
        => !string.IsNullOrEmpty(speciesId) && _equipped.TryGetValue(speciesId, out var list)
            ? list : Empty;

    /// <summary>그 종족이 이 장비를 끼고 있나.</summary>
    public bool IsEquippedOn(string speciesId, string gearId)
    {
        var list = EquippedOn(speciesId);
        for (int i = 0; i < list.Count; i++)
            if (list[i] == gearId) return true;
        return false;
    }

    /// <summary>
    /// 왜 못 끼우는가. 버튼을 끄기만 하면 이유를 알 수 없다.
    /// </summary>
    public enum Blocked
    {
        None = 0,
        NotOwned,     // 한 개도 없다
        WrongBody,    // 몸 형태가 안 맞는다
        NoSlot,       // 칸이 다 찼다 (품질을 올려야 한다)
        AlreadyWorn,  // 이 종족이 이미 끼고 있다
    }

    /// <summary>
    /// ⚠ 다른 종족이 끼고 있어도 막지 않는다 — TryEquip 이 벗겨 옮긴다.
    /// </summary>
    public Blocked Check(MonsterSpeciesData species, MonsterGearData gear)
    {
        if (species == null || gear == null) return Blocked.WrongBody;
        if (OwnedCount(gear.Id) <= 0)        return Blocked.NotOwned;
        if (!gear.Fits(species))             return Blocked.WrongBody;
        if (IsEquippedOn(species.Id, gear.Id)) return Blocked.AlreadyWorn;

        return EquippedOn(species.Id).Count < MonsterGearRule.SlotsOf(species.Id)
             ? Blocked.None
             : Blocked.NoSlot;
    }

    /// <summary>
    /// 빈 칸에 장착한다. 같은 부위를 이미 끼고 있어도 겹쳐 낀다.
    /// 다른 종족이 끼고 있었으면 <b>그 종족에게서 벗겨 온다</b> — 장비 하나는 한 마리만 낀다.
    /// 조건이 안 맞으면 아무것도 하지 않고 false.
    /// </summary>
    public bool TryEquip(MonsterSpeciesData species, MonsterGearData gear)
    {
        if (Check(species, gear) != Blocked.None) return false;

        string prev = WearerOf(gear.Id);
        if (prev != null) RemoveFrom(prev, gear.Id);

        if (!_equipped.TryGetValue(species.Id, out var list))
            _equipped[species.Id] = list = new List<string>(MonsterGearRule.MaxSlots);

        list.Add(gear.Id);

        Changed();
        return true;
    }

    /// <summary>
    /// 고른 칸에 끼울 수 있나 — 칸에 무엇이 있든 막지 않는다 (EquipAt 이 벗긴다).
    /// ⚠ 몬스터 상세의 장비 목록은 이 판정을 쓴다 (사용자 지시, 2026-09-12 —
    ///   "무엇을 끼고 있든 장착을 누르면 기존 것을 벗기고 끼운다").
    /// </summary>
    public Blocked CheckAt(MonsterSpeciesData species, MonsterGearData gear, int slot)
    {
        if (species == null || gear == null) return Blocked.WrongBody;
        if (OwnedCount(gear.Id) <= 0)        return Blocked.NotOwned;
        if (!gear.Fits(species))             return Blocked.WrongBody;
        if (IsEquippedOn(species.Id, gear.Id)) return Blocked.AlreadyWorn;

        return slot >= 0 && slot < MonsterGearRule.SlotsOf(species.Id) ? Blocked.None : Blocked.NoSlot;
    }

    /// <summary>
    /// 그 칸에 끼운다 — 칸에 있던 것은 벗겨진다. 다른 종족이 끼고 있었으면 벗겨 온다.
    /// 같은 부위가 다른 칸에 있어도 그대로 둔다 — 부위는 겹쳐 낀다.
    /// </summary>
    public bool EquipAt(MonsterSpeciesData species, MonsterGearData gear, int slot)
    {
        if (CheckAt(species, gear, slot) != Blocked.None) return false;

        string prev = WearerOf(gear.Id);
        if (prev != null) RemoveFrom(prev, gear.Id);

        if (!_equipped.TryGetValue(species.Id, out var list))
            _equipped[species.Id] = list = new List<string>(MonsterGearRule.MaxSlots);

        if (slot < list.Count) list[slot] = gear.Id;
        else                   list.Add(gear.Id);

        Changed();
        return true;
    }

    /// <summary>벗긴다. 안 끼고 있었으면 false.</summary>
    public bool Unequip(string speciesId, int slotIndex)
    {
        if (!_equipped.TryGetValue(speciesId, out var list)) return false;
        if (slotIndex < 0 || slotIndex >= list.Count)        return false;

        list.RemoveAt(slotIndex);
        if (list.Count == 0) _equipped.Remove(speciesId);

        Changed();
        return true;
    }

    /// <summary>그 종족의 목록에서 장비 하나를 뺀다 (옮겨 끼울 때). 알림은 부르는 쪽이 한다.</summary>
    void RemoveFrom(string speciesId, string gearId)
    {
        if (!_equipped.TryGetValue(speciesId, out var list)) return;

        list.Remove(gearId);
        if (list.Count == 0) _equipped.Remove(speciesId);
    }

    void Changed()
    {
        UserDataManager.Instance?.RequestSave();
        OnChanged?.Invoke();
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize()
    {
        var json = new MonsterGearJson();

        foreach (var (id, count) in _owned)
            json.owned.Add(new MonsterGearOwnedJson
            {
                id      = id,
                count   = count,
                level   = LevelOf(id),
                enhance = EnhanceOf(id),
            });

        foreach (var (speciesId, list) in _equipped)
        {
            var entry = new MonsterGearEquipJson { speciesId = speciesId };
            entry.gearIds.AddRange(list);
            json.equipped.Add(entry);
        }

        return JsonUtility.ToJson(json);
    }

    public void Deserialize(string jsonStr)
    {
        SetDefaults();
        if (string.IsNullOrEmpty(jsonStr)) return;

        var json = JsonUtility.FromJson<MonsterGearJson>(jsonStr);
        if (json == null) return;

        if (json.owned != null)
            foreach (var o in json.owned)
            {
                if (string.IsNullOrEmpty(o.id) || o.count <= 0) continue;

                _owned[o.id] = o.count;

                // 옛 세이브는 0 — Lv1 · +0 이다
                int lv = Mathf.Clamp(o.level, 1, MonsterGearLevelRule.MaxLevel);
                if (lv > 1) _levels[o.id] = lv;

                if (o.enhance > 0) _enhance[o.id] = o.enhance;
            }

        if (json.equipped == null) return;

        // ⚠ 옛 세이브는 같은 장비를 여러 종족이 끼고 있을 수 있다 (한 마리 규칙 전)
        //   먼저 읽힌 종족만 남기고 나머지에서는 벗긴다 — 사본은 재료로 남는다.
        var seen = new HashSet<string>();

        foreach (var e in json.equipped)
        {
            if (e == null || string.IsNullOrEmpty(e.speciesId) || e.gearIds == null) continue;

            var list = new List<string>(e.gearIds.Count);
            foreach (string id in e.gearIds)
                if (!string.IsNullOrEmpty(id) && seen.Add(id)) list.Add(id);

            if (list.Count > 0) _equipped[e.speciesId] = list;
        }
    }

    public void SetDefaults()
    {
        _owned.Clear();
        _equipped.Clear();
        _levels.Clear();
        _enhance.Clear();
    }
}
