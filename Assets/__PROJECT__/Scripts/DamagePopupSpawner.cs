using TMPro;
using UnityEngine;

public class DamagePopupSpawner : MonoBehaviour
{
    private const string DAMAGE_POPUP_RESOURCE_PATH = "UI/DamagePopup";
    private const string DAMAGE_POPUP_SETTINGS_RESOURCE_PATH = "UI/DamagePopupSettings";
    private const string RUNTIME_SYSTEMS_CONTAINER_NAME = "RuntimeSystems";

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
        Vector3 spawnPosition = target.position + (Vector3.up * spawner.settings.HeightOffset);
        spawner.Spawn(Mathf.RoundToInt(damage).ToString(), spawnPosition);
    }

    private static DamagePopupSpawner GetOrCreateInstance()
    {
        if (Inst != null)
        {
            return Inst;
        }

        GameObject spawnerObject = new GameObject("DamagePopupSpawner");
        spawnerObject.transform.SetParent(GetOrCreateRuntimeSystemsContainer());
        Debug.LogWarning("[DamagePopupSpawner] Scene instance was missing. A runtime DamagePopupSpawner was created automatically.");
        return spawnerObject.AddComponent<DamagePopupSpawner>();
    }

    private static MemoryPool GetOrCreateMemoryPool()
    {
        if (MemoryPool.Inst != null)
        {
            return MemoryPool.Inst;
        }

        GameObject memoryPoolObject = new GameObject("MemoryPool");
        memoryPoolObject.transform.SetParent(GetOrCreateRuntimeSystemsContainer());
        Debug.LogWarning("[DamagePopupSpawner] MemoryPool was missing. A runtime MemoryPool was created automatically.");
        return memoryPoolObject.AddComponent<MemoryPool>();
    }

    private static Transform GetOrCreateRuntimeSystemsContainer()
    {
        GameObject containerObject = GameObject.Find(RUNTIME_SYSTEMS_CONTAINER_NAME);
        if (containerObject != null)
        {
            return containerObject.transform;
        }

        return new GameObject(RUNTIME_SYSTEMS_CONTAINER_NAME).transform;
    }

    private void Prewarm()
    {
        if (damagePopupPrefab == null)
        {
            return;
        }

        GetOrCreateMemoryPool().Prewarm(damagePopupPrefab, settings.InitialPoolSize);
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

        popup.Init(text, position, settings, targetCamera);
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
        if (settings == null)
        {
            Debug.LogWarning("[DamagePopupSpawner] DamagePopupSettings was missing. Runtime default settings will be used.");
            settings = DamagePopupSettings.CreateRuntimeDefault();
        }
    }

    private void ApplyTextSettings(TextMeshPro textMesh)
    {
        if (textMesh == null)
        {
            return;
        }

        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = settings.FontSize;
        textMesh.enableAutoSizing = false;
        if (settings.FontAsset != null)
        {
            textMesh.font = settings.FontAsset;
        }

        textMesh.color = settings.DamageColor;
        textMesh.text = "0";
    }
}
