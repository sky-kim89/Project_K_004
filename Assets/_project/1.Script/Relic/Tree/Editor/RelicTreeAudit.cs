#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
//  RelicTreeAudit.cs  [Editor Only]
//  유물 트리 표(RelicTreeCatalog)가 코드·폴더와 맞는지 본다.
//  **그림은 굽지 않는다** — 그건 아이콘·텍스처 > 유물 트리 아이콘 이 한다.
//
//  ■ 왜 따로 있나
//    이 트리에서 어긋나는 것들은 **에러가 안 난다.** 노드는 화면에 멀쩡히
//    뜨고 숫자도 붙는데, 설명만 빈칸이거나 그 몫이 엉뚱한 진영으로 가거나
//    아이콘이 조용히 안 붙는다. 2026-09-07 트리 교체 때 실제로
//    46개 중 29개가 설명 빈칸이었고, 옛 PNG 60장이 아틀라스에 남아 있었다.
//
//    RelicTreeCatalog.Verify() 가 잡는 것은 **코드로 판정되는 것뿐**이다
//    (타깃·설명 줄). 폴더 상태와 좌표 겹침은 표만 봐서는 모른다 —
//    그걸 이 도구가 본다.
//
//  ■ 보는 것 넷
//    1) 아이콘 누락   — 표에 있는데 PNG 가 없는 노드
//    2) 고아 아이콘   — PNG 는 있는데 표에 없는 것 (물어본 뒤 지운다)
//    3) 좌표 겹침     — 같은 x 면 dy ≥ 2, 같은 y 면 dx ≥ 2 여야 한다
//    4) 부모 끊김     — 부모가 표에 없는 노드
//
//  ⚠ 고아를 지울지는 사람이 판단한다
//    파일명만 보고는 자리표시인지 손으로 그린 진짜인지 알 수 없다.
//    그래서 목록을 콘솔에 먼저 찍고 물어본다.
//
//  메뉴: Tools > Project K > 데이터 생성 > 유물 트리 점검
// ============================================================

public static class RelicTreeAudit
{
    static readonly string N = System.Environment.NewLine;

    [MenuItem(ProjectKMenu.Data + "유물 트리 점검", priority = ProjectKMenu.DataPrio + 26)]
    public static void Run()
    {
        var all = RelicTreeCatalog.All;   // ⚠ 여기서 Verify() 가 함께 돈다 (타깃·설명 줄)
        int problems = 0;

        // ── 1) 아이콘 누락 ──────────────────────────────────
        var wanted  = new HashSet<string>();
        var missing = new List<string>();
        foreach (var def in all)
        {
            wanted.Add(RelicIconKey.Of(def.Id));
            if (!File.Exists(RelicIconKey.PathOf(def.Id)))
                missing.Add($"{def.Name}  ({RelicIconKey.Of(def.Id)}.png)");
        }
        if (missing.Count > 0)
        {
            problems++;
            Debug.LogWarning($"[유물 트리 점검] 아이콘 없는 노드 {missing.Count}개:{N}  " +
                             string.Join(N + "  ", missing) + N +
                             "→ '아이콘·텍스처 > 유물 트리 아이콘' 으로 자리표시를 구우세요.");
        }

        // ── 3) 좌표 겹침 ────────────────────────────────────
        //    한 칸 118px 인데 노드 카드는 세로 130px 쯤 된다. 겹치면
        //    화면에서 두 노드가 포개져 하나를 못 누른다. 대각선은 자유.
        var clashes = new List<string>();
        for (int i = 0; i < all.Length; i++)
        for (int j = i + 1; j < all.Length; j++)
        {
            var a = all[i]; var b = all[j];
            int dx = Mathf.Abs(a.X - b.X), dy = Mathf.Abs(a.Y - b.Y);
            if ((dx == 0 && dy < 2) || (dy == 0 && dx < 2))
                clashes.Add($"{a.Name} ({a.X},{a.Y})  ↔  {b.Name} ({b.X},{b.Y})");
        }
        if (clashes.Count > 0)
        {
            problems++;
            Debug.LogError($"[유물 트리 점검] 좌표가 겹치는 쌍 {clashes.Count}개:{N}  " +
                           string.Join(N + "  ", clashes));
        }

        // ── 4) 부모 끊김 ────────────────────────────────────
        var ids    = new HashSet<RelicNodeId>();
        foreach (var def in all) ids.Add(def.Id);
        var broken = new List<string>();
        foreach (var def in all)
            if (def.Parent != RelicNodeId.None && !ids.Contains(def.Parent))
                broken.Add($"{def.Name} → 부모 {def.Parent} 가 표에 없다");
        if (broken.Count > 0)
        {
            problems++;
            Debug.LogError($"[유물 트리 점검] 부모가 끊긴 노드 {broken.Count}개:{N}  " +
                           string.Join(N + "  ", broken));
        }

        // ── 2) 고아 아이콘 ──────────────────────────────────
        var orphans = new List<string>();
        if (Directory.Exists(RelicIconKey.FolderPath))
            foreach (string path in Directory.GetFiles(RelicIconKey.FolderPath, "*.png"))
                if (!wanted.Contains(Path.GetFileNameWithoutExtension(path)))
                    orphans.Add(path.Replace(Path.DirectorySeparatorChar, '/'));

        int deleted = 0;
        if (orphans.Count > 0)
        {
            problems++;
            orphans.Sort();
            Debug.LogWarning($"[유물 트리 점검] 표에 없는 그림 {orphans.Count}장:{N}  " +
                             string.Join(N + "  ", orphans));

            if (EditorUtility.DisplayDialog(
                    "유물 트리 점검",
                    $"표에 없는 그림 {orphans.Count}장을 지웁니다." + N +
                    "목록은 콘솔에 있습니다." + N +
                    "⚠ 손으로 그려 넣은 그림이 섞여 있지 않은지 먼저 확인하세요.",
                    "지운다", "남겨 둔다"))
            {
                foreach (string path in orphans) AssetDatabase.DeleteAsset(path);
                deleted = orphans.Count;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        // ── 요약 ────────────────────────────────────────────
        string tail = deleted > 0
            ? $"{N}→ 그림을 지웠으니 '데이터 생성 > SpriteManager + 아틀라스' 를 다시 실행하세요."
            : "";

        if (problems == 0)
            Debug.Log($"[유물 트리 점검] 노드 {all.Length}개 · 이상 없음 " +
                      $"(아이콘 · 좌표 · 부모 · 고아 전부 정상).");
        else
            Debug.Log($"[유물 트리 점검] 노드 {all.Length}개 · 문제 {problems}가지 — " +
                      $"아이콘 누락 {missing.Count} · 좌표 겹침 {clashes.Count} · " +
                      $"부모 끊김 {broken.Count} · 고아 {orphans.Count}(삭제 {deleted}){tail}");
    }
}
#endif
