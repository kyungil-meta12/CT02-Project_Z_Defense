using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택된 터렛의 진화 전후 정보, 진화 비용, 진화 실행 버튼을 표시한다.
/// </summary>
public class TurretEvolutionPopupUI : TurretPopupPageUI
{
    [Header("진화 후보")]
    [SerializeField, Min(0)] private int evolutionIndex;

    [Header("현재 터렛")]
    [SerializeField] private TMP_Text currentTurretNameText;
    [SerializeField] private Image currentTurretImage;

    [Header("다음 터렛")]
    [SerializeField] private TMP_Text nextTurretNameText;
    [SerializeField] private Image nextTurretImage;

    [Header("필요 재화")]
    [SerializeField] private TMP_Text requireResourceText;
    [SerializeField] private TMP_Text[] resourceItemNameTexts = System.Array.Empty<TMP_Text>();
    [SerializeField] private TMP_Text[] resourceItemCountTexts = System.Array.Empty<TMP_Text>();
    [SerializeField] private Image[] resourceItemImages = System.Array.Empty<Image>();

    [Header("버튼")]
    [SerializeField] private Button evolutionCloseButton;
    [SerializeField] private Button evolutionBackButton;
    [SerializeField] private Button evolutionButton;
    [SerializeField] private TMP_Text evolutionButtonText;

    // 컴포넌트 추가 시 현재 팝업 하위 참조를 자동으로 찾는다
    private void Reset()
    {
        BindChildReferences();
    }

    // 활성화 준비 시 하위 참조와 버튼 이벤트를 준비한다
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

    // 선택된 터렛의 진화 정보를 표시한다
    public override void Show(TurretSelectionContext context)
    {
        base.Show(context);
        RefreshEvolutionTexts();
    }

    [ContextMenu("참조 다시 연결")]
    // 컨텍스트 메뉴에서 진화 팝업 하위 참조를 다시 찾는다
    public void BindChildReferences()
    {
        Transform searchRoot = transform;
        evolutionCloseButton = evolutionCloseButton != null ? evolutionCloseButton : FindChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/HighPanel/ExitFrame/Button");
        evolutionBackButton = evolutionBackButton != null ? evolutionBackButton : FindChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/LowPanel/BackButtonFrame/BackButton");
        evolutionButton = evolutionButton != null ? evolutionButton : FindChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/LowPanel/EvolutionFrame/EvolutionTextFrame");
        evolutionButtonText = evolutionButtonText != null ? evolutionButtonText : evolutionButton == null ? null : evolutionButton.GetComponentInChildren<TMP_Text>(true);
        currentTurretNameText = currentTurretNameText != null ? currentTurretNameText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/CurrentTurretImageFrame/CurrentTurretName");
        currentTurretImage = currentTurretImage != null ? currentTurretImage : FindChildComponent<Image>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/CurrentTurretImageFrame/CurrentTurretImage");
        nextTurretNameText = nextTurretNameText != null ? nextTurretNameText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/NextTurretImageFrame/NextTurretName");
        nextTurretImage = nextTurretImage != null ? nextTurretImage : FindChildComponent<Image>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/NextTurretImageFrame/TurretImage");
        requireResourceText = requireResourceText != null ? requireResourceText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddleLowPanel/RequireSorceText");
        BindResourceSlotReferences(searchRoot);
    }

    // 진화 버튼 입력으로 현재 후보 진화를 실행한다
    public void OnEvolutionButtonClicked()
    {
        if (!CurrentContext.IsValid)
        {
            return;
        }

        if (CurrentContext.Turret.TryEvolve(evolutionIndex))
        {
            RefreshEvolutionTexts();
        }
    }

    // 닫기 버튼 입력으로 전체 선택을 해제한다
    public void OnCloseButtonClicked()
    {
        RequestCloseSelection();
    }

    // 뒤로가기 버튼 입력으로 선택 팝업으로 돌아간다
    public void OnBackButtonClicked()
    {
        RequestBackToSelectPopup();
    }

    // 현재 선택 터렛 기준으로 진화 표시 정보를 갱신한다
    private void RefreshEvolutionTexts()
    {
        if (!CurrentContext.IsValid)
        {
            SetInteractable(false);
            return;
        }

        TurretEvolutionEntry entry = CurrentContext.Turret.GetAvailableEvolution(evolutionIndex);
        bool hasEvolution = entry != null && entry.targetDefinition != null;
        SetText(currentTurretNameText, CurrentContext.GetDisplayName());
        SetText(nextTurretNameText, hasEvolution ? GetEvolutionName(entry) : "진화 대기");
        SetImage(nextTurretImage, hasEvolution ? entry.evolutionIcon : null);
        SetCostTexts(hasEvolution ? entry.evolutionCosts : System.Array.Empty<ResourceCost>());
        SetText(evolutionButtonText, hasEvolution ? "진화" : "Lv.100 필요");
        SetInteractable(hasEvolution && CurrentContext.Turret.CanEvolve(evolutionIndex));
    }

    // 필요 재화 텍스트와 개별 슬롯을 갱신한다
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

    // 진화 버튼 활성 상태를 적용한다
    private void SetInteractable(bool canEvolve)
    {
        if (evolutionButton != null)
        {
            evolutionButton.interactable = canEvolve;
        }
    }

    // 버튼 클릭 이벤트를 등록한다
    private void BindButtonListeners()
    {
        UnbindButtonListeners();

        if (evolutionCloseButton != null)
        {
            evolutionCloseButton.onClick.AddListener(OnCloseButtonClicked);
        }

        if (evolutionBackButton != null)
        {
            evolutionBackButton.onClick.AddListener(OnBackButtonClicked);
        }

        if (evolutionButton != null)
        {
            evolutionButton.onClick.AddListener(OnEvolutionButtonClicked);
        }
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButtonListeners()
    {
        if (evolutionCloseButton != null)
        {
            evolutionCloseButton.onClick.RemoveListener(OnCloseButtonClicked);
        }

        if (evolutionBackButton != null)
        {
            evolutionBackButton.onClick.RemoveListener(OnBackButtonClicked);
        }

        if (evolutionButton != null)
        {
            evolutionButton.onClick.RemoveListener(OnEvolutionButtonClicked);
        }
    }

    // 개별 재화 이름, 수량, 이미지 배열을 하위 이름 패턴으로 구성한다
    private void BindResourceSlotReferences(Transform searchRoot)
    {
        Transform resourcePanel = searchRoot.Find("TurretSelectPopupBackground/MiddleLowPanel/RequireSorceImagePanel");
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

    // 진화 엔트리 표시 이름을 반환한다
    private static string GetEvolutionName(TurretEvolutionEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(entry.displayName))
        {
            return entry.displayName;
        }

        TurretDefinitionSO targetDefinition = entry.targetDefinition;
        if (targetDefinition == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(targetDefinition.displayName) ? targetDefinition.displayName : targetDefinition.name;
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
