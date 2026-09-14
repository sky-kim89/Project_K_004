#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
//  RelicTreeIconStubs.cs  [Editor Only]
//  유물 트리 노드의 **그림만** 담당한다.
//
//  ■ 하는 일은 하나다 — 없는 노드의 자리표시 PNG 를 굽는다
//    굽고 나서 자기가 만든 파일에 임포트 설정까지 걸어 둔다.
//    (설정을 남에게 맡기면 "구웠는데 안 뜬다" 가 생긴다)
//
//  ■ 표와 폴더가 맞는지는 여기서 안 본다
//    누락·고아·좌표 충돌 같은 정합성은 RelicTreeAudit 이 본다
//    (데이터 생성 > 유물 트리 점검). 그림 굽는 도구가 파일을 지우기까지
//    하면 "아이콘 만들려고 눌렀다가 뭔가 지워졌다" 가 된다.
//
//  ■ 자리표시가 무엇인가
//    진짜 그림은 밖에서 Docs/RelicTree_Icon_Spec.md 를 보고 만들어
//    이 폴더에 **같은 파일명으로 덮어씌운다.** 이 도구는 그 자리를 미리
//    만들어 둘 뿐이다 — 파일이 있어야 아틀라스가 잡히고, 그림 없이도
//    연동이 맞는지 확인할 수 있다.
//    계열 색 원반 + 티어 눈금만 그린다. "아직 그림이 없다" 가 한눈에
//    보여야 하므로 일부러 단순하다 — 진짜와 헷갈리면 셀 수가 없다.
//
//  ⚠ 이미 있는 파일은 어떤 경우에도 덮어쓰지 않는다
//    넣어 둔 진짜 그림을 자리표시로 되돌리면 작업이 통째로 날아간다.
//    전부 다시 깔려면 폴더를 지우고 다시 누를 것.
//
//  ⚠ 누른 뒤 '데이터 생성 > SpriteManager + 아틀라스' 까지 해야 화면에 뜬다.
//
//  메뉴: Tools > Project K > 아이콘·텍스처 > 유물 트리 아이콘
// ============================================================

public static class RelicTreeIconStubs
{
    const int Size = 128;   // Docs/RelicTree_Icon_Spec.md 의 최종 해상도와 같다

    /// <summary>줄바꿈 — 로그에서 줄을 나눈다.</summary>
    static readonly string N = System.Environment.NewLine;

    [MenuItem(ProjectKMenu.Icon + "유물 트리 아이콘", priority = ProjectKMenu.IconPrio + 20)]
    public static void Sync()
    {
        Directory.CreateDirectory(RelicIconKey.FolderPath);

        // ── 없는 것만 굽는다 ────────────────────────────────
        int made = 0, kept = 0;
        foreach (var def in RelicTreeCatalog.All)
        {
            string path = RelicIconKey.PathOf(def.Id);
            if (File.Exists(path)) { kept++; continue; }

            File.WriteAllBytes(path, Draw(def).EncodeToPNG());
            made++;
        }
        if (made > 0) AssetDatabase.Refresh();

        // ── 구운 것 포함, 임포트 설정을 다시 건다 ───────────
        //    정본은 IconImportSetup 이다 — 아이콘 폴더 전체가 같은 규칙을
        //    쓴다. 여기에 설정을 따로 적으면 두 곳이 갈린다.
        int reimported = 0;
        foreach (var def in RelicTreeCatalog.All)
        {
            string path = RelicIconKey.PathOf(def.Id);
            if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;

            IconImportSetup.Apply(ti, path);
            ti.SaveAndReimport();
            reimported++;
        }

        Debug.Log($"[유물 트리 아이콘] 노드 {RelicTreeCatalog.All.Length}개 · " +
                  $"자리표시 {made}장 생성 · {kept}장 유지 · 임포트 {reimported}장 재적용{N}" +
                  "→ 이어서 '데이터 생성 > SpriteManager + 아틀라스' 를 실행하세요.");
    }

    /// <summary>계열 색 원반 + 티어 눈금. 진짜 그림과 헷갈리지 않게 일부러 단순하다.</summary>
    static Texture2D Draw(RelicNodeDef def)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var px  = new Color32[Size * Size];   // 기본값 = 완전 투명

        Color c   = RelicTreePopup.ColorOf(def.Branch);
        var  fill = (Color32)c;
        var  line = (Color32)(c * 0.35f);
        float cx = (Size - 1) * 0.5f, cy = (Size - 1) * 0.5f;

        // 원반 — 반지름은 명세의 '중앙 주제 88~104px' 안에 들어간다
        const float R = 44f, Ring = 3f;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            if (d > R) continue;
            px[y * Size + x] = d > R - Ring ? line : fill;
        }

        // 티어 눈금 — 아래쪽에 티어 수만큼. 어느 깊이인지 자리표시에서도 읽히게.
        int  tier = Mathf.Clamp(def.Tier, 0, 5);
        const int TickW = 10, TickH = 5, Gap = 3;
        int total  = tier * TickW + Mathf.Max(0, tier - 1) * Gap;
        int startX = Mathf.RoundToInt(cx - total * 0.5f);
        for (int t = 0; t < tier; t++)
        for (int y = 12; y < 12 + TickH; y++)
        for (int x = startX + t * (TickW + Gap); x < startX + t * (TickW + Gap) + TickW; x++)
            if (x >= 0 && x < Size) px[y * Size + x] = line;

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
#endif
