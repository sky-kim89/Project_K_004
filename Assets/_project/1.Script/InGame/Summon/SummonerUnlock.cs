using System;
using UnityEngine;

// ============================================================
//  SummonerUnlock.cs
//  소환사 해금 조건 — 데이터 정의 + 판정.
//
//  ■ 조건이 곧 그 캐릭터의 예고편이다
//    "스테이지 12 도달" 로 열면 아무것도 가르치지 않는다. 스컬 킹을
//    "스켈레톤 품질 Rare 이상" 으로 열면, 여는 순간 이미 그 캐릭터를
//    어떻게 쓸지 안다. 조건은 잠금이 아니라 **안내**여야 한다.
//
//  ■ ⚠ 영구 기록만 본다
//    StageProgressData 는 환생 때 지워지고, 이 게임은 **패배가 곧 환생**이라
//    죽을 때마다 0 이 된다. 그 값으로 조건을 걸면 죽는 순간 다시 잠긴다.
//    여기서 보는 것은 둘뿐이다:
//      MonsterCodexData          — 환생에도 남는 도감 (등록 종족 + 영구 품질)
//      ReincarnationData.BestStage — 환생에도 남는 최고 도달 스테이지
//
//  ■ 조건은 여러 개를 걸 수 있고 **전부** 만족해야 한다
//    트롤 조련사처럼 "종족 둘 + 스테이지" 를 함께 요구하는 경우가 있다.
//
//  ■ 진행 차단이 아니다
//    견습 하나로 끝까지 갈 수 있다. 이 조건들은 막는 장치가 아니라
//    "다음에 뭘 해볼까" 의 목록이다. 그래서 잠긴 소환사도 선택 화면에
//    **보이되 고를 수만 없게** 한다 — 숨기면 존재를 모르니 목표가 못 된다.
// ============================================================

public enum SummonerUnlockKind
{
    /// <summary>최고 도달 스테이지가 Value 이상. (영구 기록)</summary>
    BestStage = 0,

    /// <summary>SpeciesIds 의 종족을 **전부** 도감에 등록했다.</summary>
    SpeciesUnlocked = 1,

    /// <summary>SpeciesIds 의 종족이 전부 Value 등급(UnitGrade) 이상이다.</summary>
    SpeciesGrade = 2,

    /// <summary>도감 등록 종족 수가 Value 이상.</summary>
    CodexCount = 3,

    /// <summary>Traits 를 가진 종족을 Value 종 이상 등록했다.</summary>
    TraitCount = 4,
}

[Serializable]
public struct SummonerUnlock
{
    public SummonerUnlockKind Kind;

    [Tooltip("스테이지 번호 · 등급(UnitGrade) · 종 수 — Kind 에 따라 뜻이 다르다.")]
    public int Value;

    [Tooltip("SpeciesUnlocked · SpeciesGrade 가 쓰는 종족 ID 목록.")]
    public string[] SpeciesIds;

    [Tooltip("TraitCount 가 세는 특성.")]
    public MonsterTrait Traits;
}

public static class SummonerUnlockRule
{
    // ── 판정 ─────────────────────────────────────────────────

    /// <summary>
    /// 이 소환사를 고를 수 있는가.
    ///
    /// ⚠ 판정의 정본이다 — 화면마다 다시 계산하지 말 것.
    ///   조건이 비어 있으면 기본 해금이다(견습 소환사).
    /// </summary>
    public static bool IsUnlocked(SummonerData summoner)
    {
        if (summoner == null) return false;
        if (summoner.Unlocks == null || summoner.Unlocks.Length == 0) return true;

        foreach (SummonerUnlock cond in summoner.Unlocks)
            if (!Satisfied(cond)) return false;

        return true;
    }

    static bool Satisfied(in SummonerUnlock cond)
    {
        var codex = UserDataManager.Instance?.Get<MonsterCodexData>();
        var reinc = UserDataManager.Instance?.Get<ReincarnationData>();

        // ⚠ 세이브가 아직 없으면 잠근 채로 둔다
        //   열어 두는 쪽으로 흘리면 부팅 순서가 어긋난 한 프레임에 전부 열려 보인다.
        if (codex == null || reinc == null) return false;

        switch (cond.Kind)
        {
            case SummonerUnlockKind.BestStage:
                return reinc.BestStage >= cond.Value;

            case SummonerUnlockKind.CodexCount:
                return codex.UnlockedCount >= cond.Value;

            case SummonerUnlockKind.SpeciesUnlocked:
                return CountUnlocked(cond.SpeciesIds, codex) == Length(cond.SpeciesIds);

            case SummonerUnlockKind.SpeciesGrade:
                foreach (string id in Safe(cond.SpeciesIds))
                {
                    if (!codex.IsUnlocked(id))               return false;
                    if ((int)codex.GetGrade(id) < cond.Value) return false;
                }
                return Length(cond.SpeciesIds) > 0;

            case SummonerUnlockKind.TraitCount:
                return CountTrait(cond.Traits, codex) >= cond.Value;

            default:
                return true;
        }
    }

    // ── 진행도 ───────────────────────────────────────────────

    /// <summary>
    /// 조건 문구 — 잠긴 카드에 그대로 뜬다.
    ///
    /// ⚠ 숫자를 문구에 박지 말 것
    ///   Value 는 밸런싱으로 바뀐다. 문구에 박으면 데이터를 고칠 때마다
    ///   화면이 조용히 거짓말을 하게 된다.
    /// </summary>
    public static string Describe(SummonerData summoner)
    {
        if (summoner?.Unlocks == null || summoner.Unlocks.Length == 0) return string.Empty;

        var sb = new System.Text.StringBuilder(64);

        foreach (SummonerUnlock cond in summoner.Unlocks)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(Describe(cond));
        }

        return sb.ToString();
    }

    static string Describe(in SummonerUnlock cond)
    {
        var codex = UserDataManager.Instance?.Get<MonsterCodexData>();
        var reinc = UserDataManager.Instance?.Get<ReincarnationData>();

        switch (cond.Kind)
        {
            case SummonerUnlockKind.BestStage:
                return $"스테이지 {cond.Value} 도달   ({reinc?.BestStage ?? 0} / {cond.Value})";

            case SummonerUnlockKind.CodexCount:
                return $"도감 {cond.Value}종 등록   ({codex?.UnlockedCount ?? 0} / {cond.Value})";

            case SummonerUnlockKind.SpeciesUnlocked:
            {
                int have = codex != null ? CountUnlocked(cond.SpeciesIds, codex) : 0;
                return $"{NamesOf(cond.SpeciesIds)} 도감 등록   ({have} / {Length(cond.SpeciesIds)})";
            }

            case SummonerUnlockKind.SpeciesGrade:
                return $"{NamesOf(cond.SpeciesIds)} 품질 {GradeName(cond.Value)} 이상";

            case SummonerUnlockKind.TraitCount:
            {
                int have = codex != null ? CountTrait(cond.Traits, codex) : 0;
                return $"{TraitName(cond.Traits)} {cond.Value}종 등록   ({have} / {cond.Value})";
            }

            default:
                return string.Empty;
        }
    }

    // ── 내부 ─────────────────────────────────────────────────

    static int Length(string[] ids) => ids?.Length ?? 0;

    static string[] Safe(string[] ids) => ids ?? System.Array.Empty<string>();

    static int CountUnlocked(string[] ids, MonsterCodexData codex)
    {
        int n = 0;
        foreach (string id in Safe(ids))
            if (codex.IsUnlocked(id)) n++;

        return n;
    }

    /// <summary>이 특성을 가진 종족을 몇 종이나 등록했는가.</summary>
    static int CountTrait(MonsterTrait trait, MonsterCodexData codex)
    {
        CardCatalog catalog = CardCatalog.Current;
        if (catalog == null || trait == MonsterTrait.None) return 0;

        int n = 0;

        foreach (MonsterSpeciesData species in catalog.Monsters)
        {
            if (species == null)                       continue;
            if ((species.Traits & trait) == 0)         continue;
            if (!codex.IsUnlocked(species.Id))         continue;

            n++;
        }

        return n;
    }

    /// <summary>종족 ID 목록을 사람이 읽는 이름으로. 카탈로그가 없으면 ID 그대로.</summary>
    static string NamesOf(string[] ids)
    {
        CardCatalog catalog = CardCatalog.Current;
        var sb = new System.Text.StringBuilder(48);

        foreach (string id in Safe(ids))
        {
            if (sb.Length > 0) sb.Append(" · ");

            MonsterSpeciesData species = catalog?.GetMonster(id);
            sb.Append(species != null ? species.DisplayName : id);
        }

        return sb.ToString();
    }

    /// <summary>등급 한국어 이름. 표는 LocalizationManager 가 정본이다.</summary>
    static string GradeName(int grade)
        => LocalizationManager.Instance.Get(((UnitGrade)grade).ToString());

    /// <summary>특성 한국어 이름. 표는 MonsterTraitNames 가 정본이다.</summary>
    static string TraitName(MonsterTrait trait) => trait.ToKorean();
}
