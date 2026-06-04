using UnityEngine;

public class BossZombieController : PoolObject, IDamageable
{
    public float TotalHp { get; set; }
    public float CurrHp { get; set; }
    public bool IsAlive{ get; set; }
    public void TakeDamage(float damage)
    {
        throw new System.NotImplementedException();
    }
}
