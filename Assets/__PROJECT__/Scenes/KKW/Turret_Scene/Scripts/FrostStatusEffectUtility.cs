using UnityEngine;

/// <summary>
/// Frost 상태 효과의 빙결 이펙트 생성과 범위 데미지 적용을 공통 처리한다.
/// </summary>
public static class FrostStatusEffectUtility
{
    private const int EXPLOSION_HIT_BUFFER_SIZE = 32;
    private static readonly Collider[] ExplosionHitBuffer = new Collider[EXPLOSION_HIT_BUFFER_SIZE];
    private static readonly IDamageable[] DamagedTargets = new IDamageable[EXPLOSION_HIT_BUFFER_SIZE];

    // 빙결 조건이 충족된 위치에 이펙트와 범위 데미지를 적용한다
    public static void TriggerFreezeExplosion(FrostStatusPayload payload, Vector3 position)
    {
        GameObject effectObject = SpawnFreezeEffect(payload, position);
        ScheduleExplosionDamage(payload, position, effectObject);
    }

    // 빙결 이펙트 프리팹을 풀링 기반으로 생성한다
    private static GameObject SpawnFreezeEffect(FrostStatusPayload payload, Vector3 position)
    {
        if (payload.freezeEffectPrefab == null)
        {
            return null;
        }

        float effectDuration = Mathf.Max(payload.freezeEffectDuration, payload.freezeExplosionDamageDelay + 0.1f);
        return PooledObjectUtility.SpawnEffect(payload.freezeEffectPrefab, position, Quaternion.identity, effectDuration);
    }

    // 빙결 이펙트의 폭발 타이밍에 맞춰 범위 데미지를 예약한다
    private static void ScheduleExplosionDamage(FrostStatusPayload payload, Vector3 position, GameObject effectObject)
    {
        if (payload.freezeExplosionDamage <= 0.0f || payload.freezeExplosionRadius <= 0.0f)
        {
            return;
        }

        if (payload.freezeExplosionDamageDelay <= 0.0f)
        {
            ApplyExplosionDamage(payload, position);
            return;
        }

        if (effectObject == null)
        {
            Debug.LogWarning("[FrostStatusEffectUtility] 빙결 이펙트가 없어 폭발 데미지를 즉시 적용합니다. Freeze Effect Prefab 연결을 확인해주세요.");
            ApplyExplosionDamage(payload, position);
            return;
        }

        FrostFreezeExplosionDamageTimer damageTimer = effectObject.GetComponent<FrostFreezeExplosionDamageTimer>();
        if (damageTimer == null)
        {
            damageTimer = effectObject.AddComponent<FrostFreezeExplosionDamageTimer>();
        }

        damageTimer.Init(payload, position, payload.freezeExplosionDamageDelay);
    }

    // 빙결 폭발 범위 안의 생존 대상에게 중복 없이 데미지를 적용한다
    public static void ApplyExplosionDamage(FrostStatusPayload payload, Vector3 position)
    {
        if (payload.freezeExplosionDamage <= 0.0f || payload.freezeExplosionRadius <= 0.0f)
        {
            return;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(
            position,
            payload.freezeExplosionRadius,
            ExplosionHitBuffer,
            payload.freezeExplosionLayerMask,
            QueryTriggerInteraction.Collide);
        int damagedCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = ExplosionHitBuffer[i];
            ExplosionHitBuffer[i] = null;

            if (hitCollider == null)
            {
                continue;
            }

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive || ContainsDamageTarget(damageable, damagedCount))
            {
                continue;
            }

            damageable.TakeDamage(payload.freezeExplosionDamage);
            ApplyExplosionSlow(payload, damageable);
            DamagedTargets[damagedCount] = damageable;
            damagedCount++;
        }

        ClearDamagedTargets(damagedCount);
    }

    // 빙결 폭발 데미지를 받은 대상에게 짧은 추가 슬로우를 적용한다
    private static void ApplyExplosionSlow(FrostStatusPayload payload, IDamageable damageable)
    {
        if (payload.freezeExplosionSlowRatio <= 0.0f || payload.freezeExplosionSlowDuration <= 0.0f)
        {
            return;
        }

        IFrostStatusEffectReceiver frostReceiver = damageable as IFrostStatusEffectReceiver;
        if (frostReceiver == null)
        {
            return;
        }

        FrostStatusPayload slowPayload = new FrostStatusPayload
        {
            tickInterval = 0.0f,
            slowBuildUpDuration = 0.0f,
            maxSlowRatio = payload.freezeExplosionSlowRatio,
            slowHoldDuration = payload.freezeExplosionSlowDuration,
            freezeTriggerRatio = 1.0f,
            canTriggerFreeze = false
        };

        frostReceiver.ApplyFrostStatus(slowPayload);
    }

    // 이번 폭발에서 같은 대상이 이미 데미지를 받았는지 확인한다
    private static bool ContainsDamageTarget(IDamageable damageable, int damagedCount)
    {
        for (int i = 0; i < damagedCount; i++)
        {
            if (DamagedTargets[i] == damageable)
            {
                return true;
            }
        }

        return false;
    }

    // 다음 폭발 판정을 위해 중복 방지 대상 배열을 비운다
    private static void ClearDamagedTargets(int damagedCount)
    {
        for (int i = 0; i < damagedCount; i++)
        {
            DamagedTargets[i] = null;
        }
    }
}
