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
    [SerializeField] public DropItem dropItemPrefab;

    /// <summary>
    /// 특정 위치에 아이템을 생성한다. 랜덤으로 위치 편차를 준다.
    /// </summary>
    /// <param name="inputResult"></param>
    /// <param name="spawnPosition"></param>
    /// <param name="rewardType"></param>
    public void CreateDropItem(RewardResult inputResult, Vector3 spawnPosition, RewardCurrencyType rewardType)
    {
        // 코인은 드랍하지 않음
        // inputResult.dict에 키가 없어도 드랍하지 않는다.
        if(rewardType == RewardCurrencyType.Coin || !inputResult.dict.ContainsKey(rewardType))
        {
            return;
        }

        // 주변 2.5f 반경에 속하는 랜덤한 위치에 아이템을 드랍한다.
        var center = spawnPosition;
        float radius = 2.5f;
        Vector2 randomCirclePoint = UnityEngine.Random.insideUnitCircle * radius;
        Vector3 randomPosition = center + new Vector3(randomCirclePoint.x, center.y, randomCirclePoint.y);
        
        // 드롭 아이템 프리펩을 생성하고, 타입을 설정하고, 위치를 설정한다.
        var itemComp = MemoryPool.Inst.GetInstance<DropItem>(dropItemPrefab);
        itemComp.transform.position = randomPosition;
        itemComp.SetupItem(rewardType, inputResult.dict[rewardType]);
    }
}
