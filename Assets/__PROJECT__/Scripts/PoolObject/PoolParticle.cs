using UnityEngine;

/// <summary>
/// 씬에 임시로 나타났다가 스스로 사라지는 파티클에 사용하는 풀링 스크립트
/// </summary>
public class PoolParticle : PoolObject
{
    private ParticleSystem particle;

    void Awake()
    {
        particle = GetComponent<ParticleSystem>();
    }

    public override void OnSpawn()
    {
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particle.Play();
    }

    void Update()
    {
        if(!particle.IsAlive())
        {
            ReturnInstance();
        }
    }
}
