using ProjectZDefense.StatusEffects;
using UnityEngine;

/// <summary>
/// 대상 하나에 적용된 Ignition 연소 상태의 지속시간, 중첩, 틱데미지를 관리한다.
/// </summary>
public sealed class IgnitionStatusRuntime : MonoBehaviour
{
    private IDamageable damageable;
    private StatusEffectVisualController statusEffectVisualController;
    private FrostStatusRuntime frostStatusRuntime;
    private PoisonStatusRuntime poisonStatusRuntime;
    private ElectroStatusRuntime electroStatusRuntime;
    private IgnitionStatusPayload ignitionStatusPayload;
    private float ignitionRemainingDuration;
    private float ignitionTickTimer;
    private int ignitionStackCount;
    private IgnitionReactionType activeReactionType;
    private bool ignitionStatusActive;
    private bool useBossDamageMultiplier;

    public bool IsActive => ignitionStatusActive;

    // Ignition 런타임이 참조할 대상과 보스 보정 정책을 초기화한다
    public void Initialize(IDamageable damageable_, StatusEffectVisualController statusEffectVisualController_, bool useBossDamageMultiplier_)
    {
        damageable = damageable_;
        statusEffectVisualController = statusEffectVisualController_;
        useBossDamageMultiplier = useBossDamageMultiplier_;
        frostStatusRuntime = GetComponent<FrostStatusRuntime>();
        poisonStatusRuntime = GetComponent<PoisonStatusRuntime>();
        electroStatusRuntime = GetComponent<ElectroStatusRuntime>();
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
        TryActivateExistingStatusReaction();
        if (activeReactionType != IgnitionReactionType.None)
        {
            ignitionTickTimer = Mathf.Min(ignitionTickTimer, GetCurrentTickInterval());
        }

        RefreshIgnitionVisuals();
    }

    // 연소 중 다른 속성 공격을 받았을 때 최초 반응 타입을 고정한다
    public void NotifyIgnitionReaction(IgnitionReactionType reactionType)
    {
        if (!ignitionStatusActive || activeReactionType != IgnitionReactionType.None || reactionType == IgnitionReactionType.None)
        {
            return;
        }

        activeReactionType = reactionType;
        ignitionTickTimer = Mathf.Min(ignitionTickTimer, GetCurrentTickInterval());
        RefreshIgnitionVisuals();
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
            ignitionTickTimer = GetCurrentTickInterval();
        }

        if (ignitionRemainingDuration <= 0.0f)
        {
            ResetStatus();
        }
    }

    // 풀 재사용이나 사망 시 Ignition 상태를 초기화한다
    public void ResetStatus()
    {
        ignitionStatusPayload = default;
        ignitionRemainingDuration = 0.0f;
        ignitionTickTimer = 0.0f;
        ignitionStackCount = 0;
        activeReactionType = IgnitionReactionType.None;
        ignitionStatusActive = false;
        SetIgnitionBurnVisualActive(false);
        SetIgnitionReactionVisualActive(IgnitionReactionType.None);
    }

    // 이미 적용된 다른 속성 상태가 있으면 최초 반응 타입을 결정한다
    private void TryActivateExistingStatusReaction()
    {
        if (activeReactionType != IgnitionReactionType.None)
        {
            return;
        }

        if (electroStatusRuntime != null && electroStatusRuntime.IsIgnitionReactionEligible)
        {
            activeReactionType = IgnitionReactionType.Electro;
            return;
        }

        if (frostStatusRuntime != null && frostStatusRuntime.IsIgnitionReactionEligible)
        {
            activeReactionType = IgnitionReactionType.Frost;
            return;
        }

        if (poisonStatusRuntime != null && poisonStatusRuntime.IsActive)
        {
            activeReactionType = IgnitionReactionType.Poison;
        }
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

        damageable.TakeDamage(new DamageInfo(damage, DamagePopupType.Normal, DamagePopupPolicyResolver.ResolveDamageOverTime()));
    }

    // 현재 설정에 맞는 Ignition 1틱 데미지를 계산한다
    private float CalculateIgnitionTickDamage()
    {
        float maxHpDamageRatioPerTick = GetCurrentMaxHpDamageRatioPerTick();
        if (maxHpDamageRatioPerTick > 0.0f)
        {
            return Mathf.Max(0.0f, damageable.TotalHp) * Mathf.Clamp01(maxHpDamageRatioPerTick) * ignitionStackCount * GetDamageMultiplier();
        }

        return GetCurrentDamagePerSecond() * GetCurrentTickInterval() * ignitionStackCount * GetDamageMultiplier();
    }

    // 대상 타입에 맞는 Ignition 데미지 배율을 반환한다
    private float GetDamageMultiplier()
    {
        return useBossDamageMultiplier ? Mathf.Max(0.0f, ignitionStatusPayload.bossDamageMultiplier) : 1.0f;
    }

    // 현재 반응 상태에 맞는 틱 간격을 반환한다
    private float GetCurrentTickInterval()
    {
        if (activeReactionType != IgnitionReactionType.None)
        {
            return Mathf.Max(0.01f, ignitionStatusPayload.reactionTickInterval);
        }

        return Mathf.Max(0.01f, ignitionStatusPayload.tickInterval);
    }

    // 현재 반응 상태에 맞는 최대체력 비례 틱데미지 비율을 반환한다
    private float GetCurrentMaxHpDamageRatioPerTick()
    {
        if (activeReactionType != IgnitionReactionType.None)
        {
            return Mathf.Clamp01(ignitionStatusPayload.reactionMaxHpDamageRatioPerTick);
        }

        return Mathf.Clamp01(ignitionStatusPayload.maxHpDamageRatioPerTick);
    }

    // 현재 반응 상태에 맞는 초당 데미지 fallback 값을 반환한다
    private float GetCurrentDamagePerSecond()
    {
        if (activeReactionType != IgnitionReactionType.None)
        {
            return Mathf.Max(0.0f, ignitionStatusPayload.reactionDamagePerSecond);
        }

        return Mathf.Max(0.0f, ignitionStatusPayload.damagePerSecond);
    }

    // 현재 반응 상태에 맞춰 기본 화염은 유지하고 강화 화염 비주얼을 추가로 전환한다
    private void RefreshIgnitionVisuals()
    {
        SetIgnitionBurnVisualActive(true);
        SetIgnitionReactionVisualActive(activeReactionType);
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

    // Ignition 속성 반응 비주얼 타입을 비주얼 컨트롤러에 전달한다
    private void SetIgnitionReactionVisualActive(IgnitionReactionType reactionType)
    {
        if (statusEffectVisualController == null)
        {
            return;
        }

        statusEffectVisualController.SetIgnitionReactionActive(reactionType);
    }
}
