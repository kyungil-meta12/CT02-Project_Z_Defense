using System.Collections.Generic;

/// <summary>
/// 좀비 사망 시 최종 보상 값을 저장하는 결과 컨테이너.
/// </summary>
public class RewardResult
{
    public readonly Dictionary<RewardCurrencyType, int> dict = new();

    // 이전 사망 보상 결과가 다음 풀 재사용에 남지 않도록 비운다
    public void Clear()
    {
        dict.Clear();
    }

    // 지정한 재화의 최종 보상 수량을 안전하게 조회한다
    public bool TryGetAmount(RewardCurrencyType currencyType, out int amount)
    {
        return dict.TryGetValue(currencyType, out amount);
    }
}
