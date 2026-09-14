// ============================================================
//  MonsterAttackKind.cs
//  몬스터가 어떻게 때리는가. 종족의 정체성을 가르는 축이다.
//
//  ■ 왜 종족 데이터에 두나
//    원작에서는 이 판단이 **직업**에서 나왔다 (UnitJob.Archer/Mage → RangedTag).
//    몬스터에는 직업이 없다. 그렇다고 사거리 숫자만 보고 자동으로 정하면
//    "사거리 3짜리 근접 거인" 같은 조합을 만들 수 없다 —
//    때리는 방식과 사거리는 별개의 축이다.
//
//  ■ ECS 쪽에서 무엇이 갈리는가 (MonsterRuntimeBridge.AddComponents)
//    Melee  : 태그 없음 → UnitAttackSystem.MeleeAttackJob 이 처리
//    Ranged : RangedTag + UnitJobComponent + ProjectileLaunchRequest 버퍼
//             → RangedAttackJob 이 처리하고 ProjectileSpawnSystem 이 발사체를 낸다
//
//    ⚠ 셋 다 있어야 한 발이라도 나간다
//      RangedAttackJob 의 쿼리가 세 개를 전부 필수로 잡는다. 하나만 빠지면
//      원거리 쿼리에 안 걸리고, RangedTag 때문에 근접 쿼리에서도 빠져
//      **아무 공격도 하지 않는 유닛**이 된다. (소환사가 실제로 그랬다)
// ============================================================

public enum MonsterAttackKind
{
    /// <summary>근접 — 사거리 안에 들어가 직접 때린다.</summary>
    Melee = 0,

    /// <summary>원거리 — 발사체를 쏜다. 사거리 밖이면 접근한다.</summary>
    Ranged = 1,
}

/// <summary>
/// 원거리 몬스터가 쏘는 발사체의 종류.
///
/// ⚠ 풀 키는 여기서 정하지 않는다
///   실제 프리팹 선택은 ProjectileSpawnSystem 이 UnitJobComponent 로 한다
///   (Archer → Arrow · 그 외 → MagicBolt). Burst 도는 ISystem 이라
///   문자열을 컴포넌트로 들고 다닐 수 없어서 생긴 제약이다.
///   그래서 이 enum 은 "어느 직업으로 위장할 것인가" 의 표식이고,
///   변환은 MonsterProjectile.ToJob 한 곳에서만 한다.
/// </summary>
public enum MonsterProjectileKind
{
    /// <summary>화살 — 포물선으로 날아간다. 빠르다(15).</summary>
    Arrow = 0,

    /// <summary>마법구 — 직선으로 날아간다. 느리지만 그림이 크다(10).</summary>
    MagicBolt = 1,
}

public static class MonsterProjectile
{
    /// <summary>
    /// 발사체 종류를 ProjectileSpawnSystem 이 읽는 직업으로 바꾼다.
    ///
    /// ⚠ 몬스터에게 직업을 주는 것이 이상해 보이지만, 다른 길이 없다
    ///   RangedAttackJob 이 UnitJobComponent 를 필수 쿼리 조건으로 잡고,
    ///   ProjectileSpawnSystem 도 그 값으로 풀 키를 고른다.
    ///   직업 조건을 보는 어빌리티들(궁수 다중사격 등)은 전부
    ///   SoldierComponent / GeneralEntity 를 함께 요구하므로
    ///   몬스터가 잘못 걸릴 일은 없다.
    /// </summary>
    public static UnitJob ToJob(MonsterProjectileKind kind)
        => kind == MonsterProjectileKind.Arrow ? UnitJob.Archer : UnitJob.Mage;
}
