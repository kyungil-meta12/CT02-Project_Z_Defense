using System.Numerics;
using UnityEngine;
using IncrementalLib;
using System;

/// <summary>
/// 플레이어가 가진 아이템을 관리하는 싱글톤 모듈
/// </summary>
public class ItemManager : MonoBehaviour
{
    public static ItemManager Inst;

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


    void Awake()
    {
        if (Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
        }

        Inst = this;

        CoinCountString = CoinCount.ToString();
        WaveCollectCoinCountString = WaveCollectCoinCount.ToString();
        FirePartCountString = FirePartCount.ToString();
        SpecialPartCountString = SpecialPartCount.ToString();

        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // 웨이브 증가 이벤트 체이닝 추가
        GameManager.Inst.OnWaveIncrease += OnWaveIncrease;
    }

    void OnDestroy()
    {
        Inst = null;
        GameManager.Inst.OnWaveIncrease -= OnWaveIncrease;
    }

    // 웨이브 증가 이벤트
    void OnWaveIncrease(int wave)
    {
        AddCoinBouns(20);
    }

    /// <summary>
    /// 코인 소지량을 증가시킨다.<para/>
    /// 웨이브 동안에 수집된 코인이 기록된다.
    /// </summary>
    /// <param name="coinsToAdd"></param>
    public void AddCoinCount(int coinsToAdd)
    {
        CoinCount += coinsToAdd;
        WaveCollectCoinCount += coinsToAdd;
        CoinCountString = CoinCount.ToString();
        WaveCollectCoinCountString = WaveCollectCoinCount.ToString();
        OnCoinValueChange?.Invoke(CoinCountString);
    }

    /// <summary>
    /// 웨이브 동안에 수집한 WaveCollectCoinCount의 percentage만큼을 현재 코인 소지량에 추가한다.<para/>
    /// 이 메서드를 호출하면 WaveCollectCoinCount가 0으로 초기화 된다.
    /// </summary>
    /// <param name="percentage"></param>
    public void AddCoinBouns(int percentage)
    {
        int percentNumerator = percentage;
        int percentDenominator = 100;
        var result = WaveCollectCoinCount * percentNumerator / percentDenominator;
        CoinCount += result;
        WaveCollectCoinCount = 0;
        CoinCountString = CoinCount.ToString();
        WaveCollectCoinCountString = WaveCollectCoinCount.ToString();
        OnCoinValueChange?.Invoke(CoinCountString);
    }

    /// <summary>
    /// 화기 부품 소지량을 증가시킨다.
    /// </summary>
    /// <param name="partsToAdd"></param>
    public void AddFirePartCount(int partsToAdd)
    {
        FirePartCount += partsToAdd;
        FirePartCountString = FirePartCount.ToString();
        OnFirePartValueChange?.Invoke(FirePartCountString);
    }

    /// <summary>
    /// 속성 부품 소지량을 증가시킨다.
    /// </summary>
    /// <param name="partsToAdd"></param>
    public void AddSpecialPartCount(int partsToAdd)
    {
        SpecialPartCount += partsToAdd;
        SpecialPartCountString = SpecialPartCount.ToString();
        OnSpecialPartValueChange?.Invoke(SpecialPartCountString);
    }

    /// <summary>
    /// 코인을 소모할 수 있는지 확인한다.
    /// </summary>
    /// <param name="coinsToUse"></param>
    /// <returns></returns>
    public bool CanUseCoin(int coinsToUse)
    {
        return CoinCount >= coinsToUse;
    }

    /// <summary>
    /// 화기 부품을 소모할 수 있는지 확인한다.
    /// </summary>
    /// <param name="partsToUse"></param>
    /// <returns></returns>
    public bool CanUseFirePart(int partsToUse)
    {
        return FirePartCount >= partsToUse;
    }

    /// <summary>
    /// 속성 부품을 소모할 수 있는지 확인한다.
    /// </summary>
    /// <param name="partsToUse"></param>
    /// <returns></returns>
    public bool CanUseSpecialPart(int partsToUse)
    {
        return SpecialPartCount >= partsToUse;
    }

    /// <summary>
    /// 코인을 사용을 시도한다.<para/>
    /// 코인이 부족하다면 false를 리턴하고, 그렇지 않다면 코인을 소모하고 true를 리턴한다.
    /// </summary>
    /// <param name="coinsToUse"></param>
    /// <returns></returns>
    public bool TryUseCoin(int coinsToUse)
    {
        if (CoinCount < coinsToUse)
        {
            return false;
        }
        CoinCount -= coinsToUse;
        CoinCountString = CoinCount.ToString();
        OnCoinValueChange?.Invoke(CoinCountString);
        return true;
    }

    /// <summary>
    /// 화기 부품 사용을 시도한다.<para/>
    /// 화기 부품이 부족하다면 false를 리턴하고, 그렇지 않다면 화기 부품을 소모하고 true를 리턴한다.
    /// </summary>
    /// <param name="partsToUse"></param>
    /// <returns></returns>
    public bool TryUseFirePart(int partsToUse)
    {
        if (FirePartCount < partsToUse)
        {
            return false;
        }
        FirePartCount -= partsToUse;
        FirePartCountString = FirePartCount.ToString();
        OnFirePartValueChange?.Invoke(FirePartCountString);
        return true;
    }

    /// <summary>
    /// 속성 부품 사용을 시도한다.<para/>
    /// 속성 부품이 부족하다면 false를 리턴하고, 그렇지 않다면 속성 부품을 소모하고 true를 리턴한다.
    /// </summary>
    /// <param name="partsToUse"></param>
    /// <returns></returns>
    public bool TryUseSpecialPart(int partsToUse)
    {
        if (SpecialPartCount < partsToUse)
        {
            return false;
        }
        SpecialPartCount -= partsToUse;
        SpecialPartCountString = SpecialPartCount.ToString();
        OnSpecialPartValueChange?.Invoke(SpecialPartCountString);
        return true;
    }
}