using UnityEngine;

/// <summary>
/// Poison 처형 사망 폭발 이펙트 생성과 약한 범위 중독 적용을 공통 처리한다.
/// </summary>
public static class PoisonDeathBurstEffectUtility
{
    private const int BURST_HIT_BUFFER_SIZE = 32;
    private static readonly Collider[] BurstHitBuffer = new Collider[BURST_HIT_BUFFER_SIZE];
    private static readonly IPoisonStatusEffectReceiver[] PoisonTargets = new IPoisonStatusEffectReceiver[BURST_HIT_BUFFER_SIZE];

    // 지정 위치에서 Poison 처형 사망 폭발 이펙트와 약한 범위 중독을 적용한다
    public static void TriggerDeathBurst(PoisonDeathBurstProfileSO profile, Vector3 position, IDamageable excludedTarget)
    {
        if (profile == null || !profile.HasBurst)
        {
            return;
        }

        Vector3 burstPosition = position + profile.effectPositionOffset;
        SpawnBurstEffect(profile, burstPosition);
        ApplyWeakPoison(profile, burstPosition, excludedTarget);
    }

    // Poison 사망 폭발 이펙트를 풀링 기반으로 생성한다
    private static void SpawnBurstEffect(PoisonDeathBurstProfileSO profile, Vector3 position)
    {
        if (profile.burstEffectPrefab == null)
        {
            return;
        }

        PooledObjectUtility.SpawnEffect(profile.burstEffectPrefab, position, Quaternion.identity, profile.effectDuration);
    }

    // 폭발 범위 안의 생존 대상에게 약한 Poison 상태를 중복 없이 적용한다
    private static void ApplyWeakPoison(PoisonDeathBurstProfileSO profile, Vector3 position, IDamageable excludedTarget)
    {
        if (!profile.HasWeakPoison)
        {
            return;
        }

        PoisonStatusPayload weakPoisonPayload = profile.CreateWeakPoisonPayload();
        int hitCount = Physics.OverlapSphereNonAlloc(
            position,
            profile.radius,
            BurstHitBuffer,
            profile.targetLayerMask,
            QueryTriggerInteraction.Collide);
        int poisonTargetCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = BurstHitBuffer[i];
            BurstHitBuffer[i] = null;

            if (hitCollider == null)
            {
                continue;
            }

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable == excludedTarget || !damageable.IsAlive)
            {
                continue;
            }

            IPoisonStatusEffectReceiver poisonReceiver = damageable as IPoisonStatusEffectReceiver;
            if (poisonReceiver == null || ContainsPoisonTarget(poisonReceiver, poisonTargetCount))
            {
                continue;
            }

            poisonReceiver.ApplyPoisonStatus(weakPoisonPayload);
            PoisonTargets[poisonTargetCount] = poisonReceiver;
            poisonTargetCount++;
        }

        ClearPoisonTargets(poisonTargetCount);
    }

    // 이번 폭발에서 같은 Poison 대상이 이미 처리됐는지 확인한다
    private static bool ContainsPoisonTarget(IPoisonStatusEffectReceiver poisonReceiver, int poisonTargetCount)
    {
        for (int i = 0; i < poisonTargetCount; i++)
        {
            if (PoisonTargets[i] == poisonReceiver)
            {
                return true;
            }
        }

        return false;
    }

    // 다음 폭발 판정을 위해 중복 방지 대상 배열을 비운다
    private static void ClearPoisonTargets(int poisonTargetCount)
    {
        for (int i = 0; i < poisonTargetCount; i++)
        {
            PoisonTargets[i] = null;
        }
    }
}
