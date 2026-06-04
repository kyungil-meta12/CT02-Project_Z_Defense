using UnityEngine;

[CreateAssetMenu(menuName = "Project Z Defense/Turret Definition")]
public class TurretDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string turretId;
    public string displayName;

    [Header("Base")]
    public GameObject basePrefab;
    public TurretStatProfileSO baseStatProfile;

    [Header("Progression")]
    public int maxLevel;
    public TurretStatGrowthProfileSO statGrowthProfile;
    public TurretVFXProgressionSO vfxProgressionProfile;
    public TurretProjectileScaleProgressionSO projectileScaleProgressionProfile;
    public TurretEvolutionProgressionSO evolutionProgressionProfile;

    private void OnValidate()
    {
        maxLevel = Mathf.Max(0, maxLevel);
    }
}
