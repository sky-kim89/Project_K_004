using Unity.Entities;
using BattleGame.Units;

// ============================================================
//  UnitAttackSfxSystem.cs
//  평타가 나간 프레임에 직업별 효과음을 낸다.
//
//  ■ 이미 있는 신호를 쓴다
//    UnitAttackSystem 이 공격할 때 AttackComponent.AttackedThisFrame 를 켠다
//    (CooldownTickJob 이 매 프레임 끈다). 별도 이벤트를 만들 필요가 없다.
//
//  ■ 소리 개수 제한은 여기서 하지 않는다
//    AudioManager 가 키마다 예산을 갖고 있어서, 넘치는 요청은 알아서 버린다.
//    여기서 또 걸러 내면 두 곳에서 다른 규칙이 돌아 디버깅이 어려워진다.
//
//  ■ 아군만 소리를 낸다
//    적까지 내면 같은 화면에서 소리가 두 배가 되는데, 아군 것과 구분도 안 된다.
//    적의 공격은 피격음(추후 BTL_Hit)으로 전달하는 편이 정확하다.
//
//  ■ managed 시스템인 이유
//    AudioManager 는 MonoBehaviour 라 Burst 에서 못 부른다.
//    대상이 공격한 유닛뿐이라 비용은 무시할 수준이다.
// ============================================================

namespace BattleGame.Units
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitAttackSystem))]
    public partial class UnitAttackSfxSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<AttackComponent>();
        }

        protected override void OnUpdate()
        {
            // ⚠ 인스턴스는 프레임당 한 번만 찾는다
            //   Singleton<T>.Instance 는 캐시가 비면 FindAnyObjectByType 를 돈다.
            //   씬에 AudioManager 가 없을 때 유닛마다 부르면 매 프레임 수백 번 스캔한다.
            var audio = AudioManager.Instance;
            if (audio == null) return;

            foreach (var (attack, job, identity)
                     in SystemAPI.Query<
                            RefRO<AttackComponent>,
                            RefRO<UnitJobComponent>,
                            RefRO<UnitIdentityComponent>>()
                        .WithNone<DeadTag>())
            {
                if (!attack.ValueRO.AttackedThisFrame) continue;
                if (identity.ValueRO.Team != TeamType.Ally) continue;

                audio.Play(KeyFor(job.ValueRO.Job));
            }
        }

        static SfxKey KeyFor(UnitJob job) => job switch
        {
            UnitJob.Knight       => SfxKey.ATK_Knight,
            UnitJob.Archer       => SfxKey.ATK_Archer,
            UnitJob.Mage         => SfxKey.ATK_Mage,
            UnitJob.ShieldBearer => SfxKey.ATK_Shield,
            _                    => SfxKey.None,
        };
    }
}
