using UnityEngine;

[CreateAssetMenu(fileName = "HelicopterMissileSkillDefinition", menuName = "Project Z Defense/Skills/Helicopter Missile Skill")]
public class HelicopterMissileSkillDefinitionSO : ScriptableObject
{
    [Header("기본 정보")]
    [SerializeField] private string skillId = "helicopter_missile";
    [SerializeField] private string displayName = "Helicopter Missile";
    [SerializeField] private Sprite icon;

    [Header("레벨 데이터")]
    [SerializeField] private HelicopterMissileSkillLevelData[] levelData =
    {
        new HelicopterMissileSkillLevelData()
    };

    [Header("프리팹")]
    [SerializeField] private GameObject rangePreviewPrefab;
    [SerializeField] private GameObject helicopterPrefab;
    [SerializeField] private GameObject missilePrefab;
    [SerializeField] private GameObject missileSmokePrefab;
    [SerializeField] private GameObject explosionEffectPrefab;

    [Header("쿨타임")]
    [Min(0f)] [SerializeField] private float cooldown = 20f;

    [Header("범위 지정")]
    [SerializeField] private LayerMask placementLayerMask = ~0;
    [SerializeField] private float fallbackGroundY = 0f;
    [SerializeField] private float previewHeightOffset = 0.05f;
    [SerializeField] private bool alignAreaToCameraForward = true;
    [SerializeField] private Vector3 fixedAreaRotationEuler;
    [SerializeField] private Vector3 previewLocalScaleMultiplier = Vector3.one;

    [Header("헬리콥터 이동")]
    [SerializeField] private Vector2 helicopterStartViewport = new Vector2(0.5f, -0.15f);
    [SerializeField] private Vector2 helicopterEndViewport = new Vector2(0.5f, 1.15f);
    [Min(0f)] [SerializeField] private float helicopterAltitude = 18f;
    [Min(0.1f)] [SerializeField] private float helicopterSpeed = 20f;
    [SerializeField] private Vector3 helicopterRotationOffsetEuler;
    [SerializeField] private Vector3 missileSpawnLocalOffset = new Vector3(0f, -0.2f, 0.8f);

    [Header("프로펠러")]
    [SerializeField] private bool autoAddPropellerAnimator = true;
    [Min(0f)] [SerializeField] private float propellerRotationSpeed = 1440f;
    [SerializeField] private Vector3 propellerLocalRotationAxis = Vector3.up;
    [SerializeField] private HelicopterPropellerPrefabBinding[] propellerBindings;
    [SerializeField] private HelicopterPropellerSearchRule[] propellerNameKeywords =
    {
        new HelicopterPropellerSearchRule("propeller", Vector3.up, 1f),
        new HelicopterPropellerSearchRule("rotor", Vector3.up, 1f),
        new HelicopterPropellerSearchRule("blade", Vector3.up, 1f)
    };

    [Header("미사일")]
    [Min(1)] [SerializeField] private int missileCount = 6;
    [Min(0f)] [SerializeField] private float missileInterval = 0.15f;
    [Min(0.1f)] [SerializeField] private float missileSpeed = 35f;
    [SerializeField] private float missileTargetHeightOffset = 0.1f;
    [Min(0f)] [SerializeField] private float explosionEffectDuration = 3f;
    [Min(0f)] [SerializeField] private float smokeDetachDuration = 1.5f;
    [SerializeField] private bool applyDamageOncePerCast = true;

    [Header("데미지 판정")]
    [SerializeField] private LayerMask damageLayerMask = ~0;
    [Min(1)] [SerializeField] private int damageBufferSize = 64;
    [Min(0.1f)] [SerializeField] private float damageBoxHeight = 12f;
    [SerializeField] private QueryTriggerInteraction damageTriggerInteraction = QueryTriggerInteraction.Collide;

    public string SkillId => skillId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public GameObject RangePreviewPrefab => rangePreviewPrefab;
    public GameObject HelicopterPrefab => helicopterPrefab;
    public GameObject MissilePrefab => missilePrefab;
    public GameObject MissileSmokePrefab => missileSmokePrefab;
    public GameObject ExplosionEffectPrefab => explosionEffectPrefab;
    public float Cooldown => cooldown;
    public LayerMask PlacementLayerMask => placementLayerMask;
    public float FallbackGroundY => fallbackGroundY;
    public float PreviewHeightOffset => previewHeightOffset;
    public bool AlignAreaToCameraForward => alignAreaToCameraForward;
    public Vector3 FixedAreaRotationEuler => fixedAreaRotationEuler;
    public Vector3 PreviewLocalScaleMultiplier => previewLocalScaleMultiplier;
    public Vector2 HelicopterStartViewport => helicopterStartViewport;
    public Vector2 HelicopterEndViewport => helicopterEndViewport;
    public float HelicopterAltitude => helicopterAltitude;
    public float HelicopterSpeed => helicopterSpeed;
    public Vector3 HelicopterRotationOffsetEuler => helicopterRotationOffsetEuler;
    public Vector3 MissileSpawnLocalOffset => missileSpawnLocalOffset;
    public bool AutoAddPropellerAnimator => autoAddPropellerAnimator;
    public float PropellerRotationSpeed => propellerRotationSpeed;
    public Vector3 PropellerLocalRotationAxis => propellerLocalRotationAxis;
    public HelicopterPropellerPrefabBinding[] PropellerBindings => propellerBindings;
    public HelicopterPropellerSearchRule[] PropellerNameKeywords => propellerNameKeywords;
    public int MissileCount => missileCount;
    public float MissileInterval => missileInterval;
    public float MissileSpeed => missileSpeed;
    public float MissileTargetHeightOffset => missileTargetHeightOffset;
    public float ExplosionEffectDuration => explosionEffectDuration;
    public float SmokeDetachDuration => smokeDetachDuration;
    public bool ApplyDamageOncePerCast => applyDamageOncePerCast;
    public LayerMask DamageLayerMask => damageLayerMask;
    public int DamageBufferSize => damageBufferSize;
    public float DamageBoxHeight => damageBoxHeight;
    public QueryTriggerInteraction DamageTriggerInteraction => damageTriggerInteraction;

    // 요청 레벨에 사용할 스킬 수치를 반환한다.
    public HelicopterMissileSkillLevelData GetLevelData(int level)
    {
        if (levelData == null || levelData.Length == 0)
        {
            return HelicopterMissileSkillLevelData.Default;
        }

        HelicopterMissileSkillLevelData bestData = levelData[0];
        int bestLevel = int.MinValue;

        for (int i = 0; i < levelData.Length; i++)
        {
            HelicopterMissileSkillLevelData currentData = levelData[i];
            if (currentData == null)
            {
                continue;
            }

            if (currentData.Level <= level && currentData.Level >= bestLevel)
            {
                bestData = currentData;
                bestLevel = currentData.Level;
            }
        }

        return bestData != null ? bestData : HelicopterMissileSkillLevelData.Default;
    }
}

[System.Serializable]
public class HelicopterPropellerPrefabBinding
{
    [Tooltip("헬리콥터 프리팹 루트 기준 자식 경로입니다. 예: Body/MainRotor")]
    [SerializeField] private string propellerPath;
    [Tooltip("프리팹 에셋의 자식 Transform을 직접 참조할 수 있을 때만 사용합니다. 씬 Hierarchy 오브젝트는 SO에 저장되지 않습니다.")]
    [SerializeField] private Transform propellerPrefabTransform;
    [SerializeField] private Vector3 localRotationAxis = Vector3.up;
    [SerializeField] private float rotationSpeedMultiplier = 1f;

    public string PropellerPath => propellerPath;
    public Transform PropellerPrefabTransform => propellerPrefabTransform;
    public Vector3 LocalRotationAxis => localRotationAxis;
    public float RotationSpeedMultiplier => rotationSpeedMultiplier;
}

[System.Serializable]
public class HelicopterPropellerSearchRule
{
    [SerializeField] private string nameKeyword = "propeller";
    [SerializeField] private Vector3 localRotationAxis = Vector3.up;
    [SerializeField] private float rotationSpeedMultiplier = 1f;

    public string NameKeyword => nameKeyword;
    public Vector3 LocalRotationAxis => localRotationAxis;
    public float RotationSpeedMultiplier => rotationSpeedMultiplier;

    // Unity 직렬화를 위한 기본 생성자다.
    public HelicopterPropellerSearchRule()
    {
    }

    // 프로펠러 자동 검색 규칙을 생성한다.
    public HelicopterPropellerSearchRule(string nameKeyword_, Vector3 localRotationAxis_, float rotationSpeedMultiplier_)
    {
        nameKeyword = nameKeyword_;
        localRotationAxis = localRotationAxis_;
        rotationSpeedMultiplier = rotationSpeedMultiplier_;
    }
}

[System.Serializable]
public class HelicopterMissileSkillLevelData
{
    private static readonly HelicopterMissileSkillLevelData defaultData = new HelicopterMissileSkillLevelData();

    [Min(1)] [SerializeField] private int level = 1;
    [Min(0f)] [SerializeField] private float damage = 100f;
    [Min(0.1f)] [SerializeField] private float areaLength = 18f;
    [Min(0.1f)] [SerializeField] private float areaWidth = 5f;

    public static HelicopterMissileSkillLevelData Default => defaultData;
    public int Level => level;
    public float Damage => damage;
    public float AreaLength => areaLength;
    public float AreaWidth => areaWidth;
}
