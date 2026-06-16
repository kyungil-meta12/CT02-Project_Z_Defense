using IncrementalLib;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 실제로 저장되는 아이템 정보
/// </summary>
public class ItemInfo
{
    public RewardCurrencyType Type;
    public string Name;
    public string InfoText;
    public Incremental Count;
    public string CountString;
}

/// <summary>
/// 아이템 추가 시 지정될 이름(인스펙터에서 설정)
/// </summary>
[Serializable]
public class ItemAttribute
{
    [Header("아이템 타입")] public RewardCurrencyType Type;
    [Header("표시할 아이템 이름 텍스트")] public string Name;
    [Header("표시할 아이템 설명 텍스트")][TextArea(5, 10)] public string InfoText;
}

public class InventorySystem : MonoBehaviour
{
    public static InventorySystem Inst;

    public ItemAttribute[] itemAttributes;

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
        return itemDict.ContainsKey(itemType) ? "" : itemDict[itemType].CountString;
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

            // itemAttributes에서 타입을 찾아 해당되는 타입과 이름을 새 아이템 데이터에 적용한다.
            foreach(var attr in itemAttributes)
            {
                if(attr.Type == itemType)
                {
                    newInfo.Type = attr.Type;
                    newInfo.Name = attr.Name;
                    newInfo.InfoText = attr.InfoText;
                }
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
    ///  만약 아이템이 없을 경우 동작을 건너뛰고, 아이템을 모두 소모할 경우 딕셔너리에서 제거한다.<para/>
    ///  소비에 성공하면 true를 리턴한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <param name="amount"></param>
    public bool UseItem(RewardCurrencyType itemType, Incremental amount)
    {
        if (amount <= 0)
        {
            return false;
        }
        if (!HasItem(itemType))
        {
            print($"[InventorySystem] 아이템이 없어 사용할 수 없음 | 사용 시도 타입: {itemType} | 사용 시도 개수: {amount}");
            return false;
        }
        else
        {
            var item = itemDict[itemType];

            if (!CanUseItem(itemType, amount))
            {
                print($"[InventorySystem] 아이템이 부족하여 사용할 수 없음 | 사용 시도 타입: {itemType} | 사용 시도 개수: {amount}");
                return false;
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

                return true;
            }
        }
    }



    /// <summary>
    /// 기본적인 동작은 UseItem과 같으나, count보다 아이템 개수가 부족할 경우 남아있는 아이템들을 우선 소비한다.<para/>
    /// 실제로 사용된 아이템 개수를 리턴한다.<para/>
    /// 소비에 성공하면 true를 리턴한다.<para/>
    /// refUsedAmount 레퍼런스 파라미터를 통해 소비된 개수를 얻을 수 있다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool ForceUseItem(RewardCurrencyType itemType, Incremental amount, ref Incremental refUsedAmount)
    {
        if (amount <= 0)
        {
            refUsedAmount = 0;
            return false;
        }
        if (!HasItem(itemType))
        {
            print($"[InventorySystem] 아이템이 없어 사용할 수 없음 | 사용 시도 타입: {itemType} | 사용 시도 개수: {amount}");
            refUsedAmount = 0;
            return false;
        }
        else
        {
            var item = itemDict[itemType];

            if (!CanUseItem(itemType, amount))
            {
                print($"[InventorySystem] 아이템 개수가 부족하여 남아있는 아이템을 모두 사용함 | 사용 타입: {itemType} | 실제 개수: {item.Count} | 사용 시도 개수: {amount}");
                Incremental copyIncremental = new(item.Count);
                refUsedAmount = copyIncremental;
                item.Count = 0;
                OnItemCountChange?.Invoke(itemType, 0);
                return true;
            }
            else
            {
                item.Count -= amount;

                if (item.Count > 0)
                {
                    item.CountString = item.Count.ToString();
                    OnItemCountChange?.Invoke(itemType, item.Count);
                    print($"아이템 사용됨 | 사용 타입: {itemType} | 사용 개수: {amount}");
                    refUsedAmount = amount;
                }
                else
                {
                    itemDict.Remove(itemType);
                    OnItemCountChange?.Invoke(itemType, 0);
                    refUsedAmount = amount;
                    print($"아이템이 모두 사용 되어 인벤토리에서 제거됨 | 제거 타입: {itemType}");
                }

                return true;
            }
        }
    }
}
