using System;
using UnityEngine;

[DisallowMultipleComponent]
public class HelicopterMissileSkillProjectile : MonoBehaviour
{
    private Vector3 targetPosition;
    private float rootMoveSpeed;
    private float explosionDuration;
    private float destroyDelayAfterImpact;
    private float impactArmDelay;
    private float impactMinTravelDistance;
    private float elapsedTime;
    private GameObject explosionPrefab;
    private Action<Vector3> impactCallback;
    private bool initialized;
    private bool hasImpacted;
    private ParticleSystem[] particleSystems;
    private Vector3 spawnPosition;

    // 미사일 이동과 충돌 연출 데이터를 초기화한다.
    public void Initialize(Vector3 targetPosition_, float rootMoveSpeed_, GameObject explosionPrefab_, float explosionDuration_, float destroyDelayAfterImpact_, float impactArmDelay_, float impactMinTravelDistance_, Action<Vector3> impactCallback_)
    {
        spawnPosition = transform.position;
        targetPosition = targetPosition_;
        rootMoveSpeed = Mathf.Max(0.1f, rootMoveSpeed_);
        explosionPrefab = explosionPrefab_;
        explosionDuration = Mathf.Max(0f, explosionDuration_);
        destroyDelayAfterImpact = Mathf.Max(0f, destroyDelayAfterImpact_);
        impactArmDelay = Mathf.Max(0f, impactArmDelay_);
        impactMinTravelDistance = Mathf.Max(0f, impactMinTravelDistance_);
        elapsedTime = 0f;
        impactCallback = impactCallback_;
        initialized = true;
        hasImpacted = false;

        ConfigurePhysics();
        CacheParticleSystems();
        RegisterParticleCollisionRelays();
    }

    // 풀 반환 시 이전 충돌 콜백과 실행 상태를 정리한다.
    private void OnDisable()
    {
        initialized = false;
        hasImpacted = false;
        impactCallback = null;
    }

    // 초기화된 미사일을 매 프레임 목표 지점으로 이동시킨다.
    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        elapsedTime += Time.deltaTime;
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

    // 미사일 비주얼 프리팹 내부 파티클을 캐시한다.
    private void CacheParticleSystems()
    {
        if (particleSystems == null)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    // 파티클 충돌 이벤트를 발사체 충돌 처리로 전달할 릴레이를 등록한다.
    private void RegisterParticleCollisionRelays()
    {
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
        }
    }

    // FX_Missile_01 파티클이 실제 충돌한 위치를 기준으로 폭발과 데미지를 실행한다.
    public void NotifyParticleCollision(Vector3 impactPosition)
    {
        if (!initialized || hasImpacted)
        {
            return;
        }

        if (!CanAcceptImpact(impactPosition))
        {
            return;
        }

        Impact(impactPosition);
    }

    // 미사일 생성 직후 발생하는 잘못된 파티클 충돌을 무시할지 판단한다.
    private bool CanAcceptImpact(Vector3 impactPosition)
    {
        if (elapsedTime < impactArmDelay)
        {
            return false;
        }

        if ((impactPosition - spawnPosition).sqrMagnitude < impactMinTravelDistance * impactMinTravelDistance)
        {
            return false;
        }

        return true;
    }

    // 파티클 충돌 설정과 무관하게 미사일 루트를 목표 지점까지 이동시킨다.
    private void MoveMissile()
    {
        Vector3 currentPosition = transform.position;
        Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, rootMoveSpeed * Time.deltaTime);
        ApplyMovement(currentPosition, nextPosition);

        if ((targetPosition - nextPosition).sqrMagnitude > 0.0001f)
        {
            return;
        }

        Impact(targetPosition);
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
        PooledProjectileReturner.ReturnOrDestroy(gameObject, destroyDelayAfterImpact);
    }

    // 폭발 이펙트를 목표 지점에 생성한다.
    private void SpawnExplosion(Vector3 impactPosition)
    {
        if (explosionPrefab == null)
        {
            return;
        }

        PooledObjectUtility.SpawnEffect(explosionPrefab, impactPosition, Quaternion.identity, explosionDuration);
    }

}
