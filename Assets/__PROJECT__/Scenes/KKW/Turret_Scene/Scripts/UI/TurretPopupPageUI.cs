using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 터렛 하위 팝업의 공통 표시, 닫기, 선택 컨텍스트 보관을 담당한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretPopupPageUI : MonoBehaviour
{
    [Header("표시 루트")]
    [SerializeField] private GameObject popupRoot;

    [Header("버튼")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backButton;

    private TurretSelectionUIController owner;
    protected TurretSelectionContext CurrentContext;

    // 컴포넌트 추가 시 기본 루트 참조를 현재 오브젝트로 설정한다
    private void Reset()
    {
        popupRoot = gameObject;
    }

    protected Button CloseButton => closeButton;
    protected Button BackButton => backButton;
    protected bool IsVisible => popupRoot != null && popupRoot.activeInHierarchy;

    // 시작 전에 버튼 이벤트를 연결한다
    protected virtual void Awake()
    {
        ValidateCommonReferences();
        BindButtonListeners();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    protected virtual void OnDestroy()
    {
        UnbindButtonListeners();
    }

    // 선택 UI 컨트롤러 참조를 설정한다
    public void Initialize(TurretSelectionUIController owner_)
    {
        owner = owner_;
    }

    // 선택된 터렛 컨텍스트로 팝업을 표시한다
    public virtual void Show(TurretSelectionContext context)
    {
        CurrentContext = context;

        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }
    }

    // 팝업을 숨긴다
    public virtual void Hide()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    // 공통 팝업 필수 참조 누락을 확인한다
    protected void ValidateCommonReferences()
    {
        if (popupRoot == null)
        {
            Debug.LogWarning($"[{nameof(TurretPopupPageUI)}] {name}의 Popup Root 참조가 비어 있습니다. 팝업 표시/숨김이 동작하지 않을 수 있습니다.", this);
        }
    }

    // 닫기 버튼 입력으로 전체 터렛 선택을 해제한다
    public void RequestCloseSelection()
    {
        if (owner != null)
        {
            owner.ClearSelection();
            return;
        }

        Hide();
    }

    // 뒤로가기 버튼 입력으로 선택 허브 팝업으로 돌아간다
    public void RequestBackToSelectPopup()
    {
        if (owner != null)
        {
            owner.ShowSelectPopupFromChild();
            return;
        }

        Hide();
    }

    // 하위 팝업에서 변경된 선택 컨텍스트를 상위 컨트롤러에 반영한다
    protected void RequestSelectionContextUpdate(TurretSelectionContext context)
    {
        if (owner != null)
        {
            owner.UpdateSelectionFromChild(context);
        }
    }

    // 버튼 클릭 이벤트를 등록한다
    private void BindButtonListeners()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(RequestCloseSelection);
            closeButton.onClick.AddListener(RequestCloseSelection);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(RequestBackToSelectPopup);
            backButton.onClick.AddListener(RequestBackToSelectPopup);
        }
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButtonListeners()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(RequestCloseSelection);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(RequestBackToSelectPopup);
        }
    }
}
