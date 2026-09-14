using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MonsterGearRule.cs
//  몬스터 장비의 **규칙 정본** — 칸 수 · 스탯 적용 · 외형 · 불빛.
//
//  ■ ⚠ 칸 수를 정하는 것은 <b>몬스터의 품질</b>이다 (사용자 확정, 2026-09-06)
//    장비 자신의 등급이 아니다. 품질을 올릴 이유가 하나 더 생기고
//    (스탯 +10% 에 더해 칸이 열린다), 품질 개선이 장비 수집과 맞물린다.
//    ⚠ 종족 패시브 슬롯과 <b>같은 표</b>를 쓴다 (CLAUDE.md 스킬 항목).
//      두 표가 갈리면 "패시브는 2칸인데 장비는 1칸" 같은 설명 못 할 상태가 된다.
//
//  ■ 스탯은 절대값이다 (사용자 확정)
//    UnitStat.Add 로 그냥 더한다. 용사 장비(EquipmentApplier)와 같은 방식이다.
//    ⚠ 합성의 **맨 마지막**에 얹는다 (MonsterStatComposer ⑦)
//      앞에 두면 카드 레벨·시너지·특성의 비율 항목이 장비 몫까지 곱해
//      "장비를 끼면 레벨 보너스도 같이 커지는" 이중 계산이 된다.
//      절대값은 절대값이어야 한다.
//
//  ■ 외형은 부위(Part)가 정한다
//    인간형 : CharacterBuilder 슬롯을 갈아 끼운다 → 정말로 다른 모습이 된다
//    비인간형 : 레이어가 없다. 색조(Hide) · 덩치(Bulk) · 불빛(Charm)으로 표현한다
//    ⚠ 부위가 한 몬스터에 하나뿐이라 셋이 서로 싸우지 않는다
//      (같은 부위를 또 끼우면 먼저 낀 것이 벗겨진다 — MonsterGearInventory)
//
//  ■ 불빛은 등급이 정한다
//    AuraMinGrade 이상인 장비 하나당 빛 하나가 몸 주위를 돈다
//    (MonsterGearAuraView). 낮은 등급은 숫자와 색조·덩치로만 티가 난다 —
//    첫 장비부터 빛나면 "좋은 걸 꼈다" 는 신호가 죽는다.
// ============================================================

public static class MonsterGearRule
{
    // ── 칸 수 ────────────────────────────────────────────────

    /// <summary>한 몬스터가 걸칠 수 있는 최대 장비 수 (1~3).</summary>
    public const int MaxSlots = 3;

    /// <summary>
    /// 그 품질에서 열려 있는 칸 수.
    /// ⚠ 종족 패시브 슬롯과 같은 표다 — 한쪽만 고치지 말 것.
    /// </summary>
    public static int SlotsFor(UnitGrade grade) => grade switch
    {
        UnitGrade.Epic                       => 3,
        UnitGrade.Rare or UnitGrade.Unique   => 2,
        _                                    => 1,
    };

    /// <summary>그 칸이 열리려면 필요한 품질. 잠긴 칸에 "희귀부터" 라고 적을 때 쓴다.</summary>
    public static UnitGrade GradeToOpen(int slotIndex) => slotIndex switch
    {
        0 => UnitGrade.Normal,
        1 => UnitGrade.Rare,
        _ => UnitGrade.Epic,
    };

    /// <summary>그 종족이 지금 쓸 수 있는 칸 수. 해금 안 된 종족은 0.</summary>
    public static int SlotsOf(string speciesId)
    {
        var codex = UserDataManager.Instance?.Get<MonsterCodexData>();
        if (codex == null || !codex.IsUnlocked(speciesId)) return 0;

        return SlotsFor(codex.GetGrade(speciesId));
    }

    // ── 불빛 ─────────────────────────────────────────────────

    /// <summary>이 등급부터 몸 주위에 빛이 돈다.</summary>
    public const UnitGrade AuraMinGrade = UnitGrade.Rare;

    // ── 스탯 ─────────────────────────────────────────────────

    /// <summary>UnitStat 레이어 키. 슬롯마다 달라야 교체 시 옛 몫이 남지 않는다.</summary>
    static string SlotKey(int slot) => $"gear_{slot}";

    /// <summary>비율 항목이 얹히는 레이어. 슬롯이 아니라 한 덩어리다 — 비율끼리는 더해서 한 번 곱한다.</summary>
    const string PctKey = "gear_pct";

    static readonly List<GearOption> OptBuffer = new(12);

    /// <summary>
    /// 그 종족이 낀 장비의 능력치를 전부 얹는다 — 레벨·강화까지.
    /// <b>MonsterStatComposer 의 맨 마지막</b>에서만 부른다 (파일 머리 주석).
    ///
    /// ■ ⚠ 비율이 먼저, 절대값이 나중이다
    ///   "체력 +5%" 가 "체력 +40" 까지 곱하면 절대값이 절대값이 아니게 된다.
    ///   비율은 ⑥ 까지 합성된 값에 곱해 더하고, 그 뒤에 절대값을 더한다.
    ///
    /// ■ 강화(Lv5 뒤)는 공격력·체력 비율로 들어간다 (MonsterGearLevelRule.EnhancePct)
    ///
    /// ⚠ 유물 '맞춤 제작'(GearStatBonus)은 **절대값(체력·공격력·방어율)에만** 곱한다
    ///   원래 그 셋만 있던 시절의 유물이다. 비율·치명타까지 키우면 한 노드가
    ///   장비의 모든 축을 동시에 올린다.
    ///
    /// ⚠ 카드 비용·넉백은 여기서 안 다룬다 — 스탯이 아니다
    ///   ManaCutFor / KnockbackBonusFor 가 따로 읽는다.
    /// </summary>
    public static void ApplyStats(UnitStat stat, string speciesId)
    {
        var inv = UserDataManager.Instance?.Get<MonsterGearInventory>();
        var db  = MonsterGearDatabase.Current;
        if (inv == null || db == null) return;

        IReadOnlyList<string> ids = inv.EquippedOn(speciesId);
        if (ids.Count == 0) return;

        int   open    = SlotsOf(speciesId);
        float absMult = 1f + RelicTreeApplier.GetSystemValue(RelicSystemEffect.GearStatBonus);

        // ── ① 비율 — 모든 칸의 몫을 더해 한 번에 곱한다 ──
        float hpPct = 0f, atkPct = 0f, aspdPct = 0f, movePct = 0f;

        for (int i = 0; i < ids.Count && i < open; i++)
        {
            MonsterGearData gear = db.Get(ids[i]);
            if (gear == null) continue;

            float enh = MonsterGearLevelRule.EnhancePct(inv.EnhanceOf(gear.Id));
            hpPct  += enh;
            atkPct += enh;

            gear.CollectUpTo(inv.LevelOf(gear.Id), OptBuffer);
            foreach (var o in OptBuffer)
            {
                switch (o.Stat)
                {
                    case GearStat.HpPct:       hpPct   += o.Value; break;
                    case GearStat.AttackPct:   atkPct  += o.Value; break;
                    case GearStat.AttackSpeed: aspdPct += o.Value; break;
                    case GearStat.MoveSpeed:   movePct += o.Value; break;
                }
            }
        }

        // ── ①-b 단계 패시브(공격력 증가 I …) — 비율 계열은 여기에 합친다 ──
        //   ⚠ 같은 단계(I + I)는 한 번만, 다른 단계(I + III)는 둘 다 (사용자 확정).
        //     PassiveResolver 와 같은 규칙이다 — 거기서도 단계 패시브는 이름 그대로 한 줄씩 남는다.
        CollectPassives(speciesId, TierBuffer);
        DistinctTiers(TierBuffer);

        foreach (SpeciesPassive p in TierBuffer)
        {
            GearTierPassive.TryGet(p, out TierFamily f, out int t);
            float v = GearTierPassive.ValueOf(f, t);

            switch (f)
            {
                case TierFamily.Hp:          hpPct   += v; break;
                case TierFamily.Attack:      atkPct  += v; break;
                case TierFamily.AttackSpeed: aspdPct += v; break;
            }
        }

        AddPct(stat, StatType.MaxHp,       hpPct);
        AddPct(stat, StatType.Attack,      atkPct);
        AddPct(stat, StatType.AttackSpeed, aspdPct);
        AddPct(stat, StatType.MoveSpeed,   movePct);

        // 단계 패시브의 %p 계열 — 한 레이어에
        foreach (SpeciesPassive p in TierBuffer)
        {
            GearTierPassive.TryGet(p, out TierFamily f, out int t);
            float v = GearTierPassive.ValueOf(f, t);

            switch (f)
            {
                case TierFamily.Defense:    stat.Add(StatType.Defense,             v, TierKey); break;
                case TierFamily.Crit:       stat.Add(StatType.CritChance,          v, TierKey); break;
                case TierFamily.CritDamage: stat.Add(StatType.CritDamage,          v, TierKey); break;
                case TierFamily.Cooldown:   stat.Add(StatType.SkillCooldownReduce, v, TierKey); break;
                case TierFamily.Pierce:     stat.Add(StatType.DefensePenetration,  v, TierKey); break;
            }
        }

        // ── ② 절대값·%p — 칸마다 제 레이어에 ──
        for (int i = 0; i < ids.Count && i < open; i++)
        {
            MonsterGearData gear = db.Get(ids[i]);
            if (gear == null) continue;

            string key = SlotKey(i);

            gear.CollectUpTo(inv.LevelOf(gear.Id), OptBuffer);
            foreach (var o in OptBuffer)
            {
                switch (o.Stat)
                {
                    case GearStat.Hp:          stat.Add(StatType.MaxHp,              o.Value * absMult, key); break;
                    case GearStat.Attack:      stat.Add(StatType.Attack,             o.Value * absMult, key); break;
                    case GearStat.Defense:     stat.Add(StatType.Defense,            o.Value * absMult, key); break;
                    case GearStat.CritChance:  stat.Add(StatType.CritChance,         o.Value, key); break;
                    case GearStat.CritDamage:  stat.Add(StatType.CritDamage,         o.Value, key); break;
                    case GearStat.Cooldown:    stat.Add(StatType.SkillCooldownReduce, o.Value, key); break;
                    case GearStat.Penetration: stat.Add(StatType.DefensePenetration, o.Value, key); break;
                    case GearStat.Range:       stat.Add(StatType.AttackRange,        o.Value, key); break;
                }
            }
        }
    }

    const string TierKey = "gear_tier";

    static readonly List<SpeciesPassive> TierBuffer = new(8);

    /// <summary>단계 패시브만 남기고, 같은 것은 하나로.</summary>
    static void DistinctTiers(List<SpeciesPassive> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!GearTierPassive.IsTier(list[i]) || list.IndexOf(list[i]) != i)
                list.RemoveAt(i);
        }
    }

    /// <summary>
    /// 그 종족이 낀 장비가 여는 패시브 — Lv4·Lv5 에 닿은 것만, 열린 칸만.
    /// 중복·각성은 여기서 하지 않는다 — PassiveResolver 가 선천·융합과 함께 한 번에 한다.
    /// </summary>
    public static void CollectPassives(string speciesId, List<SpeciesPassive> into)
    {
        into.Clear();

        var inv = UserDataManager.Instance?.Get<MonsterGearInventory>();
        var db  = MonsterGearDatabase.Current;
        if (inv == null || db == null) return;

        IReadOnlyList<string> ids = inv.EquippedOn(speciesId);
        int open = SlotsOf(speciesId);

        for (int i = 0; i < ids.Count && i < open; i++)
        {
            MonsterGearData gear = db.Get(ids[i]);
            if (gear == null) continue;

            int lv = inv.LevelOf(gear.Id);
            for (int l = 4; l <= lv && l <= MonsterGearLevelRule.MaxLevel; l++)
            {
                SpeciesPassive p = gear.PassiveAt(l);
                if (p != SpeciesPassive.None) into.Add(p);
            }
        }
    }

    /// <summary>
    /// 지금 값에 비율만큼을 <b>더하는 레이어</b>로 얹는다.
    /// ⚠ Set(Get × 배율) 로 쓰지 않는다 — 기본 레이어 하나를 덮어쓰면 다른 레이어의
    ///   몫까지 기본으로 굳어 두 번 세어진다.
    /// </summary>
    static void AddPct(UnitStat stat, StatType type, float pct)
    {
        if (Mathf.Approximately(pct, 0f)) return;
        stat.Add(type, stat.Get(type) * pct, PctKey);
    }

    /// <summary>
    /// 그 종족의 카드 비용을 장비가 얼마나 깎는가 (정수).
    /// 적용은 SummonerPerkRuntime.ManaCostFor 한 곳이다 — 하한(1)도 거기서 건다.
    /// </summary>
    public static int ManaCutFor(string speciesId) => Mathf.RoundToInt(SumOf(speciesId, GearStat.ManaCost));

    /// <summary>그 종족의 평타 넉백 가산 비율 (0.2 = +20%). MonsterSpawner 가 굽는다.</summary>
    public static float KnockbackBonusFor(string speciesId) => SumOf(speciesId, GearStat.Knockback);

    /// <summary>열린 칸의 장비가 그 종류에 주는 값의 합 — 레벨까지 반영한다.</summary>
    static float SumOf(string speciesId, GearStat type)
    {
        var inv = UserDataManager.Instance?.Get<MonsterGearInventory>();
        var db  = MonsterGearDatabase.Current;
        if (inv == null || db == null) return 0f;

        IReadOnlyList<string> ids = inv.EquippedOn(speciesId);
        if (ids.Count == 0) return 0f;

        int   open = SlotsOf(speciesId);
        float sum  = 0f;

        for (int i = 0; i < ids.Count && i < open; i++)
        {
            MonsterGearData gear = db.Get(ids[i]);
            if (gear == null) continue;

            gear.CollectUpTo(inv.LevelOf(gear.Id), OptBuffer);
            foreach (var o in OptBuffer)
                if (o.Stat == type) sum += o.Value;
        }
        return sum;
    }

    // ── 외형 ─────────────────────────────────────────────────

    /// <summary>
    /// 그 종족이 낀 장비가 만들어 내는 겉모습 한 벌.
    ///
    /// ⚠ 스폰마다 만든다 — 캐시하지 않는다
    ///   장착은 도감에서 언제든 바뀌고, 필드의 몬스터는 라인 복귀로 계속
    ///   다시 나온다. 캐시하면 "장비를 바꿨는데 다음 판까지 옛 모습" 이 된다.
    ///   대신 <see cref="MonsterGearVisual.Key"/> 로 외형 합성 자체는 건너뛴다.
    /// </summary>
    public static MonsterGearVisual BuildVisual(string speciesId)
    {
        var visual = MonsterGearVisual.None;

        var inv = UserDataManager.Instance?.Get<MonsterGearInventory>();
        var db  = MonsterGearDatabase.Current;
        if (inv == null || db == null) return visual;

        IReadOnlyList<string> ids = inv.EquippedOn(speciesId);
        if (ids.Count == 0) return visual;

        int open = SlotsOf(speciesId);
        var key  = new System.Text.StringBuilder(48);

        for (int i = 0; i < ids.Count && i < open; i++)
        {
            MonsterGearData gear = db.Get(ids[i]);
            if (gear == null) continue;

            key.Append(gear.Id).Append('|');

            // ── 비인간형은 **도는 점 하나**가 전부다 (사용자 지시, 2026-09-12) ──
            //   예전엔 가죽=몸 색조 · 덩치=크기로 입혔다. 색조는 시트 색에 곱해져 크게 못 바꾸면서
            //   그림만 탁하게 만들었고, 셋을 겹쳐 끼면 색·크기가 뒤섞여 이상해 보였다.
            //   그래서 비인간형 장비는 등급과 무관하게 장비 하나당 점 하나(등급색)만 붙인다.
            //   ⚠ Tint·ScaleBonus 는 흰색·0 그대로 남는다 — 읽는 쪽(전장·초상화)은 손대지 않았다.
            if (gear.Body == MonsterGearBody.NonHumanoid)
            {
                AddAura(ref visual, gear);
                continue;
            }

            // 인간형 불빛 — 등급이 문턱을 넘은 장비마다 하나씩 (겉모습은 레이어가 말한다).
            if (gear.Grade >= AuraMinGrade) AddAura(ref visual, gear);

            // ── 인간형 — CharacterBuilder 슬롯을 갈아 끼운다 ──
            switch (gear.Part)
            {
                case MonsterGearPart.Armor:  visual.Armor  = gear.AppearanceName; break;
                case MonsterGearPart.Helmet: visual.Helmet = gear.AppearanceName; break;
                case MonsterGearPart.Shield: visual.Shield = gear.AppearanceName; break;
                case MonsterGearPart.Cape:   visual.Cape   = gear.AppearanceName; break;
                case MonsterGearPart.Back:   visual.Back   = gear.AppearanceName; break;
            }
        }

        visual.Key = key.ToString();
        return visual;
    }

    static void AddAura(ref MonsterGearVisual visual, MonsterGearData gear)
    {
        if (visual.AuraCount >= MaxSlots) return;
        visual.AuraColors[visual.AuraCount] = GradeStyle.GetColor(gear.Grade);
        visual.AuraCount++;
    }
}

// ============================================================
//  MonsterGearVisual
//  장비가 만들어 내는 겉모습 한 벌. 순수 값이라 스폰 경로를 그냥 타고 다닌다.
//
//  ⚠ Key 가 외형 캐시의 열쇠다
//    UnitAppearanceBridge 는 (종족, 이름) 이 같으면 합성을 건너뛴다. 장비가
//    바뀌어도 그 둘은 그대로라, Key 를 함께 보지 않으면 **장비를 바꿔도
//    겉모습이 그대로**다. 빈 문자열 = 맨몸.
// ============================================================

public struct MonsterGearVisual
{
    public string Armor, Helmet, Shield, Cape, Back;   // 빈 문자열 = 그 칸은 건드리지 않는다

    public Color Tint;         // 비인간형 몸 색조. 흰색 = 원래 색
    public float ScaleBonus;   // 비인간형 덩치 가산 (0.06 = +6%)

    public int     AuraCount;
    public Color[] AuraColors;

    public string Key;

    /// <summary>맨몸. ⚠ 배열을 새로 만들어 돌려준다 — 공유하면 서로 덮어쓴다.</summary>
    public static MonsterGearVisual None => new()
    {
        Armor = "", Helmet = "", Shield = "", Cape = "", Back = "",
        Tint       = Color.white,
        ScaleBonus = 0f,
        AuraCount  = 0,
        AuraColors = new Color[MonsterGearRule.MaxSlots],
        Key        = "",
    };

    public bool IsEmpty => string.IsNullOrEmpty(Key);
}
