using BattleGame.Units;
using UnityEngine;

// ============================================================
//  BuffStatPalette.cs
//  "어떤 스탯을 올려 주는 버프인가" 를 색 하나로 답하는 표.
//
//  ■ 왜 따로 두는가
//    같은 질문에 답하는 색표가 두 군데 생기면 반드시 어긋난다.
//    발밑 빛기둥(UnitBuffAuraView)과 스킬 범위 원(ActiveSkillData.ShowRange)은
//    같은 버프를 서로 다른 화면 요소로 보여 주는 것뿐이므로 색이 같아야 한다.
//    전투 함성의 범위가 붉은데 그 안에 선 병사의 기둥이 파랗다면 아무 말도 못 한다.
//
//  ■ 스탯 창(StatColors) 과의 관계
//    스탯 창은 '출처'(장비·유물·특성)를 색으로 구분한다. 여기는 '무엇이 오르는가'다.
//    질문이 다르므로 표도 다르지만, 계통은 맞춰 둔다.
// ============================================================

public static class BuffStatPalette
{
    /// <summary>표에 없는 스탯의 색. 표시하지 않을 근거로도 쓰인다.</summary>
    public static readonly Color Unknown = Color.white;

    /// <summary>
    /// 스탯에 대응하는 버프 색을 돌려준다.
    ///
    /// false = 이 스탯은 화면에 표시하지 않는다.
    /// 모든 스탯에 색을 주면 난전에서 화면이 색으로 덮인다 —
    /// 전투 중 판단에 쓰이는 것만 남긴다.
    /// </summary>
    public static bool TryGet(StatType stat, out Color color)
    {
        switch (stat)
        {
            case StatType.Attack:      color = new Color(1.00f, 0.45f, 0.35f); return true;  // 붉은
            case StatType.AttackSpeed: color = new Color(1.00f, 0.85f, 0.35f); return true;  // 황금
            case StatType.Defense:     color = new Color(0.40f, 0.70f, 1.00f); return true;  // 파랑
            case StatType.MaxHp:       color = new Color(0.35f, 1.00f, 0.45f); return true;  // 초록
            case StatType.MoveSpeed:   color = new Color(0.55f, 1.00f, 0.90f); return true;  // 청록
            case StatType.CritChance:
            case StatType.CritDamage:  color = new Color(1.00f, 0.60f, 1.00f); return true;  // 분홍
            default:                   color = Unknown;                        return false;
        }
    }

    /// <summary>표에 없으면 흰색. 범위 원처럼 "일단 그려야 하는" 쪽이 쓴다.</summary>
    public static Color Get(StatType stat)
        => TryGet(stat, out var c) ? c : Unknown;

    /// <summary>회복 계열 — 스탯이 아니라 행위라 표에 없다. 치유 범위가 쓴다.</summary>
    public static readonly Color Heal = new Color(0.45f, 1.00f, 0.65f);
}
