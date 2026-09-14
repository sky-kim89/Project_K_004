// ============================================================
//  DifficultyEnums.cs
//  난이도 등급과 디버프 종류.
//
//  ■ 이름은 직관 우선이다
//    처음엔 출정·혈전·사지·초열·무간 으로 잡았지만, 어느 쪽이 더 어려운지
//    이름만 보고 알 수 없었다. 난이도 이름은 분위기보다 순서가 먼저 읽혀야 한다.
//    쉬움 → 보통 → 어려움 → 지옥 → 불지옥.
//
//  ■ 디버프는 누적된다
//    등급이 오를 때마다 새 디버프가 하나 추가되고,
//    기존 디버프의 수치도 함께 올라간다.
//    (보통=광포 / 어려움=+물량 / 지옥=+각성 / 불지옥=+폭주)
//
//  ⚠ 전부 '적을 강화' 하는 방향이다
//    아군을 깎으면 그동안 키운 성장이 무의미해 보인다.
//    같은 체감을 적 강화로 내면 성장 감각을 지키면서 난이도만 올릴 수 있다.
// ============================================================

public enum DifficultyTier
{
    Easy    = 0,   // 쉬움   — 디버프 없음 (기준선)
    Normal  = 1,   // 보통
    Hard    = 2,   // 어려움
    Hell    = 3,   // 지옥
    Inferno = 4,   // 불지옥
}

public enum DifficultyDebuff
{
    None      = 0,
    Ferocity  = 1,   // 광포 — 적 공격력·최대체력 증가
    Horde     = 2,   // 물량 — 적 등장 수 증가
    Awakening = 3,   // 각성 — 엘리트·보스 스킬 쿨다운 감소
    Frenzy    = 4,   // 폭주 — 엘리트가 돌진 습득, 보스가 분쇄 강타 습득
}

public static class DifficultyNames
{
    public static string Label(this DifficultyTier t) => t switch
    {
        DifficultyTier.Easy    => "쉬움",
        DifficultyTier.Normal  => "보통",
        DifficultyTier.Hard    => "어려움",
        DifficultyTier.Hell    => "지옥",
        DifficultyTier.Inferno => "불지옥",
        _                      => "쉬움",
    };

    /// <summary>선택 화면에 한 줄로 붙이는 요약. 무엇이 어떻게 힘든지 바로 말해 준다.</summary>
    public static string Summary(this DifficultyTier t) => t switch
    {
        DifficultyTier.Easy    => "기본 난이도. 추가 제약이 없다.",
        DifficultyTier.Normal  => "적이 더 단단하고 아프다.",
        DifficultyTier.Hard    => "적이 더 강해지고 수도 늘어난다.",
        DifficultyTier.Hell    => "우두머리가 스킬을 훨씬 자주 쓴다.",
        DifficultyTier.Inferno => "우두머리가 새로운 공격 패턴을 쓴다.",
        _                      => "",
    };

    /// <summary>아이콘 파일명·SpriteManager 키. difficulty_&lt;이름&gt; 형식.</summary>
    public static string IconKey(this DifficultyTier t) => $"difficulty_{t.ToString().ToLower()}";

    public static string Label(this DifficultyDebuff d) => d switch
    {
        DifficultyDebuff.Ferocity  => "광포",
        DifficultyDebuff.Horde     => "물량",
        DifficultyDebuff.Awakening => "각성",
        DifficultyDebuff.Frenzy    => "폭주",
        _                          => "",
    };

    public static string IconKey(this DifficultyDebuff d) => $"debuff_{d.ToString().ToLower()}";
}
