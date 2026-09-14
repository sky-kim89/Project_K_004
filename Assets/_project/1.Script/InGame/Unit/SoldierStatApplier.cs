using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  SoldierStatApplier.cs
//  병사 한 기에게 붙는 모든 스탯 보너스를 **한 번에** 적용하는 단일 진입점.
//
//  ■ 왜 한 곳으로 모았나 — 순서에 따라 결과가 달라졌다
//    예전엔 패시브 → 어빌리티 → 유물 순으로 각자 StatComponent.Base 를
//    읽으면서 동시에 고쳤다. 비율 보너스가 그때그때의 Base 를 기준으로
//    계산되므로 적용 순서가 결과를 바꿨다.
//
//      "병사 공격 -20%" 와 "병사 공격 +50%" 를 같이 들었을 때
//        -20% 먼저 : 100 → 80 → 120      (+20%)
//        +50% 먼저 : 100 → 150 → 120     (+20%)   ← 우연히 같아 보이지만
//      여기에 세 번째가 끼면 갈라진다. 곱연산 누적이라 교환법칙이 없다.
//
//    지금은 **환산 직후의 Base 를 스냅샷으로 고정**하고, 모든 출처의 비율을
//    더한 뒤 한 번만 곱한다. -20% 와 +50% 는 언제 들어오든 +30% 다.
//    패시브가 "병사를 깎고 장수를 올리는" 식으로 부호를 섞어도 안전하다.
//
//  ■ 여기서 붙이는 것 — '병사 전용' 옵션뿐이다
//    · 패시브 : ApplyTarget.Soldier 인 항목
//
//    공통 옵션(전체·직업·범위)과 패시브·특성·도감·장비의 평범한 스탯 증가는
//    여기 오지 않는다. 이미 장수 스탯에 들어가 있고 병사는 그것을 환산해
//    받기 때문이다 (GeneralRuntimeBridge._soldierSourceStat).
//    장수 전용(Unit_General)만 그 사본에서 빠진다.
//
//  ⚠ 음수 보너스가 스탯을 0 아래로 끌지 않게 막는다
//    공격력이 음수가 되면 피해 계산이 회복으로 뒤집힌다.
// ============================================================

public static class SoldierStatApplier
{
    /// <summary>
    /// 병사 엔티티에 병사 전용 패시브 보너스를 한 번에 적용한다.
    /// 반드시 SoldierRuntimeBridge.Initialize 뒤에 부를 것 — 환산된 Base 가
    /// 스냅샷의 기준이다.
    /// </summary>
    public static void Apply(
        Entity soldierEntity, EntityManager em,
        PassiveSkillType[] activePassives, PassiveSkillDatabase passiveDb)
    {
        if (soldierEntity == Entity.Null || !em.Exists(soldierEntity)) return;
        if (!em.HasComponent<StatComponent>(soldierEntity)) return;

        var ratios = new Dictionary<StatType, float>();   // 비율 — 스냅샷에 곱한다
        var flats  = new Dictionary<StatType, float>();   // 절대값 — 그대로 더한다

        CollectPassives(activePassives, passiveDb, ratios, flats);
        // ⚠ 유물은 병사에게 걸리지 않는다 (2026-09-07) — 병사는 용사 휘하,
        //   즉 적이다. 유물 스탯 노드는 전부 Unit_Monster 다.

        if (ratios.Count == 0 && flats.Count == 0) return;

        var sc = em.GetComponentData<StatComponent>(soldierEntity);

        // ⚠ 스냅샷을 먼저 뜬다 — 아래 루프가 Base 를 고치기 시작하면
        //   그 뒤의 비율이 이미 바뀐 값을 기준으로 잡힌다 (= 순서 의존).
        var snapshot = sc.Base;

        foreach (var kv in ratios)
            sc.Base[kv.Key] = sc.Base[kv.Key] + snapshot[kv.Key] * kv.Value;

        foreach (var kv in flats)
            sc.Base[kv.Key] = sc.Base[kv.Key] + kv.Value;

        ClampNonNegative(ref sc, ratios, flats);

        // Final 동기화 — UnitStatusEffectSystem 이 다음 프레임에 재계산한다
        sc.Final = sc.Base;
        em.SetComponentData(soldierEntity, sc);

        // MaxHp 가 바뀌었으면 현재 체력을 다시 채운다
        if (ratios.ContainsKey(StatType.MaxHp) || flats.ContainsKey(StatType.MaxHp))
            em.SetComponentData(soldierEntity, new HealthComponent
                { CurrentHp = sc.Base[StatType.MaxHp] });
    }

    // ── 수집 ─────────────────────────────────────────────────

    static void CollectPassives(
        PassiveSkillType[] actives, PassiveSkillDatabase db,
        Dictionary<StatType, float> ratios, Dictionary<StatType, float> flats)
    {
        if (actives == null || db == null) return;

        foreach (var type in actives)
        {
            var data = db.Get(type);
            if (data == null) continue;
            if (data.TriggerType != PassiveTrigger.None) continue;   // 트리거형은 스폰 시 적용 금지

            foreach (var mod in data.StatModifiers)
            {
                if (mod.Target != PassiveSkillApplier.ApplyTarget.Soldier) continue;
                Accumulate(mod.IsPercent ? ratios : flats, mod.Stat, mod.Delta);
            }
        }
    }

    // ── 유틸 ─────────────────────────────────────────────────

    static void Accumulate(Dictionary<StatType, float> map, StatType type, float value)
    {
        if (value == 0f) return;
        map[type] = map.TryGetValue(type, out float cur) ? cur + value : value;
    }

    /// <summary>
    /// 건드린 스탯만 0 아래로 안 가게 막는다.
    /// ⚠ 전 스탯을 훑지 않는다 — 원래 음수가 정상인 스탯을 건드리면 안 된다.
    /// </summary>
    static void ClampNonNegative(ref StatComponent sc,
                                 Dictionary<StatType, float> ratios,
                                 Dictionary<StatType, float> flats)
    {
        foreach (var kv in ratios)
            if (sc.Base[kv.Key] < 0f) sc.Base[kv.Key] = 0f;
        foreach (var kv in flats)
            if (sc.Base[kv.Key] < 0f) sc.Base[kv.Key] = 0f;
    }
}
