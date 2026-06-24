using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 월드 공간에서 데미지 숫자를 표시하고 수명 종료 시 풀로 반환한다.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class DamagePopup : PoolObject
{
    private TextMeshPro textMesh;
    private TMP_FontAsset defaultFontAsset;
    private Renderer cachedRenderer;
    private Material popupMaterialInstance;
    private Camera targetCamera;
    private float lifetime;
    private float elapsedTime;
    private Vector3 startPosition;
    private Vector3 moveOffset;
    private Color startColor;
    private float startScale;
    private float endScale;
    private bool isInitialized;

    // 텍스트 메시 컴포넌트를 초기화한다
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

    // 생성한 TMP 머티리얼 인스턴스를 정리한다
    private void OnDestroy()
    {
        if (popupMaterialInstance == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(popupMaterialInstance);
        }
        else
        {
            DestroyImmediate(popupMaterialInstance);
        }
    }

    // 데미지 팝업 문자열을 지정한 표시 설정으로 초기화한다
    public void Init(string text, Vector3 position, DamagePopupSettings settings, Camera camera_)
    {
        settings = PrepareTextMesh(settings);
        textMesh.text = text;
        ApplyRuntimeState(position, settings, camera_);
    }

    // 데미지 팝업 숫자를 GC 부담이 낮은 TMP 숫자 설정 경로로 초기화한다
    public void Init(int damageValue, Vector3 position, DamagePopupSettings settings, Camera camera_)
    {
        Init(damageValue, position, settings, camera_, TurretDamagePolishType.Normal);
    }

    // 데미지 팝업 숫자를 타입별 표시 설정과 함께 초기화한다
    public void Init(int damageValue, Vector3 position, DamagePopupSettings settings, Camera camera_, TurretDamagePolishType damageType)
    {
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

        if (textMesh == null)
        {
            EnsureTextMesh();
        }

        textMesh.color = settings.DamageColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = settings.FontSize;
        textMesh.enableAutoSizing = false;
        textMesh.font = settings.FontAsset != null ? settings.FontAsset : defaultFontAsset;
        ApplyRendererSettings(settings);
        return settings;
    }

    // 팝업의 위치, 이동, 생명주기 상태를 적용한다
    private void ApplyRuntimeState(Vector3 position, DamagePopupSettings settings, Camera camera_)
    {
        ApplyRuntimeState(position, settings, camera_, TurretDamagePolishType.Normal);
    }

    // 팝업의 위치, 이동, 생명주기, 타입별 표시 상태를 적용한다
    private void ApplyRuntimeState(Vector3 position, DamagePopupSettings settings, Camera camera_, TurretDamagePolishType damageType)
    {
        targetCamera = camera_;
        lifetime = Mathf.Max(0.01f, settings.Lifetime);
        elapsedTime = 0f;
        startPosition = position;
        moveOffset = settings.MoveOffset;
        startColor = settings.GetDamageColor(damageType);
        startPosition = ApplyCameraForwardOffset(startPosition, settings, camera_);
        float scaleMultiplier = settings.GetScaleMultiplier(damageType);
        startScale = settings.StartScale * scaleMultiplier;
        endScale = settings.EndScale * scaleMultiplier;
        isInitialized = true;

        transform.position = startPosition;
        transform.localScale = Vector3.one * startScale;
    }

    // 데미지 타입에 맞는 텍스트 라벨과 숫자를 설정한다
    private void SetDamageText(int damageValue, DamagePopupSettings settings, TurretDamagePolishType damageType)
    {
        string textFormat = settings.GetTextFormat(damageType);
        if (textFormat.Contains("{0}"))
        {
            textMesh.SetText(textFormat, damageValue);
            return;
        }

        textMesh.SetText(textFormat);
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

    // TextMeshPro 컴포넌트를 확보하고 기본 폰트를 저장한다
    private void EnsureTextMesh()
    {
        textMesh = GetComponent<TextMeshPro>();
        if (textMesh == null)
        {
            textMesh = gameObject.AddComponent<TextMeshPro>();
        }

        if (defaultFontAsset == null)
        {
            defaultFontAsset = textMesh.font;
        }

        if (cachedRenderer == null)
        {
            cachedRenderer = textMesh.renderer;
        }
    }

    // 팝업 렌더러가 HP바와 3D 메시 뒤에 묻히지 않도록 표시 우선순위를 적용한다
    private void ApplyRendererSettings(DamagePopupSettings settings)
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        if (cachedRenderer == null)
        {
            return;
        }

        ApplySortingLayer(cachedRenderer, settings.RenderSortingLayerName);
        cachedRenderer.sortingOrder = settings.RenderSortingOrder;
        cachedRenderer.shadowCastingMode = settings.DisableRendererShadows ? ShadowCastingMode.Off : ShadowCastingMode.On;
        cachedRenderer.receiveShadows = !settings.DisableRendererShadows;

        ApplyDepthTestMode(settings);
    }

    // 설정된 Sorting Layer가 있으면 팝업 렌더러에 적용한다
    private static void ApplySortingLayer(Renderer targetRenderer, string sortingLayerName)
    {
        if (targetRenderer == null || string.IsNullOrWhiteSpace(sortingLayerName))
        {
            return;
        }

        int sortingLayerId = SortingLayer.NameToID(sortingLayerName);
        if (sortingLayerId == 0 && sortingLayerName != "Default")
        {
            return;
        }

        targetRenderer.sortingLayerID = sortingLayerId;
    }

    // TMP 머티리얼 인스턴스의 깊이 테스트 방식을 팝업 전용으로 설정한다
    private void ApplyDepthTestMode(DamagePopupSettings settings)
    {
        if (textMesh == null)
        {
            return;
        }

        Material sourceMaterial = textMesh.fontMaterial != null ? textMesh.fontMaterial : textMesh.fontSharedMaterial;
        if (sourceMaterial == null)
        {
            return;
        }

        if (popupMaterialInstance == null || popupMaterialInstance.shader != sourceMaterial.shader)
        {
            popupMaterialInstance = new Material(sourceMaterial);
            popupMaterialInstance.name = sourceMaterial.name + " DamagePopupInstance";
        }

        if (popupMaterialInstance.HasProperty("unity_GUIZTestMode"))
        {
            popupMaterialInstance.SetFloat("unity_GUIZTestMode", settings.RenderOverSceneGeometry ? (float)CompareFunction.Always : (float)CompareFunction.LessEqual);
        }

        textMesh.fontMaterial = popupMaterialInstance;
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
