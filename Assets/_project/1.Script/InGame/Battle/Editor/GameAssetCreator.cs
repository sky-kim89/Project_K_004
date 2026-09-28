using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Target = PassiveSkillApplier.ApplyTarget;

// ============================================================
//  GameAssetCreator.cs  [Editor Only]
//  패시브 스킬 SO 40종 + Database 자동 생성 도구.
//
//  사용법:
//    Unity 메뉴 → BattleGame → 데이터 생성 → 패시브 스킬 전체 생성
//
//  생성 위치:
//    Assets/_project/Data/Passives/  ← 개별 패시브 SO
//    Assets/_project/PassiveSkillDatabase.asset  ← 기존 파일 갱신
// ============================================================

public static class GameAssetCreator
{
    const string DataRoot    = "Assets/_project/Data";
    const string PassiveDir  = "Assets/_project/Data/Passives";
    const string DBPath      = "Assets/Resources/PassiveSkillDatabase.asset";

    // ── 패시브 스킬 생성 ─────────────────────────────────────

    [MenuItem(ProjectKMenu.Data + "패시브 스킬", priority = ProjectKMenu.DataPrio + 12)]
    public static void CreateAllPassiveSkills()
    {
        // ── 폴더 준비 ─────────────────────────────────────────
        if (!AssetDatabase.IsValidFolder(DataRoot))
            AssetDatabase.CreateFolder("Assets/_project", "Data");
        if (!AssetDatabase.IsValidFolder(PassiveDir))
            AssetDatabase.CreateFolder(DataRoot, "Passives");

        // ── Database 로드 ─────────────────────────────────────
        var db = AssetDatabase.LoadAssetAtPath<PassiveSkillDatabase>(DBPath);
        if (db == null)
        {
            Debug.LogError($"[GameAssetCreator] PassiveSkillDatabase 없음: {DBPath}\n" +
                           "Assets/_project/ 에 PassiveSkillDatabase.asset 이 있는지 확인하세요.");
            return;
        }

        db.Entries.Clear();

        // ── ① 병사 강화 ───────────────────────────────────────

        Make<PassiveSkillData>(db, PassiveSkillType.ExtraSoldiers, "ExtraSoldiers",
            "병사 수 +5명",
            "장군 소속 병사 수를 5명 추가합니다.",
            PassiveTrigger.None,
            Mod(StatType.SoldierCount, 5f,    false, Target.General));

        Make<PassiveSkillData>(db, PassiveSkillType.SoldierCombatBoost, "SoldierCombatBoost",
            "병사 전투 강화",
            "병사의 공격력과 이동속도를 각각 20%·10% 증가시킵니다.",
            PassiveTrigger.None,
            Mod(StatType.Attack,    0.20f, true, Target.Soldier),
            Mod(StatType.MoveSpeed, 0.10f, true, Target.Soldier));

        Make<PassiveSkillData>(db, PassiveSkillType.SoldierHorde, "SoldierHorde",
            "군중 전술",
            "병사 수 +10명. 단, 병사 공격력·체력 -10%.",
            PassiveTrigger.None,
            Mod(StatType.SoldierCount, 10f,   false, Target.General),
            Mod(StatType.Attack,      -0.10f, true,  Target.Soldier),
            Mod(StatType.MaxHp,       -0.10f, true,  Target.Soldier));

        Make<PassiveSkillData>(db, PassiveSkillType.VanguardAura, "VanguardAura",
            "선봉 오라",
            "병사 방어율 +10%.",
            PassiveTrigger.None,
            Mod(StatType.Defense, 0.10f, false, Target.Soldier));

        // ── ② 교환 ────────────────────────────────────────────

        Make<PassiveSkillData>(db, PassiveSkillType.WeakGeneralStrongSoldier, "WeakGeneralStrongSoldier",
            "약한 장군, 강한 병사",
            "장군 공격력·체력 -20%. 병사 공격력·체력 +30%.",
            PassiveTrigger.None,
            Mod(StatType.MaxHp,  -0.20f, true, Target.General),
            Mod(StatType.Attack, -0.20f, true, Target.General),
            Mod(StatType.MaxHp,   0.30f, true, Target.Soldier),
            Mod(StatType.Attack,  0.30f, true, Target.Soldier));

        Make<PassiveSkillData>(db, PassiveSkillType.StrongGeneralWeakSoldier, "StrongGeneralWeakSoldier",
            "강한 장군, 약한 병사",
            "병사 공격력·체력 -20%. 장군 공격력·체력 +30%.",
            PassiveTrigger.None,
            Mod(StatType.MaxHp,  -0.20f, true, Target.Soldier),
            Mod(StatType.Attack, -0.20f, true, Target.Soldier),
            Mod(StatType.MaxHp,   0.30f, true, Target.General),
            Mod(StatType.Attack,  0.30f, true, Target.General));

        Make<PassiveSkillData>(db, PassiveSkillType.WeakGeneralMoreSoldiers, "WeakGeneralMoreSoldiers",
            "희생의 지휘",
            "장군 공격력·체력 -15%. 병사 수 +8명.",
            PassiveTrigger.None,
            Mod(StatType.MaxHp,        -0.15f, true,  Target.General),
            Mod(StatType.Attack,       -0.15f, true,  Target.General),
            Mod(StatType.SoldierCount,  8f,    false, Target.General));

        Make<PassiveSkillData>(db, PassiveSkillType.BerserkerPact, "BerserkerPact",
            "광전사의 맹약",
            "전체 공격력·공격속도 +25%. 방어율 -15%.",
            PassiveTrigger.None,
            Mod(StatType.Attack,      0.25f,  true,  Target.General),
            Mod(StatType.AttackSpeed, 0.25f,  true,  Target.General),
            Mod(StatType.Defense,    -0.15f,  false, Target.General),
            Mod(StatType.Attack,      0.25f,  true,  Target.Soldier),
            Mod(StatType.AttackSpeed, 0.25f,  true,  Target.Soldier),
            Mod(StatType.Defense,    -0.15f,  false, Target.Soldier));

        // ── ③ 제너럴 강화 ─────────────────────────────────────

        Make<PassiveSkillData>(db, PassiveSkillType.GeneralCombatBoost, "GeneralCombatBoost",
            "장군 전투 강화",
            "장군 공격력·이동속도 +15%.",
            PassiveTrigger.None,
            Mod(StatType.Attack,    0.15f, true, Target.General),
            Mod(StatType.MoveSpeed, 0.15f, true, Target.General));

        var titan = Make<PassiveSkillData>(db, PassiveSkillType.TitanGeneral, "TitanGeneral",
            "거인 장군",
            "장군 체력·공격력 +30%·+20%. 공격·이동속도 -15%. 크기 +30%.",
            PassiveTrigger.None,
            Mod(StatType.MaxHp,       0.30f,  true, Target.General),
            Mod(StatType.Attack,      0.20f,  true, Target.General),
            Mod(StatType.AttackSpeed, -0.15f, true, Target.General),
            Mod(StatType.MoveSpeed,   -0.15f, true, Target.General));
        titan.GeneralScaleBonusAdd = 0.3f;
        EditorUtility.SetDirty(titan);

        Make<PassiveSkillData>(db, PassiveSkillType.CommanderFury, "CommanderFury",
            "지휘관의 분노",
            "장군 크리티컬 확률 +15%, 크리티컬 배율 +0.5.",
            PassiveTrigger.None,
            Mod(StatType.CritChance, 0.15f, false, Target.General),
            Mod(StatType.CritDamage, 0.50f, false, Target.General));

        // ── ④ 시너지 ──────────────────────────────────────────

        Make<PassiveSkillData>(db, PassiveSkillType.SoldierEmpowerGeneral, "SoldierEmpowerGeneral",
            "병력의 힘",
            "병사 1명당 장군 공격력·체력 +1%.",
            PassiveTrigger.None,
            Mod(StatType.Attack, 0.01f, true, Target.General),
            Mod(StatType.MaxHp,  0.01f, true, Target.General));

        Make<PassiveSkillData>(db, PassiveSkillType.UnityStrength, "UnityStrength",
            "결속의 힘",
            "병사 1명당 장군 공격력·체력 +1.5%.",
            PassiveTrigger.None,
            Mod(StatType.Attack, 0.015f, true, Target.General),
            Mod(StatType.MaxHp,  0.015f, true, Target.General));

        Make<PassiveSoldierDeathEmpower>(db, PassiveSkillType.SoldierDeathEmpower, "SoldierDeathEmpower",
            "병사의 유산",
            "병사 사망 1명당 장군 공격력 +2%, 체력 +1%.",
            PassiveTrigger.OnSoldierDeath,
            Mod(StatType.Attack, 0.02f, true, Target.General),
            Mod(StatType.MaxHp,  0.01f, true, Target.General));

        Make<PassiveSkillData>(db, PassiveSkillType.SacrificeRitual, "SacrificeRitual",
            "희생 의식",
            "병사 5명 희생 → 장군 공격력·체력 +20%.",
            PassiveTrigger.None,
            Mod(StatType.SoldierCount, -5f,   false, Target.General),  // 비용: 시작 병사 -5명
            Mod(StatType.Attack,       0.20f, true,  Target.General),
            Mod(StatType.MaxHp,        0.20f, true,  Target.General));

        // ── ⑤ 조건부 ─────────────────────────────────────────

        Make<PassiveBloodPact>(db, PassiveSkillType.BloodPact, "BloodPact",
            "피의 계약",
            "장군 체력이 낮을수록 공격력 최대 +50%.",
            PassiveTrigger.OnHit,
            Mod(StatType.Attack, 0.50f, true, Target.General));

        var ironWill = Make<PassiveIronWill>(db, PassiveSkillType.IronWill, "IronWill",
            "강철 의지",
            "체력 50% 이하가 되면 체력 +20% (늘어난 만큼 회복)·공격력 +10% (1회).",
            PassiveTrigger.OnHit,
            // ⚠ 체력 쪽을 더 크게 (사용자 지적, 2026-09-15) — 버티는 패시브라 공격보다 체력이 먼저다.
            //   늘어난 최대 체력만큼 현재 체력도 채운다 (PassiveIronWill) — 안 채우면 비율만 떨어진다.
            Mod(StatType.Attack, 0.10f, true, Target.General),
            Mod(StatType.MaxHp,  0.20f, true, Target.General));
        ironWill.HpThreshold = 0.5f;
        EditorUtility.SetDirty(ironWill);

        var lastStand = Make<PassiveLastStand>(db, PassiveSkillType.LastStand, "LastStand",
            "최후의 항전",
            "병사 수가 초기의 50% 이하 시 남은 병사 공격력·체력 +30%·+20% (1회).",
            PassiveTrigger.OnSoldierDeath,
            Mod(StatType.Attack, 0.30f, true, Target.Soldier),
            Mod(StatType.MaxHp,  0.20f, true, Target.Soldier));
        lastStand.SoldierThreshold = 0.5f;
        EditorUtility.SetDirty(lastStand);

        // ── ⑥ OnAttack 트리거 ────────────────────────────────

        var vampiric = Make<PassiveVampiricStrike>(db, PassiveSkillType.VampiricStrike, "VampiricStrike",
            "흡혈 공격",
            "공격 시 가한 피해의 10%를 즉시 체력 회복.",
            PassiveTrigger.OnAttack);
        vampiric.VampireRatio = 0.10f;
        EditorUtility.SetDirty(vampiric);

        var strengthStack = Make<PassiveStrengthStack>(db, PassiveSkillType.StrengthStack, "StrengthStack",
            "연격 스택",
            "연속 공격마다 공격력 +8 누적 (최대 5스택).",
            PassiveTrigger.OnAttack);
        strengthStack.AttackBonusPerStack = 8f;
        strengthStack.MaxStacks           = 5;
        EditorUtility.SetDirty(strengthStack);

        var soldierMorale = Make<PassiveSoldierMorale>(db, PassiveSkillType.SoldierMorale, "SoldierMorale",
            "병사 고무",
            "공격 시 30% 확률로 병사 전체 공격력 +12를 3초 동안 버프.",
            PassiveTrigger.OnAttack);
        soldierMorale.TriggerChance   = 0.30f;
        soldierMorale.AttackBuffDelta = 12f;
        soldierMorale.BuffDuration    = 3f;
        EditorUtility.SetDirty(soldierMorale);

        // ── ⑦ OnHit 트리거 ───────────────────────────────────

        var defenseShield = Make<PassiveDefenseShield>(db, PassiveSkillType.DefenseShield, "DefenseShield",
            "방어 강화",
            "피격 시 방어율 +10%를 3초 동안 버프.",
            PassiveTrigger.OnHit);
        defenseShield.DefenseBuff  = 0.10f;
        defenseShield.BuffDuration = 3f;
        EditorUtility.SetDirty(defenseShield);

        var quickRecovery = Make<PassiveQuickRecovery>(db, PassiveSkillType.QuickRecovery, "QuickRecovery",
            "긴급 회복",
            "피격 시 최대체력의 0.5%를 즉시 회복.",
            PassiveTrigger.OnHit);
        quickRecovery.HealRatio = 0.005f;
        EditorUtility.SetDirty(quickRecovery);

        var counterStrike = Make<PassiveCounterStrike>(db, PassiveSkillType.CounterStrike, "CounterStrike",
            "피격 반격",
            "피격 시 40% 확률로 공격력 +20%를 5초 동안 버프.",
            PassiveTrigger.OnHit);
        counterStrike.TriggerChance    = 0.40f;
        counterStrike.AttackBonusRatio = 0.20f;
        counterStrike.BuffDuration     = 5f;
        EditorUtility.SetDirty(counterStrike);

        // ── ⑧ OnEnemyKill 트리거 ─────────────────────────────

        var killMomentum = Make<PassiveKillMomentum>(db, PassiveSkillType.KillMomentum, "KillMomentum",
            "처치 가속",
            "처치마다 이동속도 +0.15 누적 (최대 5스택).",
            PassiveTrigger.OnEnemyKill);
        killMomentum.SpeedBonusPerKill = 0.15f;
        killMomentum.MaxStacks         = 5;
        EditorUtility.SetDirty(killMomentum);

        var killEmpower = Make<PassiveKillEmpower>(db, PassiveSkillType.KillEmpower, "KillEmpower",
            "처치 강화",
            "처치마다 공격력 +10 누적 (최대 5스택).",
            PassiveTrigger.OnEnemyKill);
        killEmpower.AttackBonusPerKill = 10f;
        killEmpower.MaxStacks          = 5;
        EditorUtility.SetDirty(killEmpower);

        Make<PassiveKillHeal>(db, PassiveSkillType.KillHeal, "KillHeal",
            "처치 회복",
            "처치 시 최대체력의 5%를 즉시 회복.",
            PassiveTrigger.OnEnemyKill);

        var soldierVigor = Make<PassiveSoldierVigor>(db, PassiveSkillType.SoldierVigor, "SoldierVigor",
            "병사 결의",
            "처치 시 병사 전체 공격력 +15를 4초 동안 버프.",
            PassiveTrigger.OnEnemyKill);
        soldierVigor.AttackBuffDelta = 15f;
        soldierVigor.BuffDuration    = 4f;
        EditorUtility.SetDirty(soldierVigor);

        var lootHunter = Make<PassiveLootHunter>(db, PassiveSkillType.LootHunter, "LootHunter",
            "전리품 사냥",
            "적 처치 시 골드 +15 획득.",
            PassiveTrigger.OnEnemyKill);
        lootHunter.GoldPerKill = 15;
        EditorUtility.SetDirty(lootHunter);

        var slaughterer = Make<PassiveSlaughterer>(db, PassiveSkillType.Slaughterer, "Slaughterer",
            "도살자",
            "처치마다 현재 공격력의 3% 추가 누적 (최대 10스택).",
            PassiveTrigger.OnEnemyKill);
        slaughterer.AttackPerKillRatio = 0.03f;
        slaughterer.MaxStacks          = 10;
        EditorUtility.SetDirty(slaughterer);

        // ── ⑨ OnSoldierDeath 트리거 ──────────────────────────

        var sacrificeAbsorb = Make<PassiveSacrificeAbsorb>(db, PassiveSkillType.SacrificeAbsorb, "SacrificeAbsorb",
            "희생 흡수",
            "병사 사망 시 사망 1명당 즉시 체력 +30 회복.",
            PassiveTrigger.OnSoldierDeath);
        sacrificeAbsorb.HealPerDeath = 30f;
        EditorUtility.SetDirty(sacrificeAbsorb);

        // ── ⑩ OnSkillUse 트리거 ──────────────────────────────

        var skillAdrenaline = Make<PassiveSkillAdrenaline>(db, PassiveSkillType.SkillAdrenaline, "SkillAdrenaline",
            "스킬 아드레날린",
            "스킬 사용 시 공격력 +20·공격속도 +0.3을 5초 동안 버프.",
            PassiveTrigger.OnSkillUse);
        skillAdrenaline.AttackBuff   = 20f;
        skillAdrenaline.AtkSpeedBuff = 0.3f;
        skillAdrenaline.BuffDuration = 5f;
        EditorUtility.SetDirty(skillAdrenaline);

        var skillInstinct = Make<PassiveSkillInstinct>(db, PassiveSkillType.SkillInstinct, "SkillInstinct",
            "생존 본능",
            "스킬 사용 시 50% 확률로 최대체력의 8%를 즉시 회복.",
            PassiveTrigger.OnSkillUse);
        skillInstinct.TriggerChance = 0.50f;
        skillInstinct.HealRatio     = 0.08f;
        EditorUtility.SetDirty(skillInstinct);

        var skillRally = Make<PassiveSkillRally>(db, PassiveSkillType.SkillRally, "SkillRally",
            "전투 집결",
            "스킬 사용 시 병사 전체 공격력 +20·이동속도 +0.5를 6초 동안 버프.",
            PassiveTrigger.OnSkillUse);
        skillRally.AttackBuffDelta = 20f;
        skillRally.SpeedBuffDelta  = 0.5f;
        skillRally.BuffDuration    = 6f;
        EditorUtility.SetDirty(skillRally);

        // ── ⑪ OnBattleStart 트리거 ───────────────────────────

        var goldenPower = Make<PassiveGoldenPower>(db, PassiveSkillType.GoldenPower, "GoldenPower",
            "황금의 힘",
            "전투 시작 시 보유 골드 300당 공격력·최대체력 +1%.",
            PassiveTrigger.OnBattleStart);
        goldenPower.GoldPerBonus  = 300;
        goldenPower.AttackPercent = 0.01f;
        goldenPower.HpPercent     = 0.01f;
        EditorUtility.SetDirty(goldenPower);

        var swiftAssault = Make<PassiveSwiftAssault>(db, PassiveSkillType.SwiftAssault, "SwiftAssault",
            "속전속결",
            "이동속도 증가분만큼 공격속도도 동일하게 증가.",
            PassiveTrigger.OnBattleStart);
        swiftAssault.Ratio = 1.0f;
        EditorUtility.SetDirty(swiftAssault);

        var steelBody = Make<PassiveSteelBody>(db, PassiveSkillType.SteelBody, "SteelBody",
            "강철 체력",
            "전투 시작 시 최대체력의 3%를 공격력으로 전환.",
            PassiveTrigger.OnBattleStart);
        steelBody.HpToAttackRatio = 0.03f;
        EditorUtility.SetDirty(steelBody);

        var shieldEdge = Make<PassiveShieldEdge>(db, PassiveSkillType.ShieldEdge, "ShieldEdge",
            "방패의 날",
            "방어율 10%당 공격력 +4%.",
            PassiveTrigger.OnBattleStart);
        shieldEdge.DefenseStep   = 0.10f;
        shieldEdge.AttackPerStep = 0.04f;
        EditorUtility.SetDirty(shieldEdge);

        var focusedFire = Make<PassiveFocusedFire>(db, PassiveSkillType.FocusedFire, "FocusedFire",
            "집중 사격",
            "공격속도 0.1당 치명타 확률 +0.5%.",
            PassiveTrigger.OnBattleStart);
        focusedFire.CritPerAtkSpd = 0.05f;
        EditorUtility.SetDirty(focusedFire);

        // ── ⑫ None 트리거 (즉시 적용) ────────────────────────

        Make<PassiveSkillData>(db, PassiveSkillType.WideRange, "WideRange",
            "사거리 확장",
            "사정거리 +10%.",
            PassiveTrigger.None,
            Mod(StatType.AttackRange, 0.10f, true, Target.General));

        // ── 저장 ─────────────────────────────────────────────
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[GameAssetCreator] 완료 — 패시브 {db.Entries.Count}종 생성, Database 갱신: {DBPath}");
        EditorGUIUtility.PingObject(db);
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────

    /// <summary>SO 생성 또는 로드 후 기본값 설정. Database Entries 에 등록.</summary>
    static T Make<T>(
        PassiveSkillDatabase db,
        PassiveSkillType type,
        string fileName,
        string skillName,
        string description,
        PassiveTrigger trigger,
        params PassiveSkillData.StatModifierEntry[] mods) where T : PassiveSkillData
    {
        string path = $"{PassiveDir}/{fileName}.asset";
        var so = AssetDatabase.LoadAssetAtPath<T>(path);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
        }

        so.Type          = type;
        so.SkillName     = skillName;
        so.Description   = description;
        so.TriggerType   = trigger;
        so.StatModifiers = new List<PassiveSkillData.StatModifierEntry>(mods);

        // 아이콘 — 파일명이 enum 이름과 같다 (PassiveIconGenerator 가 그렇게 저장한다).
        // 아직 아이콘을 안 만들었으면 기존 값을 유지한다 (덮어써서 지우지 않는다).
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(
            $"Assets/_project/3.Textures/Icons/Passives/passive_{type}.png");
        if (icon != null) so.Icon = icon;

        EditorUtility.SetDirty(so);
        db.Entries.Add(so);
        return so;
    }

    static PassiveSkillData.StatModifierEntry Mod(
        StatType stat, float delta, bool isPercent, Target target)
        => new PassiveSkillData.StatModifierEntry
        {
            Stat      = stat,
            Delta     = delta,
            IsPercent = isPercent,
            Target    = target,
        };
}
