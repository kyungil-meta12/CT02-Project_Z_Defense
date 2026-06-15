using System;
using UnityEngine;

[Serializable]
public struct DropAttribute
{
    public RewardCurrencyType CurrencyType;
    public DropItem ItemPrefab;
}

/// <summary>
/// 드롭 아이템을 생성하는 모듈. 좀비가 이 모듈을 가지고 있어야 아이템을 드랍할 수 있다.
/// </summary>
public class ItemDropper : MonoBehaviour
{
    [SerializeField] public DropAttribute[] drops;

    /// <summary>
    /// 특정 위치에 아이템을 생성한다.
    /// </summary>
    /// <param name="inputResult"></param>
    /// <param name="spawnPosition"></param>
    /// <param name="rewardType"></param>
    public void CreateDropItem(RewardResult inputResult, Vector3 spawnPosition, RewardCurrencyType rewardType)
    {
        // 코인은 드랍하지 않음
        if(rewardType == RewardCurrencyType.Coin)
        {
            return;
        }

        foreach(var d in drops)
        {
            if(d.CurrencyType == rewardType && inputResult.dict.ContainsKey(rewardType))
            {
                var dropCount = inputResult.dict[rewardType];
                if(dropCount == 0)
                {
                    return;
                }
                var itemComp = MemoryPool.Inst.GetInstance<DropItem>(d.ItemPrefab);
                itemComp.transform.position = spawnPosition;
                itemComp.rewardType = rewardType;
                itemComp.dropCount = dropCount;
                print($"[ItemDropper] 아이템 드롭 완료 | 타입: {rewardType} | 개수: {dropCount} | 위치: {spawnPosition}");
                return;
            }
        }
    }

    /// <summary>
    /// 드랍 아이템 테스트용 메서드. 실제 게임 로직에 사용하지 말 것.
    /// </summary>
    /// <param name="spawnPosition"></param>
    /// <param name="rewardType"></param>
    /// <param name="dropCount"></param>
    public void TestDropItem(Vector3 spawnPosition, RewardCurrencyType rewardType, int dropCount)
    {
        foreach (var d in drops)
        {
            if (d.CurrencyType == rewardType)
            {
                var itemComp = MemoryPool.Inst.GetInstance<DropItem>(d.ItemPrefab);
                itemComp.transform.position = spawnPosition;
                itemComp.rewardType = rewardType;
                itemComp.dropCount = dropCount;
                print($"[ItemDropper] 아이템 드롭 완료 | 타입: {rewardType} | 개수: {dropCount} | 위치: {spawnPosition}");
                return;
            }
        }

        Debug.LogError($"[ItemDropper] RewardCurrencyType이 일치하는 아이템을 찾을 수 없음 | 시도 타입: {rewardType}");
    }
}
