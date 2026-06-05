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

    public bool HasReachedPierceLimit
    {
        get
        {
            return hitDamageables.Count > pierceCount;
        }
    }

    public void Init(float damage_, int pierceCount_)
    {
        Init(damage_, pierceCount_, false);
    }

    public void Init(float damage_, int pierceCount_, bool logDamage_)
    {
        Init(damage_, pierceCount_, logDamage_, null);
    }

    public void Init(float damage_, int pierceCount_, bool logDamage_, GameObject target)
    {
        damage = Mathf.Max(0.0f, damage_);
        pierceCount = Mathf.Max(0, pierceCount_);
        logDamage = logDamage_;
        hitDamageables.Clear();
        enabled = true;

        ApplyLegacyProjectileDamageValues();
        InitHitDetector(target);
    }

    public bool TryApplyDamage(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
        if (damageable == null || !damageable.IsAlive || hitDamageables.Contains(damageable))
        {
            return false;
        }

        damageable.TakeDamage(damage);
        hitDamageables.Add(damageable);

        if (logDamage)
        {
            Debug.Log($"[ProjectileDamageDealer] Damage:{damage:0.###}, TargetHp:{damageable.CurrHp:0.###}/{damageable.TotalHp:0.###}", this);
        }

        return true;
    }

    private void ApplyLegacyProjectileDamageValues()
    {
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

    private void InitHitDetector(GameObject target)
    {
        ProjectileHitDetector hitDetector = GetComponent<ProjectileHitDetector>();
        if (hitDetector == null)
        {
            hitDetector = gameObject.AddComponent<ProjectileHitDetector>();
        }

        hitDetector.Init(this, target);
    }
}
