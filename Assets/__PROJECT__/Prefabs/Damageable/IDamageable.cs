using UnityEngine;

public interface IDamageable
{
    float TotalHp{ get; set; }
    float CurrHp { get; set; }
    void TakeDamage(float damage);
}