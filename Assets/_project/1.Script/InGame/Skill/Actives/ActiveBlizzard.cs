using BattleGame.Units;

// ============================================================
//  ActiveBlizzard.cs — 블리자드 (법사)
//
//  이동속도 + 공격속도 감소 + 지속 피해 영역.
//  존 로직은 ActiveSkillZoneBase → SkillZoneRunner 가 처리.
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Active_Blizzard", menuName = "BattleGame/Actives/Blizzard")]
public class ActiveBlizzard : ActiveSkillZoneBase
{
    [UnityEngine.Header("블리자드 설정")]
    [UnityEngine.Tooltip("이동속도 감소 배율 (예: 0.4 → 이동속도 40%)")]
    [UnityEngine.Range(0.1f, 0.9f)]
    public float MoveSlowMultiplier = 0.25f;

    [UnityEngine.Tooltip("공격속도 감소 배율 (예: 0.5 → 공격속도 50%)")]
    [UnityEngine.Range(0.1f, 0.9f)]
    public float AttackSlowMultiplier = 0.35f;

    protected override float DefaultRadius   => 3f;
    protected override float DefaultDuration => 8f;

    protected override void ConfigureDebuffs(ref SkillZoneRunner.ZoneConfig config)
    {
        config.HasDebuff1    = true;
        config.Debuff1Stat   = StatType.MoveSpeed;
        config.Debuff1Delta  = MoveSlowMultiplier;
        config.Debuff1Mode   = EffectMode.Multiply;

        config.HasDebuff2    = true;
        config.Debuff2Stat   = StatType.AttackSpeed;
        config.Debuff2Delta  = AttackSlowMultiplier;
        config.Debuff2Mode   = EffectMode.Multiply;
    }
}
