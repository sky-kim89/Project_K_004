using UnityEngine;

// ============================================================
//  MonsterSpeciesData.cs
//  소환 몬스터 한 종족의 정의. Resources/MonsterSpeciesDatabase 에 모은다.
//
//  ■ 몬스터는 자기 기본 스탯을 갖고 있다
//    원작 병사(Soldier)처럼 장군에게서 파생되는 존재가 아니다.
//    종족마다 고유한 기본 스탯이 있고, 소환사의 소환력은 그 위에 더해진다.
//    → 소환사를 바꿔도 종족의 정체성(탱커는 탱커)은 유지된다.
//
//  ■ 레벨 없음
//    인게임 레벨업 요소가 없다. 강해지는 경로는
//      (a) 품질  (b) 소환사의 소환력  (c) 유물·3택지·특성  (d) 장비
//    ⚠ 도감 수집 버프는 폐기됐다 (CodexData 파일 머리 참고).
//
//  ■ 장비는 붙었다 (MonsterGearData, 2026-09-06)
//    이 SO 에는 아무것도 적지 않는다 — 무엇을 끼웠는지는 세이브가 들고 있고
//    (MonsterGearInventory), 낄 수 있는 칸 수는 품질이 정한다(MonsterGearRule).
//    여기서 갈리는 것은 **몸 형태**뿐이다 — 인간형은 레이어를 갈아 끼우고
//    비인간형은 색조·덩치로 받는다.
//
//  ■ 품질은 여기에 저장하지 않는다
//    품질(UnitGrade)은 "도감 개체당 영구" 값이라 세이브(MonsterCodexData)에 산다.
//    해금 시 임의 배정되고 "품질 개선" 으로 올라간다.
//    이 SO 는 종족의 불변 정의만 담는다 — 세이브에 따라 변하는 값을 넣지 말 것.
//
//  ■ ⚠ 소환 시간(SummonTime) 개념은 없앴다 (2026-08-27)
//    "트롤은 한 마리 나오는 데 6초" 같은 종족별 소환 시간이 있었다. 폐기됐다.
//    무게는 마나 비용 하나로만 표현한다 — 두 축으로 나누면 비싼데 느리기까지 한
//    카드가 이중으로 벌을 받고, 플레이어는 그 둘을 머릿속에서 곱해야 했다.
//    대기열(SummonReservation)은 남아 있다. 배출 간격은 종족과 무관한
//    고정값 하나다(SummonController.DrainInterval) — 같은 프레임에 우르르
//    겹쳐 나오지 않게 하는 연출용 간격이지 밸런스 축이 아니다.
//
//  ■ 종족은 계보를 갖는다 (기본 → 업그레이드)
//      슬라임(분열) ─┬─ 힐 슬라임 (분열 + 죽을 때 아군 회복)
//                    ├─ 독 슬라임 (분열 + 피격 시 적 중독)
//                    └─ 강철 슬라임(분열 + 피격 시 반사)
//    업그레이드는 `UpgradeOf` 로 뿌리를 가리키고, 종족 패시브는 그 계보를
//    거슬러 올라가며 **전부** 모인다 (CollectSpeciesPassives).
//
//    ⚠ 업그레이드에 기본 패시브를 다시 적지 말 것
//      상속으로 자동으로 붙는다. 두 번 적으면 분열이 두 번 일어난다.
//
//    ⚠ 스탯은 상속하지 않는다
//      계보는 **패시브의 상속**만 뜻한다. HP·공격력은 업그레이드마다 따로 잡는다 —
//      "기본의 몇 배" 로 파생시키면 기본을 손볼 때마다 파생 전부가 흔들린다.
//
//  ■ 카드 레벨 — 이 종족을 중복해서 얼마나 모았나
//    같은 카드를 또 주우면 레벨이 오르고(CardLevelRule),
//      · 스탯이 레벨당 +18%
//      · LevelPassives 가 한 칸씩 열린다 (Lv2 → [0], Lv3 → [1], Lv4 → [2])
//    만렙(Lv5)이 되면 다른 카드를 흡수해 그 카드의 SignaturePassive 를 배운다.
//
//    ⚠ 유닛이 성장하는 게 아니다 — **손에 든 카드**가 좋아지는 것이다
//      "인게임 레벨업 없음" 규칙과 어긋나지 않는다. 필드의 몬스터는
//      나올 때 확정된 스탯으로 살다 죽는다.
// ============================================================

[CreateAssetMenu(fileName = "Monster_", menuName = "ProjectK/MonsterSpeciesData")]
public class MonsterSpeciesData : ScriptableObject
{
    [Header("식별")]
    [Tooltip("내부 식별자. 도감 해금 상태가 이 값으로 저장되므로 바꾸지 않는다.")]
    public string Id;

    [Tooltip("표시 이름 (도감 · 소환 카드).")]
    public string DisplayName;

    [Tooltip("신체 형태. 외형·장비 처리 경로가 여기서 갈린다.\n" +
             "인간형 = CharacterBuilder 합성 + 장비 가능\n" +
             "비인간형 = 완성 SpriteLibrary 통짜 + 장비 불가")]
    public MonsterBodyType BodyType = MonsterBodyType.Humanoid;

    [Header("외형 — 인간형 전용")]
    [Tooltip("종족. 외형(Body/Head/Eyes/Ears)을 EnemyAppearanceRoller 가 이 값으로 결정한다.\n" +
             "BodyType 이 NonHumanoid 이면 사용하지 않는다.")]
    public EnemyRace Race;

    [Header("외형 — 비인간형 전용")]
    [Tooltip("완성된 SpriteLibraryAsset.\n" +
             "Tools > Project K > 몬스터 라이브러리 굽기 로 생성한 것을 넣는다.\n" +
             "원본(Bonus/Monsters/*/SpriteLibrary.asset)을 그대로 넣으면 안 된다 - \n" +
             "카테고리 이름이 Attack/Death 라서 Animator 가 못 찾는다.")]
    public UnityEngine.U2D.Animation.SpriteLibraryAsset NonHumanoidLibrary;

    // ──────────────────────────────────────────────────────────
    // ■ 겉모습으로 종족을 가른다 (사용자 요청, 2026-09-09)
    //
    //   같은 계보의 진화체는 **같은 그림**을 쓴다 — 인간형은 EnemyRace 가
    //   뿌리와 같고, 비인간형은 라이브러리를 통째로 물려받는다. 라인에 서면
    //   힐 슬라임과 강철 슬라임이 구분되지 않는다.
    //   고블린·오크·좀비도 같은 문제다 — 셋 다 초록 계열 인간형이라 전장에서는
    //   한 덩어리로 보인다.
    //
    //   그래서 축을 둘 준다 — **색조**(계보 안에서 가른다)와 **덩치**(종족끼리 가른다).
    //   ⚠ 둘 다 정본은 MonsterCodexCreator 로스터다. 값을 여기 적지 말 것.
    // ──────────────────────────────────────────────────────────

    [Header("겉모습 — 색조·덩치")]
    [Tooltip("몸 전체에 곱하는 색. 흰색 = 원래 색. " +
             "진화체가 뿌리와 같은 그림을 쓰므로 색으로 가른다. " +
             "장비 색조(MonsterGearVisual.Tint)와 곱해지니 둘 다 옅어야 한다.")]
    public Color BodyTint = Color.white;

    [Tooltip("프리팹 원본 크기에 곱하는 배율. 1 = 그대로. " +
             "히트박스·분리 반경도 함께 커진다 (UnitSizeComponent.Radius 가 localScale 에서 나온다).")]
    public float BodyScale = 1f;

    [Tooltip("몸 비율. (1,1) 이 그대로. (0.86, 1.22) 면 길쭉해진다. " +
             "같은 그림을 쓰는 계보를 실루엣으로 가르는 축이다 — 색조는 원본 그림에 " +
             "곱해지는 값이라 바탕이 짙으면 차이가 묻힌다.")]
    public Vector2 BodyStretch = Vector2.one;

    [Tooltip("머리 위 표식 (MonsterMarkView). 같은 그림을 쓰는 계보를 기호로 가른다 — " +
             "색조·몸 비율로도 안 갈릴 때 쓴다 (힐 슬라임 십자 · 독 슬라임 물방울).")]
    public MonsterMark Mark = MonsterMark.None;

    [Header("공통")]
    [Tooltip("미리 준비된 프리팹의 풀 키 (PoolType.Unit).")]
    public string PoolKey;

    // ⚠ 초상화를 대신할 그림 칸(Icon)은 없앴다 (2026-08-28)
    //   "손으로 넣어 두면 그것이 우선" 이라는 우회로였는데, 종족 아이콘을
    //   그 칸에 자동으로 채운 순간 **게임 전체의 초상화가 납작한 아이콘으로
    //   바뀌었다.** 화면 코드는 멀쩡했고 데이터 한 줄이 그 앞을 막고 있어서
    //   어디를 봐야 할지도 알기 어려웠다.
    //
    //   카드 바·대기열·보상·통계에 뜨는 몬스터 그림은 **언제나 런타임 합성
    //   초상화**다 (MonsterPortraitProvider). 예외를 두지 않는다.
    //   작은 식별 그림이 필요한 자리는 아래 LineageIcon 을 쓴다.

    [Tooltip("계보(뿌리 종족) 아이콘. 친화 표시·목록처럼 초상화를 띄우기엔 좁은 자리 전용.\n" +
             "⚠ 초상화를 대신하지 않는다 — 카드에 뜨는 그림은 언제나 합성 초상화다.\n" +
             "MonsterIconGenerator 가 채운다. 업그레이드는 뿌리와 같은 그림을 공유한다.")]
    public Sprite LineageIcon;

    /// <summary>장비를 착용할 수 있는가. 비인간형은 레이어가 없어 원천적으로 불가능하다.</summary>
    public bool CanEquip => BodyType == MonsterBodyType.Humanoid;

    // ──────────────────────────────────────────────────────────
    // ■ 소환 비용
    // ──────────────────────────────────────────────────────────

    [Header("소환")]
    [Tooltip("카드 1탭에 드는 마나. 완주한 개체가 자기 몫을 환수한다.")]
    public float ManaCost = 1f;

    [Tooltip("카드 1탭에 나오는 마리 수.\n" +
             "물량 종족은 여럿, 객체 강화 종족은 1마리다.\n" +
             "환수 지분은 ManaCost 를 이 수로 나눈 값이다 (1마리 도착 = 그 몫만 환수).")]
    [Range(1, 20)]
    public int SummonCount = 1;

    /// <summary>개체 1마리가 환수하는 마나. 소환 비용을 마리 수로 나눈 값이다.</summary>
    // ⚠ ManaRefundPerUnit 은 없앴다 (2026-08-28)
    //   "완주하면 개체당 이만큼 환수" 를 위한 값이었는데, 환수 자체가
    //   폐기됐다. 살아남은 몬스터는 마나로 녹지 않고 제 라인으로 돌아간다
    //   (MonsterLineReturner). 남겨 두면 없는 규칙을 있는 것처럼 읽히게 한다.

    // ──────────────────────────────────────────────────────────
    // ■ 공격 형태 — 근접이냐 원거리냐
    // ──────────────────────────────────────────────────────────

    [Header("공격 형태")]
    [Tooltip("근접 = 붙어서 때린다 / 원거리 = 발사체를 쏜다.\n" +
             "ECS 쪽에서 처리하는 잡 자체가 갈린다 — MonsterAttackKind 참고.")]
    public MonsterAttackKind AttackKind = MonsterAttackKind.Melee;

    [Tooltip("원거리일 때 쏘는 발사체. 근접이면 사용하지 않는다.")]
    public MonsterProjectileKind Projectile = MonsterProjectileKind.Arrow;

    [Tooltip("발사체 속도. 0 이면 발사체 종류의 기본값을 쓴다(화살 15 · 마법구 10).\n" +
             "사거리가 길수록 올려야 착탄이 늘어지지 않는다.")]
    [Min(0f)]
    public float ProjectileSpeed;

    // ──────────────────────────────────────────────────────────
    // ■ 특징 — 이 몬스터를 어떤 방향으로 쓰는지
    // ──────────────────────────────────────────────────────────

    [Header("특징")]
    [Tooltip("도감에 표시되는 운용 방향. 빌드 설계용 특성이 이 값을 조건으로 읽는다.")]
    public MonsterTrait Traits = MonsterTrait.None;

    [Tooltip("시너지 표식. 덱에 몇 장 모였는지로 단계(동·은·금)가 열린다.\n" +
             "⚠ Traits 와 다른 축이다 - 그쪽은 표시용 분류다.\n" +
             "⚠ 계보로 상속되지 않는다. 진화는 표식을 바꾸므로 종족마다 직접 적는다\n" +
             "  (강철 슬라임은 숲을 잃고 강철을 얻는다).")]
    public MonsterTag Tags = MonsterTag.None;

    [TextArea(2, 3)]
    [Tooltip("도감 설명 한두 줄. 숫자로 안 보이는 운용 요령을 적는다.")]
    public string Description;

    // ──────────────────────────────────────────────────────────
    // ■ 종족 기본 스탯 — 소환력이 이 위에 더해진다
    // ──────────────────────────────────────────────────────────

    [Header("종족 기본 스탯")]
    public float MaxHp       = 100f;
    public float Attack      = 10f;
    public float AttackRange = 1.5f;
    public float AttackSpeed = 1f;
    public float MoveSpeed   = 2f;

    [Range(0f, 0.95f)]
    public float Defense     = 0f;

    [Range(0f, 1f)]
    public float CritChance  = 0.05f;
    public float CritDamage  = 1.5f;

    // ──────────────────────────────────────────────────────────
    // ■ 소환력 반응도 — 같은 소환사라도 종족마다 다르게 받는다
    //   전부 1.0 이면 모든 종족이 똑같이 성장해 소환사 선택의 맛이 사라진다.
    // ──────────────────────────────────────────────────────────

    [Header("소환력 반응도 (배율)")]
    [Tooltip("소환력 보너스를 HP 로 받는 비율. 탱커 종족일수록 높게.")]
    public float SummonPowerToHp = 1f;

    [Tooltip("소환력 보너스를 공격력으로 받는 비율. 딜러 종족일수록 높게.")]
    public float SummonPowerToAttack = 1f;

    // ──────────────────────────────────────────────────────────
    // ■ 고유 스킬 / 패시브 — 기존 시스템으로 그대로 발동한다
    // ──────────────────────────────────────────────────────────

    [Header("계보")]
    [Tooltip("이 종족이 업그레이드라면, 그 뿌리가 되는 기본 종족.\n" +
             "비우면 이 종족 자신이 계보의 뿌리다.\n" +
             "⚠ 뿌리의 종족 패시브는 자동으로 상속된다 — 아래에 다시 적지 말 것.")]
    public MonsterSpeciesData UpgradeOf;

    [Tooltip("이 종족으로 진화할 수 있는 **다른** 하위 종족들 (UpgradeOf 말고도).\n" +
             "비워 두는 것이 보통이다 — 2차 업그레이드처럼 여러 1차에서 올라오는 종족만 쓴다.")]
    public MonsterSpeciesData[] AlsoUpgradeOf = new MonsterSpeciesData[0];

    /// <summary>
    /// 그 종족(id)에서 이 종족으로 진화할 수 있는가.
    ///
    /// ■ ⚠ 부모가 여럿일 수 있다 (사용자 확정, 2026-09-15)
    ///   슬라임 킹은 힐·독·강철 슬라임 <b>어느 것</b>에서도 올라온다 — "슬라임의 왕" 이
    ///   한 갈래에만 붙으면 나머지 두 갈래를 키운 런에서는 영영 못 본다.
    ///
    /// ⚠ 진화 판정을 하는 곳은 전부 이 함수를 지난다
    ///   (CardEvolution.CollectUpgrades · CanEvolveTo · MonsterCodexCreator 검산).
    ///   <c>UpgradeOf.Id</c> 를 직접 비교하지 말 것 — 대표 부모 하나만 통과한다.
    ///
    /// ⚠ UpgradeOf 는 여전히 <b>대표 부모</b>다 — 계보 패시브 상속(CollectSpeciesPassives)과
    ///   RootSpecies 는 그 한 줄만 탄다. 여러 줄을 타면 같은 패시브가 두 번 붙는다.
    /// </summary>
    public bool IsUpgradeFrom(string speciesId)
    {
        if (string.IsNullOrEmpty(speciesId)) return false;

        if (UpgradeOf != null && UpgradeOf.Id == speciesId) return true;

        for (int i = 0; i < AlsoUpgradeOf.Length; i++)
            if (AlsoUpgradeOf[i] != null && AlsoUpgradeOf[i].Id == speciesId) return true;

        return false;
    }


    [Tooltip("이 종족만의 고유 패시브. 계보를 타고 아래로 상속된다.\n" +
             "\"슬라임은 죽으면 분열한다\" 처럼 종족을 종족답게 만드는 것 하나.")]
    public SpeciesPassive SpeciesTrait = SpeciesPassive.None;

    [Tooltip("SpeciesTrait 말고 **더** 갖는 고유 패시브. 비워 두는 것이 보통이다.\n" +
             "2차 업그레이드처럼 '뿌리 각성판 + 제 특성' 둘이 필요한 종족만 쓴다.")]
    public SpeciesPassive[] ExtraTraits = new SpeciesPassive[0];

    [Tooltip("계보에서 **물려받지 않을** 패시브. 비워 두는 것이 보통이다.\n" +
             "윗단계와 성격이 갈리는 종족만 쓴다 (느린 2차가 빠른 1차의 '신속' 을 끊는 식).")]
    public SpeciesPassive[] BlockedInherit = new SpeciesPassive[0];

    /// <summary>
    /// 이 종족이 계보에서 물려받기를 <b>거부하는</b> 패시브인가.
    ///
    /// ■ 왜 필요한가 — 계보가 갈래를 틀 수 있다 (사용자 지시, 2026-09-15)
    ///   트롤 계보가 그렇다. 뿌리 트롤은 '느리고 두껍다' 인데 1차 숲의 트롤이
    ///   <b>작고 빠른</b> 갈래로 틀었고(신속), 2차 고대 트롤은 다시 <b>느리고 무거운</b>
    ///   쪽으로 돌아온다. 그런데 상속은 한 줄로만 내려오므로, 막지 않으면
    ///   "느려지는 대가로 범위를 사는" 종족이 '신속' 을 함께 들고 선다 —
    ///   그 둘은 서로를 지운다.
    ///
    /// ⚠ 제 것(SpeciesTrait·ExtraTraits)은 막지 않는다 — 물려받는 것만 본다.
    ///   자기가 적어 놓고 자기가 막는 조합은 실수이지 규칙이 아니다.
    /// ⚠ 각성판으로 덮는 것과 다르다 — 그쪽(PassiveResolver)은 상위 호환으로
    ///   <b>바꾸는</b> 것이고, 이건 아예 <b>없애는</b> 것이다.
    /// </summary>
    bool IsBlocked(SpeciesPassive p)
    {
        for (int i = 0; i < BlockedInherit.Length; i++)
            if (BlockedInherit[i] == p) return true;

        return false;
    }

    /// <summary>계보의 뿌리. 업그레이드가 아니면 자기 자신이다.</summary>
    public MonsterSpeciesData RootSpecies
    {
        get
        {
            MonsterSpeciesData cursor = this;

            // ⚠ 순환 참조 보호 — 인스펙터에서 서로를 가리키게 만들면 여기서 멎는다
            for (int guard = 0; guard < 8 && cursor.UpgradeOf != null; guard++)
                cursor = cursor.UpgradeOf;

            return cursor;
        }
    }

    /// <summary>
    /// 이 종족이 갖는 종족 패시브 전부 — 뿌리부터 자기까지.
    ///
    /// 순서는 **뿌리가 먼저**다. 분열(기본)이 회복(업그레이드)보다 먼저 처리돼야
    /// "나뉜 뒤에 회복" 이라는 그림이 나온다.
    /// </summary>
    public void CollectSpeciesPassives(System.Collections.Generic.List<SpeciesPassive> into)
    {
        // 계보를 위로 훑어 담은 뒤 뒤집는다 — 재귀 없이 순서를 맞추는 가장 짧은 길.
        int start = into.Count;

        // ⚠ 제 것부터 담는다 — ExtraTraits 도 여기서 함께 (같은 단계의 것들이다)
        for (int i = ExtraTraits.Length - 1; i >= 0; i--)
            if (ExtraTraits[i] != SpeciesPassive.None)
                into.Add(ExtraTraits[i]);

        MonsterSpeciesData cursor = this;
        for (int guard = 0; guard < 8 && cursor != null; guard++)
        {
            if (cursor.SpeciesTrait != SpeciesPassive.None
                && !IsBlocked(cursor.SpeciesTrait))
                into.Add(cursor.SpeciesTrait);

            cursor = cursor.UpgradeOf;
        }

        into.Reverse(start, into.Count - start);
    }

    [Header("고유 능력")]
    [Tooltip("고유 액티브 스킬. None 이면 없음. 발동은 ActiveSkillAISystem 이 그대로 처리한다.")]
    public ActiveSkillId ActiveSkill = ActiveSkillId.None;

    [Tooltip("고유 패시브. 카드 레벨과 무관하게 항상 붙는다.\n" +
             "발동은 PassiveSkillRuntimeSystem 이 그대로 처리한다.")]
    public PassiveSkillType[] Passives = new PassiveSkillType[0];

    // ── 권속 (2차 업그레이드 전용, 사용자 지시 2026-09-15) ─────
    //
    //  ■ 두 자리가 이 셋을 함께 읽는다
    //    ① 권속 소환 스킬 (ActiveSkillId.SummonBrood) — 주기적으로 BroodCount 마리
    //    ② 왕의 분열 (SpeciesPassive.KingSplit)       — 죽을 때 KingSplitCount 마리
    //    둘 다 **같은 종족**을 낸다. 갈라 두면 "부를 때와 터질 때 나오는 것이 다른" 왕이 된다.
    //
    //  ⚠ 부르는 것은 언제나 **아래 단계**여야 한다
    //    자기 자신을 부르면 부른 개체가 또 부른다. 세대 제한(MaxReproduceGeneration)이
    //    분열은 막지만 스킬은 안 막는다 — 스킬은 세대를 보지 않는다.
    //    MonsterCodexCreator 의 검산이 이 조건을 본다.

    [Header("권속 (2차 업그레이드)")]
    [Tooltip("이 종족이 불러내는 하위 종족. 비우면 권속 소환도 왕의 분열도 아무것도 안 낸다.")]
    public MonsterSpeciesData BroodSpecies;

    [Tooltip("권속 소환 스킬이 한 번에 부르는 마릿수. 0 이면 스킬을 달지 않는다.")]
    public int BroodCount = 0;

    [Tooltip("권속 소환 스킬의 쿨다운(초). 0 이면 기본값을 쓴다.")]
    public float BroodCooldown = 0f;

    /// <summary>
    /// 권속 소환의 기준 쿨다운 — 종족 값이 없으면 SO 기본값. 전투(MonsterRuntimeBridge)와
    /// 몬스터 상세의 '권속 소환' 줄이 같은 값을 읽는다. ⚠ 술법 쿨감은 안 들어간 원본이다.
    /// </summary>
    public float BroodBaseCooldown
    {
        get
        {
            if (BroodCooldown > 0f) return BroodCooldown;
            ActiveSkillData data = ActiveSkillDatabase.Current?.Get(ActiveSkillId.SummonBrood);
            return data != null ? data.Cooldown : 14f;
        }
    }


    // ──────────────────────────────────────────────────────────
    // ■ 카드 레벨로 열리는 능력
    // ──────────────────────────────────────────────────────────

    [Header("카드 레벨 개방")]
    [Tooltip("레벨이 오를 때마다 열리는 **정해진 고정 효과**.\n" +
             "[0] = Lv2 · [1] = Lv3 · [2] = Lv4 · [3] = Lv5.\n" +
             "⚠ 어빌리티를 대체하는 축이다 — MonsterLevelBonus 파일 머리 참고.")]
    public MonsterLevelBonus[] LevelBonuses = new MonsterLevelBonus[0];

    [Tooltip("이 종족을 합성 재료로 썼을 때 상대 카드가 배워 가는 개성.\n" +
             "⚠ 그 종족을 한 줄로 요약하는 패시브를 고를 것 — " +
             "합성의 값어치가 이 하나로 정해진다.")]
    public PassiveSkillType SignaturePassive = PassiveSkillType.None;

    /// <summary>
    /// 이 레벨까지 열린 레벨 보너스 칸 수. Lv1 은 0 칸이다.
    /// </summary>
    public int OpenedBonusCount(int cardLevel)
        => Mathf.Clamp(cardLevel - 1, 0, LevelBonuses.Length);

    /// <summary>
    /// 카드 레벨 기준으로 지금 열려 있는 패시브를 모아 준다.
    /// 항상 붙는 Passives + 레벨 보너스가 함께 여는 패시브.
    ///
    /// ⚠ 융합으로 배운 것은 여기 들어오지 않는다
    ///   융합이 넘겨주는 것은 **종족 패시브(SpeciesPassive)** 라 축이 다르다.
    ///   그쪽은 MonsterSpawner 가 SummonDeckSlot.CollectLearned 로 따로 모은다.
    /// </summary>
    public void CollectPassives(int cardLevel,
                                System.Collections.Generic.List<PassiveSkillType> into)
    {
        // ⚠ 같은 패시브를 두 번 담지 않는다
        //   슬롯이 3칸뿐이라(GeneralPassiveSetComponent) 중복 하나가 곧 한 칸을
        //   버리는 것이다. 게다가 슬롯마다 따로 발동하므로 **효과도 두 번** 돈다.
        //   레벨 보너스가 여는 패시브와 종족이 늘 갖는 패시브가 같을 수 있고,
        //   융합으로 이미 가진 것을 또 배울 수도 있다 — 어느 쪽도 에러가 안 난다.
        for (int i = 0; i < Passives.Length; i++)
            AddOnce(into, Passives[i]);

        int opened = OpenedBonusCount(cardLevel);
        for (int i = 0; i < opened; i++)
            AddOnce(into, LevelBonuses[i].Passive);
    }

    /// <summary>이미 들어 있으면 넣지 않는다. None 도 넣지 않는다.</summary>
    static void AddOnce(System.Collections.Generic.List<PassiveSkillType> into,
                        PassiveSkillType passive)
    {
        if (passive == PassiveSkillType.None) return;
        if (into.Contains(passive))           return;

        into.Add(passive);
    }

    /// <summary>
    /// 이 레벨까지 열린 스탯 보너스를 전부 얹는다.
    ///
    /// ⚠ 반드시 다른 가산이 끝난 뒤에 부를 것
    ///   비율 보너스는 **그 시점의 값**에 곱해 더한다. 소환력·품질보다 먼저
    ///   부르면 종족 기본값만 기준이 되어 레벨업의 값어치가 확 줄어든다.
    /// </summary>
    public void ApplyLevelBonuses(UnitStat stat, int cardLevel)
    {
        int opened = OpenedBonusCount(cardLevel);
        for (int i = 0; i < opened; i++)
            LevelBonuses[i].ApplyTo(stat);
    }

    // ──────────────────────────────────────────────────────────
    // ■ 소환 연출
    // ──────────────────────────────────────────────────────────

    [Header("소환 연출")]
    [Tooltip("마법진이 뜨고 몬스터가 나오기까지의 시간(초). 이 동안은 무적·정지 상태다.")]
    public float SummonDelay = 0.1f;

    // ⚠ 키는 이펙트 프리팹 파일명 그대로다
    //   풀 등록이 폴더 스캔(파일명 = 풀 키)이라 한 글자만 달라도 조용히 실패한다.
    //   한동안 "FX_SummonCircle" 로 적혀 있어(밑줄 하나 빠짐) 소환 연출이
    //   아예 뜨지 않았다 — 몬스터가 마법진 없이 맨땅에서 튀어나왔다.
    //   정본 파일: Assets/_project/2.Prefabs/Effect/FX_Summon_Circle.prefab
    [Tooltip("소환 마법진 이펙트 키. 비우면 연출 없이 바로 나온다.")]
    public string SummonCircleEffectKey = SkeletonSpawner.SummonEffectKey;
}
