using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// ============================================================
//  EntityLink.cs
//  GameObject ↔ ECS Entity 연결 + Transform 동기화 컴포넌트.
//
//  Pool 이 Instantiate 로 GO 를 생성하면 Baker 가 실행되지 않으므로
//  RuntimeBridge 가 Start() 에서 직접 Entity 를 생성하고 Entity 에 저장한다.
//
//  LateUpdate : ECS LocalTransform.Position → GameObject.transform.position
//  OnDisable  : 풀 반납 시 Entity 파괴 (다음 스폰 시 RuntimeBridge 가 재생성)
// ============================================================

public class EntityLink : MonoBehaviour
{
    [HideInInspector] public Entity Entity;

    /// <summary>
    /// false 로 설정하면 LateUpdate 의 ECS→Transform 위치 동기화를 건너뜁니다.
    /// UnitFeedback 이 사망 연출 중 자체적으로 위치를 이동할 때 사용합니다.
    /// </summary>
    [HideInInspector] public bool SyncPosition = true;

    // 한 프레임에 CompleteAllTrackedJobs 를 여러 번 호출하지 않도록 프레임 캐싱
    static int _lastCompletedFrame = -1;

    void LateUpdate()
    {
        if (!SyncPosition) return;
        if (Entity == Entity.Null) return;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        EntityManager em = world.EntityManager;

        if (_lastCompletedFrame != Time.frameCount)
        {
            em.CompleteAllTrackedJobs();
            _lastCompletedFrame = Time.frameCount;
        }

        if (!em.Exists(Entity)) return;

        float3 pos = em.GetComponentData<LocalTransform>(Entity).Position;
        transform.position = new Vector3(pos.x, pos.y, pos.z);
    }

    void OnEnable()
    {
        SyncPosition = true;
    }

    void OnDisable()
    {
        if (Entity == Entity.Null) return;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        EntityManager em = world.EntityManager;
        em.CompleteAllTrackedJobs();

        // Entity 파괴 대신 Disabled 추가 → 모든 ECS 쿼리에서 자동 제외
        // 재스폰 시 Initialize() 가 상태값 리셋 후 Disabled 제거
        if (em.Exists(Entity) && !em.HasComponent<Disabled>(Entity))
            em.AddComponent<Disabled>(Entity);
    }
}

// Baker 는 SubScene 용으로 유지 (SubScene 사용 시 자동 동작)
public class EntityLinkBaker : Baker<EntityLink>
{
    public override void Bake(EntityLink authoring)
    {
        authoring.Entity = GetEntity(TransformUsageFlags.Dynamic);
    }
}
