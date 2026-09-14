using BattleGame.Units;

// ============================================================
//  PassiveDefenseShield.cs  [OnHit]
//  방어 강화 — 피격 시 방어율 +N% T초 버프.
//
//  Inspector:
//    TriggerType = OnHit
//    DefenseBuff: 방어율 증가 절대값 (0.1 = +10%)
//    BuffDuration: 지속 시간(초)
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Passive_DefenseShield", menuName = "BattleGame/Passives/DefenseShield")]
public class PassiveDefenseShield : PassiveSkillData
{
    [UnityEngine.Header("방어 강화 설정")]
    [UnityEngine.Range(0f, 0.5f)]
    public float DefenseBuff  = 0.10f;
    public float BuffDuration = 3f;

    /// <summary>
    /// 피격 시 방어율 버프 — <b>쌓지 않고 시간만 되살린다.</b>
    ///
    /// ⚠ 예전에는 맞을 때마다 새 버프를 하나씩 더했다 (사용자 지적, 2026-09-07)
    ///   설명은 "+10%" 하나인데 실제로는 상한 없이 누적됐다. 앞줄에 선
    ///   몬스터는 여러 명에게 동시에 맞으므로 3초 안에 스무 번 넘게 쌓인다 —
    ///   실제로 <b>방어율 0.19 → 2.59</b> 가 나왔다. 소프트캡(0.9) 위로 한참
    ///   올라가 사실상 불사가 된다.
    ///
    ///   원작에서는 장수 하나가 이 패시브를 들고 상대도 소수라 드러나지
    ///   않았다. 이 게임은 아군이 물량이고 적도 부대 단위라 조건이 다르다.
    ///
    /// ⚠ 세기를 올리고 싶으면 DefenseBuff 를 키운다 — 중첩을 되살리지 말 것.
    ///   중첩은 "맞는 횟수" 가 세기를 정하게 만들어, 둘러싸일수록 단단해지는
    ///   뒤집힌 규칙이 된다.
    /// </summary>
    public override void OnTrigger(PassiveTriggerContext ctx)
    {
        var em = ctx.EntityManager;
        if (!em.HasBuffer<StatusEffectBufferElement>(ctx.GeneralEntity)) return;

        var buf = em.GetBuffer<StatusEffectBufferElement>(ctx.GeneralEntity);

        // 이미 걸려 있으면 남은 시간만 되살린다 (AbilityGhostRally 와 같은 조회 방식).
        for (int i = 0; i < buf.Length; i++)
        {
            var b = buf[i];
            if (b.SourceType != BuffSourceType.Passive) continue;
            if (b.SourceId   != (int)Type)              continue;
            if (b.Stat       != StatType.Defense)       continue;

            b.Delta     = DefenseBuff;    // 밸런스를 바꿔도 옛 값이 남지 않게 다시 쓴다
            b.Duration  = BuffDuration;
            b.Remaining = BuffDuration;
            buf[i]      = b;
            return;
        }

        buf.Add(new StatusEffectBufferElement
        {
            Stat       = StatType.Defense,
            Delta      = DefenseBuff,
            Mode       = EffectMode.Add,
            Duration   = BuffDuration,
            Remaining  = BuffDuration,
            SourceType = BuffSourceType.Passive,
            SourceId   = (int)Type,
        });
    }
}
