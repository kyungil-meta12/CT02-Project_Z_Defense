using System.Collections;
using UnityEngine;

/// <summary>
/// 풀링된 투사체의 즉시 반환, 지연 반환, 재사용 상태 초기화를 담당한다.
/// </summary>
[DisallowMultipleComponent]
public class PooledProjectileReturner : MonoBehaviour
{
    private PoolObject poolObject;
    private Rigidbody rigidbodyComp;
    private TrailRenderer[] trailRenderers;
    private ParticleSystem[] particleSystems;
    private Coroutine returnRoutine;
    private bool isReturning;

    // 투사체 반환에 필요한 컴포넌트를 캐시한다
    private void Awake()
    {
        CacheComponents();
    }

    // 풀에서 활성화될 때 반환 상태와 재사용 가능한 렌더링 상태를 초기화한다
    private void OnEnable()
    {
        isReturning = false;
        StopReturnRoutine();
        ResetReusableComponents();
    }

    // 비활성화될 때 예약된 반환 루틴을 정리한다
    private void OnDisable()
    {
        StopReturnRoutine();
    }

    // 투사체를 즉시 풀로 반환하거나 일반 오브젝트면 제거한다
    public void ReturnNow()
    {
        if (isReturning)
        {
            return;
        }

        isReturning = true;
        StopReturnRoutine();
        CachePoolObject();

        if (poolObject != null && poolObject.OriginStack != null)
        {
            poolObject.ReturnToPool();
            return;
        }

        Destroy(gameObject);
    }

    // 지정 시간 뒤 투사체를 풀로 반환하거나 일반 오브젝트면 제거한다
    public void ReturnAfter(float delay)
    {
        StopReturnRoutine();

        if (delay <= 0.0f)
        {
            ReturnNow();
            return;
        }

        returnRoutine = StartCoroutine(ReturnAfterRoutine(delay));
    }

    // 지정 오브젝트에 반환 컴포넌트가 있으면 풀 반환하고 없으면 제거한다
    public static void ReturnOrDestroy(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        PooledProjectileReturner returner = target.GetComponent<PooledProjectileReturner>();
        if (returner != null)
        {
            returner.ReturnNow();
            return;
        }

        UnityEngine.Object.Destroy(target);
    }

    // 지정 오브젝트를 지연 반환하거나 반환 컴포넌트가 없으면 지연 제거한다
    public static void ReturnOrDestroy(GameObject target, float delay)
    {
        if (target == null)
        {
            return;
        }

        PooledProjectileReturner returner = target.GetComponent<PooledProjectileReturner>();
        if (returner != null)
        {
            returner.ReturnAfter(delay);
            return;
        }

        UnityEngine.Object.Destroy(target, delay);
    }

    // 지정 시간이 지난 뒤 투사체를 반환한다
    private IEnumerator ReturnAfterRoutine(float delay)
    {
        float elapsedTime = 0.0f;
        while (elapsedTime < delay)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        returnRoutine = null;
        ReturnNow();
    }

    // 풀 오브젝트 참조를 캐시한다
    private void CachePoolObject()
    {
        if (poolObject == null)
        {
            poolObject = GetComponent<PoolObject>();
        }
    }

    // 반환 시 초기화할 컴포넌트 참조를 캐시한다
    private void CacheComponents()
    {
        CachePoolObject();
        rigidbodyComp = GetComponent<Rigidbody>();
        trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    // 진행 중인 지연 반환 루틴을 중지한다
    private void StopReturnRoutine()
    {
        if (returnRoutine == null)
        {
            return;
        }

        StopCoroutine(returnRoutine);
        returnRoutine = null;
    }

    // 풀 재사용 전 물리 속도, 트레일, 파티클 잔상을 초기화한다
    private void ResetReusableComponents()
    {
        if (rigidbodyComp != null)
        {
#if UNITY_6000_0_OR_NEWER
            rigidbodyComp.linearVelocity = Vector3.zero;
#else
            rigidbodyComp.velocity = Vector3.zero;
#endif
            rigidbodyComp.angularVelocity = Vector3.zero;
        }

        if (trailRenderers == null)
        {
            trailRenderers = System.Array.Empty<TrailRenderer>();
        }

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            if (trailRenderers[i] != null)
            {
                trailRenderers[i].Clear();
            }
        }

        if (particleSystems == null)
        {
            particleSystems = System.Array.Empty<ParticleSystem>();
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] != null)
            {
                particleSystems[i].Clear(true);
            }
        }
    }
}
