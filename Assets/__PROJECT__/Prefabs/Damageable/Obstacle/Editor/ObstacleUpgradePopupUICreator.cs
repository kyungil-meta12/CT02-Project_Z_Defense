using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 장애물 업그레이드 팝업 UI를 현재 씬에 에디터 오브젝트로 배치하는 메뉴를 제공한다.
internal static class ObstacleUpgradePopupUICreator
{
    private const string MENU_PATH = "Project Z Defense/UI/Create Obstacle Upgrade Popup UI";
    private const string CANVAS_NAME = "ObstacleUpgradePopupCanvas";
    private const string POPUP_NAME = "ObstacleUpgradePopup";

#if PROJECTZ_ENABLE_LEGACY_UI_CREATORS
    [MenuItem(MENU_PATH)]
#endif
    // 메뉴 실행 시 장애물 업그레이드 팝업 Canvas와 하위 UI를 현재 씬에 생성하거나 기존 팝업을 선택한다
    private static void CreateObstacleUpgradePopupUI()
    {
        ObstacleUpgradePopupUI existingPopup = Object.FindFirstObjectByType<ObstacleUpgradePopupUI>();
        if (existingPopup != null)
        {
            if (HasValidPopupRoot(existingPopup))
            {
                Selection.activeGameObject = existingPopup.gameObject;
                EditorGUIUtility.PingObject(existingPopup.gameObject);
                return;
            }

            Undo.DestroyObjectImmediate(existingPopup);
        }

        GameObject canvasObject = CreateCanvas();
        GameObject popupObject = CreatePopupRoot(canvasObject.transform);
        ObstacleUpgradePopupUI popupUI = popupObject.AddComponent<ObstacleUpgradePopupUI>();
        Button backgroundButton = CreateTransparentBackgroundButton(popupObject.transform);
        GameObject panelObject = CreatePopupPanel(backgroundButton.transform);

        TMP_Text titleText = CreateText("Title", panelObject.transform, "Obstacle", 30, FontStyles.Bold, TextAlignmentOptions.Left);
        TMP_Text levelText = CreateText("Level", panelObject.transform, "Lv.", 22, FontStyles.Normal, TextAlignmentOptions.Left);
        TMP_Text hpText = CreateText("Hp", panelObject.transform, "HP", 22, FontStyles.Normal, TextAlignmentOptions.Left);
        TMP_Text costText = CreateText("Cost", panelObject.transform, "Cost: None", 22, FontStyles.Normal, TextAlignmentOptions.Left);
        TMP_Text statusText = CreateText("Status", panelObject.transform, "No obstacle selected.", 22, FontStyles.Bold, TextAlignmentOptions.Left);
        Button upgradeButton = CreateButton("UpgradeButton", panelObject.transform, "Upgrade Unavailable");
        TMP_Text upgradeButtonText = upgradeButton.GetComponentInChildren<TMP_Text>(true);
        AddLayoutElement(upgradeButton.gameObject, 0.0f, 76.0f);

        AssignPopupReferences(popupUI, backgroundButton.GetComponent<RectTransform>(), backgroundButton, titleText, levelText, hpText, costText, statusText, upgradeButton, upgradeButtonText);

        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Obstacle Upgrade Popup UI");
        Selection.activeGameObject = popupObject;
        EditorGUIUtility.PingObject(popupObject);
        EditorSceneManager.MarkSceneDirty(popupObject.scene);
    }

    // 기존 팝업 컴포넌트가 실제 팝업 루트를 참조하는지 확인한다
    private static bool HasValidPopupRoot(ObstacleUpgradePopupUI popupUI)
    {
        SerializedObject serializedObject = new SerializedObject(popupUI);
        SerializedProperty popupRootProperty = serializedObject.FindProperty("popupPanel");
        SerializedProperty backgroundButtonProperty = serializedObject.FindProperty("backgroundButton");
        return popupRootProperty != null && popupRootProperty.objectReferenceValue != null &&
               backgroundButtonProperty != null && backgroundButtonProperty.objectReferenceValue != null;
    }

    // 팝업을 담을 Screen Space Overlay Canvas를 생성한다
    private static GameObject CreateCanvas()
    {
        GameObject canvasObject = new GameObject(CANVAS_NAME, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 90;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(900.0f, 2000.0f);
        canvasScaler.matchWidthOrHeight = 1.0f;
        return canvasObject;
    }

    // 항상 활성 상태로 유지할 팝업 컨트롤러 루트 오브젝트를 생성한다
    private static GameObject CreatePopupRoot(Transform parent)
    {
        GameObject popupObject = CreateUIObject(POPUP_NAME, parent);
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

    // 투명 닫기 버튼 하위의 실제 장애물 업그레이드 팝업 패널을 생성한다
    private static GameObject CreatePopupPanel(Transform parent)
    {
        GameObject panelObject = CreateUIObject("Panel", parent);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.0f);
        panelRect.anchorMax = new Vector2(0.5f, 0.0f);
        panelRect.pivot = new Vector2(0.5f, 0.0f);
        panelRect.anchoredPosition = new Vector2(0.0f, 280.0f);
        panelRect.sizeDelta = new Vector2(760.0f, 360.0f);

        Image background = panelObject.AddComponent<Image>();
        background.color = new Color(0.05f, 0.06f, 0.07f, 0.94f);

        VerticalLayoutGroup rootLayout = panelObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(24, 24, 20, 20);
        rootLayout.spacing = 10.0f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = false;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;
        return panelObject;
    }

    // ObstacleUpgradePopupUI의 직렬화된 UI 참조를 연결한다
    private static void AssignPopupReferences(ObstacleUpgradePopupUI popupUI, RectTransform popupRoot, Button backgroundButton, TMP_Text titleText, TMP_Text levelText, TMP_Text hpText, TMP_Text costText, TMP_Text statusText, Button upgradeButton, TMP_Text upgradeButtonText)
    {
        SerializedObject serializedObject = new SerializedObject(popupUI);
        serializedObject.FindProperty("popupPanel").objectReferenceValue = popupRoot;
        serializedObject.FindProperty("backgroundButton").objectReferenceValue = backgroundButton;
        serializedObject.FindProperty("titleText").objectReferenceValue = titleText;
        serializedObject.FindProperty("levelText").objectReferenceValue = levelText;
        serializedObject.FindProperty("hpText").objectReferenceValue = hpText;
        serializedObject.FindProperty("costText").objectReferenceValue = costText;
        serializedObject.FindProperty("statusText").objectReferenceValue = statusText;
        serializedObject.FindProperty("upgradeButton").objectReferenceValue = upgradeButton;
        serializedObject.FindProperty("upgradeButtonText").objectReferenceValue = upgradeButtonText;
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
        AddLayoutElement(textObject, 0.0f, fontSize + 8.0f);
        return tmpText;
    }

    // 기본 이미지와 라벨이 있는 버튼을 생성한다
    private static Button CreateButton(string objectName, Transform parent, string label)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.5f, 0.35f, 0.95f);

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
