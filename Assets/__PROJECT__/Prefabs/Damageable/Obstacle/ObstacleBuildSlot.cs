using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 방어선 장애물/게이트 설치 슬롯의 점유 상태와 배치 비용 소비를 관리한다.
/// </summary>
[DisallowMultipleComponent]
public class ObstacleBuildSlot : MonoBehaviour
{
    [Header("방어선")]
    [SerializeField, Min(0)] private int defenseLineIndex;
    [SerializeField, Min(0)] private int slotIndex;
    [SerializeField] private ObstacleBuildSlotType slotType;

    [Header("참조")]
    [SerializeField] private Transform buildPoint;
    [SerializeField] private Collider placementHitArea;

    [Header("런타임")]
    [SerializeField] private Obstacle currentObstacle;
    [SerializeField] private GameObject currentObstacleObject;
    [SerializeField] private bool logPlacementResults = true;

    [Header("저장 진행도")]
    [SerializeField] private ObstacleBuildEntrySO storedBuildEntry;
    [SerializeField] private ObstacleDefinitionSO storedObstacleDefinition;
    [SerializeField, Min(1)] private int storedLevel = 1;
    [SerializeField] private bool hasStoredProgress;

    private readonly List<Obstacle> obstacleCandidates = new List<Obstacle>(4);
    private bool isCreatingSlotObstacle;

    public int DefenseLineIndex
    {
        get
        {
            return defenseLineIndex;
        }
    }

    public int SlotIndex
    {
        get
        {
            return slotIndex;
        }
    }

    public ObstacleBuildSlotType SlotType
    {
        get
        {
            return slotType;
        }
    }

    public Transform BuildPoint
    {
        get
        {
            return buildPoint;
        }
    }

    public Collider PlacementHitArea
    {
        get
        {
            return placementHitArea;
        }
    }

    public Obstacle CurrentObstacle
    {
        get
        {
            RefreshCurrentObstacleReference();
            return currentObstacle;
        }
    }

    public ObstacleBuildEntrySO StoredBuildEntry => storedBuildEntry;

    public bool HasStoredProgressForEntry(ObstacleBuildEntrySO entry)
    {
        return hasStoredProgress && storedBuildEntry == entry;
    }

    public bool HasAliveObstacle
    {
        get
        {
            Obstacle obstacle = CurrentObstacle;
            return obstacle != null && obstacle.IsAlive;
        }
    }

    public bool CanPlace
    {
        get
        {
            return buildPoint != null && CurrentObstacle == null;
        }
    }

    public int StoredLevel
    {
        get
        {
            return Mathf.Max(1, storedLevel);
        }
    }

    // 배치 항목과 저장된 진행 상태를 기준으로 외부 UI가 사용할 설치 레벨을 반환한다
    public int GetPlacementLevelForEntry(ObstacleBuildEntrySO buildEntry)
    {
        return GetPlacementLevel(buildEntry);
    }

    // 컴포넌트를 추가하거나 리셋할 때 기본 슬롯 참조를 자동 연결한다
    private void Reset()
    {
        AutoBindReferences();
    }

    // 슬롯 활성화 전에 참조와 초기 점유 상태를 준비한다
    private void Awake()
    {
        AutoBindReferences();
        RefreshCurrentObstacleReference();
    }

    // 슬롯이 켜질 때 GameManager에 방어선 슬롯으로 등록한다
    private void OnEnable()
    {
        if (GameManager.Inst != null)
        {
            GameManager.Inst.RegisterDefenseLineSlot(this);
        }
    }

    // GameManager 생성 순서가 늦은 경우를 보완해 슬롯을 다시 등록한다
    private void Start()
    {
        if (GameManager.Inst != null)
        {
            GameManager.Inst.RegisterDefenseLineSlot(this);
        }
    }

    // 슬롯이 꺼질 때 GameManager 등록을 해제한다
    private void OnDisable()
    {
        if (GameManager.Inst != null)
        {
            GameManager.Inst.UnregisterDefenseLineSlot(this);
        }
    }

    // 레이캐스트된 콜라이더가 이 슬롯의 설치 판정 영역인지 확인한다
    public bool OwnsHitArea(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        return hitCollider == placementHitArea || hitCollider.transform.IsChildOf(transform);
    }

    // 지정 빌드 항목을 현재 슬롯에 설치할 수 있는지 확인한다
    public bool CanPlaceEntry(ObstacleBuildEntrySO buildEntry)
    {
        // 프리뷰 갱신 중 매 프레임 호출되므로 로그와 문자열 포맷팅 없이 빠른 판정만 수행한다.
        return CanPlaceEntryInternal(buildEntry);
    }

    // 빌드 항목의 프리팹을 슬롯 위치에 생성하고 점유 상태로 등록한다
    public bool TryPlace(ObstacleBuildEntrySO buildEntry, out Obstacle placedObstacle)
    {
        placedObstacle = null;

        // 실제 배치 확정 시점에서만 상세 실패 사유를 만든다. 프리뷰 경로에서 만들면 콘솔 스팸과 GC 할당이 발생한다.
        string failureReason = GetPlacementFailureReason(buildEntry);
        if (!string.IsNullOrEmpty(failureReason))
        {
            LogPlacementFailed(buildEntry, failureReason);
            return false;
        }

        if (InventorySystem.Inst == null)
        {
            Debug.LogError("[ObstacleBuildSlot] InventorySystem이 없어 장애물을 배치할 수 없습니다.", this);
            return false;
        }

        bool isRebuild = HasStoredProgressForEntry(buildEntry);
        int firstPlacementCount = GameManager.Inst != null ? GameManager.Inst.GetFirstPlacementCount(buildEntry) : 0;
        ResourceCost[] buildCosts = buildEntry.GetPlacementCosts(firstPlacementCount, isRebuild);
        // 장애물 배치도 터렛 경제와 같은 ResourceCost[] 파이프라인을 사용한다. Coin 전용 fallback은 검증 혼선을 만들기 때문에 사용하지 않는다.
        if (!InventorySystem.Inst.TrySpend(buildCosts))
        {
            LogPlacementFailed(buildEntry, $"배치 비용 지불에 실패했습니다. 필요 비용: {FormatCosts(buildCosts)}, 보유 재화: {FormatWallet()}");
            return false;
        }

        int placementLevel = GetPlacementLevel(buildEntry);
        Quaternion placementLocalRotation = GetPlacementLocalRotation(buildEntry, buildEntry.ObstacleDefinition, placementLevel);
        if (!TryCreatePlacedObstacle(
                buildEntry.GetObstaclePrefabForLevel(placementLevel),
                placementLocalRotation,
                buildEntry.ObstacleDefinition,
                placementLevel,
                1.0f,
                out placedObstacle))
        {
            // 이미 지불한 배치 비용을 환불한다
            InventorySystem.Inst.Refund(buildCosts);
            LogPlacementFailed(buildEntry, $"생성된 프리팹에 Obstacle 컴포넌트가 없어 비용을 환불했습니다. 필요 컴포넌트: Obstacle, 프리팹: {buildEntry.ObstaclePrefab.name}");
            return false;
        }

        StoreObstacleProgress(buildEntry, placementLevel);

        LogPlacementSucceeded(buildEntry, placedObstacle, buildCosts);

        if (GameManager.Inst != null)
        {
            GameManager.Inst.NotifyObstaclePlaced(this, placedObstacle);
        }

        return true;
    }

    // 현재 슬롯에 지정 빌드 항목을 배치할 수 없는 이유를 반환한다
    private string GetPlacementFailureReason(ObstacleBuildEntrySO buildEntry)
    {
        if (buildEntry == null)
        {
            return "배치 항목이 비어 있습니다.";
        }

        int placementLevel = GetPlacementLevel(buildEntry);
        if (buildEntry.GetObstaclePrefabForLevel(placementLevel) == null)
        {
            return $"배치 항목 '{buildEntry.DisplayName}'에 장애물 프리팹이 없습니다.";
        }

        if (buildEntry.SlotType != slotType)
        {
            return $"슬롯 타입이 맞지 않습니다. 슬롯 타입: {slotType}, 빌드 타입: {buildEntry.SlotType}";
        }

        if (buildPoint == null)
        {
            return "BuildPoint가 없어 배치 위치를 결정할 수 없습니다.";
        }

        if (GameManager.Inst != null && !GameManager.Inst.CanPlaceObstacleAtDefenseLine(defenseLineIndex))
        {
            return $"현재 복구된 방어선 바로 앞 방어선에만 설치할 수 있습니다. 슬롯 방어선: {defenseLineIndex}";
        }

        Obstacle obstacle = CurrentObstacle;
        if (obstacle != null)
        {
            return $"설치 장소에 이미 '{GetObstacleName(obstacle)}'이 있습니다.";
        }

        if (InventorySystem.Inst == null)
        {
            return "ItemManager가 없어 재화 보유량을 확인할 수 없습니다.";
        }

        bool isRebuildCheck = HasStoredProgressForEntry(buildEntry);
        int placementCountCheck = GameManager.Inst != null ? GameManager.Inst.GetFirstPlacementCount(buildEntry) : 0;
        ResourceCost[] buildCosts = buildEntry.GetPlacementCosts(placementCountCheck, isRebuildCheck);
        if (!InventorySystem.Inst.CanAfford(buildCosts))
        {
            return $"재화가 부족합니다. 필요 비용: {FormatCosts(buildCosts)}, 보유 재화: {FormatWallet()}";
        }

        return string.Empty;
    }

    // 프리뷰 갱신 중 문자열 할당 없이 배치 가능 여부만 확인한다
    private bool CanPlaceEntryInternal(ObstacleBuildEntrySO buildEntry)
    {
        if (buildEntry == null)
        {
            return false;
        }

        int placementLevel = GetPlacementLevel(buildEntry);
        if (buildEntry.GetObstaclePrefabForLevel(placementLevel) == null)
        {
            return false;
        }

        if (buildEntry.SlotType != slotType || buildPoint == null)
        {
            return false;
        }

        if (GameManager.Inst != null && !GameManager.Inst.CanPlaceObstacleAtDefenseLine(defenseLineIndex))
        {
            return false;
        }

        if (CurrentObstacle != null || InventorySystem.Inst == null)
        {
            return false;
        }

        bool isRebuild = HasStoredProgressForEntry(buildEntry);
        int count = GameManager.Inst != null ? GameManager.Inst.GetFirstPlacementCount(buildEntry) : 0;
        return InventorySystem.Inst.CanAfford(buildEntry.GetPlacementCosts(count, isRebuild));
    }

    // 장애물 배치 실패 사유를 콘솔에 출력하고, 플레이어에게도 경고 팝업으로 알린다
    private void LogPlacementFailed(ObstacleBuildEntrySO buildEntry, string reason)
    {
        if (logPlacementResults)
        {
            Debug.LogWarning($"[ObstacleBuildSlot] 배치 실패 - 슬롯: {name}, 항목: {GetBuildEntryName(buildEntry)}, 사유: {reason}", this);
        }

        WarningPopupManager.ShowWarning("장애물 설치 실패");
    }

    // 장애물 배치 성공 결과를 콘솔에 출력하고, 플레이어에게도 완료 팝업으로 알린다
    private void LogPlacementSucceeded(ObstacleBuildEntrySO buildEntry, Obstacle placedObstacle, ResourceCost[] buildCosts)
    {
        if (logPlacementResults)
        {
            Debug.Log($"[ObstacleBuildSlot] 배치 성공 - 슬롯: {name}, 항목: {GetBuildEntryName(buildEntry)}, 설치 대상: {GetObstacleName(placedObstacle)}, 비용: {FormatCosts(buildCosts)}, 남은 재화: {FormatWallet()}", this);
        }

        WarningPopupManager.ShowWarning("장애물 설치 성공");
    }

    // 빌드 항목의 로그용 이름을 반환한다
    private static string GetBuildEntryName(ObstacleBuildEntrySO buildEntry)
    {
        return buildEntry == null ? "없음" : buildEntry.DisplayName;
    }

    // 장애물의 로그용 이름을 반환한다
    private static string GetObstacleName(Obstacle obstacle)
    {
        return obstacle == null ? "없음" : obstacle.name;
    }

    // 비용 배열을 로그에 표시할 문자열로 변환한다
    private static string FormatCosts(ResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
        {
            return "없음";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(GetCurrencyLabel(cost.currencyType));
            builder.Append(" ");
            builder.Append(cost.amount);
        }

        return builder.Length == 0 ? "없음" : builder.ToString();
    }

    // 현재 지갑 상태를 로그에 표시할 문자열로 변환한다
    private static string FormatWallet()
    {
        if (InventorySystem.Inst == null)
        {
            return "InventorySystem 없음";
        }

        // 현재 존재하는 재화에 대해서만 출력한다.
        string fmt = "";
        foreach(RewardCurrencyType type in InventorySystem.Inst.Types)
        {
            if(InventorySystem.Inst.HasItem(type))
            {
                fmt += InventorySystem.Inst.GetFormatString(type) + "\n";
            }
        }

        return fmt;
    }

    // 재화 종류를 로그 표시용 짧은 이름으로 변환한다
    private static string GetCurrencyLabel(RewardCurrencyType currencyType)
    {
        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                return "Coin";
            default:
                return currencyType.ToString();
        }
    }

    // 지정 장애물이 현재 점유 대상이면 슬롯 점유 상태를 비운다
    public void ClearCurrentObstacle(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        if (obstacle.HasFractured)
        {
            StoreObstacleProgress(obstacle);
        }

        GameObject obstacleObject = obstacle.gameObject;
        if (currentObstacle == obstacle || currentObstacleObject == obstacleObject)
        {
            currentObstacle = null;
            currentObstacleObject = null;
        }
    }

    // 외부에서 전달된 장애물을 슬롯의 현재 점유 대상으로 동기화한다
    public void SetCurrentObstacle(Obstacle obstacle)
    {
        if (!IsValidSlotObstacle(obstacle))
        {
            currentObstacle = null;
            currentObstacleObject = null;
            return;
        }

        currentObstacle = obstacle;
        currentObstacleObject = obstacle.gameObject;

        if (!isCreatingSlotObstacle)
        {
            StoreObstacleProgress(obstacle);
        }
    }

    // 현재 슬롯의 저장 레벨과 정의를 지정 값으로 갱신한다
    public void StoreObstacleProgress(ObstacleDefinitionSO definition, int level)
    {
        StoreObstacleProgress(null, definition, level);
    }

    // 현재 슬롯의 저장 빌드 항목과 설치 진행 상태를 지정 값으로 갱신한다
    public void StoreObstacleProgress(ObstacleBuildEntrySO buildEntry, int level)
    {
        if (buildEntry == null)
        {
            return;
        }

        StoreObstacleProgress(buildEntry, buildEntry.ObstacleDefinition, level);
    }

    // 현재 슬롯의 저장 빌드 항목, 정의, 레벨을 갱신한다
    private void StoreObstacleProgress(ObstacleBuildEntrySO buildEntry, ObstacleDefinitionSO definition, int level)
    {
        if (definition == null)
        {
            return;
        }

        if (buildEntry != null)
        {
            storedBuildEntry = buildEntry;
        }

        storedObstacleDefinition = definition;
        storedLevel = Mathf.Max(1, level);
        hasStoredProgress = true;
    }

    // 현재 슬롯의 저장 레벨과 정의를 장애물 런타임 컨트롤러에서 가져와 갱신한다
    public void StoreObstacleProgress(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        ObstacleUpgradeRuntimeController upgradeController = obstacle.GetComponent<ObstacleUpgradeRuntimeController>();
        if (upgradeController == null || upgradeController.CurrentDefinition == null)
        {
            return;
        }

        StoreObstacleProgress(null, upgradeController.CurrentDefinition, upgradeController.CurrentLevel);
    }

    // 현재 장애물에 업그레이드 레벨을 적용하고 필요하면 레벨별 프리팹으로 교체한다
    public bool TryApplyObstacleUpgrade(ObstacleUpgradeRuntimeController upgradeController, int targetLevel, ResourceCost[] spentCosts, out Obstacle upgradedObstacle)
    {
        upgradedObstacle = null;
        if (upgradeController == null || upgradeController.CurrentDefinition == null)
        {
            return false;
        }

        Obstacle obstacle = upgradeController.GetComponent<Obstacle>();
        if (obstacle == null || CurrentObstacle != obstacle || buildPoint == null)
        {
            return false;
        }

        ObstacleDefinitionSO definition = upgradeController.CurrentDefinition;
        int safeTargetLevel = Mathf.Max(1, targetLevel);
        GameObject targetPrefab = definition.GetPrefabForLevel(safeTargetLevel);
        if (targetPrefab == null)
        {
            return false;
        }

        GameObject currentLevelPrefab = definition.GetPrefabForLevel(upgradeController.CurrentLevel);
        if (currentLevelPrefab == targetPrefab)
        {
            upgradeController.SetLevel(safeTargetLevel, true);
            upgradedObstacle = obstacle;
            StoreObstacleProgress(null, definition, safeTargetLevel);
            Debug.Log($"[ObstacleBuildSlot] 업그레이드 성공 - 슬롯: {name}, 대상: {definition.DisplayName}, 레벨: {safeTargetLevel}, 비용: {FormatCosts(spentCosts)}, 남은 재화: {FormatWallet()}", this);
            return true;
        }

        float hpRatio = obstacle.TotalHp > 0.0f ? Mathf.Clamp01(obstacle.CurrHp / obstacle.TotalHp) : 1.0f;
        Quaternion placementLocalRotation = GetPlacementLocalRotation(storedBuildEntry, definition, safeTargetLevel);
        if (!TryCreatePlacedObstacle(targetPrefab, placementLocalRotation, definition, safeTargetLevel, hpRatio, out upgradedObstacle))
        {
            return false;
        }
        
        StoreObstacleProgress(null, definition, safeTargetLevel);

        Destroy(obstacle.gameObject);
        Debug.Log($"[ObstacleBuildSlot] 업그레이드 프리팹 교체 성공 - 슬롯: {name}, 대상: {definition.DisplayName}, 레벨: {safeTargetLevel}, 비용: {FormatCosts(spentCosts)}, 남은 재화: {FormatWallet()}", this);
        return true;
    }

    // 저장된 장애물 진행도를 사용해 비용 없이 빈 슬롯을 다시 설치한다
    public bool TryRebuildStoredObstacleWithoutCost(out Obstacle rebuiltObstacle)
    {
        rebuiltObstacle = null;
        RefreshCurrentObstacleReference();

        if (CurrentObstacle != null)
        {
            rebuiltObstacle = CurrentObstacle;
            RestoreCurrentObstacleHealth(rebuiltObstacle);
            return true;
        }

        if (!hasStoredProgress || storedObstacleDefinition == null || buildPoint == null)
        {
            return false;
        }

        int rebuildLevel = Mathf.Max(1, storedLevel);
        GameObject obstaclePrefab = GetStoredObstaclePrefab(rebuildLevel);
        if (obstaclePrefab == null)
        {
            Debug.LogWarning("[ObstacleBuildSlot] 저장된 장애물 프리팹이 없어 재배치할 수 없습니다.", this);
            return false;
        }

        if (!TryCreatePlacedObstacle(
                obstaclePrefab,
                GetPlacementLocalRotation(storedBuildEntry, storedObstacleDefinition, rebuildLevel),
                storedObstacleDefinition,
                rebuildLevel,
                1.0f,
                out rebuiltObstacle))
        {
            return false;
        }

        if (GameManager.Inst != null)
        {
            GameManager.Inst.NotifyObstaclePlaced(this, rebuiltObstacle);
        }

        return true;
    }

    // 설치와 재배치가 같은 방식으로 프리팹 생성, 로컬 회전 적용, 런타임 진행도 적용, 슬롯 점유 갱신을 수행한다
    private bool TryCreatePlacedObstacle(GameObject obstaclePrefab, Quaternion placementLocalRotation, ObstacleDefinitionSO definition, int level, float hpRatio, out Obstacle placedObstacle)
    {
        placedObstacle = null;
        if (obstaclePrefab == null || buildPoint == null)
        {
            return false;
        }

        isCreatingSlotObstacle = true;
        GameObject obstacleObject = Instantiate(obstaclePrefab, buildPoint);
        obstacleObject.transform.localPosition = Vector3.zero;
        obstacleObject.transform.localRotation = placementLocalRotation;

        placedObstacle = obstacleObject.GetComponent<Obstacle>();
        if (placedObstacle == null)
        {
            isCreatingSlotObstacle = false;
            Debug.LogError("[ObstacleBuildSlot] 배치 프리팹에 Obstacle 컴포넌트가 없습니다.", this);
            Destroy(obstacleObject);
            return false;
        }

        ApplyPlacedObstacleProgress(definition, level, placedObstacle, hpRatio);
        currentObstacle = placedObstacle;
        currentObstacleObject = obstacleObject;
        isCreatingSlotObstacle = false;
        return true;
    }

    // 저장된 빌드 항목 또는 정의를 기준으로 재배치할 프리팹을 반환한다
    private GameObject GetStoredObstaclePrefab(int level)
    {
        if (storedBuildEntry != null)
        {
            return storedBuildEntry.GetObstaclePrefabForLevel(level);
        }

        return storedObstacleDefinition == null ? null : storedObstacleDefinition.GetPrefabForLevel(level);
    }

    // 현재 점유 장애물을 저장 진행도 기준으로 full HP 상태로 복구한다
    private void RestoreCurrentObstacleHealth(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        ObstacleUpgradeRuntimeController upgradeController = obstacle.GetComponent<ObstacleUpgradeRuntimeController>();
        if (upgradeController != null && upgradeController.CurrentDefinition != null)
        {
            int currentLevel = Mathf.Max(1, upgradeController.CurrentLevel);
            StoreObstacleProgress(null, upgradeController.CurrentDefinition, currentLevel);
            upgradeController.SetLevel(currentLevel, false);
            obstacle.transform.localRotation = GetPlacementLocalRotation(storedBuildEntry, upgradeController.CurrentDefinition, currentLevel);
            obstacle.ApplyRuntimeLevel(upgradeController.CurrentDefinition.ObstacleSpec, currentLevel, 1.0f);
            return;
        }

        if (obstacle.spec != null)
        {
            obstacle.ApplyRuntimeLevel(obstacle.spec, Mathf.Max(1, obstacle.spec.level), 1.0f);
        }
    }

    // 저장된 빌드 항목 또는 정의를 기준으로 고정 배치 로컬 회전을 반환한다
    private static Quaternion GetPlacementLocalRotation(ObstacleBuildEntrySO buildEntry, ObstacleDefinitionSO definition, int level)
    {
        if (buildEntry != null)
        {
            return buildEntry.GetPlacementLocalRotationForLevel(level);
        }

        return definition == null ? Quaternion.identity : definition.GetPlacementLocalRotationForLevel(level);
    }

    // 슬롯 하위 오브젝트에서 유효한 현재 장애물 참조를 다시 찾는다
    public void RefreshCurrentObstacleReference()
    {
        if (currentObstacle != null)
        {
            if (IsValidSlotObstacle(currentObstacle))
            {
                currentObstacleObject = currentObstacle.gameObject;
                return;
            }

            currentObstacle = null;
            currentObstacleObject = null;
        }

        if (currentObstacleObject != null)
        {
            Obstacle objectObstacle = currentObstacleObject.GetComponent<Obstacle>();
            if (IsValidSlotObstacle(objectObstacle))
            {
                currentObstacle = objectObstacle;
                return;
            }

            currentObstacleObject = null;
        }

        if (buildPoint == null)
        {
            return;
        }

        obstacleCandidates.Clear();
        buildPoint.GetComponentsInChildren(true, obstacleCandidates);
        for (int i = 0; i < obstacleCandidates.Count; i++)
        {
            Obstacle candidate = obstacleCandidates[i];
            if (!IsValidSlotObstacle(candidate))
            {
                continue;
            }

            currentObstacle = candidate;
            currentObstacleObject = candidate.gameObject;
            obstacleCandidates.Clear();
            return;
        }

        obstacleCandidates.Clear();
        currentObstacle = null;
        currentObstacleObject = null;
    }

    // 파편화가 진행된 장애물과 비활성화된 프리뷰는 슬롯 점유 대상으로 취급하지 않는다
    private static bool IsValidSlotObstacle(Obstacle obstacle)
    {
        return obstacle != null && obstacle.enabled && !obstacle.HasFractured;
    }

    // 슬롯 하위의 BuildPoint와 PlacementHitArea 참조를 자동으로 찾는다
    private void AutoBindReferences()
    {
        if (buildPoint == null)
        {
            Transform foundBuildPoint = transform.Find("BuildPoint");
            if (foundBuildPoint != null)
            {
                buildPoint = foundBuildPoint;
            }
        }

        if (placementHitArea == null)
        {
            Transform foundHitArea = transform.Find("PlacementHitArea");
            if (foundHitArea != null)
            {
                placementHitArea = foundHitArea.GetComponent<Collider>();
            }
        }
    }

    // 배치 항목과 저장된 진행 상태를 기준으로 설치 레벨을 결정한다
    private int GetPlacementLevel(ObstacleBuildEntrySO buildEntry)
    {
        if (buildEntry == null || buildEntry.ObstacleDefinition == null)
        {
            return 1;
        }

        if (!hasStoredProgress || storedObstacleDefinition != buildEntry.ObstacleDefinition)
        {
            return 1;
        }

        return Mathf.Max(1, storedLevel);
    }

    // 설치된 장애물에 정의와 계승 레벨, 체력 비율을 적용한다
    private void ApplyPlacedObstacleProgress(ObstacleDefinitionSO definition, int level, Obstacle placedObstacle, float hpRatio)
    {
        if (placedObstacle == null)
        {
            return;
        }

        if (definition == null)
        {
            if (placedObstacle.spec != null)
            {
                placedObstacle.ApplyRuntimeLevel(placedObstacle.spec, Mathf.Max(1, placedObstacle.spec.level), hpRatio);
            }

            return;
        }

        ObstacleUpgradeRuntimeController upgradeController = placedObstacle.GetComponent<ObstacleUpgradeRuntimeController>();
        if (upgradeController == null)
        {
            upgradeController = placedObstacle.gameObject.AddComponent<ObstacleUpgradeRuntimeController>();
        }

        upgradeController.SetDefinition(definition, level);
        placedObstacle.ApplyRuntimeLevel(definition.ObstacleSpec, level, hpRatio);
    }
}
