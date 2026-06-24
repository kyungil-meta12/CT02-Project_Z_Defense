using TMPro;
using UnityEngine;

/// <summary>
/// 월드 공간 캔버스에서 데미지 숫자를 표시하고 수명 종료 시 풀로 반환한다.
/// </summary>
public class DamagePopup : PoolObject
{
    private const string WORLD_CANVAS_NAME = "DamagePopupWorldCanvas";

    private TMP_Text textMesh;
    private TextMeshProUGUI uiTextMesh;
    private TextMeshPro legacyWorldText;
    private Canvas popupCanvas;
    private TMP_FontAsset defaultFontAsset;
    private Camera targetCamera;
    private float lifetime;
    private float elapsedTime;
    private Vector3 startPosition;
    private Vector3 moveOffset;
    private Color startColor;
    private float startScale;
    private float endScale;
    private int currentTextLength;
    private bool isInitialized;

    // 텍스트와 월드 캔버스 컴포넌트를 초기화한다
    private void Awake()
    {
        EnsureTextMesh();
    }

    // 풀에서 꺼내기 직전에 재사용 상태를 초기화한다
    public override void OnBeforeSpawn()
    {
        elapsedTime = 0f;
        isInitialized = false;
    }

    // 풀에 반환될 때 초기화 완료 상태를 해제한다
    public override void OnDespawn()
    {
        isInitialized = false;
    }

    // 데미지 팝업 숫자를 타입별 표시 설정과 함께 초기화한다
    public void Init(int damageValue, Vector3 position, DamagePopupSettings settings, Camera camera_, DamagePopupType damageType)
    {
        targetCamera = camera_;
        settings = PrepareTextMesh(settings);
        SetDamageText(damageValue, settings, damageType);
        ApplyRuntimeState(position, settings, camera_, damageType);
    }

    // 매 프레임 팝업 위치, 크기, 투명도, 카메라 방향을 갱신한다
    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        elapsedTime += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(elapsedTime / lifetime);

        transform.position = startPosition + (moveOffset * normalizedTime);
        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, normalizedTime);

        Color currentColor = startColor;
        currentColor.a = Mathf.Lerp(startColor.a, 0f, normalizedTime);
        textMesh.color = currentColor;

        FaceCamera();

        if (elapsedTime >= lifetime)
        {
            ReturnToPool();
        }
    }

    // 팝업 텍스트 컴포넌트와 표시 스타일을 준비한다
    private DamagePopupSettings PrepareTextMesh(DamagePopupSettings settings)
    {
        if (settings == null)
        {
            Debug.LogWarning("[DamagePopup] 설정이 없어 런타임 기본값을 사용합니다.", this);
            settings = DamagePopupSettings.CreateRuntimeDefault();
        }

        if (textMesh == null || popupCanvas == null)
        {
            EnsureTextMesh();
        }

        textMesh.color = settings.DamageColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = settings.FontSize;
        textMesh.enableAutoSizing = false;
        textMesh.fontSizeMin = settings.FontSize;
        textMesh.fontSizeMax = settings.FontSize;
        textMesh.enableWordWrapping = false;
        textMesh.overflowMode = TextOverflowModes.Overflow;
        textMesh.font = settings.FontAsset != null ? settings.FontAsset : defaultFontAsset;
        ApplyWorldCanvasScale(settings);
        ApplyCanvasSettings(settings);
        return settings;
    }

    // 팝업의 위치, 이동, 생명주기, 타입별 표시 상태를 적용한다
    private void ApplyRuntimeState(Vector3 position, DamagePopupSettings settings, Camera camera_, DamagePopupType damageType)
    {
        lifetime = Mathf.Max(0.01f, settings.Lifetime);
        elapsedTime = 0f;
        startPosition = ApplyCameraForwardOffset(position, settings, camera_);
        moveOffset = settings.MoveOffset;
        startColor = settings.GetDamageColor(damageType);
        float scaleMultiplier = settings.GetScaleMultiplier(damageType);
        startScale = settings.StartScale * scaleMultiplier;
        endScale = settings.EndScale * scaleMultiplier;
        isInitialized = true;

        transform.position = startPosition;
        transform.localScale = Vector3.one * startScale;
        textMesh.color = startColor;
    }

    // 데미지 타입에 맞는 텍스트 라벨과 숫자를 설정한다
    private void SetDamageText(int damageValue, DamagePopupSettings settings, DamagePopupType damageType)
    {
        string textFormat = settings.GetTextFormat(damageType);
        if (textFormat.Contains("{0}"))
        {
            textMesh.SetText(textFormat, damageValue);
            currentTextLength = CountFormattedDamageTextLength(textFormat, damageValue);
            ApplyTextRectSize(settings);
            return;
        }

        textMesh.SetText(textFormat);
        currentTextLength = textFormat.Length;
        ApplyTextRectSize(settings);
    }

    // 팝업이 현재 카메라를 바라보게 회전시킨다
    private void FaceCamera()
    {
        if (targetCamera == null)
        {
            return;
        }

        Transform cameraTransform = targetCamera.transform;
        transform.rotation = Quaternion.LookRotation(transform.position - cameraTransform.position, cameraTransform.up);
    }

    // TextMeshProUGUI와 월드 캔버스를 확보하고 기존 월드 메시 텍스트를 비활성화한다
    private void EnsureTextMesh()
    {
        uiTextMesh = GetComponentInChildren<TextMeshProUGUI>(true);
        if (uiTextMesh == null)
        {
            uiTextMesh = CreateWorldCanvasText();
        }

        popupCanvas = uiTextMesh.GetComponentInParent<Canvas>();
        if (popupCanvas != null)
        {
            popupCanvas.renderMode = RenderMode.WorldSpace;
            popupCanvas.overrideSorting = true;
        }

        uiTextMesh.raycastTarget = false;
        textMesh = uiTextMesh;

        legacyWorldText = GetComponent<TextMeshPro>();
        if (legacyWorldText != null)
        {
            legacyWorldText.enabled = false;
            Renderer legacyRenderer = legacyWorldText.renderer;
            if (legacyRenderer != null)
            {
                legacyRenderer.enabled = false;
            }
        }

        if (defaultFontAsset == null)
        {
            defaultFontAsset = textMesh.font;
        }
    }

    // 월드 캔버스 자식 오브젝트와 TMP UGUI 텍스트를 생성한다
    private TextMeshProUGUI CreateWorldCanvasText()
    {
        GameObject canvasObject = new GameObject(WORLD_CANVAS_NAME, typeof(RectTransform), typeof(Canvas), typeof(TextMeshProUGUI));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = Vector3.zero;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one;

        RectTransform rectTransform = canvasObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(DamagePopupSettings.DEFAULT_TEXT_RECT_WIDTH, DamagePopupSettings.DEFAULT_TEXT_RECT_HEIGHT);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        return canvasObject.GetComponent<TextMeshProUGUI>();
    }

    // 월드 캔버스의 정렬 레이어와 카메라를 설정한다
    private void ApplyCanvasSettings(DamagePopupSettings settings)
    {
        if (popupCanvas == null)
        {
            return;
        }

        popupCanvas.worldCamera = targetCamera;
        popupCanvas.sortingLayerID = ResolveSortingLayerId(settings.RenderSortingLayerName);
        popupCanvas.sortingOrder = settings.RenderSortingOrder;
    }

    // 텍스트가 줄바꿈되지 않도록 TMP UGUI RectTransform 크기를 설정한다
    private void ApplyTextRectSize(DamagePopupSettings settings)
    {
        if (uiTextMesh == null)
        {
            return;
        }

        int safeTextLength = Mathf.Max(1, currentTextLength);
        float dynamicWidth = Mathf.Max(settings.TextRectWidth, safeTextLength * settings.TextWidthPerCharacter);
        RectTransform rectTransform = uiTextMesh.rectTransform;
        rectTransform.sizeDelta = new Vector2(dynamicWidth, settings.TextRectHeight);
    }

    // 실제 표시될 데미지 텍스트 길이를 GC 없이 계산한다
    private static int CountFormattedDamageTextLength(string textFormat, int damageValue)
    {
        int placeholderIndex = textFormat.IndexOf("{0}", System.StringComparison.Ordinal);
        if (placeholderIndex < 0)
        {
            return textFormat.Length;
        }

        return textFormat.Length - 3 + CountIntegerDigits(damageValue);
    }

    // 정수 데미지 값의 표시 자릿수를 계산한다
    private static int CountIntegerDigits(int value)
    {
        if (value == 0)
        {
            return 1;
        }

        int digitCount = value < 0 ? 1 : 0;
        int remainingValue = Mathf.Abs(value);
        while (remainingValue > 0)
        {
            digitCount++;
            remainingValue /= 10;
        }

        return digitCount;
    }

    // 월드 캔버스 자식 스케일을 설정값에 맞게 적용한다
    private void ApplyWorldCanvasScale(DamagePopupSettings settings)
    {
        if (uiTextMesh == null)
        {
            return;
        }

        float safeScale = Mathf.Max(0.001f, settings.WorldCanvasScale);
        uiTextMesh.transform.localScale = Vector3.one * safeScale;
    }

    // 설정된 Sorting Layer 이름을 유효한 ID로 변환한다
    private static int ResolveSortingLayerId(string sortingLayerName)
    {
        if (string.IsNullOrWhiteSpace(sortingLayerName))
        {
            return SortingLayer.NameToID("Default");
        }

        int sortingLayerId = SortingLayer.NameToID(sortingLayerName);
        if (sortingLayerId == 0 && sortingLayerName != "Default")
        {
            return SortingLayer.NameToID("Default");
        }

        return sortingLayerId;
    }

    // 카메라 방향으로 팝업 위치를 당겨 메시나 HP바와 같은 깊이에 겹치는 상황을 줄인다
    private Vector3 ApplyCameraForwardOffset(Vector3 position, DamagePopupSettings settings, Camera camera_)
    {
        if (camera_ == null || settings.CameraForwardOffset <= 0f)
        {
            return position;
        }

        Vector3 cameraDirection = camera_.transform.position - position;
        if (cameraDirection.sqrMagnitude <= 0.0001f)
        {
            return position;
        }

        return position + cameraDirection.normalized * settings.CameraForwardOffset;
    }
}
