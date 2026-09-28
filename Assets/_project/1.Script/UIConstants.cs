using UnityEngine;

// ============================================================
//  UIConstants.cs
//  등급·직업·보너스 출처별 색상 + 스탯 포맷터를 한곳에 모은 공유 상수.
//  GradeStyle / JobStyle / StatBonusColors / StatDisplayHelper 로 접근.
// ============================================================

public static class GradeStyle
{
    // ── 색상 ──────────────────────────────────────────────────
    public static Color GetColor(UnitGrade grade) => grade switch
    {
        UnitGrade.Normal   => new Color(0.55f, 0.55f, 0.55f),
        UnitGrade.Uncommon => new Color(0.25f, 0.80f, 0.35f),
        UnitGrade.Rare     => new Color(0.20f, 0.50f, 1.00f),
        UnitGrade.Unique   => new Color(0.70f, 0.30f, 1.00f),
        UnitGrade.Epic     => new Color(1.00f, 0.60f, 0.10f),
        _                  => Color.white,
    };

    public static string GetLabel(UnitGrade grade)
        => LocalizationManager.Instance?.Get(grade.ToString()) ?? grade.ToString();

    // ── 스탯 품질 ─────────────────────────────────────────────
    //  같은 등급이라도 굴림이 좋았는지 나빴는지가 갈린다.
    //  내부 계산은 0~1 이고, 표시는 10배 해서 **버림**한 정수다 (0.93 → 9).

    /// <summary>표시용 품질 점수 (0~10, 버림).</summary>
    public static int QualityScore(string unitName)
        => Mathf.FloorToInt(UnitJobRoller.GetQuality(unitName) * 10f);

    /// <summary>"영웅 9" — 등급 라벨 + 품질 점수.</summary>
    public static string GetLabelWithQuality(UnitGrade grade, string unitName)
        => $"{GetLabel(grade)} {QualityScore(unitName)}";

    /// <summary>품질 색 — 낮으면 회색, 높을수록 밝은 금색.</summary>
    public static Color QualityColor(float quality) => quality switch
    {
        >= 0.90f => new Color(1.00f, 0.60f, 0.10f),   // 최상 (희귀 스킬 주인 구간)
        >= 0.75f => new Color(0.70f, 0.30f, 1.00f),
        >= 0.55f => new Color(0.20f, 0.50f, 1.00f),
        >= 0.35f => new Color(0.25f, 0.80f, 0.35f),
        _        => new Color(0.60f, 0.62f, 0.68f),
    };
}

public static class JobStyle
{
    public static string GetLabel(UnitJob job)
        => LocalizationManager.Instance?.Get(job.ToString()) ?? job.ToString();
}

// ── 스탯 항목별 색상 ─────────────────────────────────────────
// MercCandidateCard / BattlePanel 슬롯 / HeroDetailPopup 공통 사용.
public static class StatColors
{
    public static readonly Color Hp      = new Color(0.35f, 1.00f, 0.45f);  // 초록
    public static readonly Color Atk     = new Color(1.00f, 0.45f, 0.35f);  // 붉은
    public static readonly Color Def     = new Color(0.40f, 0.70f, 1.00f);  // 파랑
    public static readonly Color Soldier = new Color(1.00f, 0.85f, 0.35f);  // 황금
}

// ── 스탯 보너스 출처별 색상 ───────────────────────────────────
// HeroPanelUI 합산 상세, AbilityListPopup 등에서 공통 사용.
// 출처가 달라도 이 색상이 보이면 "같은 계통의 보너스"임을 직관적으로 전달.
public static class StatBonusColors
{
    public const string Equip   = "5599FF";  // 장비  — 파랑
    public const string Passive = "55CC77";  // 패시브 — 초록
    public const string Ability = "FFAA44";  // 어빌리티 — 주황
    public const string Relic   = "CC66FF";  // 유물  — 보라
    public const string Trait   = "FF6688";  // 특성  — 분홍
    public const string Codex   = "44DDCC";  // 도감  — 청록

    public static Color EquipColor   => new Color(0.33f, 0.60f, 1.00f);
    public static Color PassiveColor => new Color(0.33f, 0.80f, 0.47f);
    public static Color AbilityColor => new Color(1.00f, 0.67f, 0.27f);
    public static Color RelicColor   => new Color(0.80f, 0.40f, 1.00f);
    public static Color TraitColor   => new Color(1.00f, 0.40f, 0.53f);
    public static Color CodexColor   => new Color(0.27f, 0.87f, 0.80f);

    // ── 출처 색 입히기 ────────────────────────────────────────
    //
    //  ⚠ 색이 곧 '어디서 온 수치인가' 다
    //    스탯 창에서 숫자를 누르면 "2,343 +28 +2,502 …" 로 출처별 색이 갈린다.
    //    그런데 정작 그 수치를 주는 화면(장비·어빌리티·유물·도감)은 전부 흰 글씨라
    //    파란 +2,502 가 어느 장비에서 왔는지 알 방법이 없었다.
    //    옵션을 보여 주는 모든 화면이 같은 색을 쓰면, 색만 보고 출처가 읽힌다.
    //
    //  ⚠ 새 화면을 만들 때도 반드시 이걸 거칠 것
    //    각자 하드코딩하면 화면마다 파랑이 조금씩 달라지고, 그 순간 규칙이 깨진다.

    public static string Hex(StatSource source) => source switch
    {
        StatSource.Equip   => Equip,
        StatSource.Passive => Passive,
        StatSource.Ability => Ability,
        StatSource.Relic   => Relic,
        StatSource.Trait   => Trait,
        StatSource.Codex   => Codex,
        _                  => "FFFFFF",
    };

    public static Color Of(StatSource source) => source switch
    {
        StatSource.Equip   => EquipColor,
        StatSource.Passive => PassiveColor,
        StatSource.Ability => AbilityColor,
        StatSource.Relic   => RelicColor,
        StatSource.Trait   => TraitColor,
        StatSource.Codex   => CodexColor,
        _                  => Color.white,
    };

    /// <summary>"공격력 +30" 을 출처 색으로 감싼다. 빈 문자열은 그대로 둔다.</summary>
    public static string Wrap(StatSource source, string text)
        => string.IsNullOrEmpty(text) ? text : $"<color=#{Hex(source)}>{text}</color>";
}

/// <summary>
/// 스탯을 올려 주는 출처. 색·아이콘·분해 표시가 이 값으로 갈린다.
/// (스탯 창의 분해 순서와 같은 순서로 둔다)
/// </summary>
public enum StatSource
{
    Equip   = 0,
    Passive = 1,
    Ability = 2,
    Relic   = 3,
    Trait   = 4,
    Codex   = 5,
}

// ── 스탯 수치 포맷터 ─────────────────────────────────────────
// 스탯 타입에 따른 총합·델타 문자열 생성, 합산 과정 Rich Text 생성.
public static class StatDisplayHelper
{
    /// <summary>
    /// 원시 방어율(raw, 0~∞)에 소프트캡 공식 적용 후 퍼센트 값을 반환.
    /// 소프트캡 임계값·성장 계수·실효 상한은 GameplayConfig 에서 읽는다.
    /// </summary>
    public static float EffectiveDefensePct(float raw)
    {
        var   cfg      = GameplayConfig.Current;
        float softCap  = cfg.DefenseMax;
        float rate     = cfg.DefenseOverflowRate;
        float cap      = cfg.DefenseEffectiveCap;
        float effective = raw <= softCap ? raw : softCap + (raw - softCap) * rate;
        return Mathf.Min(effective, cap) * 100f;
    }

    /// <summary>
    /// 스탯 값 문자열.
    ///   isFinal=false (기본) : 원시 증감값 — withSign=true 이면 + 부호 포함 (장비·특성 보너스 표시용)
    ///   isFinal=true         : 최종 합산값 — 소프트캡·CDR 체감 공식 적용 (HUD·StatPanel 표시용)
    /// </summary>
    public static string FormatStat(StatType stat, float value, bool withSign = false, bool isFinal = false)
    {
        string sign = (withSign && value >= 0f) ? "+" : "";
        return stat switch
        {
            StatType.Defense => isFinal
                ? $"{EffectiveDefensePct(value):F1}%"
                : $"{sign}{value * 100f:F1}%",
            StatType.AttackSpeed => isFinal
                ? LocalizationManager.Instance.Format("{0:F2}/초", value)
                : $"{sign}{value:F2}",
            StatType.MoveSpeed    => $"{sign}{value:F1}",
            // ⚠ 소수 둘째 자리까지 — F1 이면 근접 직업의 증감이 안 보인다
            //   기사(1.0)·방패병(0.85)의 사거리에 +4% 는 +0.04 다. F1 로 찍으면
            //   분해 줄에 "+0.0" 이 뜨고 합계도 그대로라, 옵션이 안 붙은 것처럼 보였다.
            //   (궁수 7.7 에서는 F1 로도 보이지만 표기는 한 가지여야 한다)
            StatType.AttackRange  => $"{sign}{value:F2}",
            StatType.SoldierCount => LocalizationManager.Instance.Format("{0}{1}명", sign, Mathf.RoundToInt(value)),
            StatType.CritChance   => $"{sign}{value * 100f:F1}%",
            // 치명피해는 배수(1.8)로 저장되지만 화면에는 % 로 뿌린다 — 180.0%.
            // ⚠ 증감분(isFinal=false)도 같은 규칙이다
            //   장비 +0.5 는 "+50.0%" 로 읽혀야 최종값 180% 와 자릿수가 맞는다.
            //   예전처럼 "×1.80 / +0.50×" 로 섞으면 분해 문자열에서 단위가 두 개가 된다.
            StatType.CritDamage   => $"{sign}{value * 100f:F1}%",
            StatType.SkillCooldownReduce => isFinal
                ? FormatCDRFinal(value)
                : $"{sign}{value * 100f:F1}%",
            _ => $"{sign}{value:N0}",
        };
    }

    // 넘겨받는 값은 이미 곱연산으로 합쳐진 최종 쿨감이다 —
    // 여기서는 상한에 걸렸을 때만 그 사실을 알려 준다.
    // (예전에는 "3.3% (합산 10.0%)" 처럼 체감 공식 결과를 같이 적었는데,
    //  이제 출처가 하나면 액면가가 그대로 나오므로 두 수치가 갈릴 일이 없다)
    static string FormatCDRFinal(float cdr)
    {
        float maxCDR = GameplayConfig.CooldownCap;
        if (cdr <= maxCDR + 0.001f)
            return $"{cdr * 100f:F1}%";
        return LocalizationManager.Instance.Format("{0:F1}% <size=80%><color=#888888>(상한)</color></size>", maxCDR * 100f);
    }

    /// <summary>
    /// 합산 과정 Rich Text 생성 — "기본  +패시브(초록)".
    /// </summary>
    public static string BuildBreakdown(
        StatType stat, float baseVal, float passiveVal)
    {
        // ⚠ 한 줄로 유지한다 — 두 줄로 접으면 스탯 목록의 행 간격이 들쭉날쭉해진다.
        //   칸을 넘칠 때는 TMP AutoSize 로 글자를 줄여 담는다
        //   (HeroDetailPopup.RefreshStatRow 의 overflowMode 주석 참고).
        var sb = new System.Text.StringBuilder();
        sb.Append(FormatStat(stat, baseVal));
        if (passiveVal != 0f) sb.Append($"  <color=#{StatBonusColors.Passive}>{FormatStat(stat, passiveVal, withSign: true)}</color>");

        // 쿨감은 출처끼리 더하지 않고 곱연산으로 겹친다 — 단순 합과 다르므로 결과를 적어 준다
        if (stat == StatType.SkillCooldownReduce)
        {
            float sum      = baseVal + passiveVal;
            float combined = HeroStatResult.CombineResidual(baseVal, passiveVal);
            float maxCDR   = GameplayConfig.CooldownCap;
            float final    = Mathf.Min(combined, maxCDR);

            if (Mathf.Abs(sum - final) > 0.001f)
                sb.Append("\n<color=#AAAAAA>")
                  .Append(combined > maxCDR + 0.001f
                          ? LocalizationManager.Instance.Format("→ 중첩 적용 {0:F1}% (상한)", final * 100f)
                          : LocalizationManager.Instance.Format("→ 중첩 적용 {0:F1}%", final * 100f))
                  .Append("</color>");
        }
        else if (stat == StatType.Defense)
        {
            float rawTotal  = baseVal + passiveVal;
            float effective = StatDisplayHelper.EffectiveDefensePct(rawTotal);
            float rawPct    = rawTotal * 100f;
            if (Mathf.Abs(rawPct - effective) > 0.1f)
                sb.Append("\n<color=#AAAAAA>")
              .Append(LocalizationManager.Instance.Format("→ 체감 {0:F1}%", effective))
              .Append("</color>");
        }

        return sb.ToString();
    }
}
