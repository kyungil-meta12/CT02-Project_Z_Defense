using UnityEngine;

[CreateAssetMenu(menuName = "Project Z Defense/Turret Stat Profile")]
public class TurretStatProfileSO : ScriptableObject
{
    public static event System.Action<TurretStatProfileSO> ProfileChanged;

    [Header("Combat")]
    public float damage = 1.0f;
    public float range = 10.0f;
    public float fireInterval = 0.5f;

    [Header("Projectile")]
    public float projectileSpeed = 20.0f;
    public int projectileCount = 1;
    public int pierceCount = 0;

    private void OnValidate()
    {
        damage = Mathf.Max(0.0f, damage);
        range = Mathf.Max(0.0f, range);
        fireInterval = Mathf.Max(0.01f, fireInterval);
        projectileSpeed = Mathf.Max(0.0f, projectileSpeed);
        projectileCount = Mathf.Max(1, projectileCount);
        pierceCount = Mathf.Max(0, pierceCount);

        ProfileChanged?.Invoke(this);
    }
}
