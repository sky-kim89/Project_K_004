using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
//  CardCatalogCreator.cs  [Editor Only]
//  프로젝트의 모든 카드를 긁어 Resources/CardCatalog.asset 에 담는다.
//
//  ■ 왜 자동으로 긁나
//    목록을 손으로 관리하면 카드를 만들고 등록을 잊는 일이 반드시 생긴다.
//    그 증상은 "보상에 안 나온다" 이고, 원인이 목록이라는 걸 알아채기 어렵다.
//
//  ■ ⚠ 카드를 새로 만들면 이걸 다시 돌려야 한다
//    런타임은 이 에셋만 본다. 여기 없는 카드는 보상에도 안 나오고,
//    카드 바에서도 ID → SO 변환이 실패해 빈 칸으로 뜬다.
//
//  ■ 정렬은 마나 순이다
//    목록 순서가 곧 보상 후보 풀의 순서는 아니지만(무작위로 뽑는다),
//    인스펙터에서 훑어볼 때 싼 것부터 보이는 편이 밸런싱에 편하다.
//
//  사용: Tools > Project K > 데이터 생성 > 카드 목록
// ============================================================

public static class CardCatalogCreator
{
    const string ResourcesRoot = "Assets/Resources";
    const string AssetPath     = ResourcesRoot + "/CardCatalog.asset";

    [MenuItem(ProjectKMenu.Data + "카드 목록", priority = ProjectKMenu.DataPrio + 25)]
    public static void CreateAll()
    {
        Directory.CreateDirectory(ResourcesRoot);

        var catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(AssetPath);
        bool isNew = catalog == null;
        if (isNew) catalog = ScriptableObject.CreateInstance<CardCatalog>();

        catalog.Monsters  = Collect<MonsterSpeciesData>();
        catalog.Skills    = Collect<SkillCardData>();
        catalog.Summoners = Collect<SummonerData>();

        catalog.Monsters.Sort((a, b) => a.ManaCost.CompareTo(b.ManaCost));
        catalog.Skills  .Sort((a, b) => a.ManaCost.CompareTo(b.ManaCost));

        // ⚠ 이름순이 아니라 **명시한 순서**다 (2026-08-28)
        //   선택 화면의 좌우 넘김 순서가 곧 이 순서이고, 0번이 처음 켠 사람이
        //   보는 캐릭터다. 사전순으로 두면 beastmaster 가 첫 칸에 서고
        //   **견습 소환사가 7번째**로 밀린다 — 입문용을 찾아 넘겨야 한다.
        //
        //   ListOrder 의 정본은 SummonerCreator 의 스펙 배열이다.
        //   순서를 바꾸려면 그 배열을 옮기고 소환사 데이터를 다시 구울 것.
        //   (같은 값이면 Id 로 갈라 순서가 흔들리지 않게 한다)
        catalog.Summoners.Sort((a, b) =>
        {
            int byOrder = a.ListOrder.CompareTo(b.ListOrder);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
        });

        WarnOnDuplicateIds(catalog);

        if (isNew) AssetDatabase.CreateAsset(catalog, AssetPath);
        else       EditorUtility.SetDirty(catalog);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CardCatalogCreator] 완료 — 몬스터 {catalog.Monsters.Count}종 · " +
                  $"스킬 {catalog.Skills.Count}장 · 소환사 {catalog.Summoners.Count}명. " +
                  $"경로: {AssetPath}");
    }

    static List<T> Collect<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        var list = new List<T>(guids.Length);

        foreach (string guid in guids)
        {
            var so = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (so != null) list.Add(so);
        }

        return list;
    }

    /// <summary>
    /// ID 가 겹치면 알린다.
    ///
    /// ⚠ 조용히 두면 세이브가 엉킨다
    ///   카드 보유 상태는 ID 로 저장된다. 두 카드가 같은 ID 를 쓰면
    ///   한쪽을 주웠는데 다른 쪽이 레벨업하는 것으로 보인다.
    /// </summary>
    static void WarnOnDuplicateIds(CardCatalog catalog)
    {
        var seen = new HashSet<string>();

        foreach (var m in catalog.Monsters)
            if (!seen.Add(m.Id))
                Debug.LogError($"[CardCatalogCreator] 몬스터 ID 중복: '{m.Id}' ({m.name})");

        foreach (var s in catalog.Skills)
            if (!seen.Add(s.Id))
                Debug.LogError($"[CardCatalogCreator] 카드 ID 중복: '{s.Id}' ({s.name})");
    }
}
