using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  PassiveBloodPact.cs
//  BloodPact 패시브 — 제너럴 체력이 낮을수록 공격력 증가.
//
//  Inspector 설정:
//    TriggerType = OnHit
//    StatModifiers: Stat=Attack, IsPercent=true, Delta=최대보너스비율, Target=General(무시)
//      예) Delta=0.5 → HP 0% 일 때 공격력 50% 증가
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_BloodPact", menuName = "BattleGame/Passives/BloodPact")]
public class PassiveBloodPact : PassiveSkillData
{
    // StatusEffect 버퍼에서 BloodPact 버프를 식별하기 위한 마커 Duration
    const float BloodPactMarker = -2f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;
        if (!em.HasBuffer<StatusEffectBufferElement>(ctx.GeneralEntity)) return;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity)) return;

        float maxBonusRatio = GetMaxBonusRatio();
        if (maxBonusRatio <= 0f) return;

        var stat  = em.GetComponentData<StatComponent>(ctx.GeneralEntity);
        float maxHp = stat.Final[StatType.MaxHp];
        if (maxHp <= 0f) return;

        float hpRatio    = ctx.Health.CurrentHp / maxHp;
        float bonusRatio = (1f - hpRatio) * maxBonusRatio;  // HP 0% = 최대 보너스

        var buffers = em.GetBuffer<StatusEffectBufferElement>(ctx.GeneralEntity);

        // 기존 BloodPact 버프 제거
        for (int i = buffers.Length - 1; i >= 0; i--)
        {
            if (math.abs(buffers[i].Duration - BloodPactMarker) < 0.001f)
            {
                buffers.RemoveAt(i);
                break;
            }
        }

        // 새 Attack 버프 추가
        buffers.Add(new StatusEffectBufferElement
        {
            Stat       = StatType.Attack,
            Delta      = stat.Base[StatType.Attack] * bonusRatio,
            Mode       = EffectMode.Add,
            Duration   = BloodPactMarker,
            Remaining  = float.MaxValue,   // Duration<0 이면 감소 안 하지만 Remaining<=0 이면 제거되므로 양수 필수
            SourceType = BuffSourceType.Passive,
            SourceId   = (int)Type,
        });
    }

    // ── 내부 ─────────────────────────────────────────────────

    float GetMaxBonusRatio()
    {
        foreach (var mod in StatModifiers)
        {
            if (mod.Stat == StatType.Attack && mod.IsPercent)
                return mod.Delta;
        }
        return 0f;
    }
}
