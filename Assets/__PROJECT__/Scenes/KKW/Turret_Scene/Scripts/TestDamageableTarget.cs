using UnityEngine;

[DisallowMultipleComponent]
public class TestDamageableTarget : MonoBehaviour, IDamageable
{
    [SerializeField] private float totalHp = 100.0f;
    [SerializeField] private bool resetHpOnEnable = true;
    [SerializeField] private bool logDamage = true;
    [SerializeField] private bool deactivateOnDeath;

    public float TotalHp
    {
        get
        {
            return totalHp;
        }
    }

    public float CurrHp { get; private set; }

    public bool IsAlive { get; private set; }

    private void OnEnable()
    {
        if (resetHpOnEnable)
        {
            CurrHp = TotalHp;
            IsAlive = CurrHp > 0.0f;
        }
    }

    private void OnValidate()
    {
        totalHp = Mathf.Max(0.0f, totalHp);
    }

    public void TakeDamage(float damage)
    {
        if (CurrHp <= 0.0f)
        {
            return;
        }

        float appliedDamage = Mathf.Max(0.0f, damage);
        CurrHp = Mathf.Clamp(CurrHp - appliedDamage, 0.0f, TotalHp);

        if (logDamage)
        {
            Debug.Log($"[TestDamageableTarget] Damage:{appliedDamage:0.###}, HP:{CurrHp:0.###}/{TotalHp:0.###}", this);
        }

        if (CurrHp <= 0.0f)
        {
            IsAlive = false;
            if (deactivateOnDeath)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
