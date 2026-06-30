using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Text;
using UnityEngine.Events;

/// <summary>
/// 터렛 업그레이드 팝업에서 현재/다음 스탯, 변화량, 비용, 업그레이드와 진화 버튼을 제어한다.
/// </summary>
public class TurretUpgradePopupUI : TurretPopupPageUI
{
    private const int LEVEL_UP_AMOUNT = 1;

    [Header("레벨 표시")]
    [SerializeField] private TMP_Text currentTurretLevelText;
    [SerializeField] private TMP_Text nextTurretLevelText;

    [Header("현재 수치")]
    [SerializeField] private TMP_Text currentDpsText;
    [SerializeField] private TMP_Text currentFireRateText;
    [SerializeField] private TMP_Text currentRangeText;

    [Header("다음 수치")]
    [SerializeField] private TMP_Text nextDpsText;
    [SerializeField] private TMP_Text nextFireRateText;
    [SerializeField] private TMP_Text nextRangeText;

    [Header("변화량")]
    [SerializeField] private TMP_Text dpsDeltaText;
    [SerializeField] private TMP_Text fireRateDeltaText;
    [SerializeField] private TMP_Text rangeDeltaText;

    [Header("필요 재화")]
    [SerializeField] private TMP_Text requireResourceText;
    [SerializeField] private TMP_Text[] resourceItemNameTexts = System.Array.Empty<TMP_Text>();
    [SerializeField] private TMP_Text[] resourceItemCountTexts = System.Array.Empty<TMP_Text>();
    [SerializeField] private Image[] resourceItemImages = System.Array.Empty<Image>();

    [Header("버튼")]
    [SerializeField] private Button upgradeCloseButton;
    [SerializeField] private Button upgradeBackButton;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button evolutionButton;
    [SerializeField] private TMP_Text upgradeButtonText;
    [SerializeField] private TMP_Text evolutionButtonText;

    public event UnityAction EvolutionPopupRequested;

    // 컴포넌트 추가 시 현재 팝업 하위 참조를 자동으로 찾는다
    private void Reset()
    {
        BindChildReferences();
    }

    // 활성화 준비 시 버튼 이벤트와 하위 참조를 준비한다
    private void Awake()
    {
        BindChildReferences();
        BindButtonListeners();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButtonListeners();
    }

    // 선택된 터렛의 업그레이드 팝업 기본 안내를 표시한다
    public override void Show(TurretSelectionContext context)
    {
        base.Show(context);
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
        nextDpsText = nextDpsText != null ? nextDpsText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/NextDPS");
        nextFireRateText = nextFireRateText != null ? nextFireRateText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/NextFireRate");
        nextRangeText = nextRangeText != null ? nextRangeText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/NextRange");
        dpsDeltaText = dpsDeltaText != null ? dpsDeltaText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/DPSDelta");
        fireRateDeltaText = fireRateDeltaText != null ? fireRateDeltaText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/FireRateDelta");
        rangeDeltaText = rangeDeltaText != null ? rangeDeltaText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/RangeDelta");
        requireResourceText = requireResourceText != null ? requireResourceText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/DeltaDetailInfoPanel/RequireResorce");
        upgradeCloseButton = upgradeCloseButton != null ? upgradeCloseButton : FindFirstChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/HighPanel/CloseFrame/CloseButton", "TurretSelectPopupBackground/HighPanel/ExitFrame/Button");
        upgradeBackButton = upgradeBackButton != null ? upgradeBackButton : FindChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/LowPanel/BackButtonFrame/BackButton");
        upgradeButton = upgradeButton != null ? upgradeButton : FindChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/LowPanel/UpgradeFrame/Upgrade");
        evolutionButton = evolutionButton != null ? evolutionButton : FindFirstChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/LowPanel/Evolution", "TurretSelectPopupBackground/LowPanel/EvolutionFrame/Evolution", "TurretSelectPopupBackground/LowPanel/EvolutionFrame/EvolutionTextFrame", "TurretSelectPopupBackground/LowPanel/SkillFrame/Skill");
        upgradeButtonText = upgradeButtonText != null ? upgradeButtonText : upgradeButton == null ? null : upgradeButton.GetComponentInChildren<TMP_Text>(true);
        evolutionButtonText = evolutionButtonText != null ? evolutionButtonText : evolutionButton == null ? null : evolutionButton.GetComponentInChildren<TMP_Text>(true);
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

        SetText(currentTurretLevelText, $"Lv. {currentLevel}");
        SetText(nextTurretLevelText, canShowUpgrade ? $"Lv. {nextLevel}" : "MAX");
        SetStatTexts(currentStat, nextStat);
        SetDeltaTexts(currentStat, nextStat, canShowUpgrade);
        SetCostTexts(upgradeCosts);
        SetInteractable(canShowUpgrade && turret.CanUpgrade(LEVEL_UP_AMOUNT), hasEvolution);
        SetText(upgradeButtonText, canShowUpgrade ? "업그레이드" : "최대 레벨");
        SetText(evolutionButtonText, hasEvolution ? "진화" : "Lv.100 필요");
    }

    // 현재와 다음 스탯 수치를 각각 텍스트에 반영한다
    private void SetStatTexts(TurretRuntimeStat currentStat, TurretRuntimeStat nextStat)
    {
        SetText(currentDpsText, FormatDps(currentStat));
        SetText(currentFireRateText, FormatValue(currentStat.fireInterval));
        SetText(currentRangeText, FormatValue(currentStat.range));
        SetText(nextDpsText, FormatDps(nextStat));
        SetText(nextFireRateText, FormatValue(nextStat.fireInterval));
        SetText(nextRangeText, FormatValue(nextStat.range));
    }

    // 현재 대비 다음 스탯의 변화량을 퍼센트 텍스트에 반영한다
    private void SetDeltaTexts(TurretRuntimeStat currentStat, TurretRuntimeStat nextStat, bool canShowUpgrade)
    {
        if (!canShowUpgrade)
        {
            SetText(dpsDeltaText, "-");
            SetText(fireRateDeltaText, "-");
            SetText(rangeDeltaText, "-");
            return;
        }

        SetText(dpsDeltaText, FormatDeltaPercent(CalculateDps(currentStat), CalculateDps(nextStat)));
        SetText(fireRateDeltaText, FormatDeltaPercent(currentStat.fireInterval, nextStat.fireInterval));
        SetText(rangeDeltaText, FormatDeltaPercent(currentStat.range, nextStat.range));
    }

    // 업그레이드 필요 재화를 텍스트와 개별 슬롯에 반영한다
    private void SetCostTexts(ResourceCost[] costs)
    {
        SetText(requireResourceText, FormatCosts(costs));

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

            SetText(GetTextAt(resourceItemNameTexts, visibleIndex), GetCurrencyDisplayName(cost.currencyType));
            SetText(GetTextAt(resourceItemCountTexts, visibleIndex), cost.amount.ToString());
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

        if (upgradeCloseButton != null)
        {
            upgradeCloseButton.onClick.AddListener(OnCloseButtonClicked);
        }

        if (upgradeBackButton != null)
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
        if (upgradeCloseButton != null)
        {
            upgradeCloseButton.onClick.RemoveListener(OnCloseButtonClicked);
        }

        if (upgradeBackButton != null)
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

        if (resourceItemNameTexts == null || resourceItemNameTexts.Length == 0)
        {
            resourceItemNameTexts = FindTextsByName(resourcePanel, "ItemName");
        }

        if (resourceItemCountTexts == null || resourceItemCountTexts.Length == 0)
        {
            resourceItemCountTexts = FindTextsByName(resourcePanel, "ItemCount");
            if (resourceItemCountTexts.Length == 0)
            {
                resourceItemCountTexts = resourcePanel.GetComponentsInChildren<TMP_Text>(true);
            }
        }

        if (resourceItemImages == null || resourceItemImages.Length == 0)
        {
            resourceItemImages = FindImagesByName(resourcePanel, "ItemImage");
            if (resourceItemImages.Length == 0)
            {
                resourceItemImages = FindImagesByName(resourcePanel, "Image");
            }
        }

        EnsureResourceArrays();
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

    // 변화량을 소수점 둘째 자리 퍼센트로 표시한다
    private static string FormatDeltaPercent(float currentValue, float nextValue)
    {
        if (Mathf.Approximately(currentValue, 0.0f))
        {
            return Mathf.Approximately(nextValue, 0.0f) ? "0.00%" : "+100.00%";
        }

        float percent = (nextValue - currentValue) / Mathf.Abs(currentValue) * 100.0f;
        return $"{percent:+0.00;-0.00;0.00}%";
    }

    // 비용 배열을 UI 표시 문자열로 변환한다
    private static string FormatCosts(ResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
        {
            return "필요 재화 없음";
        }

        StringBuilder builder = new StringBuilder(64);
        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(" / ");
            }

            builder.Append(GetCurrencyDisplayName(cost.currencyType));
            builder.Append(' ');
            builder.Append(cost.amount);
        }

        return builder.Length == 0 ? "필요 재화 없음" : builder.ToString();
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
            SetText(GetTextAt(resourceItemNameTexts, i), string.Empty);
            SetText(GetTextAt(resourceItemCountTexts, i), string.Empty);
            SetImage(GetImageAt(resourceItemImages, i), null);
        }
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

    // 하위 텍스트 중 이름에 지정 패턴이 포함된 항목을 반환한다
    private static TMP_Text[] FindTextsByName(Transform root, string namePattern)
    {
        if (root == null)
        {
            return System.Array.Empty<TMP_Text>();
        }

        TMP_Text[] candidates = root.GetComponentsInChildren<TMP_Text>(true);
        int count = CountMatches(candidates, namePattern);
        if (count == 0)
        {
            return System.Array.Empty<TMP_Text>();
        }

        TMP_Text[] matches = new TMP_Text[count];
        int writeIndex = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            TMP_Text candidate = candidates[i];
            if (candidate != null && candidate.name.Contains(namePattern))
            {
                matches[writeIndex] = candidate;
                writeIndex++;
            }
        }

        return matches;
    }

    // 하위 이미지 중 이름에 지정 패턴이 포함된 항목을 반환한다
    private static Image[] FindImagesByName(Transform root, string namePattern)
    {
        if (root == null)
        {
            return System.Array.Empty<Image>();
        }

        Image[] candidates = root.GetComponentsInChildren<Image>(true);
        int count = CountMatches(candidates, namePattern);
        if (count == 0)
        {
            return System.Array.Empty<Image>();
        }

        Image[] matches = new Image[count];
        int writeIndex = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            Image candidate = candidates[i];
            if (candidate != null && candidate.name.Contains(namePattern))
            {
                matches[writeIndex] = candidate;
                writeIndex++;
            }
        }

        return matches;
    }

    // 컴포넌트 배열에서 이름 패턴과 일치하는 개수를 센다
    private static int CountMatches(Component[] candidates, string namePattern)
    {
        if (candidates == null || string.IsNullOrEmpty(namePattern))
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            Component candidate = candidates[i];
            if (candidate != null && candidate.name.Contains(namePattern))
            {
                count++;
            }
        }

        return count;
    }
}
