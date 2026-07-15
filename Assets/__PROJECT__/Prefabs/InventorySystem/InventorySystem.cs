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

/// <summary>
/// 저장 가능한 런타임 재화 지갑과 아이템 수량 변경 이벤트를 관리한다.
/// </summary>
public class InventorySystem : MonoBehaviour, ISaveable
{
    private const int DEBUG_COIN_GRANT_AMOUNT = 1000000;

    public static InventorySystem Inst;

    [Header("초기 지갑")]
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

    // 싱글톤과 메타데이터 캐시를 초기화한다
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
    // 저장 복원과 초기 지갑 적용을 처리한다
    void Start()
    {
        // 메타데이터 리스트 무결성 검사
        CheckItemMetaDataValidation();
        // SaveManager에 등록한다. 저장된 재화가 있으면 이 시점에 즉시 복원되며,
        // 복원에 성공하면 IsEmpty()가 false가 되어 아래 초기 자본 지급은 자동으로 건너뛴다.
        SaveManager.Inst.Register(this);
        // 초기 자본 추가
        ApplyInitialWalletIfNeeded();
    }
    // 저장 관리자 등록을 해제하고 싱글톤을 정리한다
    void OnDestroy()
    {
        if (SaveManager.Inst)
        {
            SaveManager.Inst.Unregister(this);
        }

        Inst = null;
    }
    // 인스펙터 초기 지갑 수량을 유효 범위로 보정한다
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

    // 초기 지갑 설정에 따라 시작 재화를 지급한다
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
    // 지정 아이템의 현재 보유 수량을 반환한다
    public Incremental GetCount(RewardCurrencyType itemType)
    {
        return itemDict.ContainsKey(itemType) ? itemDict[itemType].Count : 0;
    }


    /// <summary>
    /// 아이템 개수를 string 형식으로 리턴한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <returns></returns>
    // 지정 아이템의 현재 보유 수량 표시 문자열을 반환한다
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
    // 지정 아이템의 설명 문자열을 반환한다
    public string GetInfoString(RewardCurrencyType itemType)
    {
        return itemDict.ContainsKey(itemType) ? itemDict[itemType].InfoText : "";
    }

    /// <summary>
    /// 아이템의 이름을 리턴한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <returns></returns>
    // 지정 아이템의 표시 이름을 반환한다
    public string GetName(RewardCurrencyType itemType)
    {
        return itemDict.ContainsKey(itemType) ? itemDict[itemType].Name : "";
    }


    /// <summary>
    /// 아이템의 메타데이터를 리턴한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <returns></returns>
    // 지정 아이템 타입의 메타데이터를 반환한다
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
    // 지정 아이템을 필요한 수량만큼 사용할 수 있는지 확인한다
    public bool CanUseItem(RewardCurrencyType itemType, Incremental amount)
    {
        return itemDict.ContainsKey(itemType) && itemDict[itemType].Count >= amount;
    }


    /// <summary>
    /// 아이템을 가지고 있는지 확인한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <returns></returns>
    // 지정 아이템을 하나 이상 보유했는지 확인한다
    public bool HasItem(RewardCurrencyType itemType)
    {
        return itemDict.ContainsKey(itemType) && itemDict[itemType].Count > 0;
    }


    /// <summary>
    /// 인벤토리가 비어있는지 확인한다.
    /// </summary>
    /// <returns></returns>
    // 현재 지갑에 기록된 아이템이 없는지 확인한다
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
    // 지정 아이템을 보유 수량에 추가하고 변경 이벤트를 발생시킨다
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

    // 인스펙터 디버그 버튼에서 코인 100만 개를 지급한다
    public void AddDebugMillionCoins()
    {
        AddItem(RewardCurrencyType.Coin, DEBUG_COIN_GRANT_AMOUNT);
        Debug.Log("[InventorySystem] 디버그 버튼으로 코인 100만 개를 지급했습니다.", this);
    }


    /// <summary>
    ///  count 만큼 아이템을 소비한다. <para/>
    ///  만약 아이템이 없을 경우 동작을 건너뛰고, 아이템을 모두 소모할 경우 딕셔너리에서 제거한다.<para/>
    ///  소비에 성공하면 true를 리턴한다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <param name="amount"></param>
    // 지정 아이템을 보유 수량에서 소비한다
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
    // 보유 수량이 부족하면 가능한 만큼만 지정 아이템을 소비한다
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
    // 현재 보유 재화를 저장용 JSON 문자열로 직렬화한다
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
    // 저장 JSON에서 재화 보유량을 복원한다
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
    // 단일 비용 데이터를 지갑에 환불한다
    public void Refund(ResourceCost cost)
    {
        AddItem(cost.currencyType, cost.amount);
    }


    /// <summary>
    /// 여러 비용 데이터를 순서대로 환불한다
    /// </summary>
    /// <param name="costArray"></param>
    // 여러 비용 데이터를 순서대로 지갑에 환불한다
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
    // 단일 비용을 현재 보유 재화로 지불할 수 있는지 확인한다
    public bool CanAfford(ResourceCost cost)
    {
        if (cost == null || cost.amount <= 0)
        {
            return true;
        }

        return CanUseItem(cost.currencyType, cost.amount);
    }


    /// <summary>
    /// 여러 비용 데이터에 대해 모두 사용할 수 있는지 확인한다.
    /// </summary>
    /// <returns></returns>
    // 여러 비용을 재화 종류별로 합산해 지불 가능 여부를 확인한다
    public bool CanAfford(ResourceCost[] costArray)
    {
        if (costArray == null)
        {
            return true;
        }

        ProcessTotalCosts(costArray);
        foreach (var pair in itemCostDict)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            if (!CanUseItem(pair.Key, pair.Value))
            {
                return false;
            }
        }

        return true;
    }


    /// <summary>
    /// 비용 데이터가 지불 가능하다면 지불한다.
    /// </summary>
    /// <param name="cost"></param>
    /// <returns></returns>
    // 단일 비용을 실제로 소비한다
    public bool TrySpend(ResourceCost cost)
    {
        if (cost == null || cost.amount <= 0)
        {
            return true;
        }

        return UseItem(cost.currencyType, cost.amount);
    }


    /// <summary>
    /// 여러 비용 데이터를 모두 지불 가능할 때만 순서대로 소비한다
    /// </summary>
    /// <param name="costArray"></param>
    /// <returns></returns>
    // 여러 비용을 지불 가능할 때만 재화 종류별 합산 금액으로 소비한다
    public bool TrySpend(ResourceCost[] costArray)
    {
        if(costArray == null)
        {
            return true;
        }

        // 각 재화 종류별로 비용을 계산하여 딕셔너리에 저장
        ProcessTotalCosts(costArray);

        foreach (var pair in itemCostDict)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            if (!CanUseItem(pair.Key, pair.Value))
            {
                return false;
            }
        }

        foreach (var pair in itemCostDict)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            if (!UseItem(pair.Key, pair.Value))
            {
                return false;
            }
        }

        return true;
    }


    /// <summary>
    ///  비용 배열을 재화 종류별 총합으로 변환한다.<para/>
    ///  최종적으로 itemCostDict에 저장된다.
    /// </summary>
    /// <param name="costs"></param>
    // 비용 배열을 재화 종류별 총합으로 정리한다
    private void ProcessTotalCosts(ResourceCost[] costs)
    {
        itemCostDict.Clear();

        if(costs == null)
        {
            return;
        }

        // 딕셔너리에 총 비용을 재화 종류별로 저장한다.
        foreach (var costData in costs)
        {
            if (costData == null || costData.amount <= 0)
            {
                continue;
            }

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
    // 작은 수 표시 문자열에서 불필요한 소수점을 제거한다
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
