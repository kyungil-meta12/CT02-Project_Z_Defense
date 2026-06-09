using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HelicopterMissileParticleCollisionRelay : MonoBehaviour
{
    private const int DEFAULT_COLLISION_EVENT_CAPACITY = 16;

    private readonly List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>(DEFAULT_COLLISION_EVENT_CAPACITY);
    private HelicopterMissileSkillProjectile owner;
    private ParticleSystem particleSystemComp;

    // 파티클 충돌 이벤트를 전달할 발사체와 파티클 시스템을 연결한다.
    public void Initialize(HelicopterMissileSkillProjectile owner_, ParticleSystem particleSystemComp_)
    {
        owner = owner_;
        particleSystemComp = particleSystemComp_;
        EnsureCollisionEventCapacity();
    }

    // 풀 재사용 시 이전 소유자와 충돌 이벤트 참조를 정리한다.
    private void OnDisable()
    {
        owner = null;
        particleSystemComp = null;
        collisionEvents.Clear();
        if (collisionEvents.Capacity > DEFAULT_COLLISION_EVENT_CAPACITY)
        {
            collisionEvents.Capacity = DEFAULT_COLLISION_EVENT_CAPACITY;
        }
    }

    // 파티클이 바닥/콜라이더에 닿은 실제 위치를 발사체에 전달한다.
    private void OnParticleCollision(GameObject other)
    {
        if (owner == null || particleSystemComp == null)
        {
            return;
        }

        Vector3 impactPosition = transform.position;
        collisionEvents.Clear();
        int eventCount = ParticlePhysicsExtensions.GetCollisionEvents(particleSystemComp, other, collisionEvents);
        if (eventCount > 0)
        {
            impactPosition = collisionEvents[0].intersection;
        }

        owner.NotifyParticleCollision(impactPosition);
    }

    // 파티클 충돌 이벤트 리스트가 런타임 중 작은 용량으로 시작하지 않게 보장한다.
    private void EnsureCollisionEventCapacity()
    {
        if (collisionEvents.Capacity < DEFAULT_COLLISION_EVENT_CAPACITY)
        {
            collisionEvents.Capacity = DEFAULT_COLLISION_EVENT_CAPACITY;
        }
    }
}
