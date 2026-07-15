using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템 설명 팝업에서 제작 관계 아이템 하나를 버튼 슬롯으로 표시한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretItemDescriptionRelationSlotUI : MonoBehaviour
{
    [Header("표시 루트")]
    [SerializeField] private GameObject slotRoot;

    [Header("버튼")]
    [SerializeField] private Button itemButton;

    [Header("표시 요소")]
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemAmountText;

    private TurretItemDescriptionPopupUI owner;
    private RewardCurrencyType itemType;
    private bool hasItemType;

    // 컴포넌트 추가 시 같은 오브젝트의 버튼과 표시 루트를 기본값으로 설정한다
    private void Reset()
    {
        slotRoot = gameObject;
        itemButton = GetComponent<Button>();
        itemImage = GetComponentInChildren<Image>(true);
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length > 0)
        {
            itemNameText = texts[0];
        }

        if (texts.Length > 1)
        {
            itemAmountText = texts[1];
        }
    }

    // 활성화 준비 시 버튼 이벤트를 연결한다
    private void Awake()
    {
        BindButtonListener();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButtonListener();
    }

    // 지정 아이템 관계 정보를 슬롯에 표시한다
    public void Configure(TurretItemDescriptionPopupUI owner_, RewardCurrencyType itemType_, string amountText)
    {
        owner = owner_;
        itemType = itemType_;
        hasItemType = true;

        SetRootActive(true);
        SetButtonInteractable(true);
        SetItemVisual(itemType_);
        SetText(itemAmountText, amountText);
    }

    // 슬롯을 비활성 표시 상태로 비운다
    public void Clear()
    {
        owner = null;
        hasItemType = false;

        SetRootActive(false);
        SetButtonInteractable(false);
        SetImage(itemImage, null);
        SetText(itemNameText, string.Empty);
        SetText(itemAmountText, string.Empty);
    }

    // 슬롯 버튼 클릭 시 해당 아이템 설명으로 이동한다
    private void OnItemButtonClicked()
    {
        if (!hasItemType || owner == null)
        {
            return;
        }

        owner.NavigateTo(itemType);
    }

    // 버튼 클릭 이벤트를 등록한다
    private void BindButtonListener()
    {
        if (itemButton != null)
        {
            itemButton.onClick.RemoveListener(OnItemButtonClicked);
            itemButton.onClick.AddListener(OnItemButtonClicked);
        }
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButtonListener()
    {
        if (itemButton != null)
        {
            itemButton.onClick.RemoveListener(OnItemButtonClicked);
        }
    }

    // 아이템 메타데이터를 이용해 이름과 이미지를 표시한다
    private void SetItemVisual(RewardCurrencyType type)
    {
        ItemMetaDataSo metadata = InventorySystem.Inst == null ? null : InventorySystem.Inst.GetMetaData(type);
        string displayName = metadata == null || string.IsNullOrWhiteSpace(metadata.Name) ? type.ToString() : metadata.Name;
        Sprite sprite = metadata == null ? null : metadata.ItemImage;

        SetText(itemNameText, displayName);
        SetImage(itemImage, sprite);
    }

    // 슬롯 루트 활성 상태를 변경한다
    private void SetRootActive(bool isActive)
    {
        if (slotRoot != null && slotRoot.activeSelf != isActive)
        {
            slotRoot.SetActive(isActive);
        }
    }

    // 버튼 상호작용 상태를 변경한다
    private void SetButtonInteractable(bool interactable)
    {
        if (itemButton != null)
        {
            itemButton.interactable = interactable;
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
