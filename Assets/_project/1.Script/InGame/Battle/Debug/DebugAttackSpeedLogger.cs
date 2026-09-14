using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  DebugAttackSpeedLogger.cs
//  장군 설정 공격속도 vs 실제 공격속도 비교 로그 시스템.
//  5초마다 각 장군의 스탯·실제 공격 횟수를 콘솔에 출력한다.
//
//  ⚠ 웨이브가 도는 동안에만 잰다
//    로비 BattlePanel 의 '출전 대기' 는 아군만 세워 두고 적을 한 마리도 놓지 않는다.
//    때릴 대상이 없으니 공격이 0회인 게 정상인데, 그걸 모르는 이 로거가
//    "실제 0.00회/초 ← ★ 불일치" 를 5초마다 찍어 콘솔을 덮었다.
//    진짜 불일치가 났을 때 그 줄이 묻혀 버리므로 게이트가 없으면 로거가 무의미하다.
//
//    ⚠ 카운터도 같이 멈춰야 한다
//      게이트를 출력에만 걸면 대기 중 0회가 계속 쌓였다가 전투 시작 직후
//      첫 창에 섞여 들어가 또 가짜 불일치가 뜬다.
// ============================================================

#if UNITY_EDITOR || ENABLE_ATK_SPD_LOG

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitAttackSystem))]
public partial class DebugAttackSpeedLogger : SystemBase
{
    const float LogInterval = 5f;

    float _timer;
    readonly Dictionary<int, int> _attackCounts = new();

    protected override void OnUpdate()
    {
        // 웨이브가 안 도는 동안(출전 대기·로비 데모·결과창)은 재지도, 찍지도 않는다.
        if (BattleManager.Instance == null || !BattleManager.Instance.IsWaveRunning)
        {
            if (_attackCounts.Count > 0) _attackCounts.Clear();
            _timer = 0f;
            return;
        }

        _timer += SystemAPI.Time.DeltaTime;

        // 공격 횟수 카운트
        Entities
            .WithoutBurst()
            .WithAll<GeneralComponent>()
            .WithNone<DeadTag>()
            .ForEach((Entity entity, UnitPoolLinkComponent link, in AttackComponent attack) =>
            {
                if (!attack.AttackedThisFrame) return;
                _attackCounts.TryGetValue(entity.Index, out int cnt);
                _attackCounts[entity.Index] = cnt + 1;
            })
            .Run();

        if (_timer < LogInterval) return;

        // 5초마다 비교 출력
        Entities
            .WithoutBurst()
            .WithAll<GeneralComponent>()
            .WithNone<DeadTag>()
            .ForEach((Entity entity, UnitPoolLinkComponent link,
                      in StatComponent stat, in AttackComponent attack, in UnitJobComponent job) =>
            {
                _attackCounts.TryGetValue(entity.Index, out int count);

                string unitName      = link?.PoolKey ?? $"Entity:{entity.Index}";
                float  configSpd     = stat.Final[StatType.AttackSpeed];
                float  configIntvl   = configSpd > 0f ? 1f / configSpd : 0f;
                float  actualSpd     = count / LogInterval;
                float  actualIntvl   = actualSpd > 0f ? 1f / actualSpd : 0f;
                float  ratio         = configSpd > 0f ? actualSpd / configSpd : 0f;
                bool   mismatch      = Mathf.Abs(ratio - 1f) > 0.15f;

                string jobName = job.Job.ToString();
                string alert   = mismatch ? "  ← ★ 불일치" : "";

                Debug.Log(
                    $"[AtkSpdLog] [{jobName}] {unitName}\n" +
                    $"  설정  공격속도 : {configSpd:F2}회/초  ({configIntvl:F2}초마다 1회)\n" +
                    $"  실제  공격속도 : {actualSpd:F2}회/초  ({(count > 0 ? $"{actualIntvl:F2}초마다 1회" : "공격 없음")})" +
                    $"  ← {LogInterval}초간 {count}회{alert}\n" +
                    $"  현재 쿨다운 잔여: {attack.AttackCooldown:F2}초  달성률: {ratio:P0}");

                _attackCounts[entity.Index] = 0;
            })
            .Run();

        _timer = 0f;
    }
}

#endif
