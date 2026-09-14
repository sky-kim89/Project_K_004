using BattleGame.Units;
using UnityEngine;

// ============================================================
//  SummonerStrikeRule.cs
//  소환사 평타가 무는 **최대 체력 비율**의 정본.
//
//  ■ 왜 비율인가 (사용자 확정, 2026-09-07)
//    평타 피해는 패기 × 2 = 8~16 이었다. 20스테이지 용사가 체력 수천이 되면
//    그 숫자는 없는 것과 같다 — 소환사는 성벽 뒤에서 깨작대는 장식이었다.
//    비율이면 스테이지가 아무리 올라가도 같은 무게로 문다.
//
//  ■ 격이 높을수록 덜 먹힌다 — 이 세 숫자가 "소환사가 다 한다" 를 막는다
//      잡병   25%  →  4대
//      엘리트  5%  →  20대
//      보스    1%  →  100대
//    공속 0.8 이니 잡병 하나에 5초, 보스 하나에 125초다. 보스는 몬스터가 잡는다.
//
//  ⚠ 방어율을 지나지 않는다 (UnitHitSystem — 파쇄와 같은 자리·같은 규칙)
//    방어율을 태우면 방패 용사 앞에서 다시 0 이 되어 만든 이유가 사라진다.
//
//  ⚠ 평타에만 걸린다 (HitType.Normal)
//    시그니처 스킬까지 비율이 되면 횟수 제한의 값어치가 폭발한다.
//
//  ⚠ 유물이 이 값을 올린다 — 여기 한 곳만 고치면 된다
//    ('마왕의 위엄' 계열). 지금은 훅이 없어 상수 그대로다.
// ============================================================

public static class SummonerStrikeRule
{
    /// <summary>잡병(일반 용사·병사)에게 무는 최대 체력 비율.</summary>
    public const float NormalRatio = 0.25f;

    /// <summary>엘리트 용사에게 무는 비율.</summary>
    public const float EliteRatio = 0.05f;

    /// <summary>보스 용사에게 무는 비율.</summary>
    public const float BossRatio = 0.01f;

    /// <summary>소환사 엔티티에 박을 값. 스폰 때 한 번 굽는다.</summary>
    public static SummonerStrikeComponent Build(SummonerData summoner)
    {
        // 소환사 '패기' — 세 격에 **같은 배율**을 곱한다 (SummonerVigorRule, 2026-09-11).
        //   격 사이의 비(25 : 5 : 1)는 그대로라 "보스는 몬스터가 잡는다" 가 유지된다.
        float mult = SummonerVigorRule.StrikeMultFor(summoner);

        // 유물 '마왕의 위엄' — 세 격에 **똑같이** %p 를 더한다.
        // ⚠ 비율로 곱하지 않는다. 곱하면 잡병(25%)만 크게 오르고 보스(1%)는
        //   티가 안 나, "보스를 녹이는 유물" 이 되기 전에 잡병 청소기가 된다.
        // ⚠ 패기 배율 **뒤에** 더한다 — 앞에 두면 유물 몫까지 패기가 부풀린다.
        float add = RelicTreeApplier.GetSystemValue(RelicSystemEffect.SummonerStrikeBonus);

        return new SummonerStrikeComponent
        {
            NormalRatio = Mathf.Clamp01(NormalRatio * mult + add),
            EliteRatio  = Mathf.Clamp01(EliteRatio  * mult + add),
            BossRatio   = Mathf.Clamp01(BossRatio   * mult + add),
        };
    }

    /// <summary>소환사 선택 화면·툴팁에 적을 한 줄.</summary>
    public static string Describe()
        => $"평타가 적 최대 체력의 {NormalRatio * 100f:0.#}% " +
           $"(엘리트 {EliteRatio * 100f:0.#}% · 보스 {BossRatio * 100f:0.#}%) 를 깎는다";
}
