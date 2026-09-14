using UnityEngine;
using UnityEditor;

// ============================================================
//  ActiveSkillCreator.cs  [Editor Only]
//  액티브 스킬 SO + ActiveSkillDatabase 자동 생성 도구.
//
//  ⚠ 이 도구가 DB 의 정본이다 — Entries 를 손으로 편집하지 말 것
//    CreateAllActiveSkills 가 맨 앞에서 db.Entries.Clear() 를 한다.
//    여기에 넣지 않은 스킬은 다시 구울 때마다 DB 에서 사라진다.
//
//  사용법:
//    Unity 메뉴 → BattleGame → 데이터 생성 → 액티브 스킬 전체 생성
//
//  생성 위치:
//    Assets/_project/Data/Actives/  ← 개별 액티브 SO
//    Assets/_project/ActiveSkillDatabase.asset  ← 기존 파일 갱신
//
// ------------------------------------------------------------
//  ⚠ 쿨타임 기준 (2026-08 리밸런싱) — 숫자를 만지기 전에 읽을 것
// ------------------------------------------------------------
//  쿨을 "스킬 1회 성능"으로 잡으면 안 된다. 광역기는 맞는 적 수만큼
//  성능이 곱해지고, 단일기는 아무리 배율을 올려도 그 자리에 머문다.
//
//   1. 광역기는 실제 타격 수로 환산한다
//      메테오 500% 는 1명 기준이 아니다. 20~30명이 겹치는 후반 웨이브에선
//      한 방이 강타(500% 단일) 수십 방과 맞먹는다. 그래서 쿨이 길다.
//   2. 장판형은 딜로스를 깎아준다
//      블리자드·독성 지대는 앞줄을 죽이며 전진하므로 지속시간의 절반쯤은
//      빈 땅을 때린다. 명목 총딜의 6할만 계산한다.
//   3. 병사를 태우는 스킬은 페널티를 쿨에서 돌려준다
//      자폭 병사·병사 희생은 자원(병사)을 잃는다. 같은 딜이면 쿨이 더 짧다.
//   4. 소환·버프는 공격기보다 짧게
//      소환수는 즉발 딜이 아니라 "시간을 두고 회수되는 딜"이고,
//      버프는 전투가 끝나면 사라진다. 쌓을 시간을 줘야 빌드가 성립한다.
//   5. 직업 보정 — 법사 ×1.1, 그 외 ×0.8
//      법사는 쿨감 옵션을 추가로 받을 창구가 많아 실효 쿨이 더 내려간다.
//      아래 숫자에는 이 보정이 이미 반영돼 있다.
// ============================================================

public static class ActiveSkillCreator
{
    const string DataRoot   = "Assets/_project/Data";
    const string ActiveDir  = "Assets/_project/Data/Actives";
    const string DBPath     = "Assets/Resources/ActiveSkillDatabase.asset";

    [MenuItem(ProjectKMenu.Data + "액티브 스킬", priority = ProjectKMenu.DataPrio + 11)]
    public static void CreateAllActiveSkills()
    {
        // ── 폴더 준비 ─────────────────────────────────────────
        if (!AssetDatabase.IsValidFolder(DataRoot))
            AssetDatabase.CreateFolder("Assets/_project", "Data");
        if (!AssetDatabase.IsValidFolder(ActiveDir))
            AssetDatabase.CreateFolder(DataRoot, "Actives");

        // ── Database 로드 또는 생성 ───────────────────────────
        var db = AssetDatabase.LoadAssetAtPath<ActiveSkillDatabase>(DBPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<ActiveSkillDatabase>();
            AssetDatabase.CreateAsset(db, DBPath);
        }
        db.Entries.Clear();

        // ── ① 강타 (전사·방패) ────────────────────────────────
        var heavyStrike = Make<ActiveHeavyStrike>(db,
            id          : ActiveSkillId.HeavyStrike,
            fileName    : "Active_HeavyStrike",
            skillName   : "강타",
            description : "사정거리 내 적을 즉시 강타. 공격력 500% 단일 타격 + 강한 넉백.",
            cooldown    : 3f,
            effectValue : 1f,
            radius      : 0f,
            duration    : 0f,
            jobs        : new[] { UnitJob.Knight, UnitJob.ShieldBearer });
        heavyStrike.DamageMultiplier = 5f;
        heavyStrike.KnockbackMult    = 5f;
        EditorUtility.SetDirty(heavyStrike);

        // ── ② 일제 사격 (궁수·법사) ──────────────────────────
        Make<ActiveVolleyFire>(db,
            id          : ActiveSkillId.VolleyFire,
            fileName    : "Active_VolleyFire",
            skillName   : "일제 사격",
            description : "제너럴과 소속 병사 전체가 공격력 150%로 현재 타겟에 즉시 공격을 발동한다.",
            cooldown    : 13f,
            effectValue : 1.5f,
            radius      : 0f,
            duration    : 0f,
            jobs        : new[] { UnitJob.Archer, UnitJob.Mage });

        // ── ③ 도약 강타 (전사·방패) ──────────────────────────
        var leapStrike = Make<ActiveLeapStrike>(db,
            id          : ActiveSkillId.LeapStrike,
            fileName    : "Active_LeapStrike",
            skillName   : "도약 강타",
            description : "전방으로 도약하여 착지 반경 내 모든 적을 공격력 250% 강타 + 넉백.",
            cooldown    : 10f,
            effectValue : 1f,
            radius      : 2.5f,
            duration    : 0f,
            jobs        : new[] { UnitJob.Knight, UnitJob.ShieldBearer });
        leapStrike.DamageMultiplier = 2.5f;
        leapStrike.LeapSpeed        = 18f;
        leapStrike.KnockbackMult    = 4f;
        EditorUtility.SetDirty(leapStrike);

        // ── ④ 치유 오라 (공통) ───────────────────────────────
        Make<ActiveHealAura>(db,
            id          : ActiveSkillId.HealAura,
            fileName    : "Active_HealAura",
            skillName   : "치유 오라",
            description : "피해 입은 아군 장군 중 랜덤 1명과 그 휘하 병사 전체의 체력을 최대 HP의 25% 즉시 회복.",
            cooldown    : 13f,
            effectValue : 0.25f,
            radius      : 0f,
            duration    : 0f,
            jobs        : new UnitJob[0]);

        // ── ⑤ 집중 치유 (공통) ──────────────────────────────
        Make<ActiveTargetHeal>(db,
            id          : ActiveSkillId.TargetHeal,
            fileName    : "Active_TargetHeal",
            skillName   : "집중 치유",
            description : "아군 장군 중 체력 비율이 가장 낮은 장군 1명을 최대 HP의 40% 집중 치유.",
            cooldown    : 7f,
            effectValue : 0.4f,
            radius      : 0f,
            duration    : 0f,
            jobs        : new UnitJob[0]);

        // ── ⑥ 돌격 병사 (방패) ──────────────────────────────
        //  ⚠ 이 스킬은 병사를 3명 소환한다 (돌진 딜 + 소환 둘 다 하는 스킬).
        //    딜 배율 200% 만 보고 "약한 스킬"로 판단해 쿨을 내리지 말 것 —
        //    스켈레톤 소환(2기) 보다 소환량이 많고 돌진 피해까지 얹힌다.
        var chargeSoldier = Make<ActiveChargeSoldier>(db,
            id          : ActiveSkillId.ChargeSoldier,
            fileName    : "Active_ChargeSoldier",
            skillName   : "돌격 병사",
            description : "후방에 돌격 병사 3명을 소환해 전방으로 돌진. 경로 위 적에게 공격력×200% 피해 + 넉백.",
            cooldown    : 11f,
            effectValue : 2f,
            radius      : 0f,
            duration    : 0f,
            jobs        : new[] { UnitJob.ShieldBearer });
        chargeSoldier.ChargeSpeed    = 18f;
        chargeSoldier.HitRadius      = 0.8f;
        chargeSoldier.ChargeDistance = 12f;
        chargeSoldier.KnockbackForce = 5f;
        EditorUtility.SetDirty(chargeSoldier);

        // ── ⑦ 스켈레톤 소환 (공통) ──────────────────────────
        var summonSkeleton = Make<ActiveSummonSkeleton>(db,
            id          : ActiveSkillId.SummonSkeleton,
            fileName    : "Active_SummonSkeleton",
            skillName   : "스켈레톤 소환",
            description : "시전자 스텟 70% 수준의 스켈레톤 2기를 소환한다.",
            cooldown    : 9f,
            effectValue : 2f,
            radius      : 0f,
            duration    : 0f,
            jobs        : new UnitJob[0]);
        summonSkeleton.SkeletonPoolKey = "Soldier";
        summonSkeleton.StatRatio       = 0.7f;
        summonSkeleton.SpawnRadius     = 1.5f;
        EditorUtility.SetDirty(summonSkeleton);

        // ── ⑧ 독성 지대 (법사·궁수) ─────────────────────────
        var poisonZone = Make<ActivePoisonZone>(db,
            id          : ActiveSkillId.PoisonZone,
            fileName    : "Active_PoisonZone",
            skillName   : "독성 지대",
            description : "타겟 위치에 독성 지대 생성. 이동속도 50% 감소 + 0.5초마다 공격력 30% 지속 피해.",
            cooldown    : 13f,
            effectValue : 0.30f,
            radius      : 2.5f,
            duration    : 6f,
            jobs        : new[] { UnitJob.Mage, UnitJob.Archer });
        poisonZone.MoveSlowMultiplier = 0.5f;
        poisonZone.TickInterval       = 0.5f;
        EditorUtility.SetDirty(poisonZone);

        // ── ⑨ 메테오 (법사) ─────────────────────────────────
        var meteor = Make<ActiveMeteor>(db,
            id          : ActiveSkillId.Meteor,
            fileName    : "Active_Meteor",
            skillName   : "메테오",
            description : "1.5초 후 타겟 위치에 메테오 낙하. 공격력 500% AoE 피해 + 강한 넉백.",
            cooldown    : 31f,
            effectValue : 1f,
            radius      : 3.5f,
            duration    : 1.5f,
            jobs        : new[] { UnitJob.Mage });
        meteor.DamageMultiplier = 5f;
        meteor.KnockbackMult    = 8f;
        EditorUtility.SetDirty(meteor);

        // ── ⑩ 블리자드 (법사) ───────────────────────────────
        var blizzard = Make<ActiveBlizzard>(db,
            id          : ActiveSkillId.Blizzard,
            fileName    : "Active_Blizzard",
            skillName   : "블리자드",
            description : "타겟 위치에 블리자드 지대. 이동속도·공격속도 감소 + 0.5초마다 공격력 40% 지속 피해.",
            cooldown    : 24f,
            effectValue : 0.40f,
            radius      : 3f,
            duration    : 8f,
            jobs        : new[] { UnitJob.Mage });
        blizzard.MoveSlowMultiplier   = 0.25f;   // 이동 -75%
        blizzard.AttackSlowMultiplier = 0.35f;   // 공속 -65%
        blizzard.TickInterval         = 0.5f;
        EditorUtility.SetDirty(blizzard);

        // ── ⑪ 병사 희생 (공통) ──────────────────────────────
        Make<ActiveSacrificeSoldier>(db,
            id          : ActiveSkillId.SacrificeSoldier,
            fileName    : "Active_SacrificeSoldier",
            skillName   : "병사 희생",
            description : "체력 최저 병사를 즉사시키고, 그 공격력의 80%를 시전자 공격력으로 흡수한다.\n" +
                          "스테이지가 끝날 때까지 유지되며 쓸수록 계속 누적된다.",
            cooldown    : 20f,
            effectValue : 0.8f,
            radius      : 0f,
            duration    : 999f,
            jobs        : new UnitJob[0]);

        // ── ⑫ 속박 (공통) ───────────────────────────────────
        Make<ActiveBind>(db,
            id          : ActiveSkillId.Bind,
            fileName    : "Active_Bind",
            skillName   : "속박",
            description : "현재 타겟을 3초 동안 행동불능으로 만들고 매초 공격력 30% 지속 피해를 가한다.",
            cooldown    : 11f,
            effectValue : 0.3f,
            radius      : 0f,
            duration    : 3f,
            jobs        : new UnitJob[0]);

        // ── ⑬ 자폭 병사 (법사) ──────────────────────────────
        var suicideSoldier = Make<ActiveSuicideSoldier>(db,
            id          : ActiveSkillId.SuicideSoldier,
            fileName    : "Active_SuicideSoldier",
            skillName   : "자폭 병사",
            description : "병사를 포물선 궤도로 던져 착탄 시 공격력 300% 범위 폭발 피해 + 넉백.",
            cooldown    : 15f,
            effectValue : 3f,
            radius      : 2.5f,
            duration    : 0f,
            jobs        : new[] { UnitJob.Mage });
        suicideSoldier.FlightDuration = 0.5f;
        suicideSoldier.ArcHeight      = 2f;
        suicideSoldier.KnockbackMult  = 7f;
        EditorUtility.SetDirty(suicideSoldier);

        // ── ⑭ 광전사 (전사) ─────────────────────────────────
        Make<ActiveBerserker>(db,
            id          : ActiveSkillId.Berserker,
            fileName    : "Active_Berserker",
            skillName   : "광전사",
            description : "시전자와 소속 병사 전체의 공격속도를 8초 동안 1.8배로 증가.",
            cooldown    : 10f,
            effectValue : 1.8f,
            radius      : 0f,
            duration    : 8f,
            jobs        : new[] { UnitJob.Knight });

        // ── ⑮ 철벽 방어 (방패) ──────────────────────────────
        Make<ActiveIronShield>(db,
            id          : ActiveSkillId.IronShield,
            fileName    : "Active_IronShield",
            skillName   : "철벽 방어",
            description : "시전자의 방어율을 8초 동안 +30% 증가. 지속 시간 동안 도발 상태가 되어 적의 우선 타겟이 됨.",
            cooldown    : 8f,
            effectValue : 0.3f,
            radius      : 0f,
            duration    : 8f,
            jobs        : new[] { UnitJob.ShieldBearer });

        // ── ⑯ 화살 비 (궁수) ────────────────────────────────
        var arrowRain = Make<ActiveArrowRain>(db,
            id          : ActiveSkillId.ArrowRain,
            fileName    : "Active_ArrowRain",
            skillName   : "화살 비",
            description : "타겟 위치에 5초 동안 화살 비를 내려 0.4초마다 공격력 50% 범위 지속 피해.",
            cooldown    : 9f,
            effectValue : 0.50f,
            radius      : 2f,
            duration    : 5f,
            jobs        : new[] { UnitJob.Archer });
        arrowRain.TickInterval = 0.4f;
        EditorUtility.SetDirty(arrowRain);

        // ── ⑰ 전투 함성 (전사·방패) ─────────────────────────
        Make<ActiveBattleCry>(db,
            id          : ActiveSkillId.BattleCry,
            fileName    : "Active_BattleCry",
            skillName   : "전투 함성",
            description : "반경 5m 내 모든 아군의 공격력을 8초 동안 1.3배로 증가.",
            cooldown    : 13f,
            effectValue : 1.3f,
            radius      : 5f,
            duration    : 8f,
            jobs        : new[] { UnitJob.Knight, UnitJob.ShieldBearer });

        // ── ⑱ 충격파 (전사) ─────────────────────────────────
        var shockwave = Make<ActiveShockwave>(db,
            id          : ActiveSkillId.Shockwave,
            fileName    : "Active_Shockwave",
            skillName   : "충격파",
            description : "전방 120도 부채꼴 범위의 모든 적에게 공격력 150% 피해 + 강한 넉백.",
            cooldown    : 8f,
            effectValue : 1.5f,
            radius      : 4f,
            duration    : 0f,
            jobs        : new[] { UnitJob.Knight });
        shockwave.ConeAngleDegrees = 120f;
        shockwave.KnockbackMult    = 6f;
        EditorUtility.SetDirty(shockwave);

        // ── ⑲ 신속 연격 (궁수) ──────────────────────────────
        Make<ActiveSwiftStrike>(db,
            id          : ActiveSkillId.SwiftStrike,
            fileName    : "Active_SwiftStrike",
            skillName   : "신속 연격",
            description : "시전자와 소속 병사 전체의 공격속도를 6초 동안 2배로 증가.",
            cooldown    : 14f,
            effectValue : 2f,
            radius      : 0f,
            duration    : 6f,
            jobs        : new[] { UnitJob.Archer });

        // ── ⑳ 정예 소환 (법사) ──────────────────────────────
        var summonElite = Make<ActiveSummonElite>(db,
            id          : ActiveSkillId.SummonElite,
            fileName    : "Active_SummonElite",
            skillName   : "정예 소환",
            description : "시전자 스텟 200% 수준의 정예 병사 3기를 소환한다.",
            cooldown    : 31f,
            effectValue : 3f,
            radius      : 0f,
            duration    : 0f,
            jobs        : new[] { UnitJob.Mage });
        summonElite.ElitePoolKey = "Soldier";
        summonElite.StatRatio    = 2.0f;
        summonElite.SpawnRadius  = 1.5f;
        EditorUtility.SetDirty(summonElite);

        // ══════════════════════════════════════════════════════
        //  희귀 스킬 (직업당 부대에 1명만 — RareSkillArbiter)
        //  IsRare=true 는 추첨 가중치가 등급에 비례하고(Normal 은 등장 안 함),
        //  같은 직업 장수가 겹치면 한 명만 남는다.
        // ══════════════════════════════════════════════════════

        // ── ㉑ 일도양단 (기사 · 희귀) ────────────────────────
        var bisect = Make<ActiveBisect>(db,
            id          : ActiveSkillId.Bisect,
            fileName    : "Active_Bisect",
            skillName   : "일도양단",
            description : "전방 직선 위의 적을 2초간 얼어붙게 한 뒤 한 번에 베어 넘긴다. " +
                          "공격력 600% 피해 + 강한 넉백. 시전 중에는 그 자리에 선다.",
            cooldown    : 19f,
            effectValue : 1f,
            radius      : 22f,     // 참격선 길이 — 타겟이 더 멀면 그 뒤까지 자동으로 늘어난다
            duration    : 0f,
            jobs        : new[] { UnitJob.Knight });
        bisect.IsRare           = true;
        bisect.DamageMultiplier = 6f;
        bisect.LineWidth        = 9f;    // 4.5 → 배폭
        bisect.ChargeTime       = 0.35f;
        bisect.TrembleTime      = 2f;
        bisect.KnockbackMult    = 7f;
        bisect.ReachMargin      = 6f;
        EditorUtility.SetDirty(bisect);

        // ── ㉒ 화살 폭풍 (궁수 · 희귀) ───────────────────────
        var arrowStorm = Make<ActiveArrowStorm>(db,
            id          : ActiveSkillId.ArrowStorm,
            fileName    : "Active_ArrowStorm",
            skillName   : "화살 폭풍",
            description : "전방으로 화살 산탄을 3연발 퍼붓는다. 발사 중에는 그 자리에 서며, " +
                          "맞은 적은 강하게 밀려나고 이동속도가 누적 감소한다. " +
                          "마지막 발은 공격력 450% 피해 + 경직.",
            cooldown    : 19f,
            effectValue : 1f,
            radius      : 9f,      // 산탄 사거리 (부채꼴 반경)
            duration    : 0f,
            jobs        : new[] { UnitJob.Archer });
        arrowStorm.IsRare                = true;
        arrowStorm.DamageMultiplier      = 2.0f;
        arrowStorm.FinalDamageMultiplier = 4.5f;
        arrowStorm.WaveCount             = 3;
        arrowStorm.WaveInterval          = 0.5f;    // 발 사이 텀
        arrowStorm.WarningTime           = 0.35f;
        arrowStorm.ConeAngleDegrees      = 60f;
        arrowStorm.SlowRatio             = 0.4f;
        arrowStorm.SlowDuration          = 3f;
        arrowStorm.FinalStunDuration     = 1.5f;
        arrowStorm.KnockbackMult         = 9f;   // 산탄 — 세게 민다
        EditorUtility.SetDirty(arrowStorm);

        // ── ㉓ 중력 붕괴 (법사 · 희귀) ───────────────────────
        var gravity = Make<ActiveGravityCollapse>(db,
            id          : ActiveSkillId.GravityCollapse,
            fileName    : "Active_GravityCollapse",
            skillName   : "중력 붕괴",
            description : "붕괴점을 만들어 2.5초간 적을 한곳으로 빨아들이며 발을 묶는다. " +
                          "종료 시 공격력 800% 폭발.",
            cooldown    : 42f,
            effectValue : 1f,
            radius      : 7f,      // 흡입 반경
            duration    : 2.5f,
            jobs        : new[] { UnitJob.Mage });
        gravity.IsRare            = true;
        gravity.DotMultiplier     = 0.8f;
        gravity.ExplodeMultiplier = 8f;
        gravity.TickInterval      = 0.15f;
        gravity.PullForce         = 3.5f;
        gravity.RootRatio         = 0.9f;
        gravity.ExplodeKnockback  = 8f;
        EditorUtility.SetDirty(gravity);

        // ── ㉔ 불멸의 방벽 (방패병 · 희귀) ───────────────────
        var bulwark = Make<ActiveBulwark>(db,
            id          : ActiveSkillId.Bulwark,
            fileName    : "Active_Bulwark",
            skillName   : "불멸의 방벽",
            description : "5초간 아군 전체의 방어율을 45%p 끌어올린다. 방벽이 무너질 때 " +
                          "공격력 700% 폭발 + 아군 전체 최대 체력 25% 회복.",
            cooldown    : 24f,
            effectValue : 1f,
            radius      : 6.5f,    // 방벽 폭발 반경
            duration    : 5f,
            jobs        : new[] { UnitJob.ShieldBearer });
        bulwark.IsRare            = true;
        bulwark.DefenseBonus      = 0.45f;
        bulwark.ExplodeMultiplier = 7f;
        bulwark.KnockbackMult     = 6f;
        bulwark.HealRatio         = 0.25f;
        EditorUtility.SetDirty(bulwark);

        // ══════════════════════════════════════════════════════
        //  희귀 스킬 — 공통 (직업 제한 없음 · 전체에서 1명만)
        //  AllowedJobs 를 비우면 RareSkillArbiter 가 전 직업 이름에서 주인을 고른다.
        // ══════════════════════════════════════════════════════

        // ── ㉕ 연쇄 번개 (공통 · 희귀) ───────────────────────
        var chain = Make<ActiveChainLightning>(db,
            id          : ActiveSkillId.ChainLightning,
            fileName    : "Active_ChainLightning",
            skillName   : "연쇄 번개",
            description : "번개가 맞은 적마다 둘로 갈라지며 4번 번진다 (최대 15명). " +
                          "갈라질 때마다 피해가 15%씩 커진다. " +
                          "맞은 적은 2초간 감전되어 발이 묶인다.",
            cooldown    : 13f,
            effectValue : 1f,
            radius      : 5f,      // 다음 대상을 찾는 거리
            duration    : 0f,
            jobs        : new UnitJob[0]);
        chain.IsRare           = true;
        chain.DamageMultiplier = 2.2f;
        chain.MaxWaves         = 4;      // 1 → 2 → 4 → 8
        chain.SplitCount       = 2;
        chain.DamageGrowth     = 0.15f;
        chain.ChainInterval    = 0.09f;
        // 줄기가 오래 남으면 15줄이 한꺼번에 떠서 화면이 파랗게 덮인다
        chain.BoltLifetime     = 0.14f;
        chain.ShockSlowRatio   = 0.7f;
        chain.ShockDuration    = 2f;
        chain.KnockbackMult    = 1.5f;
        EditorUtility.SetDirty(chain);

        // ── ㉖ 사형 선고 (공통 · 희귀) ───────────────────────
        var sentence = Make<ActiveDeathSentence>(db,
            id          : ActiveSkillId.DeathSentence,
            fileName    : "Active_DeathSentence",
            skillName   : "사형 선고",
            description : "범위 내 적을 즉시 선고·처형한다. " +
                          "체력 35% 이하인 적은 즉사하고, 살아남은 적은 공격력 400% 피해를 받는다. " +
                          "처형한 수만큼 시전자 공격력이 누적된다.",
            cooldown    : 31f,
            effectValue : 1f,
            radius      : 6f,
            duration    : 0.8f,    // 인장·낙인이 화면에 남는 시간 (처형은 즉발)
            jobs        : new UnitJob[0]);
        sentence.IsRare           = true;
        sentence.ExecuteHpRatio   = 0.35f;
        sentence.DamageMultiplier = 4f;
        sentence.AttackPerExecute = 6f;
        sentence.ExecuteBosses    = false;   // 보스 즉사는 막는다 — 최종 콘텐츠가 무너진다
        sentence.KnockbackMult    = 3f;
        sentence.SkullEffectKey   = "FX_Death_Skull";
        EditorUtility.SetDirty(sentence);

        // ── ㉗ 피의 대가 (방패병 · 희귀) ────────────────────
        var blood = Make<ActiveBloodPrice>(db,
            id          : ActiveSkillId.BloodPrice,
            fileName    : "Active_BloodPrice",
            skillName   : "피의 대가",
            description : "현재 체력의 30%를 태워 전방을 쓸어버린다. 잃은 체력의 250% + " +
                          "공격력 200% 광역 피해 + 강한 넉백. 체력이 많을수록 강해진다.",
            cooldown    : 11f,
            effectValue : 1f,
            // 체력 30% 를 태우는 한 방이다 — 사거리·각도가 좁으면 지를 이유가 없다
            radius      : 9f,
            duration    : 0f,
            // 잃은 체력에 비례해 피해가 오르는 스킬 — 체력·방어가 가장 높은 방패병 전용
            jobs        : new[] { UnitJob.ShieldBearer });
        blood.IsRare           = true;
        blood.HpCostRatio      = 0.3f;
        blood.DamagePerHp      = 2.5f;
        blood.AttackMultiplier = 2f;
        blood.ConeAngleDegrees = 160f;   // 전방을 거의 반원으로 쓸어버린다
        blood.KnockbackMult    = 7f;
        blood.ChargeTime       = 0.3f;
        EditorUtility.SetDirty(blood);

        // ── ㉘ 관통 돌진 (근거리 · 희귀) ────────────────────
        //  1초 쿨 평타형 — 배율을 낮게 잡아야 다른 스킬을 압도하지 않는다
        var dash = Make<ActivePiercingDash>(db,
            id          : ActiveSkillId.PiercingDash,
            fileName    : "Active_PiercingDash",
            skillName   : "관통 돌진",
            description : "전방으로 짧게 돌진하며 직선 위의 적을 모두 관통 타격한다. " +
                          "공격력 100% 피해. 쿨타임 1초.",
            cooldown    : 1f,
            effectValue : 1f,
            radius      : 4f,      // 돌진 거리 = 관통 길이
            duration    : 0f,
            jobs        : new[] { UnitJob.Knight, UnitJob.ShieldBearer });
        dash.IsRare           = true;
        dash.DamageMultiplier = 1.0f;
        dash.LineWidth        = 1.6f;
        dash.DashSpeed        = 26f;
        dash.KnockbackMult    = 1.2f;
        EditorUtility.SetDirty(dash);

        // ── ㉙ 군기 강림 (공통 · 희귀) ──────────────────────
        var banner = Make<ActiveWarBanner>(db,
            id          : ActiveSkillId.WarBanner,
            fileName    : "Active_WarBanner",
            skillName   : "군기 강림",
            description : "군기를 세워 주변 아군의 공격력·공격속도를 40%, 이동속도를 15% " +
                          "8초 동안 끌어올린다.",
            cooldown    : 24f,
            effectValue : 1f,
            radius      : 6f,
            duration    : 8f,
            jobs        : new UnitJob[0]);
        banner.IsRare                = true;
        banner.AttackMultiplier      = 1.4f;
        banner.AttackSpeedMultiplier = 1.4f;
        banner.MoveSpeedMultiplier   = 1.15f;
        banner.FlashTime             = 1f;    // 깃발은 1초만 펄럭이고 반경 표시만 남는다
        EditorUtility.SetDirty(banner);

        // ── ㉚ 비석 강림 (법사 · 희귀) ──────────────────────
        var grave = Make<ActiveGravestone>(db,
            id          : ActiveSkillId.Gravestone,
            fileName    : "Active_Gravestone",
            skillName   : "비석 강림",
            description : "비석 12기가 순서대로 우수수 떨어져 꽂힌다. 착탄 지점마다 " +
                          "공격력 150% 피해 + 넉백, 그 자리에서 스켈레톤이 하나씩 일어난다.",
            // 스켈레톤 12기가 그대로 남는다 — 쿨이 짧으면 아군이 계속 불어나 전장이 잠긴다
            cooldown    : 50f,
            effectValue : 12f,     // 비석 개수
            radius      : 2f,      // 비석 1개의 착탄 반경
            duration    : 0.5f,    // 예고 → 착탄
            // 소환 스킬이라 법사 전용 — 주인도 법사 이름 중에서만 뽑힌다
            jobs        : new[] { UnitJob.Mage });
        grave.IsRare           = true;
        grave.DamageMultiplier = 1.5f;
        grave.ScatterRadius    = 5f;
        grave.DropInterval     = 0.12f;
        grave.SkeletonPoolKey  = "Soldier";
        grave.StatRatio        = 0.45f;
        grave.KnockbackMult    = 5f;
        EditorUtility.SetDirty(grave);

        // ── 소환사 전용 — 대표 종족 소환 ─────────────────────
        //
        //  ⚠ AllowedJobs 를 비워 두지만 **용사에게 뽑히지 않는다**
        //    비워 두면 보통 '공통 스킬' 로 잡혀 아군 추첨에 들어간다.
        //    이 스킬은 ActiveSkillId.IsSummonerOnly() 로 추첨에서 통째로
        //    걸러진다 (ActiveSkillRoller) — 직업으로 막는 것이 아니다.
        //
        //  ⚠ 쿨다운은 뜻이 없다
        //    이 스킬의 제한은 쿨다운이 아니라 **스테이지당 사용 횟수**다
        //    (SummonerSkillRule). 그래도 0 으로 두지 않는다 — 어떤 경로로든
        //    자동 발동에 걸리면 매 프레임 소환이 도는 사고가 난다.
        //
        //  ⚠ 무엇을 부르는지는 여기 없다
        //    소환사가 들고 있다 (SummonerData.SignatureSummonSpecies).
        //    한 SO 가 일곱 소환사를 덮는 이유다.
        Make<ActiveSummonSignature>(db,
            id          : ActiveSkillId.SummonSignature,
            fileName    : "Active_SummonSignature",
            skillName   : "권속 소환",
            description : "소환사의 대표 권속을 마나 없이 불러낸다. " +
                          "불려 나온 권속은 카드로 낸 것과 똑같이 시너지·품질·소환력을 받는다. " +
                          "다만 시너지 카운트는 올리지 않으며, 판이 끝나면 사라진다.",
            cooldown    : 8f,
            effectValue : 1f,
            radius      : 0f,
            duration    : 0f,
            jobs        : new UnitJob[0]);

        // ── ㉟ 치유 점액 (힐 슬라임 고유 · 몬스터 전용) ──────
        //  ⚠ 용사 추첨에서는 IsMonsterOnly() 가 거른다 (ActiveSkillRoller).
        //  ⚠ 원작 치유 두 종은 장군만 찾아 몬스터가 쓰면 아무도 안 낫는다 — 그래서 따로 둔다.
        var slimeMend = Make<ActiveSlimeMend>(db,
            id          : ActiveSkillId.SlimeMend,
            fileName    : "Active_SlimeMend",
            skillName   : "치유 점액",
            description : "주변의 다친 몬스터 아군 최대 6마리를 체력이 낮은 순으로 " +
                          "각자 최대 체력의 8%만큼 회복시킨다.",
            cooldown    : 6f,
            effectValue : 0.08f,
            radius      : 3.5f,
            duration    : 0f,
            jobs        : new UnitJob[0]);
        slimeMend.CasterEffectKey = "FX_Heal_Aura";
        slimeMend.TargetEffectKey = "FX_Heal_Target";
        EditorUtility.SetDirty(slimeMend);

        // ── ㊱ 마나 폭발 (대마법사 시그니처 · 소환사 전용, 2026-09-12) ──
        //  ⚠ 시그니처는 원래 마나를 안 쓰지만 이 스킬은 **남은 마나를 연료로** 쓴다.
        //    제한은 여전히 스테이지당 횟수(SummonerSkillRule)다. 쿨다운은 안전장치일 뿐이다.
        //  ⚠ 용사 추첨에서는 IsSummonerOnly() 가 거른다.
        var manaBurst = Make<ActiveManaBurst>(db,
            id          : ActiveSkillId.ManaBurst,
            fileName    : "Active_ManaBurst",
            skillName   : "마나 폭발",
            description : "남은 마나의 절반을 태워, 탭한 자리 주변의 적 전부에게 " +
                          "태운 마나 1당 최대 체력의 2% 피해를 준다 (방어율 무시 · 보스는 절반).",
            cooldown    : 8f,
            effectValue : 1f,
            radius      : 4f,
            duration    : 0f,
            jobs        : new UnitJob[0]);
        manaBurst.TargetEffectKey = "FX_Meteor_Explosion";
        EditorUtility.SetDirty(manaBurst);

        // ── ㉛ 돌진 (우두머리 전용 패턴) ─────────────────────
        //  예전엔 BossPatternSystem + BossComponent 필드로 돌던 행동이었다.
        //  쿨다운·타겟팅·이펙트를 스킬이 이미 갖고 있어 스킬로 옮겼다.
        //  ⚠ AllowedJobs 를 비워도 아군 추첨에 들어가지 않는다 —
        //    ActiveSkillRoller 가 IsBossPattern() 으로 따로 거른다.
        var bossCharge = Make<ActiveBossCharge>(db,
            id          : ActiveSkillId.BossCharge,
            fileName    : "Active_BossCharge",
            skillName   : "돌진",
            description : "몸을 날려 적진을 관통한다. 경로 위의 적을 한 번씩 밀어내며 " +
                          "공격력 220% 피해를 준다.",
            cooldown    : 9f,
            effectValue : 1f,
            radius      : 2.6f,
            duration    : 0f,
            jobs        : new UnitJob[0]);
        bossCharge.ChargeSpeed      = 26f;
        bossCharge.OvershootDistance= 8f;
        bossCharge.WindupTime       = 0.45f;
        bossCharge.WindupBackstep   = 1.2f;
        bossCharge.RecoverTime      = 0.35f;
        bossCharge.DamageMultiplier = 2.2f;
        bossCharge.HitRadius        = 2.6f;
        bossCharge.KnockbackMult    = 7f;
        EditorUtility.SetDirty(bossCharge);

        // ── ㉜ 분쇄 강타 (보스 전용 패턴) ────────────────────
        //  돌진과 정반대로 잡았다 — 제자리 / 넓은 원 / 위로 띄운다.
        //  둘 다 "달려와서 때린다" 면 패턴을 두 개 만든 의미가 없다.
        var bossSlam = Make<ActiveBossSlam>(db,
            id          : ActiveSkillId.BossSlam,
            fileName    : "Active_BossSlam",
            skillName   : "분쇄 강타",
            description : "도약해 내려찍으며 발밑을 분쇄한다. 반경 7 안의 적에게 " +
                          "공격력 350% 피해와 강한 넉백. 가장자리는 피해가 줄어든다.",
            cooldown    : 13f,
            effectValue : 1f,
            radius      : 7f,
            duration    : 0f,
            jobs        : new UnitJob[0]);
        bossSlam.WindupTime       = 0.8f;
        bossSlam.JumpHeight       = 2.2f;
        bossSlam.SlamTime         = 0.22f;
        bossSlam.RecoverTime      = 0.5f;
        bossSlam.DamageMultiplier = 3.5f;
        bossSlam.SlamRadius       = 7f;
        bossSlam.KnockbackMult    = 9f;
        EditorUtility.SetDirty(bossSlam);

        // ── ㉝ 광폭화 (보스 전용 패턴) ───────────────────────
        //  피해가 0 인 유일한 보스 패턴이다. 때리는 게 아니라 '시계' 다 —
        //  1분마다 한 겹씩, 교착을 끝내는 쪽으로 저울을 기울인다.
        var bossEnrage = Make<ActiveBossEnrage>(db,
            id          : ActiveSkillId.BossEnrage,
            fileName    : "Active_BossEnrage",
            skillName   : "광폭화",
            description : "1분마다 광폭화가 한 겹씩 영구히 쌓인다. 겹당 공격력 +50%, " +
                          "방어율 관통 +10%, 몸집 +10%. 오래 끌수록 손쓸 수 없어진다.",
            cooldown    : 60f,
            effectValue : 1f,
            radius      : 0f,
            duration    : 0f,
            jobs        : new UnitJob[0]);
        bossEnrage.AttackPerStack   = 0.5f;
        bossEnrage.PiercePerStack   = 0.1f;
        bossEnrage.SizePerStack     = 0.1f;
        bossEnrage.MaxSizeStacks    = 10;    // 몸집만 2배에서 멈춘다 (공격력·관통은 계속)
        bossEnrage.CasterEffectKey  = "FX_Berserk";
        EditorUtility.SetDirty(bossEnrage);

        // ── 저장 ─────────────────────────────────────────────
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ActiveSkillCreator] 완료 — 액티브 {db.Entries.Count}종 생성, Database 갱신: {DBPath}");
        EditorGUIUtility.PingObject(db);
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────

    /// <summary>SO 생성 또는 기존 파일 로드 후 공통 필드 설정. Database Entries 에 등록.</summary>
    static T Make<T>(
        ActiveSkillDatabase db,
        ActiveSkillId id,
        string fileName,
        string skillName,
        string description,
        float cooldown,
        float effectValue,
        float radius,
        float duration,
        UnitJob[] jobs) where T : ActiveSkillData
    {
        string path = $"{ActiveDir}/{fileName}.asset";
        var so = AssetDatabase.LoadAssetAtPath<T>(path);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
        }

        so.SkillId        = id;
        so.SkillName      = skillName;
        so.Description    = description;
        so.Cooldown       = cooldown;
        so.EffectValue    = effectValue;
        so.EffectRadius   = radius;
        so.EffectDuration = duration;
        so.AllowedJobs    = jobs;

        EditorUtility.SetDirty(so);
        db.Entries.Add(so);
        return so;
    }
}
