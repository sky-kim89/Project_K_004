using System.Collections.Generic;
using System.Text;
using UnityEngine;

// ============================================================
//  RareSkillOwnerLog.cs
//  희귀 스킬의 주인이 누구인지 게임 시작 시 한 번 콘솔에 찍는다.
//
//  주인은 이름으로 고정돼 있어서(RareSkillArbiter 할당표) 이 목록은
//  세이브·부대 구성·플레이 횟수와 무관하게 항상 같다.
//  목록이 달라졌다면 이름 풀(HeroNamePool.AllNames)이나 스킬 SO 가 바뀐 것이다.
//
//  ■ 출력 시점
//    RuntimeInitializeOnLoadMethod(AfterSceneLoad) — 실행하자마자 한 번.
//    에디터 플레이·빌드 모두 동작한다. 전투마다 찍히지 않는다.
//
//  ■ 수동 확인
//    Tools > Project K > 도구 > 희귀 스킬 주인 확인 (RareSkillOwnerMenu — Editor 폴더)
//
//  ⚠ 메뉴를 이 파일에 두지 않는다
//    ProjectKMenu 는 Editor 폴더(= Editor 전용 어셈블리)에 있다.
//    런타임 스크립트는 #if UNITY_EDITOR 안이라도 그 어셈블리를 참조할 수 없다.
// ============================================================

public static class RareSkillOwnerLog
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void LogOnGameStart() => LogOwners();

    public static void LogOwners()
    {
        var db = ActiveSkillDatabase.Current;
        if (db?.Entries == null || db.Entries.Count == 0)
        {
            Debug.LogWarning("[희귀스킬] ActiveSkillDatabase 가 비어 있다 — 주인을 정할 수 없다.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("[희귀스킬] ═══════ 희귀 스킬 주인 ═══════");

        int rareCount = 0;

        foreach (var entry in db.Entries)
        {
            if (entry == null || !entry.IsRare) continue;
            rareCount++;

            // ⚠ 직업은 스킬이 아니라 "주인"에게서 읽는다
            //   공통 스킬(AllowedJobs 비어 있음)은 전 직업이 후보다.
            //   예전엔 여기서 Knight 를 기본값으로 박아 공통 스킬이 전부
            //   "(기사) · 기사 N명 중 1명" 으로 찍혔다 — 할당이 아니라 표시가 틀린 것.
            bool   common     = entry.AllowedJobs == null || entry.AllowedJobs.Length == 0;
            string limitLabel = common ? "공통" : JobList(entry.AllowedJobs);

            string owner = RareSkillArbiter.OwnerOf(entry.SkillId, db);
            if (string.IsNullOrEmpty(owner))
            {
                sb.AppendLine($"  {entry.SkillName,-10} [{limitLabel}] → 주인 없음 (후보 이름이 없다)");
                continue;
            }

            UnitJob ownerJob = UnitJobRoller.GetJob(owner);
            int     pool     = common ? HeroNamePool.AllNames.Count : CountJobs(entry.AllowedJobs);

            // 실제 스폰이 쓰는 함수로 되짚어 본다 — 할당표와 결과가 어긋나면 여기서 드러난다
            var    grade    = UnitJobRoller.GetBirthGrade(owner);
            var    resolved = RareSkillArbiter.Resolve(owner, ownerJob, db, grade);
            string check    = resolved == entry.SkillId ? "" : $"  ⚠ 불일치 → {resolved}";

            sb.AppendLine($"  {entry.SkillName,-10} [{limitLabel}] → {owner} ({JobName(ownerJob)})" +
                          $"   [태생 {grade} · {limitLabel} {pool}명 중 1명]{check}");
        }

        if (rareCount == 0)
        {
            Debug.LogWarning("[희귀스킬] IsRare 인 스킬이 없다 — 액티브 스킬 SO 를 다시 생성했는지 확인.");
            return;
        }

        sb.Append("══════════════════════════════════");
        Debug.Log(sb.ToString());
    }

    // ── 내부 ─────────────────────────────────────────────────

    static int CountJob(UnitJob job)
    {
        int n = 0;
        foreach (string name in HeroNamePool.AllNames)
            if (UnitJobRoller.GetJob(name) == job) n++;
        return n;
    }

    /// <summary>허용 직업들의 이름 수 합계 = 그 스킬의 후보 인원.</summary>
    static int CountJobs(UnitJob[] jobs)
    {
        int n = 0;
        foreach (var job in jobs) n += CountJob(job);
        return n;
    }

    static string JobList(UnitJob[] jobs)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < jobs.Length; i++)
        {
            if (i > 0) sb.Append('·');
            sb.Append(JobName(jobs[i]));
        }
        return sb.ToString();
    }

    /// <summary>주인이 왜 그 사람인지 보고 싶을 때 — 직업별 이름 전체를 찍는다.</summary>
    public static void LogNamesByJob()
    {
        var byJob = new Dictionary<UnitJob, List<string>>();
        foreach (string name in HeroNamePool.AllNames)
        {
            var job = UnitJobRoller.GetJob(name);
            if (!byJob.TryGetValue(job, out var list))
                byJob[job] = list = new List<string>();
            list.Add(name);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[희귀스킬] ═══════ 직업별 이름 (총 {HeroNamePool.AllNames.Count}명) ═══════");
        foreach (var pair in byJob)
            sb.AppendLine($"  {JobName(pair.Key)} ({pair.Value.Count}명): {string.Join(", ", pair.Value)}");

        Debug.Log(sb.ToString());
    }

    // LocalizationManager 는 씬 싱글턴이라 초기화 시점엔 없을 수 있다 — 직접 적는다
    static string JobName(UnitJob job) => job switch
    {
        UnitJob.Knight       => "기사",
        UnitJob.Archer       => "궁수",
        UnitJob.Mage         => "법사",
        UnitJob.ShieldBearer => "방패병",
        _                    => job.ToString(),
    };
}
