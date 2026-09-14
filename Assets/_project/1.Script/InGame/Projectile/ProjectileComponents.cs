using Unity.Entities;
using Unity.Mathematics;

// ============================================================
//  ProjectileComponents.cs
//  기본 공격 발사체 전용 ECS 컴포넌트 정의.
//
//  흐름:
//    ① RangedAttackJob         — 원거리 공격 발생 시 자신의 ProjectileLaunchRequest 버퍼에 추가
//    ② ProjectileSpawnSystem   — 버퍼를 읽어 GO 풀에서 꺼내고 ProjectileView.Launch() 호출
//    ③ ProjectileMoveJob       — LocalTransform 갱신, 타겟 위치 추적 (Burst 병렬)
//    ④ ProjectileHitJob        — 거리 체크 → HitEventBuffer append + AttackHitEvent append + DestroyTag 추가
//    ⑤ ProjectileDestroySystem — DestroyTag 감지 → PoolController 반납
//                                → EntityLink.OnDisable → entity Disabled (기존 패턴 재사용)
// ============================================================

namespace BattleGame.Projectiles
{
    // ── 비행 중 발사체 데이터 ─────────────────────────────────────
    // 발사 요청 버퍼(ProjectileLaunchRequest)는 BattleGame.Units(UnitComponents.cs) 에 정의.
    public struct ProjectileComponent : IComponentData
    {
        public Entity   TargetEntity;
        public Entity   AttackerEntity;  // 발사한 유닛 (통계 귀속용)
        public float3   TargetPos;    // 타겟 마지막 위치 캐시 (사망 후 고정)
        public float    Damage;
        public float    Speed;
        public float    Lifetime;     // 남은 유효 시간 — 0 이하 시 소멸
        public TeamType Team;
        /// <summary>
        /// 발사 시점 공격자의 방어율 관통 (0~1).
        /// ⚠ 착탄 때 공격자를 다시 읽지 않는다 — 비행 중 시전자가 죽어 풀에 반납되면
        ///   관통이 조용히 0 이 되고, 예약 피해(AccumulateIncomingDamageJob)와
        ///   실제 피해가 어긋나 오버킬 방지가 깨진다.
        /// </summary>
        public float    DefensePierce;
        // ── 이동 방식 ───────────────────────────────────────────
        // ArcHeight > 0 : 포물선 이동 (Arrow)
        // ArcHeight = 0 : 직선 이동   (MagicBolt)
        public float3 StartPos;    // 발사 위치 (포물선 보간 기준점)
        public float  ArcHeight;   // 포물선 최대 높이 (0이면 직선)
        public float  TotalTime;   // 예상 비행 시간 = InitialDist / Speed
        public float  ElapsedTime; // 경과 시간
    }

    // ── GO 링크 (managed component) ───────────────────────────────
    // ProjectileDestroySystem 이 읽어서 PoolController.Despawn() 호출.
    public class ProjectileGoLink : IComponentData
    {
        public UnityEngine.GameObject Go;
        public string                 PoolKey;
    }

    // ── 소멸 요청 태그 ────────────────────────────────────────────
    // ProjectileHitJob(타격) 또는 ProjectileMoveJob(Lifetime 만료) 이 추가.
    // ProjectileDestroySystem 이 프레임 내에 처리.
    public struct ProjectileDestroyTag : IComponentData { }
}
