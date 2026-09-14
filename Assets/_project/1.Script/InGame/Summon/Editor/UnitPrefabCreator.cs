using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
//  UnitPrefabCreator.cs  [Editor Only]
//  인게임에서 쓸 유닛 프리팹을 굽는다.
//
//  ■ 굽는 것
//    Monster.prefab  — 소환 몬스터 (모든 종족이 이 하나를 공유한다)
//    Summoner.prefab — 소환사 본체 (General.prefab 을 복제해 브리지만 교체)
//
//  ■ 몬스터는 왜 종족별 프리팹이 아닌가
//    외형이 런타임에 결정되기 때문이다.
//      인간형   → CharacterBuilder 가 종족으로 합성
//      비인간형 → 완성 SpriteLibraryAsset 을 꽂기만 하면 됨
//    둘 다 같은 골격(Animator·SpriteLibrary·Body)이면 되므로 프리팹 하나로 족하다.
//    종족마다 프리팹을 만들면 9개를 똑같이 관리해야 하고, 풀도 9개로 쪼개진다.
//
//  ■ 소환사는 왜 General 복제인가
//    장비를 장착할 수 있어야 해서 인간형 합성 골격이 필요하다.
//    그 골격을 이미 갖춘 프리팹이 General.prefab 이다.
//
//  ⚠ 풀 등록은 폴더 스캔이다
//    PoolControllerEditor 가 Assets/_project/2.Prefabs/Unit 을 통째로 읽어
//    **프리팹 이름을 그대로 풀 키로** 쓴다. 그래서 파일명이 곧 PoolKey 다
//    (SpawnEntry.PoolKey => UnitType.ToString() 과 맞아떨어져야 한다).
//    이 스크립트를 돌린 뒤 PoolController 인스펙터에서
//    "Load Prefabs From Folder" 를 눌러야 반영된다.
//
//  사용: Tools > Project K > 프리팹 생성 > 인게임 유닛
// ============================================================

public static class UnitPrefabCreator
{
    const string PrefabRoot = "Assets/_project/2.Prefabs/Unit";

    const string MonsterPath  = PrefabRoot + "/Monster.prefab";
    const string GeneralPath  = PrefabRoot + "/General.prefab";
    const string SummonerPath = PrefabRoot + "/Summoner.prefab";

    [MenuItem(ProjectKMenu.InGame + "인게임 유닛", priority = ProjectKMenu.PrefabPrio + 11)]
    public static void CreateAll()
    {
        PatchMonster();
        CreateSummoner();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[UnitPrefabCreator] 완료.\n" +
                  "⚠ PoolController 인스펙터에서 'Load Prefabs From Folder' 를 눌러야 풀에 등록됩니다.");
    }

    // ── 몬스터 ───────────────────────────────────────────────

    /// <summary>
    /// Monster.prefab 에 MonsterAppearanceBridge 를 붙인다.
    /// 인간형/비인간형 외형 분기를 이 컴포넌트가 맡는다.
    /// </summary>
    static void PatchMonster()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPath);
        if (prefab == null)
        {
            Debug.LogError($"[UnitPrefabCreator] 프리팹을 찾지 못했습니다: {MonsterPath}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(MonsterPath);

        bool changed = false;

        if (root.GetComponent<MonsterAppearanceBridge>() == null)
        {
            root.AddComponent<MonsterAppearanceBridge>();
            changed = true;
            Debug.Log("[UnitPrefabCreator] Monster.prefab ← MonsterAppearanceBridge 추가");
        }

        // ⚠ 없어진 MonsterRefundCarrier 의 잔해를 먼저 걷어낸다 (2026-08-28)
        //   그 클래스는 삭제됐다(환수 → 라인 복귀). 이미 구워 둔 프리팹에는
        //   "Missing (Mono Script)" 로 남아 있어, 지우지 않으면 인스펙터가
        //   경고를 뿜고 프리팹을 저장할 때마다 따라다닌다.
        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
        if (removed > 0)
        {
            changed = true;
            Debug.Log($"[UnitPrefabCreator] Monster.prefab ← 깨진 스크립트 {removed}개 제거");
        }

        // 적을 다 잡고 살아남았을 때 제 라인으로 돌아가는 컴포넌트.
        if (root.GetComponent<MonsterLineReturner>() == null)
        {
            root.AddComponent<MonsterLineReturner>();
            changed = true;
            Debug.Log("[UnitPrefabCreator] Monster.prefab ← MonsterLineReturner 추가");
        }

        // 좋은 장비를 낀 개체 주위를 도는 불빛 (MonsterGearRule.AuraMinGrade 이상).
        // ⚠ 빛 오브젝트는 여기서 만들지 않는다 — 처음 필요해진 순간 런타임이 만든다.
        //   장비를 낀 개체가 전체의 일부라, 프리팹에 셋을 박아 두면 대부분의
        //   개체가 쓰지도 않는 SpriteRenderer 셋을 들고 다닌다.
        if (root.GetComponent<MonsterGearAuraView>() == null)
        {
            root.AddComponent<MonsterGearAuraView>();
            changed = true;
            Debug.Log("[UnitPrefabCreator] Monster.prefab ← MonsterGearAuraView 추가");
        }

        if (changed) PrefabUtility.SaveAsPrefabAsset(root, MonsterPath);
        PrefabUtility.UnloadPrefabContents(root);

        if (!changed) Debug.Log("[UnitPrefabCreator] Monster.prefab — 이미 최신입니다.");
    }

    // ── 소환사 ───────────────────────────────────────────────

    /// <summary>
    /// General.prefab 을 복제해 Summoner.prefab 을 만든다.
    /// 장수용 브리지를 떼고 소환사 브리지를 붙인다.
    /// </summary>
    static void CreateSummoner()
    {
        if (File.Exists(SummonerPath))
        {
            // 이미 있으면 브리지만 확인한다 — 통째로 다시 만들면
            // 씬에 배치된 참조가 GUID 째로 끊어진다.
            GameObject existing = PrefabUtility.LoadPrefabContents(SummonerPath);
            bool fixedUp = EnsureSummonerBridge(existing);
            if (fixedUp) PrefabUtility.SaveAsPrefabAsset(existing, SummonerPath);
            PrefabUtility.UnloadPrefabContents(existing);

            Debug.Log("[UnitPrefabCreator] Summoner.prefab — " +
                      (fixedUp ? "브리지 교체함" : "이미 최신입니다."));
            return;
        }

        if (!File.Exists(GeneralPath))
        {
            Debug.LogError($"[UnitPrefabCreator] 원본을 찾지 못했습니다: {GeneralPath}");
            return;
        }

        if (!AssetDatabase.CopyAsset(GeneralPath, SummonerPath))
        {
            Debug.LogError("[UnitPrefabCreator] Summoner.prefab 복제에 실패했습니다.");
            return;
        }

        AssetDatabase.ImportAsset(SummonerPath);

        GameObject root = PrefabUtility.LoadPrefabContents(SummonerPath);
        root.name = "Summoner";
        EnsureSummonerBridge(root);
        PrefabUtility.SaveAsPrefabAsset(root, SummonerPath);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log("[UnitPrefabCreator] Summoner.prefab 생성 (General.prefab 복제)");
    }

    /// <summary>
    /// 장수 브리지를 떼고 소환사 브리지를 붙인다.
    /// 바뀐 게 있으면 true.
    /// </summary>
    static bool EnsureSummonerBridge(GameObject root)
    {
        bool changed = false;

        // ⚠ 장수 브리지가 남아 있으면 안 된다
        //   둘 다 Start 에서 엔티티를 만들려 들어 소환사가 두 번 스폰된다.
        var general = root.GetComponent<GeneralRuntimeBridge>();
        if (general != null)
        {
            Object.DestroyImmediate(general, true);
            changed = true;
        }

        if (root.GetComponent<SummonerRuntimeBridge>() == null)
        {
            root.AddComponent<SummonerRuntimeBridge>();
            changed = true;
        }

        return changed;
    }
}
