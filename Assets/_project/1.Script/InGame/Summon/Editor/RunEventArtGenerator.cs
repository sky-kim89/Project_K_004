using System;
using UnityEditor;
using UnityEngine;

// ============================================================
//  RunEventArtGenerator.cs  [Editor Only]
//  Tools > Project K > 아이콘·텍스처 > 이벤트 그림
//  이벤트 타이틀 그림 15장을 256×256 PNG 로 만든다.
//    → Assets/_project/3.Textures/Icons/RunEvents/event_<Id>.png
//
//  ■ ⚠ 더미다 — 자리를 잡아 두는 것이 전부다
//    RunNodeArtGenerator 와 같은 계약이다. 화면(FacilityPopup 배경)이 그림을
//    전제로 짜여 있어서, 비워 두면 열다섯 이벤트가 전부 같은 갈림길 그림을
//    쓰게 되고 "어디에 들렀는지" 가 화면에 남지 않는다.
//    손그림이 오면 **같은 경로·같은 파일명**으로 덮으면 코드는 그대로 돈다.
//
//  ■ 갈림길 그림(node_Event)과 다른 물건이다
//    그쪽은 "이벤트라는 칸" 하나, 이쪽은 "어느 이벤트인가" 열다섯이다.
//    폴더도 따로다 (RunEventArtAssets 머리 주석 참고).
//
//  ■ 아이콘이 아니라 '장면' 이다
//    시설 화면 배경으로 화면 전체에 깔린다 — 실루엣 하나가 아니라
//    하늘·바닥·주인공이 있는 한 장면이어야 크게 늘려도 견딘다.
//    RunNodeArtGenerator 의 Ground() 와 같은 짜임을 쓴다.
//
//  ■ ⚠ 색으로 무게를 말한다
//    가벼운 판(체력 −3/−4)은 푸른 밤, 무거운 판(−5)은 붉은 기가 돈다.
//    대가가 체력이 아닌 셋은 보랏빛이다 — 화면만 보고도 "이번엔 다른 걸
//    묻는다" 가 읽혀야 한다.
// ============================================================

public static class RunEventArtGenerator
{
    const int Size = 256;

    /// <summary>지평선 y. 모든 장면이 이 선을 공유한다 — 열다섯이 한 세계로 읽힌다.</summary>
    const int Horizon = 168;

    [MenuItem(ProjectKMenu.Icon + "이벤트 그림", priority = ProjectKMenu.IconPrio + 10)]
    public static void Generate()
    {
        string dir = RunEventArtAssets.Dir.TrimEnd('/');
        IconGenerator.EnsureDir(dir);

        Save(RunEventId.MinerShaft,    DrawMinerShaft);
        Save(RunEventId.LostLibrary,   DrawLostLibrary);
        Save(RunEventId.WanderingSoul, DrawWanderingSoul);
        Save(RunEventId.StrayMonster,  DrawStrayMonster);
        Save(RunEventId.OfferingMark,  DrawOfferingMark);
        Save(RunEventId.BrokenCircle,  DrawBrokenCircle);
        Save(RunEventId.SupplyCart,    DrawSupplyCart);
        Save(RunEventId.RoamingSmith,  DrawRoamingSmith);
        Save(RunEventId.SealedStaff,   DrawSealedStaff);
        Save(RunEventId.HeroGraves,    DrawHeroGraves);
        Save(RunEventId.BloodMoon,     DrawBloodMoon);
        Save(RunEventId.BrokenPens,    DrawBrokenPens);
        Save(RunEventId.BrokenNest,    DrawBrokenNest);
        Save(RunEventId.HungryIdol,    DrawHungryIdol);
        Save(RunEventId.InvertedGlass, DrawInvertedGlass);

        AssetDatabase.Refresh();
        IconGenerator.ApplySpriteImportSettings(dir, Size);
        AssetDatabase.SaveAssets();

        Verify();

        Debug.Log($"[RunEventArtGenerator] 이벤트 그림 {RunEventRule.Count}장 생성 완료 → {dir}\n" +
                  "⚠ 그다음 '프리팹 생성 > 팝업 > 시설' 을 다시 구워야 화면에 뜹니다 " +
                  "(FacilityPopup 이 그림을 프리팹에 물고 있습니다).");
    }

    static void Save(RunEventId id, Action<IconGenerator.P> draw)
        => IconGenerator.Save(Size, Size, RunEventArtAssets.PathOf(id), draw);

    /// <summary>
    /// 표에 있는 이벤트가 전부 구워졌는지 본다.
    ///
    /// ⚠ 이 검사가 없으면 실패가 조용하다 — 이벤트를 하나 추가하고 Generate 에
    ///   줄을 빠뜨리면, 화면에서는 그 이벤트만 그림이 비거나(더 나쁘게는)
    ///   프리팹을 굽는 쪽이 통째로 멈춘다.
    /// </summary>
    static void Verify()
    {
        for (int i = 0; i < RunEventRule.Count; i++)
        {
            var id = (RunEventId)i;

            if (AssetDatabase.LoadAssetAtPath<Sprite>(RunEventArtAssets.PathOf(id)) == null)
                Debug.LogError($"[RunEventArtGenerator] '{id}' 그림이 만들어지지 않았습니다 — " +
                               "Generate 에 한 줄을 빠뜨렸는지 확인하세요.");
        }
    }

    // ── 공통 바탕 ────────────────────────────────────────────

    /// <summary>하늘 + 지평선 + 바닥. 모든 장면이 이 위에 얹힌다.</summary>
    static void Ground(IconGenerator.P p, string skyTop, string skyLow, string floor)
    {
        p.BgGradient(IconGenerator.Hex(skyTop), IconGenerator.Hex(skyLow));
        p.FillRect(0, Horizon, Size, Size - Horizon, IconGenerator.Hex(floor));
        p.RoundedBorder(14, 2, new Color32(0, 0, 0, 96));
    }

    /// <summary>바닥에 깔리는 그림자. 물체가 떠 보이지 않게 한다.</summary>
    static void Shade(IconGenerator.P p, int cx, int rx = 70, byte a = 100)
        => p.FillEllipse(cx, Horizon + 8, rx, 13, new Color32(0, 0, 0, a));

    /// <summary>달·불빛처럼 뒤에서 번지는 빛.</summary>
    static void Glow(IconGenerator.P p, int cx, int cy, int r, Color32 c)
        => p.FillCircleGrad(cx, cy, r, c, new Color32(c.r, c.g, c.b, 70),
                            new Color32(c.r, c.g, c.b, 0));

    // ══════════════════════════════════════════════════════════
    //  가벼운 판 — 푸른 밤
    // ══════════════════════════════════════════════════════════

    // ── 광부의 갱도 — 비탈에 뚫린 검은 입구 ──────────────────

    static void DrawMinerShaft(IconGenerator.P p)
    {
        Ground(p, "141A24", "3A4450", "20242C");

        // 비탈
        p.FillTri(0, Horizon, 96, 54, 200, Horizon, IconGenerator.Hex("2E3540"));
        p.FillTri(60, Horizon, 168, 76, 256, Horizon, IconGenerator.Hex("262C36"));

        // 갱도 입구 — 안쪽이 완전히 검다
        p.FillRRect(96, 104, 64, 64, 26, IconGenerator.Hex("0A0C10"));

        // 갱목
        p.FillRect(90, 100, 10, 68, IconGenerator.Hex("6A5238"));
        p.FillRect(156, 100, 10, 68, IconGenerator.Hex("6A5238"));
        p.FillRect(86, 96, 84, 10, IconGenerator.Hex("7C6142"));

        // 손수레 레일
        p.FillRect(112, 168, 6, 40, IconGenerator.Hex("4A4A52"));
        p.FillRect(138, 168, 6, 40, IconGenerator.Hex("4A4A52"));

        Glow(p, 128, 140, 22, new Color32(255, 200, 110, 90));
        Shade(p, 128, 78, 110);
    }

    // ── 잊힌 서고 — 늘어선 서가 ──────────────────────────────

    static void DrawLostLibrary(IconGenerator.P p)
    {
        Ground(p, "16141F", "3C3450", "22202C");

        // 서가 셋 — 뒤로 갈수록 어둡다
        Shelf(p, 18, 64, 62, "2A2438", "6E5A8C");
        Shelf(p, 96, 44, 64, "342C46", "8A72AC");
        Shelf(p, 176, 70, 62, "2A2438", "6E5A8C");

        // 떠 있는 책 한 권 — 이 장면의 주인공
        p.FillRRect(112, 92, 34, 24, 4, IconGenerator.Hex("D8CBB0"));
        p.FillRect(128, 92, 4, 24, IconGenerator.Hex("8A7A5E"));
        Glow(p, 129, 104, 30, new Color32(190, 200, 255, 110));

        Shade(p, 128, 84, 90);
    }

    static void Shelf(IconGenerator.P p, int x, int top, int w, string frame, string books)
    {
        p.FillRect(x, top, w, Horizon - top, IconGenerator.Hex(frame));

        for (int r = 0; r < 3; r++)
        {
            int y = top + 10 + r * 34;
            if (y + 22 > Horizon) break;

            for (int b = 0; b < 5; b++)
                p.FillRect(x + 6 + b * 10, y, 7, 22 - (b % 3) * 3, IconGenerator.Hex(books));
        }
    }

    // ── 떠도는 혼 — 성벽 위를 도는 푸른 것 ───────────────────

    static void DrawWanderingSoul(IconGenerator.P p)
    {
        Ground(p, "0E1620", "2A3E52", "1A2028");

        // 성벽 흉벽
        p.FillRect(0, 126, Size, Horizon - 126, IconGenerator.Hex("343C46"));
        for (int i = 0; i < 8; i++)
            p.FillRect(4 + i * 32, 108, 20, 20, IconGenerator.Hex("3E4650"));

        // 혼 — 머리 + 흐르는 꼬리
        Glow(p, 128, 78, 46, new Color32(120, 200, 255, 120));
        p.FillCircle(128, 70, 20, IconGenerator.Hex("BFE6FF"));
        p.FillEllipse(128, 104, 14, 26, IconGenerator.Hex("8FC8EE"));
        p.FillEllipse(128, 128, 8, 16, new Color32(143, 200, 238, 140));

        // 눈
        p.FillRect(120, 66, 5, 8, IconGenerator.Hex("18344A"));
        p.FillRect(132, 66, 5, 8, IconGenerator.Hex("18344A"));
    }

    // ── 길 잃은 몬스터 — 성문 앞에 웅크린 것 ─────────────────

    static void DrawStrayMonster(IconGenerator.P p)
    {
        Ground(p, "141A28", "3A4460", "1E2430");

        // 성문
        p.FillRect(64, 40, 128, Horizon - 40, IconGenerator.Hex("2A2E3A"));
        p.FillRRect(84, 66, 88, 102, 40, IconGenerator.Hex("14161E"));

        // 웅크린 몸
        p.FillEllipse(128, 148, 42, 26, IconGenerator.Hex("4C6A46"));
        p.FillCircle(128, 122, 22, IconGenerator.Hex("5A7C52"));

        // 귀
        p.FillTri(110, 108, 104, 84, 122, 100, IconGenerator.Hex("5A7C52"));
        p.FillTri(146, 108, 152, 84, 134, 100, IconGenerator.Hex("5A7C52"));

        // 눈
        p.FillRect(118, 118, 6, 7, IconGenerator.Hex("FFD86A"));
        p.FillRect(134, 118, 6, 7, IconGenerator.Hex("FFD86A"));

        Shade(p, 128, 60, 110);
    }

    // ── 제물의 흔적 — 바닥에 그려진 표식 ─────────────────────

    static void DrawOfferingMark(IconGenerator.P p)
    {
        Ground(p, "1A1220", "48304A", "241A26");

        // 바닥 원 — 위에서 내려다본 것처럼 납작하다
        p.FillEllipse(128, 176, 96, 44, IconGenerator.Hex("30203A"));
        p.FillEllipse(128, 176, 78, 34, IconGenerator.Hex("281A32"));

        // 표식 — 삼각형과 중심
        Color32 mark = IconGenerator.Hex("C4506A");
        p.DrawLine(128, 142, 60, 200, mark, 3);
        p.DrawLine(128, 142, 196, 200, mark, 3);
        p.DrawLine(60, 200, 196, 200, mark, 3);
        p.FillCircle(128, 178, 10, mark);

        // 다 타 버린 초 셋
        Candle(p, 74, 152);
        Candle(p, 128, 126);
        Candle(p, 182, 152);
    }

    static void Candle(IconGenerator.P p, int x, int y)
    {
        p.FillRect(x - 4, y, 8, 22, IconGenerator.Hex("C8BCA4"));
        Glow(p, x, y - 6, 12, new Color32(255, 190, 120, 130));
        p.FillCircle(x, y - 6, 4, IconGenerator.Hex("FFE0A0"));
    }

    // ── 깨진 소환진 — 금 간 마법진 ───────────────────────────

    static void DrawBrokenCircle(IconGenerator.P p)
    {
        Ground(p, "101A22", "284050", "18202A");

        Glow(p, 128, 176, 92, new Color32(90, 200, 190, 80));

        // 겹으로 도는 고리
        p.DrawCircle(128, 176, 84, 3, IconGenerator.Hex("4FD2C0"));
        p.DrawCircle(128, 176, 62, 2, IconGenerator.Hex("3AA396"));
        p.DrawCircle(128, 176, 34, 2, IconGenerator.Hex("4FD2C0"));

        // 깨진 자리 — 고리를 지워 낸다
        p.FillTri(150, 120, 236, 176, 150, 232, IconGenerator.Hex("18202A"));

        // 갈라진 금
        p.DrawLine(128, 176, 244, 150, IconGenerator.Hex("0C1218"), 3);
        p.DrawLine(160, 176, 236, 210, IconGenerator.Hex("0C1218"), 2);

        p.FillCircle(128, 176, 10, IconGenerator.Hex("BFF6EC"));
    }

    // ── 버려진 보급 수레 — 옆으로 넘어진 수레 ────────────────

    static void DrawSupplyCart(IconGenerator.P p)
    {
        Ground(p, "16182A", "3A4058", "20222E");

        // 짐칸 — 기울어 보이게 두 겹으로 어긋나게 쌓는다
        p.FillRRect(56, 112, 120, 52, 6, IconGenerator.Hex("6A5238"));
        p.FillRRect(64, 104, 104, 20, 4, IconGenerator.Hex("7E6242"));

        // 바퀴 하나는 빠져 굴러가 있다
        p.DrawCircle(84, 168, 22, 6, IconGenerator.Hex("5A4630"));
        p.DrawCircle(206, 186, 16, 5, IconGenerator.Hex("5A4630"));

        // 흘러나온 짐
        p.FillCircle(150, 182, 11, IconGenerator.Hex("9A7C50"));
        p.FillCircle(172, 190, 8, IconGenerator.Hex("8A6E46"));

        Shade(p, 120, 84, 100);
    }

    // ── 떠돌이 대장장이 — 화덕 없이 망치만 ───────────────────

    static void DrawRoamingSmith(IconGenerator.P p)
    {
        Ground(p, "1A1418", "4A3830", "241C1E");

        // 모루
        p.FillRRect(84, 128, 88, 26, 6, IconGenerator.Hex("3E4048"));
        p.FillRect(108, 154, 40, 14, IconGenerator.Hex("2E3038"));

        // 앉은 사람 — 등을 보이고 있다
        p.FillEllipse(66, 140, 26, 34, IconGenerator.Hex("2A2430"));
        p.FillCircle(66, 100, 18, IconGenerator.Hex("3A3242"));

        // 망치
        p.DrawLine(96, 116, 150, 74, IconGenerator.Hex("7A5E3E"), 5);
        p.FillRRect(140, 58, 34, 22, 4, IconGenerator.Hex("50525C"));

        // 불티
        Glow(p, 128, 128, 26, new Color32(255, 150, 70, 110));
        p.FillCircle(120, 122, 3, IconGenerator.Hex("FFD08A"));
        p.FillCircle(140, 116, 2, IconGenerator.Hex("FFD08A"));

        Shade(p, 118, 76, 100);
    }

    // ══════════════════════════════════════════════════════════
    //  무거운 판 — 붉은 기가 돈다
    // ══════════════════════════════════════════════════════════

    // ── 봉인된 지팡이 — 돌무더기에 박힌 것 ───────────────────

    static void DrawSealedStaff(IconGenerator.P p)
    {
        Ground(p, "1E1220", "56304A", "281A28");

        // 돌무더기
        p.FillEllipse(128, 178, 74, 30, IconGenerator.Hex("3A2E3E"));
        p.FillCircle(96, 160, 20, IconGenerator.Hex("453648"));
        p.FillCircle(156, 164, 17, IconGenerator.Hex("3E3040"));
        p.FillCircle(126, 150, 22, IconGenerator.Hex("4C3A50"));

        // 지팡이
        p.DrawLine(128, 150, 138, 44, IconGenerator.Hex("6E4E38"), 7);

        // 머리의 보석 — 봉인
        Glow(p, 139, 40, 30, new Color32(200, 120, 255, 130));
        p.FillCircle(139, 38, 13, IconGenerator.Hex("C88AFF"));
        p.DrawCircle(139, 38, 19, 2, IconGenerator.Hex("7A4AA8"));

        Shade(p, 128, 76, 110);
    }

    // ── 용사들의 무덤 — 비석 없는 흙더미 ─────────────────────

    static void DrawHeroGraves(IconGenerator.P p)
    {
        Ground(p, "1C1418", "4E2C2C", "251A1C");

        // 흙더미 넷
        for (int i = 0; i < 4; i++)
        {
            int cx = 40 + i * 60;
            int r  = 24 - (i % 2) * 5;
            p.FillEllipse(cx, Horizon - 4, r + 10, r, IconGenerator.Hex("3A2A28"));
        }

        // 꽂힌 검 — 비석 대신이다
        Sword(p, 76, 82);
        Sword(p, 152, 66);
        Sword(p, 208, 92);

        Glow(p, 128, 60, 60, new Color32(200, 90, 80, 60));
    }

    static void Sword(IconGenerator.P p, int x, int top)
    {
        p.FillRect(x - 3, top, 6, Horizon - top, IconGenerator.Hex("9AA0AC"));
        p.FillRect(x - 14, top + 14, 28, 6, IconGenerator.Hex("6E5A42"));
        p.FillCircle(x, top + 4, 5, IconGenerator.Hex("7E6448"));
    }

    // ── 핏빛 달 — 붉은 달과 우는 것들 ────────────────────────

    static void DrawBloodMoon(IconGenerator.P p)
    {
        Ground(p, "200E12", "6A2028", "281216");

        // 달
        Glow(p, 128, 76, 74, new Color32(230, 60, 60, 120));
        p.FillCircle(128, 76, 44, IconGenerator.Hex("E24A4A"));
        p.FillCircle(112, 64, 9, new Color32(160, 40, 40, 120));
        p.FillCircle(142, 88, 6, new Color32(160, 40, 40, 110));

        // 고개 든 실루엣 셋
        Howler(p, 52, 22);
        Howler(p, 128, 28);
        Howler(p, 206, 20);

        Shade(p, 128, 96, 120);
    }

    static void Howler(IconGenerator.P p, int cx, int r)
    {
        p.FillEllipse(cx, Horizon - 4, r, r - 4, IconGenerator.Hex("140A0C"));
        p.FillCircle(cx + r - 6, Horizon - r - 6, r - 10, IconGenerator.Hex("140A0C"));
        p.FillTri(cx + r - 12, Horizon - r - 12, cx + r + 4, Horizon - r - 24,
                  cx + r + 2, Horizon - r - 6, IconGenerator.Hex("140A0C"));
    }

    // ── 버려진 사육장 — 부서진 우리 ──────────────────────────

    static void DrawBrokenPens(IconGenerator.P p)
    {
        Ground(p, "1A1418", "4A3A2E", "241E1C");

        // 우리 둘 — 하나는 문이 뜯겨 있다
        Cage(p, 20, 92, 100, broken: false);
        Cage(p, 140, 104, 96, broken: true);

        // 겹겹이 남은 발자국
        for (int i = 0; i < 7; i++)
            p.FillEllipse(24 + i * 34, 196 + (i % 2) * 10, 8, 5,
                          new Color32(0, 0, 0, 110));

        Shade(p, 128, 100, 70);
    }

    static void Cage(IconGenerator.P p, int x, int top, int w, bool broken)
    {
        p.FillRect(x, top, w, 6, IconGenerator.Hex("5E4A34"));
        p.FillRect(x, Horizon - 6, w, 6, IconGenerator.Hex("5E4A34"));

        int bars = w / 16;
        for (int i = 0; i <= bars; i++)
        {
            // 뜯긴 우리는 가운데 창살이 없다
            if (broken && i > 1 && i < bars - 1) continue;

            p.FillRect(x + i * 16, top, 5, Horizon - top, IconGenerator.Hex("6E5840"));
        }

        if (broken) p.DrawLine(x + 20, Horizon - 12, x + w - 24, top + 30,
                               IconGenerator.Hex("40342A"), 4);
    }

    // ══════════════════════════════════════════════════════════
    //  대가가 체력이 아닌 판 — 보랏빛
    // ══════════════════════════════════════════════════════════

    // ── 버려진 둥지 — 나란한 우리 셋 ─────────────────────────

    static void DrawBrokenNest(IconGenerator.P p)
    {
        Ground(p, "1A1428", "463A72", "241E34");

        // 둥지 셋 — 셋이라는 것이 이 장면의 전부다
        Nest(p, 62, 150, 34);
        Nest(p, 128, 138, 38);
        Nest(p, 194, 150, 34);

        Glow(p, 128, 132, 54, new Color32(180, 150, 255, 80));
        Shade(p, 128, 100, 80);
    }

    static void Nest(IconGenerator.P p, int cx, int cy, int r)
    {
        p.FillEllipse(cx, cy + 8, r, r / 2 + 4, IconGenerator.Hex("5A4630"));
        p.FillEllipse(cx, cy + 4, r - 8, r / 2 - 2, IconGenerator.Hex("2E2418"));

        // 알 — 아직 온기가 남았다
        Glow(p, cx, cy - 2, 16, new Color32(255, 230, 190, 90));
        p.FillEllipse(cx, cy - 2, 11, 14, IconGenerator.Hex("EDE0C4"));
    }

    // ── 굶주린 우상 — 입을 벌린 돌덩이 ───────────────────────

    static void DrawHungryIdol(IconGenerator.P p)
    {
        Ground(p, "1C1424", "4A3660", "26202E");

        // 몸통
        p.FillRRect(74, 46, 108, 122, 14, IconGenerator.Hex("4A4050"));
        p.FillRRect(86, 58, 84, 98, 10, IconGenerator.Hex("564A5E"));

        // 눈
        p.FillRect(102, 78, 16, 12, IconGenerator.Hex("120E16"));
        p.FillRect(138, 78, 16, 12, IconGenerator.Hex("120E16"));

        // 벌린 입 — 안이 완전히 검다
        p.FillRRect(100, 106, 56, 44, 10, IconGenerator.Hex("0A080E"));
        for (int i = 0; i < 4; i++)
            p.FillTri(104 + i * 14, 106, 111 + i * 14, 120, 118 + i * 14, 106,
                      IconGenerator.Hex("D8CEC0"));

        Glow(p, 128, 130, 26, new Color32(190, 90, 255, 90));

        // 발치의 제물
        p.FillRRect(60, 152, 24, 16, 3, IconGenerator.Hex("7A6448"));
        Shade(p, 128, 84, 110);
    }

    // ── 뒤집힌 모래시계 — 모래가 위로 흐른다 ─────────────────

    static void DrawInvertedGlass(IconGenerator.P p)
    {
        Ground(p, "16142C", "3E3A70", "201E34");

        // 틀
        p.FillRRect(76, 44, 104, 10, 3, IconGenerator.Hex("6E5A3E"));
        p.FillRRect(76, 158, 104, 10, 3, IconGenerator.Hex("6E5A3E"));
        p.FillRect(84, 54, 6, 104, IconGenerator.Hex("5E4C34"));
        p.FillRect(166, 54, 6, 104, IconGenerator.Hex("5E4C34"));

        // 유리 — 위아래 삼각형이 가운데서 만난다
        p.FillTri(96, 56, 160, 56, 128, 106, IconGenerator.Hex("2E2C4C"));
        p.FillTri(96, 156, 160, 156, 128, 106, IconGenerator.Hex("2E2C4C"));

        // ⚠ 모래는 **위쪽**에 쌓인다 — 그게 이 장면의 전부다
        p.FillTri(100, 60, 156, 60, 128, 96, IconGenerator.Hex("E6C888"));
        p.FillTri(112, 150, 144, 150, 128, 122, new Color32(230, 200, 136, 120));

        // 거꾸로 오르는 줄기
        p.DrawLine(128, 122, 128, 100, IconGenerator.Hex("E6C888"), 3);

        Glow(p, 128, 106, 34, new Color32(190, 160, 255, 90));
        Shade(p, 128, 70, 90);
    }
}
