// ============================================================
//  ItemIconGenerator.cs  [Editor Only]
//  Tools > Project K > 아이콘·텍스처 > 아이템 아이콘
//
//  eItem 열거형 항목별 PNG 아이콘 생성.
//  저장 경로: Assets/_project/3.Textures/Icons/Items/
//
//  ■ 해상도
//    그리기 코드는 48 격자 좌표를 그대로 쓰지만, Painter 가 P.S 배(=4)
//    버퍼에 그려 실제 출력은 192×192 다. 확대가 아니라 원·선을 그 해상도에서
//    다시 계산하므로 경계가 매끄럽다 (픽셀당 3×3 서브샘플 AA).
//    보상 카드가 128px 로 띄우기 때문에 48px 원본은 뭉개져 보였다.
//
//  ■ 새 아이콘 추가
//    Draw* 함수를 만들고 GenerateAll() 에 Save() 한 줄 추가.
//    좌표는 계속 0~47 기준으로 잡으면 된다.
// ============================================================
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ItemIconGenerator
{
    const string ITEM_PATH = "Assets/_project/3.Textures/Icons/Items";

    // ── 팔레트 ────────────────────────────────────────────────
    static Color32 H(string h)
    {
        h = h.TrimStart('#');
        return new Color32(
            Convert.ToByte(h.Substring(0, 2), 16),
            Convert.ToByte(h.Substring(2, 2), 16),
            Convert.ToByte(h.Substring(4, 2), 16), 255);
    }
    static Color32 HA(string h, byte a)
    {
        var c = H(h); c.a = a; return c;
    }
    static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

    // ═══════════════════════════════════════════════════════
    //  진입점
    // ═══════════════════════════════════════════════════════

    [MenuItem(ProjectKMenu.Icon + "아이템 아이콘", priority = ProjectKMenu.IconPrio + 13)]
    public static void GenerateAll()
    {
        EnsureDir(ITEM_PATH);

        Save("item_gold.png",                 DrawGold);

        // ⚠ 마력의 정수 — 상점이 파는 최대 마나 증가 (RunShopRule.EssenceIconKey)
        //   Items 폴더에 두는 이유는 아이템 아틀라스가 이미 이 폴더를 물고 있어서다.
        //   따로 폴더를 만들면 SpriteManagerCreator 에도 줄을 더해야 하고,
        //   그 줄을 빠뜨리면 **에러 없이 그림만 사라진다.**
        Save("item_mana_essence.png",         DrawManaEssence);

        // ⚠ 상점 상시 판매 (2026-09-13) — RunShopRule.WarFundIconKey
        //   정수와 같은 폴더에 둔다 (아이템 아틀라스가 이미 물고 있다).
        Save("item_war_fund.png",             DrawWarFund);
        Save("item_war_drum.png",             DrawWarDrum);   // RunShopRule.DrumIconKey (2026-09-15)
        Save("item_mana_potion.png",          DrawManaPotion); // RunShopRule.PotionIconKey (2026-09-16)
        Save("item_gem.png",                  DrawGem);
        Save("item_energy.png",               DrawEnergy);
        Save("item_stamina.png",              DrawStamina);
        Save("item_honor.png",                DrawHonor);
        Save("item_expbook.png",              DrawExpBook);
        Save("item_battlestone.png",          DrawBattleStone);
        Save("item_skillscroll.png",          DrawSkillScroll);
        Save("item_general_upgrade_stone.png",DrawGeneralUpgradeStone);
        Save("item_equip_upgrade_stone.png",  DrawEquipUpgradeStone);
        Save("item_equipbox.png",             DrawEquipBox);
        Save("item_soldier_shard.png",        DrawSoldierShard);
        Save("item_reincarnation_point.png",  DrawReincarnationPoint);

        AssetDatabase.Refresh();
        ApplySpriteImport(ITEM_PATH);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ItemIconGenerator] 아이템 아이콘 13종 생성 완료 ({48 * P.S}×{48 * P.S}).");
    }

    // ═══════════════════════════════════════════════════════
    //  ■ 아이콘 드로잉
    // ═══════════════════════════════════════════════════════

    // ── Gold (금화) ───────────────────────────────────────
    static void DrawGold(P p)
    {
        p.BgGrad(H("1A0E00"), H("3D2200"));
        p.RoundedBorder(10, 2, H("8B6000"));
        // 바깥 금테
        p.FillCircle(24, 24, 16, H("C8920A"));
        p.FillCircle(24, 24, 14, H("F0B800"));
        // 안쪽 릴리프 (동심원)
        p.DrawCircle(24, 24, 11, 2, H("D4A000"));
        // 코인 면
        p.FillCircle(24, 24, 9, H("E8CC44"));
        // "G" 각인 느낌 (수평+수직 줄)
        p.FillRect(20, 22, 8, 4, H("C8920A"));
        p.FillRect(24, 20, 4, 8, H("C8920A"));
        p.FillCircle(24, 24, 3, H("E8CC44"));
        // 광택
        p.FillCircleAlpha(20, 18, 4, HA("FFFFFF", 90));
        p.FillCircle(20, 18, 2, HA("FFFFFF", 160));
    }

    // ── Gem (보석) ────────────────────────────────────────
    static void DrawGem(P p)
    {
        p.BgGrad(H("050826"), H("0D1A50"));
        p.RoundedBorder(10, 2, H("2244AA"));
        // 다이아몬드 형태 (팔각형 근사)
        int cx = 24, cy = 26;
        // 다이아몬드 채우기
        FillDiamond(p, cx, cy, 16, 12, H("2266DD"));
        // 면 하이라이트
        FillDiamond(p, cx, cy - 2, 14, 6, H("4488FF"));
        p.FillTri(cx - 8, cy - 6, cx + 8, cy - 6, cx, cy - 14, H("88BBFF"));
        // 상단 크라운 면
        p.FillTri(cx - 10, cy - 6, cx - 6, cy - 14, cx, cy - 10, HA("AACCFF", 180));
        p.FillTri(cx + 10, cy - 6, cx + 6, cy - 14, cx, cy - 10, HA("AACCFF", 120));
        // 광택
        p.FillCircleAlpha(cx - 4, cy - 10, 3, HA("FFFFFF", 200));
        p.BlendPixel(cx - 3, cy - 11, HA("FFFFFF", 255));
    }

    // ── Energy (에너지) ──────────────────────────────────
    static void DrawEnergy(P p)
    {
        p.BgGrad(H("080520"), H("1A0C44"));
        p.RoundedBorder(10, 2, H("5533AA"));
        // 번개 글로우
        p.FillCircleAlpha(24, 24, 18, HA("FFAA00", 35));
        p.FillCircleAlpha(24, 24, 12, HA("FFCC44", 45));
        // 번개 형태 (지그재그)
        // 상단 → 중앙 → 하단
        int[] bx = { 28, 22, 30, 18, 26 };
        int[] by = { 8,  20, 26, 36, 42 };
        for (int i = 0; i < bx.Length - 1; i++)
            p.DrawLine(bx[i], by[i], bx[i + 1], by[i + 1], H("FFCC44"), 4);
        for (int i = 0; i < bx.Length - 1; i++)
            p.DrawLine(bx[i], by[i], bx[i + 1], by[i + 1], H("FFF0AA"), 2);
        // 스파크 파티클
        p.FillCircle(32, 14, 2, H("FFAA22"));
        p.FillCircle(16, 30, 2, H("FFAA22"));
        p.FillCircle(34, 30, 2, HA("FFCC44", 150));
    }

    // ── Stamina (스태미나) ────────────────────────────────
    static void DrawStamina(P p)
    {
        p.BgGrad(H("02100E"), H("063028"));
        p.RoundedBorder(10, 2, H("1A7A5A"));
        // 물방울 (하단 원 + 상단 삼각)
        int cx = 24, bot = 36;
        p.FillCircle(cx, bot, 11, H("1A88CC"));
        p.FillCircle(cx, bot, 9,  H("33AAEE"));
        // 물방울 상단 뾰족 부분
        p.FillTri(cx - 7, bot - 8, cx + 7, bot - 8, cx, 10, H("1A88CC"));
        p.FillTri(cx - 5, bot - 8, cx + 5, bot - 8, cx, 13, H("33AAEE"));
        // 광택 (왼쪽 하이라이트)
        p.FillCircleAlpha(cx - 4, bot - 2, 4, HA("AADDFF", 180));
        p.FillCircle(cx - 4, bot - 2, 2,      HA("FFFFFF", 200));
        // 물결 라인
        p.DrawLine(cx - 4, bot + 3, cx + 4, bot + 3, HA("FFFFFF", 60), 1);
    }

    // ── Honor (명예) ─────────────────────────────────────
    static void DrawHonor(P p)
    {
        p.BgGrad(H("0C0520"), H("200A44"));
        p.RoundedBorder(10, 2, H("5522AA"));
        // 방패 형태
        FillShield(p, 24, 10, 22, 28, H("3A1A66"));
        DrawShieldOutline(p, 24, 10, 22, 28, H("AA55FF"), 2);
        FillShield(p, 24, 12, 18, 24, H("4A2288"));
        // 별
        DrawStar(p, 24, 26, 10, 5, H("FFD700"), H("FFA500"));
        // 상단 월계관 느낌
        p.FillCircle(24, 10, 3, H("AA55FF"));
        p.FillCircle(24, 10, 1, H("FFFFFF"));
    }

    // ── ExpBook (경험서) ──────────────────────────────────
    static void DrawExpBook(P p)
    {
        p.BgGrad(H("120A04"), H("2A1608"));
        p.RoundedBorder(10, 2, H("664422"));
        // 책 표지 (약간 열린 상태)
        // 왼쪽 표지
        p.FillRRect(8,  10, 16, 30, 2, H("5A2E0A"));
        p.FillRRect(10, 12, 12, 26, 1, H("7A4020"));
        // 오른쪽 표지
        p.FillRRect(26, 10, 14, 30, 2, H("5A2E0A"));
        // 내지 (오른쪽)
        p.FillRect(27, 12, 11, 26, H("F0E8D0"));
        // 줄 (텍스트 느낌)
        for (int y = 15; y <= 35; y += 4)
            p.DrawLine(29, y, 36, y, HA("886644", 100), 1);
        // 책 등 (중앙)
        p.FillRect(23, 10, 4, 30, H("3A1A08"));
        p.DrawLine(24, 10, 24, 40, H("8B6040"), 1);
        // 금 장식 테두리
        p.DrawLine(9, 10, 9, 40, HA("CC9944", 120), 1);
        // 글로우
        p.FillCircleAlpha(28, 24, 8, HA("FFCC88", 30));
        // 경험치 느낌 (별 파티클)
        p.FillCircle(34, 16, 2, H("FFDD44"));
        p.FillCircle(32, 36, 2, HA("FFDD44", 180));
    }

    // ── BattleStone (전투석) ─────────────────────────────
    static void DrawBattleStone(P p)
    {
        p.BgGrad(H("050A0E"), H("0E1E2A"));
        p.RoundedBorder(10, 2, H("225577"));
        // 결정 형태 (6각형 기둥)
        int cx = 24, cy = 26;
        // 기둥 몸체
        p.FillRect(cx - 7, cy - 14, 14, 22, H("2255AA"));
        // 상단 육각 뾰족
        p.FillTri(cx - 7, cy - 14, cx + 7, cy - 14, cx, cy - 22, H("4477CC"));
        // 하단 육각 뾰족
        p.FillTri(cx - 7, cy + 8, cx + 7, cy + 8, cx, cy + 16, H("1A3A7A"));
        // 면 하이라이트 (왼쪽 밝음)
        p.FillRect(cx - 7, cy - 14, 4, 22, HA("88BBFF", 100));
        p.FillTri(cx - 7, cy - 14, cx - 3, cy - 14, cx - 7, cy - 10, HA("88BBFF", 80));
        // 내부 광택
        p.FillRect(cx - 3, cy - 12, 2, 18, HA("FFFFFF", 60));
        p.BlendPixel(cx - 2, cy - 6, HA("FFFFFF", 180));
    }

    // ── SkillScroll (스킬 서적) ───────────────────────────
    static void DrawSkillScroll(P p)
    {
        p.BgGrad(H("120A02"), H("2E1804"));
        p.RoundedBorder(10, 2, H("774422"));
        // 두루마리 몸통 (양쪽 말린 부분)
        p.FillRRect(10, 16, 28, 18, 2, H("D4B878"));
        // 두루마리 내지
        p.FillRect(12, 17, 24, 16, H("EED898"));
        // 텍스트 줄
        p.DrawLine(15, 20, 33, 20, HA("886644", 150), 1);
        p.DrawLine(15, 24, 33, 24, HA("886644", 130), 1);
        p.DrawLine(15, 28, 28, 28, HA("886644", 110), 1);
        // 왼쪽 말린 봉
        p.FillRRect(7,  14, 6, 22, 3, H("8B5522"));
        p.DrawLine(8, 14, 8, 36, H("AA7744"), 1);
        // 오른쪽 말린 봉
        p.FillRRect(35, 14, 6, 22, 3, H("8B5522"));
        p.DrawLine(36, 14, 36, 36, H("AA7744"), 1);
        // 상단/하단 봉 끝 장식
        p.FillCircle(10, 14, 4, H("AA7744"));
        p.FillCircle(10, 36, 4, H("AA7744"));
        p.FillCircle(38, 14, 4, H("AA7744"));
        p.FillCircle(38, 36, 4, H("AA7744"));
        // 마법 룬 느낌 (중앙)
        p.FillCircle(24, 24, 3, HA("CC88FF", 180));
        p.FillCircle(24, 24, 1, H("FFFFFF"));
    }

    // ── GeneralUpgradeStone (장군 강화석) ────────────────
    static void DrawGeneralUpgradeStone(P p)
    {
        p.BgGrad(H("0A0A0A"), H("1E1E1E"));
        p.RoundedBorder(10, 2, H("666666"));
        // 돌 베이스
        p.FillCircle(24, 28, 14, H("555555"));
        p.FillCircle(24, 28, 12, H("6A6A6A"));
        // 돌 표면 질감
        p.DrawLine(18, 24, 22, 20, HA("888888", 80), 1);
        p.DrawLine(26, 22, 30, 26, HA("888888", 60), 1);
        // 금 별 (상단)
        DrawStar(p, 24, 16, 9, 4, H("FFD700"), H("CC8800"));
        // 강화 상향 화살표 (돌 위)
        p.FillTri(24, 22, 20, 28, 28, 28, H("FFCC22"));
        p.FillRect(22, 28, 4, 6, H("FFCC22"));
        // 파티클
        p.FillCircle(14, 20, 2, HA("FFD700", 180));
        p.FillCircle(34, 22, 2, HA("FFD700", 150));
        p.FillCircle(18, 36, 2, HA("FFD700", 120));
    }

    // ── EquipUpgradeStone (장비 강화석) ──────────────────
    static void DrawEquipUpgradeStone(P p)
    {
        p.BgGrad(H("020810"), H("061828"));
        p.RoundedBorder(10, 2, H("334466"));
        // 결정 (작게, 하단)
        p.FillRect(20, 28, 8, 10, H("2255AA"));
        p.FillTri(20, 28, 28, 28, 24, 21, H("4477CC"));
        p.FillTri(20, 38, 28, 38, 24, 43, H("1A3A7A"));
        p.FillRect(20, 29, 2, 8, HA("88BBFF", 80));
        // 망치 (상단)
        // 자루
        p.DrawLine(20, 26, 32, 14, H("996633"), 3);
        p.DrawLine(20, 26, 32, 14, H("BB8844"), 1);
        // 망치 헤드
        p.FillRRect(28, 9, 12, 8, 2, H("AAAAAA"));
        p.FillRect(29, 10, 10, 3, HA("DDDDDD", 180));
        p.FillRect(29, 13, 10, 1, HA("777777", 150));
        // 충격 파티클
        p.FillCircleAlpha(22, 26, 5, HA("AADDFF", 80));
        p.FillCircle(16, 30, 2, HA("88BBFF", 150));
        p.FillCircle(30, 26, 2, HA("88BBFF", 130));
    }

    // ── SoldierShard (용병조각) ───────────────────────────
    static void DrawSoldierShard(P p)
    {
        p.BgGrad(H("050C18"), H("0A1E38"));
        p.RoundedBorder(10, 2, H("1A5A88"));
        // 글로우 배경
        p.FillCircleAlpha(24, 24, 20, HA("1177AA", 55));
        p.FillCircleAlpha(24, 24, 13, HA("22AACC", 45));
        // 마름모 조각 몸체
        FillDiamond(p, 24, 26, 14, 17, H("0D3A5A"));
        FillDiamond(p, 24, 26, 12, 15, H("1A5888"));
        // 마름모 테두리 (빛나는 엣지)
        p.DrawLine(24,  9, 10, 26, H("33BBDD"), 1);
        p.DrawLine(10, 26, 24, 43, H("33BBDD"), 1);
        p.DrawLine(24,  9, 38, 26, H("22AACC"), 1);
        p.DrawLine(38, 26, 24, 43, H("22AACC"), 1);
        // 병사 실루엣 (머리 + 몸통)
        p.FillCircle(24, 17, 5, H("88DDFF")); // 머리
        p.FillTri(17, 35, 31, 35, 24, 22, H("66BBDD")); // 몸 삼각형
        // 균열 라인 (조각 느낌)
        p.DrawLine(22, 10, 20, 22, HA("AAEEFF", 130), 1);
        p.DrawLine(26, 11, 28, 22, HA("AAEEFF", 100), 1);
        // 파티클
        p.FillCircle(13, 13, 2, HA("44DDFF", 200));
        p.FillCircle(35, 13, 2, HA("44DDFF", 160));
        p.FillCircle(13, 38, 2, HA("44DDFF", 120));
        // 광택 하이라이트
        p.FillCircleAlpha(16, 14, 4, HA("BBEEFF", 110));
        p.FillCircle(16, 14, 2, HA("FFFFFF", 180));
    }

    // ── Reincarnation Point (환생 포인트) ─────────────────
    static void DrawReincarnationPoint(P p)
    {
        p.BgGrad(H("100804"), H("2C1804"));
        p.RoundedBorder(10, 2, H("CC8822"));
        // 외곽 글로우
        p.FillCircleAlpha(24, 24, 18, HA("FF9900", 35));
        p.FillCircleAlpha(24, 24, 12, HA("FFBB44", 30));
        // 원형 화살표 호 (30° ~ 320°, 시계 방향)
        var gold  = H("D4A840");
        var ltGold = H("FFE080");
        for (int i = 30; i <= 320; i += 5)
        {
            float a = i * Mathf.Deg2Rad;
            int x = 24 + Mathf.RoundToInt(Mathf.Cos(a) * 14);
            int y = 24 + Mathf.RoundToInt(Mathf.Sin(a) * 14);
            p.FillCircle(x, y, 3, gold);
            int xi = 24 + Mathf.RoundToInt(Mathf.Cos(a) * 12);
            int yi = 24 + Mathf.RoundToInt(Mathf.Sin(a) * 12);
            p.FillCircle(xi, yi, 1, ltGold);
        }
        // 화살표 머리 (320° 근처)
        float ah = 320 * Mathf.Deg2Rad;
        int ax = 24 + Mathf.RoundToInt(Mathf.Cos(ah) * 14);
        int ay = 24 + Mathf.RoundToInt(Mathf.Sin(ah) * 14);
        p.FillTri(ax, ay, ax + 7, ay - 3, ax + 3, ay + 8, gold);
        // 중앙 별
        DrawStar(p, 24, 24, 7, 3, H("FFD060"), H("D4A840"));
        p.FillCircle(24, 24, 2, H("FFFFAA"));
    }

    // ── EquipBox (장비 박스) ──────────────────────────────
    static void DrawEquipBox(P p)
    {
        p.BgGrad(H("0E0802"), H("241504"));
        p.RoundedBorder(10, 2, H("8B5A22"));
        // 상자 몸통
        p.FillRRect(8, 22, 32, 20, 2, H("8B4A18"));
        p.FillRect(9, 23, 30, 18, H("A05A22"));
        // 상자 뚜껑
        p.FillRRect(8, 14, 32, 10, 2, H("7A3A10"));
        p.FillRect(9, 15, 30, 8,  H("8B4416"));
        // 금 장식 테두리
        p.FillRect(8, 22, 32, 3, H("CC8822"));
        p.DrawLine(9, 22, 38, 22, H("EEA833"), 1);
        // 경첩 (뚜껑-몸통 연결)
        p.FillCircle(14, 22, 3, H("AA7722"));
        p.FillCircle(34, 22, 3, H("AA7722"));
        // 중앙 자물쇠
        p.FillRRect(20, 19, 8, 6, 2, H("CC9922"));
        p.FillRect(22, 22, 4, 5,    H("886600"));
        // 자물쇠 고리
        DrawArc(p, 22, 19, 26, 19, H("CC9922"), 2);
        // 반짝임 (열려는 기운)
        p.FillCircleAlpha(24, 18, 6, HA("FFCC44", 50));
        p.FillCircle(30, 14, 2, HA("FFCC44", 180));
        p.FillCircle(18, 14, 2, HA("FFCC44", 150));
        // 목재 결 느낌
        p.DrawLine(10, 27, 38, 27, HA("6A3010", 80), 1);
        p.DrawLine(10, 32, 38, 32, HA("6A3010", 60), 1);
        p.DrawLine(10, 37, 38, 37, HA("6A3010", 50), 1);
    }

    // ═══════════════════════════════════════════════════════
    //  ■ 도형 헬퍼
    // ═══════════════════════════════════════════════════════

    static void FillDiamond(P p, int cx, int cy, int hw, int hh, Color32 c)
    {
        for (int y = cy - hh; y <= cy + hh; y++)
        {
            float t = 1f - Mathf.Abs(y - cy) / (float)hh;
            int w = Mathf.RoundToInt(hw * t);
            p.FillRect(cx - w, y, w * 2 + 1, 1, c);
        }
    }

    static void FillShield(P p, int cx, int top, int hw, int h, Color32 c)
    {
        int bot = top + h;
        for (int y = top; y <= bot; y++)
        {
            float t = (float)(y - top) / h;
            int w = t < 0.5f
                ? hw
                : Mathf.RoundToInt(hw * (1f - (t - 0.5f) * 2f));
            p.FillRect(cx - w, y, w * 2 + 1, 1, c);
        }
    }

    static void DrawShieldOutline(P p, int cx, int top, int hw, int h, Color32 c, int thick)
    {
        int bot = top + h;
        for (int y = top; y <= bot; y++)
        {
            float t = (float)(y - top) / h;
            int w = t < 0.5f
                ? hw
                : Mathf.RoundToInt(hw * (1f - (t - 0.5f) * 2f));
            for (int d = 0; d < thick; d++)
            {
                p.BlendPixel(cx - w - d, y, c);
                p.BlendPixel(cx + w + d, y, c);
            }
        }
        p.DrawLine(cx - hw, top, cx + hw, top, c, thick);
    }

    static void DrawStar(P p, int cx, int cy, int outerR, int innerR, Color32 outer, Color32 inner)
    {
        // 5-pointed star via triangle fan
        for (int i = 0; i < 5; i++)
        {
            float a1 = (i * 72 - 90) * Mathf.Deg2Rad;
            float a2 = ((i * 72) + 36 - 90) * Mathf.Deg2Rad;
            float a3 = ((i + 1) * 72 - 90) * Mathf.Deg2Rad;
            int ox1 = cx + Mathf.RoundToInt(Mathf.Cos(a1) * outerR);
            int oy1 = cy + Mathf.RoundToInt(Mathf.Sin(a1) * outerR);
            int ix  = cx + Mathf.RoundToInt(Mathf.Cos(a2) * innerR);
            int iy  = cy + Mathf.RoundToInt(Mathf.Sin(a2) * innerR);
            int ox2 = cx + Mathf.RoundToInt(Mathf.Cos(a3) * outerR);
            int oy2 = cy + Mathf.RoundToInt(Mathf.Sin(a3) * outerR);
            p.FillTri(ox1, oy1, ix, iy, cx, cy, outer);
            p.FillTri(ix,  iy, ox2, oy2, cx, cy, inner);
        }
    }

    static void DrawArc(P p, int x1, int y1, int x2, int y2, Color32 c, int thick)
    {
        int mcx = (x1 + x2) / 2;
        int mcy = Mathf.Min(y1, y2) - 5;
        int steps = 20, px = x1, py = y1;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps, mt = 1 - t;
            int nx = Mathf.RoundToInt(mt * mt * x1 + 2 * mt * t * mcx + t * t * x2);
            int ny = Mathf.RoundToInt(mt * mt * y1 + 2 * mt * t * mcy + t * t * y2);
            p.DrawLine(px, py, nx, ny, c, thick);
            px = nx; py = ny;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  ■ 유틸
    // ═══════════════════════════════════════════════════════

    static void Save(string filename, Action<P> draw)
    {
        var painter = new P(48, 48);
        draw(painter);
        painter.Save(Path.Combine(ITEM_PATH, filename));
    }

    static void EnsureDir(string assetPath)
    {
        string full = Path.Combine(Application.dataPath, "..", assetPath);
        Directory.CreateDirectory(full);
    }

    static void ApplySpriteImport(string folder)
    {
        // maxTextureSize 를 원본(192)보다 작게 두면 임포트 단계에서 다시 줄어들어
        // 애써 올린 해상도가 그대로 날아간다. 원본 크기 이상으로 잡을 것.
        int maxSize = Mathf.NextPowerOfTwo(48 * P.S);   // 192 → 256

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png")) continue;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePivot         = new Vector2(0.5f, 0.5f);
            importer.filterMode          = FilterMode.Bilinear;
            importer.mipmapEnabled       = false;   // UI 는 밉맵이 없어야 선명하다
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize      = maxSize;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  ■ Painter (픽셀 그리기 헬퍼) — IconGenerator 와 동일 구조
    // ═══════════════════════════════════════════════════════
    class P
    {
        // 슈퍼샘플 배율. 그리기 좌표는 그대로 48 격자를 쓰고,
        // 실제 버퍼만 S 배로 잡아 도형을 그 해상도에서 계산한다.
        // → 단순 확대가 아니라 원·선이 실제로 매끄러워진다.
        public const int S = 4;

        // 픽셀당 서브샘플 수 (AA×AA). 경계 커버리지를 알파로 환산한다.
        const int AA = 3;

        public readonly int W, H;      // 논리 크기 (48×48)
        readonly int _dw, _dh;         // 실제 픽셀 (192×192)
        readonly Color32[] _px;

        public P(int w, int h)
        {
            W = w; H = h;
            _dw = w * S; _dh = h * S;
            _px = new Color32[_dw * _dh];
        }

        // ── 실제 픽셀 단위 ────────────────────────────────────

        int Idx(int x, int y) => (_dh - 1 - y) * _dw + x;

        void Dev(int x, int y, Color32 c)
        {
            if (x < 0 || x >= _dw || y < 0 || y >= _dh || c.a == 0) return;
            int i = Idx(x, y);
            if (c.a == 255) { _px[i] = c; return; }
            float a = c.a / 255f, ea = _px[i].a / 255f;
            float oa = a + ea * (1 - a);
            if (oa < 0.001f) { _px[i] = default; return; }
            _px[i] = new Color32(
                (byte)Mathf.RoundToInt((c.r * a + _px[i].r * ea * (1 - a)) / oa),
                (byte)Mathf.RoundToInt((c.g * a + _px[i].g * ea * (1 - a)) / oa),
                (byte)Mathf.RoundToInt((c.b * a + _px[i].b * ea * (1 - a)) / oa),
                (byte)Mathf.RoundToInt(oa * 255));
        }

        /// <summary>
        /// inside(x,y) 판정을 픽셀당 AA×AA 로 샘플링해 커버리지만큼 알파를 낮춰 칠한다.
        /// 원·선·삼각형의 경계가 계단지지 않게 하는 공용 경로.
        /// </summary>
        void FillShape(float minX, float minY, float maxX, float maxY,
                       Func<float, float, bool> inside, Color32 c)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX));
            int x1 = Mathf.Min(_dw - 1, Mathf.CeilToInt(maxX));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
            int y1 = Mathf.Min(_dh - 1, Mathf.CeilToInt(maxY));

            const float Inv = 1f / AA;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < AA; sy++)
                for (int sx = 0; sx < AA; sx++)
                    if (inside(x + (sx + 0.5f) * Inv, y + (sy + 0.5f) * Inv)) hit++;

                if (hit == 0) continue;
                var cc = c;
                cc.a = (byte)Mathf.RoundToInt(c.a * hit / (float)(AA * AA));
                Dev(x, y, cc);
            }
        }

        // 논리 좌표 → 실제 좌표 (해당 논리 픽셀의 중심)
        static float Dx(int v) => (v + 0.5f) * S;

        // ── 논리 좌표 API (그리기 코드는 48 격자 그대로 쓴다) ──

        /// <summary>논리 픽셀 1칸을 칠한다 (S×S 블록).</summary>
        public void BlendPixel(int x, int y, Color32 c)
        {
            for (int dy = 0; dy < S; dy++)
            for (int dx = 0; dx < S; dx++)
                Dev(x * S + dx, y * S + dy, c);
        }

        public void BgGrad(Color32 dark, Color32 mid)
        {
            float cx = _dw * 0.5f, cy = _dh * 0.5f;
            float maxD = Mathf.Sqrt(cx * cx + cy * cy);
            float r = 10f * S;

            for (int y = 0; y < _dh; y++)
            for (int x = 0; x < _dw; x++)
            {
                if (!InRoundedCanvas(x + 0.5f, y + 0.5f, r)) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float t = Mathf.Clamp01(d / maxD);
                Dev(x, y, Color32.Lerp(mid, dark, t * t));
            }
        }

        // 모서리가 둥근 캔버스 안쪽인지
        bool InRoundedCanvas(float x, float y, float r)
        {
            float dx = 0f, dy = 0f;
            if (x < r)            dx = r - x;
            else if (x > _dw - r) dx = x - (_dw - r);
            if (y < r)            dy = r - y;
            else if (y > _dh - r) dy = y - (_dh - r);
            return dx * dx + dy * dy <= r * r;
        }

        public void RoundedBorder(int radius, int thick, Color32 c)
        {
            float r  = radius * S;
            float th = thick  * S;
            FillShape(0, 0, _dw, _dh,
                (x, y) => InRoundedCanvas(x, y, r) && !InsetRounded(x, y, r, th), c);
        }

        // 테두리 두께만큼 안쪽으로 들어간 영역인지 (테두리 링 계산용)
        bool InsetRounded(float x, float y, float r, float th)
        {
            if (x < th || x > _dw - th || y < th || y > _dh - th) return false;
            float ir = Mathf.Max(0f, r - th);
            float dx = 0f, dy = 0f;
            if (x < th + ir)            dx = (th + ir) - x;
            else if (x > _dw - th - ir) dx = x - (_dw - th - ir);
            if (y < th + ir)            dy = (th + ir) - y;
            else if (y > _dh - th - ir) dy = y - (_dh - th - ir);
            return dx * dx + dy * dy <= ir * ir;
        }

        public void FillRect(int x, int y, int w, int h, Color32 c)
        {
            // 사각형은 격자에 딱 맞으므로 AA 없이 채운다 (경계가 흐려지지 않게)
            for (int fy = y * S; fy < (y + h) * S; fy++)
            for (int fx = x * S; fx < (x + w) * S; fx++)
                Dev(fx, fy, c);
        }

        public void FillRRect(int x, int y, int w, int h, int r, Color32 c)
        {
            float x0 = x * S, y0 = y * S, x1 = (x + w) * S, y1 = (y + h) * S;
            float rr = r * S;
            FillShape(x0, y0, x1, y1, (px, py) =>
            {
                if (px < x0 || px > x1 || py < y0 || py > y1) return false;
                float dx = 0f, dy = 0f;
                if (px < x0 + rr)      dx = (x0 + rr) - px;
                else if (px > x1 - rr) dx = px - (x1 - rr);
                if (py < y0 + rr)      dy = (y0 + rr) - py;
                else if (py > y1 - rr) dy = py - (y1 - rr);
                return dx * dx + dy * dy <= rr * rr;
            }, c);
        }

        public void FillCircle(int cx, int cy, int r, Color32 c)
        {
            float ccx = Dx(cx), ccy = Dx(cy), rr = (r + 0.5f) * S;
            FillShape(ccx - rr - 1, ccy - rr - 1, ccx + rr + 1, ccy + rr + 1,
                (x, y) => (x - ccx) * (x - ccx) + (y - ccy) * (y - ccy) <= rr * rr, c);
        }

        public void FillCircleAlpha(int cx, int cy, int r, Color32 c)
            => FillCircle(cx, cy, r, c);

        public void DrawCircle(int cx, int cy, int r, int thick, Color32 c)
        {
            float ccx = Dx(cx), ccy = Dx(cy);
            float ro = (r + thick + 0.5f) * S;
            float ri = Mathf.Max(0f, (r - thick + 0.5f) * S);
            FillShape(ccx - ro - 1, ccy - ro - 1, ccx + ro + 1, ccy + ro + 1, (x, y) =>
            {
                float d2 = (x - ccx) * (x - ccx) + (y - ccy) * (y - ccy);
                return d2 <= ro * ro && d2 >= ri * ri;
            }, c);
        }

        public void DrawLine(int x0, int y0, int x1, int y1, Color32 c, int thick)
        {
            float ax = Dx(x0), ay = Dx(y0), bx = Dx(x1), by = Dx(y1);
            float half = (thick + 1) * S * 0.5f;   // 예전 박스 스탬프와 같은 굵기
            float vx = bx - ax, vy = by - ay;
            float len2 = vx * vx + vy * vy;

            FillShape(Mathf.Min(ax, bx) - half - 1, Mathf.Min(ay, by) - half - 1,
                      Mathf.Max(ax, bx) + half + 1, Mathf.Max(ay, by) + half + 1, (px, py) =>
            {
                float t = len2 > 0f
                    ? Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / len2)
                    : 0f;
                float dx = px - (ax + vx * t), dy = py - (ay + vy * t);
                return dx * dx + dy * dy <= half * half;
            }, c);
        }

        public void FillTri(int x0, int y0, int x1, int y1, int x2, int y2, Color32 c)
        {
            float ax = Dx(x0), ay = Dx(y0);
            float bx = Dx(x1), by = Dx(y1);
            float cx2 = Dx(x2), cy2 = Dx(y2);

            FillShape(Mathf.Min(ax, Mathf.Min(bx, cx2)) - 1, Mathf.Min(ay, Mathf.Min(by, cy2)) - 1,
                      Mathf.Max(ax, Mathf.Max(bx, cx2)) + 1, Mathf.Max(ay, Mathf.Max(by, cy2)) + 1,
                (px, py) =>
                {
                    float d0 = Sign(px, py, ax, ay, bx, by);
                    float d1 = Sign(px, py, bx, by, cx2, cy2);
                    float d2 = Sign(px, py, cx2, cy2, ax, ay);
                    bool hasNeg = d0 < 0 || d1 < 0 || d2 < 0;
                    bool hasPos = d0 > 0 || d1 > 0 || d2 > 0;
                    return !(hasNeg && hasPos);
                }, c);
        }

        static float Sign(float px, float py, float ax, float ay, float bx, float by)
            => (px - bx) * (ay - by) - (ax - bx) * (py - by);

        public void Save(string assetPath)
        {
            var tex = new Texture2D(_dw, _dh, TextureFormat.RGBA32, false);
            tex.SetPixels32(_px);
            tex.Apply();
            string full = Path.Combine(Application.dataPath, "..", assetPath);
            File.WriteAllBytes(full, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    // ── Mana Essence (마력의 정수) ─────────────────────────
    //
    //  ⚠ 마나 물방울(UIIconAssets)과 달라야 한다
    //    그쪽은 "지금 쓰는 마나" 이고 이건 "그릇을 키우는 물건" 이다.
    //    같은 모양이면 상점이 마나를 파는 것으로 읽힌다 — 그래서 물방울이
    //    아니라 **각진 결정**이고, 안에 심지가 하나 박혀 있다.
    static void DrawManaEssence(P p)
    {
        p.BgGrad(H("0A0620"), H("1E1046"));
        p.RoundedBorder(10, 2, H("5A3AC8"));

        // 결정 — 위아래로 뾰족한 마름모. 가로로 한 줄씩 좁혀 그린다.
        for (int y = 6; y <= 42; y++)
        {
            // 24 를 중심으로 위아래 대칭. 가운데가 가장 넓다.
            int half = 11 - Mathf.Abs(24 - y) * 11 / 18;
            if (half <= 0) continue;

            // 왼쪽 위가 밝은 단일 광원 (아이콘 공통 규칙)
            p.FillRect(24 - half, y, half,     1, H("4A2A96"));
            p.FillRect(24,        y, half + 1, 1, H("7A4EE0"));
        }

        // 심지 — 가운데 세로 빛
        p.FillRect(22, 14, 4, 20, H("B99AFF"));
        p.FillRect(23, 18, 2, 12, H("E6DBFF"));

        // 광택
        p.FillCircleAlpha(19, 16, 3, HA("FFFFFF", 80));
    }

    // ── War Fund (전쟁 자금) ─────────────────────────────
    //
    //  ⚠ item_gold 와 한눈에 갈려야 한다 — 그쪽은 **동전 한 닢**이다.
    //    이건 자루라, 같은 금색이어도 실루엣이 다르다. 붉은 바탕도 그 몫이다
    //    (공격 축 = 붉음 · 방어 축 = 돌 · 마나 = 보라).
    static void DrawWarFund(P p)
    {
        p.BgGrad(H("200A08"), H("4A1410"));
        p.RoundedBorder(10, 2, H("A03828"));

        // 자루 — 아래가 불룩하다
        p.FillCircle(24, 30, 13, H("6B3A1E"));
        p.FillRect(11, 30, 26, 8, H("6B3A1E"));

        // 왼쪽 위가 밝은 단일 광원 (아이콘 공통 규칙)
        p.FillCircle(21, 28, 9, H("8A4E28"));

        // 묶은 목 + 금끈
        p.FillRect(17, 14, 14, 5, H("3E2010"));
        p.FillRect(18, 12, 12, 3, H("C8920A"));

        // 삐져나온 금화
        p.FillCircle(18, 10, 4, H("C8920A"));
        p.FillCircle(30, 11, 4, H("C8920A"));
        p.FillCircle(24,  8, 5, H("F0B800"));
        p.FillCircle(24,  8, 3, H("E8CC44"));

        // 광택
        p.FillCircleAlpha(19, 25, 4, HA("FFFFFF", 60));
    }


    // ── War Drum (소집의 북) ─────────────────────────────
    //
    //  배출 간격을 줄이는 상시 판매품. 전쟁 자금(붉은 자루)·정수(보라 결정)와
    //  바탕색부터 갈린다 — 초록 바탕 · 가죽 북 · 엇갈린 북채.
    static void DrawWarDrum(P p)
    {
        p.BgGrad(H("0A1C10"), H("163C22"));
        p.RoundedBorder(10, 2, H("3A9A58"));

        // 북통 — 옆면
        p.FillRect(11, 22, 26, 16, H("7A3A1C"));
        p.FillRect(11, 22, 12, 16, H("9A5028"));   // 왼쪽 위 광원

        // 테 — 금색 띠 둘
        p.FillRect(11, 22, 26, 3, H("C8920A"));
        p.FillRect(11, 35, 26, 3, H("A87808"));

        // 가죽 면 — 위쪽 타원
        for (int y = 16; y <= 24; y++)
        {
            int half = 13 - Mathf.Abs(20 - y) * 13 / 5;
            if (half <= 0) continue;
            p.FillRect(24 - half, y, half * 2, 1, H("E8D6A8"));
        }

        // 북채 둘 — 엇갈려 위로
        for (int i = 0; i < 12; i++)
        {
            p.FillRect(12 + i, 4 + i, 2, 2, H("D8B070"));
            p.FillRect(34 - i, 4 + i, 2, 2, H("C09858"));
        }
        p.FillCircle(12, 4, 3, H("F0E0C0"));
        p.FillCircle(36, 4, 3, H("F0E0C0"));

        // 광택
        p.FillCircleAlpha(18, 19, 3, HA("FFFFFF", 70));
    }

    // 마나 회복 포션 — 둥근 물약병. 정수(결정)와 모양이 달라야 한다: 이건 "채우는 것", 정수는 "그릇을 키우는 것".
    static void DrawManaPotion(P p)
    {
        p.BgGrad(H("120A22"), H("2A1648"));
        p.RoundedBorder(10, 2, H("8A5AD8"));

        // 병 몸통 — 둥근 플라스크
        p.FillCircle(24, 30, 12, H("C8C0E0"));      // 유리 테두리
        p.FillCircle(24, 30, 10, H("3A2A8A"));      // 빈 윗부분
        for (int y = 30; y <= 40; y++)             // 아래 2/3 에 담긴 마나
        {
            int half = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, 100f - (y - 30) * (y - 30))));
            if (half > 0) p.FillRect(24 - half, y, half * 2, 1, H("4A9CFF"));
        }
        p.FillRect(15, 29, 18, 2, H("9ACCFF"));     // 수면

        // 병목 + 코르크
        p.FillRect(20, 12, 8, 8, H("C8C0E0"));
        p.FillRect(21, 13, 6, 7, H("5A4AA0"));
        p.FillRect(19, 8, 10, 5, H("A87848"));

        // 광택
        p.FillCircleAlpha(19, 25, 3, HA("FFFFFF", 90));
    }

}
