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
            ManaCost = 7f, Count = 6,
            Attack = MonsterAttackKind.Melee,
            Hp = 105f, Atk = 7f, Range = 1.1f, AtkSpeed = 0.7f, MoveSpeed = 1.7f, Defense = 0.08f,
            ToHp = 1.3f, ToAtk = 0.6f,
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
            Levels = new[]
            {
                Lv ("마력 순환", StatType.Attack, 0.15f),
                Lv2("먼 시야",   StatType.AttackRange, 0.12f, StatType.AttackSpeed, 0.08f),
                Lv ("영혼 착취", StatType.Attack, 0.12f, PassiveSkillType.FocusedFire),
                Lv2("대마력",    StatType.Attack, 0.18f, StatType.AttackRange, 0.10f),
            },
            Signature = PassiveSkillType.VampiricStrike,
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
            Traits = MonsterTrait.Swarm | MonsterTrait.Shield | MonsterTrait.NonHumanoid,
            Desc = "주기적으로 주변의 다친 아군을 치유하고, 죽을 때도 회복을 남긴다.\n" +
                   "때리는 힘은 약하지만 질기다 — 물량 앞줄에 섞어 두면 줄 전체가 오래 버틴다.",
            Species = SpeciesPassive.HealOnDeath,
            // ⚠ 치유 스킬이 있어야 술법 시너지(쿨감)를 받는다 (사용자 지시, 2026-09-11)
            Skill   = ActiveSkillId.SlimeMend,
            ManaCost = 7f, Count = 6,
            Attack = MonsterAttackKind.Melee,
            // ⚠ 공격 13 → 8 · 체력 195 → 300 (사용자 지시, 2026-09-11 "공격 낮추고 체력 높이자")
            //   진화 검산 — 슬라임 대비 마나당 전투력 지수 ×1.22 (최소 ×1.15, IndexOf)
            Hp = 300f, Atk = 8f, Range = 1.0f, AtkSpeed = 0.8f, MoveSpeed = 1.6f, Defense = 0.08f,
            ToHp = 1.5f, ToAtk = 0.4f,
            // ⚠ 색만으로는 안 갈린다 — 납작하게 눌러 실루엣을 바꾼다 (Shape 주석)
            // ⚠ 색조를 걷었다 — 색은 시트가 준다 (SlugLibrary_Pink). 곱하면 분홍이 탁해진다
            Shape = new Vector2(1.20f, 0.84f),   // 납작
            Mark = MonsterMark.Cross,   // 머리 위 십자 (사용자 지적, 2026-09-11 — 독 슬라임과 안 갈렸다)
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
            Levels = new[]
            {
                Lv ("뼈 마력",   StatType.Attack, 0.15f),
                Lv2("먼 조준",   StatType.AttackRange, 0.12f, StatType.AttackSpeed, 0.08f),
                Lv ("파편 증폭", StatType.Attack, 0.12f, PassiveSkillType.FocusedFire),
                Lv2("해골 폭풍", StatType.Attack, 0.18f, StatType.AttackRange, 0.10f),
            },
            Signature = PassiveSkillType.WideRange,
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
            Tags = MonsterTag.Undead | MonsterTag.Regrowth,   // 뿌리(스켈레톤)와 같은 표식
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
            Hp = 70f, Atk = 19f, Range = 6.0f, AtkSpeed = 1.0f, MoveSpeed = 2.6f, Defense = 0f,
            ToHp = 0.6f, ToAtk = 1.2f,
            Tint = Rgb(240, 235, 160), Size = 0.85f,   // 노란기 · 뿌리와 같은 덩치
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
            Traits = MonsterTrait.Swarm | MonsterTrait.Sustained,
            Desc = "역병을 퍼뜨리고, 터지며 주변에 피해까지 준다.\n한 마리씩 죽어 나갈수록 판이 정리된다.",
            Species = SpeciesPassive.ExplodeOnDeath,
            ManaCost = 9f, Count = 5,
            Attack = MonsterAttackKind.Melee,
            Hp = 165f, Atk = 16f, Range = 1.1f, AtkSpeed = 0.7f, MoveSpeed = 1.8f, Defense = 0.08f,
            ToHp = 1.3f, ToAtk = 0.8f,
            Tint = Rgb(200, 240, 150),          // 병색 — 역병
            Levels = new[]
            {
                Lv ("역병 축적", StatType.Attack, 0.15f),
                Lv2("부패한 몸", StatType.MaxHp, 0.18f, StatType.Defense, 0.05f),
                Lv ("전염 확산", StatType.Attack, 0.12f, PassiveSkillType.VampiricStrike),
                Lv2("대역병",    StatType.MaxHp, 0.18f, StatType.Attack, 0.15f),
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
            Levels = new[]
            {
                Lv ("서리 발톱", StatType.Attack, 0.15f),
                Lv2("빙결 질주", StatType.MoveSpeed, 0.15f, StatType.AttackSpeed, 0.08f),
                Lv ("한기",      StatType.MaxHp, 0.12f, PassiveSkillType.CounterStrike),
                Lv2("혹한",      StatType.Attack, 0.18f, StatType.CritChance, 0.08f),
            },
            Signature = PassiveSkillType.CounterStrike,
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
            Levels = new[]
            {
                Lv ("전열",     StatType.MaxHp, 0.15f),
                Lv2("전쟁 훈련", StatType.Attack, 0.15f, StatType.Defense, 0.05f),
                Lv ("전장의 함성", StatType.Attack, 0.12f, PassiveSkillType.StrengthStack),
                Lv2("불굴의 진형", StatType.MaxHp, 0.20f, StatType.DefensePenetration, 0.06f),
            },
            Signature = PassiveSkillType.ShieldEdge,
        },

        // ── 멧돼지 계열 ──────────────────────────────────────
        new Spec
        {
            Id = "flame_hog", Name = "화염 멧돼지", UpgradeOf = "hog",
            Body = MonsterBodyType.NonHumanoid, LibraryName = "HogLibrary_Flame",   // 불꽃 주황 시트 (2026-09-11)
            Traits = MonsterTrait.Charger | MonsterTrait.Sustained | MonsterTrait.NonHumanoid,
            Desc = "튼튼하고, 때린 자리마다 불이 붙는다.\n오래 붙어 싸울수록 화상이 겹쳐 쌓인다.",
            Species = SpeciesPassive.BurnOnAttack,
            Skill   = ActiveSkillId.HeavyStrike,   // 뿌리와 같은 돌진 — 계보가 읽히게
            ManaCost = 10f, Count = 4,
            Attack = MonsterAttackKind.Melee,
            Hp = 155f, Atk = 30f, Range = 1.1f, AtkSpeed = 0.9f, MoveSpeed = 3.2f, Defense = 0.10f,
            ToHp = 1.0f, ToAtk = 1.3f,
            // ⚠ 색조를 걷었다 — 색은 시트가 준다 (HogLibrary_Flame)
            Shape = new Vector2(1.10f, 1.06f), Size = 1.10f,   // 한 덩치
            Levels = new[]
            {
                Lv ("불붙은 엄니", StatType.Attack, 0.15f),
                Lv2("달아오른 가죽", StatType.MaxHp, 0.15f, StatType.MoveSpeed, 0.10f),
                Lv ("연소",        StatType.Attack, 0.12f, PassiveSkillType.CounterStrike),
                Lv2("화염 돌진",   StatType.Attack, 0.20f, StatType.AttackSpeed, 0.10f),
            },
            Signature = PassiveSkillType.BerserkerPact,
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
            Levels = new[]
            {
                Lv ("대마력 순환", StatType.Attack, 0.18f),
                Lv2("심연의 시야", StatType.AttackRange, 0.14f, StatType.AttackSpeed, 0.10f),
                Lv ("영혼 포식",   StatType.Attack, 0.12f, PassiveSkillType.VampiricStrike),
                Lv2("역병의 군주", StatType.Attack, 0.20f, StatType.AttackRange, 0.12f),
            },
            Signature = PassiveSkillType.WideRange,
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
            Tags = MonsterTag.Undead | MonsterTag.Sorcery,    // 뿌리(리치)와 같은 표식
            Levels = new[]
            {
                Lv ("비전 순환",   StatType.Attack, 0.15f),
                Lv2("마력의 눈",   StatType.AttackRange, 0.12f, StatType.AttackSpeed, 0.08f),
                Lv ("공명 증폭",   StatType.MaxHp, 0.12f, PassiveSkillType.FocusedFire),
                Lv2("비전의 군주", StatType.Attack, 0.18f, StatType.MaxHp, 0.12f),
            },
            Signature = PassiveSkillType.VampiricStrike,
        },

        // ── 트롤 계열 ────────────────────────────────────────
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
            Hp = 95f, Atk = 12f, Range = 1.7f, AtkSpeed = 0.9f, MoveSpeed = 2.6f, Defense = 0.08f,
            ToHp = 1.0f, ToAtk = 1.0f,
            // ⚠ 색조를 걷었다 — 색은 시트가 준다 (TrollLibrary_Forest)
            Size = 1.45f,   // 뿌리와 같은 덩치
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

        Debug.Log($"[MonsterCodexCreator] 완료 — {made.Count}종. 경로: {OutputRoot}\n" +
                  "⚠ 이어서 '데이터 생성 > 카드 목록' 을 실행해야 보상에 나옵니다.");
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
            if (string.IsNullOrEmpty(child.UpgradeOf))              continue;
            if (!byId.TryGetValue(child.UpgradeOf, out Spec parent)) continue;

            float ratio = IndexOf(child) / IndexOf(parent);
            if (ratio >= MinUpgradeRatio) continue;

            Debug.LogError(
                $"[MonsterCodexCreator] '{child.Name}' 가 부모 '{parent.Name}' 보다 약합니다 " +
                $"(마나당 전투력 지수 ×{ratio:0.00}, 최소 ×{MinUpgradeRatio:0.00}).\n" +
                $"  {child.Name}: 체력 {child.Hp} · 공격 {child.Atk} × {child.Count}마리 / 마나 {child.ManaCost}\n" +
                $"  {parent.Name}: 체력 {parent.Hp} · 공격 {parent.Atk} × {parent.Count}마리 / 마나 {parent.ManaCost}\n" +
                "  → 진화는 카드 한 장을 바쳐서 하는 것이라 반드시 상위 호환이어야 합니다.");
        }
    }

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

    static MonsterSpeciesData Create(Spec spec)
    {
        string path = $"{OutputRoot}/Monster_{spec.Id}.asset";

        // 이미 있으면 내용만 갈아끼운다 — 지웠다 만들면 GUID 가 바뀌어
        // 도감 세이브(종족 Id 기준)와는 무관하지만 씬·프리팹 참조가 끊어진다.
        var so = AssetDatabase.LoadAssetAtPath<MonsterSpeciesData>(path);
        bool isNew = so == null;
        if (isNew) so = ScriptableObject.CreateInstance<MonsterSpeciesData>();

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
