using TMPro;
using UnityEngine;

public class DamagePopupSpawner : MonoBehaviour
{
    private const string DAMAGE_POPUP_RESOURCE_PATH = "DamagePopup";
    private const string DAMAGE_POPUP_SETTINGS_RESOURCE_PATH = "DamagePopupSettings";
    private const int DEFAULT_INITIAL_POOL_SIZE = 32;
    private const int DEFAULT_FONT_SIZE = 24;
    private const float DEFAULT_LIFETIME = 0.75f;
    private const float DEFAULT_HEIGHT_OFFSET = 2.2f;
    private const float DEFAULT_START_SCALE = 1.15f;
    private const float DEFAULT_END_SCALE = 0.85f;

    private static DamagePopupSpawner Inst;

    [SerializeField] private DamagePopupSettings settings;

    private DamagePopup damagePopupPrefab;
    private Camera targetCamera;

    private void Awake()
    {
        if (Inst != null && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;
        targetCamera = Camera.main;
        EnsureSettings();
        EnsurePrefab();
        Prewarm();
    }

    private void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }
    }

    /// <summary>
    /// 대상 위치 위에 데미지 숫자 팝업을 표시한다
    /// </summary>
    /// <param name="target"></param>
    /// <param name="damage"></param>
    public static void SpawnDamage(Transform target, float damage)
    {
        if (target == null || damage <= 0f)
        {
            return;
        }

        DamagePopupSpawner spawner = GetOrCreateInstance();
        Vector3 spawnPosition = target.position + (Vector3.up * spawner.GetHeightOffset());
        spawner.Spawn(Mathf.RoundToInt(damage).ToString(), spawnPosition);
    }

    private static DamagePopupSpawner GetOrCreateInstance()
    {
        if (Inst != null)
        {
            return Inst;
        }

        GameObject spawnerObject = new GameObject("DamagePopupSpawner");
        return spawnerObject.AddComponent<DamagePopupSpawner>();
    }

    private static MemoryPool GetOrCreateMemoryPool()
    {
        if (MemoryPool.Inst != null)
        {
            return MemoryPool.Inst;
        }

        GameObject memoryPoolObject = new GameObject("MemoryPool");
        return memoryPoolObject.AddComponent<MemoryPool>();
    }

    private void Prewarm()
    {
        if (damagePopupPrefab == null)
        {
            return;
        }

        GetOrCreateMemoryPool().Prewarm(damagePopupPrefab, GetInitialPoolSize());
    }

    private void Spawn(string text, Vector3 position)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        EnsurePrefab();

        if (damagePopupPrefab == null)
        {
            return;
        }

        DamagePopup popup = GetOrCreateMemoryPool().GetInstance<DamagePopup>(damagePopupPrefab);
        if (popup == null)
        {
            return;
        }

        popup.Init(text, position, GetDamageColor(), GetFontSize(), GetFontAsset(), GetStartScale(), GetEndScale(), GetLifetime(), GetMoveOffset(), targetCamera);
    }

    private void EnsurePrefab()
    {
        EnsureSettings();

        if (damagePopupPrefab != null)
        {
            ApplyTextSettings(damagePopupPrefab.GetComponent<TextMeshPro>());
            return;
        }

        if (settings != null && settings.DamagePopupPrefab != null)
        {
            damagePopupPrefab = settings.DamagePopupPrefab;
            ApplyTextSettings(damagePopupPrefab.GetComponent<TextMeshPro>());
            return;
        }

        damagePopupPrefab = Resources.Load<DamagePopup>(DAMAGE_POPUP_RESOURCE_PATH);
        if (damagePopupPrefab != null)
        {
            ApplyTextSettings(damagePopupPrefab.GetComponent<TextMeshPro>());
            return;
        }

        damagePopupPrefab = CreateRuntimePrefab();
    }

    private DamagePopup CreateRuntimePrefab()
    {
        GameObject popupObject = new GameObject("DamagePopup");
        popupObject.transform.SetParent(transform);
        popupObject.SetActive(false);

        TextMeshPro textMesh = popupObject.AddComponent<TextMeshPro>();
        ApplyTextSettings(textMesh);

        return popupObject.AddComponent<DamagePopup>();
    }

    private void EnsureSettings()
    {
        if (settings != null)
        {
            return;
        }

        settings = Resources.Load<DamagePopupSettings>(DAMAGE_POPUP_SETTINGS_RESOURCE_PATH);
    }

    private void ApplyTextSettings(TextMeshPro textMesh)
    {
        if (textMesh == null)
        {
            return;
        }

        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = GetFontSize();
        textMesh.enableAutoSizing = false;
        if (GetFontAsset() != null)
        {
            textMesh.font = GetFontAsset();
        }

        textMesh.color = GetDamageColor();
        textMesh.text = "0";
    }

    private int GetInitialPoolSize()
    {
        return settings != null ? settings.InitialPoolSize : DEFAULT_INITIAL_POOL_SIZE;
    }

    private int GetFontSize()
    {
        return settings != null ? settings.FontSize : DEFAULT_FONT_SIZE;
    }

    private TMP_FontAsset GetFontAsset()
    {
        return settings != null ? settings.FontAsset : null;
    }

    private float GetLifetime()
    {
        return settings != null ? settings.Lifetime : DEFAULT_LIFETIME;
    }

    private float GetHeightOffset()
    {
        return settings != null ? settings.HeightOffset : DEFAULT_HEIGHT_OFFSET;
    }

    private Vector3 GetMoveOffset()
    {
        return settings != null ? settings.MoveOffset : new Vector3(0f, 1.2f, 0f);
    }

    private Color GetDamageColor()
    {
        return settings != null ? settings.DamageColor : new Color(1f, 0.35f, 0.12f, 1f);
    }

    private float GetStartScale()
    {
        return settings != null ? settings.StartScale : DEFAULT_START_SCALE;
    }

    private float GetEndScale()
    {
        return settings != null ? settings.EndScale : DEFAULT_END_SCALE;
    }
}
