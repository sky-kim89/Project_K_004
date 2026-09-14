using Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts;
using UnityEngine;

// ============================================================
//  UnitAppearanceBridge.cs
//  RuntimeBridge ↔ CharacterBuilder 를 연결하는 외형 적용 컴포넌트.
//
//  사용법:
//    유닛 프리팹에 CharacterBuilder 와 함께 부착한다.
//    RuntimeBridge.Initialize() 내에서:
//      GetComponent<UnitAppearanceBridge>()?.ApplyAlly(unitName, job, grade);
//      GetComponent<UnitAppearanceBridge>()?.ApplyEnemy(race, unitName);
//
//  주의:
//    [DefaultExecutionOrder(-100)] 로 CharacterBuilderBase.Awake()(order 0) 보다
//    먼저 실행해 RebuildOnStart = false 를 선점한다.
//    이렇게 하면 SpriteCollection / Character 가 없어도 NullReference 가 발생하지 않는다.
//
//  풀 재사용 최적화 (2단):
//    ① 인스턴스 단위 — 마지막으로 적용한 외형 키를 기억해 같은 조합이면 Rebuild() 자체를 건너뛴다.
//       (디스폰 시 키를 지우지 않는다. 지우면 같은 유닛이 다시 나올 때마다 재합성한다)
//    ② 전역 단위 — CharacterBuilder 의 공유 캐시가 같은 외형의 텍스처·SpriteLibraryAsset 을
//       모든 인스턴스에 돌려쓴다. 한 웨이브 적 20기는 조합이 같아 합성은 1회로 끝난다.
//
//  ⚠ 공유 캐시를 비우면 인스턴스 키도 같이 죽어야 한다 (InvalidateAll)
//    ①의 키는 "이 인스턴스에 그 외형이 이미 올라가 있다" 는 뜻이다. 그 근거는
//    ②가 들고 있는 텍스처·SpriteLibraryAsset 이다.
//    BattleManager.PrepareRoutine 이 스테이지마다 ClearSharedCache() +
//    Resources.UnloadUnusedAssets() 를 도는데, 그때 그 에셋들이 **파괴된다.**
//    키만 남으면 재사용된 유닛이 Rebuild 를 건너뛰고 **파괴된 스프라이트를 그대로 참조** —
//    전투는 멀쩡히 하는데 캐릭터만 안 보이는 유닛이 된다.
//    (환생·스테이지를 반복할수록 '이미 적용됨' 인스턴스가 늘어 증상이 누적됐다)
// ============================================================

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CharacterBuilder))]
public class UnitAppearanceBridge : MonoBehaviour
{
    CharacterBuilder _builder;

    // ── 공유 캐시 세대 ────────────────────────────────────────
    //  ClearSharedCache 가 돌 때마다 올라간다. 인스턴스 키는 자기가 만들어진
    //  세대에서만 유효하다 — 세대가 다르면 근거가 된 에셋이 이미 없다.
    static int _cacheGeneration;
    int        _appliedGeneration = -1;

    /// <summary>
    /// 모든 인스턴스의 외형 키를 무효화한다.
    /// CharacterBuilder.ClearSharedCache() 를 부른 직후에 반드시 함께 부를 것.
    /// </summary>
    public static void InvalidateAll() => _cacheGeneration++;

    // ── 적군 외형 캐시 키 ─────────────────────────────────────
    EnemyRace _lastEnemyRace;
    string    _lastEnemyUnitName;
    bool      _hasAppliedEnemy;

    // ⚠ 공격 형태도 캐시 키의 일부다 — 무기 풀이 여기서 갈린다 (2026-09-12)
    MonsterAttackKind _lastAttackKind;

    // ⚠ 장비 조합도 캐시 키의 일부다 (MonsterGearVisual.Key)
    //   종족·이름이 같아도 낀 장비가 다르면 다른 외형이다. 이 키가 없으면
    //   도감에서 장비를 갈아 끼워도 **겉모습이 그대로**다 —
    //   ApplyEnemy 가 "같은 조합" 으로 보고 Rebuild 를 건너뛰기 때문이다.
    string _lastGearKey = "";

    // ── 아군 외형 캐시 키 ─────────────────────────────────────
    string    _lastAllyUnitName;
    UnitJob   _lastAllyJob;
    UnitGrade _lastAllyGrade;
    bool      _hasAppliedAlly;

    void Awake()
    {
        _builder = GetComponent<CharacterBuilder>();
        // CharacterBuilderBase.Awake() 보다 먼저 실행돼 Rebuild() 자동 호출을 막는다.
        _builder.RebuildOnStart = false;
    }

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>아군 외형 적용 (unitName 시드 + 직업 + 등급 기반).</summary>
    public void ApplyAlly(string unitName, UnitJob job, UnitGrade grade)
    {
        EnsureBuilder();
        if (_builder == null) return;

        // 동일한 조합이면 Rebuild 스킵
        if (_hasAppliedAlly
            && _appliedGeneration == _cacheGeneration
            && _lastAllyUnitName == unitName
            && _lastAllyJob      == job
            && _lastAllyGrade    == grade)
            return;

        _lastAllyUnitName  = unitName;
        _lastAllyJob       = job;
        _lastAllyGrade     = grade;
        _hasAppliedAlly    = true;
        _appliedGeneration = _cacheGeneration;

        Apply(AllyAppearanceRoller.Roll(unitName, job, grade));
    }

    /// <summary>적군 외형 적용 (종족 고정 + unitName 시드 무기).</summary>
    public void ApplyEnemy(EnemyRace race, string unitName)
        => ApplyEnemy(race, unitName, MonsterGearVisual.None);

    /// <summary>
    /// 종족 외형 위에 <b>장비 외형</b>을 덮어 적용한다 (소환 몬스터 전용).
    ///
    /// ⚠ 롤 결과를 그대로 고치지 않는다
    ///   EnemyAppearanceRoller.Roll 은 (종족, 이름) 조합을 **캐시해 돌려준다**.
    ///   그 인스턴스에 장비를 써 넣으면 같은 이름의 맨몸 개체까지 갑옷을 입는다.
    ///   그래서 사본을 만들어 덮는다.
    ///
    /// ⚠ 장비 이름이 빈 칸이면 건드리지 않는다
    ///   빈 문자열을 그대로 넣으면 종족이 원래 갖고 있던 것(무기 등)까지 지워진다.
    /// </summary>
    public void ApplyEnemy(EnemyRace race, string unitName, in MonsterGearVisual gear)
        => ApplyEnemy(race, unitName, MonsterAttackKind.Melee, gear);

    /// <summary>
    /// 위와 같되 <b>공격 형태</b>까지 넘긴다 — 원거리 몬스터가 활·지팡이를 든다.
    /// ⚠ 공격 형태가 캐시 열쇠에 들어간다. 빼면 같은 이름의 근접 개체가
    ///   먼저 합성돼 있을 때 궁수가 그 무기를 그대로 물려받는다.
    /// </summary>
    public void ApplyEnemy(EnemyRace race, string unitName, MonsterAttackKind kind,
                           in MonsterGearVisual gear)
    {
        EnsureBuilder();
        if (_builder == null) return;

        string gearKey = gear.Key ?? "";

        // 동일한 조합이면 Rebuild 스킵
        if (_hasAppliedEnemy
            && _appliedGeneration == _cacheGeneration
            && _lastEnemyRace     == race
            && _lastEnemyUnitName == unitName
            && _lastAttackKind    == kind
            && _lastGearKey       == gearKey)
            return;

        _lastEnemyRace     = race;
        _lastEnemyUnitName = unitName;
        _lastAttackKind    = kind;
        _lastGearKey       = gearKey;
        _hasAppliedEnemy   = true;
        _appliedGeneration = _cacheGeneration;

        UnitAppearanceData rolled = EnemyAppearanceRoller.Roll(race, unitName, kind);

        if (string.IsNullOrEmpty(gearKey)) { Apply(rolled); return; }

        Apply(WithGear(rolled, gear));
    }

    /// <summary>
    /// 롤 결과의 사본에 장비 칸만 덮어쓴다.
    /// ⚠ 초상화(MonsterPortraitProvider)도 이 함수를 쓴다 — 전장과 초상화가 같은 덮어쓰기를 지나야
    ///   "필드에서는 갑옷을 입었는데 카드에서는 맨몸" 이 안 생긴다.
    /// </summary>
    public static UnitAppearanceData WithGear(UnitAppearanceData src, in MonsterGearVisual gear)
    {
        var data = new UnitAppearanceData
        {
            Body    = src.Body,
            Head    = src.Head,
            Ears    = src.Ears,
            Eyes    = src.Eyes,
            Hair    = src.Hair,
            Armor   = src.Armor,
            Helmet  = src.Helmet,
            Mask    = src.Mask,
            Horns   = src.Horns,
            Cape    = src.Cape,
            Weapon  = src.Weapon,
            Shield  = src.Shield,
            Back    = src.Back,
            Firearm = src.Firearm,
        };

        if (!string.IsNullOrEmpty(gear.Armor))  data.Armor  = gear.Armor;
        if (!string.IsNullOrEmpty(gear.Helmet)) data.Helmet = gear.Helmet;
        if (!string.IsNullOrEmpty(gear.Shield)) data.Shield = gear.Shield;
        if (!string.IsNullOrEmpty(gear.Cape))   data.Cape   = gear.Cape;
        if (!string.IsNullOrEmpty(gear.Back))   data.Back   = gear.Back;

        return data;
    }

    // ── 내부 ─────────────────────────────────────────────────

    void EnsureBuilder()
    {
        if (_builder == null)
            _builder = GetComponent<CharacterBuilder>();
    }

    void Apply(UnitAppearanceData data)
    {
        _builder.Body    = data.Body;
        _builder.Head    = data.Head;
        _builder.Ears    = data.Ears;
        _builder.Eyes    = data.Eyes;
        _builder.Hair    = data.Hair;
        _builder.Armor   = data.Armor;
        _builder.Helmet  = data.Helmet;
        _builder.Mask    = data.Mask;
        _builder.Horns   = data.Horns;
        _builder.Cape    = data.Cape;
        _builder.Weapon  = data.Weapon;
        _builder.Shield  = data.Shield;
        _builder.Back    = data.Back;
        _builder.Firearm = data.Firearm;

        _builder.Rebuild();
    }
}
