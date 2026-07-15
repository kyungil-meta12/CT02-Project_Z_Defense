using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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
    [SerializeField] private TMP_Text itemOwnedCountText;
    [SerializeField] private Image itemImage;

    [Header("관계 표시 모드")]
    [SerializeField] private Toggle requiredForCraftToggle;
    [SerializeField] private Toggle decomposeOutputToggle;
    [SerializeField] private TMP_Text relationTitleText;

    [Header("관계 슬롯")]
    [SerializeField] private TurretItemDescriptionRelationSlotUI[] relationSlots = Array.Empty<TurretItemDescriptionRelationSlotUI>();

    [Header("버튼")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button inventoryButton;

    [Header("연결 UI")]
    [SerializeField] private InventoryUI inventoryUI;

    [Header("탐색")]
    [SerializeField, Min(1)] private int historyLimit = DEFAULT_HISTORY_LIMIT;

    private readonly List<RewardCurrencyType> history = new List<RewardCurrencyType>(DEFAULT_HISTORY_LIMIT);
    private RewardCurrencyType currentType;
    private RelationMode currentRelationMode = RelationMode.RequiredForCraft;
    private bool hasCurrentItem;

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
        Hide();
        if (inventoryUI != null)
        {
            inventoryUI.OnOpenInventory();
            return;
        }

        Debug.LogWarning("[TurretItemDescriptionPopupUI] Inventory UI 참조가 없어 인벤토리를 열 수 없습니다.", this);
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
        SetText(itemOwnedCountText, "보유량: " + GetOwnedCountText(currentType));
        SetImage(itemImage, sprite);
    }

    // 상단 아이템 정보 영역을 빈 상태로 정리한다
    private void ClearHeader()
    {
        SetText(itemNameText, string.Empty);
        SetText(itemDescriptionText, string.Empty);
        SetText(itemOwnedCountText, string.Empty);
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
            SetText(relationTitleText, "제작 가능 아이템");
            FillRequiredForCraftSlots();
            return;
        }

        SetText(relationTitleText, "제작시 필요 자원");
        FillCraftInputSlots(metadata);
    }

    // 현재 아이템을 재료로 사용하는 제작 결과 아이템을 슬롯에 채운다
    private void FillRequiredForCraftSlots()
    {
        if (InventorySystem.Inst == null || InventorySystem.Inst.Types == null)
        {
            return;
        }

        int slotIndex = 0;
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
                    return;
                }

                slotIndex++;
                break;
            }
        }
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

    // 관계 모드 토글 상태를 현재 모드에 맞게 반영한다
    private void RefreshRelationToggles()
    {
        SetToggle(requiredForCraftToggle, currentRelationMode == RelationMode.RequiredForCraft);
        SetToggle(decomposeOutputToggle, currentRelationMode == RelationMode.CraftInput);
    }

    // 뒤로가기 버튼 활성 상태를 현재 히스토리에 맞춘다
    private void RefreshBackButtonState()
    {
        if (backButton != null)
        {
            backButton.interactable = history.Count > 0;
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
    private void OnDecomposeOutputToggleChanged(bool isOn)
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

    // 현재 보유량 표시 문자열을 반환한다
    private static string GetOwnedCountText(RewardCurrencyType itemType)
    {
        return InventorySystem.Inst == null ? "0" : InventorySystem.Inst.GetCountString(itemType);
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

        if (decomposeOutputToggle != null)
        {
            decomposeOutputToggle.onValueChanged.RemoveListener(OnDecomposeOutputToggleChanged);
            decomposeOutputToggle.onValueChanged.AddListener(OnDecomposeOutputToggleChanged);
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

        if (decomposeOutputToggle != null)
        {
            decomposeOutputToggle.onValueChanged.RemoveListener(OnDecomposeOutputToggleChanged);
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

        if (requiredForCraftToggle == null || decomposeOutputToggle == null)
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
