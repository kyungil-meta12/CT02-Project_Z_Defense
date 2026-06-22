using ProjectZDefense.StatusEffects;
using UnityEngine;

/// <summary>
/// Frost 상태의 누적 슬로우, 유지시간, 빙결 타이머, 빙결 폭발 이펙트를 대상 단위로 관리한다.
/// </summary>
public sealed class FrostStatusRuntime : MonoBehaviour
{
    private IDamageable damageable;
    private IFrostStatusRuntimeOwner owner;
    private StatusEffectVisualController statusEffectVisualController;
    private float frostSlowRatio;
    private float frostExposureTimer;
    private float frostHoldTimer;
    private float frostFreezeTimer;
    private float frostFreezeCooldownTimer;
    private bool frostStatusDirty;
    private bool frostStatusActive;
    private bool canTriggerFreeze;
    private FrostStatusPayload activeFreezePayload;
    private GameObject activeFrostFreezeEffect;

    public bool IsActive => frostStatusActive;
    public bool IsFrozen => canTriggerFreeze && frostFreezeTimer > 0.0f;
    public bool IsFreezeCooldownActive => canTriggerFreeze && frostFreezeCooldownTimer > 0.0f;
    public bool IsFreezeRetargetSuppressed => IsFrozen || IsFreezeCooldownActive;
    public bool IsIgnitionReactionEligible => frostStatusActive || frostStatusDirty || frostHoldTimer > 0.0f || frostFreezeTimer > 0.0f;

    // Frost 런타임이 참조할 대상, 속도 반영자, 비주얼 정책을 초기화한다
    public void Initialize(IDamageable damageable_, IFrostStatusRuntimeOwner owner_, StatusEffectVisualController statusEffectVisualController_, bool canTriggerFreeze_)
    {
        damageable = damageable_;
        owner = owner_;
        statusEffectVisualController = statusEffectVisualController_;
        canTriggerFreeze = canTriggerFreeze_;

        if (statusEffectVisualController == null)
        {
            statusEffectVisualController = GetComponentInChildren<StatusEffectVisualController>(true);
        }
    }

    // Frost 빔으로 전달된 누적 슬로우와 선택적 빙결 데이터를 갱신한다
    public void ApplyFrostStatus(FrostStatusPayload payload)
    {
        if (damageable == null || !damageable.IsAlive)
        {
            return;
        }

        float safeMaxSlowRatio = Mathf.Clamp01(payload.maxSlowRatio);
        float safeBuildUpDuration = Mathf.Max(0.0f, payload.slowBuildUpDuration);
        float safeTickInterval = Mathf.Max(0.0f, payload.tickInterval);

        if (safeMaxSlowRatio > 0.0f)
        {
            frostExposureTimer += safeTickInterval > 0.0f ? safeTickInterval : Time.deltaTime;
            float buildUpRatio = safeBuildUpDuration > 0.0f ? Mathf.Clamp01(frostExposureTimer / safeBuildUpDuration) : 1.0f;
            frostSlowRatio = Mathf.Max(frostSlowRatio, safeMaxSlowRatio * buildUpRatio);
            frostHoldTimer = Mathf.Max(frostHoldTimer, payload.slowHoldDuration);
            frostStatusDirty = true;
        }

        if (canTriggerFreeze && payload.canTriggerFreeze && frostFreezeCooldownTimer <= 0.0f && frostSlowRatio >= payload.freezeTriggerRatio)
        {
            TriggerFrostFreeze(payload);
        }
    }

    // Frost 상태 타이머를 감소시키고 현재 속도 배율을 대상에 반영한다
    public void Tick(float deltaTime)
    {
        if (frostFreezeCooldownTimer > 0.0f)
        {
            frostFreezeCooldownTimer = Mathf.Max(0.0f, frostFreezeCooldownTimer - deltaTime);
        }

        if (!frostStatusActive && !frostStatusDirty)
        {
            return;
        }

        if (frostHoldTimer > 0.0f)
        {
            frostHoldTimer = Mathf.Max(0.0f, frostHoldTimer - deltaTime);
        }

        if (frostFreezeTimer > 0.0f)
        {
            frostFreezeTimer = Mathf.Max(0.0f, frostFreezeTimer - deltaTime);
        }

        if (frostHoldTimer <= 0.0f && frostFreezeTimer <= 0.0f)
        {
            frostSlowRatio = 0.0f;
            frostExposureTimer = 0.0f;
        }

        ApplyFrostSpeedModifier();
    }

    // 현재 Frost 타이머 상태를 기준으로 속도 배율과 비주얼을 즉시 다시 반영한다
    public void RefreshSpeedModifier()
    {
        ApplyFrostSpeedModifier();
    }

    // 풀 재사용이나 사망 시 Frost 상태를 초기화하고 원래 속도를 복구한다
    public void ResetStatus()
    {
        CancelActiveFrostFreezeEffect();
        frostSlowRatio = 0.0f;
        frostExposureTimer = 0.0f;
        frostHoldTimer = 0.0f;
        frostFreezeTimer = 0.0f;
        frostFreezeCooldownTimer = 0.0f;
        frostStatusDirty = false;
        frostStatusActive = false;
        activeFreezePayload = default;
        ApplySpeedMultiplier(1.0f);
        SetFrostVisualActive(false);
    }

    // Frost 누적치가 빙결 조건에 도달했을 때 이펙트와 폭발 데미지를 실행한다
    private void TriggerFrostFreeze(FrostStatusPayload payload)
    {
        frostFreezeCooldownTimer = Mathf.Max(0.0f, payload.freezeCooldownPerTarget);
        frostFreezeTimer = Mathf.Max(frostFreezeTimer, payload.freezeDuration);
        activeFreezePayload = payload;
        frostStatusDirty = true;

        CancelActiveFrostFreezeEffect();
        Vector3 effectPosition = TurretAimPointUtility.GetAimPosition(gameObject);
        activeFrostFreezeEffect = FrostStatusEffectUtility.TriggerFreezeExplosion(payload, effectPosition, damageable);
    }

    // 빙결 상태에서 사망한 대상의 전용 사망 이펙트를 재생한다
    public void TriggerFreezeDeathEffectIfNeeded()
    {
        if (!IsFrozen)
        {
            return;
        }

        Vector3 effectPosition = TurretAimPointUtility.GetAimPosition(gameObject);
        FrostStatusEffectUtility.TriggerFreezeDeathEffect(activeFreezePayload, effectPosition);
    }

    // 현재 Frost 상태에 맞춰 대상 속도 배율과 비주얼을 반영한다
    private void ApplyFrostSpeedModifier()
    {
        float speedMultiplier = 1.0f;
        if (frostFreezeTimer > 0.0f)
        {
            speedMultiplier = 0.0f;
        }
        else if (frostHoldTimer > 0.0f)
        {
            speedMultiplier = Mathf.Clamp01(1.0f - frostSlowRatio);
        }

        ApplySpeedMultiplier(speedMultiplier);
        frostStatusActive = speedMultiplier < 1.0f;
        SetFrostVisualActive(frostStatusActive);
        frostStatusDirty = false;
    }

    // 대상별 이동/공격 속도 반영 구현으로 속도 배율을 전달한다
    private void ApplySpeedMultiplier(float speedMultiplier)
    {
        if (owner == null)
        {
            return;
        }

        owner.ApplyFrostSpeedMultiplier(speedMultiplier);
    }

    // 현재 대상에게 묶인 빙결 이펙트와 예약 폭발 데미지를 취소한다
    private void CancelActiveFrostFreezeEffect()
    {
        if (activeFrostFreezeEffect == null)
        {
            return;
        }

        FrostStatusEffectUtility.CancelFreezeExplosionEffect(activeFrostFreezeEffect, damageable);
        activeFrostFreezeEffect = null;
    }

    // 프로스트 상태 활성 여부에 맞춰 비주얼 컨트롤러를 갱신한다
    private void SetFrostVisualActive(bool isActive)
    {
        if (statusEffectVisualController == null)
        {
            return;
        }

        statusEffectVisualController.SetFrostSlowActive(isActive);
    }
}

/// <summary>
/// Frost 런타임이 계산한 속도 배율을 대상별 이동/공격 시스템에 반영하는 계약이다.
/// </summary>
public interface IFrostStatusRuntimeOwner
{
    // Frost 상태가 계산한 속도 배율을 실제 이동/공격 속도에 반영한다
    void ApplyFrostSpeedMultiplier(float speedMultiplier);
}
