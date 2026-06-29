using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 에디터에서 배치한 터렛 업그레이드/진화 팝업 UI를 제어하고 선택된 터렛의 성장을 처리한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretTemporaryUpgradePopupUI : MonoBehaviour
{
    private const int DEFAULT_EVOLUTION_BUTTON_CAPACITY = 2;

    [Header("업그레이드 설정")]
    [SerializeField, Min(1)] private int levelUpAmount = 1;
    [SerializeField] private bool replacePrefabOnEvolution = true;
    [SerializeField] private bool spendCurrencyForTemporaryUpgrade = true;
    [SerializeField, Min(0.0f)] private float holdStartDelay = 0.5f;
    [SerializeField, Min(0.1f)] private float minHoldLevelsPerSecond = 4.0f;
    [SerializeField, Min(0.1f)] private float maxHoldLevelsPerSecond = 45.0f;
    [SerializeField, Min(0.1f)] private float accelerationDuration = 4.0f;

    [Header("선택 입력")]
    [SerializeField] private bool requireDoubleClickToOpenPopup = true;
    [SerializeField, Min(0.05f)] private float popupDoubleClickInterval = 1.0f;

    [Header("UI 참조")]
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private Button backgroundButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Toggle spendCurrencyToggle;
    [SerializeField] private TMP_Text currentStatText;
    [SerializeField] private TMP_Text nextStatText;
    [SerializeField] private Button levelUpButton;
    [SerializeField] private TMP_Text levelUpButtonText;
    [SerializeField] private RectTransform engineerSeatContainer;
    [SerializeField, Min(0)] private int engineerSeatTriggerCount = 4;
    [SerializeField] private TurretEngineerSeatButton[] engineerSeatButtons = Array.Empty<TurretEngineerSeatButton>();
    [SerializeField] private RectTransform evolutionButtonContainer;
    [SerializeField] private Button[] evolutionButtons = Array.Empty<Button>();
    [SerializeField] private Image[] evolutionButtonIcons = Array.Empty<Image>();
    [SerializeField] private TMP_Text[] evolutionButtonLabels = Array.Empty<TMP_Text>();

    [Header("사거리 표시")]
    [SerializeField] private bool showRangeIndicatorOnSelection = true;
    [SerializeField] private GameObject rangeIndicatorPrefab;
    [SerializeField, Min(0.001f)] private float rangeIndicatorPrefabRadiusAtScaleOne = 1.0f;
    [SerializeField] private bool forceRangeIndicatorPrefabParticleLoop = true;
    [SerializeField] private bool restartRangeIndicatorPrefabParticlesOnShow = true;
    [SerializeField] private bool useLineRangeIndicatorFallback = true;
    [SerializeField, Min(12)] private int rangeIndicatorSegments = 96;
    [SerializeField, Min(0.001f)] private float rangeIndicatorLineWidth = 0.08f;
    [SerializeField] private float rangeIndicatorYOffset = 0.05f;
    [SerializeField] private Color rangeIndicatorColor = new Color(0.2f, 0.85f, 1.0f, 0.65f);

    private TurretPlacementController placementController;
    private TurretDefinitionRuntimeController selectedTurret;
    private TurretBaseSlot selectedSlot;
    private TurretRangeIndicator rangeIndicator;
    private bool isHoldingLevelButton;
    private bool hasLoggedMissingUI;
    private bool hasLoggedMissingEvolutionButtons;
    private bool hasSubscribedCameraTouch;
    private float holdElapsedTime;
    private float holdLevelAccumulator;
    private TurretDefinitionRuntimeController lastClickedTurret;
    private float lastTurretClickTime = -1.0f;

    // 컴포넌트 추가 시 기본 참조를 자동으로 찾는다
    private void Reset()
    {
        placementController = FindFirstObjectByType<TurretPlacementController>();
        BindChildReferences();
    }

    // 게임 시작 전에 카메라, 컨트롤러, 에디터 배치 UI 참조를 준비한다
    private void Awake()
    {
        if (placementController == null)
        {
            placementController = FindFirstObjectByType<TurretPlacementController>();
        }

        BindChildReferences();
        BindButtonListeners();
        HidePopup();
    }

    // 활성화될 때 엔지니어 탑승 상태 변경 이벤트를 구독한다
    private void OnEnable()
    {
        SubscribeCameraTouchEvent();
        TurretEngineerBuffReceiver.OnBuffStateChanged += OnEngineerBuffStateChanged;
    }

    // 비활성화될 때 홀드 입력 상태와 이벤트 구독을 해제한다
    private void OnDisable()
    {
        UnsubscribeCameraTouchEvent();
        TurretEngineerBuffReceiver.OnBuffStateChanged -= OnEngineerBuffStateChanged;
        EndLevelHold();
    }

    // 파괴 시 버튼 이벤트와 사거리 표시 오브젝트를 정리한다
    private void OnDestroy()
    {
        UnsubscribeCameraTouchEvent();
        UnbindButtonListeners();
        DestroyRangeIndicator();
    }

    // 홀드 업그레이드, 터렛 선택, 배치 중 팝업 숨김 상태를 갱신한다
    private void Update()
    {
        SubscribeCameraTouchEvent();

        if (!IsUIReady())
        {
            LogMissingUIOnce();
            return;
        }

        UpdateLevelHold();

        if (placementController != null && placementController.IsPlacing)
        {
            HidePopup();
            return;
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

        backgroundButton = backgroundButton != null ? backgroundButton : popupPanel == null ? null : popupPanel.GetComponent<Button>();
        Transform searchRoot = popupPanel != null ? popupPanel : transform;
        titleText = titleText != null ? titleText : FindChildComponent<TMP_Text>(searchRoot, "Title");
        levelText = levelText != null ? levelText : FindChildComponent<TMP_Text>(searchRoot, "Level");
        statusText = statusText != null ? statusText : FindChildComponent<TMP_Text>(searchRoot, "Status");
        spendCurrencyToggle = spendCurrencyToggle != null ? spendCurrencyToggle : FindChildComponent<Toggle>(searchRoot, "SpendCurrencyToggle");
        currentStatText = currentStatText != null ? currentStatText : FindChildComponent<TMP_Text>(searchRoot, "StatsRow/CurrentStats/Text");
        nextStatText = nextStatText != null ? nextStatText : FindChildComponent<TMP_Text>(searchRoot, "StatsRow/NextStats/Text");
        levelUpButton = levelUpButton != null ? levelUpButton : FindChildComponent<Button>(searchRoot, "ButtonRow/LevelUpButton");
        levelUpButtonText = levelUpButtonText != null ? levelUpButtonText : levelUpButton == null ? null : levelUpButton.GetComponentInChildren<TMP_Text>(true);
        engineerSeatContainer = engineerSeatContainer != null ? engineerSeatContainer : FindChildComponent<RectTransform>(searchRoot, "EngineerSeatTriggers");
        evolutionButtonContainer = evolutionButtonContainer != null ? evolutionButtonContainer : FindChildComponent<RectTransform>(searchRoot, "ButtonRow/EvolutionButtons");
        BindEngineerSeatButtonReferences();
        BindEvolutionButtonReferences();
        BindButtonListeners();
    }

    // 투명 배경 버튼 입력으로 팝업을 닫는다
    public void OnBackgroundButtonClicked()
    {
        HidePopup();
    }

    // 엔지니어 탑승 슬롯 버튼 입력으로 해당 엔지니어를 하차시킨다
    public void OnEngineerSeatButtonClicked(int seatIndex)
    {
        if (selectedTurret == null || seatIndex < 0)
        {
            return;
        }

        TurretEngineerBuffReceiver buffReceiver = selectedTurret.GetComponent<TurretEngineerBuffReceiver>();
        Survivor engineer = buffReceiver == null ? null : buffReceiver.GetEngineerAt(seatIndex);
        if (engineer == null)
        {
            RefreshEngineerSeatButtons();
            return;
        }

        if (!engineer.TryDismountEngineerFromTurret())
        {
            Debug.LogWarning("[TurretTemporaryUpgradePopupUI] 엔지니어 하차 요청을 처리하지 못했습니다.", engineer);
        }

        RefreshEngineerSeatButtons();
    }

    // 선택된 터렛과 슬롯을 저장하고 사거리만 표시한다
    private void SelectTurretRangeOnly(TurretDefinitionRuntimeController turret, TurretBaseSlot slot)
    {
        selectedTurret = turret;
        selectedSlot = slot;
        EndLevelHold();
        HidePopupPanelOnly();
        RefreshSelectedRangeIndicator();
    }

    // 선택된 터렛과 슬롯을 저장하고 팝업을 표시한다
    private void SelectTurret(TurretDefinitionRuntimeController turret, TurretBaseSlot slot)
    {
        selectedTurret = turret;
        selectedSlot = slot;
        RefreshUI();
        ShowPopup();
    }

    // 선택된 터렛의 현재 티어 레벨을 설정에 맞게 올린다
    private bool AddLevel()
    {
        if (selectedTurret == null)
        {
            HidePopup();
            return false;
        }

        if (ShouldSpendCurrency())
        {
            if (!selectedTurret.TryUpgrade(levelUpAmount))
            {
                EndLevelHold();
                RefreshUI();
                return false;
            }
        }
        else
        {
            selectedTurret.AddLevel(levelUpAmount);
        }

        RefreshUI();
        return true;
    }

    // 레벨업 버튼을 누르기 시작할 때 홀드 가속 상태를 초기화한다
    private void BeginLevelHold()
    {
        isHoldingLevelButton = true;
        holdElapsedTime = 0.0f;
        holdLevelAccumulator = 0.0f;
        if (!AddLevel())
        {
            EndLevelHold();
        }
    }

    // 레벨업 버튼 홀드 상태를 해제하고 누적값을 초기화한다
    private void EndLevelHold()
    {
        isHoldingLevelButton = false;
        holdElapsedTime = 0.0f;
        holdLevelAccumulator = 0.0f;
    }

    // 레벨업 버튼을 누르고 있는 동안 가속 레벨업을 처리한다
    private void UpdateLevelHold()
    {
        if (!isHoldingLevelButton)
        {
            return;
        }

        holdElapsedTime += Time.unscaledDeltaTime;
        if (holdElapsedTime < holdStartDelay)
        {
            return;
        }

        float accelerationTime = Mathf.Max(0.01f, accelerationDuration);
        float accelerationRate = Mathf.Clamp01((holdElapsedTime - holdStartDelay) / accelerationTime);
        float levelsPerSecond = Mathf.Lerp(minHoldLevelsPerSecond, maxHoldLevelsPerSecond, accelerationRate);
        holdLevelAccumulator += levelsPerSecond * Time.unscaledDeltaTime;

        int levelAmount = Mathf.FloorToInt(holdLevelAccumulator);
        if (levelAmount <= 0)
        {
            return;
        }

        holdLevelAccumulator -= levelAmount;
        for (int i = 0; i < levelAmount; i++)
        {
            if (!AddLevel())
            {
                EndLevelHold();
                return;
            }

            if (selectedTurret == null ||
                selectedTurret.GetAvailableEvolutionCount() > 0 ||
                selectedTurret.IsMaxTierLevelReached)
            {
                EndLevelHold();
                return;
            }
        }
    }

    // 선택된 진화 후보 인덱스로 터렛을 진화시키고 슬롯 참조를 갱신한다
    private void Evolve(int availableIndex)
    {
        if (selectedTurret == null)
        {
            HidePopup();
            return;
        }

        TurretDefinitionRuntimeController evolvedTurret;
        if (replacePrefabOnEvolution)
        {
            evolvedTurret = ShouldSpendCurrency()
                ? selectedTurret.TryCreateEvolvedInstance(availableIndex)
                : selectedTurret.CreateEvolvedInstance(availableIndex);
        }
        else
        {
            bool isEvolved = ShouldSpendCurrency()
                ? selectedTurret.TryEvolve(availableIndex)
                : selectedTurret.Evolve(availableIndex);
            evolvedTurret = isEvolved ? selectedTurret : null;
        }

        if (evolvedTurret == null)
        {
            return;
        }

        selectedTurret = evolvedTurret;
        if (selectedSlot == null)
        {
            selectedSlot = evolvedTurret.GetComponentInParent<TurretBaseSlot>();
        }

        if (selectedSlot != null)
        {
            selectedSlot.SetCurrentTurret(evolvedTurret);
        }

        RefreshUI();
    }

    // 카메라 터치 이벤트로 전달된 월드 히트에서 터렛을 선택한다
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

        if (TryGetTurretSelectionFromHit(hit, out TurretDefinitionRuntimeController turret, out TurretBaseSlot slot))
        {
            HandleTurretSelectionClick(turret, slot);
        }
    }

    // 터렛 클릭 횟수와 간격에 따라 사거리 표시 또는 팝업 표시를 처리한다
    private void HandleTurretSelectionClick(TurretDefinitionRuntimeController turret, TurretBaseSlot slot)
    {
        if (turret == null)
        {
            HidePopup();
            return;
        }

        if (!requireDoubleClickToOpenPopup)
        {
            ClearPendingTurretClick();
            SelectTurret(turret, slot);
            return;
        }

        float currentTime = Time.unscaledTime;
        bool isSameTurret = lastClickedTurret == turret;
        bool isWithinDoubleClickInterval = currentTime - lastTurretClickTime <= popupDoubleClickInterval;
        if (isSameTurret && isWithinDoubleClickInterval)
        {
            ClearPendingTurretClick();
            SelectTurret(turret, slot);
            return;
        }

        lastClickedTurret = turret;
        lastTurretClickTime = currentTime;
        SelectTurretRangeOnly(turret, slot);
    }

    // 레이캐스트 히트에서 터렛 컨트롤러와 점유 슬롯을 추출한다
    private static bool TryGetTurretSelectionFromHit(RaycastHit hit, out TurretDefinitionRuntimeController turret, out TurretBaseSlot slot)
    {
        turret = null;
        slot = null;

        if (hit.collider == null)
        {
            return false;
        }

        turret = hit.collider.GetComponentInParent<TurretDefinitionRuntimeController>();
        if (turret != null)
        {
            slot = turret.GetComponentInParent<TurretBaseSlot>();
            return true;
        }

        slot = hit.collider.GetComponentInParent<TurretBaseSlot>();
        if (slot == null || slot.CurrentTurret == null)
        {
            return false;
        }

        turret = slot.CurrentTurret;
        return turret != null;
    }

    // 선택된 터렛의 현재 상태를 기준으로 팝업 텍스트와 버튼 상태를 갱신한다
    private void RefreshUI()
    {
        if (selectedTurret == null)
        {
            HidePopup();
            return;
        }

        if (!IsUIReady())
        {
            LogMissingUIOnce();
            HidePopup();
            return;
        }

        TurretDefinitionSO definition = selectedTurret.CurrentTurretDefinition;
        string turretName = GetDefinitionName(definition);
        titleText.text = turretName;
        levelText.text = $"Tier Lv. {selectedTurret.CurrentTierLevel} / Total Lv. {selectedTurret.CurrentTotalLevel}";

        TurretRuntimeStat currentStat = CalculateStat(selectedTurret.CurrentTierLevel);
        TurretRuntimeStat nextStat = CalculateStat(selectedTurret.CurrentTierLevel + levelUpAmount);

        currentStatText.text = "Current\n" + FormatStats(currentStat);
        nextStatText.text = "Next\n" + FormatStats(nextStat);
        RefreshRangeIndicator(currentStat.range);
        RefreshEngineerSeatButtons();

        int evolutionCount = selectedTurret.GetAvailableEvolutionCount();
        ResourceCost[] upgradeCosts = selectedTurret.GetUpgradeCosts(levelUpAmount);
        bool isLevelUpVisible = evolutionCount == 0 && !selectedTurret.IsMaxTierLevelReached;
        bool canLevelUp = isLevelUpVisible && (!ShouldSpendCurrency() || selectedTurret.CanUpgrade(levelUpAmount));
        if (!isLevelUpVisible || !canLevelUp)
        {
            EndLevelHold();
        }

        levelUpButton.gameObject.SetActive(isLevelUpVisible);
        levelUpButton.interactable = canLevelUp;
        levelUpButtonText.text = $"Upgrade +{levelUpAmount}{FormatCosts(upgradeCosts)}{FormatCostMode()}";

        if (evolutionCount > 0)
        {
            statusText.text = "Evolution Available";
        }
        else if (selectedTurret.IsMaxTierLevelReached)
        {
            statusText.text = "Max Level";
        }
        else
        {
            int maxLevel = selectedTurret.CurrentMaxTierLevel;
            statusText.text = maxLevel > 0 ? $"Upgrade Available / Max Lv. {maxLevel}" : "Upgrade Available";
        }

        RefreshEvolutionButtons(evolutionCount);
    }

    // 선택된 터렛의 엔지니어 탑승 상태를 상단 트리거 버튼에 반영한다
    private void RefreshEngineerSeatButtons()
    {
        if (engineerSeatContainer == null || engineerSeatButtons == null)
        {
            return;
        }

        TurretEngineerBuffReceiver buffReceiver = selectedTurret == null ? null : selectedTurret.GetComponent<TurretEngineerBuffReceiver>();
        int visibleCount = 0;
        int targetCount = Mathf.Min(Mathf.Max(0, engineerSeatTriggerCount), engineerSeatButtons.Length);

        for (int i = 0; i < targetCount; i++)
        {
            TurretEngineerSeatButton seatButton = engineerSeatButtons[i];
            if (seatButton == null)
            {
                continue;
            }

            Survivor engineer = buffReceiver == null ? null : buffReceiver.GetEngineerAt(i);
            bool isVisible = engineer != null;
            seatButton.gameObject.SetActive(isVisible);
            if (!isVisible)
            {
                seatButton.Clear();
                continue;
            }

            seatButton.Configure(this, i, "Engineer " + (i + 1), FormatEngineerBuffValue(buffReceiver));
            visibleCount++;
        }

        for (int i = targetCount; i < engineerSeatButtons.Length; i++)
        {
            if (engineerSeatButtons[i] == null)
            {
                continue;
            }

            engineerSeatButtons[i].Clear();
            engineerSeatButtons[i].gameObject.SetActive(false);
        }
    }

    // 엔지니어 1명당 적용되는 버프 수치를 UI 문자열로 변환한다
    private static string FormatEngineerBuffValue(TurretEngineerBuffReceiver buffReceiver)
    {
        if (buffReceiver == null || buffReceiver.DamageBonusRatioPerEngineer <= 0.0f)
        {
            return string.Empty;
        }

        return $"+{buffReceiver.DamageBonusRatioPerEngineer * 100.0f:0.#}%";
    }

    // 선택 터렛의 엔지니어 버프 상태가 바뀌면 탑승 슬롯 UI를 갱신한다
    private void OnEngineerBuffStateChanged(TurretEngineerBuffReceiver buffReceiver)
    {
        if (selectedTurret == null || buffReceiver == null || buffReceiver.gameObject != selectedTurret.gameObject)
        {
            return;
        }

        RefreshEngineerSeatButtons();
    }

    // 현재 선택 터렛의 사용 가능한 진화 후보를 미리 배치된 버튼 목록에 반영한다
    private void RefreshEvolutionButtons(int evolutionCount)
    {
        if (evolutionButtonContainer != null)
        {
            evolutionButtonContainer.gameObject.SetActive(evolutionCount > 0);
        }

        int readyButtonCount = GetReadyEvolutionButtonCount();
        if (evolutionCount > readyButtonCount)
        {
            LogMissingEvolutionButtonsOnce(evolutionCount, readyButtonCount);
        }

        for (int i = 0; i < readyButtonCount; i++)
        {
            TurretEvolutionEntry entry = selectedTurret == null ? null : selectedTurret.GetAvailableEvolution(i);
            bool isVisible = i < evolutionCount && entry != null;
            evolutionButtons[i].gameObject.SetActive(isVisible);

            if (!isVisible)
            {
                continue;
            }

            string evolutionName = GetEvolutionName(entry);
            ResourceCost[] evolutionCosts = selectedTurret.GetEvolutionCosts(i);
            bool canEvolve = !ShouldSpendCurrency() || selectedTurret.CanEvolve(i);
            evolutionButtons[i].interactable = canEvolve;
            evolutionButtonLabels[i].text = evolutionName + FormatCosts(evolutionCosts) + FormatCostMode();

            Sprite sprite = GetEvolutionSprite(entry);
            evolutionButtonIcons[i].gameObject.SetActive(sprite != null);
            if (sprite != null)
            {
                evolutionButtonIcons[i].sprite = sprite;
                evolutionButtonIcons[i].preserveAspect = true;
            }
        }
    }

    // 에디터 배치 진화 버튼의 내부 참조를 배열에 맞춰 수집한다
    private void BindEvolutionButtonReferences()
    {
        if (evolutionButtonContainer == null)
        {
            return;
        }

        int childButtonCount = CountChildButtons(evolutionButtonContainer);
        int targetCount = Mathf.Max(DEFAULT_EVOLUTION_BUTTON_CAPACITY, Mathf.Max(childButtonCount, evolutionButtons == null ? 0 : evolutionButtons.Length));
        EnsureEvolutionReferenceArraySizes(targetCount);

        int buttonIndex = 0;
        for (int i = 0; i < evolutionButtonContainer.childCount && buttonIndex < targetCount; i++)
        {
            Button button = evolutionButtonContainer.GetChild(i).GetComponent<Button>();
            if (button == null)
            {
                continue;
            }

            evolutionButtons[buttonIndex] = evolutionButtons[buttonIndex] != null ? evolutionButtons[buttonIndex] : button;
            evolutionButtonIcons[buttonIndex] = evolutionButtonIcons[buttonIndex] != null ? evolutionButtonIcons[buttonIndex] : FindChildComponent<Image>(button.transform, "Icon");
            evolutionButtonLabels[buttonIndex] = evolutionButtonLabels[buttonIndex] != null ? evolutionButtonLabels[buttonIndex] : button.GetComponentInChildren<TMP_Text>(true);
            buttonIndex++;
        }
    }

    // 에디터 배치 엔지니어 탑승 버튼의 내부 참조를 배열에 맞춰 수집한다
    private void BindEngineerSeatButtonReferences()
    {
        if (engineerSeatContainer == null)
        {
            return;
        }

        int childButtonCount = CountEngineerSeatButtons(engineerSeatContainer);
        int targetCount = Mathf.Max(Mathf.Max(0, engineerSeatTriggerCount), Mathf.Max(childButtonCount, engineerSeatButtons == null ? 0 : engineerSeatButtons.Length));
        EnsureEngineerSeatReferenceArraySizes(targetCount);

        int buttonIndex = 0;
        for (int i = 0; i < engineerSeatContainer.childCount && buttonIndex < targetCount; i++)
        {
            TurretEngineerSeatButton button = engineerSeatContainer.GetChild(i).GetComponent<TurretEngineerSeatButton>();
            if (button == null)
            {
                continue;
            }

            engineerSeatButtons[buttonIndex] = engineerSeatButtons[buttonIndex] != null ? engineerSeatButtons[buttonIndex] : button;
            buttonIndex++;
        }
    }

    // 엔지니어 탑승 버튼 참조 배열의 길이를 필요한 크기로 맞춘다
    private void EnsureEngineerSeatReferenceArraySizes(int targetCount)
    {
        int safeCount = Mathf.Max(0, targetCount);
        if (engineerSeatButtons == null)
        {
            engineerSeatButtons = Array.Empty<TurretEngineerSeatButton>();
        }

        if (engineerSeatButtons.Length < safeCount)
        {
            Array.Resize(ref engineerSeatButtons, safeCount);
        }
    }

    // 엔지니어 탑승 버튼 컨테이너의 직접 자식 버튼 수를 센다
    private static int CountEngineerSeatButtons(RectTransform container)
    {
        if (container == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < container.childCount; i++)
        {
            if (container.GetChild(i).GetComponent<TurretEngineerSeatButton>() != null)
            {
                count++;
            }
        }

        return count;
    }

    // 진화 버튼 참조 배열의 길이를 필요한 크기로 맞춘다
    private void EnsureEvolutionReferenceArraySizes(int targetCount)
    {
        int safeCount = Mathf.Max(0, targetCount);
        if (evolutionButtons == null)
        {
            evolutionButtons = Array.Empty<Button>();
        }

        if (evolutionButtonIcons == null)
        {
            evolutionButtonIcons = Array.Empty<Image>();
        }

        if (evolutionButtonLabels == null)
        {
            evolutionButtonLabels = Array.Empty<TMP_Text>();
        }

        if (evolutionButtons.Length < safeCount)
        {
            Array.Resize(ref evolutionButtons, safeCount);
        }

        if (evolutionButtonIcons.Length < safeCount)
        {
            Array.Resize(ref evolutionButtonIcons, safeCount);
        }

        if (evolutionButtonLabels.Length < safeCount)
        {
            Array.Resize(ref evolutionButtonLabels, safeCount);
        }
    }

    // 진화 버튼 컨테이너의 직접 자식 버튼 수를 센다
    private static int CountChildButtons(RectTransform container)
    {
        if (container == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < container.childCount; i++)
        {
            if (container.GetChild(i).GetComponent<Button>() != null)
            {
                count++;
            }
        }

        return count;
    }

    // 실제 표시 가능한 진화 버튼 참조 수를 반환한다
    private int GetReadyEvolutionButtonCount()
    {
        if (evolutionButtons == null || evolutionButtonIcons == null || evolutionButtonLabels == null)
        {
            return 0;
        }

        int count = Mathf.Min(evolutionButtons.Length, Mathf.Min(evolutionButtonIcons.Length, evolutionButtonLabels.Length));
        for (int i = 0; i < count; i++)
        {
            if (evolutionButtons[i] == null || evolutionButtonIcons[i] == null || evolutionButtonLabels[i] == null)
            {
                return i;
            }
        }

        return count;
    }

    // 레벨업, 비용 토글, 진화 버튼의 이벤트를 중복 없이 연결한다
    private void BindButtonListeners()
    {
        if (backgroundButton != null)
        {
            backgroundButton.onClick.RemoveListener(OnBackgroundButtonClicked);
            backgroundButton.onClick.AddListener(OnBackgroundButtonClicked);
        }

        if (levelUpButton != null)
        {
            BindLevelHoldButton(levelUpButton.gameObject);
        }

        if (spendCurrencyToggle != null)
        {
            spendCurrencyToggle.onValueChanged.RemoveListener(OnSpendCurrencyToggleChanged);
            spendCurrencyToggle.isOn = spendCurrencyForTemporaryUpgrade;
            spendCurrencyToggle.onValueChanged.AddListener(OnSpendCurrencyToggleChanged);
        }

        int readyButtonCount = GetReadyEvolutionButtonCount();
        for (int i = 0; i < readyButtonCount; i++)
        {
            int capturedIndex = i;
            evolutionButtons[i].onClick.RemoveAllListeners();
            evolutionButtons[i].onClick.AddListener(() => Evolve(capturedIndex));
            evolutionButtons[i].gameObject.SetActive(false);
        }
    }

    // 버튼 이벤트 연결을 해제한다
    private void UnbindButtonListeners()
    {
        if (backgroundButton != null)
        {
            backgroundButton.onClick.RemoveListener(OnBackgroundButtonClicked);
        }

        if (spendCurrencyToggle != null)
        {
            spendCurrencyToggle.onValueChanged.RemoveListener(OnSpendCurrencyToggleChanged);
        }

        int readyButtonCount = GetReadyEvolutionButtonCount();
        for (int i = 0; i < readyButtonCount; i++)
        {
            evolutionButtons[i].onClick.RemoveAllListeners();
        }
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

    // 레벨업 버튼에 홀드 입력용 EventTrigger를 연결한다
    private void BindLevelHoldButton(GameObject buttonObject)
    {
        EventTrigger eventTrigger = buttonObject.GetComponent<EventTrigger>();
        if (eventTrigger == null)
        {
            eventTrigger = buttonObject.AddComponent<EventTrigger>();
        }

        eventTrigger.triggers.Clear();
        AddEventTriggerEntry(eventTrigger, EventTriggerType.PointerDown, BeginLevelHold);
        AddEventTriggerEntry(eventTrigger, EventTriggerType.PointerUp, EndLevelHold);
        AddEventTriggerEntry(eventTrigger, EventTriggerType.PointerExit, EndLevelHold);
    }

    // EventTrigger에 지정한 입력 이벤트 콜백을 등록한다
    private static void AddEventTriggerEntry(EventTrigger eventTrigger, EventTriggerType eventType, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };

        entry.callback.AddListener(_ => action.Invoke());
        eventTrigger.triggers.Add(entry);
    }

    // 팝업 구성에 필요한 참조가 유효한지 확인한다
    private bool IsUIReady()
    {
        return popupPanel != null &&
               titleText != null &&
               levelText != null &&
               statusText != null &&
               backgroundButton != null &&
               spendCurrencyToggle != null &&
               currentStatText != null &&
               nextStatText != null &&
               levelUpButton != null &&
               levelUpButtonText != null &&
               evolutionButtonContainer != null;
    }

    // UI 참조 누락 경고를 한 번만 출력한다
    private void LogMissingUIOnce()
    {
        if (hasLoggedMissingUI)
        {
            return;
        }

        hasLoggedMissingUI = true;
        Debug.LogWarning("[TurretTemporaryUpgradePopupUI] 팝업 UI 참조가 부족합니다. 에디터 메뉴로 UI를 생성하거나 '참조 다시 연결'을 실행하세요.", this);
    }

    // 진화 버튼 부족 경고를 한 번만 출력한다
    private void LogMissingEvolutionButtonsOnce(int requiredCount, int readyCount)
    {
        if (hasLoggedMissingEvolutionButtons)
        {
            return;
        }

        hasLoggedMissingEvolutionButtons = true;
        Debug.LogWarning($"[TurretTemporaryUpgradePopupUI] 진화 버튼이 부족합니다. 필요: {requiredCount}, 준비됨: {readyCount}. 에디터 메뉴로 팝업을 다시 생성하거나 버튼을 추가하세요.", this);
    }

    // 지정 경로의 자식 Transform에서 컴포넌트를 찾는다
    private static T FindChildComponent<T>(Transform parent, string path) where T : Component
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(path);
        return child == null ? null : child.GetComponent<T>();
    }

    // 선택된 터렛 정의와 성장값으로 지정 레벨의 스탯을 계산한다
    private TurretRuntimeStat CalculateStat(int tierLevel)
    {
        TurretDefinitionSO definition = selectedTurret.CurrentTurretDefinition;
        if (definition == null)
        {
            return new TurretRuntimeStat();
        }

        return TurretStatCalculator.Calculate(definition, tierLevel);
    }

    // 터렛 스탯을 팝업에 표시할 여러 줄 문자열로 변환한다
    private string FormatStats(TurretRuntimeStat stat)
    {
        return $"Damage: {stat.damage:0.##}\n" +
               $"Range: {stat.range:0.##}\n" +
               $"Fire Interval: {stat.fireInterval:0.###}\n" +
               $"Projectile Speed: {stat.projectileSpeed:0.##}\n" +
               $"Projectile Count: {stat.projectileCount}\n" +
               $"Pierce Count: {stat.pierceCount}";
    }

    // 비용 배열을 UI에 표시할 짧은 문자열로 변환한다
    private static string FormatCosts(ResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(64);
        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            if (builder.Length == 0)
            {
                builder.Append("\n");
            }
            else
            {
                builder.Append(" / ");
            }

            builder.Append(GetCurrencyLabel(cost.currencyType));
            builder.Append(' ');
            builder.Append(cost.amount);
        }

        return builder.ToString();
    }

    // 재화 종류를 UI 표시용 짧은 이름으로 변환한다
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

    // 비용 소모 여부를 반환한다
    private bool ShouldSpendCurrency()
    {
        return spendCurrencyForTemporaryUpgrade;
    }

    // 비용 소모 비활성화 상태를 버튼 라벨에 표시한다
    private string FormatCostMode()
    {
        return ShouldSpendCurrency() ? string.Empty : "\nCost OFF";
    }

    // 비용 소모 토글 변경값을 저장하고 현재 UI 상태를 다시 계산한다
    private void OnSpendCurrencyToggleChanged(bool isOn)
    {
        spendCurrencyForTemporaryUpgrade = isOn;
        RefreshUI();
    }

    // 팝업 패널을 활성화한다
    private void ShowPopup()
    {
        if (popupPanel != null)
        {
            popupPanel.gameObject.SetActive(true);
        }
    }

    // 선택 상태를 해제하고 팝업 패널을 비활성화한다
    private void HidePopup()
    {
        EndLevelHold();
        selectedTurret = null;
        selectedSlot = null;
        ClearPendingTurretClick();
        RefreshEngineerSeatButtons();
        HideRangeIndicator();

        if (popupPanel != null)
        {
            popupPanel.gameObject.SetActive(false);
        }
    }

    // 선택과 사거리는 유지한 채 팝업 패널만 숨긴다
    private void HidePopupPanelOnly()
    {
        if (popupPanel != null)
        {
            popupPanel.gameObject.SetActive(false);
        }
    }

    // 마지막 터렛 클릭 대기 상태를 초기화한다
    private void ClearPendingTurretClick()
    {
        lastClickedTurret = null;
        lastTurretClickTime = -1.0f;
    }

    // 선택된 터렛의 현재 스탯에서 사거리 표시만 갱신한다
    private void RefreshSelectedRangeIndicator()
    {
        if (selectedTurret == null)
        {
            HideRangeIndicator();
            return;
        }

        TurretRuntimeStat currentStat = CalculateStat(selectedTurret.CurrentTierLevel);
        RefreshRangeIndicator(currentStat.range);
    }

    // 선택된 터렛의 현재 사거리 표시를 갱신한다
    private void RefreshRangeIndicator(float range)
    {
        if (!showRangeIndicatorOnSelection || selectedTurret == null)
        {
            HideRangeIndicator();
            return;
        }

        EnsureRangeIndicator();
        ConfigureRangeIndicator();
        Vector3 center = GetSelectedTurretRangeCenter();
        rangeIndicator.Show(center, range, rangeIndicatorSegments, rangeIndicatorLineWidth, rangeIndicatorYOffset, rangeIndicatorColor);
    }

    // 사거리 표시 컴포넌트를 런타임에 준비한다
    private void EnsureRangeIndicator()
    {
        if (rangeIndicator != null)
        {
            return;
        }

        GameObject indicatorObject = new GameObject("Temporary_TurretRangeIndicator");
        rangeIndicator = indicatorObject.AddComponent<TurretRangeIndicator>();
    }

    // 사거리 표시 컴포넌트에 현재 인스펙터 설정을 전달한다
    private void ConfigureRangeIndicator()
    {
        if (rangeIndicator == null)
        {
            return;
        }

        rangeIndicator.ConfigurePrefab(
            rangeIndicatorPrefab,
            rangeIndicatorPrefabRadiusAtScaleOne,
            forceRangeIndicatorPrefabParticleLoop,
            restartRangeIndicatorPrefabParticlesOnShow,
            useLineRangeIndicatorFallback);
    }

    // 선택된 터렛 또는 슬롯 기준으로 사거리 표시 중심점을 반환한다
    private Vector3 GetSelectedTurretRangeCenter()
    {
        if (selectedSlot != null && selectedSlot.BuildPoint != null)
        {
            return selectedSlot.BuildPoint.position;
        }

        return selectedTurret != null ? selectedTurret.transform.position : Vector3.zero;
    }

    // 사거리 표시를 숨긴다
    private void HideRangeIndicator()
    {
        if (rangeIndicator != null)
        {
            rangeIndicator.Hide();
        }
    }

    // 생성된 사거리 표시 오브젝트를 제거한다
    private void DestroyRangeIndicator()
    {
        if (rangeIndicator == null)
        {
            return;
        }

        Destroy(rangeIndicator.gameObject);
        rangeIndicator = null;
    }

    // 터렛 정의에서 표시 이름을 가져온다
    private static string GetDefinitionName(TurretDefinitionSO definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(definition.displayName))
        {
            return definition.displayName;
        }

        return definition.name;
    }

    // 진화 엔트리에서 표시 이름을 가져온다
    private static string GetEvolutionName(TurretEvolutionEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(entry.displayName))
        {
            return entry.displayName;
        }

        return GetDefinitionName(entry.targetDefinition);
    }

    // 진화 엔트리에 표시할 아이콘 스프라이트를 가져온다
    private static Sprite GetEvolutionSprite(TurretEvolutionEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        return entry.evolutionIcon;
    }
}
