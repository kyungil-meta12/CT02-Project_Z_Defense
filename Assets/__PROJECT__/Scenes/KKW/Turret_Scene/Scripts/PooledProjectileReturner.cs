using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PooledProjectileReturner : MonoBehaviour
{
    private PoolObject poolObject;
    private Rigidbody rigidbodyComp;
    private TrailRenderer[] trailRenderers;
    private ParticleSystem[] particleSystems;
    private Coroutine returnRoutine;
    private bool isReturning;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        isReturning = false;
        StopReturnRoutine();
        ResetReusableComponents();
    }

    private void OnDisable()
    {
        StopReturnRoutine();
    }

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

    public static void ReturnOrDestroy(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        var returner = target.GetComponent<PooledProjectileReturner>();
        if (returner != null)
        {
            returner.ReturnNow();
            return;
        }

        UnityEngine.Object.Destroy(target);
    }

    public static void ReturnOrDestroy(GameObject target, float delay)
    {
        if (target == null)
        {
            return;
        }

        var returner = target.GetComponent<PooledProjectileReturner>();
        if (returner != null)
        {
            returner.ReturnAfter(delay);
            return;
        }

        UnityEngine.Object.Destroy(target, delay);
    }

    private IEnumerator ReturnAfterRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        returnRoutine = null;
        ReturnNow();
    }

    private void CachePoolObject()
    {
        if (poolObject == null)
        {
            poolObject = GetComponent<PoolObject>();
        }
    }

    private void CacheComponents()
    {
        CachePoolObject();
        rigidbodyComp = GetComponent<Rigidbody>();
        trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void StopReturnRoutine()
    {
        if (returnRoutine == null)
        {
            return;
        }

        StopCoroutine(returnRoutine);
        returnRoutine = null;
    }

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
