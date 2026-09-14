using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  HeroStatPipeline.cs
//  장수 스탯을 만드는 **유일한** 자리. 로비 표시도 전투도 여기서 나온다.
//
//  ■ 왜 하나로 합쳤나
//    예전엔 같은 규칙이 두 곳에 손으로 적혀 있었다 —
//      로비 : HeroStatResolver.Resolve  (Dictionary 8통)
//      전투 : GeneralRuntimeBridge      (UnitStat 레이어)
//    자료구조부터 달라서 한쪽에 항목을 추가하면 다른 쪽이 조용히 빠졌다.
//    "장군의 위엄이 병사에게 샌다", "패시브가 용병 탭에 안 뜬다" 처럼
//    같은 종류의 버그가 반복된 이유가 이것이다. 주석으로 막을 수 없는 종류였다.
//
//    지금은 계산이 한 번만 존재하고, 결과물(UnitStat) 하나를 양쪽이 각자
//    필요한 방식으로 읽는다.
//      전투 → Stat 을 그대로 쓴다. 병사 원본은 SoldierSource.
//      표시 → 같은 Stat 에서 레이어별로 꺼내 분해한다 (UnitStat.GetLayer).
//
//  ■ 적용 순서 — 이 순서가 곧 규칙이다
//    1. base        등급·레벨 롤
//    ── 여기까지가 '부대 전체' 다. 병사 환산 원본은 이 시점의 값이다.
//    2. *@g         장수 전용 (패시브 General)
//
//    ⚠ 유물은 여기 걸리지 않는다 — 장수는 적(용사)이다.
//      유물 스탯 노드는 전부 RelicTarget.Unit_Monster 다 (RelicTreeCatalog.Verify).
//
//    ⚠ 장수 전용이 마지막인 이유
//      % 옵션은 '그 시점의 총합' 을 기준으로 계산된다. 장수 전용을 먼저 붙이면
//      뒤따르는 공용 % 의 계산 근거가 부풀려지고, 그 몫은 공용 레이어에 담겨
//      병사에게 그대로 흘러간다.
//
//  ■ 소프트캡은 여기서 굽지 않는다
//    방어율 상한은 **출력 시점에만** 적용한다 —
//      전투: DamageMath.AfterDefense / 표시: StatDisplayHelper.EffectiveDefensePct
//    예전엔 조립 도중 base 레이어를 캡 결과로 덮어썼는데, 그 순간 장수 전용
//    방어율이 base 에 섞여 병사에게 새고, 피격 때 캡이 한 번 더 걸려 두 번 눌렸다.
// ============================================================

/// <summary>파이프라인 산출물 — 이 셋이면 전투도 표시도 다 된다.</summary>
public class HeroStatBuild
{
    /// <summary>장수 최종 스탯 (모든 레이어 포함).</summary>
    public UnitStat Stat;

    /// <summary>성장만의 값 (등급·레벨 롤). 전투의 BaseRollStatComponent 기준선.</summary>
    public UnitStat BaseRoll;

    /// <summary>장수 전용 레이어를 걷어낸 사본 — 병사 환산의 원본.</summary>
    public UnitStat SoldierSource;

    /// <summary>병사 전용 패시브 몫 (환산된 병사 스탯에 곱한다).</summary>
    public Dictionary<StatType, float> SoldierPassiveRatios = new();

    /// <summary>병사 전용 패시브 몫 (환산 없이 그대로 더한다).</summary>
    public Dictionary<StatType, float> SoldierPassiveFlats = new();
}

public static class HeroStatPipeline
{
    /// <summary>
    /// 장수 하나의 스탯을 조립한다.
    /// </summary>
    /// <param name="previewBattleStart">
    /// OnBattleStart 패시브가 전투 시작 순간 더할 값을 미리 얹을지.
    ///
    /// ⚠ 전투는 false 다 — 실제로는 패시브 시스템이 전투 시작에 직접 건다.
    ///   true 로 부르면 같은 보너스가 두 번 들어간다. 화면에서 "전투에 들어가면
    ///   이만큼 된다" 를 보여 줄 때만 켠다.
    /// </param>
    public static HeroStatBuild Build(UnitEntry entry, bool previewBattleStart)
    {
        var build = new HeroStatBuild();

        // ── 1. 기본 (등급·레벨 롤) ─────────────────────────
        UnitStat stat  = HeroStatResolver.RollBase(entry);
        build.Stat     = stat;
        build.BaseRoll = stat.Clone();

        // 병사 전용 패시브는 장수 스탯에 넣지 않는다 — 표시·병사 적용이 따로 가져간다
        CollectSoldierPassives(entry, build);

        // ── 병사 환산의 원본 ─────────────────────────────────
        //  ⚠ 장수 전용을 붙이기 전에 뜬다
        //    "부대 전체를 올리는 옵션" 까지가 병사 몫이다. 그 뒤는 장수 혼자다.
        //    (병사 수·지휘력은 이 사본에도 남지만 병사가 쓰지 않는다 —
        //     부대 규모는 장수의 최종 스탯에서 읽는다. GeneralRuntimeBridge 참고)
        build.SoldierSource = stat.CloneWithoutGeneralOnly();

        // ── 2. 장수 전용 층 ──────────────────────────────────
        var passiveDb = PassiveSkillDatabase.Current;
        if (passiveDb != null)
            PassiveSkillApplier.ApplyToGeneralStat(stat, ActivePassives(entry), passiveDb);

        // ── 3. 표시 전용 예상치 ──────────────────────────────
        //  전투에서는 스탯이 아니라 트리거·시스템이 직접 거는 것들이다.
        //  화면에는 "전투에 들어가면 이만큼 된다" 로 보여야 하므로 여기서만 얹는다.
        if (previewBattleStart)
            ApplyBattleStartPreview(entry, build);

        return build;
    }

    /// <summary>등급이 허용하는 만큼의 패시브 슬롯.</summary>
    public static PassiveSkillType[] ActivePassives(UnitEntry entry)
    {
        var (p0, p1, p2) = PassiveSkillRoller.Roll(entry.UnitName);
        byte slots = PassiveSkillRoller.GetActiveSlotCount(entry.Grade);
        var  all   = new[] { p0, p1, p2 };
        var  result = new PassiveSkillType[slots];
        for (int i = 0; i < slots; i++) result[i] = all[i];
        return result;
    }

    // ── 병사 전용 패시브 수집 ────────────────────────────────
    //  장수 스탯에는 한 푼도 안 들어간다. 병사에게는 SoldierStatApplier 가,
    //  화면에는 용병 탭이 이 값을 쓴다 — 둘이 같은 출처를 보게 하려고 여기서 모은다.
    static void CollectSoldierPassives(UnitEntry entry, HeroStatBuild build)
    {
        var db = PassiveSkillDatabase.Current;
        if (db == null) return;

        foreach (var type in ActivePassives(entry))
        {
            var pd = db.Get(type);
            if (pd == null || pd.TriggerType != PassiveTrigger.None) continue;

            foreach (var e in pd.StatModifiers)
            {
                if (e.Target != PassiveSkillApplier.ApplyTarget.Soldier) continue;

                var bucket = e.IsPercent ? build.SoldierPassiveRatios : build.SoldierPassiveFlats;
                bucket[e.Stat] = bucket.TryGetValue(e.Stat, out var cur) ? cur + e.Delta : e.Delta;
            }
        }
    }

    // ── 전투 시작 패시브 예상치 ──────────────────────────────
    //
    //  ⚠ 슬롯 순서대로 앞의 결과를 물려준다
    //    전투에서는 Slot0 → Slot1 → Slot2 로 돌며 각자 그때의 스탯을 읽는다.
    //    여기서도 같은 순서로 누적해야 숫자가 맞는다.
    static void ApplyBattleStartPreview(UnitEntry entry, HeroStatBuild build)
    {
        var db = PassiveSkillDatabase.Current;
        if (db == null) return;

        var preview = new Dictionary<StatType, float>();

        float Current(StatType s)
            => build.Stat.Get(s) + (preview.TryGetValue(s, out var v) ? v : 0f);
        float BaseRoll(StatType s) => build.BaseRoll.Get(s);

        foreach (var type in ActivePassives(entry))
        {
            var pd = db.Get(type);
            if (pd == null || pd.TriggerType != PassiveTrigger.OnBattleStart) continue;
            pd.CollectPreviewStats(Current, BaseRoll, preview);
        }

        // 장수 전용 패시브 층에 얹는다 — 병사에게 가지 않는 보너스라 자리가 같다
        foreach (var kv in preview)
        {
            if (Mathf.Abs(kv.Value) < 0.001f) continue;
            build.Stat.Add(kv.Key, kv.Value, PassiveSkillApplier.GeneralLayerKey);
        }
    }
}
