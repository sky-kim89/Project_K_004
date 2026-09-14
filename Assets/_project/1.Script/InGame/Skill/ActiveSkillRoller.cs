// ============================================================
//  ActiveSkillRoller.cs
//  장군 이름 + 직업 + 등급을 시드로 액티브 스킬 1종을 결정하는 정적 클래스.
//
//  알고리즘:
//    FNV-1a 해시로 unitName 을 uint 시드로 변환.
//    직업(UnitJob) 값을 시드에 XOR 하여 같은 이름이라도 직업에 따라 다른 결과 보장.
//    AllowedJobs 에 해당 직업이 포함된(또는 비어있는) 스킬 목록을 필터링한 뒤
//    가중치를 적용하여 LCG 난수로 1개 선택.
//
//  가중치 규칙:
//    공통 스킬(AllowedJobs 비어 있음) = weight 1
//    전용 스킬(AllowedJobs 지정)      = weight 3
//
//  ⚠ 등급은 결과에 영향을 주지 않는다
//    시드·가중치 어디에도 등급을 넣지 않는다. 넣으면 등급업할 때마다 스킬이
//    바뀌어, 키우던 장수가 남이 된다. grade 파라미터는 호출부 호환용으로만 남아 있다.
//
//  ⚠ 희귀 스킬(IsRare)은 이 추첨에 아예 들어오지 않는다
//    주인이 이름으로 고정돼 있다 (RareSkillArbiter 할당표).
//    주인은 거기서 스킬을 받고, 나머지는 여기서 뽑는다 — 두 경로가 겹치지 않는다.
//
//  결정론적 보장:
//    동일 unitName + job + grade 는 항상 동일한 스킬을 반환.
//
//  fallback:
//    ActiveSkillDatabase 가 null 이거나 해당 직업 스킬이 없으면 HeavyStrike 반환.
// ============================================================

public static class ActiveSkillRoller
{
    /// <summary>
    /// unitName, job, grade 를 시드로 액티브 스킬 1종을 결정한다.
    /// db 가 null 이거나 해당 직업에 맞는 스킬이 없으면 HeavyStrike 를 반환.
    /// </summary>
    public static ActiveSkillId Roll(string unitName, UnitJob job, ActiveSkillDatabase db,
        UnitGrade grade = UnitGrade.Normal)
    {
        if (db == null || db.Entries == null || db.Entries.Count == 0)
            return ActiveSkillId.HeavyStrike;

        // ⚠ 등급은 결과에 반영하지 않는다 (grade 파라미터는 호출부 호환용으로만 남긴다)
        //   예전엔 시드와 가중치에 등급을 섞었는데, 그러면 **등급업할 때마다 액티브
        //   스킬이 통째로 바뀌었다.** 키우던 장수가 다른 스킬을 쓰게 되니 성장이 아니라
        //   교체가 된다. 스킬 정체성은 이름에 고정한다.
        const int specificWeight = 3;   // 전용 스킬은 공통 스킬보다 3배 자주

        // 직업에 맞는 스킬 풀 구성 (공통=1회, 전용=weight회 반복)
        var pool = new System.Collections.Generic.List<ActiveSkillId>(db.Entries.Count * specificWeight);
        foreach (var entry in db.Entries)
        {
            if (entry == null || entry.SkillId == ActiveSkillId.None) continue;

            // 희귀 스킬은 주인이 이름으로 고정돼 있다 — 추첨 풀에 아예 넣지 않는다
            if (entry.IsRare) continue;

            // 우두머리 전용 패턴(돌진·분쇄 강타)은 적만 쓴다.
            // AllowedJobs 를 비워 두면 '공통 스킬' 로 잡혀 아군에게 뽑히므로
            // 여기서 명시적으로 걸러야 한다.
            if (entry.SkillId.IsBossPattern()) continue;

            // 소환사 전용 — 용사가 뽑으면 적의 스킬이 플레이어의 몬스터를 부른다.
            if (entry.SkillId.IsSummonerOnly()) continue;

            // 몬스터 종족 전용 — 용사가 뽑으면 플레이어의 몬스터를 치유한다.
            if (entry.SkillId.IsMonsterOnly()) continue;

            bool isCommon = entry.AllowedJobs == null || entry.AllowedJobs.Length == 0;
            if (isCommon)
            {
                pool.Add(entry.SkillId);  // 공통: 가중치 1
                continue;
            }

            foreach (var allowed in entry.AllowedJobs)
            {
                if (allowed != job) continue;

                for (int w = 0; w < specificWeight; w++)
                    pool.Add(entry.SkillId);  // 전용: 가중치 N
                break;
            }
        }

        if (pool.Count == 0)
            return ActiveSkillId.HeavyStrike;

        // unitName + job 으로 결정론적 선택 — 등급은 섞지 않는다 (위 주석 참고)
        uint seed = Fnv1aHash(unitName);
        seed ^= (uint)job * 2654435761u;
        seed  = LcgNext(seed);

        int index = (int)(seed % (uint)pool.Count);
        return pool[index];
    }

    // ── 내부 유틸 ─────────────────────────────────────────────

    static uint Fnv1aHash(string text)
    {
        uint hash = 2166136261u;
        if (string.IsNullOrEmpty(text)) return hash;
        foreach (char c in text)
        {
            hash ^= (uint)c;
            hash *= 16777619u;
        }
        return hash;
    }

    static uint LcgNext(uint seed)
        => seed * 1664525u + 1013904223u;
}
