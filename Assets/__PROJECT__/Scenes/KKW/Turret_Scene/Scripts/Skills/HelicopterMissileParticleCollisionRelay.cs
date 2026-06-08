using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HelicopterMissileParticleCollisionRelay : MonoBehaviour
{
    private readonly List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>(4);
    private HelicopterMissileSkillProjectile owner;
    private ParticleSystem particleSystemComp;

    // 파티클 충돌 이벤트를 전달할 발사체와 파티클 시스템을 연결한다.
    public void Initialize(HelicopterMissileSkillProjectile owner_, ParticleSystem particleSystemComp_)
    {
        owner = owner_;
        particleSystemComp = particleSystemComp_;
    }

    // 파티클이 바닥/콜라이더에 닿은 실제 위치를 발사체에 전달한다.
    private void OnParticleCollision(GameObject other)
    {
        if (owner == null || particleSystemComp == null)
        {
            return;
        }

        Vector3 impactPosition = transform.position;
        int eventCount = ParticlePhysicsExtensions.GetCollisionEvents(particleSystemComp, other, collisionEvents);
        if (eventCount > 0)
        {
            impactPosition = collisionEvents[0].intersection;
        }

        owner.NotifyParticleCollision(impactPosition);
    }
}
