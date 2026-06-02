using UnityEngine;

public static class PooledObjectUtility
{
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }

        MemoryPool memoryPool = GetMemoryPool();
        if (memoryPool == null)
        {
            Debug.LogWarning($"[PooledObjectUtility] MemoryPool is missing. Instantiating {prefab.name} without pooling.");
            return UnityEngine.Object.Instantiate(prefab, position, rotation);
        }

        return memoryPool.GetInstance(prefab, position, rotation);
    }

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

        return projectile;
    }

    public static GameObject SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, float duration)
    {
        GameObject effect = Spawn(prefab, position, rotation);
        if (effect == null)
        {
            return null;
        }

        var returner = effect.GetComponent<PooledEffectReturner>();
        if (returner == null)
        {
            returner = effect.AddComponent<PooledEffectReturner>();
        }

        returner.Init(duration);
        return effect;
    }

    private static MemoryPool GetMemoryPool()
    {
        return MemoryPool.Inst;
    }
}
