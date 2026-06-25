using UnityEngine;

public class DropItem : PoolObject
{
    [HideInInspector] public RewardCurrencyType rewardType;
    [HideInInspector] public int dropCount;

    public PoolParticle pickupParticlePrefab;

    private ParticleSystem particle;

    void Awake()
    {
        particle = GetComponentInChildren<ParticleSystem>();
    }

    public override void OnSpawn()
    {
        // 지정된 파티클을 메모리풀에서 생성한다. 파티클 재생은 DropItemParticle에서 자체적으로 처리한다.
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particle.Play();
        DropItemManager.Inst.AddItem(this); // 드롭된 아이템 목록에 아이템 추가
    }

    /// <summary>
    ///  아이템을 획득할 때 호출되는 함수
    /// </summary>
    public void GetItem()
    {
        //print($"[DropItem] 아이템 회수 됨 | 아이템: {gameObject.name} | 획득량: {dropCount}");

        // 아이템 매니저에 보상 개수만큼 추가
        InventorySystem.Inst.AddItem(rewardType, dropCount);

        // 드옵 아이템 목록에서 아이템 제거
        DropItemManager.Inst.RemoveItem(this);

        // 아이템 획득 파티클 추가
        var pickupParticle = MemoryPool.Inst.GetInstance<PoolParticle>(pickupParticlePrefab);
        pickupParticle.transform.position = transform.position;
        pickupParticle.transform.localScale = transform.localScale;

        ReturnInstance();
    }
}