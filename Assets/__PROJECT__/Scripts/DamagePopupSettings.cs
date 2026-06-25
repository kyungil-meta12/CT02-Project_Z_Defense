using DamageNumbersPro;
using UnityEngine;

/// <summary>
/// 데미지 팝업 정책, 위치, 타입별 스타일, DNP 렌더링 값을 정의하는 설정 에셋.
/// </summary>
[CreateAssetMenu(fileName = "DamagePopupSettings", menuName = "__PROJECT__/UI/Damage Popup Settings")]
public class DamagePopupSettings : ScriptableObject
{
    public const float DEFAULT_HEIGHT_OFFSET = 2.2f;
    public const float DEFAULT_STACK_WINDOW = 0.12f;
    public const int DEFAULT_MAX_STACK_SLOTS = 5;
    public const float DEFAULT_STACK_HORIZONTAL_STEP = 0.35f;
    public const float DEFAULT_STACK_VERTICAL_STEP = 0.18f;
    public const float DEFAULT_ACCUMULATION_WINDOW = 0.12f;
    public const int DEFAULT_MAX_POPUPS_PER_SECOND = 45;
    public const float DEFAULT_DNP_SCALE = 0.35f;

    [Header("공통 표시 정책")]
    [Tooltip("데미지 소스별 팝업 표시 정책 프로필. 비워두면 코드 기본 정책을 사용한다.")]
    [SerializeField] private DamagePopupPolicyProfileSO popupPolicyProfile;
    [Tooltip("데미지 숫자를 반올림한 정수로 표시한다. 현재 데미지 적용 경로는 정수 표시를 사용한다.")]
    [SerializeField] private bool showRoundedDamage = true;
    [Tooltip("데미지 값이 0 이하일 때도 팝업을 표시한다.")]
    [SerializeField] private bool showZeroOrNegativeDamage;

    [Header("공통 생성 위치")]
    [Tooltip("기본 대상 위치 기준 위쪽 생성 높이")]
    [SerializeField] private float heightOffset = DEFAULT_HEIGHT_OFFSET;
    [Tooltip("기본 대상 위치 기준 추가 생성 오프셋")]
    [SerializeField] private Vector3 spawnOffset;
    [Tooltip("일반좀비 위치 기준 위쪽 생성 높이")]
    [SerializeField] private float normalZombieHeightOffset = DEFAULT_HEIGHT_OFFSET;
    [Tooltip("일반좀비 위치 기준 추가 생성 오프셋")]
    [SerializeField] private Vector3 normalZombieSpawnOffset;
    [Tooltip("보스좀비 위치 기준 위쪽 생성 높이")]
    [SerializeField] private float bossZombieHeightOffset = 4.0f;
    [Tooltip("보스좀비 위치 기준 추가 생성 오프셋")]
    [SerializeField] private Vector3 bossZombieSpawnOffset;
    [Tooltip("생성 위치에 추가할 수평 랜덤 반경")]
    [SerializeField, Min(0f)] private float randomHorizontalRadius = 0.15f;
    [Tooltip("생성 위치에 추가할 수직 랜덤 범위")]
    [SerializeField, Min(0f)] private float randomVerticalRange = 0.05f;

    [Header("공통 연속 피격 분산")]
    [Tooltip("같은 대상에게 짧은 시간 안에 뜨는 팝업을 계단식으로 분산한다.")]
    [SerializeField] private bool useStackedSpawnOffset = true;
    [Tooltip("같은 대상의 연속 피격으로 판단할 시간")]
    [SerializeField, Min(0.01f)] private float stackWindow = DEFAULT_STACK_WINDOW;
    [Tooltip("연속 피격 분산에 사용할 최대 슬롯 수")]
    [SerializeField, Min(1)] private int maxStackSlots = DEFAULT_MAX_STACK_SLOTS;
    [Tooltip("연속 팝업 슬롯마다 카메라 오른쪽 방향으로 벌어지는 거리")]
    [SerializeField, Min(0f)] private float stackHorizontalStep = DEFAULT_STACK_HORIZONTAL_STEP;
    [Tooltip("연속 팝업 슬롯마다 위쪽으로 벌어지는 거리")]
    [SerializeField, Min(0f)] private float stackVerticalStep = DEFAULT_STACK_VERTICAL_STEP;

    [Header("공통 표시량 제어")]
    [Tooltip("누적 정책 데미지를 같은 대상 기준으로 합산해 표시한다.")]
    [SerializeField] private bool useAccumulatedDamagePopup = true;
    [Tooltip("같은 대상의 누적 정책 데미지를 하나의 팝업으로 합산할 시간")]
    [SerializeField, Min(0.01f)] private float accumulationWindow = DEFAULT_ACCUMULATION_WINDOW;
    [Tooltip("1초 동안 즉시 생성 가능한 데미지 팝업 최대 개수. 0이면 제한하지 않는다.")]
    [SerializeField, Min(0)] private int maxPopupsPerSecond = DEFAULT_MAX_POPUPS_PER_SECOND;

    [Header("런타임 계측")]
    [Tooltip("데미지 팝업 요청, 생성, 제한, 누적 상태를 주기적으로 로그로 출력한다.")]
    [SerializeField] private bool enableRuntimeStats;
    [Tooltip("런타임 계측 로그 출력 간격")]
    [SerializeField, Min(0.5f)] private float runtimeStatsLogInterval = 5.0f;

    [Header("공통 타입 스타일")]
    [Tooltip("데미지 숫자 색상")]
    [SerializeField] private Color damageColor = new Color(1f, 0.35f, 0.12f, 1f);
    [Tooltip("치명타 데미지 숫자 색상")]
    [SerializeField] private Color criticalDamageColor = new Color(1f, 0.9f, 0.1f, 1f);
    [Tooltip("강타 데미지 숫자 색상")]
    [SerializeField] private Color heavyDamageColor = new Color(1f, 0.15f, 0.05f, 1f);
    [Tooltip("치명타 팝업 크기 배율")]
    [SerializeField, Min(0f)] private float criticalScaleMultiplier = 1.35f;
    [Tooltip("강타 팝업 크기 배율")]
    [SerializeField, Min(0f)] private float heavyScaleMultiplier = 1.6f;

    [Header("DNP 렌더링")]
    [Tooltip("일반 데미지에 사용할 DamageNumbersPro Mesh 프리팹")]
    [SerializeField] private DamageNumberMesh dnpNormalPrefab;
    [Tooltip("치명타 데미지에 사용할 DamageNumbersPro Mesh 프리팹. 비워두면 일반 프리팹을 사용한다.")]
    [SerializeField] private DamageNumberMesh dnpCriticalPrefab;
    [Tooltip("강타 데미지에 사용할 DamageNumbersPro Mesh 프리팹. 비워두면 치명타 또는 일반 프리팹을 사용한다.")]
    [SerializeField] private DamageNumberMesh dnpHeavyPrefab;
    [Tooltip("DamageNumbersPro 팝업 생성 시 적용할 기본 크기 배율")]
    [SerializeField, Min(0.01f)] private float dnpScale = DEFAULT_DNP_SCALE;
    [Tooltip("DamageNumbersPro 팝업에 타입별 텍스트 접두사를 표시한다.")]
    [SerializeField] private bool dnpUseTypePrefix = true;
    [Tooltip("DamageNumbersPro 치명타 접두사")]
    [SerializeField] private string dnpCriticalPrefix = "CRIT ";
    [Tooltip("DamageNumbersPro 강타 접두사")]
    [SerializeField] private string dnpHeavyPrefix = "HEAVY ";

    public DamagePopupPolicyProfileSO PopupPolicyProfile => popupPolicyProfile;
    public float DnpScale => dnpScale;
    public bool DnpUseTypePrefix => dnpUseTypePrefix;
    public string DnpCriticalPrefix => dnpCriticalPrefix;
    public string DnpHeavyPrefix => dnpHeavyPrefix;
    public Color DamageColor => damageColor;
    public bool ShowRoundedDamage => showRoundedDamage;
    public bool ShowZeroOrNegativeDamage => showZeroOrNegativeDamage;
    public bool UseStackedSpawnOffset => useStackedSpawnOffset;
    public float StackWindow => stackWindow;
    public int MaxStackSlots => maxStackSlots;
    public float StackHorizontalStep => stackHorizontalStep;
    public float StackVerticalStep => stackVerticalStep;
    public bool UseAccumulatedDamagePopup => useAccumulatedDamagePopup;
    public float AccumulationWindow => accumulationWindow;
    public int MaxPopupsPerSecond => maxPopupsPerSecond;
    public bool EnableRuntimeStats => enableRuntimeStats;
    public float RuntimeStatsLogInterval => runtimeStatsLogInterval;
    public float CriticalScaleMultiplier => criticalScaleMultiplier;
    public float HeavyScaleMultiplier => heavyScaleMultiplier;

    // 데미지 타입에 맞는 DamageNumbersPro 프리팹을 반환한다
    public DamageNumberMesh GetDnpPrefab(DamagePopupType damageType)
    {
        switch (damageType)
        {
            case DamagePopupType.Heavy:
                if (dnpHeavyPrefab != null)
                {
                    return dnpHeavyPrefab;
                }

                if (dnpCriticalPrefab != null)
                {
                    return dnpCriticalPrefab;
                }

                return dnpNormalPrefab;
            case DamagePopupType.Critical:
                return dnpCriticalPrefab != null ? dnpCriticalPrefab : dnpNormalPrefab;
            default:
                return dnpNormalPrefab;
        }
    }

    // 데미지 타입에 맞는 DamageNumbersPro 접두사를 반환한다
    public string GetDnpPrefix(DamagePopupType damageType)
    {
        if (!dnpUseTypePrefix)
        {
            return string.Empty;
        }

        switch (damageType)
        {
            case DamagePopupType.Heavy:
                return dnpHeavyPrefix ?? string.Empty;
            case DamagePopupType.Critical:
                return dnpCriticalPrefix ?? string.Empty;
            default:
                return string.Empty;
        }
    }

    // 데미지 타입에 맞는 팝업 색상을 반환한다
    public Color GetDamageColor(DamagePopupType damageType)
    {
        switch (damageType)
        {
            case DamagePopupType.Heavy:
                return heavyDamageColor;
            case DamagePopupType.Critical:
                return criticalDamageColor;
            default:
                return damageColor;
        }
    }

    // 데미지 타입에 맞는 팝업 크기 배율을 반환한다
    public float GetScaleMultiplier(DamagePopupType damageType)
    {
        switch (damageType)
        {
            case DamagePopupType.Heavy:
                return Mathf.Max(0f, heavyScaleMultiplier);
            case DamagePopupType.Critical:
                return Mathf.Max(0f, criticalScaleMultiplier);
            default:
                return 1f;
        }
    }

    // 대상 위치 기준으로 데미지 팝업 생성 위치를 계산한다
    public Vector3 GetSpawnPosition(Vector3 targetPosition)
    {
        return GetSpawnPosition(targetPosition, DamagePopupTargetType.Default);
    }

    // 대상 종류별 위치 설정을 반영해 데미지 팝업 생성 위치를 계산한다
    public Vector3 GetSpawnPosition(Vector3 targetPosition, DamagePopupTargetType targetType)
    {
        ResolveSpawnOffset(targetType, out float resolvedHeightOffset, out Vector3 resolvedSpawnOffset);
        Vector3 position = targetPosition + (Vector3.up * resolvedHeightOffset) + resolvedSpawnOffset;
        if (randomHorizontalRadius > 0f)
        {
            Vector2 randomCircle = Random.insideUnitCircle * randomHorizontalRadius;
            position.x += randomCircle.x;
            position.z += randomCircle.y;
        }

        if (randomVerticalRange > 0f)
        {
            position.y += Random.Range(-randomVerticalRange, randomVerticalRange);
        }

        return position;
    }

    // 대상 종류에 맞는 높이와 추가 오프셋을 반환한다
    private void ResolveSpawnOffset(DamagePopupTargetType targetType, out float resolvedHeightOffset, out Vector3 resolvedSpawnOffset)
    {
        switch (targetType)
        {
            case DamagePopupTargetType.BossZombie:
                resolvedHeightOffset = bossZombieHeightOffset;
                resolvedSpawnOffset = bossZombieSpawnOffset;
                break;
            case DamagePopupTargetType.NormalZombie:
                resolvedHeightOffset = normalZombieHeightOffset;
                resolvedSpawnOffset = normalZombieSpawnOffset;
                break;
            default:
                resolvedHeightOffset = heightOffset;
                resolvedSpawnOffset = spawnOffset;
                break;
        }
    }

    // 인스펙터 입력값을 유효한 팝업 표시 범위로 보정한다
    private void OnValidate()
    {
        normalZombieHeightOffset = Mathf.Max(0f, normalZombieHeightOffset);
        bossZombieHeightOffset = Mathf.Max(0f, bossZombieHeightOffset);
        randomHorizontalRadius = Mathf.Max(0f, randomHorizontalRadius);
        randomVerticalRange = Mathf.Max(0f, randomVerticalRange);
        stackWindow = Mathf.Max(0.01f, stackWindow);
        maxStackSlots = Mathf.Max(1, maxStackSlots);
        stackHorizontalStep = Mathf.Max(0f, stackHorizontalStep);
        stackVerticalStep = Mathf.Max(0f, stackVerticalStep);
        accumulationWindow = Mathf.Max(0.01f, accumulationWindow);
        maxPopupsPerSecond = Mathf.Max(0, maxPopupsPerSecond);
        runtimeStatsLogInterval = Mathf.Max(0.5f, runtimeStatsLogInterval);
        dnpScale = Mathf.Max(0.01f, dnpScale);
        if (dnpCriticalPrefix == null)
        {
            dnpCriticalPrefix = string.Empty;
        }

        if (dnpHeavyPrefix == null)
        {
            dnpHeavyPrefix = string.Empty;
        }

        criticalScaleMultiplier = Mathf.Max(0f, criticalScaleMultiplier);
        heavyScaleMultiplier = Mathf.Max(0f, heavyScaleMultiplier);
    }

    // 런타임 기본 설정 인스턴스를 생성한다
    public static DamagePopupSettings CreateRuntimeDefault()
    {
        return CreateInstance<DamagePopupSettings>();
    }
}
