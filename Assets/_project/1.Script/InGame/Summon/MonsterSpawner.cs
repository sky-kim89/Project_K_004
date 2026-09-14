using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MonsterSpawner.cs
//  몬스터를 필드에 세우는 **단 하나의 지점**.
//
//  ■ 왜 하나로 모았나
//    몬스터가 나오는 길이 네 갈래로 늘었다.
//      ① 카드 소환   — SummonController (마나를 내고 환수 몫을 갖는다)
//      ② 개성 부활   — SummonerPerkRuntime (공짜, 환수 없음)
//      ③ 종족 증식   — SpeciesPassiveRuntime (분열·재조립. 세대가 는다)
//      ④ 스킬 소환   — 앞으로 늘어난다
//    스폰 코드를 각자 복사하면 "품질은 반영됐는데 종족 패시브는 안 붙은 몬스터",
//    "환수 몫이 남아 공짜 개체가 마나를 돌려주는" 류의 어긋남이 반드시 난다.
//
//  ■ 환수 규칙 (사용자 확정, 2026-08-27)
//    **카드로 소환한 개체만 환수한다.** 부활·분열·스킬로 공짜로 나온 개체는
//    마나를 돌려주지 않는다. 공짜 개체가 환수하면 "죽을수록 이득" 이 되어
//    마나가 사실상 재생 자원이 되고, "런 시작에 1회 부여" 라는 축이 무너진다.
//
//    ⚠ Carry(0) 을 반드시 부른다 — 안 부르는 것과 다르다
//      Monster.prefab 은 풀에서 재사용되므로 지난 판의 몫이 남아 있다.
//
//  ■ 세대(Generation) — 무한 증식을 막는 유일한 장치
//      0세대 = 카드로 직접 소환된 개체        → 분열·재조립 가능
//      1세대 = 그 개체가 분열해 나온 것        → 더 이상 증식하지 않는다
//    한계값은 SpeciesPassiveRule.MaxReproduceGeneration 이 소유한다.
//    ⚠ 새 소환 경로를 추가할 때 세대를 빠뜨리면 그 경로로 무한 증식이 뚫린다.
// ============================================================

/// <summary>
/// 죽은 개체가 <b>다음 개체에게 물려주는 것</b> — 카드 레벨·융합 개성·외형 시드.
///
/// ■ 왜 필요한가 (사용자 확정, 2026-09-07)
///   부활·분열로 나온 개체는 한때 늘 "Lv1, 융합 없음, 외형 새로 굴림" 이었다.
///   그래서 Lv5 스켈레톤이 부활하면 레벨 보너스 +48% 와 열린 고정 효과 4칸이
///   통째로 빠진 채 일어났고, 인간형은 <b>얼굴과 무기까지 다른 개체</b>가 됐다.
///   "같은 몬스터가 다시 선다" 는 약속과 정면으로 어긋난다.
///
/// ■ 무엇이 안 넘어가나
///   라인(lane)은 넘기지 않는다 — 파생 개체는 대기열로 돌아가지 않으므로
///   라인 특성 보너스까지 주면 공짜 물량이 이중으로 이득을 본다
///   (사용자 확정 — 그대로 둔다).
///
/// ⚠ 스킬·개성이 <b>새로</b> 부른 몬스터는 None 이다
///   시그니처 소환·비석·강령술사의 스켈레톤은 죽은 개체가 되살아난 것이
///   아니라 새로 태어난 것이다. 물려줄 카드도 몸도 없다.
/// </summary>
public readonly struct MonsterOrigin
{
    /// <summary>물려받을 카드 레벨. 1 이면 없는 것과 같다.</summary>
    public readonly int CardLevel;

    /// <summary>융합으로 배운 개성이 여기 들어 있다 (SummonDeckSlot.CollectLearned).</summary>
    public readonly SummonDeckSlot Card;

    /// <summary>
    /// 외형 시드. 인간형은 이 문자열 하나가 얼굴·머리·무기를 정한다
    /// (EnemyAppearanceRoller.Roll). 비우면 새로 굴린다.
    /// </summary>
    public readonly string SeedName;

    public MonsterOrigin(int cardLevel, in SummonDeckSlot card, string seedName)
    {
        CardLevel = Mathf.Max(1, cardLevel);
        Card      = card;
        SeedName  = seedName;
    }

    /// <summary>물려받을 것이 없는 개체 — 스킬·개성이 새로 부른 몬스터.</summary>
    public static readonly MonsterOrigin None =
        new(1, SummonDeckSlot.Empty, null);
}

public static class MonsterSpawner
{
    /// <summary>패시브 목록을 매번 새로 만들지 않기 위한 재사용 버퍼 (메인 스레드 전용).</summary>
    static readonly List<PassiveSkillType> _passiveBuffer = new(8);

    /// <summary>종족 패시브 버퍼. 위와 같은 이유로 재사용한다.</summary>
    static readonly List<SpeciesPassive> _speciesBuffer = new(8);

    // ── 소환 경로 ────────────────────────────────────────────

    /// <summary>
    /// 카드로 소환한다. 마나 차감은 부르는 쪽(SummonController)이 이미 했다.
    /// </summary>
    /// <param name="card">카드 칸 — 레벨·합성 개성이 여기서 나온다</param>
    /// <param name="lane">
    /// 이 개체가 선 라인. 살아남으면 판이 끝날 때 이 라인 **대기열**로 돌아가
    /// 다음 스테이지에 다시 나온다 (MonsterLineReturner).
    ///
    /// ⚠ 예전에는 여기에 '환수할 마나 몫' 을 심었다
    ///   살아남은 개체가 마나로 녹는 구조였는데, 그러면 마나 손실이 곧
    ///   사망률이라 "더 많이 소환하기" 가 언제나 최적해가 됐다.
    ///   지금은 마나가 아니라 전력으로 남는다.
    /// </param>
    public static GameObject SpawnFromCard(MonsterSpeciesData species, SummonerData summoner,
                                           in SummonDeckSlot card, Vector3 at, int lane,
                                           float drainMult = 1f)
    {
        GameObject go = Spawn(species, summoner, at, card.Level, card,
                              powerScale: 1f, generation: 0, lane: lane, drainMult: drainMult);
        if (go == null) return null;

        // 살아남으면 이 라인 대기열로 돌아가 다음 판에 다시 나온다.
        //   ⚠ Spawn 안에서 이미 Setup(returnable: false) 이 돌았다 — 여기서
        //     자격만 올린다. 두 번 불러도 시너지 집계는 어긋나지 않는다
        //     (NoteAlive 는 Setup 안에서 _counted 로 짝을 맞춘다).
        go.GetComponent<MonsterLineReturner>().Setup(lane, species, returnable: true);

        // 이 개체가 죽으면 분열·부활이 여기 담긴 것을 그대로 물려받는다.
        Arm(go, summoner, species, isCardSummoned: true, generation: 0,
            cardLevel: card.Level, card: card);

        SummonerPerkRuntime.OnMonsterSpawned(summoner, species, go, lane);

        return go;
    }

    /// <summary>
    /// 공짜로 세운다 — 소환사 개성의 부활, 시그니처 스킬 소환 등.
    /// <b>마나를 환수하지 않는다.</b> 카드가 없으므로 레벨 1, 합성 개성 없음으로 나온다.
    /// </summary>
    /// <param name="generation">
    /// 이 개체의 세대. <b>부르는 쪽이 정한다</b> — 기본값 1 이 안전한 쪽이다.
    ///
    /// ⚠ 1 (기본) — <b>죽음에서 다시 나온 개체</b>
    ///   부활·비석처럼 "이미 한 번 죽은 것" 이 되살아난 경우다. 0 으로 내면
    ///   부활한 스켈레톤이 또 재조립을 굴려 전투가 끝나지 않는다.
    ///
    /// ⚠ 0 — <b>플레이어가 자원을 내고 새로 부른 개체</b>
    ///   시그니처 스킬이 그렇다. 마나 대신 <b>횟수</b>를 냈을 뿐 카드 소환과
    ///   같은 자리에 있는 물건이라, 종족 패시브도 카드로 낸 것과 같아야 한다.
    ///   1 로 두면 슬라임을 불러 놓고 분열이 안 터진다 — 그 종족을 그 종족답게
    ///   만드는 것 하나가 통째로 죽는다 (사용자 지적, 2026-09-07).
    ///   증식이 새지 않는 이유는 그 개체가 낳는 것이 1세대이기 때문이다.
    ///   횟수 제한이 이미 물량의 상한이라 카드보다 더 낼 수도 없다.
    /// </param>
    public static GameObject SpawnFree(MonsterSpeciesData species, SummonerData summoner,
                                       Vector3 at, int generation = 1)
        // ⚠ 외형 시드는 물려받지 않는다 — 죽은 개체가 일어난 것이 아니라 새로 부른 것이다.
        //   대신 **덱에 그 종족이 있으면 그 카드로 낸다** (아래 DeckCardFor).
        => SpawnDerived(species, summoner, at, powerScale: 1f, generation: generation,
                        origin: DeckCardFor(species));

    /// <summary>
    /// 덱에 그 종족의 카드가 있으면 그것으로, 없으면 빈 카드(Lv1)로 낸다.
    ///
    /// ■ ⚠ 왜 필요한가 (사용자 지적, 2026-09-09)
    ///   시그니처 스킬로 부른 슬라임이 <b>한 방에 죽어 나갔다.</b> 같은 슬라임인데
    ///   대기열에서 나온 것은 멀쩡했다 — 이쪽만 <b>언제나 Lv1</b> 이었기 때문이다.
    ///   Lv4 카드와 견주면 기본 공/체 +36%(CardLevelRule.StatBonusPerLevel) ·
    ///   레벨 표의 체력 +25%p·방어율 +6%p · 그리고 <b>Lv4 에 열리는 패시브</b>
    ///   (슬라임이면 피격 시 방어율 증가)가 통째로 빠진 개체가 적진 한복판에 선다.
    ///   그 창은 "보통 몬스터와 똑같다" 고 약속하고 있었다(ActiveSummonSignature).
    ///
    /// ■ 그래도 공짜 물량은 늘지 않는다
    ///   세지 않고(MarkSummoned 없음) 돌아가지 않는다(returnable = false).
    ///   횟수 제한이 상한이고, 여기서 바뀌는 것은 <b>세기</b>뿐이다 —
    ///   플레이어가 키운 카드가 스킬에도 반영된다는 뜻이다.
    ///
    /// ⚠ 외형 시드는 넘기지 않는다 — 개체마다 새로 굴려야 같은 얼굴이 줄 서지 않는다.
    /// </summary>
    static MonsterOrigin DeckCardFor(MonsterSpeciesData species)
    {
        var deck = UserDataManager.Instance?.Get<SummonDeckData>();
        if (deck == null) return MonsterOrigin.None;

        int index = deck.IndexOf(species.Id);
        if (index < 0) return MonsterOrigin.None;

        SummonDeckSlot card = deck.GetSlot(index);
        return new MonsterOrigin(card.Level, card, seedName: null);
    }

    /// <summary>
    /// 종족 패시브가 만들어 낸 개체 — 분열체·재조립체.
    ///
    /// 원본보다 약하게 나온다(powerScale). 같은 세기로 나오면 죽는 것이 이득이 되어
    /// "잃지 않으려 애쓴다" 는 긴장이 사라진다.
    /// </summary>
    /// <param name="powerScale">HP·공격력·크기 배율</param>
    /// <param name="generation">이 개체의 세대. 부르는 쪽이 원본 세대 + 1 을 넘긴다.</param>
    /// <param name="origin">
    /// 원본이 물려주는 것 — 카드 레벨·융합 개성·외형 시드 (MonsterOrigin).
    ///
    /// ⚠ 카드 정보를 물려받는다 (사용자 확정, 2026-09-07)
    ///   예전에는 늘 Lv1·융합 없음으로 냈다. 그러면 Lv5 카드가 분열·부활할 때
    ///   레벨 보너스 +48% 와 열린 고정 효과가 통째로 빠져, "키운 카드일수록
    ///   증식이 손해" 라는 거꾸로 된 규칙이 생긴다.
    ///   불어나지 않는 이유는 여기가 아니라 <b>세대</b>가 막기 때문이다 —
    ///   파생체는 1세대라 다시 증식하지 못한다.
    /// </param>
    public static GameObject SpawnDerived(MonsterSpeciesData species, SummonerData summoner,
                                          Vector3 at, float powerScale, int generation,
                                          in MonsterOrigin origin,
                                          bool scaleHpOnly = false)
    {
        GameObject go = Spawn(species, summoner, at, origin.CardLevel, origin.Card,
                              powerScale: powerScale, generation: generation,
                              scaleHpOnly: scaleHpOnly,
                              seedName: origin.SeedName);
        if (go == null) return null;

        // ⚠ 파생 개체는 대기열로 돌아가지 않는다 (사용자 확정, 2026-08-28)
        //   분열체·부활체는 마나를 내지 않고 나온 것이다. 그것이 줄에 쌓이면
        //   판을 거듭할수록 공짜 물량이 불어나 소환 경제가 무너진다.
        //   판이 끝나면 그냥 사라진다 — MonsterLineReturner 의 기본값이 그렇다.

        // 물려받은 것을 그대로 다시 물려준다 — 세대가 한 번 더 열리는 경우
        // (언데드 금)에도 레벨과 몸이 이어져야 한다.
        Arm(go, summoner, species, isCardSummoned: false, generation: generation,
            cardLevel: origin.CardLevel, card: origin.Card);

        // 땅에서 일어나는 연출 — 허공에서 튀어나온 게 아니라 '갈라져 나온' 것으로 읽히게.
        if (go.TryGetComponent<UnitAnimationSync>(out var anim))
            anim.PlayRise();

        return go;
    }

    // ── 내부 ─────────────────────────────────────────────────

    /// <param name="lane">
    /// 라인 특성(소수정예·군세·주공·측면)이 읽는다. −1 = 라인 없는 개체.
    /// ⚠ 파생체·공짜 소환은 −1 로 둔다 — 라인에 서긴 하지만 대기열로 돌아가지
    ///   않는 개체라, 라인 보너스까지 주면 공짜 물량이 이중으로 이득을 본다.
    /// </param>
    static GameObject Spawn(MonsterSpeciesData species, SummonerData summoner, Vector3 at,
                            int cardLevel, in SummonDeckSlot card,
                            float powerScale, int generation, bool scaleHpOnly = false,
                            int lane = -1, string seedName = null, float drainMult = 1f)
    {
        if (!string.IsNullOrEmpty(species.SummonCircleEffectKey))
            SkillEffectHelper.Spawn(species.SummonCircleEffectKey, at, 1f);

        GameObject go = PoolController.Instance.Spawn(
            PoolType.Unit, species.PoolKey, at, Quaternion.identity);

        if (go == null)
        {
            Debug.LogWarning($"[MonsterSpawner] 풀 스폰 실패: '{species.PoolKey}'");
            return null;
        }

        if (!go.TryGetComponent<MonsterRuntimeBridge>(out var bridge))
        {
            Debug.LogError($"[MonsterSpawner] '{species.PoolKey}' 에 MonsterRuntimeBridge 가 없습니다.");
            return null;
        }

        // ⚠ 순서 고정 — Initialize 안에서 Team 이 박힌다
        bridge.SetTeam(Faction.Monster);

        UnitGrade grade = SummonerPerkRuntime.GradeFor(summoner, species, CodexGrade(species));
        // ⚠ 진화 계승분(card.InheritBonus)을 함께 넘긴다 — 분열체도 원본의 카드를
        //   물려받으므로(MonsterOrigin) 같은 값이 그대로 따라간다.
        UnitStat  stat  = MonsterStatComposer.Compose(species, summoner, grade, cardLevel, lane,
                                                      card.InheritBonus, drainMult);

        if (!Mathf.Approximately(powerScale, 1f)) ScalePower(stat, powerScale, scaleHpOnly);

        // ── 겉모습 배율은 스탯 배율과 갈린다 (사용자 확정, 2026-09-07) ──
        //   ⚠ 부활은 작아지지 않는다
        //     분열·재조립이 작아지는 것은 "몸이 쪼개졌다" 는 그림이 있어서다.
        //     부활은 **같은 몸이 체력만 적게 일어나는 것**이라 작아질 이유가 없다.
        //     한때 powerScale 을 그대로 크기에 곱해, 동 등급 부활 스켈레톤이
        //     0.30배 크기로 서면서 **원래 공격력을 때렸다**. 겉모습과 세기가
        //     정반대를 말하는 상태였고, localScale 에서 나오는 히트박스·분리
        //     반경(UnitSizeComponent.Radius)까지 30% 가 됐다.
        //   기준은 scaleHpOnly 하나다 — 스탯을 통째로 깎은 개체만 작아진다.
        //
        //   ⚠ 종족 덩치(BodyScale)를 여기서 곱한다 (사용자 요청, 2026-09-09)
        //     고블린·오크·좀비는 셋 다 초록 계열 인간형이라 전장에서 한 덩어리로
        //     보였다. 색으로는 못 가르니 덩치로 가른다 —
        //     고블린 0.85 · 좀비 1 · 오크 1.2 · 트롤 1.45.
        //     ⚠ 히트박스·분리 반경도 함께 커진다 (UnitSizeComponent.Radius).
        //       덩치가 곧 자리를 먹는다는 뜻이라, 값을 키울 때 라인이 얼마나
        //       비는지 함께 볼 것.
        //     ⚠ 분열체에도 곱해진다 — 같은 종족이니 그게 맞다. 반쪽(0.5)이
        //       종족 덩치 위에서 반쪽이 된다.
        float visualScale = (scaleHpOnly ? 1f : powerScale) * species.BodyScale;

        _passiveBuffer.Clear();
        species.CollectPassives(cardLevel, _passiveBuffer);

        // ── 이름 = 외형 시드 ─────────────────────────────────
        //   인간형은 이 문자열 하나가 얼굴·머리·눈·무기를 정한다
        //   (EnemyAppearanceRoller.Roll — 이름이 같으면 캐시된 같은 외형이 나온다).
        //   개체마다 달라야 같은 종족도 조금씩 다르게 보인다.
        //
        //   ⚠ 물려받은 이름이 있으면 새로 굴리지 않는다 (사용자 확정, 2026-09-07)
        //     부활·분열은 **그 몬스터가 다시 서는 것**이다. 이름을 새로 굴리면
        //     스켈레톤이 얼굴도 무기도 다른 개체로 일어나 "같은 몸" 으로 읽히지
        //     않는다 — 크기를 되돌린 것과 같은 이유다.
        //     (비인간형은 통짜 라이브러리를 꽂으므로 원래 영향이 없다)
        //
        //   ⚠ 이름이 겹쳐도 안전하다 — 풀 반납 키로 쓰이지 않는다
        //     UnitPoolLinkComponent.PoolKey 에 들어가긴 하지만 실제 반납은
        //     LinkedObject 로 한다 (UnitDeathDespawnSystem).
        string unitName = string.IsNullOrEmpty(seedName)
            ? $"{species.Id}_{Random.Range(0, 9999)}"
            : seedName;

        bridge.InitializeWithStat(unitName, stat, species, _passiveBuffer, visualScale);

        // ⚠ 시너지의 '지금 존재하는 종족' 집계는 **모든 스폰**이 지나야 한다
        //   (2026-09-09) 분열체·스킬 소환도 그 종족이 전장에 있다는 뜻이다.
        //   자격(returnable)은 카드 소환이 뒤에서 올린다 — 여기서는 세기만 한다.
        go.GetComponent<MonsterLineReturner>().Setup(lane, species, returnable: false);

        // ⚠ 종족 패시브는 카드 패시브보다 **뒤에** 붙는다
        //   패시브 슬롯이 3칸뿐이라 순서가 곧 우선순위다. 카드를 키운 쪽이
        //   먼저 자리를 갖는 것이 "카드를 겹친 대가" 로 읽힌다.
        // ⚠ 선천 · 융합 · 장비를 PassiveResolver 가 한 목록으로 만든다 (2026-09-10)
        //   겹친 것은 하나로, 둘이 모인 것은 각성판으로. 사망(MonsterDeathWatcher)과
        //   화면(MonsterDetailPopup)도 같은 함수를 부른다 — 셋이 갈리면 "도감엔
        //   있는데 전투엔 없는" 패시브가 생긴다.
        PassiveResolver.CollectFor(species, card, _speciesBuffer);

        SpeciesPassiveRuntime.ApplyOnSpawn(go, species, _speciesBuffer);

        // ⚠ 종족 패시브 **뒤**다 — 시너지는 이미 붙은 컴포넌트에 얹는다
        //   (재생은 더하고, 반사는 합치고, 중독은 센 쪽을 남긴다)
        MonsterSynergyRuntime.ApplyOnSpawn(go, species.Tags,
                                           MonsterSynergyRule.KeyOf(species.Id));
        MonsterSynergyRuntime.PrimeSkill(go, species.Tags);

        // 이 개체가 낸 피해를 어느 카드에 달 것인지 심는다 (전투 통계).
        RegisterForStats(go, species);

        // ⚠ 몬스터에게 "전투 시작" 은 자기가 선 순간이다
        //   일괄 발동(BattleManager.FireBattleStartTriggers)은 BeginStage 시점에
        //   필드에 있던 유닛만 훑는다. 전투 도중에 나오는 몬스터는 거기 없어서,
        //   이 호출이 없으면 OnBattleStart 계열 패시브가 영영 발동하지 않는다.
        FireSpawnPassives(go);

        // ── 특성 '파쇄' — 착탄마다 대상 최대 체력 비례 추가 피해 ──
        //   ⚠ 상한(공격력 ×3)은 여기서 구워 넣는다. 피해 계산이 Burst 잡이라
        //     그 안에서 공격자의 스탯을 다시 조회할 수 없다.
        ApplyRend(go, stat);

        // ── 소환사 '패기' — 이 개체의 평타 넉백 배율 ──
        //   ⚠ 스폰 때 구워 둔다. 피해 계산이 Burst 잡이라 그 안에서 소환사를
        //     조회할 수 없다 (ApplyRend 와 같은 이유).
        ApplyKnockbackPower(go, summoner, species);

        BattleManager.Instance?.OnUnitSpawned(Faction.Monster);

        return go;
    }

    /// <summary>특성 '파쇄' 를 엔티티에 굽는다. 특성이 없으면 아무것도 하지 않는다.</summary>
    static void ApplyRend(GameObject go, UnitStat stat)
    {
        if (!RunPerkRule.Has(RunPerk.Rend)) return;

        // 엔티티를 찾는 방법은 MonsterSynergyRuntime.TryEntity 와 같다 — EntityLink 경유.
        if (!go.TryGetComponent<EntityLink>(out var link)) return;
        if (link.Entity == Unity.Entities.Entity.Null) return;

        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        Unity.Entities.EntityManager em = world.EntityManager;
        if (!em.Exists(link.Entity)) return;

        Unity.Entities.Entity e = link.Entity;

        var comp = new BattleGame.Units.RendComponent
        {
            MaxHpRatio = RunPerkRule.RendMaxHpRatio,
            Cap        = stat.Get(StatType.Attack) * RunPerkRule.RendCapAttackMult,
        };

        if (em.HasComponent<BattleGame.Units.RendComponent>(e)) em.SetComponentData(e, comp);
        else                                                    em.AddComponentData(e, comp);
    }

    /// <summary>
    /// 소환사 패기가 정하는 평타 넉백 배율을 엔티티에 굽는다 (SummonerVigorRule).
    ///
    /// ⚠ 기준값(1)이어도 붙인다 — 풀에서 재사용되기 때문이다
    ///   안 붙이면 지난 판에 패기 8 소환사가 남긴 컴포넌트를 그대로 물려받는다.
    ///
    /// ⚠ 몬스터 장비의 '넉백' 옵션도 여기서 곱한다 (MonsterGearRule.KnockbackBonusFor)
    ///   같은 컴포넌트 하나에 굽는다 — 둘을 따로 두면 피해 계산이 두 곳을 봐야 한다.
    /// </summary>
    static void ApplyKnockbackPower(GameObject go, SummonerData summoner, MonsterSpeciesData species)
    {
        if (!go.TryGetComponent<EntityLink>(out var link)) return;
        if (link.Entity == Unity.Entities.Entity.Null)     return;

        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        Unity.Entities.EntityManager em = world.EntityManager;
        if (!em.Exists(link.Entity)) return;

        Unity.Entities.Entity e = link.Entity;

        var comp = new BattleGame.Units.KnockbackPowerComponent
        {
            Mult = SummonerVigorRule.KnockbackMultFor(summoner)
                 * (1f + MonsterGearRule.KnockbackBonusFor(species.Id)),
        };

        if (em.HasComponent<BattleGame.Units.KnockbackPowerComponent>(e))
            em.SetComponentData(e, comp);
        else
            em.AddComponentData(e, comp);
    }

    /// <summary>
    /// 분열체·재조립체를 약하게 만든다.
    ///
    /// ⚠ HP·공격력만 줄인다
    ///   이동속도·사거리·연사까지 줄이면 분열체가 전장에 아무 영향도 못 주는
    ///   장식이 된다. "작지만 여전히 슬라임" 이어야 분열이 이득으로 읽힌다.
    /// </summary>
    /// <summary>
    /// 파생 개체의 힘을 깎는다.
    ///
    /// ⚠ hpOnly 는 부활 전용이다 (2026-09-03)
    ///   분열체는 작아진 슬라임이라 공격력도 같이 줄어야 그림이 맞다.
    ///   반면 언데드 부활은 "체력 X% 로 다시 일어난다" 는 약속이라, 공격력까지
    ///   깎으면 시너지의 전투력 환산(1 + 확률×체력비)이 실제보다 후하게 잡힌다.
    /// </summary>
    static void ScalePower(UnitStat stat, float scale, bool hpOnly)
    {
        stat.Set(StatType.MaxHp, stat.Get(StatType.MaxHp) * scale);

        if (!hpOnly)
            stat.Set(StatType.Attack, stat.Get(StatType.Attack) * scale);
    }

    /// <summary>
    /// 전투 통계에 "이 엔티티 = 이 카드" 를 등록한다.
    ///
    /// ⚠ 분열체·부활체도 같은 카드로 단다
    ///   슬라임이 분열해 낸 피해는 슬라임 카드의 값어치다. 따로 세면
    ///   "분열이 얼마나 벌어 주는가" 를 볼 수 없다.
    /// </summary>
    static void RegisterForStats(GameObject go, MonsterSpeciesData species)
    {
        if (!go.TryGetComponent<EntityLink>(out var link)) return;
        if (link.Entity == Unity.Entities.Entity.Null)     return;

        CardStatsTracker.Instance.RegisterOwner(link.Entity, species.Id, SummonKind.Monster);
    }

    /// <summary>스폰 직후 OnBattleStart 계열 패시브를 발동한다.</summary>
    static void FireSpawnPassives(GameObject go)
    {
        if (!go.TryGetComponent<EntityLink>(out var link)) return;
        if (link.Entity == Unity.Entities.Entity.Null)     return;

        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        BattleManager.FireBattleStartFor(world.EntityManager, link.Entity);
    }

    /// <summary>도감에 기록된 그 종족의 영구 품질. 기록이 없으면 Normal.</summary>
    static UnitGrade CodexGrade(MonsterSpeciesData species)
    {
        var codex = UserDataManager.Instance.Get<MonsterCodexData>();
        return codex.IsUnlocked(species.Id) ? codex.GetGrade(species.Id) : UnitGrade.Normal;
    }

    /// <summary>
    /// 사망 감시자를 세운다. 프리팹에 없으면 붙인다 —
    /// UnitRuntimeBridge 가 UnitBuffAuraView 를 다루는 방식과 같다
    /// (프리팹을 고치지 않고 모든 유닛이 지나는 길목에서 한 번만 붙인다).
    /// </summary>
    static void Arm(GameObject go, SummonerData summoner, MonsterSpeciesData species,
                    bool isCardSummoned, int generation,
                    int cardLevel, in SummonDeckSlot card)
    {
        MonsterDeathWatcher watcher = go.TryGetComponent<MonsterDeathWatcher>(out var w)
            ? w
            : go.AddComponent<MonsterDeathWatcher>();

        // ⚠ 이름은 스폰이 끝난 뒤에 읽는다 — InitializeWithStat 이 박아 넣는다
        //   물려받은 시드든 새로 굴린 것이든 여기서는 '지금 이 몸' 하나로 보인다.
        string seedName = go.GetComponent<MonsterRuntimeBridge>().UnitName;

        watcher.Arm(summoner, species, isCardSummoned, generation,
                    new MonsterOrigin(cardLevel, card, seedName));
    }
}
