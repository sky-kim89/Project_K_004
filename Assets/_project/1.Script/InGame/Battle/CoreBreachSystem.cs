using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

// ============================================================
//  CoreBreachSystem.cs
//  성벽에 닿은 용사를 거둬 마왕성 체력을 1 깎는다.
//
//  ■ 흐름
//    용사가 몬스터를 다 뚫고 왼쪽으로 진군 → 성벽선(SummonFieldLayout.WallX)에 도달
//    → 여기서 잡아 코어 −1 → BreachedTag + DeadTag → 기존 사망 파이프라인이 반납
//
//  ■ ⚠ 왜 직접 반납하지 않고 DeadTag 를 붙이나
//    풀 반납·생존 카운트·엔티티 정리를 UnitDeathDespawnSystem 이 이미 전부 한다.
//    여기서 따로 반납하면 그 절차를 통째로 다시 써야 하고, 한 줄만 빠뜨려도
//    생존 카운트가 어긋나 판이 영영 안 끝난다.
//
//  ■ ⚠ 통과당한 용사는 골드를 주지 않는다
//    BreachedTag 가 그 표식이다. UnitDeathDespawnSystem 이 이 태그를 보고
//    지급을 건너뛴다. 성을 때린 적이 돈까지 주면 "일부러 흘려보내기" 가
//    이득이 되는 길이 열린다.
//
//  ■ ⚠ 관리형 시스템이다 (SystemBase)
//    코어 체력이 세이브 섹션(관리형)이라 Burst 잡에서 못 만진다.
//    성벽에 닿는 적은 한 프레임에 몇 기뿐이라 비용이 문제되지 않는다.
// ============================================================

namespace BattleGame.Units
{
    /// <summary>성벽을 통과해 거둬진 용사. 처치가 아니므로 골드를 주지 않는다.</summary>
    public struct BreachedTag : IComponentData { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(UnitDeathDespawnSystem))]
    public partial class CoreBreachSystem : SystemBase
    {
        /// <summary>
        /// 성벽선에서 이만큼 안쪽까지 오면 통과로 친다.
        ///
        /// ⚠ 0 으로 두지 말 것 — ScreenClampJob 이 정확히 WallX 에 물려 두므로
        ///   부동소수 오차 한 틱에 판정이 들락거린다.
        /// </summary>
        const float BreachMargin = 0.15f;

        /// <summary>Max 가 0 이라는 경고를 한 번만 낸다 — 매 프레임 도는 시스템이다.</summary>
        bool _warnedNoCore;

        protected override void OnUpdate()
        {
            // 전투 중이 아니면 아무것도 하지 않는다 — 대기 화면에서 카메라가
            // 옆으로 밀려 있는 동안 엉뚱한 판정이 돌면 안 된다.
            BattleManager bm = BattleManager.Instance;
            if (bm == null || !bm.IsWaveRunning) return;

            var core = UserDataManager.Instance?.Get<RunCoreData>();
            if (core == null) return;

            // ⚠ 조용히 돌아가지 않는다 (2026-09-06)
            //   Max 가 0 이면 이 시스템이 매 프레임 말없이 빠져나가고,
            //   RunCoreData.IsDown 도 `Max > 0` 조건이라 영영 false 다.
            //   화면에는 "적이 성벽에 붙었는데 아무 일도 안 일어남" 으로만 보인다.
            //   런 시작에서 GrantForRun 이 불리지 않은 세이브(기능 추가 이전에
            //   시작된 런)가 이 상태가 된다 — RunBootstrap.StartRun 이 이어하기에서도
            //   0 이면 채우도록 고쳤지만, 그래도 뚫리면 여기서 말한다.
            if (core.Max <= 0)
            {
                if (!_warnedNoCore)
                {
                    _warnedNoCore = true;
                    Debug.LogError("[CoreBreachSystem] 마왕성 체력이 0 입니다 — 성벽 통과가 " +
                                   "집계되지 않아 런이 끝나지 않습니다. " +
                                   "RunCoreData.GrantForRun 이 불렸는지 확인하세요.");
                }
                return;
            }

            var layout = SummonFieldLayout.Instance;
            if (layout == null) return;

            float line = SummonFieldLayout.WallX + BreachMargin;

            // ── 아군이 남아 있으면 보스는 성벽에 붙들린다 (사용자 지시, 2026-09-16) ──
            //
            //  ⚠ 특성 '매복'(판 시작 5초 배출 정지) + 보스판에서 보스가 돌진해 빈 전장을
            //    가로질러 성벽에 닿으면, 대기열에 몬스터가 잔뜩 있는데도 그 자리에서 런이 끝났다.
            //    "막지 못했다" 가 아니라 "아직 막을 차례가 안 왔다" 였다.
            //  지금은 필드에 몬스터가 있거나 **실제로 나오고 있는** 대기열이 있으면 보스를 거두지 않는다 —
            //  ScreenClampJob 이 성벽선에 물려 두므로 보스는 그 자리에서 몰려나오는 몬스터와 싸운다.
            //  둘 다 없을 때만 함락이다.
            //  ⚠ 대기열 수(TotalCount)로 보지 않는다 (2026-09-16 교착)
            //    이번 판에 안 나오는 대기열(배출이 멈춘 라인에 되돌아온 몬스터)까지 세면
            //    필드가 텅 빈 채 보스가 영원히 붙들렸다. 배출이 도는 라인만 센다.
            //  ⚠ 보통 용사는 그대로 −1 이다. 한 기 = 1 이라 즉사 문제가 없다.
            bool alliesStand = MonsterLineReturner.AliveCount > 0
                            || SummonController.Instance.HasPendingSpawns;

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            int  breached  = 0;
            bool bossBroke = false;

            Entities
                .WithAll<UnitIdentityComponent>()
                .WithNone<DeadTag, BreachedTag>()
                .WithoutBurst()
                .ForEach((Entity entity,
                          in LocalTransform xform,
                          in UnitIdentityComponent identity) =>
                {
                    // ⚠ 진영은 Faction 으로 본다 — 이 게임은 TeamType.Enemy 가 용사다.
                    if (identity.Team != Faction.Hero) return;
                    if (xform.Position.x > line)       return;

                    bool isBoss = EntityManager.HasComponent<BossComponent>(entity);

                    // 싸울 아군이 남아 있으면 보스는 성벽에 붙든 채 둔다 (위 alliesStand 주석)
                    if (isBoss && alliesStand) return;

                    // ⚠ 순회 중에 구조적 변경을 하지 않는다 — ECB 로 미룬다.
                    ecb.AddComponent<BreachedTag>(entity);
                    ecb.AddComponent<DeadTag>(entity);

                    // 보스는 한 기로 끝이다 (사용자 확정, 2026-09-06)
                    if (isBoss) bossBroke = true;

                    breached++;
                })
                .Run();

            ecb.Playback(EntityManager);
            ecb.Dispose();

            if (breached == 0) return;

            // ── 보스가 닿았다 — 남은 체력과 무관하게 함락이다 ──
            //
            //  ⚠ 잡병과 같은 저울에 올리지 않는다
            //    보통 용사는 1기당 1 이라 "몇 마리를 놓쳤나" 가 그대로 점수가 된다.
            //    보스는 그 저울 밖이다 — 막지 못했다는 것 자체가 결과여야 한다.
            //    체력이 20 남았다고 보스를 스무 번 흘려보낼 수 있으면,
            //    허들 스테이지가 "버티는 판" 이 아니라 "지나가는 판" 이 된다.
            if (bossBroke)
            {
                core.Breach(core.Current);

                Debug.Log($"[CoreBreachSystem] 보스가 성벽에 도달했다 — 마왕성 함락 " +
                          $"(남아 있던 체력 {core.Max} 무관)");
                return;
            }

            core.Breach(breached);

            Debug.Log($"[CoreBreachSystem] 성벽 통과 {breached}기 — " +
                      $"마왕성 {core.Current}/{core.Max}");
        }
    }
}
