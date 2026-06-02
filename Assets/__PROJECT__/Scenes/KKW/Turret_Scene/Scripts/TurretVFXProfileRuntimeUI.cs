using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(TurretStatProfileApplier))]
public class TurretVFXProfileRuntimeUI : MonoBehaviour
{
    private static readonly System.Collections.Generic.List<TurretVFXProfileRuntimeUI> ActiveRuntimes = new System.Collections.Generic.List<TurretVFXProfileRuntimeUI>(16);

    [SerializeField] private Turret targetTurret;
    [SerializeField] private FiringEvent targetFiringEvent;
    [SerializeField] private TurretStatProfileApplier statProfileApplier;
    [SerializeField] private TurretVFXProfileSO[] profiles;
    [FormerlySerializedAs("statProfile")]
    [SerializeField, HideInInspector] private TurretStatProfileSO legacyStatProfile;
    [FormerlySerializedAs("currentIndex")]
    [SerializeField, Min(0)] private int profileIndex = 0;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool applyOnInspectorChange = true;
    [SerializeField] private string currentProfileName;
    [Header("Runtime UI")]
    [SerializeField] private bool createRuntimeUI = true;
    [SerializeField] private Vector2 uiAnchoredPosition = new Vector2(20.0f, -20.0f);
    [SerializeField] private Vector2 uiSize = new Vector2(360.0f, 150.0f);

    private Text profileNameText;
    private Text autoFireText;
    private bool autoFireEnabled = true;
    private bool isApplyingSharedProfile;

    public TurretVFXProfileSO CurrentProfile
    {
        get
        {
            if (profiles == null || profiles.Length == 0)
            {
                return null;
            }

            return profiles[Mathf.Clamp(profileIndex, 0, profiles.Length - 1)];
        }
    }

    private void Reset()
    {
        targetTurret = GetComponent<Turret>();
        targetFiringEvent = GetComponent<FiringEvent>();
        statProfileApplier = GetComponent<TurretStatProfileApplier>();
    }

    private void OnValidate()
    {
        ClampProfileIndex();
        RefreshCurrentProfileName();

        if (Application.isPlaying && applyOnInspectorChange)
        {
            ApplyCurrentProfile();
        }
    }

    private void Start()
    {
        ClampProfileIndex();

        if (applyOnStart)
        {
            ApplyCurrentProfile();
        }

        if (createRuntimeUI)
        {
            CreateRuntimeUI();
            RefreshRuntimeUI();
        }
    }

    private void OnEnable()
    {
        if (!ActiveRuntimes.Contains(this))
        {
            ActiveRuntimes.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveRuntimes.Remove(this);
    }

    [ContextMenu("Apply Current Profile")]
    public void ApplyCurrentProfile()
    {
        EnsureReferences();

        TurretVFXProfileSO profile = CurrentProfile;
        if (profile == null)
        {
            Debug.LogWarning("[TurretVFXProfileRuntimeUI] No VFX profile assigned.", this);
            return;
        }

        if (targetTurret != null)
        {
            targetTurret.SetProjectilePrefab(profile.projectilePrefab, 0.0f);
        }

        if (targetFiringEvent != null)
        {
            targetFiringEvent.muzzleVFX = profile.muzzleVFX;
            targetFiringEvent.muzzleVFXDuration = profile.muzzleVFXDuration;
            targetFiringEvent.firingSound = profile.fireSound;
        }

        ApplyStatProfileApplier();

        RefreshCurrentProfileName();
        RefreshRuntimeUI();
        Debug.Log($"[TurretVFXProfileRuntimeUI] Applied profile: {profile.displayName}", this);
    }

    private void ApplyStatProfileApplier()
    {
        EnsureReferences();

        if (statProfileApplier == null)
        {
            return;
        }

        if (!statProfileApplier.HasStatProfile && legacyStatProfile != null)
        {
            statProfileApplier.SetStatProfile(legacyStatProfile);
        }

        statProfileApplier.SetAutoFireEnabled(autoFireEnabled);
        statProfileApplier.Apply();
    }

    private void EnsureReferences()
    {
        if (targetTurret == null)
        {
            targetTurret = GetComponent<Turret>();
        }

        if (targetFiringEvent == null)
        {
            targetFiringEvent = GetComponent<FiringEvent>();
        }

        if (statProfileApplier == null)
        {
            statProfileApplier = GetComponent<TurretStatProfileApplier>();
        }

        if (statProfileApplier == null && legacyStatProfile != null)
        {
            statProfileApplier = gameObject.AddComponent<TurretStatProfileApplier>();
        }
    }

    public void SetProfileIndex(int index)
    {
        SetSharedProfileIndex(index);
    }

    public void FireOnce()
    {
        if (targetTurret != null)
        {
            targetTurret.FireOnce();
        }
    }

    public void ToggleAutoFire()
    {
        autoFireEnabled = !autoFireEnabled;

        EnsureReferences();

        if (statProfileApplier != null)
        {
            statProfileApplier.SetAutoFireEnabled(autoFireEnabled);
        }
        else if (targetTurret != null)
        {
            targetTurret.SetAutoFireEnabled(autoFireEnabled);
        }

        RefreshRuntimeUI();
    }

    [ContextMenu("Next Profile")]
    public void NextProfile()
    {
        if (profiles == null || profiles.Length == 0)
        {
            return;
        }

        SetSharedProfileIndex((profileIndex + 1) % profiles.Length);
    }

    [ContextMenu("Previous Profile")]
    public void PreviousProfile()
    {
        if (profiles == null || profiles.Length == 0)
        {
            return;
        }

        int nextProfileIndex = profileIndex - 1;
        if (nextProfileIndex < 0)
        {
            nextProfileIndex = profiles.Length - 1;
        }

        SetSharedProfileIndex(nextProfileIndex);
    }

    private void SetSharedProfileIndex(int index)
    {
        for (int i = ActiveRuntimes.Count - 1; i >= 0; i--)
        {
            TurretVFXProfileRuntimeUI runtime = ActiveRuntimes[i];
            if (runtime == null)
            {
                ActiveRuntimes.RemoveAt(i);
                continue;
            }

            runtime.ApplyProfileIndexSilently(index);
        }
    }

    private void ApplyProfileIndexSilently(int index)
    {
        if (isApplyingSharedProfile)
        {
            return;
        }

        isApplyingSharedProfile = true;
        profileIndex = index;
        ClampProfileIndex();
        ApplyCurrentProfile();
        isApplyingSharedProfile = false;
    }

    private void ClampProfileIndex()
    {
        if (profiles == null || profiles.Length == 0)
        {
            profileIndex = 0;
            return;
        }

        profileIndex = Mathf.Clamp(profileIndex, 0, profiles.Length - 1);
    }

    private void RefreshCurrentProfileName()
    {
        TurretVFXProfileSO profile = CurrentProfile;
        if (profile == null)
        {
            currentProfileName = string.Empty;
            return;
        }

        currentProfileName = string.IsNullOrWhiteSpace(profile.displayName) ? profile.name : profile.displayName;
    }

    private void CreateRuntimeUI()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Turret VFX Test Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasScaler.scaleFactor = 1.0f;
        canvasScaler.referencePixelsPerUnit = 100.0f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.0f, 1.0f);
        panelRect.anchorMax = new Vector2(0.0f, 1.0f);
        panelRect.pivot = new Vector2(0.0f, 1.0f);
        panelRect.anchoredPosition = uiAnchoredPosition;
        panelRect.sizeDelta = uiSize;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.05f, 0.05f, 0.78f);

        profileNameText = CreateText(panelObject.transform, "Profile Text", new Vector2(16.0f, -16.0f), new Vector2(328.0f, 32.0f), 24, TextAnchor.MiddleLeft);

        CreateButton(panelObject.transform, "Prev Button", "Prev", new Vector2(16.0f, -60.0f), new Vector2(82.0f, 44.0f), PreviousProfile);
        CreateButton(panelObject.transform, "Next Button", "Next", new Vector2(106.0f, -60.0f), new Vector2(82.0f, 44.0f), NextProfile);
        CreateButton(panelObject.transform, "Fire Button", "Fire", new Vector2(196.0f, -60.0f), new Vector2(82.0f, 44.0f), FireOnce);

        Button autoFireButton = CreateButton(panelObject.transform, "Auto Fire Button", string.Empty, new Vector2(16.0f, -110.0f), new Vector2(172.0f, 44.0f), ToggleAutoFire);
        autoFireText = autoFireButton.GetComponentInChildren<Text>();
    }

    private Text CreateText(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.0f, 1.0f);
        rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
        rectTransform.pivot = new Vector2(0.0f, 1.0f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Text text = textObject.AddComponent<Text>();
        text.font = GetRuntimeFont();
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.resizeTextForBestFit = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        return text;
    }

    private Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.0f, 1.0f);
        rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
        rectTransform.pivot = new Vector2(0.0f, 1.0f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.2f, 0.22f, 0.96f);

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        Text text = CreateText(buttonObject.transform, "Label", Vector2.zero, size, 20, TextAnchor.MiddleCenter);
        text.text = label;

        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        return button;
    }

    private void RefreshRuntimeUI()
    {
        if (profileNameText != null)
        {
            profileNameText.text = $"Profile {profileIndex + 1}: {currentProfileName}";
        }

        if (autoFireText != null)
        {
            autoFireText.text = autoFireEnabled ? "Auto: ON" : "Auto: OFF";
        }
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private Font GetRuntimeFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
