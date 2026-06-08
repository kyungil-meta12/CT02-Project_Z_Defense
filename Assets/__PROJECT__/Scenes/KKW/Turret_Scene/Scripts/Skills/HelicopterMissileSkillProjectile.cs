using System;
using UnityEngine;

[DisallowMultipleComponent]
public class HelicopterMissileSkillProjectile : MonoBehaviour
{
    private Vector3 targetPosition;
    private float speed;
    private float explosionDuration;
    private float smokeDetachDuration;
    private GameObject explosionPrefab;
    private Action<Vector3> impactCallback;
    private bool initialized;
    private bool hasImpacted;
    private bool usesInternalImpactTiming;
    private bool hasLoggedRootArrivalWait;
    private ParticleSystem[] particleSystems;
    private ParticleSystem[] impactParticleSystems;
    private int impactParticleSystemCount;
    private int collisionRelayCount;

    // 미사일 이동과 충돌 연출 데이터를 초기화한다.
    public void Initialize(Vector3 targetPosition_, float speed_, GameObject explosionPrefab_, float explosionDuration_, float smokeDetachDuration_, Action<Vector3> impactCallback_)
    {
        targetPosition = targetPosition_;
        speed = Mathf.Max(0.1f, speed_);
        explosionPrefab = explosionPrefab_;
        explosionDuration = Mathf.Max(0f, explosionDuration_);
        smokeDetachDuration = Mathf.Max(0f, smokeDetachDuration_);
        impactCallback = impactCallback_;
        initialized = true;
        hasImpacted = false;
        usesInternalImpactTiming = false;
        hasLoggedRootArrivalWait = false;

        ConfigurePhysics();
        CacheParticleSystems();
        RegisterParticleCollisionRelays();
        usesInternalImpactTiming = impactParticleSystemCount > 0 || collisionRelayCount > 0;
    }

    // 초기화된 미사일을 매 프레임 목표 지점으로 이동시킨다.
    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        if (TryImpactFromInternalExplosion())
        {
            return;
        }

        MoveMissile();
    }

    // 외부 에셋 미사일 프리팹의 물리 이동과 런타임 제어가 충돌하지 않도록 정리한다.
    private void ConfigurePhysics()
    {
        Rigidbody rigidbodyComp = GetComponent<Rigidbody>();
        if (rigidbodyComp == null)
        {
            return;
        }

        rigidbodyComp.isKinematic = true;
        rigidbodyComp.detectCollisions = false;
    }

    // 미사일 비주얼 프리팹 내부 파티클을 캐시하고 폭발 계열 파티클을 분류한다.
    private void CacheParticleSystems()
    {
        if (particleSystems == null)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            impactParticleSystems = new ParticleSystem[particleSystems.Length];
        }

        impactParticleSystemCount = 0;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystemComp = particleSystems[i];
            if (particleSystemComp == null)
            {
                continue;
            }

            if (IsImpactParticleSystem(particleSystemComp))
            {
                impactParticleSystems[impactParticleSystemCount] = particleSystemComp;
                impactParticleSystemCount++;
            }
        }
    }

    // 내부 폭발 파티클이 실제로 재생되기 시작했는지 확인한다.
    private bool TryImpactFromInternalExplosion()
    {
        if (impactParticleSystems == null || impactParticleSystemCount <= 0)
        {
            return false;
        }

        for (int i = 0; i < impactParticleSystemCount; i++)
        {
            ParticleSystem particleSystemComp = impactParticleSystems[i];
            if (particleSystemComp == null)
            {
                continue;
            }

            if (particleSystemComp.particleCount > 0)
            {
                Impact(particleSystemComp.transform.position);
                return true;
            }
        }

        return false;
    }

    // 이름 기준으로 FX_Missile_01 내부 폭발 계열 파티클인지 판단한다.
    private bool IsImpactParticleSystem(ParticleSystem particleSystemComp)
    {
        string particleName = particleSystemComp.gameObject.name;
        return particleName.IndexOf("explosion", StringComparison.OrdinalIgnoreCase) >= 0
               || particleName.IndexOf("blast", StringComparison.OrdinalIgnoreCase) >= 0
               || particleName.IndexOf("flash", StringComparison.OrdinalIgnoreCase) >= 0
               || particleName.IndexOf("debris", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // 파티클 충돌 이벤트를 발사체 충돌 처리로 전달할 릴레이를 등록한다.
    private void RegisterParticleCollisionRelays()
    {
        collisionRelayCount = 0;
        if (particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystemComp = particleSystems[i];
            if (particleSystemComp == null)
            {
                continue;
            }

            ParticleSystem.CollisionModule collisionModule = particleSystemComp.collision;
            if (!collisionModule.enabled)
            {
                continue;
            }

            collisionModule.sendCollisionMessages = true;

            HelicopterMissileParticleCollisionRelay relay = particleSystemComp.GetComponent<HelicopterMissileParticleCollisionRelay>();
            if (relay == null)
            {
                relay = particleSystemComp.gameObject.AddComponent<HelicopterMissileParticleCollisionRelay>();
            }

            relay.Initialize(this, particleSystemComp);
            collisionRelayCount++;
        }
    }

    // FX_Missile_01 파티클이 실제 충돌한 위치를 기준으로 폭발과 데미지를 실행한다.
    public void NotifyParticleCollision(Vector3 impactPosition)
    {
        if (!initialized || hasImpacted)
        {
            return;
        }

        Impact(impactPosition);
    }

    // 목표 지점까지 미사일을 이동시키고 방향을 갱신한다.
    private void MoveMissile()
    {
        Vector3 currentPosition = transform.position;
        Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, speed * Time.deltaTime);
        ApplyMovement(currentPosition, nextPosition);

        if ((targetPosition - nextPosition).sqrMagnitude > 0.0001f)
        {
            return;
        }

        if (!usesInternalImpactTiming)
        {
            Impact(targetPosition);
            return;
        }

        if (!hasLoggedRootArrivalWait)
        {
            hasLoggedRootArrivalWait = true;
            Debug.LogWarning("FX_Missile_01 루트가 목표점에 도착했지만 내부 폭발/파티클 충돌 신호가 아직 감지되지 않아 데미지 처리를 대기합니다.", this);
        }
    }

    // 미사일 위치와 진행 방향 회전을 적용한다.
    private void ApplyMovement(Vector3 previousPosition, Vector3 nextPosition)
    {
        Vector3 direction = nextPosition - previousPosition;

        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        transform.position = nextPosition;
    }

    // 미사일 충돌 처리와 폭발 이펙트를 실행한다.
    private void Impact(Vector3 impactPosition)
    {
        if (hasImpacted)
        {
            return;
        }

        hasImpacted = true;
        initialized = false;
        transform.position = impactPosition;
        impactCallback?.Invoke(impactPosition);
        SpawnExplosion(impactPosition);
        Destroy(gameObject, smokeDetachDuration);
    }

    // 폭발 이펙트를 목표 지점에 생성한다.
    private void SpawnExplosion(Vector3 impactPosition)
    {
        if (explosionPrefab == null)
        {
            return;
        }

        GameObject explosion = Instantiate(explosionPrefab, impactPosition, Quaternion.identity);
        if (explosionDuration > 0f)
        {
            Destroy(explosion, explosionDuration);
        }
    }

}
