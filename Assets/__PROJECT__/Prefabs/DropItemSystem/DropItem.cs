using UnityEngine;
using UnityEngine.EventSystems;


public class DropItem : PoolObject, IPointerDownHandler
{
    [HideInInspector] public RewardCurrencyType rewardType;
    [HideInInspector] public int dropCount;
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

                // 아이템 매니저에 보상 개수만큼 추가
                ItemManager.Inst.AddReward(rewardType, dropCount, false);
                ReturnInstance();
            }
        }
    }
}