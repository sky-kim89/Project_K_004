using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
//  SummonerCreator.cs  [Editor Only]
//  소환사(메인 캐릭터) 데이터를 한 번에 굽는다.
//
//  ■ 3대 스탯 합계는 전부 19 로 맞춘다
//    캐릭터 자체는 10 을 넘지 못하고(SummonerData.MaxCoreStat),
//    합계로 강약이 갈리면 선택이 사라진다. **배분이 곧 개성**이다.
//    ⚠ 표를 고칠 때 합계를 반드시 다시 셀 것.
//
//  ■ 캐릭터를 가르는 축은 셋이다
//    ① 배분      — 힘/체력/지능
//    ② 친화도    — 어떤 몬스터에게 소환력이 1.75배로 실리는가 (A안)
//    ③ 개성(Perk)— 소환 파이프라인에 끼어드는 규칙 하나
//    여기에 시작 카드(StarterMonsters)가 더해져 "무엇을 데리고 시작하는가" 가 된다.
//
//  ■ ⚠ 신규 몬스터 종족이 필요한 캐릭터는 넣지 않았다
//    정령술사·디아볼리스트·인형사처럼 없는 종족을 전제로 하는 것들은
//    그 종족이 생긴 뒤에 추가한다. 지금 넣으면 시작 카드가 비어
//    아무것도 소환할 수 없는 캐릭터가 된다.
//
//  ■ 외형은 인간형 합성이다
//    장비를 장착할 수 있어야 하므로 CharacterBuilder 경로를 쓴다
//    (비인간형 통짜 스프라이트는 장비를 못 입는다).
//    AppearanceSeed 가 종족·머리·의상 조합을 통째로 정한다 —
//    마음에 드는 모습이 나올 때까지 이 문자열을 바꿔 가며 고르는 값이다.
//
//  사용: Tools > Project K > 데이터 생성 > 소환사
//  ⚠ 먼저 '몬스터 도감' 과 '스킬 카드' 를 굽고, 끝나면 '카드 목록' 을 굽는다.
// ============================================================

public static class SummonerCreator
{
    const string OutputRoot = "Assets/_project/Data/Summoners";

    /// <summary>한 소환사의 굽기 설정. 아래 Roster 가 정본이다.</summary>
    struct Spec
    {
        public string Id;
        public string Name;
        public string Desc;

        public float Str, Vit, Int;      // 합계 19 고정

        public float AttackRange;
        public float ProjectileSpeed;
        public float AttackSpeed;
        public float Defense;

        // ⚠ 마나 총량은 스펙에 적지 않는다 (2026-08-28)
        //   최대 마나 = BaseMana + 지능 × ManaPerIntelligence 로 굴러 나온다
        //   (SummonerData.MaxMana). 여기에 숫자를 또 적으면 지능을 올려도
        //   마나가 안 늘어나는 캐릭터가 생겨 스탯 배분의 뜻이 사라진다.
        public int   DeckSlots;

        /// <summary>
        /// 지능 1당 소환력. 비우면 1 (전원 공통값).
        /// ⚠ 결정술사만 낮다 (2026-09-12) — 그릇(지능)은 크게 두고 소환력만 깎아
        ///   "마나는 넉넉한데 몬스터가 약하다" 를 만든다. 흔해지면 배분 비교가 무의미해진다.
        /// </summary>
        public float PowerScale;

        /// <summary>시작 카드 (MonsterSpeciesData.Id). 앞칸부터 놓인다.</summary>
        public string[] Starters;

        /// <summary>
        /// 시작 스킬 카드 (SkillCardData.Id). 없어도 된다.
        ///
        /// ⚠ 이제 전부 비어 있다 (2026-09-04)
        ///   스킬은 3택으로 줍는 카드가 아니라 소환사가 무료로 쓰는 시그니처
        ///   하나가 됐다 (아래 Skill/SkillUses…). 이 칸은 남겨 두지만 채우지 말 것 —
        ///   채우면 덱 8칸 중 하나를 스킬이 먹는다.
        /// </summary>
        public string[] StarterSkills;

        // ── 시그니처 스킬 ────────────────────────────────────
        //
        //  ■ 마나를 안 낸다 — 세기는 횟수로만 조절한다
        //    제한은 **스테이지당 횟수 하나뿐**이다 (사용자 확정, 2026-09-04).
        //      센 스킬(메테오·사형 선고·비석 강림·피의 대가·트롤) → 1회
        //      가벼운 스킬(슬라임·고블린·독성 지대)              → 2회
        //    ⚠ 0 이면 스킬이 없는 소환사다.
        //
        //  ■ 소환형은 SummonSpecies 를 함께 채운다
        //    스킬 하나(SummonSignature)가 여섯 소환사를 덮는다 — 무엇을
        //    부르는지는 스킬이 아니라 소환사가 들고 있다.

        public ActiveSkillId Skill;

        /// <summary>
        /// 시그니처의 표시 이름. 비우면 스킬 SO 이름. '권속 소환' 을 쓰는 소환사는
        /// 반드시 채운다 — 여덟이 나눠 쓰는 스킬이라 이름이 곧 "무엇을 부르나" 다.
        /// </summary>
        public string SkillName;

        /// <summary>스테이지당 사용 횟수. 센 스킬은 1, 가벼운 스킬은 2.</summary>
        public int UsesPerStage;

        /// <summary>SummonSignature 가 부를 종족 ID. 그 스킬이 아니면 비운다.</summary>
        public string SummonSpecies;

        /// <summary>한 번에 부르는 마리 수.</summary>
        public int SummonCount;

        /// <summary>해금 조건. 비우면 기본 해금 (견습 소환사만).</summary>
        public SummonerUnlock[] Unlocks;

        /// <summary>친화 종족 ID. 비우면 아래 AffinityTraits 로 판정한다.</summary>
        public string[] AffinityIds;

        /// <summary>ID 대신 특징으로 묶는 친화. None 이면 사용하지 않는다.</summary>
        public MonsterTrait AffinityTraits;

        public SummonerPerk Perk;
        public float        PerkValue;

        public string   Seed;
        public UnitJob  Look;
        public UnitGrade Grade;

        /// <summary>몬스터 모습으로 서는 소환사의 종족 ID (비인간형만). 비우면 인간형 합성.</summary>
        public string      LookSpecies;

        /// <summary>그 모습의 머리 위 표식.</summary>
        public MonsterMark LookMark;

        /// <summary>그 모습의 몸집 배율. 비우면 1.</summary>
        public float       LookScale;
    }

    // ──────────────────────────────────────────────────────────
    // ■ 로스터 — 여기가 캐릭터 목록의 정본이다
    // ──────────────────────────────────────────────────────────
    static readonly Spec[] Roster =
    {
        // ⚠ 이 배열의 순서가 곧 선택 화면 순서다 (2026-08-28)
        //   SummonerCreator 가 배열 인덱스를 SummonerData.ListOrder 에 심고,
        //   CardCatalogCreator 가 그 값으로 정렬한다. 이름순이 아니다 —
        //   사전순으로 두면 beastmaster 가 첫 칸에 서고 견습이 7번째로 밀린다.
        //
        //   배열은 **해금 단계 순**으로 세워 둔다. 좌우로 넘길수록 깊은 캐릭터가
        //   나오므로, 목록을 훑는 것 자체가 "다음에 무엇을 향해 가는가" 가 된다.
        //   순서를 바꾸면 소환사 데이터를 다시 굽고 카드 목록도 다시 만들 것.

        // ⚠ 해금이 쉬운 순서다 (사용자 지시, 2026-09-15) — 기본 종족만 → 진화체 아무거나 1종 →
        //   1차 하나 → 1차 둘 → 2차 → 대마법사(2차 + 25 스테이지). 시작 카드의 진화체는 반드시 해금 조건이 요구한다 (VerifyStarterUnlocks).

        // ① 견습 소환사 — 슬라임. 가장 쉬운 시작.
        new Spec
        {
            Id = "novice", Name = "견습 소환사",
            Desc = "슬라임을 싸게 부린다. 벽을 두껍게 세우고 천천히 배우는 캐릭터.",
            // ⚠ 지능을 8 → 6 으로 내렸다 (사용자 지적, 2026-09-11 "아직도 너무 강력")
            //   슬라임 −2 할인 + 시그니처 2회가 이미 물량을 준다. 거기에 지능 8 의
            //   큰 그릇·소환력까지 얹혀 모든 축에서 앞섰다. 몫은 체력·패기로 옮겨
            //   "벽은 두껍지만 몬스터는 평범한" 입문 캐릭터로 되돌린다.
            // ⚠ 2026-09-11 다시 — 5/9/5. 그릇은 작고 벽은 가장 두껍다 (입문용).
            Str = 5f, Vit = 9f, Int = 5f,
            AttackRange = 24f, ProjectileSpeed = 22f, AttackSpeed = 0.8f, Defense = 0.10f,
            DeckSlots = 5,   // 기준 — 입문용이라 표준 그대로
            Skill = ActiveSkillId.SummonSignature, UsesPerStage = 2,
            SkillName = "슬라임 떼",   // 표시 이름 — 그림은 부르는 종족의 초상화 (SignatureSkillDisplay)
            SummonSpecies = "slime", SummonCount = 3,
            Starters = new[] { "slime", "skeleton", "goblin" },   // 시작 시너지: 무리 (3) — 기본 종족만이라 조건 없이 열린다
            AffinityIds = new[] { "slime" },
            Perk = SummonerPerk.CheapAffinity, PerkValue = 2f,   // 친화 종족 마나 -2
            Seed = "summoner_novice", Look = UnitJob.Mage, Grade = UnitGrade.Normal,
        },

        // ② 군악대장 — 한 장이 두 라인에 선다 (사용자 지시, 2026-09-12)
        //   ⚠ 개성은 '쌍둥이 무녀' 안에서 가져왔다 — 옆 라인에 한 마리가 공짜로 더 선다.
        //     특성 '쌍둥이 라인'(절반 · 비용 +2)의 약한 상시판이라 값이 겹치지 않는다.
        new Spec
        {
            Id = "bandmaster", Name = "군악대장",
            Desc = "행진곡에 맞춰 두 라인이 함께 나선다.\n" +
                   "카드를 낼 때마다 옆 라인에도 한 마리가 공짜로 선다.",
            Str = 6f, Vit = 7f, Int = 6f,
            AttackRange = 24f, ProjectileSpeed = 24f, AttackSpeed = 0.9f, Defense = 0.12f,
            DeckSlots = 6,   // 넓게 편성해야 두 라인을 모두 채운다
            Skill = ActiveSkillId.SummonSignature, UsesPerStage = 2,
            SkillName = "늑대 행진",   // 표시 이름 — 그림은 부르는 종족의 초상화 (SignatureSkillDisplay)
            SummonSpecies = "wolf", SummonCount = 3,
            // 시작 시너지: 선봉 (늑대+멧돼지 2 · 동) · 야수 (멧돼지+트롤 2/3) — 기본 종족만
            // ⚠ 고블린을 트롤로 바꿨다 (사용자 지적, 2026-09-16 — "고블린은 시너지에 안 들어간다")
            //   고블린(무리·투지)은 늑대·멧돼지와 겹치는 표식이 없었다. 기본 종족 중 이 둘과 표식을 나누는 것은
            //   트롤(야수) 하나다. 비싼 한두 마리 카드라 옆 라인 공짜 1마리(쌍둥이 소집)와도 맞는다.
            Starters = new[] { "wolf", "hog", "troll" },
            AffinityTraits = MonsterTrait.Charger,
            Perk = SummonerPerk.TwinCall, PerkValue = 1f,   // 옆 라인에 1마리
            Seed = "summoner_bandmaster", Look = UnitJob.Knight, Grade = UnitGrade.Rare,

            // ⚠ 두 번째로 여는 소환사다 (사용자 지적, 2026-09-15)
            //   첫 런은 견습의 슬라임을 키운다. 진화는 무작위라 특정 1차를 요구하면 첫 진화가
            //   아무 소환사도 안 연다 — "진화체 1종" 은 무엇으로 진화했든 센다. 한두 런이면 닿는다.
            //   시작 카드가 기본 종족뿐이라 진화체 규칙(VerifyStarterUnlocks)과도 어긋나지 않는다.
            Unlocks = new[]
            {
                new SummonerUnlock { Kind = SummonerUnlockKind.UpgradeCount, Value = 1 },
            },
        },

        // ③ 오크 킹 — 오크가 여럿이면 함께 세진다.
        new Spec
        {
            Id = "orc_king", Name = "오크 킹",
            Desc = "직접 싸우는 소환사. 패기를 끝까지 올려 평타가 가장 무겁다.\n" +
                   "오크·고블린 카드가 한 마리씩 더 불러낸다.",
            Str = 10f, Vit = 5f, Int = 4f,
            AttackRange = 26f, ProjectileSpeed = 26f, AttackSpeed = 1.0f, Defense = 0.12f,
            DeckSlots = 4,   // 직접 싸우는 소환사 — 패기 10 이 칸 수를 대신한다
            Skill = ActiveSkillId.SummonSignature, UsesPerStage = 1,
            SkillName = "오크 전사 소집",   // 표시 이름 — 그림은 부르는 종족의 초상화 (SignatureSkillDisplay)
            SummonSpecies = "orc", SummonCount = 2,
            Starters = new[] { "orc", "war_orc" },   // 시작 시너지: 강철 (2 · 동) · 투지 (2 · 동)
            AffinityIds = new[] { "orc", "goblin", "goblin_archer" },
            // ⚠ 옛 개성 '전쟁 함성'(공격력 누적)을 걷었다 (사용자 지시, 2026-09-12) — 물량 개성으로
            Perk = SummonerPerk.Muster, PerkValue = 1f,   // 친화 카드 마릿수 +1
            Seed = "summoner_orcking", Look = UnitJob.Knight, Grade = UnitGrade.Epic,

            // 시작 카드의 1차(전쟁 오크)를 도감에 올린 뒤에 연다
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "war_orc" },
                },
            },
        },

        // ④ 결정술사 — 아낀 마나가 그릇이 된다 (사용자 지시, 2026-09-12)
        new Spec
        {
            Id = "crystalmancer", Name = "결정술사",
            Desc = "남긴 마나가 결정으로 굳어 그릇을 키운다. 대신 소환력이 약하다.\n" +
                   "아껴 가며 판을 넘길수록 강해지는 장기전 소환사.",
            Str = 4f, Vit = 6f, Int = 9f,
            AttackRange = 26f, ProjectileSpeed = 24f, AttackSpeed = 0.8f, Defense = 0.10f,
            DeckSlots = 5,
            PowerScale = 0.6f,   // 지능 9 → 소환력 5.4 (다른 소환사면 9)
            Skill = ActiveSkillId.SummonSignature, UsesPerStage = 2,
            SkillName = "마력 해골 소환",   // 표시 이름 — 그림은 부르는 종족의 초상화 (SignatureSkillDisplay)
            SummonSpecies = "mana_skeleton", SummonCount = 4,
            Starters = new[] { "mana_skeleton", "goblin", "zombie" },   // 시작 시너지: 무리 (3 · 동)
            AffinityIds = new[] { "skeleton", "skeleton_guard", "mana_skeleton", "goblin", "goblin_archer" },
            Perk = SummonerPerk.Crystallize, PerkValue = 0.20f,   // 남은 마나의 20% → 최대 마나
            Seed = "summoner_crystal", Look = UnitJob.Mage, Grade = UnitGrade.Rare,

            // 시작 카드의 1차(마력 해골)를 도감에 올린 뒤에 연다
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "mana_skeleton" },
                },
            },
        },

        // ⑤ 드루이드 — 야수가 훨씬 빠르다.
        new Spec
        {
            Id = "druid", Name = "드루이드",
            Desc = "야수를 몬다. 판을 넘길 때마다 자연이 마나를 더 돌려준다.",
            Str = 4f, Vit = 6f, Int = 9f,
            AttackRange = 24f, ProjectileSpeed = 24f, AttackSpeed = 0.9f, Defense = 0.12f,
            DeckSlots = 6,   // 친화가 '야수 전부' 다 — 여러 야수를 섞어 굴린다
            Skill = ActiveSkillId.SummonSignature, UsesPerStage = 1,
            SkillName = "멧돼지 돌격",   // 표시 이름 — 그림은 부르는 종족의 초상화 (SignatureSkillDisplay)
            SummonSpecies = "hog", SummonCount = 2,
            Starters = new[] { "slime", "forest_troll", "hog" },   // 시작 시너지: 숲 (2 · 동) — 자연의 회복과 한 결
            AffinityTraits = MonsterTrait.Charger | MonsterTrait.NonHumanoid,
            // ⚠ 옛 개성 '야성 질주'(이동속도)를 걷었다 (사용자 지시, 2026-09-12) — 마나 회복 개성으로
            Perk = SummonerPerk.NatureRestore, PerkValue = 0.10f,   // 판 경계에 최대 마나의 10% 더
            Seed = "summoner_druid", Look = UnitJob.Archer, Grade = UnitGrade.Rare,

            // 시작 카드의 1차(숲의 트롤)를 도감에 올린 뒤에 연다
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "forest_troll" },
                },
            },
        },

        // ⑥ 트롤 조련사 — 친화 종족이 맞으면서 회복한다.
        new Spec
        {
            Id = "troll_tamer", Name = "트롤 조련사",
            Desc = "트롤을 부린다. 맞을수록 회복해 오래 버틴다.",
            // ⚠ 지능 4 → 6 (2026-09-11) — 트롤 12마나에 그릇 36 은 카드 세 장이면 바닥이었다
            Str = 5f, Vit = 8f, Int = 6f,
            AttackRange = 22f, ProjectileSpeed = 22f, AttackSpeed = 0.75f, Defense = 0.18f,
            DeckSlots = 4,   // 트롤은 한 장이 12 마나다 — 칸보다 그릇이 먼저 찬다
            Skill = ActiveSkillId.SummonSignature, UsesPerStage = 1,
            SkillName = "트롤 풀어놓기",   // 표시 이름 — 그림은 부르는 종족의 초상화 (SignatureSkillDisplay)
            SummonSpecies = "troll", SummonCount = 1,
            Starters = new[] { "troll", "swamp_troll" },   // 시작 시너지: 재생 (2 · 동)
            AffinityIds = new[] { "troll", "hog" },
            Perk = SummonerPerk.Regenerate, PerkValue = 1f,
            Seed = "summoner_trolltamer", Look = UnitJob.ShieldBearer, Grade = UnitGrade.Unique,

            // 시작 카드의 1차(늪 트롤)를 도감에 올린 뒤에 연다
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "swamp_troll" },
                },
            },
        },

        // ⑦ 도살자 — 시체를 재료로 쓰는 무거운 캐릭터.
        new Spec
        {
            Id = "butcher", Name = "도살자",
            Desc = "직접 때리고, 쓰러진 몬스터를 재료로 삼는다.\n" +
                   "물량을 갈아 넣는 운용에 어울린다.",
            Str = 9f, Vit = 6f, Int = 4f,
            AttackRange = 20f, ProjectileSpeed = 20f, AttackSpeed = 1.0f, Defense = 0.15f,
            DeckSlots = 5,   // 기준 — 세 계열(멧돼지·좀비·트롤)을 재료로 섞는다
            Skill = ActiveSkillId.BloodPrice, UsesPerStage = 1,
            Starters = new[] { "orc_butcher", "wolf" },   // 시작 시너지: 사냥 (2 · 동)
            AffinityIds = new[] { "hog", "zombie", "troll" },
            Perk = SummonerPerk.FleshGolem, PerkValue = 1f,
            Seed = "summoner_butcher", Look = UnitJob.Knight, Grade = UnitGrade.Unique,

            // 시작 카드의 1차(오크 도살자)를 도감에 올린 뒤에 연다
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "orc_butcher" },
                },
            },
        },

        // ⑧ 고블린 두목 — 환수를 더 많이 받는다.
        new Spec
        {
            Id = "goblin_boss", Name = "고블린 두목",
            // ⚠ 개성이 마나 회복 → 런 골드 배율로 바뀌었다 (사용자 지시, 2026-09-15)
            Desc = "고블린 무리가 전리품을 챙겨 런 골드를 더 많이 번다.\n" +
                   "상점·강화소를 자주 들르는 운용에 어울린다.",
            Str = 7f, Vit = 4f, Int = 8f,
            AttackRange = 23f, ProjectileSpeed = 22f, AttackSpeed = 0.85f, Defense = 0.10f,
            DeckSlots = 6,   // 무리를 넓게 굴린다 — 마왕성이 약한(체력 4) 몫
            Skill = ActiveSkillId.SummonSignature, UsesPerStage = 2,
            SkillName = "고블린 약탈대",   // 표시 이름 — 그림은 부르는 종족의 초상화 (SignatureSkillDisplay)
            SummonSpecies = "goblin", SummonCount = 4,
            Starters = new[] { "goblin", "goblin_archer", "goblin_chief" },   // 시작 시너지: 투지 (2 · 동)
            AffinityIds = new[] { "goblin", "goblin_archer" },
            Perk = SummonerPerk.Plunder, PerkValue = 1.5f,   // 런 골드 ×1.5
            Seed = "summoner_goblinboss", Look = UnitJob.Archer, Grade = UnitGrade.Rare,

            // 시작 카드의 1차(고블린 궁수·고블린 두목)를 도감에 올린 뒤에 연다
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "goblin_archer", "goblin_chief" },
                },
            },
        },

        // ⑨ 스컬 킹 — 스켈레톤 전문. 품질을 한 단계 올려 부른다.
        new Spec
        {
            Id = "skull_king", Name = "스컬 킹",
            Desc = "해골 붙이만 다루지만, 카드 한 장에 해골이 떼로 일어난다.\n" +
                   "본인도 성벽 뒤에서 무겁게 내려친다.",
            Str = 8f, Vit = 6f, Int = 5f,
            AttackRange = 23f, ProjectileSpeed = 22f, AttackSpeed = 0.8f, Defense = 0.10f,
            DeckSlots = 4,   // 해골 둘뿐인 친화 — 카드 한 장이 떼로 부르므로 칸이 적어도 물량이 된다
            Skill = ActiveSkillId.DeathSentence, UsesPerStage = 1,
            Starters = new[] { "skeleton", "skeleton_guard", "mana_skeleton" },   // 시작 시너지: 언데드 (3 · 동) · 무리 2
            AffinityIds = new[] { "skeleton", "skeleton_mage" },
            // ⚠ 옛 개성 '품질 각인'(+1단계 = +10%)은 체감이 가장 약해 교체했다 (사용자 지시, 2026-09-11)
            Perk = SummonerPerk.BoneLegion, PerkValue = 2f,   // 해골 카드 마릿수 +2
            Seed = "summoner_skullking", Look = UnitJob.Knight, Grade = UnitGrade.Unique,

            // 시작 카드의 1차(해골 방패병·마력 해골)를 도감에 올린 뒤에 연다
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "skeleton_guard", "mana_skeleton" },
                },
            },
        },

        // ⑩ 강령술사 — 아군이 죽은 자리에 스켈레톤이 공짜로 일어난다.
        new Spec
        {
            Id = "necromancer", Name = "흑마법사",
            Desc = "몬스터가 죽은 자리에서 스켈레톤이 공짜로 일어난다.\n" +
                   "소모를 각오한 물량 운용일수록 이득이 커진다.",
            Str = 7f, Vit = 3f, Int = 9f,
            AttackRange = 25f, ProjectileSpeed = 22f, AttackSpeed = 0.85f, Defense = 0.08f,
            DeckSlots = 5,   // 기준 — 언데드 금(7)은 확장 편성·유물 칸을 얹어야 닿는다 (의도)
            // ⚠ 비석이 부르는 것도 **이 게임의 스켈레톤 종족**이다
            //   SummonSpecies 를 채워야 GravestoneRunner 가 원작 병사 풀
            //   대신 몬스터 종족을 세운다 (시너지·품질을 그대로 탄다).
            Skill = ActiveSkillId.Gravestone, UsesPerStage = 1,
            SummonSpecies = "skeleton",
            Starters = new[] { "skeleton", "ghoul", "skeleton_mage" },   // 시작 시너지: 언데드 (3 · 동)
            AffinityIds = new[] { "skeleton", "zombie", "skeleton_mage" },
            Perk = SummonerPerk.RaiseOnDeath, PerkValue = 1f,
            Seed = "summoner_necro", Look = UnitJob.Mage, Grade = UnitGrade.Epic,

            // 시작 카드의 1차(구울·해골 술사)를 도감에 올린 뒤에 연다
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "ghoul", "skeleton_mage" },
                },
            },
        },

        // ⑪ 역병 술사 — 좀비가 용사를 잡으면 그 자리에서 좀비가 일어난다.
        new Spec
        {
            Id = "plaguebringer", Name = "역병 술사",
            Desc = "좀비 계열이 적을 쓰러뜨리면 그 자리에서 확률로 새 좀비가 일어난다.\n" +
                   "마왕성이 가장 튼튼하다 — 느리지만 시간이 갈수록 판이 좀비로 덮인다.",
            Str = 3f, Vit = 10f, Int = 6f,
            AttackRange = 21f, ProjectileSpeed = 20f, AttackSpeed = 0.7f, Defense = 0.16f,
            DeckSlots = 4,   // 좀비 한 계열 · 마왕성이 가장 튼튼한 몫
            Skill = ActiveSkillId.PoisonZone, UsesPerStage = 2,
            Starters = new[] { "zombie", "plague_zombie", "poison_slime" },   // 시작 시너지: 역병 (3 · 동)
            AffinityIds = new[] { "zombie" },
            // ⚠ 발동이 '죽을 때' → '적을 쓰러뜨릴 때' 로 바뀌었다 (사용자 지시, 2026-09-11)
            //   PerkValue = 확률. 카드로 낸 좀비만 일으킨다 — 일어난 좀비가 또 일으키면 판이 끝나지 않는다.
            Perk = SummonerPerk.PlagueRise, PerkValue = 0.35f,
            Seed = "summoner_plague", Look = UnitJob.Mage, Grade = UnitGrade.Rare,

            // 시작 카드의 1차(역병 좀비·독 슬라임)를 도감에 올린 뒤에 연다
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "plague_zombie", "poison_slime" },
                },
            },
        },

        // ⑫ 리치 — 소환력이 언데드에게 두 배로 실린다. 본체는 약하다.
        new Spec
        {
            Id = "lich_lord", Name = "리치 로드",
            Desc = "지능에 전부 몰았다. 언데드에게 소환력이 두 배로 실리지만\n" +
                   "본체는 종잇장이라 성벽이 뚫리면 그대로 끝난다.",
            Str = 2f, Vit = 7f, Int = 10f,
            AttackRange = 28f, ProjectileSpeed = 26f, AttackSpeed = 0.65f, Defense = 0.05f,
            DeckSlots = 5,   // 기준 — 언데드 넷이 친화, 금(7)은 확장 편성·유물 칸으로
            Skill = ActiveSkillId.Meteor, UsesPerStage = 1,
            Starters = new[] { "lich", "skeleton_mage", "plague_zombie" },   // 시작 시너지: 사격 (3 · 동)
            AffinityIds = new[] { "skeleton", "skeleton_mage", "zombie", "lich" },
            Perk = SummonerPerk.DeepChannel, PerkValue = 1.6f,   // 친화 소환력 ×1.6 추가
            Seed = "summoner_lichlord", Look = UnitJob.Mage, Grade = UnitGrade.Epic,

            // ⚠ 환생 횟수로 걸지 않는다 — 패배가 곧 환생이라 3분이면 채워진다
            //   그건 진행이 아니라 시간이다.
            //
            // 시작 카드의 1차(해골 술사·역병 좀비)를 도감에 올린 뒤에 연다
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "skeleton_mage", "plague_zombie" },
                },
            },
        },

        // ⑬ 비스트마스터 — 처치할수록 무리가 세진다.
        new Spec
        {
            Id = "beastmaster", Name = "비스트마스터",
            Desc = "야수가 사냥할수록 강해진다. 한 라인에 무리를 몰아 굴린다.",
            Str = 8f, Vit = 5f, Int = 6f,
            AttackRange = 24f, ProjectileSpeed = 24f, AttackSpeed = 0.9f, Defense = 0.12f,
            DeckSlots = 6,   // 비인간형 전부가 친화 — 도감을 채운 사람의 넓은 덱
            Skill = ActiveSkillId.SummonSignature, UsesPerStage = 1,
            SkillName = "늑대 무리",   // 표시 이름 — 그림은 부르는 종족의 초상화 (SignatureSkillDisplay)
            SummonSpecies = "wolf", SummonCount = 3,
            Starters = new[] { "hog", "frost_wolf", "iron_hog" },   // 시작 시너지: 야수 (3 · 동)
            AffinityTraits = MonsterTrait.NonHumanoid,
            Perk = SummonerPerk.PackBond, PerkValue = 1f,
            Seed = "summoner_beastmaster", Look = UnitJob.Archer, Grade = UnitGrade.Rare,

            // 시작 카드의 1차(서리 늑대·철갑 멧돼지)를 도감에 올린 뒤에 연다
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "frost_wolf", "iron_hog" },
                },
            },
        },

        // ⑭ 슬라임 킹 — 같은 종족을 겹칠수록 커진다.
        new Spec
        {
            Id = "slime_king", Name = "슬라임 킹",
            Desc = "부른 슬라임 셋 중 하나는 점액을 뱉는 원거리로 선다.\n" +
                   "앞줄은 몸으로 막고 뒷줄은 쏜다 — 슬라임만으로 진형이 된다.",
            Str = 3f, Vit = 6f, Int = 10f,
            AttackRange = 22f, ProjectileSpeed = 20f, AttackSpeed = 0.75f, Defense = 0.14f,
            DeckSlots = 4,   // 슬라임 한 계열로 진형을 짠다 — 지능 10 이 칸 수를 대신한다
            // ⚠ 견습의 "슬라임 더 많이" 가 아니다 (2026-09-04, 사용자 지적)
            //   왕이 부하를 더 많이 부르는 것은 견습의 강화판일 뿐이다.
            //   왕은 **진화한 슬라임**을 부른다.
            //   ⚠ 힐 슬라임이다 (사용자 지시, 2026-09-15) — 해금 조건이 이미 요구하는 종족이라
            //     "도감에 없는 진화체를 스킬로 세우는" 길이 생기지 않는다 (옛 강철 슬라임은 그랬다).
            //   ⚠ 시너지 카운트는 주지 않으므로(MarkSummoned 미호출) 공짜로 시너지를 켜는 길은 없다.
            Skill = ActiveSkillId.SummonSignature, UsesPerStage = 1,
            SkillName = "치유의 친위대",   // 표시 이름 — 그림은 부르는 종족의 초상화 (SignatureSkillDisplay)
            SummonSpecies = "heal_slime", SummonCount = 2,
            // ⚠ 2차(슬라임 킹)로 시작한다 (사용자 확정, 2026-09-15) — 해금 조건이 그 도감 등록을 요구한다
            Starters = new[] { "slime", "heal_slime", "king_slime" },   // 시작 시너지: 숲 (3 · 동) · 왕권 (1 · 동)
            AffinityIds = new[] { "slime" },
            // ⚠ 옛 개성 '증식'(연달아 부르면 커진다)은 체감이 없어 교체했다 (사용자 지시, 2026-09-11)
            Perk = SummonerPerk.SlimeSpit, PerkValue = 3f,   // 3마리 중 1마리 원거리
            Seed = "summoner_slimeking", Look = UnitJob.Mage, Grade = UnitGrade.Rare,
            // 슬라임 킹은 **슬라임 모습**이다 (사용자 요청, 2026-09-11) — 머리 위에 왕관
            // 기본 슬라임(노랑)의 커다란 판 — 색은 같은 시트가 준다
            LookSpecies = "slime", LookMark = MonsterMark.Crown, LookScale = 1.6f,

            // 시작 카드의 진화체(힐 슬라임·슬라임 킹)를 도감에 올린 뒤에 연다 (VerifyStarterUnlocks)
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "heal_slime", "king_slime" },
                },
            },
        },

        // ⑮ 대마법사 — 그릇이 곧 힘이다 (사용자 지시, 2026-09-12)
        new Spec
        {
            Id = "archmage", Name = "대마법사",
            Desc = "지능에 모두 걸었다. 최대 마나가 클수록 모든 몬스터가 강해지고,\n" +
                   "남은 마나를 태워 전장을 쓸어 버린다.",
            Str = 3f, Vit = 6f, Int = 10f,
            AttackRange = 28f, ProjectileSpeed = 26f, AttackSpeed = 0.7f, Defense = 0.05f,
            DeckSlots = 5,   // 기준 — 그릇을 키우는 것이 이 캐릭터의 성장이다
            Skill = ActiveSkillId.ManaBurst, UsesPerStage = 1,
            Starters = new[] { "lich", "arcane_lich" },   // 시작 시너지: 술법 (2 · 동) · 사격 (2 · 동)
            AffinityIds = new[] { "lich", "archlich", "arcane_lich", "skeleton_mage" },
            Perk = SummonerPerk.ArcaneMight, PerkValue = 0.03f,   // 최대 마나 10당 공/체 +3%
            Seed = "summoner_archmage", Look = UnitJob.Mage, Grade = UnitGrade.Epic,

            // ⚠ 가장 늦게 여는 소환사다 (사용자 지시, 2026-09-15) — 마나 폭발이 화면 전체를 쓸어 버린다.
            //   비전 리치(시작 카드)에 더해 리치 계보의 끝(리치 킹)과 25 스테이지 도달을 요구한다.
            Unlocks = new[]
            {
                new SummonerUnlock
                {
                    Kind       = SummonerUnlockKind.SpeciesUnlocked,
                    SpeciesIds = new[] { "arcane_lich", "lich_king" },
                },
                new SummonerUnlock { Kind = SummonerUnlockKind.BestStage, Value = 25 },
            },
        },
    };

    [MenuItem(ProjectKMenu.Data + "소환사", priority = ProjectKMenu.DataPrio + 23)]
    public static void CreateAll()
    {
        Directory.CreateDirectory(OutputRoot);

        foreach (Spec spec in Roster)
            Create(spec);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SummonerCreator] 완료 — {Roster.Length}명. 경로: {OutputRoot}");
    }

    static void Create(Spec spec)
    {
        VerifyStatBudget(spec);

        string path = $"{OutputRoot}/Summoner_{spec.Id}.asset";

        var so = AssetDatabase.LoadAssetAtPath<SummonerData>(path);
        bool isNew = so == null;
        if (isNew) so = ScriptableObject.CreateInstance<SummonerData>();

        so.Id          = spec.Id;
        so.DisplayName = spec.Name;
        so.Description = spec.Desc;

        // 프리팹 풀 키. UnitPrefabCreator 가 이 이름으로 굽는다.
        // 소환사는 전원 같은 골격을 쓰고 외형만 시드로 갈린다.
        so.PoolKey = "Summoner";

        so.AppearanceSeed  = spec.Seed;
        so.AppearanceJob   = spec.Look;
        so.AppearanceGrade = spec.Grade;

        // 몬스터 모습 — 비우면 인간형 합성 그대로다.
        so.AppearanceSpecies = string.IsNullOrEmpty(spec.LookSpecies)
                             ? null
                             : FindById<MonsterSpeciesData>(spec.LookSpecies, s => s.Id);
        so.AppearanceMark    = spec.LookMark;
        so.AppearanceScale   = spec.LookScale > 0f ? spec.LookScale : 1f;

        if (!string.IsNullOrEmpty(spec.LookSpecies) && so.AppearanceSpecies == null)
            Debug.LogError($"[SummonerCreator] '{spec.Id}' 의 외형 종족 '{spec.LookSpecies}' 를 찾지 못했습니다. " +
                           "먼저 '데이터 생성 > 몬스터 도감' 을 실행하세요.");

        so.Vigor        = spec.Str;
        so.Vitality     = spec.Vit;
        so.Intelligence = spec.Int;

        so.AttackRange     = spec.AttackRange;
        so.ProjectileSpeed = spec.ProjectileSpeed;
        so.AttackSpeed     = spec.AttackSpeed;
        so.Defense         = spec.Defense;

        // ⚠ 상한을 넘기면 강화소·제단·전황이 뒤 칸을 조용히 자른다
        if (spec.DeckSlots < 1 || spec.DeckSlots > RunPerkRule.MaxDeckSlots)
            Debug.LogError($"[SummonerCreator] '{spec.Id}' 카드 칸 {spec.DeckSlots} 이 " +
                           $"범위(1~{RunPerkRule.MaxDeckSlots}) 밖입니다.");
        so.DeckSlots = spec.DeckSlots;
        so.ListOrder = System.Array.IndexOf(Roster, spec);
        so.Unlocks   = spec.Unlocks ?? System.Array.Empty<SummonerUnlock>();

        // ── 시그니처 스킬 ──
        //   ⚠ 이 대입이 빠지면 소환사 SO 에 스킬이 안 실린다 —
        //     화면에는 버튼이 뜨지 않고, 원인도 눈에 안 보인다.
        so.SignatureSkill         = spec.Skill;
        so.SkillUsesPerStage      = spec.UsesPerStage;
        so.SignatureSummonSpecies = ResolveSpecies(spec);
        so.SignatureSummonCount   = Mathf.Max(1, spec.SummonCount);
        so.SignatureName          = spec.SkillName ?? "";

        so.StarterMonsters = ResolveMonsters(spec);
        VerifyStarterUnlocks(spec, so.StarterMonsters);
        so.StarterSkills   = ResolveSkills(spec);
        so.Affinities      = BuildAffinities(spec);

        so.Perk      = spec.Perk;
        so.PerkValue = spec.PerkValue;

        // 환산 계수는 전 캐릭터 공통이다 — 여기가 갈리면 배분 비교가 무의미해진다.
        so.AttackPerVigor             = 2f;
        so.HpPerVitality              = 50f;
        so.SummonPowerPerIntelligence = spec.PowerScale > 0f ? spec.PowerScale : 1f;   // 결정술사만 예외 (Spec 주석)

        if (isNew) AssetDatabase.CreateAsset(so, path);
        else       EditorUtility.SetDirty(so);

        Debug.Log($"[SummonerCreator] {so.DisplayName} — " +
                  $"패기 {so.Vigor}({SummonerVigorRule.DescribeFor(so)}) · " +
                  $"체력 {so.Vitality}(마왕성 {so.MaxCoreHp}) · " +
                  $"지능 {so.Intelligence}(소환력 {so.SummonPower}) · 개성 {spec.Perk.ToKorean()}");
    }

    // ── 검증 ─────────────────────────────────────────────────

    /// <summary>
    /// 합계가 19 가 아니면 알린다.
    ///
    /// ⚠ 조용히 넘기면 캐릭터 선택이 무의미해진다
    ///   합계가 다르면 그냥 더 센 캐릭터가 생긴다. 배분으로 갈려야
    ///   "무엇을 포기할 것인가" 가 선택이 된다.
    /// </summary>
    static void VerifyStatBudget(Spec spec)
    {
        const float Budget = 19f;

        float total = spec.Str + spec.Vit + spec.Int;
        if (Mathf.Approximately(total, Budget)) return;

        Debug.LogError($"[SummonerCreator] '{spec.Id}' 의 3대 스탯 합계가 {total} 입니다 " +
                       $"(기준 {Budget}). 배분을 다시 잡으세요.");
    }

    /// <summary>
    /// 시작 카드에 진화체(1차·2차)가 있으면 그 종족의 도감 등록이 해금 조건에 있어야 한다
    /// (사용자 확정, 2026-09-15).
    ///
    /// ■ 왜 필요한가
    ///   도감은 빈 채로 시작하고 기본 종족만 카드 보상으로 열린다. 진화체는 진화로만 처음 얻는다.
    ///   해금 조건 없이 진화체를 쥐여 주면 **도감에 올린 적 없는 몬스터로 시작**하게 되고,
    ///   런 시작이 그 종족을 도감에 공짜로 올려 준다(RunBootstrap) — 진화의 문이 통째로 우회된다.
    /// </summary>
    static void VerifyStarterUnlocks(Spec spec, MonsterSpeciesData[] starters)
    {
        foreach (MonsterSpeciesData sp in starters)
        {
            if (sp.UpgradeOf == null)        continue;   // 기본 종족 — 카드 보상으로 열린다
            if (RequiresSpecies(spec, sp.Id)) continue;

            Debug.LogError($"[SummonerCreator] '{spec.Name}' 의 시작 카드 '{sp.DisplayName}' 는 진화체인데 " +
                           "해금 조건에 그 종족의 도감 등록(SpeciesUnlocked)이 없습니다 — " +
                           "도감에 없는 몬스터로 시작하게 됩니다.");
        }
    }

    static bool RequiresSpecies(Spec spec, string speciesId)
    {
        if (spec.Unlocks == null) return false;

        foreach (SummonerUnlock u in spec.Unlocks)
            if (u.Kind == SummonerUnlockKind.SpeciesUnlocked
                && u.SpeciesIds != null
                && System.Array.IndexOf(u.SpeciesIds, speciesId) >= 0)
                return true;

        return false;
    }

    // ── 참조 해석 ────────────────────────────────────────────

    /// <summary>소환형 시그니처 스킬이 부를 종족을 찾는다. 소환형이 아니면 null.</summary>
    static MonsterSpeciesData ResolveSpecies(Spec spec)
    {
        if (string.IsNullOrEmpty(spec.SummonSpecies)) return null;

        MonsterSpeciesData found = FindById<MonsterSpeciesData>(spec.SummonSpecies, s => s.Id);

        if (found == null)
            Debug.LogError($"[SummonerCreator] '{spec.Id}' 의 시그니처 소환 종족 " +
                           $"'{spec.SummonSpecies}' 를 찾지 못했습니다. " +
                           "먼저 '데이터 생성 > 몬스터 도감' 을 실행하세요.");

        return found;
    }

    static MonsterSpeciesData[] ResolveMonsters(Spec spec)
    {
        if (spec.Starters == null) return new MonsterSpeciesData[0];

        var list = new List<MonsterSpeciesData>(spec.Starters.Length);
        foreach (string id in spec.Starters)
        {
            MonsterSpeciesData found = FindById<MonsterSpeciesData>(id, s => s.Id);
            if (found == null)
            {
                Debug.LogError($"[SummonerCreator] '{spec.Id}' 의 시작 카드 '{id}' 를 찾지 못했습니다. " +
                               "먼저 '데이터 생성 > 몬스터 도감' 을 실행하세요.");
                continue;
            }
            list.Add(found);
        }

        return list.ToArray();
    }

    static SkillCardData[] ResolveSkills(Spec spec)
    {
        if (spec.StarterSkills == null) return new SkillCardData[0];

        var list = new List<SkillCardData>(spec.StarterSkills.Length);
        foreach (string id in spec.StarterSkills)
        {
            SkillCardData found = FindById<SkillCardData>(id, s => s.Id);
            if (found == null)
            {
                Debug.LogError($"[SummonerCreator] '{spec.Id}' 의 시작 스킬 '{id}' 를 찾지 못했습니다. " +
                               "먼저 '데이터 생성 > 스킬 카드' 를 실행하세요.");
                continue;
            }
            list.Add(found);
        }

        return list.ToArray();
    }

    static SummonerAffinity[] BuildAffinities(Spec spec)
    {
        var list = new List<SummonerAffinity>();

        if (spec.AffinityIds != null)
            foreach (string id in spec.AffinityIds)
                list.Add(new SummonerAffinity { SpeciesId = id });

        if (spec.AffinityTraits != MonsterTrait.None)
            list.Add(new SummonerAffinity { Traits = spec.AffinityTraits });

        if (list.Count == 0)
            Debug.LogWarning($"[SummonerCreator] '{spec.Id}' 에 친화 대상이 없습니다 — " +
                             "모든 종족이 소환력을 0.55배만 받습니다.");

        return list.ToArray();
    }

    static T FindById<T>(string id, System.Func<T, string> idOf) where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        foreach (string guid in guids)
        {
            var so = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (so != null && idOf(so) == id) return so;
        }

        return null;
    }
}
