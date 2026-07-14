using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ItemSellBuyCost
{
    [Header("아이템 등급")] public ItemGrade Grade;
    [Header("아이템 판매 가격")] public int CostToSell;
    [Header("아이템 구매 가격")] public int CostToBuy;
}

/// <summary>
/// 아이템 등급 별로 가격을 저장하는 ScriptableObject
/// 아이템 등급으로 모든 가격을 통일하도록 한다.
/// </summary>
[CreateAssetMenu(fileName = "ItemSellBuyCost", menuName = "Scriptable Objects/ItemSellBuyCost")]
public class ItemSellBuyCostSo : ScriptableObject
{
    [Header("등급 별 아이템 판매/구매 가격 목록")] public List<ItemSellBuyCost> CostWithGrade;
}
