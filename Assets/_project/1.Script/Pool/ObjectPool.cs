using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  ObjectPool (MonoBehaviour)
//  PoolType 하나를 담당하며 여러 종류의 프리팹을 함께 관리
//  키 = 오브젝트 이름 (prefab.name)
// ============================================================

public class ObjectPool : MonoBehaviour
{
    [Header("풀 설정")]
    public PoolType Type;

    [Header("관리 프리팹 목록 (에디터 버튼으로 자동 로드 가능)")]
    public List<GameObject> Prefabs = new();

    // 오브젝트 이름 → 비활성 인스턴스 목록
    readonly Dictionary<string, List<GameObject>> _inactive   = new();
    // 오브젝트 이름 → 원본 프리팹 (신규 생성 시 사용)
    readonly Dictionary<string, GameObject>        _prefabMap  = new();

    // ── 초기화 (PoolController가 Awake에서 호출) ───────────────
    public void Initialize()
    {
        foreach (var prefab in Prefabs)
        {
            if (prefab == null) continue;

            string key = prefab.name;
            _inactive[key]  = new List<GameObject>();
            _prefabMap[key] = prefab;
        }
    }

    // ── 꺼내기 ────────────────────────────────────────────────
    public GameObject Get(string name, Vector3 position = default, Quaternion rotation = default)
    {
        if (!_inactive.TryGetValue(name, out var pool))
        {
            Debug.LogWarning($"[ObjectPool:{Type}] 등록되지 않은 이름: {name}");
            return null;
        }

        // 파괴된 GO 건너뜀 (씬 리로드·직접 Destroy 호출 시 stale 참조 발생)
        GameObject obj = null;
        while (pool.Count > 0)
        {
            int last      = pool.Count - 1;
            var candidate = pool[last];
            pool.RemoveAt(last);

            if (candidate == null) continue;

            // ⚠ 아직 쓰이고 있는 오브젝트는 절대 다시 내주지 않는다
            //   대기 목록에 활성 오브젝트가 들어 있다는 것은 같은 인스턴스가
            //   두 번 반납됐다는 뜻이다. 그대로 내주면 주인이 둘이 되고,
            //   먼저 쓰던 쪽이 반납하는 순간 **뒤에 받은 쪽 캐릭터가 꺼진다**
            //   ("스폰했는데 비활성" 의 정체). 여기서 끊고 원인을 남긴다.
            if (candidate.activeSelf)
            {
                Debug.LogError($"[ObjectPool:{Type}] '{name}' 이 사용 중인데 대기 목록에 있다 " +
                               "— 이중 반납이다. 이 인스턴스는 버리고 새로 만든다.");
                continue;
            }

            obj = candidate;
            break;
        }
        if (obj == null)
            obj = CreateInstance(name);

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);

        // ⚠ 부모가 꺼져 있으면 SetActive(true) 를 해도 화면에 안 나온다
        //   풀 오브젝트 자신이 꺼진 채로 남아 있는 경우를 바로 잡아 준다 —
        //   여기서 안 막으면 "스폰은 했는데 아무것도 안 보인다" 로만 나타난다.
        if (!obj.activeInHierarchy)
        {
            Debug.LogError($"[ObjectPool:{Type}] '{name}' 을 켰는데 계층이 비활성이다 " +
                           $"— 부모({transform.name})가 꺼져 있다. 부모를 켠다.");
            gameObject.SetActive(true);
        }

        return obj;
    }

    /// <summary>
    /// 등록된 원본 프리팹을 이름으로 조회한다 (인스턴스 아님).
    /// 프리팹이 들고 있는 머티리얼·설정을 런타임에 읽고 싶을 때 쓴다
    /// — 같은 에셋을 Resources 에 또 복사해 두는 것보다 출처가 하나로 유지된다.
    /// </summary>
    public GameObject GetPrefab(string name)
        => _prefabMap.TryGetValue(name, out var prefab) ? prefab : null;

    // ── 미리 만들어 두기 ──────────────────────────────────────
    /// <summary>
    /// name 프리팹의 비활성 인스턴스를 count 개까지 미리 채워 둔다.
    /// 이미 그만큼 있으면 아무것도 하지 않으므로 여러 번 불러도 안전하다.
    ///
    /// 전투 중 Instantiate 가 몰리면 프레임이 끊긴다 — 이펙트처럼
    /// 짧은 시간에 여러 개가 동시에 필요한 오브젝트는 전투 시작 전에 채운다.
    /// </summary>
    public void Prewarm(string name, int count)
    {
        if (!_inactive.TryGetValue(name, out var pool))
        {
            Debug.LogWarning($"[ObjectPool:{Type}] 등록되지 않은 이름: {name}");
            return;
        }

        for (int i = pool.Count; i < count; i++)
            pool.Add(CreateInstance(name));
    }

    /// <summary>지금 대기 중인(꺼내 쓸 수 있는) 인스턴스 수. 등록되지 않은 이름이면 0.</summary>
    public int InactiveCount(string name)
        => _inactive.TryGetValue(name, out var pool) ? pool.Count : 0;

    // ── 반납 ──────────────────────────────────────────────────
    public void Release(string name, GameObject instance)
    {
        if (instance == null) return;

        if (!_inactive.TryGetValue(name, out var pool))
        {
            // 예전엔 조용히 흘려보냈다 — 인스턴스가 어느 목록에도 없이 사라져
            // 풀이 매번 새로 만들기만 했다. 이름이 어긋난 것은 버그다.
            Debug.LogError($"[ObjectPool:{Type}] 등록되지 않은 이름으로 반납: '{name}' " +
                           $"({instance.name}) — 인스턴스를 파괴한다.");
            Destroy(instance);
            return;
        }

        // ⚠ 이중 반납 차단
        //   같은 인스턴스가 목록에 두 번 들어가면 Get 이 서로 다른 두 곳에 같은
        //   오브젝트를 내준다. 그 뒤로는 한쪽이 반납할 때마다 다른 쪽 캐릭터가 꺼진다.
        if (pool.Contains(instance))
        {
            Debug.LogError($"[ObjectPool:{Type}] '{name}' 이중 반납 — 두 번째 요청은 무시한다.");
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(transform);
        pool.Add(instance);
    }

    // ── 전체 정리 ──────────────────────────────────────────────
    public void Clear()
    {
        foreach (var list in _inactive.Values)
        {
            foreach (var obj in list)
                if (obj != null) Destroy(obj);
            list.Clear();
        }
    }

    // ── 내부 생성 ──────────────────────────────────────────────
    GameObject CreateInstance(string name)
    {
        var obj = Instantiate(_prefabMap[name], transform);
        obj.SetActive(false);
        return obj;
    }
}
