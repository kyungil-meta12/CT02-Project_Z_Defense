using ProjectZDefense.StatusEffects;
using UnityEngine;

/// <summary>
/// 대상 하나에 적용된 Ignition 연소 상태의 지속시간, 중첩, 틱데미지, 사망 연출을 관리한다.
/// </summary>
public sealed class IgnitionStatusRuntime : MonoBehaviour
{
    private IDamageable damageable;
    private StatusEffectVisualController statusEffectVisualController;
    private IgnitionStatusPayload ignitionStatusPayload;
    private float ignitionRemainingDuration;
    private float ignitionTickTimer;
    private int ignitionStackCount;
    private bool ignitionStatusActive;
    private bool useBossDamageMultiplier;

    public bool IsActive => ignitionStatusActive;

    // Ignition 런타임이 참조할 대상과 보스 보정 정책을 초기화한다
    public void Initialize(IDamageable damageable_, StatusEffectVisualController statusEffectVisualController_, bool useBossDamageMultiplier_)
    {
        damageable = damageable_;
        statusEffectVisualController = statusEffectVisualController_;
        useBossDamageMultiplier = useBossDamageMultiplier_;
    }

    // 화염 공격으로 전달된 연소 틱데미지 데이터를 갱신한다
    public void ApplyIgnitionStatus(IgnitionStatusPayload payload)
    {
        if (damageable == null || !damageable.IsAlive || !payload.hasIgnitionStatus)
        {
            return;
        }

        ignitionStatusPayload = payload;
        int safeMaxStackCount = Mathf.Max(1, payload.maxStackCount);

        if (ignitionStackCount <= 0)
        {
            ignitionStackCount = 1;
        }
        else if (payload.stackRefreshMode == IgnitionStackRefreshMode.AddStackAndRefreshDuration)
        {
            ignitionStackCount = Mathf.Min(safeMaxStackCount, ignitionStackCount + 1);
        }

        ignitionRemainingDuration = Mathf.Max(ignitionRemainingDuration, payload.duration);
        if (ignitionTickTimer <= 0.0f)
        {
            ignitionTickTimer = Mathf.Max(0.01f, payload.tickInterval);
        }

        ignitionStatusActive = true;
        SetIgnitionBurnVisualActive(true);
    }

    // Ignition 상태 타이머를 감소시키고 틱마다 연소 데미지를 적용한다
    public void Tick(float deltaTime)
    {
        if (!ignitionStatusActive)
        {
            return;
        }

        if (damageable == null || !damageable.IsAlive)
        {
            ResetStatus();
            return;
        }

        float previousRemainingDuration = ignitionRemainingDuration;
        float previousTickTimer = ignitionTickTimer;
        ignitionRemainingDuration = Mathf.Max(0.0f, ignitionRemainingDuration - deltaTime);
        ignitionTickTimer -= deltaTime;

        if (ignitionTickTimer <= 0.0f && CanApplyTick(previousRemainingDuration, previousTickTimer))
        {
            ApplyIgnitionTickDamage();
            ignitionTickTimer = Mathf.Max(0.01f, ignitionStatusPayload.tickInterval);
        }

        if (ignitionRemainingDuration <= 0.0f)
        {
            ResetStatus();
        }
    }

    // 연소 상태로 사망한 경우 사망 이펙트를 실행한다
    public void TriggerBurnDeathEffectIfNeeded(Vector3 effectPosition)
    {
        if (!ignitionStatusActive || ignitionStatusPayload.burnDeathEffectPrefab == null)
        {
            return;
        }

        PooledObjectUtility.SpawnEffect(
            ignitionStatusPayload.burnDeathEffectPrefab,
            effectPosition,
            Quaternion.identity,
            ignitionStatusPayload.burnDeathEffectDuration);
    }

    // 풀 재사용이나 사망 시 Ignition 상태를 초기화한다
    public void ResetStatus()
    {
        ignitionStatusPayload = default;
        ignitionRemainingDuration = 0.0f;
        ignitionTickTimer = 0.0f;
        ignitionStackCount = 0;
        ignitionStatusActive = false;
        SetIgnitionBurnVisualActive(false);
    }

    // 현재 프레임의 연소 틱이 지속시간 안에서 발생 가능한지 확인한다
    private static bool CanApplyTick(float previousRemainingDuration, float previousTickTimer)
    {
        return previousRemainingDuration > 0.0f && previousTickTimer > 0.0f;
    }

    // 현재 연소 중첩 수에 맞는 틱데미지를 적용한다
    private void ApplyIgnitionTickDamage()
    {
        if (damageable == null || !damageable.IsAlive || ignitionStackCount <= 0)
        {
            return;
        }

        float damage = CalculateIgnitionTickDamage();
        if (damage <= 0.0f)
        {
            return;
        }

        damageable.TakeDamage(damage);
    }

    // 현재 설정에 맞는 Ignition 1틱 데미지를 계산한다
    private float CalculateIgnitionTickDamage()
    {
        if (ignitionStatusPayload.maxHpDamageRatioPerTick > 0.0f)
        {
            return Mathf.Max(0.0f, damageable.TotalHp) * Mathf.Clamp01(ignitionStatusPayload.maxHpDamageRatioPerTick) * ignitionStackCount * GetDamageMultiplier();
        }

        return ignitionStatusPayload.damagePerSecond * Mathf.Max(0.01f, ignitionStatusPayload.tickInterval) * ignitionStackCount * GetDamageMultiplier();
    }

    // 대상 타입에 맞는 Ignition 데미지 배율을 반환한다
    private float GetDamageMultiplier()
    {
        return useBossDamageMultiplier ? Mathf.Max(0.0f, ignitionStatusPayload.bossDamageMultiplier) : 1.0f;
    }

    // Ignition 화상 상태 활성 여부에 맞춰 비주얼 컨트롤러를 갱신한다
    private void SetIgnitionBurnVisualActive(bool isActive)
    {
        if (statusEffectVisualController == null)
        {
            return;
        }

        statusEffectVisualController.SetIgnitionBurnActive(isActive);
    }
}
