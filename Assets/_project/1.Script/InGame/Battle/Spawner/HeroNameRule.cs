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
//  ⚠ 보스·엘리트는 접두어가 붙는다 — 화면에서 바로 갈려야 한다.
// ============================================================

public static class HeroNameRule
{
    static readonly string[] Epithets =
    {
        "무쇠의", "잿빛", "새벽의", "폭풍의", "강철", "서릿발", "황혼의", "붉은",
        "성난", "고요한", "불굴의", "창백한", "여명의", "굶주린", "천둥의", "은빛",
    };

    static readonly string[] Names =
    {
        "라이하르트", "베르나", "가레스", "이졸데", "루드빅", "카시안", "엘윈", "도르만",
        "미르아", "테오발트", "셀윈", "브란", "아델하이트", "요한", "리케", "오스릭",
        "발렌", "그웨인", "니콜라", "하르윈", "세라핀", "코르빈", "이드리스", "마르셀",
    };

    static readonly string[] KnightTitles       = { "기사", "근위", "선봉" };
    static readonly string[] ArcherTitles       = { "궁수", "사수", "추적자" };
    static readonly string[] MageTitles         = { "마도사", "술사", "현자" };
    static readonly string[] ShieldBearerTitles = { "방패병", "수호자", "성벽" };

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
        string epithet = Epithets[(int)(h % (uint)Epithets.Length)];
        string given   = Names   [(int)((h / 17u) % (uint)Names.Length)];
        string title   = TitleFor(UnitJobRoller.GetJob(seedName), h / 401u);

        string body = $"{epithet} {given}  ·  {title}";

        if (isBoss)  return $"【보스】 {body}";
        if (isElite) return $"〈정예〉 {body}";
        return body;
    }

    /// <summary>이름만 (칭호 없이). 좁은 칸에 쓴다.</summary>
    public static string ShortOf(string seedName)
    {
        uint h = UnitJobRoller.StableHash(seedName ?? "");
        return $"{Epithets[(int)(h % (uint)Epithets.Length)]} " +
               $"{Names[(int)((h / 17u) % (uint)Names.Length)]}";
    }

    static string TitleFor(UnitJob job, uint h) => job switch
    {
        UnitJob.Knight       => KnightTitles      [(int)(h % (uint)KnightTitles.Length)],
        UnitJob.Archer       => ArcherTitles      [(int)(h % (uint)ArcherTitles.Length)],
        UnitJob.Mage         => MageTitles        [(int)(h % (uint)MageTitles.Length)],
        UnitJob.ShieldBearer => ShieldBearerTitles[(int)(h % (uint)ShieldBearerTitles.Length)],
        _                    => "용사",
    };
}
