using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class TurretEvolutionRuntimeUI : MonoBehaviour
{
    [System.Serializable]
    public class EvolutionButtonBinding
    {
        public Button button;
        public Image iconImage;
        public Text labelText;
        public TMP_Text tmpLabelText;
        public Sprite sprite;
    }

    [Header("References")]
    [SerializeField] private TurretDefinitionRuntimeTester runtimeTester;
    [SerializeField] private Button levelUpButton;
    [SerializeField] private Text levelText;
    [SerializeField] private TMP_Text tmpLevelText;
    [SerializeField] private Text evolutionStatusText;
    [SerializeField] private TMP_Text tmpEvolutionStatusText;
    [SerializeField] private EvolutionButtonBinding[] evolutionButtons = new EvolutionButtonBinding[2];
    [SerializeField] private bool replacePrefabOnEvolution = true;

    [Header("Level Hold")]
    [SerializeField, Min(1)] private int clickLevelAmount = 1;
    [SerializeField, Min(0.0f)] private float holdStartDelay = 0.5f;
    [SerializeField, Min(0.1f)] private float minHoldLevelsPerSecond = 4.0f;
    [SerializeField, Min(0.1f)] private float maxHoldLevelsPerSecond = 45.0f;
    [SerializeField, Min(0.1f)] private float accelerationDuration = 4.0f;

    private bool isHoldingLevelButton;
    private float holdElapsedTime;
    private float holdLevelAccumulator;
    private Text levelButtonLabelText;
    private TMP_Text tmpLevelButtonLabelText;
    private Button[] boundEvolutionButtons;
    private UnityAction[] boundEvolutionActions;

    private void Reset()
    {
        runtimeTester = GetComponent<TurretDefinitionRuntimeTester>();
    }

    private void Awake()
    {
        if (runtimeTester == null)
        {
            runtimeTester = GetComponent<TurretDefinitionRuntimeTester>();
        }

        CacheOptionalTextReferences();
        BindLevelButton();
        BindEvolutionButtons();
        RefreshUI();
    }

    private void Start()
    {
        if (runtimeTester != null)
        {
            runtimeTester.Apply();
        }

        RefreshUI();
    }

    private void OnDestroy()
    {
        UnbindEvolutionButtons();
    }

    private void Update()
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
        AddLevel(levelAmount);
    }

    public void BeginLevelHold()
    {
        isHoldingLevelButton = true;
        holdElapsedTime = 0.0f;
        holdLevelAccumulator = 0.0f;
        AddLevel(clickLevelAmount);
    }

    public void EndLevelHold()
    {
        isHoldingLevelButton = false;
        holdElapsedTime = 0.0f;
        holdLevelAccumulator = 0.0f;
    }

    public void AddClickLevel()
    {
        AddLevel(clickLevelAmount);
    }

    public void EvolveFirst()
    {
        Evolve(0);
    }

    public void EvolveSecond()
    {
        Evolve(1);
    }

    public void Initialize(TurretDefinitionRuntimeTester runtimeTester_, TurretEvolutionRuntimeUI source)
    {
        runtimeTester = runtimeTester_;

        if (source == null)
        {
            return;
        }

        levelUpButton = source.levelUpButton;
        levelText = source.levelText;
        tmpLevelText = source.tmpLevelText;
        evolutionStatusText = source.evolutionStatusText;
        tmpEvolutionStatusText = source.tmpEvolutionStatusText;
        evolutionButtons = source.evolutionButtons;
        replacePrefabOnEvolution = source.replacePrefabOnEvolution;
        clickLevelAmount = source.clickLevelAmount;
        holdStartDelay = source.holdStartDelay;
        minHoldLevelsPerSecond = source.minHoldLevelsPerSecond;
        maxHoldLevelsPerSecond = source.maxHoldLevelsPerSecond;
        accelerationDuration = source.accelerationDuration;

        CacheOptionalTextReferences();
        BindLevelButton();
        BindEvolutionButtons();
        RefreshUI();
    }

    private void AddLevel(int levelAmount)
    {
        if (runtimeTester == null)
        {
            return;
        }

        runtimeTester.AddLevel(levelAmount);
        RefreshUI();
    }

    private void Evolve(int availableIndex)
    {
        if (runtimeTester == null)
        {
            return;
        }

        if (replacePrefabOnEvolution)
        {
            TurretDefinitionRuntimeTester evolvedRuntimeTester = runtimeTester.CreateEvolvedInstance(availableIndex);
            if (evolvedRuntimeTester == null)
            {
                return;
            }

            AttachRuntimeUIToEvolvedTester(evolvedRuntimeTester);
            runtimeTester = evolvedRuntimeTester;
        }
        else if (!runtimeTester.Evolve(availableIndex))
        {
            return;
        }

        EndLevelHold();
        RefreshUI();
    }

    private void BindLevelButton()
    {
        if (levelUpButton == null)
        {
            return;
        }

        LevelHoldButton levelHoldButton = levelUpButton.GetComponent<LevelHoldButton>();
        if (levelHoldButton == null)
        {
            levelHoldButton = levelUpButton.gameObject.AddComponent<LevelHoldButton>();
        }

        levelHoldButton.Initialize(this);
    }

    private void CacheOptionalTextReferences()
    {
        if (levelUpButton != null)
        {
            if (levelButtonLabelText == null)
            {
                levelButtonLabelText = levelUpButton.GetComponentInChildren<Text>(true);
            }

            if (tmpLevelButtonLabelText == null)
            {
                tmpLevelButtonLabelText = levelUpButton.GetComponentInChildren<TMP_Text>(true);
            }
        }

        if (evolutionButtons == null)
        {
            return;
        }

        for (int i = 0; i < evolutionButtons.Length; i++)
        {
            EvolutionButtonBinding binding = evolutionButtons[i];
            if (binding == null || binding.button == null)
            {
                continue;
            }

            if (binding.iconImage == null)
            {
                binding.iconImage = binding.button.GetComponent<Image>();
            }

            if (binding.labelText == null)
            {
                binding.labelText = binding.button.GetComponentInChildren<Text>(true);
            }

            if (binding.tmpLabelText == null)
            {
                binding.tmpLabelText = binding.button.GetComponentInChildren<TMP_Text>(true);
            }
        }
    }

    private void BindEvolutionButtons()
    {
        UnbindEvolutionButtons();

        if (evolutionButtons == null)
        {
            return;
        }

        boundEvolutionButtons = new Button[evolutionButtons.Length];
        boundEvolutionActions = new UnityAction[evolutionButtons.Length];

        for (int i = 0; i < evolutionButtons.Length; i++)
        {
            EvolutionButtonBinding binding = evolutionButtons[i];
            if (binding == null || binding.button == null)
            {
                continue;
            }

            int availableIndex = i;
            UnityAction action = () => Evolve(availableIndex);
            binding.button.onClick.AddListener(action);
            boundEvolutionButtons[i] = binding.button;
            boundEvolutionActions[i] = action;
        }
    }

    private void UnbindEvolutionButtons()
    {
        if (boundEvolutionButtons == null || boundEvolutionActions == null)
        {
            return;
        }

        int boundCount = Mathf.Min(boundEvolutionButtons.Length, boundEvolutionActions.Length);
        for (int i = 0; i < boundCount; i++)
        {
            if (boundEvolutionButtons[i] == null || boundEvolutionActions[i] == null)
            {
                continue;
            }

            boundEvolutionButtons[i].onClick.RemoveListener(boundEvolutionActions[i]);
        }

        boundEvolutionButtons = null;
        boundEvolutionActions = null;
    }

    private void RefreshUI()
    {
        if (runtimeTester == null)
        {
            return;
        }

        if (levelText != null)
        {
            levelText.text = $"Level {runtimeTester.CurrentLevel}";
        }

        if (tmpLevelText != null)
        {
            tmpLevelText.text = $"Level {runtimeTester.CurrentLevel}";
        }

        if (levelText == null && tmpLevelText == null)
        {
            SetFallbackLevelButtonText();
        }

        int evolutionCount = runtimeTester.GetAvailableEvolutionCount();
        string statusText = evolutionCount > 0 ? "Evolution Available" : "Evolution Locked";
        if (evolutionStatusText != null)
        {
            evolutionStatusText.text = statusText;
        }

        if (tmpEvolutionStatusText != null)
        {
            tmpEvolutionStatusText.text = statusText;
        }

        RefreshEvolutionButtons(evolutionCount);
    }

    private void SetFallbackLevelButtonText()
    {
        string buttonText = $"Level Up\nLv. {runtimeTester.CurrentLevel}";

        if (levelButtonLabelText != null)
        {
            levelButtonLabelText.text = buttonText;
        }

        if (tmpLevelButtonLabelText != null)
        {
            tmpLevelButtonLabelText.text = buttonText;
        }
    }

    private void RefreshEvolutionButtons(int evolutionCount)
    {
        if (evolutionButtons == null)
        {
            return;
        }

        for (int i = 0; i < evolutionButtons.Length; i++)
        {
            EvolutionButtonBinding binding = evolutionButtons[i];
            if (binding == null || binding.button == null)
            {
                continue;
            }

            TurretEvolutionEntry entry = runtimeTester.GetAvailableEvolution(i);
            bool isVisible = i < evolutionCount && entry != null;
            binding.button.gameObject.SetActive(isVisible);

            if (!isVisible)
            {
                continue;
            }

            string displayName = GetEvolutionName(entry);
            if (binding.labelText != null)
            {
                binding.labelText.text = displayName;
            }

            if (binding.tmpLabelText != null)
            {
                binding.tmpLabelText.text = displayName;
            }

            Sprite evolutionIcon = GetEvolutionIcon(entry, binding);
            if (binding.iconImage != null && evolutionIcon != null)
            {
                binding.iconImage.sprite = evolutionIcon;
                binding.iconImage.preserveAspect = true;
            }
        }
    }

    private Sprite GetEvolutionIcon(TurretEvolutionEntry entry, EvolutionButtonBinding binding)
    {
        if (entry != null && entry.evolutionIcon != null)
        {
            return entry.evolutionIcon;
        }

        return binding == null ? null : binding.sprite;
    }

    private void AttachRuntimeUIToEvolvedTester(TurretDefinitionRuntimeTester evolvedRuntimeTester)
    {
        if (evolvedRuntimeTester == null)
        {
            return;
        }

        TurretEvolutionRuntimeUI evolvedRuntimeUI = evolvedRuntimeTester.GetComponent<TurretEvolutionRuntimeUI>();
        if (evolvedRuntimeUI == null)
        {
            evolvedRuntimeUI = evolvedRuntimeTester.gameObject.AddComponent<TurretEvolutionRuntimeUI>();
        }

        evolvedRuntimeUI.Initialize(evolvedRuntimeTester, this);
    }

    private string GetEvolutionName(TurretEvolutionEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(entry.displayName))
        {
            return entry.displayName;
        }

        if (entry.targetDefinition != null && !string.IsNullOrWhiteSpace(entry.targetDefinition.displayName))
        {
            return entry.targetDefinition.displayName;
        }

        return entry.targetDefinition == null ? string.Empty : entry.targetDefinition.name;
    }
}

public class LevelHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private TurretEvolutionRuntimeUI owner;

    public void Initialize(TurretEvolutionRuntimeUI owner_)
    {
        owner = owner_;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.BeginLevelHold();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.EndLevelHold();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.EndLevelHold();
        }
    }
}
