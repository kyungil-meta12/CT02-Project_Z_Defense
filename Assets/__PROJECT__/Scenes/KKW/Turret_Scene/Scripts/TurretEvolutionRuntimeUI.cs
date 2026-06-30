using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 씬에 배치된 터렛 진화 UI를 런타임 터렛 컨트롤러와 연결하고 비용 기반 업그레이드/진화를 처리한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretEvolutionRuntimeUI : MonoBehaviour
{
    /// <summary>
    /// 진화 버튼에 필요한 버튼, 아이콘, 라벨, 기본 스프라이트 참조 묶음.
    /// </summary>
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
    [FormerlySerializedAs("runtimeTester")]
    [SerializeField] private TurretDefinitionRuntimeController runtimeController;
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
    private const string AUTO_BIND_CANVAS_NAME = "TurretEvolutionRuntimeCanvas";

    // 컴포넌트 추가 시 같은 오브젝트의 런타임 컨트롤러를 자동 연결한다
    private void Reset()
    {
        runtimeController = GetComponent<TurretDefinitionRuntimeController>();
    }

    // 시작 전 UI 참조를 자동 연결하고 버튼 이벤트를 바인딩한다
    private void Awake()
    {
        if (runtimeController == null)
        {
            runtimeController = GetComponent<TurretDefinitionRuntimeController>();
        }

        AutoBindSceneCanvasIfNeeded();
        CacheOptionalTextReferences();
        BindLevelButton();
        BindEvolutionButtons();
        RefreshUI();
    }

    // 시작 시 터렛 정의를 적용하고 UI를 최신 상태로 갱신한다
    private void Start()
    {
        if (runtimeController != null)
        {
            runtimeController.Apply();
        }

        RefreshUI();
    }

    // 파괴 시 동적으로 연결한 진화 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindEvolutionButtons();
    }

    // 레벨업 버튼 홀드 중이면 누적 시간에 따라 반복 레벨업을 처리한다
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

    // 레벨업 버튼을 누르기 시작할 때 홀드 상태를 초기화하고 1회 레벨업한다
    public void BeginLevelHold()
    {
        isHoldingLevelButton = true;
        holdElapsedTime = 0.0f;
        holdLevelAccumulator = 0.0f;
        if (!AddLevel(clickLevelAmount))
        {
            EndLevelHold();
        }
    }

    // 레벨업 버튼 홀드 상태를 해제하고 누적값을 초기화한다
    public void EndLevelHold()
    {
        isHoldingLevelButton = false;
        holdElapsedTime = 0.0f;
        holdLevelAccumulator = 0.0f;
    }

    // 클릭 설정 수량만큼 레벨업을 시도한다
    public void AddClickLevel()
    {
        AddLevel(clickLevelAmount);
    }

    // 첫 번째 진화 후보로 진화를 시도한다
    public void EvolveFirst()
    {
        Evolve(0);
    }

    // 두 번째 진화 후보로 진화를 시도한다
    public void EvolveSecond()
    {
        Evolve(1);
    }

    // 진화 후 생성된 런타임 UI에 기존 UI 설정을 복사한다
    public void Initialize(TurretDefinitionRuntimeController runtimeController_, TurretEvolutionRuntimeUI source)
    {
        runtimeController = runtimeController_;

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

        AutoBindSceneCanvasIfNeeded();
        CacheOptionalTextReferences();
        BindLevelButton();
        BindEvolutionButtons();
        RefreshUI();
    }

    // 지정한 수량만큼 비용을 소모해 레벨업을 시도한다
    private bool AddLevel(int levelAmount)
    {
        if (runtimeController == null)
        {
            return false;
        }

        if (!runtimeController.TryUpgrade(levelAmount))
        {
            EndLevelHold();
            RefreshUI();
            return false;
        }

        RefreshUI();
        return true;
    }

    // 지정한 진화 후보로 비용을 소모해 진화를 시도한다
    private void Evolve(int availableIndex)
    {
        if (runtimeController == null)
        {
            return;
        }

        if (replacePrefabOnEvolution)
        {
            TurretDefinitionRuntimeController evolvedRuntimeController = runtimeController.TryCreateEvolvedInstance(availableIndex);
            if (evolvedRuntimeController == null)
            {
                return;
            }

            AttachRuntimeUIToEvolvedTester(evolvedRuntimeController);
            runtimeController = evolvedRuntimeController;
        }
        else if (!runtimeController.TryEvolve(availableIndex))
        {
            return;
        }

        EndLevelHold();
        RefreshUI();
    }

    // 레벨업 버튼에 홀드 입력 보조 컴포넌트를 연결한다
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

    // 수동 참조가 비어 있으면 씬 캔버스에서 버튼을 자동 탐색한다
    private void AutoBindSceneCanvasIfNeeded()
    {
        if (!NeedsSceneCanvasBinding())
        {
            return;
        }

        GameObject canvasObject = GameObject.Find(AUTO_BIND_CANVAS_NAME);
        if (canvasObject == null)
        {
            return;
        }

        Button[] sceneButtons = canvasObject.GetComponentsInChildren<Button>(true);
        if (sceneButtons == null || sceneButtons.Length == 0)
        {
            return;
        }

        List<Button> evolutionButtonList = new List<Button>();
        for (int i = 0; i < sceneButtons.Length; i++)
        {
            Button sceneButton = sceneButtons[i];
            if (sceneButton == null)
            {
                continue;
            }

            if (IsLevelButton(sceneButton))
            {
                levelUpButton = sceneButton;
                continue;
            }

            evolutionButtonList.Add(sceneButton);
        }

        evolutionButtonList.Sort(CompareButtonsByScreenPosition);
        AssignAutoEvolutionButtons(evolutionButtonList);
    }

    // 씬 캔버스 자동 바인딩이 필요한지 확인한다
    private bool NeedsSceneCanvasBinding()
    {
        if (levelUpButton == null)
        {
            return true;
        }

        if (evolutionButtons == null || evolutionButtons.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < evolutionButtons.Length; i++)
        {
            EvolutionButtonBinding binding = evolutionButtons[i];
            if (binding == null || binding.button == null)
            {
                return true;
            }
        }

        return false;
    }

    // 씬 버튼이 레벨업 버튼인지 텍스트로 판정한다
    private bool IsLevelButton(Button sceneButton)
    {
        TMP_Text tmpText = sceneButton.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null && tmpText.text.Contains("Level Up"))
        {
            return true;
        }

        Text text = sceneButton.GetComponentInChildren<Text>(true);
        return text != null && text.text.Contains("Level Up");
    }

    // 버튼의 화면 X 위치를 기준으로 정렬한다
    private int CompareButtonsByScreenPosition(Button left, Button right)
    {
        RectTransform leftTransform = left == null ? null : left.transform as RectTransform;
        RectTransform rightTransform = right == null ? null : right.transform as RectTransform;

        float leftX = leftTransform == null ? 0.0f : leftTransform.anchoredPosition.x;
        float rightX = rightTransform == null ? 0.0f : rightTransform.anchoredPosition.x;
        return leftX.CompareTo(rightX);
    }

    // 자동 탐색한 버튼 목록을 진화 버튼 슬롯에 할당한다
    private void AssignAutoEvolutionButtons(List<Button> sceneButtons)
    {
        if (sceneButtons == null || sceneButtons.Count == 0)
        {
            return;
        }

        if (evolutionButtons == null || evolutionButtons.Length < sceneButtons.Count)
        {
            System.Array.Resize(ref evolutionButtons, sceneButtons.Count);
        }

        for (int i = 0; i < sceneButtons.Count && i < evolutionButtons.Length; i++)
        {
            if (evolutionButtons[i] == null)
            {
                evolutionButtons[i] = new EvolutionButtonBinding();
            }

            Button sceneButton = sceneButtons[i];
            evolutionButtons[i].button = sceneButton;
            evolutionButtons[i].iconImage = sceneButton.GetComponent<Image>();
        }
    }

    // 연결된 버튼과 라벨의 선택 참조를 캐싱한다
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

    // 진화 버튼 클릭 이벤트를 현재 컨트롤러에 맞게 연결한다
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

    // 기존에 바인딩한 진화 버튼 클릭 이벤트를 해제한다
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

    // 현재 터렛 상태, 비용, 진화 가능 여부를 UI에 반영한다
    private void RefreshUI()
    {
        if (runtimeController == null)
        {
            return;
        }

        if (levelText != null)
        {
            levelText.text = GetLevelText();
        }

        if (tmpLevelText != null)
        {
            tmpLevelText.text = GetLevelText();
        }

        RefreshLevelButtonText();

        bool canLevelUp = runtimeController.GetAvailableEvolutionCount() == 0 &&
                          !runtimeController.IsMaxTierLevelReached &&
                          runtimeController.CanUpgrade(clickLevelAmount);
        if (levelUpButton != null)
        {
            levelUpButton.interactable = canLevelUp;
        }

        int evolutionCount = runtimeController.GetAvailableEvolutionCount();
        string statusText = GetEvolutionStatusText(evolutionCount);
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

    // 레벨업 버튼 라벨에 현재 레벨업 비용을 표시한다
    private void RefreshLevelButtonText()
    {
        string levelSuffix = levelText == null && tmpLevelText == null ? $"\n{GetLevelText()}" : string.Empty;
        string buttonText = $"Level Up{levelSuffix}{FormatCosts(runtimeController.GetUpgradeCosts(clickLevelAmount))}";

        if (levelButtonLabelText != null)
        {
            levelButtonLabelText.text = buttonText;
        }

        if (tmpLevelButtonLabelText != null)
        {
            tmpLevelButtonLabelText.text = buttonText;
        }
    }

    // 현재 티어 레벨과 누적 레벨을 UI 문자열로 변환한다
    private string GetLevelText()
    {
        if (runtimeController.CurrentTierLevel == runtimeController.CurrentTotalLevel)
        {
            return $"Lv. {runtimeController.CurrentTierLevel}";
        }

        return $"Tier Lv. {runtimeController.CurrentTierLevel} / Total Lv. {runtimeController.CurrentTotalLevel}";
    }

    // 현재 진화 가능 상태를 UI 문자열로 변환한다
    private string GetEvolutionStatusText(int evolutionCount)
    {
        if (evolutionCount > 0)
        {
            return "Evolution Available";
        }

        if (runtimeController.IsMaxTierLevelReached)
        {
            return "Max Level";
        }

        return "Evolution Locked";
    }

    // 사용 가능한 진화 후보와 비용을 버튼 목록에 반영한다
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

            TurretEvolutionEntry entry = runtimeController.GetAvailableEvolution(i);
            bool isVisible = i < evolutionCount && entry != null;
            binding.button.gameObject.SetActive(isVisible);

            if (!isVisible)
            {
                continue;
            }

            string displayName = GetEvolutionName(entry);
            string displayText = displayName + FormatCosts(runtimeController.GetEvolutionCosts(i));
            binding.button.interactable = runtimeController.CanEvolve(i);
            if (binding.labelText != null)
            {
                binding.labelText.text = displayText;
            }

            if (binding.tmpLabelText != null)
            {
                binding.tmpLabelText.text = displayText;
            }

            Sprite evolutionIcon = GetEvolutionIcon(entry, binding);
            if (binding.iconImage != null && evolutionIcon != null)
            {
                binding.iconImage.sprite = evolutionIcon;
                binding.iconImage.preserveAspect = true;
            }
        }
    }

    // 진화 엔트리 또는 버튼 바인딩에서 표시할 아이콘을 가져온다
    private Sprite GetEvolutionIcon(TurretEvolutionEntry entry, EvolutionButtonBinding binding)
    {
        if (entry != null && entry.evolutionIcon != null)
        {
            return entry.evolutionIcon;
        }

        return binding == null ? null : binding.sprite;
    }

    // 진화로 생성된 새 터렛 오브젝트에 런타임 UI 컴포넌트를 연결한다
    private void AttachRuntimeUIToEvolvedTester(TurretDefinitionRuntimeController evolvedRuntimeController)
    {
        if (evolvedRuntimeController == null)
        {
            return;
        }

        TurretEvolutionRuntimeUI evolvedRuntimeUI = evolvedRuntimeController.GetComponent<TurretEvolutionRuntimeUI>();
        if (evolvedRuntimeUI == null)
        {
            evolvedRuntimeUI = evolvedRuntimeController.gameObject.AddComponent<TurretEvolutionRuntimeUI>();
        }

        evolvedRuntimeUI.Initialize(evolvedRuntimeController, this);
    }

    // 진화 엔트리의 표시 이름을 반환한다
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
            default:
                return currencyType.ToString();
        }
    }
}
