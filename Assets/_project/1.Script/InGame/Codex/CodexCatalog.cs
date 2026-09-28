using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  CodexCatalog.cs
//  도감에 실릴 "전체 목록" 을 만드는 단일 진입점.
//
//  ■ 세 분류다 — 몬스터 · 특성 · 장비 (사용자 확정, 2026-09-07)
//    원작에서 넘어온 다섯 분류 중 둘을 걷어냈다.
//      어빌리티 — 축이 통째로 폐기됐다 (카드 레벨업이 대신한다)
//      장수     — 이 게임에 플레이어의 장수가 없다
//
//    ⚠ 장비 탭은 **몬스터 장비(MonsterGearData)** 다 — 용사 장비가 아니다
//      원작의 장비 탭(EquipmentData)은 적(용사) 전용이라 걷어냈었다. 지금
//      들어오는 것은 그것과 다른 물건이다 (MonsterGearData 머리 주석 참고).
//      플레이어가 실제로 모으는 것이고, 얻는 곳이 런 종료 상자 하나뿐이라
//      "무엇이 더 있는가" 를 볼 자리가 이 화면 말고는 없었다.
//
//  ■ ⚠ 수집 버프는 없다
//    "몇 종 모았나" 로 공격력·체력이 오르지 않는다 (CodexData 파일 머리 참고).
//    도감의 값어치는 몬스터 탭의 **품질 개선** 하나가 갖는다.
//
//  ⚠ 총 종수를 상수로 박지 말 것
//    항목은 계속 늘어난다. 어딘가에 숫자를 적어 두면 늘어난 날부터
//    진행률이 조용히 틀려진다. 전부 DB 에서 센다.
//
//  ⚠ 도감의 "미보유" 는 존재 자체를 숨기는 게 아니다
//    칸은 그대로 두고 이름·그림만 ? 로 가린다 (CodexEntry.Owned=false).
//    몇 칸이 비었는지 보여야 모으고 싶어진다.
// ============================================================

public enum CodexCategory
{
    /// <summary>
    /// 몬스터 — 이 프로젝트의 주인공 분류다. 그래서 **맨 앞**이고 기본 탭이다.
    ///
    /// ⚠ 특성과 성격이 다르다 — <b>여기서만 무언가를 할 수 있다</b>
    ///   특성은 "만나 봤나" 를 세는 기록이지만, 몬스터 칸은 품질 개선
    ///   버튼이 달린다 (MonsterGradeUpgradeRule). 그래서 CodexEntry 에
    ///   Species 가 붙어 있다 — 칸에서 대상을 다시 찾을 수 있어야 한다.
    /// </summary>
    Monster = 0,

    /// <summary>특성 — 런 도중 줍는 RunPerk 30종. 읽기 전용 기록이다 (CodexData.HasPerk).</summary>
    Trait   = 1,

    /// <summary>
    /// 몬스터 장비 — 런 종료 상자로 모으는 영구 수집품 (MonsterGearInventory).
    ///
    /// ⚠ 여기서는 **끼우지 않는다** — 읽기 전용 목록이다
    ///   장착은 종족에 하는 일이라 몬스터 상세(MonsterDetailPopup)의 칸이 맡는다.
    ///   도감이 답하는 질문은 "무엇이 더 있고 나는 무엇을 가졌나" 뿐이다.
    ///
    /// ⚠ 맨 뒤다 — CodexCategory 는 뒤에만 추가한다
    ///   탭 버튼 배열이 enum 순서를 그대로 쓴다 (CodexPopupCreator).
    /// </summary>
    Gear    = 2,
}

/// <summary>도감 한 칸.</summary>
public struct CodexEntry
{
    public string Name;        // 미보유면 화면에 안 쓴다 (? 로 대체)
    public Sprite Icon;        // 특성 아이콘. 몬스터는 초상화를 쓰므로 비어 있다
    public bool   Owned;
    public Color  Accent;      // 테두리 색 — 등급이 있으면 등급색

    /// <summary>이름 아래 한 줄. 몬스터는 품질, 특성은 비어 있다.</summary>
    public string SubLabel;

    // 눌렀을 때 띄울 내용 (미보유면 안 쓴다)
    public string Desc;
    public string StatLine;

    /// <summary>
    /// 몬스터 탭에서만 채워진다 — 초상화·배지·시너지 표식·상세 팝업이 전부 여기서 나온다.
    ///
    /// ⚠ ID 만 넘기던 것을 SO 통째로 바꿨다 (2026-09-06)
    ///   격자 칸이 마나·마릿수·표식까지 그리게 되면서 칸마다 카드 목록을
    ///   다시 뒤져야 했다. 목록을 만들 때 이미 손에 쥔 것을 그대로 넘긴다.
    /// </summary>
    public MonsterSpeciesData Species;

    /// <summary>
    /// 장비 탭에서만 채워진다 — 칸을 누르면 장비 상세(GearDetailPopup)가 열린다.
    /// 몬스터 칸의 Species 와 같은 이유로 SO 를 통째로 넘긴다.
    /// </summary>
    public MonsterGearData Gear;
}

public static class CodexCatalog
{
    /// <summary>등급이 없는 항목의 테두리 색.</summary>
    static readonly Color NeutralAccent = new Color(0.32f, 0.36f, 0.52f);

    public static string Label(CodexCategory c) => c switch
    {
        CodexCategory.Monster => "몬스터",
        CodexCategory.Trait   => "특성",
        CodexCategory.Gear    => "장비",
        _                     => "?",
    };

    /// <summary>한 분류의 전체 칸 목록. 보유 여부까지 채워서 돌려준다.</summary>
    public static List<CodexEntry> Build(CodexCategory category)
    {
        var result = new List<CodexEntry>();

        switch (category)
        {
            case CodexCategory.Monster: BuildMonsters(result); break;
            case CodexCategory.Trait:   BuildTraits(result);   break;
            case CodexCategory.Gear:    BuildGear(result);     break;
        }

        return result;
    }

    static void BuildMonsters(List<CodexEntry> result)
    {
        var cards = CardCatalog.Current;
        if (cards == null) return;

        var mon = UserDataManager.Instance?.Get<MonsterCodexData>();

        var order = new Dictionary<MonsterSpeciesData, int>();

        foreach (var s in cards.Monsters)
        {
            if (s == null) continue;

            order[s] = order.Count;

            bool owned = mon != null && mon.IsUnlocked(s.Id);

            // ⚠ 품질은 해금된 종족에만 있다
            //   MonsterCodexData.GetGrade 는 미해금이면 예외를 던진다.
            //   미해금 칸은 등급색 대신 중립색을 쓴다 — 열지도 않은
            //   종족에 등급을 붙이면 "이미 가진 것" 처럼 보인다.
            UnitGrade grade = owned ? mon.GetGrade(s.Id) : UnitGrade.Normal;

            result.Add(new CodexEntry
            {
                Name     = s.DisplayName,
                Icon     = null,                 // 초상화는 칸이 직접 만든다
                Owned    = owned,
                Accent   = owned ? GradeStyle.GetColor(grade) : NeutralAccent,
                SubLabel = owned ? GradeStyle.GetLabel(grade) : null,
                Desc     = s.Description,
                StatLine = MonsterStatLine(s),
                Species  = s,
            });
        }

        // ── 순서는 고정이다 — 기본 → 1차 → 2차, 같은 단계는 카드 목록 순 (사용자 지시, 2026-09-15) ──
        //   ⚠ 이름으로 정렬하지 않는다 — 번역이 들어갈 때마다 칸이 뒤섞인다.
        //   ⚠ 품질·해금으로도 정렬하지 않는다 — 개선을 누를 때마다 칸이 자리를 옮겨
        //     방금 누른 종족을 다시 찾아야 했다. 미해금은 칸 모양이 이미 말한다.
        result.Sort((a, b) =>
        {
            int ta = TierOf(a.Species), tb = TierOf(b.Species);
            if (ta != tb) return ta.CompareTo(tb);
            return order[a.Species].CompareTo(order[b.Species]);
        });
    }

    /// <summary>진화 단계 — 뿌리 0 · 1차 1 · 2차 2. 대표 부모(UpgradeOf) 줄을 센다.</summary>
    static int TierOf(MonsterSpeciesData s)
    {
        int tier = 0;
        for (var c = s.UpgradeOf; c != null && tier < 8; c = c.UpgradeOf) tier++;   // 8 = 순환 참조 보호
        return tier;
    }

    /// <summary>
    /// 특성 — 이 게임의 **런 특성(RunPerk)** 30종 (사용자 지적, 2026-09-11).
    ///
    /// ⚠ 원작 특성(TraitDatabase)을 읽지 않는다
    ///   그쪽은 적(용사) 직업 시너지 축이라 플레이어가 얻는 일이 없어 탭이 영영 비어 있었다.
    ///   이름·설명·아이콘은 RunPerk 가 정본이다 (ToKorean · Describe · IconKey) — 손으로 적지 말 것.
    /// ⚠ 지금 런에 쥔 특성도 '본 것' 이다 — 기록(CodexData.HasPerk)이 생기기 전의 세이브를 받는다.
    /// </summary>
    static void BuildTraits(List<CodexEntry> result)
    {
        var codex = UserDataManager.Instance?.Get<CodexData>();
        var run   = UserDataManager.Instance?.Get<RunPerkData>();

        foreach (RunPerk perk in System.Enum.GetValues(typeof(RunPerk)))
        {
            if (perk == RunPerk.None) continue;

            bool owned = (codex != null && codex.HasPerk(perk)) || (run != null && run.Has(perk));

            result.Add(new CodexEntry
            {
                Name     = perk.ToKorean(),
                Icon     = SpriteManager.Instance?.Get(perk.IconKey()),
                Owned    = owned,
                Accent   = NeutralAccent,
                Desc     = perk.Describe(),
                StatLine = "",
            });
        }

        // 본 것부터, 그 안에서는 이름순 — 몇 칸이 남았는지 보이도록 미보유도 목록에 남긴다.
        result.Sort((a, b) =>
        {
            if (a.Owned != b.Owned) return b.Owned.CompareTo(a.Owned);
            return string.CompareOrdinal(a.Name, b.Name);
        });
    }

    /// <summary>
    /// 몬스터 장비 — 등급이 높은 것부터, 같은 등급이면 부위끼리 모아 놓는다.
    ///
    /// ⚠ 보유 판정은 <b>남는 수</b>가 아니라 보유 수다
    ///   끼워 둔 장비도 가진 것이다. FreeCount 로 재면 몬스터에 장착하는
    ///   순간 도감에서 사라져 "잃어버린 것처럼" 보인다.
    ///
    /// ⚠ 여기서 몸 형태로 거르지 않는다
    ///   해금한 종족이 인간형뿐이어도 비인간형 장비는 목록에 남는다 —
    ///   도감은 "무엇이 더 있는가" 를 보여 주는 자리다.
    /// </summary>
    static void BuildGear(List<CodexEntry> result)
    {
        var db = MonsterGearDatabase.Current;
        if (db == null) return;

        var inv = UserDataManager.Instance?.Get<MonsterGearInventory>();

        foreach (var g in db.Entries)
        {
            if (g == null) continue;

            int count = inv != null ? inv.OwnedCount(g.Id) : 0;
            int level = inv != null ? inv.LevelOf(g.Id)    : 1;

            result.Add(new CodexEntry
            {
                Name     = g.DisplayName,
                Icon     = g.Icon,
                Owned    = count > 0,
                Accent   = count > 0 ? GradeStyle.GetColor(g.Grade) : NeutralAccent,
                // 부위까지 함께 — 이름만으로는 어느 칸을 먹는지 알 수 없다
                // (부위는 한 몬스터에 하나뿐이라 그게 곧 장착 가능 여부다)
                // 레벨은 올렸을 때만 붙인다 — Lv1 을 전부 적으면 칸마다 같은 글자가 는다
                SubLabel = count > 0
                         ? $"{GradeStyle.GetLabel(g.Grade)} · {MonsterGearData.NameOf(g.Part)}" +
                           (level > 1 ? $" · Lv{level}" : "")
                         : null,
                Desc     = g.Description,
                StatLine = GearStatLine(g, count, level),
                Gear     = g,
            });
        }

        // 미보유를 뒤로, 그다음 등급 높은 것부터, 같은 등급이면 부위끼리.
        result.Sort((a, b) =>
        {
            if (a.Owned != b.Owned) return b.Owned.CompareTo(a.Owned);
            return string.CompareOrdinal(a.SubLabel ?? a.Name, b.SubLabel ?? b.Name);
        });
    }

    /// <summary>전 분류 합계 (수집, 전체).</summary>
    public static (int owned, int total) TotalProgress()
    {
        int owned = 0, total = 0;
        foreach (CodexCategory c in System.Enum.GetValues(typeof(CodexCategory)))
        {
            var (o, t) = Progress(c);
            owned += o;
            total += t;
        }
        return (owned, total);
    }

    public static (int owned, int total) Progress(CodexCategory category)
    {
        var list  = Build(category);
        int owned = 0;
        foreach (var e in list) if (e.Owned) owned++;
        return (owned, list.Count);
    }

    // ── 문구 조립 ────────────────────────────────────────────

    /// <summary>
    /// 몬스터 한 줄 — 기본 스탯과 종족 패시브.
    ///
    /// ⚠ 품질이 얹힌 값이 아니라 <b>종족 기본값</b>이다
    ///   실제 소환 스탯은 품질·소환력·카드 레벨이 전부 곱해져 나온다
    ///   (MonsterStatComposer). 도감은 "이 종족이 무엇인가" 를 말하는 자리라
    ///   기준값을 보여 준다. 여기에 합성값을 적으면 소환사를 바꿀 때마다
    ///   도감 숫자가 흔들려 종족끼리 비교할 수가 없다.
    ///
    /// ⚠ 패시브 이름은 SpeciesPassive.ToKorean 에서 뽑는다 — 손으로 적지 말 것
    /// </summary>
    static string MonsterStatLine(MonsterSpeciesData s)
    {
        var sb = new System.Text.StringBuilder(96);
        sb.Append(LocalizationManager.Instance.Format("공격력 {0:0.#} · 체력 {1:0.#}", s.Attack, s.MaxHp));

        var passives = new List<SpeciesPassive>(4);
        s.CollectSpeciesPassives(passives);

        for (int i = 0; i < passives.Count; i++)
            sb.Append(i == 0 ? "\n" : " · ").Append(passives[i].ToKorean());

        return sb.ToString();
    }

    /// <summary>
    /// 장비 한 줄 — 스탯과 "어디에 걸치는가", 그리고 보유 수.
    ///
    /// ⚠ 스탯 문구를 손으로 적지 말 것 — MonsterGearData.StatLine 이 정본이다
    ///   (그쪽은 다시 StatDisplayHelper 를 본다). 여기서 다시 조립하면
    ///   몬스터 상세의 장비 칸과 도감이 같은 장비를 다르게 말한다.
    ///
    /// ⚠ 보유 수를 적는 이유 — 이 장비는 <b>개체 구분이 없다</b>
    ///   같은 장비 둘은 완전히 같아서(MonsterGearInventory) 칸이 하나뿐이다.
    ///   몇 마리에게 더 끼울 수 있는지는 수로만 알 수 있다.
    /// </summary>
    static string GearStatLine(MonsterGearData gear, int owned, int level)
    {
        var sb = new System.Text.StringBuilder(96);

        string stats = gear.StatLine(level);
        if (!string.IsNullOrEmpty(stats)) sb.Append(stats).AppendLine();

        sb.Append(gear.Body == MonsterGearBody.Humanoid ? "인간형" : "비인간형")
          .Append(" · ")
          .Append(MonsterGearData.NameOf(gear.Part))
          .Append(" 칸");

        if (owned > 0) sb.Append(" · 보유 ").Append(owned);

        return sb.ToString();
    }
}
