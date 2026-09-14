using BattleGame.Units;

// ============================================================
//  ActivePoisonZone.cs — 독성 지대 (법사·궁수)
//
//  이동속도 감소 + 지속 피해 영역.
//  존 로직은 ActiveSkillZoneBase → SkillZoneRunner 가 처리.
// ============================================================

[UnityEngine.CreateAssetMenu(fileName = "Active_PoisonZone", menuName = "BattleGame/Actives/PoisonZone")]
public class ActivePoisonZone : ActiveSkillZoneBase
{
    [UnityEngine.Header("독성 지대 설정")]
    [UnityEngine.Tooltip("이동속도 감소 배율 (예: 0.5 → 이동속도 50%)")]
    [UnityEngine.Range(0.1f, 0.9f)]
    public float MoveSlowMultiplier = 0.5f;

    protected override float DefaultRadius   => 2.5f;
    protected override float DefaultDuration => 6f;

    protected override void ConfigureDebuffs(ref SkillZoneRunner.ZoneConfig config)
    {
        config.HasDebuff1    = true;
        config.Debuff1Stat   = StatType.MoveSpeed;
        config.Debuff1Delta  = MoveSlowMultiplier;
        config.Debuff1Mode   = EffectMode.Multiply;
    }
}
