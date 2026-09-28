using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  CardEvolution.cs
//  만렙 카드가 갈 수 있는 두 갈래 — **진화**와 **융합**의 규칙.
//
//  ■ 갈래
//    진화(Evolve) — 같은 계보의 상위 종족으로 **무작위** 변한다.
//                   슬라임 → 힐/독/강철 슬라임 중 하나.
//                   카드가 다른 것이 되고, 그 종족의 패시브를 얻는다.
//    융합(Fuse)   — 다른 카드를 재료로 먹고 **그 카드의 종족 패시브를 배운다.**
//                   내 종족은 그대로다. 재료 카드는 사라진다.
//
//  ■ ⚠ 한쪽을 고르면 다른 쪽이 닫힌다 (사용자 확정 규칙)
//      진화한 카드 → 융합만 가능 (더 진화하지 않는다)
//      융합한 카드 → 진화 불가 (융합은 계속 가능)
//    즉 **진화는 런당 카드마다 최대 1회**이고, 융합보다 먼저 해야 한다.
//    이 순서 제약이 곧 선택이다 — "지금 진화할까, 신속을 먹여 둘까".
//
//  ■ 진화가 무작위인 이유
//    고르게 하면 언제나 가장 센 업그레이드 하나로 수렴한다. 무작위라야
//    "무엇이 나왔는가" 에 맞춰 나머지 판을 짜게 된다.
//    ⚠ 대신 나쁜 결과가 나와도 되돌릴 수 없으므로, 업그레이드끼리는
//      강약이 아니라 **성격**으로 갈려 있어야 한다 (힐 vs 독 vs 가시).
//
//  ■ 레벨(장수)은 그대로 넘어간다
//    진화해도 모은 장수를 잃지 않는다. 잃게 만들면 진화가 벌처럼 느껴져
//    아무도 하지 않는다. 대신 진화 자체가 "더는 진화 못 함" 이라는 값을 치른다.
//
//  ■ 런이 끝나면 전부 사라진다
//    진화·융합 결과는 SummonDeckData(런 스코프)에만 산다. 다음 런은
//    소환사의 시작 카드에서 기본 패시브만 갖고 다시 시작한다.
// ============================================================

public static class CardEvolution
{
    /// <summary>한 카드가 융합으로 배울 수 있는 패시브의 최대 수.</summary>
    public const int MaxLearned = 3;

    /// <summary>
    /// 진화할 때 <b>부모가 쌓아 둔 레벨 보너스 중 물려받는 몫</b>.
    ///
    /// ■ ⚠ 진화는 레벨을 1 로 되돌린다 (사용자 확정, 2026-09-09)
    ///   만렙을 그대로 물려주면 진화한 그 판에 또 만렙이라, 다음 보상에서
    ///   곧바로 진화·융합 창이 뜬다 — 키우는 과정이 통째로 사라진다.
    ///
    /// ■ 대신 절반을 영구히 얹는다
    ///   그냥 되돌리기만 하면 "진화했더니 주워 온 카드와 똑같다" 가 되어
    ///   진화할 이유가 없다. Lv5(+48%)의 절반 = <b>+24%</b> 가 Lv1 부터 붙고,
    ///   다시 만렙까지 키우면 +72% 가 된다 — 주워 키운 카드(+48%)보다 확실히 세다.
    ///   ⚠ 종족 표(LevelBonuses)는 물려받지 않는다 — 그건 새 종족의 것이다.
    /// </summary>
    public const float EvolveInheritShare = 0.5f;

    /// <summary>
    /// 융합할 때 <b>재료가 쌓아 둔 레벨 보너스 중 대상에게 넘어가는 몫</b>.
    ///
    /// ■ ⚠ 키운 카드를 먹여도 손해는 아니게, 다만 <b>덤이다</b> (사용자 확정, 2026-09-10)
    ///   전에는 재료의 레벨이 통째로 사라졌다. 그래서 융합 재료로는 언제나
    ///   <b>Lv1 짜리 잡카드</b>를 쓰는 것이 정답이었고, 키운 카드는 재료로
    ///   고르는 순간 손해였다 — 고를 수 있는데 고르면 안 되는 선택지는
    ///   선택지가 아니다.
    ///
    /// ■ 왜 작은가 (0.35 → 0.10) · 왜 다시 올렸나 (0.10 → 0.20, 2026-09-13)
    ///   융합은 <b>종족 패시브</b>를 이미 준다. 그게 이 갈래의 본상이고,
    ///   대상은 제 만렙까지 그대로 유지한다 — 진화처럼 Lv1 로 되돌아가지 않는다.
    ///   여기까지 크게 주면 "만렙 카드끼리 먹이기" 가 다른 모든 성장을 덮어서
    ///   0.35 는 걷어냈다.
    ///
    ///   그런데 상점에 <b>전쟁 자금</b>(전 몬스터 공/체 +2%, 무한 구매)이 생기면서
    ///   기준이 달라졌다 (사용자 지적). 골드만 내면 전군이 오르는데, 카드 한 장을
    ///   통째로 없애 얻는 값이 +5% 면 융합을 고를 이유가 없다. 내주는 것이 큰 쪽이
    ///   더 받아야 한다.
    ///
    /// ■ 셈
    ///   Lv5 재료(+48%) × 0.20 = <b>+9.6%</b> 가 대상에게 영구히 얹힌다 —
    ///   전쟁 자금 다섯 번어치(+10%)를 카드 한 장으로 산 셈이고, 그쪽은 값이
    ///   살수록 오르므로 다섯 번이 결코 싸지 않다.
    ///   Lv1 재료는 0 이다 — 아무것도 투자하지 않은 카드에서 나올 것은 없다.
    ///   융합은 카드당 3회까지라 다 채워도 +29% 아래에서 멈춘다.
    ///   ⚠ 더 올리려면 전쟁 자금(RunShopRule.WarFundStatBonus)과 <b>함께</b> 볼 것.
    ///
    /// ⚠ 진화(EvolveInheritShare 0.5)와 <b>같은 칸</b>에 쌓인다
    ///   (SummonDeckSlot.InheritBonus) — 적용 지점도 MonsterStatComposer ④ 하나다.
    ///   새 축을 만들지 말 것.
    /// </summary>
    public const float FuseInheritShare = 0.20f;

    /// <summary>
    /// 이 재료를 먹였을 때 대상에게 넘어가는 공/체 비율. 0 이면 넘어갈 것이 없다.
    ///
    /// ⚠ 화면(CardEvolveUI)과 실제 적용(SummonDeckData.Fuse)이 <b>이 함수</b>를
    ///   함께 쓴다. 한쪽이 따로 계산하면 "적힌 것과 다른 값이 붙는다".
    /// </summary>
    public static float FuseInheritFrom(in SummonDeckSlot material)
        => CardLevelRule.StatBonusRatio(material.Level) * FuseInheritShare;

    /// <summary>
    /// 제3의 갈래 '강화' 가 얹는 공/체 몫.
    ///
    /// ■ 왜 있나 (사용자 지시, 2026-09-12)
    ///   진화가 막힌 카드(이미 진화했거나 계보 끝)를 고르면 남는 갈래가
    ///   <b>융합뿐</b>이라 융합이 강제됐다. 그런데 융합은 재료 카드를 **없앤다** —
    ///   덱이 좁을 때는 하고 싶지 않은 것이 정상이다. 고를 것이 하나뿐인 창은
    ///   선택지가 아니라 확인 버튼이다.
    ///
    /// ■ 작아야 한다
    ///   언제나 고를 수 있는 안전한 갈래라, 크면 진화·융합을 볼 이유가 없어진다.
    ///   재료를 잃지도 종족이 바뀌지도 않는 대신 <b>덤만큼만</b> 준다.
    ///
    /// ⚠ 진화·융합과 <b>같은 칸</b>에 쌓인다 (SummonDeckSlot.InheritBonus) —
    ///   적용 지점도 MonsterStatComposer ④ 하나다. 새 축을 만들지 말 것.
    /// </summary>
    public const float EmpowerBonus = 0.10f;

    // ── 가능 여부 ────────────────────────────────────────────

    /// <summary>
    /// 이 칸이 지금 진화할 수 있는가.
    ///
    /// 만렙 · 아직 진화하지 않음 · 융합한 적 없음 · 몬스터 카드 · 상위 종족 존재.
    /// (스킬 카드에는 계보가 없다)
    /// </summary>
    public static bool CanEvolve(in SummonDeckSlot slot)
    {
        if (slot.IsEmpty)                     return false;
        if (slot.Kind != SummonKind.Monster)  return false;
        if (!slot.IsMaxLevel)                 return false;
        if (slot.HasFused)                    return false;

        // ■ ⚠ HasEvolved 는 더 이상 막지 않는다 (사용자 지시, 2026-09-15)
        //   2차 업그레이드가 생기면서 **1차 카드가 한 번 더 진화해야** 한다.
        //   막는 것은 이제 아래 한 줄이다 — "갈 곳이 있는가".
        //   2차는 위가 없어 목록이 비므로 저절로 멈춘다. 단계를 따로 세지 않는 이유다.
        //
        //   ⚠ 공짜로 열린 것이 아니다 — 진화하면 Lv1 로 되돌아간다.
        //     2차로 가려면 그 1차를 **만렙까지 다시 키워야** 한다. 그 되감기가 값이다.
        return CollectUpgrades(slot.Id).Count > 0;
    }

    /// <summary>
    /// 이 칸이 지금 융합할 수 있는가 (재료는 따로 고른다).
    /// 진화 여부와 무관하다 — 진화한 카드도 융합할 수 있다.
    /// </summary>
    public static bool CanFuse(in SummonDeckSlot slot)
    {
        if (slot.IsEmpty)                    return false;
        if (slot.Kind != SummonKind.Monster) return false;
        if (!slot.IsMaxLevel)                return false;

        return slot.LearnedCount < MaxLearned;
    }

    /// <summary>
    /// 덱에 이 칸이 먹을 재료가 하나라도 있는가.
    ///
    /// ⚠ CanFuse 는 이걸 보지 않는다 — 그쪽은 "칸이 남았나" 만 본다.
    ///   둘을 따로 쓰면 재료가 없는 카드도 융합할 수 있다고 판정된다.
    /// </summary>
    public static bool HasMaterial(SummonDeckData deck, int targetSlot)
    {
        if (deck == null) return false;

        SummonDeckSlot target = deck.GetSlot(targetSlot);

        for (int i = 0; i < deck.SlotCount; i++)
        {
            if (i == targetSlot) continue;
            if (CanBeMaterial(target, deck.GetSlot(i))) return true;
        }
        return false;
    }

    /// <summary>
    /// 이 칸을 골랐을 때 <b>실제로 할 수 있는 것이 있는가</b>.
    ///
    /// ■ ⚠ 판정은 이 함수 하나다 (사용자 지적, 2026-09-07)
    ///   보상 후보를 거르는 쪽(CardRewardPicker)과 창을 여는 쪽(CardEvolveUI)이
    ///   서로 다른 조건을 쓰고 있었다 — 후보 쪽은 CanFuse 만 보고 **재료를
    ///   안 봤다.** 그래서 융합을 한 번 하고 재료가 떨어지면
    ///     · 그 카드가 보상에 또 뜨고
    ///     · 고르면 창이 안 뜨고 그냥 다음 판으로 넘어갔다
    ///   보상 한 번을 통째로 버리는 셈이라 "스킵된다" 로 보인다.
    /// </summary>
    public static bool HasAnyChoice(SummonDeckData deck, int targetSlot)
    {
        if (deck == null) return false;

        SummonDeckSlot slot = deck.GetSlot(targetSlot);

        return CanEvolve(slot) || (CanFuse(slot) && HasMaterial(deck, targetSlot));
    }

    /// <summary>
    /// 그 칸을 <b>지정한 종족으로</b> 진화시킬 수 있는가 — 카드 3택의 '진화' 후보가 쓴다.
    ///
    /// ■ ⚠ 만렙을 요구하지 않는다 (사용자 확정, 2026-09-12)
    ///   만렙 조건은 <b>처음 그 종족을 얻는 길</b>(무작위 진화)에만 걸린다 —
    ///   그걸로 도감이 열린다. 도감에 이미 오른 종족은 "슬라임만 들고 있으면
    ///   바로 진화시킬 수 있는" 선택지로 나온다. 그래서 <see cref="CanEvolve"/>
    ///   와 조건이 다르다 — 둘은 서로 다른 문이다.
    ///
    /// ■ 그래도 나머지 규칙은 그대로다
    ///   융합한 칸은 막히고, 같은 종족이 두 칸이 되지도 않는다.
    ///   ⚠ 진화 이력(HasEvolved)은 더 이상 막지 않는다 — 2차 업그레이드가 그 길로 간다
    ///     (CanEvolve 주석 참고). 막는 것은 "갈 곳이 있는가" 하나다.
    ///   같은 종족이 두 칸이 되지도 않는다 — 그러면 IndexOf 가 앞 칸만 찾아
    ///   레벨·강화·시너지가 한쪽에만 걸린다 (CollectUpgrades 주석과 같은 이유).
    ///
    /// ⚠ 후보를 고르는 쪽(CardRewardPicker)과 실제로 바꾸는 쪽
    ///   (SummonDeckData.EvolveTo)이 <b>이 함수 하나</b>를 함께 본다.
    /// </summary>
    public static bool CanEvolveTo(SummonDeckData deck, int slotIndex, MonsterSpeciesData target)
    {
        if (deck == null || target == null)               return false;
        if (target.UpgradeOf == null)                     return false;
        if (slotIndex < 0 || slotIndex >= deck.SlotCount) return false;

        SummonDeckSlot slot = deck.GetSlot(slotIndex);

        if (slot.IsEmpty)                    return false;
        if (slot.Kind != SummonKind.Monster) return false;
        if (slot.HasFused)                   return false;

        // ⚠ HasEvolved 를 보지 않는다 — 2차 업그레이드 (CanEvolve 주석과 같은 이유)

        // 그 칸이 이 진화체의 **바로 아래 단계**여야 한다.
        // ⚠ UpgradeOf.Id 를 직접 비교하지 말 것 — 부모가 여럿일 수 있다
        //   (슬라임 킹은 힐·독·강철 어느 것에서도 올라온다). IsUpgradeFrom 이 정본이다.
        if (!target.IsUpgradeFrom(slot.Id)) return false;

        // 이미 덱에 있는 종족으로는 진화하지 않는다.
        return !deck.Contains(target.Id);
    }

    /// <summary>
    /// 이 칸이 저 칸의 융합 재료가 될 수 있는가.
    ///
    /// ⚠ 같은 종족은 재료가 될 수 없다
    ///   자기 패시브를 자기에게 먹이면 아무 일도 일어나지 않는데 카드만 사라진다.
    /// </summary>
    public static bool CanBeMaterial(in SummonDeckSlot target, in SummonDeckSlot material)
    {
        if (material.IsEmpty)                    return false;
        if (material.Kind != SummonKind.Monster) return false;
        if (material.Id == target.Id)            return false;

        // ⚠ 넘겨줄 것이 없으면 재료가 아니다 (사용자 지적, 2026-09-09)
        //   SummonDeckData.Fuse 는 TryLearn 이 실패하면 **아무 일도 하지 않고**
        //   false 를 돌려준다 — 화면에서는 "골랐는데 융합이 안 된다" 로만 보인다.
        //   이미 배운 개성을 또 주는 재료가 그 경우다(두 번째 융합부터 흔해진다).
        //   고를 수 있으면 반드시 되어야 한다 — 판정을 여기로 끌어올린다.
        return CanLearnFrom(target, material);
    }

    /// <summary>
    /// 이 재료가 실제로 새 개성을 넘겨줄 수 있는가.
    ///
    /// ⚠ 판정은 TryLearn 과 같아야 한다 — 거기가 실제로 넣는 곳이다.
    ///   (칸이 남았는가 · 이미 갖고 있지 않은가)
    /// </summary>
    static bool CanLearnFrom(in SummonDeckSlot target, in SummonDeckSlot material)
    {
        CardCatalog catalog = CardCatalog.Current;
        if (catalog == null) return false;

        SpeciesPassive give = PassiveFrom(catalog.GetMonster(material.Id));
        if (give == SpeciesPassive.None) return false;

        for (int i = 0; i < MaxLearned; i++)
            if (target.GetLearned(i) == give) return false;

        return target.LearnedCount < MaxLearned;
    }

    // ── 진화 후보 ────────────────────────────────────────────

    static readonly List<MonsterSpeciesData> _upgrades = new(8);

    /// <summary>
    /// 이 종족에서 진화할 수 있는 상위 종족 목록.
    ///
    /// "같은 뿌리를 가리키는 업그레이드" 를 카드 목록 전체에서 찾는다.
    /// ⚠ 반환 리스트는 재사용된다 — 붙들어 두지 말고 즉시 쓸 것.
    /// </summary>
    public static List<MonsterSpeciesData> CollectUpgrades(string speciesId)
    {
        _upgrades.Clear();

        CardCatalog catalog = CardCatalog.Current;
        if (catalog == null) return _upgrades;

        MonsterSpeciesData self = catalog.GetMonster(speciesId);
        if (self == null) return _upgrades;

        // ⚠ 이미 덱에 있는 종족으로는 진화하지 않는다 (사용자 지적, 2026-09-09)
        //   독 슬라임을 들고 있는데 슬라임을 진화시켰더니 **또 독 슬라임**이 나왔다.
        //   같은 Id 카드가 두 칸이 되면 IndexOf 가 앞 칸만 찾아 레벨·강화·시너지가
        //   한쪽에만 걸린다. 무엇보다 진화의 값어치(새 종족)가 사라진다.
        //   ⚠ 후보가 전부 덱에 있으면 목록이 빈다 → CanEvolve 가 false 가 되어
        //     진화 갈래가 흐려진다. 그 카드는 융합으로 간다.
        SummonDeckData deck = UserDataManager.Instance?.Get<SummonDeckData>();

        // ── ⚠ 이미 도감에 오른 종족도 빼다 (사용자 지시, 2026-09-12) ──
        //
        //  ■ 무작위 진화는 이제 **발굴 전용**이다
        //    도감에 오른 종족은 "베이스를 들고 있으면 뜨는 진화 카드"(CanEvolveTo)로
        //    **골라서** 가져갈 수 있다. 그런데 무작위 진화가 그 종족을 또 뽑으면,
        //    만렙까지 키워 얻은 단 한 번의 무작위가 **이미 가진 것**으로 굴러
        //    통째로 낭비된다. 두 문이 서로 다른 것을 주도록 갈라 둔다 —
        //      무작위 진화 = 못 본 종족을 연다 (그래서 도감이 열린다)
        //      진화 카드   = 이미 아는 종족을 원할 때 고른다
        //
        //  ⚠ 후보가 전부 해금돼 있으면 목록이 빈다 → CanEvolve 가 false 가 되어
        //    진화 갈래가 **통째로 감춰진다**(CardEvolveUI.StackBranches). 그게 맞다 —
        //    그 카드에게 무작위 진화는 더 줄 것이 없고, 융합·강화가 남아 있다.
        //
        //  ⚠ 도감은 **영구**다(환생 무관). 그래서 이 필터는 런이 거듭될수록 좁아지고,
        //    결국 계보를 다 연 종족은 진화 카드 쪽으로만 간다 — 의도한 흐름이다.
        var codex = UserDataManager.Instance?.Get<MonsterCodexData>();

        foreach (MonsterSpeciesData candidate in catalog.Monsters)
        {
            if (candidate == null)               continue;
            if (candidate == self)               continue;
            // 바로 아래 단계만. ⚠ UpgradeOf 참조 비교가 아니다 —
            //   부모가 여럿인 종족이 있다 (슬라임 킹: 힐·독·강철 슬라임 어느 것에서나).
            if (!candidate.IsUpgradeFrom(self.Id)) continue;
            if (deck != null && deck.Contains(candidate.Id)) continue;
            if (codex != null && codex.IsUnlocked(candidate.Id)) continue;

            _upgrades.Add(candidate);
        }

        return _upgrades;
    }

    /// <summary>진화 결과를 무작위로 고른다. 후보가 없으면 null.</summary>
    public static MonsterSpeciesData RollUpgrade(string speciesId)
    {
        List<MonsterSpeciesData> candidates = CollectUpgrades(speciesId);
        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    // ── 재료가 넘겨주는 것 ───────────────────────────────────

    static readonly List<SpeciesPassive> _materialBuffer = new(8);

    /// <summary>
    /// 재료 카드가 넘겨주는 패시브.
    ///
    /// 재료의 **종족 패시브 중 마지막 것**(= 그 종족 고유의 것)을 넘긴다.
    /// 계보를 타고 물려받은 것까지 전부 넘기면 융합 한 번에 두세 개가 붙어
    /// 슬롯이 즉시 차고, "무엇을 얻었는가" 도 읽히지 않는다.
    ///
    /// ⚠ 재료가 융합으로 배운 것은 넘기지 않는다
    ///   패시브가 카드 사이를 무한히 옮겨 다니게 된다. 넘어가는 것은
    ///   **그 종족이 원래 가진 것** 하나뿐이다.
    /// </summary>
    public static SpeciesPassive PassiveFrom(MonsterSpeciesData material)
    {
        if (material == null) return SpeciesPassive.None;

        _materialBuffer.Clear();
        material.CollectSpeciesPassives(_materialBuffer);

        return _materialBuffer.Count > 0
            ? _materialBuffer[_materialBuffer.Count - 1]
            : SpeciesPassive.None;
    }
}
