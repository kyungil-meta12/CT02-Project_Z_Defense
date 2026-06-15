using UnityEngine;

public class CoinParticle : PoolObject
{
    ParticleSystem particle;

    void Awake()
    {
        particle = GetComponent<ParticleSystem>();
    }

    public override void OnSpawn()
    {
        // 파티클 초기화 후 재생
        particle.Simulate(0f, true);
        particle.Play();
    }

    void Update()
    {
        // 파티클이 모두 재생되면 풀로 반환
        if(!particle.IsAlive())
        {
            ReturnInstance();
        }
    }
}
