using UnityEngine;

/// <summary>
/// 터렛 UI에서 재화 이름과 아이콘을 인벤토리 메타데이터 기준으로 표시하도록 돕는다.
/// </summary>
public static class TurretCurrencyDisplayUtility
{
    // 재화 타입을 터렛 UI에 표시할 이름으로 변환한다
    public static string GetDisplayName(RewardCurrencyType currencyType)
    {
        if (InventorySystem.Inst != null)
        {
            ItemMetaDataSo metadata = InventorySystem.Inst.GetMetaData(currencyType);
            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Name))
            {
                return metadata.Name;
            }

            string itemName = InventorySystem.Inst.GetName(currencyType);
            if (!string.IsNullOrWhiteSpace(itemName))
            {
                return itemName;
            }
        }

        return GetFallbackName(currencyType);
    }

    // 재화 타입에 연결된 인벤토리 메타데이터 아이콘을 반환한다
    public static Sprite GetIcon(RewardCurrencyType currencyType)
    {
        if (InventorySystem.Inst == null)
        {
            return null;
        }

        ItemMetaDataSo metadata = InventorySystem.Inst.GetMetaData(currencyType);
        return metadata == null ? null : metadata.ItemImage;
    }

    // 메타데이터가 없을 때 사용할 최소 fallback 이름을 반환한다
    private static string GetFallbackName(RewardCurrencyType currencyType)
    {
        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                return "Coin";
            default:
                return currencyType.ToString();
        }
    }
}
