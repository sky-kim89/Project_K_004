using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  HeroDeployment.cs
//  용사(Hero) 측 편성 — 한 스테이지에 하나, 최대 5부대.
//
//  ■ 원작 아군 편성을 그대로 뒤집은 것이다
//    원작에서는 플레이어가 DeploymentData 5슬롯에 장수를 배치하고,
//    NormalMode.GetAllySpawnEntries 가 1웨이브에 한 번 스폰한 뒤
//    이후 웨이브에서는 그대로 유지했다.
//
//    이 게임에서는 그 역할이 적(용사) 쪽으로 넘어간다.
//    플레이어가 고르는 게 아니라 스테이지가 결정한다는 것만 다르다.
//
//  ■ 1부대 = 장수 1 + 휘하 병사
//    부대 수가 곧 슬롯 수다. 병사는 GeneralRuntimeBridge 가
//    SoldierCount 스탯만큼 알아서 딸려 스폰한다 — 여기서 셀 필요가 없다.
//
//  ■ 편성은 스테이지 번호로 결정적(deterministic)으로 뽑는다
//    같은 스테이지는 항상 같은 편성이 나온다. 이유:
//      · 죽고 다시 시도했는데 편성이 달라지면 대응을 학습할 수 없다
//      · 세이브에 편성을 적을 필요가 없다 — 스테이지 번호만 있으면 재현된다
//
//  ■ 부대 수는 스테이지가 오를수록 늘어난다
//    1 스테이지는 1부대로 시작해 10 스테이지에 5부대(라인 전부)로 찬다.
//    허들(엘리트) 스테이지는 한 부대를 더 얹는다 — 원작 난이도 곡선의
//    "허들에서 적 수가 는다" 규칙과 같은 취지다.
//
//  ■ ⚠ 이 게임의 물량은 **아군 것이다** (2026-09-04 확정)
//    아군은 살아남으면 라인으로 돌아와 다음 판에도 싸운다
//    (MonsterLineReturner) — 판을 거듭할수록 저절로 불어난다.
//    적까지 물량으로 나오면 그 그림이 지워진다. 그래서 용사는
//    **소수정예**다: 수는 적고 개체가 세다.
//    한때 2~5부대 · 기사 병사 3~7 이라 1스테이지부터 개체가 6기 넘게
//    쏟아졌고, 그걸 감당시키려 개체 스탯을 3분의 1로 깎았더니
//    "용사가 강하다" 는 느낌만 사라졌다. 수를 먼저 줄이고 개체를 되돌렸다.
//
//  ■ 첫 보스(5스테이지) 전까지는 장수가 없다 — 병사만 온다
//    부대는 그대로 만들되 장수를 세우지 않고 휘하 병사만 내보낸다.
//    자세한 근거는 GeneralFromStage 주석 참고.
//
//  ■ 허들 스테이지에는 보스 히어로가 선다
//    늘어난 그 한 자리를 보스가 가져간다. 총 부대 수는 그대로 두고
//    구성만 바뀌는 셈이다 — 물량과 보스를 동시에 늘리면 난이도가 두 번 뛴다.
// ============================================================

public static class HeroDeployment
{
    /// <summary>
    /// 부대 수 상한 = 라인 수.
    ///
    /// ⚠ 4 → 5 로 올렸다 (사용자 확정, 2026-09-06)
    ///   한때 "한 자리는 늘 비워 둔다 — 아군이 밀어낼 빈 라인이 있어야 한다" 였다.
    ///   지금은 10스테이지부터 다섯 라인이 전부 찬다. 빈 라인으로 도는 대신
    ///   **정면으로 밀어내는** 판이 되는 쪽을 택했다.
    ///   허들의 +1 도 이 값에 걸려 넘지 못한다.
    /// </summary>
    public const int MaxSquads = 5;

    /// <summary>
    /// 1스테이지의 부대 수.
    ///
    /// ⚠ 스테이지별 부대 수는 **눈으로 정한 표**다 (사용자 확정, 2026-09-06)
    ///     1~2 → 1 · 3~4 → 2 · 5 → 3 · 7~8 → 4 · 9~10 → 5
    ///   Min 1 · Max 5 · RampEnd 10 의 선형 보간이 이 표와 정확히 맞는다.
    ///   셋 중 하나만 바꾸면 표가 어긋나므로 반드시 같이 본다.
    /// </summary>
    public const int MinSquads = 1;

    /// <summary>
    /// 이 스테이지까지 오면 부대 수가 상한에 닿는다.
    ///
    /// ⚠ 20 → 10 (사용자 확정, 2026-09-06). 10스테이지에 라인이 전부 찬다.
    ///   MinSquads 주석의 표와 한 묶음이다 — 셋 중 하나만 바꾸면 표가 어긋난다.
    /// </summary>
    const int SquadRampEndStage = 10;

    /// <summary>
    /// 장수(용사 본인)가 등장하기 시작하는 스테이지. 그 전까지는 **병사만** 온다.
    ///
    /// ■ 왜 초반에는 병사만인가 (사용자 확정, 2026-08-28)
    ///   장수는 직업·등급·액티브·패시브·장비를 전부 달고 나온다. 그것이
    ///   1스테이지부터 쏟아지면 플레이어가 소환 조작을 익히기도 전에 판이 끝난다.
    ///   앞 네 판은 병사 물결로 두고, **첫 보스(5스테이지)에서 장수를 처음 보여 준다** —
    ///   그때 등장이 사건이 된다.
    ///
    /// ⚠ 첫 허들 스테이지와 같은 번호여야 한다
    ///   허들 주기는 RunBootstrap._hurdleEvery(5)가 쥐고 있다. 이 값이 그보다 크면
    ///   보스 스테이지인데 보스가 병사로 나오고, 작으면 보스보다 장수가 먼저 나온다.
    /// </summary>
    public const int GeneralFromStage = 5;

    /// <summary>
    /// 엘리트 스테이지의 적 체력·공격력 배율 (사용자 지시, 2026-09-13 — 10% 상향).
    ///
    /// ■ ⚠ 판 전체에 걸린다 — 엘리트 한 기가 아니다
    ///   "엘리트 스테이지를 10% 더 강하게" 이므로 그 판에 서는 <b>모든 부대</b>가
    ///   받는다. 엘리트 용사에게만 걸면 옆 부대는 일반 판과 똑같아서, 판이
    ///   세졌다는 느낌이 그 한 기에서만 난다.
    ///
    /// ⚠ 보스판에는 걸지 않는다 — 보스는 이미 제 장치(AoE 평타·평타 ×3·광폭화)로
    ///   무게를 갖는다. 여기에 배율까지 얹으면 5의 배수마다 벽이 두 번 선다.
    ///
    /// ⚠ 전체 상향(GeneralStatRoller.GlobalScale)과 <b>곱해진다</b> —
    ///   엘리트 판의 적은 일반 판의 1.1 배다. 두 값을 같은 자리에 두지 말 것:
    ///   저쪽은 모든 판, 이쪽은 엘리트 판에만 얹는 몫이다.
    /// </summary>
    public const float EliteStageStatMult = 1.1f;

    // ── 부대 수 ──────────────────────────────────────────────

    /// <summary>
    /// 해당 스테이지의 부대 수를 반환한다. MinSquads → MaxSquads 로 선형 증가하고,
    /// 허들 스테이지는 1부대를 더 얹는다 (상한은 넘지 않는다).
    /// </summary>
    public static int GetSquadCount(int stageNumber, bool isHurdle)
    {
        float t     = Mathf.InverseLerp(1f, SquadRampEndStage, stageNumber);
        int   count = Mathf.RoundToInt(Mathf.Lerp(MinSquads, MaxSquads, t));

        // ⚠ 첫 허들(5스테이지)에는 부대를 더 얹지 않는다
        //   그 판은 이미 두 가지가 한꺼번에 일어난다 — 장수가 처음 서고
        //   (그 전까지는 병사만), 보스가 처음 선다. 여기에 부대까지 얹으면
        //   4스테이지 대비 적 총량이 일곱 배로 뛰어 벽이 된다.
        //   장수를 한 판이라도 겪어 본 뒤(6스테이지~)부터 물량을 더한다.
        if (isHurdle && stageNumber > GeneralFromStage) count++;

        return Mathf.Clamp(count, MinSquads, MaxSquads);
    }

    // ── 편성 생성 ────────────────────────────────────────────

    /// <summary>
    /// 스테이지의 용사 편성을 만든다. 같은 인자면 항상 같은 결과가 나온다.
    /// </summary>
    /// <param name="stageNumber">스테이지 번호 (1부터)</param>
    /// <param name="isHurdle">허들(엘리트) 스테이지인가</param>
    /// <param name="stageBias">스텟 범위 편향 0~1. 원작 SpawnEntry.StageBias 와 같은 의미.</param>
    /// <param name="kind">
    /// 이 판의 성격. 정본은 <see cref="RunStageKindRule.Of"/> 다 —
    /// 여기서 스테이지 번호로 다시 판정하지 말 것.
    /// </param>
    public static List<SpawnEntry> Build(int stageNumber, RunStageKind kind, float stageBias)
    {
        // ── 최종 스테이지 — 무한 보스 하나로 갈음한다 ──
        //   정본은 EndlessBossRule 이다. 부대·병사·엘리트 규칙이 전부 여기서 끝난다.
        //   ⚠ 이 판은 클리어되지 않는다 — 잡으면 RunBootstrap 이 다음 보스를 부른다.
        if (EndlessBossRule.IsEndlessStage(stageNumber))
            return new List<SpawnEntry> { EndlessBossRule.BuildEntry(stageNumber, 0) };

        bool isHurdle = kind != RunStageKind.Normal;

        int squads = GetSquadCount(stageNumber, isHurdle);

        // 엘리트 판은 판 전체가 조금 세다 (EliteStageStatMult 주석 참고).
        // ⚠ 아래 두 곳(우두머리 자리·일반 부대)에 **같은 값**을 실어야 한다 —
        //   한쪽만 실으면 같은 판의 부대끼리 세기가 갈린다.
        float statMult = kind == RunStageKind.Elite ? EliteStageStatMult : 1f;

        // 스테이지 번호를 시드로 쓴다 — 재시도해도 같은 편성이 나온다.
        var rng = new Unity.Mathematics.Random((uint)(stageNumber * 7919 + 13));

        var entries = new List<SpawnEntry>(squads);

        // ── 보스 히어로 — 엘리트(허들) 스테이지에만 ──
        //
        //  부대 하나를 보스로 바꾼다. 부대 수를 늘리는 게 아니다 —
        //  늘리면 허들에서 물량과 보스를 동시에 감당해야 해 난이도가 두 번 뛴다.
        //
        //  편성 0번에 세운다. HeroSpawner.LaneOrder 가 0번을 **가운데 자리**에
        //  놓으므로 보스가 전장 한복판에 선다 — 시선이 먼저 가야 할 것이 가운데다.
        // 보스판이면 보스, 엘리트판이면 엘리트가 그 자리를 대신한다.
        if (isHurdle)
        {
            bool boss = kind == RunStageKind.Boss;

            entries.Add(new SpawnEntry
            {
                Name        = (boss ? "BossHero_S" : "EliteHero_S") + stageNumber,
                UnitType    = SpawnUnitType.General,
                Count       = 1,
                Level       = stageNumber,
                IsBossHero  = boss,
                IsEliteHero = !boss,

                EnemyRace      = PickRace(ref rng),
                StageBias      = stageBias,
                StatMultiplier = statMult,
                DelayBefore    = 0f,
                DelayBetween   = 0f,
            });

            squads--;   // 보스(또는 엘리트)가 한 자리를 차지한다
        }

        // 첫 보스 전까지는 장수를 세우지 않는다 (GeneralFromStage 참고).
        bool soldiersOnly = stageNumber < GeneralFromStage;

        for (int i = 0; i < squads; i++)
        {
            // ⚠ 이름이 곧 시드다
            //   UnitJobRoller / EnemyAppearanceRoller 가 이 문자열로 직업·외형·
            //   패시브를 결정한다. 스테이지·슬롯이 같으면 항상 같은 용사가 선다.
            string heroName = $"Hero_S{stageNumber}_{i}";

            entries.Add(new SpawnEntry
            {
                Name     = heroName,
                UnitType = SpawnUnitType.General,
                Count    = 1,

                // 레벨은 스테이지 번호를 그대로 쓴다.
                // 몬스터·소환사에는 레벨이 없고 용사에게만 Lv 이 붙는다 —
                // "점점 강한 적이 온다" 를 눈으로 보여 주는 유일한 장치다.
                Level    = stageNumber,

                SoldiersOnly  = soldiersOnly,

                EnemyRace      = PickRace(ref rng),
                StageBias      = stageBias,
                StatMultiplier = statMult,
                DelayBefore    = 0f,
                DelayBetween   = 0f,
            });
        }

        return entries;
    }

    // ── 종족 추첨 ────────────────────────────────────────────

    /// <summary>
    /// 용사 편성에 쓸 인간형 종족.
    /// 용사는 사람 진영이므로 몬스터스러운 종족(스켈레톤·좀비·데몬)은 넣지 않는다.
    /// </summary>
    static readonly EnemyRace[] HeroRaces =
    {
        EnemyRace.Demigod,
        EnemyRace.Vampire,
        EnemyRace.Furry,
        EnemyRace.Merman,
    };

    static EnemyRace PickRace(ref Unity.Mathematics.Random rng)
        => HeroRaces[rng.NextInt(0, HeroRaces.Length)];

    /// <summary>
    /// 시드 하나로 결정되는 용사 종족. 편성 밖에서 용사를 만드는 쪽이 쓴다
    /// (무한 보스 — EndlessBossRule).
    ///
    /// ⚠ 목록은 위 HeroRaces 하나다 — 다른 곳에 종족 배열을 또 두지 말 것.
    ///   두면 "보스만 스켈레톤인 용사 진영" 같은 것이 조용히 생긴다.
    /// </summary>
    public static EnemyRace RaceForSeed(uint seed)
        => HeroRaces[seed % (uint)HeroRaces.Length];
}
