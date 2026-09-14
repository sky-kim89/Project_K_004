using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SummonDeckData.cs
//  이번 런에 손에 든 카드 — 런 스코프 세이브 섹션.
//
//  ■ ⚠ 설계가 바뀌었다 (2026-08-27) — 편성해서 들어가지 않는다
//    예전에는 도감에서 해금된 종족을 골라 덱을 짜고 런에 들어갔다.
//    지금은
//      ① 소환사에 붙박인 시작 카드(SummonerData.StarterMonsters/StarterSkills)만
//         들고 시작하고
//      ② 런을 돌면서 보상으로 카드를 주워 칸을 채운다
//      ③ 이미 가진 카드를 또 주우면 **그 카드가 레벨업**한다
//    도감(MonsterCodexData)은 다시 순수한 기록으로 돌아갔다 — 편성 풀이 아니다.
//
//    그래서 이 섹션은 "편성표" 가 아니라 **런 중 카드 인벤토리**다.
//    이름은 세이브 키(SaveKey.SummonDeck) 호환을 위해 그대로 둔다.
//
//  ■ 칸 순서는 절대 밀지 않는다
//    슬롯 인덱스 = 하단 카드 바의 자리다. 카드가 늘어도 기존 카드의 자리가
//    바뀌면 손가락이 기억한 위치를 잃는다. 새 카드는 **뒤에만** 붙는다.
//    합성으로 재료가 사라진 칸도 당기지 않고 빈 칸으로 남긴다.
//
//  ■ 한 칸이 들고 있는 것
//      Kind    — 몬스터냐 스킬이냐 (같은 칸을 공유한다)
//      Id      — MonsterSpeciesData.Id 또는 SkillCardData.Id
//      Copies  — 누적 획득 장수. 레벨은 여기서 계산한다 (CardLevelRule)
//      Learned — **융합으로 배워 온 종족 패시브** (최대 3)
//      HasEvolved / HasFused — 진화·융합 이력. 갈래를 잠그는 데 쓴다
//
//  ■ 만렙 카드의 두 갈래 (규칙 정본은 CardEvolution)
//      진화 → 같은 계보의 상위 종족으로 무작위 변신. Id 가 바뀐다
//      융합 → 다른 카드를 먹고 그 종족 패시브를 배운다. Id 는 그대로
//    ⚠ 진화한 카드는 융합만, 융합한 카드는 진화 불가.
//      한쪽을 고르면 다른 쪽이 닫히는 것이 이 시스템의 선택 지점이다.
//
//    ⚠ 레벨을 저장하지 않고 장수만 저장한다
//      레벨 곡선(CardLevelRule)을 밸런싱으로 바꾸면 저장된 레벨과 어긋난다.
//      장수는 사실이고 레벨은 해석이다 — 사실만 저장한다.
//
//  ■ 환생 시 초기화된다 (런 스코프)
// ============================================================

[Serializable]
class SummonDeckSlotJson
{
    public int    kind;      // SummonKind
    public string id;
    public int    copies;    // 누적 획득 장수 (1 = 처음 획득)
    public bool   evolved;   // 진화 이력 — 다시 진화할 수 없다
    public bool   fused;     // 융합 이력 — 진화할 수 없다

    public int    manaCut;   // 강화소 — 깎인 소환 비용
    public int    extra;     // 강화소 — 늘어난 소환 마릿수
    public float  inherit;   // 진화로 물려받은 공/체 비율 (0.24 = +24%)

    /// <summary>융합으로 배워 온 SpeciesPassive 들 (정수로 저장).</summary>
    public List<int> learned = new();
}

[Serializable]
class SummonDeckJson
{
    public List<SummonDeckSlotJson> slots = new();
}

/// <summary>
/// 카드 한 칸. 비어 있으면 Id 가 빈 문자열이다.
///
/// 값 타입으로 두는 이유 — 칸은 배열의 자리이지 살아 있는 객체가 아니다.
/// 참조로 두면 "덱에서 뺐는데 어딘가 그 카드를 아직 들고 있는" 상태가 생긴다.
/// </summary>
[Serializable]
public struct SummonDeckSlot
{
    public SummonKind Kind;
    public string     Id;
    public int        Copies;

    /// <summary>진화한 적이 있는가. true 면 더 진화할 수 없다.</summary>
    public bool HasEvolved;

    /// <summary>융합한 적이 있는가. true 면 진화할 수 없다.</summary>
    public bool HasFused;

    // ── 강화소(RunNodeKind.Forge)가 붙이는 것 ────────────────
    //
    //  ⚠ 카드에 붙는다 — 종족이 아니다
    //    같은 종족을 나중에 또 주워도 다른 칸이면 강화가 따라가지 않는다.
    //    종족에 붙이면 "강화한 슬라임을 제물로 넣고 새 슬라임을 받으면
    //    강화가 유지되는" 상태가 생긴다.
    //
    //  ⚠ 카드가 덱에서 빠지면 함께 사라진다 (제단 제물 등). 그것이 대가다.

    /// <summary>강화소가 깎아 준 소환 비용. 하한은 ManaCostFor 가 건다.</summary>
    public int ManaDiscount;

    /// <summary>강화소가 늘려 준 소환 마릿수.</summary>
    public int ExtraSummons;

    /// <summary>
    /// 진화하며 물려받은 공/체 비율 (0.24 = +24%).
    ///
    /// ■ ⚠ 진화는 레벨을 <b>1 로 되돌린다</b> — 그 대신 이것이 남는다
    ///   (사용자 확정, 2026-09-09) 예전에는 만렙을 그대로 물려받아, 진화하자마자
    ///   또 만렙 카드가 되어 다음 판에 곧바로 진화·융합 창이 떴다. 키우는 과정이
    ///   통째로 건너뛰어진 셈이라 "5레벨이 되었다" 는 사건의 무게가 사라졌다.
    ///   지금은 Lv1 부터 다시 키운다.
    ///
    ///   그렇다고 진화체가 <b>주워 온 카드와 같으면</b> 진화할 이유가 없다.
    ///   그래서 부모가 쌓아 둔 레벨 보너스의 절반을 영구히 얹는다 —
    ///   같은 Lv1 이어도 진화체가 확실히 세다.
    ///   ⚠ 종족 표(LevelBonuses)는 물려받지 않는다. 그건 새 종족의 것이다.
    ///
    /// ⚠ 적용은 MonsterStatComposer ④ 한 곳뿐이다 (레벨 비율과 같은 자리).
    /// </summary>
    public float InheritBonus;

    // ⚠ 배열이 아니라 필드 셋이다
    //   구조체 안의 배열은 값 복사가 얕아서, 칸을 리스트에 넣었다 뺐다 하는
    //   동안 두 칸이 같은 배열을 가리키게 된다 (한쪽을 고치면 다른 쪽도 바뀐다).
    //   최대 3개뿐이라 펼쳐 두는 편이 안전하고 읽기도 쉽다.
    public SpeciesPassive Learned0;
    public SpeciesPassive Learned1;
    public SpeciesPassive Learned2;

    public SummonDeckSlot(SummonKind kind, string id, int copies = 1)
    {
        Kind         = kind;
        Id           = id;
        Copies       = copies;
        HasEvolved   = false;
        HasFused     = false;
        ManaDiscount = 0;
        ExtraSummons = 0;
        InheritBonus = 0f;
        Learned0   = SpeciesPassive.None;
        Learned1   = SpeciesPassive.None;
        Learned2   = SpeciesPassive.None;
    }

    public bool IsEmpty => string.IsNullOrEmpty(Id);

    /// <summary>누적 장수에서 계산한 카드 레벨. 규칙의 정본은 CardLevelRule 이다.</summary>
    public int Level => CardLevelRule.LevelForCopies(Copies);

    public bool IsMaxLevel => CardLevelRule.IsMaxLevel(Copies);

    /// <summary>융합으로 배운 패시브 수.</summary>
    public int LearnedCount
    {
        get
        {
            int n = 0;
            if (Learned0 != SpeciesPassive.None) n++;
            if (Learned1 != SpeciesPassive.None) n++;
            if (Learned2 != SpeciesPassive.None) n++;
            return n;
        }
    }

    public SpeciesPassive GetLearned(int index) => index switch
    {
        0 => Learned0,
        1 => Learned1,
        _ => Learned2,
    };

    /// <summary>배운 패시브를 목록에 담는다. 소환 시 종족 패시브 뒤에 붙는다.</summary>
    public void CollectLearned(System.Collections.Generic.List<SpeciesPassive> into)
    {
        if (Learned0 != SpeciesPassive.None) into.Add(Learned0);
        if (Learned1 != SpeciesPassive.None) into.Add(Learned1);
        if (Learned2 != SpeciesPassive.None) into.Add(Learned2);
    }

    /// <summary>빈 칸에 배운 패시브를 넣는다. 이미 갖고 있거나 자리가 없으면 false.</summary>
    public bool TryLearn(SpeciesPassive passive)
    {
        if (passive == SpeciesPassive.None)                       return false;
        if (Learned0 == passive || Learned1 == passive
                                || Learned2 == passive)           return false;

        if      (Learned0 == SpeciesPassive.None) Learned0 = passive;
        else if (Learned1 == SpeciesPassive.None) Learned1 = passive;
        else if (Learned2 == SpeciesPassive.None) Learned2 = passive;
        else                                      return false;

        return true;
    }

    public static readonly SummonDeckSlot Empty = new(SummonKind.Monster, string.Empty, 0);
}

public class SummonDeckData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.SummonDeck;

    readonly List<SummonDeckSlot> _slots = new();

    /// <summary>카드가 늘거나 레벨이 올랐다. 하단 카드 바가 구독한다.</summary>
    public event Action Changed;

    /// <summary>칸 수 (빈 칸 포함).</summary>
    public int SlotCount => _slots.Count;

    public SummonDeckSlot GetSlot(int index) => _slots[index];

    public IReadOnlyList<SummonDeckSlot> Slots => _slots;

    // ── 칸 관리 ──────────────────────────────────────────────

    /// <summary>
    /// 카드 바 크기를 소환사의 칸 수에 맞춘다. 런 시작 시 한 번 부른다.
    /// 줄어들면 뒤쪽 칸이 잘린다.
    /// </summary>
    public void ResizeTo(int slotCount)
    {
        while (_slots.Count < slotCount) _slots.Add(SummonDeckSlot.Empty);
        while (_slots.Count > slotCount) _slots.RemoveAt(_slots.Count - 1);

        Changed?.Invoke();
    }

    /// <summary>
    /// 빈 칸 하나를 없앤다 (뒤에서부터) — 특성 '봉인된 칸'.
    /// ⚠ 빈 칸이 없으면 false — 카드를 버리지 않는다. ResizeTo 는 뒤 칸을 카드째 자르므로 쓰지 않는다.
    /// </summary>
    public bool RemoveEmptySlot()
    {
        for (int i = _slots.Count - 1; i >= 0; i--)
        {
            if (!_slots[i].IsEmpty) continue;

            _slots.RemoveAt(i);
            Changed?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>그 카드가 들어 있는 칸 번호. 없으면 -1.</summary>
    public int IndexOf(string id)
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i].Id == id) return i;

        return -1;
    }

    public bool Contains(string id) => IndexOf(id) >= 0;

    /// <summary>비어 있는 칸이 하나라도 있는가. 보상 후보를 고를 때 본다.</summary>
    public bool HasFreeSlot => FirstFreeSlot >= 0;

    /// <summary>
    /// 비어 있는 칸 수.
    ///
    /// 한 번에 여러 장을 주는 자리(이벤트 '버려진 둥지')가 <b>고르기 전에</b> 본다 —
    /// ⚠ 자리가 모자란 채로 주면 Acquire 가 0 을 돌려주고 그 장은 사라진다.
    ///   대가는 이미 치른 뒤라 화면에는 "냈는데 덜 받았다" 로만 보인다.
    /// </summary>
    public int FreeSlotCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].IsEmpty) n++;

            return n;
        }
    }

    int FirstFreeSlot
    {
        get
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].IsEmpty) return i;

            return -1;
        }
    }

    // ── 카드 획득 ────────────────────────────────────────────

    /// <summary>
    /// 카드를 한 장 얻는다. 처음이면 빈 칸에 놓고, 이미 있으면 장수를 올린다.
    ///
    /// 돌려주는 값은 **획득 후 레벨**이다 (0 = 넣을 자리가 없어 실패).
    /// 보상 화면이 "새 카드" 와 "레벨업" 을 구분해 보여 주는 데 쓴다.
    ///
    /// ⚠ 만렙 카드도 중복을 계속 먹는다
    ///   장수는 계속 오르지만 레벨은 만렙에서 멈춘다. 남는 장수를 버리지 않는
    ///   이유는, 나중에 "장수를 합성 비용으로 쓰는" 규칙을 넣을 여지를 남기기 위해서다.
    /// </summary>
    public int Acquire(SummonKind kind, string id)
    {
        int at = IndexOf(id);

        if (at >= 0)
        {
            SummonDeckSlot slot = _slots[at];
            slot.Copies++;
            _slots[at] = slot;

            Changed?.Invoke();
            return slot.Level;
        }

        int free = FirstFreeSlot;
        if (free < 0) return 0;   // 자리가 없다 — 부르는 쪽이 판단한다

        var fresh = new SummonDeckSlot(kind, id);

        // 특성 '속성' — 새 카드가 1레벨이 아니라 2레벨로 들어온다.
        // ⚠ 이미 갖고 있던 카드(위 분기)에는 걸리지 않는다. 거기까지 얹으면
        //   중복 한 장이 두 레벨을 올려 '중복 특화' 와 역할이 겹친다.
        // 특성 '속성' · 유물 '조기 성장' — 둘 다 시작 레벨을 올린다.
        // ⚠ 더해서 한 번에 잡는다. 따로 적용하면 둘 다 가졌을 때 뒤엣것이
        //   앞엣것을 덮어써 하나가 조용히 무효가 된다.
        int startLevel = 1
                       + (RunPerkRule.Has(RunPerk.QuickStudy)
                          ? RunPerkRule.QuickStudyCopies - 1 : 0)
                       + RelicTreeApplier.GetSystemInt(RelicSystemEffect.NewCardLevel);

        if (startLevel > 1)
            fresh.Copies = CardLevelRule.CopiesForLevel(startLevel);

        _slots[free] = fresh;

        Changed?.Invoke();
        return fresh.Level;
    }

    /// <summary>
    /// 강화소 — 그 칸의 소환 비용을 <paramref name="cut"/> 만큼 깎고
    /// 마릿수를 <paramref name="extra"/> 만큼 늘린다. 누적된다.
    ///
    /// ⚠ 비용 하한은 여기서 걸지 않는다
    ///   SummonerPerkRuntime.ManaCostFor 가 Max(1f) 로 한 번만 막는다.
    ///   두 곳에서 자르면 "깎았는데 안 깎인" 상태의 출처를 못 찾는다.
    /// </summary>
    public bool UpgradeCard(int index, int cut, int extra)
    {
        if (index < 0 || index >= _slots.Count) return false;

        SummonDeckSlot slot = _slots[index];
        if (slot.IsEmpty || slot.Kind != SummonKind.Monster) return false;

        slot.ManaDiscount += cut;
        slot.ExtraSummons += extra;
        _slots[index]      = slot;

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// 그 칸을 비운다 — 제단의 제물이 쓰는 문이다.
    ///
    /// ⚠ 강화(ManaDiscount·ExtraSummons)도 함께 사라진다. 그것이 대가다.
    /// ⚠ 뒤 칸을 당기지 않는다 — 칸 번호가 밀리면 다른 화면이 잡고 있던
    ///   인덱스가 엉뚱한 카드를 가리킨다 (분해 팝업이 ID 대신 칸 번호로
    ///   선택을 잡는 것과 같은 이유).
    /// </summary>
    public bool ClearSlot(int index)
    {
        if (index < 0 || index >= _slots.Count) return false;
        if (_slots[index].IsEmpty)              return false;

        _slots[index] = new SummonDeckSlot(SummonKind.Monster, string.Empty, 0);

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// 칸을 끌어 옮긴다 — 하단 카드 바의 드래그 앤 드롭이 부른다.
    ///
    /// ■ 순서가 곧 <b>전장에 나가는 순서</b>다 (사용자 확정, 2026-09-07)
    ///   대기열이 이 칸 번호 순으로 줄을 선다 (SummonReservation).
    ///   그래서 이건 보기 좋으라고 정리하는 기능이 아니라 진형을 짜는 조작이다.
    ///
    /// ■ 빼서 끼운다 — 맞바꾸지 않는다
    ///   맞바꾸면 3번 카드를 맨 앞으로 옮길 때 1번이 3번 자리로 날아간다.
    ///   끌어다 놓는 손짓의 뜻은 "이 사이에 넣어라" 지 "둘을 바꿔라" 가 아니다.
    ///
    /// ⚠ 빈 칸도 옮길 수 있다 — 앞쪽에 자리를 비워 두는 것도 배치다.
    /// </summary>
    /// <returns>실제로 자리가 바뀌었으면 true.</returns>
    public bool Move(int from, int to)
    {
        if (from < 0 || from >= _slots.Count) return false;
        if (to   < 0 || to   >= _slots.Count) return false;
        if (from == to)                       return false;

        SummonDeckSlot moving = _slots[from];
        _slots.RemoveAt(from);
        _slots.Insert(to, moving);

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// 시작 카드를 놓는다. Acquire 와 달리 **중복이면 아무것도 하지 않는다** —
    /// 소환사 정의에 같은 종족이 두 번 적혀 있다고 레벨을 주면 안 된다.
    /// </summary>
    public void PlaceStarter(SummonKind kind, string id)
    {
        if (Contains(id)) return;
        Acquire(kind, id);
    }

    // ── 진화 / 융합 ──────────────────────────────────────────

    /// <summary>
    /// 만렙 카드를 같은 계보의 상위 종족으로 <b>무작위</b> 진화시킨다.
    /// 모은 장수(레벨)는 그대로 넘어간다.
    ///
    /// 성공하면 바뀐 종족을, 실패하면 null 을 돌려준다.
    /// 판정의 정본은 CardEvolution 이다 — 여기서 조건을 다시 쓰지 말 것.
    ///
    /// ⚠ 진화하면 그 칸은 다시 진화하지 못한다
    ///   "진화냐 융합이냐" 를 고르게 만드는 것이 이 시스템의 전부다.
    ///   여기서 잠그지 않으면 계보 끝까지 자동으로 올라가 선택이 사라진다.
    /// </summary>
    public MonsterSpeciesData Evolve(int index)
    {
        SummonDeckSlot slot = _slots[index];

        if (!CardEvolution.CanEvolve(slot)) return null;

        MonsterSpeciesData next = CardEvolution.RollUpgrade(slot.Id);
        if (next == null) return null;

        // ⚠ 부모가 쌓아 둔 몫을 먼저 계산한다 — Copies 를 되돌리기 전에
        slot.InheritBonus += CardLevelRule.StatBonusRatio(slot.Level)
                           * CardEvolution.EvolveInheritShare;

        slot.Id         = next.Id;
        slot.HasEvolved = true;

        // ⚠ 레벨은 1 로 되돌린다 (사용자 확정, 2026-09-09 — InheritBonus 주석)
        //   만렙을 그대로 물려받으면 진화한 그 판에 또 만렙 카드가 되어
        //   진화·융합 창이 곧바로 다시 뜬다. 키우는 과정이 사라진다.
        slot.Copies = 1;

        _slots[index] = slot;

        Changed?.Invoke();
        return next;
    }

    /// <summary>
    /// 만렙 카드가 다른 카드를 먹고 그 종족 패시브를 배운다.
    /// 재료 칸은 비워지고, 그 자리는 <b>당기지 않는다</b> (카드 위치는 고정이다).
    ///
    /// ⚠ 융합한 카드는 더 이상 진화하지 못한다
    ///   진화와 융합 중 하나만 고르게 하는 규칙의 나머지 절반이다.
    /// </summary>
    public bool Fuse(int targetSlot, int materialSlot)
    {
        if (targetSlot == materialSlot) return false;

        SummonDeckSlot target   = _slots[targetSlot];
        SummonDeckSlot material = _slots[materialSlot];

        if (!CardEvolution.CanFuse(target))                     return false;
        if (!CardEvolution.CanBeMaterial(target, material))     return false;

        CardCatalog catalog = CardCatalog.Current;
        if (catalog == null) return false;

        SpeciesPassive learned =
            CardEvolution.PassiveFrom(catalog.GetMonster(material.Id));

        if (!target.TryLearn(learned)) return false;

        // ── 재료가 쌓아 둔 레벨 보너스의 일부가 넘어온다 ──
        //   ⚠ 정본은 CardEvolution.FuseInheritFrom 이다 (진화와 같은 칸에 쌓인다).
        //     여기서 다시 계산하지 말 것 — 융합 창이 미리 적어 주는 값과 갈린다.
        target.InheritBonus += CardEvolution.FuseInheritFrom(material);

        target.HasFused = true;

        _slots[targetSlot]   = target;
        _slots[materialSlot] = SummonDeckSlot.Empty;

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// 그 칸을 <b>지정한 상위 종족으로</b> 진화시킨다 — 카드 3택의 '진화' 후보가 쓴다.
    ///
    /// 무작위 진화(<see cref="Evolve"/>)와 <b>결과는 똑같다</b> —
    /// Lv1 로 되돌아가고, 부모가 쌓아 둔 레벨 보너스의 절반이 영구히 얹힌다.
    /// 다른 것은 "무엇이 될지" 를 누가 정하는가뿐이다.
    ///
    /// ⚠ 판정은 <see cref="CardEvolution.CanEvolveTo"/> 하나다 —
    ///   후보를 고르는 쪽과 조건이 갈리면 "골랐는데 아무 일도 안 난다" 가 된다.
    /// </summary>
    public MonsterSpeciesData EvolveTo(int index, MonsterSpeciesData target)
    {
        if (!CardEvolution.CanEvolveTo(this, index, target)) return null;

        SummonDeckSlot slot = _slots[index];

        // ⚠ 부모가 쌓아 둔 몫을 먼저 계산한다 — Copies 를 되돌리기 전에
        slot.InheritBonus += CardLevelRule.StatBonusRatio(slot.Level)
                           * CardEvolution.EvolveInheritShare;

        slot.Id         = target.Id;
        slot.HasEvolved = true;

        // ⚠ 레벨은 1 로 되돌린다 (Evolve 와 같은 규칙 — InheritBonus 주석 참고)
        slot.Copies = 1;

        _slots[index] = slot;

        Changed?.Invoke();
        return target;
    }

    /// <summary>
    /// 진화·융합 대신 <b>공/체만 영구히 얹는다</b> — 갈림길의 제3의 갈래.
    ///
    /// 종족도 레벨도 바뀌지 않고 재료도 잃지 않는다. 값의 정본은
    /// <see cref="CardEvolution.EmpowerBonus"/> 다 — 여기서 숫자를 적지 말 것.
    ///
    /// ⚠ 진화·융합과 같은 칸(InheritBonus)에 쌓인다. 새 축을 만들지 말 것.
    /// </summary>
    public bool Empower(int index, float ratio)
    {
        SummonDeckSlot slot = _slots[index];

        if (slot.IsEmpty || slot.Kind != SummonKind.Monster) return false;
        if (ratio <= 0f)                                     return false;

        slot.InheritBonus += ratio;
        _slots[index]      = slot;

        Changed?.Invoke();
        return true;
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize()
    {
        var json = new SummonDeckJson();
        foreach (var slot in _slots)
        {
            var entry = new SummonDeckSlotJson
            {
                kind    = (int)slot.Kind,
                id      = slot.Id,
                copies  = slot.Copies,
                evolved = slot.HasEvolved,
                fused   = slot.HasFused,
                manaCut = slot.ManaDiscount,
                extra   = slot.ExtraSummons,
                inherit = slot.InheritBonus,
            };

            for (int i = 0; i < CardEvolution.MaxLearned; i++)
            {
                SpeciesPassive learned = slot.GetLearned(i);
                if (learned != SpeciesPassive.None) entry.learned.Add((int)learned);
            }

            json.slots.Add(entry);
        }

        return JsonUtility.ToJson(json);
    }

    public void Deserialize(string json)
    {
        var data = JsonUtility.FromJson<SummonDeckJson>(json);

        _slots.Clear();
        foreach (var s in data.slots)
        {
            // 옛 세이브에는 copies 가 없다 — 0 이면 1장으로 본다.
            int copies = s.copies > 0 ? s.copies : (string.IsNullOrEmpty(s.id) ? 0 : 1);

            var slot = new SummonDeckSlot((SummonKind)s.kind, s.id, copies)
            {
                HasEvolved   = s.evolved,
                HasFused     = s.fused,
                ManaDiscount = s.manaCut,
                ExtraSummons = s.extra,
                InheritBonus = s.inherit,
            };

            if (s.learned != null)
                foreach (int value in s.learned)
                    slot.TryLearn((SpeciesPassive)value);

            _slots.Add(slot);
        }

        Changed?.Invoke();
    }

    public void SetDefaults()
    {
        _slots.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// 런이 끝났다 — 손에 든 카드를 통째로 버린다.
    ///
    /// ⚠ 진화·융합 결과가 남으면 안 된다 (사용자 확정 규칙)
    ///   다음 런은 소환사의 시작 카드에서 **기본 패시브만** 갖고 시작한다.
    ///   여기서 지우지 않으면 지난 런에서 신속을 먹인 슬라임이 그대로 따라와,
    ///   런을 거듭할수록 시작이 세지는 구조가 된다.
    ///
    /// SetDefaults 와 하는 일은 같지만 부르는 뜻이 다르다 —
    /// 그쪽은 "신규 세이브", 이쪽은 "런 종료" 다. 나중에 한쪽만 달라질 때
    /// 이름이 갈려 있어야 어디를 고칠지 알 수 있다.
    /// </summary>
    public void ResetForNewRun()
    {
        _slots.Clear();
        Changed?.Invoke();
    }
}
