using System;
using UnityEngine;

[DisallowMultipleComponent]
public class HelicopterMissileSkillProjectile : MonoBehaviour
{
    private Vector3 targetPosition;
    private float speed;
    private float explosionDuration;
    private float smokeDetachDuration;
    private GameObject smokePrefab;
    private GameObject explosionPrefab;
    private GameObject smokeInstance;
    private Action<Vector3> impactCallback;
    private bool initialized;

    // 미사일 이동과 충돌 연출 데이터를 초기화한다.
    public void Initialize(Vector3 targetPosition_, float speed_, GameObject smokePrefab_, GameObject explosionPrefab_, float explosionDuration_, float smokeDetachDuration_, Action<Vector3> impactCallback_)
    {
        targetPosition = targetPosition_;
        speed = Mathf.Max(0.1f, speed_);
        smokePrefab = smokePrefab_;
        explosionPrefab = explosionPrefab_;
        explosionDuration = Mathf.Max(0f, explosionDuration_);
        smokeDetachDuration = Mathf.Max(0f, smokeDetachDuration_);
        impactCallback = impactCallback_;
        initialized = true;

        ConfigurePhysics();
        SpawnSmoke();
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

    // 미사일 연기 이펙트를 미사일 하위에 생성한다.
    private void SpawnSmoke()
    {
        if (smokePrefab == null)
        {
            return;
        }

        smokeInstance = Instantiate(smokePrefab, transform);
        smokeInstance.transform.localPosition = Vector3.zero;
        smokeInstance.transform.localRotation = Quaternion.identity;
    }

    // 목표 지점까지 미사일을 이동시키고 방향을 갱신한다.
    private void MoveMissile()
    {
        Vector3 currentPosition = transform.position;
        Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, speed * Time.deltaTime);
        Vector3 direction = nextPosition - currentPosition;

        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        transform.position = nextPosition;

        if ((targetPosition - nextPosition).sqrMagnitude <= 0.0001f)
        {
            Impact();
        }
    }

    // 미사일 충돌 처리와 폭발 이펙트를 실행한다.
    private void Impact()
    {
        initialized = false;
        impactCallback?.Invoke(targetPosition);
        SpawnExplosion();
        DetachSmoke();
        Destroy(gameObject);
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

    // 연기 이펙트가 바로 끊기지 않도록 잠시 유지한다.
    private void DetachSmoke()
    {
        if (smokeInstance == null)
        {
            return;
        }

        smokeInstance.transform.SetParent(null);
        Destroy(smokeInstance, smokeDetachDuration);
    }
}
