using UnityEngine;

public class CoinParticleCreator : MonoBehaviour
{
    public static CoinParticleCreator Inst;

    [Header("브론즈 코인 파티클")] public PoolObject bronzeCoinParticle;
    [Header("실버 코인 파티클")] public PoolObject silverCoinParticle;
    [Header("골드 코인 파티클")] public PoolObject goldCoinParticle;
    [Header("실버 코인 파티클을 표시할 코인 최소 범위")] public int minSilverRange;
    [Header("골드 코인 파티클을 표시할 코인 최소 범위")] public int minGoldRange;

    void Awake()
    {
        if(Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
            return;
        }
        Inst = this;
    }

    private void OnDestroy()
    {
        Inst = null;
    }

    /// <summary>
    /// 코인 파티클을 생성한다.
    /// </summary>
    /// <param name="refResult"></param>
    public void Create(ref RewardResult refResult, Vector3 createPosition, Vector3 createScale)
    {
        CoinParticle particle = null;
        var dropCoinCount = refResult.dict[RewardCurrencyType.Coin];

        if (dropCoinCount < minSilverRange)  // 브론즈 코인 파티클 생성
        {
            particle = MemoryPool.Inst.GetInstance<CoinParticle>(bronzeCoinParticle);
        }
        else if (minSilverRange <= dropCoinCount && dropCoinCount < minGoldRange) // 실버 코인 파티클 생성
        {
            particle = MemoryPool.Inst.GetInstance<CoinParticle>(silverCoinParticle);
        }
        else // 골드 코인 파티클 생성
        {
            particle = MemoryPool.Inst.GetInstance<CoinParticle>(goldCoinParticle);
        }
        if (particle)
        {
            particle.transform.position = createPosition;
            particle.transform.localScale = createScale;
        }
    }
}
