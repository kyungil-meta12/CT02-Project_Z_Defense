using IncrementalLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 실제로 저장되는 아이템 정보
/// </summary>
public class ItemData
{
    public RewardCurrencyType Type;
    public string Name;
    public string InfoText;
    public Incremental PrevCount = new();
    public Incremental Count = new();
    public string CountString;
}

/// <summary>
/// 저장 파일에 기록되는 재화 한 종류의 수량 데이터
/// </summary>
[Serializable]
public class CurrencySaveEntry
{
    public RewardCurrencyType Type;
    public double Value;
    public int Exponent;
    public bool Negative;
}

/// <summary>
/// 재화 저장 파일 전체 구조
/// </summary>
[Serializable]
public class CurrencySaveData
{
    public List<CurrencySaveEntry> Currencies = new();
}


public class InventorySystem : MonoBehaviour, ISaveable
{
    public static InventorySystem Inst;

    [Header("Initial Wallet")]
    [SerializeField] private bool applyInitialWalletOnAwake = true;
    [SerializeField] private bool applyInitialWalletOnlyWhenEmpty = true;
    [SerializeField] private bool logInitialWalletApply = true;
    [SerializeField] private ResourceCost[] initialWalletCurrencies = { new ResourceCost(RewardCurrencyType.Coin, 50) };

    // SaveManager 저장 파일 안에서 재화 데이터를 구분하는 키
    public string SaveKey => "Currency";
    // SaveManager에서 복원 중인지 여부. 복원 중 발생하는 변경은 다시 저장을 유발하면 안 된다.
    private bool isLoadingSave;

    // 아이템이 딕셔너리에 추가될 때 사용할 메타데이터 리스트
    public ItemMetaDataListSo itemMetaDataListSo;
    private Dictionary<RewardCurrencyType, ItemData> itemDict = new();
    private Dictionary<RewardCurrencyType, int> itemCostDict = new();
    private Dictionary<RewardCurrencyType, ItemMetaDataSo> itemMetaDataDict = new();
    private List<ItemMetaDataSo> metaDataValidationList = new();

    // 아이템 개수 변경 이벤트
    public Action<ItemData, Incremental> OnItemCountChange;

    // 웨이브 동안에 얻은 코인 개수
    public Incremental WaveCollectCoinCount { get; private set; } = new(0);

    /// <summary>
    /// RewardCurrencyType의 값들을 저장한 배열
    /// </summary>
    public Array Types { get; private set; }


    void Awake()
    {
        if (Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
            return;
        }

        // 재화 타입 열거형들을 배열에 저장
        Types = Enum.GetValues(typeof(RewardCurrencyType));

        // 아이쳄 메타 데이터들을 딕셔너리에 저장
        var metaDataList = itemMetaDataListSo.MetaDataList;
        foreach(var data in metaDataList)
        {
            itemMetaDataDict.Add(data.Type, data);
        }

        metaDataValidationList = metaDataList;

        Inst = this;
    }

    void Start()
    {
        // 메타데이터 리스트 무결성 검사
        CheckItemMetaDataValidation();
        // SaveManager에 등록한다. 저장된 재화가 있으면 이 시점에 즉시 복원되며,
        // 복원에 성공하면 IsEmpty()가 false가 되어 아래 초기 자본 지급은 자동으로 건너뛴다.
        //SaveManager.Inst.Register(this);
        // 초기 자본 추가
        ApplyInitialWalletIfNeeded();
    }

    void OnDestroy()
    {
        if (SaveManager.Inst)
        {
            SaveManager.Inst.Unregister(this);
        }

        Inst = null;
    }

    void OnValidate()
    {
        if (initialWalletCurrencies == null)
        {
            return;
        }

        for (int i = 0; i < initialWalletCurrencies.Length; i++)
        {
            ResourceCost initialCurrency = initialWalletCurrencies[i];
            if (initialCurrency == null)
            {
                continue;
            }

            initialCurrency.amount = Mathf.Max(0, initialCurrency.amount);
        }
    }

    // 아이템 메타 데이터 무결성 체크
    // 중복된 이미지나 타입이 발견되면 에러 메시지 발생후 에디터 종료
    private void CheckItemMetaDataValidation()
    {
        bool isDuplicateTypeExist = metaDataValidationList.GroupBy(m => m.Type).Any(g => g.Count() > 1);
        bool isDuplicateImageExist = metaDataValidationList.GroupBy(m => m.ItemImage).Any(g => g.Count() > 1);
        if (isDuplicateTypeExist || isDuplicateImageExist)
        {
            Debug.LogError("[InventorySystem] 아이템 메타 데이터 무결성이 훼손되었습니다.");
            var duplicateTypeGroup = metaDataValidationList.GroupBy(m => m.Type).Where(g => g.Count() > 1);
            var duplicateImageGroup = metaDataValidationList.GroupBy(m => m.ItemImage).Where(g => g.Count() > 1);

            foreach (var group in duplicateTypeGroup)
            {
                Debug.LogError($"[InventorySystem] 중복된 아이템 타입 발견: {group.Key} | 개수: {group.Count()}");
                foreach(var item in group)
                {
                    Debug.LogError($"[InventorySystem] 아이템: [{item.Type}]Type | {item.Name}");
                }
            }

            foreach (var group in duplicateImageGroup)
            {
                Debug.LogError($"[InventorySystem] 중복된 아이템 이미지 발견: {group.Key} | 개수: {group.Count()}");
                foreach(var item in group)
                {
                    Debug.LogError($"[InventorySystem] 아이템: {item.Name}");
                }
            }

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; 
        #endif
        }
    }


    private void ApplyInitialWalletIfNeeded()
    {
        if (!applyInitialWalletOnAwake || initialWalletCurrencies == null)
        {
            return;
        }

        if (applyInitialWalletOnlyWhenEmpty && !IsEmpty())
        {
            return;
        }

        for (int i = 0; i < initialWalletCurrencies.Length; i++)
        {
            ResourceCost initialCurrency = initialWalletCurrencies[i];
            if (initialCurrency == null || initialCurrency.amount <= 0)
            {
                continue;
            }

            AddItem(initialCurrency.currencyType, initialCurrency.amount);
        }

        if (logInitialWalletApply)
        {    
            Debug.Log($"[InventorySystem] 초기 지갑 재화를 적용했습니다.");
            foreach(RewardCurrencyType type in Types)
            {
                if(itemDict.ContainsKey(type)) // 존재하는 아이템에 대해서만 출력한다.
                {
                    Debug.Log(GetFormatString(type));
                }
            }
        }
    }

    // 값 변경 이벤트를 발생시킨다.
    // 이전 값과의 차이도 이벤트로 전달한다.
    private void InvokeEvent(ItemData data)
    {
        var prevCountCopy = new Incremental(data.Count - data.PrevCount);
        OnItemCountChange?.Invoke(data, prevCountCopy);
        data.PrevCount = new Incremental(data.Count);

        // 저장 파일을 불러오는 중에 발생한 변경은 저장이 필요한 변경이 아니므로 dirty 표시하지 않는다
        if (!isLoadingSave)
        {
            SaveManager.Inst?.MarkDirty();
        }
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


    /// <summary>
    /// 아이템 개수를 string 형식으로 리턴한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <returns></returns>
    public string GetCountString(RewardCurrencyType itemType)
    {
        return itemDict.ContainsKey(itemType) ? itemDict[itemType].CountString : "0";
    }

    // 아이템 이름과 개수를 합친 형식으로 리턴한다.
    public string GetFormatString(RewardCurrencyType itemType)
    {
        if (HasItem(itemType))
        {
            return $"[{itemType}]: " + GetCountString(itemType);
        }
        return "[N/A] : 0";
    }

    /// <summary>
    /// 아이템의 정보 텍스트를 얻는다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <returns></returns>
    public string GetInfoString(RewardCurrencyType itemType)
    {
        return itemDict.ContainsKey(itemType) ? itemDict[itemType].InfoText : "";
    }

    /// <summary>
    /// 아이템의 이름을 리턴한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <returns></returns>
    public string GetName(RewardCurrencyType itemType)
    {
        return itemDict.ContainsKey(itemType) ? itemDict[itemType].Name : "";
    }


    /// <summary>
    /// 아이템의 메타데이터를 리턴한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <returns></returns>
    public ItemMetaDataSo GetMetaData(RewardCurrencyType itemType)
    {
        if(itemMetaDataDict.ContainsKey(itemType))
        {
            return itemMetaDataDict[itemType];
        }
        else
        {
            return null;
        }
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
        return itemDict.ContainsKey(itemType) && itemDict[itemType].Count > 0;
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
            Debug.Log($"[InventorySystem] 0개 또는 음수 추가를 시도함 | 추가 시도 타입: {itemType}");
            return;
        }
        if (!itemDict.ContainsKey(itemType))
        {
            var metaData = GetMetaData(itemType);
            if(metaData == null)
            {
                Debug.LogError($"[InventorySystem] 아이템 정보를 찾을 수 없음 | 찾기 시도 타입: {itemType}");
                return;
            }

            ItemData newItem = new()
            {
                Type = metaData.Type,
                Name = metaData.Name,
                InfoText = metaData.InfoText,
                Count = amount
            };

            var newCountStr = RemoveDemicalPoint(newItem.Count.ToString(), newItem.Count);
            newItem.CountString = newCountStr;
            itemDict.Add(itemType, newItem);
            InvokeEvent(newItem);

            print($"[InventorySystem] 새로운 아이템 추가됨 | 타입: {itemType} | 현재 개수: {newItem.Count}");
        }
        else
        {
            var item = itemDict[itemType];
            item.Count += amount;
            item.CountString = RemoveDemicalPoint(item.Count.ToString(), item.Count);
            InvokeEvent(item);
            //print($"[InventorySystem] 아이템 획득함 | 타입: {itemType} | 현재 개수: {item.Count}");
        }
    }

    /// <summary>
    /// 좀비 처치/드롭 회수 등 웨이브 동안 실제로 획득하는 보상을 지급한다.<para/>
    /// 초기 지갑 지급, 환불, 보너스 지급은 웨이브 동안 모은 코인량을 오염시키므로 AddItem을 직접 사용한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <param name="amount"></param>
    // 웨이브 보상으로 재화를 지급하고, Coin이면 웨이브 동안 모은 코인량도 함께 갱신한다
    public void AddReward(RewardCurrencyType itemType, Incremental amount)
    {
        AddItem(itemType, amount);

        if (amount > 0 && itemType == RewardCurrencyType.Coin)
        {
            WaveCollectCoinCount += amount;
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
            Debug.LogError($"[InventorySystem] 0개 또는 음수 사용을 시도함 | 사용 시도 타입: {itemType}");
            return false;
        }
        if (!HasItem(itemType))
        {
            Debug.LogWarning($"[InventorySystem] 아이템이 없어 사용할 수 없음 | 사용 시도 타입: {itemType} | 사용 시도 개수: {amount}");
            return false;
        }
        else
        {
            var item = itemDict[itemType];

            if (!CanUseItem(itemType, amount))
            {
                Debug.LogWarning($"[InventorySystem] 아이템이 부족하여 사용할 수 없음 | 사용 시도 타입: {itemType} | 사용 시도 개수: {amount} | 현재 개수: {item.Count}");
                return false;
            }
            else
            {
                item.Count -= amount;
                item.CountString = RemoveDemicalPoint(item.Count.ToString(), item.Count);
                InvokeEvent(item);
                print($"[InventorySystem] 아이템 사용됨 | 사용 타입: {itemType} | 사용 개수: {amount} | 현재 개수: {item.Count}");
                return true;
            }
        }
    }


    /// <summary>
    /// 기본적인 동작은 UseItem과 같으나, count보다 아이템 개수가 부족할 경우 남아있는 아이템들을 우선 소비한다.<para/>
    /// 실제로 사용된 아이템 개수를 리턴한다.<para/>
    /// 소비에 성공하면 true를 리턴한다.<para/>
    /// amountUsed 레퍼런스 파라미터를 통해 소비된 개수를 얻을 수 있다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <param name="amount"></param>
    /// <param name="amountUsed"></param>
    /// <returns></returns>
    public bool ForceUseItem(RewardCurrencyType itemType, Incremental amount, ref Incremental amountUsed)
    {
        if (amount <= 0)
        {
            amountUsed = 0;
            Debug.LogError($"[InventorySystem] 0개 또는 음수 사용을 시도함 | 사용 시도 타입: {itemType}");
            return false;
        }
        if (!HasItem(itemType))
        {
            amountUsed = 0;
            Debug.LogWarning($"[InventorySystem] 아이템이 없어 사용할 수 없음 | 사용 시도 타입: {itemType} | 사용 시도 개수: {amount}");
            return false;
        }
        else
        {
            var item = itemDict[itemType];

            if (!CanUseItem(itemType, amount))
            {
                var copyValue = new Incremental(item.Count);
                amountUsed = copyValue;
                item.Count = 0;
                item.CountString = "0";
                InvokeEvent(item);
                print($"[InventorySystem] 아이템 개수가 부족하여 남아있는 아이템을 모두 사용함 | 사용 타입: {itemType} | 실제 사용 개수: {item.Count} | 사용 시도 개수: {amount}");
                return true;
            }
            else
            {
                item.Count -= amount;
                item.CountString = RemoveDemicalPoint(item.Count.ToString(), item.Count);
                InvokeEvent(item);
                amountUsed = amount;
                print($"[InventorySystem] 아이템 사용됨 | 사용 타입: {itemType} | 사용 개수: {amount} | 남은 개수: {item.Count}");
                return true;
            }
        }
    }


    //Save / Load (ISaveable)
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// 현재 보유한 모든 재화를 JSON 문자열로 직렬화한다. SaveManager가 저장 시점에 호출한다.
    /// </summary>
    public string CaptureSaveData()
    {
        CurrencySaveData saveData = new();
        foreach (var pair in itemDict)
        {
            Incremental count = pair.Value.Count;
            if (count <= 0)
            {
                continue;
            }

            saveData.Currencies.Add(new CurrencySaveEntry
            {
                Type = pair.Key,
                Value = count.Value,
                Exponent = count.Exponent,
                Negative = count.Negative
            });
        }

        return JsonUtility.ToJson(saveData);
    }

    /// <summary>
    /// 저장 파일에서 읽어온 JSON으로 재화를 복원한다. SaveManager.Register 시점에 호출된다.
    /// </summary>
    public void RestoreSaveData(string json)
    {
        CurrencySaveData saveData = JsonUtility.FromJson<CurrencySaveData>(json);
        if (saveData?.Currencies == null)
        {
            return;
        }

        isLoadingSave = true;
        try
        {
            foreach (CurrencySaveEntry entry in saveData.Currencies)
            {
                double signedValue = entry.Negative ? -entry.Value : entry.Value;
                AddItem(entry.Type, new Incremental(signedValue, entry.Exponent));
            }
        }
        finally
        {
            isLoadingSave = false;
        }
    }


    //Utility
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// 웨이브 동안에 수집한 WaveCollectCoinCount의 percentage만큼을 현재 코인 소지량에 추가한다.<para/>
    /// GameManger에서 웨이브 증가 시 호출된다.
    /// </summary>
    /// <param name="percentage"></param>
    // 웨이브 획득 코인 기준 보너스를 지급하고 웨이브 획득량을 초기화한다
    public void AddCoinBouns(int percentage)
    {
        int percentNumerator = percentage;
        int percentDenominator = 100;
        var result = WaveCollectCoinCount * percentNumerator / percentDenominator;
        AddItem(RewardCurrencyType.Coin, result);
        WaveCollectCoinCount = 0;
    }

    /// <summary>
    /// 웨이브 실패로 재시작할 때 그동안 모은 코인량을 0으로 되돌려 패널티를 준다.<para/>
    /// GameManager에서 게이트 붕괴로 이전 웨이브를 재시작할 때 호출된다.
    /// </summary>
    // 웨이브 재시작 패널티로 웨이브 동안 모은 코인량을 초기화한다
    public void ResetWaveCollectCoinCount()
    {
        WaveCollectCoinCount = 0;
    }

    /// <summary>
    /// 비용 데이터를 환불한다.
    /// </summary>
    /// <param name="cost"></param>
    public void Refund(ResourceCost cost)
    {
        AddItem(cost.currencyType, cost.amount);
    }


    /// <summary>
    /// 여러 비용 데이터를 순서대로 환불한다
    /// </summary>
    /// <param name="costArray"></param>
    public void Refund(ResourceCost[] costArray)
    {
        foreach(var cost in costArray)
        {
            Refund(cost);
        }
    }


    /// <summary>
    /// 비용 데이터에 대해 사용할 수 있는지 확인한다.
    /// </summary>
    /// <param name="cost"></param>
    /// <returns></returns>
    public bool CanAfford(ResourceCost cost)
    {
        return CanUseItem(cost.currencyType, cost.amount);
    }


    /// <summary>
    /// 여러 비용 데이터에 대해 모두 사용할 수 있는지 확인한다.
    /// </summary>
    /// <returns></returns>
    public bool CanAfford(ResourceCost[] costArray)
    {
        // 아이템이 사용 가능하다면 trueCount를 1씩 올린다.
        int trueCount = 0;
        foreach(var cost in costArray)
        {
            if(CanAfford(cost))
            {
                trueCount++;
            }
        }
        // costArray의 모든 아이템들이 사용 가능하다면 trueCount가 costArray.Length와 동일해지므로 결과적으로 true를 리턴하게 된다.
        return trueCount == costArray.Length;
    }


    /// <summary>
    /// 비용 데이터가 지불 가능하다면 지불한다.
    /// </summary>
    /// <param name="cost"></param>
    /// <returns></returns>
    public bool TrySpend(ResourceCost cost)
    {
        return UseItem(cost.currencyType, cost.amount);
    }


    /// <summary>
    /// 여러 비용 데이터를 모두 지불 가능할 때만 순서대로 소비한다
    /// </summary>
    /// <param name="costArray"></param>
    /// <returns></returns>
    public bool TrySpend(ResourceCost[] costArray)
    {
        if(costArray == null)
        {
            return false;
        }

        // 각 재화 종류별로 비용을 계산하여 딕셔너리에 저장
        ProcessTotalCosts(costArray);

        // 아이템 사용에 성공했다면 trueCount를 1씩 증가시킨다.
        int trueCount = 0;
        foreach (var cost in costArray)
        {
            if (UseItem(cost.currencyType, itemCostDict[cost.currencyType]))
            {
                trueCount++;
            }
        }
        // costArray의 모든 아이템들을 사용하는 것에 성공했다면 trueCount가 costArray.Length와 동일해지므로 결과적으로 true를 리턴하게 된다.
        return trueCount == costArray.Length;
    }


    /// <summary>
    ///  비용 배열을 재화 종류별 총합으로 변환한다.<para/>
    ///  최종적으로 itemCostDict에 저장된다.
    /// </summary>
    /// <param name="costs"></param>
    private void ProcessTotalCosts(ResourceCost[] costs)
    {
        if(costs == null)
        {
            return;
        }

        // 현재 딕셔너리에 저장된 비용들을 초기화한다.
        foreach(var cost in costs)
        {
            if(!itemCostDict.ContainsKey(cost.currencyType))
            {
                itemCostDict.Add(cost.currencyType, 0);
            }
            else
            {
                itemCostDict[cost.currencyType] = 0;
            }
        }

        // 딕셔너리에 총 비용을 재화 종류별로 저장한다.
        foreach (var costData in costs)
        {
            if(!itemCostDict.ContainsKey(costData.currencyType))
            {
                itemCostDict.Add(costData.currencyType, costData.amount);
            }
            else
            {
                itemCostDict[costData.currencyType] += costData.amount;
            }
        }
    }

    private string RemoveDemicalPoint(string str, Incremental incrementalInst)
    {
        if(incrementalInst >= 1000)
        {
            return str;
        }
        int index = str.IndexOf('.');
        if (index != -1)
        {
            string result = str.Substring(0, index);
            return result;
        }
        return str;
    }
}