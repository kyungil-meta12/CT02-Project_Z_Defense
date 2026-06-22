using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 터렛 배치 버튼과 업그레이드 팝업 UI를 현재 씬에 에디터 오브젝트로 배치하는 메뉴를 제공한다.
internal static class TurretUICreator
{
    private const string PLACEMENT_MENU_PATH = "Project Z Defense/UI/Create Turret Placement UI";
    private const string UPGRADE_MENU_PATH = "Project Z Defense/UI/Create Turret Upgrade Popup UI";
    private const string PLACEMENT_CANVAS_NAME = "TurretPlacementCanvas";
    private const string PLACEMENT_ROOT_NAME = "TurretPlacementBar";
    private const string UPGRADE_CANVAS_NAME = "TurretUpgradePopupCanvas";
    private const string UPGRADE_ROOT_NAME = "TurretUpgradePopup";
    private const int DEFAULT_PLACEMENT_SLOT_COUNT = 4;
    private const int DEFAULT_EVOLUTION_BUTTON_COUNT = 4;
    private const int DEFAULT_ENGINEER_SEAT_BUTTON_COUNT = 4;

    [MenuItem(PLACEMENT_MENU_PATH)]
    // 메뉴 실행 시 터렛 배치 UI Canvas와 수동 배치 버튼을 현재 씬에 생성하거나 기존 UI를 선택한다
    private static void CreateTurretPlacementUI()
    {
        TurretPlacementUI existingUI = Object.FindFirstObjectByType<TurretPlacementUI>();
        if (existingUI != null)
        {
            Selection.activeGameObject = existingUI.gameObject;
            EditorGUIUtility.PingObject(existingUI.gameObject);
            return;
        }

        TurretShopEntrySO[] selectedEntries = GetSelectedTurretShopEntries();
        int slotCount = selectedEntries.Length > 0 ? selectedEntries.Length : DEFAULT_PLACEMENT_SLOT_COUNT;
        GameObject canvasObject = CreateCanvas(PLACEMENT_CANVAS_NAME, 80);
        GameObject rootObject = CreatePlacementRoot(canvasObject.transform);
        TurretPlacementUI placementUI = rootObject.AddComponent<TurretPlacementUI>();
        GameObject slotContainer = CreatePlacementSlotContainer(rootObject.transform);
        TurretPlacementController placementController = Object.FindFirstObjectByType<TurretPlacementController>();

        for (int i = 0; i < slotCount; i++)
        {
            TurretShopEntrySO entry = i < selectedEntries.Length ? selectedEntries[i] : null;
            CreatePlacementSlot(slotContainer.transform, placementController, entry, i);
        }

        AssignPlacementUIReferences(placementUI, placementController, slotContainer.transform);
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Turret Placement UI");
        Selection.activeGameObject = rootObject;
        EditorGUIUtility.PingObject(rootObject);
        EditorSceneManager.MarkSceneDirty(rootObject.scene);
    }

    [MenuItem(UPGRADE_MENU_PATH)]
    // 메뉴 실행 시 터렛 업그레이드 팝업 Canvas와 하위 UI를 현재 씬에 생성하거나 기존 팝업을 선택한다
    private static void CreateTurretUpgradePopupUI()
    {
        TurretTemporaryUpgradePopupUI existingPopup = Object.FindFirstObjectByType<TurretTemporaryUpgradePopupUI>();
        if (existingPopup != null)
        {
            if (HasValidTurretPopupRoot(existingPopup))
            {
                Selection.activeGameObject = existingPopup.gameObject;
                EditorGUIUtility.PingObject(existingPopup.gameObject);
                return;
            }

            Undo.DestroyObjectImmediate(existingPopup.gameObject);
        }

        GameObject canvasObject = CreateCanvas(UPGRADE_CANVAS_NAME, 100);
        GameObject popupObject = CreateUpgradePopupRoot(canvasObject.transform);
        TurretTemporaryUpgradePopupUI popupUI = popupObject.AddComponent<TurretTemporaryUpgradePopupUI>();
        Button backgroundButton = CreateTransparentBackgroundButton(popupObject.transform);
        GameObject panelObject = CreateUpgradePopupPanel(backgroundButton.transform);

        GameObject engineerSeatContainer = CreateEngineerSeatContainer(panelObject.transform);
        TurretEngineerSeatButton[] engineerSeatButtons = CreateEngineerSeatButtons(engineerSeatContainer.transform, DEFAULT_ENGINEER_SEAT_BUTTON_COUNT);
        TMP_Text titleText = CreateText("Title", panelObject.transform, "Turret", 34, FontStyles.Bold, TextAlignmentOptions.Left);
        TMP_Text levelText = CreateText("Level", panelObject.transform, "Tier Lv.", 24, FontStyles.Normal, TextAlignmentOptions.Left);
        TMP_Text statusText = CreateText("Status", panelObject.transform, "No turret selected.", 22, FontStyles.Bold, TextAlignmentOptions.Left);
        Toggle spendCurrencyToggle = CreateToggle("SpendCurrencyToggle", panelObject.transform, "Spend Cost", true);
        GameObject statsRow = CreateStatsRow(panelObject.transform);
        TMP_Text currentStatText = CreatePanelText("CurrentStats", statsRow.transform, "Current");
        TMP_Text nextStatText = CreatePanelText("NextStats", statsRow.transform, "Next");
        GameObject buttonRow = CreateButtonRow(panelObject.transform);
        Button levelUpButton = CreateButton("LevelUpButton", buttonRow.transform, "Upgrade", new Color(0.15f, 0.48f, 0.72f, 0.95f));
        TMP_Text levelUpButtonText = levelUpButton.GetComponentInChildren<TMP_Text>(true);
        AddLayoutElement(levelUpButton.gameObject, 210.0f, 86.0f);
        GameObject evolutionContainer = CreateEvolutionButtonContainer(buttonRow.transform);
        Button[] evolutionButtons = new Button[DEFAULT_EVOLUTION_BUTTON_COUNT];
        Image[] evolutionIcons = new Image[DEFAULT_EVOLUTION_BUTTON_COUNT];
        TMP_Text[] evolutionLabels = new TMP_Text[DEFAULT_EVOLUTION_BUTTON_COUNT];

        for (int i = 0; i < DEFAULT_EVOLUTION_BUTTON_COUNT; i++)
        {
            Button button = CreateButton($"EvolutionButton_{i + 1}", evolutionContainer.transform, "Evolve", new Color(0.18f, 0.5f, 0.35f, 0.95f));
            Image icon = CreateButtonIcon(button.transform);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            AddLayoutElement(button.gameObject, 132.0f, 86.0f);
            button.gameObject.SetActive(false);
            evolutionButtons[i] = button;
            evolutionIcons[i] = icon;
            evolutionLabels[i] = label;
        }

        AssignUpgradePopupReferences(
            popupUI,
            backgroundButton.GetComponent<RectTransform>(),
            backgroundButton,
            titleText,
            levelText,
            statusText,
            spendCurrencyToggle,
            currentStatText,
            nextStatText,
            levelUpButton,
            levelUpButtonText,
            engineerSeatContainer.GetComponent<RectTransform>(),
            DEFAULT_ENGINEER_SEAT_BUTTON_COUNT,
            engineerSeatButtons,
            evolutionContainer.GetComponent<RectTransform>(),
            evolutionButtons,
            evolutionIcons,
            evolutionLabels);

        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Turret Upgrade Popup UI");
        Selection.activeGameObject = popupObject;
        EditorGUIUtility.PingObject(popupObject);
        EditorSceneManager.MarkSceneDirty(popupObject.scene);
    }

    // 선택된 프로젝트 에셋 중 터렛 배치 엔트리만 배열로 반환한다
    private static TurretShopEntrySO[] GetSelectedTurretShopEntries()
    {
        Object[] selectedObjects = Selection.objects;
        TurretShopEntrySO[] entries = new TurretShopEntrySO[selectedObjects.Length];
        int count = 0;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            if (selectedObjects[i] is TurretShopEntrySO entry)
            {
                entries[count] = entry;
                count++;
            }
        }

        if (count == entries.Length)
        {
            return entries;
        }

        TurretShopEntrySO[] compactEntries = new TurretShopEntrySO[count];
        for (int i = 0; i < count; i++)
        {
            compactEntries[i] = entries[i];
        }

        return compactEntries;
    }

    // 지정 이름과 정렬 순서의 Screen Space Overlay Canvas를 생성한다
    private static GameObject CreateCanvas(string canvasName, int sortingOrder)
    {
        GameObject canvasObject = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(900.0f, 2000.0f);
        canvasScaler.matchWidthOrHeight = 1.0f;
        return canvasObject;
    }

    // 터렛 배치 버튼을 담는 하단 루트를 생성한다
    private static GameObject CreatePlacementRoot(Transform parent)
    {
        GameObject rootObject = CreateUIObject(PLACEMENT_ROOT_NAME, parent);
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.0f);
        rootRect.anchorMax = new Vector2(0.5f, 0.0f);
        rootRect.pivot = new Vector2(0.5f, 0.0f);
        rootRect.anchoredPosition = new Vector2(0.0f, 70.0f);
        rootRect.sizeDelta = new Vector2(820.0f, 150.0f);
        return rootObject;
    }

    // 배치 버튼들이 들어갈 가로 컨테이너를 생성한다
    private static GameObject CreatePlacementSlotContainer(Transform parent)
    {
        GameObject containerObject = CreateUIObject("SlotContainer", parent);
        RectTransform containerRect = containerObject.GetComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = containerObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 12.0f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return containerObject;
    }

    // 터렛 배치 버튼 하나를 생성하고 표시 참조와 상점 엔트리를 연결한다
    private static TurretPlacementSlotUI CreatePlacementSlot(Transform parent, TurretPlacementController placementController, TurretShopEntrySO entry, int index)
    {
        GameObject slotObject = CreateUIObject($"TurretPlacementSlot_{index + 1}", parent);
        Image background = slotObject.AddComponent<Image>();
        background.color = new Color(0.04f, 0.07f, 0.1f, 0.86f);
        Button button = slotObject.AddComponent<Button>();
        button.targetGraphic = background;
        TurretPlacementSlotUI slotUI = slotObject.AddComponent<TurretPlacementSlotUI>();
        AddLayoutElement(slotObject, 140.0f, 128.0f);

        Image iconImage = CreateIcon("Icon", slotObject.transform, entry == null ? null : entry.Icon);
        TMP_Text nameText = CreateText("Name", slotObject.transform, entry == null ? "Turret" : entry.DisplayName, 18, FontStyles.Bold, TextAlignmentOptions.Center);
        TMP_Text costText = CreateText("Cost", slotObject.transform, "Cost", 16, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigurePlacementSlotText(iconImage.rectTransform, nameText.rectTransform, costText.rectTransform);
        AssignPlacementSlotReferences(slotUI, placementController, entry, iconImage, nameText, costText);
        return slotUI;
    }

    // 배치 버튼 내부 텍스트와 아이콘 위치를 설정한다
    private static void ConfigurePlacementSlotText(RectTransform iconRect, RectTransform nameRect, RectTransform costRect)
    {
        iconRect.anchorMin = new Vector2(0.5f, 1.0f);
        iconRect.anchorMax = new Vector2(0.5f, 1.0f);
        iconRect.pivot = new Vector2(0.5f, 1.0f);
        iconRect.anchoredPosition = new Vector2(0.0f, -10.0f);
        iconRect.sizeDelta = new Vector2(52.0f, 52.0f);

        nameRect.anchorMin = new Vector2(0.0f, 0.0f);
        nameRect.anchorMax = new Vector2(1.0f, 0.0f);
        nameRect.pivot = new Vector2(0.5f, 0.0f);
        nameRect.offsetMin = new Vector2(8.0f, 44.0f);
        nameRect.offsetMax = new Vector2(-8.0f, 78.0f);

        costRect.anchorMin = new Vector2(0.0f, 0.0f);
        costRect.anchorMax = new Vector2(1.0f, 0.0f);
        costRect.pivot = new Vector2(0.5f, 0.0f);
        costRect.offsetMin = new Vector2(8.0f, 10.0f);
        costRect.offsetMax = new Vector2(-8.0f, 40.0f);
    }

    // 기존 터렛 팝업 컴포넌트가 실제 팝업 패널을 참조하는지 확인한다
    private static bool HasValidTurretPopupRoot(TurretTemporaryUpgradePopupUI popupUI)
    {
        SerializedObject serializedObject = new SerializedObject(popupUI);
        SerializedProperty popupRootProperty = serializedObject.FindProperty("popupPanel");
        SerializedProperty engineerSeatContainerProperty = serializedObject.FindProperty("engineerSeatContainer");
        return popupRootProperty != null &&
               popupRootProperty.objectReferenceValue != null &&
               engineerSeatContainerProperty != null &&
               engineerSeatContainerProperty.objectReferenceValue != null;
    }

    // 터렛 업그레이드 팝업 컨트롤러 루트 오브젝트를 생성한다
    private static GameObject CreateUpgradePopupRoot(Transform parent)
    {
        GameObject popupObject = CreateUIObject(UPGRADE_ROOT_NAME, parent);
        RectTransform popupRect = popupObject.GetComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.anchoredPosition = Vector2.zero;
        popupRect.sizeDelta = Vector2.zero;
        return popupObject;
    }

    // 화면 전체를 덮는 투명 닫기 버튼을 생성한다
    private static Button CreateTransparentBackgroundButton(Transform parent)
    {
        GameObject buttonObject = CreateUIObject("BackgroundButton", parent);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = Vector2.zero;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    // 투명 닫기 버튼 하위의 실제 터렛 업그레이드 팝업 패널을 생성한다
    private static GameObject CreateUpgradePopupPanel(Transform parent)
    {
        GameObject panelObject = CreateUIObject("Panel", parent);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.0f);
        panelRect.anchorMax = new Vector2(0.5f, 0.0f);
        panelRect.pivot = new Vector2(0.5f, 0.0f);
        panelRect.anchoredPosition = new Vector2(0.0f, 320.0f);
        panelRect.sizeDelta = new Vector2(920.0f, 620.0f);

        Image background = panelObject.AddComponent<Image>();
        background.color = new Color(0.04f, 0.06f, 0.08f, 0.92f);

        VerticalLayoutGroup rootLayout = panelObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(28, 28, 24, 24);
        rootLayout.spacing = 12.0f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = false;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;
        return panelObject;
    }

    // 스탯 비교 행을 생성한다
    private static GameObject CreateStatsRow(Transform parent)
    {
        GameObject statsRow = CreateUIObject("StatsRow", parent);
        HorizontalLayoutGroup statsLayout = statsRow.AddComponent<HorizontalLayoutGroup>();
        statsLayout.spacing = 16.0f;
        statsLayout.childControlWidth = true;
        statsLayout.childControlHeight = true;
        statsLayout.childForceExpandWidth = true;
        statsLayout.childForceExpandHeight = true;
        AddLayoutElement(statsRow, 0.0f, 172.0f);
        return statsRow;
    }

    // 업그레이드와 진화 버튼 행을 생성한다
    private static GameObject CreateButtonRow(Transform parent)
    {
        GameObject buttonRow = CreateUIObject("ButtonRow", parent);
        HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 16.0f;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = false;
        AddLayoutElement(buttonRow, 0.0f, 96.0f);
        return buttonRow;
    }

    // 엔지니어 탑승 해제 트리거 버튼 컨테이너를 생성한다
    private static GameObject CreateEngineerSeatContainer(Transform parent)
    {
        GameObject seatRoot = CreateUIObject("EngineerSeatTriggers", parent);
        HorizontalLayoutGroup seatLayout = seatRoot.AddComponent<HorizontalLayoutGroup>();
        seatLayout.spacing = 10.0f;
        seatLayout.childControlWidth = false;
        seatLayout.childControlHeight = true;
        seatLayout.childForceExpandWidth = false;
        seatLayout.childForceExpandHeight = false;
        AddLayoutElement(seatRoot, 0.0f, 52.0f);
        seatRoot.SetActive(false);
        return seatRoot;
    }

    // 기본 엔지니어 탑승 해제 트리거 버튼들을 생성한다
    private static TurretEngineerSeatButton[] CreateEngineerSeatButtons(Transform parent, int count)
    {
        int safeCount = Mathf.Max(0, count);
        TurretEngineerSeatButton[] seatButtons = new TurretEngineerSeatButton[safeCount];
        for (int i = 0; i < safeCount; i++)
        {
            Button button = CreateButton($"EngineerSeatButton_{i + 1}", parent, "Engineer", new Color(0.62f, 0.44f, 0.16f, 0.95f));
            AddLayoutElement(button.gameObject, 142.0f, 46.0f);
            TurretEngineerSeatButton seatButton = button.gameObject.AddComponent<TurretEngineerSeatButton>();
            button.gameObject.SetActive(false);
            seatButtons[i] = seatButton;
        }

        return seatButtons;
    }

    // 진화 버튼 컨테이너를 생성한다
    private static GameObject CreateEvolutionButtonContainer(Transform parent)
    {
        GameObject evolutionRoot = CreateUIObject("EvolutionButtons", parent);
        HorizontalLayoutGroup evolutionLayout = evolutionRoot.AddComponent<HorizontalLayoutGroup>();
        evolutionLayout.spacing = 12.0f;
        evolutionLayout.childControlWidth = false;
        evolutionLayout.childControlHeight = false;
        evolutionLayout.childForceExpandWidth = false;
        evolutionLayout.childForceExpandHeight = false;
        AddLayoutElement(evolutionRoot, 580.0f, 86.0f);
        return evolutionRoot;
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

    // UI 전용 RectTransform 오브젝트를 생성한다
    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.layer = 5;
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    // TextMeshPro 텍스트 UI를 생성한다
    private static TMP_Text CreateText(string objectName, Transform parent, string text, int fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(objectName, parent);
        TMP_Text tmpText = textObject.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.fontStyle = fontStyle;
        tmpText.alignment = alignment;
        tmpText.color = Color.white;
        tmpText.raycastTarget = false;
        AddLayoutElement(textObject, 0.0f, fontSize + 10.0f);
        return tmpText;
    }

    // 아이콘 이미지를 생성한다
    private static Image CreateIcon(string objectName, Transform parent, Sprite sprite)
    {
        GameObject iconObject = CreateUIObject(objectName, parent);
        Image image = iconObject.AddComponent<Image>();
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    // 기본 배경 이미지와 라벨을 가진 버튼을 생성한다
    private static Button CreateButton(string objectName, Transform parent, string label, Color color)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = color;

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

    // TurretPlacementUI의 직렬화된 참조를 연결한다
    private static void AssignPlacementUIReferences(TurretPlacementUI placementUI, TurretPlacementController placementController, Transform slotContainer)
    {
        SerializedObject serializedObject = new SerializedObject(placementUI);
        serializedObject.FindProperty("placementController").objectReferenceValue = placementController;
        serializedObject.FindProperty("slotContainer").objectReferenceValue = slotContainer;
        serializedObject.FindProperty("rebuildOnStart").boolValue = false;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    // TurretPlacementSlotUI의 직렬화된 참조를 연결한다
    private static void AssignPlacementSlotReferences(TurretPlacementSlotUI slotUI, TurretPlacementController placementController, TurretShopEntrySO entry, Image iconImage, TMP_Text nameText, TMP_Text costText)
    {
        SerializedObject serializedObject = new SerializedObject(slotUI);
        serializedObject.FindProperty("iconImage").objectReferenceValue = iconImage;
        serializedObject.FindProperty("tmpNameText").objectReferenceValue = nameText;
        serializedObject.FindProperty("tmpCostText").objectReferenceValue = costText;
        serializedObject.FindProperty("placementController").objectReferenceValue = placementController;
        serializedObject.FindProperty("shopEntry").objectReferenceValue = entry;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    // TurretTemporaryUpgradePopupUI의 직렬화된 UI 참조를 연결한다
    private static void AssignUpgradePopupReferences(
        TurretTemporaryUpgradePopupUI popupUI,
        RectTransform popupPanel,
        Button backgroundButton,
        TMP_Text titleText,
        TMP_Text levelText,
        TMP_Text statusText,
        Toggle spendCurrencyToggle,
        TMP_Text currentStatText,
        TMP_Text nextStatText,
        Button levelUpButton,
        TMP_Text levelUpButtonText,
        RectTransform engineerSeatContainer,
        int engineerSeatTriggerCount,
        TurretEngineerSeatButton[] engineerSeatButtons,
        RectTransform evolutionButtonContainer,
        Button[] evolutionButtons,
        Image[] evolutionIcons,
        TMP_Text[] evolutionLabels)
    {
        SerializedObject serializedObject = new SerializedObject(popupUI);
        serializedObject.FindProperty("popupPanel").objectReferenceValue = popupPanel;
        serializedObject.FindProperty("backgroundButton").objectReferenceValue = backgroundButton;
        serializedObject.FindProperty("titleText").objectReferenceValue = titleText;
        serializedObject.FindProperty("levelText").objectReferenceValue = levelText;
        serializedObject.FindProperty("statusText").objectReferenceValue = statusText;
        serializedObject.FindProperty("spendCurrencyToggle").objectReferenceValue = spendCurrencyToggle;
        serializedObject.FindProperty("currentStatText").objectReferenceValue = currentStatText;
        serializedObject.FindProperty("nextStatText").objectReferenceValue = nextStatText;
        serializedObject.FindProperty("levelUpButton").objectReferenceValue = levelUpButton;
        serializedObject.FindProperty("levelUpButtonText").objectReferenceValue = levelUpButtonText;
        serializedObject.FindProperty("engineerSeatContainer").objectReferenceValue = engineerSeatContainer;
        serializedObject.FindProperty("engineerSeatTriggerCount").intValue = Mathf.Max(0, engineerSeatTriggerCount);
        AssignObjectArray(serializedObject.FindProperty("engineerSeatButtons"), engineerSeatButtons);
        serializedObject.FindProperty("evolutionButtonContainer").objectReferenceValue = evolutionButtonContainer;
        AssignObjectArray(serializedObject.FindProperty("evolutionButtons"), evolutionButtons);
        AssignObjectArray(serializedObject.FindProperty("evolutionButtonIcons"), evolutionIcons);
        AssignObjectArray(serializedObject.FindProperty("evolutionButtonLabels"), evolutionLabels);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    // SerializedProperty 배열에 오브젝트 참조 배열을 복사한다
    private static void AssignObjectArray(SerializedProperty arrayProperty, Object[] objects)
    {
        if (arrayProperty == null || objects == null)
        {
            return;
        }

        arrayProperty.arraySize = objects.Length;
        for (int i = 0; i < objects.Length; i++)
        {
            arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
        }
    }
}
