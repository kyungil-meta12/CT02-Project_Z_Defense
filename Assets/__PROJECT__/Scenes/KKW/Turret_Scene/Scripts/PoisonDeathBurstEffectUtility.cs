using UnityEngine;
using ProjectZDefense.StatusEffects;
using ProjectZDefense.Audio;

/// <summary>
/// Poison 처형 사망 폭발 이펙트 생성과 약한 범위 중독 적용을 공통 처리한다.
/// </summary>
public static class PoisonDeathBurstEffectUtility
{
    private const int BURST_HIT_BUFFER_SIZE = 32;
    private static readonly Collider[] BurstHitBuffer = new Collider[BURST_HIT_BUFFER_SIZE];
    private static readonly IPoisonStatusEffectReceiver[] PoisonTargets = new IPoisonStatusEffectReceiver[BURST_HIT_BUFFER_SIZE];

    // 지정 위치에서 Poison 처형 사망 폭발 이펙트와 약한 범위 중독을 적용한다
    public static void TriggerDeathBurst(PoisonStatusPayload sourcePayload, Vector3 position, IDamageable excludedTarget)
    {
        PoisonDeathBurstProfileSO profile = sourcePayload.deathBurstProfile;
        if (profile == null || !HasDeathBurst(sourcePayload, profile))
        {
            return;
        }

        Vector3 burstPosition = position + profile.effectPositionOffset;
        PlayBurstAudio(sourcePayload, burstPosition);
        SpawnBurstEffect(profile, burstPosition);
        ApplyWeakPoison(sourcePayload, profile, burstPosition, excludedTarget);
    }

    // Poison 사망 폭발 위치에서 상태 폭발 사운드를 재생한다
    private static void PlayBurstAudio(PoisonStatusPayload sourcePayload, Vector3 position)
    {
        if (sourcePayload.damageSource == null)
        {
            return;
        }

        TurretAudioController audioController = sourcePayload.damageSource.GetComponent<TurretAudioController>();
        if (audioController == null)
        {
            return;
        }

        audioController.PlayAt(TurretAudioEvent.StatusBurst, position);
    }

    // Poison 사망 폭발 이펙트를 풀링 기반으로 생성한다
    private static void SpawnBurstEffect(PoisonDeathBurstProfileSO profile, Vector3 position)
    {
        if (profile.burstEffectPrefab == null)
        {
            return;
        }

        PooledObjectUtility.SpawnEffect(profile.burstEffectPrefab, position, Quaternion.identity, profile.effectDuration, profile.effectFadeOutDuration);
    }

    // 폭발 범위 안의 생존 대상에게 약한 Poison 상태를 중복 없이 적용한다
    private static void ApplyWeakPoison(PoisonStatusPayload sourcePayload, PoisonDeathBurstProfileSO profile, Vector3 position, IDamageable excludedTarget)
    {
        if (!HasWeakPoison(sourcePayload))
        {
            return;
        }

        PoisonStatusPayload weakPoisonPayload = CreateWeakPoisonPayload(sourcePayload);
        int hitCount = Physics.OverlapSphereNonAlloc(
            position,
            sourcePayload.deathBurstRadius,
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

    // 원본 Poison payload에 계산된 성장값을 기준으로 약한 범위 중독 payload를 생성한다
    private static PoisonStatusPayload CreateWeakPoisonPayload(PoisonStatusPayload sourcePayload)
    {
        PoisonStatusPayload weakPoisonPayload = new PoisonStatusPayload
        {
            hasPoisonStatus = sourcePayload.deathBurstMaxHpDamageRatioPerTick > 0.0f &&
                              sourcePayload.deathBurstTickInterval > 0.0f &&
                              sourcePayload.deathBurstDuration > 0.0f &&
                              sourcePayload.deathBurstMaxStackCount > 0,
            maxHpDamageRatioPerTick = Mathf.Clamp01(sourcePayload.deathBurstMaxHpDamageRatioPerTick),
            tickInterval = Mathf.Max(0.01f, sourcePayload.deathBurstTickInterval),
            duration = Mathf.Max(0.0f, sourcePayload.deathBurstDuration),
            maxStackCount = Mathf.Max(1, sourcePayload.deathBurstMaxStackCount),
            stackRefreshMode = sourcePayload.deathBurstStackRefreshMode,
            bossDamageMultiplier = Mathf.Max(0.0f, sourcePayload.deathBurstBossDamageMultiplier),
            deathBurstProfile = sourcePayload.deathBurstAllowChain ? sourcePayload.deathBurstProfile : null,
            deathBurstRadius = sourcePayload.deathBurstRadius,
            deathBurstMaxHpDamageRatioPerTick = sourcePayload.deathBurstMaxHpDamageRatioPerTick,
            deathBurstTickInterval = sourcePayload.deathBurstTickInterval,
            deathBurstDuration = sourcePayload.deathBurstDuration,
            deathBurstMaxStackCount = sourcePayload.deathBurstMaxStackCount,
            deathBurstStackRefreshMode = sourcePayload.deathBurstStackRefreshMode,
            deathBurstBossDamageMultiplier = sourcePayload.deathBurstBossDamageMultiplier,
            deathBurstAllowChain = sourcePayload.deathBurstAllowChain,
            damageSource = sourcePayload.damageSource
        };

        return weakPoisonPayload;
    }

    // 이펙트 또는 약한 범위 중독 중 하나라도 실행 가능한지 확인한다
    private static bool HasDeathBurst(PoisonStatusPayload sourcePayload, PoisonDeathBurstProfileSO profile)
    {
        return profile.burstEffectPrefab != null || HasWeakPoison(sourcePayload);
    }

    // 계산된 payload 기준으로 약한 범위 중독이 실행 가능한지 확인한다
    private static bool HasWeakPoison(PoisonStatusPayload sourcePayload)
    {
        return sourcePayload.deathBurstRadius > 0.0f &&
               sourcePayload.deathBurstMaxHpDamageRatioPerTick > 0.0f &&
               sourcePayload.deathBurstTickInterval > 0.0f &&
               sourcePayload.deathBurstDuration > 0.0f &&
               sourcePayload.deathBurstMaxStackCount > 0;
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

