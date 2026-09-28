using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

// ============================================================
//  CardStatsTracker.cs
//  "어느 카드가 얼마나 때렸나" 를 세는 곳.
//
//  ■ 왜 BattleStatsTracker 로는 안 되나
//    그쪽은 **장군 엔티티**를 키로 쓴다. 병사·스킬 피해를 소속 장군에게
//    귀속시키는 구조라, 이 게임처럼 "장군" 이 없는 판에서는 아무것도 담기지 않는다.
//    여기서는 키가 **카드 ID** 다 — 플레이어가 실제로 고르고 키우는 단위.
//
//  ■ 귀속 규칙 (BattleStatCollectorSystem 이 부른다)
//      몬스터 공격    → 그 몬스터의 종족 카드      (정확)
//      소환사 평타    → "소환사" 행                (정확)
//      스킬 카드 피해 → 시전 중인 스킬 카드        (아래 주의)
//
//  ■ ⚠ 스킬 카드 귀속은 '시전 창(window)' 으로 한다
//    스킬 피해는 전부 **소환사 엔티티** 이름으로 들어온다 — 스킬 실행기가
//    ctx.CasterEntity 를 공격자로 넘기기 때문이다. 그래서 엔티티만 봐서는
//    메테오인지 블리자드인지 구별할 수 없다.
//
//    대신 SkillCardCaster 가 시전 직전에 BeginCast(카드, 지속시간) 을 부르고,
//    그 창이 살아 있는 동안 들어온 스킬 피해를 그 카드에 귀속한다.
//      · 즉발 스킬(메테오·충격파) → 정확하다
//      · 장판 스킬(독지대·블리자드) → 지속시간만큼 창을 열어 두어 대부분 맞는다
//      · ⚠ 두 장판이 겹치면 나중에 깐 쪽으로 몰릴 수 있다
//    완전히 정확하게 하려면 스킬마다 전용 공격자 엔티티를 만들어야 하는데,
//    통계 하나 때문에 33종 스킬의 실행 규약을 바꿀 값어치는 없다고 봤다.
//
//  ■ 런 전체가 아니라 **이번 스테이지**를 센다
//    스테이지를 깰 때마다 보는 화면이라 그 판의 기여가 궁금한 것이다.
//    누적을 보고 싶어지면 그때 런 합계를 따로 들면 된다.
// ============================================================

/// <summary>카드 한 장의 전투 기여.</summary>
public class CardStatEntry
{
    /// <summary>카드 ID (몬스터 종족 Id · 스킬 카드 Id · 소환사는 SummonerKey).</summary>
    public string CardId;

    public SummonKind Kind;

    /// <summary>소환사 행인가. 카드가 아니라 본체다.</summary>
    public bool IsSummoner;

    public float DamageDealt;

    /// <summary>평타로 낸 피해. 세그먼트 바의 첫 칸이다.</summary>
    public float NormalDamage;

    /// <summary>스킬로 낸 피해(몬스터 고유 스킬 + 스킬 카드). 세그먼트 바의 셋째 칸.</summary>
    public float SkillDamage;

    /// <summary>
    /// 상태 이상(중독·화상·역병)이 낸 지속 피해. 세그먼트 바의 둘째 칸 (사용자 지시, 2026-09-12).
    ///
    /// ⚠ 평타·스킬과 따로 센다 — 도트는 <b>때린 순간이 아니라 그 뒤에</b> 들어와서,
    ///   합쳐 두면 "독 슬라임이 한 일" 이 평타 칸에 섞여 독 덱의 값어치를 읽을 수 없다.
    ///   판정은 HitType.Dot 하나다 (UnitStatusEffectSystem 이 그 종류로 넣는다).
    /// </summary>
    public float DotDamage;

    public float DamageTaken;
    public float HealingDone;
    public int   KillCount;

    /// <summary>이번 스테이지에 필드에 나온 개체 수 (스킬 카드는 시전 횟수).</summary>
    public int SpawnCount;
}

public class CardStatsTracker : SingletonPure<CardStatsTracker>
{
    /// <summary>소환사 본체의 행에 쓰는 키. 카드 ID 와 겹칠 수 없는 이름을 쓴다.</summary>
    public const string SummonerKey = "#summoner";

    readonly Dictionary<string, CardStatEntry> _entries = new();

    /// <summary>엔티티 → 카드 ID. 스폰할 때 심고, 판을 비울 때 지운다.</summary>
    readonly Dictionary<Entity, string> _ownerByEntity = new();

    /// <summary>지금 살아 있는 스킬 시전 창. 나중에 연 것이 앞에 온다.</summary>
    readonly List<(string cardId, float endTime)> _casts = new(4);

    // ── 등록 ─────────────────────────────────────────────────

    /// <summary>
    /// 이 엔티티가 낸 피해를 어느 카드에 달 것인지 정한다.
    /// 몬스터는 스폰 시, 소환사는 세울 때 한 번 부른다.
    /// </summary>
    public void RegisterOwner(Entity entity, string cardId, SummonKind kind,
                              bool isSummoner = false)
    {
        if (entity == Entity.Null || string.IsNullOrEmpty(cardId)) return;

        _ownerByEntity[entity] = cardId;

        CardStatEntry entry = Ensure(cardId, kind, isSummoner);
        entry.SpawnCount++;
    }

    /// <summary>
    /// 스킬 카드를 시전했다 — 이 순간부터 duration 초 동안의 스킬 피해를 이 카드에 단다.
    ///
    /// ⚠ duration 은 넉넉히 준다
    ///   장판은 시전 직후가 아니라 몇 초에 걸쳐 때린다. 짧게 주면 뒷부분이
    ///   "누구 것도 아닌 피해" 가 되어 합계가 실제보다 작게 나온다.
    /// </summary>
    public void BeginCast(string cardId, float duration)
    {
        if (string.IsNullOrEmpty(cardId)) return;

        Ensure(cardId, SummonKind.Skill, isSummoner: false).SpawnCount++;

        // 나중에 연 창이 먼저 잡히도록 앞에 넣는다.
        _casts.Insert(0, (cardId, Time.time + Mathf.Max(0.5f, duration)));

        // 만료된 창을 함께 걷어낸다 — 따로 도는 정리 루프를 두지 않기 위해.
        for (int i = _casts.Count - 1; i >= 0; i--)
            if (_casts[i].endTime < Time.time) _casts.RemoveAt(i);
    }

    // ── 기록 ─────────────────────────────────────────────────

    /// <summary>
    /// 피해를 기록한다. 공격자 엔티티로 카드를 찾는다.
    ///
    /// 스킬이면 시전 창을 먼저 본다 — 스킬 피해는 공격자가 늘 소환사라
    /// 엔티티만으로는 어느 스킬 카드인지 알 수 없다.
    ///
    /// ⚠ 도트는 시전 창을 보지 않는다 (2026-09-12)
    ///   도트는 걸린 뒤 몇 초에 걸쳐 들어온다. 그 사이에 다른 스킬을 쓰면
    ///   그 창으로 빨려 들어가 엉뚱한 카드의 딜이 된다. 도트의 주인은
    ///   <b>건 개체</b>이고, 그건 엔티티로 정확히 찾을 수 있다
    ///   (UnitStatusEffectSystem 이 가장 센 도트의 시전자를 공격자로 넣는다).
    /// </summary>
    public void RecordDamage(Entity attacker, float amount,
                             BattleGame.Units.HitType type, bool isKill)
    {
        bool isSkill = type == BattleGame.Units.HitType.Skill;
        bool isDot   = type == BattleGame.Units.HitType.Dot;

        string cardId = ResolveAttacker(attacker, isSkill);
        if (cardId == null) return;

        if (!_entries.TryGetValue(cardId, out var entry)) return;

        entry.DamageDealt += amount;

        // 세그먼트 바가 "평타로 벌었나 · 상태 이상으로 벌었나 · 스킬로 벌었나" 를 보여 준다 —
        // 같은 딜량이라도 성격이 다르면 다음 3택의 판단이 달라진다.
        if      (isDot)   entry.DotDamage    += amount;
        else if (isSkill) entry.SkillDamage  += amount;
        else              entry.NormalDamage += amount;

        if (isKill) entry.KillCount++;
    }

    public void RecordDamageTaken(Entity victim, float amount)
    {
        if (!_ownerByEntity.TryGetValue(victim, out string cardId)) return;
        if (!_entries.TryGetValue(cardId, out var entry))           return;

        entry.DamageTaken += amount;
    }

    public void RecordHealing(Entity source, float amount)
    {
        if (!_ownerByEntity.TryGetValue(source, out string cardId)) return;
        RecordHealingForCard(cardId, amount);
    }

    /// <summary>
    /// 카드 ID 로 곧장 치유를 단다.
    ///
    /// ⚠ 사망 발동 회복(힐 슬라임)이 쓴다 (사용자 지적, 2026-09-11)
    ///   회복이 도는 순간 시전자는 이미 죽어 엔티티로는 찾을 수 없다 — 그래서
    ///   출처를 비운 채(Entity.Null) 넣었고, 통계의 '치유' 가 늘 0 이었다.
    ///   죽은 개체가 무엇이었는지는 종족(= 카드 ID)이 안다.
    /// </summary>
    public void RecordHealingForCard(string cardId, float amount)
    {
        if (amount <= 0f || string.IsNullOrEmpty(cardId)) return;
        if (!_entries.TryGetValue(cardId, out var entry)) return;

        entry.HealingDone += amount;
    }

    // ── 조회 ─────────────────────────────────────────────────

    /// <summary>피해량이 큰 순서로 정렬한 목록. 화면이 그대로 그린다.</summary>
    public List<CardStatEntry> BuildRanking()
    {
        var list = new List<CardStatEntry>(_entries.Count);

        foreach (CardStatEntry e in _entries.Values)
        {
            // 아무것도 한 게 없는 행은 빼 준다 — 목록만 길어진다.
            if (e.DamageDealt <= 0f && e.DamageTaken <= 0f && e.HealingDone <= 0f) continue;
            list.Add(e);
        }

        list.Sort((a, b) => b.DamageDealt.CompareTo(a.DamageDealt));
        return list;
    }

    /// <summary>
    /// 카드 통계를 **원작 통계 화면이 읽는 판**(GeneralStatEntry)으로 옮긴다.
    ///
    /// ■ 왜 변환인가 — 화면을 새로 만들지 않기 위해
    ///   딜/탱/힐 탭·세그먼트 프로그레스 바·DPS·범례는 이미 다 있다
    ///   (GeneralStatRowUI · StatBarUI · CombatStatTab · ReincarnationPopup).
    ///   그쪽이 읽는 것은 GeneralStatEntry 하나뿐이라, 여기서 그 모양으로
    ///   옮겨 주기만 하면 화면 코드는 한 줄도 건드릴 필요가 없다.
    ///
    ///   칸 대응:
    ///     GeneralDamageDealt ← 평타     DotDamageDealt     ← 상태 이상 (2026-09-12)
    ///     SkillDamageDealt   ← 스킬     DamageTaken        ← 받은 피해
    ///     HealingDone        ← 치유     KillCount          ← 처치
    ///     SoldierDamageDealt ← (안 씀, 0 — 이 게임에는 병사가 아군에 없다)
    ///
    /// ■ 그림은 각자 제 출처에서
    ///   몬스터는 합성 초상화, 스킬은 카드 아이콘, 소환사는 전투에 세운 것과
    ///   같은 외형 세 값을 넘겨 행이 직접 합성하게 한다.
    /// </summary>
    public List<GeneralStatEntry> BuildStatEntries(CardCatalog catalog)
    {
        var list = new List<GeneralStatEntry>(_entries.Count);

        foreach (CardStatEntry e in BuildRanking())
        {
            var row = new GeneralStatEntry
            {
                GeneralName        = e.CardId,
                GeneralDamageDealt = e.NormalDamage,
                DotDamageDealt     = e.DotDamage,
                SkillDamageDealt   = e.SkillDamage,
                TotalDamageDealt   = e.DamageDealt,
                DamageTaken        = e.DamageTaken,
                HealingDone        = e.HealingDone,
                KillCount          = e.KillCount,
            };

            if (e.IsSummoner)
            {
                SummonerData data = SummonerRuntimeBridge.Current?.Data;

                row.GeneralName = data != null ? data.DisplayName : "소환사";

                // 몬스터 모습의 소환사(슬라임 킹)는 그 종족의 초상화 — 시드 합성은 인간형 전용이다.
                //   ⚠ 표식도 함께 넘긴다 — 안 넘기면 왕관 없는 맨 슬라임이 된다
                //     (종족 슬라임의 Mark 는 None 이다). 전장에는 왕관이 있으므로 어긋난다.
                if (data != null && data.AppearanceSpecies != null)
                {
                    row.Portrait = MonsterPortraitProvider.Get(data.AppearanceSpecies,
                                                               data.AppearanceMark);
                }
                else if (data != null)
                {
                    row.AppearanceSeed  = data.ResolvedAppearanceSeed;
                    row.AppearanceJob   = data.AppearanceJob;
                    row.AppearanceGrade = data.AppearanceGrade;
                }
            }
            else if (e.Kind == SummonKind.Skill)
            {
                SkillCardData card = catalog?.GetSkill(e.CardId);
                if (card != null)
                {
                    row.GeneralName = card.DisplayName;
                    row.Portrait    = card.Icon;
                }
            }
            else
            {
                MonsterSpeciesData species = catalog?.GetMonster(e.CardId);
                if (species != null)
                {
                    row.GeneralName = species.DisplayName;
                    row.Portrait    = MonsterPortraitProvider.Get(species);
                }
            }

            list.Add(row);
        }

        return list;
    }

    /// <summary>전체 피해 합계. 각 행의 비중을 그릴 때 분모로 쓴다.</summary>
    public float TotalDamage
    {
        get
        {
            float sum = 0f;
            foreach (CardStatEntry e in _entries.Values) sum += e.DamageDealt;
            return sum;
        }
    }

    // ── 초기화 ───────────────────────────────────────────────

    /// <summary>
    /// 스테이지가 새로 시작됐다 — 통계를 비운다.
    ///
    /// ⚠ 엔티티 매핑도 함께 지운다
    ///   풀에서 재사용되는 엔티티가 지난 판의 카드로 남아 있으면,
    ///   슬라임이 낸 피해가 트롤 행에 쌓인다.
    /// </summary>
    public void Reset()
    {
        _entries.Clear();
        _ownerByEntity.Clear();
        _casts.Clear();
    }

    // ── 내부 ─────────────────────────────────────────────────

    string ResolveAttacker(Entity attacker, bool isSkill)
    {
        if (isSkill)
        {
            for (int i = 0; i < _casts.Count; i++)
                if (_casts[i].endTime >= Time.time) return _casts[i].cardId;

            // 시전 창이 없다 — 몬스터의 고유 스킬(멧돼지 돌진 등)이다.
            // 그건 엔티티로 정확히 찾을 수 있다.
        }

        return _ownerByEntity.TryGetValue(attacker, out string cardId) ? cardId : null;
    }

    CardStatEntry Ensure(string cardId, SummonKind kind, bool isSummoner)
    {
        if (_entries.TryGetValue(cardId, out var entry)) return entry;

        entry = new CardStatEntry
        {
            CardId     = cardId,
            Kind       = kind,
            IsSummoner = isSummoner,
        };
        _entries[cardId] = entry;
        return entry;
    }
}
