using UnityEngine;

// ============================================================
//  CardLevelRule.cs
//  카드 레벨 규칙의 정본. "중복 획득 → 레벨업" 의 셈이 전부 여기 있다.
//
//  ■ 왜 규칙을 한곳에 모으나
//    필요 장수·레벨 배율·만렙을 UI 와 전투 양쪽에서 각자 계산하면
//    "카드에는 Lv3 이라고 떴는데 실제 스탯은 Lv2" 같은 어긋남이 난다.
//    표시도 전투도 이 클래스만 부른다.
//
//  ■ 중복 한 장 = 한 레벨 (2026-09-03, 사용자 확정)
//
//      Lv1 : 1장   Lv2 : 2장   Lv3 : 3장   Lv4 : 4장   Lv5 : 5장  ← 만렙
//
//    ⚠ 예전의 1/2/4/7/11 문턱은 폐기했다
//      런이 30스테이지고 3택이 스테이지마다 한 번이라, 같은 카드를 11장
//      모으려면 3택의 3분의 1 이상을 한 종족에만 써야 했다. 만렙이
//      사실상 도달 불가였고, 만렙 뒤에 열리는 진화·융합은 아예 못 봤다.
//      한 장에 한 레벨이면 5장 — 30스테이지 안에서 한두 카드는 끝까지
//      키우고 진화까지 볼 수 있다.
//
//    ⚠ "몇 장 더" 표시가 사라진다
//      중복은 이제 언제나 레벨을 올린다. CopiesToNextLevel 은 만렙이
//      아니면 늘 1 이고, 카드 3택의 "(N장 더)" 는 뜨지 않는다.
//
//  ■ 레벨이 주는 것 — **종족마다 미리 정해진 고정 효과**
//    MonsterSpeciesData.LevelBonuses 의 Lv2/3/4/5 칸이 하나씩 열린다.
//    한 칸 = 스탯 1~2개 + 선택적 패시브 1개 (MonsterLevelBonus).
//
//    ⚠ 예전의 "레벨당 일괄 +18%" 는 폐기했다 (2026-08-27)
//      모든 종족이 똑같이 커지면 "무엇을 키울까" 가 선택이 아니게 된다.
//      어빌리티 축을 없애고 그 역할을 레벨업으로 옮기면서, 종족마다
//      **다른 것이 열리도록** 표로 바꿨다. 그래야 카드 3택이 곧
//      "이번 런을 어느 방향으로 키울까" 가 된다.
//
//  ■ 만렙(Lv5) 이후에 그 카드를 또 뽑으면 — 진화 또는 융합
//    중복이 더 이상 레벨을 올리지 못하는 대신 **갈림길**이 열린다.
//      진화 → 같은 계보의 상위 종족으로 무작위 변신
//      융합 → 덱의 다른 카드를 먹고 그 종족 패시브를 배움
//    규칙의 정본은 CardEvolution 이다.
// ============================================================

public static class CardLevelRule
{
    /// <summary>만렙. 이 위로는 중복을 아무리 먹어도 오르지 않는다.</summary>
    public const int MaxLevel = 5;

    // ── 레벨당 기본 공/체 증가 ───────────────────────────────
    //
    //  ■ 종족별 표(LevelBonuses)와 **별개로** 항상 붙는다 (사용자 확정, 2026-09-04)
    //    표는 종족마다 다른 것이 열리는 자리다. 그런데 표가 비어 있는 종족은
    //    레벨을 올려도 아무 일이 없어서, 같은 카드를 또 주운 것이 손해처럼
    //    보였다. 기본 증가가 있으면 어느 종족이든 레벨업이 반드시 값을 한다.
    //
    //  ■ 공격력·체력 둘만 올린다
    //    사거리·공속·이속은 종족의 정체성이다(MonsterStatComposer 주석).
    //    레벨로 그것까지 오르면 키운 카드끼리 서로 닮아 간다.
    //
    //  ⚠ 만렙(Lv5)이면 +48% 다 — 종족 표까지 더하면 그 위에 얹힌다.
    //    수치를 고칠 때 표 쪽 값과 합쳐서 볼 것.

    /// <summary>레벨 한 칸당 오르는 공격력·체력 비율.</summary>
    public const float StatBonusPerLevel = 0.12f;

    /// <summary>
    /// 이 레벨에서 기본으로 붙는 공/체 비율. Lv1 은 0 이다.
    /// 카드 화면이 "기본 + 증가" 를 그릴 때도 이 값을 쓴다.
    /// </summary>
    public static float StatBonusRatio(int level)
        => Mathf.Max(0, Mathf.Min(level, MaxLevel) - 1) * StatBonusPerLevel;

    /// <summary>레벨 L 에 도달하는 데 필요한 누적 장수. 한 장에 한 레벨이다.</summary>
    public static int CopiesForLevel(int level) => Mathf.Clamp(level, 1, MaxLevel);

    /// <summary>누적 장수로 레벨을 구한다. 1장 = Lv1, 5장 = 만렙.</summary>
    public static int LevelForCopies(int copies) => Mathf.Clamp(copies, 1, MaxLevel);

    /// <summary>다음 레벨까지 남은 장수. 만렙이면 0.</summary>
    public static int CopiesToNextLevel(int copies)
    {
        int level = LevelForCopies(copies);
        if (level >= MaxLevel) return 0;

        return CopiesForLevel(level + 1) - copies;
    }

    public static bool IsMaxLevel(int copies) => LevelForCopies(copies) >= MaxLevel;

    /// <summary>
    /// 그 레벨까지 열려 있는 보너스 칸 수. Lv1 = 0칸, Lv5 = 4칸.
    /// 실제 내용은 종족이 갖는다 (MonsterSpeciesData.LevelBonuses).
    /// </summary>
    public static int UnlockedBonusCount(int level) => Mathf.Max(0, level - 1);
}
