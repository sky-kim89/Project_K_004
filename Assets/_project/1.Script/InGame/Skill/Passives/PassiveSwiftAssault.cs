using System;
using System.Collections.Generic;
using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  PassiveSwiftAssault.cs  [OnBattleStart]
//  속전속결 — 이동속도 **증가분**만큼 공격속도도 동일하게 증가.
//
//  ■ '증가분' 의 정의
//    성장(등급·레벨 롤)으로 얻은 이동속도를 뺀 나머지 전부다.
//    장비·패시브·어빌리티·유물·특성·도감 — 외부 컨텐츠로 오른 몫만 센다.
//
//      증가분 = StatComponent.Base[MoveSpeed] - BaseRollStatComponent.Roll[MoveSpeed]
//
//  ⚠ 예전엔 Final - Base 로 읽어 **항상 0 이었다**
//    Final 은 스폰 때 Base 를 그대로 복사하므로 전투 시작 시점의 차이는 0 이다.
//    (그 식은 '전투 중 버프' 를 재는 것이지 성장/외부를 가르는 식이 아니다)
//    그래서 이 패시브는 지금까지 아무 효과가 없었다.
//
//  ⚠ 기준선은 BaseRollStatComponent 가 들고 온다
//    StatComponent 만으로는 성장분과 외부분을 가를 수 없다 —
//    Base 는 이미 전부 합쳐진 값이다. GeneralRuntimeBridge 가 롤 직후
//    스냅샷을 떠서 넘겨 준다.
//
//  Inspector:
//    TriggerType = OnBattleStart
//    Ratio       : 이동속도 증가분 대비 공격속도 전환 비율 (1.0 = 100%)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_SwiftAssault", menuName = "BattleGame/Passives/SwiftAssault")]
public class PassiveSwiftAssault : PassiveSkillData
{
    [UnityEngine.Header("속전속결 설정")]
    [UnityEngine.Tooltip("이동속도 증가분 대비 공격속도 전환 비율 (1.0 = 100% 동일 적용)")]
    [UnityEngine.Range(0f, 2f)]
    public float Ratio = 1.0f;

    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;
        if (!em.HasComponent<StatComponent>(ctx.GeneralEntity)) return;
        if (!em.HasComponent<BaseRollStatComponent>(ctx.GeneralEntity)) return;

        var stat = em.GetComponentData<StatComponent>(ctx.GeneralEntity);
        var roll = em.GetComponentData<BaseRollStatComponent>(ctx.GeneralEntity).Roll;

        float moveDelta = stat.Base[StatType.MoveSpeed] - roll[StatType.MoveSpeed];
        if (moveDelta <= 0f) return;

        float atkSpdBonus = moveDelta * Ratio;
        stat.Base[StatType.AttackSpeed]  += atkSpdBonus;
        stat.Final[StatType.AttackSpeed] += atkSpdBonus;
        em.SetComponentData(ctx.GeneralEntity, stat);
    }

    public override void CollectPreviewStats(Func<StatType, float> current,
                                             Func<StatType, float> baseRoll,
                                             Dictionary<StatType, float> outDeltas)
    {
        float moveDelta = current(StatType.MoveSpeed) - baseRoll(StatType.MoveSpeed);
        if (moveDelta <= 0f) return;

        float bonus = moveDelta * Ratio;
        outDeltas[StatType.AttackSpeed] =
            outDeltas.TryGetValue(StatType.AttackSpeed, out var v) ? v + bonus : bonus;
    }
}
