using UnityEngine;

// ============================================================
//  MonsterGearLevelRule.cs
//  몬스터 장비 **레벨업·강화의 규칙 정본** (사용자 확정, 2026-09-10)
//
//  ■ 두 단계다 — 둘 다 **같은 장비 여분 + 영구 골드**
//    ① 레벨업 Lv1 → Lv5 — 재료 1 · 2 · 2 · 3 (Lv2 · Lv3 · Lv4 · Lv5)
//       레벨마다 능력치가 하나씩 더해지고(MonsterGearData.Levels),
//       Lv4·Lv5 에는 패시브가 열린다(2차 작업).
//    ② 강화 +1, +2, … — 한 번에 재료 1 + 골드. 몬스터 공격력·체력 +1% 씩.
//       **상한이 없다** (사용자 확정). 끝을 막는 것은 재료다 — 같은 장비를
//       또 얻어야 한 번 더 오른다. 골드는 한 번마다 25% 씩 비싸진다.
//
//  ■ 왜 같은 장비인가
//    런 종료 상자가 같은 장비를 거듭 주면 사본이 쌓이기만 했다.
//    장비는 **한 마리만** 낄 수 있으므로(MonsterGearInventory) 사본은 전부 재료다 —
//    중복이 곧 성장이다.
//
//  ■ ⚠ 결과는 확정이다 — 확률이 아니다
//    품질 개선(MonsterGradeUpgradeRule)과 같은 규칙. 무게는 재료 수와 값이 만든다.
//
//  ■ ⚠ 영구 골드로 산다 · 런 중에는 잠긴다 (CodexEditLock)
//    런 중에 영구 재화로 스탯을 사면 품질 개선을 잠근 뜻이 새어 나간다.
//
//  ⚠ 숫자를 화면에 적지 말 것 — 표시·판정·차감이 전부 이 파일을 본다.
// ============================================================

public static class MonsterGearLevelRule
{
    public const int MaxLevel = 5;

    /// <summary>강화 한 번이 올리는 몬스터 공격력·체력 비율 (0.01 = 1%).</summary>
    public const float EnhanceStatPct = 0.01f;

    /// <summary>강화 한 번에 드는 같은 장비 수.</summary>
    public const int EnhanceMaterials = 1;

    /// <summary>이 레벨에 패시브가 열린다 (2차 작업 — 아직 효과 없음).</summary>
    public static bool OpensPassive(int level) => level >= 4;

    public static bool IsMaxLevel(int level) => level >= MaxLevel;

    /// <summary>강화가 올리는 공격력·체력 비율 합.</summary>
    public static float EnhancePct(int enhance) => Mathf.Max(0, enhance) * EnhanceStatPct;

    // ── 재료 ─────────────────────────────────────────────────

    /// <summary>
    /// 지금 레벨에서 한 번 더 올리는 데 드는 같은 장비 수.
    /// 레벨업 1 · 2 · 2 · 3 (Lv5 까지 여분 8 개), 그 뒤 강화는 언제나 1.
    /// </summary>
    public static int MaterialsFor(int currentLevel) => currentLevel switch
    {
        1 => 1,
        2 => 2,
        3 => 2,
        4 => 3,
        _ => EnhanceMaterials,
    };

    // ── 골드 ─────────────────────────────────────────────────
    //
    //  ⚠ 등급이 단위를 정하고 레벨이 배수를 정한다
    //    Lv5 까지 합계 = 단위 × 11 — 일반 1,650 · 영웅 38,500.
    //    품질 개선(한 종족 Epic 까지 63,200)보다 가볍게 둔다 — 그쪽은 종족 전체의
    //    스탯이고 이쪽은 칸 하나다. 수입(한 런 700~28,000)을 고치면 함께 볼 것.

    // ⚠ 10% 인상 · 10 단위 (사용자 지시, 2026-09-13)
    //   런 골드(RunGoldRule.CostBump) · 품질 개선과 같은 폭이다.
    //   ⚠ 단위 자체를 10 단위로 맞춘다 — LevelGoldFor 가 ×1·2·3·5 로 곱하므로
    //     단위가 10 의 배수여야 모든 레벨 값이 딱 떨어진다.
    //     (150×1.1 = 165 → 170. 반올림 탓에 일반 등급만 +13% 다)
    static int GradeUnit(UnitGrade grade) => grade switch
    {
        UnitGrade.Normal   =>  170,
        UnitGrade.Uncommon =>  440,
        UnitGrade.Rare     =>  990,
        UnitGrade.Unique   => 1980,
        _                  => 3850,
    };

    /// <summary>지금 레벨에서 다음 레벨로 가는 값.</summary>
    public static int LevelGoldFor(UnitGrade grade, int currentLevel) => currentLevel switch
    {
        1 => GradeUnit(grade) * 1,
        2 => GradeUnit(grade) * 2,
        3 => GradeUnit(grade) * 3,
        4 => GradeUnit(grade) * 5,
        _ => 0,
    };

    /// <summary>
    /// 지금 강화 수치에서 한 번 더 올리는 값. 단위의 절반에서 시작해 한 번마다 25% 씩 오른다.
    /// ⚠ 상한이 없으니 값이 올라가는 쪽이 브레이크다 — 선형이라 재료만 있으면 언젠가 산다.
    /// </summary>
    public static int EnhanceGoldFor(UnitGrade grade, int currentEnhance)
    {
        float raw = GradeUnit(grade) * 0.5f * (1f + 0.25f * Mathf.Max(0, currentEnhance));
        return Mathf.RoundToInt(raw / 10f) * 10;
    }

    // ── 판정 ─────────────────────────────────────────────────

    public enum Blocked
    {
        None = 0,
        InRun,          // 런 중 (CodexEditLock)
        NotOwned,       // 한 개도 없다
        NoMaterial,     // 여분이 모자란다
        NotEnoughGold,
    }

    static MonsterGearInventory Inv => UserDataManager.Instance.Get<MonsterGearInventory>();

    /// <summary>다음 한 번이 레벨업인가. 아니면 강화다.</summary>
    public static bool NextIsLevelUp(MonsterGearData gear) => !IsMaxLevel(Inv.LevelOf(gear.Id));

    /// <summary>다음 한 번에 드는 여분 수.</summary>
    public static int MaterialsNeeded(MonsterGearData gear) => MaterialsFor(Inv.LevelOf(gear.Id));

    /// <summary>다음 한 번에 드는 골드.</summary>
    public static int CostFor(MonsterGearData gear)
    {
        var inv = Inv;
        int lv  = inv.LevelOf(gear.Id);

        return IsMaxLevel(lv)
             ? EnhanceGoldFor(gear.Grade, inv.EnhanceOf(gear.Id))
             : LevelGoldFor(gear.Grade, lv);
    }

    public static Blocked Check(MonsterGearData gear)
    {
        // ⚠ 가장 먼저 본다 — 품질 개선과 같은 이유 (CodexEditLock 머리 주석)
        if (CodexEditLock.Locked) return Blocked.InRun;

        var inv = Inv;
        if (inv.OwnedCount(gear.Id) <= 0) return Blocked.NotOwned;

        if (inv.SpareCount(gear.Id) < MaterialsFor(inv.LevelOf(gear.Id))) return Blocked.NoMaterial;

        return MonsterGradeUpgradeRule.Wallet >= CostFor(gear) ? Blocked.None : Blocked.NotEnoughGold;
    }

    /// <summary>
    /// 한 번 올린다 — Lv5 전이면 레벨업, 그 뒤면 강화.
    /// 조건이 안 맞으면 <b>아무것도 하지 않고</b> false.
    ///
    /// ⚠ 돈을 먼저 낸다 — 품질 개선과 같은 순서
    ///   올리고 나서 내면 잔액이 모자란 순간에 공짜로 오른다.
    /// </summary>
    public static bool TryAdvance(MonsterGearData gear)
    {
        if (Check(gear) != Blocked.None) return false;

        var inv  = Inv;
        int lv   = inv.LevelOf(gear.Id);
        int cost = CostFor(gear);

        if (!UserDataManager.Instance.Get<ItemData>().Spend(eItem.Gold, cost)) return false;

        // ⚠ 재료를 못 먹었는데 오르면 안 된다 — Check 와 어긋난 것이다
        if (!inv.ConsumeSpares(gear.Id, MaterialsFor(lv)))
        {
            Debug.LogError($"[MonsterGearLevelRule] '{gear.Id}' 여분을 소모하지 못했는데 골드 {cost} 를 " +
                           "이미 냈습니다 — Check 와 ConsumeSpares 의 판정이 어긋났습니다.");
            return false;
        }

        if (IsMaxLevel(lv)) inv.SetEnhance(gear.Id, inv.EnhanceOf(gear.Id) + 1);
        else                inv.SetLevel(gear.Id, lv + 1);

        return true;
    }
}
