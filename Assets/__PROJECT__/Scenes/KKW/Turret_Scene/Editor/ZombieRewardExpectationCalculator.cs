using System.Collections.Generic;
using UnityEngine;

// 좀비 처치 보상의 런타임 지급 규칙을 재화별 기대 보상 계산으로 재현한다.
internal sealed class ZombieRewardExpectationCalculator
{
    // 좀비 컴포넌트와 스펙 기준으로 재화별 기대 보상을 계산한다
    public Dictionary<RewardCurrencyType, float> CalculateExpectedRewards(ZombieRewardProfileSO rewardProfileOverride, ScriptableObject sourceSpec, int wave, bool isBoss, float rewardMultiplier)
    {
        Dictionary<RewardCurrencyType, float> result = new Dictionary<RewardCurrencyType, float>();
        ZombieRewardProfileSO rewardProfile = ResolveRewardProfile(rewardProfileOverride, sourceSpec, isBoss);
        if (rewardProfile == null)
        {
            return result;
        }

        ZombieRewardContext context = CreateContext(sourceSpec, wave, isBoss, rewardMultiplier);
        AccumulateExpectedRewards(result, rewardProfile, context);
        return result;
    }

    // 런타임과 같은 우선순위로 보상 프로필을 선택한다
    private static ZombieRewardProfileSO ResolveRewardProfile(ZombieRewardProfileSO rewardProfileOverride, ScriptableObject sourceSpec, bool isBoss)
    {
        if (rewardProfileOverride != null)
        {
            return rewardProfileOverride;
        }

        BossZombieSpec bossSpec = sourceSpec as BossZombieSpec;
        if (isBoss && bossSpec != null)
        {
            return bossSpec.RewardProfile;
        }

        return null;
    }

    // 보상 계산에 사용할 런타임 컨텍스트를 만든다
    private static ZombieRewardContext CreateContext(ScriptableObject sourceSpec, int wave, bool isBoss, float rewardMultiplier)
    {
        ZombieRewardContext context = isBoss
            ? ZombieRewardContext.CreateBossZombie(wave, sourceSpec, Vector3.zero)
            : ZombieRewardContext.CreateNormalZombie(wave, sourceSpec, Vector3.zero);
        return context.WithRewardMultiplier(rewardMultiplier);
    }

    // 보상 프로필의 모든 재화 엔트리 기대값을 재화별로 누적한다
    private static void AccumulateExpectedRewards(Dictionary<RewardCurrencyType, float> target, ZombieRewardProfileSO rewardProfile, ZombieRewardContext context)
    {
        RewardEntry[] rewards = rewardProfile.Rewards;
        if (rewards == null)
        {
            return;
        }

        ZombieRewardModifier[] modifiers = rewardProfile.Modifiers;
        for (int i = 0; i < rewards.Length; i++)
        {
            RewardEntry reward = rewards[i];
            if (reward == null || reward.amount <= 0)
            {
                continue;
            }

            float expectation = CalculateRewardEntryExpectation(reward, context, modifiers);
            if (expectation <= 0.0f)
            {
                continue;
            }

            target.TryGetValue(reward.currencyType, out float existing);
            target[reward.currencyType] = existing + expectation;
        }
    }

    // 단일 보상 엔트리의 기대 지급량을 계산한다
    private static float CalculateRewardEntryExpectation(RewardEntry reward, ZombieRewardContext context, ZombieRewardModifier[] modifiers)
    {
        float amount = reward.amount;
        float amountMultiplier = reward.currencyType == RewardCurrencyType.Coin
            ? Mathf.Max(0.0f, context.rewardMultiplier)
            : 1.0f;
        float dropChance = reward.dropChance;

        if (modifiers != null)
        {
            for (int i = 0; i < modifiers.Length; i++)
            {
                ZombieRewardModifier modifier = modifiers[i];
                if (modifier == null || !modifier.IsMatch(reward, context))
                {
                    continue;
                }

                amount += modifier.FlatAmountBonus;
                amountMultiplier *= modifier.AmountMultiplier;
                dropChance *= modifier.DropChanceMultiplier;
                dropChance += modifier.AdditionalDropChance;
            }
        }

        float minRandomMultiplier = Mathf.Max(0.0f, reward.minAmountMultiplier);
        float maxRandomMultiplier = Mathf.Max(minRandomMultiplier, reward.maxAmountMultiplier);
        float averageRandomMultiplier = (minRandomMultiplier + maxRandomMultiplier) * 0.5f;
        return Mathf.Max(0.0f, amount) * amountMultiplier * averageRandomMultiplier * Mathf.Clamp01(dropChance);
    }
}
