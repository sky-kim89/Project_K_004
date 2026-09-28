using System;
using UnityEngine;

// ============================================================
//  DifficultyConfig.cs
//  난이도 등급 5단계의 수치 테이블 (ScriptableObject).
//
//  ■ StageConfig 를 고치지 않는다
//    난이도는 StageConfig 값에 곱해지는 '계수' 다. 원본을 직접 바꾸면
//    난이도를 되돌렸을 때 원래 값이 뭐였는지 알 수 없다.
//
//  ■ 디버프는 누적이다
//    Tiers[3](초열)은 광포·물량·각성을 전부 갖고 있고 수치도 더 높다.
//    UI 는 Tier.ActiveDebuffs() 로 켜진 디버프만 뽑아 쓴다.
//
//  생성: Tools > Project K > 데이터 생성 > 난이도
//  위치: Assets/Resources/DifficultyConfig.asset
// ============================================================

[CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Project K/Difficulty Config")]
public class DifficultyConfig : ScriptableObject
{
    static DifficultyConfig _current;
    public static DifficultyConfig Current
    {
        get => _current != null ? _current : (_current = Resources.Load<DifficultyConfig>("DifficultyConfig"));
        internal set => _current = value;
    }

    [Serializable]
    public class TierEntry
    {
        public DifficultyTier Tier;

        [Header("광포 — 적 공격력·최대체력 배율 (0 = 미적용)")]
        [Tooltip("0.5 = +50%")]
        public float EnemyStatBonus;

        [Header("물량 — 적 등장 수 배율 (0 = 미적용)")]
        [Tooltip("0.2 = +20%. 1,000마리 상한이 있어 0.8 이상은 프레임이 먼저 무너진다.")]
        public float EnemyCountBonus;

        [Header("각성 — 엘리트·보스 스킬 쿨다운 감소 (0 = 미적용)")]
        [Tooltip("0.4 = 쿨다운 -40%")]
        [Range(0f, 0.9f)]
        public float BossCooldownCut;

        [Header("폭주 — 우두머리 신규 공격 패턴 해금")]
        [Tooltip("켜면 엘리트가 보스의 돌진을 습득하고, 보스가 분쇄 강타를 추가로 쓴다.")]
        public bool FrenzyPatterns;

        [Header("보상")]
        [Tooltip("환생 포인트 배율. 1.3 = +30%. 1 미만이면 깎인다 (쉬움).")]
        public float ReincarnationMultiplier = 1f;

        [Tooltip("런이 끝날 때 받는 장비 상자 수. 유물 '대장간의 기억' 몫은 여기에 더해진다.")]
        [Min(1)]
        public int GearBoxes = 1;

        /// <summary>이 등급에서 켜져 있는 디버프 목록 (표시 순서 = 추가된 순서).</summary>
        public DifficultyDebuff[] ActiveDebuffs()
        {
            var list = new System.Collections.Generic.List<DifficultyDebuff>(4);
            if (EnemyStatBonus  > 0f) list.Add(DifficultyDebuff.Ferocity);
            if (EnemyCountBonus > 0f) list.Add(DifficultyDebuff.Horde);
            if (BossCooldownCut > 0f) list.Add(DifficultyDebuff.Awakening);
            if (FrenzyPatterns)       list.Add(DifficultyDebuff.Frenzy);
            return list.ToArray();
        }

        /// <summary>툴팁에 띄울 수치 문구. 없으면 빈 문자열.</summary>
        public string DescribeDebuff(DifficultyDebuff d) => d switch
        {
            DifficultyDebuff.Ferocity  => LocalizationManager.Instance.Format(
                                  "적 공격력·최대체력 +{0:0}%", EnemyStatBonus * 100f),
            DifficultyDebuff.Horde     => LocalizationManager.Instance.Format(
                                  "적 등장 수 +{0:0}%", EnemyCountBonus * 100f),
            DifficultyDebuff.Awakening => LocalizationManager.Instance.Format(
                                  "엘리트·보스 스킬 쿨다운 -{0:0}%", BossCooldownCut * 100f),
            DifficultyDebuff.Frenzy    => "엘리트가 돌진을 익히고, 보스가 분쇄 강타를 쓴다",
            _                          => "",
        };
    }

    [Header("등급 5단계 — 인덱스 = DifficultyTier 값")]
    public TierEntry[] Tiers = new TierEntry[0];

    // ── 조회 ─────────────────────────────────────────────────

    public TierEntry Get(DifficultyTier tier)
    {
        foreach (var t in Tiers)
            if (t != null && t.Tier == tier) return t;
        return null;
    }

    // ⚠ 스폰마다 불린다 — 캐시가 없으면 안 된다
    //   GeneralStatRoller.Roll(광포)이 용사·병사 부대마다 부른다.
    //   UserDataManager.Get<T>() 는 섹션 딕셔너리를 훑는 선형 탐색이라
    //   캐시 없이 두면 스폰 프레임이 통째로 튄다.
    //   DifficultyData.OnChanged 로만 무효화한다 (선택은 런 시작 전에만 바뀐다).
    static TierEntry _cached;
    static bool      _hooked;

    /// <summary>현재 선택된 등급의 수치. 설정이 없으면 출정(디버프 없음)으로 본다.</summary>
    public static TierEntry CurrentTier()
    {
        if (!_hooked)
        {
            DifficultyData.OnChanged += Invalidate;
            _hooked = true;
        }
        if (_cached != null) return _cached;

        var cfg = Current;
        if (cfg == null) return Fallback;

        var data = UserDataManager.Instance?.Get<DifficultyData>();
        _cached = cfg.Get(data?.SelectedTier ?? DifficultyTier.Easy) ?? Fallback;
        return _cached;
    }

    /// <summary>선택이 바뀌었거나 에셋을 다시 만들었을 때 캐시를 버린다.</summary>
    public static void Invalidate()
    {
        _cached  = null;
        _current = null;   // 에디터에서 에셋을 다시 만들면 참조가 끊긴다
    }

    // Config 를 아직 안 만든 상태에서도 게임이 돌아가야 한다 — 전부 0 = 기본 난이도.
    static readonly TierEntry Fallback = new() { Tier = DifficultyTier.Easy };

    // ── 디버프 설명 (수치 없는 고정 문구) ────────────────────

    public static string Flavor(DifficultyDebuff d) => d switch
    {
        DifficultyDebuff.Ferocity  => "적들이 피에 굶주려 날뛴다.",
        DifficultyDebuff.Horde     => "끝이 보이지 않는 무리가 몰려온다.",
        DifficultyDebuff.Awakening => "우두머리들이 힘을 각성했다.",
        DifficultyDebuff.Frenzy    => "우두머리들이 이성을 잃고 새로운 공격을 쏟아낸다.",
        _                          => "",
    };
}
