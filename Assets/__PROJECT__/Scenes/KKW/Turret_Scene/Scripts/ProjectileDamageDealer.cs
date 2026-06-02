using System.Collections.Generic;
using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;

[DisallowMultipleComponent]
public class ProjectileDamageDealer : MonoBehaviour
{
    [SerializeField] private float damage = 1.0f;
    [SerializeField] private int pierceCount = 0;
    [SerializeField] private bool logDamage;

    private readonly List<IDamageable> hitDamageables = new List<IDamageable>(4);

    public void Init(float damage_, int pierceCount_)
    {
        Init(damage_, pierceCount_, false);
    }

    public void Init(float damage_, int pierceCount_, bool logDamage_)
    {
        damage = Mathf.Max(0.0f, damage_);
        pierceCount = Mathf.Max(0, pierceCount_);
        logDamage = logDamage_;
        hitDamageables.Clear();
        enabled = true;

        DamageManager damageManager = GetComponent<DamageManager>();
        if (damageManager != null)
        {
            damageManager.SetDamage(damage);
        }

        Projectile projectile = GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.damage = damage;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null || hitDamageables.Contains(damageable))
        {
            return;
        }

        damageable.TakeDamage(damage);
        hitDamageables.Add(damageable);

        if (logDamage)
        {
            Debug.Log($"[ProjectileDamageDealer] Damage:{damage:0.###}, TargetHp:{damageable.CurrHp:0.###}/{damageable.TotalHp:0.###}", this);
        }

        if (hitDamageables.Count > pierceCount)
        {
            enabled = false;
            PooledProjectileReturner.ReturnOrDestroy(gameObject);
        }
    }
}
