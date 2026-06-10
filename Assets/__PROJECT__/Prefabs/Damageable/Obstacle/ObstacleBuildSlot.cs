using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ObstacleBuildSlot : MonoBehaviour
{
    [Header("Defense Line")]
    [SerializeField, Min(0)] private int defenseLineIndex;
    [SerializeField, Min(0)] private int slotIndex;
    [SerializeField] private ObstacleBuildSlotType slotType;

    [Header("References")]
    [SerializeField] private Transform buildPoint;
    [SerializeField] private Collider placementHitArea;

    [Header("Runtime")]
    [SerializeField] private Obstacle currentObstacle;
    [SerializeField] private GameObject currentObstacleObject;

    private readonly List<Obstacle> obstacleCandidates = new List<Obstacle>(4);

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
        return buildEntry != null && buildEntry.ObstaclePrefab != null && buildEntry.SlotType == slotType && CanPlace;
    }

    // 빌드 항목의 프리팹을 슬롯 위치에 생성하고 점유 상태로 등록한다
    public bool TryPlace(ObstacleBuildEntrySO buildEntry, out Obstacle placedObstacle)
    {
        placedObstacle = null;

        if (!CanPlaceEntry(buildEntry))
        {
            return false;
        }

        GameObject obstacleObject = Instantiate(buildEntry.ObstaclePrefab, buildPoint);
        obstacleObject.transform.localPosition = Vector3.zero;
        obstacleObject.transform.localRotation = buildEntry.PlacementLocalRotation;

        placedObstacle = obstacleObject.GetComponent<Obstacle>();
        if (placedObstacle == null)
        {
            Debug.LogError("[ObstacleBuildSlot] 설치한 프리팹에 Obstacle 컴포넌트가 없습니다.", this);
            Destroy(obstacleObject);
            return false;
        }

        currentObstacle = placedObstacle;
        currentObstacleObject = obstacleObject;

        if (GameManager.Inst != null)
        {
            GameManager.Inst.NotifyObstaclePlaced(this, placedObstacle);
        }

        return true;
    }

    // 지정 장애물이 현재 점유 대상이면 슬롯 점유 상태를 비운다
    public void ClearCurrentObstacle(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
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

    // 파편화가 진행된 장애물은 슬롯 점유 대상으로 취급하지 않는다
    private static bool IsValidSlotObstacle(Obstacle obstacle)
    {
        return obstacle != null && !obstacle.HasFractured;
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
}
