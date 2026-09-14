using UnityEngine;
using BattleGame.Units;

// ============================================================
//  ActiveBossCharge.cs
//  돌진 — 적을 향해 몸을 날려 경로 위 모든 적을 밀어내며 관통한다.
//
//  ■ 패턴을 스킬로 옮긴 것
//    예전엔 BossPatternSystem + BossComponent 의 돌진 필드로 돌았다.
//    쿨다운·타겟팅·이펙트는 스킬이 이미 다 갖고 있는데 그걸 따로
//    한 벌 더 들고 있는 셈이라, 패턴을 추가할 때마다 컴포넌트 필드가 늘었다.
//    스킬로 만들면 슬롯에 꽂기만 하면 되고, 엘리트에게도 그대로 넘길 수 있다.
//
//  ■ 연출 3박자 — 웅크림 → 돌진 → 착지
//    1,000마리 난전에서 보스가 그냥 빨리 움직이면 그게 돌진인지 안 읽힌다.
//    치고 나가기 전에 살짝 뒤로 빼는 예비 동작이 있어야
//    "지금 뭔가 온다" 가 전달된다.
//
//  ■ 이펙트 키
//    BaseEffect   : 웅크림 (시전자 발밑)
//    CasterEffect : 돌진 중 잔상 (0.1초 간격)
//    TargetEffect : 관통 타격
//
//  ⚠ 시전 중에는 이동·평타·다른 슬롯이 잠긴다 (SkillCastLock)
//    안 잠그면 이동 잡이 돌진 궤적을 뒤로 끌어당겨 제자리에서 떠는 것처럼 보인다.
// ============================================================

[CreateAssetMenu(fileName = "Active_BossCharge", menuName = "BattleGame/Actives/BossCharge")]
public class ActiveBossCharge : ActiveSkillData
{
    [Header("돌진")]
    [Tooltip("돌진 이동 속도 (유닛/초)")]
    public float ChargeSpeed = 26f;

    [Tooltip("타겟을 이만큼 지나쳐 간다 — 관통 느낌을 만든다")]
    public float OvershootDistance = 8f;

    [Tooltip("웅크림(예비 동작) 시간 (초)")]
    public float WindupTime = 0.45f;

    [Tooltip("웅크릴 때 뒤로 빼는 거리")]
    public float WindupBackstep = 1.2f;

    [Tooltip("착지 후 경직 (초)")]
    public float RecoverTime = 0.35f;

    [Header("피해")]
    [Tooltip("공격력 배율")]
    public float DamageMultiplier = 2.2f;

    [Tooltip("돌진 경로 좌우 타격 반경")]
    public float HitRadius = 2.6f;

    [Tooltip("넉백 배율 — 몸통박치기라 세게 민다")]
    public float KnockbackMult = 7f;

    public override void Execute(ActiveSkillContext context)
    {
        if (context.CasterObject == null) return;

        var runner = context.CasterObject.GetComponent<BossChargeRunner>()
                  ?? context.CasterObject.AddComponent<BossChargeRunner>();

        // 총 연출 시간만큼 잠근다. 실제 이동 시간은 거리에 따라 달라지므로
        // 넉넉히 잡고, 끝나면 Runner 가 아니라 타이머가 알아서 푼다.
        float travel   = (OvershootDistance + 14f) / Mathf.Max(1f, ChargeSpeed);
        float lockTime = WindupTime + travel + RecoverTime;
        SkillCastLockUtil.Apply(context.EntityManager, context.CasterEntity, lockTime);

        runner.Run(this, context);
    }
}
