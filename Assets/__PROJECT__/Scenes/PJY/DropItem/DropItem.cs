using UnityEngine;


public class DropItem : PoolObject
{
    [Header("아이템 파티클 프리펩")] public DropItemParticle particlePrefab;

    [HideInInspector] public RewardCurrencyType rewardType;
    [HideInInspector] public int dropCount;
    
    private PoolObject particleInstance;
    private ParticleSystem particle;

    public override void OnSpawn()
    {
        // 지정된 파티클을 메모리풀에서 생성한다. 파티클 재생은 DropItemParticle에서 자체적으로 처리한다.
        particleInstance = MemoryPool.Inst.GetInstance<DropItemParticle>(particlePrefab);
        particle = particleInstance.GetComponent<ParticleSystem>();
        particle.transform.position = transform.position;
    }

    /// <summary>
    /// 디스폰 시 파티클 오브젝트도 함께 디스폰 된다.
    /// </summary>
    public override void OnDespawn()
    {
        particleInstance.ReturnToPool();
    }
}