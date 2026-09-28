using UnityEditor;
using UnityEngine;

// ============================================================
//  EffectKeyLinker.cs
//  Unity 메뉴 BattleGame > Link Effect Keys to Skills 실행 시
//  모든 ActiveSkillData SO 에 이펙트 풀 키와 자동반납 딜레이를 일괄 적용한다.
//
//  ■ 키 배정 근거 (EffectPrefabGenerator 주석 기반)
//    BaseEffectKey  : 스킬 시작 위치 이펙트 (존 유지형 포함)
//    CasterEffectKey: 시전자 위치 이펙트 (타격/버프 연출)
//    TargetEffectKey: 피격 대상 위치 이펙트
//
//  ■ DespawnDelay 선정 기준
//    - 즉발 이펙트 : 파티클 재생 시간 + 0.5f 여유
//    - 루프 이펙트 : EffectDuration(zone/buff 지속시간) + 1~2f 여유
//    - 메테오 Base : Runner 내부에서 delay + DespawnDelay 로 계산되므로
//                   여유분(1.5f)만 설정하면 됨
//
//  ■ 사용법
//    BattleGame > Link Effect Keys to Skills
// ============================================================

public static class EffectKeyLinker
{
    const string kDataPath = "Assets/_project/Data/Actives";

    [MenuItem(ProjectKMenu.Tool + "스킬 SO 에 이펙트 키 연결", priority = ProjectKMenu.ToolPrio + 1)]
    public static void LinkAll()
    {
        // Link(assetName, baseKey, casterKey, targetKey, despawnDelay)
        // ──────────────────────────────────────────────────────────────────────
        // ① HeavyStrike — 강타 (돌진 타격)
        //    Base  = 출발 먼지, Caster = 타격 섬광, Target = 피격 섬광
        Link("Active_HeavyStrike",
            baseKey:   "FX_Dust_Dash",
            casterKey: "FX_Slash_Impact",
            targetKey: "FX_Slash_Impact",
            delay:     1.0f);

        // ② VolleyFire — 일제 사격
        //    Caster = 발사 연출 (전체 일제사격이므로 피격 이펙트는 자체 처리)
        Link("Active_VolleyFire",
            baseKey:   "",
            casterKey: "FX_Arrow_Volley",
            targetKey: "",
            delay:     0.8f);

        // ③ LeapStrike — 도약 강타
        //    Base = 출발 먼지, Caster = 착지 충격파, Target = 피격 섬광
        Link("Active_LeapStrike",
            baseKey:   "FX_Dust_Dash",
            casterKey: "FX_Leap_Land",
            targetKey: "FX_Slash_Impact",
            delay:     1.5f);

        // ④ HealAura — 치유 오라 (루프, EffectDuration 동안 지속)
        //    Caster = 오라 (loop) → 딜레이를 EffectDuration + 여유로 설정
        Link("Active_HealAura",
            baseKey:   "",
            casterKey: "FX_Heal_Aura",
            targetKey: "",
            delay:     4.5f);

        // ⑤ TargetHeal — 집중 치유 (단일 대상)
        //    Target = 치유 십자 심볼
        Link("Active_TargetHeal",
            baseKey:   "",
            casterKey: "",
            targetKey: "FX_Heal_Target",
            delay:     1.5f);

        // ㉟ SlimeMend — 치유 점액 (힐 슬라임 고유)
        //    Caster = 치유 오라 · Target = 치유 십자 (나은 몬스터마다)
        Link("Active_SlimeMend",
            baseKey:   "",
            casterKey: "FX_Heal_Aura",
            targetKey: "FX_Heal_Target",
            delay:     1.5f);

        // ⑥ ChargeSoldier — 돌격 병사 소환
        //    Base = 병사 출발 먼지, Target = 충돌 임팩트
        Link("Active_ChargeSoldier",
            baseKey:   "FX_Dust_Dash",
            casterKey: "",
            targetKey: "FX_Charge_Impact",
            delay:     1.0f);

        // ⑦ SummonSkeleton — 스켈레톤 소환
        //    Base = 소환진 마법진
        Link("Active_SummonSkeleton",
            baseKey:   "FX_Summon_Circle",
            casterKey: "",
            targetKey: "",
            delay:     2.5f);

        // ⑧ PoisonZone — 독성 지대 (루프 존, EffectDuration ≈ 8f)
        //    Base = 독 안개 (Zone Runner 에서 BaseEffectKey 를 zone 지속 시간만큼 유지)
        Link("Active_PoisonZone",
            baseKey:   "FX_Poison_Zone",
            casterKey: "",
            targetKey: "",
            delay:     10.0f);

        // ⑧-b FlameAura — 화염 오라 (따라다니는 루프 존, EffectDuration 4f)
        //    ⚠ ActiveSkillCreator 가 굽는 순간 이미 BaseEffectKey 를 박아 둔다.
        //      여기 한 줄은 그것과 **같은 값**이어야 한다 — 이 도구를 나중에 돌렸을 때
        //      조용히 덮어써서 불이 사라지는 일이 없게. (둘이 갈리면 어느 쪽이 정본인지
        //      알 길이 없어진다)
        Link("Active_FlameAura",
            baseKey:   "FX_Flame_Zone",
            casterKey: "",
            targetKey: "",
            delay:     6.0f);

        // ⑨ Meteor — 메테오
        //    Base  = 낙하 예고 마커 (Runner: delay + DespawnDelay 로 자동 연장)
        //    Target = 착탄 폭발
        Link("Active_Meteor",
            baseKey:   "FX_Meteor_Warning",
            casterKey: "",
            targetKey: "FX_Meteor_Explosion",
            delay:     2.0f);

        // ⑩ Blizzard — 블리자드 (루프 존, EffectDuration ≈ 8f)
        //    Base = 눈보라 영역
        Link("Active_Blizzard",
            baseKey:   "FX_Blizzard",
            casterKey: "",
            targetKey: "",
            delay:     10.0f);

        // ⑪ SacrificeSoldier — 병사 희생
        //    Caster = 흡수 연출 (시전자), Target = 사망 연출 (병사)
        Link("Active_SacrificeSoldier",
            baseKey:   "",
            casterKey: "FX_Absorb",
            targetKey: "FX_Sacrifice",
            delay:     1.5f);

        // ⑫ Bind — 속박 (지속 상태이상, EffectDuration ≈ 3f)
        //    Target = 속박 링 (루프)
        Link("Active_Bind",
            baseKey:   "",
            casterKey: "",
            targetKey: "FX_Bind",
            delay:     4.0f);

        // ⑬ SuicideSoldier — 자폭 병사
        //    Base = 병사 출발 먼지, Target = 폭발
        Link("Active_SuicideSoldier",
            baseKey:   "FX_Dust_Dash",
            casterKey: "",
            targetKey: "FX_Explosion",
            delay:     2.0f);

        // ⑭ Berserker — 광전사 (루프 버프, EffectDuration ≈ 5f)
        //    Caster = 분노 오라 (loop)
        Link("Active_Berserker",
            baseKey:   "",
            casterKey: "FX_Berserk",
            targetKey: "",
            delay:     6.0f);

        // ⑮ IronShield — 철벽 방어 (루프 버프, EffectDuration ≈ 5f)
        //    Caster = 방어막 링 (loop)
        Link("Active_IronShield",
            baseKey:   "",
            casterKey: "FX_Shield_Up",
            targetKey: "",
            delay:     6.0f);

        // ⑯ ArrowRain — 화살 비 (루프 존, EffectDuration ≈ 5f)
        //    Base = 화살비 낙하 영역 (loop)
        Link("Active_ArrowRain",
            baseKey:   "FX_Arrow_Rain_Zone",
            casterKey: "",
            targetKey: "",
            delay:     7.0f);

        // ⑰ BattleCry — 전투 함성
        //    Caster = 별빛 폭발 (즉발 버프 연출)
        Link("Active_BattleCry",
            baseKey:   "",
            casterKey: "FX_Battle_Cry",
            targetKey: "",
            delay:     1.5f);

        // ⑱ Shockwave — 충격파 (전방 부채꼴)
        //    Caster = 충격파 발사 연출
        Link("Active_Shockwave",
            baseKey:   "",
            casterKey: "FX_Shockwave",
            targetKey: "",
            delay:     1.0f);

        // ⑲ SwiftStrike — 신속 연격 (루프 버프, EffectDuration ≈ 5f)
        //    Caster = 속도 잔상 오라 (loop)
        Link("Active_SwiftStrike",
            baseKey:   "",
            casterKey: "FX_Speed_Up",
            targetKey: "",
            delay:     6.0f);

        // ⑳ SummonElite — 정예 소환
        //    Base = 소환진 마법진 (SummonSkeleton 과 동일)
        Link("Active_SummonElite",
            baseKey:   "FX_Summon_Circle",
            casterKey: "",
            targetKey: "",
            delay:     2.5f);

        // ══════════════════════════════════════════════════════
        //  희귀 스킬 4종 — RareSkillEffectGenerator 가 만든 프리팹
        //  Runner 가 Base/Caster/Target 을 단계별 연출로 나눠 쓴다.
        // ══════════════════════════════════════════════════════

        // ㉑ Bisect — 일도양단
        //    Caster = 검광 응축(①) + 적 발도 표식(②), Base = 참격선(③), Target = 절단 섬광(③)
        //    경직 2초 + 응축 0.35초를 넘겨야 연출이 중간에 잘리지 않는다
        Link("Active_Bisect",
            baseKey:   "FX_Bisect_Slash",
            casterKey: "FX_Bisect_Charge",
            targetKey: "FX_Bisect_Cut",
            delay:     4.0f);

        // ㉒ ArrowStorm — 화살 폭풍 (전방 산탄 3연발)
        //    Caster = 발사 산탄(발마다 조준 방향으로 스폰), Target = 명중
        Link("Active_ArrowStorm",
            baseKey:   "",
            casterKey: "FX_ArrowStorm_Volley",
            targetKey: "FX_ArrowStorm_Burst",
            delay:     1.5f);

        // ㉓ GravityCollapse — 중력 붕괴
        //    Base = 흡입 소용돌이(루프, EffectDuration 2.5 + 여유), Target = 붕괴 폭발
        Link("Active_GravityCollapse",
            baseKey:   "FX_Gravity_Vortex",
            casterKey: "",
            targetKey: "FX_Gravity_Collapse",
            delay:     3.5f);

        // ㉔ Bulwark — 불멸의 방벽
        //    Caster = 방벽 돔(루프, Runner 가 duration+delay 로 직접 연장), Target = 붕괴
        Link("Active_Bulwark",
            baseKey:   "",
            casterKey: "FX_Bulwark_Dome",
            targetKey: "FX_Bulwark_Burst",
            delay:     2.0f);

        // ㉕ ChainLightning — 연쇄 번개
        //    Caster = 시전 방전, Base = 번개 줄기(from→to), Target = 감전 착탄
        Link("Active_ChainLightning",
            baseKey:   "FX_Chain_Bolt",
            casterKey: "FX_Chain_Cast",
            targetKey: "FX_Chain_Hit",
            delay:     1.5f);

        // ㉖ DeathSentence — 사형 선고
        //    Base = 범위 표시(2초 유지), Caster = 대상별 낙인, Target = 처형
        Link("Active_DeathSentence",
            baseKey:   "FX_Sentence_Mark",
            casterKey: "FX_Sentence_Mark",
            targetKey: "FX_Sentence_Execute",
            delay:     1.5f);

        // ㉗ BloodPrice — 피의 대가
        //    Caster = 자해 연출, Base = 전방 충격파, Target = 피격
        Link("Active_BloodPrice",
            baseKey:   "FX_Blood_Burst",
            casterKey: "FX_Sacrifice",
            targetKey: "FX_Explosion",
            delay:     1.5f);

        // ㉘ PiercingDash — 관통 돌진 (1초 쿨 — 연출은 짧고 가볍게)
        Link("Active_PiercingDash",
            baseKey:   "FX_Dust_Dash",
            casterKey: "FX_Dash_Slash",
            targetKey: "FX_Slash_Impact",
            delay:     0.8f);

        // ㉙ WarBanner — 군기 강림 (Base = 군기 오라, 8초 유지)
        Link("Active_WarBanner",
            baseKey:   "FX_Banner_Aura",
            casterKey: "FX_Battle_Cry",
            targetKey: "FX_Speed_Up",
            delay:     2.0f);

        // ㉚ Gravestone — 비석 강림
        //    Base = 낙하 예고, Target = 착탄 폭발, Caster = 소환진(망자가 일어난다)
        Link("Active_Gravestone",
            baseKey:   "FX_Grave_Warning",
            casterKey: "FX_Grave_Rise",
            targetKey: "FX_Grave_Impact",
            delay:     2.5f);

        // ㉛ BossCharge — 돌진 (보스 패턴)
        //    Base = 웅크림(발밑 링), Caster = 돌진 잔상(0.1초마다), Target = 관통 착지
        //    ⚠ 잔상은 경로 내내 반복 스폰된다 — 딜레이가 길면 풀에 그대로 쌓인다
        Link("Active_BossCharge",
            baseKey:   "FX_BossCharge_Windup",
            casterKey: "FX_BossCharge_Trail",
            targetKey: "FX_BossCharge_Impact",
            delay:     1.2f);

        // ㉜ BossSlam — 분쇄 강타 (보스 패턴)
        //    Base = 예고 장판(Runner 가 WindupTime+SlamTime+0.3 로 직접 유지),
        //    Caster = 착탄, Target = 대상별 피격
        //    Base·Caster 는 Runner 가 SlamRadius/3 배로 키워 띄운다
        Link("Active_BossSlam",
            baseKey:   "FX_BossSlam_Warning",
            casterKey: "FX_BossSlam_Impact",
            targetKey: "FX_BossSlam_Hit",
            delay:     1.6f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[EffectKeyLinker] ✓ 32개 액티브 스킬 이펙트 키 연동 완료 (보스 패턴 2종 포함).");
    }

    // ── 내부 헬퍼 ───────────────────────────────────────────────────────

    static void Link(
        string assetName,
        string baseKey,
        string casterKey,
        string targetKey,
        float  delay)
    {
        string path = $"{kDataPath}/{assetName}.asset";
        var    so   = AssetDatabase.LoadAssetAtPath<ActiveSkillData>(path);

        if (so == null)
        {
            Debug.LogWarning($"[EffectKeyLinker] SO 없음: {path}");
            return;
        }

        so.BaseEffectKey      = baseKey;
        so.CasterEffectKey    = casterKey;
        so.TargetEffectKey    = targetKey;
        so.EffectDespawnDelay = delay;

        EditorUtility.SetDirty(so);

        Debug.Log($"[EffectKeyLinker] {assetName}\n" +
                  $"  Base={Quote(baseKey)}  Caster={Quote(casterKey)}  Target={Quote(targetKey)}  Delay={delay}s");
    }

    static string Quote(string s) => string.IsNullOrEmpty(s) ? "(없음)" : s;
}
