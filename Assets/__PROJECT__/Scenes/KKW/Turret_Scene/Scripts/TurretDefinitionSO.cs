using UnityEngine;

/// <summary>
/// 터렛의 정체성, 프리팹, 스탯, 성장, 비용, VFX, 상태이상, 진화 프로필을 연결하는 최상위 정의 에셋.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Turret Definition")]
public class TurretDefinitionSO : ScriptableObject
{
    [Header("정체성")]
    public string turretId;
    public string displayName;
    [TextArea(2, 5)]
    public string shortDescription;

    [Header("기본값")]
    public GameObject basePrefab;
    public TurretStatProfileSO baseStatProfile;

    [Header("성장")]
    // 진화 레벨은 EvolutionProgressionSO.requiredLevel에서 관리하고, 이 값은 더 이상 진화하지 않는 최종 터렛의 레벨 상한에만 사용합니다.
    [Tooltip("0이면 이 Definition 자체의 최대 레벨 제한은 없습니다. 진화 대기 레벨은 Evolution Progression SO의 requiredLevel에서 관리하고, maxLevel은 최종 터렛처럼 더 이상 진화하지 않는 터렛의 하드 캡에만 사용합니다.")]
    public int maxLevel;
    public TurretStatGrowthProfileSO statGrowthProfile;
    public TurretUpgradeCostProfileSO upgradeCostProfile;
    public TurretVFXProgressionSO vfxProgressionProfile;
    public TurretProjectileScaleProgressionSO projectileScaleProgressionProfile;
    public TurretEvolutionProgressionSO evolutionProgressionProfile;

    [Header("엔지니어")]
    [Tooltip("0이면 엔지니어가 탑승할 수 없습니다. 양수 값은 이 터렛에 동시에 탑승할 수 있는 최대 엔지니어 수입니다.")]
    public int maxEngineerSeatCount;
    [Header("데미지 표시")]
    public TurretDamagePolishProfileSO damagePolishProfile;

    [Header("상태이상")]
    public FrostStatusProfileSO frostStatusProfile;
    public PoisonStatusProfileSO poisonStatusProfile;
    public ElectroStatusProfileSO electroStatusProfile;
    public IgnitionStatusProfileSO ignitionStatusProfile;

    // 인스펙터 입력값을 유효한 터렛 정의 범위로 보정한다
    private void OnValidate()
    {
        maxLevel = Mathf.Max(0, maxLevel);
        maxEngineerSeatCount = Mathf.Max(0, maxEngineerSeatCount);
    }
}
