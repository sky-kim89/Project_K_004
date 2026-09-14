using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;      // ProjectileLaunchRequest
using BattleGame.Projectiles; // ProjectileComponent, ProjectileDestroyTag

// ============================================================
//  ProjectileView.cs
//  발사체 시각 오브젝트 (PoolController 로 관리).
//
//  - EntityLink : ECS entity 연결 + LateUpdate 위치 동기화 (기존 패턴 재사용)
//  - Launch()   : ProjectileSpawnSystem 이 스폰 직후 호출 — entity 초기화 & 방향 설정
//  - OnDisable  : EntityLink.OnDisable 이 entity 에 Disabled 추가 (기존 패턴)
//
//  프리팹 구성:
//    Root  — SpriteRenderer (화살/마법 구슬 스프라이트) + EntityLink + ProjectileView
//            SpriteRenderer 의 기준 방향: 오른쪽(+X) 향하도록 설정 → 회전이 올바르게 적용
// ============================================================

[RequireComponent(typeof(EntityLink))]
public class ProjectileView : MonoBehaviour
{
    const float Lifetime = 8f;  // 최대 비행 시간 (초) — 타겟 도달 못 하면 소멸

    EntityLink _link;

    void Awake() => _link = GetComponent<EntityLink>();

    // EntityLink.LateUpdate 가 position 을 동기화한 뒤 Arrow 회전을 추가 갱신
    // MagicBolt(ArcHeight=0) 는 발사 시 고정 각도를 그대로 유지
    void LateUpdate()
    {
        if (_link.Entity == Entity.Null) return;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        EntityManager em = world.EntityManager;
        if (!em.Exists(_link.Entity)) return;

        var proj = em.GetComponentData<BattleGame.Projectiles.ProjectileComponent>(_link.Entity);
        if (proj.ArcHeight <= 0f) return; // MagicBolt 는 회전 고정

        // 포물선 접선 방향 계산
        // y(t) = lerp(startY, targetY, t) + ArcHeight * sin(t * PI)
        // dy/dt = (targetY - startY) + ArcHeight * PI * cos(t * PI)
        // x(t) = lerp(startX, targetX, t)
        // dx/dt = (targetX - startX)
        float t  = proj.TotalTime > 0f ? math.saturate(proj.ElapsedTime / proj.TotalTime) : 1f;
        float dx = proj.TargetPos.x - proj.StartPos.x;
        float dy = (proj.TargetPos.y - proj.StartPos.y)
                 + proj.ArcHeight * math.PI * math.cos(t * math.PI);

        if (math.abs(dx) > 0.001f || math.abs(dy) > 0.001f)
        {
            float angle = math.degrees(math.atan2(dy, dx));
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>ProjectileSpawnSystem 이 풀에서 꺼낸 직후 호출.</summary>
    /// <param name="arcHeight">포물선 최대 높이. 0이면 직선(MagicBolt), >0이면 포물선(Arrow).</param>
    public void Launch(in ProjectileLaunchRequest req, float arcHeight = 0f)
    {
        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        EntityManager em = world.EntityManager;
        em.CompleteAllTrackedJobs();

        // 발사 방향으로 스프라이트 회전 (+X 기준)
        float3 dir = req.TargetPos - req.AttackerPos;
        if (math.lengthsq(dir) > 0.0001f)
        {
            float angle = math.degrees(math.atan2(dir.y, dir.x));
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // GO 초기 위치
        transform.position = new Vector3(req.AttackerPos.x, req.AttackerPos.y, req.AttackerPos.z);

        float initDist  = math.distance(req.AttackerPos, req.TargetPos);
        float totalTime = req.Speed > 0.0001f ? initDist / req.Speed : 1f;

        var projData = new ProjectileComponent
        {
            TargetEntity   = req.TargetEntity,
            AttackerEntity = req.AttackerEntity,
            TargetPos      = req.TargetPos,
            Damage         = req.Damage,
            Speed          = req.Speed,
            Lifetime       = Lifetime,
            Team           = req.Team,
            DefensePierce  = req.DefensePierce,
            StartPos       = req.AttackerPos,
            ArcHeight      = arcHeight,
            TotalTime      = totalTime,
            ElapsedTime    = 0f,
        };

        if (_link.Entity != Entity.Null && em.Exists(_link.Entity))
        {
            // ── 재사용: 상태값 리셋 ──────────────────────────────
            em.SetComponentData(_link.Entity, LocalTransform.FromPosition(req.AttackerPos));
            em.SetComponentData(_link.Entity, projData);

            if (em.HasComponent<Disabled>(_link.Entity))
                em.RemoveComponent<Disabled>(_link.Entity);
            if (em.HasComponent<ProjectileDestroyTag>(_link.Entity))
                em.RemoveComponent<ProjectileDestroyTag>(_link.Entity);
        }
        else
        {
            // ── 최초 생성 ────────────────────────────────────────
            Entity e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(req.AttackerPos));
            em.AddComponentData(e, projData);
            em.AddComponentObject(e, new ProjectileGoLink
            {
                Go      = gameObject,
                PoolKey = gameObject.name.Replace("(Clone)", "").Trim(),
            });
            _link.Entity = e;
        }
    }
}
