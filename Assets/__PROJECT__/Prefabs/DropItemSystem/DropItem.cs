using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드롭 아이템 파티클 속성
/// </summary>
[Serializable]
public struct DropItemAttribute
{
    public ItemGrade Grade;
    public PoolObject Particle;
}

public class DropItem : PoolObject
{
    public PoolParticle pickupParticlePrefab;
    public MeshRenderer mashRenderer;
    public List<DropItemAttribute> particleList;

    private Dictionary<ItemGrade, PoolObject> particleDict = new();
    private PoolObject selectedParticle;
    private Material material;
    private RewardCurrencyType rewardType;
    private int dropCount;

    void Awake()
    {
        material = mashRenderer.material;

        // 빠른 탐색을 위해 파티클을 등급별로 딕셔너리에 추가
        foreach(var p in particleList)
        {
            particleDict.Add(p.Grade, p.Particle);
        }
    }

    public override void OnSpawn()
    {
        DropItemManager.Inst.AddItem(this); // 드롭된 아이템 목록에 아이템 추가
    }

    /// <summary>
    /// 아이템을 세팅한다. 세팅 시 각 타입에 맞는 이미지로 변경된다.
    /// </summary>
    /// <param name="itemType"></param>
    /// <param name="amount"></param>
    public void SetupItem(RewardCurrencyType itemType, int amount)
    {
        rewardType = itemType;
        dropCount = amount;
        var metaData = InventorySystem.Inst.GetMetaData(itemType);
        material.mainTexture = metaData.ItemImage.texture;

        // 등급에 맞는 파티클을 선택
        // PoolParticle에서 알아서 재생
        selectedParticle = MemoryPool.Inst.GetInstance<PoolObject>(particleDict[metaData.Grade]);
        selectedParticle.transform.position = transform.position;
    }

    /// <summary>
    ///  아이템을 획득할 때 호출되는 함수
    /// </summary>
    public void GetItem()
    {
        //print($"[DropItem] 아이템 회수 됨 | 아이템: {gameObject.name} | 획득량: {dropCount}");

        // 아이템 매니저에 보상 개수만큼 추가
        InventorySystem.Inst.AddReward(rewardType, dropCount);

        // 아이템 획득 파티클 추가
        var pickupParticle = MemoryPool.Inst.GetInstance<PoolParticle>(pickupParticlePrefab);
        pickupParticle.transform.position = transform.position;
        pickupParticle.transform.localScale = transform.localScale;

        // 파티클도 같이 회수
        selectedParticle.ReturnToPool();
        ReturnInstance();
    }

    /// <summary>
    /// 월드에서 직접 아이템을 줍는다.
    /// </summary>
    public void PickItem()
    {
        GetItem(); // 아이템 획득
        
        // 드롭 아이템 목록에서 아이템 제거
        DropItemManager.Inst.RemoveItem(this);
    }
}