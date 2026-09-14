using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PassiveSkillApplier.cs
//  패시브 스킬 스폰 시점 즉시 적용 헬퍼.
//
//  역할:
//    PassiveSkillData.StatModifiers 를 순회해
//    ApplyTarget.General / Soldier 항목을 스폰 시 한 번 반영한다.
//
//  ■ 처리 대상
//    ApplyTarget.General → UnitStat.Add(GeneralLayerKey) 로 제너럴 스텟 변경
//                          (장수 전용 층 — 병사 환산에서 걷힌다)
//    ApplyTarget.Soldier → 여기서 하지 않는다. SoldierStatApplier 가 패시브·어빌리티·
//                          유물을 한 번에 모아 적용한다 (출처별로 나눠 부르면
//                          각자 Base 를 읽으며 고쳐 적용 순서가 결과를 바꾼다).
//    ApplyTarget.Runtime → 런타임 이벤트 콜백에서 처리 (여기서는 건너뜀)
//
//  ■ 특수 케이스
//    SoldierEmpower / UnityStrength 는 현재 병사 수에 비례하므로
//    ApplyToGeneralStat() 호출 전 SoldierCount 를 먼저 계산해 전달한다.
// ============================================================

public static class PassiveSkillApplier
{
    /// <summary>장수 전용 패시브가 들어가는 레이어 — 병사 환산에서 걷힌다.</summary>
    public const string GeneralLayerKey = "passive" + UnitStat.GeneralOnlySuffix;

    // ── 적용 대상 ─────────────────────────────────────────────

    public enum ApplyTarget : byte
    {
        General = 0,    // 제너럴 UnitStat 에 즉시 적용 (SpawnEntity 전)
        Soldier = 1,    // 병사 ECS StatComponent.Base 에 즉시 적용 (Initialize 후)
        Runtime = 2,    // 런타임 이벤트 콜백에서 처리 (여기서는 건너뜀)
    }

    // ── 제너럴 UnitStat 적용 ─────────────────────────────────

    /// <summary>
    /// 활성 패시브 목록을 제너럴 UnitStat 에 즉시 적용한다.
    /// SpawnEntity() 호출 전에 실행해야 한다.
    /// </summary>
    public static void ApplyToGeneralStat(
        UnitStat stat,
        PassiveSkillType[] activePassives,
        PassiveSkillDatabase db)
    {
        if (stat == null || activePassives == null || db == null) return;

        // SoldierEmpower 계열 패시브는 병사 수에 비례하므로 먼저 읽음
        float soldierCountSnapshot = stat.Get(StatType.SoldierCount);

        foreach (var passiveType in activePassives)
        {
            var data = db.Get(passiveType);
            if (data == null) continue;
            if (data.TriggerType != PassiveTrigger.None) continue;   // 트리거 패시브 스폰 시 적용 금지

            // 병사 수만큼 배율을 쌓는 패시브 여부
            bool scaleWithSoldiers = passiveType == PassiveSkillType.SoldierEmpowerGeneral
                                  || passiveType == PassiveSkillType.UnityStrength;

#if UNITY_EDITOR
            var sbPassive = new System.Text.StringBuilder();
            sbPassive.AppendLine($"[Passive] 스폰 적용 ▶ {data.SkillName} ({data.Type})");
            if (scaleWithSoldiers)
                sbPassive.AppendLine($"  soldierCount = {soldierCountSnapshot}");
#endif

            foreach (var mod in data.StatModifiers)
            {
                if (mod.Target != ApplyTarget.General) continue;

                float currentValue = stat.Get(mod.Stat);
                // SoldierEmpower 계열: 병사 1명당 mod.Delta% → soldierCount 배 적용
                float soldierMult  = (mod.IsPercent && scaleWithSoldiers) ? soldierCountSnapshot : 1f;
                float delta        = mod.IsPercent ? currentValue * mod.Delta * soldierMult : mod.Delta;

#if UNITY_EDITOR
                sbPassive.AppendLine($"  {mod.Stat,-14} {currentValue:F1} → {currentValue + delta:F1}  ({(mod.IsPercent ? $"{mod.Delta * 100f:+0.#;-0.#}% × {soldierMult}" : $"{delta:+0.#;-0.#}")})");
#endif

                // ⚠ 반드시 장수 전용 레이어다 — 예전엔 "passive" 였다
                //   병사 환산의 원본은 CloneWithoutGeneralOnly() 다.
                //   "passive" 로 넣으면 이 층이 걷히지 않아 **장수 전용 보너스가
                //   병사에게 그대로 흘러갔다**.
                //   "강한 장군, 약한 병사"(장군 +30% / 병사 -20%)가 병사에게
                //   1.30 × 0.80 = 1.04 로 들어가 오히려 세지는 게 그 증상이었다.
                //   Target.General 은 말 그대로 장수만 가리킨다 — 부대 전체를
                //   올리고 싶은 패시브는 Soldier 항목을 따로 적는다 (광전사의 맹약 참고).
                stat.Add(mod.Stat, delta, GeneralLayerKey);
            }

#if UNITY_EDITOR
            UnityEngine.Debug.Log(sbPassive.ToString());
#endif
        }
    }

    // ── 제너럴 크기 배율 ─────────────────────────────────────

    /// <summary>
    /// 활성 패시브 중 GeneralScaleBonusAdd 합산을 반환.
    /// TitanGeneral 등 크기 변경 패시브에 사용.
    /// 없으면 1.0f 반환.
    /// </summary>
    public static float GetGeneralScaleMultiplier(
        PassiveSkillType[] activePassives,
        PassiveSkillDatabase db)
    {
        if (activePassives == null || db == null) return 1f;

        float totalBonus = 0f;
        foreach (var passiveType in activePassives)
        {
            var data = db.Get(passiveType);
            if (data == null) continue;
            totalBonus += data.GeneralScaleBonusAdd;
        }
        return 1f + totalBonus;
    }

    // ── 내부 유틸리티 ────────────────────────────────────────

    /// <summary>activePassives 배열에 특정 타입이 있는지 확인한다.</summary>
    public static bool HasPassive(PassiveSkillType[] activePassives, PassiveSkillType type)
    {
        if (activePassives == null) return false;
        foreach (var p in activePassives)
            if (p == type) return true;
        return false;
    }
}
