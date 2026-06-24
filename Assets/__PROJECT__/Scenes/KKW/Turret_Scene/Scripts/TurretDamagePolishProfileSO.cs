using UnityEngine;

/// <summary>
/// 터렛의 최종 피해량에 랜덤 편차, 치명타, 강타 같은 표시용 전투 변동성을 적용하는 설정 에셋.
/// </summary>
[CreateAssetMenu(fileName = "TurretDamagePolishProfile", menuName = "Project Z Defense/Turret Damage Polish Profile")]
public class TurretDamagePolishProfileSO : ScriptableObject
{
    [Header("데미지 편차")]
    [SerializeField] private bool useDamageVariance = true;
    [SerializeField, Min(0.0f)] private float minDamageMultiplier = 0.9f;
    [SerializeField, Min(0.0f)] private float maxDamageMultiplier = 1.1f;

    [Header("치명타")]
    [SerializeField, Range(0.0f, 1.0f)] private float criticalChance = 0.075f;
    [SerializeField, Min(0.0f)] private float criticalMultiplier = 1.75f;

    [Header("강타")]
    [SerializeField, Range(0.0f, 1.0f)] private float heavyHitChance;
    [SerializeField, Min(0.0f)] private float heavyHitMultiplier = 2.5f;

    public bool UseDamageVariance
    {
        get
        {
            return useDamageVariance;
        }
    }

    public float MinDamageMultiplier
    {
        get
        {
            return Mathf.Max(0.0f, minDamageMultiplier);
        }
    }

    public float MaxDamageMultiplier
    {
        get
        {
            return Mathf.Max(MinDamageMultiplier, maxDamageMultiplier);
        }
    }

    public float CriticalChance
    {
        get
        {
            return Mathf.Clamp01(criticalChance);
        }
    }

    public float CriticalMultiplier
    {
        get
        {
            return Mathf.Max(0.0f, criticalMultiplier);
        }
    }

    public float HeavyHitChance
    {
        get
        {
            return Mathf.Clamp01(heavyHitChance);
        }
    }

    public float HeavyHitMultiplier
    {
        get
        {
            return Mathf.Max(0.0f, heavyHitMultiplier);
        }
    }

    // 기본 피해량에 폴리싱 규칙을 적용해 실제 피해량과 표시 타입을 반환한다
    public TurretDamagePolishResult RollDamage(float baseDamage)
    {
        float damage = Mathf.Max(0.0f, baseDamage);
        DamagePopupType popupType = RollSpecialType();

        if (useDamageVariance)
        {
            damage *= Random.Range(MinDamageMultiplier, MaxDamageMultiplier);
        }

        if (popupType == DamagePopupType.Heavy)
        {
            damage *= HeavyHitMultiplier;
        }
        else if (popupType == DamagePopupType.Critical)
        {
            damage *= CriticalMultiplier;
        }

        return new TurretDamagePolishResult(damage, popupType);
    }

    // 강타와 치명타 중 이번 피해의 특수 타입을 확률로 결정한다
    private DamagePopupType RollSpecialType()
    {
        if (heavyHitChance > 0.0f && Random.value < HeavyHitChance)
        {
            return DamagePopupType.Heavy;
        }

        if (criticalChance > 0.0f && Random.value < CriticalChance)
        {
            return DamagePopupType.Critical;
        }

        return DamagePopupType.Normal;
    }

    // 인스펙터 입력값을 유효한 데미지 폴리싱 범위로 보정한다
    private void OnValidate()
    {
        minDamageMultiplier = Mathf.Max(0.0f, minDamageMultiplier);
        maxDamageMultiplier = Mathf.Max(minDamageMultiplier, maxDamageMultiplier);
        criticalChance = Mathf.Clamp01(criticalChance);
        criticalMultiplier = Mathf.Max(0.0f, criticalMultiplier);
        heavyHitChance = Mathf.Clamp01(heavyHitChance);
        heavyHitMultiplier = Mathf.Max(0.0f, heavyHitMultiplier);
    }
}
