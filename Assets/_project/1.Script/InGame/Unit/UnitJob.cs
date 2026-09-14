// ============================================================
//  UnitJob.cs
//  유닛 직업 / 등급 Enum 정의
//
//  직업(UnitJob):
//    unitName 시드로 결정적(Deterministic) 배정.
//    같은 이름은 항상 같은 직업. UnitJobRoller.GetJob() 으로 조회 가능.
//
//  등급(UnitGrade):
//    UnitEntry.Grade 에 저장. 기본값 Normal.
//    등급마다 전체 능력치(고정 스텟 제외) +10% 추가.
//      Normal   = ×1.0  (보너스 없음)
//      Uncommon = ×1.1
//      Rare     = ×1.2
//      Unique   = ×1.3
//      Epic     = ×1.4
// ============================================================

/// <summary>
/// 유닛 직업.
/// UnitJobRoller 가 unitName 시드로 결정 — 같은 이름은 항상 같은 직업.
/// </summary>
public enum UnitJob
{
    Knight       = 0,  // 기사   — 병사 특화. 병사 수·지휘력 최고, 이동속도 최고
    Archer       = 1,  // 궁수   — 사거리 최고, 중간 공격, 낮은 체력
    Mage         = 2,  // 마법사  — 공격력 최고, 낮은 체력, 낮은 연사속도
    ShieldBearer = 3,  // 방패병  — 방어율·체력 최고
}

/// <summary>
/// 유닛 등급.
/// 등급이 오를수록 전체 능력치(고정 스텟 제외) 10% 추가 상승.
/// </summary>
public enum UnitGrade
{
    Normal   = 0,  // 기본   — 보너스 없음
    Uncommon = 1,  // 언커먼 — +10%
    Rare     = 2,  // 레어   — +20%
    Unique   = 3,  // 유니크 — +30%
    Epic     = 4,  // 에픽   — +40%
}
