using UnityEngine;
using UnityEngine.EventSystems;


public class DropItem : PoolObject, IPointerDownHandler
{
    [Header("아이템 파티클 프리펩")] public DropItemParticle particlePrefab;

    [HideInInspector] public RewardCurrencyType rewardType;
    [HideInInspector] public int dropCount;
    
    private PoolObject particleInstance;
    private ParticleSystem particle;
    private MeshCollider meshCollider;

    void Awake()
    {
        meshCollider = GetComponent<MeshCollider>();
    }

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

    /// <summary>
    /// 아이템을 터치하면 회수되면서 월드 상에서 인스턴스가 메모리 풀로 회수된다.
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.isValid)
        {
            if (eventData.pointerCurrentRaycast.gameObject == this.gameObject)
            {
                print($"[DropItem] 아이템 회수 됨 | 아이템: {gameObject.name} | 획득량: {dropCount}");
                ReturnInstance();
            }
        }
    }
}