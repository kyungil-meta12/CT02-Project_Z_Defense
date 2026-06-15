using UnityEngine;

public enum TurretAttackVfxType
{
    Projectile,
    Beam
}

/// <summary>
/// 터렛 공격 연출에 사용할 projectile, beam, muzzle VFX 프리팹 참조를 관리한다.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Turret VFX Profile")]
public class TurretVFXProfileSO : ScriptableObject
{
    [Header("Display")]
    public string displayName;

    [Header("Attack Type")]
    public TurretAttackVfxType attackVfxType = TurretAttackVfxType.Projectile;

    [Header("Projectile")]
    public GameObject projectilePrefab;

    [Header("Beam")]
    public GameObject beamPrefab;
    public BeamAttackProfileSO beamAttackProfile;

    [Header("Muzzle")]
    public GameObject muzzleVFX;
    public float muzzleVFXDuration = 2.0f;
}
