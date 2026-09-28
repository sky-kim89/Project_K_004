using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  LobbyDemoBattle.cs
//  소환사 선택 화면(MainPanel) 뒤에서 **고른 소환사의 시작 덱**이 진군하는 배경.
//
//  ■ 인게임을 그대로 쓴다
//    몬스터는 실전과 같은 MonsterSpawner.SpawnFree 로 서고, 적이 없으니
//    이동 시스템이 알아서 +X 로 걸어 나간다 (UnitMovementSystem — 타겟 없는 아군은 전진).
//    오른쪽 끝(ScreenClampJob 이 가두는 자리)에 닿으면 풀에 돌려보낸다.
//
//  ■ 용사는 세우지 않는다
//    용사가 죽으면 사망 파이프라인이 런 골드를 준다 (UnitDeathDespawnSystem).
//    로비에서 싸움이 나면 세이브가 오염된다 — 진군만 보여 준다.
//
//  ■ 리셋 규칙 — 판은 BattleArena 가 소유한다
//    · 열기  : BattleArena.Open(Demo) + PresentMode.ArenaBehindUI
//    · 닫기  : BattleArena.Close() — 유닛·발사체·이펙트·통계를 한 번에 지운다
//    · 실전  : Open(Real) 이 데모를 먼저 닫는다 → Close 가 Halt() 를 부른다
//    ⚠ 유닛을 여기서 따로 지우지 않는다 (소환사를 바꿀 때만 제 것을 돌려보낸다).
//      정리 경로가 둘이 되면 한쪽만 고쳐진다.
//
//  ■ Returning 중에 켜질 수 있다
//    런이 끝나면 로비 캔버스가 먼저 켜지고(MainPanel.OnEnable) 그 뒤에 실전 판을 치운다.
//    그래서 Idle 이 될 때까지 기다렸다가 연다.
// ============================================================

public class LobbyDemoBattle : Singleton<LobbyDemoBattle>
{
    /// <summary>한 화면에 동시에 걷는 최대 마릿수.</summary>
    const int MaxAlive = 28;

    /// <summary>한 마리를 세우는 간격(초).</summary>
    const float SpawnInterval = 0.45f;

    /// <summary>처음 열 때·소환사를 바꿀 때 전장에 미리 흩어 두는 수 — 빈 화면에서 시작하지 않게.</summary>
    const int BurstCount = 14;

    /// <summary>오른쪽 끝에서 이만큼 안쪽에 닿으면 거둔다 (클램프에 걸려 쌓이기 전에).</summary>
    const float RecycleInset = 1f;

    SummonerData _summoner;
    Coroutine    _run;
    bool         _needBurst;

    readonly List<GameObject> _units = new(MaxAlive);

    public static LobbyDemoBattle Ensure()
    {
        if (Instance != null) return Instance;
        return new GameObject(nameof(LobbyDemoBattle)).AddComponent<LobbyDemoBattle>();
    }

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>이 소환사의 덱으로 배경을 돌린다. 같은 소환사면 이어서 돈다.</summary>
    public void Show(SummonerData summoner)
    {
        if (summoner != _summoner)
        {
            _summoner = summoner;
            RecallUnits();
            _needBurst = true;
        }

        if (_run == null) _run = StartCoroutine(Run());
    }

    /// <summary>선택 화면을 떠났다 — 데모 판을 닫고 로비 화면으로 되돌린다.</summary>
    public void Hide()
    {
        StopRun();
        _summoner = null;

        var arena = BattleArena.Instance;
        if (arena != null && arena.IsDemo) arena.Close();   // → Halt()

        // ⚠ 실전으로 넘어가는 중이면 화면을 건드리지 않는다 — 그쪽이 Battle 로 돌린다.
        if (LobbyManager.Instance != null && LobbyManager.Instance.Flow == LobbyFlow.Idle &&
            SceneDirector.Instance != null && SceneDirector.Instance.Mode == PresentMode.ArenaBehindUI)
            SceneDirector.Instance.Present(PresentMode.LobbyOnly);
    }

    /// <summary>
    /// 데모 판이 닫혔다 (BattleArena.Close) — 세우기를 멈추고 목록을 비운다.
    /// ⚠ 유닛은 Close 가 이미 전부 풀에 돌려보냈다. 여기서 또 반납하지 않는다.
    /// </summary>
    public void Halt()
    {
        StopRun();
        _units.Clear();
        _needBurst = true;
    }

    // ── 루프 ─────────────────────────────────────────────────

    IEnumerator Run()
    {
        yield return SceneDirector.Ensure().EnsureInGameResident();
        yield return null;

        // 런을 닫는 중(Returning)이면 청소가 끝나 Idle 이 될 때까지 기다린다.
        while (LobbyManager.Instance.Flow != LobbyFlow.Idle) yield return null;

        var arena = BattleArena.Ensure();
        if (arena.IsOpen && !arena.IsDemo)
        {
            Debug.LogWarning($"[LobbyDemoBattle] 실전 판이 열려 있어 데모를 띄우지 않습니다 ({arena.Kind}).");
            _run = null;
            yield break;
        }

        arena.Open(ArenaKind.Demo);

        var director = SceneDirector.Instance;
        director.Present(PresentMode.ArenaBehindUI);
        ArenaCameraRig.Snap(director.InGameCam, ArenaCameraRig.HomeX, ArenaCameraRig.HomeSize);

        var wait = new WaitForSeconds(SpawnInterval);

        while (arena.IsDemo)
        {
            Prune();

            if (_needBurst)
            {
                _needBurst = false;
                for (int i = 0; i < BurstCount; i++) SpawnOne(scattered: true);
            }
            else if (_units.Count < MaxAlive)
            {
                SpawnOne(scattered: false);
            }

            yield return wait;
        }

        _run = null;
    }

    void StopRun()
    {
        if (_run != null) StopCoroutine(_run);
        _run = null;
    }

    // ── 세우기 · 거두기 ──────────────────────────────────────

    /// <param name="scattered">true 면 성벽~오른쪽 끝 사이 아무 데나 — 이미 행군 중인 그림.</param>
    void SpawnOne(bool scattered)
    {
        MonsterSpeciesData[] deck = _summoner.StarterMonsters;
        MonsterSpeciesData species = deck[Random.Range(0, deck.Length)];

        SummonFieldLayout field = SummonFieldLayout.Instance;
        Vector3 at = field.GetSpawnPosition(Random.Range(0, SummonFieldLayout.LaneCount));

        if (scattered) at.x = Random.Range(at.x, RightEdge() - RecycleInset * 3f);

        // generation 1 — 죽어도 분열·부활하지 않는다 (로비 배경이 불어나지 않게)
        GameObject go = MonsterSpawner.SpawnFree(species, _summoner, at, generation: 1);
        if (go != null) _units.Add(go);
    }

    /// <summary>오른쪽 끝에 닿은 개체와 이미 풀에 돌아간 개체를 목록에서 뺀다.</summary>
    void Prune()
    {
        float edge = RightEdge() - RecycleInset;

        for (int i = _units.Count - 1; i >= 0; i--)
        {
            GameObject go = _units[i];

            // 풀에 돌아갔다(다른 경로가 거뒀다) — 이미 다른 개체일 수 있으니 목록에서만 뺀다.
            if (go == null || !go.activeInHierarchy) { _units.RemoveAt(i); continue; }

            if (go.transform.position.x < edge) continue;

            PoolController.Instance.Despawn(go);
            _units.RemoveAt(i);
        }
    }

    /// <summary>소환사를 바꿨다 — 걷던 개체를 풀에 돌려보낸다.</summary>
    void RecallUnits()
    {
        foreach (GameObject go in _units)
            if (go != null && go.activeInHierarchy) PoolController.Instance.Despawn(go);
        _units.Clear();
    }

    float RightEdge()
    {
        Camera cam = SceneDirector.Instance.InGameCam;
        return cam.transform.position.x + cam.orthographicSize * cam.aspect;
    }
}
