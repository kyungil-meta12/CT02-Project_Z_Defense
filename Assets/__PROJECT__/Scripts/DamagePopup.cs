using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshPro))]
public class DamagePopup : PoolObject
{
    private TextMeshPro textMesh;
    private Camera targetCamera;
    private float lifetime;
    private float elapsedTime;
    private Vector3 startPosition;
    private Vector3 moveOffset;
    private Color startColor;
    private float startScale;
    private float endScale;
    private bool isInitialized;

    private void Awake()
    {
        EnsureTextMesh();
    }

    public override void OnBeforeSpawn()
    {
        elapsedTime = 0f;
        isInitialized = false;
    }

    public override void OnDespawn()
    {
        isInitialized = false;
    }

    /// <summary>
    /// 데미지 팝업 표시 값을 초기화한다
    /// </summary>
    /// <param name="text"></param>
    /// <param name="position"></param>
    /// <param name="color"></param>
    /// <param name="fontSize"></param>
    /// <param name="fontAsset"></param>
    /// <param name="startScale_"></param>
    /// <param name="endScale_"></param>
    /// <param name="lifetime_"></param>
    /// <param name="moveOffset_"></param>
    /// <param name="camera_"></param>
    public void Init(string text, Vector3 position, Color color, int fontSize, TMP_FontAsset fontAsset, float startScale_, float endScale_, float lifetime_, Vector3 moveOffset_, Camera camera_)
    {
        if (textMesh == null)
        {
            EnsureTextMesh();
        }

        textMesh.text = text;
        textMesh.color = color;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = fontSize;
        textMesh.enableAutoSizing = false;
        if (fontAsset != null)
        {
            textMesh.font = fontAsset;
        }

        targetCamera = camera_;
        lifetime = Mathf.Max(0.01f, lifetime_);
        elapsedTime = 0f;
        startPosition = position;
        moveOffset = moveOffset_;
        startColor = color;
        startScale = startScale_;
        endScale = endScale_;
        isInitialized = true;

        transform.position = startPosition;
        transform.localScale = Vector3.one * startScale;
    }

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

    private void FaceCamera()
    {
        if (targetCamera == null)
        {
            return;
        }

        Transform cameraTransform = targetCamera.transform;
        transform.rotation = Quaternion.LookRotation(transform.position - cameraTransform.position, cameraTransform.up);
    }

    private void EnsureTextMesh()
    {
        textMesh = GetComponent<TextMeshPro>();
        if (textMesh == null)
        {
            textMesh = gameObject.AddComponent<TextMeshPro>();
        }
    }
}
