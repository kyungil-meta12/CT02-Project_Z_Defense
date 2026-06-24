using TMPro;
using UnityEngine;

/// <summary>
/// 데미지 팝업 프리팹, 풀 크기, 타입별 표시 스타일을 정의하는 설정 에셋.
/// </summary>
[CreateAssetMenu(fileName = "DamagePopupSettings", menuName = "__PROJECT__/UI/Damage Popup Settings")]
public class DamagePopupSettings : ScriptableObject
{
    public const int DEFAULT_INITIAL_POOL_SIZE = 32;
    public const int DEFAULT_FONT_SIZE = 24;
    public const float DEFAULT_LIFETIME = 0.75f;
    public const float DEFAULT_HEIGHT_OFFSET = 2.2f;
    public const float DEFAULT_START_SCALE = 1.15f;
    public const float DEFAULT_END_SCALE = 0.85f;
    public const float DEFAULT_WORLD_CANVAS_SCALE = 0.04f;
    public const float DEFAULT_TEXT_RECT_WIDTH = 12f;
    public const float DEFAULT_TEXT_RECT_HEIGHT = 3f;
    public const float DEFAULT_TEXT_WIDTH_PER_CHARACTER = 1.2f;
    public const float DEFAULT_STACK_WINDOW = 0.12f;
    public const int DEFAULT_MAX_STACK_SLOTS = 5;
    public const float DEFAULT_STACK_HORIZONTAL_STEP = 0.35f;
    public const float DEFAULT_STACK_VERTICAL_STEP = 0.18f;

    [Header("풀")]
    [Tooltip("MemoryPool에서 사용할 데미지 팝업 프리팹")]
    [SerializeField] private DamagePopup damagePopupPrefab;
    [Tooltip("초기 생성해 둘 데미지 팝업 개수")]
    [SerializeField, Min(1)] private int initialPoolSize = DEFAULT_INITIAL_POOL_SIZE;

    [Header("텍스트")]
    [Tooltip("데미지 숫자 폰트 크기")]
    [SerializeField, Min(1)] private int fontSize = DEFAULT_FONT_SIZE;
    [Tooltip("월드 캔버스 자식 오브젝트의 기본 스케일. TextMeshProUGUI가 너무 크게 보이면 이 값을 낮춘다.")]
    [SerializeField, Min(0.001f)] private float worldCanvasScale = DEFAULT_WORLD_CANVAS_SCALE;
    [Tooltip("데미지 숫자 TMP UGUI RectTransform 폭. 숫자가 줄바꿈되면 이 값을 키운다.")]
    [SerializeField, Min(0.1f)] private float textRectWidth = DEFAULT_TEXT_RECT_WIDTH;
    [Tooltip("데미지 숫자 TMP UGUI RectTransform 높이")]
    [SerializeField, Min(0.1f)] private float textRectHeight = DEFAULT_TEXT_RECT_HEIGHT;
    [Tooltip("긴 데미지 숫자 표시 시 글자 수당 추가로 확보할 RectTransform 폭")]
    [SerializeField, Min(0f)] private float textWidthPerCharacter = DEFAULT_TEXT_WIDTH_PER_CHARACTER;
    [Tooltip("데미지 숫자에 사용할 TMP 폰트 에셋. 비워두면 TMP 기본 폰트를 사용한다.")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [Tooltip("데미지 숫자 색상")]
    [SerializeField] private Color damageColor = new Color(1f, 0.35f, 0.12f, 1f);
    [Tooltip("치명타 데미지 숫자 색상")]
    [SerializeField] private Color criticalDamageColor = new Color(1f, 0.9f, 0.1f, 1f);
    [Tooltip("강타 데미지 숫자 색상")]
    [SerializeField] private Color heavyDamageColor = new Color(1f, 0.15f, 0.05f, 1f);
    [Tooltip("일반 데미지 텍스트 포맷. {0} 위치에 데미지 숫자가 들어간다.")]
    [SerializeField] private string normalTextFormat = "{0}";
    [Tooltip("치명타 데미지 텍스트 포맷. {0} 위치에 데미지 숫자가 들어간다.")]
    [SerializeField] private string criticalTextFormat = "CRIT {0}";
    [Tooltip("강타 데미지 텍스트 포맷. {0} 위치에 데미지 숫자가 들어간다.")]
    [SerializeField] private string heavyTextFormat = "HEAVY {0}";
    [Tooltip("데미지 숫자를 반올림한 정수로 표시한다. 현재 데미지 적용 경로는 정수 표시를 사용한다.")]
    [SerializeField] private bool showRoundedDamage = true;
    [Tooltip("데미지 값이 0 이하일 때도 팝업을 표시한다.")]
    [SerializeField] private bool showZeroOrNegativeDamage;

    [Header("생성 위치")]
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

    [Header("연속 피격 분산")]
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

    [Header("움직임")]
    [Tooltip("데미지 팝업 유지 시간")]
    [SerializeField, Min(0.01f)] private float lifetime = DEFAULT_LIFETIME;
    [Tooltip("유지 시간 동안 이동할 거리")]
    [SerializeField] private Vector3 moveOffset = new Vector3(0f, 1.2f, 0f);
    [Tooltip("생성 직후 크기")]
    [SerializeField, Min(0f)] private float startScale = DEFAULT_START_SCALE;
    [Tooltip("사라질 때 크기")]
    [SerializeField, Min(0f)] private float endScale = DEFAULT_END_SCALE;
    [Tooltip("치명타 팝업 크기 배율")]
    [SerializeField, Min(0f)] private float criticalScaleMultiplier = 1.35f;
    [Tooltip("강타 팝업 크기 배율")]
    [SerializeField, Min(0f)] private float heavyScaleMultiplier = 1.6f;

    [Header("가림 방지")]
    [Tooltip("데미지 팝업 월드 캔버스에 적용할 Sorting Layer 이름")]
    [SerializeField] private string renderSortingLayerName = "WorldUI";
    [Tooltip("데미지 팝업 월드 캔버스의 Order in Layer")]
    [SerializeField] private int renderSortingOrder = 500;
    [Tooltip("팝업을 카메라 방향으로 당겨 3D 메시와 깊이 충돌하는 상황을 줄인다.")]
    [SerializeField, Min(0f)] private float cameraForwardOffset = 0.25f;

    public DamagePopup DamagePopupPrefab => damagePopupPrefab;
    public int InitialPoolSize => initialPoolSize;
    public int FontSize => fontSize;
    public float WorldCanvasScale => worldCanvasScale;
    public float TextRectWidth => textRectWidth;
    public float TextRectHeight => textRectHeight;
    public float TextWidthPerCharacter => textWidthPerCharacter;
    public TMP_FontAsset FontAsset => fontAsset;
    public float Lifetime => lifetime;
    public Vector3 MoveOffset => moveOffset;
    public Color DamageColor => damageColor;
    public bool ShowRoundedDamage => showRoundedDamage;
    public bool ShowZeroOrNegativeDamage => showZeroOrNegativeDamage;
    public bool UseStackedSpawnOffset => useStackedSpawnOffset;
    public float StackWindow => stackWindow;
    public int MaxStackSlots => maxStackSlots;
    public float StackHorizontalStep => stackHorizontalStep;
    public float StackVerticalStep => stackVerticalStep;
    public float StartScale => startScale;
    public float EndScale => endScale;
    public float CriticalScaleMultiplier => criticalScaleMultiplier;
    public float HeavyScaleMultiplier => heavyScaleMultiplier;
    public string RenderSortingLayerName => renderSortingLayerName;
    public int RenderSortingOrder => renderSortingOrder;
    public float CameraForwardOffset => cameraForwardOffset;

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

    // 데미지 타입에 맞는 텍스트 포맷을 반환한다
    public string GetTextFormat(DamagePopupType damageType)
    {
        switch (damageType)
        {
            case DamagePopupType.Heavy:
                return GetSafeTextFormat(heavyTextFormat);
            case DamagePopupType.Critical:
                return GetSafeTextFormat(criticalTextFormat);
            default:
                return GetSafeTextFormat(normalTextFormat);
        }
    }

    // 비어 있는 텍스트 포맷을 기본값으로 보정한다
    private static string GetSafeTextFormat(string textFormat)
    {
        if (string.IsNullOrWhiteSpace(textFormat))
        {
            return "{0}";
        }

        return textFormat;
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
        initialPoolSize = Mathf.Max(1, initialPoolSize);
        fontSize = Mathf.Max(1, fontSize);
        worldCanvasScale = Mathf.Max(0.001f, worldCanvasScale);
        textRectWidth = Mathf.Max(0.1f, textRectWidth);
        textRectHeight = Mathf.Max(0.1f, textRectHeight);
        textWidthPerCharacter = Mathf.Max(0f, textWidthPerCharacter);
        lifetime = Mathf.Max(0.01f, lifetime);
        normalZombieHeightOffset = Mathf.Max(0f, normalZombieHeightOffset);
        bossZombieHeightOffset = Mathf.Max(0f, bossZombieHeightOffset);
        randomHorizontalRadius = Mathf.Max(0f, randomHorizontalRadius);
        randomVerticalRange = Mathf.Max(0f, randomVerticalRange);
        stackWindow = Mathf.Max(0.01f, stackWindow);
        maxStackSlots = Mathf.Max(1, maxStackSlots);
        stackHorizontalStep = Mathf.Max(0f, stackHorizontalStep);
        stackVerticalStep = Mathf.Max(0f, stackVerticalStep);
        cameraForwardOffset = Mathf.Max(0f, cameraForwardOffset);
        startScale = Mathf.Max(0f, startScale);
        endScale = Mathf.Max(0f, endScale);
        criticalScaleMultiplier = Mathf.Max(0f, criticalScaleMultiplier);
        heavyScaleMultiplier = Mathf.Max(0f, heavyScaleMultiplier);
        normalTextFormat = GetSafeTextFormat(normalTextFormat);
        criticalTextFormat = GetSafeTextFormat(criticalTextFormat);
        heavyTextFormat = GetSafeTextFormat(heavyTextFormat);
        if (string.IsNullOrWhiteSpace(renderSortingLayerName))
        {
            renderSortingLayerName = "WorldUI";
        }
    }

    // 런타임 기본 설정 인스턴스를 생성한다
    public static DamagePopupSettings CreateRuntimeDefault()
    {
        return CreateInstance<DamagePopupSettings>();
    }
}
