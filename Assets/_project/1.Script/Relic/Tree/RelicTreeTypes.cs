using System;
using UnityEngine;

// ============================================================
//  RelicTreeTypes.cs
//  유물 테크트리 — 타입 정의 (노드 정의는 RelicTreeCatalog.cs)
//  ⚠ 2026-09-07 에 원작 트리(장수·병사 축)를 통째로 갈아 끼웠다.
//
//  ■ 왜 SO 가 아니라 코드 테이블인가
//    노드가 수십 개다. SO 로 만들면 부모 참조가 끊긴 에셋을 눈으로 찾아야 하고
//    가지 구조를 한눈에 볼 수 없다. 트리는 "표 하나"로 있을 때만 관리된다.
//    (구 RelicData SO + RelicPopup 은 2026-08-25 에 제거됐다)
//
//  ■ 좌표계
//    정수 그리드. +Y = 위. 뿌리에서 **사방 4갈래**만 뻗는다.
//      위     = 소환수 (날카로운 발톱) → 본선·종족 패시브·장비 세 갈래
//      아래   = 마왕성 (두꺼운 성문)   → 본선·용사 약화 두 갈래
//      왼쪽   = 마나   (넓은 그릇)     → 그릇·회복 두 갈래
//      오른쪽 = 통솔   (부름의 나팔)   → 템포·덱/시너지·재화 세 갈래
//
//    ⚠ 뿌리에 갈래를 더 붙이지 말 것
//      첫 화면에서 여러 갈래가 한꺼번에 열리면 무엇부터 찍을지 판단이 안 선다.
//      8방향 분기 자체는 살아 있다 — 아래쪽 노드가 쓴다.
//
//    ⚠ 좌표 간격 규칙 (노드 칸이 겹친다)
//      한 칸 = 118px 인데 노드 카드는 세로 130px 쯤 된다.
//      같은 x 면 dy ≥ 2, 같은 y 면 dx ≥ 2. 대각선은 자유.
//
//  ■ 해금·시야 규칙
//    부모 레벨 ≥ 1 이어야 자식이 열린다(Unlocked).
//    열리기 전까지는 존재 자체가 보이지 않는다(Visible == false) — 안개.
//    단, 보이는 노드의 자식은 "미지의 노드"로 실루엣만 그린다 (트리 끝을 알 수 있게).
// ============================================================

// ── 계열 ──────────────────────────────────────────────────────
//
//  ⚠ 원작 계열(공격/체력/병사/유틸)을 통째로 갈아 끼웠다 (2026-09-07)
//    그쪽은 **장수와 병사**의 축이었다. 이 게임에서 장수·병사·직업은 전부
//    적(용사)의 장치라, 69노드 중 40개가 "적을 강화하는 유물" 이었다.
//    새 네 갈래는 이 게임이 실제로 굴리는 자원 축과 1:1 이다.
public enum RelicBranch
{
    Root      = 0,
    Brood     = 1,   // 소환수 — 몬스터 스탯·종족 패시브·장비          (위)
    Keep      = 2,   // 마왕성 — 성 체력·소환사 평타·용사 약화         (아래)
    Font      = 3,   // 마나   — 그릇·회복·비용·과부하                 (왼쪽)
    Dominion  = 4,   // 통솔   — 배출 속도·물량·덱·시너지·런 보상      (오른쪽)
}

// ── 노드 ID ───────────────────────────────────────────────────
//  1xx = 소환수 / 2xx = 마왕성 / 3xx = 마나 / 4xx = 통솔
//  ⚠ 번호는 계열 표시일 뿐 트리 순서가 아니다 — 부모는 RelicTreeCatalog 가 정한다
//  ⚠ 아이콘 파일명이 이 이름에서 나온다 (RelicIconKey) — 바꾸면 다시 구울 것
public enum RelicNodeId
{
    None = 0,

    /// <summary>뿌리 — 몬스터 공격력·체력 +5%. 여기서 사방 4갈래가 뻗는다.</summary>
    N_Origin            = 1,

    // ── 소환수 · 본선 (위) ───────────────────────────────────
    N_Claw              = 101,  // 날카로운 발톱   — 공격력 +4%
    N_Carapace          = 102,  // 굳은 껍질       — 체력 +5%
    N_Fang              = 103,  // 사나운 이빨     — 공격력 +5%
    N_ThickHide         = 104,  // 두꺼운 가죽     — 방어율 +2%p
    N_Predator          = 105,  // 포식자          — 치명 +3%p, 치명피해 +12%
    N_UndyingFlesh      = 106,  // 불사의 살점     — 체력 +10%
    N_DemonHorde        = 107,  // 마왕의 군세     — 공/체 +12%
    N_EndlessSwarm      = 108,  // 끝없는 무리     — 카드당 마릿수 +1 (끝·200pt)
    N_SwarmCall         = 109,  // 무리의 부름     — 카드당 마릿수 +1 (100pt, 2026-09-12)

    // ── 소환수 · 종족 패시브 (왼쪽 위) ───────────────────────
    N_FeralMemory       = 111,  // 치유의 기억     — 재생·회복량 +10% (옛 '야성의 기억')
    N_SplitLegacy       = 112,  // 분열의 유산     — 분열·재조립체 배율 +0.07
    N_DeathToll         = 113,  // 죽음의 대가     — 사망 발동 패시브 +25%
    N_PlagueLore        = 114,  // 역병의 지혜     — 중독·화상·역병 피해 +15%

    // ── 소환수 · 장비 (오른쪽 위) ────────────────────────────
    N_ForgeMemory       = 121,  // 대장간의 기억   — 런 종료 상자 +1
    N_Bespoke           = 122,  // 맞춤 제작       — 장비 스탯 +20%

    // ── 마왕성 · 본선 (아래) ─────────────────────────────────
    N_ThickGate         = 201,  // 두꺼운 성문     — 마왕성 체력 +2
    N_SealedWall        = 202,  // 봉인된 성벽     — 마왕성 체력 +2
    N_DemonMajesty      = 203,  // 마왕의 위엄     — 평타 비율 +2%p
    N_Rebuild           = 204,  // 재건            — 판을 넘길 때 성 +1
    N_UnbrokenKeep      = 205,  // 불락의 성       — 마왕성 체력 +4
    N_EternalThrone     = 206,  // 영원한 옥좌     — 마왕성 체력 +6

    // ── 마왕성 · 용사 약화 (오른쪽 아래) ─────────────────────
    N_TrialBaptism      = 221,  // 시련의 세례     — 용사 체력 −2.5%
    N_FearBrand         = 222,  // 공포의 각인     — 용사 공격력 −2.5%
    N_SlowMarch         = 223,  // 느려진 진군     — 용사 이동속도 −5%
    N_WitherCurse       = 224,  // 쇠약의 저주     — 용사 체력 −4%
    N_Disarm            = 225,  // 무력화          — 용사 공격력 −4%
    N_DoomProphecy      = 226,  // 몰락의 예언     — 용사 체력 −12%

    // ── 마나 (왼쪽) ──────────────────────────────────────────
    N_WideVessel        = 301,  // 넓은 그릇       — 최대 마나 +4%
    N_DeepSpring        = 302,  // 깊은 샘         — 최대 마나 +5%
    N_Frugality         = 303,  // 절약            — 소환 비용 −1 (Lv1·100pt, 2026-09-12)
    N_FlowingMana       = 304,  // 흐르는 마력     — 스테이지 회복 +8%
    N_Patience          = 305,  // 인내            — 과부하 계수 −0.01
    N_EndlessWell       = 306,  // 마르지 않는 우물 — 최대 마나 +10%
    N_MeditationCrystal = 307,  // 명상의 결정     — 스테이지 회복 +12%
    N_InfiniteVessel    = 308,  // 무한의 그릇     — 최대 마나 +20%
    N_LastDrop          = 309,  // 마지막 한 방울  — 모든 종족 소환 비용 −1 (끝·200pt)

    // ── 통솔 (오른쪽) ────────────────────────────────────────
    N_CallHorn          = 401,  // 부름의 나팔     — 배출 간격 −5%
    N_OpenGate          = 402,  // 성문 개방       — 배출 간격 −8%
    N_Insight           = 403,  // 안목            — 상점·시설 값 −6%
    // ⚠ N_ReinforceSeal(404) 은 트리에서 빠졌다 (2026-09-10)
    //   효과(마릿수 +1)는 소환수 최종 노드로 옮겼다. 번호는 **재사용하지 말 것** —
    //   옛 세이브에 이 번호의 레벨이 남아 있다.
    N_Sprint            = 405,  // 질주            — 몬스터 이동속도 +8%
    N_Attunement        = 406,  // 조율            — 시너지 중첩 보너스 +1%p
    N_ResonanceTome     = 407,  // 공명의 서       — 시너지 중첩 보너스 +2%p
    N_LegacyPrice       = 408,  // 전승의 대가     — 특성 선택지 +1
    N_EarlyBloom        = 409,  // 조기 성장       — 새 카드가 2레벨로
    N_PlunderHand       = 410,  // 약탈의 손       — 런 골드 +12%
    N_SoulUrn           = 411,  // 영혼의 항아리   — 환생 포인트 +12%
    N_TimeReins         = 412,  // 시간의 고삐     — 배속 1.5× 해금
    N_MomentMastery     = 413,  // 찰나의 지배     — 배속 2× 해금
    N_WarCamp           = 414,  // 전열 확장       — 카드 칸 +1 (최대 2레벨)
    N_OldPact           = 415,  // 오래된 계약     — 런 시작 시 무작위 특성 +1 (최대 2레벨)
}

// ── 노드가 주는 스탯 한 줄 ────────────────────────────────────
[Serializable]
public struct RelicNodeStat
{
    public StatType Stat;
    public float    PerLevel;

    /// <summary>
    /// true = 절대값 가산 (방어율·치명확률 %p, 병사 수 '명', 지휘력 포인트).
    /// false = 기저값 대비 비율 (%).
    ///
    /// ⚠ 스탯마다 따로 잡는다 — 구 RelicData 는 노드 전체에 하나였다
    ///   '치명피해 +50%(비율) + 치명확률 +10%p(절대)' 같은 노드를 만들 수 없었다.
    /// </summary>
    public bool Absolute;
}

// ── 노드 정의 ─────────────────────────────────────────────────
public sealed class RelicNodeDef
{
    public RelicNodeId Id;
    public RelicNodeId Parent;      // None = 뿌리
    public string      Name;
    public RelicBranch Branch;
    public int         Tier;        // 0=뿌리 … 5=말단. 비용이 여기서 나온다
    public int         X;           // 그리드 좌표 (+Y = 위)
    public int         Y;
    public int         MaxLevel;    // 1 = 단일 습득
    public int         CostBase;    // 레벨업 비용 = CostBase × (현재레벨+1)

    public RelicTarget      Target;
    public RelicNodeStat[]  Stats;          // EffectType == Stat
    public RelicSystemEffect System;        // EffectType == System (None 이면 스탯 노드)
    public float            SystemPerLevel;

    /// <summary>
    /// 화면에 띄울 이름. <b>표에서 <c>Relic.&lt;Id&gt;</c> 기호 키로 찾는다.</b>
    ///
    /// ⚠ <b><see cref="Name"/> 을 화면에 직접 쓰지 말 것</b> (2026-09-16) —
    ///   노드 이름은 `절약`·`인내`·`질주`·`안목` 처럼 **짧고 흔한 낱말**이다.
    ///   한국어 키로 표에 넣으면 <see cref="LocalizationManager.LocalizeText"/> 의
    ///   부분 치환이 **다른 문구 속 같은 글자까지 조용히 바꾼다**
    ///   (`인내` → "인내심", `질주` → "야성 질주"). 그래서 기호 키다.
    ///   같은 이유로 `HeroNameRule` 도 `Hero.*` 기호 키를 쓴다.
    ///
    /// ⚠ 반대로 <see cref="Name"/> 은 **한국어 원문 그대로 남긴다** — 개발자 진단이 쓴다
    ///   (`RelicTreeCatalog.Verify` 의 예외 · `RelicTreeAudit` 의 목록).
    ///   거기까지 기호 키로 바꾸면 에러가 `'Relic.N_Claw' 의 Target 이…` 로 읽힌다.
    ///
    /// ⚠ 표에 줄이 없으면 <c>Get</c> 이 <b>키를 그대로 돌려준다</b> — 화면에
    ///   `Relic.N_Claw` 가 뜨고 에러는 안 난다. 노드를 추가하면 표에도 한 줄 넣을 것.
    /// </summary>
    public string DisplayName => LocalizationManager.Instance.Get("Relic." + Id);

    /// <summary>레벨이 없는 한 방 노드 — 아이콘 테두리와 툴팁 표기가 다르다.</summary>
    public bool Special => MaxLevel == 1 && Branch != RelicBranch.Root;

    public bool IsSystem => System != RelicSystemEffect.None;

    // ── 비용 ──────────────────────────────────────────────────

    /// <summary>현재 레벨 → 다음 레벨 비용. 만렙이면 0.</summary>
    public int LevelUpCost(int currentLevel)
        => currentLevel >= MaxLevel ? 0 : CostBase * (currentLevel + 1);

    /// <summary>0 → 만렙까지 총 비용.</summary>
    public int TotalCost
    {
        get
        {
            int sum = 0;
            for (int lv = 0; lv < MaxLevel; lv++) sum += LevelUpCost(lv);
            return sum;
        }
    }

    // ── 설명 ──────────────────────────────────────────────────

    /// <summary>
    /// 툴팁 본문. level 0 이면 "1레벨을 찍었을 때" 값을 보여 준다 —
    /// 아직 없는 노드의 툴팁이 전부 +0% 로 나오면 살지 말지 판단할 수가 없다.
    /// </summary>
    public string GetDescription(int level)
    {
        int shown = Mathf.Max(1, level);

        if (IsSystem)
            return StatBonusColors.Wrap(StatSource.Relic, SystemLine(SystemPerLevel * shown));

        string body = string.Empty;
        for (int i = 0; i < Stats.Length; i++)
        {
            if (i > 0) body += "\n";
            body += StatLine(Stats[i], shown);
        }
        return $"[{LocalizationManager.Instance.Get(Target.ToString())}]\n" +
               StatBonusColors.Wrap(StatSource.Relic, body);
    }

    string StatLine(RelicNodeStat s, int level)
    {
        float total = s.PerLevel * level;
        string label = LocalizationManager.Instance.Get(s.Stat.ToString());
        string sign  = total < 0f ? "" : "+";

        if (!s.Absolute) return $"{label} {sign}{total * 100f:0.#}%";

        if (s.Stat == StatType.SoldierCount)  return F("{0} {1}{2}명", label, sign, Mathf.RoundToInt(total));
        if (s.Stat == StatType.CommandPower)  return $"{label} {sign}{Mathf.RoundToInt(total)}";
        return $"{label} {sign}{total * 100f:0.#}%p";
    }

    /// <summary>
    /// 설명 줄이 있는가. <b>RelicTreeCatalog.Verify 가 쓴다.</b>
    ///
    /// ⚠ GetDescription 으로는 못 잰다 — 그쪽은 빈 글자도 색 태그로 감싸
    ///   돌려주므로 언제나 "비어 있지 않은" 문자열이 된다.
    ///   실제로 그 때문에 빈칸 노드 29개가 눈에 안 띄었다 (2026-09-07).
    /// </summary>
    public bool HasEffectText => !IsSystem || !string.IsNullOrEmpty(SystemLine(SystemPerLevel));

    /// <summary>
    /// 표에서 문장을 찾아 숫자를 끼워 넣는다 — 원본 표가 쓰는 <c>{0}</c> 방식.
    ///
    /// ⚠ <b>수치가 든 설명에 보간 문자열($"…{값}…")을 쓰지 말 것</b> (2026-09-16).
    ///   보간은 실행 시점에 이미 숫자로 바뀌어 있어서 번역표의 키(코드에 적힌 그대로의
    ///   문자열)와 **영원히 일치하지 않는다** — 표에는 줄이 있는데 화면에는 한국어로 남고,
    ///   에러도 경고도 안 난다. 그렇게 죽어 있던 119줄을 한 번 걷어냈다.
    ///
    /// ⚠ 한글이 없는 줄(`{label} {sign}{v}%`)은 번역할 것이 없으니 그대로 둔다 —
    ///   라벨은 이미 Get 으로 번역되어 들어온다.
    /// </summary>
    static string F(string key, params object[] args)
        => LocalizationManager.Instance.Format(key, args);

    string SystemLine(float v) => System switch
    {
        RelicSystemEffect.AbilityRefreshCount   => F("어빌리티 새로고침 +{0}회", Mathf.RoundToInt(v)),
        RelicSystemEffect.AbilityChoiceCount    => F("어빌리티 선택지 +{0}개", Mathf.RoundToInt(v)),
        RelicSystemEffect.AbilityAdvancedChance => F("고급 이상 어빌리티 확률 +{0:0.#}%p", v * 100f),
        RelicSystemEffect.GoldGainBonus         => F("골드 획득량 +{0:0.#}%", v * 100f),
        RelicSystemEffect.EnemyMaxHpReduction   => F("적 최대 체력 -{0:0.#}%", v * 100f),
        RelicSystemEffect.EnemyAttackReduction  => F("적 공격력 -{0:0.#}%", v * 100f),
        RelicSystemEffect.GeneralSlotBonus      => F("장수 배치 슬롯 +{0}칸", Mathf.RoundToInt(v)),
        // 배속 값의 정본은 TopBarUI.SpeedSteps 다. 여기에 숫자를 박으면 둘이 갈라진다.
        // ⚠ v 는 '이 노드가 주는 양'(둘 다 1)이라 노드만으로는 몇 번째 단계인지 모른다.
        //   두 번째 해금 노드(찰나의 지배)만 짚어 준다 — 아니면 둘 다 같은 배속을 말한다.
        RelicSystemEffect.BattleSpeedUnlock     =>
            F("전투 배속 {0:0.##}× 해금",
              TopBarUI.SpeedAtStep(Id == RelicNodeId.N_MomentMastery ? 2 : 1)),

        // ── 이 게임의 효과 (2026-09-07) ───────────────────────
        //  ⚠ 여기를 빠뜨리면 노드가 **빈칸으로 뜬다.** 에러가 안 난다 —
        //    트리를 갈아 끼웠을 때 실제로 46개 중 29개가 빈칸이었다.
        //    새 RelicSystemEffect 를 만들면 읽는 쪽(GetSystemValue)과
        //    이 줄을 **함께** 만들 것.
        //  ⚠ 단위는 RelicEnums.cs 의 줄 주석이 정본이다 —
        //    비율(%)·절대값·%p 가 효과마다 다르다. 눈대중으로 적지 말 것.

        // 소환수
        // ⚠ 실제 범위를 적는다 — 한때 "종족 패시브 효과" 라 적었는데 재생 둘에만 걸려 있었다 (2026-09-16)
        RelicSystemEffect.SpeciesPassivePower   => F("재생·회복량 +{0:0.#}%", v * 100f),
        RelicSystemEffect.DerivedScaleBonus     => F("분열·재조립체 스탯 배율 +{0:0.##}", v),
        RelicSystemEffect.DeathTriggerPower     => F("죽을 때 터지는 패시브 +{0:0.#}%", v * 100f),
        RelicSystemEffect.GearBoxBonus          => F("여정 종료 장비 상자 +{0}개", Mathf.RoundToInt(v)),
        RelicSystemEffect.GearStatBonus         => F("몬스터 장비 스탯 +{0:0.#}%", v * 100f),

        // 마왕성
        RelicSystemEffect.CoreHpBonus           => F("마왕성 체력 +{0}", Mathf.RoundToInt(v)),
        RelicSystemEffect.SummonerStrikeBonus   => F("소환사 평타 피해 +{0:0.#}%p (적 최대 체력 비례)", v * 100f),
        RelicSystemEffect.CampHealBonus         => F("야영지 회복량 +{0}", Mathf.RoundToInt(v)),
        RelicSystemEffect.EnemyMoveReduction    => F("용사 이동속도 -{0:0.#}%", v * 100f),

        // 마나
        RelicSystemEffect.ManaCapacityBonus     => F("최대 마나 +{0:0.#}%", v * 100f),
        RelicSystemEffect.SummonCostCut         => F("소환 비용 -{0:0.##}", v),
        RelicSystemEffect.ManaRegenBonus        => F("스테이지 마나 회복 +{0:0.#}%", v * 100f),
        RelicSystemEffect.OverloadRelief        => F("과부하 증가폭 -{0:0.#}%p", v * 100f),

        // 통솔
        RelicSystemEffect.DrainSpeedBonus       => F("라인 배출 간격 -{0:0.#}%", v * 100f),
        RelicSystemEffect.SummonCountBonus      => F("카드당 소환 마릿수 +{0}", Mathf.RoundToInt(v)),
        RelicSystemEffect.CardChoiceCount       => F("카드 선택지 +{0}장", Mathf.RoundToInt(v)),
        RelicSystemEffect.PerkChoiceCount       => F("특성 선택지 +{0}개", Mathf.RoundToInt(v)),
        RelicSystemEffect.NewCardLevel          => F("새로 받는 카드가 {0}레벨로 들어온다", Mathf.RoundToInt(v) + 1),
        RelicSystemEffect.SynergyStepCut        => F("시너지 동 문턱 -{0}", Mathf.RoundToInt(v)),
        RelicSystemEffect.SynergyStackBonus     => F("시너지 중첩 보너스 +{0:0.#}%p", v * 100f),
        RelicSystemEffect.ReincarnPointBonus    => F("환생 포인트 +{0:0.#}%", v * 100f),
        RelicSystemEffect.ShopPriceCut          => F("상점·시설 값 -{0:0.#}%", v * 100f),
        RelicSystemEffect.DotDamageBonus        => F("중독·화상·역병 피해 +{0:0.#}%", v * 100f),
        RelicSystemEffect.CoreRegenPerStage     => F("스테이지를 넘길 때마다 마왕성 체력 +{0}", Mathf.RoundToInt(v)),
        RelicSystemEffect.DeckSlotBonus         => F("카드 칸 +{0} (최대 {1}칸)",
                                                     Mathf.RoundToInt(v), RunPerkRule.MaxDeckSlots),
        RelicSystemEffect.StartingPerkCount     => F("런을 시작할 때 무작위 특성 +{0}개", Mathf.RoundToInt(v)),

        _                                       => string.Empty,
    };
}

// ── 트리 전역 규칙 ────────────────────────────────────────────
public static class RelicTreeRules
{
    /// <summary>
    /// 지휘력이 음수로 내려가도 병사 스탯은 이 비율 아래로 떨어지지 않는다.
    ///
    /// ⚠ 하한이 없으면 역분기가 '병사 삭제 버튼' 이 된다
    ///   지휘력 1포인트당 병사 스탯 1% 라서 일기당천(-15)까지 타면 지휘력이
    ///   0 밑으로 한참 내려간다. 하한 없이 곱하면 병사가 종잇장이 되어
    ///   "장수 하나로 간다" 가 아니라 "병사가 없다" 가 되어 버린다.
    ///   최소 20% 는 남겨야 병사가 몸빵 역할이라도 한다.
    ///
    ///   적용 지점은 SoldierRuntimeBridge.StatRatio 하나다 — 로비 표시와 전투가
    ///   같은 값을 쓰게 하려면 거기서 한 번만 걸어야 한다.
    /// </summary>
    public const float MinSoldierStatRatio = 0.20f;

    /// <summary>정공 노드로 올릴 수 있는 병사 수 총합.</summary>
    public const int MaxSoldierGain = 30;

    /// <summary>역분기로 깎을 수 있는 병사 수 총합 (고독한 장수+일기당천+무쌍).</summary>
    public const int MaxSoldierCut = 50;
}

/// <summary>
/// 유물 스탯 노드가 무엇에 걸리는가.
/// ⚠ 스탯 노드는 반드시 Unit_Monster 다 — 장수·병사는 적(용사)이라 걸리면 적이 세진다.
///   번호는 원작 AbilityTarget 과 같다 (노드 표가 이 값으로 적혀 있었다).
/// </summary>
public enum RelicTarget
{
    All          = 0,   // 시스템 노드 — 스탯이 없다
    Unit_Monster = 9,   // 플레이어의 소환 몬스터
}
