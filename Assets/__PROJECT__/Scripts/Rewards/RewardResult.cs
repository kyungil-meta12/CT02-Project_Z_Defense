using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 좀비 사망 시 최종 보상 값을 저장하는 구조체
/// </summary>
public class RewardResult
{
    public Dictionary<RewardCurrencyType, int> dict = new();
}