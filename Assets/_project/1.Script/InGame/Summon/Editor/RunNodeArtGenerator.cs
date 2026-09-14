using UnityEditor;
using UnityEngine;

// ============================================================
//  RunNodeArtGenerator.cs  [Editor Only]
//  Tools > Project K > 아이콘·텍스처 > 갈림길 그림
//  갈림길·시설 그림 7장을 256×256 PNG 로 만든다.
//    → Assets/_project/3.Textures/Icons/RunNodes/node_<Kind>.png
//
//  ■ ⚠ 더미다 — 자리를 잡아 두는 것이 전부다
//    화면(CrossroadUI 카드 · FacilityPopup 배경)이 그림을 전제로 짜여 있어서,
//    비워 두면 레이아웃을 확인할 수가 없다. 그래서 도형으로라도 채운다.
//    손그림이 오면 **같은 경로·같은 파일명**으로 덮으면 코드는 그대로 돈다
//    (RunNodeArtAssets 참고).
//
//  ■ 아이콘이 아니라 '장면' 이다
//    64px 아이콘 키트(IconArt)를 쓰지 않는다. 이 그림은 카드 왼쪽에 140px,
//    시설 화면 배경으로는 그보다 크게 깔린다 — 실루엣 하나가 아니라
//    하늘·바닥·주인공이 있는 한 장면이어야 크게 늘려도 견딘다.
//
//  ■ 그리는 방법
//    IconGenerator.P 의 픽셀 캔버스를 그대로 쓴다. 위/아래를 다른 색으로
//    나눠 지평선을 만들고, 그 위에 도형 몇 개를 얹는다.
// ============================================================

public static class RunNodeArtGenerator
{
    const int Size = 256;

    [MenuItem(ProjectKMenu.Icon + "갈림길 그림", priority = ProjectKMenu.IconPrio + 3)]
    public static void Generate()
    {
        string dir = RunNodeArtAssets.Dir.TrimEnd('/');
        IconGenerator.EnsureDir(dir);

        Save(RunNodeKind.NormalBattle, DrawNormalBattle);
        Save(RunNodeKind.EliteBattle,  DrawEliteBattle);
        Save(RunNodeKind.Camp,         DrawCamp);
        Save(RunNodeKind.Forge,        DrawForge);
        Save(RunNodeKind.Shop,         DrawShop);
        Save(RunNodeKind.Altar,        DrawAltar);
        Save(RunNodeKind.Event,        DrawEvent);

        AssetDatabase.Refresh();
        IconGenerator.ApplySpriteImportSettings(dir, Size);
        AssetDatabase.SaveAssets();

        Verify();

        Debug.Log($"[RunNodeArtGenerator] 갈림길 그림 {RunNodeRule.AllKinds.Length}장 생성 완료 → {dir}");
    }

    static void Save(RunNodeKind kind, System.Action<IconGenerator.P> draw)
        => IconGenerator.Save(Size, Size, RunNodeArtAssets.PathOf(kind), draw);

    /// <summary>
    /// 순서 정본에 있는 종류가 전부 구워졌는지 본다.
    ///
    /// ⚠ 이 검사가 없으면 실패가 조용하다 — 화면에서는 "그림이 없네" 로만 보인다.
    /// </summary>
    static void Verify()
    {
        foreach (RunNodeKind kind in RunNodeRule.AllKinds)
            if (AssetDatabase.LoadAssetAtPath<Sprite>(RunNodeArtAssets.PathOf(kind)) == null)
                Debug.LogError($"[RunNodeArtGenerator] '{kind}' 그림이 만들어지지 않았습니다 — " +
                               "Generate 에 한 줄을 빠뜨렸는지 확인하세요.");
    }

    // ── 공통 바탕 ────────────────────────────────────────────

    /// <summary>하늘 + 지평선 + 바닥. 모든 장면이 이 위에 얹힌다.</summary>
    static void Ground(IconGenerator.P p, string skyTop, string skyLow, string floor)
    {
        p.BgGradient(IconGenerator.Hex(skyTop), IconGenerator.Hex(skyLow));
        p.FillRect(0, 168, Size, Size - 168, IconGenerator.Hex(floor));
        p.RoundedBorder(14, 2, new Color32(0, 0, 0, 96));
    }

    // ── 일반 전투 — 창을 든 대열이 다가온다 ──────────────────

    static void DrawNormalBattle(IconGenerator.P p)
    {
        Ground(p, "2A2036", "5A3A38", "241A22");

        // 뒤로 갈수록 작아지는 창 — 대열처럼 보이게 한다
        for (int i = 0; i < 6; i++)
        {
            int x = 34 + i * 34;
            int h = 92 - Mathf.Abs(i - 3) * 10;
            p.FillRect(x, 168 - h, 5, h, IconGenerator.Hex("C8B9A0"));
            p.FillRect(x - 4, 168 - h - 12, 13, 14, IconGenerator.Hex("E4DCCB"));
        }

        p.FillEllipse(128, 176, 96, 14, new Color32(0, 0, 0, 90));
    }

    // ── 엘리트 전투 — 하나가 앞에 나와 선다 ──────────────────

    static void DrawEliteBattle(IconGenerator.P p)
    {
        Ground(p, "30161C", "7A2530", "26141A");

        // 뒤에 잔챙이, 앞에 큰 실루엣 하나 — "한 놈이 다르다"
        for (int i = 0; i < 4; i++)
            p.FillRRect(30 + i * 52, 108, 20, 60, 8, IconGenerator.Hex("3A2630"));

        p.FillRRect(100, 62, 56, 106, 22, IconGenerator.Hex("1A1016"));
        p.FillRRect(108, 70, 40, 92, 18, IconGenerator.Hex("B03040"));
        p.FillRect(118, 96, 8, 8, IconGenerator.Hex("FFE0A0"));
        p.FillRect(132, 96, 8, 8, IconGenerator.Hex("FFE0A0"));

        p.FillEllipse(128, 176, 90, 14, new Color32(0, 0, 0, 110));
    }

    // ── 야영지 — 천막과 모닥불 ───────────────────────────────

    static void DrawCamp(IconGenerator.P p)
    {
        Ground(p, "141C2E", "2E3A52", "1E2432");

        // 천막 — 삼각형 대신 둥근 사각형 둘로 지붕을 만든다
        p.FillRRect(38, 92, 92, 76, 10, IconGenerator.Hex("6A5A48"));
        p.FillRRect(48, 82, 72, 30, 14, IconGenerator.Hex("8A7358"));
        p.FillRRect(72, 124, 24, 44, 10, IconGenerator.Hex("2A2018"));

        // 모닥불 — 이 장면의 온기
        p.FillEllipse(178, 158, 30, 10, IconGenerator.Hex("3A2A20"));
        p.FillCircleAlpha(178, 140, 20, new Color32(255, 150, 60, 190));
        p.FillCircleAlpha(178, 134, 12, new Color32(255, 220, 140, 230));

        p.FillEllipse(128, 178, 100, 12, new Color32(0, 0, 0, 80));
    }

    // ── 강화소 — 화덕과 모루 ─────────────────────────────────

    static void DrawForge(IconGenerator.P p)
    {
        Ground(p, "1E1410", "4A2A18", "241A14");

        // 화덕 — 아치 입구에서 빛이 샌다
        p.FillRRect(28, 74, 96, 94, 12, IconGenerator.Hex("453026"));
        p.FillRRect(44, 96, 64, 72, 24, IconGenerator.Hex("1A100C"));
        p.FillCircleAlpha(76, 140, 26, new Color32(255, 140, 50, 200));
        p.FillCircleAlpha(76, 140, 14, new Color32(255, 225, 160, 235));

        // 모루
        p.FillRRect(150, 122, 76, 22, 6, IconGenerator.Hex("6E7480"));
        p.FillRect(170, 144, 34, 24, IconGenerator.Hex("565C68"));
        p.FillEllipse(188, 168, 46, 8, IconGenerator.Hex("3A3E48"));

        // 튀는 불티
        p.FillCircleAlpha(206, 108, 4, new Color32(255, 210, 120, 220));
        p.FillCircleAlpha(220, 96, 3, new Color32(255, 180, 90, 180));
    }

    // ── 상점 — 짐수레와 걸린 등불 ────────────────────────────

    static void DrawShop(IconGenerator.P p)
    {
        Ground(p, "141E24", "2E4A52", "1C262C");

        // 차양
        p.FillRRect(34, 78, 158, 26, 10, IconGenerator.Hex("C06A4A"));
        p.FillRRect(34, 96, 158, 12, 4, IconGenerator.Hex("E0A87A"));

        // 좌판
        p.FillRRect(46, 108, 134, 60, 8, IconGenerator.Hex("5A4636"));
        p.FillRect(46, 140, 134, 8, IconGenerator.Hex("3E3024"));

        // 바퀴 — 짐수레라는 표식
        p.FillCircleAlpha(72, 172, 16, new Color32(60, 46, 34, 255));
        p.FillCircleAlpha(160, 172, 16, new Color32(60, 46, 34, 255));

        // 등불
        p.FillCircleAlpha(206, 96, 14, new Color32(255, 205, 120, 190));
        p.FillCircleAlpha(206, 96, 7, new Color32(255, 240, 200, 240));
    }

    // ── 제단 — 마른 피가 앉은 돌 ─────────────────────────────

    static void DrawAltar(IconGenerator.P p)
    {
        Ground(p, "160E1E", "3A1830", "1C1224");

        // 뒤에 선 비석 둘
        p.FillRRect(26, 70, 26, 98, 8, IconGenerator.Hex("3A3040"));
        p.FillRRect(204, 82, 26, 86, 8, IconGenerator.Hex("3A3040"));

        // 제단 — 낮고 넓은 돌
        p.FillRRect(70, 118, 116, 50, 8, IconGenerator.Hex("585062"));
        p.FillRRect(78, 110, 100, 18, 6, IconGenerator.Hex("6E6678"));

        // 마른 피 — 돌 위에서 흘러내린 자국
        p.FillRect(96, 128, 10, 34, IconGenerator.Hex("6A1420"));
        p.FillRect(126, 128, 7, 40, IconGenerator.Hex("58101A"));
        p.FillRect(150, 128, 9, 28, IconGenerator.Hex("6A1420"));

        // 아래에서 새어 나오는 빛
        p.FillCircleAlpha(128, 108, 24, new Color32(190, 60, 220, 120));
        p.FillCircleAlpha(128, 104, 12, new Color32(240, 170, 255, 160));
    }

    // ── 이벤트 — 갈라지는 길 (미구현 자리) ───────────────────

    static void DrawEvent(IconGenerator.P p)
    {
        Ground(p, "16182A", "3A4068", "1E2036");

        // 갈라지는 두 길
        p.FillRRect(96, 120, 64, 48, 6, IconGenerator.Hex("46435E"));
        p.FillRRect(26, 96, 74, 30, 6, IconGenerator.Hex("3E3C54"));
        p.FillRRect(156, 96, 74, 30, 6, IconGenerator.Hex("3E3C54"));

        // 이정표
        p.FillRect(122, 60, 10, 64, IconGenerator.Hex("6A5A44"));
        p.FillRRect(84, 66, 46, 16, 4, IconGenerator.Hex("8A7358"));
        p.FillRRect(126, 88, 46, 16, 4, IconGenerator.Hex("8A7358"));

        p.FillCircleAlpha(128, 44, 10, new Color32(200, 210, 255, 130));
    }
}
