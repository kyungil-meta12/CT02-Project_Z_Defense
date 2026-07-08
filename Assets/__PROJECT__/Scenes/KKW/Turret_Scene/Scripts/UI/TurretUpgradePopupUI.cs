using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 터렛 업그레이드 팝업에서 현재/다음 스탯, 변화량, 비용, 업그레이드와 진화 버튼을 제어한다.
/// </summary>
public class TurretUpgradePopupUI : TurretPopupPageUI
{
    private const int LEVEL_UP_AMOUNT = 1;
    private const float HOLD_UPGRADE_START_DELAY = 0.5f;
    private const float HOLD_UPGRADE_RAMP_DURATION = 1.0f;
    private const float HOLD_UPGRADE_START_RATE = 4.0f;
    private const float HOLD_UPGRADE_MAX_RATE = 60.0f;
    private const string POSITIVE_DELTA_PLUS_TEXT = "<color=#FF4040>+</color>";
    private const string INSUFFICIENT_COST_COLOR = "#FF4040";
    private const string UPGRADE_BACKGROUND_PATH = "TurretUpgradePopupBackground";

    [Header("레벨 표시")]
    [SerializeField] private TMP_Text currentTurretNameText;
    [SerializeField] private TMP_Text currentTurretLevelText;
    [SerializeField] private TMP_Text nextTurretLevelText;

    [Header("현재 수치")]
    [FormerlySerializedAs("currentDpsText")]
    [SerializeField] private TMP_Text currentDamageText;
    [SerializeField] private TMP_Text currentFireRateText;
    [SerializeField] private TMP_Text currentRangeText;
    [SerializeField] private TMP_Text currentPierceText;

    [Header("다음 수치")]
    [FormerlySerializedAs("nextDpsText")]
    [SerializeField] private TMP_Text nextDamageText;
    [SerializeField] private TMP_Text nextFireRateText;
    [SerializeField] private TMP_Text nextRangeText;
    [SerializeField] private TMP_Text nextPierceText;

    [Header("변화량")]
    [FormerlySerializedAs("dpsDeltaText")]
    [SerializeField] private TMP_Text damageDeltaText;
    [SerializeField] private TMP_Text fireRateDeltaText;
    [SerializeField] private TMP_Text rangeDeltaText;
    [SerializeField] private TMP_Text pierceDeltaText;

    [Header("터렛 이미지")]
    [SerializeField] private Image turretImage;

    [Header("필요 재화")]
    [SerializeField] private TMP_Text[] resourceItemNameTexts = System.Array.Empty<TMP_Text>();
    [SerializeField] private TMP_Text[] resourceItemCountTexts = System.Array.Empty<TMP_Text>();
    [SerializeField] private Image[] resourceItemImages = System.Array.Empty<Image>();

    [Header("버튼")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private EventTrigger upgradeButtonEventTrigger;
    [SerializeField] private Button evolutionButton;

    private string currentTurretNameTextTemplate;
    private string currentLevelTextTemplate;
    private string nextLevelTextTemplate;
    private string currentDamageTextTemplate;
    private string currentFireRateTextTemplate;
    private string currentRangeTextTemplate;
    private string currentPierceTextTemplate;
    private string nextDamageTextTemplate;
    private string nextFireRateTextTemplate;
    private string nextRangeTextTemplate;
    private string nextPierceTextTemplate;
    private string damageDeltaTextTemplate;
    private string fireRateDeltaTextTemplate;
    private string rangeDeltaTextTemplate;
    private string pierceDeltaTextTemplate;
    private Sprite[] resourceItemDefaultSprites = System.Array.Empty<Sprite>();
    private EventTrigger.Entry upgradePointerDownEntry;
    private EventTrigger.Entry upgradePointerUpEntry;
    private EventTrigger.Entry upgradePointerExitEntry;
    private EventTrigger.Entry upgradeCancelEntry;
    private bool isUpgradeHolding;
    private bool hasUpgradeHoldRepeatStarted;
    private float upgradeHoldElapsedTime;
    private float upgradeHoldRepeatElapsedTime;
    private float upgradeHoldAccumulator;

    public event UnityAction EvolutionPopupRequested;

    // 컴포넌트 추가 시 현재 팝업 하위 참조를 자동으로 찾는다
    private void Reset()
    {
        BindChildReferences();
    }

    // 활성화 준비 시 버튼 이벤트와 하위 참조를 준비한다
    protected override void Awake()
    {
        base.Awake();
        ValidateRequiredReferences();
        CacheResourceDefaultSprites();
        CacheTextTemplates();
        BindButtonListeners();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    protected override void OnDestroy()
    {
        UnbindButtonListeners();
        base.OnDestroy();
    }

    // 비활성화 시 누르고 있는 업그레이드 입력을 중단한다
    private void OnDisable()
    {
        StopUpgradeHold();
    }

    // 누르고 있는 동안 업그레이드 반복 속도를 점진적으로 증가시킨다
    private void Update()
    {
        if (!isUpgradeHolding)
        {
            return;
        }

        if (!CanProcessUpgradeInput())
        {
            StopUpgradeHold();
            return;
        }

        upgradeHoldElapsedTime += Time.unscaledDeltaTime;
        if (!hasUpgradeHoldRepeatStarted)
        {
            if (upgradeHoldElapsedTime < HOLD_UPGRADE_START_DELAY)
            {
                return;
            }

            hasUpgradeHoldRepeatStarted = true;
            upgradeHoldRepeatElapsedTime = 0.0f;
            upgradeHoldAccumulator = 0.0f;
        }

        upgradeHoldRepeatElapsedTime += Time.unscaledDeltaTime;
        float upgradeRate = Mathf.Lerp(HOLD_UPGRADE_START_RATE, HOLD_UPGRADE_MAX_RATE, Mathf.Clamp01(upgradeHoldRepeatElapsedTime / HOLD_UPGRADE_RAMP_DURATION));
        float upgradeInterval = 1.0f / upgradeRate;
        upgradeHoldAccumulator += Time.unscaledDeltaTime;

        while (upgradeHoldAccumulator >= upgradeInterval)
        {
            upgradeHoldAccumulator -= upgradeInterval;
            if (!TryUpgradeOnce())
            {
                StopUpgradeHold();
                return;
            }
        }
    }

    // 선택된 터렛의 업그레이드 팝업 기본 안내를 표시한다
    public override void Show(TurretSelectionContext context)
    {
        base.Show(context);
        CacheTextTemplates();
        RefreshUpgradeTexts();
    }

    [ContextMenu("참조 다시 연결")]
    // 컨텍스트 메뉴에서 업그레이드 팝업 하위 참조를 다시 찾는다
    public void BindChildReferences()
    {
        Transform searchRoot = transform;
        currentTurretNameText = currentTurretNameText != null ? currentTurretNameText : FindFirstPopupComponent<TMP_Text>(searchRoot, "HighPanel/CurrentTurretFrame/CurrentTurretName");
        currentTurretLevelText = currentTurretLevelText != null ? currentTurretLevelText : FindFirstPopupComponent<TMP_Text>(searchRoot, "HighPanel/CurrentTurretFrame/CurrentTurretLevel");
        nextTurretLevelText = nextTurretLevelText != null ? nextTurretLevelText : FindFirstPopupComponent<TMP_Text>(searchRoot, "HighPanel/NextTurretFrame/NextTurretLevel");
        currentDamageText = currentDamageText != null ? currentDamageText : FindFirstPopupComponent<TMP_Text>(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/Damage", "MiddlePanel/DeltaDetailInfoPanel/DPS");
        currentFireRateText = currentFireRateText != null ? currentFireRateText : FindFirstPopupComponent<TMP_Text>(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/FireRate");
        currentRangeText = currentRangeText != null ? currentRangeText : FindFirstPopupComponent<TMP_Text>(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/Range");
        currentPierceText = currentPierceText != null ? currentPierceText : FindFirstPopupComponent<TMP_Text>(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/Pierce");
        nextDamageText = nextDamageText != null ? nextDamageText : FindFirstPopupComponent<TMP_Text>(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/NextDamage", "MiddlePanel/DeltaDetailInfoPanel/NextDPS");
        nextFireRateText = nextFireRateText != null ? nextFireRateText : FindFirstPopupComponent<TMP_Text>(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/NextFireRate");
        nextRangeText = nextRangeText != null ? nextRangeText : FindFirstPopupComponent<TMP_Text>(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/NextRange");
        nextPierceText = nextPierceText != null ? nextPierceText : FindFirstPopupComponent<TMP_Text>(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/NextPierce");
        damageDeltaText = damageDeltaText != null ? damageDeltaText : FindFirstPopupComponent<TMP_Text>(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/DamageDelta", "MiddlePanel/DeltaDetailInfoPanel/DPSDelta");
        fireRateDeltaText = fireRateDeltaText != null ? fireRateDeltaText : FindFirstPopupComponent<TMP_Text>(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/FireRateDelta");
        rangeDeltaText = rangeDeltaText != null ? rangeDeltaText : FindFirstPopupComponent<TMP_Text>(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/RangeDelta");
        pierceDeltaText = pierceDeltaText != null ? pierceDeltaText : FindFirstPopupComponent<TMP_Text>(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/PierceDelta");
        turretImage = ResolveTurretIconImage(searchRoot, turretImage);
        upgradeButton = upgradeButton != null ? upgradeButton : FindFirstPopupComponent<Button>(searchRoot, "LowPanel/UpgradeFrame/Upgrade");
        evolutionButton = evolutionButton != null ? evolutionButton : FindFirstPopupComponent<Button>(searchRoot, "LowPanel/Evolution", "LowPanel/EvolutionFrame/Evolution", "LowPanel/EvolutionFrame/EvolutionTextFrame", "LowPanel/SkillFrame/Skill");
        BindResourceSlotReferences(searchRoot);
    }

    // 업그레이드 버튼 입력으로 현재 터렛을 1레벨 업그레이드한다
    public void OnUpgradeButtonClicked()
    {
        TryUpgradeOnce();
    }

    // 진화 버튼 입력으로 첫 번째 진화 후보를 실행한다
    public void OnEvolutionButtonClicked()
    {
        if (!CurrentContext.IsValid || CurrentContext.Turret.GetAvailableEvolutionCount() <= 0)
        {
            return;
        }

        EvolutionPopupRequested?.Invoke();
    }

    // 현재 선택 터렛 기준으로 업그레이드 표시 정보를 갱신한다
    private void RefreshUpgradeTexts()
    {
        if (!CurrentContext.IsValid)
        {
            SetInteractable(false, false);
            return;
        }

        TurretDefinitionRuntimeController turret = CurrentContext.Turret;
        int currentLevel = turret.CurrentTierLevel;
        int nextLevel = GetNextDisplayLevel(turret);
        TurretRuntimeStat currentStat = TurretStatCalculator.Calculate(turret.CurrentTurretDefinition, currentLevel);
        TurretRuntimeStat nextStat = TurretStatCalculator.Calculate(turret.CurrentTurretDefinition, nextLevel);
        ResourceCost[] upgradeCosts = turret.GetUpgradeCosts(LEVEL_UP_AMOUNT);
        bool canShowUpgrade = nextLevel > currentLevel && !turret.IsMaxTierLevelReached;
        bool hasEvolution = turret.GetAvailableEvolutionCount() > 0;

        SetText(currentTurretNameText, ApplyTemplate(currentTurretNameTextTemplate, CurrentContext.GetDisplayName()));
        SetText(currentTurretLevelText, ApplyTemplate(currentLevelTextTemplate, currentLevel.ToString()));
        SetText(nextTurretLevelText, ApplyTemplate(nextLevelTextTemplate, canShowUpgrade ? nextLevel.ToString() : "MAX"));
        SetTurretImage(turret.CurrentTurretDefinition);
        SetStatTexts(currentStat, nextStat);
        SetDeltaTexts(currentStat, nextStat, canShowUpgrade);
        SetCostTexts(upgradeCosts);
        SetInteractable(canShowUpgrade && turret.CanUpgrade(LEVEL_UP_AMOUNT), hasEvolution);
    }

    // TMP 원문 템플릿을 보관해 괄호와 고정 문구를 유지한다
    private void CacheTextTemplates()
    {
        EnableDeltaRichText();

        if (currentTurretNameText != null && string.IsNullOrEmpty(currentTurretNameTextTemplate))
        {
            currentTurretNameTextTemplate = currentTurretNameText.text;
        }

        if (currentTurretLevelText != null && string.IsNullOrEmpty(currentLevelTextTemplate))
        {
            currentLevelTextTemplate = currentTurretLevelText.text;
        }

        if (nextTurretLevelText != null && string.IsNullOrEmpty(nextLevelTextTemplate))
        {
            nextLevelTextTemplate = nextTurretLevelText.text;
        }

        if (currentDamageText != null && string.IsNullOrEmpty(currentDamageTextTemplate))
        {
            currentDamageTextTemplate = currentDamageText.text;
        }

        if (currentFireRateText != null && string.IsNullOrEmpty(currentFireRateTextTemplate))
        {
            currentFireRateTextTemplate = currentFireRateText.text;
        }

        if (currentRangeText != null && string.IsNullOrEmpty(currentRangeTextTemplate))
        {
            currentRangeTextTemplate = currentRangeText.text;
        }

        if (currentPierceText != null && string.IsNullOrEmpty(currentPierceTextTemplate))
        {
            currentPierceTextTemplate = currentPierceText.text;
        }

        if (nextDamageText != null && string.IsNullOrEmpty(nextDamageTextTemplate))
        {
            nextDamageTextTemplate = nextDamageText.text;
        }

        if (nextFireRateText != null && string.IsNullOrEmpty(nextFireRateTextTemplate))
        {
            nextFireRateTextTemplate = nextFireRateText.text;
        }

        if (nextRangeText != null && string.IsNullOrEmpty(nextRangeTextTemplate))
        {
            nextRangeTextTemplate = nextRangeText.text;
        }

        if (nextPierceText != null && string.IsNullOrEmpty(nextPierceTextTemplate))
        {
            nextPierceTextTemplate = nextPierceText.text;
        }

        if (damageDeltaText != null && string.IsNullOrEmpty(damageDeltaTextTemplate))
        {
            damageDeltaTextTemplate = damageDeltaText.text;
        }

        if (fireRateDeltaText != null && string.IsNullOrEmpty(fireRateDeltaTextTemplate))
        {
            fireRateDeltaTextTemplate = fireRateDeltaText.text;
        }

        if (rangeDeltaText != null && string.IsNullOrEmpty(rangeDeltaTextTemplate))
        {
            rangeDeltaTextTemplate = rangeDeltaText.text;
        }

        if (pierceDeltaText != null && string.IsNullOrEmpty(pierceDeltaTextTemplate))
        {
            pierceDeltaTextTemplate = pierceDeltaText.text;
        }
    }

    // 델타 텍스트의 색상 태그가 표시되도록 Rich Text를 켠다
    private void EnableDeltaRichText()
    {
        EnableRichText(damageDeltaText);
        EnableRichText(fireRateDeltaText);
        EnableRichText(rangeDeltaText);
        EnableRichText(pierceDeltaText);
    }

    // 텍스트 참조가 있을 때 Rich Text 옵션을 활성화한다
    private static void EnableRichText(TMP_Text targetText)
    {
        if (targetText != null)
        {
            targetText.richText = true;
        }
    }

    // 현재와 다음 스탯 수치를 각각 텍스트에 반영한다
    private void SetStatTexts(TurretRuntimeStat currentStat, TurretRuntimeStat nextStat)
    {
        SetText(currentDamageText, ApplyTemplate(currentDamageTextTemplate, FormatValue(currentStat.damage)));
        SetText(currentFireRateText, ApplyTemplate(currentFireRateTextTemplate, FormatValue(currentStat.fireInterval)));
        SetText(currentRangeText, ApplyTemplate(currentRangeTextTemplate, FormatValue(currentStat.range)));
        SetText(currentPierceText, ApplyTemplate(currentPierceTextTemplate, currentStat.pierceCount.ToString()));
        SetText(nextDamageText, ApplyTemplate(nextDamageTextTemplate, FormatValue(nextStat.damage)));
        SetText(nextFireRateText, ApplyTemplate(nextFireRateTextTemplate, FormatValue(nextStat.fireInterval)));
        SetText(nextRangeText, ApplyTemplate(nextRangeTextTemplate, FormatValue(nextStat.range)));
        SetText(nextPierceText, ApplyTemplate(nextPierceTextTemplate, nextStat.pierceCount.ToString()));
    }

    // 현재 대비 다음 스탯의 변화량을 퍼센트 텍스트에 반영한다
    private void SetDeltaTexts(TurretRuntimeStat currentStat, TurretRuntimeStat nextStat, bool canShowUpgrade)
    {
        if (!canShowUpgrade)
        {
            SetText(damageDeltaText, "-");
            SetText(fireRateDeltaText, "-");
            SetText(rangeDeltaText, "-");
            SetText(pierceDeltaText, "-");
            return;
        }

        SetText(damageDeltaText, FormatDeltaPercentText(damageDeltaTextTemplate, currentStat.damage, nextStat.damage));
        SetText(fireRateDeltaText, FormatDeltaPercentText(fireRateDeltaTextTemplate, currentStat.fireInterval, nextStat.fireInterval));
        SetText(rangeDeltaText, FormatDeltaPercentText(rangeDeltaTextTemplate, currentStat.range, nextStat.range));
        SetText(pierceDeltaText, FormatDeltaIntegerText(pierceDeltaTextTemplate, currentStat.pierceCount, nextStat.pierceCount));
    }

    // 현재 터렛 정의에 연결된 UI 이미지를 팝업에 반영한다
    private void SetTurretImage(TurretDefinitionSO definition)
    {
        Sprite sprite = definition == null ? null : definition.uiIcon;
        SetTurretIconImage(turretImage, sprite);
    }

    // 업그레이드 필요 재화를 텍스트와 개별 슬롯에 반영한다
    private void SetCostTexts(ResourceCost[] costs)
    {
        if (costs == null)
        {
            ClearResourceSlots(0);
            return;
        }

        int visibleIndex = 0;
        int slotCount = GetResourceSlotCount();
        for (int i = 0; i < costs.Length && visibleIndex < slotCount; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            TMP_Text countText = GetTextAt(resourceItemCountTexts, visibleIndex);
            EnableRichText(countText);
            SetText(GetTextAt(resourceItemNameTexts, visibleIndex), GetCurrencyDisplayName(cost.currencyType));
            SetText(countText, FormatCostAmountText(cost));
            SetImage(GetImageAt(resourceItemImages, visibleIndex), GetCurrencySprite(cost.currencyType));
            visibleIndex++;
        }

        ClearResourceSlots(visibleIndex);
    }

    // 버튼 활성 상태를 현재 조건에 맞춘다
    private void SetInteractable(bool canUpgrade, bool canEvolve)
    {
        if (upgradeButton != null)
        {
            upgradeButton.interactable = canUpgrade;
        }

        if (evolutionButton != null)
        {
            evolutionButton.interactable = canEvolve;
        }
    }

    // 버튼 클릭 이벤트를 등록한다
    private void BindButtonListeners()
    {
        UnbindButtonListeners();

        if (upgradeButton != null)
        {
            BindUpgradeHoldListeners();
        }

        if (evolutionButton != null)
        {
            evolutionButton.onClick.AddListener(OnEvolutionButtonClicked);
        }
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButtonListeners()
    {
        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(OnUpgradeButtonClicked);
            UnbindUpgradeHoldListeners();
        }

        if (evolutionButton != null)
        {
            evolutionButton.onClick.RemoveListener(OnEvolutionButtonClicked);
        }
    }

    // 업그레이드 버튼의 길게 누르기 포인터 이벤트를 등록한다
    private void BindUpgradeHoldListeners()
    {
        if (upgradeButton == null)
        {
            return;
        }

        UnbindUpgradeHoldListeners();
        if (upgradeButtonEventTrigger == null)
        {
            Debug.LogWarning("[TurretUpgradePopupUI] Upgrade Button EventTrigger 참조가 없어 길게 누르기 업그레이드를 사용할 수 없습니다.", this);
            upgradeButton.onClick.RemoveListener(OnUpgradeButtonClicked);
            upgradeButton.onClick.AddListener(OnUpgradeButtonClicked);
            return;
        }

        upgradePointerDownEntry = CreateUpgradeHoldEntry(EventTriggerType.PointerDown, OnUpgradeHoldPointerDown);
        upgradePointerUpEntry = CreateUpgradeHoldEntry(EventTriggerType.PointerUp, OnUpgradeHoldPointerUp);
        upgradePointerExitEntry = CreateUpgradeHoldEntry(EventTriggerType.PointerExit, OnUpgradeHoldPointerExit);
        upgradeCancelEntry = CreateUpgradeHoldEntry(EventTriggerType.Cancel, OnUpgradeHoldCancel);
        upgradeButtonEventTrigger.triggers.Add(upgradePointerDownEntry);
        upgradeButtonEventTrigger.triggers.Add(upgradePointerUpEntry);
        upgradeButtonEventTrigger.triggers.Add(upgradePointerExitEntry);
        upgradeButtonEventTrigger.triggers.Add(upgradeCancelEntry);
    }

    // 업그레이드 버튼의 길게 누르기 포인터 이벤트를 해제한다
    private void UnbindUpgradeHoldListeners()
    {
        StopUpgradeHold();
        if (upgradeButtonEventTrigger == null)
        {
            return;
        }

        RemoveUpgradeHoldEntry(upgradePointerDownEntry);
        RemoveUpgradeHoldEntry(upgradePointerUpEntry);
        RemoveUpgradeHoldEntry(upgradePointerExitEntry);
        RemoveUpgradeHoldEntry(upgradeCancelEntry);
        upgradePointerDownEntry = null;
        upgradePointerUpEntry = null;
        upgradePointerExitEntry = null;
        upgradeCancelEntry = null;
    }

    // 업그레이드 버튼 EventTrigger 항목을 생성한다
    private static EventTrigger.Entry CreateUpgradeHoldEntry(EventTriggerType eventType, UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };
        entry.callback.AddListener(callback);
        return entry;
    }

    // 등록된 업그레이드 버튼 EventTrigger 항목을 제거한다
    private void RemoveUpgradeHoldEntry(EventTrigger.Entry entry)
    {
        if (upgradeButtonEventTrigger != null && entry != null)
        {
            upgradeButtonEventTrigger.triggers.Remove(entry);
        }
    }

    // 업그레이드 버튼을 누르는 순간 1회 업그레이드하고 반복 입력을 시작한다
    private void OnUpgradeHoldPointerDown(BaseEventData eventData)
    {
        if (!CanProcessUpgradeInput())
        {
            StopUpgradeHold();
            return;
        }

        isUpgradeHolding = true;
        hasUpgradeHoldRepeatStarted = false;
        upgradeHoldElapsedTime = 0.0f;
        upgradeHoldRepeatElapsedTime = 0.0f;
        upgradeHoldAccumulator = 0.0f;
        if (!TryUpgradeOnce())
        {
            StopUpgradeHold();
        }
    }

    // 업그레이드 버튼에서 손을 뗄 때 반복 입력을 중단한다
    private void OnUpgradeHoldPointerUp(BaseEventData eventData)
    {
        StopUpgradeHold();
    }

    // 업그레이드 버튼 밖으로 포인터가 나가면 반복 입력을 중단한다
    private void OnUpgradeHoldPointerExit(BaseEventData eventData)
    {
        StopUpgradeHold();
    }

    // 업그레이드 입력이 취소되면 반복 입력을 중단한다
    private void OnUpgradeHoldCancel(BaseEventData eventData)
    {
        StopUpgradeHold();
    }

    // 업그레이드 입력을 처리할 수 있는 상태인지 확인한다
    private bool CanProcessUpgradeInput()
    {
        return CurrentContext.IsValid && upgradeButton != null && upgradeButton.interactable;
    }

    // 현재 터렛을 1회 업그레이드하고 UI를 갱신한다
    private bool TryUpgradeOnce()
    {
        if (!CurrentContext.IsValid)
        {
            return false;
        }

        if (!CurrentContext.Turret.TryUpgrade(LEVEL_UP_AMOUNT))
        {
            RefreshUpgradeTexts();
            return false;
        }

        RefreshUpgradeTexts();
        RequestSelectionContextUpdate(CurrentContext);
        return true;
    }

    // 누르고 있는 업그레이드 입력 상태를 초기화한다
    private void StopUpgradeHold()
    {
        isUpgradeHolding = false;
        hasUpgradeHoldRepeatStarted = false;
        upgradeHoldElapsedTime = 0.0f;
        upgradeHoldRepeatElapsedTime = 0.0f;
        upgradeHoldAccumulator = 0.0f;
    }

    // 개별 재화 이름, 수량, 이미지 배열을 하위 이름 패턴으로 구성한다
    private void BindResourceSlotReferences(Transform searchRoot)
    {
        Transform resourcePanel = FindFirstPopupTransform(searchRoot, "MiddlePanel/DeltaDetailInfoPanel/RequireResorce/ResorcePanel");
        if (resourcePanel == null)
        {
            EnsureResourceArrays();
            return;
        }

        int slotCount = resourcePanel.childCount;
        resourceItemNameTexts = new TMP_Text[slotCount];
        resourceItemCountTexts = new TMP_Text[slotCount];
        resourceItemImages = new Image[slotCount];
        resourceItemDefaultSprites = new Sprite[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            Transform slotRoot = resourcePanel.GetChild(i);
            resourceItemNameTexts[i] = FindDescendantTextByName(slotRoot, "ItemName");
            resourceItemCountTexts[i] = FindDescendantTextByName(slotRoot, "ItemCount");
            resourceItemImages[i] = FindResourceSlotIconImage(slotRoot, i + 1);
            resourceItemDefaultSprites[i] = resourceItemImages[i] == null ? null : resourceItemImages[i].sprite;
        }
    }

    // 다음 표시 레벨을 계산한다
    private static int GetNextDisplayLevel(TurretDefinitionRuntimeController turret)
    {
        if (turret == null || turret.IsMaxTierLevelReached || turret.GetAvailableEvolutionCount() > 0)
        {
            return turret == null ? 1 : turret.CurrentTierLevel;
        }

        int maxLevel = turret.CurrentMaxTierLevel;
        int nextLevel = turret.CurrentTierLevel + LEVEL_UP_AMOUNT;
        return maxLevel > 0 ? Mathf.Min(nextLevel, maxLevel) : nextLevel;
    }

    // 단일 스탯 값을 소수점 둘째 자리까지 표시한다
    private static string FormatValue(float value)
    {
        return value.ToString("0.##");
    }

    // 템플릿의 중괄호 구간을 값으로 교체한다
    private static string ApplyTemplate(string template, string value)
    {
        if (string.IsNullOrEmpty(template))
        {
            return value;
        }

        int openIndex = template.IndexOf('{');
        int closeIndex = template.IndexOf('}', openIndex + 1);
        if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex)
        {
            return value;
        }

        return template.Substring(0, openIndex) + value + template.Substring(closeIndex + 1);
    }

    // 변화량 퍼센트를 템플릿에 맞춰 표시한다
    private static string FormatDeltaPercentText(string template, float currentValue, float nextValue)
    {
        float percent;
        if (Mathf.Approximately(currentValue, 0.0f))
        {
            percent = Mathf.Approximately(nextValue, 0.0f) ? 0.0f : 100.0f;
        }
        else
        {
            percent = (nextValue - currentValue) / Mathf.Abs(currentValue) * 100.0f;
        }

        string value = percent > 0.0f ? percent.ToString("0.00") + "%" : percent.ToString("0.00") + "%";
        return ApplySignedDeltaTemplate(template, value, percent > 0.0f);
    }

    // 정수 변화량을 템플릿에 맞춰 표시한다
    private static string FormatDeltaIntegerText(string template, int currentValue, int nextValue)
    {
        int delta = nextValue - currentValue;
        string value = Mathf.Abs(delta).ToString();
        if (delta < 0)
        {
            value = delta.ToString();
        }

        return ApplySignedDeltaTemplate(template, value, delta > 0);
    }

    // 양수 변화량의 플러스 기호만 붉은색으로 표시한다
    private static string ApplySignedDeltaTemplate(string template, string value, bool isPositive)
    {
        string signedValue = isPositive ? POSITIVE_DELTA_PLUS_TEXT + value : value;
        if (string.IsNullOrEmpty(template))
        {
            return signedValue;
        }

        int openIndex = template.IndexOf('{');
        int closeIndex = template.IndexOf('}', openIndex + 1);
        if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex)
        {
            return signedValue;
        }

        string prefix = template.Substring(0, openIndex);
        string suffix = template.Substring(closeIndex + 1);
        if (prefix.EndsWith("+"))
        {
            prefix = prefix.Substring(0, prefix.Length - 1);
            signedValue = isPositive ? POSITIVE_DELTA_PLUS_TEXT + value : value;
        }

        return prefix + signedValue + suffix;
    }

    // 재화 타입을 UI 표시 이름으로 변환한다
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

    // 재화 타입을 인벤토리 메타데이터 표시 이름으로 변환한다
    private static string GetCurrencyDisplayName(RewardCurrencyType currencyType)
    {
        if (InventorySystem.Inst != null)
        {
            string itemName = InventorySystem.Inst.GetName(currencyType);
            if (!string.IsNullOrWhiteSpace(itemName))
            {
                return itemName;
            }
        }

        return GetCurrencyLabel(currencyType);
    }

    // 재화 타입을 인벤토리 메타데이터 이미지로 변환한다
    private static Sprite GetCurrencySprite(RewardCurrencyType currencyType)
    {
        if (InventorySystem.Inst == null)
        {
            return null;
        }

        ItemMetaDataSo metadata = InventorySystem.Inst.GetMetaData(currencyType);
        return metadata == null ? null : metadata.ItemImage;
    }

    // 지정 인덱스 이후 재화 슬롯 표시를 비운다
    private void ClearResourceSlots(int startIndex)
    {
        int slotCount = GetResourceSlotCount();
        for (int i = startIndex; i < slotCount; i++)
        {
            SetText(GetTextAt(resourceItemNameTexts, i), "-");
            SetText(GetTextAt(resourceItemCountTexts, i), "-");
            SetImage(GetImageAt(resourceItemImages, i), GetSpriteAt(resourceItemDefaultSprites, i));
        }
    }

    // 재화 보유량이 부족하면 보유량/필요량을 붉은색으로 표시한다
    private static string FormatCostAmountText(ResourceCost cost)
    {
        if (cost == null)
        {
            return "-";
        }

        int requiredAmount = Mathf.Max(0, cost.amount);
        if (InventorySystem.Inst != null && !InventorySystem.Inst.CanUseItem(cost.currencyType, requiredAmount))
        {
            return $"<color={INSUFFICIENT_COST_COLOR}>{InventorySystem.Inst.GetCountString(cost.currencyType)}/{requiredAmount}</color>";
        }

        return requiredAmount.ToString();
    }

    // 재화 슬롯 배열 중 사용할 수 있는 최대 슬롯 수를 반환한다
    private int GetResourceSlotCount()
    {
        int count = resourceItemCountTexts == null ? 0 : resourceItemCountTexts.Length;
        if (resourceItemNameTexts != null && resourceItemNameTexts.Length > count)
        {
            count = resourceItemNameTexts.Length;
        }

        if (resourceItemImages != null && resourceItemImages.Length > count)
        {
            count = resourceItemImages.Length;
        }

        return count;
    }

    // 재화 배열 null 상태를 빈 배열로 보정한다
    private void EnsureResourceArrays()
    {
        resourceItemNameTexts = resourceItemNameTexts ?? System.Array.Empty<TMP_Text>();
        resourceItemCountTexts = resourceItemCountTexts ?? System.Array.Empty<TMP_Text>();
        resourceItemImages = resourceItemImages ?? System.Array.Empty<Image>();
        resourceItemDefaultSprites = resourceItemDefaultSprites ?? System.Array.Empty<Sprite>();
    }

    // 재화 기본 스프라이트가 비어 있으면 현재 이미지 스프라이트를 기본값으로 저장한다
    private void CacheResourceDefaultSprites()
    {
        if (resourceItemImages == null)
        {
            return;
        }

        if (resourceItemDefaultSprites == null || resourceItemDefaultSprites.Length != resourceItemImages.Length)
        {
            resourceItemDefaultSprites = new Sprite[resourceItemImages.Length];
        }

        for (int i = 0; i < resourceItemImages.Length; i++)
        {
            if (resourceItemDefaultSprites[i] == null && resourceItemImages[i] != null)
            {
                resourceItemDefaultSprites[i] = resourceItemImages[i].sprite;
            }
        }
    }

    // 지정 배열에서 안전하게 텍스트 참조를 얻는다
    private static TMP_Text GetTextAt(TMP_Text[] texts, int index)
    {
        return texts != null && index >= 0 && index < texts.Length ? texts[index] : null;
    }

    // 지정 배열에서 안전하게 이미지 참조를 얻는다
    private static Image GetImageAt(Image[] images, int index)
    {
        return images != null && index >= 0 && index < images.Length ? images[index] : null;
    }

    // 업그레이드 팝업에 필요한 수동 연결 참조를 검증한다
    private void ValidateRequiredReferences()
    {
        if (currentTurretNameText == null || currentTurretLevelText == null || nextTurretLevelText == null)
        {
            Debug.LogWarning("[TurretUpgradePopupUI] 레벨 표시 TMP 참조가 일부 비어 있습니다.", this);
        }

        if (currentDamageText == null || currentFireRateText == null || currentRangeText == null || currentPierceText == null)
        {
            Debug.LogWarning("[TurretUpgradePopupUI] 현재 수치 TMP 참조가 일부 비어 있습니다.", this);
        }

        if (nextDamageText == null || nextFireRateText == null || nextRangeText == null || nextPierceText == null)
        {
            Debug.LogWarning("[TurretUpgradePopupUI] 다음 수치 TMP 참조가 일부 비어 있습니다.", this);
        }

        if (damageDeltaText == null || fireRateDeltaText == null || rangeDeltaText == null || pierceDeltaText == null)
        {
            Debug.LogWarning("[TurretUpgradePopupUI] 변화량 TMP 참조가 일부 비어 있습니다.", this);
        }

        if (turretImage == null)
        {
            Debug.LogWarning("[TurretUpgradePopupUI] Turret Image 참조가 비어 있습니다.", this);
        }

        if (upgradeButton == null || evolutionButton == null)
        {
            Debug.LogWarning("[TurretUpgradePopupUI] Upgrade/Evolution 버튼 참조가 비어 있습니다.", this);
        }

        if (upgradeButtonEventTrigger == null)
        {
            Debug.LogWarning("[TurretUpgradePopupUI] Upgrade Button EventTrigger 참조가 비어 있습니다.", this);
        }
    }

    // 지정 배열에서 안전하게 기본 슬롯 스프라이트를 얻는다
    private static Sprite GetSpriteAt(Sprite[] sprites, int index)
    {
        return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }

    // 이미지 참조에 스프라이트를 적용한다
    private static void SetImage(Image targetImage, Sprite sprite)
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.sprite = sprite;
        targetImage.enabled = sprite != null;
    }

    // 터렛 대표 이미지를 현재 RectTransform 안에 비율 유지 방식으로 표시한다
    private static void SetTurretIconImage(Image targetImage, Sprite sprite)
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.sprite = sprite;
        targetImage.enabled = sprite != null;
        targetImage.type = Image.Type.Simple;
        targetImage.preserveAspect = true;
        targetImage.color = Color.white;
    }

    // 현재 업그레이드 팝업 하위의 터렛 이미지 참조만 사용한다
    private static Image ResolveTurretIconImage(Transform searchRoot, Image currentImage)
    {
        if (currentImage != null && currentImage.name != "TurretImageFrame" && currentImage.transform.IsChildOf(searchRoot))
        {
            return currentImage;
        }

        Image iconImage = FindFirstPopupComponent<Image>(searchRoot, "MiddlePanel/TurretImage", "MiddlePanel/TurretImageFrame/TurretImage");
        if (iconImage != null)
        {
            return iconImage;
        }

        return null;
    }

    // 텍스트 참조가 있을 때만 문자열을 적용한다
    private static void SetText(TMP_Text targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }

    // 지정 경로의 하위 컴포넌트를 찾는다
    private static T FindChildComponent<T>(Transform searchRoot, string childPath) where T : Component
    {
        if (searchRoot == null || string.IsNullOrWhiteSpace(childPath))
        {
            return null;
        }

        Transform child = searchRoot.Find(childPath);
        return child == null ? null : child.GetComponent<T>();
    }

    // 지정 경로의 하위 Transform을 찾는다
    private static Transform FindChildTransform(Transform searchRoot, string childPath)
    {
        if (searchRoot == null || string.IsNullOrWhiteSpace(childPath))
        {
            return null;
        }

        return searchRoot.Find(childPath);
    }

    // UpgradePopup 배경명 기준으로 하위 컴포넌트를 찾는다
    private static T FindFirstPopupComponent<T>(Transform searchRoot, params string[] relativePaths) where T : Component
    {
        if (relativePaths == null)
        {
            return null;
        }

        for (int i = 0; i < relativePaths.Length; i++)
        {
            T component = FindChildComponent<T>(searchRoot, UPGRADE_BACKGROUND_PATH + "/" + relativePaths[i]);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    // UpgradePopup 배경명 기준으로 하위 Transform을 찾는다
    private static Transform FindFirstPopupTransform(Transform searchRoot, string relativePath)
    {
        return FindChildTransform(searchRoot, UPGRADE_BACKGROUND_PATH + "/" + relativePath);
    }

    // 하위 텍스트 중 이름에 지정 패턴이 포함된 첫 항목을 반환한다
    private static TMP_Text FindDescendantTextByName(Transform root, string namePattern)
    {
        if (root == null || string.IsNullOrEmpty(namePattern))
        {
            return null;
        }

        TMP_Text[] candidates = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            TMP_Text candidate = candidates[i];
            if (candidate != null && candidate.name.Contains(namePattern))
            {
                return candidate;
            }
        }

        return null;
    }

    // 슬롯 내부에서 비용 아이콘 이미지를 찾는다
    private static Image FindResourceSlotIconImage(Transform slotRoot, int slotNumber)
    {
        if (slotRoot == null)
        {
            return null;
        }

        string numberedName = "Item " + slotNumber;
        Image[] candidates = slotRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            Image candidate = candidates[i];
            if (candidate != null && candidate.name == numberedName)
            {
                return candidate;
            }
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            Image candidate = candidates[i];
            if (candidate != null && candidate.transform != slotRoot)
            {
                return candidate;
            }
        }

        return null;
    }
}
