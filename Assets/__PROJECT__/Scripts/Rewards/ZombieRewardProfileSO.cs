using UnityEngine;

/// <summary>
/// 좀비 처치 시 지급할 기본 보상 목록을 정의하는 ScriptableObject.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Rewards/Zombie Reward Profile")]
public class ZombieRewardProfileSO : ScriptableObject
{
    [SerializeField] private RewardEntry[] rewards;
    [SerializeField] private ZombieRewardModifier[] modifiers;

    public RewardEntry[] Rewards
    {
        get
        {
            return rewards;
        }
    }

    public ZombieRewardModifier[] Modifiers
    {
        get
        {
            return modifiers;
        }
    }

    // 인스펙터 입력값을 유효한 보상 범위로 보정한다
    private void OnValidate()
    {
        ValidateRewards();
        ValidateModifiers();
    }

    // 기본 보상 엔트리 입력값을 유효한 범위로 보정한다
    private void ValidateRewards()
    {
        if (rewards == null)
        {
            return;
        }

        for (int i = 0; i < rewards.Length; i++)
        {
            RewardEntry reward = rewards[i];
            if (reward == null)
            {
                continue;
            }

            reward.amount = Mathf.Max(0, reward.amount);
            reward.dropChance = Mathf.Clamp01(reward.dropChance);
        }
    }

    // 조건부 보정 입력값을 유효한 범위로 보정한다
    private void ValidateModifiers()
    {
        if (modifiers == null)
        {
            return;
        }

        for (int i = 0; i < modifiers.Length; i++)
        {
            ZombieRewardModifier modifier = modifiers[i];
            if (modifier == null)
            {
                continue;
            }

            modifier.Validate();
        }
    }
}
