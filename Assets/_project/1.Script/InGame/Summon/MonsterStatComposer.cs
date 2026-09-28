using UnityEngine;

// ============================================================
//  MonsterStatComposer.cs
//  소환 몬스터의 최종 스탯을 만드는 단 하나의 지점.
//
//  ■ 합성 공식
//      최종 = (종족 기본 스탯 + 소환력 보너스 + 소환사 힘·체력 보너스) × 품질 배율
//
//  ■ ⚠ 소환력은 지휘력이 아니다 — 구조가 다르다
//      원작 지휘력 : 병사 스탯 = 장군 스탯 × (0.2 + 지휘력×0.01)   ← 종속 파생
//                    병사는 자기 스탯이 없다. 장군이 곧 병사의 전부다.
//      이 게임 소환력 : 몬스터 스탯 = 종족 기본 스탯 + 소환력 보너스  ← 가산
//                    몬스터는 종족별 기본 스탯을 이미 갖고 있다.
//
//      그래서 소환사를 바꿔도 종족의 정체성은 유지되고(탱커는 여전히 탱커),
//      소환사는 그 위에 얹는 두께만 바꾼다.
//      SoldierStatApplier 공식을 그대로 베껴 오면 안 된다.
//
//  ■ 힘·체력도 "조금" 영향을 준다
//      주역은 소환력이다. 힘·체력의 계수를 소환력과 비슷하게 잡으면
//      지능을 찍을 이유가 사라지고 3대 스탯이 하나로 뭉개진다.
//      그래서 계수를 한 자릿수 낮게 둔다.
//
//  ■ 품질은 마지막에 곱한다
//      원작 등급과 동일하게 등급당 +10% (GameplayConfig.GradeMultPerTier).
//      가산이 끝난 뒤 곱해야 "좋은 품질일수록 소환력 투자도 같이 값어치가 오른다"
//      가 성립한다.
//
//  ■ 친화도가 소환력의 두께를 정한다 (2026-09-04 재조정)
//      친화 종족   : 소환력 ×1.2
//      그 외 종족   : 소환력 ×1.0   ← 벌점 없음. 친화는 순수 보너스다.
//    편성 제한이 아니라 배율이라, 주운 카드는 무엇이든 쓸 수 있으면서도
//    캐릭터마다 잘 굴러가는 조합이 갈린다.
//    ⚠ 배율의 정본은 SummonerAffinityRule 이고, 특성이 그것을 갈아 끼울 수 있다
//      (심연 공명 → 1.6 / 0.8). 판정과 배율을 함께 보는 곳은
//      RunPerkRule.AffinityMultFor 하나다 — 여기에 숫자를 박지 말 것.

//  ■ 특성은 ⑥ 에서 마지막으로 얹힌다
//    조건이 붙은 것만 있다(라인·시너지 수·덱 종족 수·판이 열린 뒤인가).
//    밋밋한 스탯 %는 카드 레벨업의 몫이다 — RunPerk.cs 머리 주석 참고.
//
//  ■ 카드 레벨은 **맨 마지막에** 얹는다
//      최종 = (종족 + 소환력×친화 + 소환사 보조) × 품질  →  레벨 보너스 가산
//    레벨 보너스는 종족마다 정해진 표(MonsterSpeciesData.LevelBonuses)이고,
//    비율 항목은 **그 시점의 값**에 곱해 더한다. 그래서 소환력·품질보다
//    뒤에 와야 "키운 카드일수록 소환력 투자도 같이 값어치가 오른다" 가 성립한다.
//
//    ⚠ 예전의 "레벨당 일괄 +18%" 는 폐기됐다 — 어빌리티를 대체하면서
//      종족마다 다른 것이 열리는 표로 바뀌었다.
//
//  ■ 유닛 레벨 축은 여전히 없다
//      인게임 레벨업이 없다. UnitJobRoller 의 levelMult·flatHp 에 해당하는 항은
//      여기 없는 게 맞다. 카드 레벨은 **손에 든 카드**의 등급이지
//      필드에 선 유닛이 자라는 것이 아니다.
// ============================================================

public static class MonsterStatComposer
{
    // ── 소환력 → 스탯 환산 계수 ──────────────────────────────
    //  종족별 반응도(SummonPowerToHp / SummonPowerToAttack)가 여기에 곱해진다.

    /// <summary>소환력 1당 더해지는 HP.</summary>
    public const float HpPerSummonPower = 8f;

    /// <summary>소환력 1당 더해지는 공격력.</summary>
    public const float AttackPerSummonPower = 0.8f;

    // ── 소환사 힘·체력의 보조 기여 ──────────────────────────
    //  주역은 소환력이다. 여기가 커지면 3대 스탯이 하나로 뭉개진다.

    // ⚠ 소환사 '패기'(옛 힘) 는 여기 없다 (사용자 지적, 2026-09-07)
    //   한때 +힘×0.1 을 공격력에 더했는데, 4~8 의 폭이 +0.4~0.8 이라
    //   몬스터 공격력의 4% 도 안 됐다. 소환력이 이미 공격력을 올리므로
    //   그건 소환력의 열화판일 뿐이었다.
    //   패기는 이제 **밀어내기와 과부하 저항**을 맡는다 (SummonerVigorRule).

    /// <summary>소환사 체력 1당 몬스터 HP 에 더해지는 양. 소환력보다 훨씬 작다.</summary>
    public const float HpPerSummonerVitality = 1f;

    /// <summary>
    /// 종족 정의 + 소환사 + 품질로 몬스터의 최종 스탯을 만든다.
    /// </summary>
    /// <param name="species">종족 정의 (기본 스탯의 출처)</param>
    /// <param name="summoner">소환한 소환사 (소환력·힘·체력의 출처)</param>
    /// <param name="grade">도감에 저장된 그 종족의 영구 품질</param>
    /// <param name="cardLevel">이 종족 카드의 레벨 (중복 획득으로 오른다). 1 = 기본</param>
    /// <param name="lane">
    /// 이 개체가 설 라인. −1 이면 라인이 없는 개체(분열체·스킬 소환 등)라
    /// 라인 특성(소수정예·군세·주공·측면)이 걸리지 않는다.
    /// </param>
    /// <param name="inherit">
    /// 진화로 물려받은 공/체 비율 (SummonDeckSlot.InheritBonus).
    /// ⚠ 레벨 비율과 <b>같은 자리</b>에서 더한다 — 종족 표보다 앞이라야
    ///   표의 비율 항목이 그 위에서 곱해진다 (④ 주석과 같은 이유).
    /// </param>
    /// <param name="drainMult">
    /// 배출되는 순간에 정해진 특성 배율 (RunPerkRule.DrainStatMult — 뒤집힌 과부하·기다림의 미학·매복).
    /// 카드 몬스터만 1 이 아닌 값을 받는다. ⑥ 에서 다른 특성과 함께 곱한다.
    /// </param>
    public static UnitStat Compose(MonsterSpeciesData species, SummonerData summoner,
                                   UnitGrade grade, int cardLevel = 1, int lane = -1,
                                   float inherit = 0f, float drainMult = 1f)
    {
        var cfg = GameplayConfig.Current;

        float gradeCoef = cfg != null ? cfg.GradeMultPerTier : 0.10f;
        float defMax    = cfg != null ? cfg.DefenseMax       : 0.95f;

        // ── ⓪ 친화도 — 이 소환사가 이 종족을 얼마나 잘 다루는가 ──
        //   ⚠ 특성(심연 공명·친화 확장)이 배율과 판정을 함께 바꾸므로
        //     summoner.SummonPowerMultFor 가 아니라 RunPerkRule 을 지난다.
        //     특성이 없으면 SummonerAffinityRule 의 값 그대로다.
        float affinity = RunPerkRule.AffinityMultFor(summoner, species);

        // 심연 공명(리치) — 친화 종족에 한해 소환력이 한 번 더 실린다.
        // ⚠ 스탯을 바꾸는 개성은 반드시 여기서 처리한다
        //   SummonerPerkRuntime 은 태그를 붙이는 훅이지 스탯 계산기가 아니다.
        //   두 곳에서 곱하기 시작하면 최종 수치의 출처를 추적할 수 없게 된다.
        if (summoner.Perk == SummonerPerk.DeepChannel && summoner.IsAffinity(species))
            affinity *= Mathf.Max(1f, summoner.PerkValue);

        // 특성 '마력 폭주' — 잔량을 0 까지 쓴 판에만 켜진다.
        float summonPower = summoner.SummonPower * affinity * RunPerkRule.SummonPowerMult;

        // ── ① 가산 — 종족 기본값 위에 소환사 기여분을 더한다 ──
        float hp = species.MaxHp
                 + summonPower       * HpPerSummonPower * species.SummonPowerToHp
                 + summoner.Vitality * HpPerSummonerVitality;

        float attack = species.Attack
                     + summonPower * AttackPerSummonPower * species.SummonPowerToAttack;

        // ── ② 품질 배율 + 배출 등급 보너스 — 가산이 끝난 뒤 곱한다 ──
        //   카드 레벨은 여기가 아니라 맨 마지막에 얹는다 (③ 뒤).
        float gradeMult = 1f + (int)grade * gradeCoef;

        // ⚠ 늦게 나오는 종족은 그만큼 무겁다 (사용자 확정, 2026-09-07)
        //   배출 간격이 등급으로 갈리면서(SpawnPaceRule) 느린 종족은 순수한
        //   손해만 봤다. 대가가 없으면 "빠른 놈만 쓰는" 것이 언제나 정답이
        //   되어 그 축이 죽는다. 등급마다 공/체 +5% 씩.
        //
        //   ⚠ 여기가 유일한 곱셈 지점이다
        //     도감 표시(MonsterDetailPopup)도 이 함수를 다시 부르지 말고
        //     SpawnPaceRule.StatMultiplierFor 를 그대로 쓴다.
        //   ⚠ 등급 판정은 SO 원본값으로 한다 — 여기서 곱한 값이 되먹임되지 않는다.
        float paceMult = SpawnPaceRule.StatMultiplierFor(species);

        // 빠른 종족은 먼저 닿아 점사를 받는다 — 체력만 조금 채운다 (SpeedHpRule)
        float speedHpMult = SpeedHpRule.HpMultiplierFor(species);

        hp     *= gradeMult * paceMult * speedHpMult;
        attack *= gradeMult * paceMult;

        // ── ③ 레이어에 굽는다 ────────────────────────────────
        var stat = new UnitStat();

        // 쿨감은 출처끼리 더하지 않고 곱연산으로 겹친다 — 원작 UnitJobRoller 와 동일 규칙.
        stat.SetCombineMode(StatType.SkillCooldownReduce, CombineMode.MultiplyResidual);

        stat.Set(StatType.MaxHp,  hp);
        stat.Set(StatType.Attack, attack);

        // 사거리·이동속도·연사속도는 종족의 정체성이다.
        // 소환력으로 이것까지 올리면 모든 몬스터가 같은 유닛으로 수렴한다.
        stat.Set(StatType.AttackRange, species.AttackRange);
        stat.Set(StatType.AttackSpeed, species.AttackSpeed);

        // 야성 질주(드루이드) — 이동속도만 건드리는 개성.
        // 이동속도는 위 규칙의 예외다: "종족의 정체성" 이지만, 야수를 빠르게 만드는 것이
        // 이 캐릭터의 정체성이기도 하다. 예외를 늘리지 말 것 —
        // 사거리·연사까지 개성이 만지기 시작하면 종족 구분이 사라진다.
        float moveSpeed = species.MoveSpeed;
        if (summoner.Perk == SummonerPerk.WildSprint && summoner.IsAffinity(species))
            moveSpeed *= Mathf.Max(1f, summoner.PerkValue);

        stat.Set(StatType.MoveSpeed,   moveSpeed);
        stat.Set(StatType.Defense,     Mathf.Min(species.Defense, defMax));
        stat.Set(StatType.CritChance,  species.CritChance);
        stat.Set(StatType.CritDamage,  species.CritDamage);

        // ── ④ 카드 레벨 보너스 — 종족마다 정해진 표 ──
        //   ⚠ 반드시 맨 마지막이다. 비율 항목이 '그 시점의 값' 에 곱해 더해지므로
        //     사거리·이동속도까지 오르는 종족이 있고, 그 값들은 ③ 에서야 정해진다.
        // ⚠ 종족 표보다 **먼저** 얹는다
        //   표의 비율 항목은 '그 시점의 값' 에 곱해 더한다. 기본 증가를 먼저
        //   올려 두면 표가 그 위에서 곱해져 "키운 카드일수록 표도 값어치가
        //   오른다" 가 성립한다. 순서를 뒤집으면 둘이 그냥 더해진다.
        float levelStat = CardLevelRule.StatBonusRatio(cardLevel) + Mathf.Max(0f, inherit);
        if (levelStat > 0f)
        {
            stat.Set(StatType.MaxHp,  stat.Get(StatType.MaxHp)  * (1f + levelStat));
            stat.Set(StatType.Attack, stat.Get(StatType.Attack) * (1f + levelStat));
        }

        species.ApplyLevelBonuses(stat, cardLevel);

        // ── ⑤ 시너지 — 덱 구성이 정한다 ──
        //   ⚠ 레벨 보너스보다 뒤다. 앞에 두면 레벨 보너스의 비율 항목이
        //     이미 부풀려진 값을 기준으로 다시 곱해 이중 계산이 된다.
        MonsterSynergyRuntime.ApplyStats(stat, species.Tags,
                                         species.AttackKind == MonsterAttackKind.Ranged);

        // ── ⑤-b 소환사 개성 '마력 증폭'(대마법사) — 그릇 10당 전군 공/체 (2026-09-12) ──
        //   ⚠ 소환되는 순간의 그릇으로 굳는다 (필드에 선 개체는 안 바뀐다)
        //   ⚠ 스탯을 바꾸는 개성은 여기서만 처리한다 (⓪ 주석과 같은 이유)
        if (summoner.Perk == SummonerPerk.ArcaneMight)
        {
            var   mana  = UserDataManager.Instance?.Get<SummonManaData>();
            float steps = mana != null ? Mathf.Floor(mana.Max / 10f) : 0f;
            float arc   = 1f + summoner.PerkValue * steps;

            stat.Set(StatType.MaxHp,  stat.Get(StatType.MaxHp)  * arc);
            stat.Set(StatType.Attack, stat.Get(StatType.Attack) * arc);
        }

        // ── ⑥ 특성 — 맨 마지막이다 ────────────────────────────
        //   ⚠ 시너지보다 뒤다. 앞에 두면 시너지의 비율 항목이 이미 부풀린
        //     값 위에서 다시 곱해져 이중 계산이 된다 (⑤ 주석과 같은 이유).
        ApplyRunPerks(stat, lane, drainMult);

        // ── ⑥-c 상점 '전쟁 자금' — 런 스코프 · 전 몬스터 (2026-09-13) ──
        //   ⚠ 특성과 **같은 성격**이라 같은 자리에 둔다 (런에 사서 런에 사라진다).
        //     유물(⑥-b)보다 앞이다 — "이번 런에 산 것" 위에 "쌓아 온 것" 이 곱해진다.
        //   ⚠ 곱하는 곳은 여기 하나다 (RunShopRule.WarFundStatMult 주석).
        float warFund = RunShopRule.WarFundStatMult;
        if (warFund > 1f)
        {
            stat.Set(StatType.MaxHp,  stat.Get(StatType.MaxHp)  * warFund);
            stat.Set(StatType.Attack, stat.Get(StatType.Attack) * warFund);
        }

        // ── ⑥-b 유물 — 런을 넘어 남는 영구 보정 (2026-09-07) ──
        //   ⚠ 특성 **뒤**, 장비 **앞**이다
        //     비율끼리는 순서가 값을 바꾸므로 자리를 못 박아 둔다.
        //     특성(런 스코프)이 먼저 붙고 그 위에 유물(영구)이 얹힌다 —
        //     "이번 런에 주운 것" 위에 "쌓아 온 것" 이 곱해지는 그림이다.
        //   ⚠ Unit_Monster 노드만 걸린다. All 로 두면 같은 노드가 장수
        //     경로에도 걸려 **적(용사)까지 강화된다** (RelicTreeApplier 주석).
        RelicTreeApplier.ApplyToMonsterStat(stat);

        // ── ⑦ 장비 — 절대값이라 정말로 맨 마지막이다 ──────────
        //   ⚠ 앞에 두면 안 된다
        //     ④~⑥ 의 비율 항목은 전부 '그 시점의 값' 에 곱해 더한다. 장비를
        //     먼저 얹으면 카드 레벨·시너지·특성이 장비 몫까지 함께 곱해
        //     "장비를 끼면 레벨 보너스도 같이 커지는" 이중 계산이 된다.
        //     절대값은 어디서든 같은 값이어야 절대값이다.
        //
        //   ⚠ 품질 배율(②)도 안 곱한다 — 품질은 이미 **칸 수**로 값을 한다
        //     (MonsterGearRule.SlotsFor). 배율까지 곱하면 같은 손잡이가 두 번 돈다.
        MonsterGearRule.ApplyStats(stat, species.Id);

        return stat;
    }

    /// <summary>
    /// 런 특성이 스탯에 얹는 몫. <b>조건이 붙은 것만</b> 여기 있다 —
    /// 밋밋한 스탯 %는 카드 레벨업의 몫이다 (RunPerk.cs 규칙).
    /// </summary>
    /// <param name="lane">−1 이면 라인 특성을 건너뛴다.</param>
    static void ApplyRunPerks(UnitStat stat, int lane, float drainMult)
    {
        RunPerkData perks = RunPerkRule.Data;
        if (perks == null) return;

        // ── 전 몬스터에 걸리는 배율 ──
        //   곱으로 겹친다. 더하면 세 장을 모았을 때 값이 선형으로만 늘어
        //   "특성을 모을수록 가속된다" 는 느낌이 사라진다.
        float mult = RunPerkRule.LateBloomMult * RunPerkRule.MenagerieMult;

        if (perks.Has(RunPerk.Resonance))
            mult *= 1f + RunPerkRule.ResonancePerSynergy * MonsterSynergyRule.ActiveCount;

        // 원군 — 판이 열린 뒤에 부른 개체만. 비용 할증은 ManaCostDelta 쪽에 있다.
        if (perks.Has(RunPerk.Reinforcements) && RunPerkRule.IsAfterStageStart())
            mult *= RunPerkRule.ReinforcementsStatMult;

        // 봉인된 칸 — 칸 하나를 내준 값. 모든 몬스터에 걸린다.
        if (perks.Has(RunPerk.SealedSlot))
            mult *= 1f + RunPerkRule.SealedSlotBonus;

        // 배출되는 순간에 정해진 몫 (뒤집힌 과부하·기다림의 미학·매복) — 부르는 쪽이 계산해 넘겼다.
        mult *= drainMult;

        // ── 라인 특성 ──
        if (lane >= 0)
        {
            int lanes = SummonFieldLayout.LaneCount;

            // 소수정예 — 스폰 시점에 굳힌다. 매 프레임 다시 재면 교전 중에
            // 대상 라인이 옮겨 다니며 같은 카드가 낸 개체끼리 세기가 달라진다.
            if (perks.Has(RunPerk.FewButElite))
            {
                int mine = MonsterLineReturner.AliveInLane(lane);
                int min  = MonsterLineReturner.MinOccupiedLaneCount(lanes);

                // 아직 아무도 없으면 이 개체가 곧 최소 라인이 된다.
                if (min == 0 || mine + 1 <= min)
                    mult *= 1f + RunPerkRule.FewButEliteBonus;
            }

            // 군세 — 자기 라인이 붐빌수록 강해진다. 소수정예와 정반대 축이다.
            if (perks.Has(RunPerk.Horde))
                mult *= 1f + RunPerkRule.HordePerAlly * MonsterLineReturner.AliveInLane(lane);

            // 주공 — 중앙 라인. 용사 배치 순서(3·2·4·1·5)상 가장 먼저 붙는 자리다.
            if (perks.Has(RunPerk.CenterPush) && lane == lanes / 2)
                mult *= 1f + RunPerkRule.CenterPushBonus;

            // 측면 — 바깥 두 라인. 가장 늦게 채워지는 자리라 먼저 밀고 들어간다.
            if (perks.Has(RunPerk.Flank) && (lane == 0 || lane == lanes - 1))
            {
                stat.Set(StatType.Attack,
                         stat.Get(StatType.Attack) * (1f + RunPerkRule.FlankAttackBonus));
                stat.Set(StatType.MoveSpeed,
                         stat.Get(StatType.MoveSpeed) * (1f + RunPerkRule.FlankMoveBonus));
            }
        }

        if (Mathf.Approximately(mult, 1f)) return;

        stat.Set(StatType.MaxHp,  stat.Get(StatType.MaxHp)  * mult);
        stat.Set(StatType.Attack, stat.Get(StatType.Attack) * mult);
    }
}
