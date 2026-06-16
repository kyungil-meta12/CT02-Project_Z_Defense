using IncrementalLib;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemInfo
{
    public string Name;
    public string CountString;
    public Incremental Count;
}

public class InventorySystem : MonoBehaviour
{
    public static InventorySystem Inst;
    private Dictionary<RewardCurrencyType, ItemInfo> itemDict = new();

    // 아이템 개수 변경 이벤트
    public Action<RewardCurrencyType, Incremental> OnItemCountChange;

    void Awake()
    {
        if (Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
        }
        Inst = this;
    }

    void OnDestroy()
    {
        Inst = null;
    }



    /// <summary>
    /// 아이템 개수를 리턴한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <returns></returns>
    public Incremental GetCount(RewardCurrencyType itemType)
    {
        return itemDict.ContainsKey(itemType) ? itemDict[itemType].Count : 0;
    }



    public string GetString(RewardCurrencyType itemType)
    {
        return itemDict.ContainsKey(itemType) ? "" : itemDict[itemType].Count.ToString();
    }



    /// <summary>
    /// 아이템을 사용할 수 있는지 확인한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool CanUseItem(RewardCurrencyType itemType, Incremental amount)
    {
        return itemDict.ContainsKey(itemType) && itemDict[itemType].Count >= amount;
    }



    /// <summary>
    /// 아이템을 가지고 있는지 확인한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <returns></returns>
    public bool HasItem(RewardCurrencyType itemType)
    {
        return itemDict.ContainsKey(itemType);
    }



    /// <summary>
    /// 인벤토리가 비어있는지 확인한다.
    /// </summary>
    /// <returns></returns>
    public bool IsEmpty()
    {
        return itemDict.Count == 0;
    }



    /// <summary>
    /// 인벤토리에 아이템을 추가한다<para/>
    /// 딕셔너리에 존재하지 않는 아이템이라면 새로 추가하고, 이미 존재한다면 개수만 증가시킨다.
    /// </summary>
    /// <param name=""></param>
    /// <param name="amount"></param>
    public void AddItem(RewardCurrencyType itemType, Incremental amount)
    {
        if(amount <= 0)
        {
            return;
        }
        if(!HasItem(itemType))
        {
            ItemInfo newInfo = new();
            switch (itemType)
            {
                case RewardCurrencyType.FirePart:
                    newInfo.Name = "일반 기계 부품";
                    break;
                case RewardCurrencyType.SpecialPart:
                    newInfo.Name = "정밀 기계 부품";
                    break;
                case RewardCurrencyType.Coin:
                    newInfo.Name = "코인";
                    break;
                default:
                    return;
            }
            newInfo.Count = amount;
            newInfo.CountString = newInfo.Count.ToString();
            itemDict.Add(itemType, newInfo);
            OnItemCountChange?.Invoke(itemType, newInfo.Count);
            print($"[InventorySystem] 새로운 아이템 추가됨 | 타입: {itemType} | 현재 개수: {newInfo.Count}");
        }
        else
        {
            var item = itemDict[itemType];
            item.Count += amount;
            item.CountString = item.Count.ToString();
            OnItemCountChange?.Invoke(itemType, item.Count);
            print($"[InventorySystem] 아이템 획득함 | 타입: {itemType} | 현재 개수: {item.Count}");
        }
    }



    /// <summary>
    ///  count 만큼 아이템을 소비한다. <para/>
    ///  만약 아이템이 없을 경우 동작을 건너뛰고, 아이템을 모두 소모할 경우 딕셔너리에서 제거한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <param name="amount"></param>
    public void UseItem(RewardCurrencyType itemType, Incremental amount)
    {
        if (amount <= 0)
        {
            return;
        }
        if (!HasItem(itemType))
        {
            print($"[InventorySystem] 아이템이 없어 사용할 수 없음 | 사용 시도 타입: {itemType} | 사용 시도 개수: {amount}");
            return;
        }
        else
        {
            var item = itemDict[itemType];

            if (!CanUseItem(itemType, amount))
            {
                print($"[InventorySystem] 아이템이 부족하여 사용할 수 없음 | 사용 시도 타입: {itemType} | 사용 시도 개수: {amount}");
            }
            else
            {
                item.Count -= amount;
                if (item.Count > 0)
                {
                    print($"아이템 사용됨 | 사용 타입: {itemType} | 사용 개수: {amount}");
                    item.CountString = item.Count.ToString();
                    OnItemCountChange?.Invoke(itemType, item.Count);
                }
                else
                {
                    itemDict.Remove(itemType);
                    OnItemCountChange?.Invoke(itemType, 0);
                    print($"아이템이 모두 사용 되어 인벤토리에서 제거됨 | 제거 타입: {itemType}");
                }
            }
        }
    }



    /// <summary>
    /// 기본적인 동작은 UseItem과 같으나, count보다 아이템 개수가 부족할 경우 남아있는 아이템들을 우선 사용한다.<para/>
    /// 실제로 사용된 아이템 개수를 리턴한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public Incremental ForceUseItem(RewardCurrencyType itemType, Incremental amount)
    {
        if (amount <= 0)
        {
            return 0;
        }
        if (!HasItem(itemType))
        {
            print($"[InventorySystem] 아이템이 없어 사용할 수 없음 | 사용 시도 타입: {itemType} | 사용 시도 개수: {amount}");
            return 0;
        }
        else
        {
            var item = itemDict[itemType];

            if (!CanUseItem(itemType, amount))
            {
                print($"[InventorySystem] 아이템 개수가 부족하여 남아있는 아이템을 모두 사용함 | 사용 타입: {itemType} | 실제 개수: {item.Count} | 사용 시도 개수: {amount}");
                var retCount = item.Count;
                item.Count = 0;
                OnItemCountChange?.Invoke(itemType, 0);
                return retCount;
            }
            else
            {
                item.Count -= amount;

                if (item.Count > 0)
                {
                    item.CountString = item.Count.ToString();
                    OnItemCountChange?.Invoke(itemType, item.Count);
                    print($"아이템 사용됨 | 사용 타입: {itemType} | 사용 개수: {amount}");
                }
                else
                {
                    itemDict.Remove(itemType);
                    OnItemCountChange?.Invoke(itemType, 0);
                    print($"아이템이 모두 사용 되어 인벤토리에서 제거됨 | 제거 타입: {itemType}");
                }

                return amount;
            }
        }
    }
}
