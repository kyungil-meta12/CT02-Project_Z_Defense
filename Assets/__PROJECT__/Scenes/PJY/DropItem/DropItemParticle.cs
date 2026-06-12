using UnityEngine;

public class DropItemParticle : PoolObject
{
    private ParticleSystem particle;

    void Awake()
    {
        particle = GetComponent<ParticleSystem>();
    }

    /// <summary>
    /// 파티클이 스폰되면 리셋 후 재생한다
    /// </summary>
    public override void OnSpawn()
    {
        particle.Simulate(0f, true);
        particle.Play();
    }

    public override void OnDespawn()
    {
        particle.Stop();
    }
}
