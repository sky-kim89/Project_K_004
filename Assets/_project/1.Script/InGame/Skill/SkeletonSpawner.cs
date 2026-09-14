using Unity.Entities;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  SkeletonSpawner.cs
//  소환 스켈레톤 생성의 단일 진입점.
//
//  스켈레톤 소환(ActiveSummonSkeleton)과 비석 강림(ActiveGravestone)이
//  같은 함수를 쓴다 — 외형·태그·생존 카운트 규칙이 갈라지면 안 된다.
//
//  ■ 외형
//    SoldierRuntimeBridge.Initialize 는 아군 외형(ApplyAlly)을 입힌다.
//    그대로 두면 소환수가 평범한 병사로 보인다 — 스켈레톤이 아니다.
//    그래서 초기화 뒤에 ApplyEnemy(EnemyRace.Skeleton) 로 외형만 덮어쓴다.
//    (팀·스탯은 아군 그대로다. 외형만 언데드다)
//
//  ■ SummonedTag
//    소환수의 딜은 병사 딜이 아니라 스킬 딜로 집계된다 (BattleStatCollectorSystem).
// ============================================================

public static class SkeletonSpawner
{
    /// <summary>
    /// 땅에서 일어나는 연출 길이(초).
    ///
    /// ⚠ 연출·이동잠금·무적이 같은 값을 써야 한다
    ///   따로 두면 "일어섰는데 아직 못 움직인다" 거나 "웅크린 채 맞는다" 가 된다.
    /// </summary>
    public const float RiseDuration = 0.3f;

    /// <summary>
    /// 소환진 이펙트 — 스켈레톤이 나오는 자리에 항상 깔린다.
    ///
    /// ⚠ 소환은 이펙트가 없으면 "일어나지 않은 일" 이 된다
    ///   장비 트리거(망자의 소환서·처형자의 낙인)로 나오는 한 기는 병사 떼 사이에
    ///   섞여 버려서, 실제로는 소환되고 있는데도 효과가 없는 것처럼 보였다.
    ///   스킬·비석·장비가 전부 이 함수를 지나므로 여기 한 곳에서 깐다 —
    ///   부르는 쪽마다 따로 깔면 또 빠뜨리는 곳이 생긴다.
    /// </summary>
    public const string SummonEffectKey     = "FX_Summon_Circle";
    const        float  SummonEffectDespawn = 1.0f;

    /// <summary>지정 위치에 스켈레톤 1기를 소환한다. 실패하면 null.</summary>
    public static GameObject Spawn(EntityManager em, string poolKey, Vector3 position,
                                   UnitStat generalStat, float statRatio,
                                   Entity casterEntity, UnitJob generalJob)
    {
        if (string.IsNullOrEmpty(poolKey) || PoolController.Instance == null) return null;

        GameObject go = PoolController.Instance.Spawn(
            PoolType.Unit, poolKey, position, Quaternion.identity);

        if (go == null)
        {
            Debug.LogWarning($"[SkeletonSpawner] 풀 스폰 실패: '{poolKey}'");
            return null;
        }

        // 실제로 한 기가 나온 것이 확정된 뒤에 깐다 — 스폰이 실패했는데 소환진만
        // 도는 것은 거짓말이고, 소환이 도는지 안 도는지 판단할 근거도 사라진다.
        SkillEffectHelper.Spawn(SummonEffectKey, position, SummonEffectDespawn);

        if (!go.TryGetComponent<SoldierRuntimeBridge>(out var bridge))
        {
            Debug.LogWarning($"[SkeletonSpawner] '{poolKey}' 프리팹에 SoldierRuntimeBridge 가 없다.");
            return go;
        }

        // ⚠ 진영은 **시전자에게서** 온다. 상수로 박지 말 것
        //   TeamType.Ally 를 박아 두고 SetTeam 을 빠뜨렸더니, 실제로는 적으로
        //   선 유닛이 아군으로 신고돼 적 수가 조용히 줄었다
        //   (SkillCrowdControl.TeamOf 주석 참고).
        // ⚠ 순서 고정 — Initialize 안에서 Team 이 박힌다
        TeamType team = SkillCrowdControl.TeamOf(em, casterEntity);
        bridge.SetTeam(team);

        BattleManager.Instance?.OnUnitSpawned(team);

        bridge.Initialize(poolKey, generalStat, statRatio, casterEntity,
                          generalJob, "Skeleton", UnitGrade.Normal);

        // 외형만 언데드로 덮어쓴다 — Initialize 안의 ApplyAlly 이후여야 한다
        ApplySkeletonLook(go, position);

        // 땅에서 기어 나오는 연출 — 웅크렸다가 일어선다.
        //  ⚠ 외형을 덮어쓴 뒤에 부른다
        //    ApplySkeletonLook 이 SpriteLibrary 를 갈아 끼우므로, 그 전에 자세를
        //    잡으면 아군 외형의 웅크림이 한 프레임 스친다.
        if (go.TryGetComponent<UnitAnimationSync>(out var anim))
            anim.PlayRise(RiseDuration);

        if (go.TryGetComponent<EntityLink>(out var link) && link.Entity != Entity.Null)
        {
            // 소환 유닛 마킹 — 딜을 스킬 딜로 귀속
            em.AddComponent<SummonedTag>(link.Entity);

            // 일어나는 동안은 가만히 있고, 맞지도 않는다.
            //  ⚠ 새 규칙을 만들지 않는다 — 이미 있는 부품 두 개를 그대로 쓴다
            //    SkillCastLock   : 이동·평타·추가 스킬 잠금 (스킬 시전과 같은 장치)
            //    SpawnProtection : 그 동안 받는 피해 무효
            //    둘 다 시간 기반이라 연출이 중간에 끊기거나 풀로 반납돼도 남지 않는다.
            SkillCastLockUtil.Apply(em, link.Entity, RiseDuration);
            SpawnProtectionUtil.Apply(em, link.Entity, RiseDuration);
        }

        return go;
    }

    /// <summary>
    /// 소환수에게 스켈레톤 외형을 입힌다.
    /// 위치를 시드로 써서 같은 소환에서도 조금씩 다른 무기를 든다.
    /// </summary>
    static void ApplySkeletonLook(GameObject go, Vector3 position)
    {
        if (!go.TryGetComponent<UnitAppearanceBridge>(out var appearance)) return;

        // ApplyEnemy 는 "같은 조합이면 Rebuild 스킵" 이라, 이름이 매번 같으면
        // 아군 외형에서 스켈레톤으로 갈아입는 첫 호출만 비용이 든다.
        string seed = $"Skeleton_{Mathf.RoundToInt(position.x * 3f)}_{Mathf.RoundToInt(position.y * 3f)}";
        appearance.ApplyEnemy(EnemyRace.Skeleton, seed);
    }
}
