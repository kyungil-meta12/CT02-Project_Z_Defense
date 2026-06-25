using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 데미지 팝업 프리팹과 설정을 관리하고 피격 위치에 팝업을 스폰한다.
/// </summary>
public class DamagePopupSpawner : MonoBehaviour
{
    private const string DAMAGE_POPUP_SETTINGS_RESOURCE_PATH = "UI/DamagePopupSettings";
    private const string RUNTIME_SYSTEMS_CONTAINER_NAME = "RuntimeSystems";
    private const float POPUP_RATE_WINDOW_SECONDS = 1.0f;

    private static readonly Dictionary<int, PopupStackState> PopupStackStates = new Dictionary<int, PopupStackState>(128);
    private static readonly Dictionary<int, AccumulatedPopupState> AccumulatedPopupStates = new Dictionary<int, AccumulatedPopupState>(128);
    private static DamagePopupSpawner Inst;

    [SerializeField] private DamagePopupSettings settings;

    private readonly List<int> accumulationFlushKeys = new List<int>(128);
    private IDamagePopupRenderBackend activeRenderBackend;
    private Camera targetCamera;
    private float popupRateWindowStartTime;
    private int popupCountInCurrentWindow;
    private float statsWindowStartTime;
    private int statsRequestedCount;
    private int statsSuppressedCount;
    private int statsAccumulatedRequestCount;
    private int statsAccumulationMergedCount;
    private int statsRateLimitedCount;
    private int statsSpawnedCount;
    private int statsSpawnFailedCount;
    private bool hasLoggedSpawnFailure;

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
        AccumulatedPopupStates.Clear();
        popupRateWindowStartTime = Time.time;
        popupCountInCurrentWindow = 0;
        targetCamera = Camera.main;
        EnsureSettings();
        DamagePopupPolicyResolver.SetSettings(settings);
        InitializeRenderBackend();
        ResetRuntimeStats(Time.time);
        if (activeRenderBackend == null)
        {
            Debug.LogError("[DamagePopupSpawner] DNP 데미지 팝업 백엔드를 초기화하지 못했습니다. DamagePopupSettings의 DNP 프리팹 연결을 확인하세요.", this);
            return;
        }

        activeRenderBackend.Prewarm();
    }

    // 누적된 데미지 팝업 표시 시점을 갱신한다
    private void Update()
    {
        float currentTime = Time.time;
        FlushDueAccumulatedPopups(currentTime);
        LogRuntimeStatsIfNeeded(currentTime);
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

        spawner.HandleDamagePopup(target, damageInfo, targetType);
    }

    // 데미지 팝업 정책에 맞춰 즉시 표시하거나 누적한다
    private void HandleDamagePopup(Transform target, DamageInfo damageInfo, DamagePopupTargetType targetType)
    {
        RecordPopupRequest();
        switch (damageInfo.PopupPolicy)
        {
            case DamagePopupPolicy.Suppressed:
                RecordSuppressedPopup();
                return;
            case DamagePopupPolicy.Accumulate:
                if (settings.UseAccumulatedDamagePopup)
                {
                    AccumulateDamagePopup(target, damageInfo, targetType);
                    return;
                }

                SpawnDamageNow(target, damageInfo, targetType, true);
                return;
            case DamagePopupPolicy.Throttled:
                SpawnDamageNow(target, damageInfo, targetType, true);
                return;
            default:
                SpawnDamageNow(target, damageInfo, targetType, false);
                return;
        }
    }

    // 같은 대상의 누적 정책 데미지를 합산한다
    private void AccumulateDamagePopup(Transform target, DamageInfo damageInfo, DamagePopupTargetType targetType)
    {
        RecordAccumulatedRequest();
        int targetId = target.GetInstanceID();
        float currentTime = Time.time;
        AccumulatedPopupState state;
        if (!AccumulatedPopupStates.TryGetValue(targetId, out state) || state.Target == null)
        {
            state = new AccumulatedPopupState(target, targetType, damageInfo.Damage, damageInfo.PopupType, currentTime + settings.AccumulationWindow);
            AccumulatedPopupStates[targetId] = state;
            return;
        }

        RecordAccumulationMerged();
        state.AddDamage(damageInfo.Damage, damageInfo.PopupType);
        state.RefreshTarget(target, targetType);
        AccumulatedPopupStates[targetId] = state;
    }

    // 표시 시간이 된 누적 팝업을 한 번에 처리한다
    private void FlushDueAccumulatedPopups(float currentTime)
    {
        if (AccumulatedPopupStates.Count == 0)
        {
            return;
        }

        accumulationFlushKeys.Clear();
        foreach (KeyValuePair<int, AccumulatedPopupState> pair in AccumulatedPopupStates)
        {
            if (pair.Value.Target == null || currentTime >= pair.Value.FlushTime)
            {
                accumulationFlushKeys.Add(pair.Key);
            }
        }

        for (int i = 0; i < accumulationFlushKeys.Count; i++)
        {
            FlushAccumulatedPopup(accumulationFlushKeys[i]);
        }
    }

    // 지정 대상의 누적 팝업을 표시하고 버퍼에서 제거한다
    private void FlushAccumulatedPopup(int targetId)
    {
        AccumulatedPopupState state;
        if (!AccumulatedPopupStates.TryGetValue(targetId, out state))
        {
            return;
        }

        AccumulatedPopupStates.Remove(targetId);
        if (state.Target == null)
        {
            return;
        }

        DamageInfo accumulatedInfo = new DamageInfo(state.Damage, state.PopupType, DamagePopupPolicy.Throttled);
        SpawnDamageNow(state.Target, accumulatedInfo, state.TargetType, true);
    }

    // 대상 위치 위에 데미지 팝업을 즉시 표시한다
    private void SpawnDamageNow(Transform target, DamageInfo damageInfo, DamagePopupTargetType targetType, bool applyRateLimit)
    {
        if (target == null)
        {
            return;
        }

        if (applyRateLimit && !TryConsumePopupRateBudget())
        {
            RecordRateLimitedPopup();
            return;
        }

        RefreshTargetCamera();
        Vector3 spawnPosition = settings.GetSpawnPosition(target.position, targetType);
        spawnPosition += GetStackedSpawnOffset(target);
        int damageValue = settings.ShowRoundedDamage ? Mathf.RoundToInt(damageInfo.Damage) : Mathf.FloorToInt(damageInfo.Damage);
        Spawn(damageValue, spawnPosition, damageInfo.PopupType);
    }

    // 초당 팝업 표시 제한 예산을 하나 소비한다
    private bool TryConsumePopupRateBudget()
    {
        int maxPopupsPerSecond = settings.MaxPopupsPerSecond;
        if (maxPopupsPerSecond <= 0)
        {
            return true;
        }

        float currentTime = Time.time;
        if (currentTime - popupRateWindowStartTime >= POPUP_RATE_WINDOW_SECONDS)
        {
            popupRateWindowStartTime = currentTime;
            popupCountInCurrentWindow = 0;
        }

        if (popupCountInCurrentWindow >= maxPopupsPerSecond)
        {
            return false;
        }

        popupCountInCurrentWindow++;
        return true;
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

    // DNP 데미지 팝업 렌더러를 초기화한다
    private void InitializeRenderBackend()
    {
        activeRenderBackend = null;

        DnpDamagePopupBackend dnpBackend = new DnpDamagePopupBackend(settings);
        if (dnpBackend.IsAvailable)
        {
            activeRenderBackend = dnpBackend;
        }
    }

    // 데미지 숫자 팝업을 타입별 표시 설정과 함께 풀에서 가져와 표시한다
    private void Spawn(int damageValue, Vector3 position, DamagePopupType damageType)
    {
        RefreshTargetCamera();
        if (activeRenderBackend != null && activeRenderBackend.TrySpawn(damageValue, position, damageType, targetCamera))
        {
            RecordSpawnedPopup();
            return;
        }

        RecordSpawnFailedPopup();
        if (!hasLoggedSpawnFailure)
        {
            hasLoggedSpawnFailure = true;
            Debug.LogWarning("[DamagePopupSpawner] DNP 데미지 팝업 생성에 실패했습니다. DNP 프리팹과 풀 설정을 확인하세요.", this);
        }
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

    // 런타임 계측 카운터를 초기화한다
    private void ResetRuntimeStats(float currentTime)
    {
        statsWindowStartTime = currentTime;
        statsRequestedCount = 0;
        statsSuppressedCount = 0;
        statsAccumulatedRequestCount = 0;
        statsAccumulationMergedCount = 0;
        statsRateLimitedCount = 0;
        statsSpawnedCount = 0;
        statsSpawnFailedCount = 0;
    }

    // 팝업 요청 수를 기록한다
    private void RecordPopupRequest()
    {
        if (!settings.EnableRuntimeStats)
        {
            return;
        }

        statsRequestedCount++;
    }

    // 표시 억제된 팝업 수를 기록한다
    private void RecordSuppressedPopup()
    {
        if (!settings.EnableRuntimeStats)
        {
            return;
        }

        statsSuppressedCount++;
    }

    // 누적 정책으로 접수된 팝업 수를 기록한다
    private void RecordAccumulatedRequest()
    {
        if (!settings.EnableRuntimeStats)
        {
            return;
        }

        statsAccumulatedRequestCount++;
    }

    // 기존 누적 상태에 합산된 팝업 수를 기록한다
    private void RecordAccumulationMerged()
    {
        if (!settings.EnableRuntimeStats)
        {
            return;
        }

        statsAccumulationMergedCount++;
    }

    // 초당 표시량 제한으로 버린 팝업 수를 기록한다
    private void RecordRateLimitedPopup()
    {
        if (!settings.EnableRuntimeStats)
        {
            return;
        }

        statsRateLimitedCount++;
    }

    // 실제 생성된 팝업 수를 기록한다
    private void RecordSpawnedPopup()
    {
        if (!settings.EnableRuntimeStats)
        {
            return;
        }

        statsSpawnedCount++;
    }

    // 백엔드 생성 실패 팝업 수를 기록한다
    private void RecordSpawnFailedPopup()
    {
        if (!settings.EnableRuntimeStats)
        {
            return;
        }

        statsSpawnFailedCount++;
    }

    // 설정된 간격마다 데미지 팝업 계측 로그를 출력한다
    private void LogRuntimeStatsIfNeeded(float currentTime)
    {
        if (!settings.EnableRuntimeStats)
        {
            return;
        }

        float interval = settings.RuntimeStatsLogInterval;
        if (currentTime - statsWindowStartTime < interval)
        {
            return;
        }

        Debug.Log($"[DamagePopupSpawner] 팝업 계측 {interval:0.##}초 - 요청:{statsRequestedCount}, 생성:{statsSpawnedCount}, 생성실패:{statsSpawnFailedCount}, 제한폐기:{statsRateLimitedCount}, 억제:{statsSuppressedCount}, 누적요청:{statsAccumulatedRequestCount}, 누적합산:{statsAccumulationMergedCount}, 대기타겟:{AccumulatedPopupStates.Count}", this);
        ResetRuntimeStats(currentTime);
    }

    private struct AccumulatedPopupState
    {
        public Transform Target;
        public DamagePopupTargetType TargetType;
        public float Damage;
        public DamagePopupType PopupType;
        public float FlushTime;

        // 누적 팝업 상태를 초기화한다
        public AccumulatedPopupState(Transform target, DamagePopupTargetType targetType, float damage, DamagePopupType popupType, float flushTime)
        {
            Target = target;
            TargetType = targetType;
            Damage = Mathf.Max(0.0f, damage);
            PopupType = popupType;
            FlushTime = flushTime;
        }

        // 누적 데미지와 더 높은 우선순위의 팝업 타입을 반영한다
        public void AddDamage(float damage, DamagePopupType popupType)
        {
            Damage += Mathf.Max(0.0f, damage);
            if (DamagePopupSpawner.GetPopupTypePriority(popupType) > DamagePopupSpawner.GetPopupTypePriority(PopupType))
            {
                PopupType = popupType;
            }
        }

        // 재사용 대상 참조와 대상 종류를 최신 상태로 갱신한다
        public void RefreshTarget(Transform target, DamagePopupTargetType targetType)
        {
            Target = target;
            TargetType = targetType;
        }
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

    // 팝업 타입 우선순위를 반환한다
    private static int GetPopupTypePriority(DamagePopupType popupType)
    {
        switch (popupType)
        {
            case DamagePopupType.Heavy:
                return 2;
            case DamagePopupType.Critical:
                return 1;
            default:
                return 0;
        }
    }
}

