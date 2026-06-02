using UnityEngine;

[CreateAssetMenu(menuName = "Project Z Defense/Turret Stat Profile")]
public class TurretStatProfileSO : ScriptableObject
{
    [Header("Combat")]
    public float damage = 1.0f;
    public float range = 10.0f;
    public float fireInterval = 0.5f;

    [Header("Projectile")]
    public float projectileSpeed = 20.0f;
    public int projectileCount = 1;
    public int pierceCount = 0;
}
