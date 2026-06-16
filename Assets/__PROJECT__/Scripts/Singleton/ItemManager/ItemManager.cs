using System.Numerics;
using UnityEngine;
using IncrementalLib;
using System;

/// <summary>
/// 플레이어가 가진 재화와 아이템성 자원을 관리하는 싱글톤 지갑 모듈.
/// </summary>
public class ItemManager : MonoBehaviour
{
    public static ItemManager Inst;

    [Header("Initial Wallet")]
    [SerializeField] private bool applyInitialWalletOnAwake = true;
    [SerializeField] private bool applyInitialWalletOnlyWhenEmpty = true;
    [SerializeField] private bool logInitialWalletApply = true;
    [SerializeField] private ResourceCost[] initialWalletCurrencies = { new ResourceCost(RewardCurrencyType.Coin, 50) };

    // ItemIndicator에서 구독하는 이벤트
    // string 가비지 발생을 줄이기 위해 값이 변경되었을 때만 인디케이터의 텍스트에 반영한다.
    public event Action<string> OnCoinValueChange;
    public event Action<string> OnFirePartValueChange;
    public event Action<string> OnSpecialPartValueChange;

    // 소지한 코인 개수
    public Incremental CoinCount { get; private set; } = new(0);
    public string CoinCountString { get; private set; }

    // 소지한 화기 부품 개수
    public Incremental FirePartCount { get; private set; } = new(0);
    public string FirePartCountString { get; private set; }

    // 소지한 속성 부품 개수
    public Incremental SpecialPartCount { get; private set; } = new(0);
    public string SpecialPartCountString { get; private set; }

    // 현재 웨이브에서 얻은 코인 개수
    public Incremental WaveCollectCoinCount { get; private set; } = new(0);
    public string WaveCollectCoinCountString { get; private set; }


    // 싱글톤 인스턴스와 표시용 재화 문자열을 초기화한다
    void Awake()
    {
        if (Inst && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;

        RefreshAllCurrencyStringsAndNotify();
        ApplyInitialWalletIfNeeded();
        RefreshAllCurrencyStringsAndNotify();

        DontDestroyOnLoad(gameObject);
    }

    // 인스펙터 입력값을 유효한 초기 지갑 설정으로 보정한다
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

    // 웨이브 증가 이벤트를 구독한다
    void Start()
    {
        // 웨이브 증가 이벤트 체이닝 추가
        GameManager.Inst.OnWaveIncrease += OnWaveIncrease;
    }

    // 싱글톤과 이벤트 구독을 정리한다
    void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }

        if (GameManager.Inst)
        {
            GameManager.Inst.OnWaveIncrease -= OnWaveIncrease;
        }
    }

    // 웨이브 증가 이벤트
    void OnWaveIncrease(int wave)
    {
        AddCoinBouns(20);
    }

    // 설정된 초기 지갑 재화를 현재 플레이 세션에 적용한다
    private void ApplyInitialWalletIfNeeded()
    {
        if (!applyInitialWalletOnAwake || initialWalletCurrencies == null)
        {
            return;
        }

        if (applyInitialWalletOnlyWhenEmpty && !IsWalletEmpty())
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

            AddReward(initialCurrency.currencyType, initialCurrency.amount, false);
        }

        RefreshAllCurrencyStringsAndNotify();
        if (logInitialWalletApply)
        {
            Debug.Log($"[ItemManager] 초기 지갑 재화를 적용했습니다. Coin:{CoinCountString}, Fire:{FirePartCountString}, Special:{SpecialPartCountString}", this);
        }
    }

    // 현재 지갑에 보유 재화가 없는지 확인한다
    private bool IsWalletEmpty()
    {
        return CoinCount <= 0 && FirePartCount <= 0 && SpecialPartCount <= 0;
    }

    /// <summary>
    /// 웨이브 동안에 수집한 WaveCollectCoinCount의 percentage만큼을 현재 코인 소지량에 추가한다.<para/>
    /// 이 메서드를 호출하면 WaveCollectCoinCount가 0으로 초기화 된다.
    /// </summary>
    /// <param name="percentage"></param>
    // 웨이브 획득 코인 기준 보너스를 지급하고 웨이브 획득량을 초기화한다
    public void AddCoinBouns(int percentage)
    {
        int percentNumerator = percentage;
        int percentDenominator = 100;
        var result = WaveCollectCoinCount * percentNumerator / percentDenominator;
        CoinCount += result;
        WaveCollectCoinCount = 0;
        RefreshCurrencyStringAndNotify(RewardCurrencyType.Coin);
    }

    /// <summary>
    /// 화기 부품 소지량을 증가시킨다.
    /// </summary>
    /// <param name="partsToAdd"></param>
    // 화기 부품을 보상으로 지급한다
    public void AddFirePartCount(int partsToAdd)
    {
        AddReward(RewardCurrencyType.FirePart, partsToAdd, false);
    }

    /// <summary>
    /// 속성 부품 소지량을 증가시킨다.
    /// </summary>
    /// <param name="partsToAdd"></param>
    // 속성 부품을 보상으로 지급한다
    public void AddSpecialPartCount(int partsToAdd)
    {
        AddReward(RewardCurrencyType.SpecialPart, partsToAdd, false);
    }

    /// <summary>
    /// 지정한 재화를 보상으로 지급한다.
    /// </summary>
    /// <param name="currencyType"></param>
    /// <param name="amount"></param>
    /// <param name="countAsWaveReward"></param>
    // 지정한 재화를 보상으로 지급하고 필요하면 웨이브 획득량에 반영한다
    public void AddReward(RewardCurrencyType currencyType, int amount, bool countAsWaveReward)
    {
        if (amount <= 0)
        {
            return;
        }

        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                CoinCount += amount;
                if (countAsWaveReward)
                {
                    WaveCollectCoinCount += amount;
                }
                break;
            case RewardCurrencyType.FirePart:
                FirePartCount += amount;
                break;
            case RewardCurrencyType.SpecialPart:
                SpecialPartCount += amount;
                break;
        }

        RefreshCurrencyStringAndNotify(currencyType);
    }

    /// <summary>
    /// 환불 재화를 지급하되 웨이브 획득량에는 반영하지 않는다.
    /// </summary>
    /// <param name="currencyType"></param>
    /// <param name="amount"></param>
    // 지정한 재화를 환불하고 웨이브 획득량에는 반영하지 않는다
    public void Refund(RewardCurrencyType currencyType, int amount)
    {
        AddReward(currencyType, amount, false);
    }

    /// <summary>
    /// 단일 비용을 환불한다.
    /// </summary>
    /// <param name="cost"></param>
    // 단일 비용 데이터를 기준으로 재화를 환불한다
    public void Refund(ResourceCost cost)
    {
        if (cost == null)
        {
            return;
        }

        Refund(cost.currencyType, cost.amount);
    }

    /// <summary>
    /// 여러 비용을 환불한다.
    /// </summary>
    /// <param name="costs"></param>
    // 여러 비용 데이터를 순서대로 환불한다
    public void Refund(ResourceCost[] costs)
    {
        if (costs == null)
        {
            return;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            Refund(costs[i]);
        }
    }

    /// <summary>
    /// 화기 부품을 소모할 수 있는지 확인한다.
    /// </summary>
    /// <param name="partsToUse"></param>
    /// <returns></returns>
    // 화기 부품을 지정 수량만큼 보유했는지 확인한다
    public bool CanUseFirePart(int partsToUse)
    {
        return CanAfford(RewardCurrencyType.FirePart, partsToUse);
    }

    /// <summary>
    /// 속성 부품을 소모할 수 있는지 확인한다.
    /// </summary>
    /// <param name="partsToUse"></param>
    /// <returns></returns>
    // 속성 부품을 지정 수량만큼 보유했는지 확인한다
    public bool CanUseSpecialPart(int partsToUse)
    {
        return CanAfford(RewardCurrencyType.SpecialPart, partsToUse);
    }

    /// <summary>
    /// 지정한 재화를 충분히 보유했는지 확인한다.
    /// </summary>
    /// <param name="currencyType"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    // 지정한 재화를 충분히 보유했는지 확인한다
    public bool CanAfford(RewardCurrencyType currencyType, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                return CoinCount >= amount;
            case RewardCurrencyType.FirePart:
                return FirePartCount >= amount;
            case RewardCurrencyType.SpecialPart:
                return SpecialPartCount >= amount;
            default:
                return false;
        }
    }

    /// <summary>
    /// 단일 비용을 지불할 수 있는지 확인한다.
    /// </summary>
    /// <param name="cost"></param>
    /// <returns></returns>
    // 단일 비용 데이터를 지불할 수 있는지 확인한다
    public bool CanAfford(ResourceCost cost)
    {
        return cost == null || CanAfford(cost.currencyType, cost.amount);
    }

    /// <summary>
    /// 여러 비용을 모두 지불할 수 있는지 확인한다.
    /// </summary>
    /// <param name="costs"></param>
    /// <returns></returns>
    // 여러 비용 데이터를 모두 지불할 수 있는지 확인한다
    public bool CanAfford(ResourceCost[] costs)
    {
        if (costs == null)
        {
            return true;
        }

        GetTotalCosts(costs, out int coinCost, out int firePartCost, out int specialPartCost);
        return CanAfford(RewardCurrencyType.Coin, coinCost) &&
               CanAfford(RewardCurrencyType.FirePart, firePartCost) &&
               CanAfford(RewardCurrencyType.SpecialPart, specialPartCost);
    }

    /// <summary>
    /// 화기 부품 사용을 시도한다.<para/>
    /// 화기 부품이 부족하다면 false를 리턴하고, 그렇지 않다면 화기 부품을 소모하고 true를 리턴한다.
    /// </summary>
    /// <param name="partsToUse"></param>
    /// <returns></returns>
    // 화기 부품 소비를 시도한다
    public bool TryUseFirePart(int partsToUse)
    {
        return TrySpend(RewardCurrencyType.FirePart, partsToUse);
    }

    /// <summary>
    /// 속성 부품 사용을 시도한다.<para/>
    /// 속성 부품이 부족하다면 false를 리턴하고, 그렇지 않다면 속성 부품을 소모하고 true를 리턴한다.
    /// </summary>
    /// <param name="partsToUse"></param>
    /// <returns></returns>
    // 속성 부품 소비를 시도한다
    public bool TryUseSpecialPart(int partsToUse)
    {
        return TrySpend(RewardCurrencyType.SpecialPart, partsToUse);
    }

    /// <summary>
    /// 지정한 재화 소비를 시도한다.
    /// </summary>
    /// <param name="currencyType"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    // 지정한 재화 소비를 시도하고 성공 시 문자열 캐시와 UI 이벤트를 갱신한다
    public bool TrySpend(RewardCurrencyType currencyType, int amount)
    {
        if (!CanAfford(currencyType, amount))
        {
            return false;
        }

        if (amount <= 0)
        {
            return true;
        }

        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                CoinCount -= amount;
                break;
            case RewardCurrencyType.FirePart:
                FirePartCount -= amount;
                break;
            case RewardCurrencyType.SpecialPart:
                SpecialPartCount -= amount;
                break;
            default:
                return false;
        }

        RefreshCurrencyStringAndNotify(currencyType);
        return true;
    }

    /// <summary>
    /// 단일 비용 소비를 시도한다.
    /// </summary>
    /// <param name="cost"></param>
    /// <returns></returns>
    // 단일 비용 데이터 소비를 시도한다
    public bool TrySpend(ResourceCost cost)
    {
        return cost == null || TrySpend(cost.currencyType, cost.amount);
    }

    /// <summary>
    /// 여러 비용 소비를 원자적으로 시도한다.
    /// </summary>
    /// <param name="costs"></param>
    /// <returns></returns>
    // 여러 비용 데이터를 모두 지불 가능할 때만 순서대로 소비한다
    public bool TrySpend(ResourceCost[] costs)
    {
        if (!CanAfford(costs))
        {
            return false;
        }

        if (costs == null)
        {
            return true;
        }

        GetTotalCosts(costs, out int coinCost, out int firePartCost, out int specialPartCost);
        bool spentCoin = TrySpend(RewardCurrencyType.Coin, coinCost);
        bool spentFirePart = TrySpend(RewardCurrencyType.FirePart, firePartCost);
        bool spentSpecialPart = TrySpend(RewardCurrencyType.SpecialPart, specialPartCost);
        return spentCoin && spentFirePart && spentSpecialPart;
    }

    // 비용 배열을 재화 종류별 총합으로 변환한다
    private static void GetTotalCosts(ResourceCost[] costs, out int coinCost, out int firePartCost, out int specialPartCost)
    {
        coinCost = 0;
        firePartCost = 0;
        specialPartCost = 0;

        if (costs == null)
        {
            return;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            switch (cost.currencyType)
            {
                case RewardCurrencyType.Coin:
                    coinCost += cost.amount;
                    break;
                case RewardCurrencyType.FirePart:
                    firePartCost += cost.amount;
                    break;
                case RewardCurrencyType.SpecialPart:
                    specialPartCost += cost.amount;
                    break;
            }
        }
    }

    // 재화 문자열 캐시를 갱신하고 해당 UI 이벤트를 발행한다
    private void RefreshCurrencyStringAndNotify(RewardCurrencyType currencyType)
    {
        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                CoinCountString = CoinCount.ToString();
                WaveCollectCoinCountString = WaveCollectCoinCount.ToString();
                OnCoinValueChange?.Invoke(CoinCountString);
                break;
            case RewardCurrencyType.FirePart:
                FirePartCountString = FirePartCount.ToString();
                OnFirePartValueChange?.Invoke(FirePartCountString);
                break;
            case RewardCurrencyType.SpecialPart:
                SpecialPartCountString = SpecialPartCount.ToString();
                OnSpecialPartValueChange?.Invoke(SpecialPartCountString);
                break;
        }
    }

    // 모든 재화 문자열 캐시를 갱신하고 UI 이벤트를 발행한다
    private void RefreshAllCurrencyStringsAndNotify()
    {
        RefreshCurrencyStringAndNotify(RewardCurrencyType.Coin);
        RefreshCurrencyStringAndNotify(RewardCurrencyType.FirePart);
        RefreshCurrencyStringAndNotify(RewardCurrencyType.SpecialPart);
    }
}
