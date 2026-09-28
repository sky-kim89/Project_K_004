using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MonsterGearData.cs
//  몬스터 장비 한 종류의 정의. Resources/MonsterGearDatabase 에 모은다.
//
//  ■ ⚠ 용사 장비(EquipmentData)와 **다른 물건**이다 — 겸용하지 말 것
//    이름을 Equipment 가 아니라 Gear 로 쓴 이유가 그것이다. 검색이 갈린다.
//    그쪽이 들고 있는 것 중 몬스터에 없는 것:
//      · 강화석·분해              — 몬스터 장비는 **같은 장비 + 영구 골드**로 올린다
//      · OnSoldierDeath 트리거    — 몬스터에겐 병사가 없다
//      · EquipTriggerTarget.Soldiers
//    반대로 여기만 있는 것: **몸 형태 제한**과 **외형 변경**이다.
//
//  ■ 레벨 1~5 + 그 뒤 강화 (사용자 확정, 2026-09-10)
//    Lv1 = BaseOptions, Lv2~Lv5 = Levels[0..3] 이 **하나씩 더해진다**.
//    레벨은 장비 **종류**에 붙는다(MonsterGearInventory) — 여분 사본이 곧 재료다.
//    규칙(재료 수·골드·강화)은 MonsterGearLevelRule 이 정본이다.
//    ⚠ Lv4·Lv5 는 패시브가 열리는 자리다 — 패시브 축은 아직 없다 (2차 작업).
//
//  ■ 얻는 곳은 하나뿐이다 — 런 종료 보상 상자
//    도달 스테이지가 등급을 정하고, 그 등급 안에서 무엇이 나올지는 무작위다
//    (MonsterGearRewardRule). 상점·이벤트로는 나오지 않는다.
//
//  ■ 몸 형태가 갈린다 (Body)
//    인간형은 CharacterBuilder 레이어를 갈아 끼워 **정말로 다른 모습**이 되고,
//    비인간형은 레이어가 없어 색·덩치·장식으로 표현한다.
//    ⚠ 자산 제약이지 설계 선택이 아니다 — MonsterAppearanceBridge 머리 주석 참고.
//
//  ■ 부위(Part)는 겹쳐 낄 수 있다 (사용자 확정, 2026-09-15)
//    투구 둘도 함께 낀다 — 능력치는 전부 받고, 겉모습은 앞 칸 장비가 정한다
//    (MonsterGearRule.BuildVisual). 옛 "같은 부위는 하나뿐" 규칙은 폐기했다.
//
//  ■ 스탯은 **절대값**이다 (사용자 확정, 2026-09-06)
//    ⚠ 그래서 수치를 종족 기본 스탯 범위(HP 55~260 · 공격력 5~25)에 맞춰
//      잡아야 한다. 무심코 +100 을 주면 약한 종족에겐 세 배가 되고 센 종족에겐
//      티도 안 난다. 등급별 기준값은 MonsterGearCreator 가 한 곳에서 정한다.
// ============================================================

/// <summary>이 장비를 걸칠 수 있는 몸 형태. MonsterSpeciesData.BodyType 과 짝이다.</summary>
public enum MonsterGearBody
{
    Humanoid    = 0,
    NonHumanoid = 1,
}

/// <summary>
/// 장비가 차지하는 부위. 같은 부위를 겹쳐 끼면 앞 칸 것이 겉모습을 정한다.
///
/// ⚠ 앞 다섯은 CharacterBuilder 슬롯 이름과 1:1 이다
///   MonsterGearRule 이 이 값으로 UnitAppearanceData 의 어느 칸을 덮을지 고른다.
///   이름을 바꾸면 그 매핑부터 고쳐야 한다.
///
/// ⚠ 뒤 셋은 비인간형 전용이다 — 레이어가 없어 얹을 자리가 없다
///   가죽=색조 · 덩치=크기 · 장식=불빛만. 셋으로 나눈 덕에 비인간형도
///   서로 다른 부위 셋을 채우면 세 가지 변화가 함께 보인다.
/// </summary>
public enum MonsterGearPart
{
    Armor  = 0,
    Helmet = 1,
    Shield = 2,
    Cape   = 3,
    Back   = 4,

    Hide   = 5,   // 비인간형 — 몸 색조
    Bulk   = 6,   // 비인간형 — 덩치
    Charm  = 7,   // 비인간형 — 장식 (불빛만)
}

[CreateAssetMenu(fileName = "Gear_", menuName = "ProjectK/MonsterGearData")]
public class MonsterGearData : ScriptableObject
{
    [Header("식별")]
    [Tooltip("내부 식별자. 보유·장착 상태가 이 값으로 저장되므로 바꾸지 않는다.")]
    public string Id;

    [Tooltip("표시 이름 (보상 상자 · 몬스터 상세).")]
    public string DisplayName;

    [TextArea(2, 3)]
    public string Description;

    [Tooltip("컨셉 한 단어 (\"급소\" · \"방벽\"). 레벨이 무엇을 두껍게 하는지를 말한다.\n" +
             "MonsterGearCreator 가 Theme 에서 굽는다 — 손으로 적지 말 것.")]
    public string Concept = "";

    [Tooltip("목록·칸에 뜨는 그림. 없으면 부위 기호로 대신 그린다.")]
    public Sprite Icon;

    [Header("등급")]
    [Tooltip("도달 스테이지가 정한다 (MonsterGearRewardRule). 슬롯 수와는 무관하다 —\n" +
             "칸 수를 정하는 것은 **몬스터의 품질**이다.")]
    public UnitGrade Grade = UnitGrade.Normal;

    [Header("장착 조건")]
    public MonsterGearBody Body = MonsterGearBody.Humanoid;

    [Tooltip("차지하는 부위. 겹쳐 낄 수 있고, 겉모습은 앞 칸 장비가 정한다.")]
    public MonsterGearPart Part = MonsterGearPart.Armor;

    [Header("능력치")]
    [Tooltip("Lv1 에 이미 붙어 있는 것. 절대값(체력·공격력)은 종족 기본 스탯 범위\n" +
             "(HP 55~260 · 공격력 5~25)에 맞춰 잡을 것 — MonsterGearCreator 가 등급 예산으로 굽는다.")]
    public List<GearOption> BaseOptions = new();

    [Tooltip("Lv2~Lv5 가 각각 더하는 것. ⚠ 길이는 MonsterGearLevelRule.MaxLevel − 1 (= 4).")]
    public List<GearLevelStep> Levels = new();

    [Header("패시브 (Lv4 · Lv5)")]
    [Tooltip("Lv4 에 열리는 패시브. 선천·융합과 같은 것이면 **각성**할 수 있다 (PassiveAwakening).")]
    public SpeciesPassive Lv4Passive = SpeciesPassive.None;

    [Tooltip("Lv5 에 열리는 패시브.")]
    public SpeciesPassive Lv5Passive = SpeciesPassive.None;

    /// <summary>그 레벨이 여는 패시브. Lv4·Lv5 가 아니면 None.</summary>
    public SpeciesPassive PassiveAt(int level) => level switch
    {
        4 => Lv4Passive,
        5 => Lv5Passive,
        _ => SpeciesPassive.None,
    };

    // ──────────────────────────────────────────────────────────
    // ■ 외형 — 인간형
    // ──────────────────────────────────────────────────────────

    [Header("외형 — 인간형 전용")]
    [Tooltip("CharacterBuilder 슬롯에 넣을 이름 (예: \"DarkKnight\").\n" +
             "⚠ 벤더 에셋(PixelFantasy)에 실제로 있는 이름이어야 한다 — 없으면 조용히 안 그려진다.\n" +
             "Sprites/Armor · Sprites/Helmet · Sprites/Shield · Sprites/Cape · Sprites/Back 참고.")]
    public string AppearanceName = "";

    // ──────────────────────────────────────────────────────────
    // ■ 외형 — 비인간형
    // ──────────────────────────────────────────────────────────

    [Header("외형 — 비인간형 전용")]
    [Tooltip("몸에 입히는 색조 (Part = Hide). 흰색이면 원래 색 그대로다.\n" +
             "SpriteRenderer.color 라 정점 색이다 — 머티리얼이 갈리지 않아 배칭이 안 깨진다.")]
    public Color Tint = Color.white;

    [Tooltip("덩치 배율에 더하는 값 (Part = Bulk). 0.06 = 6% 커진다.\n" +
             "⚠ 히트박스·분리 반경도 함께 커진다 — UnitSizeComponent.Radius 가 localScale 에서 나온다.\n" +
             "그래서 상한을 낮게 잡는다. 크게 주면 라인에서 서로를 밀어낸다.")]
    [Range(0f, 0.2f)]
    public float ScaleBonus = 0f;

    // ── 조회 ─────────────────────────────────────────────────

    /// <summary>이 종족이 걸칠 수 있는 장비인가.</summary>
    public bool Fits(MonsterSpeciesData species)
        => species != null && Fits(species.BodyType);

    public bool Fits(MonsterBodyType bodyType)
        => bodyType == MonsterBodyType.Humanoid
            ? Body == MonsterGearBody.Humanoid
            : Body == MonsterGearBody.NonHumanoid;

    /// <summary>그 레벨이 <b>새로</b> 여는 것. Lv1 이면 BaseOptions. 범위 밖이면 빈 목록.</summary>
    public IReadOnlyList<GearOption> StepOf(int level)
    {
        if (level <= 1) return BaseOptions;

        int i = level - 2;
        return Levels != null && i < Levels.Count && Levels[i] != null
             ? Levels[i].Options
             : NoOptions;
    }

    static readonly List<GearOption> NoOptions = new();

    /// <summary>
    /// Lv1 부터 그 레벨까지 붙은 것을 <b>종류별로 합쳐</b> 담는다.
    /// 같은 종류는 한 줄이 된다 — "체력 +20 · 체력 +10" 이 아니라 "체력 +30".
    ///
    /// ⚠ 특이 옵션은 따로 합친다 — 합쳐 버리면 색(특이)이 사라진다.
    /// </summary>
    public void CollectUpTo(int level, List<GearOption> into)
    {
        into.Clear();

        for (int lv = 1; lv <= level; lv++)
        {
            IReadOnlyList<GearOption> step = StepOf(lv);
            for (int i = 0; i < step.Count; i++) Merge(into, step[i]);
        }
    }

    static void Merge(List<GearOption> into, in GearOption o)
    {
        for (int i = 0; i < into.Count; i++)
        {
            if (into[i].Stat != o.Stat || into[i].Quirk != o.Quirk) continue;

            var merged = into[i];
            merged.Value += o.Value;
            into[i] = merged;
            return;
        }
        into.Add(o);
    }

    static readonly List<GearOption> LineBuffer = new(8);

    /// <summary>
    /// 그 레벨의 능력치 한 줄 요약 ("공격력 +8 · 체력 +40").
    ///
    /// ⚠ 손으로 적지 말 것 — 서식은 GearOptionText 가 정본이다.
    /// ⚠ 카드 비용(마나)은 여기 안 나온다 — 마나는 아이콘이다 (GearOptionText.Describe).
    /// </summary>
    public string StatLine(int level = 1)
    {
        CollectUpTo(level, LineBuffer);
        return GearOptionText.Join(LineBuffer);
    }

    /// <summary>부위 이름 — 칸에 "무엇을 끼우는 자리인가" 를 적을 때 쓴다.</summary>
    public static string NameOf(MonsterGearPart part) => part switch
    {
        MonsterGearPart.Armor  => "갑옷",
        MonsterGearPart.Helmet => "투구",
        MonsterGearPart.Shield => "방패",
        MonsterGearPart.Cape   => "망토",
        MonsterGearPart.Back   => "등짐",
        MonsterGearPart.Hide   => "가죽",
        MonsterGearPart.Bulk   => "덩치",
        MonsterGearPart.Charm  => "장식",
        _                      => "",
    };
}
