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

    [MenuItem(MENU_PATH)]
    // 메뉴 실행 시 생존자 상호작용 Canvas와 하위 UI를 현재 씬에 생성하거나 기존 UI를 선택한다
    private static void CreateSurvivorInteractionUI()
    {
        SurvivorInteractionController existingController = Object.FindFirstObjectByType<SurvivorInteractionController>();
        if (existingController != null)
        {
            if (HasValidPopupPanel(existingController))
            {
                Selection.activeGameObject = existingController.gameObject;
                EditorGUIUtility.PingObject(existingController.gameObject);
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

        AssignControllerReferences(controller, panelObject, titleText, statusText, treatmentButton, constructionWorkerButton, engineerButton);

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
        return popupPanelProperty != null && popupPanelProperty.objectReferenceValue != null;
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
        rootRect.sizeDelta = new Vector2(760.0f, 420.0f);
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
    private static void AssignControllerReferences(SurvivorInteractionController controller, GameObject popupPanel, TMP_Text titleText, TMP_Text statusText, Button treatmentButton, Button constructionWorkerButton, Button engineerButton)
    {
        SerializedObject serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("popupPanel").objectReferenceValue = popupPanel;
        serializedObject.FindProperty("titleText").objectReferenceValue = titleText;
        serializedObject.FindProperty("statusText").objectReferenceValue = statusText;
        serializedObject.FindProperty("treatmentButton").objectReferenceValue = treatmentButton;
        serializedObject.FindProperty("constructionWorkerButton").objectReferenceValue = constructionWorkerButton;
        serializedObject.FindProperty("engineerButton").objectReferenceValue = engineerButton;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
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
