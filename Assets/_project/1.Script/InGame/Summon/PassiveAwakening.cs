using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  PassiveAwakening.cs
//  몬스터 패시브의 **최종 목록을 만드는 단 한 곳** — 출처 셋을 합치고,
//  겹친 것을 지우고, 둘이 모인 것은 각성판으로 바꿔 끼운다.
//
//  ■ 출처는 셋이다
//    선천(계보) · 융합으로 배운 것(카드) · 장비(Lv4·Lv5)
//    셋이 따로 흐르면 "도감엔 보이는데 전투엔 없는" 패시브가 반드시 생긴다.
//    스폰(MonsterSpawner) · 사망(MonsterDeathWatcher) · 화면(MonsterDetailPopup)이
//    **전부 이 함수 하나**를 부른다.
//
//  ■ 규칙 (사용자 확정, 2026-09-10)
//    ① 같은 패시브는 한 번만 — 둘째부터는 무시한다
//    ② 단, 각성표(Table)에 있는 패시브가 **둘 이상** 모이면 각성판 하나로 바뀐다
//       — 원래 것은 목록에서 사라진다(각성판이 상위 호환이라 잃는 것이 없다)
//    ③ 단계 패시브(공격력 I · III)는 서로 **다른 패시브**다 — 둘 다 남는다.
//       같은 단계(I + I)만 ①로 하나가 된다. 단계 패시브는 각성하지 않는다
//
//  ■ ⚠ 결과는 **그때그때 다시 계산한다** — 저장하지 않는다
//    장비를 벗기면 각성이 풀리고 원래 패시브가 돌아와야 한다. 각성 여부를
//    어딘가에 기록해 두면 벗긴 뒤에도 남는 길이 생긴다. 출처만 저장하고
//    결과는 매번 이 함수가 만든다 — 그러면 "벗겼는데 남아 있다" 가 구조적으로 없다.
//    (필드의 몬스터는 스폰 때 받은 목록을 들고 있다 — 런 중에는 장비를 못 바꾸므로
//     CodexEditLock 이 그 사이의 어긋남을 막는다)
// ============================================================

/// <summary>패시브가 어디서 왔는가. 여럿이 겹칠 수 있다.</summary>
[System.Flags]
public enum PassiveOrigin : byte
{
    None    = 0,
    Innate  = 1,   // 종족 선천 (계보)
    Learned = 2,   // 융합으로 배운 것
    Gear    = 4,   // 장비 Lv4·Lv5
}

/// <summary>최종 목록의 한 줄.</summary>
public readonly struct ResolvedPassive
{
    public readonly SpeciesPassive Passive;
    public readonly PassiveOrigin  Origins;

    /// <summary>각성판이면 재료가 된 원래 패시브. 아니면 None.</summary>
    public readonly SpeciesPassive Base;

    public bool IsAwakened => Base != SpeciesPassive.None;

    public ResolvedPassive(SpeciesPassive passive, PassiveOrigin origins, SpeciesPassive basePassive)
    {
        Passive = passive; Origins = origins; Base = basePassive;
    }
}

public static class PassiveAwakening
{
    /// <summary>
    /// 각성표 — 원래 패시브 둘 → 각성판 하나.
    /// ⚠ 각성판은 반드시 원래 것의 상위 호환이어야 한다 (SpeciesPassiveRule 의 나란한 상수 참고).
    /// </summary>
    public static readonly (SpeciesPassive From, SpeciesPassive To)[] Table =
    {
        (SpeciesPassive.SplitOnDeath,   SpeciesPassive.GreatSplit),
        (SpeciesPassive.Reassemble,     SpeciesPassive.Undying),
        (SpeciesPassive.Loot,           SpeciesPassive.GoldRush),
        (SpeciesPassive.PlagueBurst,    SpeciesPassive.Pandemic),
        (SpeciesPassive.Bloodlust,      SpeciesPassive.Berserker),
        (SpeciesPassive.PackHunt,       SpeciesPassive.Alpha),
        (SpeciesPassive.Sturdy,         SpeciesPassive.Colossus),
        (SpeciesPassive.SoulDrain,      SpeciesPassive.Vampire),
        (SpeciesPassive.Regrow,         SpeciesPassive.TrollBlood),
        (SpeciesPassive.HealOnDeath,    SpeciesPassive.LifeSeed),
        (SpeciesPassive.PoisonOnHit,    SpeciesPassive.Venom),
        (SpeciesPassive.ThornOnHit,     SpeciesPassive.IronThorns),
        (SpeciesPassive.ChillOnHit,     SpeciesPassive.Frostbite),
        (SpeciesPassive.Bulwark,        SpeciesPassive.Fortress),
        (SpeciesPassive.RallyOnDeath,   SpeciesPassive.WarDrum),
        (SpeciesPassive.ExplodeOnDeath, SpeciesPassive.Cataclysm),
        (SpeciesPassive.Volley,         SpeciesPassive.Barrage),
        (SpeciesPassive.BurnOnAttack,   SpeciesPassive.Hellfire),
        (SpeciesPassive.Swiftness,      SpeciesPassive.Gale),
    };

    /// <summary>그 패시브가 둘 모이면 무엇이 되는가. 각성이 없으면 None.</summary>
    public static SpeciesPassive AwakenedOf(SpeciesPassive p)
    {
        for (int i = 0; i < Table.Length; i++)
            if (Table[i].From == p) return Table[i].To;
        return SpeciesPassive.None;
    }

    /// <summary>각성판의 재료. 각성판이 아니면 None.</summary>
    public static SpeciesPassive BaseOf(SpeciesPassive awakened)
    {
        for (int i = 0; i < Table.Length; i++)
            if (Table[i].To == awakened) return Table[i].From;
        return SpeciesPassive.None;
    }

    public static bool IsAwakened(SpeciesPassive p) => BaseOf(p) != SpeciesPassive.None;

    /// <summary>"대분열 (분열 + 분열)" — 화면이 쓰는 이름표. 각성판이 아니면 이름만.</summary>
    public static string LabelOf(SpeciesPassive p)
    {
        SpeciesPassive b = BaseOf(p);
        if (b == SpeciesPassive.None) return p.ToKorean();

        string n = b.ToKorean();
        return $"{p.ToKorean()} ({n} + {n})";
    }
}

// ============================================================
//  GearTierPassive — 단계 패시브(공격력 증가 I·II·III …)의 값 정본
// ============================================================

public enum TierFamily
{
    Hp, Attack, Defense, Crit, CritDamage, Cooldown, Pierce, AttackSpeed,
}

public static class GearTierPassive
{
    const int First = (int)SpeciesPassive.HpUp1;
    const int Last  = (int)SpeciesPassive.AttackSpeedUp3;

    /// <summary>단계 패시브면 계열과 단계(1~3)를 돌려준다.</summary>
    public static bool TryGet(SpeciesPassive p, out TierFamily family, out int tier)
    {
        int v = (int)p;
        if (v < First || v > Last)
        {
            family = default; tier = 0;
            return false;
        }

        family = (TierFamily)((v - First) / 3);
        tier   = (v - First) % 3 + 1;
        return true;
    }

    public static bool IsTier(SpeciesPassive p) => TryGet(p, out _, out _);

    /// <summary>
    /// 그 계열·단계의 값. 비율(0.05 = 5%) · 방어율·치명타·관통은 %p.
    /// ⚠ 적용은 MonsterGearRule.ApplyStats 한 곳이다.
    /// </summary>
    public static float ValueOf(TierFamily family, int tier)
    {
        int i = Mathf.Clamp(tier, 1, 3) - 1;
        return family switch
        {
            TierFamily.Hp          => new[] { 0.05f, 0.08f, 0.12f }[i],
            TierFamily.Attack      => new[] { 0.04f, 0.07f, 0.10f }[i],
            TierFamily.Defense     => new[] { 0.02f, 0.04f, 0.06f }[i],
            TierFamily.Crit        => new[] { 0.04f, 0.07f, 0.10f }[i],
            TierFamily.CritDamage  => new[] { 0.15f, 0.25f, 0.40f }[i],
            TierFamily.Cooldown    => new[] { 0.05f, 0.08f, 0.12f }[i],
            TierFamily.Pierce      => new[] { 0.03f, 0.05f, 0.08f }[i],
            _                      => new[] { 0.04f, 0.07f, 0.10f }[i],   // AttackSpeed
        };
    }

    static string FamilyName(TierFamily f) => f switch
    {
        TierFamily.Hp          => "체력 증가",
        TierFamily.Attack      => "공격력 증가",
        TierFamily.Defense     => "방어율 증가",
        TierFamily.Crit        => "치명타 확률",
        TierFamily.CritDamage  => "치명타 피해",
        TierFamily.Cooldown    => "쿨타임 감소",
        TierFamily.Pierce      => "방어 관통",
        _                      => "공격 속도",
    };

    static string StatName(TierFamily f) => f switch
    {
        TierFamily.Hp          => "최대 체력",
        TierFamily.Attack      => "공격력",
        TierFamily.Defense     => "방어율",
        TierFamily.Crit        => "치명타 확률",
        TierFamily.CritDamage  => "치명타 피해",
        TierFamily.Cooldown    => "스킬 쿨타임",
        TierFamily.Pierce      => "방어 관통",
        _                      => "공격 속도",
    };

    static string Roman(int tier) => tier switch { 1 => "I", 2 => "II", _ => "III" };

    public static string NameOf(SpeciesPassive p)
        => TryGet(p, out var f, out int t) ? $"{FamilyName(f)} {Roman(t)}" : "";

    public static string DescribeOf(SpeciesPassive p)
    {
        if (!TryGet(p, out var f, out int t)) return "";

        float pct  = ValueOf(f, t) * 100f;
        string sign = f == TierFamily.Cooldown ? "-" : "+";
        return $"{StatName(f)} {sign}{pct:0.#}%";
    }
}

// ============================================================
//  PassiveResolver
// ============================================================

public static class PassiveResolver
{
    // 메인 스레드 전용 재사용 버퍼 — 스폰마다 새로 만들지 않는다
    static readonly List<SpeciesPassive> _innate  = new(8);
    static readonly List<SpeciesPassive> _learned = new(4);
    static readonly List<SpeciesPassive> _gear    = new(8);

    static readonly List<SpeciesPassive> _order   = new(16);
    static readonly List<int>            _counts  = new(16);
    static readonly List<PassiveOrigin>  _origins = new(16);

    static readonly List<ResolvedPassive> _scratch = new(16);

    /// <summary>
    /// 핵심 — 세 목록을 합쳐 최종 목록을 만든다. 순수 함수다 (세이브를 읽지 않는다).
    ///
    /// 순서: <b>각성판이 맨 앞</b>, 그 뒤는 처음 나온 순서 (선천 → 융합 → 장비).
    /// ⚠ 선천의 순서(뿌리 먼저)를 지킨다 — 분열이 회복보다 먼저 돌아야
    ///   "나뉜 뒤에 회복" 이 된다 (MonsterSpeciesData.CollectSpeciesPassives).
    /// </summary>
    public static void Resolve(List<SpeciesPassive> innate, List<SpeciesPassive> learned,
                               List<SpeciesPassive> gear, List<ResolvedPassive> into)
    {
        into.Clear();
        _order.Clear(); _counts.Clear(); _origins.Clear();

        Tally(innate,  PassiveOrigin.Innate);
        Tally(learned, PassiveOrigin.Learned);
        Tally(gear,    PassiveOrigin.Gear);

        // 각성판 먼저
        for (int i = 0; i < _order.Count; i++)
        {
            SpeciesPassive awakened = _counts[i] >= 2 ? PassiveAwakening.AwakenedOf(_order[i])
                                                      : SpeciesPassive.None;
            if (awakened == SpeciesPassive.None) continue;

            into.Add(new ResolvedPassive(awakened, _origins[i], _order[i]));
        }

        // 나머지 — 각성으로 바뀐 것은 빠진다 (원래 것이 사라지고 각성판이 선다)
        for (int i = 0; i < _order.Count; i++)
        {
            bool awakened = _counts[i] >= 2 &&
                            PassiveAwakening.AwakenedOf(_order[i]) != SpeciesPassive.None;
            if (awakened) continue;

            into.Add(new ResolvedPassive(_order[i], _origins[i], SpeciesPassive.None));
        }
    }

    static void Tally(List<SpeciesPassive> source, PassiveOrigin origin)
    {
        if (source == null) return;

        foreach (SpeciesPassive p in source)
        {
            if (p == SpeciesPassive.None) continue;

            int at = _order.IndexOf(p);
            if (at < 0)
            {
                _order.Add(p);
                _counts.Add(1);
                _origins.Add(origin);
            }
            else
            {
                _counts[at]++;
                _origins[at] |= origin;
            }
        }
    }

    /// <summary>
    /// 이 종족 · 이 카드의 최종 목록. 장비는 도감에 끼워 둔 것을 지금 읽는다.
    /// 카드가 없으면(도감에서 여는 경우) SummonDeckSlot.Empty 를 넘긴다 — 융합이 없는 것과 같다.
    /// </summary>
    public static void ResolveFor(MonsterSpeciesData species, in SummonDeckSlot card,
                                  List<ResolvedPassive> into)
    {
        _innate.Clear(); _learned.Clear(); _gear.Clear();

        species.CollectSpeciesPassives(_innate);
        card.CollectLearned(_learned);
        MonsterGearRule.CollectPassives(species.Id, _gear);

        Resolve(_innate, _learned, _gear, into);
    }

    /// <summary>실행용 — 패시브만 담는다 (스폰·사망).</summary>
    public static void CollectFor(MonsterSpeciesData species, in SummonDeckSlot card,
                                  List<SpeciesPassive> into)
    {
        into.Clear();
        ResolveFor(species, card, _scratch);

        foreach (ResolvedPassive r in _scratch)
            into.Add(r.Passive);
    }
}
