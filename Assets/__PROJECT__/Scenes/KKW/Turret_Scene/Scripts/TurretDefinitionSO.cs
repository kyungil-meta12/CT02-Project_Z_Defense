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
    // 진화 레벨은 EvolutionProgressionSO.requiredLevel에서 관리하고, 이 값은 더 이상 진화하지 않는 최종 터렛의 레벨 상한에만 사용합니다.
    [Tooltip("0이면 이 Definition 자체의 최대 레벨 제한은 없습니다. 진화 대기 레벨은 Evolution Progression SO의 requiredLevel에서 관리하고, maxLevel은 최종 터렛처럼 더 이상 진화하지 않는 터렛의 하드 캡에만 사용합니다.")]
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
