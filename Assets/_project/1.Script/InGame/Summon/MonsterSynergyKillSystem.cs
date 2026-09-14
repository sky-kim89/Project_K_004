using Unity.Entities;
using Unity.Mathematics;
using BattleGame.Units;

// ============================================================
//  MonsterSynergyKillSystem.cs
//  투지 시너지 — 처치할 때마다 공격력이 **비율로** 누적된다.
//
//  ■ 왜 기존 KillEmpower 패시브를 못 쓰나
//    PassiveKillEmpower 는 SO 에 적힌 **절대값**을 더한다(AttackBonusPerKill).
//    개체마다 다른 비율을 넣을 수 없고, 패시브 슬롯도 3칸뿐이라 시너지가
//    카드 패시브를 밀어낸다. 그래서 시너지 전용 컴포넌트를 따로 뒀다.
//
//  ■ 처치 감지는 기존 버퍼를 그대로 읽는다
//    EnemyKillEvent 는 CombatTriggerSystem 이 매 프레임 비운다.
//    ⚠ 이 시스템은 **읽기만 한다. 절대 Clear 하지 않는다** —
//      먼저 도는 시스템이 지우면 장비·어빌리티·특성의 처치 트리거가 통째로 죽는다
//      (CLAUDE.md '전투 트리거' 항목). 그래서 CombatTriggerSystem 보다 앞에 선다.
//
//  ■ ⚠ EntityManager 를 쓰지 않는다 (2026-09-03 수정)
//    처음엔 쿼리를 도는 도중에 EntityManager.GetBuffer / SetComponentData 를
//    불렀다. 그 둘은 동기화 지점이라 **아직 돌고 있는 잡을 기다리며 쿼리의
//    청크 캐시를 무효화한다.** 그 상태로 순회를 이어 가면 메모리가 어긋나고,
//    그 여파가 엉뚱한 곳에서 터진다 — 실제로 Burst 로 컴파일된
//    ActiveSkillCooldownSystem 안에서 NullReferenceException 이 났다.
//    SystemAPI 접근자만 쓰고, 버퍼를 직접 읽기 전에 CompleteDependency 를 부른다.
//
//  ■ 누적은 Base 가 아니라 Final 에 얹는다
//    Base 를 만지면 다른 버프 계산의 기준선이 흔들린다. 이미 적용한 양을
//    Applied 에 적어 두고 차액만 더한다 — 상속(Inherited)이 늦게 들어와도
//    이중으로 얹히지 않는다.
// ============================================================

namespace BattleGame.Units
{
    /// <summary>
    /// 투지 누적 상태. 시너지가 켜진 몬스터에게만 붙는다.
    /// </summary>
    public struct SynergyKillStackComponent : IComponentData
    {
        /// <summary>처치 하나당 오르는 공격력 비율 (0.06 = 6%).</summary>
        public float PercentPerKill;

        /// <summary>누적 상한. 판을 오래 끌어도 끝없이 커지지 않는다.</summary>
        public int MaxStacks;

        /// <summary>지금까지 쌓은 처치 수.</summary>
        public int Stacks;

        /// <summary>죽은 투지 아군에게서 물려받은 비율. 상한과 별개로 얹힌다.</summary>
        public float Inherited;

        /// <summary>이미 스탯에 반영한 비율. 차액만 더하기 위한 기록이다.</summary>
        public float Applied;

        // ── 야수 축 — 처치할수록 빨라진다 ────────────────────
        //   투지(공격력)와 **같은 처치 수**를 쓴다. 늑대처럼 두 표식을 다 가진
        //   종족은 한 번 처치로 둘 다 오른다 — 표식을 둘 챙긴 값어치다.

        /// <summary>처치 하나당 오르는 공격속도 비율.</summary>
        public float SpeedPerKill;

        /// <summary>누적 공격속도 상한.</summary>
        public float SpeedMax;

        /// <summary>이미 반영한 공격속도 비율.</summary>
        public float SpeedApplied;

        /// <summary>
        /// 투지 금 — 이 누적을 물려줄 종족 열쇠 (MonsterSynergyRule.KeyOf).
        /// 0 이면 개체와 함께 사라진다.
        /// </summary>
        public int CarryKey;

        /// <summary>지금 붙어 있어야 할 총 공격력 비율.</summary>
        public float Target => PercentPerKill * Stacks + Inherited;

        /// <summary>지금 붙어 있어야 할 총 공격속도 비율. 상한에서 멈춘다.</summary>
        public float SpeedTarget => math.min(SpeedPerKill * Stacks, SpeedMax);
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PassiveSkillRuntimeSystem))]
    [UpdateBefore(typeof(CombatTriggerSystem))]
    public partial class MonsterSynergyKillSystem : SystemBase
    {
        protected override void OnCreate()
        {
            // 투지 몬스터가 하나도 없으면 돌 필요가 없다.
            RequireForUpdate<SynergyKillStackComponent>();
        }

        protected override void OnUpdate()
        {
            // 처치 버퍼를 메인 스레드에서 직접 읽는다 — 쓰는 잡이 끝나야 한다.
            CompleteDependency();

            foreach (var (stackRef, statRef, entity) in
                     SystemAPI.Query<RefRW<SynergyKillStackComponent>, RefRW<StatComponent>>()
                              .WithNone<DeadTag>()
                              .WithEntityAccess())
            {
                SynergyKillStackComponent s = stackRef.ValueRO;

                // ⚠ 읽기만 한다 — 비우는 것은 CombatTriggerSystem 뿐이다.
                if (SystemAPI.HasBuffer<EnemyKillEvent>(entity) && s.Stacks < s.MaxStacks)
                {
                    int kills = SystemAPI.GetBuffer<EnemyKillEvent>(entity).Length;

                    if (kills > 0)
                    {
                        s.Stacks = math.min(s.Stacks + kills, s.MaxStacks);

                        // 투지 금 — 종족 단위로 남긴다. 라인 복귀로 다시 나온
                        // 개체가 이 자리에서 이어 쌓는다.
                        if (s.CarryKey != 0)
                            MonsterSynergyRule.CarryStacks(s.CarryKey, s.Stacks);
                    }
                }

                float target      = s.Target;
                float speedTarget = s.SpeedTarget;

                bool wantAttack = target      > s.Applied      + 0.0001f;
                bool wantSpeed  = speedTarget > s.SpeedApplied + 0.0001f;

                if (wantAttack || wantSpeed)
                {
                    StatComponent stat = statRef.ValueRO;

                    if (wantAttack)
                        stat.Final[StatType.Attack] +=
                            stat.Base[StatType.Attack] * (target - s.Applied);

                    if (wantSpeed)
                        stat.Final[StatType.AttackSpeed] +=
                            stat.Base[StatType.AttackSpeed] * (speedTarget - s.SpeedApplied);

                    statRef.ValueRW = stat;

                    s.Applied      = target;
                    s.SpeedApplied = speedTarget;
                }

                stackRef.ValueRW = s;
            }
        }
    }
}
