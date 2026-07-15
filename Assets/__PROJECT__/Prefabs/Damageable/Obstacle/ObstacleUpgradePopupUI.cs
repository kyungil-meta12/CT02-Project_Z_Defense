using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 에디터에서 배치한 장애물 업그레이드 팝업 UI를 제어하고 설치된 장애물 선택과 업그레이드를 처리한다.
/// </summary>
[DisallowMultipleComponent]
public class ObstacleUpgradePopupUI : MonoBehaviour
{
    private const float HOLD_UPGRADE_START_DELAY = 0.5f;
    private const float HOLD_UPGRADE_RAMP_DURATION = 1.0f;
    private const float HOLD_UPGRADE_START_RATE = 4.0f;
    private const float HOLD_UPGRADE_MAX_RATE = 60.0f;
    private const string INSUFFICIENT_COST_COLOR = "#FF4040";
    private const float UI_AUTO_REFRESH_INTERVAL = 1.0f;

    [Header("업그레이드 설정 - 버튼 1회 입력으로 올릴 레벨 수")]
    [SerializeField, Min(1)] private int levelUpAmount = 1;

    [Header("UI 참조 - 에디터에서 배치한 표시 패널과 표시 요소")]
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private Button backgroundButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradeButtonText;
    [SerializeField] private EventTrigger upgradeButtonEventTrigger;

    private ObstaclePlacementController placementController;
    private Obstacle selectedObstacle;
    private ObstacleUpgradeRuntimeController selectedUpgradeController;
    private bool hasLoggedMissingUI;
    private bool hasSubscribedCameraTouch;
    private float uiRefreshTimer;
    private EventTrigger.Entry upgradePointerDownEntry;
    private EventTrigger.Entry upgradePointerUpEntry;
    private EventTrigger.Entry upgradePointerExitEntry;
    private EventTrigger.Entry upgradeCancelEntry;
    private bool isUpgradeHolding;
    private bool hasUpgradeHoldRepeatStarted;
    private float upgradeHoldElapsedTime;
    private float upgradeHoldRepeatElapsedTime;
    private float upgradeHoldAccumulator;

    // 컴포넌트 추가 시 기본 참조를 자동으로 찾는다
    private void Reset()
    {
        placementController = FindFirstObjectByType<ObstaclePlacementController>();
        BindChildReferences();
    }

    // 게임 시작 시 선택 참조와 배치된 UI 참조를 준비한다
    private void Awake()
    {
        if (placementController == null)
        {
            placementController = FindFirstObjectByType<ObstaclePlacementController>();
        }

        BindChildReferences();
        EnsureButtonListener();
        HidePopup();
    }

    // 활성화될 때 카메라 터치 이벤트를 구독한다
    private void OnEnable()
    {
        SubscribeCameraTouchEvent();
    }

    // 비활성화될 때 카메라 터치 이벤트를 해제한다
    private void OnDisable()
    {
        UnsubscribeCameraTouchEvent();
        StopUpgradeHold();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnsubscribeCameraTouchEvent();
        UnbindButtonListeners();
    }

    // 배치 상태와 UI 참조 유효성을 갱신한다
    private void Update()
    {
        SubscribeCameraTouchEvent();

        if (!IsUIReady())
        {
            LogMissingUIOnce();
            return;
        }

        if (placementController != null && placementController.IsPlacing)
        {
            HidePopup();
            return;
        }

        UpdateAutoRefreshTimer();
        UpdateUpgradeHold();
    }

    // 선택된 장애물이 있는 동안 일정 간격으로 체력과 재화 표시를 자동 갱신한다
    private void UpdateAutoRefreshTimer()
    {
        if (selectedObstacle == null)
        {
            uiRefreshTimer = 0.0f;
            return;
        }

        uiRefreshTimer += Time.unscaledDeltaTime;
        if (uiRefreshTimer < UI_AUTO_REFRESH_INTERVAL)
        {
            return;
        }

        uiRefreshTimer = 0.0f;
        RefreshUI();
    }

    // 누르고 있는 동안 업그레이드 반복 속도를 점진적으로 증가시킨다
    private void UpdateUpgradeHold()
    {
        if (!isUpgradeHolding)
        {
            return;
        }

        if (!CanProcessUpgradeInput())
        {
            StopUpgradeHold();
            return;
        }

        upgradeHoldElapsedTime += Time.unscaledDeltaTime;
        if (!hasUpgradeHoldRepeatStarted)
        {
            if (upgradeHoldElapsedTime < HOLD_UPGRADE_START_DELAY)
            {
                return;
            }

            hasUpgradeHoldRepeatStarted = true;
            upgradeHoldRepeatElapsedTime = 0.0f;
            upgradeHoldAccumulator = 0.0f;
        }

        upgradeHoldRepeatElapsedTime += Time.unscaledDeltaTime;
        float upgradeRate = Mathf.Lerp(HOLD_UPGRADE_START_RATE, HOLD_UPGRADE_MAX_RATE, Mathf.Clamp01(upgradeHoldRepeatElapsedTime / HOLD_UPGRADE_RAMP_DURATION));
        float upgradeInterval = 1.0f / upgradeRate;
        upgradeHoldAccumulator += Time.unscaledDeltaTime;

        while (upgradeHoldAccumulator >= upgradeInterval)
        {
            upgradeHoldAccumulator -= upgradeInterval;
            if (!TryUpgradeOnce())
            {
                StopUpgradeHold();
                return;
            }
        }
    }

    [ContextMenu("참조 다시 연결")]
    // 컨텍스트 메뉴에서 하위 UI 참조를 다시 연결한다
    public void BindChildReferences()
    {
        if (popupPanel == null)
        {
            Transform panelTransform = transform.Find("BackgroundButton");
            if (panelTransform == null)
            {
                panelTransform = transform.Find("Panel");
            }

            popupPanel = panelTransform == null ? null : panelTransform as RectTransform;
        }

        if (popupPanel == null)
        {
            return;
        }

        backgroundButton = backgroundButton != null ? backgroundButton : popupPanel.GetComponent<Button>();
        Transform searchRoot = popupPanel.Find("Panel");
        if (searchRoot == null)
        {
            searchRoot = popupPanel;
        }

        titleText = titleText != null ? titleText : FindChildComponent<TMP_Text>(searchRoot, "Title");
        levelText = levelText != null ? levelText : FindChildComponent<TMP_Text>(searchRoot, "Level");
        hpText = hpText != null ? hpText : FindChildComponent<TMP_Text>(searchRoot, "Hp");
        costText = costText != null ? costText : FindChildComponent<TMP_Text>(searchRoot, "Cost");
        statusText = statusText != null ? statusText : FindChildComponent<TMP_Text>(searchRoot, "Status");
        upgradeButton = upgradeButton != null ? upgradeButton : FindChildComponent<Button>(searchRoot, "UpgradeButton");
        upgradeButtonText = upgradeButtonText != null ? upgradeButtonText : upgradeButton == null ? null : upgradeButton.GetComponentInChildren<TMP_Text>(true);
        upgradeButtonEventTrigger = upgradeButtonEventTrigger != null ? upgradeButtonEventTrigger : EnsureUpgradeButtonEventTrigger();

        if (costText != null)
        {
            costText.richText = true;
        }

        EnsureButtonListener();
    }

    // 업그레이드 버튼에 길게 누르기 감지용 EventTrigger가 없으면 추가해 확보한다
    private EventTrigger EnsureUpgradeButtonEventTrigger()
    {
        if (upgradeButton == null)
        {
            return null;
        }

        EventTrigger trigger = upgradeButton.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = upgradeButton.gameObject.AddComponent<EventTrigger>();
        }

        return trigger;
    }

    // 투명 배경 버튼 입력으로 팝업을 닫는다
    public void OnBackgroundButtonClicked()
    {
        HidePopup();
    }

    // 선택된 장애물을 저장하고 팝업을 표시한다
    private void SelectObstacle(Obstacle obstacle, ObstacleUpgradeRuntimeController upgradeController)
    {
        selectedObstacle = obstacle;
        selectedUpgradeController = upgradeController;
        RefreshUI();
        ShowPopup();
    }

    // 선택된 장애물을 1회 업그레이드하고 UI를 갱신한다. 성공 여부를 반환한다
    private bool TryUpgradeOnce()
    {
        if (selectedUpgradeController == null)
        {
            HidePopup();
            return false;
        }

        bool upgraded = selectedUpgradeController.TryUpgrade(levelUpAmount);
        if (selectedUpgradeController == null)
        {
            HidePopup();
            return false;
        }

        selectedObstacle = selectedUpgradeController.GetComponent<Obstacle>();
        RefreshUI();
        uiRefreshTimer = 0.0f;
        return upgraded;
    }

    // 업그레이드 버튼 단일 클릭 처리 (EventTrigger 미확보 시 대비용)
    private void OnUpgradeButtonClicked()
    {
        TryUpgradeOnce();
    }

    // 카메라 터치 이벤트로 전달된 월드 히트에서 장애물을 선택한다
    private void OnCameraTargetTouched(RaycastHit hit)
    {
        if (!IsUIReady())
        {
            LogMissingUIOnce();
            return;
        }

        if (placementController != null && placementController.IsPlacing)
        {
            HidePopup();
            return;
        }

        if (TrySelectObstacleFromHit(hit, out Obstacle obstacle, out ObstacleUpgradeRuntimeController upgradeController))
        {
            SelectObstacle(obstacle, upgradeController);
        }
    }

    // 월드 히트 결과에서 선택 가능한 장애물을 찾는다
    private static bool TrySelectObstacleFromHit(RaycastHit hit, out Obstacle obstacle, out ObstacleUpgradeRuntimeController upgradeController)
    {
        obstacle = null;
        upgradeController = null;

        if (hit.collider == null)
        {
            return false;
        }

        obstacle = hit.collider.GetComponentInParent<Obstacle>();
        if (obstacle == null)
        {
            return false;
        }

        upgradeController = obstacle.GetComponent<ObstacleUpgradeRuntimeController>();
        return true;
    }

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
        hpText.text = $"체력 {selectedObstacle.CurrHp:0.#} / {selectedObstacle.TotalHp:0.#}";
        costText.text = "비용: " + FormatCosts(upgradeCosts);
        statusText.text = GetStatusText(prefabWillChange);

        upgradeButton.interactable = canUpgrade;
        upgradeButtonText.text = canUpgrade ? $"업그레이드 +{levelUpAmount}" : "업그레이드 불가";
    }

    // 현재 레벨과 최대 레벨을 UI 문자열로 변환한다
    private static string FormatLevelText(int currentLevel, ObstacleDefinitionSO definition)
    {
        if (definition == null || definition.MaxLevel <= 0)
        {
            return $"레벨 {currentLevel}";
        }

        return $"레벨 {currentLevel} / {definition.MaxLevel}";
    }

    // 현재 선택 상태의 안내 문구를 반환한다
    private string GetStatusText(bool prefabWillChange)
    {
        if (selectedObstacle == null)
        {
            return "선택된 장애물이 없습니다.";
        }

        if (selectedUpgradeController == null)
        {
            return "업그레이드 컨트롤러가 없습니다.";
        }

        if (selectedUpgradeController.CurrentDefinition == null)
        {
            return "장애물 정의 데이터가 없습니다.";
        }

        if (!selectedObstacle.IsAlive)
        {
            return "파괴된 장애물은 다시 건설해야 합니다.";
        }

        if (selectedUpgradeController.IsMaxLevelReached)
        {
            return "최대 레벨에 도달했습니다.";
        }

        if (InventorySystem.Inst == null)
        {
            return "인벤토리 시스템이 없습니다.";
        }

        if (!selectedUpgradeController.CanUpgrade(levelUpAmount))
        {
            return "재화가 부족합니다.";
        }

        return prefabWillChange ? "다음 업그레이드 시 외형이 변경됩니다." : "업그레이드 가능합니다.";
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
        StopUpgradeHold();

        if (popupPanel != null)
        {
            popupPanel.gameObject.SetActive(false);
        }
    }

    // 배경/업그레이드 버튼 이벤트를 중복 없이 연결한다
    private void EnsureButtonListener()
    {
        if (backgroundButton != null)
        {
            backgroundButton.onClick.RemoveListener(OnBackgroundButtonClicked);
            backgroundButton.onClick.AddListener(OnBackgroundButtonClicked);
        }

        if (upgradeButton == null)
        {
            return;
        }

        BindUpgradeHoldListeners();
    }

    // 버튼 이벤트 연결을 해제한다
    private void UnbindButtonListeners()
    {
        if (backgroundButton != null)
        {
            backgroundButton.onClick.RemoveListener(OnBackgroundButtonClicked);
        }

        UnbindUpgradeHoldListeners();
    }

    // 업그레이드 버튼의 길게 누르기 포인터 이벤트를 등록한다
    private void BindUpgradeHoldListeners()
    {
        if (upgradeButton == null)
        {
            return;
        }

        UnbindUpgradeHoldListeners();
        if (upgradeButtonEventTrigger == null)
        {
            Debug.LogWarning("[ObstacleUpgradePopupUI] Upgrade Button EventTrigger 참조가 없어 길게 누르기 업그레이드를 사용할 수 없습니다.", this);
            upgradeButton.onClick.RemoveListener(OnUpgradeButtonClicked);
            upgradeButton.onClick.AddListener(OnUpgradeButtonClicked);
            return;
        }

        upgradePointerDownEntry = CreateUpgradeHoldEntry(EventTriggerType.PointerDown, OnUpgradeHoldPointerDown);
        upgradePointerUpEntry = CreateUpgradeHoldEntry(EventTriggerType.PointerUp, OnUpgradeHoldPointerUp);
        upgradePointerExitEntry = CreateUpgradeHoldEntry(EventTriggerType.PointerExit, OnUpgradeHoldPointerExit);
        upgradeCancelEntry = CreateUpgradeHoldEntry(EventTriggerType.Cancel, OnUpgradeHoldCancel);
        upgradeButtonEventTrigger.triggers.Add(upgradePointerDownEntry);
        upgradeButtonEventTrigger.triggers.Add(upgradePointerUpEntry);
        upgradeButtonEventTrigger.triggers.Add(upgradePointerExitEntry);
        upgradeButtonEventTrigger.triggers.Add(upgradeCancelEntry);
    }

    // 업그레이드 버튼의 길게 누르기 포인터 이벤트를 해제한다
    private void UnbindUpgradeHoldListeners()
    {
        StopUpgradeHold();
        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(OnUpgradeButtonClicked);
        }

        if (upgradeButtonEventTrigger == null)
        {
            return;
        }

        RemoveUpgradeHoldEntry(upgradePointerDownEntry);
        RemoveUpgradeHoldEntry(upgradePointerUpEntry);
        RemoveUpgradeHoldEntry(upgradePointerExitEntry);
        RemoveUpgradeHoldEntry(upgradeCancelEntry);
        upgradePointerDownEntry = null;
        upgradePointerUpEntry = null;
        upgradePointerExitEntry = null;
        upgradeCancelEntry = null;
    }

    // 업그레이드 버튼 EventTrigger 항목을 생성한다
    private static EventTrigger.Entry CreateUpgradeHoldEntry(EventTriggerType eventType, UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };
        entry.callback.AddListener(callback);
        return entry;
    }

    // 등록된 업그레이드 버튼 EventTrigger 항목을 제거한다
    private void RemoveUpgradeHoldEntry(EventTrigger.Entry entry)
    {
        if (upgradeButtonEventTrigger != null && entry != null)
        {
            upgradeButtonEventTrigger.triggers.Remove(entry);
        }
    }

    // 업그레이드 버튼을 누르는 순간 1회 업그레이드하고 반복 입력을 시작한다
    private void OnUpgradeHoldPointerDown(BaseEventData eventData)
    {
        if (!CanProcessUpgradeInput())
        {
            StopUpgradeHold();
            return;
        }

        isUpgradeHolding = true;
        hasUpgradeHoldRepeatStarted = false;
        upgradeHoldElapsedTime = 0.0f;
        upgradeHoldRepeatElapsedTime = 0.0f;
        upgradeHoldAccumulator = 0.0f;
        if (!TryUpgradeOnce())
        {
            StopUpgradeHold();
        }
    }

    // 업그레이드 버튼에서 손을 뗄 때 반복 입력을 중단한다
    private void OnUpgradeHoldPointerUp(BaseEventData eventData)
    {
        StopUpgradeHold();
    }

    // 업그레이드 버튼 밖으로 포인터가 나가면 반복 입력을 중단한다
    private void OnUpgradeHoldPointerExit(BaseEventData eventData)
    {
        StopUpgradeHold();
    }

    // 업그레이드 입력이 취소되면 반복 입력을 중단한다
    private void OnUpgradeHoldCancel(BaseEventData eventData)
    {
        StopUpgradeHold();
    }

    // 업그레이드 입력을 처리할 수 있는 상태인지 확인한다
    private bool CanProcessUpgradeInput()
    {
        return selectedUpgradeController != null && upgradeButton != null && upgradeButton.interactable;
    }

    // 누르고 있는 업그레이드 입력 상태를 초기화한다
    private void StopUpgradeHold()
    {
        isUpgradeHolding = false;
        hasUpgradeHoldRepeatStarted = false;
        upgradeHoldElapsedTime = 0.0f;
        upgradeHoldRepeatElapsedTime = 0.0f;
        upgradeHoldAccumulator = 0.0f;
    }

    // 카메라 터치 이벤트를 중복 없이 구독한다
    private void SubscribeCameraTouchEvent()
    {
        if (hasSubscribedCameraTouch || CameraTouchHandler.Inst == null)
        {
            return;
        }

        CameraTouchHandler.Inst.OnCameraTargetTouchEvent += OnCameraTargetTouched;
        hasSubscribedCameraTouch = true;
    }

    // 카메라 터치 이벤트 구독을 해제한다
    private void UnsubscribeCameraTouchEvent()
    {
        if (!hasSubscribedCameraTouch || CameraTouchHandler.Inst == null)
        {
            hasSubscribedCameraTouch = false;
            return;
        }

        CameraTouchHandler.Inst.OnCameraTargetTouchEvent -= OnCameraTargetTouched;
        hasSubscribedCameraTouch = false;
    }

    // 팝업 구성에 필요한 참조가 유효한지 확인한다
    private bool IsUIReady()
    {
        return popupPanel != null &&
               backgroundButton != null &&
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
            return "없음";
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

            builder.Append(FormatSingleCost(cost));
        }

        return builder.Length == 0 ? "없음" : builder.ToString();
    }

    // 재화 한 항목을 현재 보유량과 비교해 부족하면 붉은색으로 표시한다
    private static string FormatSingleCost(ResourceCost cost)
    {
        string label = GetCurrencyLabel(cost.currencyType);
        if (InventorySystem.Inst != null && !InventorySystem.Inst.CanUseItem(cost.currencyType, cost.amount))
        {
            return $"<color={INSUFFICIENT_COST_COLOR}>{label} {InventorySystem.Inst.GetCountString(cost.currencyType)}/{cost.amount}</color>";
        }

        return $"{label} {cost.amount}";
    }

    // 재화 타입을 UI 표시용 이름으로 변환한다
    private static string GetCurrencyLabel(RewardCurrencyType currencyType)
    {
        if (InventorySystem.Inst != null)
        {
            string itemName = InventorySystem.Inst.GetName(currencyType);
            if (!string.IsNullOrWhiteSpace(itemName))
            {
                return itemName;
            }
        }

        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                return "코인";
            default:
                return currencyType.ToString();
        }
    }

}
