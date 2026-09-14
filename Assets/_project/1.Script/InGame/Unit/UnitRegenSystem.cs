using Unity.Burst;
using Unity.Entities;

// ============================================================
//  UnitRegenSystem.cs
//  RegenComponent 를 가진 유닛의 체력을 주기적으로 회복시킨다.
//  (트롤 '재생' 같은 종족 패시브의 실행부)
//
//  ■ 왜 HealEventBufferElement 로 넣나 — 직접 채우지 않는다
//    회복의 최종 처리는 UnitHealSystem 하나가 소유한다. 최대 체력 클램프,
//    치유량 통계, 사망 직전 유닛 처리가 전부 거기 있다.
//    여기서 CurrentHp 를 직접 올리면 그 규칙을 우회하게 되고,
//    "최대 체력을 넘겨 회복하는 트롤" 같은 게 조용히 생긴다.
//
//  ■ 매 프레임 회복하지 않는다
//    1초에 한 번씩 몰아서 넣는다. 프레임마다 이벤트를 만들면 60배 많은
//    버퍼 쓰기가 생기는데, 체감은 똑같다. 회복 숫자가 뜨는 연출과도
//    1초 간격이 더 잘 맞는다.
//
//  ■ 죽은 유닛은 회복하지 않는다
//    DeadTag 를 제외한다. 사망 연출 중에 회복해도 이미 늦었고,
//    체력 바가 되살아난 것처럼 보인다.
// ============================================================

namespace BattleGame.Units
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct UnitRegenSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new RegenTickJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }
    }

    [BurstCompile]
    [WithNone(typeof(DeadTag))]
    public partial struct RegenTickJob : IJobEntity
    {
        public float DeltaTime;

        /// <summary>회복 간격(초). 이 주기로 몰아서 넣는다.</summary>
        const float TickInterval = 1f;

        public void Execute(
            ref RegenComponent                        regen,
            ref DynamicBuffer<HealEventBufferElement> healBuffer,
            in  StatComponent                         stat,
            in  HealthComponent                       health)
        {
            if (regen.RatioPerSecond <= 0f) return;

            regen.Timer += DeltaTime;
            if (regen.Timer < TickInterval) return;

            // ⚠ 지나간 만큼 되돌린다 — 0 으로 초기화하지 않는다
            //   배속에서 한 프레임에 여러 틱이 지나갈 수 있다. 0 으로 밀면
            //   그 초과분이 버려져 배속일수록 회복이 느려진다.
            regen.Timer -= TickInterval;

            // 이미 가득이면 이벤트를 만들지 않는다 — 회복 숫자만 계속 뜬다.
            float maxHp = stat.Final[StatType.MaxHp];
            if (health.CurrentHp >= maxHp) return;

            float amount = maxHp * regen.RatioPerSecond * TickInterval;

            // 재생 시너지 은·금 — 위태로울 때 더 크게 차오른다.
            // 배율이 0 이면 안 걸린 것이다 (1 로 취급).
            if (regen.LowHpMult > 1f && health.CurrentHp <= maxHp * regen.LowHpThreshold)
                amount *= regen.LowHpMult;

            healBuffer.Add(new HealEventBufferElement
            {
                Amount       = amount,
                SourceEntity = Entity.Null,
            });
        }
    }
}
