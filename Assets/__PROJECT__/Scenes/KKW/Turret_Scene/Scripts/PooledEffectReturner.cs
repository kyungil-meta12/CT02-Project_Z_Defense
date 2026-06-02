using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PooledEffectReturner : MonoBehaviour
{
    private PoolObject poolObject;
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
    }

    private void OnDisable()
    {
        StopReturnRoutine();
    }

    public void Init(float duration)
    {
        isReturning = false;
        StopReturnRoutine();
        PlayParticles();

        if (duration <= 0.0f)
        {
            ReturnNow();
            return;
        }

        returnRoutine = StartCoroutine(ReturnAfterRoutine(duration));
    }

    private IEnumerator ReturnAfterRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        returnRoutine = null;
        ReturnNow();
    }

    private void ReturnNow()
    {
        if (isReturning)
        {
            return;
        }

        isReturning = true;
        CachePoolObject();

        if (poolObject != null && poolObject.OriginStack != null)
        {
            poolObject.ReturnToPool();
            return;
        }

        Destroy(gameObject);
    }

    private void PlayParticles()
    {
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    private void CacheComponents()
    {
        CachePoolObject();
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void CachePoolObject()
    {
        if (poolObject == null)
        {
            poolObject = GetComponent<PoolObject>();
        }
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
}
