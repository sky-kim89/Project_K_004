using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  EndlessBossRule.cs
//  **최종 스테이지(기본 30)는 깰 수 없다** — 보스가 끝없이 나온다.
//
//  ■ 원작 규칙을 그대로 되살린 것이다 (사용자 지시, 2026-09-10)
//    원작 NormalMode 에 IsEndless / GetEndlessBossEntries 가 있었는데,
//    웨이브 개념을 걷어내면서 BattleContext.EndlessBossIndex 가 함께 사라져
//    **부르는 곳이 없는 죽은 코드**가 돼 있었다. 그래서 이 게임은 30을 넘어도
//    평범한 스테이지가 계속 이어졌고, 40스테이지가 넘도록 런이 안 끝났다.
//
//  ■ 규칙은 셋뿐이다
//      · 보스 1기마다 스텟 ×10 (누적)
//      · 크기는 언제나 프리팹의 ×2
//      · 넉백 면역
//    잡으면 다음 보스가 나온다. 스테이지 클리어는 영원히 없다 —
//    카드 보상도 갈림길도 열리지 않는다. 런은 **마왕성이 뚫려야** 끝난다.
//
//  ■ ⚠ 보스는 혼자 온다
//    이 게임의 보스는 용사(General)라 휘하 병사를 데려온다. 그대로 두면
//    30스테이지 기준 33명이 보스마다 따라붙어, 화면에서 "무한 보스" 가 아니라
//    "보스가 낀 평범한 판" 으로 읽힌다. 원작 무한 보스는 한 기였다.
//
//  ■ ⚠ 세 곳이 이 규칙을 함께 본다 — 하나만 고치면 어긋난다
//      ① HeroDeployment.Build      — 0번 보스를 세운다
//      ② RunBootstrap.Update       — 잡히면 다음 보스를 부른다 (승리 선언 대신)
//      ③ SummonController          — 거두기를 하지 않는다 (판이 안 끝나므로)
//    ③ 이 없으면 보스가 죽는 순간 살아남은 몬스터가 전부 대기열로 거둬지고,
//    다음 스테이지가 영영 오지 않아 **필드가 텅 빈 채로 멈춘다.**
// ============================================================

public static class EndlessBossRule
{
    /// <summary>
    /// 무한 보스가 시작되는 스테이지. `StageConfig.NormalStageCount`(기본 30)다.
    ///
    /// ⚠ 숫자를 여기에 박지 않는다 — 원작 `NormalMode.IsEndless` 와 같은 값을 본다.
    /// </summary>
    public static int Stage => GameplayConfig.Current != null
                             ? GameplayConfig.Current.MaxStage
                             : 30;

    /// <summary>이 스테이지가 무한 보스 구간인가.</summary>
    public static bool IsEndlessStage(int stageNumber) => stageNumber >= Stage;

    /// <summary>보스 1기마다 곱해지는 스텟 배율 (원작과 같은 값).</summary>
    public const float StatStep = 10f;

    /// <summary>무한 보스 크기 — 프리팹의 2배. 보스마다 같다(누적하지 않는다).</summary>
    public const float ScaleMult = 2f;

    /// <summary>
    /// 다음 보스가 걸어 들어오기까지의 사이(초).
    ///
    /// ⚠ 0 으로 두지 말 것 — 잡자마자 다음이 서면 "죽였다" 가 화면에 안 남는다.
    /// </summary>
    public const float RespawnDelay = 2f;

    /// <summary>
    /// float 무한대(≈3.4e38)로 넘어가면 체력이 Infinity 가 되어 피해 계산이
    /// 전부 NaN 이 된다. 원작이 두었던 천장을 그대로 쓴다.
    /// </summary>
    const float StatCap = 1e30f;

    /// <summary>0번(첫 보스)은 ×1, 1번은 ×10, 2번은 ×100 …</summary>
    public static float StatMultiplierFor(int bossIndex)
        => Mathf.Min(StatCap, Mathf.Pow(StatStep, Mathf.Max(0, bossIndex)));

    /// <summary>
    /// bossIndex 번째 무한 보스의 편성 항목.
    ///
    /// ⚠ 이름이 곧 시드다 — 번호가 바뀌면 직업·외형·패시브가 함께 바뀐다.
    ///   매번 다른 얼굴의 보스가 오는 것이 노린 것이다.
    /// </summary>
    public static SpawnEntry BuildEntry(int stageNumber, int bossIndex) => new()
    {
        Name       = $"EndlessBoss_S{stageNumber}_{bossIndex}",
        UnitType   = SpawnUnitType.General,
        Count      = 1,
        Level      = stageNumber,
        IsBossHero = true,
        LoneHero   = true,

        EnemyRace  = HeroDeployment.RaceForSeed((uint)(stageNumber * 7919 + bossIndex)),

        // 최종 스테이지 — 스텟 범위 최댓값에서 굴린다 (원작과 같다).
        StageBias       = 1f,
        StatMultiplier  = StatMultiplierFor(bossIndex),
        ScaleMultiplier = ScaleMult,
        KnockbackImmune = true,

        // 첫 보스는 판이 시작하는 그 자리에서 함께 선다.
        DelayBefore  = bossIndex == 0 ? 0f : RespawnDelay,
        DelayBetween = 0f,
    };
}
