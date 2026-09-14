using System;
using UnityEngine;

// ============================================================
//  MonsterLevelBonus.cs
//  카드 레벨이 오를 때 그 몬스터가 얻는 **정해진 고정 효과** 한 칸.
//
//  ■ 어빌리티를 대체한다 (2026-08-27 설계 변경)
//    원래는 스테이지 클리어마다 어빌리티를 뽑아 부대 전체를 강화했다.
//    그 축을 없애고 **몬스터 카드 레벨업**으로 옮겼다.
//      · 무작위로 뽑는 것이 아니라 종족마다 **미리 정해진** 순서로 열린다
//      · 그래서 "이 종족을 키우면 무엇이 되는가" 를 처음부터 알고 고를 수 있다
//    카드 3택으로 무엇을 키울지 고르는 것 자체가 어빌리티 선택을 대신한다.
//
//  ■ 한 칸이 줄 수 있는 것
//      스탯 1~2개 (AbilityData 와 같은 모양)  +  선택적으로 패시브 1개
//    스탯만 주면 레벨업이 "숫자가 조금 커졌다" 로 끝나고, 패시브만 주면
//    중간 레벨이 밋밋하다. 종족마다 섞어서 잡는다.
//
//  ■ ⚠ Value 의 뜻이 스탯에 따라 다르다
//      비율 스탯 (체력·공격력·공속·이속·사거리·치명피해) → 0.15 = +15%
//      절대 스탯 (방어율·치명확률·방어관통·쿨감)          → 0.06 = +6%p
//    구분 규칙은 StatRules.IsAbsoluteStat 하나가 소유한다 —
//    여기에 목록을 복사해 두면 두 곳이 조용히 어긋난다.
//
//  ■ 레벨 1은 칸이 없다
//    배열 [0]=Lv2 · [1]=Lv3 · [2]=Lv4 · [3]=Lv5 다.
//    Lv1 은 종족 기본 스탯 그대로이고, Lv5 에 닿으면 **진화 자격**이 생긴다.
// ============================================================

[Serializable]
public struct MonsterLevelBonus
{
    [Tooltip("카드에 뜨는 이름. \"점액질\" 처럼 무엇이 열렸는지 한눈에 읽히게.")]
    public string Label;

    [Tooltip("첫 번째 스탯.")]
    public StatType Stat1;

    [Tooltip("비율 스탯이면 0.15 = +15%, 절대 스탯이면 0.06 = +6%p.")]
    public float Value1;

    [Tooltip("두 번째 스탯 사용 여부.")]
    public bool HasStat2;

    public StatType Stat2;
    public float    Value2;

    [Tooltip("이 레벨에서 함께 열리는 패시브. None 이면 스탯만 오른다.\n" +
             "발동은 기존 PassiveSkillRuntimeSystem 이 그대로 처리한다.")]
    public PassiveSkillType Passive;

    /// <summary>비어 있는 칸인가 (스탯도 패시브도 없음).</summary>
    public bool IsEmpty
        => Mathf.Approximately(Value1, 0f)
        && (!HasStat2 || Mathf.Approximately(Value2, 0f))
        && Passive == PassiveSkillType.None;

    /// <summary>
    /// 이 칸의 스탯 보너스를 실제 값으로 바꿔 더한다.
    ///
    /// ⚠ 곱이 아니라 '현재값 × 비율' 가산이다
    ///   어빌리티(AbilityApplier)와 같은 규칙이어야 두 시스템의 수치가
    ///   같은 뜻으로 읽힌다. 레벨 4칸이 전부 +15% 면 총 +60% 이지 ×1.15⁴ 가 아니다.
    /// </summary>
    public void ApplyTo(UnitStat stat)
    {
        AddOne(stat, Stat1, Value1);
        if (HasStat2) AddOne(stat, Stat2, Value2);
    }

    static void AddOne(UnitStat stat, StatType type, float value)
    {
        if (Mathf.Approximately(value, 0f)) return;

        float delta = StatRules.IsAbsoluteStat(type)
            ? value
            : stat.Get(type) * value;

        stat.Add(type, delta, "cardlevel");
    }

    /// <summary>
    /// 도감·카드에 띄울 한 줄. "점액질 — 체력 +15% · 방어율 +6%p"
    ///
    /// ■ 패시브는 이름으로 적는다
    ///   예전에는 " · 패시브" 라고만 붙였다. 카드 3택에서 이 줄이 곧 고를
    ///   이유가 되므로("이 레벨에 뭐가 열리나"), 무엇이 열리는지 이름이 있어야
    ///   한다 — "패시브" 세 글자로는 두 후보를 비교할 수 없다.
    ///
    ///   DB 가 아직 없으면(에디터에서 SO 만 열어 본 경우) enum 이름으로 떨어진다.
    /// </summary>
    public string Describe()
    {
        string body = DescribeOne(Stat1, Value1);
        if (HasStat2) body += " · " + DescribeOne(Stat2, Value2);

        if (Passive != PassiveSkillType.None)
            body += " · " + PassiveName(Passive);

        return string.IsNullOrEmpty(Label) ? body : $"{Label} — {body}";
    }

    /// <summary>
    /// 카드 3택에 띄울 <b>한 줄에 하나씩</b> 끊은 표기.
    ///
    ///     방어율 +6%p
    ///     체력 +10%
    ///     굳은 껍질          ← 패시브가 함께 열릴 때만
    ///
    /// ■ Describe() 와 무엇이 다른가 (2026-09-02)
    ///   그쪽은 " · " 로 이어 붙인 한 줄이다. 카드 폭이 380 뿐이라 스탯이
    ///   둘만 돼도 자동 축소가 걸려 글자가 쪼그라들었다. 세로로 쌓으면
    ///   폰트를 줄이지 않아도 되고, 무엇이 몇 개 오르는지 세기도 쉽다.
    ///
    /// ■ ⚠ Label 을 붙이지 않는다
    ///   "굳은 껍질 — " 같은 이름은 분위기지 정보가 아니다. 고를 때 필요한
    ///   것은 **무엇이 얼마나 오르는가** 뿐이라, 좁은 칸에서는 그 앞머리가
    ///   실제 수치를 밀어낸다. (열리는 패시브 이름은 정보이므로 남긴다)
    /// </summary>
    public string DescribeLines()
    {
        var sb = new System.Text.StringBuilder(64);

        AppendLine(sb, DescribeOne(Stat1, Value1), Value1);
        if (HasStat2) AppendLine(sb, DescribeOne(Stat2, Value2), Value2);

        if (Passive != PassiveSkillType.None)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(PassiveName(Passive));
        }

        return sb.ToString();
    }

    static void AppendLine(System.Text.StringBuilder sb, string text, float value)
    {
        if (Mathf.Approximately(value, 0f)) return;   // 안 오르는 칸은 줄을 먹지 않는다

        if (sb.Length > 0) sb.Append('\n');
        sb.Append(text);
    }

    /// <summary>패시브의 표시 이름. DB 에 없으면 enum 이름을 그대로 쓴다.</summary>
    static string PassiveName(PassiveSkillType type)
    {
        PassiveSkillData data = PassiveSkillDatabase.Current?.Get(type);

        return data != null && !string.IsNullOrEmpty(data.SkillName)
            ? data.SkillName
            : type.ToString();
    }

    static string DescribeOne(StatType type, float value)
    {
        string name = StatNames.ToKorean(type);

        return StatRules.IsAbsoluteStat(type)
            ? $"{name} +{value * 100f:0.#}%p"
            : $"{name} +{value * 100f:0.#}%";
    }
}

/// <summary>스탯 이름의 한국어 표기. 카드·도감이 함께 쓴다.</summary>
public static class StatNames
{
    public static string ToKorean(StatType type) => type switch
    {
        StatType.MaxHp              => "체력",
        StatType.Attack             => "공격력",
        StatType.AttackSpeed        => "공격속도",
        StatType.MoveSpeed          => "이동속도",
        StatType.AttackRange        => "사거리",
        StatType.Defense            => "방어율",
        StatType.CritChance         => "치명확률",
        StatType.CritDamage         => "치명피해",
        StatType.DefensePenetration => "방어관통",
        StatType.SkillCooldownReduce => "쿨타임 감소",
        StatType.SummonPower        => "소환력",
        _                           => type.ToString(),
    };
}
