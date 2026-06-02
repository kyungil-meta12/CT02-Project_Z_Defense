using UnityEngine;

[CreateAssetMenu(menuName = "Project Z Defense/Turret VFX Profile")]
public class TurretVFXProfileSO : ScriptableObject
{
    [Header("Display")]
    public string displayName;

    [Header("Projectile")]
    public GameObject projectilePrefab;

    [Header("Muzzle")]
    public GameObject muzzleVFX;
    public float muzzleVFXDuration = 2.0f;

    [Header("Sound")]
    public AudioClip fireSound;
}
