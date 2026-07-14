using UnityEngine;
using ProjectZDefense.StatusEffects;
using ProjectZDefense.Audio;

/// <summary>
/// 대상 하나에 적용된 Poison 상태의 지속시간, 스택, 틱데미지, 처형 예고, 사망 폭발을 관리한다.
/// </summary>
public sealed class PoisonStatusRuntime : MonoBehaviour
{
    private const float POISON_AUDIO_FADE_DURATION = 0.5f;

    private IDamageable damageable;
    private StatusEffectVisualController statusEffectVisualController;
    private PoisonStatusPayload poisonStatusPayload;
    private ProjectAudioHandle poisonLoopAudioHandle;
    private float poisonRemainingDuration;
    private float poisonTickTimer;
    private int poisonStackCount;
    private bool poisonStatusActive;
    private bool useBossDamageMultiplier;
    private bool canTriggerDeathBurst;

    public bool IsLethalPending { get; private set; }
    public bool IsActive => poisonStatusActive;

    // Poison 런타임이 참조할 대상과 비주얼 정책을 초기화한다
    public void Initialize(IDamageable damageable_, StatusEffectVisualController statusEffectVisualController_, bool useBossDamageMultiplier_, bool canTriggerDeathBurst_)
    {
        damageable = damageable_;
        statusEffectVisualController = statusEffectVisualController_;
        useBossDamageMultiplier = useBossDamageMultiplier_;
        canTriggerDeathBurst = canTriggerDeathBurst_;

        if (statusEffectVisualController == null)
        {
            statusEffectVisualController = GetComponentInChildren<StatusEffectVisualController>(true);
        }
    }

    // Poison 투사체로 전달된 중독 틱데미지 데이터를 갱신한다
    public void ApplyPoisonStatus(PoisonStatusPayload payload)
    {
        if (damageable == null || !damageable.IsAlive || !payload.hasPoisonStatus)
        {
            return;
        }

        bool wasPoisonStatusActive = poisonStatusActive;
        poisonStatusPayload = payload;
        int safeMaxStackCount = Mathf.Max(1, payload.maxStackCount);

        if (poisonStackCount <= 0)
        {
            poisonStackCount = 1;
        }
        else if (payload.stackRefreshMode == PoisonStackRefreshMode.AddStackAndRefreshDuration)
        {
            poisonStackCount = Mathf.Min(safeMaxStackCount, poisonStackCount + 1);
        }

        poisonRemainingDuration = Mathf.Max(poisonRemainingDuration, payload.duration);
        if (poisonTickTimer <= 0.0f)
        {
            poisonTickTimer = Mathf.Max(0.01f, payload.tickInterval);
        }

        poisonStatusActive = true;
        PlayPoisonLoopAudioIfNeeded(payload, wasPoisonStatusActive);
        SetPoisonVisualActive(true);
        RefreshLethalPrediction();
    }

    // Poison 상태 타이머를 감소시키고 틱마다 체력비례 데미지를 적용한다
    public void Tick(float deltaTime)
    {
        if (!poisonStatusActive)
        {
            return;
        }

        if (damageable == null || !damageable.IsAlive)
        {
            ResetStatus();
            return;
        }

        float previousRemainingDuration = poisonRemainingDuration;
        float previousTickTimer = poisonTickTimer;
        poisonRemainingDuration = Mathf.Max(0.0f, poisonRemainingDuration - deltaTime);
        poisonTickTimer -= deltaTime;

        if (poisonTickTimer <= 0.0f && PoisonStatusRuntimeUtility.CanApplyTick(previousRemainingDuration, previousTickTimer))
        {
            ApplyPoisonTickDamage();
            poisonTickTimer = Mathf.Max(0.01f, poisonStatusPayload.tickInterval);
        }

        if (poisonRemainingDuration <= 0.0f)
        {
            ResetStatus();
            return;
        }

        RefreshLethalPrediction();
    }

    // 현재 체력 기준으로 Poison 처형 예고 여부를 다시 계산한다
    public void RefreshLethalPrediction()
    {
        bool wasLethalPending = IsLethalPending;
        IsLethalPending = IsPoisonDamageLethal();
        PlayLethalMarkAudioIfNeeded(wasLethalPending);
        SetPoisonLethalVisualActive(IsLethalPending);
    }

    // Poison 처형 확정 상태로 사망한 경우 사망 폭발과 약한 범위 중독을 실행한다
    public void TriggerDeathBurstIfNeeded(Vector3 effectPosition)
    {
        if (!canTriggerDeathBurst || !IsLethalPending || poisonStatusPayload.deathBurstProfile == null)
        {
            return;
        }

        PoisonDeathBurstEffectUtility.TriggerDeathBurst(poisonStatusPayload, effectPosition, damageable);
    }

    // 풀 재사용이나 사망 시 Poison 상태를 초기화하고 비주얼을 끈다
    public void ResetStatus()
    {
        StopPoisonLoopAudio();
        poisonStatusPayload = default;
        poisonRemainingDuration = 0.0f;
        poisonTickTimer = 0.0f;
        poisonStackCount = 0;
        poisonStatusActive = false;
        IsLethalPending = false;
        SetPoisonLethalVisualActive(false);
        SetPoisonVisualActive(false);
    }

    // 중독 상태 최초 진입 시 루프 사운드를 페이드 인으로 재생한다
    private void PlayPoisonLoopAudioIfNeeded(PoisonStatusPayload payload, bool wasPoisonStatusActive)
    {
        if (wasPoisonStatusActive || payload.damageSource == null)
        {
            return;
        }

        TurretAudioController audioController = payload.damageSource.GetComponent<TurretAudioController>();
        if (audioController == null)
        {
            return;
        }

        poisonLoopAudioHandle = audioController.Play(TurretAudioEvent.StatusApply, transform);
        if (!poisonLoopAudioHandle.IsValid)
        {
            return;
        }

        poisonLoopAudioHandle.SetVolumeScale(0f);
        poisonLoopAudioHandle.FadeToVolumeScale(1f, POISON_AUDIO_FADE_DURATION);
    }

    // 중독 상태 종료 시 루프 사운드를 페이드 아웃으로 정지한다
    private void StopPoisonLoopAudio()
    {
        if (!poisonLoopAudioHandle.IsValid)
        {
            poisonLoopAudioHandle = default;
            return;
        }

        poisonLoopAudioHandle.FadeOutAndStop(POISON_AUDIO_FADE_DURATION);
        poisonLoopAudioHandle = default;
    }

    // 처형 예고 표시가 새로 켜질 때 알림 사운드를 재생한다
    private void PlayLethalMarkAudioIfNeeded(bool wasLethalPending)
    {
        if (wasLethalPending || !IsLethalPending || poisonStatusPayload.damageSource == null)
        {
            return;
        }

        TurretAudioController audioController = poisonStatusPayload.damageSource.GetComponent<TurretAudioController>();
        if (audioController == null)
        {
            return;
        }

        audioController.PlayAt(TurretAudioEvent.StatusLethal, transform.position);
    }

    // 현재 중독 중첩 수에 맞는 최대체력 비례 틱데미지를 적용한다
    private void ApplyPoisonTickDamage()
    {
        if (damageable == null || !damageable.IsAlive || poisonStackCount <= 0 || poisonStatusPayload.maxHpDamageRatioPerTick <= 0.0f)
        {
            return;
        }

        float damage = PoisonStatusRuntimeUtility.CalculateTickDamage(
            damageable.TotalHp,
            poisonStatusPayload.maxHpDamageRatioPerTick,
            poisonStackCount,
            GetDamageMultiplier());
        damageable.TakeDamage(new DamageInfo(damage, DamagePopupType.Normal, DamagePopupPolicyResolver.ResolveDamageOverTime(), poisonStatusPayload.damageSource));
    }

    // 남은 Poison 틱데미지 총합이 현재 체력을 넘는지 확인한다
    private bool IsPoisonDamageLethal()
    {
        if (damageable == null || !damageable.IsAlive || !poisonStatusActive || poisonStackCount <= 0)
        {
            return false;
        }

        float tickDamage = GetPoisonTickDamage();
        int remainingTickCount = GetRemainingPoisonTickCount();
        return tickDamage > 0.0f && remainingTickCount > 0 && damageable.CurrHp <= tickDamage * remainingTickCount;
    }

    // 현재 중첩 수와 대상 타입 기준 Poison 1틱 데미지를 반환한다
    private float GetPoisonTickDamage()
    {
        if (damageable == null || poisonStatusPayload.maxHpDamageRatioPerTick <= 0.0f || poisonStackCount <= 0)
        {
            return 0.0f;
        }

        return damageable.TotalHp * Mathf.Clamp01(poisonStatusPayload.maxHpDamageRatioPerTick) * poisonStackCount * GetDamageMultiplier();
    }

    // 대상 타입에 맞는 Poison 데미지 배율을 반환한다
    private float GetDamageMultiplier()
    {
        return useBossDamageMultiplier ? Mathf.Max(0.0f, poisonStatusPayload.bossDamageMultiplier) : 1.0f;
    }

    // 남은 지속시간 안에 발생할 Poison 틱 수를 계산한다
    private int GetRemainingPoisonTickCount()
    {
        return PoisonStatusRuntimeUtility.GetRemainingTickCount(poisonRemainingDuration, poisonTickTimer, poisonStatusPayload.tickInterval);
    }

    // 포이즌 상태 활성 여부에 맞춰 비주얼 컨트롤러를 갱신한다
    private void SetPoisonVisualActive(bool isActive)
    {
        if (statusEffectVisualController == null)
        {
            return;
        }

        statusEffectVisualController.SetPoisonActive(isActive);
    }

    // 포이즌 처치 확정 표시 활성 여부에 맞춰 비주얼 컨트롤러를 갱신한다
    private void SetPoisonLethalVisualActive(bool isActive)
    {
        if (statusEffectVisualController == null)
        {
            return;
        }

        statusEffectVisualController.SetPoisonLethalIndicatorActive(isActive);
    }
}


