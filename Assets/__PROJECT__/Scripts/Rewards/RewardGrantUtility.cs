using UnityEngine;

/// <summary>
/// 보상 프로필과 런타임 컨텍스트를 실제 재화 지급으로 변환한다.
/// </summary>
public static class RewardGrantUtility
{
    // 좀비 처치 보상 프로필을 실제 재화 지급으로 변환한다
    public static void GrantZombieReward(ZombieRewardProfileSO rewardProfile, ZombieRewardContext context, Object logContext)
    {
        if (ItemManager.Inst == null)
        {
            Debug.LogWarning("[RewardGrantUtility] ItemManager가 없어 좀비 처치 보상을 지급할 수 없습니다.", logContext);
            return;
        }

        if (rewardProfile == null)
        {
            Debug.LogWarning("[RewardGrantUtility] 보상 프로필이 없어 좀비 처치 보상을 지급하지 않습니다.", logContext);
            return;
        }

        RewardEntry[] rewards = rewardProfile.Rewards;
        if (rewards == null)
        {
            return;
        }

        ZombieRewardModifier[] modifiers = rewardProfile.Modifiers;
        for (int i = 0; i < rewards.Length; i++)
        {
            GrantRewardEntry(rewards[i], context, modifiers);
        }
    }

    // 단일 보상 엔트리의 확률과 배율을 계산해 지급한다
    private static void GrantRewardEntry(RewardEntry reward, ZombieRewardContext context, ZombieRewardModifier[] modifiers)
    {
        if (reward == null || reward.amount <= 0)
        {
            return;
        }

        float dropChance = CalculateDropChance(reward, context, modifiers);
        if (dropChance <= 0.0f || Random.value > dropChance)
        {
            return;
        }

        int finalAmount = CalculateFinalAmount(reward, context, modifiers);
        if (finalAmount <= 0)
        {
            return;
        }

        ItemManager.Inst.AddReward(reward.currencyType, finalAmount, true);
    }

    // 보상 엔트리와 조건부 보정 목록으로 최종 지급 수량을 계산한다
    private static int CalculateFinalAmount(RewardEntry reward, ZombieRewardContext context, ZombieRewardModifier[] modifiers)
    {
        float amount = reward.amount;
        float multiplier = Mathf.Max(0.0f, context.rewardMultiplier);

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
                multiplier *= modifier.AmountMultiplier;
            }
        }

        return Mathf.FloorToInt(Mathf.Max(0.0f, amount) * multiplier);
    }

    // 보상 엔트리와 조건부 보정 목록으로 최종 드랍 확률을 계산한다
    private static float CalculateDropChance(RewardEntry reward, ZombieRewardContext context, ZombieRewardModifier[] modifiers)
    {
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

                dropChance *= modifier.DropChanceMultiplier;
                dropChance += modifier.AdditionalDropChance;
            }
        }

        return Mathf.Clamp01(dropChance);
    }
}
