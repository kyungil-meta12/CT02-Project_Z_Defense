using TMPro;
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

    [Header("텍스트")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;

    private TurretSelectionUIController owner;
    protected TurretSelectionContext CurrentContext;

    // 컴포넌트 추가 시 기본 루트 참조를 현재 오브젝트로 설정한다
    private void Reset()
    {
        popupRoot = gameObject;
    }

    // 시작 전에 버튼 이벤트를 연결한다
    private void Awake()
    {
        if (popupRoot == null)
        {
            popupRoot = gameObject;
        }

        BindButtonListeners();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
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
        RefreshCommonTexts();

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

    // 공통 제목과 상태 문구를 현재 선택 기준으로 갱신한다
    protected virtual void RefreshCommonTexts()
    {
        if (titleText != null)
        {
            titleText.text = CurrentContext.GetDisplayName();
        }

        if (statusText != null)
        {
            statusText.text = CurrentContext.GetLevelText();
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
