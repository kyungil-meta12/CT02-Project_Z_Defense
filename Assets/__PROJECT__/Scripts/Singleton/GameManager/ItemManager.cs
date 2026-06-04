using System.Numerics;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 플레이어가 가진 아이템을 관리하는 싱글톤 모듈
/// </summary>
public class ItemManager : MonoBehaviour
{
    public static ItemManager Inst;

    // BigInteger: 메모리가 버티는 한 무제한으로 큰 수를 저장할 수 있는 정수

    // 소지한 코인 개수
    public BigInteger CoinCount{ get; private set; } = 0;

    // 소지한 화기 부품 개수
    public BigInteger FirePartCount { get; private set; } = 0;

    // 소지한 속성 부품 개수
    public BigInteger SpecialPartCount { get; private set; } = 0;


    void Awake()
    {
        if(Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
        }
        Inst = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 코인 소지량을 증가시킨다.
    /// </summary>
    /// <param name="coinsToAdd"></param>
    public void AddCoinCount(int coinsToAdd)
    {
        CoinCount += coinsToAdd;
    }

    /// <summary>
    /// 화기 부품 소지량을 증가시킨다.
    /// </summary>
    /// <param name="partsToAdd"></param>
    public void AddFirePartCount(int partsToAdd)
    {
        FirePartCount += partsToAdd;
    }

    /// <summary>
    /// 속성 부품 소지량을 증가시킨다.
    /// </summary>
    /// <param name="partsToAdd"></param>
    public void AddSpecialPartCount(int partsToAdd)
    {
        SpecialPartCount += partsToAdd;
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
        if(CoinCount < coinsToUse)
        {
            return false;
        }
        CoinCount -= coinsToUse;
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
        if(FirePartCount < partsToUse)
        {
            return false;
        }
        FirePartCount -= partsToUse;
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
        return true;
    }


    /// <summary>
    /// 현재 코인 소지량을 string 타입으로 리턴한다.
    /// </summary>
    /// <returns></returns>
    public string CoinCountToString()
    {
        return CoinCount.ToString();
    }

    /// <summary>
    /// 현재 화기 부품 소지량을 string 타입으로 리턴한다.
    /// </summary>
    /// <returns></returns>
    public string FirePartCountToString()
    {
        return FirePartCount.ToString();
    }

    /// <summary>
    /// 현재 속성 부품 소지량을 string 타입으로 리턴한다.
    /// </summary>
    /// <returns></returns>
    public string SpecialPartCountToString()
    {
        return SpecialPartCount.ToString();
    }
}