// ============================================================
//  HeroNameRule.cs
//  용사의 **표시 이름**을 만든다. 시드 이름("Hero_S6_0")과 짝을 이룬다.
//
//  ■ ⚠ 시드 이름을 화면에 그대로 내지 말 것
//    "Hero_S6_0" 은 직업·외형·패시브를 결정하는 **씨앗**이라 바꾸면 그 판의
//    적이 통째로 달라진다(HeroDeployment 머리 주석). 그래서 시드는 그대로 두고
//    보여 줄 이름만 여기서 만든다.
//
//  ■ ⚠ 이름은 시드에서 결정적으로 나온다
//    같은 스테이지·같은 자리면 언제나 같은 이름이다. 무작위로 뽑으면
//    정보창을 닫았다 열 때마다 적 이름이 바뀐다.
//
//  ■ 이름 = 수식어 + 이름 + (직업 칭호)
//    "무쇠의 라이하르트 · 마도사" 처럼 읽힌다.
//    ⚠ 세 조각을 각각 다른 자리의 해시로 뽑는다. 같은 해시를 나눠 쓰면
//      수식어와 이름이 함께 움직여 조합 수가 확 준다.
//
//  ■ ⚠ 조각은 **기호 키**로 번역한다 (사용자 확정, 2026-09-16)
//    한국어를 코드에 박아 두면 그 조각이 번역표의 부분 치환 목록에 올라간다.
//    LocalizationManager.LocalizeText 는 완전 일치가 없으면 표의 항목을
//    **긴 것부터 부분 치환**하므로, "강철"·"성벽"·"기사" 같은 짧은 조각이
//    표에 오르는 순간 다른 문구 속 같은 글자까지 함께 바뀐다 —
//    "강철 슬라임", "마왕성 성벽", 직업 이름 "기사" 가 전부 사정권이다.
//    그래서 여기서는 Get(키) 로 **완전 일치 조회**만 한다. 부분 치환을 타지 않는다.
//
//    ⚠ 키를 바꾸면 LocalizationTable.txt 의 Hero.* 줄도 함께 고칠 것.
//      표에 없는 키는 Get 이 키 문자열을 그대로 돌려주므로,
//      화면에 "Hero.Epithet.01" 이 그대로 뜨는 것으로 바로 드러난다.
//
//  ⚠ 보스·엘리트는 접두어가 붙는다 — 화면에서 바로 갈려야 한다.
// ============================================================

public static class HeroNameRule
{
    // ⚠ 배열 순서가 곧 시드 해시의 자리다. 중간에 끼우면 그 판의 적 이름이 통째로 바뀐다.
    //   늘리는 것은 **뒤에만** 할 것.
    static readonly string[] Epithets =
    {
        "Hero.Epithet.01", "Hero.Epithet.02", "Hero.Epithet.03", "Hero.Epithet.04",
        "Hero.Epithet.05", "Hero.Epithet.06", "Hero.Epithet.07", "Hero.Epithet.08",
        "Hero.Epithet.09", "Hero.Epithet.10", "Hero.Epithet.11", "Hero.Epithet.12",
        "Hero.Epithet.13", "Hero.Epithet.14", "Hero.Epithet.15", "Hero.Epithet.16",
    };

    static readonly string[] Names =
    {
        "Hero.Name.01", "Hero.Name.02", "Hero.Name.03", "Hero.Name.04",
        "Hero.Name.05", "Hero.Name.06", "Hero.Name.07", "Hero.Name.08",
        "Hero.Name.09", "Hero.Name.10", "Hero.Name.11", "Hero.Name.12",
        "Hero.Name.13", "Hero.Name.14", "Hero.Name.15", "Hero.Name.16",
        "Hero.Name.17", "Hero.Name.18", "Hero.Name.19", "Hero.Name.20",
        "Hero.Name.21", "Hero.Name.22", "Hero.Name.23", "Hero.Name.24",
    };

    static readonly string[] KnightTitles       = { "Hero.Title.Knight.1", "Hero.Title.Knight.2", "Hero.Title.Knight.3" };
    static readonly string[] ArcherTitles       = { "Hero.Title.Archer.1", "Hero.Title.Archer.2", "Hero.Title.Archer.3" };
    static readonly string[] MageTitles         = { "Hero.Title.Mage.1",   "Hero.Title.Mage.2",   "Hero.Title.Mage.3" };
    static readonly string[] ShieldBearerTitles = { "Hero.Title.Shield.1", "Hero.Title.Shield.2", "Hero.Title.Shield.3" };

    const string DefaultTitleKey = "Hero.Title.None";
    const string BossPrefixKey   = "Hero.Prefix.Boss";
    const string ElitePrefixKey  = "Hero.Prefix.Elite";

    /// <summary>표의 기호 키를 지금 언어의 글자로 바꾼다.</summary>
    static string T(string key) => LocalizationManager.Instance.Get(key);

    /// <summary>
    /// 이 시드의 표시 이름. 직업 칭호까지 붙는다.
    ///
    /// <param name="seedName">HeroDeployment 가 지은 시드 이름 (Hero_S6_0 등).</param>
    /// <param name="isBoss">보스 히어로인가.</param>
    /// <param name="isElite">엘리트 히어로인가.</param>
    /// </summary>
    public static string Of(string seedName, bool isBoss = false, bool isElite = false)
    {
        uint h = UnitJobRoller.StableHash(seedName ?? "");

        // ⚠ 세 조각을 서로 다른 자리에서 뽑는다 (파일 머리 주석 참고).
        string epithet = T(Epithets[(int)(h % (uint)Epithets.Length)]);
        string given   = T(Names   [(int)((h / 17u) % (uint)Names.Length)]);
        string title   = TitleFor(UnitJobRoller.GetJob(seedName), h / 401u);

        // ⚠ 조각을 $"…" 로 이어 붙이지 말 것 (2026-09-17 버그)
        //   이어 붙인 문장은 표에 없다. TMP 의 LocalizedText 가 그것을 한국어로 되돌렸다가
        //   다시 번역할 때 조각 단위 치환만 탔고, Hero.* 는 치환 풀에서 빠져 있어
        //   **화면에 한국어 이름이 그대로 남았다.** Format 으로 조립해야 LocalizeText 가
        //   원래 조각(표의 줄)으로 다시 Format 한다 — 언어를 바꿔도 이름이 따라온다.
        //   일본어·중국어는 띄어 쓰지 않는다 — 어순·띄어쓰기는 표의 Hero.Format.* 줄이 정한다.
        var loc  = LocalizationManager.Instance;
        string body = loc.Format("Hero.Format.Full", epithet, given, title);

        if (isBoss)  return loc.Format("Hero.Format.Prefixed", T(BossPrefixKey),  body);
        if (isElite) return loc.Format("Hero.Format.Prefixed", T(ElitePrefixKey), body);
        return body;
    }

    /// <summary>이름만 (칭호 없이). 좁은 칸에 쓴다.</summary>
    public static string ShortOf(string seedName)
    {
        uint h = UnitJobRoller.StableHash(seedName ?? "");
        return LocalizationManager.Instance.Format("Hero.Format.Short",
            T(Epithets[(int)(h % (uint)Epithets.Length)]),
            T(Names[(int)((h / 17u) % (uint)Names.Length)]));
    }

    static string TitleFor(UnitJob job, uint h) => job switch
    {
        UnitJob.Knight       => T(KnightTitles      [(int)(h % (uint)KnightTitles.Length)]),
        UnitJob.Archer       => T(ArcherTitles      [(int)(h % (uint)ArcherTitles.Length)]),
        UnitJob.Mage         => T(MageTitles        [(int)(h % (uint)MageTitles.Length)]),
        UnitJob.ShieldBearer => T(ShieldBearerTitles[(int)(h % (uint)ShieldBearerTitles.Length)]),
        _                    => T(DefaultTitleKey),
    };
}
