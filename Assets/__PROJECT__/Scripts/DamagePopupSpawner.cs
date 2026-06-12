using TMPro;
using UnityEngine;

/// <summary>
/// 데미지 팝업 프리팹과 설정을 관리하고 피격 위치에 팝업을 스폰한다.
/// </summary>
public class DamagePopupSpawner : MonoBehaviour
{
    private const string DAMAGE_POPUP_RESOURCE_PATH = "UI/DamagePopup";
    private const string DAMAGE_POPUP_SETTINGS_RESOURCE_PATH = "UI/DamagePopupSettings";
    private const string RUNTIME_SYSTEMS_CONTAINER_NAME = "RuntimeSystems";

    private static DamagePopupSpawner Inst;

    [SerializeField] private DamagePopupSettings settings;

    private DamagePopup damagePopupPrefab;
    private Camera targetCamera;

    // 싱글톤 인스턴스와 팝업 풀을 초기화한다
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

    // 현재 싱글톤 인스턴스가 제거될 때 정적 참조를 정리한다
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
        spawner.Spawn(Mathf.RoundToInt(damage), spawnPosition);
    }

    // 씬 인스턴스가 없으면 런타임 컨테이너 아래에 스포너를 생성한다
    private static DamagePopupSpawner GetOrCreateInstance()
    {
        if (Inst != null)
        {
            return Inst;
        }

        GameObject spawnerObject = new GameObject("DamagePopupSpawner");
        spawnerObject.transform.SetParent(GetOrCreateRuntimeSystemsContainer());
        Debug.LogWarning("[DamagePopupSpawner] 씬 인스턴스가 없어 런타임 DamagePopupSpawner를 생성했습니다.");
        return spawnerObject.AddComponent<DamagePopupSpawner>();
    }

    // 메모리 풀이 없으면 런타임 컨테이너 아래에 생성한다
    private static MemoryPool GetOrCreateMemoryPool()
    {
        if (MemoryPool.Inst != null)
        {
            return MemoryPool.Inst;
        }

        GameObject memoryPoolObject = new GameObject("MemoryPool");
        memoryPoolObject.transform.SetParent(GetOrCreateRuntimeSystemsContainer());
        return memoryPoolObject.AddComponent<MemoryPool>();
    }

    // 런타임 시스템을 묶을 컨테이너 트랜스폼을 반환한다
    private static Transform GetOrCreateRuntimeSystemsContainer()
    {
        GameObject containerObject = GameObject.Find(RUNTIME_SYSTEMS_CONTAINER_NAME);
        if (containerObject != null)
        {
            return containerObject.transform;
        }

        return new GameObject(RUNTIME_SYSTEMS_CONTAINER_NAME).transform;
    }

    // 설정된 초기 개수만큼 데미지 팝업을 풀에 미리 생성한다
    private void Prewarm()
    {
        if (damagePopupPrefab == null)
        {
            return;
        }

        GetOrCreateMemoryPool().Prewarm(damagePopupPrefab, settings.InitialPoolSize);
    }

    // 데미지 숫자 팝업을 풀에서 가져와 표시한다
    private void Spawn(int damageValue, Vector3 position)
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

        popup.Init(damageValue, position, settings, targetCamera);
    }

    // 설정 또는 리소스에서 데미지 팝업 프리팹을 확보한다
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

    // 리소스 프리팹이 없을 때 사용할 런타임 기본 팝업을 만든다
    private DamagePopup CreateRuntimePrefab()
    {
        GameObject popupObject = new GameObject("DamagePopup");
        popupObject.transform.SetParent(transform);
        popupObject.SetActive(false);

        TextMeshPro textMesh = popupObject.AddComponent<TextMeshPro>();
        ApplyTextSettings(textMesh);

        return popupObject.AddComponent<DamagePopup>();
    }

    // 데미지 팝업 설정을 확보하고 없으면 런타임 기본값을 사용한다
    private void EnsureSettings()
    {
        if (settings != null)
        {
            return;
        }

        settings = Resources.Load<DamagePopupSettings>(DAMAGE_POPUP_SETTINGS_RESOURCE_PATH);
        if (settings == null)
        {
            Debug.LogWarning("[DamagePopupSpawner] DamagePopupSettings가 없어 런타임 기본 설정을 사용합니다.");
            settings = DamagePopupSettings.CreateRuntimeDefault();
        }
    }

    // 팝업 프리팹의 TextMeshPro 표시 설정을 적용한다
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
