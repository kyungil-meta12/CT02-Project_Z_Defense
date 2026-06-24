using UnityEngine;

/// <summary>
/// 좀비 처치 코인 보상량에 맞는 등급별 코인 파티클 생성을 담당한다.
/// </summary>
public class CoinParticleCreator : MonoBehaviour
{
    public static CoinParticleCreator Inst;

    [Header("브론즈 코인 파티클")] public PoolParticle bronzeCoinParticle;
    [Header("실버 코인 파티클")] public PoolParticle silverCoinParticle;
    [Header("골드 코인 파티클")] public PoolParticle goldCoinParticle;
    [Header("실버 코인 파티클을 표시할 코인 최소 범위")] public int minSilverRange;
    [Header("골드 코인 파티클을 표시할 코인 최소 범위")] public int minGoldRange;

    // 싱글톤 인스턴스를 등록하고 중복 인스턴스를 제거한다
    private void Awake()
    {
        if (Inst && Inst != this)
        {
            Destroy(gameObject);
            return;
        }
        Inst = this;
    }

    // 싱글톤 인스턴스가 자신일 때만 해제한다
    private void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }
    }

    /// <summary>
    /// 코인 파티클을 생성한다.
    /// </summary>
    /// <param name="inputResult"></param>
    // 코인 보상이 실제로 있을 때만 등급별 코인 파티클을 생성한다
    public void Create(RewardResult inputResult, Vector3 createPosition, Vector3 createScale)
    {
        if (inputResult == null || !inputResult.TryGetAmount(RewardCurrencyType.Coin, out int dropCoinCount) || dropCoinCount <= 0)
        {
            return;
        }

        if (MemoryPool.Inst == null)
        {
            return;
        }

        PoolParticle particle = null;
        if (dropCoinCount < minSilverRange)  // 브론즈 코인 파티클 생성
        {
            if (bronzeCoinParticle == null)
            {
                return;
            }

            particle = MemoryPool.Inst.GetInstance<PoolParticle>(bronzeCoinParticle);
        }
        else if (minSilverRange <= dropCoinCount && dropCoinCount < minGoldRange) // 실버 코인 파티클 생성
        {
            if (silverCoinParticle == null)
            {
                return;
            }

            particle = MemoryPool.Inst.GetInstance<PoolParticle>(silverCoinParticle);
        }
        else // 골드 코인 파티클 생성
        {
            if (goldCoinParticle == null)
            {
                return;
            }

            particle = MemoryPool.Inst.GetInstance<PoolParticle>(goldCoinParticle);
        }
        if (particle)
        {
            particle.transform.position = createPosition;
            particle.transform.localScale = createScale;
        }
    }
}
