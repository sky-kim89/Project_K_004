using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

// ============================================================
//  MonsterCodexCreator.cs  [Editor Only]
//  종족별 MonsterSpeciesData 를 한 번에 굽는다 — 몬스터 도감의 원본이다.
//
//  ■ ⚠ 밸런싱 기준이 바뀌었다 (2026-08-27) — 다수 생산 체제
//    예전에는 "비싼 1마리 vs 싼 6마리" 를 대등하게 뒀다. 지금은 **다수 쪽으로
//    기울인다** — 가장 비싼 트롤도 3마리씩 나온다. 이유는 셋이다.
//      · 종족 패시브(분열·재조립·자폭·최후의 함성)가 전부 **개체 수**에
//        비례해 값어치가 커진다. 1마리 체제에서는 그 축이 죽는다.
//      · 라인 5개에 골고루 배치하려면 손에 쥔 마릿수가 라인 수보다 많아야 한다.
//      · 마나 환수가 개체 단위라, 마릿수가 적으면 한 마리 죽을 때의 손실이
//        너무 크게 튄다.
//    "정예" 는 이제 **마릿수가 적다** 가 아니라 **개체가 두껍다** 로만 표현한다.
//
//  ■ 종족 패시브 — 이 표의 핵심
//    종족마다 하나씩, 그 종족을 그 종족답게 만드는 규칙을 준다
//    (슬라임 = 죽으면 분열). 업그레이드 종족은 `UpgradeOf` 로 뿌리를 가리키고
//    뿌리의 패시브를 **자동으로 물려받는다** — 여기에 다시 적지 말 것.
//      슬라임(분열) ─┬─ 힐 슬라임 (분열 + 죽을 때 회복)
//                    ├─ 독 슬라임 (분열 + 피격 시 중독)
//                    └─ 강철 슬라임(분열 + 피격 시 반사)
//
//  ■ 소환력 반응도(SummonPowerToHp / ToAttack)로 성격을 가른다
//    전부 1.0 이면 모든 종족이 소환력을 똑같이 받아 결국 같은 유닛으로 수렴한다.
//    방패 종족은 HP 쪽으로, 딜러 종족은 공격력 쪽으로 받게 기울인다.
//
//  ■ 근접·원거리를 섞는다
//    전부 근접이면 "앞을 막는다" 는 선택지밖에 없다. 원거리 종족이 있어야
//    벽을 세우고 뒤에서 때리는 조합이 성립한다.
//    ⚠ 새 그림이 필요 없다 — 원거리 종족도 기존 EnemyRace 외형을 쓴다.
//
//  ■ 카드 레벨 보너스 — **어빌리티를 대체하는 축이다**
//    Lv2·Lv3·Lv4·Lv5 에 하나씩 열리는 정해진 고정 효과 (MonsterLevelBonus).
//    한 칸 = 스탯 1~2개 + 선택적 패시브 1개.
//
//    ⚠ 종족마다 **다른 것이 열려야** 한다
//      전부 같으면 "무엇을 키울까" 가 선택이 아니게 된다. 그 종족이 아쉬운
//      축을 메우거나(슬라임의 이속), 잘하는 축을 더 밀어 준다(리치의 사거리).
//
//    ⚠ 패시브는 기존 40종에서만 고른다 — 새 실행 코드가 필요 없다.
//      병사·장군에 종속된 것(ExtraSoldiers 등)은 고르지 말 것.
//      몬스터에는 병사가 없어 아무 일도 일어나지 않는다.
//
//  ■ SignaturePassive = 합성으로 물려주는 개성
//    만렙 카드가 이 종족을 재료로 먹으면 이 패시브 하나를 배워 간다.
//    그 종족을 한 줄로 요약하는 것을 고른다.
//
//  ■ 비인간형은 라이브러리를 함께 물린다
//    MonsterLibraryCreator 가 먼저 돌아야 한다 — 없으면 경고만 내고 비워 둔다.
//
//  사용: Tools > Project K > 데이터 생성 > 몬스터 도감
// ============================================================

public static class MonsterCodexCreator
{
    const string OutputRoot  = "Assets/_project/Data/Monsters";
    const string LibraryRoot = "Assets/_project/Data/MonsterLibraries";

    /// <summary>한 종족의 굽기 설정. 아래 Roster 가 정본이다.</summary>
    struct Spec
    {
        public string          Id;
        public string          Name;
        public MonsterBodyType Body;
        public EnemyRace       Race;          // 인간형 전용
        public string          LibraryName;   // 비인간형 전용
        public MonsterTrait    Traits;
        public string          Desc;

        public float ManaCost;
        public int   Count;

        /// <summary>업그레이드라면 뿌리 종족의 Id. 비우면 자기가 뿌리다.</summary>
        public string UpgradeOf;

        /// <summary>이 종족만의 고유 패시브. 뿌리 것은 상속되므로 다시 적지 않는다.</summary>
        public SpeciesPassive Species;

        /// <summary>Species 말고 **더** 갖는 고유 패시브. 2차가 '뿌리 각성판 + 제 특성' 을 함께 들 때.</summary>
        public SpeciesPassive[] ExtraSpecies;

        /// <summary>계보에서 **물려받지 않을** 패시브. 윗단계와 성격이 갈릴 때만.</summary>
        public SpeciesPassive[] BlockInherit;

        public MonsterAttackKind     Attack;
        public MonsterProjectileKind Projectile;   // 원거리 전용
        public float                 ProjSpeed;    // 0 = 발사체 기본값

        public float Hp, Atk, Range, AtkSpeed, MoveSpeed, Defense;
        public float ToHp, ToAtk;   // 소환력 반응도

        /// <summary>Lv2·Lv3·Lv4·Lv5 에서 하나씩 열리는 고정 효과.</summary>
        public MonsterLevelBonus[] Levels;

        /// <summary>합성 재료로 쓰였을 때 물려주는 개성.</summary>
        public PassiveSkillType Signature;

        /// <summary>고유 액티브 스킬. None 이면 없음 (대부분 없다).</summary>
        public ActiveSkillId Skill;

        // ── 2차 업그레이드 전용 (사용자 지시, 2026-09-15) ─────
        //
        //  2차는 셋을 함께 갖는다 — **희귀 스킬 · 적은 마릿수 · 권속 소환**.
        //  앞의 둘은 위 Skill / Count 가 그대로 받고, 권속만 여기 셋이다.

        /// <summary>이 종족으로 올라올 수 있는 **다른** 하위 종족들 (UpgradeOf 말고도).</summary>
        public string[] AlsoUpgradeOf;

        /// <summary>권속으로 불러내는 종족 Id. 비우면 권속이 없다.</summary>
        public string Brood;

        /// <summary>권속 소환 스킬이 한 번에 부르는 마릿수.</summary>
        public int BroodCount;

        /// <summary>권속 소환 쿨다운(초). 0 이면 SO 기본값.</summary>
        public float BroodCooldown;

        // ── 겉모습 (사용자 요청, 2026-09-09) ──────────────────
        //
        //  ■ 색조 = 계보 안에서 가른다 · 덩치 = 종족끼리 가른다
        //    진화체는 뿌리와 **같은 그림**을 쓴다(인간형은 같은 EnemyRace,
        //    비인간형은 같은 라이브러리). 라인에 서면 힐/독/강철 슬라임이
        //    한 마리로 보인다 → 뿌리는 흰색, 진화체마다 옅은 색을 준다.
        //    고블린·오크·좀비는 셋 다 초록 계열 인간형이라 색으로는 안 갈린다
        //    → 덩치로 가른다 (고블린 작게 · 좀비 중간 · 오크 크게 · 트롤 제일 크게).
        //
        //  ⚠ 둘 다 비워 두면 기본값이다 (흰색 · 1배) — Create 가 채운다.
        //    struct 기본값이 0/투명이라 "안 적음" 과 "0" 이 구분된다.

        /// <summary>몸 색조. 비우면 흰색(원래 색).</summary>
        public Color Tint;

        /// <summary>덩치 배율. 비우면 1.</summary>
        public float Size;

        /// <summary>
        /// 몸 비율 (가로, 세로). 비우면 (1,1).
        ///
        /// ■ ⚠ 색조가 못 하는 일을 한다 (사용자 지적, 2026-09-09)
        ///   같은 라이브러리를 쓰는 계보는 색만 바꿔서는 갈리지 않는다 —
        ///   색조는 원본 그림에 **곱해지는** 값이라 바탕이 짙은 종족일수록
        ///   차이가 묻힌다(슬라임이 그렇다). 실루엣은 그 사정을 타지 않는다.
        ///   ⚠ 너무 벌리지 말 것 — 히트박스·분리 반경이 max(x, y) 에서 나온다.
        /// </summary>
        public Vector2 Shape;

        /// <summary>머리 위 표식 (MonsterMarkView). 대부분 None.</summary>
        public MonsterMark Mark;

        /// <summary>
        /// 시너지 표식. ⚠ 비우면 **에셋의 값을 건드리지 않는다** — 옛 종족의 표식은
        /// 에셋에만 있다(인스펙터에서 적었다). 2026-09-12 에 새로 넣은 종족만 여기서 채운다.
        /// </summary>
        public MonsterTag Tags;
    }

    // ── 표를 짧게 적기 위한 헬퍼 ─────────────────────────────
    //
    //  ⚠ 값의 뜻이 스탯에 따라 다르다
    //    비율 스탯(체력·공격력·공속·이속·사거리·치명피해) → 0.15 = +15%
    //    절대 스탯(방어율·치명확률·방어관통)              → 0.06 = +6%p
    //    구분은 StatRules.IsAbsoluteStat 하나가 소유한다.

    /// <summary>스탯 하나짜리 칸.</summary>
    static MonsterLevelBonus Lv(string label, StatType s1, float v1,
                                PassiveSkillType passive = PassiveSkillType.None)
        => new() { Label = label, Stat1 = s1, Value1 = v1, Passive = passive };

    /// <summary>
    /// 옅은 색조 하나. 0~255 로 적는다 — 눈으로 고른 값이 그대로 보이게.
    ///
    /// ⚠ 진하게 잡지 말 것
    ///   몸 전체에 곱해지므로 0.6 아래로 내려가면 그림이 아니라 실루엣이 된다.
    ///   장비 색조와 또 곱해진다는 것도 잊지 말 것 (MonsterGearVisual.Tint).
    /// </summary>
    static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f, 1f);

    /// <summary>스탯 둘짜리 칸.</summary>
    static MonsterLevelBonus Lv2(string label, StatType s1, float v1, StatType s2, float v2,
                                 PassiveSkillType passive = PassiveSkillType.None)
        => new()
        {
            Label = label, Stat1 = s1, Value1 = v1,
            HasStat2 = true, Stat2 = s2, Value2 = v2,
            Passive = passive,
        };

    // ──────────────────────────────────────────────────────────
    // ■ 로스터 — 여기가 도감의 정본이다
    // ──────────────────────────────────────────────────────────
    static readonly Spec[] Roster =
    {
        // ══════════════════════════════════════════════════════
        //  기본 종족 — 계보의 뿌리. 각자 고유 패시브를 하나씩 갖는다.
        // ══════════════════════════════════════════════════════

        new Spec
        {
            Id = "slime", Name = "슬라임", Body = MonsterBodyType.NonHumanoid,
            // ⚠ 노랑으로 구운 시트다 (MonsterLibraryCreator 색 변형, 2026-09-11) — 원본 초록은 독 슬라임 몫
            LibraryName = "SlugLibrary_Yellow",
            Traits = MonsterTrait.Swarm | MonsterTrait.Shield | MonsterTrait.NonHumanoid,
            Desc = "가장 싼 벽. 죽으면 둘로 나뉘어 실제로는 두 배를 막아 준다.",
            Species = SpeciesPassive.SplitOnDeath,
            ManaCost = 5f, Count = 8,
            Attack = MonsterAttackKind.Melee,
            Hp = 80f, Atk = 5f, Range = 1.0f, AtkSpeed = 0.8f, MoveSpeed = 1.6f, Defense = 0.08f,
            ToHp = 1.4f, ToAtk = 0.5f,
            Tags = MonsterTag.Swarm | MonsterTag.Forest,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("점액질",   StatType.MaxHp, 0.15f),
                Lv2("굳은 껍질", StatType.Defense, 0.06f, StatType.MaxHp, 0.10f),
                Lv ("끈적임",   StatType.MoveSpeed, 0.12f, PassiveSkillType.DefenseShield),
                Lv2("분열 촉진", StatType.MaxHp, 0.20f, StatType.Defense, 0.05f),
            },
            Signature = PassiveSkillType.DefenseShield,
        },
        new Spec
        {
            Id = "skeleton", Name = "스켈레톤", Body = MonsterBodyType.Humanoid,
            Race = EnemyRace.Skeleton,
            Traits = MonsterTrait.Swarm | MonsterTrait.Charger,
            Desc = "싸게 여럿 나와 앞줄을 채운다. 부서져도 절반은 다시 일어난다.",
            Species = SpeciesPassive.Reassemble,
            ManaCost = 6f, Count = 8,
            Attack = MonsterAttackKind.Melee,
            Hp = 55f, Atk = 8f, Range = 1.2f, AtkSpeed = 1.1f, MoveSpeed = 2.8f, Defense = 0.02f,
            ToHp = 0.8f, ToAtk = 0.9f,
            Tags = MonsterTag.Swarm | MonsterTag.Undead,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("마른 뼈",     StatType.Attack, 0.12f),
                Lv2("빠른 손놀림", StatType.AttackSpeed, 0.10f, StatType.MoveSpeed, 0.08f),
                Lv ("뼈 창",       StatType.Attack, 0.12f, PassiveSkillType.KillMomentum),
                Lv2("완전 재조립", StatType.MaxHp, 0.15f, StatType.Attack, 0.12f),
            },
            Signature = PassiveSkillType.KillMomentum,
        },
        new Spec
        {
            Id = "goblin", Name = "고블린", Body = MonsterBodyType.Humanoid,
            Race = EnemyRace.Goblin,
            Traits = MonsterTrait.Swarm | MonsterTrait.Charger,
            Desc = "빠르고 무르다. 죽을 때 훔쳐 둔 골드를 떨군다 — 죽어도 벌이는 된다.",
            Species = SpeciesPassive.Loot,
            ManaCost = 6f, Count = 7,
            Attack = MonsterAttackKind.Melee,
            Hp = 48f, Atk = 10f, Range = 1.0f, AtkSpeed = 1.3f, MoveSpeed = 3.4f, Defense = 0f,
            ToHp = 0.7f, ToAtk = 1.1f,
            Size = 0.85f,                       // 초록 셋 중 가장 작다
            Tags = MonsterTag.Swarm | MonsterTag.Ferocity,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("도둑 근성",   StatType.MoveSpeed, 0.12f),
                Lv2("급소 찌르기", StatType.CritChance, 0.08f, StatType.Attack, 0.08f),
                Lv ("날랜 손",     StatType.AttackSpeed, 0.12f, PassiveSkillType.SwiftAssault),
                Lv2("대약탈",      StatType.Attack, 0.15f, StatType.CritDamage, 0.30f),
            },
            Signature = PassiveSkillType.SwiftAssault,
        },
        new Spec
        {
            Id = "zombie", Name = "좀비", Body = MonsterBodyType.Humanoid,
            Race = EnemyRace.ZombieA,
            Traits = MonsterTrait.Swarm | MonsterTrait.Shield | MonsterTrait.Sustained,
            Desc = "느리지만 잘 안 죽고, 죽으면 주변에 역병을 퍼뜨린다.",
            Species = SpeciesPassive.PlagueBurst,
            // ⚠ 7 → 6 (사용자 지시, 2026-09-15) — 같은 역할의 슬라임보다 마나당 체력·DPS 가 모두 낮아
            //   기본 단계 꼴찌(전투력 0.77)였다. 역병 폭발은 지수에 안 잡힌다.
            ManaCost = 6f, Count = 6,
            Attack = MonsterAttackKind.Melee,
            Hp = 105f, Atk = 7f, Range = 1.1f, AtkSpeed = 0.7f, MoveSpeed = 1.7f, Defense = 0.08f,
            ToHp = 1.3f, ToAtk = 0.6f,
            Tags = MonsterTag.Swarm | MonsterTag.Plague,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("썩지 않는 살", StatType.MaxHp, 0.18f),
                Lv2("무딘 감각",    StatType.Defense, 0.05f, StatType.MaxHp, 0.10f),
                Lv ("전염",         StatType.Attack, 0.12f, PassiveSkillType.VampiricStrike),
                Lv2("창궐",         StatType.MaxHp, 0.20f, StatType.Attack, 0.10f),
            },
            Signature = PassiveSkillType.QuickRecovery,
        },
        new Spec
        {
            Id = "wolf", Name = "늑대", Body = MonsterBodyType.NonHumanoid,
            LibraryName = "WolfLibrary",
            Traits = MonsterTrait.Charger | MonsterTrait.NonHumanoid,
            Desc = "가장 빠르다. 사냥할수록 더 빨라져 뒷줄까지 파고든다.",
            Species = SpeciesPassive.PackHunt,
            ManaCost = 7f, Count = 5,
            Attack = MonsterAttackKind.Melee,
            Hp = 70f, Atk = 15f, Range = 1.0f, AtkSpeed = 1.5f, MoveSpeed = 4.2f, Defense = 0.02f,
            ToHp = 0.7f, ToAtk = 1.3f,
            Tags = MonsterTag.Vanguard | MonsterTag.Hunt,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("사냥 본능", StatType.MoveSpeed, 0.15f),
                Lv2("물어뜯기",  StatType.Attack, 0.12f, StatType.CritChance, 0.06f),
                Lv ("추격",      StatType.AttackSpeed, 0.12f, PassiveSkillType.KillMomentum),
                Lv2("우두머리",  StatType.Attack, 0.15f, StatType.MoveSpeed, 0.10f),
            },
            Signature = PassiveSkillType.Slaughterer,
        },
        new Spec
        {
            Id = "orc", Name = "오크", Body = MonsterBodyType.Humanoid,
            Race = EnemyRace.Orc,
            Traits = MonsterTrait.Reinforced | MonsterTrait.Shield,
            Desc = "두꺼운 인간형. 쓰러뜨릴수록 공격력이 누적돼 오래 살수록 세진다.",
            Species = SpeciesPassive.Bloodlust,
            ManaCost = 8f, Count = 4,
            Attack = MonsterAttackKind.Melee,
            Hp = 150f, Atk = 16f, Range = 1.3f, AtkSpeed = 0.85f, MoveSpeed = 2.3f, Defense = 0.15f,
            ToHp = 1.1f, ToAtk = 1.1f,
            Size = 1.20f,                       // 좀비보다 크고 트롤보다 작다
            Tags = MonsterTag.Ferocity | MonsterTag.Steel,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("전투 근육",  StatType.Attack, 0.15f),
                Lv2("두꺼운 가죽", StatType.MaxHp, 0.12f, StatType.Defense, 0.05f),
                Lv ("파쇄",       StatType.DefensePenetration, 0.08f, PassiveSkillType.StrengthStack),
                Lv2("광란",       StatType.Attack, 0.18f, StatType.AttackSpeed, 0.10f),
            },
            Signature = PassiveSkillType.StrengthStack,
        },
        new Spec
        {
            Id = "hog", Name = "멧돼지", Body = MonsterBodyType.NonHumanoid,
            LibraryName = "HogLibrary",
            Traits = MonsterTrait.Charger | MonsterTrait.Shield | MonsterTrait.NonHumanoid,
            Desc = "일정 간격으로 한 놈을 골라 들이받는다. 첫 접전을 여는 데 쓴다.",
            Species = SpeciesPassive.Sturdy,
            Skill   = ActiveSkillId.HeavyStrike,   // 강타 — 단일 대상 돌진 타격 + 넉백
            ManaCost = 8f, Count = 4,
            Attack = MonsterAttackKind.Melee,
            Hp = 130f, Atk = 17f, Range = 1.1f, AtkSpeed = 0.9f, MoveSpeed = 3.0f, Defense = 0.12f,
            ToHp = 1.2f, ToAtk = 1.0f,
            Tags = MonsterTag.Vanguard | MonsterTag.Beast,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("두터운 등가죽", StatType.MaxHp, 0.15f),
                Lv2("억센 발굽",     StatType.MoveSpeed, 0.10f, StatType.Defense, 0.06f),
                Lv ("무쇠 돌진",     StatType.Attack, 0.15f, PassiveSkillType.CounterStrike),
                Lv2("저돌맹진",      StatType.MaxHp, 0.18f, StatType.Attack, 0.12f),
            },
            Signature = PassiveSkillType.CounterStrike,
        },
        new Spec
        {
            Id = "lich", Name = "리치", Body = MonsterBodyType.Humanoid,
            Race = EnemyRace.Demon,
            Traits = MonsterTrait.Reinforced | MonsterTrait.Ranged,
            Desc = "뒤에서 때리며 준 피해만큼 스스로 회복한다. 앞을 막아 줄 벽이 필요하다.",
            Species = SpeciesPassive.SoulDrain,
            ManaCost = 10f, Count = 3,
            Attack = MonsterAttackKind.Ranged, Projectile = MonsterProjectileKind.MagicBolt,
            ProjSpeed = 14f,
            Hp = 85f, Atk = 26f, Range = 8.5f, AtkSpeed = 0.6f, MoveSpeed = 2.0f, Defense = 0.03f,
            ToHp = 0.6f, ToAtk = 1.6f,
            Tags = MonsterTag.Marksman | MonsterTag.Sorcery,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("마력 순환", StatType.Attack, 0.15f),
                Lv2("먼 시야",   StatType.AttackRange, 0.12f, StatType.AttackSpeed, 0.08f),
                Lv ("영혼 착취", StatType.Attack, 0.12f, PassiveSkillType.FocusedFire),
                Lv2("대마력",    StatType.Attack, 0.18f, StatType.AttackRange, 0.10f),
            },
            Signature = PassiveSkillType.VampiricStrike,
            // ⚠ 술법 표식 = 쿨감이라 깎을 쿨다운이 있어야 한다 (사용자 지시, 2026-09-15)
            //   ⚠ 뿌리는 **충격파**다 (사용자 지시, 2026-09-15) — 블리자드와 비전 리치의 것을 맞바꿨다.
            //     원거리 딜러가 파고든 적을 제 앞에서 밀어낸다. 장판은 진화체의 몫이다.
            //
            //   ⚠ 메테오를 걷어냈다 (사용자 지적, 2026-09-15 — "오버 밸런스")
            //     뿌리 종족은 마나 10에 **3기**가 나온다. 3기가 각자 31초마다 대폭발을
            //     돌리면 그것만으로 판이 정리돼, 위에 있는 두 진화체의 스킬이 값을 잃는다.
            //     한 방짜리 큰 스킬은 **마릿수 1인 2차**(리치 킹)의 몫이다.
            //   ⚠ 희귀 스킬(21~30)도 여기 두지 않는다 — 같은 이유로 전부 2차의 몫이다.
            //   ⚠ 비석 강림(Gravestone)도 아니다 — **스켈레톤을 공짜로 낳는다**(12기/시전).
            //     마나를 안 낸 물량이 판마다 곱으로 는다. 소환 경제에 구멍을 내지 않는다.
            Skill = ActiveSkillId.Shockwave,
        },
        new Spec
        {
            Id = "troll", Name = "트롤", Body = MonsterBodyType.NonHumanoid,
            LibraryName = "TrollLibrary",
            Traits = MonsterTrait.Reinforced | MonsterTrait.Shield | MonsterTrait.NonHumanoid,
            Desc = "가장 두껍다. 초당 체력을 재생해 오래 붙들고 버틴다.",
            Species = SpeciesPassive.Regrow,
            ManaCost = 12f, Count = 3,
            Attack = MonsterAttackKind.Melee,
            // ⚠ 사거리는 덩치와 한 묶음이다 (사용자 지적, 2026-09-09)
            //   공격 판정은 **중심 사이 거리**를 본다(UnitAttackSystem). 덩치를 키우면
            //   분리(Separation)가 중심을 더 멀리 떼어 놓으므로, 그림에서는 몸이 거의
            //   닿았는데 사거리 밖이라 가만히 서 있는 상태가 된다.
            //   1.6 → 2.1 : 늘어난 반경(0.75 × 0.45 ≈ 0.34)을 덮고 조금 남는 값이다.
            Hp = 260f, Atk = 30f, Range = 2.1f, AtkSpeed = 0.6f, MoveSpeed = 2.0f, Defense = 0.20f,
            ToHp = 1.5f, ToAtk = 1.4f,
            Size = 1.45f,                       // 전장에서 가장 큰 덩치
            Tags = MonsterTag.Regrowth | MonsterTag.Beast,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("거목의 몸", StatType.MaxHp, 0.20f),
                Lv2("굳은 피부", StatType.Defense, 0.06f, StatType.Attack, 0.08f),
                Lv ("재생 촉진", StatType.MaxHp, 0.15f, PassiveSkillType.IronWill),
                Lv2("산의 무게", StatType.Attack, 0.15f, StatType.DefensePenetration, 0.06f),
            },
            Signature = PassiveSkillType.SteelBody,
        },

        // ══════════════════════════════════════════════════════
        //  업그레이드 종족 — 뿌리의 패시브를 물려받고 하나를 더 얻는다.
        //
        //  ⚠ Species 칸에 뿌리 패시브를 다시 적지 말 것
        //    상속으로 자동으로 붙는다. 두 번 적으면 분열이 두 번 일어난다.
        //  ⚠ 새 그림이 필요 없다 — 뿌리와 같은 외형을 쓰고 색·수치만 다르다.
        // ══════════════════════════════════════════════════════

        // ── 슬라임 계열 ──────────────────────────────────────
        new Spec
        {
            Id = "heal_slime", Name = "힐 슬라임", UpgradeOf = "slime",
            Body = MonsterBodyType.NonHumanoid, LibraryName = "SlugLibrary_Pink",   // 분홍 시트
            Traits = MonsterTrait.Swarm | MonsterTrait.Shield
                   | MonsterTrait.Ranged | MonsterTrait.NonHumanoid,
            Desc = "주기적으로 주변의 다친 아군을 치유하고, 죽을 때도 회복을 남긴다.\n" +
                   "앞줄 바로 뒤에서 점액을 뱉는다 — 맞지 않고 줄 전체를 오래 버티게 한다.",
            Species = SpeciesPassive.HealOnDeath,
            // ⚠ 치유 스킬이 있어야 술법 시너지(쿨감)를 받는다 (사용자 지시, 2026-09-11)
            Skill   = ActiveSkillId.SlimeMend,
            ManaCost = 7f, Count = 6,
            // ── 원거리로 돌렸다 (사용자 지시, 2026-09-15) ──
            //   치유가 본업인데 앞줄에 서서 제일 먼저 죽었다. 뒤에 서면 역할이 뚜렷해진다.
            //
            //   ⚠ 사거리는 SlimeMend 의 반경(3.5)을 넘기지 않는다
            //     넘기면 앞줄이 치유 반경 밖으로 빠져 **아무도 안 낫는다** — 원거리로 만든
            //     것이 곧 이 카드를 죽이는 길이 된다. 사거리와 치유 반경은 한 묶음이다.
            //   ⚠ 비인간형이라 무기를 들지 않는다 (통짜 라이브러리) — 점액을 뱉는 그림이다.
            Attack = MonsterAttackKind.Ranged, Projectile = MonsterProjectileKind.MagicBolt,
            ProjSpeed = 9f,
            // ⚠ 공격 13 → 8 · 체력 195 → 300 (사용자 지시, 2026-09-11 "공격 낮추고 체력 높이자")
            //   진화 검산 — 슬라임 대비 마나당 전투력 지수 ×1.22 (최소 ×1.15, IndexOf)
            Hp = 300f, Atk = 8f, Range = 3.4f, AtkSpeed = 0.8f, MoveSpeed = 1.6f, Defense = 0.08f,
            ToHp = 1.5f, ToAtk = 0.4f,
            // ⚠ 색만으로는 안 갈린다 — 납작하게 눌러 실루엣을 바꾼다 (Shape 주석)
            // ⚠ 색조를 걷었다 — 색은 시트가 준다 (SlugLibrary_Pink). 곱하면 분홍이 탁해진다
            // ⚠ 몸 비율을 걷어냈다 (사용자 지적, 2026-09-16 — "인게임에서 납작하다")
            //   예전엔 슬라임 계열이 같은 초록 그림이라 (1.20, 0.84) 로 눌러 갈랐다.
            //   지금은 분홍 시트(SlugLibrary_Pink)가 색으로 가르고, 초상화엔 비율이 안 들어가 카드·도감과 필드 모양만 달랐다.
            Mark = MonsterMark.Cross,   // 머리 위 십자 (사용자 지적, 2026-09-11 — 독 슬라임과 안 갈렸다)
            Tags = MonsterTag.Forest | MonsterTag.Sorcery,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("치유 점액", StatType.MaxHp, 0.18f),
                Lv2("포근한 껍질", StatType.Defense, 0.06f, StatType.MaxHp, 0.12f),
                Lv ("생명의 잔재", StatType.MaxHp, 0.15f, PassiveSkillType.QuickRecovery),
                Lv2("대치유",    StatType.MaxHp, 0.22f, StatType.MoveSpeed, 0.10f),
            },
            Signature = PassiveSkillType.QuickRecovery,
        },
        new Spec
        {
            Id = "poison_slime", Name = "독 슬라임", UpgradeOf = "slime",
            // 원본 초록 그대로 (사용자 지시, 2026-09-11) — 변형을 굽지 않는다
            Body = MonsterBodyType.NonHumanoid, LibraryName = "SlugLibrary",
            Traits = MonsterTrait.Swarm | MonsterTrait.Sustained | MonsterTrait.NonHumanoid,
            Desc = "분열하고, 자기를 때린 적을 중독시킨다.\n맞을수록 이득이라 앞줄에 세워 둔다.",
            Species = SpeciesPassive.PoisonOnHit,
            ManaCost = 7f, Count = 6,
            Attack = MonsterAttackKind.Melee,
            Hp = 140f, Atk = 16f, Range = 1.0f, AtkSpeed = 0.8f, MoveSpeed = 1.7f, Defense = 0.05f,
            ToHp = 1.2f, ToAtk = 0.9f,
            // ⚠ 색조를 걷었다 — 원본 시트가 이미 초록이다 (SlugLibrary). 기본 슬라임이 노랑으로 갔다
            Shape = new Vector2(0.84f, 1.24f),   // 길쭉
            Mark = MonsterMark.Drop,    // 머리 위 물방울
            Tags = MonsterTag.Forest | MonsterTag.Plague,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("맹독 분비", StatType.Attack, 0.15f),
                Lv2("독 껍질",   StatType.MaxHp, 0.12f, StatType.Defense, 0.05f),
                Lv ("역류",      StatType.Attack, 0.12f, PassiveSkillType.VampiricStrike),
                Lv2("맹독 폭주", StatType.Attack, 0.20f, StatType.AttackSpeed, 0.10f),
            },
            Signature = PassiveSkillType.VampiricStrike,
        },
        new Spec
        {
            Id = "steel_slime", Name = "강철 슬라임", UpgradeOf = "slime",
            Body = MonsterBodyType.NonHumanoid, LibraryName = "SlugLibrary_Steel",   // 회청 시트
            Traits = MonsterTrait.Swarm | MonsterTrait.Shield | MonsterTrait.NonHumanoid,
            Desc = "분열하고, 받은 피해의 일부를 그대로 되돌려준다.\n근접 용사 앞에 세울수록 값어치가 커진다.",
            Species = SpeciesPassive.ThornOnHit,
            ManaCost = 8f, Count = 5,
            Attack = MonsterAttackKind.Melee,
            Hp = 335f, Atk = 15f, Range = 1.0f, AtkSpeed = 0.7f, MoveSpeed = 1.4f, Defense = 0.18f,
            ToHp = 1.6f, ToAtk = 0.4f,
            // ⚠ 색조를 걷었다 — 색은 시트가 준다 (SlugLibrary_Steel)
            Shape = new Vector2(1.12f, 1.02f), Size = 1.12f,   // 크고 두껍다
            Tags = MonsterTag.Steel | MonsterTag.Regrowth,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("강철 껍질", StatType.Defense, 0.08f),
                Lv2("무거운 몸", StatType.MaxHp, 0.20f, StatType.Defense, 0.05f),
                Lv ("가시 강화", StatType.MaxHp, 0.15f, PassiveSkillType.ShieldEdge),
                Lv2("철벽",      StatType.Defense, 0.08f, StatType.Attack, 0.12f),
            },
            Signature = PassiveSkillType.ShieldEdge,
        },

        // ── 스켈레톤 계열 ────────────────────────────────────
        new Spec
        {
            Id = "skeleton_mage", Name = "해골 술사", UpgradeOf = "skeleton",
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Skeleton,
            Traits = MonsterTrait.Ranged | MonsterTrait.Sustained,
            Desc = "다시 일어나고, 부서질 때 뼈 파편이 주변으로 터진다.\n뒤에서 굵게 때린다.",
            Species = SpeciesPassive.ExplodeOnDeath,
            ManaCost = 8f, Count = 4,
            Attack = MonsterAttackKind.Ranged, Projectile = MonsterProjectileKind.MagicBolt,
            ProjSpeed = 12f,
            Hp = 115f, Atk = 53f, Range = 7.5f, AtkSpeed = 0.55f, MoveSpeed = 2.2f, Defense = 0.02f,
            ToHp = 0.6f, ToAtk = 1.5f,
            Tint = Rgb(190, 175, 255),          // 보랏빛 — 술사
            Tags = MonsterTag.Undead | MonsterTag.Marksman,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("뼈 마력",   StatType.Attack, 0.15f),
                Lv2("먼 조준",   StatType.AttackRange, 0.12f, StatType.AttackSpeed, 0.08f),
                Lv ("파편 증폭", StatType.Attack, 0.12f, PassiveSkillType.FocusedFire),
                Lv2("해골 폭풍", StatType.Attack, 0.18f, StatType.AttackRange, 0.10f),
            },
            Signature = PassiveSkillType.WideRange,
            // ⚠ 술법 표식 = 쿨감이라 깎을 쿨다운이 있어야 한다 (사용자 지시, 2026-09-15)
            //   저티어 술사라 단일기다 — 한 기를 묶어 두고 뒤에서 마저 때린다.
            Skill = ActiveSkillId.Bind,
        },
        new Spec
        {
            Id = "skeleton_guard", Name = "해골 방패병", UpgradeOf = "skeleton",
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Skeleton,
            Traits = MonsterTrait.Swarm | MonsterTrait.Shield,
            Desc = "다시 일어나고, 맞을 때마다 방어가 단단해진다.\n두 번 막아 주는 앞줄.",
            Species = SpeciesPassive.Bulwark,
            ManaCost = 8f, Count = 5,
            Attack = MonsterAttackKind.Melee,
            Hp = 235f, Atk = 17f, Range = 1.1f, AtkSpeed = 0.8f, MoveSpeed = 2.3f, Defense = 0.16f,
            ToHp = 1.4f, ToAtk = 0.5f,
            Tint = Rgb(190, 215, 240),          // 창백한 청 — 방패
            Tags = MonsterTag.Undead | MonsterTag.Steel,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("두꺼운 방패", StatType.Defense, 0.08f),
                Lv2("버티는 뼈",   StatType.MaxHp, 0.18f, StatType.Defense, 0.05f),
                Lv ("방벽 강화",   StatType.MaxHp, 0.12f, PassiveSkillType.IronWill),
                Lv2("불괴",        StatType.Defense, 0.07f, StatType.Attack, 0.12f),
            },
            Signature = PassiveSkillType.DefenseShield,
        },
        // ⚠ 새 업그레이드 (사용자 지시, 2026-09-12) — '마나 방출'
        //   진화 검산: 스켈레톤 대비 마나당 전투력 지수 ×1.22 (최소 ×1.15, IndexOf)
        new Spec
        {
            Id = "mana_skeleton", Name = "마력 해골", UpgradeOf = "skeleton",
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Skeleton,
            Traits = MonsterTrait.Swarm | MonsterTrait.Charger,
            Desc = "다시 일어나고, 부서질 때 흩어진 마력을 돌려준다.\n물량으로 밀어 넣고 마나를 되찾는다.",
            Species = SpeciesPassive.ManaRelease,
            ManaCost = 6f, Count = 8,
            Attack = MonsterAttackKind.Melee,
            Hp = 62f, Atk = 9.5f, Range = 1.2f, AtkSpeed = 1.1f, MoveSpeed = 2.8f, Defense = 0.02f,
            ToHp = 0.8f, ToAtk = 0.9f,
            Tint = Rgb(165, 205, 255),          // 푸른 마력
            Tags = MonsterTag.Undead | MonsterTag.Swarm,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("마력 뼈",     StatType.Attack, 0.12f),
                Lv2("가벼운 뼈",   StatType.AttackSpeed, 0.10f, StatType.MoveSpeed, 0.08f),
                Lv ("흩어진 마력", StatType.MaxHp, 0.12f, PassiveSkillType.KillMomentum),
                Lv2("마력 재조립", StatType.MaxHp, 0.15f, StatType.Attack, 0.12f),
            },
            Signature = PassiveSkillType.KillMomentum,
        },

        // ── 고블린 계열 ──────────────────────────────────────
        new Spec
        {
            Id = "goblin_archer", Name = "고블린 궁수", UpgradeOf = "goblin",
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Goblin,
            Traits = MonsterTrait.Swarm | MonsterTrait.Ranged,
            Desc = "골드를 떨구고, 한 번에 두 발을 쏜다.\n혼자 두면 붙는 즉시 녹는다 — 벽과 함께 쓴다.",
            Species = SpeciesPassive.Volley,
            ManaCost = 7f, Count = 6,
            Attack = MonsterAttackKind.Ranged, Projectile = MonsterProjectileKind.Arrow,
            // 공격 19 → 20 (2026-09-15) — 진화 검산이 ×1.149 로 문턱(1.15) 바로 아래였다
            Hp = 70f, Atk = 20f, Range = 6.0f, AtkSpeed = 1.0f, MoveSpeed = 2.6f, Defense = 0f,
            ToHp = 0.6f, ToAtk = 1.2f,
            Tint = Rgb(240, 235, 160), Size = 0.85f,   // 노란기 · 뿌리와 같은 덩치
            Tags = MonsterTag.Marksman | MonsterTag.Hunt,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("정조준",   StatType.AttackRange, 0.12f),
                Lv2("속사",     StatType.AttackSpeed, 0.12f, StatType.CritChance, 0.06f),
                Lv ("관통 화살", StatType.DefensePenetration, 0.08f, PassiveSkillType.FocusedFire),
                Lv2("탄막",     StatType.Attack, 0.18f, StatType.AttackRange, 0.10f),
            },
            Signature = PassiveSkillType.FocusedFire,
        },
        new Spec
        {
            Id = "goblin_chief", Name = "고블린 두목", UpgradeOf = "goblin",
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Goblin,
            Traits = MonsterTrait.Charger | MonsterTrait.Reinforced,
            Desc = "골드를 떨구고, 쓰러질 때 주변 아군의 공격력을 끌어올린다.\n물량 한복판에 섞어 넣는 카드.",
            Species = SpeciesPassive.RallyOnDeath,
            ManaCost = 9f, Count = 4,
            Attack = MonsterAttackKind.Melee,
            Hp = 165f, Atk = 38f, Range = 1.1f, AtkSpeed = 1.2f, MoveSpeed = 3.2f, Defense = 0.06f,
            ToHp = 0.9f, ToAtk = 1.3f,
            Tint = Rgb(255, 175, 150), Size = 0.95f,   // 붉은기 · 두목이라 조금 크다
            Tags = MonsterTag.Ferocity | MonsterTag.Vanguard,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("두목의 위압", StatType.Attack, 0.15f),
                Lv2("단단한 갑옷", StatType.MaxHp, 0.15f, StatType.Defense, 0.05f),
                Lv ("호령",        StatType.AttackSpeed, 0.12f, PassiveSkillType.KillEmpower),
                Lv2("최후의 명령", StatType.Attack, 0.18f, StatType.CritDamage, 0.30f),
            },
            Signature = PassiveSkillType.KillEmpower,
        },

        // ── 좀비 계열 ────────────────────────────────────────
        new Spec
        {
            Id = "plague_zombie", Name = "역병 좀비", UpgradeOf = "zombie",
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.ZombieB,
            Traits = MonsterTrait.Swarm | MonsterTrait.Ranged | MonsterTrait.Sustained,
            Desc = "썩은 것을 던져 역병을 퍼뜨리고, 터지며 주변에 피해까지 준다.\n" +
                   "한 마리씩 죽어 나갈수록 판이 정리된다.",
            Species = SpeciesPassive.ExplodeOnDeath,
            // ⚠ 9 → 8 · 공·체 +6% (2026-09-15) — 좀비가 마나 7 → 6 으로 싸지며 진화 검산(×1.15)이 깨졌다
            ManaCost = 8f, Count = 5,
            // ── 원거리로 돌렸다 (사용자 지시, 2026-09-15) ──
            //   개성이 **사망 발동**(ExplodeOnDeath)이라 접촉 조건이 없다 — 뒤에서 던지다
            //   결국 휩쓸려 터진다. 맞아야 값을 하는 종족(독 슬라임·해골 방패병)과 갈리는 점이다.
            //   ⚠ 사거리를 리치 계열(8.5~9)까지 올리지 않는다 — 술사의 자리를 뺏는다.
            Attack = MonsterAttackKind.Ranged, Projectile = MonsterProjectileKind.MagicBolt,
            ProjSpeed = 10f,
            Hp = 175f, Atk = 17f, Range = 5.0f, AtkSpeed = 0.7f, MoveSpeed = 1.8f, Defense = 0.08f,
            ToHp = 1.38f, ToAtk = 0.85f,
            Tint = Rgb(200, 240, 150),          // 병색 — 역병
            Tags = MonsterTag.Plague | MonsterTag.Marksman,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("역병 축적", StatType.Attack, 0.15f),
                Lv2("부패한 몸", StatType.MaxHp, 0.18f, StatType.Defense, 0.05f),
                Lv ("전염 확산", StatType.Attack, 0.12f, PassiveSkillType.VampiricStrike),
                Lv2("대역병",    StatType.MaxHp, 0.18f, StatType.Attack, 0.15f),
            },
            Signature = PassiveSkillType.VampiricStrike,
        },

        // ── 좀비 계열 — 두 번째 1차 (사용자 지시, 2026-09-15) ──
        //  1차가 하나뿐인 뿌리에 갈래를 하나씩 더했다. 역병 좀비가 '느린 원거리 역병' 이라
        //  구울은 반대로 **빠르게 달려드는 근접 물량**이다.
        //  ⚠ 시너지 표식은 비워 뒀다 — 시너지 종류를 늘리는 결정이 먼저다.
        new Spec
        {
            Id = "ghoul", Name = "구울", UpgradeOf = "zombie",
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.ZombieA,
            Traits = MonsterTrait.Swarm | MonsterTrait.Charger | MonsterTrait.Sustained,
            Desc = "굶주린 시체. 빠르게 달려들어 물어뜯는 대신 몸이 쉽게 무너진다.",
            Species = SpeciesPassive.Hunger,
            ManaCost = 7f, Count = 5,
            Attack = MonsterAttackKind.Melee,
            Hp = 110f, Atk = 22f, Range = 1.1f, AtkSpeed = 1.1f, MoveSpeed = 2.6f, Defense = 0.04f,
            ToHp = 0.9f, ToAtk = 1.0f,
            Tint = Rgb(215, 228, 200),          // 창백한 잿빛 — 좀비(민무늬)·역병 좀비(병색 연두)와 갈린다
            Tags = MonsterTag.Swarm | MonsterTag.Undead,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("날카로운 손톱", StatType.AttackSpeed, 0.10f),
                Lv2("썩은 이빨",     StatType.Attack, 0.12f, StatType.CritChance, 0.05f),
                Lv ("탐식",          StatType.MaxHp, 0.15f, PassiveSkillType.VampiricStrike),
                Lv2("굶주린 무리",   StatType.Attack, 0.15f, StatType.AttackSpeed, 0.10f),
            },
            Signature = PassiveSkillType.VampiricStrike,
        },

        // ── 늑대 계열 ────────────────────────────────────────
        new Spec
        {
            Id = "frost_wolf", Name = "서리 늑대", UpgradeOf = "wolf",
            Body = MonsterBodyType.NonHumanoid, LibraryName = "WolfLibrary_Frost",   // 얼음 청색 시트 (2026-09-11)
            Traits = MonsterTrait.Charger | MonsterTrait.NonHumanoid,
            Desc = "사냥할수록 빨라지고, 자기를 때린 적을 얼려 둔화시킨다.",
            Species = SpeciesPassive.ChillOnHit,
            ManaCost = 9f, Count = 4,
            Attack = MonsterAttackKind.Melee,
            Hp = 135f, Atk = 31f, Range = 1.0f, AtkSpeed = 1.4f, MoveSpeed = 4.0f, Defense = 0.04f,
            ToHp = 0.8f, ToAtk = 1.3f,
            // ⚠ 색조를 걷었다 — 색은 시트가 준다 (WolfLibrary_Frost)
            Shape = new Vector2(1.08f, 0.94f),   // 낮고 길다
            Tags = MonsterTag.Beast | MonsterTag.Hunt,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("서리 발톱", StatType.Attack, 0.15f),
                Lv2("빙결 질주", StatType.MoveSpeed, 0.15f, StatType.AttackSpeed, 0.08f),
                Lv ("한기",      StatType.MaxHp, 0.12f, PassiveSkillType.CounterStrike),
                Lv2("혹한",      StatType.Attack, 0.18f, StatType.CritChance, 0.08f),
            },
            Signature = PassiveSkillType.CounterStrike,
        },

        // 핏빛 늑대 — 두 번째 1차 (2026-09-15). 서리 늑대가 '맞으면 얼린다' 라 이쪽은 **치명타로 뚫는 사냥꾼**.
        new Spec
        {
            Id = "blood_wolf", Name = "핏빛 늑대", UpgradeOf = "wolf",
            Body = MonsterBodyType.NonHumanoid, LibraryName = "WolfLibrary_Blood",   // 붉은 털 시트
            Traits = MonsterTrait.Charger | MonsterTrait.NonHumanoid,
            Desc = "피 냄새를 쫓는 사냥꾼. 급소를 물어 갑옷을 무시한다.",
            Species = SpeciesPassive.VitalStrike,
            ManaCost = 9f, Count = 4,
            Attack = MonsterAttackKind.Melee,
            // ⚠ 공격 36 이 상한이다 — 더 올리면 알파 울프(2차)의 진화 검산이 깨진다
            Hp = 125f, Atk = 36f, Range = 1.0f, AtkSpeed = 1.3f, MoveSpeed = 3.9f, Defense = 0.03f,
            ToHp = 0.7f, ToAtk = 1.4f,
            Shape = new Vector2(1.04f, 0.98f),
            Tags = MonsterTag.Hunt | MonsterTag.Vanguard,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("피 냄새",     StatType.CritChance, 0.08f),
                Lv2("찢는 송곳니", StatType.Attack, 0.12f, StatType.CritDamage, 0.15f),
                Lv ("사냥의 끝",   StatType.MaxHp, 0.12f, PassiveSkillType.Slaughterer),
                Lv2("붉은 달",     StatType.Attack, 0.18f, StatType.CritChance, 0.06f),
            },
            Signature = PassiveSkillType.Slaughterer,
        },

        // ── 오크 계열 ────────────────────────────────────────
        new Spec
        {
            Id = "war_orc", Name = "전쟁 오크", UpgradeOf = "orc",
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Orc,
            Traits = MonsterTrait.Reinforced | MonsterTrait.Shield,
            Desc = "쓰러뜨릴수록 세지고, 쓰러질 때 주변 아군을 북돋운다.\n죽어도 값을 하는 앞줄.",
            Species = SpeciesPassive.RallyOnDeath,
            ManaCost = 10f, Count = 4,
            Attack = MonsterAttackKind.Melee,
            Hp = 210f, Atk = 25f, Range = 1.3f, AtkSpeed = 0.85f, MoveSpeed = 2.3f, Defense = 0.17f,
            ToHp = 1.2f, ToAtk = 1.1f,
            Tint = Rgb(255, 180, 165), Size = 1.20f,   // 붉은기 · 뿌리와 같은 덩치
            Tags = MonsterTag.Steel | MonsterTag.Ferocity,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("전열",     StatType.MaxHp, 0.15f),
                Lv2("전쟁 훈련", StatType.Attack, 0.15f, StatType.Defense, 0.05f),
                Lv ("전장의 함성", StatType.Attack, 0.12f, PassiveSkillType.StrengthStack),
                Lv2("불굴의 진형", StatType.MaxHp, 0.20f, StatType.DefensePenetration, 0.06f),
            },
            Signature = PassiveSkillType.ShieldEdge,
        },

        // 오크 도살자 — 두 번째 1차 (2026-09-15). 전쟁 오크가 '버티는 앞줄' 이라 이쪽은 **느린 한 방 딜러**.
        new Spec
        {
            Id = "orc_butcher", Name = "오크 도살자", UpgradeOf = "orc",
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Orc,
            Traits = MonsterTrait.Reinforced | MonsterTrait.Charger,
            Desc = "커다란 식칼을 휘두른다. 체력이 낮은 적은 반드시 급소를 맞는다.",
            Species = SpeciesPassive.Executioner,
            ManaCost = 10f, Count = 3,
            Attack = MonsterAttackKind.Melee,
            Hp = 230f, Atk = 48f, Range = 1.4f, AtkSpeed = 0.75f, MoveSpeed = 2.2f, Defense = 0.10f,
            ToHp = 1.0f, ToAtk = 1.4f,
            Tint = Rgb(228, 208, 182), Size = 1.25f,   // 누런 가죽 — 전쟁 오크(붉은기)와 갈린다
            Tags = MonsterTag.Hunt | MonsterTag.Ferocity,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("묵직한 칼",     StatType.Attack, 0.15f),
                Lv2("피범벅 앞치마", StatType.MaxHp, 0.12f, StatType.CritDamage, 0.15f),
                Lv ("도축",          StatType.Attack, 0.12f, PassiveSkillType.KillEmpower),
                Lv2("학살자",        StatType.Attack, 0.20f, StatType.CritChance, 0.06f),
            },
            Signature = PassiveSkillType.KillEmpower,
        },

        // ── 멧돼지 계열 ──────────────────────────────────────
        new Spec
        {
            Id = "flame_hog", Name = "화염 멧돼지", UpgradeOf = "hog",
            Body = MonsterBodyType.NonHumanoid, LibraryName = "HogLibrary_Flame",   // 불꽃 주황 시트 (2026-09-11)
            Traits = MonsterTrait.Charger | MonsterTrait.Sustained | MonsterTrait.NonHumanoid,
            Desc = "튼튼하고, 때린 자리마다 불이 붙는다.\n오래 붙어 싸울수록 화상이 겹쳐 쌓인다.",
            Species = SpeciesPassive.BurnOnAttack,
            // ⚠ 뿌리와 같은 돌진(HeavyStrike)을 쓰던 자리다 (사용자 지시, 2026-09-15)
            //   둘이 같은 스킬이라 진화해도 하는 짓이 똑같았다. 개성이 이미
            //   '때린 자리마다 불'(BurnOnAttack)이라, 그 불이 **몸 주위로 번지는 것**이
            //   계보의 다음 칸이다. 표식도 역병(도트 축)이라 축이 어긋나지 않는다.
            Skill   = ActiveSkillId.FlameAura,
            ManaCost = 10f, Count = 4,
            Attack = MonsterAttackKind.Melee,
            Hp = 155f, Atk = 30f, Range = 1.1f, AtkSpeed = 0.9f, MoveSpeed = 3.2f, Defense = 0.10f,
            ToHp = 1.0f, ToAtk = 1.3f,
            // ⚠ 색조를 걷었다 — 색은 시트가 준다 (HogLibrary_Flame)
            Shape = new Vector2(1.10f, 1.06f), Size = 1.10f,   // 한 덩치
            Tags = MonsterTag.Vanguard | MonsterTag.Plague,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("불붙은 엄니", StatType.Attack, 0.15f),
                Lv2("달아오른 가죽", StatType.MaxHp, 0.15f, StatType.MoveSpeed, 0.10f),
                Lv ("연소",        StatType.Attack, 0.12f, PassiveSkillType.CounterStrike),
                Lv2("화염 돌진",   StatType.Attack, 0.20f, StatType.AttackSpeed, 0.10f),
            },
            Signature = PassiveSkillType.BerserkerPact,
        },

        // 철갑 멧돼지 — 두 번째 1차 (2026-09-15). 화염 멧돼지가 '불붙은 돌격' 이라 이쪽은 **느리게 버티는 벽**.
        new Spec
        {
            Id = "iron_hog", Name = "철갑 멧돼지", UpgradeOf = "hog",
            Body = MonsterBodyType.NonHumanoid, LibraryName = "HogLibrary_Iron",   // 쇳빛 시트
            Traits = MonsterTrait.Shield | MonsterTrait.NonHumanoid,
            Desc = "쇠처럼 굳은 가죽. 느려진 대신 받는 피해가 줄어 앞줄을 오래 버틴다.",
            Species = SpeciesPassive.Rampart,
            ManaCost = 10f, Count = 4,
            Attack = MonsterAttackKind.Melee,
            Hp = 285f, Atk = 18f, Range = 1.1f, AtkSpeed = 0.85f, MoveSpeed = 2.6f, Defense = 0.20f,
            ToHp = 1.6f, ToAtk = 0.7f,
            Shape = new Vector2(1.12f, 1.00f), Size = 1.15f,
            Tags = MonsterTag.Steel | MonsterTag.Beast,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("쇠가죽",     StatType.Defense, 0.06f),
                Lv2("무거운 발굽", StatType.MaxHp, 0.15f, StatType.Defense, 0.04f),
                Lv ("철갑",       StatType.MaxHp, 0.12f, PassiveSkillType.SteelBody),
                Lv2("강철 방벽",   StatType.MaxHp, 0.20f, StatType.Attack, 0.10f),
            },
            Signature = PassiveSkillType.SteelBody,
        },

        // ── 리치 계열 ────────────────────────────────────────
        new Spec
        {
            Id = "archlich", Name = "대리치", UpgradeOf = "lich",
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Demon,
            Traits = MonsterTrait.Reinforced | MonsterTrait.Ranged | MonsterTrait.Sustained,
            Desc = "흡혈하며 때리고, 쓰러질 때 주변을 역병으로 덮는다.\n가장 비싼 원거리.",
            Species = SpeciesPassive.PlagueBurst,
            ManaCost = 13f, Count = 3,
            Attack = MonsterAttackKind.Ranged, Projectile = MonsterProjectileKind.MagicBolt,
            ProjSpeed = 15f,
            Hp = 130f, Atk = 39f, Range = 9.0f, AtkSpeed = 0.6f, MoveSpeed = 2.0f, Defense = 0.05f,
            ToHp = 0.7f, ToAtk = 1.7f,
            Tint = Rgb(200, 165, 255),          // 짙은 보라
            Tags = MonsterTag.Undead | MonsterTag.Plague,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("대마력 순환", StatType.Attack, 0.18f),
                Lv2("심연의 시야", StatType.AttackRange, 0.14f, StatType.AttackSpeed, 0.10f),
                Lv ("영혼 포식",   StatType.Attack, 0.12f, PassiveSkillType.VampiricStrike),
                Lv2("역병의 군주", StatType.Attack, 0.20f, StatType.AttackRange, 0.12f),
            },
            Signature = PassiveSkillType.WideRange,
            // ⚠ 술법 표식 = 쿨감이라 깎을 쿨다운이 있어야 한다 (사용자 지시, 2026-09-15)
            //   역병까지 함께 든 종족이라 장판이다 — 계보에서 리치(비석)와 갈린다.
            Skill = ActiveSkillId.PoisonZone,
        },
        // ⚠ 새 업그레이드 (사용자 지시, 2026-09-12) — '마나 공명'
        //   진화 검산: 리치 대비 마나당 전투력 지수 ×1.21 (최소 ×1.15, IndexOf)
        //   ⚠ 지수는 공명 몫을 모른다 — 그릇이 클수록 표 밖에서 더 세진다 (그게 이 종족의 뜻이다)
        new Spec
        {
            Id = "arcane_lich", Name = "비전 리치", UpgradeOf = "lich",
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Demon,
            Traits = MonsterTrait.Reinforced | MonsterTrait.Ranged,
            Desc = "흡혈하며 때리고, 최대 마나가 클수록 강해진다.\n그릇을 키우는 소환사와 어울린다.",
            Species = SpeciesPassive.ManaResonance,
            ManaCost = 11f, Count = 3,
            Attack = MonsterAttackKind.Ranged, Projectile = MonsterProjectileKind.MagicBolt,
            ProjSpeed = 14f,
            Hp = 105f, Atk = 33f, Range = 8.5f, AtkSpeed = 0.6f, MoveSpeed = 2.0f, Defense = 0.03f,
            ToHp = 0.6f, ToAtk = 1.7f,
            Tint = Rgb(150, 190, 255),          // 비전의 푸른빛 — 대리치(보라)와 갈린다
            Tags = MonsterTag.Sorcery | MonsterTag.Marksman,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("비전 순환",   StatType.Attack, 0.15f),
                Lv2("마력의 눈",   StatType.AttackRange, 0.12f, StatType.AttackSpeed, 0.08f),
                Lv ("공명 증폭",   StatType.MaxHp, 0.12f, PassiveSkillType.FocusedFire),
                Lv2("비전의 군주", StatType.Attack, 0.18f, StatType.MaxHp, 0.12f),
            },
            Signature = PassiveSkillType.VampiricStrike,
            // ⚠ 술법 표식 = 쿨감이라 깎을 쿨다운이 있어야 한다 (사용자 지시, 2026-09-15)
            //   블리자드 — 뿌리 리치와 맞바꿨다 (사용자 지시, 2026-09-15). 충격파는 리치가 든다.
            //   ⚠ 연쇄 번개(25)는 **희귀 스킬**이라 2차(리치 킹)로 올려 보냈다.
            //     희귀는 마릿수 1인 2차만 든다 — 3기가 함께 돌리면 무게가 무너진다.
            Skill = ActiveSkillId.Blizzard,
        },

        // ══════════════════════════════════════════════════════
        //  2차 업그레이드 — 계보의 끝 (사용자 지시, 2026-09-15)
        // ══════════════════════════════════════════════════════
        //
        //  ■ 셋이 한 묶음이다 — 이 셋이 2차를 2차답게 만든다
        //    ① **희귀 스킬**(21~30) — 1차 이하에는 주지 않는다
        //    ② **마릿수 1** (한 마리 고정) — 물량이 아니라 한 기가 판을 바꾸는 카드다
        //    ③ **권속 소환** — 제 하위 종족을 주기적으로 불러낸다 (BroodSpecies)
        //
        //    ①과 ②는 한 몸이다. 희귀 스킬은 세기가 아니라 **한 번에 판을 정리하는 것**이
        //    값인데, 3기 이상이 동시에 돌리면 그게 평타가 된다 — 리치가 메테오를 3기로
        //    돌리던 것이 그 예다 (사용자 지적). 마릿수를 줄여야 희귀가 희귀로 읽힌다.
        //
        //  ■ 진화 조건은 1차와 **같다** (사용자 확정)
        //    도감 해금 + **1차 종족을 덱에 들고 있을 것**. 뿌리에서 바로 못 올라온다 —
        //    그래서 UpgradeOf/AlsoUpgradeOf 에 1차만 적는다 (뿌리를 적으면 건너뛴다).
        //
        //  ⚠ 부모가 여럿이다 — UpgradeOf 는 **대표**일 뿐이다
        //    계보 패시브 상속(CollectSpeciesPassives)과 RootSpecies 는 대표 한 줄만 탄다.
        //    어느 1차에서 올라왔든 물려받는 패시브는 같다 — 그래서 대표는 계보를 대표할
        //    만한 것으로 고른다 (슬라임 킹 = 강철 슬라임의 가시).

        new Spec
        {
            Id = "king_slime", Name = "슬라임 킹",
            // 대표 부모 = 강철 슬라임 (가시 껍질을 물려받는다). 나머지 둘도 올라올 수 있다.
            UpgradeOf = "steel_slime",
            AlsoUpgradeOf = new[] { "heal_slime", "poison_slime" },
            // ⚠ 소환사 '슬라임 킹' 과 **같은 노란 시트**다 (사용자 확정, 2026-09-15)
            //   한때 자주(SlugLibrary_Royal)로 갈라 뒀다 — 소환사가 마왕성 체력 그 자체라
            //   라인의 몬스터와 헷갈리면 안 되기 때문이었다. 사용자가 "슬라임의 왕은
            //   노랑" 으로 확정했으므로 색으로는 안 가른다.
            //   ⚠ 그래서 갈라 주는 것이 **덩치뿐**이다 — 소환사 1.6 / 이쪽 2.0.
            //     둘 중 하나의 크기를 만질 때 나머지를 반드시 함께 볼 것.
            Body = MonsterBodyType.NonHumanoid, LibraryName = "SlugLibrary_Yellow",
            Traits = MonsterTrait.Reinforced | MonsterTrait.Shield | MonsterTrait.NonHumanoid,
            Desc = "슬라임의 왕. 혼자서 줄 하나를 막고, 주기적으로 슬라임을 토해 낸다.\n" +
                   "쓰러질 때 몸 안에 있던 슬라임 열둘이 쏟아진다.",
            Species = SpeciesPassive.KingSplit,
            // ⚠ 희귀 스킬 — 불멸의 방벽 (아군 보호막 → 폭발 + 치유)
            //   왕이 줄 전체를 감싸는 그림이다. 슬라임 계보가 처음부터 '벽' 이었으니
            //   그 끝은 더 큰 벽이어야 한다 — 딜러로 만들면 계보가 중간에서 꺾인다.
            Skill = ActiveSkillId.Bulwark,
            // ⚠ 권속 — 뿌리 슬라임을 셋씩. 자기(king_slime)를 부르면 부른 것이 또 부른다.
            Brood = "slime", BroodCount = 3, BroodCooldown = 12f,
            ManaCost = 14f, Count = 1,
            Attack = MonsterAttackKind.Melee,
            // 진화 검산 — 가장 센 부모(힐 슬라임) 대비 마나당 전투력 지수 ×1.20
            //   ⚠ 부모가 셋이라 **가장 센 쪽**을 넘어야 한다. 검산이 셋 다 본다.
            //   ⚠ 체력으로 채웠다 — 공격력으로 채우면 '벽' 이라는 계보가 깨진다.
            // ⚠ 한 마리 고정(2026-09-15)으로 옛 두 마리 몫을 한 몸에 담았다 — 벽: 체력 ×2.6 · 공격 ×1.6
            Hp = 6000f, Atk = 102f, Range = 2.0f, AtkSpeed = 0.7f, MoveSpeed = 1.4f, Defense = 0.12f,
            ToHp = 5.2f, ToAtk = 1.3f,
            // ⚠ 사거리와 한 묶음이다 — 공격 판정은 **중심 사이 거리**를 본다.
            //   덩치를 키우면 분리(Separation)가 중심을 더 멀리 떼어 놓아, 몸이 닿아
            //   보이는데 사거리 밖이라 가만히 서 있는다 (트롤이 그래서 2.1 이다).
            Size = 2.0f,
            Mark = MonsterMark.Crown,   // 머리 위 왕관
            Tags = MonsterTag.Forest | MonsterTag.Regrowth | MonsterTag.Royal,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("왕의 껍질",   StatType.MaxHp, 0.18f),
                Lv2("옥좌",        StatType.Defense, 0.07f, StatType.MaxHp, 0.12f),
                Lv ("점액 군주",   StatType.MaxHp, 0.15f, PassiveSkillType.IronWill),
                Lv2("만인의 왕",   StatType.MaxHp, 0.20f, StatType.Attack, 0.15f),
            },
            Signature = PassiveSkillType.IronWill,
        },
        new Spec
        {
            Id = "lich_king", Name = "리치 킹",
            // 대표 부모 = 대리치 (역병을 물려받는다). 비전 리치에서도 올라온다.
            UpgradeOf = "archlich",
            AlsoUpgradeOf = new[] { "arcane_lich" },
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Demon,
            Traits = MonsterTrait.Reinforced | MonsterTrait.Ranged | MonsterTrait.Sustained,
            Desc = "언데드의 왕. 멀리서 공간을 무너뜨리고, 부하 리치를 불러 세운다.\n" +
                   "가장 비싼 카드 — 홀로 줄 하나를 통째로 맡는다.",
            // ⚠ 2차는 **제 뿌리 패시브의 각성판**을 갖는다 (규칙, 2026-09-15)
            //   리치의 뿌리 능력이 생명 흡수(SoulDrain)라 그 각성판이다.
            //   상속으로 따라오는 원본은 PassiveResolver 가 뺀다 — 둘 다 발동하지 않는다.
            Species = SpeciesPassive.Vampire,
            // ⚠ 희귀 스킬 — 중력 붕괴 (흡입 구속 → 붕괴 폭발)
            //   연쇄 번개(25)는 1차(비전 리치)에서 여기로 올렸다가 다시 바꿨다 —
            //   붕괴는 **모아서 한 번에** 터뜨리는 그림이라 마릿수 2인 카드에 맞는다.
            //   ⚠ 사형 선고(26)는 피했다 — 소환사 시그니처가 이미 쓴다. 같은 스킬이
            //     소환사와 몬스터 양쪽에 있으면 무엇이 터진 것인지 화면에서 안 갈린다.
            Skill = ActiveSkillId.GravityCollapse,
            // ⚠ 권속 = 뿌리 리치. 쿨다운이 길다 — 리치는 마나 10짜리 원거리라
            //   짧게 두면 "공짜 리치 자판기" 가 된다. 권속은 덤이지 본업이 아니다.
            Brood = "lich", BroodCount = 2, BroodCooldown = 26f,
            ManaCost = 16f, Count = 1,
            Attack = MonsterAttackKind.Ranged, Projectile = MonsterProjectileKind.MagicBolt,
            ProjSpeed = 16f,
            // 진화 검산 — 가장 센 부모(비전 리치) 대비 마나당 전투력 지수 ×1.20
            // 한 마리 고정 — 원거리 딜러: 체력 ×1.6 · 공격 ×2.6
            Hp = 480f, Atk = 203f, Range = 9.5f, AtkSpeed = 0.55f, MoveSpeed = 1.9f, Defense = 0.06f,
            ToHp = 1.44f, ToAtk = 5.72f,
            Tint = Rgb(228, 196, 255),          // 창백한 보라 — 대리치(짙은 보라)보다 밝다
            Size = 1.25f,                       // 계층이 실루엣으로 읽히게
            Tags = MonsterTag.Undead | MonsterTag.Sorcery | MonsterTag.Royal,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("왕의 마력",   StatType.Attack, 0.18f),
                Lv2("심연의 옥좌", StatType.AttackRange, 0.14f, StatType.AttackSpeed, 0.10f),
                Lv ("영혼 징수",   StatType.Attack, 0.15f, PassiveSkillType.VampiricStrike),
                Lv2("불사의 군주", StatType.Attack, 0.20f, StatType.MaxHp, 0.15f),
            },
            Signature = PassiveSkillType.VampiricStrike,
        },

        new Spec
        {
            Id = "bone_lord", Name = "본 로드",
            // 대표 부모 = 해골 술사 (뼈 파편 폭발을 물려받는다). 셋 어디서든 올라온다.
            UpgradeOf = "skeleton_mage",
            AlsoUpgradeOf = new[] { "skeleton_guard", "mana_skeleton" },
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Skeleton,
            Traits = MonsterTrait.Reinforced | MonsterTrait.Ranged | MonsterTrait.Sustained,
            Desc = "뼈의 군주. 번개가 뼈를 타고 적 사이를 옮겨 다닌다.\n" +
                   "쓰러져도 반드시 한 번 다시 일어난다.",
            Species = SpeciesPassive.Undying,          // 재조립(스켈레톤)의 각성판
            Skill = ActiveSkillId.ChainLightning,
            Brood = "skeleton", BroodCount = 4, BroodCooldown = 14f,
            ManaCost = 14f, Count = 1,
            Attack = MonsterAttackKind.Ranged, Projectile = MonsterProjectileKind.MagicBolt,
            ProjSpeed = 15f,
            // 한 마리 고정 — 원거리지만 불사(재조립): 체력 ×1.8 · 공격 ×2.3
            Hp = 1656f, Atk = 272f, Range = 8.0f, AtkSpeed = 0.6f, MoveSpeed = 2.1f, Defense = 0.08f,
            ToHp = 2.16f, ToAtk = 3.68f,
            Tint = Rgb(255, 232, 170),          // 황금 뼈 — 술사(보라)·방패병(청)·마력(푸름)과 갈린다
            Size = 1.30f,
            Tags = MonsterTag.Marksman | MonsterTag.Regrowth | MonsterTag.Royal,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("뼈의 왕관",   StatType.Attack, 0.18f),
                Lv2("영겁의 뼈",   StatType.MaxHp, 0.18f, StatType.Defense, 0.05f),
                Lv ("뇌전 전도",   StatType.Attack, 0.15f, PassiveSkillType.FocusedFire),
                Lv2("불멸의 군주", StatType.Attack, 0.20f, StatType.MaxHp, 0.15f),
            },
            Signature = PassiveSkillType.IronWill,
        },
        new Spec
        {
            Id = "goblin_warchief", Name = "고블린 대족장",
            UpgradeOf = "goblin_chief",
            AlsoUpgradeOf = new[] { "goblin_archer" },
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Goblin,
            Traits = MonsterTrait.Reinforced | MonsterTrait.Charger,
            Desc = "고블린 부족을 통째로 이끈다. 하늘을 덮는 화살비를 세 번 내린다.\n" +
                   "떨어뜨리는 골드도 세 배다.",
            Species = SpeciesPassive.GoldRush,         // 약탈(고블린)의 각성판
            Skill = ActiveSkillId.ArrowStorm,
            Brood = "goblin", BroodCount = 4, BroodCooldown = 13f,
            ManaCost = 13f, Count = 1,
            Attack = MonsterAttackKind.Melee,
            // 한 마리 고정 — 돌격 딜러: 체력 ×1.7 · 공격 ×2.45
            Hp = 1020f, Atk = 307f, Range = 1.5f, AtkSpeed = 1.0f, MoveSpeed = 2.9f, Defense = 0.09f,
            ToHp = 1.7f, ToAtk = 3.68f,
            Tint = Rgb(255, 176, 150),          // 전쟁 도색 — 뿌리(민무늬)·궁수와 갈린다
            Size = 1.25f,                       // 고블린(0.85)의 한참 위 — 덩치로 족장이 읽힌다
            Tags = MonsterTag.Ferocity | MonsterTag.Forest | MonsterTag.Royal,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("대족장의 외침", StatType.Attack, 0.18f),
                Lv2("약탈 부대",     StatType.Attack, 0.12f, StatType.AttackSpeed, 0.10f),
                Lv ("전쟁 도색",     StatType.MaxHp, 0.15f, PassiveSkillType.KillMomentum),
                Lv2("부족의 왕",     StatType.Attack, 0.20f, StatType.MaxHp, 0.15f),
            },
            Signature = PassiveSkillType.KillMomentum,
        },
        new Spec
        {
            Id = "corpse_lord", Name = "시체 군주",
            UpgradeOf = "plague_zombie",
            AlsoUpgradeOf = new[] { "ghoul" },
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.ZombieB,
            Traits = MonsterTrait.Reinforced | MonsterTrait.Shield | MonsterTrait.Sustained,
            Desc = "썩은 살로 쌓아 올린 벽. 적에게 낙인을 찍고 한꺼번에 처형한다.\n" +
                   "터질 때 퍼지는 역병이 판을 덮는다.",
            Species = SpeciesPassive.Pandemic,         // 역병 폭발(좀비)의 각성판
            Skill = ActiveSkillId.DeathSentence,
            Brood = "zombie", BroodCount = 3, BroodCooldown = 14f,
            ManaCost = 14f, Count = 1,
            Attack = MonsterAttackKind.Melee,
            // 한 마리 고정 — 벽: 체력 ×2.6 · 공격 ×1.6
            //   + 체력 12% · 공격 9% (2026-09-15) — 부모 역병 좀비가 올라가 검산을 다시 맞췄다
            //   + 체력 10% · 공격 4% (2026-09-15) — 새 부모 구울(×1.15 이상)까지 넘도록
            Hp = 3843f, Atk = 94f, Range = 1.6f, AtkSpeed = 0.65f, MoveSpeed = 1.5f, Defense = 0.14f,
            ToHp = 5.76f, ToAtk = 1.64f,
            Tint = Rgb(186, 170, 200),          // 잿빛 보라 — 역병 좀비(병색 연두)와 갈린다
            Size = 1.35f,
            Tags = MonsterTag.Plague | MonsterTag.Regrowth | MonsterTag.Royal,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("시체 더미",   StatType.MaxHp, 0.18f),
                Lv2("썩은 갑주",   StatType.Defense, 0.07f, StatType.MaxHp, 0.12f),
                Lv ("역병의 낙인", StatType.Attack, 0.15f, PassiveSkillType.VampiricStrike),
                Lv2("만인의 무덤", StatType.MaxHp, 0.20f, StatType.Attack, 0.15f),
            },
            Signature = PassiveSkillType.IronWill,
        },
        new Spec
        {
            Id = "alpha_wolf", Name = "알파 울프",
            UpgradeOf = "frost_wolf",
            AlsoUpgradeOf = new[] { "blood_wolf" },
            Body = MonsterBodyType.NonHumanoid, LibraryName = "WolfLibrary_Alpha",   // 검은 은빛 (2026-09-15)
            Traits = MonsterTrait.Reinforced | MonsterTrait.Charger | MonsterTrait.NonHumanoid,
            Desc = "무리의 우두머리. 깃발을 세워 주변 아군을 통째로 끌어올린다.\n" +
                   "혼자 싸우지 않는다 — 부를 늑대가 늘 곁에 있다.",
            Species = SpeciesPassive.Alpha,            // 무리 사냥(늑대)의 각성판
            Skill = ActiveSkillId.WarBanner,
            Brood = "wolf", BroodCount = 3, BroodCooldown = 12f,
            ManaCost = 12f, Count = 1,
            Attack = MonsterAttackKind.Melee,
            // 한 마리 고정 — 빠른 돌격수: 체력 ×1.8 · 공격 ×2.3
            Hp = 936f, Atk = 191f, Range = 1.4f, AtkSpeed = 1.15f, MoveSpeed = 3.6f, Defense = 0.07f,
            ToHp = 1.8f, ToAtk = 3.45f,
            Size = 1.35f,
            Tags = MonsterTag.Beast | MonsterTag.Hunt | MonsterTag.Royal,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("우두머리의 송곳니", StatType.Attack, 0.18f),
                Lv2("사냥 본능",         StatType.AttackSpeed, 0.12f, StatType.MoveSpeed, 0.10f),
                Lv ("무리의 심장",       StatType.MaxHp, 0.15f, PassiveSkillType.SwiftAssault),
                Lv2("초원의 왕",         StatType.Attack, 0.20f, StatType.AttackSpeed, 0.12f),
            },
            Signature = PassiveSkillType.SwiftAssault,
        },
        new Spec
        {
            Id = "orc_warlord", Name = "오크 워로드",
            UpgradeOf = "war_orc",
            AlsoUpgradeOf = new[] { "orc_butcher" },
            Body = MonsterBodyType.Humanoid, Race = EnemyRace.Orc,
            Traits = MonsterTrait.Reinforced | MonsterTrait.Shield,
            Desc = "전장을 한 번에 가른다. 직선 위의 모든 것이 두 동강 난다.\n" +
                   "적을 쓰러뜨릴수록 걷잡을 수 없이 세진다.",
            Species = SpeciesPassive.Berserker,        // 피의 갈망(오크)의 각성판
            Skill = ActiveSkillId.Bisect,
            Brood = "orc", BroodCount = 3, BroodCooldown = 14f,
            ManaCost = 13f, Count = 1,
            Attack = MonsterAttackKind.Melee,
            // 한 마리 고정 — 방패 전사(처치할수록 세진다): 체력 ×2.2 · 공격 ×1.9
            Hp = 1760f, Atk = 122f, Range = 1.8f, AtkSpeed = 0.8f, MoveSpeed = 2.2f, Defense = 0.15f,
            ToHp = 3.3f, ToAtk = 2.47f,
            Tint = Rgb(255, 172, 160),          // 핏빛 — 오크(민무늬)·전쟁 오크와 갈린다
            Size = 1.45f,
            Tags = MonsterTag.Vanguard | MonsterTag.Steel | MonsterTag.Royal,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("거대 도끼",   StatType.Attack, 0.18f),
                Lv2("무쇠 갑주",   StatType.Defense, 0.07f, StatType.MaxHp, 0.12f),
                Lv ("피의 광란",   StatType.Attack, 0.15f, PassiveSkillType.StrengthStack),
                Lv2("전장의 군주", StatType.Attack, 0.20f, StatType.MaxHp, 0.15f),
            },
            Signature = PassiveSkillType.StrengthStack,
        },
        new Spec
        {
            Id = "war_boar", Name = "워 보어",
            UpgradeOf = "flame_hog",
            AlsoUpgradeOf = new[] { "iron_hog" },
            Body = MonsterBodyType.NonHumanoid, LibraryName = "HogLibrary_War",      // 검붉은 (2026-09-15)
            Traits = MonsterTrait.Reinforced | MonsterTrait.Charger | MonsterTrait.NonHumanoid,
            Desc = "멈추지 않는 돌진. 적진을 관통하며 길 위의 모든 것을 들이받는다.\n" +
                   "덩치가 커진 만큼 어지간해서는 쓰러지지 않는다.",
            Species = SpeciesPassive.Colossus,         // 튼튼함(멧돼지)의 각성판
            Skill = ActiveSkillId.PiercingDash,
            Brood = "hog", BroodCount = 3, BroodCooldown = 13f,
            ManaCost = 13f, Count = 1,
            Attack = MonsterAttackKind.Melee,
            // 한 마리 고정 — 관통 돌진(안 쓰러진다): 체력 ×2.3 · 공격 ×1.8
            Hp = 1495f, Atk = 126f, Range = 1.7f, AtkSpeed = 0.95f, MoveSpeed = 3.4f, Defense = 0.12f,
            ToHp = 2.76f, ToAtk = 2.52f,
            Shape = new Vector2(1.14f, 1.02f),
            Size = 1.45f,
            Tags = MonsterTag.Beast | MonsterTag.Sorcery | MonsterTag.Royal,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("강철 엄니",   StatType.Attack, 0.18f),
                Lv2("두꺼운 가죽", StatType.MaxHp, 0.18f, StatType.Defense, 0.05f),
                Lv ("멈추지 않는", StatType.MoveSpeed, 0.12f, PassiveSkillType.CounterStrike),
                Lv2("파괴 전차",   StatType.Attack, 0.20f, StatType.MaxHp, 0.15f),
            },
            Signature = PassiveSkillType.CounterStrike,
        },
        new Spec
        {
            Id = "ancient_troll", Name = "고대 트롤",
            UpgradeOf = "forest_troll",
            AlsoUpgradeOf = new[] { "swamp_troll" },
            Body = MonsterBodyType.NonHumanoid, LibraryName = "TrollLibrary_Ancient", // 돌회색 (2026-09-15)
            Traits = MonsterTrait.Reinforced | MonsterTrait.Shield | MonsterTrait.NonHumanoid,
            Desc = "바위처럼 늙은 트롤. 느리게 휘두르는 대신 한 번에 여럿을 쓸어 낸다.\n" +
                   "제 피를 태워 앞을 비우고, 곧 다시 채운다.",
            Species = SpeciesPassive.TrollBlood,       // 재생(트롤)의 각성판
            // ⚠ 휩쓸기 — 느려지는 값으로 범위를 산다 (사용자 지시, 2026-09-15)
            //   "기존 트롤에서 더 강력한 모습" 이 이 계보가 돌아갈 자리다. 뿌리 트롤은
            //   느리고 두꺼운 벽인데 1차 숲의 트롤이 작고 빠른 갈래로 틀었다 —
            //   2차는 그쪽이 아니라 **뿌리 쪽으로** 더 밀고 나간다.
            ExtraSpecies = new[] { SpeciesPassive.Cleave },
            // ⚠ 그래서 숲의 트롤의 '신속' 은 끊는다
            //   상속은 한 줄로 내려오므로 막지 않으면 "느려지는 대가로 범위를 산" 종족이
            //   신속을 함께 들고 선다. 둘이 서로를 지워 아무 축도 안 남는다.
            BlockInherit = new[] { SpeciesPassive.Swiftness },
            Skill = ActiveSkillId.BloodPrice,
            Brood = "troll", BroodCount = 2, BroodCooldown = 16f,
            ManaCost = 15f, Count = 1,
            Attack = MonsterAttackKind.Melee,
            // 사거리는 덩치와 한 묶음이다 — 트롤(1.45/2.1)에서 덩치가 더 커졌다
            // 한 마리 고정 — 느린 벽(휩쓸기): 체력 ×2.4 · 공격 ×1.75
            Hp = 4080f, Atk = 124f, Range = 2.4f, AtkSpeed = 0.7f, MoveSpeed = 1.7f, Defense = 0.16f,
            ToHp = 5.28f, ToAtk = 2.1f,
            Size = 1.75f,
            Tags = MonsterTag.Sorcery | MonsterTag.Beast | MonsterTag.Royal,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("바위 피부",   StatType.MaxHp, 0.18f),
                Lv2("고대의 힘",   StatType.Attack, 0.15f, StatType.Defense, 0.05f),
                Lv ("끓는 피",     StatType.MaxHp, 0.15f, PassiveSkillType.QuickRecovery),
                Lv2("산의 주인",   StatType.MaxHp, 0.20f, StatType.Attack, 0.15f),
            },
            Signature = PassiveSkillType.QuickRecovery,
        },


        // ── 트롤 계열
        // 늪 트롤 — 두 번째 1차 (2026-09-15). 숲의 트롤이 '작고 빠른 물량' 이라 이쪽은 뿌리 쪽 **중독 벽**.
        new Spec
        {
            Id = "swamp_troll", Name = "늪 트롤", UpgradeOf = "troll",
            Body = MonsterBodyType.NonHumanoid, LibraryName = "TrollLibrary_Swamp",   // 탁한 청록 시트
            Traits = MonsterTrait.Reinforced | MonsterTrait.Shield | MonsterTrait.Sustained | MonsterTrait.NonHumanoid,
            Desc = "늪의 독을 머금은 거구. 자기를 때린 적을 중독시켜 붙어 싸울수록 적이 녹는다.",
            Species = SpeciesPassive.PoisonOnHit,
            ManaCost = 13f, Count = 3,
            Attack = MonsterAttackKind.Melee,
            // 사거리는 덩치와 한 묶음이다 — 트롤(1.45/2.1)보다 조금 크다
            Hp = 375f, Atk = 34f, Range = 2.2f, AtkSpeed = 0.6f, MoveSpeed = 1.8f, Defense = 0.22f,
            ToHp = 1.7f, ToAtk = 1.2f,
            Size = 1.5f,
            Tags = MonsterTag.Regrowth | MonsterTag.Plague,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("이끼 가죽", StatType.MaxHp, 0.15f),
                Lv2("독 늪",     StatType.Defense, 0.05f, StatType.MaxHp, 0.10f),
                Lv ("늪의 숨결", StatType.MaxHp, 0.15f, PassiveSkillType.QuickRecovery),
                Lv2("늪의 주인", StatType.MaxHp, 0.20f, StatType.Attack, 0.15f),
            },
            Signature = PassiveSkillType.QuickRecovery,
        },
        // (숲의 트롤) ────────────────────────────────────────
        new Spec
        {
            Id = "forest_troll", Name = "숲의 트롤", UpgradeOf = "troll",
            Body = MonsterBodyType.NonHumanoid, LibraryName = "TrollLibrary_Forest",   // 초록 피부 시트 (2026-09-11)
            Traits = MonsterTrait.Swarm | MonsterTrait.Charger | MonsterTrait.NonHumanoid,
            Desc = "작고 약해진 대신 싸고 아홉씩 나온다. 재생하면서 빠르게 때린다.\n" +
                   "⚠ 이 '신속' 을 융합으로 다른 카드에 넘길 수 있다 — 종족을 가리지 않는다.",
            Species = SpeciesPassive.Swiftness,
            ManaCost = 9f, Count = 9,
            Attack = MonsterAttackKind.Melee,
            // 사거리는 뿌리와 같은 이유로 함께 올린다 (덩치가 같다) — 위 트롤 주석 참고.
            Hp = 95f, Atk = 12f, Range = 1.55f, AtkSpeed = 0.9f, MoveSpeed = 2.6f, Defense = 0.08f,
            ToHp = 1.0f, ToAtk = 1.0f,
            // ⚠ 색조를 걷었다 — 색은 시트가 준다 (TrollLibrary_Forest)
            // ⚠ 뿌리와 **다른** 덩치다 (사용자 지시, 2026-09-15)
            //   설명이 "작고 약해진 대신 싸고 아홉씩 나온다" 인데 덩치가 뿌리와 같아서,
            //   화면에서는 트롤이 아홉 마리 서 있는 것으로만 보였다 — 카드가 하는 말과
            //   눈에 보이는 것이 어긋나 있었다. 계보 셋이 이제 덩치로 갈린다:
            //   숲의 트롤 1.15(작다) · 트롤 1.45 · 고대 트롤 1.75(가장 크다).
            //   ⚠ 사거리도 함께 내렸다 (2.1 → 1.7 → 1.55) — 덩치가 작아지면 분리가
            //     중심을 덜 벌려 놓으므로 같이 줄여야 "몸은 닿았는데 안 때린다" 가 안 생긴다.
            Size = 1.15f,
            Tags = MonsterTag.Forest | MonsterTag.Swarm,   // 시너지 재분배 (2026-09-15)
            Levels = new[]
            {
                Lv ("숲의 활력", StatType.AttackSpeed, 0.12f),
                Lv2("잰걸음",    StatType.MoveSpeed, 0.15f, StatType.MaxHp, 0.12f),
                Lv ("무성한 재생", StatType.MaxHp, 0.15f, PassiveSkillType.QuickRecovery),
                Lv2("숲의 분노", StatType.Attack, 0.18f, StatType.AttackSpeed, 0.12f),
            },
            Signature = PassiveSkillType.SwiftAssault,
        },
    };

    // ──────────────────────────────────────────────────────────

    [MenuItem(ProjectKMenu.Data + "몬스터 도감", priority = ProjectKMenu.DataPrio + 22)]
    public static void CreateAll()
    {
        Directory.CreateDirectory(OutputRoot);

        // ⚠ 두 번 돈다 — 계보는 전부 만든 뒤에야 이을 수 있다
        //   업그레이드가 가리키는 뿌리 종족이 아직 없을 수 있기 때문이다.
        var made = new Dictionary<string, MonsterSpeciesData>(Roster.Length);

        foreach (Spec spec in Roster)
            made[spec.Id] = Create(spec);

        foreach (Spec spec in Roster)
            LinkLineage(spec, made);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        VerifyEvolutionsAreUpgrades();
        VerifySorceryHasSkill(made);
        VerifyRangedLooksRanged(made);
        VerifyTierTwo();
        VerifySynergyTags();
        VerifyPortraitScale();

        Debug.Log($"[MonsterCodexCreator] 완료 — {made.Count}종. 경로: {OutputRoot}\n" +
                  "⚠ 이어서 '데이터 생성 > 카드 목록' 을 실행해야 보상에 나옵니다.");
    }

    /// <summary>
    /// 술법 표식을 단 종족은 <b>반드시</b> 고유 액티브 스킬이 있어야 한다
    /// (사용자 지시, 2026-09-15).
    ///
    /// ■ 왜 검사하는가 — 조용히 죽는 시너지다
    ///   술법의 단계 효과는 <b>전부 스킬 쿨다운</b>이다 (MonsterSynergyRule.SorceryCooldownReduce,
    ///   금 능력도 "스킬을 준비한 채로 소환된다"). 스킬이 없는 종족이 그 표식을 달면
    ///   깎을 쿨다운이 없어 동·은·금 셋이 모두 아무 일도 하지 않는다 — 에러도 로그도 없고,
    ///   화면에는 시너지가 켜진 것으로 멀쩡히 뜬다.
    ///   실제로 리치·비전 리치·대리치·해골 술사 넷이 이 상태였다. 술법의 얼굴 격인 종족들이다.
    ///
    /// ⚠ Spec.Tags 가 아니라 <b>구워진 SO</b>를 본다
    ///   옛 종족의 표식은 에셋에만 있다 (Spec.Tags 는 비우면 안 덮는다).
    ///   로스터만 보면 그 종족들이 전부 "표식 없음" 으로 읽혀 검사가 통째로 헛돈다.
    /// </summary>
    static void VerifySorceryHasSkill(Dictionary<string, MonsterSpeciesData> made)
    {
        foreach (var pair in made)
        {
            MonsterSpeciesData so = pair.Value;
            if ((so.Tags & MonsterTag.Sorcery) == 0)      continue;
            if (so.ActiveSkill != ActiveSkillId.None)     continue;

            Debug.LogError(
                $"[MonsterCodexCreator] '{so.DisplayName}'({pair.Key}) 은 술법 표식을 달았는데 " +
                "고유 액티브 스킬이 없습니다 — 술법 시너지(쿨다운 감소)가 이 종족에게는 " +
                "동·은·금 전부 무효입니다. Spec.Skill 을 채우거나 술법 표식을 빼십시오.");
        }
    }

    /// <summary>
    /// 원거리 종족이 <b>실제로 원거리로 보이는지</b> 검사한다 (사용자 지시, 2026-09-15).
    ///
    /// ■ 인간형 — 그 종족(EnemyRace)에 전용 원거리 무기 풀이 있어야 한다
    ///   무기는 EnemyAppearanceRoller.RangedWeaponsFor 가 <b>종족으로</b> 고른다.
    ///   표에 없는 종족이 오면 최후의 풀(FallbackRanged)로 떨어지는데, 그건
    ///   "일단 활은 들린다" 는 뜻이지 그 종족의 무기가 아니다 — 두 종족이 같은 활을
    ///   들면 실루엣이 거짓말을 한다 (같은 파일의 근접 풀 주석과 같은 이유).
    ///
    /// ■ 도감 표시(MonsterTrait.Ranged)와 실제 행동이 갈리지 않아야 한다
    ///   카드에는 '원거리' 라고 적혀 있는데 걸어가서 때리면 그것대로 거짓말이다.
    ///
    /// ⚠ 비인간형은 무기를 들지 않는다 (통짜 라이브러리) — 무기 검사에서 뺀다.
    /// </summary>
    static void VerifyRangedLooksRanged(Dictionary<string, MonsterSpeciesData> made)
    {
        // 전용 원거리 풀을 가진 종족. EnemyAppearanceRoller.RangedWeaponsFor 의 switch 와
        // 같은 목록이어야 한다 — 거기에 갈래를 더하면 여기에도 더할 것.
        var hasRangedPool = new HashSet<EnemyRace>
        {
            EnemyRace.Orc, EnemyRace.Goblin, EnemyRace.Skeleton,
            EnemyRace.ZombieA, EnemyRace.ZombieB, EnemyRace.Demon,
        };

        foreach (var pair in made)
        {
            MonsterSpeciesData so = pair.Value;

            bool ranged      = so.AttackKind == MonsterAttackKind.Ranged;
            bool saysRanged  = (so.Traits & MonsterTrait.Ranged) != 0;

            if (ranged != saysRanged)
                Debug.LogError(
                    $"[MonsterCodexCreator] '{so.DisplayName}'({pair.Key}) 의 도감 표시와 실제 " +
                    $"공격 형태가 다릅니다 — 표시={(saysRanged ? "원거리" : "근접")} · " +
                    $"실제={(ranged ? "원거리" : "근접")}. Traits 의 MonsterTrait.Ranged 를 맞추십시오.");

            if (!ranged)                                          continue;
            if (so.BodyType != MonsterBodyType.Humanoid)          continue;   // 비인간형은 무기가 없다
            if (hasRangedPool.Contains(so.Race))                  continue;

            Debug.LogError(
                $"[MonsterCodexCreator] '{so.DisplayName}'({pair.Key}) 은 원거리인데 " +
                $"종족 {so.Race} 에 전용 원거리 무기 풀이 없습니다 — 최후의 풀로 떨어져 " +
                "다른 종족과 같은 활을 듭니다. EnemyAppearanceRoller.RangedWeaponsFor 에 " +
                "그 종족 풀을 추가하십시오.");
        }
    }

    /// <summary>소환력 기준점 — 견습 소환사의 값. 비교 기준일 뿐 밸런스 값이 아니다.</summary>
    const float RefSummonPower = 8f;

    /// <summary>진화체가 부모보다 최소 이만큼은 세야 한다.</summary>
    const float MinUpgradeRatio = 1.15f;

    /// <summary>
    /// 진화체가 정말로 부모보다 센지 검사한다.
    ///
    /// ■ 왜 필요한가 (사용자 지적, 2026-09-07)
    ///   개체 스탯만 보고 짜면 <b>마릿수와 마나를 빼먹는다.</b> 실제로 13종 중
    ///   12종이 부모보다 약했다 — 힐 슬라임은 슬라임의 0.29배였다.
    ///   개체는 더 셌지만(90 vs 80) 마나가 5→7 로 오르고 마릿수가 8→6 으로
    ///   줄어 마나당 전투력이 3분의 1 토막 났다. 화면에서는
    ///   "진화했더니 더 약해졌다" 로만 보인다.
    ///
    /// ■ 지수 = (마나당 총 체력) × (마나당 총 공격력)
    ///   곱으로 잡아야 종족의 성격이 유지된다 — 탱커는 체력으로, 딜러는
    ///   공격력으로 채우면 되고 한쪽만 보고 억지로 균형을 맞추지 않아도 된다.
    ///
    /// ⚠ 마나·마릿수를 바꾸면 스탯도 함께 다시 잡을 것 — 셋이 한 묶음이다.
    /// </summary>
    static void VerifyEvolutionsAreUpgrades()
    {
        var byId = new Dictionary<string, Spec>();
        foreach (Spec sp in Roster) byId[sp.Id] = sp;

        foreach (Spec child in Roster)
        {
            if (string.IsNullOrEmpty(child.UpgradeOf)) continue;

            // ⚠ 부모가 여럿이면 **전부** 넘어야 한다 (2026-09-15)
            //   가장 센 부모만 봐도 될 것 같지만, 그러면 "어느 부모에서 올라오느냐에
            //   따라 진화가 손해가 되는" 조합이 조용히 남는다. 하나씩 다 본다.
            CheckUpgrade(byId, child, child.UpgradeOf);

            if (child.AlsoUpgradeOf == null) continue;
            foreach (string alt in child.AlsoUpgradeOf)
                CheckUpgrade(byId, child, alt);
        }
    }

    /// <summary>한 부모에 대해서만 검산한다. 부모가 여럿인 종족은 여러 번 불린다.</summary>
    static void CheckUpgrade(Dictionary<string, Spec> byId, Spec child, string parentId)
    {
            if (!byId.TryGetValue(parentId, out Spec parent)) return;

            float ratio = IndexOf(child) / IndexOf(parent);
            if (ratio >= MinUpgradeRatio) return;

            Debug.LogError(
                $"[MonsterCodexCreator] '{child.Name}' 가 부모 '{parent.Name}' 보다 약합니다 " +
                $"(마나당 전투력 지수 ×{ratio:0.00}, 최소 ×{MinUpgradeRatio:0.00}).\n" +
                $"  {child.Name}: 체력 {child.Hp} · 공격 {child.Atk} × {child.Count}마리 / 마나 {child.ManaCost}\n" +
                $"  {parent.Name}: 체력 {parent.Hp} · 공격 {parent.Atk} × {parent.Count}마리 / 마나 {parent.ManaCost}\n" +
                "  → 진화는 카드 한 장을 바쳐서 하는 것이라 반드시 상위 호환이어야 합니다.");
    }

    /// <summary>
    /// 2차 업그레이드가 **2차답게** 짜였는지 본다 (사용자 지시, 2026-09-15).
    ///
    /// ■ 2차의 정의는 셋이다 — 희귀 스킬 · 적은 마릿수 · 권속
    ///   셋 중 하나만 빠져도 "비싼 1차" 가 된다. 특히 마릿수는 조용히 어긋난다 —
    ///   희귀 스킬을 3기가 함께 돌리면 그 스킬이 평타로 읽힌다 (리치 메테오가 그랬다).
    ///
    /// ■ 그리고 희귀 스킬은 **2차만** 든다
    ///   1차 이하가 희귀를 들면 위의 균형이 통째로 무너진다. 양방향으로 검사한다.
    ///
    /// ⚠ 권속은 자기 계보의 **아래**여야 한다 — 자기를 부르면 부른 것이 또 부른다.
    ///   LinkBrood 가 자기 자신만 막으므로, 여기서 '내 위/나와 같은 단계' 도 본다.
    /// </summary>
    static void VerifyTierTwo()
    {
        var byId = new Dictionary<string, Spec>();
        foreach (Spec sp in Roster) byId[sp.Id] = sp;

        // 2차 = 부모가 또 부모를 가진 종족.
        bool IsTierTwo(Spec sp)
            => !string.IsNullOrEmpty(sp.UpgradeOf)
            && byId.TryGetValue(sp.UpgradeOf, out Spec p)
            && !string.IsNullOrEmpty(p.UpgradeOf);

        foreach (Spec sp in Roster)
        {
            bool tierTwo = IsTierTwo(sp);
            bool rare    = IsRareSkill(sp.Skill);

            if (tierTwo)
            {
                if (!rare)
                    Debug.LogError($"[MonsterCodexCreator] 2차 '{sp.Name}' 에 희귀 스킬이 없습니다 " +
                                   $"(지금 {sp.Skill}). 희귀(21~30)는 2차만 듭니다.");

                // ⚠ 2차는 한 마리 고정이다 (사용자 지시, 2026-09-15)
                if (sp.Count != 1)
                    Debug.LogError($"[MonsterCodexCreator] 2차 '{sp.Name}' 의 마릿수가 {sp.Count} 입니다 " +
                                   "— 2차는 1 이어야 합니다. 여럿이 희귀 스킬을 함께 돌리면 " +
                                   "그 스킬이 평타로 읽힙니다.");

                if (string.IsNullOrEmpty(sp.Brood) || sp.BroodCount <= 0)
                    Debug.LogError($"[MonsterCodexCreator] 2차 '{sp.Name}' 에 권속이 없습니다 — " +
                                   "Brood 와 BroodCount 를 채우십시오.");
            }
            else if (rare)
            {
                Debug.LogError($"[MonsterCodexCreator] '{sp.Name}' 는 2차가 아닌데 희귀 스킬 " +
                               $"({sp.Skill})을 들고 있습니다 — 희귀는 2차만 듭니다.");
            }

            if (string.IsNullOrEmpty(sp.Brood)) continue;

            // 권속이 나보다 아래인가 — 내 조상 줄에 그 종족이 있어야 한다.
            bool below = false;
            string cursor = sp.UpgradeOf;
            for (int guard = 0; guard < 8 && !string.IsNullOrEmpty(cursor); guard++)
            {
                if (cursor == sp.Brood) { below = true; break; }
                cursor = byId.TryGetValue(cursor, out Spec c) ? c.UpgradeOf : null;
            }

            if (!below)
                Debug.LogError($"[MonsterCodexCreator] '{sp.Name}' 의 권속 '{sp.Brood}' 가 " +
                               "제 계보의 아래 단계가 아닙니다 — 권속 소환은 세대를 보지 않으므로 " +
                               "같거나 위를 부르면 부른 개체가 또 불러 끝없이 늡니다.");
        }
    }

    /// <summary>
    /// 덩치가 초상화 기준값을 넘지 않는지 본다 (2026-09-15).
    ///
    /// 초상화는 가장 큰 종족이 칸을 꽉 채우도록 여백을 두른다
    /// (MonsterPortraitProvider.PortraitMaxBodyScale). 그 값을 넘는 종족이 생기면
    /// 여백이 음수가 되어 **그 종족만 칸에 꽉 찬 채 다른 것과 구분이 안 된다** —
    /// 에러도 안 나고, 화면에서는 "제일 큰 둘이 같아 보인다" 로만 보인다.
    /// </summary>
    static void VerifyPortraitScale()
    {
        foreach (Spec sp in Roster)
        {
            float size = sp.Size > 0f ? sp.Size : 1f;
            if (size <= MonsterPortraitProvider.PortraitMaxBodyScale) continue;

            Debug.LogError(
                $"[MonsterCodexCreator] '{sp.Name}' 의 덩치 {size:0.00} 가 초상화 기준값 " +
                $"{MonsterPortraitProvider.PortraitMaxBodyScale:0.00} 를 넘습니다 — " +
                "MonsterPortraitProvider.PortraitMaxBodyScale 을 올리거나 덩치를 낮추십시오.");
        }
    }

    /// <summary>
    /// 시너지 표식 규칙을 본다 (사용자 확정, 2026-09-15).
    ///
    /// ■ 기본·1차 = 일반 표식 2개 · 2차 = 일반 2개 + 왕권
    /// ■ 일반 시너지 하나에 6~7종 — 문턱(2/4/6 · 3/5/7)이 그 수를 전제한다
    ///   (MonsterSynergyRule.StepsOf 가 소속 수로 문턱을 고른다).
    /// ⚠ 표식을 옮기면 여기가 먼저 말한다 — 조용히 한 시너지만 쉬워지는 것을 막는다.
    /// </summary>
    static void VerifySynergyTags()
    {
        var byId = new Dictionary<string, Spec>();
        foreach (Spec sp in Roster) byId[sp.Id] = sp;

        var members = new Dictionary<MonsterTag, int>();

        foreach (Spec sp in Roster)
        {
            bool tierTwo = !string.IsNullOrEmpty(sp.UpgradeOf)
                        && byId.TryGetValue(sp.UpgradeOf, out Spec p)
                        && !string.IsNullOrEmpty(p.UpgradeOf);

            int  regular = 0;
            bool royal   = (sp.Tags & MonsterTag.Royal) != 0;

            foreach (MonsterTag t in MonsterSynergyRule.AllTags)
            {
                if ((sp.Tags & t) == 0) continue;
                members[t] = (members.TryGetValue(t, out int n) ? n : 0) + 1;
                if (t != MonsterTag.Royal) regular++;
            }

            if (regular != 2 || royal != tierTwo)
                Debug.LogError($"[MonsterCodexCreator] '{sp.Name}' 의 시너지 표식이 규칙과 다릅니다 — " +
                               $"일반 {regular}개 · 왕권 {(royal ? "있음" : "없음")} " +
                               $"(규칙: 일반 2개{(tierTwo ? " + 왕권" : "")}).");
        }

        foreach (MonsterTag t in MonsterSynergyRule.AllTags)
        {
            if (t == MonsterTag.Royal) continue;

            int n = members.TryGetValue(t, out int c) ? c : 0;
            if (n < 6 || n > 7)
                Debug.LogWarning($"[MonsterCodexCreator] 시너지 '{MonsterSynergyRule.NameOf(t)}' 소속이 {n}종입니다 " +
                                 "— 6~7종이어야 문턱(2/4/6 · 3/5/7)이 계열 완주와 맞습니다.");
        }
    }

    /// <summary>희귀 스킬인가 (ActiveSkillId 21~30). 정본은 그 enum 의 구획 주석이다.</summary>
    static bool IsRareSkill(ActiveSkillId id)
        => (int)id >= (int)ActiveSkillId.Bisect && (int)id <= (int)ActiveSkillId.Gravestone;

    static float IndexOf(Spec s)
    {
        float mana = Mathf.Max(0.01f, s.ManaCost);
        float hp   = (s.Hp  + RefSummonPower * s.ToHp)  * s.Count / mana;
        float atk  = (s.Atk + RefSummonPower * s.ToAtk) * s.Count / mana;
        return hp * atk;
    }

    /// <summary>
    /// 업그레이드 → 뿌리 종족 참조를 잇는다.
    ///
    /// ⚠ Create 안에서 하면 안 된다
    ///   뿌리가 로스터 뒤쪽에 있으면 그 시점에는 아직 에셋이 없다.
    /// </summary>
    static void LinkLineage(Spec spec, Dictionary<string, MonsterSpeciesData> made)
    {
        MonsterSpeciesData so = made[spec.Id];

        // ⚠ 부모·권속은 **뿌리 종족에도** 이어 둔다 (2026-09-15)
        //   아래 UpgradeOf 분기보다 먼저 한다 — 뒤에 두면 뿌리 종족이 early return 에
        //   걸려 권속이 조용히 비고, 그러면 권속 소환 스킬이 아무것도 안 낸다.
        LinkExtraParents(spec, so, made);
        LinkBrood(spec, so, made);

        if (string.IsNullOrEmpty(spec.UpgradeOf))
        {
            so.UpgradeOf = null;
            EditorUtility.SetDirty(so);
            return;
        }

        if (!made.TryGetValue(spec.UpgradeOf, out MonsterSpeciesData root))
        {
            Debug.LogError($"[MonsterCodexCreator] '{spec.Id}' 의 뿌리 종족 " +
                           $"'{spec.UpgradeOf}' 가 로스터에 없습니다.");
            return;
        }

        so.UpgradeOf = root;
        EditorUtility.SetDirty(so);
    }

    /// <summary>
    /// 부모가 여럿인 종족(2차 업그레이드)의 나머지 부모를 잇는다.
    ///
    /// ⚠ UpgradeOf(대표 부모)는 여기 넣지 않는다 — 계보 패시브 상속이 그 줄만 타므로
    ///   같은 부모가 두 목록에 있으면 판정이 두 번 참이 되어 헷갈릴 뿐이다.
    /// </summary>
    static void LinkExtraParents(Spec spec, MonsterSpeciesData so,
                                 Dictionary<string, MonsterSpeciesData> made)
    {
        if (spec.AlsoUpgradeOf == null || spec.AlsoUpgradeOf.Length == 0)
        {
            so.AlsoUpgradeOf = new MonsterSpeciesData[0];
            return;
        }

        var list = new List<MonsterSpeciesData>(spec.AlsoUpgradeOf.Length);

        foreach (string id in spec.AlsoUpgradeOf)
        {
            if (id == spec.UpgradeOf)
            {
                Debug.LogError($"[MonsterCodexCreator] '{spec.Id}' 의 AlsoUpgradeOf 에 " +
                               $"대표 부모 '{id}' 가 또 들어 있습니다 — 빼십시오.");
                continue;
            }

            if (!made.TryGetValue(id, out MonsterSpeciesData parent))
            {
                Debug.LogError($"[MonsterCodexCreator] '{spec.Id}' 의 추가 부모 " +
                               $"'{id}' 가 로스터에 없습니다.");
                continue;
            }

            list.Add(parent);
        }

        so.AlsoUpgradeOf = list.ToArray();
    }

    /// <summary>
    /// 권속을 잇는다.
    ///
    /// ⚠ 권속은 **아래 단계**여야 한다 (자기 자신·자기 상위 금지)
    ///   자기를 부르면 부른 개체가 또 부른다. 권속 소환은 세대를 보지 않으므로
    ///   (분열과 달리 MaxReproduceGeneration 이 막아 주지 않는다) 여기서 막는다.
    /// </summary>
    static void LinkBrood(Spec spec, MonsterSpeciesData so,
                          Dictionary<string, MonsterSpeciesData> made)
    {
        if (string.IsNullOrEmpty(spec.Brood)) { so.BroodSpecies = null; return; }

        if (spec.Brood == spec.Id)
        {
            Debug.LogError($"[MonsterCodexCreator] '{spec.Id}' 의 권속이 자기 자신입니다 — " +
                           "부른 개체가 또 불러 무한히 늘어납니다.");
            return;
        }

        if (!made.TryGetValue(spec.Brood, out MonsterSpeciesData brood))
        {
            Debug.LogError($"[MonsterCodexCreator] '{spec.Id}' 의 권속 " +
                           $"'{spec.Brood}' 가 로스터에 없습니다.");
            return;
        }

        so.BroodSpecies = brood;
    }

    static MonsterSpeciesData Create(Spec spec)
    {
        string path = $"{OutputRoot}/Monster_{spec.Id}.asset";

        // 이미 있으면 내용만 갈아끼운다 — 지웠다 만들면 GUID 가 바뀌어
        // 도감 세이브(종족 Id 기준)와는 무관하지만 씬·프리팹 참조가 끊어진다.
        var so = AssetDatabase.LoadAssetAtPath<MonsterSpeciesData>(path);
        bool isNew = so == null;
        if (isNew) so = ScriptableObject.CreateInstance<MonsterSpeciesData>();

        so.ExtraTraits    = spec.ExtraSpecies ?? new SpeciesPassive[0];
        so.BlockedInherit = spec.BlockInherit ?? new SpeciesPassive[0];

        so.BroodCount    = spec.BroodCount;
        so.BroodCooldown = spec.BroodCooldown;

        so.Id          = spec.Id;
        so.DisplayName = spec.Name;
        so.BodyType    = spec.Body;
        so.Race        = spec.Race;
        so.Traits      = spec.Traits;
        so.Description = spec.Desc;

        // 몬스터는 전부 같은 프리팹을 쓴다 — 외형만 종족별로 갈린다.
        so.PoolKey = SpawnUnitType.Monster.ToString();

        so.ManaCost    = spec.ManaCost;
        so.SummonCount = spec.Count;

        so.AttackKind      = spec.Attack;
        so.Projectile      = spec.Projectile;
        so.ProjectileSpeed = spec.ProjSpeed;

        // 카드 레벨 보너스 — Lv2·Lv3·Lv4·Lv5 순서다. 순서가 곧 레벨이라 비우지 말 것.
        so.LevelBonuses     = spec.Levels ?? new MonsterLevelBonus[0];
        so.SignaturePassive = spec.Signature;

        if (so.LevelBonuses.Length != CardLevelRule.MaxLevel - 1)
            Debug.LogWarning($"[MonsterCodexCreator] '{spec.Id}' 의 레벨 보너스가 " +
                             $"{so.LevelBonuses.Length}칸입니다 (필요 {CardLevelRule.MaxLevel - 1}칸). " +
                             "빈 레벨에서는 아무 일도 일어나지 않습니다.");

        so.MaxHp       = spec.Hp;
        so.Attack      = spec.Atk;
        so.AttackRange = spec.Range;
        so.AttackSpeed = spec.AtkSpeed;
        so.MoveSpeed   = spec.MoveSpeed;
        so.Defense     = spec.Defense;
        so.CritChance  = 0.05f;
        so.CritDamage  = 1.5f;

        so.SummonPowerToHp     = spec.ToHp;
        so.SummonPowerToAttack = spec.ToAtk;

        // 종족 패시브. 뿌리 것은 UpgradeOf 로 상속되므로 여기엔 자기 것만 들어간다.
        so.SpeciesTrait = spec.Species;
        so.ActiveSkill  = spec.Skill;

        // ── 겉모습 — 안 적은 종족은 기본값이다 ──
        //  ⚠ 색조·덩치는 계보로 상속되지 않는다 (스탯과 다르다)
        //    진화체가 뿌리와 갈려 보이게 하려고 두는 값이라, 물려받으면
        //    바로 그 목적이 사라진다. 뿌리와 같은 덩치를 쓰려면 다시 적는다.
        so.BodyTint    = spec.Tint.a > 0f ? spec.Tint : Color.white;
        so.BodyScale   = spec.Size  > 0f ? spec.Size  : 1f;
        so.BodyStretch = spec.Shape.sqrMagnitude > 0f ? spec.Shape : Vector2.one;
        so.Mark        = spec.Mark;

        // ⚠ 적은 종족만 덮는다 — 옛 종족의 표식은 에셋에만 있다 (Spec.Tags 주석)
        if (spec.Tags != MonsterTag.None) so.Tags = spec.Tags;

        if (spec.Body == MonsterBodyType.NonHumanoid)
        {
            string libPath = $"{LibraryRoot}/{spec.LibraryName}.asset";
            so.NonHumanoidLibrary = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(libPath);

            if (so.NonHumanoidLibrary == null)
                Debug.LogWarning(
                    $"[MonsterCodexCreator] '{spec.Id}' 의 라이브러리를 찾지 못했습니다: {libPath}\n" +
                    "먼저 Tools > Project K > 데이터 생성 > 비인간형 몬스터 라이브러리 를 실행하세요.");
        }

        if (isNew) AssetDatabase.CreateAsset(so, path);
        else       EditorUtility.SetDirty(so);

        return so;
    }
}
