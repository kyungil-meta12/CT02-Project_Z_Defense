using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class TurretTemporaryUpgradePopupUI : MonoBehaviour
{
    private const string ROOT_NAME = "Temporary_TurretUpgradePopup";
    private const string ART_FOLDER_PATH = "Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Art";

    [Header("Selection")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask selectionLayerMask = ~0;
    [SerializeField, Min(1.0f)] private float maxRayDistance = 500.0f;

    [Header("Upgrade")]
    [SerializeField, Min(1)] private int levelUpAmount = 1;
    [SerializeField] private bool replacePrefabOnEvolution = true;
    [SerializeField, Min(0.0f)] private float holdStartDelay = 0.5f;
    [SerializeField, Min(0.1f)] private float minHoldLevelsPerSecond = 4.0f;
    [SerializeField, Min(0.1f)] private float maxHoldLevelsPerSecond = 45.0f;
    [SerializeField, Min(0.1f)] private float accelerationDuration = 4.0f;

    private TurretPlacementController placementController;
    private TurretDefinitionRuntimeController selectedTurret;
    private TurretBaseSlot selectedSlot;

    private GameObject popupRoot;
    private TMP_Text titleText;
    private TMP_Text levelText;
    private TMP_Text statusText;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<TurretTemporaryUpgradePopupUI>() != null)
        {
            return;
        }

        TurretPlacementController placementController = FindObjectOfType<TurretPlacementController>();
        Canvas canvas = FindObjectOfType<Canvas>();
        if (placementController == null || canvas == null)
        {
            return;
        }

        GameObject uiObject = new GameObject("TemporaryTurretUpgradePopupUI");
        TurretTemporaryUpgradePopupUI popupUI = uiObject.AddComponent<TurretTemporaryUpgradePopupUI>();
        popupUI.placementController = placementController;
    }

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (placementController == null)
        {
            placementController = FindObjectOfType<TurretPlacementController>();
        }

        BuildUI();
        HidePopup();
    }

    private void Update()
    {
        UpdateLevelHold();

        if (!WasPrimaryPointerPressed() || IsPointerOverUI())
        {
            return;
        }

        if (placementController != null && placementController.IsPlacing)
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

    private void SelectTurret(TurretDefinitionRuntimeController turret, TurretBaseSlot slot)
    {
        selectedTurret = turret;
        selectedSlot = slot;
        RefreshUI();
        ShowPopup();
    }

    private void AddLevel()
    {
        if (selectedTurret == null)
        {
            HidePopup();
            return;
        }

        selectedTurret.AddLevel(levelUpAmount);
        RefreshUI();
    }

    private void BeginLevelHold()
    {
        isHoldingLevelButton = true;
        holdElapsedTime = 0.0f;
        holdLevelAccumulator = 0.0f;
        AddLevel();
    }

    private void EndLevelHold()
    {
        isHoldingLevelButton = false;
        holdElapsedTime = 0.0f;
        holdLevelAccumulator = 0.0f;
    }

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
            AddLevel();

            if (selectedTurret == null ||
                selectedTurret.GetAvailableEvolutionCount() > 0 ||
                selectedTurret.IsMaxTierLevelReached)
            {
                EndLevelHold();
                return;
            }
        }
    }

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
            evolvedTurret = selectedTurret.CreateEvolvedInstance(availableIndex);
        }
        else
        {
            evolvedTurret = selectedTurret.Evolve(availableIndex) ? selectedTurret : null;
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
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, selectionLayerMask, QueryTriggerInteraction.Collide))
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
        if (slot != null && slot.CurrentTurret != null)
        {
            turret = slot.CurrentTurret;
            return true;
        }

        return false;
    }

    private void BuildUI()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Transform existingRoot = canvas.transform.Find(ROOT_NAME);
        if (existingRoot != null)
        {
            popupRoot = existingRoot.gameObject;
            return;
        }

        popupRoot = CreateUIObject(ROOT_NAME, canvas.transform);
        RectTransform rootRect = popupRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.0f);
        rootRect.anchorMax = new Vector2(0.5f, 0.0f);
        rootRect.pivot = new Vector2(0.5f, 0.0f);
        rootRect.anchoredPosition = new Vector2(0.0f, 360.0f);
        rootRect.sizeDelta = new Vector2(840.0f, 480.0f);

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

        evolutionButtons = new Button[2];
        evolutionButtonIcons = new Image[2];
        evolutionButtonLabels = new TMP_Text[2];
        for (int i = 0; i < evolutionButtons.Length; i++)
        {
            int index = i;
            Button button = CreateButton($"EvolutionButton_{i + 1}", evolutionRoot.transform, "Evolve");
            button.onClick.AddListener(() => Evolve(index));
            AddLayoutElement(button.gameObject, 280.0f, 86.0f);

            evolutionButtons[i] = button;
            evolutionButtonLabels[i] = button.GetComponentInChildren<TMP_Text>(true);
            evolutionButtonIcons[i] = CreateButtonIcon(button.transform);
        }
    }

    private void RefreshUI()
    {
        if (selectedTurret == null)
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

        int evolutionCount = selectedTurret.GetAvailableEvolutionCount();
        bool canLevelUp = evolutionCount == 0 && !selectedTurret.IsMaxTierLevelReached;
        if (!canLevelUp)
        {
            EndLevelHold();
        }

        levelUpButton.gameObject.SetActive(canLevelUp);
        levelUpButton.interactable = canLevelUp;
        levelUpButtonText.text = $"Upgrade +{levelUpAmount}";

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

    private void RefreshEvolutionButtons(int evolutionCount)
    {
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
            evolutionButtonLabels[i].text = evolutionName;

            Sprite sprite = GetEvolutionSprite(entry);
            evolutionButtonIcons[i].gameObject.SetActive(sprite != null);
            if (sprite != null)
            {
                evolutionButtonIcons[i].sprite = sprite;
                evolutionButtonIcons[i].preserveAspect = true;
            }
        }
    }

    private TurretRuntimeStat CalculateStat(int tierLevel)
    {
        TurretDefinitionSO definition = selectedTurret.CurrentTurretDefinition;
        if (definition == null)
        {
            return new TurretRuntimeStat();
        }

        return TurretStatCalculator.Calculate(definition.baseStatProfile, definition.statGrowthProfile, tierLevel);
    }

    private string FormatStats(TurretRuntimeStat stat)
    {
        return $"Damage: {stat.damage:0.##}\n" +
               $"Range: {stat.range:0.##}\n" +
               $"Fire Interval: {stat.fireInterval:0.###}\n" +
               $"Projectile Speed: {stat.projectileSpeed:0.##}\n" +
               $"Projectile Count: {stat.projectileCount}\n" +
               $"Pierce Count: {stat.pierceCount}";
    }

    private void ShowPopup()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }
    }

    private void HidePopup()
    {
        EndLevelHold();
        selectedTurret = null;
        selectedSlot = null;

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.layer = 5;
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

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
        iconRect.sizeDelta = new Vector2(56.0f, 56.0f);
        return image;
    }

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

    private static void AddEventTriggerEntry(EventTrigger eventTrigger, EventTriggerType eventType, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };

        entry.callback.AddListener(_ => action.Invoke());
        eventTrigger.triggers.Add(entry);
    }

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

    private static bool WasPrimaryPointerPressed()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            return touch.phase == TouchPhase.Began;
        }

        return Input.GetMouseButtonDown(0);
    }

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
