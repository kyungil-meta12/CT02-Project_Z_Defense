using UnityEngine;

/// <summary>
/// 드롭되는 아이텝 타입
/// </summary>
public enum DropItemType
{
    NormalPart,
    SpecialPart
}


public class DropItem : PoolObject
{
    [Header("아이템 타입")] public DropItemType itemType;
    [Header("아이템 파티클")] public PoolObject particlePrefab;
    [HideInInspector] public int dropCount;
    
    private PoolObject particleInstance;
    private ParticleSystem particle;

    public override void OnSpawn()
    {
        // 지정된 파티클을 생성 후 파티클을 초기화하고 재생한다.
        particleInstance = MemoryPool.Inst.GetInstance<PoolObject>(particlePrefab);
        particle = particleInstance.GetComponent<ParticleSystem>();
        particle.transform.position = transform.position;
        particle.Simulate(0f, true);
        particle.Play();
    }

    /// <summary>
    /// 디스폰 시 파티클 오브젝트도 함께 디스폰 된다.
    /// </summary>
    public override void OnDespawn()
    {
        particleInstance.ReturnToPool();
    }
}