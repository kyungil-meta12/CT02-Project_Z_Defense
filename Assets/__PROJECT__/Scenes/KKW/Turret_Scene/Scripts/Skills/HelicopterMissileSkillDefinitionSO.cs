using UnityEngine.Serialization;
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
    [Tooltip("날아가는 미사일 비주얼 프리팹입니다. FX_Missile_01처럼 연기까지 포함된 프리팹 하나를 넣는 것을 권장합니다.")]
    [SerializeField] private GameObject missilePrefab;
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

    [Header("미사일")]
    [Min(1)] [SerializeField] private int missileCount = 6;
    [Min(0f)] [SerializeField] private float missileInterval = 0.15f;
    [SerializeField] private HelicopterMissileSpreadMode missileSpreadMode = HelicopterMissileSpreadMode.Zigzag;
    [Tooltip("표시 범위 길이 안에서 미사일 착탄점을 흩뿌릴 비율입니다. 1이면 전체 길이를 사용합니다.")]
    [Range(0.1f, 1f)] [SerializeField] private float missileSpreadLengthRatio = 0.9f;
    [Tooltip("표시 범위 폭 안에서 미사일 착탄점을 흩뿌릴 비율입니다. 1이면 전체 폭을 사용합니다.")]
    [Range(0.1f, 1f)] [SerializeField] private float missileSpreadWidthRatio = 0.7f;
    [Tooltip("FX_Missile_01 루트 오브젝트를 목표 지점 쪽으로 이동시키는 속도입니다.")]
    [FormerlySerializedAs("missileSpeed")] [Min(0.1f)] [SerializeField] private float missileRootMoveSpeed = 35f;
    [SerializeField] private float missileTargetHeightOffset = 0.1f;
    [Tooltip("미사일 생성 직후 발생하는 파티클 충돌을 무시할 최소 시간입니다.")]
    [Min(0f)] [SerializeField] private float missileImpactArmDelay = 0.2f;
    [Tooltip("미사일 생성 직후 발생하는 파티클 충돌을 무시할 최소 이동 거리입니다.")]
    [Min(0f)] [SerializeField] private float missileImpactMinTravelDistance = 2f;
    [Min(0f)] [SerializeField] private float explosionEffectDuration = 3f;
    [Tooltip("충돌 후 FX_Missile_01 루트 오브젝트를 제거하기 전까지 기다릴 시간입니다.")]
    [FormerlySerializedAs("smokeDetachDuration")] [Min(0f)] [SerializeField] private float missileDestroyDelayAfterImpact = 1.5f;
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
    public int MissileCount => missileCount;
    public float MissileInterval => missileInterval;
    public HelicopterMissileSpreadMode MissileSpreadMode => missileSpreadMode;
    public float MissileSpreadLengthRatio => missileSpreadLengthRatio;
    public float MissileSpreadWidthRatio => missileSpreadWidthRatio;
    public float MissileRootMoveSpeed => missileRootMoveSpeed;
    public float MissileTargetHeightOffset => missileTargetHeightOffset;
    public float MissileImpactArmDelay => missileImpactArmDelay;
    public float MissileImpactMinTravelDistance => missileImpactMinTravelDistance;
    public float ExplosionEffectDuration => explosionEffectDuration;
    public float MissileDestroyDelayAfterImpact => missileDestroyDelayAfterImpact;
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

public enum HelicopterMissileSpreadMode
{
    Random,
    EvenLine,
    Zigzag,
    Grid
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
