// ============================================================
//  PassiveSkillRoller.cs
//  장군 이름을 시드로 패시브 스킬 3종을 결정하는 정적 클래스.
//
//  알고리즘:
//    FNV-1a 해시로 unitName 을 uint 시드로 변환.
//    LCG(Linear Congruential Generator) 로 순서를 섞어
//    중복 없이 3개의 PassiveSkillType(1~18) 을 뽑는다.
//
//  결정론적 보장:
//    동일 unitName 은 항상 동일한 3가지 패시브를 반환.
//    세이브/로드 시 별도 직렬화 불필요.
// ============================================================

public static class PassiveSkillRoller
{
    // PassiveSkillType 중 None(0) 을 제외한 개수 — enum 추가 시 자동 반영
    static readonly int PassiveTypeCount = System.Enum.GetValues(typeof(PassiveSkillType)).Length - 1;

    // ── 공개 API ──────────────────────────────────────────────

    /// <summary>
    /// unitName 을 시드로 패시브 스킬 3종(중복 없음)을 결정한다.
    /// s0~s2 모두 None(0) 이 아님을 보장.
    /// </summary>
    public static (PassiveSkillType s0, PassiveSkillType s1, PassiveSkillType s2) Roll(string unitName)
    {
        uint seed = Fnv1aHash(unitName);

        // 1~18 을 담은 풀에서 Fisher-Yates 셔플로 3개 추출
        int[] pool = new int[PassiveTypeCount];
        for (int i = 0; i < PassiveTypeCount; i++)
            pool[i] = i + 1;  // 1~18

        // 처음 3개만 셔플하면 됨
        for (int i = 0; i < 3; i++)
        {
            seed = LcgNext(seed);
            int j = i + (int)(seed % (uint)(PassiveTypeCount - i));
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return (
            (PassiveSkillType)pool[0],
            (PassiveSkillType)pool[1],
            (PassiveSkillType)pool[2]
        );
    }

    /// <summary>
    /// 유닛 등급에 따른 활성 패시브 슬롯 수를 반환한다.
    /// 슬롯 수는 GameplayConfig.Current 에서 읽는다.
    /// </summary>
    public static byte GetActiveSlotCount(UnitGrade grade)
    {
        var cfg = GameplayConfig.Current;
        if (cfg != null) return cfg.GetPassiveSlotCount(grade);

        // GameplayConfig 미할당 시 폴백
        return grade switch
        {
            UnitGrade.Epic                         => 3,
            UnitGrade.Unique or UnitGrade.Rare     => 2,
            _                                      => 1,
        };
    }

    // ── 내부 유틸리티 ────────────────────────────────────────

    /// <summary>FNV-1a 32비트 해시 — 문자열을 결정론적 uint 시드로 변환.</summary>
    static uint Fnv1aHash(string text)
    {
        uint hash = 2166136261u;  // FNV offset basis
        if (string.IsNullOrEmpty(text)) return hash;

        foreach (char c in text)
        {
            hash ^= (uint)c;
            hash *= 16777619u;   // FNV prime
        }
        return hash;
    }

    /// <summary>LCG(Linear Congruential Generator) — 빠른 의사난수 생성.</summary>
    static uint LcgNext(uint seed)
        => seed * 1664525u + 1013904223u;  // Numerical Recipes 상수
}
