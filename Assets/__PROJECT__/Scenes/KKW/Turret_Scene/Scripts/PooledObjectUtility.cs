using UnityEngine;

/// <summary>
/// 프로젝트 런타임에서 풀링 오브젝트, 투사체, 이펙트를 생성하고 반환 보조 컴포넌트를 보장한다.
/// </summary>
public static class PooledObjectUtility
{
    // 일반 풀링 오브젝트를 지정 위치와 회전으로 생성한다
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }

        MemoryPool memoryPool = GetMemoryPool();
        if (memoryPool == null)
        {
            Debug.LogWarning("[PooledObjectUtility] MemoryPool이 없어 풀링 없이 생성합니다. 대상: " + prefab.name);
            return UnityEngine.Object.Instantiate(prefab, position, rotation);
        }

        return memoryPool.GetInstance(prefab, position, rotation);
    }

    // 투사체를 생성하고 풀 반환 및 관통 보정 컴포넌트를 보장한다
    public static GameObject SpawnProjectile(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject projectile = Spawn(prefab, position, rotation);
        if (projectile == null)
        {
            return null;
        }

        if (projectile.GetComponent<PooledProjectileReturner>() == null)
        {
            projectile.AddComponent<PooledProjectileReturner>();
        }

        ProjectileLifetimePolicy lifetimePolicy = projectile.GetComponent<ProjectileLifetimePolicy>();
        if (lifetimePolicy == null)
        {
            lifetimePolicy = projectile.AddComponent<ProjectileLifetimePolicy>();
        }

        if (projectile.GetComponent<HovlProjectilePierceGuard>() == null)
        {
            projectile.AddComponent<HovlProjectilePierceGuard>();
        }

        lifetimePolicy.ApplyPolicy();
        return projectile;
    }

    // 이펙트를 생성하고 지정 시간 뒤 풀로 반환되도록 초기화한다
    public static GameObject SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, float duration)
    {
        return SpawnEffect(prefab, position, rotation, duration, 0.0f);
    }

    // 이펙트를 생성하고 지정 시간 뒤 선택적 페이드아웃을 거쳐 풀로 반환되도록 초기화한다
    public static GameObject SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, float duration, float fadeOutDuration)
    {
        GameObject effect = Spawn(prefab, position, rotation);
        if (effect == null)
        {
            return null;
        }

        PooledEffectReturner returner = effect.GetComponent<PooledEffectReturner>();
        if (returner == null)
        {
            returner = effect.AddComponent<PooledEffectReturner>();
        }

        returner.Init(duration, fadeOutDuration);
        return effect;
    }

    // 풀에서 꺼낸 오브젝트는 반환하고, 일반 생성 오브젝트는 제거한다.
    public static void ReturnOrDestroy(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        PoolObject poolObject = target.GetComponent<PoolObject>();
        if (poolObject != null && poolObject.OriginStack != null)
        {
            poolObject.ReturnToPool();
            return;
        }

        UnityEngine.Object.Destroy(target);
    }

    // 현재 활성화된 메모리 풀 인스턴스를 반환한다
    private static MemoryPool GetMemoryPool()
    {
        return MemoryPool.Inst;
    }
}
