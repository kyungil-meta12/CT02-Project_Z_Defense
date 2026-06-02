using UnityEngine;

[CreateAssetMenu(menuName = "Project Z Defense/Turret Stat Growth Profile")]
public class TurretStatGrowthProfileSO : ScriptableObject
{
    [Header("Per Level")]
    public float damagePercentPerLevel = 1.0f;
    public float rangePerLevel = 0.0f;
    public float fireIntervalReductionPerLevel = 0.0f;

    [Header("Interval Growth")]
    public int projectileSpeedIntervalLevel = 50;
    public float projectileSpeedPerInterval = 1.0f;
    public int projectileCountIntervalLevel = 0;
    public int pierceCountIntervalLevel = 0;

    [Header("Limits")]
    public float maxRange = 200.0f;
    public float minFireInterval = 0.05f;
    public float maxProjectileSpeed = 200.0f;
    public int maxProjectileCount = 20;
    public int maxPierceCount = 20;

    private void OnValidate()
    {
        damagePercentPerLevel = Mathf.Max(0.0f, damagePercentPerLevel);
        fireIntervalReductionPerLevel = Mathf.Max(0.0f, fireIntervalReductionPerLevel);
        projectileSpeedIntervalLevel = Mathf.Max(0, projectileSpeedIntervalLevel);
        projectileSpeedPerInterval = Mathf.Max(0.0f, projectileSpeedPerInterval);
        projectileCountIntervalLevel = Mathf.Max(0, projectileCountIntervalLevel);
        pierceCountIntervalLevel = Mathf.Max(0, pierceCountIntervalLevel);
        maxRange = Mathf.Max(0.0f, maxRange);
        minFireInterval = Mathf.Max(0.01f, minFireInterval);
        maxProjectileSpeed = Mathf.Max(0.0f, maxProjectileSpeed);
        maxProjectileCount = Mathf.Max(1, maxProjectileCount);
        maxPierceCount = Mathf.Max(0, maxPierceCount);
    }
}
