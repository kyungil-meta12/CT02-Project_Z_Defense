using System;
using IncrementalLib;
using UnityEngine;

/// <summary>
/// 업그레이드, 진화, 배치, 스킬 등에서 소비할 재화 비용을 정의한다.
/// </summary>
[Serializable]
public class ResourceCost
{
    public RewardCurrencyType currencyType;
    [Min(0)] public int amount;

    [NonSerialized] private Incremental runtimeAmount;
    [NonSerialized] private int cachedSerializedAmount = int.MinValue;

    // 직렬화된 정수 비용을 런타임 계산용 Incremental 값으로 반환한다
    public Incremental RuntimeAmount
    {
        get
        {
            if (runtimeAmount == null || cachedSerializedAmount != amount)
            {
                cachedSerializedAmount = amount;
                runtimeAmount = new Incremental(Mathf.Max(0, amount));
            }

            return runtimeAmount;
        }
    }

    // Unity 직렬화를 위해 기본 생성자를 유지한다
    public ResourceCost()
    {
    }

    // 런타임 계산 결과 비용을 생성한다
    public ResourceCost(RewardCurrencyType currencyType_, int amount_)
    {
        currencyType = currencyType_;
        amount = Mathf.Max(0, amount_);
    }
}
