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
    private ParticleSystem[] particleSystems;

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

        ConfigurePhysics();
        PlayParticleSystems();
    }

    // 초기화된 미사일을 매 프레임 목표 지점으로 이동시킨다.
    private void Update()
    {
        if (!initialized)
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

    // 미사일 비주얼 프리팹 내부 파티클을 초기 상태부터 재생한다.
    private void PlayParticleSystems()
    {
        if (particleSystems == null)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystemComp = particleSystems[i];
            if (particleSystemComp == null)
            {
                continue;
            }

            particleSystemComp.Clear(true);
            particleSystemComp.Play(true);
        }
    }

    // 목표 지점까지 미사일을 이동시키고 방향을 갱신한다.
    private void MoveMissile()
    {
        Vector3 currentPosition = transform.position;
        Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, speed * Time.deltaTime);
        ApplyMovement(currentPosition, nextPosition);

        if ((targetPosition - nextPosition).sqrMagnitude <= 0.0001f)
        {
            Impact();
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
    private void Impact()
    {
        initialized = false;
        impactCallback?.Invoke(targetPosition);
        SpawnExplosion();
        Destroy(gameObject, smokeDetachDuration);
    }

    // 폭발 이펙트를 목표 지점에 생성한다.
    private void SpawnExplosion()
    {
        if (explosionPrefab == null)
        {
            return;
        }

        GameObject explosion = Instantiate(explosionPrefab, targetPosition, Quaternion.identity);
        if (explosionDuration > 0f)
        {
            Destroy(explosion, explosionDuration);
        }
    }

}
