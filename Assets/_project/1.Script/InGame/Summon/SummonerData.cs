using System;
using UnityEngine;
using UnityEngine.Serialization;

// ============================================================
//  SummonerData.cs
//  소환사(메인 캐릭터) 한 명의 정의. Resources/SummonerDatabase 에 모은다.
//
//  ■ 소환사 = 마왕성이다 (다만 HP 축은 갈라졌다 — 2026-09-04)
//    별도 코어 엔티티는 여전히 없다. 하지만 **마왕성 체력은 이 캐릭터의 HP 가
//    아니다** — 성벽을 통과한 용사 1기당 1 씩 줄어드는 별도 카운터다
//    (RunCoreData · MaxCoreHp). 용사는 소환사를 조준하지 않는다.
//
//    ⚠ 왜 갈랐나
//      옛 규칙은 용사가 성벽까지 와서 소환사를 때리는 것이었다. 그러면
//      스테이지가 오를수록 공격력이 커져 **한 마리만 새어 나가도 즉사**한다.
//      막아 낸 정도와 결과 사이에 아무 관계가 없어진다.
//
//    아래 MaxHp(전투 스탯)는 스킬·상태효과가 참조하므로 남아 있지만,
//    런 종료 판정은 보지 않는다.
//
//  ■ 외형은 프리팹으로 미리 굽는다
//    원작 히어로는 런타임에 Body/Head/장비 스프라이트를 합성했다.
//    소환사는 그 방식을 쓰지 않는다 — 정해진 의상·무기를 착용한
//    완성 프리팹을 PoolKey 로 꺼내 쓴다. 스킨은 지금 넣지 않는다.
//
//  ■ 3대 전투 스탯 (캐릭터 선택창에 노출)
//      패기 Vigor        → 밀어내기 · 과부하 저항 (SummonerVigorRule)
//      체력 Vitality     → 마왕성 HP (MaxCoreHp)
//      지능 Intelligence → 소환력 (SummonPower) + 마나 그릇 (MaxMana)
//
//    ⚠ 셋은 서로 다른 것을 맡아야 한다 (사용자 확정, 2026-09-07)
//      지능이 "많이"(마나 그릇)와 "세게"(소환력)를 둘 다 먹는다. 나머지 둘이
//      공격력·체력을 맡으면 소환력의 열화판이 되어 고를 이유가 없어진다.
//      옛 '힘'이 그 상태였다 — 평타 DPS 6~13 과 몬스터 공격력 +4% 가 전부였다.
//
//    ⚠ 지능이 곧 소환력이지만, 소환력은 원작 지휘력과 구조가 다르다.
//      지휘력 : 병사 스탯 = 장군 스탯 × 비율        ← 종속 파생
//      소환력 : 몬스터 스탯 = 종족 기본 스탯 + 보너스 ← 가산
//      합성은 MonsterStatComposer 한 곳에서만 한다.
//
//  ■ 레벨 없음 (캐릭터·유닛 이야기다. 카드는 다르다)
//    소환사도 필드의 몬스터도 인게임 레벨이 없다. Lv 을 보여 주는 건 적(용사)뿐이다.
//    ⚠ **카드 레벨**은 별개다 — 같은 카드를 중복 획득하면 그 카드가 오른다
//      (CardLevelRule). 유닛이 전투 중에 성장하는 게 아니라, 손에 든 카드가
//      좋아지는 것이라 위 규칙과 어긋나지 않는다.
//
//  ■ 이 소환사가 무엇을 데리고 들어가는가 = StarterCards
//    ⚠ 런 시작 전 덱 편성은 없다 (설계 변경, 2026-08-27)
//      예전에는 도감에서 해금된 종족을 골라 덱을 짜고 들어갔다. 지금은
//      **소환사에 붙박인 카드**만 들고 시작하고, 나머지는 런을 돌며 줍는다.
//      도감(MonsterCodexData)은 다시 순수한 기록이 됐다 — 편성 풀이 아니다.
//
//  ■ 친화도 (Affinities) — 캐릭터의 색깔
//    "이 소환사는 이 몬스터를 잘 다룬다" 를 소환력 배율로 표현한다.
//    편성 제한이 아니라 보너스라, 주운 카드는 무엇이든 쓸 수 있으면서도
//    캐릭터마다 잘 굴러가는 조합이 달라진다.
//    실제 배율은 SummonerAffinityRule 이 소유한다.
//
//  ■ 개성 (Perk) — 숫자로 안 끝나는 규칙 하나
//    소환 파이프라인에 끼어드는 동작. SummonerPerk 파일 머리에
//    "왜 TraitData 로는 안 되는가" 를 적어 뒀다.
// ============================================================

/// <summary>
/// 친화 대상 한 줄. 종족 ID 로 콕 집거나, 특징(MonsterTrait)으로 묶어서 지정한다.
///
/// ⚠ 둘 중 하나만 채운다
///   드루이드처럼 "야수 전부" 는 특징으로, 스컬 킹처럼 "스켈레톤만" 은 ID 로 잡는다.
///   ID 를 채우면 특징은 무시된다 — 더 좁은 쪽이 이긴다.
/// </summary>
[Serializable]
public struct SummonerAffinity
{
    [Tooltip("친화 종족 ID (MonsterSpeciesData.Id). 비우면 아래 특징으로 판정한다.")]
    public string SpeciesId;

    [Tooltip("이 특징을 **모두** 가진 종족을 친화로 본다. None 이면 사용하지 않는다.")]
    public MonsterTrait Traits;

    public bool Matches(MonsterSpeciesData species)
    {
        if (!string.IsNullOrEmpty(SpeciesId))
        {
            if (species.Id == SpeciesId) return true;

            // ⚠ 계보를 따라 올라간다 — 업그레이드도 친화다
            //   "스켈레톤을 잘 다룬다" 는 해골 방패병·해골 술사에도 걸려야 한다.
            //   업그레이드마다 친화 목록에 한 줄씩 더 적게 만들면, 종족을
            //   추가할 때마다 소환사 12명을 전부 손봐야 한다.
            return species.RootSpecies.Id == SpeciesId;
        }

        if (Traits == MonsterTrait.None) return false;
        return (species.Traits & Traits) == Traits;
    }
}

/// <summary>
/// 친화도가 실제로 무엇을 바꾸는가 — 모든 소환사가 공유하는 공통 규칙.
///
/// ⚠ 소환사마다 다른 배율을 주지 않는다
///   캐릭터 차이는 "무엇이 친화인가" 와 개성(Perk)이 만든다. 배율까지 캐릭터마다
///   다르면 강한 소환사·약한 소환사가 생겨 선택이 사라진다
///   (3대 스탯 합계를 19 로 맞춘 것과 같은 이유).
/// </summary>
public static class SummonerAffinityRule
{
    // ⚠ 친화는 **순수 보너스**다 — 비친화에 벌점을 주지 않는다 (2026-09-04 확정)
    //   옛 값은 1.75 / 0.55 였다. 그러면 두 배율의 격차가 3.2배라
    //   "친화가 아닌 카드는 주워도 쓰레기" 가 되어, 3택이 사실상 1택으로
    //   줄고 시너지 조합을 짤 여지가 사라진다. 캐릭터의 색깔은 격차가 아니라
    //   **어느 쪽이 친화인가**로 드러나면 된다.
    //
    //   지금 격차는 1.2배다. 친화 종족이 확실히 낫지만, 비친화 카드도
    //   시너지·품질이 맞으면 고를 만하다.

    /// <summary>친화 종족이 소환력을 받는 배율.</summary>
    public const float AffinitySummonPower = 1.2f;

    /// <summary>친화가 아닌 종족이 소환력을 받는 배율. 벌점 없음 — 보너스가 없을 뿐이다.</summary>
    public const float NeutralSummonPower = 1f;
}

[CreateAssetMenu(fileName = "Summoner_", menuName = "ProjectK/SummonerData")]
public class SummonerData : ScriptableObject
{
    [Header("식별")]
    [Tooltip("내부 식별자. 세이브에 남으므로 한 번 정하면 바꾸지 않는다.")]
    public string Id;

    [Tooltip("표시 이름 (캐릭터 선택창).")]
    public string DisplayName;

    [TextArea(2, 4)]
    [Tooltip("캐릭터 선택창 설명. 어떤 플레이를 하는 소환사인지 한 줄로.")]
    public string Description;

    [Header("외형")]
    [Tooltip("미리 빌드해 둔 완성 프리팹의 풀 키 (PoolType.Unit).\n" +
             "의상·무기를 이미 착용한 상태의 프리팹이다.")]
    public string PoolKey;

    [Tooltip("캐릭터 선택창 초상화.")]
    public Sprite Portrait;

    // ──────────────────────────────────────────────────────────
    // ■ 외형 조립 — 프리팹만으로는 캐릭터가 보이지 않는다
    //
    //   Summoner.prefab 은 General.prefab 을 복제한 것이라 스프라이트가 비어 있다.
    //   원작 장수는 스폰될 때 UnitAppearanceBridge.ApplyAlly 가 몸·머리·장비를
    //   합성해 채운다. 소환사만 그 호출이 없어서 **투명 인간으로 서 있었다.**
    //
    //   아래 세 값이 그 합성의 입력이다. 전부 결정적이라 같은 소환사는 언제나
    //   같은 모습으로 나온다 — "미리 정해 둔 의상·무기" 라는 기획과 결과가 같다.
    //   (런타임 스킨 교체 시스템은 여전히 없다)
    // ──────────────────────────────────────────────────────────

    [Tooltip("외형 시드. 비우면 Id 를 쓴다.\n" +
             "이 문자열이 종족·머리·의상 조합을 통째로 결정한다 — " +
             "마음에 드는 모습이 나올 때까지 바꿔 가며 고르는 값이다.")]
    public string AppearanceSeed;

    [Tooltip("외형에 쓸 직업. 무기와 옷차림이 여기서 갈린다.\n" +
             "전투 스탯과는 무관하다 — 소환사에게는 직업 개념이 없다.")]
    public UnitJob AppearanceJob = UnitJob.Mage;

    [Tooltip("외형에 쓸 등급. 높을수록 장비가 화려해진다.\n" +
             "전투 스탯과는 무관하다.")]
    public UnitGrade AppearanceGrade = UnitGrade.Epic;

    [Tooltip("몬스터 모습으로 서는 소환사 (슬라임 킹 = 슬라임). 비우면 위 인간형 합성을 쓴다.\n" +
             "⚠ 비인간형 종족이어야 한다 — 몸이 통짜 라이브러리로 바뀐다 (MonsterAppearanceBridge).")]
    public MonsterSpeciesData AppearanceSpecies;

    [Tooltip("머리 위 표식 — 몬스터 모습일 때만 (슬라임 킹 = 왕관).")]
    public MonsterMark AppearanceMark = MonsterMark.None;

    [Tooltip("몬스터 모습일 때의 몸집 배율 (슬라임 킹 = 기본 슬라임의 커다란 판).")]
    public float AppearanceScale = 1f;

    /// <summary>외형 합성에 넘길 시드. 비워 두면 Id 가 대신 쓰인다.</summary>
    public string ResolvedAppearanceSeed
        => string.IsNullOrEmpty(AppearanceSeed) ? Id : AppearanceSeed;

    // ──────────────────────────────────────────────────────────
    // ■ 3대 전투 스탯 — 선택창에 그대로 노출된다
    // ──────────────────────────────────────────────────────────

    // ⚠ 셋 다 상한이 10 이다
    //   캐릭터 자체가 이 값을 넘을 수는 없다. 10 을 넘기려면 유물·특성·장비 같은
    //   **외부 강화**를 얹어야 한다 — 그건 여기가 아니라 스탯 레이어에서 붙는다.
    //   그래서 이 필드는 "이 캐릭터의 타고난 그릇" 이고, 합계로 캐릭터 간 강약이
    //   드러나면 안 된다. 배분이 곧 개성이다 (공격형 8/8/3 처럼).

    /// <summary>3대 전투 스탯의 상한. 이 위는 외부 강화로만 올라간다.</summary>
    public const float MaxCoreStat = 10f;

    [Header("3대 전투 스탯 (최대 10 — 그 이상은 외부 강화로만)")]
    [Tooltip("패기 — 부린 것들을 얼마나 억세게 밀어붙이는가. " +
             "몬스터 평타의 밀어내기와 과부하 저항을 정한다 (SummonerVigorRule).")]
    [Range(1f, MaxCoreStat)]
    [FormerlySerializedAs("Strength")]
    public float Vigor = 5f;

    [Tooltip("체력 — 마왕성이 몇 번의 통과를 견디는가를 정한다 (MaxCoreHp).")]
    [Range(1f, MaxCoreStat)]
    public float Vitality = 5f;

    [Tooltip("지능 — 소환력을 대변한다. 소환한 몬스터에게 더해지는 보너스의 크기.")]
    [Range(1f, MaxCoreStat)]
    public float Intelligence = 5f;

    // ──────────────────────────────────────────────────────────
    // ■ 그 외 스탯
    // ──────────────────────────────────────────────────────────

    // ⚠ 소환사의 사거리는 용사 기준으로 재면 안 된다
    //   용사 최장 사거리는 궁수 9.9 다(GameplayConfig.ArcherRange). 그 근처로 잡으면
    //   성벽 뒤에 못 박힌 채 아무것도 못 때린다 — 용사는 성벽(x -15)에서
    //   멈추고 소환사는 성 안뜰(x ≈ -20.2)에 서므로, 사거리 10 이면
    //   공격이 성벽 앞 한 줄에만 닿는다.
    //
    //   전장은 대략 x ∈ [-15, +21] 이다. 사거리 24 면 소환사에서
    //   x ≈ +3.8 까지, 전장의 절반 남짓을 덮는다.

    /// <summary>용사 최장 사거리(궁수 9.9). 소환사는 이보다 월등히 길어야 한다.</summary>
    public const float HeroMaxRangeReference = 10f;

    [Header("전투")]
    [Tooltip("공격 사거리. 소환사마다 다르게 잡는 개성의 축이다.\n" +
             "성벽 뒤에서 싸우므로 기본적으로 넓다.")]
    [Min(HeroMaxRangeReference)]
    public float AttackRange = 24f;

    [Tooltip("발사체(마법구) 속도. 사거리를 늘리면 이 값도 같이 올린다.\n" +
             "사거리 ÷ 이 값 = 착탄까지 걸리는 시간(초).")]
    [Min(1f)]
    public float ProjectileSpeed = 22f;

    [Tooltip("초당 공격 횟수.")]
    public float AttackSpeed = 0.8f;

    [Tooltip("방어율 0~1.")]
    [Range(0f, 0.95f)]
    public float Defense = 0.1f;

    // ──────────────────────────────────────────────────────────
    // ■ 마나 — 지능이 그릇을 정하고, 스테이지마다 일부 찬다 (2026-08-28 개편)
    //
    //   옛 규칙: 런 시작에 StartMana 를 한 번 주고 끝. 완주한 몬스터가 환수.
    //   그 구조에서는 마나 손실이 곧 **사망률**이었고, 사망률을 낮추는 최선책이
    //   "더 많이 소환하기" 였다 — 아끼면 손해, 쏟아부으면 이득이라 자원 결정이
    //   뒤집혀 있었다. 무손실 승리를 반복하면 런이 갈수록 쉬워지기까지 했다.
    //
    //   지금은 그릇(MaxMana)이 있고 매 스테이지 일부만 채워진다.
    //   한 판에 쓸 수 있는 양이 정해지므로 "이번 판에 얼마를 걸까" 가 결정이 되고,
    //   지능이 소환력뿐 아니라 **판을 굴리는 폭**까지 정하게 된다.
    // ──────────────────────────────────────────────────────────

    //   ⚠ 그릇을 2.2배 좁혔다 (2026-09-04) — 바닥 40→20 · 지능당 9→4
    //     견습(지능 8)이 112 를 들고 시작해 1스테이지부터 카드를 열댓 장
    //     쏟아부을 수 있었다. 그러면 "쌓아 올린다" 가 아니라 "첫 판에 다 낸다" 가
    //     최적해가 되어, 라인 복귀로 물량이 불어나는 그림이 안 보인다.
    //     이제 견습은 52 로 시작한다 — 첫 판에 두세 장, 나머지는 아껴야 한다.
    //     적 부대 수를 2~5 → 1~4 로 깎은 것과 같은 폭이다. 한쪽만 고치지 말 것.
    //
    //   ⚠ 여기 기본값을 고쳐도 **이미 만들어진 SO 에셋은 안 바뀐다**
    //     Assets/_project/Data/Summoners/*.asset 에 값이 구워져 있고,
    //     SummonerCreator 는 마나를 적지 않아 다시 구워도 그대로다.
    //     둘을 함께 고칠 것.

    [Header("소환")]
    [Tooltip("지능과 무관하게 깔리는 마나 바닥값.")]
    [Min(0f)]
    public float BaseMana = 20f;

    [Tooltip("최대 마나 = 바닥값 + 지능 × 이 값.")]
    [Min(0f)]
    public float ManaPerIntelligence = 4f;

    // ──────────────────────────────────────────────────────────
    // ■ 회복량 = **지능 + 남은 마나의 일부** (2026-09-04 개편)
    //
    //   옛 규칙: 회복량 = 최대 마나 × 25%. 그릇 크기만 보고 정해지므로
    //   판을 어떻게 굴렸든 매 스테이지 같은 양이 들어왔다 — 바닥까지 쏟아부은
    //   사람과 아껴 둔 사람이 똑같이 받았고, 그러면 "전부 낸다" 가 아무 대가도
    //   치르지 않는 최적해가 된다.
    //
    //   지금은 두 몫이다.
    //     지능 몫 — 바닥을 쳐도 이만큼은 들어온다 (회복의 하한, 판을 되살리는 몫)
    //     보유 몫 — 남겨 둔 마나에 이자가 붙는다 (아낀 것에 대한 보상)
    //
    //   ⚠ 이자는 그릇을 넘지 못한다 (SummonManaData.RegenForStage 가 자른다).
    //     상한이 없으면 이번엔 "아끼기만 한다" 가 최적해가 되어 방향만 뒤집힌다.
    //
    //   ⚠ 옛 ManaRegenRatio 는 없앴다. SO 에셋에 남은 그 키는 무시된다.
    // ──────────────────────────────────────────────────────────

    [Tooltip("스테이지를 넘길 때 지능 1당 돌아오는 마나. 회복의 하한이다.")]
    [Min(0f)]
    public float ManaRegenPerIntelligence = 1f;

    [Tooltip("스테이지를 넘길 때 **남아 있는** 마나의 몇 %가 이자로 붙는가.")]
    [Range(0f, 1f)]
    public float ManaRegenHoldRatio = 0.1f;

    /// <summary>
    /// 마나 그릇. 런 시작에는 이만큼 가득 차 있다.
    ///
    /// ⚠ 상한이자 회복의 기준이다 — 두 곳에서 따로 계산하지 말 것.
    /// </summary>
    public float MaxMana => BaseMana + Intelligence * ManaPerIntelligence;

    /// <summary>
    /// 스테이지 하나를 넘길 때 돌아오는 양.
    ///
    /// ⚠ 현재 잔량에 의존한다 — 회복량을 다른 곳에서 다시 계산하지 말 것.
    /// </summary>
    public float ManaRegenFor(float currentMana)
        => Intelligence * ManaRegenPerIntelligence
         + Mathf.Max(0f, currentMana) * ManaRegenHoldRatio;

    /// <summary>선택 화면 미리보기 — 그릇이 가득 찬 상태의 회복량(= 이 소환사의 최대 회복).</summary>
    public float ManaRegenAtFull => ManaRegenFor(MaxMana);

    // ⚠ 기본 6칸이다 (2026-09-04 축소, 8 → 6)
    //   특성 '확장 편성'(RunPerk.ExtraSlots)이 +2 해서 8칸이 된다 — 칸을 늘리는 것이
    //   그 특성의 존재 이유이므로, 기본이 이미 8이면 특성이 사치품이 된다.
    //
    //   ⚠ 시너지 문턱을 함께 볼 것
    //     7종 계열(언데드·숲)의 금 문턱이 7 이라 6칸으로는 카드만으로 못 닿는다.
    //     '확장 편성'(8칸) 이나 카운트 가산(시너지 강화 카드 · 특성 '편중')이 있어야
    //     열리는 구조다. 의도된 것이니 문턱을 낮추는 쪽으로 되돌리지 말 것.
    //
    //   ⚠ SO 에셋 12개에도 구워져 있다 (Data/Summoners/*.asset) — 함께 고쳤다.

    //   ⚠ 2026-09-11 부터 **소환사마다 다르다** (사용자 지시 — 4 · 5 · 6)
    //     좁은 소환사(슬라임 킹·스컬 킹·역병 술사·트롤 조련사·오크 킹)는 적은 카드를 두껍게,
    //     넓은 소환사(고블린 두목·드루이드·비스트마스터)는 여러 종족을 넓게 굴린다.
    //     정본은 SummonerCreator 로스터다. 유물 '전열 확장'(+2)·특성 '확장 편성'(+2)을
    //     얹어도 RunPerkRule.MaxDeckSlots(8)에서 잘린다.

    /// <summary>
    /// 기준 칸 수. 선택 화면이 이보다 많으면 초록 · 적으면 주황으로 칸 수를 칠한다.
    /// </summary>
    public const int StandardDeckSlots = 5;

    [Tooltip("카드 바에 놓을 수 있는 칸 수 (소환사마다 4~6). " +
             "유물 '전열 확장'·특성 '확장 편성'이 더한다 — 상한 RunPerkRule.MaxDeckSlots.")]
    [Range(1, 10)]
    public int DeckSlots = StandardDeckSlots;

    /// <summary>
    /// 런을 시작할 때의 칸 수 = 소환사 + 유물 '전열 확장' (상한 8).
    /// 선택 화면이 이 값을 보여 준다 — 마왕성 체력(MaxCoreHp)이 유물을 얹어 보여 주는 것과 같다.
    /// ⚠ 특성(확장 편성·봉인된 칸)은 RunPerkRule.DeckSlotsFor 가 더한다.
    /// </summary>
    public int StartDeckSlots
        => Mathf.Min(DeckSlots + RelicTreeApplier.GetSystemInt(RelicSystemEffect.DeckSlotBonus),
                     RunPerkRule.MaxDeckSlots);

    // ══════════════════════════════════════════════════════════
    //  시그니처 스킬 — 마나 없이 쓰는 소환사 고유 기술
    // ══════════════════════════════════════════════════════════
    //
    //  ■ 스킬 카드는 없앴다 (2026-09-04, 사용자 확정)
    //    3택으로 스킬을 줍던 방식은 덱 8칸을 먹어 시너지 조합을 좁혔다.
    //    이제 소환사마다 **무료로 쓰는 하나**를 들고 시작하고, 3택에서
    //    줍는 것은 그 스킬을 강화하는 카드 한 장뿐이다.
    //
    //  ■ 제한이 곧 세기다 — **스테이지당 횟수 하나로 통일** (사용자 확정, 2026-09-04)
    //    마나를 안 내므로 세기는 몇 번 쓸 수 있는가로만 조절한다.
    //      센 스킬 → 스테이지당 1회
    //      가벼운 스킬 → 스테이지당 2회
    //
    //    ⚠ 한때 "런 중 총 N회" 도 있었다. 없앴다 —
    //      두 종류가 있으면 화면이 "판당인가 런당인가" 를 먼저 설명해야 하고,
    //      런 제한형은 세이브까지 따로 타야 했다. 세기 차이는 횟수만으로 충분하다.

    [Header("시그니처 스킬")]
    [Tooltip("소환사 고유 스킬. None 이면 스킬이 없다.")]
    public ActiveSkillId SignatureSkill = ActiveSkillId.None;

    [Tooltip("시그니처 스킬의 표시 이름. 비우면 스킬 SO 의 이름을 쓴다.\n" +
             "'권속 소환' 처럼 여러 소환사가 나눠 쓰는 스킬은 소환사마다 이름을 준다 — " +
             "무엇을 부르는지가 이름에 보인다 (SignatureSkillDisplay).")]
    public string SignatureName;

    [Tooltip("스테이지당 사용 횟수. 매 판 리필된다. 0 이면 스킬이 없는 소환사다.")]
    [Min(0)]
    public int SkillUsesPerStage;

    [Tooltip("소환을 동반하는 시그니처 스킬이 부를 종족.\n" +
             "SummonSignature 는 이것을 그대로 부르고, 비석 강림은 낙하 지점마다 세운다.\n" +
             "소환이 없는 스킬이면 비워 둔다.")]
    public MonsterSpeciesData SignatureSummonSpecies;

    [Tooltip("한 번에 부르는 마리 수.")]
    [Min(1)]
    public int SignatureSummonCount = 1;

    /// <summary>스킬을 갖고 있나.</summary>
    public bool HasSignatureSkill
        => SignatureSkill != ActiveSkillId.None && SkillUsesPerStage > 0;

    /// <summary>한 판에 쓸 수 있는 총 횟수.</summary>
    public int SkillUses => SkillUsesPerStage;

    /// <summary>
    /// 선택 화면에 서는 순서. 작을수록 앞이다 (0 = 첫 칸).
    ///
    /// ■ 왜 필요한가 — 이름순은 의도가 아니다
    ///   예전에는 카탈로그가 Id 를 사전순으로 정렬했다. 그러면
    ///   beastmaster · butcher · druid … 순이 되어 **견습 소환사가 7번째**에
    ///   섰다. 처음 켠 사람이 좌우로 넘겨 가며 입문용 캐릭터를 찾아야 했다.
    ///
    ///   순서는 난이도이자 안내다. 견습이 0번이고, 뒤로 갈수록 깊은 캐릭터가
    ///   선다 — 그 배열의 정본은 SummonerCreator 의 스펙 배열이고,
    ///   이 값은 거기서 굴러 나온 자리 번호다.
    ///
    /// ⚠ 손으로 채우지 말 것. SummonerCreator 가 배열 순서대로 넣는다.
    /// </summary>
    [Tooltip("선택 화면 순서. SummonerCreator 가 채운다 — 손대지 말 것.")]
    public int ListOrder;

    /// <summary>
    /// 해금 조건. **비어 있으면 기본 해금**이다 (견습 소환사).
    ///
    /// 여러 개면 **전부** 만족해야 열린다. 판정은 SummonerUnlockRule 하나가 한다 —
    /// 화면마다 다시 계산하지 말 것.
    ///
    /// ⚠ 영구 기록만 조건으로 쓴다
    ///   StageProgressData 는 환생 때 지워지고 이 게임은 패배가 곧 환생이라,
    ///   그 값으로 걸면 죽는 순간 다시 잠긴다. 쓸 수 있는 것은 도감
    ///   (MonsterCodexData)과 ReincarnationData.BestStage 둘뿐이다.
    /// </summary>
    [Tooltip("비우면 기본 해금. 여러 개면 전부 만족해야 열린다.")]
    public SummonerUnlock[] Unlocks = System.Array.Empty<SummonerUnlock>();

    // ──────────────────────────────────────────────────────────
    // ■ 시작 카드 — 이 소환사가 붙박이로 데리고 들어가는 것
    // ──────────────────────────────────────────────────────────

    [Header("시작 카드 (붙박이)")]
    [Tooltip("런 시작 시 카드 바에 이미 들어 있는 몬스터. 앞칸부터 순서대로 놓인다.\n" +
             "⚠ 도감 해금과 무관하다 — 이건 캐릭터의 정체성이지 수집물이 아니다.")]
    public MonsterSpeciesData[] StarterMonsters = new MonsterSpeciesData[0];

    [Tooltip("런 시작 시 함께 들고 가는 스킬 카드. 없어도 된다.")]
    public SkillCardData[] StarterSkills = new SkillCardData[0];

    // ──────────────────────────────────────────────────────────
    // ■ 친화도 · 개성
    // ──────────────────────────────────────────────────────────

    [Header("친화도")]
    [Tooltip("이 소환사가 잘 다루는 몬스터. 소환력이 1.2배로 실린다.\n" +
             "여기 없는 종족은 소환력을 그대로(1배) 받는다 — 벌점은 없다.")]
    public SummonerAffinity[] Affinities = new SummonerAffinity[0];

    [Header("개성")]
    [Tooltip("이 소환사만의 규칙. 실행은 SummonerPerkRuntime 이 한다.")]
    public SummonerPerk Perk = SummonerPerk.None;

    [Tooltip("개성의 수치. 무엇을 뜻하는지는 개성마다 다르다 " +
             "(할인액·배율·재생량 …). SummonerPerkRuntime 의 각 분기 주석 참고.")]
    public float PerkValue = 1f;

    /// <summary>이 종족이 친화 대상인가.</summary>
    public bool IsAffinity(MonsterSpeciesData species)
    {
        for (int i = 0; i < Affinities.Length; i++)
            if (Affinities[i].Matches(species)) return true;

        return false;
    }

    /// <summary>이 종족이 소환력을 받는 배율. 친화 여부 하나로 갈린다.</summary>
    public float SummonPowerMultFor(MonsterSpeciesData species)
        => IsAffinity(species)
            ? SummonerAffinityRule.AffinitySummonPower
            : SummonerAffinityRule.NeutralSummonPower;

    // ──────────────────────────────────────────────────────────
    // ■ 3대 스탯 → 실제 전투 스탯 환산
    //   계수는 밸런싱 대상이라 여기 한곳에만 둔다.
    //   "패기 1 = 공격력 몇" 을 여러 군데에 박으면 밸런스를 못 잡는다.
    // ──────────────────────────────────────────────────────────

    [Header("환산 계수 (밸런싱)")]
    [Tooltip("소환사 평타 = 패기 × 이 값. " +
             "⚠ 곁다리다 — 패기의 본 역할은 밀어내기·과부하 저항이다 (SummonerVigorRule).")]
    [FormerlySerializedAs("AttackPerStrength")]
    public float AttackPerVigor = 2f;

    [Tooltip("최대 HP = 체력 × 이 값")]
    public float HpPerVitality = 50f;

    [Tooltip("소환력 = 지능 × 이 값")]
    public float SummonPowerPerIntelligence = 1f;

    // ⚠ 지능은 이제 두 가지를 정한다 — 소환력(개체의 세기)과 마나 그릇(판의 폭).
    //   같은 스탯이 질과 양을 함께 쥐고 있으므로, 밸런싱 때 한쪽만 만지면
    //   다른 쪽이 조용히 따라 움직인다는 것을 염두에 둘 것.

    // ──────────────────────────────────────────────────────────
    // ■ 마왕성 체력 — **통과당할 수 있는 마릿수** (2026-09-04 개편)
    //
    //   옛 규칙: 용사가 성벽까지 걸어와 소환사를 **때려서** MaxHp 를 깎았다.
    //   그러면 스테이지가 오를수록 용사의 공격력이 커져 어느 순간부터
    //   **한 마리만 새어 나가도 즉사**한다. 막아 낸 정도와 결과가 무관해진다.
    //
    //   지금은 성벽에 닿은 용사 1기당 1 이다. 공격력이 아무리 세도 한 마리는
    //   1 이고, 20 을 잃으려면 20 마리를 놓쳐야 한다.
    //
    //   ⚠ MaxHp(전투 스탯)와 다른 값이다. 소환사는 이제 조준당하지 않으므로
    //     MaxHp 는 사실상 쓰이지 않지만, 스킬·상태효과가 참조하므로 남겨 둔다.
    // ──────────────────────────────────────────────────────────

    // ■ ⚠ 마왕성 체력은 **적의 마릿수**와 견줘야 한다 (사용자 지적, 2026-09-10)
    //   성벽을 지난 용사 1기가 1 을 깎는다. 그런데 한 판의 적은 스테이지를 따라
    //   늘어난다 — 10스테이지 약 68기, 30스테이지 약 170기다.
    //   바닥 10 + 체력×2 = **18~26** 이던 시절에는 한 판을 놓치는 순간은 물론이고
    //   **부대 하나만 새어 나가도**(30스테이지 기준 33기) 그 자리에서 런이 끝났다.
    //   막아 낸 정도와 결과 사이에 아무 관계가 없어지는 것이 이 게임에서 가장
    //   피해야 할 상태다(위 '왜 갈랐나' 주석과 같은 이유).
    //
    //   그래서 3배로 올렸다 — 체력 4~8 → **54~78**.
    //   30스테이지에서 부대 하나가 통째로 새면 절반쯤 깎인다. 아프지만 끝은 아니다.
    //
    //   ⚠ 회복·증축도 같은 비율로 함께 옮긴다 (RunNodeRule.CampHealAmount ·
    //     CampMaxAmount · 이벤트 표의 체력 대가). 하나만 3배로 올리면
    //     "야영지가 조금밖에 안 채워 주는" 상태로 되돌아간다.

    [Header("마왕성")]
    [Tooltip("체력과 무관하게 깔리는 마왕성 체력 바닥값.")]
    [Min(1)]
    // ⚠ 30 → 12 · 체력 1당 6 → 9 (사용자 지시, 2026-09-12 — "기본을 깎고 체력 스탯 효율을 좋게")
    //   체력 6 에서 66 으로 예전과 같고, 양끝이 벌어진다 (체력 3 = 39 · 체력 10 = 102).
    //   ⚠ 소환사 SO 14개에 직렬화돼 있다 — 이 기본값만 고치면 에셋은 옛 값을 쓴다 (함께 고쳤다)
    public int BaseCoreHp = 12;

    [Tooltip("마왕성 체력 = 바닥값 + 체력 × 이 값.")]
    [Min(0)]
    public int CoreHpPerVitality = 9;

    /// <summary>
    /// 마왕성 체력 = 통과를 몇 번까지 견디는가. 체력 5 면 60, 8 이면 78.
    ///
    /// ⚠ 상한이자 야영지 회복의 기준이다 — 두 곳에서 따로 계산하지 말 것.
    /// </summary>
    /// <summary>
    /// 이 소환사로 시작할 때의 마왕성 체력.
    ///
    /// ⚠ 유물(두꺼운 성문 계열)이 여기에 더해진다 — 묻는 곳은 전부 이 값을 쓴다.
    /// </summary>
    public int MaxCoreHp
        => BaseCoreHp + Mathf.RoundToInt(Vitality) * CoreHpPerVitality
         + RelicTreeApplier.GetSystemInt(RelicSystemEffect.CoreHpBonus);

    public float Attack      => Vigor        * AttackPerVigor;
    public float MaxHp       => Vitality     * HpPerVitality;
    public float SummonPower => Intelligence * SummonPowerPerIntelligence;
}
