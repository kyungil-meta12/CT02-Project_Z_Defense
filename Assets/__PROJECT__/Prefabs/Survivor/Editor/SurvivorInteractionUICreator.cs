using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 생존자 상호작용 팝업 UI를 현재 씬에 에디터 오브젝트로 배치하는 메뉴를 제공한다.
internal static class SurvivorInteractionUICreator
{
    private const string MENU_PATH = "Project Z Defense/UI/Create Survivor Interaction UI";
    private const string CANVAS_NAME = "SurvivorInteractionCanvas";
    private const string ROOT_NAME = "SurvivorInteractionPopup";
    private const int ENGINEER_TARGET_SLOT_COUNT = 8;

#if PROJECTZ_ENABLE_LEGACY_UI_CREATORS
    [MenuItem(MENU_PATH)]
#endif
    // 메뉴 실행 시 생존자 상호작용 Canvas와 하위 UI를 현재 씬에 생성하거나 기존 UI를 선택한다
    private static void CreateSurvivorInteractionUI()
    {
        SurvivorInteractionController existingController = Object.FindFirstObjectByType<SurvivorInteractionController>();
        if (existingController != null)
        {
            if (HasValidPopupPanel(existingController))
            {
                TryFillMissingEngineerTargetSlots(existingController);
                Selection.activeGameObject = existingController.gameObject;
                EditorGUIUtility.PingObject(existingController.gameObject);
                return;
            }

            if (TryAppendMissingEngineerBuffTargetPanel(existingController))
            {
                Selection.activeGameObject = existingController.gameObject;
                EditorGUIUtility.PingObject(existingController.gameObject);
                EditorSceneManager.MarkSceneDirty(existingController.gameObject.scene);
                return;
            }

            Undo.DestroyObjectImmediate(existingController);
        }

        GameObject canvasObject = CreateCanvas();
        GameObject rootObject = CreatePopupRoot(canvasObject.transform);
        SurvivorInteractionController controller = rootObject.AddComponent<SurvivorInteractionController>();
        GameObject panelObject = CreatePopupPanel(rootObject.transform);

        TMP_Text titleText = CreateText("Title", panelObject.transform, "Survivor", 30, FontStyles.Bold, TextAlignmentOptions.Left);
        TMP_Text statusText = CreateText("Status", panelObject.transform, "No survivor selected.", 22, FontStyles.Bold, TextAlignmentOptions.Left);
        Button treatmentButton = CreateButton("TreatmentButton", panelObject.transform, "Treat", new Color(0.18f, 0.45f, 0.72f, 0.95f));
        Button constructionWorkerButton = CreateButton("ConstructionWorkerButton", panelObject.transform, "Construction Worker", new Color(0.28f, 0.5f, 0.28f, 0.95f));
        Button engineerButton = CreateButton("EngineerButton", panelObject.transform, "Engineer", new Color(0.55f, 0.42f, 0.18f, 0.95f));
        EngineerBuffTargetPanelUI engineerBuffTargetPanel = CreateEngineerBuffTargetPanel(rootObject.transform);

        AssignControllerReferences(controller, panelObject, titleText, statusText, treatmentButton, constructionWorkerButton, engineerButton, engineerBuffTargetPanel);

        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Survivor Interaction UI");
        Selection.activeGameObject = rootObject;
        EditorGUIUtility.PingObject(rootObject);
        EditorSceneManager.MarkSceneDirty(rootObject.scene);
    }

    // 기존 컨트롤러가 표시/숨김 대상 패널을 참조하는지 확인한다
    private static bool HasValidPopupPanel(SurvivorInteractionController controller)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        SerializedProperty popupPanelProperty = serializedObject.FindProperty("popupPanel");
        SerializedProperty engineerBuffTargetPanelProperty = serializedObject.FindProperty("engineerBuffTargetPanel");
        return popupPanelProperty != null && popupPanelProperty.objectReferenceValue != null && engineerBuffTargetPanelProperty != null && engineerBuffTargetPanelProperty.objectReferenceValue != null;
    }

    // 기존 생존자 UI에 엔지니어 버프 대상 패널만 없으면 하위 오브젝트로 추가한다
    private static bool TryAppendMissingEngineerBuffTargetPanel(SurvivorInteractionController controller)
    {
        if (controller == null)
        {
            return false;
        }

        SerializedObject serializedObject = new SerializedObject(controller);
        SerializedProperty popupPanelProperty = serializedObject.FindProperty("popupPanel");
        SerializedProperty engineerBuffTargetPanelProperty = serializedObject.FindProperty("engineerBuffTargetPanel");
        if (popupPanelProperty == null || popupPanelProperty.objectReferenceValue == null || engineerBuffTargetPanelProperty == null || engineerBuffTargetPanelProperty.objectReferenceValue != null)
        {
            return false;
        }

        EngineerBuffTargetPanelUI engineerBuffTargetPanel = CreateEngineerBuffTargetPanel(controller.transform);
        Undo.RegisterCreatedObjectUndo(engineerBuffTargetPanel.gameObject, "Create Engineer Buff Target Panel");
        engineerBuffTargetPanelProperty.objectReferenceValue = engineerBuffTargetPanel;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // 기존 엔지니어 패널의 터렛 베이스와 대상 버튼 배열을 고정 슬롯 수에 맞춘다
    private static bool TryFillMissingEngineerTargetSlots(SurvivorInteractionController controller)
    {
        if (controller == null)
        {
            return false;
        }

        SerializedObject controllerObject = new SerializedObject(controller);
        SerializedProperty panelProperty = controllerObject.FindProperty("engineerBuffTargetPanel");
        EngineerBuffTargetPanelUI panel = panelProperty == null ? null : panelProperty.objectReferenceValue as EngineerBuffTargetPanelUI;
        if (panel == null)
        {
            return false;
        }

        SerializedObject panelObject = new SerializedObject(panel);
        SerializedProperty targetSlotsProperty = panelObject.FindProperty("targetSlots");
        SerializedProperty targetButtonsProperty = panelObject.FindProperty("targetButtons");
        if (targetSlotsProperty == null || targetButtonsProperty == null)
        {
            return false;
        }

        TurretBaseSlot[] targetSlots = FindSceneTurretSlots();
        if (!NeedsEngineerTargetRefresh(targetSlotsProperty, targetButtonsProperty))
        {
            return false;
        }

        Transform buttonContainer = FindEngineerTargetButtonContainer(panel.transform);
        if (buttonContainer == null)
        {
            return false;
        }

        EnsureGridLayout(buttonContainer);
        EngineerBuffTargetButton[] targetButtons = EnsureEditorTargetButtons(buttonContainer, ENGINEER_TARGET_SLOT_COUNT);
        AssignObjectArray(targetSlotsProperty, targetSlots);
        AssignObjectArray(targetButtonsProperty, targetButtons);
        panelObject.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        return true;
    }

    // 엔지니어 대상 슬롯이나 버튼 배열을 다시 채워야 하는지 확인한다
    private static bool NeedsEngineerTargetRefresh(SerializedProperty targetSlotsProperty, SerializedProperty targetButtonsProperty)
    {
        if (targetSlotsProperty.arraySize != ENGINEER_TARGET_SLOT_COUNT || targetButtonsProperty.arraySize != ENGINEER_TARGET_SLOT_COUNT)
        {
            return true;
        }

        for (int i = 0; i < ENGINEER_TARGET_SLOT_COUNT; i++)
        {
            if (targetButtonsProperty.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                return true;
            }
        }

        return false;
    }

    // 엔지니어 대상 버튼 컨테이너를 하위 경로에서 찾는다
    private static Transform FindEngineerTargetButtonContainer(Transform panelTransform)
    {
        if (panelTransform == null)
        {
            return null;
        }

        Transform found = panelTransform.Find("TargetScrollView/Viewport/TargetButtonContainer");
        return found != null ? found : panelTransform;
    }

    // 생존자 상호작용 UI를 담을 Screen Space Overlay Canvas를 생성한다
    private static GameObject CreateCanvas()
    {
        GameObject canvasObject = new GameObject(CANVAS_NAME, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 85;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(900.0f, 2000.0f);
        canvasScaler.matchWidthOrHeight = 1.0f;
        return canvasObject;
    }

    // 항상 활성 상태로 유지할 컨트롤러 루트 오브젝트를 생성한다
    private static GameObject CreatePopupRoot(Transform parent)
    {
        GameObject rootObject = CreateUIObject(ROOT_NAME, parent);
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.0f);
        rootRect.anchorMax = new Vector2(0.5f, 0.0f);
        rootRect.pivot = new Vector2(0.5f, 0.0f);
        rootRect.anchoredPosition = new Vector2(0.0f, 650.0f);
        rootRect.sizeDelta = new Vector2(760.0f, 620.0f);
        return rootObject;
    }

    // 실제 표시와 숨김 대상이 되는 팝업 패널을 생성한다
    private static GameObject CreatePopupPanel(Transform parent)
    {
        GameObject panelObject = CreateUIObject("Panel", parent);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image background = panelObject.AddComponent<Image>();
        background.color = new Color(0.06f, 0.07f, 0.08f, 0.94f);

        VerticalLayoutGroup layoutGroup = panelObject.AddComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(24, 24, 22, 22);
        layoutGroup.spacing = 12.0f;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
        return panelObject;
    }

    // SurvivorInteractionController의 직렬화된 UI 참조를 연결한다
    private static void AssignControllerReferences(SurvivorInteractionController controller, GameObject popupPanel, TMP_Text titleText, TMP_Text statusText, Button treatmentButton, Button constructionWorkerButton, Button engineerButton, EngineerBuffTargetPanelUI engineerBuffTargetPanel)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("popupPanel").objectReferenceValue = popupPanel;
        serializedObject.FindProperty("titleText").objectReferenceValue = titleText;
        serializedObject.FindProperty("statusText").objectReferenceValue = statusText;
        serializedObject.FindProperty("treatmentButton").objectReferenceValue = treatmentButton;
        serializedObject.FindProperty("constructionWorkerButton").objectReferenceValue = constructionWorkerButton;
        serializedObject.FindProperty("engineerButton").objectReferenceValue = engineerButton;
        serializedObject.FindProperty("engineerBuffTargetPanel").objectReferenceValue = engineerBuffTargetPanel;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    // 엔지니어 버프 대상 선택 패널과 재사용 버튼 프리팹을 생성한다
    private static EngineerBuffTargetPanelUI CreateEngineerBuffTargetPanel(Transform parent)
    {
        GameObject panelObject = CreatePopupPanel(parent);
        panelObject.name = "EngineerBuffTargetPanel";

        TMP_Text titleText = CreateText("Title", panelObject.transform, "Select Buff Target", 30, FontStyles.Bold, TextAlignmentOptions.Left);
        TMP_Text statusText = CreateText("Status", panelObject.transform, "Choose a turret to buff.", 22, FontStyles.Bold, TextAlignmentOptions.Left);

        GameObject scrollObject = CreateUIObject("TargetScrollView", panelObject.transform);
        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.color = new Color(0.03f, 0.04f, 0.05f, 0.65f);
        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        AddLayoutElement(scrollObject, 0.0f, 360.0f);

        GameObject viewportObject = CreateUIObject("Viewport", scrollObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = Color.clear;
        Mask viewportMask = viewportObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject buttonContainerObject = CreateUIObject("TargetButtonContainer", viewportObject.transform);
        RectTransform buttonContainerRect = buttonContainerObject.GetComponent<RectTransform>();
        buttonContainerRect.anchorMin = new Vector2(0.0f, 1.0f);
        buttonContainerRect.anchorMax = new Vector2(1.0f, 1.0f);
        buttonContainerRect.pivot = new Vector2(0.5f, 1.0f);
        buttonContainerRect.offsetMin = new Vector2(12.0f, 0.0f);
        buttonContainerRect.offsetMax = new Vector2(-12.0f, 0.0f);
        scrollRect.viewport = viewportRect;
        scrollRect.content = buttonContainerRect;

        GridLayoutGroup buttonLayoutGroup = buttonContainerObject.AddComponent<GridLayoutGroup>();
        buttonLayoutGroup.cellSize = new Vector2(220.0f, 76.0f);
        buttonLayoutGroup.spacing = new Vector2(10.0f, 10.0f);
        buttonLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        buttonLayoutGroup.constraintCount = 3;
        ContentSizeFitter contentSizeFitter = buttonContainerObject.AddComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TurretBaseSlot[] targetSlots = FindSceneTurretSlots();
        EngineerBuffTargetButton[] targetButtons = CreateTargetButtons(buttonContainerObject.transform, ENGINEER_TARGET_SLOT_COUNT);

        Button closeButton = CreateButton("CloseButton", panelObject.transform, "Cancel", new Color(0.35f, 0.35f, 0.38f, 0.95f));
        EngineerBuffTargetPanelUI panel = panelObject.AddComponent<EngineerBuffTargetPanelUI>();

        SerializedObject serializedObject = new SerializedObject(panel);
        serializedObject.FindProperty("panelRoot").objectReferenceValue = panelObject;
        serializedObject.FindProperty("titleText").objectReferenceValue = titleText;
        serializedObject.FindProperty("statusText").objectReferenceValue = statusText;
        serializedObject.FindProperty("closeButton").objectReferenceValue = closeButton;
        AssignObjectArray(serializedObject.FindProperty("targetSlots"), targetSlots);
        AssignObjectArray(serializedObject.FindProperty("targetButtons"), targetButtons);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        panelObject.SetActive(false);
        return panel;
    }

    // 현재 씬의 터렛 베이스 슬롯을 찾는다
    private static TurretBaseSlot[] FindSceneTurretSlots()
    {
        TurretBaseSlot[] foundSlots = Object.FindObjectsByType<TurretBaseSlot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        System.Array.Sort(foundSlots, CompareTurretBaseSlotName);

        TurretBaseSlot[] targetSlots = new TurretBaseSlot[ENGINEER_TARGET_SLOT_COUNT];
        int count = Mathf.Min(foundSlots.Length, ENGINEER_TARGET_SLOT_COUNT);
        for (int i = 0; i < count; i++)
        {
            targetSlots[i] = foundSlots[i];
        }

        return targetSlots;
    }

    // 터렛 베이스 이름 기준으로 슬롯 표시 순서를 정렬한다
    private static int CompareTurretBaseSlotName(TurretBaseSlot left, TurretBaseSlot right)
    {
        string leftName = left == null ? string.Empty : left.name;
        string rightName = right == null ? string.Empty : right.name;
        return string.CompareOrdinal(leftName, rightName);
    }

    // 터렛 베이스 수만큼 대상 버튼을 미리 생성한다
    private static EngineerBuffTargetButton[] CreateTargetButtons(Transform parent, int count)
    {
        EngineerBuffTargetButton[] buttons = new EngineerBuffTargetButton[count];
        for (int i = 0; i < count; i++)
        {
            Button targetButton = CreateButton("TargetButton_" + (i + 1).ToString("00"), parent, string.Empty, new Color(0.2f, 0.32f, 0.45f, 0.95f));
            EngineerBuffTargetButton targetButtonComponent = targetButton.gameObject.AddComponent<EngineerBuffTargetButton>();
            targetButtonComponent.Clear();
            targetButton.gameObject.SetActive(false);
            buttons[i] = targetButtonComponent;
        }

        return buttons;
    }

    // 기존 컨테이너 하위 버튼을 재사용하고 부족한 수만큼 미리 생성한다
    private static EngineerBuffTargetButton[] EnsureEditorTargetButtons(Transform parent, int count)
    {
        if (parent == null || count <= 0)
        {
            return System.Array.Empty<EngineerBuffTargetButton>();
        }

        EngineerBuffTargetButton[] existingButtons = parent.GetComponentsInChildren<EngineerBuffTargetButton>(true);
        EngineerBuffTargetButton[] buttons = new EngineerBuffTargetButton[count];
        int existingCount = Mathf.Min(existingButtons.Length, count);
        for (int i = 0; i < existingCount; i++)
        {
            buttons[i] = existingButtons[i];
            buttons[i].Clear();
            buttons[i].gameObject.SetActive(false);
        }

        for (int i = existingCount; i < count; i++)
        {
            Button targetButton = CreateButton("TargetButton_" + (i + 1).ToString("00"), parent, string.Empty, new Color(0.2f, 0.32f, 0.45f, 0.95f));
            EngineerBuffTargetButton targetButtonComponent = targetButton.gameObject.AddComponent<EngineerBuffTargetButton>();
            targetButtonComponent.Clear();
            targetButton.gameObject.SetActive(false);
            buttons[i] = targetButtonComponent;
        }

        return buttons;
    }

    // 대상 버튼 컨테이너를 그리드 레이아웃으로 맞춘다
    private static void EnsureGridLayout(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        VerticalLayoutGroup verticalLayoutGroup = parent.GetComponent<VerticalLayoutGroup>();
        if (verticalLayoutGroup != null)
        {
            Object.DestroyImmediate(verticalLayoutGroup);
        }

        GridLayoutGroup gridLayoutGroup = parent.GetComponent<GridLayoutGroup>();
        if (gridLayoutGroup == null)
        {
            gridLayoutGroup = parent.gameObject.AddComponent<GridLayoutGroup>();
        }

        gridLayoutGroup.cellSize = new Vector2(220.0f, 76.0f);
        gridLayoutGroup.spacing = new Vector2(10.0f, 10.0f);
        gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayoutGroup.constraintCount = 3;

        ContentSizeFitter contentSizeFitter = parent.GetComponent<ContentSizeFitter>();
        if (contentSizeFitter == null)
        {
            contentSizeFitter = parent.gameObject.AddComponent<ContentSizeFitter>();
        }

        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // 직렬화 배열 프로퍼티에 오브젝트 배열을 할당한다
    private static void AssignObjectArray<T>(SerializedProperty arrayProperty, T[] values) where T : Object
    {
        if (arrayProperty == null || !arrayProperty.isArray || values == null)
        {
            return;
        }

        arrayProperty.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
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
        AddLayoutElement(textObject, 0.0f, fontSize + 10.0f);
        return tmpText;
    }

    // 기본 이미지와 라벨이 있는 버튼을 생성한다
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
        AddLayoutElement(buttonObject, 0.0f, 76.0f);
        return button;
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
}
