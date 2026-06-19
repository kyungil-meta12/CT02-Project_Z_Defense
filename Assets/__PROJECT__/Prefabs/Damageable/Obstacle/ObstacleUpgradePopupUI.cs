using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 에디터에서 배치한 장애물 업그레이드 팝업 UI를 제어하고 설치된 장애물 선택과 업그레이드를 처리한다.
/// </summary>
[DisallowMultipleComponent]
public class ObstacleUpgradePopupUI : MonoBehaviour
{
    private const int SELECTION_RAYCAST_BUFFER_SIZE = 32;

    [Header("선택 설정 - 장애물 클릭 판정에 사용할 카메라와 레이어")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask selectionLayerMask = ~0;
    [SerializeField, Min(1.0f)] private float maxRayDistance = 500.0f;

    [Header("업그레이드 설정 - 버튼 1회 입력으로 올릴 레벨 수")]
    [SerializeField, Min(1)] private int levelUpAmount = 1;

    [Header("UI 참조 - 에디터에서 배치한 표시 패널과 표시 요소")]
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradeButtonText;

    private ObstaclePlacementController placementController;
    private Obstacle selectedObstacle;
    private ObstacleUpgradeRuntimeController selectedUpgradeController;
    private bool hasLoggedMissingUI;
    private readonly RaycastHit[] selectionHits = new RaycastHit[SELECTION_RAYCAST_BUFFER_SIZE];

    // 컴포넌트 추가 시 기본 참조를 자동으로 찾는다
    private void Reset()
    {
        targetCamera = Camera.main;
        placementController = FindFirstObjectByType<ObstaclePlacementController>();
        BindChildReferences();
    }

    // 게임 시작 시 선택 참조와 배치된 UI 참조를 준비한다
    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (placementController == null)
        {
            placementController = FindFirstObjectByType<ObstaclePlacementController>();
        }

        BindChildReferences();
        EnsureButtonListener();
        HidePopup();
    }

    void Start()
    {
        CameraTouchHandler.Inst.OnCameraTargetTouchEvent += OnTargetTouchEvent;
        CameraTouchHandler.Inst.OnCameraOtherTouchEvent += OnOtherTouchEvent;
    }

    void OnDestroy()
    {
        CameraTouchHandler.Inst.OnCameraTargetTouchEvent -= OnTargetTouchEvent;
        CameraTouchHandler.Inst.OnCameraOtherTouchEvent -= OnOtherTouchEvent;
    }

    // 다른 곳을 터치하면 팝업을 숨긴다.
    public void OnOtherTouchEvent()
    {
        HidePopup();
        return;
    }

    // Obstalce 터치 시
    public void OnTargetTouchEvent(RaycastHit hit)
    {
        // 타겟 터치 이벤트가 발생했는데 Obstacle 컴포넌트를 찾을 수 없다면 장애물을 터치 하지 않은 것으로 간주하고 팝업을 숨긴다.
        Obstacle hitObstacle = hit.collider.GetComponentInParent<Obstacle>();
        if (hitObstacle == null)
        {
            HidePopup();
            return;
        }
        var obstacle = hitObstacle;
        var upgradeController = hitObstacle.GetComponent<ObstacleUpgradeRuntimeController>();
        SelectObstacle(obstacle, upgradeController);
    }

    // 포인터 입력으로 장애물을 선택하거나 팝업을 닫는다
    private void Update()
    {
        if (!IsUIReady())
        {
            LogMissingUIOnce();
            return;
        }

        if (/*placementController != null && */placementController.IsPlacing)
        {
            HidePopup();
            return;
        }

        //if (!WasPrimaryPointerPressed() || IsPointerOverUI() || IsPointerInsidePopup())
        //{
        //    return;
        //}

        //if (!TryGetPrimaryPointerPosition(out Vector2 pointerPosition))
        //{
        //    return;
        //}

        //if (TrySelectObstacle(pointerPosition, out Obstacle obstacle, out ObstacleUpgradeRuntimeController upgradeController))
        //{
        //    SelectObstacle(obstacle, upgradeController);
        //    return;
        //}

        //HidePopup();
    }

    [ContextMenu("참조 다시 연결")]
    // 컨텍스트 메뉴에서 하위 UI 참조를 다시 연결한다
    public void BindChildReferences()
    {
        if (popupPanel == null)
        {
            Transform panelTransform = transform.Find("Panel");
            popupPanel = panelTransform == null ? null : panelTransform as RectTransform;
        }

        if (popupPanel == null)
        {
            return;
        }

        titleText = titleText != null ? titleText : FindChildComponent<TMP_Text>(popupPanel, "Title");
        levelText = levelText != null ? levelText : FindChildComponent<TMP_Text>(popupPanel, "Level");
        hpText = hpText != null ? hpText : FindChildComponent<TMP_Text>(popupPanel, "Hp");
        costText = costText != null ? costText : FindChildComponent<TMP_Text>(popupPanel, "Cost");
        statusText = statusText != null ? statusText : FindChildComponent<TMP_Text>(popupPanel, "Status");
        upgradeButton = upgradeButton != null ? upgradeButton : FindChildComponent<Button>(popupPanel, "UpgradeButton");
        upgradeButtonText = upgradeButtonText != null ? upgradeButtonText : upgradeButton == null ? null : upgradeButton.GetComponentInChildren<TMP_Text>(true);
        EnsureButtonListener();
    }

    // 선택된 장애물을 저장하고 팝업을 표시한다
    private void SelectObstacle(Obstacle obstacle, ObstacleUpgradeRuntimeController upgradeController)
    {
        selectedObstacle = obstacle;
        selectedUpgradeController = upgradeController;
        RefreshUI();
        ShowPopup();
    }

    // 선택된 장애물을 1회 업그레이드하고 UI를 갱신한다
    private void UpgradeSelectedObstacle()
    {
        if (selectedUpgradeController == null)
        {
            HidePopup();
            return;
        }

        selectedUpgradeController.TryUpgrade(levelUpAmount);
        if (selectedUpgradeController == null)
        {
            HidePopup();
            return;
        }

        selectedObstacle = selectedUpgradeController.GetComponent<Obstacle>();
        RefreshUI();
    }

    //// 포인터 위치에서 선택 가능한 장애물을 찾는다
    //private bool TrySelectObstacle(Vector2 pointerPosition, out Obstacle obstacle, out ObstacleUpgradeRuntimeController upgradeController)
    //{
    //    obstacle = null;
    //    upgradeController = null;

    //    if (targetCamera == null)
    //    {
    //        targetCamera = Camera.main;
    //    }

    //    if (targetCamera == null)
    //    {
    //        return false;
    //    }

    //    Ray ray = targetCamera.ScreenPointToRay(pointerPosition);
    //    int hitCount = Physics.RaycastNonAlloc(ray, selectionHits, maxRayDistance, selectionLayerMask, QueryTriggerInteraction.Collide);
    //    if (hitCount <= 0)
    //    {
    //        return false;
    //    }

    //    float nearestDistance = Mathf.Infinity;
    //    for (int i = 0; i < hitCount; i++)
    //    {
    //        RaycastHit hit = selectionHits[i];
    //        if (hit.collider == null || hit.distance >= nearestDistance)
    //        {
    //            continue;
    //        }

    //        Obstacle hitObstacle = hit.collider.GetComponentInParent<Obstacle>();
    //        if (hitObstacle == null)
    //        {
    //            continue;
    //        }

    //        obstacle = hitObstacle;
    //        upgradeController = hitObstacle.GetComponent<ObstacleUpgradeRuntimeController>();
    //        nearestDistance = hit.distance;
    //    }

    //    return obstacle != null;
    //}

    // 선택된 장애물 상태를 기준으로 팝업 텍스트와 버튼을 갱신한다
    private void RefreshUI()
    {
        if (!IsUIReady())
        {
            LogMissingUIOnce();
            return;
        }

        if (selectedObstacle == null)
        {
            HidePopup();
            return;
        }

        ObstacleDefinitionSO definition = selectedUpgradeController == null ? null : selectedUpgradeController.CurrentDefinition;
        string obstacleName = definition == null ? selectedObstacle.name : definition.DisplayName;
        int currentLevel = selectedUpgradeController == null ? 1 : selectedUpgradeController.CurrentLevel;
        int targetLevel = currentLevel + levelUpAmount;
        ResourceCost[] upgradeCosts = selectedUpgradeController == null ? System.Array.Empty<ResourceCost>() : selectedUpgradeController.GetUpgradeCosts(levelUpAmount);
        bool canUpgrade = selectedUpgradeController != null && selectedUpgradeController.CanUpgrade(levelUpAmount);
        bool prefabWillChange = definition != null &&
                                definition.GetPrefabForLevel(currentLevel) != definition.GetPrefabForLevel(targetLevel);

        titleText.text = obstacleName;
        levelText.text = FormatLevelText(currentLevel, definition);
        hpText.text = $"HP {selectedObstacle.CurrHp:0.#} / {selectedObstacle.TotalHp:0.#}";
        costText.text = "Cost: " + FormatCosts(upgradeCosts);
        statusText.text = GetStatusText(prefabWillChange);

        upgradeButton.interactable = canUpgrade;
        upgradeButtonText.text = canUpgrade ? $"Upgrade +{levelUpAmount}" : "Upgrade Unavailable";
    }

    // 현재 레벨과 최대 레벨을 UI 문자열로 변환한다
    private static string FormatLevelText(int currentLevel, ObstacleDefinitionSO definition)
    {
        if (definition == null || definition.MaxLevel <= 0)
        {
            return $"Lv. {currentLevel}";
        }

        return $"Lv. {currentLevel} / {definition.MaxLevel}";
    }

    // 현재 선택 상태의 안내 문구를 반환한다
    private string GetStatusText(bool prefabWillChange)
    {
        if (selectedObstacle == null)
        {
            return "No obstacle selected.";
        }

        if (selectedUpgradeController == null)
        {
            return "Missing ObstacleUpgradeRuntimeController.";
        }

        if (selectedUpgradeController.CurrentDefinition == null)
        {
            return "Missing ObstacleDefinitionSO.";
        }

        if (!selectedObstacle.IsAlive)
        {
            return "Destroyed obstacles must be rebuilt.";
        }

        if (selectedObstacle.ReservedRepairer != null)
        {
            return "Cannot upgrade while repair is reserved.";
        }

        if (selectedUpgradeController.IsMaxLevelReached)
        {
            return "Max level reached.";
        }

        if (InventorySystem.Inst == null)
        {
            return "Missing InventorySystem.";
        }

        if (!selectedUpgradeController.CanUpgrade(levelUpAmount))
        {
            return "Not enough resources.";
        }

        return prefabWillChange ? "Next upgrade changes appearance." : "Ready to upgrade.";
    }

    // 팝업을 표시한다
    private void ShowPopup()
    {
        if (popupPanel != null)
        {
            popupPanel.gameObject.SetActive(true);
        }
    }

    // 선택 상태를 해제하고 팝업을 숨긴다
    private void HidePopup()
    {
        selectedObstacle = null;
        selectedUpgradeController = null;

        if (popupPanel != null)
        {
            popupPanel.gameObject.SetActive(false);
        }
    }

    // 업그레이드 버튼 이벤트를 중복 없이 연결한다
    private void EnsureButtonListener()
    {
        if (upgradeButton == null)
        {
            return;
        }

        upgradeButton.onClick.RemoveListener(UpgradeSelectedObstacle);
        upgradeButton.onClick.AddListener(UpgradeSelectedObstacle);
    }

    // 팝업 구성에 필요한 참조가 유효한지 확인한다
    private bool IsUIReady()
    {
        return popupPanel != null &&
               titleText != null &&
               levelText != null &&
               hpText != null &&
               costText != null &&
               statusText != null &&
               upgradeButton != null &&
               upgradeButtonText != null;
    }

    // UI 참조 누락 경고를 한 번만 출력한다
    private void LogMissingUIOnce()
    {
        if (hasLoggedMissingUI)
        {
            return;
        }

        hasLoggedMissingUI = true;
        Debug.LogWarning("[ObstacleUpgradePopupUI] 팝업 UI 참조가 부족합니다. 에디터 메뉴로 UI를 생성하거나 '참조 다시 연결'을 실행하세요.", this);
    }

    // 지정 경로의 자식 컴포넌트를 찾는다
    private static T FindChildComponent<T>(Transform parent, string path) where T : Component
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(path);
        return child == null ? null : child.GetComponent<T>();
    }

    // 비용 배열을 UI에 표시할 문자열로 변환한다
    private static string FormatCosts(ResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
        {
            return "None";
        }

        StringBuilder builder = new StringBuilder(64);
        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(" / ");
            }

            builder.Append(GetCurrencyLabel(cost.currencyType));
            builder.Append(' ');
            builder.Append(cost.amount);
        }

        return builder.Length == 0 ? "None" : builder.ToString();
    }

    // 재화 타입을 UI 표시용 이름으로 변환한다
    private static string GetCurrencyLabel(RewardCurrencyType currencyType)
    {
        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                return "Coin";
            case RewardCurrencyType.FirePart:
                return "Fire";
            case RewardCurrencyType.SpecialPart:
                return "Special";
            default:
                return currencyType.ToString();
        }
    }

    // 현재 기본 포인터 위치를 가져온다
    private static bool TryGetPrimaryPointerPosition(out Vector2 pointerPosition)
    {
        if (Input.touchCount > 0)
        {
            pointerPosition = Input.GetTouch(0).position;
            return true;
        }

        pointerPosition = Input.mousePosition;
        return true;
    }

    // 터치 시작 또는 마우스 클릭 시작 여부를 확인한다
    private static bool WasPrimaryPointerPressed()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            return touch.phase == TouchPhase.Began;
        }

        return Input.GetMouseButtonDown(0);
    }

    // 현재 포인터가 Unity UI 위에 있는지 확인한다
    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            return EventSystem.current.IsPointerOverGameObject(touch.fingerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    // 현재 포인터가 팝업 내부에 있는지 확인한다
    private bool IsPointerInsidePopup()
    {
        if (popupPanel == null || !popupPanel.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!TryGetPrimaryPointerPosition(out Vector2 pointerPosition))
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(popupPanel, pointerPosition, null);
    }
}
