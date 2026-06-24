using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 데미지 팝업 프리팹과 설정을 관리하고 피격 위치에 팝업을 스폰한다.
/// </summary>
public class DamagePopupSpawner : MonoBehaviour
{
    private const string DAMAGE_POPUP_RESOURCE_PATH = "UI/DamagePopup";
    private const string DAMAGE_POPUP_SETTINGS_RESOURCE_PATH = "UI/DamagePopupSettings";
    private const string RUNTIME_SYSTEMS_CONTAINER_NAME = "RuntimeSystems";

    private static readonly Dictionary<int, PopupStackState> PopupStackStates = new Dictionary<int, PopupStackState>(128);
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
        PopupStackStates.Clear();
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

    // 대상 위치 위에 일반 데미지 숫자 팝업을 표시한다
    public static void SpawnDamage(Transform target, float damage)
    {
        SpawnDamage(target, new DamageInfo(damage));
    }

    // 대상 위치 위에 데미지 정보 기반 숫자 팝업을 표시한다
    public static void SpawnDamage(Transform target, DamageInfo damageInfo)
    {
        SpawnDamage(target, damageInfo, DamagePopupTargetType.Default);
    }

    // 대상 위치 위에 대상 종류와 데미지 정보 기반 숫자 팝업을 표시한다
    public static void SpawnDamage(Transform target, DamageInfo damageInfo, DamagePopupTargetType targetType)
    {
        if (target == null)
        {
            return;
        }

        DamagePopupSpawner spawner = GetOrCreateInstance();
        if (!spawner.settings.ShowZeroOrNegativeDamage && damageInfo.Damage <= 0f)
        {
            return;
        }

        spawner.RefreshTargetCamera();
        Vector3 spawnPosition = spawner.settings.GetSpawnPosition(target.position, targetType);
        spawnPosition += spawner.GetStackedSpawnOffset(target);
        int damageValue = spawner.settings.ShowRoundedDamage ? Mathf.RoundToInt(damageInfo.Damage) : Mathf.FloorToInt(damageInfo.Damage);
        spawner.Spawn(damageValue, spawnPosition, damageInfo.PopupType);
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

    // 데미지 숫자 팝업을 타입별 표시 설정과 함께 풀에서 가져와 표시한다
    private void Spawn(int damageValue, Vector3 position, DamagePopupType damageType)
    {
        RefreshTargetCamera();
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

        popup.Init(damageValue, position, settings, targetCamera, damageType);
    }

    // 설정 또는 리소스에서 데미지 팝업 프리팹을 확보한다
    private void EnsurePrefab()
    {
        EnsureSettings();

        if (damagePopupPrefab != null)
        {
            return;
        }

        if (settings != null && settings.DamagePopupPrefab != null)
        {
            damagePopupPrefab = settings.DamagePopupPrefab;
            return;
        }

        damagePopupPrefab = Resources.Load<DamagePopup>(DAMAGE_POPUP_RESOURCE_PATH);
        if (damagePopupPrefab != null)
        {
            return;
        }

        damagePopupPrefab = CreateRuntimePrefab();
    }

    // 리소스 프리팹이 없을 때 사용할 런타임 기본 팝업을 만든다
    private DamagePopup CreateRuntimePrefab()
    {
        GameObject popupObject = new GameObject("DamagePopup");
        popupObject.transform.SetParent(transform);
        DamagePopup popup = popupObject.AddComponent<DamagePopup>();
        popupObject.SetActive(false);
        return popup;
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

    // 팝업 배치에 사용할 카메라 참조를 갱신한다
    private void RefreshTargetCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    // 같은 대상에게 연속으로 뜨는 팝업의 시작 위치를 분산한다
    private Vector3 GetStackedSpawnOffset(Transform target)
    {
        if (!settings.UseStackedSpawnOffset || target == null)
        {
            return Vector3.zero;
        }

        int targetId = target.GetInstanceID();
        float currentTime = Time.time;
        int slotIndex = ResolveStackSlot(targetId, currentTime);
        if (slotIndex <= 0)
        {
            return Vector3.zero;
        }

        Vector3 cameraRight = targetCamera != null ? targetCamera.transform.right : Vector3.right;
        int horizontalIndex = ((slotIndex + 1) / 2) * (slotIndex % 2 == 0 ? -1 : 1);
        float horizontalOffset = horizontalIndex * settings.StackHorizontalStep;
        float verticalOffset = slotIndex * settings.StackVerticalStep;
        return (cameraRight * horizontalOffset) + (Vector3.up * verticalOffset);
    }

    // 대상별 최근 팝업 상태를 기준으로 이번 팝업의 분산 슬롯을 계산한다
    private int ResolveStackSlot(int targetId, float currentTime)
    {
        PopupStackState state;
        if (!PopupStackStates.TryGetValue(targetId, out state) || currentTime - state.LastSpawnTime > settings.StackWindow)
        {
            state = new PopupStackState(0, currentTime);
            PopupStackStates[targetId] = state;
            return 0;
        }

        int maxSlots = Mathf.Max(1, settings.MaxStackSlots);
        int nextSlot = (state.SlotIndex + 1) % maxSlots;
        PopupStackStates[targetId] = new PopupStackState(nextSlot, currentTime);
        return nextSlot;
    }

    private readonly struct PopupStackState
    {
        public readonly int SlotIndex;
        public readonly float LastSpawnTime;

        // 연속 팝업 슬롯과 마지막 생성 시간을 저장한다
        public PopupStackState(int slotIndex, float lastSpawnTime)
        {
            SlotIndex = slotIndex;
            LastSpawnTime = lastSpawnTime;
        }
    }
}
