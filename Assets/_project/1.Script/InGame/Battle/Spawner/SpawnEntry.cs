using UnityEngine;

// ============================================================
//  SpawnEntry.cs
//  웨이브 하나에 등록하는 스폰 항목.
//
//  ■ Name
//    유닛 고유 이름. MonsterStatRoller / EnemyAppearanceRoller 의 랜덤 시드로 사용.
//    같은 Name 은 항상 같은 스텟·외형을 가진다.
//
//  ■ UnitType
//    PoolController 풀 키로 자동 변환된다. (Enemy / Elite / Boss / General)
//    별도의 PoolKey 필드는 없으며 UnitType.ToString() 이 키가 된다.
//
//  ■ EnemyRace
//    이 항목의 종족. WaveData.DefaultRace 를 기본값으로 사용하고
//    항목별로 다르게 설정할 수 있다.
// ============================================================

[System.Serializable]
public class SpawnEntry
{
    [Tooltip("유닛 고유 이름. 스텟·외형 랜덤 시드 및 식별자로 사용.\n" +
             "같은 이름은 항상 같은 스텟·외형을 가진다.")]
    public string Name;

    [Tooltip("유닛 레벨 — 스텟 배율에 반영")]
    public int Level = 1;

    [Tooltip("유닛 종류. PoolController 풀 키로 자동 변환된다.\n" +
             "(Enemy → \"Enemy\", Elite → \"Elite\", Boss → \"Boss\", General → \"General\")")]
    public SpawnUnitType UnitType;

    [Tooltip("이 항목에서 스폰할 수")]
    public int Count = 1;

    [Tooltip("이 항목 시작 전 대기 시간 (초)")]
    public float DelayBefore = 0f;

    [Tooltip("같은 항목 내 유닛 간 스폰 간격 (초)")]
    public float DelayBetween = 0.5f;

    [Tooltip("적군 종족. WaveData.DefaultRace 를 기본값으로 사용하되 항목별로 덮어쓸 수 있다.")]
    public EnemyRace EnemyRace = EnemyRace.Orc;

    [Tooltip("스테이지 진행도 기반 스텟 배율. HP·공격력에 곱해진다 (1.0 = 기준값).")]
    public float StatMultiplier = 1f;

    [Tooltip("이 항목의 유닛 1기마다 함께 등장하는 호위 병사 수. 0 이면 호위 없음.\n" +
             "호위는 Enemy 풀에서 나오며, 한 명씩 흘러나오지 않고\n" +
             "본대 뒤에 격자 대형을 이루어 한 덩어리로 진입한다 (엘리트 부대 연출).")]
    public int EscortCount = 0;

    [Tooltip("프리팹 원본 크기에 곱할 배율. 1 = 원본 그대로.\n" +
             "무한 보스처럼 같은 프리팹을 더 크게 내보낼 때 사용한다.")]
    public float ScaleMultiplier = 1f;

    [Tooltip("넉백 완전 면역 여부. 보스에만 의미가 있다 (기본 보스 내성 0.8 → 1.0).")]
    public bool KnockbackImmune = false;

    [Tooltip("이 용사가 보스 히어로인가. 엘리트 스테이지에만 등장한다.\n" +
             "켜면 BossHeroSetup 이 BossComponent 를 얹어 기존 보스 패턴을 그대로 태운다\n" +
             "(공속 1/3 · AoE 평타 · 평타 ×3). 보스 HP 바도 자동으로 뜬다.\n" +
             "UnitType 은 General 그대로다 — 용사는 장수 체계를 쓴다.")]
    public bool IsBossHero = false;

    [Tooltip("이 용사가 엘리트 히어로인가. 엘리트 스테이지에만 등장한다. " +
             "켜면 HeroTierSetup.ApplyElite 가 EliteComponent 를 얹는다. " +
             "⚠ IsBossHero 와 동시에 켜지 말 것 — 보스 쪽이 이긴다.")]
    public bool IsEliteHero = false;

    [Tooltip("장수 없이 휘하 병사만 내보낼 것인가.\n" +
             "초반 스테이지(보스가 나오기 전)를 병사 물결로 만드는 데 쓴다.\n" +
             "켜면 HeroSpawner 가 장수 프리팹을 세우지 않고 병사만 격자로 세운다.\n" +
             "직업·등급·병사 수는 그대로 Name 시드에서 굴러 나온다.")]
    public bool SoldiersOnly = false;

    [Tooltip("휘하 병사 없이 이 용사 한 기만 세울 것인가. SoldiersOnly 의 정반대다 — 무한 보스가 쓴다. 이 게임의 보스는 용사(General)라 그냥 두면 병사 수십을 데려온다.")]
    public bool LoneHero = false;

    /// <summary>스텟 범위 편향 (0=최솟값 균등, 1=항상 최댓값). NormalMode 가 런타임에 주입.</summary>
    [System.NonSerialized] public float StageBias = 0f;

    /// <summary>PoolController 에 전달할 풀 키. UnitType 에서 자동 파생.</summary>
    public string PoolKey => UnitType.ToString();
}
