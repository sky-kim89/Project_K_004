using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using BattleGame.Units;

// ============================================================
//  ActiveManaBurst.cs — 마나 폭발 (대마법사 시그니처)
//
//  **화면 전체의 적** 에게 대상 최대 체력의 (기본 30% + 태운 마나 1당 1.5%) × 패기 배율 피해.
//  남은 마나의 절반을 태운다. 보스는 절반.
//
//  ■ 기본 피해 + 마나 몫 (사용자 지시, 2026-09-15)
//    대가(마나)를 치르는 유일한 시그니처라 아주 강해야 한다. 예전엔 마나 몫뿐이라
//    마나가 바닥인 판에는 횟수만 버렸다 — 기본 피해가 바닥을 받치고, 아낀 마나가 위를 올린다.
//    ⚠ 마나가 0 이어도 기본 피해는 나간다. 마나는 연료이지 발동 조건이 아니다.
//
//  ■ 화면 전체 (사용자 지시, 2026-09-15)
//    반경 4 는 "탭한 자리 폭발" 로 읽혀 대가에 비해 작았다. 이제 전장의 적 전부다.
//    ⚠ 연출도 화면 전체로 읽혀야 한다 — 전면 섬광 · 강한 흔들림 · 화면 가운데 큰 폭발 ·
//      적마다 작은 폭발(상한 MaxHitEffects). 한 자리 이펙트만 두면 "넓게 맞았다" 가 안 보인다.
//
//  ■ 최대 체력 비례 · 방어율 무시 · 패기 배율 — 다른 시그니처와 같은 규칙 (SignatureDamageRule)
//
//  ⚠ 태운 양은 **내림**이다 — 마나는 정수로만 움직인다 (ManaRegenRule 주석).
//  ⚠ 용사 추첨에서는 IsSummonerOnly 가 거른다 — 적이 쓰면 플레이어의 마나가 빠진다.
// ============================================================

[CreateAssetMenu(fileName = "Active_ManaBurst", menuName = "BattleGame/Actives/ManaBurst")]
public class ActiveManaBurst : ActiveSkillData
{
    [Header("마나 폭발")]
    [Tooltip("태우는 비율 — 0.5 = 남은 마나의 절반")]
    public float BurnRatio = 0.5f;

    [Tooltip("기본 피해 — 마나를 못 태워도 나간다. 대상 최대 체력 비율 (0.30 = 30%)")]
    public float BaseMaxHpRatio = 0.30f;

    [Tooltip("태운 마나 1당 더하는 대상 최대 체력 비율 — 0.015 = 1.5%")]
    public float MaxHpRatioPerMana = 0.015f;

    [Tooltip("보스에게 곱하는 배율 — 0.5 = 반감")]
    public float BossMult = 0.5f;

    [Tooltip("넉백 세기")]
    public float KnockbackMult = 4f;

    /// <summary>전장 전체를 덮는 수집 반경 — 화면 밖 적도 전부 맞는다.</summary>
    const float WholeField = 10000f;

    /// <summary>적마다 터뜨리는 작은 폭발의 상한 — 100기가 넘게 서면 이펙트만으로 프레임이 무너진다.</summary>
    const int MaxHitEffects = 40;

    const float HitEffectScale    = 0.55f;
    const float CenterEffectScale = 6f;      // FX_Meteor_Explosion 기준 반경 3 → 화면 가로 절반쯤

    const float FlashAlpha   = 0.75f;
    const float FlashSeconds = 0.65f;
    static readonly Color FlashColor = new Color(0.62f, 0.80f, 1f);

    public override void Execute(ActiveSkillContext ctx)
    {
        var mana = UserDataManager.Instance.Get<SummonManaData>();

        float burned = Mathf.Floor(mana.Current * BurnRatio);
        if (burned > 0f) mana.Spend(burned);

        EntityManager em = ctx.EntityManager;
        em.CompleteAllTrackedJobs();

        float ratio = (BaseMaxHpRatio + burned * MaxHpRatioPerMana) * EffectValue
                    * SignatureDamageRule.VigorMult(em, ctx.CasterEntity);

        TeamType team = SkillCrowdControl.TeamOf(em, ctx.CasterEntity);

        Vector3 screenCenter = Camera.main.transform.position;
        screenCenter.z = 0f;

        // ── 연출 — 화면 전체로 읽히게 ──
        Flash(ctx.CasterObject.GetComponent<MonoBehaviour>());
        CameraShaker.Impulse(1f);
        SkillEffectHelper.Spawn(TargetEffectKey, screenCenter, EffectDespawnDelay, scale: CenterEffectScale);

        List<Entity> enemies = SkillCrowdControl.CollectEnemiesInRadius(
            em, new float3(screenCenter.x, screenCenter.y, 0f), WholeField, team);

        int effects = 0;

        foreach (Entity enemy in enemies)
        {
            if (!em.HasBuffer<HitEventBufferElement>(enemy)) continue;

            float maxHp = em.GetComponentData<StatComponent>(enemy).Final[StatType.MaxHp];
            float mult  = em.HasComponent<BossComponent>(enemy) ? BossMult : 1f;

            Vector3 p   = SkillCrowdControl.PositionOf(em, enemy);
            float3  dir = math.normalizesafe(new float3(p.x - screenCenter.x, p.y - screenCenter.y, 0f),
                                             new float3(1f, 0f, 0f));

            if (effects++ < MaxHitEffects)
                SkillEffectHelper.Spawn(TargetEffectKey, p, EffectDespawnDelay, scale: HitEffectScale);

            // ⚠ SkillCrowdControl.DealDamage 를 거치지 않는다 — 대상마다 배율이 달라 여기서 바로 넣는다
            em.GetBuffer<HitEventBufferElement>(enemy).Add(new HitEventBufferElement
            {
                Damage         = maxHp * ratio * mult,
                HitDirection   = dir * KnockbackMult,
                AttackerEntity = ctx.CasterEntity,
                Type           = HitType.Skill,
                DefensePierce  = 1f,   // 방어율 무시 — SignatureDamageRule 과 같은 규칙
            });
        }
    }

    // ── 전면 섬광 ────────────────────────────────────────────
    //  ⚠ 전용 캔버스 하나를 만들어 두고 켜고 끈다 — 팝업·튜토리얼(1000)보다 아래(500)다.
    //  ⚠ 코루틴은 시전자(소환사)의 컴포넌트가 돌린다 — SO 는 코루틴을 못 돌린다.

    static Image _flash;

    static void Flash(MonoBehaviour host)
    {
        if (_flash == null)
        {
            var go = new GameObject("[ManaBurstFlash]", typeof(Canvas), typeof(Image));
            Object.DontDestroyOnLoad(go);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            _flash = go.GetComponent<Image>();
            _flash.raycastTarget = false;   // 섬광이 입력을 먹으면 안 된다
        }

        host.StartCoroutine(FadeFlash());
    }

    static IEnumerator FadeFlash()
    {
        _flash.enabled = true;

        for (float t = 0f; t < FlashSeconds; t += Time.unscaledDeltaTime)
        {
            float a = FlashAlpha * (1f - t / FlashSeconds);
            _flash.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, a * a / FlashAlpha);
            yield return null;
        }

        _flash.enabled = false;
    }
}
