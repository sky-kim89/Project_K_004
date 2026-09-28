using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MonsterGearOption.cs
//  몬스터 장비가 주는 **능력치 한 줄**의 정의.
//
//  ■ StatType 을 그대로 쓰지 않는 이유
//    장비가 주는 것 중 절반은 StatType 에 없다 — 카드 비용(마나)·넉백은
//    스탯이 아니라 규칙이 읽는 값이다. 또 같은 체력이라도 "+40" 과 "+5%" 는
//    적용 자리가 다르다(MonsterGearRule.ApplyStats — 비율이 먼저, 절대값이 나중).
//    StatType 에 모드 플래그를 붙이면 그 구분이 데이터 한 칸에 숨어 버린다.
//    그래서 장비 전용 어휘를 따로 둔다. **적용은 MonsterGearRule 한 곳이다.**
//
//  ■ 값의 단위
//    절대값 — Hp·Attack·Range 는 그 숫자 그대로
//    비율   — 0.05 = 5%  (Defense·CritChance·Penetration 은 %p, 나머지는 %)
//    ManaCost — 깎는 양(양수). 1 = 카드 비용 −1
//
//  ⚠ 번호는 뒤에만 추가한다 — SO 에 정수로 직렬화된다
// ============================================================

public enum GearStat
{
    Hp          = 0,    // 체력 +N (절대)
    Attack      = 1,    // 공격력 +N (절대)
    Defense     = 2,    // 방어율 +N%p
    HpPct       = 3,    // 체력 +N%
    AttackPct   = 4,    // 공격력 +N%
    CritChance  = 5,    // 치명타 확률 +N%p
    CritDamage  = 6,    // 치명타 피해 +N%p
    AttackSpeed = 7,    // 공격 속도 +N%
    MoveSpeed   = 8,    // 이동 속도 +N% (음수 = 느려진다)
    Cooldown    = 9,    // 스킬 쿨타임 −N%
    Penetration = 10,   // 방어 관통 +N%p
    Range       = 11,   // 사거리 +N (절대)
    ManaCost    = 12,   // 카드 소환 비용 −N — ⚠ 화면에는 글자가 아니라 마나 아이콘으로 적는다 (UI 규칙 7)
    Knockback   = 13,   // 평타 넉백 +N%
}

/// <summary>능력치 한 줄.</summary>
[Serializable]
public struct GearOption
{
    public GearStat Stat;
    public float    Value;

    [Tooltip("특이 옵션 — 혼자서는 쓸모가 적고 다른 옵션·종족과 겹칠 때 값을 한다.\n" +
             "화면에서 색을 달리해 '일부러 붙은 것' 으로 읽히게 한다.")]
    public bool Quirk;

    public GearOption(GearStat stat, float value, bool quirk = false)
    {
        Stat = stat; Value = value; Quirk = quirk;
    }
}

/// <summary>레벨 하나가 여는 것. Lv2~Lv5 가 하나씩 갖는다 (MonsterGearData.Levels).</summary>
[Serializable]
public class GearLevelStep
{
    public List<GearOption> Options = new();
}

public static class GearOptionText
{
    /// <summary>특이 옵션의 글자색. 보라 — 등급색(영웅=보라)과 겹치지 않게 분홍 쪽으로 기울였다.</summary>
    public const string QuirkHex = "E08CFF";

    /// <summary>
    /// "체력 +40" · "치명타 확률 +5%" 한 줄.
    ///
    /// ⚠ ManaCost 는 빈 문자열이다 — 마나는 언제나 아이콘이다 (UI 규칙 7)
    ///   글 목록 안에서는 아이콘을 못 그리므로 부르는 쪽이 따로 배지를 세운다
    ///   (GearDetailPopup). 여기서 "마나 −1" 로 적어 두면 규칙이 한 곳에서 새어 나간다.
    /// </summary>
    public static string Describe(in GearOption o)
    {
        float v = o.Value;

        return o.Stat switch
        {
            GearStat.Hp          => F("체력 {0}", Signed(v, "0")),
            GearStat.Attack      => F("공격력 {0}", Signed(v, "0.#")),
            GearStat.Defense     => F("방어율 {0}", SignedPct(v)),
            GearStat.HpPct       => F("체력 {0}", SignedPct(v)),
            GearStat.AttackPct   => F("공격력 {0}", SignedPct(v)),
            GearStat.CritChance  => F("치명타 확률 {0}", SignedPct(v)),
            GearStat.CritDamage  => F("치명타 피해 {0}", SignedPct(v)),
            GearStat.AttackSpeed => F("공격 속도 {0}", SignedPct(v)),
            GearStat.MoveSpeed   => F("이동 속도 {0}", SignedPct(v)),
            GearStat.Cooldown    => F("스킬 쿨타임 {0}", SignedPct(-v)),
            GearStat.Penetration => F("방어 관통 {0}", SignedPct(v)),
            GearStat.Range       => F("사거리 {0}", Signed(v, "0.0#")),
            GearStat.Knockback   => F("넉백 {0}", SignedPct(v)),
            _                    => "",
        };
    }

    /// <summary>
    /// 표에서 문장을 찾아 값을 끼워 넣는다 — 원본 표가 쓰는 <c>{0}</c> 방식.
    ///
    /// ⚠ <b>스탯 이름과 값을 따로 두지 않는다</b> (2026-09-16) — 어순이 언어마다 다르다.
    ///   이름만 번역해 붙이면 독일어·프랑스어에서 자리가 어긋난다. 문장째 키로 잡는다.
    ///
    /// ⚠ 보간 문자열($"…{값}…")을 쓰지 말 것 — 실행 시점에 이미 숫자로 바뀌어 있어
    ///   표의 키와 영원히 일치하지 않는다. 에러도 경고도 안 난다.
    /// </summary>
    static string F(string key, params object[] args)
        => LocalizationManager.Instance.Format(key, args);

    /// <summary>특이 옵션이면 색을 입힌다.</summary>
    public static string DescribeRich(in GearOption o)
    {
        string text = Describe(o);
        if (string.IsNullOrEmpty(text) || !o.Quirk) return text;
        return $"<color=#{QuirkHex}>{text}</color>";
    }

    /// <summary>
    /// 여러 줄을 " · " 로 잇는다. ManaCost 는 건너뛴다 (Describe 주석).
    /// </summary>
    public static string Join(IReadOnlyList<GearOption> options, bool rich = true)
    {
        if (options == null || options.Count == 0) return "";

        var sb = new System.Text.StringBuilder(64);
        for (int i = 0; i < options.Count; i++)
        {
            string line = rich ? DescribeRich(options[i]) : Describe(options[i]);
            if (string.IsNullOrEmpty(line)) continue;

            if (sb.Length > 0) sb.Append(" · ");
            sb.Append(line);
        }
        return sb.ToString();
    }

    /// <summary>이 목록이 깎는 카드 비용 합. 0 이면 마나 배지를 끈다.</summary>
    public static int ManaCutOf(IReadOnlyList<GearOption> options)
    {
        if (options == null) return 0;

        float sum = 0f;
        for (int i = 0; i < options.Count; i++)
            if (options[i].Stat == GearStat.ManaCost) sum += options[i].Value;

        return Mathf.RoundToInt(sum);
    }

    // ⚠ 음수 부호는 '−'(U+2212) 가 아니라 하이픈이다 — 기본 폰트에 U+2212 가 없다 (UI 규칙 2)
    static string Signed(float v, string fmt)
        => (v >= 0f ? "+" : "-") + Mathf.Abs(v).ToString(fmt);

    static string SignedPct(float v)
        => (v >= 0f ? "+" : "-") + (Mathf.Abs(v) * 100f).ToString("0.#") + "%";
}
