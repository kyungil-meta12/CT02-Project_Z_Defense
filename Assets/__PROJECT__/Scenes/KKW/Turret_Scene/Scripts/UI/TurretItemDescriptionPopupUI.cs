using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 터렛 업그레이드/진화 비용 슬롯에서 열린 아이템 설명과 제작 관계 탐색을 담당한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretItemDescriptionPopupUI : MonoBehaviour
{
    private enum RelationMode
    {
        RequiredForCraft,
        CraftInput
    }

    private const int DEFAULT_HISTORY_LIMIT = 16;

    [Header("표시 루트")]
    [SerializeField] private GameObject popupRoot;

    [Header("상단 아이템 정보")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemDescriptionText;
    [SerializeField] private Image itemImage;

    [Header("관계 표시 모드")]
    [SerializeField] private Toggle requiredForCraftToggle;
    [FormerlySerializedAs("decomposeOutputToggle")]
    [SerializeField] private Toggle craftInputToggle;
    [SerializeField] private TMP_Text relationTitleText;

    [Header("관계 슬롯")]
    [SerializeField] private TurretItemDescriptionRelationSlotUI[] relationSlots = Array.Empty<TurretItemDescriptionRelationSlotUI>();

    [Header("관련 터렛")]
    [SerializeField] private TurretDefinitionSO[] relatedTurretDefinitions = Array.Empty<TurretDefinitionSO>();

    [Header("버튼")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button inventoryButton;

    [Header("연결 UI")]
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private TurretSelectionUIController turretSelectionUI;

    [Header("탐색")]
    [SerializeField, Min(1)] private int historyLimit = DEFAULT_HISTORY_LIMIT;

    private readonly List<RewardCurrencyType> history = new List<RewardCurrencyType>(DEFAULT_HISTORY_LIMIT);
    private RewardCurrencyType currentType;
    private RelationMode currentRelationMode = RelationMode.RequiredForCraft;
    private bool hasCurrentItem;
    private TurretSelectionRestoreState pendingTurretRestoreState;
    private bool hasPendingTurretRestore;
    private readonly List<RewardCurrencyType> pendingItemHistory = new List<RewardCurrencyType>(DEFAULT_HISTORY_LIMIT);
    private RewardCurrencyType pendingItemRestoreType;
    private RelationMode pendingItemRestoreMode;
    private bool hasPendingItemRestore;

    // 컴포넌트 추가 시 표시 루트를 현재 오브젝트로 설정한다
    private void Reset()
    {
        popupRoot = gameObject;
    }

    // 활성화 준비 시 참조를 검증하고 버튼 이벤트를 연결한다
    private void Awake()
    {
        ValidateRequiredReferences();
        BindButtonListeners();
        Hide();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindInventoryRestoreListener();
        UnbindButtonListeners();
    }

    // 지정 아이템을 루트 진입점으로 표시한다
    public void Show(RewardCurrencyType itemType)
    {
        history.Clear();
        hasCurrentItem = true;
        currentType = itemType;
        SetRelationMode(RelationMode.RequiredForCraft);
        SetRootActive(true);
        Refresh();
    }

    // 관계 슬롯에서 선택한 아이템으로 이동하고 이전 아이템을 히스토리에 쌓는다
    public void NavigateTo(RewardCurrencyType itemType)
    {
        if (hasCurrentItem && currentType.Equals(itemType))
        {
            return;
        }

        PushHistory(currentType);
        hasCurrentItem = true;
        currentType = itemType;
        Refresh();
    }

    // 팝업을 숨기고 탐색 히스토리를 초기화한다
    public void Hide()
    {
        history.Clear();
        hasCurrentItem = false;
        ClearAllRelationSlots();
        SetRootActive(false);
    }

    // 뒤로가기 버튼 입력으로 이전 아이템 정보를 표시한다
    public void Back()
    {
        if (history.Count <= 0)
        {
            Hide();
            return;
        }

        int lastIndex = history.Count - 1;
        currentType = history[lastIndex];
        history.RemoveAt(lastIndex);
        hasCurrentItem = true;
        Refresh();
    }

    // 인벤토리 버튼 입력으로 인벤토리 UI를 연다
    public void OpenInventory()
    {
        CaptureItemRestoreState();
        CaptureTurretRestoreState();
        Hide();
        ClearTurretSelectionForInventory();
        if (inventoryUI != null)
        {
            BindInventoryRestoreListener();
            inventoryUI.OnOpenInventory();
            return;
        }

        Debug.LogWarning("[TurretItemDescriptionPopupUI] Inventory UI 참조가 없어 인벤토리를 열 수 없습니다.", this);
    }

    // 인벤토리 입력을 막는 터렛 선택 팝업 상태를 정리한다
    private void ClearTurretSelectionForInventory()
    {
        if (turretSelectionUI != null)
        {
            turretSelectionUI.ClearSelection();
        }
    }

    // 인벤토리 열기 전 복구할 터렛 UI 상태를 저장한다
    private void CaptureTurretRestoreState()
    {
        hasPendingTurretRestore = false;

        if (turretSelectionUI == null)
        {
            return;
        }

        pendingTurretRestoreState = turretSelectionUI.CaptureRestoreState();
        hasPendingTurretRestore = pendingTurretRestoreState.CanRestore;
    }

    // 인벤토리 열기 전 복구할 아이템 설명 팝업 상태를 저장한다
    private void CaptureItemRestoreState()
    {
        hasPendingItemRestore = false;
        pendingItemHistory.Clear();

        if (!hasCurrentItem)
        {
            return;
        }

        pendingItemRestoreType = currentType;
        pendingItemRestoreMode = currentRelationMode;
        for (int i = 0; i < history.Count; i++)
        {
            pendingItemHistory.Add(history[i]);
        }

        hasPendingItemRestore = true;
    }

    // 인벤토리 닫힘 이벤트에 터렛 UI 복구 처리를 연결한다
    private void BindInventoryRestoreListener()
    {
        if (inventoryUI == null)
        {
            return;
        }

        inventoryUI.InventoryClosed -= OnInventoryClosedAfterDescriptionRoute;
        inventoryUI.InventoryClosed += OnInventoryClosedAfterDescriptionRoute;
    }

    // 인벤토리 닫힘 이벤트의 터렛 UI 복구 처리를 해제한다
    private void UnbindInventoryRestoreListener()
    {
        if (inventoryUI != null)
        {
            inventoryUI.InventoryClosed -= OnInventoryClosedAfterDescriptionRoute;
        }
    }

    // 아이템 설명 팝업 경로로 열린 인벤토리가 닫히면 이전 UI 상태를 복구한다
    private void OnInventoryClosedAfterDescriptionRoute()
    {
        UnbindInventoryRestoreListener();

        RestoreTurretSelectionAfterInventory();
        RestoreItemDescriptionAfterInventory();
    }

    // 인벤토리 닫힘 후 이전 터렛 선택 UI를 복구한다
    private void RestoreTurretSelectionAfterInventory()
    {
        if (hasPendingTurretRestore && turretSelectionUI != null)
        {
            turretSelectionUI.RestoreSelection(pendingTurretRestoreState);
        }

        hasPendingTurretRestore = false;
    }

    // 인벤토리 닫힘 후 이전 아이템 설명 팝업을 복구한다
    private void RestoreItemDescriptionAfterInventory()
    {
        if (!hasPendingItemRestore)
        {
            return;
        }

        history.Clear();
        for (int i = 0; i < pendingItemHistory.Count; i++)
        {
            history.Add(pendingItemHistory[i]);
        }

        currentType = pendingItemRestoreType;
        currentRelationMode = pendingItemRestoreMode;
        hasCurrentItem = true;
        hasPendingItemRestore = false;
        pendingItemHistory.Clear();
        SetRootActive(true);
        Refresh();
    }

    // 현재 아이템 정보를 다시 표시한다
    private void Refresh()
    {
        if (!hasCurrentItem)
        {
            ClearHeader();
            ClearAllRelationSlots();
            return;
        }

        ItemMetaDataSo metadata = GetMetaData(currentType);
        RefreshHeader(metadata);
        RefreshRelationToggles();
        RefreshRelationSlots(metadata);
        RefreshBackButtonState();
    }

    // 상단 아이템 정보 영역을 갱신한다
    private void RefreshHeader(ItemMetaDataSo metadata)
    {
        string displayName = GetDisplayName(currentType, metadata);
        string description = metadata == null ? "아이템 정보를 찾을 수 없습니다." : metadata.InfoText;
        Sprite sprite = metadata == null ? null : metadata.ItemImage;

        SetText(itemNameText, "{" + displayName + "}");
        SetText(itemDescriptionText, description);
        SetImage(itemImage, sprite);
    }

    // 상단 아이템 정보 영역을 빈 상태로 정리한다
    private void ClearHeader()
    {
        SetText(itemNameText, string.Empty);
        SetText(itemDescriptionText, string.Empty);
        SetImage(itemImage, null);
    }

    // 현재 관계 모드에 맞게 슬롯 목록을 갱신한다
    private void RefreshRelationSlots(ItemMetaDataSo metadata)
    {
        ClearAllRelationSlots();

        if (metadata == null)
        {
            SetText(relationTitleText, "아이템");
            return;
        }

        if (currentRelationMode == RelationMode.RequiredForCraft)
        {
            SetText(relationTitleText, "다음을 위해 필요");
            FillRequiredForCraftSlots();
            return;
        }

        SetText(relationTitleText, "제작시 필요 자원");
        FillCraftInputSlots(metadata);
    }

    // 현재 아이템을 재료로 사용하는 제작 결과 아이템과 진화 대상 터렛을 슬롯에 채운다
    private void FillRequiredForCraftSlots()
    {
        int slotIndex = FillRequiredForCraftItemSlots(0);
        FillRequiredForEvolutionTurretSlots(slotIndex);
    }

    // 현재 아이템을 재료로 사용하는 제작 결과 아이템을 슬롯에 채운다
    private int FillRequiredForCraftItemSlots(int startSlotIndex)
    {
        if (InventorySystem.Inst == null || InventorySystem.Inst.Types == null)
        {
            return startSlotIndex;
        }

        int slotIndex = startSlotIndex;
        foreach (RewardCurrencyType candidateType in InventorySystem.Inst.Types)
        {
            ItemMetaDataSo candidateMetadata = InventorySystem.Inst.GetMetaData(candidateType);
            if (candidateMetadata == null || !candidateMetadata.Createable || candidateMetadata.ItemsToCreate == null)
            {
                continue;
            }

            for (int i = 0; i < candidateMetadata.ItemsToCreate.Count; i++)
            {
                ItemCraftData craftData = candidateMetadata.ItemsToCreate[i];
                if (!craftData.Type.Equals(currentType))
                {
                    continue;
                }

                if (!TryConfigureRelationSlot(slotIndex, candidateType, FormatRequiredForCraftAmount(candidateMetadata, craftData)))
                {
                    return slotIndex;
                }

                slotIndex++;
                break;
            }
        }

        return slotIndex;
    }

    // 현재 아이템을 진화 재료로 사용하는 터렛을 슬롯에 채운다
    private void FillRequiredForEvolutionTurretSlots(int startSlotIndex)
    {
        if (relatedTurretDefinitions == null)
        {
            return;
        }

        int slotIndex = startSlotIndex;
        for (int i = 0; i < relatedTurretDefinitions.Length; i++)
        {
            TurretDefinitionSO sourceDefinition = relatedTurretDefinitions[i];
            TurretEvolutionProgressionSO progression = sourceDefinition == null ? null : sourceDefinition.evolutionProgressionProfile;
            if (progression == null || progression.evolutionEntries == null)
            {
                continue;
            }

            if (!TryFillEvolutionEntryRelationSlots(progression.evolutionEntries, ref slotIndex))
            {
                return;
            }
        }
    }

    // 진화 엔트리 배열에서 현재 아이템을 요구하는 터렛 슬롯을 채운다
    private bool TryFillEvolutionEntryRelationSlots(TurretEvolutionEntry[] entries, ref int slotIndex)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            TurretEvolutionEntry entry = entries[i];
            ResourceCost matchingCost = FindMatchingEvolutionCost(entry);
            if (matchingCost == null)
            {
                continue;
            }

            if (!TryConfigureTurretRelationSlot(slotIndex, entry, FormatEvolutionCostAmount(matchingCost)))
            {
                return false;
            }

            slotIndex++;
        }

        return true;
    }

    // 진화 엔트리에서 현재 아이템과 일치하는 비용을 찾는다
    private ResourceCost FindMatchingEvolutionCost(TurretEvolutionEntry entry)
    {
        if (entry == null || entry.targetDefinition == null || entry.evolutionCosts == null)
        {
            return null;
        }

        for (int i = 0; i < entry.evolutionCosts.Length; i++)
        {
            ResourceCost cost = entry.evolutionCosts[i];
            if (cost != null && cost.currencyType.Equals(currentType))
            {
                return cost;
            }
        }

        return null;
    }

    // 현재 아이템 제작에 필요한 재료 아이템을 슬롯에 채운다
    private void FillCraftInputSlots(ItemMetaDataSo metadata)
    {
        if (!metadata.Createable || metadata.ItemsToCreate == null)
        {
            return;
        }

        for (int i = 0; i < metadata.ItemsToCreate.Count; i++)
        {
            ItemCraftData craftData = metadata.ItemsToCreate[i];
            if (!TryConfigureRelationSlot(i, craftData.Type, FormatCraftInputAmount(craftData)))
            {
                return;
            }
        }
    }

    // 지정 슬롯에 아이템 관계 정보를 표시한다
    private bool TryConfigureRelationSlot(int slotIndex, RewardCurrencyType relationType, string amountText)
    {
        if (relationSlots == null || slotIndex < 0 || slotIndex >= relationSlots.Length)
        {
            return false;
        }

        TurretItemDescriptionRelationSlotUI slot = relationSlots[slotIndex];
        if (slot == null)
        {
            return true;
        }

        slot.Configure(this, relationType, amountText);
        return true;
    }

    // 지정 슬롯에 터렛 진화 관계 정보를 표시한다
    private bool TryConfigureTurretRelationSlot(int slotIndex, TurretEvolutionEntry entry, string amountText)
    {
        if (relationSlots == null || slotIndex < 0 || slotIndex >= relationSlots.Length)
        {
            return false;
        }

        TurretItemDescriptionRelationSlotUI slot = relationSlots[slotIndex];
        if (slot == null)
        {
            return true;
        }

        slot.ConfigureTurret(GetEvolutionDisplayName(entry), GetEvolutionIcon(entry), amountText);
        return true;
    }

    // 모든 관계 슬롯을 빈 상태로 돌린다
    private void ClearAllRelationSlots()
    {
        if (relationSlots == null)
        {
            return;
        }

        for (int i = 0; i < relationSlots.Length; i++)
        {
            if (relationSlots[i] != null)
            {
                relationSlots[i].Clear();
            }
        }
    }

    // 제작 결과 슬롯의 수량 표시 문구를 만든다
    private static string FormatRequiredForCraftAmount(ItemMetaDataSo targetMetadata, ItemCraftData craftData)
    {
        int resultCount = targetMetadata == null ? 0 : Mathf.Max(0, targetMetadata.CountPerCraft);
        int requiredCount = Mathf.Max(0, craftData.Count);
        return "제작 +" + resultCount + " / 필요 " + requiredCount;
    }

    // 제작 필요 재료 슬롯의 수량 표시 문구를 만든다
    private static string FormatCraftInputAmount(ItemCraftData craftData)
    {
        int requiredCount = Mathf.Max(0, craftData.Count);
        return "필요 " + requiredCount;
    }

    // 터렛 진화 재료 슬롯의 수량 표시 문구를 만든다
    private static string FormatEvolutionCostAmount(ResourceCost cost)
    {
        int requiredCount = cost == null ? 0 : Mathf.Max(0, cost.amount);
        return "진화 필요 " + requiredCount;
    }

    // 진화 엔트리 표시 이름을 반환한다
    private static string GetEvolutionDisplayName(TurretEvolutionEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        TurretDefinitionSO targetDefinition = entry.targetDefinition;
        if (targetDefinition != null && !string.IsNullOrWhiteSpace(targetDefinition.displayName))
        {
            return targetDefinition.displayName;
        }

        if (!string.IsNullOrWhiteSpace(entry.displayName))
        {
            return entry.displayName;
        }

        return targetDefinition == null ? string.Empty : targetDefinition.name;
    }

    // 진화 엔트리 표시 아이콘을 반환한다
    private static Sprite GetEvolutionIcon(TurretEvolutionEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        if (entry.targetDefinition != null && entry.targetDefinition.uiIcon != null)
        {
            return entry.targetDefinition.uiIcon;
        }

        return entry.evolutionIcon;
    }

    // 관계 모드 토글 상태를 현재 모드에 맞게 반영한다
    private void RefreshRelationToggles()
    {
        SetToggle(requiredForCraftToggle, currentRelationMode == RelationMode.RequiredForCraft);
        SetToggle(craftInputToggle, currentRelationMode == RelationMode.CraftInput);
    }

    // 뒤로가기 버튼 활성 상태를 현재 팝업 표시 상태에 맞춘다
    private void RefreshBackButtonState()
    {
        if (backButton != null)
        {
            backButton.interactable = hasCurrentItem;
        }
    }

    // 제작 관계 토글 입력을 처리한다
    private void OnRequiredForCraftToggleChanged(bool isOn)
    {
        if (isOn)
        {
            SetRelationMode(RelationMode.RequiredForCraft);
            Refresh();
        }
    }

    // 제작 필요 재료 토글 입력을 처리한다
    private void OnCraftInputToggleChanged(bool isOn)
    {
        if (isOn)
        {
            SetRelationMode(RelationMode.CraftInput);
            Refresh();
        }
    }

    // 현재 관계 표시 모드를 변경한다
    private void SetRelationMode(RelationMode relationMode)
    {
        currentRelationMode = relationMode;
        RefreshRelationToggles();
    }

    // 히스토리 제한을 지키며 이전 아이템을 저장한다
    private void PushHistory(RewardCurrencyType itemType)
    {
        if (!hasCurrentItem)
        {
            return;
        }

        if (history.Count >= Mathf.Max(1, historyLimit))
        {
            history.RemoveAt(0);
        }

        history.Add(itemType);
    }

    // 아이템 메타데이터를 안전하게 조회한다
    private static ItemMetaDataSo GetMetaData(RewardCurrencyType itemType)
    {
        return InventorySystem.Inst == null ? null : InventorySystem.Inst.GetMetaData(itemType);
    }

    // 아이템 표시 이름을 반환한다
    private static string GetDisplayName(RewardCurrencyType itemType, ItemMetaDataSo metadata)
    {
        if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Name))
        {
            return metadata.Name;
        }

        return itemType.ToString();
    }

    // 팝업 루트 활성 상태를 변경한다
    private void SetRootActive(bool isActive)
    {
        if (popupRoot != null && popupRoot.activeSelf != isActive)
        {
            popupRoot.SetActive(isActive);
        }
    }

    // 버튼과 토글 이벤트를 등록한다
    private void BindButtonListeners()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(Back);
            backButton.onClick.AddListener(Back);
        }

        if (inventoryButton != null)
        {
            inventoryButton.onClick.RemoveListener(OpenInventory);
            inventoryButton.onClick.AddListener(OpenInventory);
        }

        if (requiredForCraftToggle != null)
        {
            requiredForCraftToggle.onValueChanged.RemoveListener(OnRequiredForCraftToggleChanged);
            requiredForCraftToggle.onValueChanged.AddListener(OnRequiredForCraftToggleChanged);
        }

        if (craftInputToggle != null)
        {
            craftInputToggle.onValueChanged.RemoveListener(OnCraftInputToggleChanged);
            craftInputToggle.onValueChanged.AddListener(OnCraftInputToggleChanged);
        }
    }

    // 버튼과 토글 이벤트를 해제한다
    private void UnbindButtonListeners()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(Back);
        }

        if (inventoryButton != null)
        {
            inventoryButton.onClick.RemoveListener(OpenInventory);
        }

        if (requiredForCraftToggle != null)
        {
            requiredForCraftToggle.onValueChanged.RemoveListener(OnRequiredForCraftToggleChanged);
        }

        if (craftInputToggle != null)
        {
            craftInputToggle.onValueChanged.RemoveListener(OnCraftInputToggleChanged);
        }
    }

    // 필수 수동 연결 참조 누락을 경고한다
    private void ValidateRequiredReferences()
    {
        if (popupRoot == null)
        {
            Debug.LogWarning("[TurretItemDescriptionPopupUI] Popup Root 참조가 비어 있습니다.", this);
        }

        if (itemNameText == null || itemDescriptionText == null || itemImage == null)
        {
            Debug.LogWarning("[TurretItemDescriptionPopupUI] 상단 아이템 정보 참조가 일부 비어 있습니다.", this);
        }

        if (requiredForCraftToggle == null || craftInputToggle == null)
        {
            Debug.LogWarning("[TurretItemDescriptionPopupUI] 관계 표시 토글 참조가 일부 비어 있습니다.", this);
        }

        if (relationSlots == null || relationSlots.Length == 0)
        {
            Debug.LogWarning("[TurretItemDescriptionPopupUI] 관계 아이템 슬롯 배열이 비어 있습니다.", this);
        }

        if (closeButton == null || backButton == null || inventoryButton == null)
        {
            Debug.LogWarning("[TurretItemDescriptionPopupUI] 닫기/뒤로가기/인벤토리 버튼 참조가 일부 비어 있습니다.", this);
        }

        if (inventoryUI == null)
        {
            Debug.LogWarning("[TurretItemDescriptionPopupUI] Inventory UI 참조가 비어 있습니다.", this);
        }

        if (turretSelectionUI == null)
        {
            Debug.LogWarning("[TurretItemDescriptionPopupUI] Turret Selection UI 참조가 비어 있습니다. 인벤토리 열기 시 터렛 팝업 입력 차단이 남을 수 있습니다.", this);
        }
    }

    // 토글 참조가 있을 때 이벤트 없이 값을 적용한다
    private static void SetToggle(Toggle toggle, bool isOn)
    {
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(isOn);
        }
    }

    // 텍스트 참조가 있을 때만 값을 적용한다
    private static void SetText(TMP_Text targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }

    // 이미지 참조가 있을 때만 스프라이트를 적용한다
    private static void SetImage(Image targetImage, Sprite sprite)
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.sprite = sprite;
        targetImage.enabled = sprite != null;
        targetImage.preserveAspect = true;
    }
}
