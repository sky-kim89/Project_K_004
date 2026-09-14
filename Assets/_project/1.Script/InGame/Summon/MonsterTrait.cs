using System;

// ============================================================
//  MonsterTrait.cs
//  몬스터의 "특징" — 이 몬스터를 어떤 방향으로 쓰는지 알려주는 표식.
//
//  ■ 스탯이 아니다. 사용 방향성이다
//    도감에서 숫자만 보면 "체력 300 공격 12" 가 무슨 뜻인지 감이 안 온다.
//    특징은 그 숫자를 어떻게 쓰라는 안내다 —
//      물량   : 싸게 여러 마리, 개체는 약하다. 벽으로 쓴다.
//      객체강화: 비싸게 한 마리, 개체가 세다. 소환력 투자가 값어치를 한다.
//
//  ■ 빌드 설계의 축이기도 하다
//    "비인간형을 쓰면 이득" 같은 특성(Trait 시스템)이 이 표식을 조건으로 읽는다.
//    그래서 표시용 문자열이 아니라 enum 이어야 한다.
//
//  ■ [Flags] — 한 몬스터가 여러 특징을 가질 수 있다
//    예) 슬라임 = 물량 | 방패   (싸게 여럿 나와 앞을 막는다)
//        트롤   = 객체강화 | 방패 (한 마리가 비싸고 단단하다)
// ============================================================

[Flags]
public enum MonsterTrait
{
    None = 0,

    // ── 운용 방향 ────────────────────────────────────────────

    /// <summary>물량 — 한 번에 여러 마리가 싸게 나온다. 개체 성능은 낮다.</summary>
    Swarm = 1 << 0,

    /// <summary>객체 강화 — 한 마리가 비싸고 세다. 소환력 투자가 크게 돌아온다.</summary>
    Reinforced = 1 << 1,

    // ── 전투 역할 ────────────────────────────────────────────

    /// <summary>방패 — 앞에서 맞아 준다. 체력·방어율이 높다.</summary>
    Shield = 1 << 2,

    /// <summary>돌격 — 빠르게 파고든다. 이동속도가 높다.</summary>
    Charger = 1 << 3,

    /// <summary>원거리 — 뒤에서 때린다. 사거리가 길다.</summary>
    Ranged = 1 << 4,

    /// <summary>지속 피해 — 시간이 갈수록 이득이 커진다.</summary>
    Sustained = 1 << 5,

    // ── 신체 계열 (특성 조건용) ──────────────────────────────

    /// <summary>
    /// 비인간형 — 늑대·멧돼지·슬라임·트롤.
    /// 장비를 못 입는 대신, 이 표식을 조건으로 삼는 특성이 따로 있다.
    /// </summary>
    NonHumanoid = 1 << 6,
}

public static class MonsterTraitNames
{
    /// <summary>도감에 띄울 한국어 이름.</summary>
    public static string ToKorean(this MonsterTrait trait) => trait switch
    {
        MonsterTrait.Swarm       => "물량",
        MonsterTrait.Reinforced  => "객체 강화",
        MonsterTrait.Shield      => "방패",
        MonsterTrait.Charger     => "돌격",
        MonsterTrait.Ranged      => "원거리",
        MonsterTrait.Sustained   => "지속",
        MonsterTrait.NonHumanoid => "비인간형",
        _                        => "",
    };

    /// <summary>도감 카드에 한 줄로 뿌릴 문자열 ("물량 · 방패").</summary>
    public static string Describe(MonsterTrait traits)
    {
        if (traits == MonsterTrait.None) return "";

        var parts = new System.Collections.Generic.List<string>(4);
        foreach (MonsterTrait t in Enum.GetValues(typeof(MonsterTrait)))
        {
            if (t == MonsterTrait.None) continue;
            if ((traits & t) != 0) parts.Add(t.ToKorean());
        }
        return string.Join(" · ", parts);
    }
}
