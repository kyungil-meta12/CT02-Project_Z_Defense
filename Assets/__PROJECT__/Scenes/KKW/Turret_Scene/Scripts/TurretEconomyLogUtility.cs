using System.Text;
using UnityEngine;

// 터렛 설치, 업그레이드, 진화의 재화 소모 결과 로그를 공통 형식으로 출력한다
internal static class TurretEconomyLogUtility
{
    // 터렛 경제 액션의 성공 또는 실패 결과를 현재 보유 재화와 함께 출력한다
    public static void LogResult(string actionName, string targetName, ResourceCost[] costs, bool success, Object context, string reason = null)
    {
        StringBuilder builder = new StringBuilder(192);
        builder.Append("[터렛 재화] ");
        builder.Append(actionName);
        builder.Append(success ? " 성공" : " 실패");

        if (!string.IsNullOrWhiteSpace(targetName))
        {
            builder.Append(" - 대상: ");
            builder.Append(targetName);
        }

        builder.Append(" / 필요: ");
        builder.Append(FormatCosts(costs));
        builder.Append(" / 보유: ");
        builder.Append(FormatCurrentWallet());

        if (!string.IsNullOrWhiteSpace(reason))
        {
            builder.Append(" / 사유: ");
            builder.Append(reason);
        }

        if (success)
        {
            Debug.Log(builder.ToString(), context);
            return;
        }

        Debug.LogWarning(builder.ToString(), context);
    }

    // ResourceCost 배열을 로그용 문자열로 변환한다
    private static string FormatCosts(ResourceCost[] costs)
    {
        if (!HasPayableCosts(costs))
        {
            return "없음";
        }

        StringBuilder builder = new StringBuilder(64);
        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(GetCurrencyLabel(cost.currencyType));
            builder.Append(' ');
            builder.Append(cost.amount);
        }

        return builder.ToString();
    }

    // 현재 ItemManager 보유 재화를 로그용 문자열로 변환한다
    private static string FormatCurrentWallet()
    {
        if (ItemManager.Inst == null)
        {
            return "ItemManager 없음";
        }

        StringBuilder builder = new StringBuilder(96);
        builder.Append("Coin ");
        builder.Append(ItemManager.Inst.CoinCountString);
        builder.Append(", Fire ");
        builder.Append(ItemManager.Inst.FirePartCountString);
        builder.Append(", Special ");
        builder.Append(ItemManager.Inst.SpecialPartCountString);
        return builder.ToString();
    }

    // 실제 소모할 비용 항목이 하나 이상 있는지 확인한다
    private static bool HasPayableCosts(ResourceCost[] costs)
    {
        if (costs == null)
        {
            return false;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost != null && cost.amount > 0)
            {
                return true;
            }
        }

        return false;
    }

    // 재화 타입을 로그용 짧은 라벨로 변환한다
    private static string GetCurrencyLabel(RewardCurrencyType currencyType)
    {
        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                return "Coin";
            case RewardCurrencyType.FirePart:
                return "Fire";
            case RewardCurrencyType.SpecialPart:
                return "Special";
            default:
                return currencyType.ToString();
        }
    }
}
