using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  SummonerSkillRule.cs
//  소환사 시그니처 스킬의 **남은 횟수** 정본. 화면과 발동이 같은 값을 본다.
//
//  ■ 마나를 내지 않는다 — 세기는 횟수로만 조절한다
//    마나는 런당 1회 지급이 설계의 뿌리다(CLAUDE.md v2 5.0절). 무료 스킬에
//    마나를 매기면 "스킬을 아끼려고 소환을 줄이는" 상태가 생겨 두 자원이
//    서로를 잡아먹는다. 그래서 제한은 횟수 하나다.
//
//  ■ 제한은 **스테이지당 횟수 하나뿐**이다 (사용자 확정, 2026-09-04)
//    판이 바뀌면 차오르므로 **저장하지 않는다.**
//    ⚠ 저장하면 앱을 껐다 켜서 되돌리는 길이 생긴다 — 저장할 것이 없는 편이 안전하다.
//    ⚠ 한때 "런 중 총 N회" 도 있었다. 없앴다 — 두 종류가 있으면 화면이
//      "판당인가 런당인가" 를 먼저 설명해야 했다.
//
//  ■ 발동 자체는 기존 스킬 파이프라인이 한다
//    쿨다운·타겟팅·이펙트를 새로 만들지 않는다. 소환사 엔티티에
//    UseActiveSkillTag 를 붙이면 ActiveSkillCooldownSystem 이 그다음을 굴린다.
// ============================================================

public static class SummonerSkillRule
{
    /// <summary>남은 횟수가 바뀌었다. 버튼이 구독한다.</summary>
    public static event Action Changed;

    /// <summary>이번 스테이지에 쓴 횟수 (스테이지 제한형).</summary>
    static int _usedThisStage;

    static SummonerData Summoner
        => SummonerRuntimeBridge.Current != null ? SummonerRuntimeBridge.Current.Data : null;

    // ── 상태 ─────────────────────────────────────────────────

    public static bool HasSkill
    {
        get
        {
            SummonerData s = Summoner;
            return s != null && s.HasSignatureSkill;
        }
    }

    public static ActiveSkillId SkillId
        => Summoner != null ? Summoner.SignatureSkill : ActiveSkillId.None;

    /// <summary>지금 몇 번 남았나.</summary>
    public static int Remaining
    {
        get
        {
            SummonerData s = Summoner;
            if (s == null || !s.HasSignatureSkill) return 0;

            // 특성 '집중' 이 스테이지당 횟수를 늘린다.
            // ⚠ 이벤트 '봉인된 지팡이' 도 같은 자리에 얹힌다 (RunBoonData.SignatureBonus).
            //   저장고는 그쪽이고 규칙은 여기다 — 화면과 발동이 이 값 하나를 본다.
            int uses = s.SkillUses
                     + (RunPerkRule.Has(RunPerk.Focus) ? RunPerkRule.FocusExtraUses : 0)
                     + (UserDataManager.Instance?.Get<RunBoonData>()?.SignatureBonus ?? 0);

            return Mathf.Max(0, uses - _usedThisStage);
        }
    }

    /// <summary>
    /// 지금 쓸 수 있나.
    ///
    /// ⚠ 대기 중에도 쓸 수 있다
    ///   소환과 같은 규칙이다 — 판이 시작되기 전에 미리 깔아 두는 것이
    ///   이 게임의 준비 시간이 하는 일이다. 소환 자체를 막는 상태
    ///   (StageLoopDirector.CanSummon) 에서만 함께 막힌다.
    /// </summary>
    public static bool CanUse
    {
        get
        {
            if (!HasSkill || Remaining <= 0) return false;

            var director = StageLoopDirector.Instance;
            return director == null || director.CanSummon;
        }
    }

    // ── 사용 ─────────────────────────────────────────────────

    // ── 겨냥 ─────────────────────────────────────────────────

    /// <summary>
    /// 버튼을 눌러 <b>겨냥 중</b>인가. 다음 탭이 이 스킬을 쓴다.
    ///
    /// ■ 왜 두 단계인가 — 카드와 같은 규칙이다 (사용자 확정, 2026-09-06)
    ///   카드도 "누르고 → 전장을 탭" 이다. 스킬만 누르는 즉시 나가면 어디에
    ///   떨어질지 고를 수 없고, 실제로 소환 위치를 코드가 대신 정하고 있었다.
    ///   같은 격자에 앉은 버튼이 다르게 동작하면 그것도 배워야 할 규칙이 된다.
    /// </summary>
    public static bool IsArmed { get; private set; }

    /// <summary>겨냥 상태가 바뀌었다. 버튼이 구독한다.</summary>
    public static event Action ArmedChanged;

    /// <summary>겨냥을 켠다/끈다. 쓸 수 없으면 켜지지 않는다.</summary>
    public static void ToggleArm()
    {
        if (IsArmed) { Cancel(); return; }
        if (!CanUse) return;

        IsArmed = true;
        ArmedChanged?.Invoke();
    }

    /// <summary>
    /// 겨냥을 끈다.
    ///
    /// 푸는 자리는 셋뿐이다 — 버튼을 다시 누를 때(ToggleArm) · 카드를 고를 때
    /// (SummonController.HandleCardSelected) · 횟수를 다 썼을 때(TryUseAt).
    /// ⚠ 한 번 썼다고 풀지 않는다 — 카드와 같은 조작이어야 한다.
    /// </summary>
    public static void Cancel()
    {
        if (!IsArmed) return;

        IsArmed = false;
        ArmedChanged?.Invoke();
    }

    // ── 사용 ─────────────────────────────────────────────────

    /// <summary>
    /// 탭한 자리에 스킬을 쓴다. 쓸 수 없으면 false — 부르는 쪽은 아무것도 하지 않는다.
    ///
    /// ⚠ 횟수를 먼저 깎는다 (소환과 같은 순서)
    ///   발동은 다음 프레임에 시스템이 돌린다. 그 사이에 또 눌리면 한 번의
    ///   횟수로 두 번 나간다.
    ///
    /// ⚠ 쓰고 나서도 <b>겨냥은 그대로 켜져 있다</b> (사용자 확정, 2026-09-07)
    ///   카드와 같은 조작이다 — 카드도 한 번 고르면 라인을 몇 번 탭하든
    ///   선택이 유지된다. 한 번 쓸 때마다 버튼을 다시 눌러야 하면, 횟수가
    ///   둘 이상인 소환사에서 손이 두 배로 바쁘다.
    ///   푸는 것은 셋뿐이다 — 버튼을 다시 누르거나, 카드를 고르거나,
    ///   횟수를 다 써서 더는 쓸 수 없게 되거나.
    /// </summary>
    public static bool TryUseAt(Vector3 worldPos)
    {
        if (!CanUse) return false;

        _usedThisStage++;
        Changed?.Invoke();

        // 다 썼으면 겨냥을 접는다 — 켜 둔 채로 두면 눌러도 아무 일이 없는
        // 상태가 화면에 남아 "왜 안 나가지" 가 된다.
        if (!CanUse) Cancel();

        return Fire(worldPos);
    }

    /// <summary>
    /// 발동 이벤트를 **직접** 적는다.
    ///
    /// ■ UseActiveSkillTag 를 쓰지 않는다
    ///   그 경로(ActiveSkillCooldownSystem)는 쿨다운을 확인하고, 겨냥할 곳을
    ///   **시전자의 공격 타겟**에서 가져온다. 그런데 소환사는 성벽 뒤에 서서
    ///   대부분 타겟이 없다 — 그러면 스킬이 시전자 발밑에 떨어져 "안 나갔다"
    ///   로 보인다. 이 스킬의 제한은 쿨다운이 아니라 횟수이기도 하다.
    ///   그래서 겨냥할 곳을 여기서 정하고 이벤트를 바로 적는다
    ///   (ActiveSkillAISystem 이 쓰는 것과 같은 버퍼다).
    ///
    /// ⚠ 버퍼가 없으면 적지 않는다
    ///   없는데 적으면 ECB 재생에서 죽는다 (몬스터에게 스킬을 붙이며 겪었다).
    ///
    /// ⚠ 조용히 돌아가지 않는다 (2026-09-06)
    ///   여섯 갈래가 전부 말없이 false 를 돌려주던 때가 있었다. 그러면 화면에는
    ///   "횟수만 줄고 아무 일도 안 일어남" 으로 보여, 무엇이 빠졌는지 알 길이
    ///   없다. 어디서 멈췄는지는 반드시 말해야 한다.
    /// </summary>
    static bool Fire(Vector3 worldPos)
    {
        SummonerRuntimeBridge bridge = SummonerRuntimeBridge.Current;
        if (bridge == null)
        {
            Debug.LogError("[SummonerSkillRule] 소환사가 아직 서지 않았습니다.");
            return false;
        }

        if (!bridge.TryGetComponent<EntityLink>(out var link) || link.Entity == Entity.Null)
        {
            Debug.LogError("[SummonerSkillRule] 소환사에 EntityLink 가 없습니다 — 프리팹을 확인하세요.");
            return false;
        }

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return false;

        EntityManager em = world.EntityManager;

        Entity e = link.Entity;
        if (!em.Exists(e))
        {
            Debug.LogError("[SummonerSkillRule] 소환사 엔티티가 이미 사라졌습니다.");
            return false;
        }

        if (!em.HasBuffer<ActiveSkillExecuteEvent>(e))
        {
            Debug.LogError("[SummonerSkillRule] 소환사에 ActiveSkillExecuteEvent 버퍼가 없습니다 — " +
                           "SummonerRuntimeBridge 가 엔티티를 만들었는지 확인하세요.");
            return false;
        }

        // ⚠ 겨냥할 곳은 **플레이어가 탭한 자리**다
        //   예전에는 성벽에 가장 가까운 적을 코드가 대신 골랐다. 그러면 소환
        //   위치를 플레이어가 정할 수 없어서, 라인을 고르는 이 게임의 조작과
        //   어긋난다. 엔티티 타겟만 탭한 자리 근처에서 찾아 준다 —
        //   피해 계열 스킬이 대상 엔티티를 요구하기 때문이다.
        Entity target = NearestEnemyTo(em, worldPos);

        em.GetBuffer<ActiveSkillExecuteEvent>(e).Add(new ActiveSkillExecuteEvent
        {
            SkillId        = (int)SkillId,
            TargetEntity   = target,
            TargetPosition = new Unity.Mathematics.float3(worldPos.x, worldPos.y, 0f),
        });

        return true;
    }

    /// <summary>
    /// 탭한 자리에 가장 가까운 적. 없으면 Entity.Null.
    ///
    /// ⚠ 위치를 정하는 것이 아니다 — 위치는 이미 플레이어가 정했다
    ///   대상 엔티티를 요구하는 스킬(단일 피해·낙인)만 이 값을 쓴다.
    ///   소환 계열은 TargetPosition 만 본다.
    /// </summary>
    static Entity NearestEnemyTo(EntityManager em, Vector3 at)
    {
        List<Entity> enemies = SkillCrowdControl.CollectEnemiesInRadius(
            em, at, SearchRadius, Faction.Monster);

        Entity best     = Entity.Null;
        float  bestDist = float.MaxValue;

        foreach (Entity enemy in enemies)
        {
            float d = (SkillCrowdControl.PositionOf(em, enemy) - at).sqrMagnitude;
            if (d >= bestDist) continue;

            bestDist = d;
            best     = enemy;
        }

        return best;
    }

    /// <summary>겨냥할 적을 찾는 반경. 탭한 자리 주변이라 넓지 않아도 된다.</summary>
    const float SearchRadius = 12f;

    // ── 판 경계 ──────────────────────────────────────────────

    /// <summary>사용 기록을 비운다. 스테이지 대기에 들어설 때 부른다.</summary>
    public static void RefillForStage()
    {
        Cancel();

        if (_usedThisStage == 0) return;

        _usedThisStage = 0;
        Changed?.Invoke();
    }

    /// <summary>런이 시작됐다. 사용 기록을 비운다.</summary>
    public static void Bind()
    {
        Cancel();

        _usedThisStage = 0;
        Changed?.Invoke();
    }
}

// ============================================================
//  SignatureSkillDisplay — 시그니처 스킬의 **표시 이름 · 그림** 정본 (2026-09-11)
//
//  ■ 왜 필요한가 (사용자 지시)
//    '권속 소환'(SummonSignature)은 여덟 소환사가 나눠 쓴다. 이름도 그림도 하나라
//    선택 화면·전투 버튼 어디서도 무엇이 나오는지 알 수 없었다.
//    소환형은 **부르는 종족의 초상화**와 **소환사마다의 이름**(SummonerData.SignatureName)을 쓴다.
//
//  ⚠ 줄을 더 적어 설명하지 않는다 — 이름과 그림이 말한다 (사용자 지시).
//  ⚠ 선택 화면(SummonerCandidateCardUI)과 전투 버튼(SummonerSkillButtonUI)이 이 둘을 지난다.
// ============================================================
public static class SignatureSkillDisplay
{
    /// <summary>표시 이름 — 소환사가 이름을 들고 있으면 그것, 아니면 스킬 SO 이름.</summary>
    public static string NameOf(SummonerData summoner)
        => !string.IsNullOrEmpty(summoner.SignatureName)
            ? summoner.SignatureName
            : ActiveSkillDatabase.Current.Get(summoner.SignatureSkill).SkillName;

    /// <summary>그림 — 권속 소환은 부르는 종족의 초상화, 나머지는 스킬 그림.</summary>
    public static UnityEngine.Sprite IconOf(SummonerData summoner)
    {
        if (summoner.SignatureSkill == ActiveSkillId.SummonSignature &&
            summoner.SignatureSummonSpecies != null)
            return MonsterPortraitProvider.Get(summoner.SignatureSummonSpecies);

        string key = summoner.SignatureSkill.IconKey();
        return key != null ? SpriteManager.Instance.Get(key) : null;
    }
}

// ============================================================
//  SignatureDamageRule — 소환사 시그니처 스킬의 피해 (사용자 지시, 2026-09-15)
//
//  ■ 대상 최대 체력 비례 × 패기 배율
//    예전엔 소환사 공격력(패기 × 2 = 4~20) × 배율이었다. 용사 체력은 스테이지를 따라
//    수천까지 오르는데 그 값은 그대로라, 후반에는 메테오가 20 을 때렸다.
//    소환사 평타(SummonerStrikeRule)·마나 폭발과 같은 생각으로 맞췄다 —
//    몇 스테이지에서 쓰든 같은 무게이고, **패기가 높을수록 세다** (StrikeMultFor 와 같은 배율).
//
//  ■ 방어율을 지나지 않는다 — 비율 피해가 방어율에 깎이면 방패병에게만 유독 약해진다.
//  ■ 보스는 절반 — 무한 보스까지 스킬 몇 번으로 지우지 않게 (마나 폭발과 같은 값).
//
//  ⚠ 시전자가 **소환사일 때만**이다 (SummonerStrikeComponent)
//    메테오·사형 선고·피의 대가는 용사 보스·2차 몬스터도 쓴다. 그들은 예전 공식 그대로다 —
//    적 보스가 플레이어 몬스터의 최대 체력을 %로 깎으면 판이 통째로 뒤집힌다.
// ============================================================

public static class SignatureDamageRule
{
    public const float BossMult = 0.5f;

    // ── 스킬별 비율 (패기 배율 곱하기 전) ──
    public const float MeteorRatio        = 0.30f;    // 반경 3.5 · 스테이지 1회
    public const float GravestoneRatio    = 0.05f;    // 비석 1개 (12개가 흩어져 떨어진다)
    public const float DeathSentenceRatio = 0.20f;    // 처형되지 않은 적 (35% 이하 처형은 그대로)
    public const float PoisonTickRatio    = 0.015f;   // 0.5초마다 · 6초 = 18%
    public const float BloodPerHp         = 0.01f;    // 태운 체력 1당

    public static bool IsSummoner(Unity.Entities.EntityManager em, Unity.Entities.Entity caster)
        => em.Exists(caster) && em.HasComponent<BattleGame.Units.SummonerStrikeComponent>(caster);

    /// <summary>패기 배율 — 소환사 평타와 같은 값. 소환사가 아니면 1.</summary>
    public static float VigorMult(Unity.Entities.EntityManager em, Unity.Entities.Entity caster)
        => IsSummoner(em, caster) ? SummonerVigorRule.StrikeMultFor(SummonerRuntimeBridge.Current.Data) : 1f;

    /// <summary>소환사면 기준 비율 × 패기 배율, 아니면 0 (= 예전 공식을 쓴다).</summary>
    public static float RatioFor(Unity.Entities.EntityManager em, Unity.Entities.Entity caster, float baseRatio)
        => IsSummoner(em, caster) ? baseRatio * VigorMult(em, caster) : 0f;

    /// <summary>비율을 그 대상의 최대 체력에 맞춘 피해로. 보스는 BossMult.</summary>
    public static float DamageFor(Unity.Entities.EntityManager em, Unity.Entities.Entity target, float ratio)
    {
        if (ratio <= 0f || !em.HasComponent<BattleGame.Units.StatComponent>(target)) return 0f;

        float maxHp = em.GetComponentData<BattleGame.Units.StatComponent>(target).Final[StatType.MaxHp];
        float mult  = em.HasComponent<BattleGame.Units.BossComponent>(target) ? BossMult : 1f;
        return maxHp * ratio * mult;
    }

    /// <summary>
    /// 한 대상에 넣는다 — 비율이 있으면 최대 체력 비례(방어율 무시), 없으면 고정 피해.
    /// ⚠ 러너들이 전부 이 함수를 지난다. 공식을 러너마다 다시 쓰지 말 것.
    /// </summary>
    public static void Hit(Unity.Entities.EntityManager em, Unity.Entities.Entity target,
                           float flatDamage, float ratio, Unity.Mathematics.float3 dir,
                           float knock, Unity.Entities.Entity attacker)
    {
        if (ratio > 0f)
            SkillCrowdControl.DealDamage(em, target, DamageFor(em, target, ratio), dir, knock, attacker,
                                         defensePierce: 1f);
        else
            SkillCrowdControl.DealDamage(em, target, flatDamage, dir, knock, attacker);
    }
}
