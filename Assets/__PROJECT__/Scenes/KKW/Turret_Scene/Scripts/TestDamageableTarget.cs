using UnityEngine;

/// <summary>
/// 터렛 테스트 씬에서 데미지 적용 여부를 확인하는 간단한 피격 대상.
/// </summary>
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

    // 활성화 시 테스트 대상 체력을 초기화한다
    private void OnEnable()
    {
        if (resetHpOnEnable)
        {
            CurrHp = TotalHp;
            IsAlive = CurrHp > 0.0f;
        }
    }

    // 인스펙터 입력 체력을 유효 범위로 보정한다
    private void OnValidate()
    {
        totalHp = Mathf.Max(0.0f, totalHp);
    }

    // 외부 공격으로 받은 데미지를 테스트 대상 체력에 반영한다
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (CurrHp <= 0.0f)
        {
            return;
        }

        float beforeHp = CurrHp;
        float requestedDamage = Mathf.Max(0.0f, damageInfo.Damage);
        CurrHp = Mathf.Clamp(CurrHp - requestedDamage, 0.0f, TotalHp);
        float appliedDamage = Mathf.Max(0.0f, beforeHp - CurrHp);
        TurretDamageMeterManager.ReportDamage(damageInfo.DamageSource, appliedDamage);

        if (logDamage)
        {
            Debug.Log($"[TestDamageableTarget] 데미지:{appliedDamage:0.###}, 체력:{CurrHp:0.###}/{TotalHp:0.###}", this);
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
