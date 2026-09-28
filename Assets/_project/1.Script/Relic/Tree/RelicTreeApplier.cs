using System.Collections.Generic;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  RelicTreeApplier.cs
//  유물 테크트리에서 찍은 노드를 스텟·시스템 보너스에 반영한다.
//  구 RelicApplier(RelicData SO + RelicInventoryData)를 대체한다.
//
//  ■ 부르는 쪽이 데이터를 들고 다니지 않는다
//    구 API 는 (inventory, db) 를 매번 넘겨받았고, 호출처 10곳이 각자
//    UserDataManager 에서 꺼내 오느라 같은 코드가 흩어져 있었다.
//    트리는 세이브가 하나뿐이라 여기서 직접 읽는다.
//
//  ■ 적용 지점
//    몬스터 스탯 : MonsterStatComposer ⑥-b (ApplyToMonsterStat)  ← 이 게임의 주 경로
//    적 약화     : GeneralRuntimeBridge (ApplyEnemyWeaken)
//    시스템 값   : 각 규칙 클래스 (GetSystemInt / GetSystemValue)
//
//  ⚠ 장수·병사 경로는 걷어냈다 (2026-09-07)
//    ApplyToGeneralStat / ApplyGeneralOnly / CollectSoldier 는 원작 트리의 것이다.
//    이 게임의 스탯 노드는 전부 RelicTarget.Unit_Monster 라 그 셋은 **구조적으로
//    아무것도 못 찾는다.** 남겨 두면 "적을 강화하는 유물" 로 가는 길만 열어 둔 셈이라
//    지웠다 — RelicTreeCatalog.Verify() 가 타깃을 강제하므로 되살릴 이유도 없다.
//
//  ⚠ 저장된 레벨을 그대로 믿지 않는다
//    밸런스로 MaxLevel 을 내리면 이미 그 위로 저장된 세이브가 초과 효과를 낸다.
//    LevelOf() 한 곳에서 상한을 다시 건다 — 구 RelicApplier 가 겪은 문제다.
// ============================================================

public static class RelicTreeApplier
{


    static RelicTreeData Data => UserDataManager.Instance?.Get<RelicTreeData>();

    /// <summary>세이브 레벨에 MaxLevel 상한을 건 값.</summary>
    static int LevelOf(RelicTreeData data, RelicNodeDef def)
        => Mathf.Min(data.GetLevel(def.Id), def.MaxLevel);

    // ══════════════════════════════════════════════════════════
    //  몬스터 스탯 — 이 게임의 주 경로 (2026-09-07)
    // ══════════════════════════════════════════════════════════

    /// <summary>유물 몫이 들어가는 레이어 키.</summary>
    public const string MonsterLayerKey = "relic_monster";

    /// <summary>
    /// 소환 몬스터에게 유물 스탯 노드를 얹는다 (MonsterStatComposer 가 부른다).
    ///
    /// ■ ⚠ RelicTarget.Unit_Monster 노드만 본다
    ///   장수(= 적 용사) 경로는 아예 걷어냈고, RelicTreeCatalog.Verify() 가
    ///   스탯 노드의 타깃을 강제한다. 다른 값으로 두면 표를 읽는 순간 터진다.
    ///
    /// ⚠ 부르는 자리는 하나뿐이다 (MonsterStatComposer ⑥ 뒤)
    ///   두 곳에서 부르면 유물 몫이 두 번 곱해진다. 스탯 합성의 정본은
    ///   언제나 Composer 하나다.
    /// </summary>
    public static void ApplyToMonsterStat(UnitStat stat)
    {
        var data = Data;
        if (stat == null || data == null) return;

        foreach (var def in RelicTreeCatalog.All)
        {
            if (def.IsSystem) continue;
            if (def.Target != RelicTarget.Unit_Monster) continue;

            int level = LevelOf(data, def);
            if (level <= 0) continue;

            foreach (var line in def.Stats)
            {
                float delta = line.Absolute
                    ? line.PerLevel * level
                    : stat.Get(line.Stat) * line.PerLevel * level;
                stat.Add(line.Stat, delta, MonsterLayerKey);
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  적 약화
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 용사(적) 스탯을 깎는다. 용사 스폰 경로가 SpawnEntity 직전에 부른다.
    ///
    /// ⚠ 이동속도는 상한을 다르게 잡는다
    ///   체력·공격력은 100%까지 깎아도 "약해진다" 로 끝나지만, 이동속도를
    ///   100% 깎으면 용사가 **영영 안 걸어와** 판이 끝나지 않는다.
    ///   0.6 은 트리를 다 찍어도 넘지 않는 값이다(느려진 진군 5×5% = 25%).
    /// </summary>
    public static void ApplyEnemyWeaken(UnitStat stat)
    {
        if (stat == null) return;

        float hpRatio   = GetSystemValue(RelicSystemEffect.EnemyMaxHpReduction);
        float atkRatio  = GetSystemValue(RelicSystemEffect.EnemyAttackReduction);
        float moveRatio = GetSystemValue(RelicSystemEffect.EnemyMoveReduction);

        if (hpRatio > 0f)
            stat.Add(StatType.MaxHp,  -stat.Get(StatType.MaxHp)  * Mathf.Clamp01(hpRatio),  "relic_weaken");
        if (atkRatio > 0f)
            stat.Add(StatType.Attack, -stat.Get(StatType.Attack) * Mathf.Clamp01(atkRatio), "relic_weaken");
        if (moveRatio > 0f)
            stat.Add(StatType.MoveSpeed,
                     -stat.Get(StatType.MoveSpeed) * Mathf.Clamp(moveRatio, 0f, MaxEnemySlow),
                     "relic_weaken");
    }

    /// <summary>용사 이동속도를 깎을 수 있는 최대 비율. 넘으면 판이 안 끝난다.</summary>
    const float MaxEnemySlow = 0.6f;

    // ══════════════════════════════════════════════════════════
    //  시스템 값
    // ══════════════════════════════════════════════════════════

    /// <summary>같은 시스템 효과를 가진 노드의 합 (0.15 = 15%).</summary>
    public static float GetSystemValue(RelicSystemEffect effect)
    {
        var data = Data;
        if (data == null) return 0f;

        float total = 0f;
        foreach (var def in RelicTreeCatalog.All)
        {
            if (def.System != effect) continue;
            total += def.SystemPerLevel * LevelOf(data, def);
        }
        return total;
    }

    public static int GetSystemInt(RelicSystemEffect effect)
        => Mathf.RoundToInt(GetSystemValue(effect));

    /// <summary>
    /// 전투 배속으로 쓸 수 있는 단계 수. 기본 1단계(1× 뿐) + 해금 노드.
    /// 시간의 고삐 → 2단계, 찰나의 지배까지 → 3단계.
    /// ⚠ 배속을 묻는 곳은 전부 여기를 거친다 — TopBarUI 가 직접 세면 트리가 무시된다.
    /// </summary>
    public static int GetBattleSpeedStepCount()
        => 1 + GetSystemInt(RelicSystemEffect.BattleSpeedUnlock);


    /// <summary>
    /// 그 효과를 주는 노드 중 <b>아직 만렙이 아닌 첫 노드</b>의 이름.
    /// 잠긴 기능을 안내할 때 "무엇을 찍으면 되는지" 를 짚는 데 쓴다.
    ///
    /// ⚠ 이름을 문자열로 박아 두지 말 것
    ///   표에서 노드 이름을 고치면 안내만 옛 이름으로 남는다.
    ///   한 효과에 노드가 여럿인 경우(배속 2단계)도 여기서 걸러진다.
    /// </summary>
    public static string NextNodeNameFor(RelicSystemEffect effect)
    {
        var data = Data;
        if (data == null) return null;

        foreach (var def in RelicTreeCatalog.All)
        {
            if (def.System != effect) continue;
            if (LevelOf(data, def) >= def.MaxLevel) continue;
            // ⚠ 화면에 나가는 값이라 DisplayName 이다 — Name 은 한국어 원문(진단용)이다
            return def.DisplayName;
        }
        return null;
    }
}
