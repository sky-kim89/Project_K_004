using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
//  MonsterGearCreator.cs  [Editor Only]
//  Tools > Project K > 데이터 생성 > 몬스터 장비
//
//  몬스터 장비 SO 를 굽고 MonsterGearDatabase 에 등록한다.
//
//  ■ ⚠ 이 파일이 DB 의 정본이다
//    Run 은 db.Entries 를 통째로 비우고 다시 채운다 (액티브 스킬 DB 와 같은 계약).
//    여기 없는 장비는 다시 구울 때마다 사라진다.
//
//  ■ 등급이 수치를 정한다 — 장비마다 손으로 적지 않는다
//    Lv1 스탯은 **절대값**이라(사용자 확정) 수치를 눈대중으로 적으면 약한 종족
//    전용 장비가 되기 쉽다. 그래서 등급별 기준값(BudgetOf)을 한 곳에 두고,
//    각 장비는 "그 예산을 어느 스탯에 어떤 비율로 나눌지(Focus)" 만 고른다.
//
//  ■ 레벨 2~5 는 **컨셉(Theme)** 이 정한다 (사용자 지시, 2026-09-10)
//    "한 장비가 치명타 올렸다 소환 시간 줄였다" 가 아니라
//    "공격력 → 치명타 → 공격력 → 치명타" 처럼 **두 축을 번갈아** 두껍게 한다.
//    컨셉마다 네 칸의 모양(ThemeSteps)이 정해져 있고, 값은 등급이 정한다(StepValue).
//    Lv5 는 재료가 셋이라 두 줄을 준다 — 마지막 칸이 가장 값어치 있어야 한다.
//    ⚠ Lv4·Lv5 는 패시브가 열리는 자리다 (2차 작업 — 아직 효과 없음).
//
//  ■ 특이 옵션 (Quirk) — 낮은 등급 몇 개에만
//    혼자서는 쓸모가 적고, 다른 옵션·종족과 겹칠 때 값을 하는 한 줄이다.
//      치명타 피해만 (치명타가 없는 종족에게는 0) · 스킬 쿨감 (스킬 없는 종족에게는 0)
//      사거리 (근접에게는 조금, 원거리에게는 크게) · 넉백 (밀쳐내기 컨셉과 겹치면 크게)
//    ⚠ 전부 기존 스탯 경로를 탄다 — 새 전투 훅이 필요한 특이 옵션은 패시브(2차)의 몫이다.
//
//  ■ 인간형은 외형 이름이 곧 정체성이다
//    Sprites/Armor · Helmet · Shield 의 파일명을 그대로 쓴다.
//    ⚠ 없는 이름을 적으면 조용히 안 그려진다 — 이 파일의 Verify 가 잡아 준다.
//
//  ■ 비인간형은 부위 셋으로 나뉜다
//    가죽(Hide) = 색조 · 덩치(Bulk) = 크기 · 장식(Charm) = 불빛만.
// ============================================================

public static class MonsterGearCreator
{
    const string Tag    = "MonsterGearCreator";
    const string OutDir = "Assets/_project/Data/MonsterGear";
    const string DbPath = "Assets/Resources/MonsterGearDatabase.asset";

    // ── 등급별 예산 (Lv1) ────────────────────────────────────
    //
    //  ⚠ 절대값 장비의 유일한 손잡이다 — 개별 장비에 숫자를 적지 말 것
    //    Hp    : 종족 기본 체력(55~260) 대비
    //    Attack: 종족 기본 공격력(5~25) 대비

    struct Budget { public float Hp, Attack, Defense; }

    static Budget BudgetOf(UnitGrade grade) => grade switch
    {
        UnitGrade.Normal   => new Budget { Hp =  18f, Attack = 1.6f, Defense = 0.02f },
        UnitGrade.Uncommon => new Budget { Hp =  30f, Attack = 2.8f, Defense = 0.03f },
        UnitGrade.Rare     => new Budget { Hp =  46f, Attack = 4.2f, Defense = 0.04f },
        UnitGrade.Unique   => new Budget { Hp =  66f, Attack = 6.0f, Defense = 0.05f },
        _                  => new Budget { Hp =  92f, Attack = 8.4f, Defense = 0.07f },
    };

    // ── 레벨 한 칸의 값 ──────────────────────────────────────
    //
    //  ⚠ 절대값은 Lv1 예산의 절반이다 — Lv5 까지 주 축이 두 번 오르면 Lv1 의 두 배가 된다.
    //  ⚠ 비율 계열은 등급 다섯 칸의 표다. 한 칸을 고치면 그 축의 장비가 전부 바뀐다.

    static float Pick(UnitGrade g, float n, float u, float r, float un, float e) => g switch
    {
        UnitGrade.Normal   => n,
        UnitGrade.Uncommon => u,
        UnitGrade.Rare     => r,
        UnitGrade.Unique   => un,
        _                  => e,
    };

    static float StepValue(GearStat stat, UnitGrade g)
    {
        Budget b = BudgetOf(g);

        return stat switch
        {
            GearStat.Hp          => Mathf.Round(b.Hp * 0.5f),
            GearStat.Attack      => Mathf.Round(b.Attack * 0.5f * 10f) / 10f,
            GearStat.Defense     => b.Defense * 0.5f,
            GearStat.HpPct       => Pick(g, 0.03f, 0.04f, 0.05f, 0.06f, 0.08f),
            GearStat.AttackPct   => Pick(g, 0.03f, 0.04f, 0.05f, 0.06f, 0.08f),
            GearStat.CritChance  => Pick(g, 0.03f, 0.04f, 0.05f, 0.06f, 0.08f),
            GearStat.CritDamage  => Pick(g, 0.15f, 0.20f, 0.25f, 0.30f, 0.40f),
            GearStat.AttackSpeed => Pick(g, 0.04f, 0.05f, 0.06f, 0.08f, 0.10f),
            GearStat.MoveSpeed   => Pick(g, 0.05f, 0.06f, 0.08f, 0.10f, 0.12f),
            GearStat.Cooldown    => Pick(g, 0.04f, 0.05f, 0.06f, 0.08f, 0.10f),
            GearStat.Penetration => Pick(g, 0.02f, 0.03f, 0.04f, 0.05f, 0.06f),
            GearStat.Range       => Pick(g, 0.20f, 0.25f, 0.30f, 0.40f, 0.50f),
            GearStat.Knockback   => Pick(g, 0.15f, 0.20f, 0.25f, 0.30f, 0.40f),

            // ⚠ 등급과 무관하게 1 — 카드 비용은 5~12 라 1 이 이미 크다.
            //   유물 '마지막 한 방울'(전 종족 −1)이 200pt 다. 이건 한 종족뿐이다.
            GearStat.ManaCost    => 1f,
            _                    => 0f,
        };
    }

    // ── 무엇에 예산을 쓰는가 (Lv1) ───────────────────────────
    //
    //  ⚠ 셋을 합쳐 1.0 이 되게 둔다. 그래야 어느 성향을 골라도 세기가 같다.

    enum Focus
    {
        Tank,     // 체력 위주
        Damage,   // 공격력 위주
        Guard,    // 방어율 + 체력
        Even,     // 고르게
    }

    // ── 컨셉 — Lv2~Lv5 의 모양 ───────────────────────────────

    enum Theme
    {
        Warden,    // 균형 — 체력·공격력을 번갈아
        Guardian,  // 방벽 — 체력·방어율
        Bastion,   // 거체 — 체력·체력%
        Striker,   // 완력 — 공격력·공격력%
        Assassin,  // 급소 — 치명타·공격력
        Swift,     // 신속 — 공격 속도·공격력 (끝에 이동 속도)
        Breaker,   // 파쇄 — 방어 관통·공격력
        Arcane,    // 술법 — 스킬 쿨감·공격력 (끝에 카드 비용 −1)
        Rampart,   // 밀쳐내기 — 넉백·체력
        Hunter,    // 사냥 — 사거리·공격력 (끝에 치명타)
        Royal,     // 왕권 — 체력%·공격력% (Lv3 에 카드 비용 −1)
    }

    static string ConceptName(Theme t) => t switch
    {
        Theme.Warden   => "균형",
        Theme.Guardian => "방벽",
        Theme.Bastion  => "거체",
        Theme.Striker  => "완력",
        Theme.Assassin => "급소",
        Theme.Swift    => "신속",
        Theme.Breaker  => "파쇄",
        Theme.Arcane   => "술법",
        Theme.Rampart  => "밀쳐내기",
        Theme.Hunter   => "사냥",
        Theme.Royal    => "왕권",
        _              => "",
    };

    /// <summary>
    /// 컨셉의 네 칸 — [0]=Lv2 · [1]=Lv3 · [2]=Lv4 · [3]=Lv5.
    ///
    /// ⚠ 두 축을 번갈아 둔다 (파일 머리 주석). 세 번째 축은 Lv5 에만 —
    ///   그래야 "이 장비는 무엇을 하는 장비인가" 가 한 줄로 말해진다.
    /// ⚠ 카드 비용(ManaCost)은 술법 Lv5 · 왕권 Lv3 둘뿐이다. 늘리면 한 종족을
    ///   공짜에 가깝게 부르는 길이 열린다 (하한 1 이 막아 주긴 한다).
    /// </summary>
    static GearStat[][] ThemeSteps(Theme t) => t switch
    {
        Theme.Warden   => new[] { S(GearStat.Hp),          S(GearStat.Attack),    S(GearStat.HpPct),       S(GearStat.AttackPct, GearStat.HpPct) },
        Theme.Guardian => new[] { S(GearStat.Hp),          S(GearStat.Defense),   S(GearStat.Hp),          S(GearStat.Defense, GearStat.HpPct) },
        Theme.Bastion  => new[] { S(GearStat.Hp),          S(GearStat.HpPct),     S(GearStat.Hp),          S(GearStat.HpPct, GearStat.Hp) },
        Theme.Striker  => new[] { S(GearStat.Attack),      S(GearStat.AttackPct), S(GearStat.Attack),      S(GearStat.AttackPct, GearStat.Attack) },
        Theme.Assassin => new[] { S(GearStat.CritChance),  S(GearStat.Attack),    S(GearStat.CritChance),  S(GearStat.CritDamage, GearStat.Attack) },
        Theme.Swift    => new[] { S(GearStat.AttackSpeed), S(GearStat.Attack),    S(GearStat.AttackSpeed), S(GearStat.MoveSpeed, GearStat.Attack) },
        Theme.Breaker  => new[] { S(GearStat.Penetration), S(GearStat.Attack),    S(GearStat.Penetration), S(GearStat.AttackPct, GearStat.Attack) },
        Theme.Arcane   => new[] { S(GearStat.Cooldown),    S(GearStat.Attack),    S(GearStat.Cooldown),    S(GearStat.ManaCost, GearStat.Attack) },
        Theme.Rampart  => new[] { S(GearStat.Knockback),   S(GearStat.Hp),        S(GearStat.Knockback),   S(GearStat.Defense, GearStat.Hp) },
        Theme.Hunter   => new[] { S(GearStat.Range),       S(GearStat.Attack),    S(GearStat.Range),       S(GearStat.CritChance, GearStat.Attack) },
        Theme.Royal    => new[] { S(GearStat.HpPct),       S(GearStat.ManaCost),  S(GearStat.AttackPct),   S(GearStat.HpPct, GearStat.AttackPct) },
        _              => new[] { S(GearStat.Hp),          S(GearStat.Hp),        S(GearStat.Hp),          S(GearStat.Hp) },
    };

    static GearStat[] S(params GearStat[] s) => s;

    /// <summary>특이 옵션 한 줄 — 값을 손으로 적는다(등급 예산 밖의 물건이라서다).</summary>
    static GearOption Q(GearStat stat, float value) => new GearOption(stat, value, quirk: true);

    // ── 정의 ─────────────────────────────────────────────────

    struct Spec
    {
        public string          Id;
        public string          Name;
        public string          Desc;
        public UnitGrade       Grade;
        public MonsterGearBody Body;
        public MonsterGearPart Part;
        public Focus           Focus;
        public Theme           Theme;

        /// <summary>Lv1 에 붙는 특이 옵션. 대부분 비어 있다.</summary>
        public GearOption[] Quirks;

        /// <summary>인간형 — CharacterBuilder 슬롯 이름 (Sprites/&lt;부위&gt;/ 파일명).</summary>
        public string Look;

        /// <summary>비인간형 Hide — 몸에 입힐 색.</summary>
        public Color Tint;

        /// <summary>비인간형 Bulk — 덩치 가산.</summary>
        public float Scale;
    }

    static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

    static readonly Spec[] Specs =
    {
        // ══════════════════════════════════════════════════════
        //  인간형 — 레이어를 갈아 끼운다 (정말로 다른 모습이 된다)
        // ══════════════════════════════════════════════════════

        // ── 갑옷 ──
        new Spec { Id = "gear_tunic_farmer", Name = "낡은 작업복", Grade = UnitGrade.Normal,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Armor,
                   Focus = Focus.Even, Theme = Theme.Warden, Look = "FarmerClothes",
                   Desc = "밭에서 벗겨 온 옷. 없는 것보단 낫다." },

        new Spec { Id = "gear_tunic_thief", Name = "도둑의 옷", Grade = UnitGrade.Uncommon,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Armor,
                   Focus = Focus.Damage, Theme = Theme.Assassin, Look = "ThiefTunic",
                   Quirks = new[] { Q(GearStat.Penetration, 0.03f) },
                   Desc = "가볍다. 무겁게 입은 쪽을 먼저 찌른다." },

        new Spec { Id = "gear_armor_iron", Name = "무쇠 갑옷", Grade = UnitGrade.Rare,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Armor,
                   Focus = Focus.Guard, Theme = Theme.Guardian, Look = "IronKnight",
                   Desc = "용사에게서 벗겨 낸 것. 크기가 맞을 리 없지만 두껍다." },

        new Spec { Id = "gear_armor_dark", Name = "흑기사의 판금", Grade = UnitGrade.Unique,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Armor,
                   Focus = Focus.Tank, Theme = Theme.Bastion, Look = "DarkKnight",
                   Desc = "안에 있던 자가 누구였는지는 아무도 모른다." },

        new Spec { Id = "gear_armor_demigod", Name = "반신의 흉갑", Grade = UnitGrade.Epic,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Armor,
                   Focus = Focus.Guard, Theme = Theme.Guardian, Look = "DemigodArmour",
                   Desc = "신이 되다 만 자의 갑옷. 걸친 것만으로 숨이 달라진다." },

        // ── 투구 ──
        new Spec { Id = "gear_hood_bandit", Name = "산적 두건", Grade = UnitGrade.Normal,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Helmet,
                   Focus = Focus.Damage, Theme = Theme.Striker, Look = "BanditBandana",
                   Quirks = new[] { Q(GearStat.CritDamage, 0.25f) },
                   Desc = "얼굴을 가리면 조금 더 대담해진다." },

        new Spec { Id = "gear_helm_militia", Name = "민병 투구", Grade = UnitGrade.Uncommon,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Helmet,
                   Focus = Focus.Tank, Theme = Theme.Bastion, Look = "MilitiamanHelmet",
                   Desc = "찌그러진 자리마다 누군가의 마지막이 있다." },

        new Spec { Id = "gear_helm_viking", Name = "뿔투구", Grade = UnitGrade.Rare,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Helmet,
                   Focus = Focus.Damage, Theme = Theme.Breaker, Look = "VikingHelmet",
                   Desc = "뿔은 장식이 아니다. 먼저 닿는 쪽이 뿔이다." },

        new Spec { Id = "gear_helm_executioner", Name = "처형인의 복면", Grade = UnitGrade.Unique,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Helmet,
                   Focus = Focus.Damage, Theme = Theme.Assassin, Look = "ExecutionerHood",
                   Desc = "쓴 자의 표정이 보이지 않아, 맞는 쪽이 먼저 무너진다." },

        new Spec { Id = "gear_helm_king", Name = "왕관", Grade = UnitGrade.Epic,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Helmet,
                   Focus = Focus.Even, Theme = Theme.Royal, Look = "King",
                   Desc = "성을 잃은 왕의 것. 이제 마왕성에 있다." },

        // ── 방패 ──
        new Spec { Id = "gear_shield_wood", Name = "나무 소방패", Grade = UnitGrade.Normal,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Shield,
                   Focus = Focus.Guard, Theme = Theme.Rampart, Look = "WoodenBuckler",
                   Desc = "한 번은 막는다. 두 번은 장담 못 한다." },

        new Spec { Id = "gear_shield_iron", Name = "무쇠 소방패", Grade = UnitGrade.Uncommon,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Shield,
                   Focus = Focus.Guard, Theme = Theme.Guardian, Look = "IronBuckler",
                   Desc = "가볍고 단단하다. 앞줄에 세울 것." },

        new Spec { Id = "gear_shield_tower", Name = "탑 방패", Grade = UnitGrade.Rare,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Shield,
                   Focus = Focus.Tank, Theme = Theme.Rampart, Look = "TowerShield",
                   Desc = "들고 있으면 그 자리가 성벽이 된다." },

        new Spec { Id = "gear_shield_crusader", Name = "성전사의 방패", Grade = UnitGrade.Unique,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Shield,
                   Focus = Focus.Guard, Theme = Theme.Arcane, Look = "CrusaderShield",
                   Desc = "성스러운 문양이 새겨져 있다. 든 것은 몬스터지만." },

        new Spec { Id = "gear_shield_royal", Name = "왕실 대방패", Grade = UnitGrade.Epic,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Shield,
                   Focus = Focus.Tank, Theme = Theme.Bastion, Look = "RoyalGreatShield",
                   Desc = "왕을 지키던 것. 지금은 왕을 무너뜨리러 간다." },

        // ══════════════════════════════════════════════════════
        //  비인간형 — 레이어가 없다. 색조·덩치·불빛으로 표현한다
        // ══════════════════════════════════════════════════════

        // ── 가죽 (색조) ──
        //   특이 옵션 둘 — 진흙은 "느리지만 밀친다", 이끼는 "마력을 머금는다"
        new Spec { Id = "gear_hide_mud", Name = "진흙 범벅", Grade = UnitGrade.Normal,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Hide,
                   Focus = Focus.Guard, Theme = Theme.Rampart, Tint = Rgb(150, 130, 105),
                   Quirks = new[] { Q(GearStat.Knockback, 0.20f), Q(GearStat.MoveSpeed, -0.08f) },
                   Desc = "굳은 진흙이 한 겹. 보기엔 지저분해도 한 대는 덜 아프다." },

        // ⚠ 등급마다 **적어도 셋**을 둔다 (사용자 지적, 2026-09-09)
        //   한때 고급 비인간형이 '잿빛 가죽' 하나뿐이라, 5~9 스테이지에서
        //   비인간형이 뽑히면 **언제나 같은 것**이 나왔다 (세 번 연속 목격).
        //   보상이 무작위인지 아닌지는 후보 수가 정한다 — 아래 Verify 가 센다.
        new Spec { Id = "gear_hide_moss", Name = "이끼 껍질", Grade = UnitGrade.Normal,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Hide,
                   Focus = Focus.Tank, Theme = Theme.Bastion, Tint = Rgb(126, 152, 104),
                   Quirks = new[] { Q(GearStat.Cooldown, 0.06f) },
                   Desc = "물기를 머금은 이끼가 한 겹 앉았다. 생각보다 질기다." },

        new Spec { Id = "gear_hide_ash", Name = "잿빛 가죽", Grade = UnitGrade.Uncommon,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Hide,
                   Focus = Focus.Tank, Theme = Theme.Bastion, Tint = Rgb(140, 142, 150),
                   Desc = "불에 그을려 질겨졌다." },

        new Spec { Id = "gear_hide_tanned", Name = "무두질한 가죽", Grade = UnitGrade.Uncommon,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Hide,
                   Focus = Focus.Guard, Theme = Theme.Guardian, Tint = Rgb(176, 132, 92),
                   Desc = "손이 많이 간 가죽. 칼끝이 한 번은 미끄러진다." },

        new Spec { Id = "gear_hide_steel", Name = "강철 비늘", Grade = UnitGrade.Rare,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Hide,
                   Focus = Focus.Guard, Theme = Theme.Guardian, Tint = Rgb(120, 148, 178),
                   Desc = "쇳가루를 먹여 기른 가죽. 몸이 차갑게 빛난다." },

        new Spec { Id = "gear_hide_ember", Name = "잉걸 가죽", Grade = UnitGrade.Unique,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Hide,
                   Focus = Focus.Damage, Theme = Theme.Breaker, Tint = Rgb(214, 108, 70),
                   Desc = "속에서 불씨가 식지 않는다." },

        new Spec { Id = "gear_hide_void", Name = "심연의 껍질", Grade = UnitGrade.Epic,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Hide,
                   Focus = Focus.Tank, Theme = Theme.Arcane, Tint = Rgb(112, 88, 176),
                   Desc = "빛이 닿으면 그 자리만 조금 어두워진다." },

        // ── 덩치 (크기) ──
        new Spec { Id = "gear_bulk_feed", Name = "잘 먹인 몸", Grade = UnitGrade.Normal,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Bulk,
                   Focus = Focus.Tank, Theme = Theme.Bastion, Scale = 0.05f,
                   Quirks = new[] { Q(GearStat.Range, 0.20f) },
                   Desc = "한 끼를 더 줬다. 그만큼 커졌고, 그만큼 멀리 닿는다." },

        new Spec { Id = "gear_bulk_swollen", Name = "불린 몸집", Grade = UnitGrade.Uncommon,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Bulk,
                   Focus = Focus.Tank, Theme = Theme.Rampart, Scale = 0.07f,
                   Desc = "물을 잔뜩 먹여 부풀렸다. 오래가지는 않는다." },

        new Spec { Id = "gear_bulk_gorge", Name = "포식의 흔적", Grade = UnitGrade.Rare,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Bulk,
                   Focus = Focus.Tank, Theme = Theme.Bastion, Scale = 0.09f,
                   Desc = "무엇을 먹었는지는 묻지 않는 편이 좋다." },

        new Spec { Id = "gear_bulk_beast", Name = "괴수의 골격", Grade = UnitGrade.Unique,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Bulk,
                   Focus = Focus.Tank, Theme = Theme.Rampart, Scale = 0.12f,
                   Desc = "뼈가 굵어졌다. 걸음마다 땅이 조금씩 눌린다." },

        new Spec { Id = "gear_bulk_titan", Name = "거인의 뼈대", Grade = UnitGrade.Epic,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Bulk,
                   Focus = Focus.Tank, Theme = Theme.Bastion, Scale = 0.14f,
                   Desc = "골격이 통째로 바뀌었다. 라인 하나를 혼자 메운다." },

        // ── 장식 (불빛만) ──
        //   ⚠ 희귀 미만은 만들지 않는다 — 불빛이 안 뜨는 '장식' 은 이름값을 못 한다
        new Spec { Id = "gear_charm_wisp", Name = "도깨비불", Grade = UnitGrade.Rare,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Charm,
                   Focus = Focus.Damage, Theme = Theme.Hunter,
                   Desc = "따라다니는 불빛. 주인을 대신해 앞을 본다." },

        new Spec { Id = "gear_charm_soul", Name = "혼불", Grade = UnitGrade.Unique,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Charm,
                   Focus = Focus.Even, Theme = Theme.Arcane,
                   Desc = "삼킨 것들의 마지막이 아직 타고 있다." },

        new Spec { Id = "gear_charm_crown", Name = "짐승의 관", Grade = UnitGrade.Epic,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Charm,
                   Focus = Focus.Damage, Theme = Theme.Royal,
                   Desc = "무리에서 하나에게만 주어진다." },

        // ══════════════════════════════════════════════════════
        //  2차 — 패시브 재배치로 빠진 22종을 이어받는다 (사용자 지시, 2026-09-11)
        //
        //  ■ 패시브가 한 칸으로 줄며 22개 패시브가 **어느 장비에도 없게** 됐다.
        //    그 패시브를 하나씩 들고 새 장비로 나온다 — 장비 하나에 패시브 하나.
        //  ■ 규칙은 위와 같다 — 일반·고급 Lv4(단계) / 특이·희귀↑ Lv5 / 술법·왕권 금지
        //  ■ 망토·등짐이 여기서 처음 쓰인다 (외형 경로는 MonsterGearRule 에 이미 있었다)
        // ══════════════════════════════════════════════════════

        // ── 인간형 · 일반 ──
        new Spec { Id = "gear_cape_tattered", Name = "해진 망토", Grade = UnitGrade.Normal,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Cape,
                   Focus = Focus.Even, Theme = Theme.Swift, Look = "Cape",
                   Desc = "바람에 너덜너덜해졌다. 가벼워서 발이 빨라진다." },

        new Spec { Id = "gear_back_satchel", Name = "잡동사니 보따리", Grade = UnitGrade.Normal,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Back,
                   Focus = Focus.Tank, Theme = Theme.Rampart, Look = "SmallBackpack",
                   Desc = "무엇이 들었는지 모르지만 휘두르면 제법 묵직하다." },

        // ── 인간형 · 고급 ──
        new Spec { Id = "gear_helm_archer", Name = "궁수 두건", Grade = UnitGrade.Uncommon,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Helmet,
                   Focus = Focus.Damage, Theme = Theme.Hunter, Look = "ArcherHood",
                   Desc = "눈가에 그늘이 지면 겨냥이 차분해진다." },

        new Spec { Id = "gear_armor_miner", Name = "광부의 갑주", Grade = UnitGrade.Uncommon,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Armor,
                   Focus = Focus.Guard, Theme = Theme.Guardian, Look = "MinerArmour",
                   Desc = "낙석을 버티려고 만든 옷. 칼끝쯤이야." },

        new Spec { Id = "gear_back_quiver", Name = "가죽 화살통", Grade = UnitGrade.Uncommon,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Back,
                   Focus = Focus.Damage, Theme = Theme.Breaker, Look = "LeatherQuiver",
                   Desc = "화살촉이 전부 쇠를 뚫는 모양으로 벼려져 있다." },

        // ── 인간형 · 희귀 ──
        new Spec { Id = "gear_helm_horns", Name = "황소뿔 투구", Grade = UnitGrade.Rare,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Helmet,
                   Focus = Focus.Damage, Theme = Theme.Striker, Look = "HornsHelmet",
                   Desc = "쓰는 순간 어깨가 펴진다. 다치기 전까지는." },

        new Spec { Id = "gear_shield_steel", Name = "강철 방패", Grade = UnitGrade.Rare,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Shield,
                   Focus = Focus.Guard, Theme = Theme.Rampart, Look = "SteelShield",
                   Desc = "무겁다. 든 자리에서 한 걸음도 물러나지 않는다." },

        // ── 인간형 · 유일 ──
        new Spec { Id = "gear_armor_plague", Name = "역병 의사의 옷", Grade = UnitGrade.Unique,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Armor,
                   Focus = Focus.Even, Theme = Theme.Assassin, Look = "PlagueDoctor",
                   Desc = "고치러 왔던 자의 옷. 이제는 옮기는 쪽이 입는다." },

        new Spec { Id = "gear_helm_cleric", Name = "사제의 두건", Grade = UnitGrade.Unique,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Helmet,
                   Focus = Focus.Guard, Theme = Theme.Guardian, Look = "ClericHood",
                   Desc = "쓰러질 때 기도가 새어 나온다. 곁에 있던 자가 그걸 듣는다." },

        new Spec { Id = "gear_back_sword", Name = "등에 멘 대검", Grade = UnitGrade.Unique,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Back,
                   Focus = Focus.Damage, Theme = Theme.Striker, Look = "BackSword",
                   Desc = "뽑을 일이 없다. 멘 것만으로도 피가 끓는다." },

        new Spec { Id = "gear_back_expedition", Name = "원정대 배낭", Grade = UnitGrade.Unique,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Back,
                   Focus = Focus.Tank, Theme = Theme.Bastion, Look = "LargeBackpack",
                   Desc = "용사 원정대가 두고 간 짐. 식량이 넉넉하다." },

        // ── 인간형 · 영웅 ──
        new Spec { Id = "gear_helm_warlord", Name = "군주의 투구", Grade = UnitGrade.Epic,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Helmet,
                   Focus = Focus.Even, Theme = Theme.Warden, Look = "HeavyKnightHelmet",
                   Desc = "쓴 자가 쓰러지면 부하들이 그 이름을 외친다." },

        new Spec { Id = "gear_shield_ancient", Name = "고대의 대방패", Grade = UnitGrade.Epic,
                   Body = MonsterGearBody.Humanoid, Part = MonsterGearPart.Shield,
                   Focus = Focus.Tank, Theme = Theme.Guardian, Look = "AncientGreatShield",
                   Desc = "성이 무너져도 이 방패는 남았다. 그래서 여기 있다." },

        // ── 비인간형 · 일반 ──
        new Spec { Id = "gear_hide_frost", Name = "서리 낀 가죽", Grade = UnitGrade.Normal,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Hide,
                   Focus = Focus.Guard, Theme = Theme.Rampart, Tint = Rgb(176, 204, 228),
                   Desc = "만지면 손끝이 얼얼하다. 때린 쪽도 마찬가지다." },

        // ── 비인간형 · 고급 ──
        new Spec { Id = "gear_bulk_lean", Name = "다부진 근육", Grade = UnitGrade.Uncommon,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Bulk,
                   Focus = Focus.Even, Theme = Theme.Striker, Scale = 0.06f,
                   Desc = "군살을 걷어 냈다. 크지는 않아도 한 방이 무겁다." },

        new Spec { Id = "gear_hide_scar", Name = "흉터투성이 가죽", Grade = UnitGrade.Uncommon,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Hide,
                   Focus = Focus.Damage, Theme = Theme.Assassin, Tint = Rgb(170, 128, 118),
                   Desc = "살아남은 싸움만큼 흉이 졌다. 어디를 물어야 하는지 안다." },

        new Spec { Id = "gear_hide_starscale", Name = "별빛 비늘", Grade = UnitGrade.Uncommon,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Hide,
                   Focus = Focus.Even, Theme = Theme.Swift, Tint = Rgb(150, 168, 222),
                   Desc = "밤마다 희미하게 빛난다. 기운이 빨리 돈다." },

        // ── 비인간형 · 희귀 ──
        new Spec { Id = "gear_charm_fang", Name = "송곳니 목걸이", Grade = UnitGrade.Rare,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Charm,
                   Focus = Focus.Damage, Theme = Theme.Striker,
                   Desc = "사냥한 것의 이빨을 꿰었다. 무리가 그 냄새를 따른다." },

        new Spec { Id = "gear_bulk_brute", Name = "억센 몸통", Grade = UnitGrade.Rare,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Bulk,
                   Focus = Focus.Tank, Theme = Theme.Rampart, Scale = 0.09f,
                   Desc = "통나무 같은 몸통. 부딪친 쪽이 튕겨 나간다." },

        // ── 비인간형 · 유일 ──
        new Spec { Id = "gear_hide_blood", Name = "핏빛 가죽", Grade = UnitGrade.Unique,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Hide,
                   Focus = Focus.Damage, Theme = Theme.Breaker, Tint = Rgb(196, 86, 94),
                   Desc = "베인 자리마다 남의 피가 스며들어 굳었다." },

        new Spec { Id = "gear_charm_howl", Name = "울음 뼈피리", Grade = UnitGrade.Unique,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Charm,
                   Focus = Focus.Even, Theme = Theme.Hunter,
                   Desc = "동족이 쓰러질 때마다 저 혼자 운다." },

        // ── 비인간형 · 영웅 ──
        new Spec { Id = "gear_charm_warbrand", Name = "전쟁의 낙인", Grade = UnitGrade.Epic,
                   Body = MonsterGearBody.NonHumanoid, Part = MonsterGearPart.Charm,
                   Focus = Focus.Damage, Theme = Theme.Striker,
                   Desc = "달군 쇠로 찍은 표식. 찍힌 것은 싸우는 법만 기억한다." },
    };

    // ── 실행 ─────────────────────────────────────────────────

    [MenuItem(ProjectKMenu.Data + "몬스터 장비", priority = ProjectKMenu.DataPrio + 8)]
    public static void Run()
    {
        if (!Verify()) return;

        Directory.CreateDirectory(OutDir);

        var db = AssetDatabase.LoadAssetAtPath<MonsterGearDatabase>(DbPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<MonsterGearDatabase>();
            AssetDatabase.CreateAsset(db, DbPath);
        }

        // ⚠ 통째로 비우고 다시 채운다 — 이 파일이 DB 의 정본이다
        db.Entries.Clear();

        foreach (var spec in Specs)
            db.Entries.Add(Bake(spec));

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[{Tag}] 몬스터 장비 {db.Entries.Count}종 생성 완료 → {DbPath}");
    }

    static MonsterGearData Bake(in Spec spec)
    {
        string path = $"{OutDir}/{spec.Id}.asset";

        var so = AssetDatabase.LoadAssetAtPath<MonsterGearData>(path);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<MonsterGearData>();
            AssetDatabase.CreateAsset(so, path);
        }

        so.Id          = spec.Id;
        so.DisplayName = spec.Name;
        so.Description = spec.Desc;
        so.Concept     = ConceptName(spec.Theme);
        so.Grade       = spec.Grade;
        so.Body        = spec.Body;
        so.Part        = spec.Part;

        // ⚠ 아이콘은 **부위마다 하나**다 (사용자 지적, 2026-09-07)
        //   Icon 필드는 처음부터 있었는데 채우는 코드가 없어서, 도감 장비 탭과
        //   몬스터 상세의 장비 칸이 전부 빈 그림이었다. 에러는 안 난다.
        //   ⚠ 못 찾으면 그 사실을 말한다 — 조용히 null 로 두면 또 같은 일이 난다.
        so.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(
                      MonsterGearIconGenerator.PathOf(spec.Part));

        if (so.Icon == null)
            Debug.LogError($"[{Tag}] '{spec.Name}' 의 아이콘을 못 찾았습니다 " +
                           $"({MonsterGearIconGenerator.PathOf(spec.Part)}).\n" +
                           "아이콘·텍스처 > 몬스터 장비 아이콘 을 먼저 실행하세요.");

        so.AppearanceName = spec.Look ?? "";
        so.Tint           = spec.Part == MonsterGearPart.Hide ? spec.Tint  : Color.white;
        so.ScaleBonus     = spec.Part == MonsterGearPart.Bulk ? spec.Scale : 0f;

        so.BaseOptions = BuildBase(spec.Grade, spec.Focus);
        if (spec.Quirks != null) so.BaseOptions.AddRange(spec.Quirks);

        so.Levels = BuildLevels(spec.Grade, spec.Theme);

        (so.Lv4Passive, so.Lv5Passive) = PassivesOf(spec.Id);

        EditorUtility.SetDirty(so);
        return so;
    }

    // ── Lv4 · Lv5 패시브 (사용자 확정, 2026-09-10 — Docs/MonsterGear_PassivePlan.md 5장) ──
    //
    //  ⚠ 원칙 — 일반·고급은 단계 패시브, 가끔 특이. 희귀부터 종족 패시브,
    //    영웅 Lv5 는 조합 핵심(분열·재조립·역병·연사).
    //  ⚠ 각성 거리를 일부러 남겨 둔 짝 — 같은 몸 형태 · **다른 부위**여야 함께 낀다
    //    방벽: 탑 방패 Lv4 + 무쇠 갑옷 Lv5 · 가시: 반신의 흉갑 Lv4 + 왕실 대방패 Lv4
    //    선천과 겹치는 것(거인의 뼈대 분열 → 슬라임 계열 · 흉갑 재조립 → 스켈레톤)은 저절로 된다.
    // ⚠ 패시브는 **Lv4 · Lv5 중 한 곳에만** 있다 (사용자 확정, 2026-09-11) — 위 원칙을 대체한다
    //   두 칸 다 주던 때는 영웅 장비 하나가 종족 패시브 둘을 얹어, 장비가 종족보다
    //   그 몬스터를 더 많이 정했다 (심연의 껍질 = 생명흡수 + 역병 폭발 + 카드 비용 −1).
    //   자리 — 일반·고급은 Lv4(단계 패시브, 빨리 맛본다) · 희귀부터 Lv5(종족 패시브, 끝의 보상).
    //   특이 패시브(도둑·이끼·불린 몸집)는 Lv5 에 둔다.
    // ⚠ **카드 비용 −1 이 있는 장비는 패시브가 없다** (사용자 확정) — 술법·왕권 컨셉 다섯.
    //   비용 −1 은 유물 '마지막 한 방울'(200pt)의 한 종족판이라 그 자체가 패시브 한 칸 값이다.
    //   Verify 가 둘 다 검사한다 (GivesManaCut).
    // ⚠ 각성 짝 — 방벽: 탑 방패 Lv4 + 무쇠 갑옷 Lv5 는 남겼다.
    //   가시 짝(흉갑+대방패)은 흉갑이 재조립 하나로 줄며 풀렸다 — 선천 가시 종족과는 여전히 겹친다.
    static (SpeciesPassive lv4, SpeciesPassive lv5) PassivesOf(string id) => id switch
    {
        // 인간형 — 갑옷
        "gear_tunic_farmer"     => (SpeciesPassive.HpUp1,       SpeciesPassive.None),
        "gear_tunic_thief"      => (SpeciesPassive.None,        SpeciesPassive.VitalStrike),
        "gear_armor_iron"       => (SpeciesPassive.None,        SpeciesPassive.Bulwark),
        "gear_armor_dark"       => (SpeciesPassive.None,        SpeciesPassive.Regrow),
        "gear_armor_demigod"    => (SpeciesPassive.None,        SpeciesPassive.Reassemble),
        // 인간형 — 투구
        "gear_hood_bandit"      => (SpeciesPassive.AttackUp1,   SpeciesPassive.None),
        "gear_helm_militia"     => (SpeciesPassive.HpUp1,       SpeciesPassive.None),
        "gear_helm_viking"      => (SpeciesPassive.None,        SpeciesPassive.LoneWolf),
        "gear_helm_executioner" => (SpeciesPassive.None,        SpeciesPassive.Executioner),
        "gear_helm_king"        => (SpeciesPassive.None,        SpeciesPassive.None),   // 왕권 — 비용 −1
        // 인간형 — 방패
        "gear_shield_wood"      => (SpeciesPassive.DefenseUp1,  SpeciesPassive.None),
        "gear_shield_iron"      => (SpeciesPassive.DefenseUp1,  SpeciesPassive.None),
        "gear_shield_tower"     => (SpeciesPassive.Bulwark,     SpeciesPassive.None),   // 방벽 짝
        "gear_shield_crusader"  => (SpeciesPassive.None,        SpeciesPassive.None),   // 술법 — 비용 −1
        "gear_shield_royal"     => (SpeciesPassive.None,        SpeciesPassive.ThornOnHit),
        // 비인간형 — 가죽
        "gear_hide_mud"         => (SpeciesPassive.HpUp1,       SpeciesPassive.None),
        "gear_hide_moss"        => (SpeciesPassive.None,        SpeciesPassive.Photosynthesis),
        "gear_hide_ash"         => (SpeciesPassive.HpUp2,       SpeciesPassive.None),
        "gear_hide_tanned"      => (SpeciesPassive.DefenseUp1,  SpeciesPassive.None),
        "gear_hide_steel"       => (SpeciesPassive.None,        SpeciesPassive.ThornOnHit),
        "gear_hide_ember"       => (SpeciesPassive.None,        SpeciesPassive.BurnOnAttack),
        "gear_hide_void"        => (SpeciesPassive.None,        SpeciesPassive.None),   // 술법 — 비용 −1
        // 비인간형 — 덩치
        "gear_bulk_feed"        => (SpeciesPassive.HpUp1,       SpeciesPassive.None),
        "gear_bulk_swollen"     => (SpeciesPassive.None,        SpeciesPassive.BurstBody),
        "gear_bulk_gorge"       => (SpeciesPassive.None,        SpeciesPassive.Hunger),
        "gear_bulk_beast"       => (SpeciesPassive.None,        SpeciesPassive.Embers),
        "gear_bulk_titan"       => (SpeciesPassive.None,        SpeciesPassive.SplitOnDeath),
        // 비인간형 — 장식
        "gear_charm_wisp"       => (SpeciesPassive.None,        SpeciesPassive.Volley),
        "gear_charm_soul"       => (SpeciesPassive.None,        SpeciesPassive.None),   // 술법 — 비용 −1
        "gear_charm_crown"      => (SpeciesPassive.None,        SpeciesPassive.None),   // 왕권 — 비용 −1

        // ── 2차 — 빠진 22종을 이어받는다 (Specs 2차 머리 주석) ──
        // 인간형
        "gear_cape_tattered"    => (SpeciesPassive.CritUp1,     SpeciesPassive.None),
        "gear_back_satchel"     => (SpeciesPassive.None,        SpeciesPassive.Recoil),         // 특이
        "gear_helm_archer"      => (SpeciesPassive.CritUp2,     SpeciesPassive.None),
        "gear_armor_miner"      => (SpeciesPassive.DefenseUp2,  SpeciesPassive.None),
        "gear_back_quiver"      => (SpeciesPassive.PierceUp2,   SpeciesPassive.None),
        "gear_helm_horns"       => (SpeciesPassive.None,        SpeciesPassive.Bravado),
        "gear_shield_steel"     => (SpeciesPassive.None,        SpeciesPassive.Anchor),
        "gear_armor_plague"     => (SpeciesPassive.None,        SpeciesPassive.PlagueBurst),
        "gear_helm_cleric"      => (SpeciesPassive.None,        SpeciesPassive.HealOnDeath),
        "gear_back_sword"       => (SpeciesPassive.None,        SpeciesPassive.Bloodlust),
        "gear_back_expedition"  => (SpeciesPassive.None,        SpeciesPassive.HpUp3),
        "gear_helm_warlord"     => (SpeciesPassive.None,        SpeciesPassive.RallyOnDeath),
        "gear_shield_ancient"   => (SpeciesPassive.None,        SpeciesPassive.Rampart),
        // 비인간형
        "gear_hide_frost"       => (SpeciesPassive.None,        SpeciesPassive.ChillOnHit),     // 특이 자리
        "gear_bulk_lean"        => (SpeciesPassive.AttackUp2,   SpeciesPassive.None),
        "gear_hide_scar"        => (SpeciesPassive.CritDmgUp2,  SpeciesPassive.None),
        "gear_hide_starscale"   => (SpeciesPassive.CooldownUp2, SpeciesPassive.None),
        "gear_charm_fang"       => (SpeciesPassive.None,        SpeciesPassive.PackHunt),
        "gear_bulk_brute"       => (SpeciesPassive.None,        SpeciesPassive.Sturdy),
        "gear_hide_blood"       => (SpeciesPassive.None,        SpeciesPassive.SoulDrain),
        "gear_charm_howl"       => (SpeciesPassive.None,        SpeciesPassive.Vengeance),
        "gear_charm_warbrand"   => (SpeciesPassive.None,        SpeciesPassive.AttackUp3),
        _                       => (SpeciesPassive.None,        SpeciesPassive.None),
    };

    /// <summary>이 컨셉이 카드 비용 −1 을 주는가 — 주면 패시브를 받지 않는다.</summary>
    static bool GivesManaCut(Theme t)
    {
        foreach (GearStat[] step in ThemeSteps(t))
            foreach (GearStat s in step)
                if (s == GearStat.ManaCost) return true;
        return false;
    }

    /// <summary>
    /// Lv1 — 등급 예산을 성향대로 나눈다.
    ///
    /// ⚠ 비율의 합이 1.0 이다 — 어느 성향을 골라도 세기가 같아야 한다
    ///   한쪽만 합이 크면 그 성향이 언제나 정답이 된다.
    /// </summary>
    static List<GearOption> BuildBase(UnitGrade grade, Focus focus)
    {
        Budget b    = BudgetOf(grade);
        var    list = new List<GearOption>(3);

        void Add(GearStat stat, float value)
        {
            if (value <= 0f) return;
            list.Add(new GearOption(stat, value));
        }

        switch (focus)
        {
            case Focus.Tank:
                Add(GearStat.Hp, b.Hp);
                break;

            case Focus.Damage:
                Add(GearStat.Attack, b.Attack);
                break;

            case Focus.Guard:
                Add(GearStat.Hp,      b.Hp * 0.5f);
                Add(GearStat.Defense, b.Defense * 0.5f);
                break;

            default:   // Even
                Add(GearStat.Hp,     b.Hp     * 0.5f);
                Add(GearStat.Attack, b.Attack * 0.5f);
                break;
        }

        return list;
    }

    /// <summary>Lv2~Lv5 — 컨셉의 모양에 등급 값을 채운다.</summary>
    static List<GearLevelStep> BuildLevels(UnitGrade grade, Theme theme)
    {
        GearStat[][] shape = ThemeSteps(theme);
        var levels = new List<GearLevelStep>(shape.Length);

        foreach (GearStat[] step in shape)
        {
            var s = new GearLevelStep();
            foreach (GearStat stat in step)
                s.Options.Add(new GearOption(stat, StepValue(stat, grade)));
            levels.Add(s);
        }

        return levels;
    }

    // ── 검증 ─────────────────────────────────────────────────

    /// <summary>
    /// 등급 × 몸 형태마다 후보가 몇인지 센다.
    ///
    /// ■ ⚠ 하나뿐이면 그 칸의 보상은 **무작위가 아니다** (사용자 지적, 2026-09-09)
    ///   실제로 고급 비인간형이 '잿빛 가죽' 하나여서 세 번 연속 같은 것이 나왔다.
    ///   MonsterGearRewardRule 은 후보 중에서 고를 뿐이라 그쪽에서는 고칠 수 없다 —
    ///   <b>표에 물건이 없는 것</b>이 원인이다.
    ///   ⚠ 굽기를 막지는 않는다(경고). 새 등급을 시험하는 중일 수 있다.
    /// </summary>
    static void VerifyPoolSize()
    {
        foreach (MonsterGearBody body in new[] { MonsterGearBody.Humanoid, MonsterGearBody.NonHumanoid })
        foreach (UnitGrade grade in System.Enum.GetValues(typeof(UnitGrade)))
        {
            int n = 0;
            foreach (var spec in Specs)
                if (spec.Body == body && spec.Grade == grade) n++;

            if (n >= 2) continue;

            Debug.LogWarning($"[{Tag}] {GradeStyle.GetLabel(grade)} · {body} 후보가 {n}개뿐입니다 — " +
                             "이 칸의 보상 상자는 늘 같은 것이 나옵니다.");
        }
    }

    /// <summary>
    /// 굽기 전에 정의가 말이 되는지 본다.
    ///
    /// ⚠ 여기서 잡지 않으면 게임 안에서 조용히 실패한다
    ///   없는 외형 이름은 "아무것도 안 그려짐" 으로, 부위·몸 형태가 어긋난
    ///   장비는 "아무도 못 끼움" 으로 나타난다. 둘 다 원인을 찾기 어렵다.
    /// </summary>
    static bool Verify()
    {
        var seen = new HashSet<string>();
        bool ok  = true;

        // ⚠ 컨셉의 칸 수가 레벨 수와 같아야 한다 — 모자라면 그 레벨이 아무것도 안 준다
        foreach (Theme t in System.Enum.GetValues(typeof(Theme)))
        {
            if (ThemeSteps(t).Length == MonsterGearLevelRule.MaxLevel - 1) continue;

            Debug.LogError($"[{Tag}] 컨셉 {t} 의 칸이 {ThemeSteps(t).Length}개입니다 — " +
                           $"Lv2~Lv{MonsterGearLevelRule.MaxLevel} 에 맞춰 {MonsterGearLevelRule.MaxLevel - 1}개여야 합니다.");
            ok = false;
        }

        foreach (var spec in Specs)
        {
            // ⚠ 패시브는 정확히 한 칸 — 비용 −1 장비는 0 칸 (PassivesOf 머리 주석)
            //   빠지면 Lv4·Lv5 를 올려도 아무것도 안 주고, 둘 다 있으면 장비가 너무 세진다 — 둘 다 조용하다
            var (p4, p5) = PassivesOf(spec.Id);
            int want  = GivesManaCut(spec.Theme) ? 0 : 1;
            int given = (p4 != SpeciesPassive.None ? 1 : 0) + (p5 != SpeciesPassive.None ? 1 : 0);
            if (given != want)
            {
                Debug.LogError($"[{Tag}] '{spec.Id}' 의 패시브가 {given}칸입니다 — {want}칸이어야 합니다 " +
                               (want == 0 ? "(카드 비용 −1 장비는 패시브가 없다)." : "(Lv4·Lv5 중 한 곳만)."));
                ok = false;
            }

            // ⚠ 각성판은 장비가 직접 주지 않는다 — 둘이 모여야 생긴다
            if (PassiveAwakening.IsAwakened(PassivesOf(spec.Id).lv4) ||
                PassiveAwakening.IsAwakened(PassivesOf(spec.Id).lv5))
            {
                Debug.LogError($"[{Tag}] '{spec.Id}' 가 각성 패시브를 직접 줍니다 — 각성은 겹쳐서만 생긴다.");
                ok = false;
            }

            if (!seen.Add(spec.Id))
            {
                Debug.LogError($"[{Tag}] Id 가 겹칩니다: '{spec.Id}'");
                ok = false;
            }

            bool humanoidPart = spec.Part <= MonsterGearPart.Back;

            if (spec.Body == MonsterGearBody.Humanoid != humanoidPart)
            {
                Debug.LogError($"[{Tag}] '{spec.Id}' — 몸 형태({spec.Body})와 " +
                               $"부위({spec.Part})가 맞지 않습니다.");
                ok = false;
            }

            if (spec.Body != MonsterGearBody.Humanoid) continue;

            if (string.IsNullOrEmpty(spec.Look))
            {
                Debug.LogError($"[{Tag}] '{spec.Id}' — 인간형인데 외형 이름이 비어 있습니다.");
                ok = false;
                continue;
            }

            string dir  = $"Assets/PixelFantasy/PixelHeroes/FantasyHeroes/Sprites/{spec.Part}";
            string file = $"{dir}/{spec.Look}.png";

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(file) != null) continue;

            Debug.LogError($"[{Tag}] '{spec.Id}' — 벤더 에셋에 없는 외형 이름입니다: {file}\n" +
                           "이름을 잘못 적으면 장비를 껴도 아무것도 그려지지 않습니다.");
            ok = false;
        }

        VerifyPoolSize();

        if (!ok) Debug.LogError($"[{Tag}] 검증 실패 — 아무것도 굽지 않았습니다.");
        return ok;
    }
}
