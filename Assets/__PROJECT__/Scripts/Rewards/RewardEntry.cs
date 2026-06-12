using System;
using UnityEngine;

/// <summary>
/// 처치 보상으로 지급할 재화와 수량, 드랍 확률을 정의한다.
/// </summary>
[Serializable]
public class RewardEntry
{
    public RewardCurrencyType currencyType;
    [Min(0)] public int amount;
    [Range(0.0f, 1.0f)] public float dropChance = 1.0f;
}
