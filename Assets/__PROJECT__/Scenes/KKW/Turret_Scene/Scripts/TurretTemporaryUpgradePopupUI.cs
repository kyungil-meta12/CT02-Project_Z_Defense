using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 터렛 클릭 시 임시 업그레이드/진화 팝업을 생성하고 선택된 터렛의 런타임 성장을 처리한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretTemporaryUpgradePopupUI : MonoBehaviour
{
    private const int SELECTION_RAYCAST_BUFFER_SIZE = 32;
    private const int DEFAULT_EVOLUTION_BUTTON_CAPACITY = 2;
    private const float EVOLUTION_BUTTON_WIDTH = 132.0f;
    private const float EVOLUTION_BUTTON_HEIGHT = 86.0f;
    private const string CANVAS_NAME = "Temporary_TurretUpgradePopupCanvas";
    private const string ROOT_NAME = "Temporary_TurretUpgradePopup";
    private const string ART_FOLDER_PATH = "Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Art";

    [Header("Selection")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask selectionLayerMask = ~0;
    [SerializeField, Min(1.0f)] private float maxRayDistance = 500.0f;

    [Header("Upgrade")]
    [SerializeField, Min(1)] private int levelUpAmount = 1;
    [SerializeField] private bool replacePrefabOnEvolution = true;
    [SerializeField] private bool spendCurrencyForTemporaryUpgrade = true;
    [SerializeField, Min(0.0f)] private float holdStartDelay = 0.5f;
    [SerializeField, Min(0.1f)] private float minHoldLevelsPerSecond = 4.0f;
    [SerializeField, Min(0.1f)] private float maxHoldLevelsPerSecond = 45.0f;
    [SerializeField, Min(0.1f)] private float accelerationDuration = 4.0f;

    [Header("Temporary UI")]
    [SerializeField] private Vector2 popupAnchoredPosition = new Vector2(0.0f, 360.0f);
    [SerializeField] private Vector2 popupSize = new Vector2(920.0f, 540.0f);

    [Header("Range Indicator")]
    [SerializeField] private bool showRangeIndicatorOnSelection = true;
    [SerializeField, Min(12)] private int rangeIndicatorSegments = 96;
    [SerializeField, Min(0.001f)] private float rangeIndicatorLineWidth = 0.08f;
    [SerializeField] private float rangeIndicatorYOffset = 0.05f;
    [SerializeField] private Color rangeIndicatorColor = new Color(0.2f, 0.85f, 1.0f, 0.65f);

    private TurretPlacementController placementController;
    private TurretDefinitionRuntimeController selectedTurret;
    private TurretBaseSlot selectedSlot;
    private TurretRangeIndicator rangeIndicator;

    private Canvas popupCanvas;
    private GameObject popupRoot;
    private RectTransform popupRootRect;
    private TMP_Text titleText;
    private TMP_Text levelText;
    private TMP_Text statusText;
    private Toggle spendCurrencyToggle;
    private TMP_Text currentStatText;
    private TMP_Text nextStatText;
    private Button levelUpButton;
    private TMP_Text levelUpButtonText;
    private RectTransform evolutionButtonContainer;
    private Button[] evolutionButtons;
    private Image[] evolutionButtonIcons;
    private TMP_Text[] evolutionButtonLabels;
    private bool isHoldingLevelButton;
    private float holdElapsedTime;
    private float holdLevelAccumulator;
    private readonly RaycastHit[] selectionHits = new RaycastHit[SELECTION_RAYCAST_BUFFER_SIZE];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    // 터렛 배치 컨트롤러가 있는 씬에서 임시 팝업 UI를 자동으로 생성한다
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<TurretTemporaryUpgradePopupUI>() != null)
        {
            return;
        }

        TurretPlacementController placementController = FindFirstObjectByType<TurretPlacementController>();
        if (placementController == null)
        {
            return;
        }

        GameObject uiObject = new GameObject("TemporaryTurretUpgradePopupUI");
        TurretTemporaryUpgradePopupUI popupUI = uiObject.AddComponent<TurretTemporaryUpgradePopupUI>();
        popupUI.placementController = placementController;
    }

    // 게임 시작 시 카메라와 배치 컨트롤러를 확보하고 팝업 UI를 구성한다
    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (placementController == null)
        {
            placementController = FindFirstObjectByType<TurretPlacementController>();
        }

        BuildUI();
        HidePopup();
    }

    // 파괴 시 런타임 사거리 표시 오브젝트를 정리한다
    private void OnDestroy()
    {
        DestroyRangeIndicator();
    }

    // 입력을 감지해 터렛 선택, 팝업 갱신, 팝업 닫기를 처리한다
    private void Update()
    {
        UpdateLevelHold();

        if (placementController != null && placementController.IsPlacing)
        {
            HidePopup();
            return;
        }

        if (!WasPrimaryPointerPressed() || IsPointerOverUI() || IsPointerInsidePopup())
        {
            return;
        }

        if (!TryGetPrimaryPointerPosition(out Vector2 pointerPosition))
        {
            return;
        }

        if (TrySelectTurret(pointerPosition, out TurretDefinitionRuntimeController turret, out TurretBaseSlot slot))
        {
            SelectTurret(turret, slot);
            return;
        }

        HidePopup();
    }

    // 선택된 터렛과 슬롯을 저장하고 팝업을 표시한다
    private void SelectTurret(TurretDefinitionRuntimeController turret, TurretBaseSlot slot)
    {
        selectedTurret = turret;
        selectedSlot = slot;
        RefreshUI();
        ShowPopup();
    }

    // 선택된 터렛의 현재 티어 레벨을 테스트 토글 상태에 맞게 올린다
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

    // 레이아웃 갱신 이후에도 팝업 위치와 크기를 고정한다
    private void LateUpdate()
    {
        LockPopupTransform();
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

    // 포인터 위치에서 가장 가까운 터렛 또는 점유 슬롯을 찾는다
    private bool TrySelectTurret(Vector2 pointerPosition, out TurretDefinitionRuntimeController turret, out TurretBaseSlot slot)
    {
        turret = null;
        slot = null;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return false;
        }

        Ray ray = targetCamera.ScreenPointToRay(pointerPosition);
        int hitCount = Physics.RaycastNonAlloc(ray, selectionHits, maxRayDistance, selectionLayerMask, QueryTriggerInteraction.Collide);
        if (hitCount <= 0)
        {
            return false;
        }

        float nearestDistance = Mathf.Infinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = selectionHits[i];
            if (hit.collider == null || hit.distance >= nearestDistance)
            {
                continue;
            }

            if (TryGetTurretSelectionFromHit(hit, out TurretDefinitionRuntimeController hitTurret, out TurretBaseSlot hitSlot))
            {
                turret = hitTurret;
                slot = hitSlot;
                nearestDistance = hit.distance;
            }
        }

        return turret != null;
    }

    // 레이캐스트 히트에서 터렛 컨트롤러와 점유 슬롯을 추출한다
    private static bool TryGetTurretSelectionFromHit(RaycastHit hit, out TurretDefinitionRuntimeController turret, out TurretBaseSlot slot)
    {
        // 건물/컨테이너 콜라이더가 먼저 맞아도 실제 터렛 또는 점유 베이스 후보를 선별합니다.
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

    // 임시 팝업 Canvas와 모든 하위 UI 요소를 생성하거나 기존 루트에 재연결한다
    private void BuildUI()
    {
        Canvas canvas = GetOrCreatePopupCanvas();
        if (canvas == null)
        {
            ClearRuntimeUIReferences();
            return;
        }

        Transform existingRoot = canvas.transform.Find(ROOT_NAME);
        if (existingRoot != null)
        {
            popupRoot = existingRoot.gameObject;
            popupRootRect = popupRoot.GetComponent<RectTransform>();
            LockPopupTransform();
            if (TryBindExistingUI())
            {
                return;
            }

            Destroy(popupRoot);
            ClearRuntimeUIReferences();
        }

        popupRoot = CreateUIObject(ROOT_NAME, canvas.transform);
        popupRootRect = popupRoot.GetComponent<RectTransform>();
        LockPopupTransform();

        Image background = popupRoot.AddComponent<Image>();
        background.color = new Color(0.04f, 0.06f, 0.08f, 0.92f);

        VerticalLayoutGroup rootLayout = popupRoot.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(28, 28, 24, 24);
        rootLayout.spacing = 12.0f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = false;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        titleText = CreateText("Title", popupRoot.transform, "Turret", 34, FontStyles.Bold, TextAlignmentOptions.Left);
        levelText = CreateText("Level", popupRoot.transform, "Lv.", 24, FontStyles.Normal, TextAlignmentOptions.Left);
        statusText = CreateText("Status", popupRoot.transform, string.Empty, 22, FontStyles.Bold, TextAlignmentOptions.Left);
        spendCurrencyToggle = CreateToggle("SpendCurrencyToggle", popupRoot.transform, "Spend Cost", spendCurrencyForTemporaryUpgrade);
        spendCurrencyToggle.onValueChanged.AddListener(OnSpendCurrencyToggleChanged);

        GameObject statsRow = CreateUIObject("StatsRow", popupRoot.transform);
        HorizontalLayoutGroup statsLayout = statsRow.AddComponent<HorizontalLayoutGroup>();
        statsLayout.spacing = 16.0f;
        statsLayout.childControlWidth = true;
        statsLayout.childControlHeight = true;
        statsLayout.childForceExpandWidth = true;
        statsLayout.childForceExpandHeight = true;
        AddLayoutElement(statsRow, 0.0f, 172.0f);

        currentStatText = CreatePanelText("CurrentStats", statsRow.transform, "Current");
        nextStatText = CreatePanelText("NextStats", statsRow.transform, "Next");

        GameObject buttonRow = CreateUIObject("ButtonRow", popupRoot.transform);
        HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 16.0f;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = false;
        AddLayoutElement(buttonRow, 0.0f, 96.0f);

        levelUpButton = CreateButton("LevelUpButton", buttonRow.transform, "Upgrade");
        BindLevelHoldButton(levelUpButton.gameObject);
        levelUpButtonText = levelUpButton.GetComponentInChildren<TMP_Text>(true);
        AddLayoutElement(levelUpButton.gameObject, 210.0f, 86.0f);

        GameObject evolutionRoot = CreateUIObject("EvolutionButtons", buttonRow.transform);
        evolutionButtonContainer = evolutionRoot.GetComponent<RectTransform>();
        HorizontalLayoutGroup evolutionLayout = evolutionRoot.AddComponent<HorizontalLayoutGroup>();
        evolutionLayout.spacing = 12.0f;
        evolutionLayout.childControlWidth = false;
        evolutionLayout.childControlHeight = false;
        evolutionLayout.childForceExpandWidth = false;
        evolutionLayout.childForceExpandHeight = false;
        AddLayoutElement(evolutionRoot, 580.0f, 86.0f);

        evolutionButtons = Array.Empty<Button>();
        evolutionButtonIcons = Array.Empty<Image>();
        evolutionButtonLabels = Array.Empty<TMP_Text>();
        EnsureEvolutionButtonCapacity(DEFAULT_EVOLUTION_BUTTON_CAPACITY);
    }

    // 선택된 터렛의 현재 상태를 기준으로 팝업 텍스트와 버튼 상태를 갱신한다
    private void RefreshUI()
    {
        if (selectedTurret == null)
        {
            HidePopup();
            return;
        }

        if (!EnsureUIReady())
        {
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

    // 파괴되었거나 누락된 런타임 UI 참조를 재생성한다
    private bool EnsureUIReady()
    {
        // 런타임 Canvas 재구성으로 파괴된 버튼 참조가 남아 있으면 UI를 다시 연결합니다.
        if (IsRuntimeUIReady())
        {
            return true;
        }

        BuildUI();
        return IsRuntimeUIReady();
    }

    // 팝업을 구성하는 필수 UI 참조가 유효한지 확인한다
    private bool IsRuntimeUIReady()
    {
        // UnityEngine.Object는 파괴 후 null처럼 비교되므로 모든 필수 참조를 접근 전에 확인합니다.
        return popupCanvas != null &&
               popupRoot != null &&
               popupRootRect != null &&
               titleText != null &&
               levelText != null &&
               statusText != null &&
               spendCurrencyToggle != null &&
               currentStatText != null &&
               nextStatText != null &&
               levelUpButton != null &&
               levelUpButtonText != null &&
               evolutionButtonContainer != null &&
               evolutionButtons != null &&
               evolutionButtonIcons != null &&
               evolutionButtonLabels != null;
    }

    // 이미 생성된 팝업 루트에서 자식 UI 참조를 다시 수집한다
    private bool TryBindExistingUI()
    {
        // 이미 생성된 임시 팝업 루트가 있으면 자식 UI 참조를 다시 수집합니다.
        if (popupRoot == null)
        {
            return false;
        }

        popupRootRect = popupRoot.GetComponent<RectTransform>();
        LockPopupTransform();

        titleText = FindChildComponent<TMP_Text>(popupRoot.transform, "Title");
        levelText = FindChildComponent<TMP_Text>(popupRoot.transform, "Level");
        statusText = FindChildComponent<TMP_Text>(popupRoot.transform, "Status");
        spendCurrencyToggle = FindChildComponent<Toggle>(popupRoot.transform, "SpendCurrencyToggle");
        currentStatText = FindChildComponent<TMP_Text>(popupRoot.transform, "StatsRow/CurrentStats/Text");
        nextStatText = FindChildComponent<TMP_Text>(popupRoot.transform, "StatsRow/NextStats/Text");
        levelUpButton = FindChildComponent<Button>(popupRoot.transform, "ButtonRow/LevelUpButton");
        levelUpButtonText = levelUpButton == null ? null : levelUpButton.GetComponentInChildren<TMP_Text>(true);
        evolutionButtonContainer = FindChildComponent<RectTransform>(popupRoot.transform, "ButtonRow/EvolutionButtons");

        evolutionButtons = Array.Empty<Button>();
        evolutionButtonIcons = Array.Empty<Image>();
        evolutionButtonLabels = Array.Empty<TMP_Text>();

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

        return IsRuntimeUIReady() && EnsureEvolutionButtonCapacity(DEFAULT_EVOLUTION_BUTTON_CAPACITY);
    }

    // 필요한 진화 후보 수만큼 버튼 배열과 실제 버튼 오브젝트를 확보한다
    private bool EnsureEvolutionButtonCapacity(int requiredCount)
    {
        int safeCount = Mathf.Max(0, requiredCount);
        if (evolutionButtonContainer == null)
        {
            return safeCount == 0;
        }

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

        for (int i = 0; i < safeCount; i++)
        {
            EnsureEvolutionButton(i);
        }

        return AreEvolutionButtonReferencesReady(safeCount);
    }

    // 지정한 인덱스의 진화 버튼과 아이콘, 라벨 참조를 생성하거나 재연결한다
    private void EnsureEvolutionButton(int index)
    {
        Transform containerTransform = evolutionButtonContainer.transform;
        Transform buttonTransform = containerTransform.Find($"EvolutionButton_{index + 1}");
        Button button = buttonTransform == null ? null : buttonTransform.GetComponent<Button>();

        if (button == null)
        {
            button = CreateButton($"EvolutionButton_{index + 1}", containerTransform, "Evolve");
        }

        button.onClick.RemoveAllListeners();
        int capturedIndex = index;
        button.onClick.AddListener(() => Evolve(capturedIndex));
        AddLayoutElement(button.gameObject, EVOLUTION_BUTTON_WIDTH, EVOLUTION_BUTTON_HEIGHT);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        Image icon = FindChildComponent<Image>(button.transform, "Icon");
        if (icon == null)
        {
            icon = CreateButtonIcon(button.transform);
        }

        evolutionButtons[index] = button;
        evolutionButtonLabels[index] = label;
        evolutionButtonIcons[index] = icon;
    }

    // 요청한 개수만큼 진화 버튼 내부 참조가 유효한지 확인한다
    private bool AreEvolutionButtonReferencesReady(int requiredCount)
    {
        // 진화 버튼 배열 내부의 파괴된 참조를 Refresh 단계 전에 걸러냅니다.
        if (evolutionButtons == null || evolutionButtonIcons == null || evolutionButtonLabels == null)
        {
            return false;
        }

        if (evolutionButtons.Length < requiredCount ||
            evolutionButtonIcons.Length < requiredCount ||
            evolutionButtonLabels.Length < requiredCount)
        {
            return false;
        }

        for (int i = 0; i < requiredCount; i++)
        {
            if (evolutionButtons[i] == null || evolutionButtonIcons[i] == null || evolutionButtonLabels[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    // 런타임 UI 참조를 모두 비워 파괴된 오브젝트 접근을 방지한다
    private void ClearRuntimeUIReferences()
    {
        // 파괴된 Unity UI 참조를 들고 있다가 MissingReferenceException이 나는 상황을 방지합니다.
        popupCanvas = null;
        popupRoot = null;
        popupRootRect = null;
        titleText = null;
        levelText = null;
        statusText = null;
        spendCurrencyToggle = null;
        currentStatText = null;
        nextStatText = null;
        levelUpButton = null;
        levelUpButtonText = null;
        evolutionButtonContainer = null;
        evolutionButtons = null;
        evolutionButtonIcons = null;
        evolutionButtonLabels = null;
    }

    // 전용 Screen Space Overlay Canvas를 찾거나 생성한다
    private Canvas GetOrCreatePopupCanvas()
    {
        // 임시 팝업은 기존 게임 UI 레이아웃과 분리된 전용 Overlay Canvas에 고정합니다.
        if (popupCanvas != null)
        {
            return popupCanvas;
        }

        GameObject canvasObject = GameObject.Find(CANVAS_NAME);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(CANVAS_NAME, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        }

        popupCanvas = canvasObject.GetComponent<Canvas>();
        if (popupCanvas == null)
        {
            popupCanvas = canvasObject.AddComponent<Canvas>();
        }

        popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 100;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        if (canvasScaler == null)
        {
            canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        }

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(900.0f, 2000.0f);
        canvasScaler.matchWidthOrHeight = 1.0f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        return popupCanvas;
    }

    // 팝업 루트 RectTransform을 기준 위치와 크기로 고정한다
    private void LockPopupTransform()
    {
        // 외부 레이아웃이나 재바인딩으로 위치가 밀리지 않도록 팝업 루트 위치를 고정합니다.
        if (popupRootRect == null)
        {
            return;
        }

        popupRootRect.anchorMin = new Vector2(0.5f, 0.0f);
        popupRootRect.anchorMax = new Vector2(0.5f, 0.0f);
        popupRootRect.pivot = new Vector2(0.5f, 0.0f);
        popupRootRect.anchoredPosition = popupAnchoredPosition;
        popupRootRect.sizeDelta = popupSize;
        popupRootRect.localScale = Vector3.one;
    }

    // 지정한 경로의 자식 Transform에서 컴포넌트를 찾는다
    private static T FindChildComponent<T>(Transform parent, string path) where T : Component
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(path);
        if (child == null)
        {
            return null;
        }

        return child.GetComponent<T>();
    }

    // 현재 선택 터렛의 사용 가능한 진화 후보를 버튼 목록에 반영한다
    private void RefreshEvolutionButtons(int evolutionCount)
    {
        int requiredButtonCount = Mathf.Max(DEFAULT_EVOLUTION_BUTTON_CAPACITY, evolutionCount);
        if (!EnsureEvolutionButtonCapacity(requiredButtonCount))
        {
            return;
        }

        if (evolutionButtonContainer != null)
        {
            evolutionButtonContainer.gameObject.SetActive(evolutionCount > 0);
        }

        for (int i = 0; i < evolutionButtons.Length; i++)
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

    // 선택된 터렛 정의와 성장값으로 지정 레벨의 스탯을 계산한다
    private TurretRuntimeStat CalculateStat(int tierLevel)
    {
        TurretDefinitionSO definition = selectedTurret.CurrentTurretDefinition;
        if (definition == null)
        {
            return new TurretRuntimeStat();
        }

        return TurretStatCalculator.Calculate(definition.baseStatProfile, definition.statGrowthProfile, tierLevel);
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

        StringBuilder builder = new StringBuilder();
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
            builder.Append(" ");
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

    // 임시 UI 테스트 옵션에서 비용 소모 여부를 반환한다
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

    // 팝업 루트를 활성화한다
    private void ShowPopup()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }
    }

    // 선택 상태를 해제하고 팝업 루트를 비활성화한다
    private void HidePopup()
    {
        EndLevelHold();
        selectedTurret = null;
        selectedSlot = null;
        HideRangeIndicator();

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
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

    // UI 전용 RectTransform 오브젝트를 생성하고 부모에 연결한다
    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.layer = 5;
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    // TextMeshPro 텍스트 UI를 생성하고 기본 레이아웃 값을 설정한다
    private static TMP_Text CreateText(string objectName, Transform parent, string text, int fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(objectName, parent);
        TMP_Text tmpText = textObject.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.fontStyle = fontStyle;
        tmpText.alignment = alignment;
        tmpText.color = Color.white;
        AddLayoutElement(textObject, 0.0f, fontSize + 10.0f);
        return tmpText;
    }

    // 배경 패널이 포함된 텍스트 영역을 생성한다
    private static TMP_Text CreatePanelText(string objectName, Transform parent, string text)
    {
        GameObject panel = CreateUIObject(objectName, parent);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(1.0f, 1.0f, 1.0f, 0.08f);

        TMP_Text tmpText = CreateText("Text", panel.transform, text, 22, FontStyles.Normal, TextAlignmentOptions.Left);
        RectTransform textRect = tmpText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18.0f, 12.0f);
        textRect.offsetMax = new Vector2(-18.0f, -12.0f);

        return tmpText;
    }

    // 기본 배경 이미지와 라벨을 가진 버튼을 생성한다
    private static Button CreateButton(string objectName, Transform parent, string label)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.15f, 0.48f, 0.72f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text labelText = CreateText("Label", buttonObject.transform, label, 22, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12.0f, 8.0f);
        labelRect.offsetMax = new Vector2(-12.0f, -8.0f);

        return button;
    }

    // 테스트용 비용 소모 토글 UI를 생성한다
    private static Toggle CreateToggle(string objectName, Transform parent, string label, bool isOn)
    {
        GameObject toggleObject = CreateUIObject(objectName, parent);
        HorizontalLayoutGroup layout = toggleObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10.0f;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        AddLayoutElement(toggleObject, 0.0f, 32.0f);

        GameObject backgroundObject = CreateUIObject("Background", toggleObject.transform);
        Image backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = new Color(1.0f, 1.0f, 1.0f, 0.22f);
        AddLayoutElement(backgroundObject, 28.0f, 28.0f);

        GameObject checkmarkObject = CreateUIObject("Checkmark", backgroundObject.transform);
        Image checkmarkImage = checkmarkObject.AddComponent<Image>();
        checkmarkImage.color = new Color(0.25f, 0.82f, 0.52f, 0.95f);
        RectTransform checkmarkRect = checkmarkImage.rectTransform;
        checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
        checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;

        TMP_Text labelText = CreateText("Label", toggleObject.transform, label, 20, FontStyles.Bold, TextAlignmentOptions.Left);
        AddLayoutElement(labelText.gameObject, 180.0f, 30.0f);

        Toggle toggle = toggleObject.AddComponent<Toggle>();
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkmarkImage;
        toggle.isOn = isOn;
        return toggle;
    }

    // 진화 버튼 안에 표시할 아이콘 이미지를 생성한다
    private static Image CreateButtonIcon(Transform buttonTransform)
    {
        GameObject iconObject = CreateUIObject("Icon", buttonTransform);
        Image image = iconObject.AddComponent<Image>();
        image.raycastTarget = false;

        RectTransform iconRect = image.rectTransform;
        iconRect.anchorMin = new Vector2(0.0f, 0.5f);
        iconRect.anchorMax = new Vector2(0.0f, 0.5f);
        iconRect.pivot = new Vector2(0.0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(12.0f, 0.0f);
        iconRect.sizeDelta = new Vector2(42.0f, 42.0f);
        return image;
    }

    // 대상 UI 오브젝트에 LayoutElement를 추가하거나 갱신한다
    private static void AddLayoutElement(GameObject target, float preferredWidth, float preferredHeight)
    {
        LayoutElement layoutElement = target.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = target.AddComponent<LayoutElement>();
        }

        if (preferredWidth > 0.0f)
        {
            layoutElement.preferredWidth = preferredWidth;
        }

        if (preferredHeight > 0.0f)
        {
            layoutElement.preferredHeight = preferredHeight;
        }
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

    // 터치 또는 마우스의 기본 포인터 위치를 가져온다
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
        // Simulator에서 EventSystem UI 판정이 누락되어도 팝업 내부 터치는 월드 선택으로 넘기지 않습니다.
        if (popupRootRect == null || !popupRootRect.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!TryGetPrimaryPointerPosition(out Vector2 pointerPosition))
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(popupRootRect, pointerPosition, null);
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

        if (entry.evolutionIcon != null)
        {
            return entry.evolutionIcon;
        }

#if UNITY_EDITOR
        string evolutionName = GetEvolutionName(entry);
        if (string.IsNullOrWhiteSpace(evolutionName))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>($"{ART_FOLDER_PATH}/{evolutionName}.png");
#else
        return null;
#endif
    }
}
