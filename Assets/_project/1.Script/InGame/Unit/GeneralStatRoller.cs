using UnityEngine;

// ============================================================
//  GeneralStatRoller.cs
//  장군(= 적 용사) 스텟 롤러 — 직업·등급·레벨은 UnitJobRoller 에 위임.
//
//  ■ 적 체력·공격력의 **단일 관문**이다 (2026-09-11)
//    용사 스탯이 나오는 길은 전부 여기를 지난다 —
//      장수 스폰(GeneralRuntimeBridge) · 병사만 오는 부대(HeroSpawner.SpawnSoldierSquad)
//      · 전황(BattleInfoPopup) · 용사 상세(HeroDetailPopup ← HeroStatPipeline)
//    그래서 전체 배율과 난이도 '광포' 를 여기서 곱한다. 표시와 전투가 저절로 같다.
//    병사·스킬 소환 병사는 장수 스탯의 비율이라 함께 따라온다.
//
//  ■ ⚠ 난이도 '광포' 는 한동안 **아무 데도 안 걸려 있었다**
//    MonsterStatRoller(원작의 적 롤러)에만 있었는데, 진영이 뒤집힌 뒤 그 경로를
//    부르는 곳이 없다. 보통~불지옥의 적 공·체 보너스가 통째로 무효였다.
//
//  사용:
//    UnitStat stat = GeneralStatRoller.Roll("Knight_A", level: 5, grade: UnitGrade.Rare);
// ============================================================

public static class GeneralStatRoller
{
    /// <summary>
    /// 적 전체 체력·공격력 배율.
    ///
    /// ■ 0.8 → 0.88 (사용자 지시, 2026-09-13 — 모든 스테이지 10% 상향)
    ///   2026-09-11 에 20% 를 깎았던 값을 10% 되돌린 것이다. 여기 하나가
    ///   <b>모든 판의 적 세기</b>다 — 스테이지·난이도·계층과 무관하게 곱해진다.
    ///
    /// ⚠ 체력·공격력만이다 — 사거리·공속·이속·방어율은 그대로.
    /// ⚠ 엘리트판의 추가 10% 는 여기가 아니다 (HeroDeployment 의 엘리트 배율).
    ///   여기서 함께 올리면 일반 판까지 두 번 오른다.
    /// </summary>
    public const float GlobalScale = 0.88f;

    /// <summary>지금 곱해지는 값 = 전체 배율 × (1 + 난이도 광포).</summary>
    public static float EnemyMultiplier =>
        GlobalScale * (1f + Mathf.Max(0f, DifficultyConfig.CurrentTier()?.EnemyStatBonus ?? 0f));

    /// <summary>
    /// unitName 을 시드로 장군 스텟을 생성해 반환한다.
    /// 직업은 시드에서 결정적으로 배정. 레벨·등급 보너스 적용.
    /// </summary>
    public static UnitStat Roll(string unitName, int level = 1, UnitGrade grade = UnitGrade.Normal)
    {
        // UnitJobRoller 는 부를 때마다 새 UnitStat 을 만든다 — 곱해도 누적되지 않는다.
        UnitStat stat = UnitJobRoller.Roll(unitName, level, grade);

        float m = EnemyMultiplier;
        stat.Set(StatType.MaxHp,  stat.Get(StatType.MaxHp)  * m);
        stat.Set(StatType.Attack, stat.Get(StatType.Attack) * m);
        return stat;
    }
}
