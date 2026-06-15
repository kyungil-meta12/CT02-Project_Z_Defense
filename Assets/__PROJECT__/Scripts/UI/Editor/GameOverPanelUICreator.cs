using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 게임오버 페이드 패널 UI를 현재 씬에 에디터 오브젝트로 배치하는 메뉴를 제공한다.
internal static class GameOverPanelUICreator
{
    private const string MENU_PATH = "Project Z Defense/UI/Create Game Over Panel UI";
    private const string CANVAS_NAME = "GameOverPanelCanvas";
    private const string ROOT_NAME = "GameOverPanelController";

    [MenuItem(MENU_PATH)]
    // 메뉴 실행 시 게임오버 페이드 Canvas와 하위 UI를 현재 씬에 생성하거나 기존 UI를 선택한다
    private static void CreateGameOverPanelUI()
    {
        GameOverPanelUI existingPanel = Object.FindFirstObjectByType<GameOverPanelUI>();
        if (existingPanel != null)
        {
            if (HasValidPanelRoot(existingPanel))
            {
                Selection.activeGameObject = existingPanel.gameObject;
                EditorGUIUtility.PingObject(existingPanel.gameObject);
                return;
            }

            Undo.DestroyObjectImmediate(existingPanel);
        }

        GameObject canvasObject = CreateCanvas();
        GameObject rootObject = CreateControllerRoot(canvasObject.transform);
        GameOverPanelUI panelUI = rootObject.AddComponent<GameOverPanelUI>();
        GameObject panelObject = CreateFadePanel(rootObject.transform);
        CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();

        TMP_Text titleText = CreateText("Title", panelObject.transform, "Game Over", 64, FontStyles.Bold, TextAlignmentOptions.Center);
        TMP_Text statusText = CreateText("Status", panelObject.transform, "Preparing previous wave...", 28, FontStyles.Normal, TextAlignmentOptions.Center);
        _ = titleText;
        _ = statusText;

        AssignPanelReferences(panelUI, canvasGroup, panelObject);

        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Game Over Panel UI");
        Selection.activeGameObject = rootObject;
        EditorGUIUtility.PingObject(rootObject);
        EditorSceneManager.MarkSceneDirty(rootObject.scene);
    }

    // 기존 게임오버 패널이 실제 패널 루트를 참조하는지 확인한다
    private static bool HasValidPanelRoot(GameOverPanelUI panelUI)
    {
        SerializedObject serializedObject = new SerializedObject(panelUI);
        SerializedProperty panelRootProperty = serializedObject.FindProperty("panelRoot");
        return panelRootProperty != null && panelRootProperty.objectReferenceValue != null;
    }

    // 게임오버 UI를 담을 Screen Space Overlay Canvas를 생성한다
    private static GameObject CreateCanvas()
    {
        GameObject canvasObject = new GameObject(CANVAS_NAME, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 200;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(900.0f, 2000.0f);
        canvasScaler.matchWidthOrHeight = 1.0f;
        return canvasObject;
    }

    // 항상 활성 상태로 유지할 게임오버 컨트롤러 루트를 생성한다
    private static GameObject CreateControllerRoot(Transform parent)
    {
        GameObject rootObject = CreateUIObject(ROOT_NAME, parent);
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        return rootObject;
    }

    // 투명도 페이드 대상이 되는 전체 화면 패널을 생성한다
    private static GameObject CreateFadePanel(Transform parent)
    {
        GameObject panelObject = CreateUIObject("Panel", parent);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image background = panelObject.AddComponent<Image>();
        background.color = new Color(0.0f, 0.0f, 0.0f, 0.92f);

        CanvasGroup canvasGroup = panelObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        VerticalLayoutGroup layoutGroup = panelObject.AddComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(48, 48, 0, 0);
        layoutGroup.spacing = 28.0f;
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
        return panelObject;
    }

    // GameOverPanelUI의 직렬화된 참조를 연결한다
    private static void AssignPanelReferences(GameOverPanelUI panelUI, CanvasGroup canvasGroup, GameObject panelRoot)
    {
        SerializedObject serializedObject = new SerializedObject(panelUI);
        serializedObject.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        serializedObject.FindProperty("panelRoot").objectReferenceValue = panelRoot;
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
        AddLayoutElement(textObject, 0.0f, fontSize + 18.0f);
        return tmpText;
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
