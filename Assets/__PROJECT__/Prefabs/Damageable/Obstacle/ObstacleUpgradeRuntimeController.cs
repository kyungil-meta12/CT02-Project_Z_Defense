using UnityEngine;

/// <summary>
/// 슬롯에 설치된 장애물의 정의, 현재 레벨, 업그레이드 비용 소모를 관리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Obstacle))]
public class ObstacleUpgradeRuntimeController : MonoBehaviour
{
    [SerializeField] private ObstacleDefinitionSO obstacleDefinition;
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private Obstacle targetObstacle;

    public ObstacleDefinitionSO CurrentDefinition
    {
        get
        {
            return obstacleDefinition;
        }
    }

    public int CurrentLevel
    {
        get
        {
            return level;
        }
    }

    public bool IsMaxLevelReached
    {
        get
        {
            return obstacleDefinition != null && obstacleDefinition.MaxLevel > 0 && level >= obstacleDefinition.MaxLevel;
        }
    }

    // 컴포넌트 추가 시 필요한 참조를 자동으로 수집한다
    private void Reset()
    {
        RefreshReferences();
    }

    // 인스펙터 입력값을 유효한 레벨 범위로 보정한다
    private void OnValidate()
    {
        level = Mathf.Max(1, level);
    }

    // 시작 시 현재 정의와 레벨을 장애물에 적용한다
    private void Start()
    {
        if (applyOnStart)
        {
            Apply(false);
        }
    }

    // 현재 정의와 레벨을 장애물 HP 스펙에 적용한다
    public void Apply(bool preserveHpRatio)
    {
        RefreshReferences();
        if (targetObstacle == null || obstacleDefinition == null)
        {
            return;
        }

        targetObstacle.ApplyRuntimeLevel(obstacleDefinition.ObstacleSpec, level, preserveHpRatio);
        SyncSlotStoredProgress();
    }

    // 지정한 레벨업 수량에 필요한 비용을 반환한다
    public ResourceCost[] GetUpgradeCosts(int levelAmount)
    {
        if (obstacleDefinition == null || levelAmount <= 0)
        {
            return System.Array.Empty<ResourceCost>();
        }

        int targetLevel = GetClampedLevel(level + levelAmount);
        return obstacleDefinition.GetUpgradeCosts(level, targetLevel);
    }

    // 지정한 수량만큼 업그레이드할 수 있는지 확인한다
    public bool CanUpgrade(int levelAmount)
    {
        if (levelAmount <= 0 || obstacleDefinition == null || GetClampedLevel(level + levelAmount) <= level)
        {
            return false;
        }

        if (targetObstacle != null && (!targetObstacle.IsAlive || targetObstacle.ReservedRepairer != null))
        {
            return false;
        }

        ResourceCost[] costs = GetUpgradeCosts(levelAmount);
        return ItemManager.Inst != null && ItemManager.Inst.CanAfford(costs);
    }

    // 비용을 소모한 뒤 지정한 수량만큼 업그레이드한다
    public bool TryUpgrade(int levelAmount)
    {
        if (levelAmount <= 0)
        {
            Debug.LogWarning("[장애물 업그레이드] 업그레이드 수량이 유효하지 않습니다.", this);
            return false;
        }

        RefreshReferences();
        if (obstacleDefinition == null || targetObstacle == null)
        {
            Debug.LogWarning("[장애물 업그레이드] 장애물 정의 또는 대상 장애물이 없어 업그레이드할 수 없습니다.", this);
            return false;
        }

        if (!targetObstacle.IsAlive || targetObstacle.ReservedRepairer != null)
        {
            Debug.LogWarning("[장애물 업그레이드] 파괴되었거나 수리 예약 중인 장애물은 업그레이드할 수 없습니다.", this);
            return false;
        }

        int targetLevel = GetClampedLevel(level + levelAmount);
        if (targetLevel <= level)
        {
            Debug.LogWarning("[장애물 업그레이드] 현재 레벨에서 더 이상 업그레이드할 수 없습니다.", this);
            return false;
        }

        ResourceCost[] costs = obstacleDefinition.GetUpgradeCosts(level, targetLevel);
        if (ItemManager.Inst == null || !ItemManager.Inst.TrySpend(costs))
        {
            Debug.LogWarning("[장애물 업그레이드] 재화가 부족하거나 ItemManager가 없어 업그레이드할 수 없습니다.", this);
            return false;
        }

        ObstacleBuildSlot slot = GetComponentInParent<ObstacleBuildSlot>();
        if (slot != null)
        {
            if (slot.TryApplyObstacleUpgrade(this, targetLevel, costs, out _))
            {
                return true;
            }

            ItemManager.Inst.Refund(costs);
            Debug.LogWarning("[장애물 업그레이드] 슬롯 교체 처리에 실패해 비용을 환불했습니다.", this);
            return false;
        }

        SetLevel(targetLevel, true);
        Debug.Log($"[장애물 업그레이드] 업그레이드 성공 - 대상: {obstacleDefinition.DisplayName}, 레벨: {level}", this);
        return true;
    }

    [ContextMenu("Upgrade One Level")]
    // 컨텍스트 메뉴에서 1레벨 업그레이드를 시도한다
    private void UpgradeOneLevel()
    {
        TryUpgrade(1);
    }

    // 정의와 레벨을 설정하고 장애물에 적용한다
    public void SetDefinition(ObstacleDefinitionSO definition, int level_)
    {
        obstacleDefinition = definition;
        SetLevel(level_, false);
    }

    // 현재 레벨을 설정하고 장애물에 적용한다
    public void SetLevel(int level_, bool preserveHpRatio)
    {
        level = GetClampedLevel(level_);
        Apply(preserveHpRatio);
    }

    // 지정 레벨에서 사용할 프리팹을 반환한다
    public GameObject GetPrefabForLevel(int level_)
    {
        return obstacleDefinition == null ? null : obstacleDefinition.GetPrefabForLevel(level_);
    }

    // 필요한 장애물 참조를 수집한다
    private void RefreshReferences()
    {
        if (targetObstacle == null)
        {
            targetObstacle = GetComponent<Obstacle>();
        }
    }

    // 현재 정의의 최대 레벨을 고려해 요청 레벨을 보정한다
    private int GetClampedLevel(int requestedLevel)
    {
        int clampedLevel = Mathf.Max(1, requestedLevel);
        if (obstacleDefinition != null && obstacleDefinition.MaxLevel > 0)
        {
            clampedLevel = Mathf.Min(clampedLevel, obstacleDefinition.MaxLevel);
        }

        return clampedLevel;
    }

    // 부모 슬롯이 있으면 저장된 진행 상태를 현재 레벨과 동기화한다
    private void SyncSlotStoredProgress()
    {
        ObstacleBuildSlot slot = GetComponentInParent<ObstacleBuildSlot>();
        if (slot != null)
        {
            slot.StoreObstacleProgress(obstacleDefinition, level);
        }
    }
}
