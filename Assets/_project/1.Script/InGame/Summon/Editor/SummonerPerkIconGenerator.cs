using UnityEditor;
using UnityEngine;

// ============================================================
//  SummonerPerkIconGenerator.cs  [Editor Only]
//  Tools > Project K > 아이콘·텍스처 > 소환사 개성 아이콘
//  소환사 개성 12종의 64×64 PNG 를 만든다.
//    → Assets/_project/3.Textures/Icons/RunPerks/sperk_<Enum>.png
//
//  ■ 왜 필요했나 (사용자 지적, 2026-09-07 — 두 번 지적받았다)
//    상단 보유 특성 줄(RunPerkBarUI)의 맨 앞 칸이 **소환사 개성**인데,
//    그 자리에 `SetupCustom(null, ...)` 로 **아이콘을 안 넘기고 있었다.**
//    "개성은 소환사마다 하나뿐이라 세트를 또 굽는 것보다 이름과 설명만
//    띄우는 편이 맞다" 는 주석이 붙어 있었는데, 화면에서는 그냥
//    **빈 칸**으로 보인다 — 줄에 선 다른 칸은 전부 그림이라 더 그렇다.
//    개성은 그 런의 성격을 정하는 단 하나의 상시 효과라, 오히려 제일
//    잘 보여야 하는 칸이다.
//
//  ■ ⚠ 런 특성과 접두사를 나눈다 (sperk_ vs perk_)
//    두 enum 에 CheapAffinity 가 둘 다 있다. 접두사가 같으면 아틀라스에서
//    한쪽이 다른 쪽 그림을 가져간다 — SpriteManager.Get 은 이름으로만 찾는다.
//    폴더는 같이 쓴다 (SpriteManagerCreator 에 줄을 더할 필요가 없게).
//
//  ■ 그림은 뜻을 따라간다 — 런 특성 표와 같은 축을 쓴다
//    같은 뜻이면 같은 글리프를 쓴다 (친화 할인 = 물방울 + 마이너스 배지).
//    개성과 특성이 같은 줄에 나란히 서므로, 뜻이 같은데 그림이 다르면
//    "다른 것" 으로 읽힌다.
//
//  ■ 더미다 — 진짜 그림이 오면 같은 경로·같은 파일명으로 덮으면 된다.
//
//  ⚠ 개성을 추가하면 아래 표에도 넣고 다시 구울 것 (Verify 가 잡아 준다)
//  ⚠ 구운 뒤 데이터 생성 > SpriteManager + 아틀라스 까지 해야 화면에 뜬다
// ============================================================

public static class SummonerPerkIconGenerator
{
    /// <summary>런 특성과 같은 폴더를 쓴다 — 접두사(sperk_)로 이미 갈린다.</summary>
    static string Dir => RunPerkIconAssets.Dir.TrimEnd('/');

    static string PathOf(SummonerPerk p) => $"{Dir}/{SummonerPerkIconKey.Of(p)}.png";

    [MenuItem(ProjectKMenu.Icon + "소환사 개성 아이콘", priority = ProjectKMenu.IconPrio + 8)]
    public static void Generate()
    {
        IconGenerator.EnsureDir(Dir);

        foreach (Entry e in Table)
            IconGenerator.Save(64, 64, PathOf(e.Perk), p => IconArt.Compose(p, e.Style));

        AssetDatabase.Refresh();
        IconGenerator.ApplySpriteImportSettings(Dir, 64);
        AssetDatabase.SaveAssets();

        Verify();

        Debug.Log($"[SummonerPerkIconGenerator] 개성 아이콘 {Table.Length}장 생성 완료 → {Dir}\n" +
                  "⚠ 아틀라스에 넣으려면 데이터 생성 > SpriteManager + 아틀라스 도 실행하세요.");
    }

    /// <summary>
    /// 표에 빠진 개성이 없는지 본다.
    ///
    /// ⚠ 이 검사가 없으면 실패가 조용하다 — 그 개성을 가진 소환사를 골랐을 때만
    ///   빈 칸이 되므로, 열두 명을 다 굴려 보기 전에는 눈에 띄지 않는다.
    /// </summary>
    static void Verify()
    {
        foreach (SummonerPerk perk in System.Enum.GetValues(typeof(SummonerPerk)))
        {
            if (perk == SummonerPerk.None) continue;

            bool found = false;
            foreach (Entry e in Table) if (e.Perk == perk) { found = true; break; }

            if (!found)
                Debug.LogError($"[SummonerPerkIconGenerator] '{perk}' 가 조합표에 없습니다 — " +
                               "그 소환사를 골랐을 때만 칸이 비어 보입니다.");
        }
    }

    readonly struct Entry
    {
        public readonly SummonerPerk  Perk;
        public readonly IconArt.Style Style;
        public Entry(SummonerPerk p, IconArt.Style style) { Perk = p; Style = style; }
    }

    static Entry E(SummonerPerk p, IconArt.Glyph g, string accent,
                   IconArt.Bg bg, IconArt.Frame fr, IconArt.Badge bd = IconArt.Badge.None)
        => new Entry(p, new IconArt.Style(g, IconGenerator.Hex(accent), bg, fr, bd));

    // 색은 런 특성 표(RunPerkIconGenerator)와 같은 팔레트다 — 한 줄에 서기 때문이다.
    const string Violet = "9B5CFF", Indigo = "6C6CFF", Sky   = "6FA8FF", Cyan = "3FC8FF";
    const string Teal   = "2FC5B5", Green  = "4ED96A", Lime  = "9EE04A", Gold = "FFD34A";
    const string Amber  = "FF9F45", Red    = "FF5E5E", Rose  = "FF6FA8", Bone = "E8E2D0";

    static readonly Entry[] Table =
    {
        // ── 마나 · 비용 ──────────────────────────────────────
        //   ⚠ 물방울 + 마이너스 = "마나가 덜 든다". 런 특성 CheapAffinity 와
        //     같은 그림이다 — 뜻이 같으므로 일부러 맞췄다.
        E(SummonerPerk.CheapAffinity, IconArt.Glyph.Drop,     Violet, IconArt.Bg.Halo,     IconArt.Frame.Round,  IconArt.Badge.Minus),
        E(SummonerPerk.Plunder,       IconArt.Glyph.Coin,     Gold,   IconArt.Bg.Burst,    IconArt.Frame.Rivet,  IconArt.Badge.Up),
        E(SummonerPerk.DeepChannel,   IconArt.Glyph.Spiral,   Indigo, IconArt.Bg.Radial,   IconArt.Frame.Double, IconArt.Badge.Bolt),

        // ── 몸집 · 품질 ──────────────────────────────────────
        E(SummonerPerk.SwellOnRepeat, IconArt.Glyph.Pulse,    Lime,   IconArt.Bg.Radial,   IconArt.Frame.Round,  IconArt.Badge.Up),
        E(SummonerPerk.AffinityGrade, IconArt.Glyph.Crown,    Gold,   IconArt.Bg.Plate,    IconArt.Frame.Double, IconArt.Badge.Star),
        E(SummonerPerk.FleshGolem,    IconArt.Glyph.Fist,     Bone,   IconArt.Bg.Split,    IconArt.Frame.Rivet,  IconArt.Badge.Plus),

        // ── 죽음에서 나오는 것 ───────────────────────────────
        E(SummonerPerk.RaiseOnDeath,  IconArt.Glyph.Skull,    Sky,    IconArt.Bg.Halo,     IconArt.Frame.Notch,  IconArt.Badge.Plus),
        E(SummonerPerk.PlagueRise,    IconArt.Glyph.Cross,    Green,  IconArt.Bg.Diagonal, IconArt.Frame.Cut,    IconArt.Badge.Up),

        // ── 전투 보정 ────────────────────────────────────────
        E(SummonerPerk.WildSprint,    IconArt.Glyph.Boot,     Cyan,   IconArt.Bg.Diagonal, IconArt.Frame.Cut,    IconArt.Badge.Up),
        E(SummonerPerk.WarCry,        IconArt.Glyph.Horn,     Amber,  IconArt.Bg.Burst,    IconArt.Frame.Notch,  IconArt.Badge.Up),
        E(SummonerPerk.Regenerate,    IconArt.Glyph.Heart,    Teal,   IconArt.Bg.Halo,     IconArt.Frame.Round,  IconArt.Badge.Plus),
        E(SummonerPerk.PackBond,      IconArt.Glyph.Soldiers, Rose,   IconArt.Bg.Plate,    IconArt.Frame.Double, IconArt.Badge.Up),

        // 2026-09-11 교체분 — 점액 사격(슬라임 킹) · 뼈의 군단(스컬 킹)
        E(SummonerPerk.SlimeSpit,     IconArt.Glyph.Arrows,   Lime,   IconArt.Bg.Diagonal, IconArt.Frame.Round,  IconArt.Badge.Up),
        E(SummonerPerk.BoneLegion,    IconArt.Glyph.Skull,    Bone,   IconArt.Bg.Plate,    IconArt.Frame.Double, IconArt.Badge.Plus),

        // 2026-09-12 — 마나·물량 개성 (공격력·이동속도 개성 교체분 + 새 소환사 둘)
        E(SummonerPerk.ArcaneMight,   IconArt.Glyph.Spiral,   Violet, IconArt.Bg.Burst,    IconArt.Frame.Notch,  IconArt.Badge.Up),
        E(SummonerPerk.Crystallize,   IconArt.Glyph.Potion,   Cyan,   IconArt.Bg.Plate,    IconArt.Frame.Double, IconArt.Badge.Plus),
        E(SummonerPerk.Muster,        IconArt.Glyph.Soldiers, Amber,  IconArt.Bg.Burst,    IconArt.Frame.Notch,  IconArt.Badge.Plus),
        E(SummonerPerk.NatureRestore, IconArt.Glyph.Aura,     Green,  IconArt.Bg.Halo,     IconArt.Frame.Round,  IconArt.Badge.Plus),
        E(SummonerPerk.TwinCall,      IconArt.Glyph.Horn,     Rose,   IconArt.Bg.Split,    IconArt.Frame.Double, IconArt.Badge.Plus),
    };
}
