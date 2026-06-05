public interface IDamageable
{
    float TotalHp { get; }
    float CurrHp { get; }
    bool IsAlive { get; }
    void TakeDamage(float damage);
}
