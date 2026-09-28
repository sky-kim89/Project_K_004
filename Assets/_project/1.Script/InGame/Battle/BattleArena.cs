using UnityEngine;
using Unity.Entities;
using BattleGame.Units;
using BattleGame.Projectiles;

// ============================================================
//  BattleArena.cs
//  "전투가 벌어지는 판" 을 열고 닫는 단일 창구.
//
//  ■ 왜 필요한가 — ECS 월드에는 씬 개념이 없다
//    로비 배경 데모와 실전은 같은 EntityManager 를 쓴다.
//    쿼리는 월드 전역이라 서로를 구분하지 못한다 —
//      · 승리 판정은 "적 엔티티가 0" 을 보므로 데모 적이 남아 있으면 안 끝나고
//      · DespawnAllUnits 는 유닛 전부를 지우므로 실전이 데모를 쓸어 간다.
//    그래서 규칙은 하나다: **판은 한 번에 하나만 열려 있다.**
//
//  ■ 닫을 때 남는 것이 없어야 한다
//    유닛만 지우면 부족하다. 전투를 끝낸 순간에도
//      · 날아가던 발사체 엔티티
//      · 수명 타이머로 돌아가는 이펙트 오브젝트
//      · 통계 누적치
//    가 남아서 다음 판(또는 로비)에서 터진다. 여기서 한 번에 정리한다.
//
//  사용:
//    BattleArena.Instance.Open(ArenaKind.Demo);   // 로비 배경 데모
//    BattleArena.Instance.Open(ArenaKind.Real);   // 실전 (Close 후 열림)
//    BattleArena.Instance.Close();
// ============================================================

public enum ArenaKind
{
    None = 0,
    Demo = 1,   // 로비 배경 데모 — 보상·저장·승패 없음
    Real = 2,   // 실전 — BattleManager 가 주도
}

public class BattleArena : Singleton<BattleArena>
{
    ArenaKind _kind = ArenaKind.None;

    public ArenaKind Kind    => _kind;
    public bool      IsOpen  => _kind != ArenaKind.None;
    public bool      IsDemo  => _kind == ArenaKind.Demo;

    /// <summary>씬에 배치돼 있지 않아도 쓸 수 있게 필요할 때 만든다.</summary>
    public static BattleArena Ensure()
    {
        if (Instance != null) return Instance;
        return new GameObject(nameof(BattleArena)).AddComponent<BattleArena>();
    }

    // ── 열기 / 닫기 ──────────────────────────────────────────

    /// <summary>
    /// 판을 연다. 다른 종류가 열려 있으면 먼저 닫는다 —
    /// 같은 종류면 아무것도 하지 않는다 (중복 진입 방지).
    /// </summary>
    public bool Open(ArenaKind kind)
    {
        if (kind == ArenaKind.None) { Close(); return false; }
        if (_kind == kind) return false;

        if (_kind != ArenaKind.None) Close();

        _kind = kind;
        Debug.Log($"[BattleArena] 열림 — {kind}");
        return true;
    }

    /// <summary>판을 닫고 월드에서 이 판의 흔적을 전부 지운다.</summary>
    public void Close()
    {
        if (_kind == ArenaKind.None) return;

        Debug.Log($"[BattleArena] 닫힘 — {_kind}");
        ArenaKind closing = _kind;
        _kind = ArenaKind.None;

        // ⓪ 새로 세우는 곳을 먼저 막는다 — 몬스터 배출 · 용사 편성 · 로비 데모
        //   치운 **뒤에** 나온 개체는 아무도 거두지 않는다. 전투 도중 '즉시 환생' 하면
        //   배출 코루틴이 남아 로비 뒤에 몬스터가 계속 섰다 (2026-09-22).
        SummonController.Instance?.Halt();
        foreach (var spawner in FindObjectsByType<HeroSpawner>(FindObjectsSortMode.None))
            spawner.Halt();
        //   ⚠ 데모는 **데모 판을 닫을 때만** 멈춘다 — 실전을 닫는 순간(Returning)은
        //     로비가 막 켜지며 데모가 다음 판을 기다리기 시작한 때다. 여기서 끊으면 배경이 빈다.
        if (closing == ArenaKind.Demo) LobbyDemoBattle.Instance?.Halt();

        // ① 유닛 — 이미 검증된 경로를 그대로 쓴다 (풀 반납 + 엔티티 파괴)
        BattleManager.Instance?.DespawnAllUnits();

        // ② 발사체 — 유닛이 사라져도 공중에 떠 있던 것은 남는다
        DestroyProjectiles();

        // ③ 화면에 남은 이펙트·발사체 오브젝트 회수
        //    수명 타이머를 기다리면 다음 판에서 터진다.
        PoolController.Instance?.DespawnAll(PoolType.Effect);
        PoolController.Instance?.DespawnAll(PoolType.Projectile);

        // ④ 통계 — 데모에서 쌓인 수치가 실전 결과창에 섞이면 안 된다
        BattleStatsTracker.Instance?.Reset();
        CardStatsTracker.Instance?.Reset();   // 데모 몬스터의 엔티티 → 카드 매핑도 남기지 않는다

        VerifyClean();
    }

    // ── 내부 ─────────────────────────────────────────────────

    static void DestroyProjectiles()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        var em = world.EntityManager;
        em.CompleteAllTrackedJobs();

        var query = em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileComponent>());
        em.DestroyEntity(query);
        query.Dispose();
    }

    /// <summary>
    /// 닫은 뒤에도 유닛 엔티티가 남아 있으면 알린다.
    /// ⚠ 조용히 남으면 다음 판의 승리 판정이 영영 안 떨어진다 —
    ///   그때 원인을 찾는 것보다 여기서 바로 시끄러운 편이 싸다.
    /// </summary>
    static void VerifyClean()
    {
#if UNITY_EDITOR
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        var em    = world.EntityManager;
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<UnitIdentityComponent>());
        int left  = query.CalculateEntityCount();
        query.Dispose();

        if (left > 0)
            Debug.LogWarning($"[BattleArena] 판을 닫았는데 유닛 엔티티가 {left}개 남았습니다.");
#endif
    }
}
