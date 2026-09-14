using System.Collections.Generic;

// ============================================================
//  RareSkillArbiter.cs
//  희귀 액티브 스킬(ActiveSkillData.IsRare)의 주인을 정하는 할당표.
//
//  ■ 규칙은 하나뿐이다
//    희귀 스킬 하나당 **주인 영웅 한 명이 고정**된다.
//    그 영웅은 등급·부대 구성·런과 무관하게 항상 그 스킬을 쓰고,
//    나머지 영웅은 그 스킬을 절대 가질 수 없다.
//
//  ■ 주인은 어떻게 정하나
//    전체 이름 목록(HeroNamePool.AllNames)에서 그 스킬의 직업에 해당하는 이름만 추려
//    해시가 가장 작은 하나를 고른다. 목록도 해시도 고정이라 결과가 늘 같다.
//    (한 번 계산하고 캐시한다 — 이름 목록은 런타임에 바뀌지 않는다)
//
//  ■ 추첨(ActiveSkillRoller)에는 희귀 스킬이 아예 안 들어간다
//    주인은 여기서 받고, 나머지는 추첨에서 만날 일이 없다. 두 경로가 겹치지 않는다.
// ============================================================

public static class RareSkillArbiter
{
    // 희귀 스킬 → 주인 이름. 첫 조회 때 한 번만 만든다.
    static Dictionary<ActiveSkillId, string> _owners;

    /// <summary>이 영웅이 실제로 쓰는 액티브 스킬. 스폰·UI 모두 이 함수를 쓴다.</summary>
    public static ActiveSkillId Resolve(string unitName, UnitJob job, ActiveSkillDatabase db,
                                        UnitGrade grade = UnitGrade.Normal)
    {
        var owners = GetOwners(db);
        if (owners != null)
            foreach (var pair in owners)
                if (pair.Value == unitName)
                    return pair.Key;

        // 주인이 아니면 희귀 스킬이 빠진 일반 추첨 결과
        return ActiveSkillRoller.Roll(unitName, job, db, grade);
    }

    /// <summary>
    /// 이 영웅이 희귀 스킬 주인인지. 태생 등급·스탯 품질 강제에 쓰인다.
    ///
    /// ⚠ 여기서 UnitJobRoller.GetBirthGrade / Roll 을 부르면 안 된다
    ///   그쪽이 이 함수를 다시 부르므로 무한 재귀가 된다. 직업(GetJob)만 쓴다.
    /// </summary>
    public static bool IsRareOwner(string unitName)
    {
        if (string.IsNullOrEmpty(unitName)) return false;

        var owners = GetOwners(ActiveSkillDatabase.Current);
        if (owners == null) return false;

        foreach (var pair in owners)
            if (pair.Value == unitName) return true;
        return false;
    }

    /// <summary>이 희귀 스킬의 주인 이름. 없으면 null. (UI 표기용)</summary>
    public static string OwnerOf(ActiveSkillId rareSkill, ActiveSkillDatabase db)
    {
        var owners = GetOwners(db);
        return owners != null && owners.TryGetValue(rareSkill, out var name) ? name : null;
    }

    // ── 할당표 생성 ──────────────────────────────────────────

    static Dictionary<ActiveSkillId, string> GetOwners(ActiveSkillDatabase db)
    {
        if (_owners != null) return _owners;
        if (db?.Entries == null || db.Entries.Count == 0) return null;

        var table = new Dictionary<ActiveSkillId, string>();

        foreach (var entry in db.Entries)
        {
            if (entry == null || !entry.IsRare) continue;

            // 직업 제한이 없으면(공통 희귀 스킬) 전 직업이 후보다 — 전체에서 한 명만 갖는다
            string owner = PickOwner(entry.SkillId, entry.AllowedJobs);
            if (owner != null) table[entry.SkillId] = owner;
        }

        _owners = table;
        return _owners;
    }

    // 해당 직업 이름들 중 (이름 해시 ⊕ 스킬 ID) 가 가장 작은 하나.
    // 스킬마다 다른 값이 섞이므로 희귀 스킬이 한 명에게 몰리지 않는다.
    static string PickOwner(ActiveSkillId skillId, UnitJob[] allowedJobs)
    {
        string owner    = null;
        uint   bestHash = uint.MaxValue;

        foreach (string name in HeroNamePool.AllNames)
        {
            if (!JobAllowed(UnitJobRoller.GetJob(name), allowedJobs)) continue;

            uint h = Hash(name) ^ (uint)((int)skillId * 2654435761u);
            if (h > bestHash) continue;
            if (h == bestHash && string.CompareOrdinal(name, owner) >= 0) continue;

            owner    = name;
            bestHash = h;
        }

        return owner;
    }

    // 비어 있으면 제한 없음 = 전 직업 통과 (공통 희귀 스킬)
    static bool JobAllowed(UnitJob job, UnitJob[] allowed)
    {
        if (allowed == null || allowed.Length == 0) return true;

        foreach (var a in allowed)
            if (a == job) return true;
        return false;
    }

    static uint Hash(string text)
    {
        uint hash = 2166136261u;
        if (string.IsNullOrEmpty(text)) return hash;
        foreach (char c in text)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return hash;
    }
}
