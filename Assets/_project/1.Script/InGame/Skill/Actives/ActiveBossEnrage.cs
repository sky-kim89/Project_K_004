using UnityEngine;
using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  ActiveBossEnrage.cs
//  광폭화 (보스 전용 패턴) — 쿨다운마다 자신에게 영구 버프를 한 겹 더 얹는다.
//
//  스택당:  공격력      +AttackPerStack (기본 50%)
//           방어율 관통 +PiercePerStack (기본 10%p)
//           몸집        +SizePerStack   (기본 10%, MaxSizeStacks 에서 멈춤)
//
//  ■ 왜 있나 — 오토배틀은 '버티기만 하는 교착' 이 성립한다
//    방패 위주 편성이 보스 딜을 못 넘기면 전투가 영원히 안 끝난다.
//    시간이 지날수록 보스가 세지므로 반드시 어느 쪽으로든 끝난다.
//
//  ■ 별도 시스템을 만들지 않은 이유
//    "쿨다운 돌고 → 효과 내고 → 이펙트 띄운다" 는 스킬 그 자체다.
//    ActiveSkillSlot 이 이미 쿨다운을 굴리고 ActiveSkillExecuteSystem 이
//    Execute() 를 불러 준다. 타이머 컴포넌트도 전용 시스템도 필요 없다.
//
//  ⚠ 반드시 EffectMode.Add 여야 한다
//    Multiply 로 붙이면 장마다 곱해져 복리가 된다 (×1.5 → ×2.25 → ×3.375…).
//    10스택이면 57배다. "기본 공격력의 50%" 를 Add 로 얹으면 스택 수에 정확히
//    비례한다 — 3스택 = 기본의 ×2.5, 기획한 그대로다.
//
//  ⚠ 스택은 '전투 종료까지' 다
//    Duration = -1 (영구). 보스가 죽으면 엔티티째 사라지고, 풀에서 다시 나올 때는
//    UnitRuntimeBridge 가 StatusEffectBufferElement 를 통째로 비운다.
// ============================================================

[CreateAssetMenu(fileName = "Active_BossEnrage", menuName = "BattleGame/Actives/BossEnrage")]
public class ActiveBossEnrage : ActiveSkillData
{
    [Header("스택 효과 (1회 시전 = 1스택)")]
    [Tooltip("스택당 공격력 증가 비율. 0.5 = 기본 공격력의 +50%\n" +
             "가산이므로 3스택이면 기본의 ×2.5 다 (곱연산 아님).")]
    public float AttackPerStack = 0.5f;

    [Tooltip("스택당 방어율 관통 증가 (0~1). 0.1 = +10%p.\n" +
             "대상의 최종 방어율에서 그대로 뺀다 — 10스택이면 방어율을 통째로 무시한다.")]
    public float PiercePerStack = 0.1f;

    [Tooltip("스택당 크기 증가 비율. 0.1 = 등장 크기의 +10%\n" +
             "가산이라 3스택이면 등장 크기의 1.3배다 (복리 아님).")]
    public float SizePerStack = 0.1f;

    [Tooltip("크기만 따로 두는 스택 상한. 0 이하면 무제한.\n" +
             "공격력·관통은 계속 오르고 몸집만 여기서 멈춘다 — 화면을 덮지 않게 하는 안전장치.")]
    public int MaxSizeStacks = 10;

    public override void Execute(ActiveSkillContext context)
    {
        var em = context.EntityManager;
        em.CompleteAllTrackedJobs();

        var buffs = em.GetBuffer<StatusEffectBufferElement>(context.CasterEntity);
        var stat  = em.GetComponentData<StatComponent>(context.CasterEntity);

        // 공격력 — "기본 공격력의 AttackPerStack" 만큼. Base 를 기준으로 삼아야
        // 스택마다 같은 양이 붙어 정확히 선형으로 오른다.
        AddPermanent(buffs, StatType.Attack, stat.Base[StatType.Attack] * AttackPerStack);

        // 방어율 관통 — 그대로. DamageMath.AfterDefense 가 saturate 로 1.0 에서 자른다.
        AddPermanent(buffs, StatType.DefensePenetration, PiercePerStack);

        // 크기 — 스택이 쌓이는 게 눈에 보여야 한다. 숫자만 오르면 플레이어는
        // 왜 갑자기 밀리는지 모른 채 진다.
        //
        // ⚠ 몸집을 키우면 보스 AoE 사거리도 같이 넓어진다
        //   BossAttackSystem 이 UnitSizeComponent.Radius 에 비례해 AoE 반경을 잡는다.
        //   의도한 동작이다 — 커진 팔이 더 멀리 닿는 게 자연스럽다. 다만 그래서
        //   크기에는 따로 상한(MaxSizeStacks)을 둔다.
        if (SizePerStack > 0f)
        {
            // 베이스 타입으로 받는다 — 보스는 용사(General)일 수도, 몬스터일 수도 있다.
            var bridge = context.CasterObject.GetComponent<UnitRuntimeBridge>();

            var size = em.GetComponentData<UnitSizeComponent>(context.CasterEntity);
            size.Radius = bridge.GrowEnrage(SizePerStack, MaxSizeStacks);
            em.SetComponentData(context.CasterEntity, size);
        }

        if (context.CasterTransform != null)
            SkillEffectHelper.Spawn(CasterEffectKey, context.CasterTransform.position, EffectDespawnDelay);
    }

    /// <summary>전투가 끝날 때까지 안 풀리는 가산 버프 한 장.</summary>
    void AddPermanent(DynamicBuffer<StatusEffectBufferElement> buffs, StatType stat, float delta)
        => buffs.Add(new StatusEffectBufferElement
        {
            Stat       = stat,
            Delta      = delta,
            Mode       = EffectMode.Add,
            // ⚠ Duration = -1 만으로는 영구가 아니다
            //   StatusEffectTickJob 은 Duration < 0 이면 Remaining 을 안 깎지만,
            //   바로 다음 줄에서 Remaining <= 0 인 항목을 지운다. Remaining 을 -1 로
            //   두면 붙자마자 사라진다 — 반드시 큰 양수를 넣는다.
            Duration   = -1f,
            Remaining  = float.MaxValue,
            SourceType = BuffSourceType.ActiveSkill,
            SourceId   = (int)SkillId,
        });
}
