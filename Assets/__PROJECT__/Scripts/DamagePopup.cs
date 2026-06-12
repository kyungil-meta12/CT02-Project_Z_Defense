using TMPro;
using UnityEngine;

/// <summary>
/// 월드 공간에서 데미지 숫자를 표시하고 수명 종료 시 풀로 반환한다.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class DamagePopup : PoolObject
{
    private TextMeshPro textMesh;
    private TMP_FontAsset defaultFontAsset;
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

    /// <summary>
    /// 데미지 팝업 표시 값을 초기화한다
    /// </summary>
    /// <param name="text"></param>
    /// <param name="position"></param>
    /// <param name="settings"></param>
    /// <param name="camera_"></param>
    public void Init(string text, Vector3 position, DamagePopupSettings settings, Camera camera_)
    {
        settings = PrepareTextMesh(settings);
        textMesh.text = text;
        ApplyRuntimeState(position, settings, camera_);
    }

    // 데미지 팝업 숫자를 GC 부담이 낮은 TMP 숫자 설정 경로로 초기화한다
    public void Init(int damageValue, Vector3 position, DamagePopupSettings settings, Camera camera_)
    {
        settings = PrepareTextMesh(settings);
        textMesh.SetText("{0}", damageValue);
        ApplyRuntimeState(position, settings, camera_);
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
        return settings;
    }

    // 팝업의 위치, 이동, 생명주기 상태를 적용한다
    private void ApplyRuntimeState(Vector3 position, DamagePopupSettings settings, Camera camera_)
    {
        targetCamera = camera_;
        lifetime = Mathf.Max(0.01f, settings.Lifetime);
        elapsedTime = 0f;
        startPosition = position;
        moveOffset = settings.MoveOffset;
        startColor = settings.DamageColor;
        startScale = settings.StartScale;
        endScale = settings.EndScale;
        isInitialized = true;

        transform.position = startPosition;
        transform.localScale = Vector3.one * startScale;
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
    }
}
