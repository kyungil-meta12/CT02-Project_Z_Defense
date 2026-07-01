using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 터렛 업그레이드 팝업에서 현재/다음 스탯, 변화량, 비용, 업그레이드와 진화 버튼을 제어한다.
/// </summary>
public class TurretUpgradePopupUI : TurretPopupPageUI
{
    private const int LEVEL_UP_AMOUNT = 1;
    private const string POSITIVE_DELTA_PLUS_TEXT = "<color=#FF4040>+</color>";
    private const string INSUFFICIENT_COST_COLOR = "#FF4040";

    [Header("레벨 표시")]
    [SerializeField] private TMP_Text currentTurretLevelText;
    [SerializeField] private TMP_Text nextTurretLevelText;

    [Header("현재 수치")]
    [SerializeField] private TMP_Text currentDpsText;
    [SerializeField] private TMP_Text currentFireRateText;
    [SerializeField] private TMP_Text currentRangeText;
    [SerializeField] private TMP_Text currentPierceText;

    [Header("다음 수치")]
    [SerializeField] private TMP_Text nextDpsText;
    [SerializeField] private TMP_Text nextFireRateText;
    [SerializeField] private TMP_Text nextRangeText;
    [SerializeField] private TMP_Text nextPierceText;

    [Header("변화량")]
    [SerializeField] private TMP_Text dpsDeltaText;
    [SerializeField] private TMP_Text fireRateDeltaText;
    [SerializeField] private TMP_Text rangeDeltaText;
    [SerializeField] private TMP_Text pierceDeltaText;

    [Header("필요 재화")]
    [SerializeField] private TMP_Text[] resourceItemNameTexts = System.Array.Empty<TMP_Text>();
    [SerializeField] private TMP_Text[] resourceItemCountTexts = System.Array.Empty<TMP_Text>();
    [SerializeField] private Image[] resourceItemImages = System.Array.Empty<Image>();
    [SerializeField] private Sprite[] resourceItemDefaultSprites = System.Array.Empty<Sprite>();

    [Header("버튼")]
    [SerializeField] private Button upgradeCloseButton;
    [SerializeField] private Button upgradeBackButton;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button evolutionButton;

    private string currentLevelTextTemplate;
    private string nextLevelTextTemplate;
    private string currentDpsTextTemplate;
    private string currentFireRateTextTemplate;
    private string currentRangeTextTemplate;
    private string currentPierceTextTemplate;
    private string nextDpsTextTemplate;
    private string nextFireRateTextTemplate;
    private string nextRangeTextTemplate;
    private string nextPierceTextTemplate;
    private string dpsDeltaTextTemplate;
    private string fireRateDeltaTextTemplate;
    private string rangeDeltaTextTemplate;
    private string pierceDeltaTextTemplate;

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
        BindChildReferences();
        CacheTextTemplates();
        BindButtonListeners();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    protected override void OnDestroy()
    {
        UnbindButtonListeners();
        base.OnDestroy();
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
        currentTurretLevelText = currentTurretLevelText != null ? currentTurretLevelText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/HighPanel/CurrentTurretFrame/CurrentTurretLevel");
        nextTurretLevelText = nextTurretLevelText != null ? nextTurretLevelText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/HighPanel/NextTurretFrame/NextTurretLevel");
        currentDpsText = currentDpsText != null ? currentDpsText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/DPS");
        currentFireRateText = currentFireRateText != null ? currentFireRateText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/FireRate");
        currentRangeText = currentRangeText != null ? currentRangeText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/Range");
        currentPierceText = currentPierceText != null ? currentPierceText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/Pierce");
        nextDpsText = nextDpsText != null ? nextDpsText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/NextDPS");
        nextFireRateText = nextFireRateText != null ? nextFireRateText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/NextFireRate");
        nextRangeText = nextRangeText != null ? nextRangeText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/NextRange");
        nextPierceText = nextPierceText != null ? nextPierceText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/NextPierce");
        dpsDeltaText = dpsDeltaText != null ? dpsDeltaText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/DPSDelta");
        fireRateDeltaText = fireRateDeltaText != null ? fireRateDeltaText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/FireRateDelta");
        rangeDeltaText = rangeDeltaText != null ? rangeDeltaText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/RangeDelta");
        pierceDeltaText = pierceDeltaText != null ? pierceDeltaText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/PierceDelta");
        upgradeCloseButton = upgradeCloseButton != null ? upgradeCloseButton : FindFirstChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/HighPanel/CloseFrame/CloseButton", "TurretSelectPopupBackground/HighPanel/ExitFrame/Button");
        upgradeBackButton = upgradeBackButton != null ? upgradeBackButton : FindChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/LowPanel/BackButtonFrame/BackButton");
        upgradeCloseButton = upgradeCloseButton != null ? upgradeCloseButton : CloseButton;
        upgradeBackButton = upgradeBackButton != null ? upgradeBackButton : BackButton;
        upgradeButton = upgradeButton != null ? upgradeButton : FindChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/LowPanel/UpgradeFrame/Upgrade");
        evolutionButton = evolutionButton != null ? evolutionButton : FindFirstChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/LowPanel/Evolution", "TurretSelectPopupBackground/LowPanel/EvolutionFrame/Evolution", "TurretSelectPopupBackground/LowPanel/EvolutionFrame/EvolutionTextFrame", "TurretSelectPopupBackground/LowPanel/SkillFrame/Skill");
        BindResourceSlotReferences(searchRoot);
    }

    // 업그레이드 버튼 입력으로 현재 터렛을 1레벨 업그레이드한다
    public void OnUpgradeButtonClicked()
    {
        if (!CurrentContext.IsValid)
        {
            return;
        }

        if (CurrentContext.Turret.TryUpgrade(LEVEL_UP_AMOUNT))
        {
            RefreshUpgradeTexts();
        }
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

    // 닫기 버튼 입력으로 터렛 선택을 해제한다
    public void OnCloseButtonClicked()
    {
        RequestCloseSelection();
    }

    // 뒤로가기 버튼 입력으로 선택 팝업으로 돌아간다
    public void OnBackButtonClicked()
    {
        RequestBackToSelectPopup();
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

        SetText(currentTurretLevelText, ApplyTemplate(currentLevelTextTemplate, currentLevel.ToString()));
        SetText(nextTurretLevelText, ApplyTemplate(nextLevelTextTemplate, canShowUpgrade ? nextLevel.ToString() : "MAX"));
        SetStatTexts(currentStat, nextStat);
        SetDeltaTexts(currentStat, nextStat, canShowUpgrade);
        SetCostTexts(upgradeCosts);
        SetInteractable(canShowUpgrade && turret.CanUpgrade(LEVEL_UP_AMOUNT), hasEvolution);
    }

    // TMP 원문 템플릿을 보관해 괄호와 고정 문구를 유지한다
    private void CacheTextTemplates()
    {
        EnableDeltaRichText();

        if (currentTurretLevelText != null && string.IsNullOrEmpty(currentLevelTextTemplate))
        {
            currentLevelTextTemplate = currentTurretLevelText.text;
        }

        if (nextTurretLevelText != null && string.IsNullOrEmpty(nextLevelTextTemplate))
        {
            nextLevelTextTemplate = nextTurretLevelText.text;
        }

        if (currentDpsText != null && string.IsNullOrEmpty(currentDpsTextTemplate))
        {
            currentDpsTextTemplate = currentDpsText.text;
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

        if (nextDpsText != null && string.IsNullOrEmpty(nextDpsTextTemplate))
        {
            nextDpsTextTemplate = nextDpsText.text;
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

        if (dpsDeltaText != null && string.IsNullOrEmpty(dpsDeltaTextTemplate))
        {
            dpsDeltaTextTemplate = dpsDeltaText.text;
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
        EnableRichText(dpsDeltaText);
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
        SetText(currentDpsText, ApplyTemplate(currentDpsTextTemplate, FormatDps(currentStat)));
        SetText(currentFireRateText, ApplyTemplate(currentFireRateTextTemplate, FormatValue(currentStat.fireInterval)));
        SetText(currentRangeText, ApplyTemplate(currentRangeTextTemplate, FormatValue(currentStat.range)));
        SetText(currentPierceText, ApplyTemplate(currentPierceTextTemplate, currentStat.pierceCount.ToString()));
        SetText(nextDpsText, ApplyTemplate(nextDpsTextTemplate, FormatDps(nextStat)));
        SetText(nextFireRateText, ApplyTemplate(nextFireRateTextTemplate, FormatValue(nextStat.fireInterval)));
        SetText(nextRangeText, ApplyTemplate(nextRangeTextTemplate, FormatValue(nextStat.range)));
        SetText(nextPierceText, ApplyTemplate(nextPierceTextTemplate, nextStat.pierceCount.ToString()));
    }

    // 현재 대비 다음 스탯의 변화량을 퍼센트 텍스트에 반영한다
    private void SetDeltaTexts(TurretRuntimeStat currentStat, TurretRuntimeStat nextStat, bool canShowUpgrade)
    {
        if (!canShowUpgrade)
        {
            SetText(dpsDeltaText, "-");
            SetText(fireRateDeltaText, "-");
            SetText(rangeDeltaText, "-");
            SetText(pierceDeltaText, "-");
            return;
        }

        SetText(dpsDeltaText, FormatDeltaPercentText(dpsDeltaTextTemplate, CalculateDps(currentStat), CalculateDps(nextStat)));
        SetText(fireRateDeltaText, FormatDeltaPercentText(fireRateDeltaTextTemplate, currentStat.fireInterval, nextStat.fireInterval));
        SetText(rangeDeltaText, FormatDeltaPercentText(rangeDeltaTextTemplate, currentStat.range, nextStat.range));
        SetText(pierceDeltaText, FormatDeltaIntegerText(pierceDeltaTextTemplate, currentStat.pierceCount, nextStat.pierceCount));
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

        if (upgradeCloseButton != null && upgradeCloseButton != CloseButton)
        {
            upgradeCloseButton.onClick.AddListener(OnCloseButtonClicked);
        }

        if (upgradeBackButton != null && upgradeBackButton != BackButton)
        {
            upgradeBackButton.onClick.AddListener(OnBackButtonClicked);
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(OnUpgradeButtonClicked);
        }

        if (evolutionButton != null)
        {
            evolutionButton.onClick.AddListener(OnEvolutionButtonClicked);
        }
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButtonListeners()
    {
        if (upgradeCloseButton != null && upgradeCloseButton != CloseButton)
        {
            upgradeCloseButton.onClick.RemoveListener(OnCloseButtonClicked);
        }

        if (upgradeBackButton != null && upgradeBackButton != BackButton)
        {
            upgradeBackButton.onClick.RemoveListener(OnBackButtonClicked);
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(OnUpgradeButtonClicked);
        }

        if (evolutionButton != null)
        {
            evolutionButton.onClick.RemoveListener(OnEvolutionButtonClicked);
        }
    }

    // 개별 재화 이름, 수량, 이미지 배열을 하위 이름 패턴으로 구성한다
    private void BindResourceSlotReferences(Transform searchRoot)
    {
        Transform resourcePanel = searchRoot.Find("TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/RequireResorce/ResorcePanel");
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

    // 초당 피해량 표시 문자열을 생성한다
    private static string FormatDps(TurretRuntimeStat stat)
    {
        return FormatValue(CalculateDps(stat));
    }

    // 초당 피해량을 계산한다
    private static float CalculateDps(TurretRuntimeStat stat)
    {
        float safeFireInterval = Mathf.Max(0.01f, stat.fireInterval);
        int safeProjectileCount = Mathf.Max(1, stat.projectileCount);
        return Mathf.Max(0.0f, stat.damage) * safeProjectileCount / safeFireInterval;
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

    // 여러 경로 중 처음 발견되는 하위 컴포넌트를 반환한다
    private static T FindFirstChildComponent<T>(Transform searchRoot, params string[] childPaths) where T : Component
    {
        if (childPaths == null)
        {
            return null;
        }

        for (int i = 0; i < childPaths.Length; i++)
        {
            T component = FindChildComponent<T>(searchRoot, childPaths[i]);
            if (component != null)
            {
                return component;
            }
        }

        return null;
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
