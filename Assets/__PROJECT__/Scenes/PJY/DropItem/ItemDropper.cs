using UnityEngine;

/// <summary>
/// 드롭 아이템을 생성하는 모듈. 모든 좀비가 가지고 있어야 아이템을 드랍할 수 있다.
/// </summary>
public class ItemDropper : MonoBehaviour
{
    public DropItem itemObjectPrefab;

    /// <summary>
    /// 특정 위치에 드롭 아이템을 생성하고 아이텝의 타입과 드롭 개수를 설정한다.
    /// </summary>
    /// <param name="spawnPosition"></param>
    /// <param name="dropCount"></param>
    public void DropItem(Vector3 spawnPosition, int dropCount)
    {
        var itemComp = MemoryPool.Inst.GetInstance<DropItem>(itemObjectPrefab);
        itemComp.dropCount = dropCount;
        itemComp.transform.position = spawnPosition;
    }
}
