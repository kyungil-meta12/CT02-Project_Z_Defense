using System;
using UnityEngine;

/// <summary>
/// 좀비 처치 보상에 적용할 조건부 수량/확률 보정 규칙.
/// </summary>
[Serializable]
public class ZombieRewardModifier
{
    [SerializeField] private bool enabled = true;
    [SerializeField] private ZombieRewardTargetFilter targetFilter = ZombieRewardTargetFilter.AllCurrencies;
    [SerializeField] private RewardCurrencyType targetCurrency;
    [SerializeField] private ZombieRewardTypeFilter zombieTypeFilter = ZombieRewardTypeFilter.Any;
    [SerializeField, Min(1)] private int minWave = 1;
    [SerializeField, Min(0)] private int maxWave;
    [SerializeField] private int defenseLineIndex = -1;
    [SerializeField] private ZombieRewardSituation requiredSituations;
    [SerializeField, Min(0.0f)] private float amountMultiplier = 1.0f;
    [SerializeField] private int flatAmountBonus;
    [SerializeField, Min(0.0f)] private float dropChanceMultiplier = 1.0f;
    [SerializeField, Range(-1.0f, 1.0f)] private float additionalDropChance;

    public float AmountMultiplier
    {
        get
        {
            return amountMultiplier;
        }
    }

    public int FlatAmountBonus
    {
        get
        {
            return flatAmountBonus;
        }
    }

    public float DropChanceMultiplier
    {
        get
        {
            return dropChanceMultiplier;
        }
    }

    public float AdditionalDropChance
    {
        get
        {
            return additionalDropChance;
        }
    }

    // 현재 보상 엔트리와 런타임 컨텍스트에 이 보정 규칙이 적용되는지 확인한다
    public bool IsMatch(RewardEntry reward, ZombieRewardContext context)
    {
        if (!enabled || reward == null)
        {
            return false;
        }

        if (!IsCurrencyMatch(reward.currencyType) ||
            !IsZombieTypeMatch(context) ||
            !IsWaveMatch(context.wave) ||
            !IsDefenseLineMatch(context.defenseLineIndex) ||
            !IsSituationMatch(context.situationFlags))
        {
            return false;
        }

        return true;
    }

    // 인스펙터 입력값을 유효한 보정 범위로 보정한다
    public void Validate()
    {
        minWave = Mathf.Max(1, minWave);
        maxWave = Mathf.Max(0, maxWave);
        amountMultiplier = Mathf.Max(0.0f, amountMultiplier);
        dropChanceMultiplier = Mathf.Max(0.0f, dropChanceMultiplier);
        additionalDropChance = Mathf.Clamp(additionalDropChance, -1.0f, 1.0f);
    }

    // 보정 대상 재화 타입이 현재 보상 재화와 일치하는지 확인한다
    private bool IsCurrencyMatch(RewardCurrencyType currencyType)
    {
        return targetFilter == ZombieRewardTargetFilter.AllCurrencies || targetCurrency == currencyType;
    }

    // 보정 대상 좀비 타입이 현재 컨텍스트와 일치하는지 확인한다
    private bool IsZombieTypeMatch(ZombieRewardContext context)
    {
        switch (zombieTypeFilter)
        {
            case ZombieRewardTypeFilter.NormalOnly:
                return !context.isBoss;
            case ZombieRewardTypeFilter.BossOnly:
                return context.isBoss;
            default:
                return true;
        }
    }

    // 현재 웨이브가 보정 적용 범위에 포함되는지 확인한다
    private bool IsWaveMatch(int wave)
    {
        int safeWave = Mathf.Max(1, wave);
        if (safeWave < minWave)
        {
            return false;
        }

        return maxWave <= 0 || safeWave <= maxWave;
    }

    // 방어선 조건이 현재 컨텍스트와 일치하는지 확인한다
    private bool IsDefenseLineMatch(int contextDefenseLineIndex)
    {
        return defenseLineIndex < 0 || defenseLineIndex == contextDefenseLineIndex;
    }

    // 필요한 상황 플래그가 현재 컨텍스트에 모두 포함되어 있는지 확인한다
    private bool IsSituationMatch(ZombieRewardSituation contextSituations)
    {
        return requiredSituations == ZombieRewardSituation.None ||
               (contextSituations & requiredSituations) == requiredSituations;
    }
}

/// <summary>
/// 보상 보정 규칙이 적용될 재화 범위.
/// </summary>
public enum ZombieRewardTargetFilter
{
    AllCurrencies,
    SpecificCurrency
}

/// <summary>
/// 보상 보정 규칙이 적용될 좀비 타입 범위.
/// </summary>
public enum ZombieRewardTypeFilter
{
    Any,
    NormalOnly,
    BossOnly
}
