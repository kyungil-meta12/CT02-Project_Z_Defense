using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 비용 슬롯 버튼에서 공용 아이템 설명 팝업을 여는 클릭 전달 컴포넌트다.
/// </summary>
[DisallowMultipleComponent]
public class TurretItemDescriptionOpenButton : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button button;

    private TurretItemDescriptionPopupUI popup;
    private RewardCurrencyType itemType;
    private bool hasItemType;

    // 컴포넌트 추가 시 같은 오브젝트의 버튼을 기본 참조로 설정한다
    private void Reset()
    {
        button = GetComponent<Button>();
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

    // 비용 슬롯이 가리킬 아이템 타입과 팝업 참조를 설정한다
    public void Configure(TurretItemDescriptionPopupUI popup_, RewardCurrencyType itemType_, bool interactable)
    {
        popup = popup_;
        itemType = itemType_;
        hasItemType = interactable;
        SetInteractable(interactable && popup != null);
    }

    // 비용 슬롯 클릭 정보를 비우고 버튼을 비활성화한다
    public void Clear()
    {
        popup = null;
        hasItemType = false;
        SetInteractable(false);
    }

    // 버튼 입력으로 아이템 설명 팝업을 연다
    private void OnButtonClicked()
    {
        if (!hasItemType || popup == null)
        {
            return;
        }

        popup.Show(itemType);
    }

    // 버튼 클릭 이벤트를 등록한다
    private void BindButtonListener()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButtonListener()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
        }
    }

    // 버튼 상호작용 상태를 변경한다
    private void SetInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }
}
