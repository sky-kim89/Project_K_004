using UnityEngine;
// ============================================================
//  SummonerPerk.cs
//  소환사의 "개성" — 그 캐릭터를 다른 캐릭터와 다르게 만드는 규칙 하나.
//
//  ■ 왜 TraitData 로 못 하나 (검토 결과)
//    TraitData 가 표현할 수 있는 것은 세 가지뿐이다.
//      · 스탯 가산/비율   (TraitStatEntry)
//      · 스탯 → 스탯 환산 (StatConversion)
//      · 트리거로 쌓는 스택 보너스
//    전부 **숫자를 얼마 올릴 것인가** 다. 그래서
//      "오크 3기 이상이면 공격력 +25%"            → 스택 보너스로 가능
//      "트롤이 초당 HP 재생"                       → PassiveSkillType 로 가능
//    까지는 기존 시스템에 얹힌다.
//
//    반면
//      "아군이 죽은 자리에 스켈레톤을 공짜로 세운다"
//      "환수 마나를 1.5배로 돌려받는다"
//      "마나 대신 마왕성 HP 로 소환한다"
//      "시체 두 구를 합쳐 거인을 만든다"
//    는 **소환 파이프라인에 끼어드는 동작**이지 스탯이 아니다.
//    TraitData 에는 이런 훅이 없고, 넣으려면 결국 여기 있는 것과 같은
//    분기 코드를 TraitApplier 안에 만들게 된다 — 특성이 스탯 전용이라는
//    성질만 잃고 얻는 게 없다.
//
//  ■ 그래서 두 층으로 나눴다
//    · 숫자로 끝나는 개성  → MonsterSpeciesData.LevelPassives / TraitData
//                            (기존 시스템 그대로. 새 코드 없음)
//    · 파이프라인 개성      → 이 enum + SummonerPerkRuntime 의 훅
//
//    ActiveSkillId + Active*.cs 와 같은 구조다 — enum 이 정본이고
//    구현이 그 아래 붙는다.
//
//  ■ 훅은 네 개뿐이다 (SummonerPerkRuntime)
//    ModifyManaCost   — 소환 비용을 깎거나 다른 자원으로 치환
//    ModifyRefund     — 환수액 배율
//    OnMonsterSpawned — 소환 직후 개체에 무언가 얹기
//    OnMonsterDied    — 아군 몬스터가 죽었을 때
//
//    훅을 늘리기 전에 정말 필요한지 볼 것. 지금 20개 남짓한 개성이
//    이 넷으로 전부 표현된다.
// ============================================================

public enum SummonerPerk
{
    /// <summary>개성 없음. 순수 스탯·친화도로만 갈리는 소환사.</summary>
    None = 0,

    /// <summary>견습 — 친화 종족의 소환 비용을 1 깎는다. 문턱을 낮추는 개성.</summary>
    CheapAffinity = 1,

    /// <summary>슬라임 킹 — 같은 종족을 연달아 소환할수록 그 종족이 커진다(크기·스탯).</summary>
    SwellOnRepeat = 2,

    /// <summary>강령술사 — 아군 몬스터가 죽은 자리에 스켈레톤 1기를 공짜로 세운다.</summary>
    RaiseOnDeath = 3,

    /// <summary>스컬 킹 — 친화 종족의 품질을 한 단계 올려 소환한다.</summary>
    AffinityGrade = 4,

    /// <summary>역병 술사 — 몬스터가 용사를 잡으면 그 자리에서 좀비가 일어난다.</summary>
    PlagueRise = 5,

    /// <summary>드루이드 — 친화 종족의 이동속도를 크게 올린다.</summary>
    WildSprint = 6,

    /// <summary>오크 킹 — 친화 종족이 여럿 살아 있을수록 전군 공격력이 오른다.</summary>
    WarCry = 7,

    /// <summary>고블린 두목 — 환수 마나를 더 많이 돌려받는다.</summary>
    Plunder = 8,

    /// <summary>트롤 조련사 — 친화 종족이 초당 HP 를 재생한다.</summary>
    Regenerate = 9,

    /// <summary>리치 — 소환력이 친화 종족에게 두 배로 실린다. 본체는 약하다.</summary>
    DeepChannel = 10,

    /// <summary>비스트마스터 — 같은 라인에 같은 종족을 겹칠수록 그 라인이 강해진다.</summary>
    PackBond = 11,

    /// <summary>도살자 — 아군 시체가 일정 수 쌓이면 합쳐 거인 1기를 만든다.</summary>
    FleshGolem = 12,

    // ⚠ 2(SwellOnRepeat)·4(AffinityGrade)는 **쓰지 않는다** (2026-09-11 교체) — 번호는 옛 세이브라 남긴다.

    /// <summary>슬라임 킹 — 부른 친화 종족 N마리 중 1마리가 점액을 뱉는 원거리로 선다. PerkValue = N.</summary>
    SlimeSpit = 13,

    /// <summary>스컬 킹 — 친화 종족 카드가 부르는 마릿수 +PerkValue.</summary>
    BoneLegion = 14,

    // ── 2026-09-12 (사용자 지시) — 공격력·이동속도 개성(6 WildSprint · 7 WarCry)을 걷고
    //    물량·마나 개성으로 바꿨다. ⚠ 6·7 번호는 옛 SO 가 들고 있을 수 있어 남긴다.

    /// <summary>대마법사 — 최대 마나 10당 모든 몬스터 공/체 +PerkValue.</summary>
    ArcaneMight = 15,

    /// <summary>결정술사 — 판이 끝날 때 남은 마나의 PerkValue 가 최대 마나로 쌓인다 (상한 있음).</summary>
    Crystallize = 16,

    /// <summary>오크 킹 — 친화 종족 카드가 부르는 마릿수 +PerkValue (뼈의 군단과 같은 규칙).</summary>
    Muster = 17,

    /// <summary>드루이드 — 판을 넘길 때 최대 마나의 PerkValue 만큼 더 회복한다.</summary>
    NatureRestore = 18,

    /// <summary>군악대장 — 카드를 낼 때 옆 라인에도 PerkValue 마리가 공짜로 선다.</summary>
    TwinCall = 19,
}

public static class SummonerPerkNames
{
    public static string ToKorean(this SummonerPerk perk) => perk switch
    {
        SummonerPerk.CheapAffinity => "친화 할인",
        SummonerPerk.SwellOnRepeat => "증식",
        SummonerPerk.RaiseOnDeath  => "사자 부활",
        SummonerPerk.AffinityGrade => "품질 각인",
        SummonerPerk.PlagueRise    => "역병",
        SummonerPerk.WildSprint    => "야성 질주",
        SummonerPerk.WarCry        => "전쟁 함성",
        SummonerPerk.Plunder       => "약탈",
        SummonerPerk.Regenerate    => "재생",
        SummonerPerk.DeepChannel   => "심연 공명",
        SummonerPerk.PackBond      => "무리 결속",
        SummonerPerk.FleshGolem    => "시체 합성",
        SummonerPerk.SlimeSpit     => "점액 사격",
        SummonerPerk.BoneLegion    => "뼈의 군단",
        SummonerPerk.ArcaneMight   => "마력 증폭",
        SummonerPerk.Crystallize   => "결정화",
        SummonerPerk.Muster        => "총동원",
        SummonerPerk.NatureRestore => "자연의 회복",
        SummonerPerk.TwinCall      => "쌍둥이 소집",
        _                          => "",
    };

    /// <summary>
    /// 개성 한 줄 설명. <b>정본은 여기 하나다.</b>
    ///
    /// ■ 왜 옮겨 왔나 (2026-09-07)
    ///   소환사 선택 카드(SummonerCandidateCardUI)의 private 함수였는데,
    ///   인게임 보유 줄(RunPerkBarUI)이 같은 문장을 써야 했다. 두 벌이 되면
    ///   수치를 고친 날부터 한쪽만 옛말을 한다.
    ///
    /// ⚠ 수치는 소환사마다 다르다 (SummonerData.PerkValue) — 그래서 인자로 받는다.
    ///   여기에 상수를 적지 말 것.
    /// </summary>
    public static string Describe(this SummonerPerk perk, float value) => perk switch
    {
        SummonerPerk.CheapAffinity => $"친화 종족의 소환 마나 −{value:0.#}",
        SummonerPerk.SwellOnRepeat => "친화 종족을 연달아 부르면 점점 커진다",
        SummonerPerk.RaiseOnDeath  => "몬스터가 죽은 자리에 스켈레톤이 공짜로 일어난다",
        SummonerPerk.AffinityGrade => $"친화 종족의 품질이 {Mathf.RoundToInt(value)}단계 높게 나온다",
        SummonerPerk.PlagueRise    => $"친화 종족이 적을 쓰러뜨리면 {value * 100f:0}% 확률로 그 자리에 좀비가 일어난다",
        SummonerPerk.WildSprint    => $"친화 종족의 이동속도 ×{value:0.##}",
        SummonerPerk.WarCry        => "친화 종족이 싸울수록 공격력이 누적된다",
        SummonerPerk.Plunder       => $"스테이지마다 회복하는 마나 ×{value:0.##}",
        SummonerPerk.Regenerate    => "친화 종족이 맞으면서 체력을 회복한다",
        SummonerPerk.DeepChannel   => $"친화 종족에게 소환력이 ×{value:0.##} 로 더 실린다",
        SummonerPerk.PackBond      => "친화 종족이 사냥할수록 강해진다",
        SummonerPerk.FleshGolem    => "아군 시체가 쌓이면 거인이 일어난다",
        SummonerPerk.SlimeSpit     => $"부른 친화 종족 {Mathf.RoundToInt(value)}마리 중 1마리는 점액을 뱉는 원거리로 선다",
        SummonerPerk.BoneLegion    => $"친화 종족 카드가 부르는 마릿수 +{Mathf.RoundToInt(value)}",
        SummonerPerk.ArcaneMight   => $"최대 마나 10당 모든 몬스터 공/체 +{value * 100f:0}% (소환될 때 정해짐)",
        SummonerPerk.Crystallize   => $"스테이지를 넘길 때 남은 마나의 {value * 100f:0}%가 최대 마나로 쌓인다 " +
                                      $"(최대 +{SummonerPerkRuntime.CrystalCeiling:0})",
        SummonerPerk.Muster        => $"친화 종족 카드가 부르는 마릿수 +{Mathf.RoundToInt(value)}",
        SummonerPerk.NatureRestore => $"스테이지를 넘길 때 최대 마나의 {value * 100f:0}%를 더 회복한다",
        SummonerPerk.TwinCall      => $"카드를 낼 때 옆 라인에도 {Mathf.RoundToInt(value)}마리가 공짜로 선다",
        _                          => "",
    };
}

// ============================================================
//  아이콘 조회 키
//
//  ■ ⚠ 런 특성(perk_*)과 접두사를 나눈다
//    두 enum 에 같은 이름이 있다 — RunPerk.CheapAffinity 와
//    SummonerPerk.CheapAffinity 가 그렇다. 접두사가 같으면 아틀라스에서
//    한쪽이 다른 쪽 그림을 가져간다. SpriteManager.Get 은 **이름으로만**
//    찾으므로 어느 enum 에서 왔는지 구분하지 못한다.
//
//  ■ 왜 SpriteManager 인가 — 무엇이 뜰지 런타임에 정해진다
//    소환사를 고르고 나서야 어떤 개성이 붙는지 정해진다. Creator 가 미리
//    Sprite 를 박아 둘 수가 없다 (런 특성이 같은 이유로 같은 방식이다).
//
//  ⚠ 그림 파일은 런 특성과 **같은 폴더**에 있다 (Icons/RunPerks/).
//    폴더를 나누면 SpriteManagerCreator 에도 줄을 더해야 하는데, 그 줄을
//    빠뜨리면 에러 없이 아이콘만 전부 사라진다. 접두사로 이미 갈린다.
//    굽는 곳은 SummonerPerkIconGenerator 다.
// ============================================================

public static class SummonerPerkIconKey
{
    /// <summary>"sperk_CheapAffinity" 같은 아틀라스 키.</summary>
    public static string Of(SummonerPerk perk) => $"sperk_{perk}";
}

public static class SummonerPerkIconExtensions
{
    /// <summary>
    /// 이 개성의 아이콘 키. 화면은 이 값으로 SpriteManager 에 물어본다.
    ///
    /// ⚠ None 에도 키를 준다 — 부르는 쪽이 분기하지 않게.
    ///   조회는 어차피 실패하고 null 이 나오고, 화면은 그림이 없으면
    ///   칸을 끄면 그만이다.
    /// </summary>
    public static string IconKey(this SummonerPerk perk) => SummonerPerkIconKey.Of(perk);
}
