using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BottomBar의 터렛 트리 버튼에서 전체 터렛 트리 팝업 열기 입력을 연결한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretTechTreeOpenButton : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Button button;
    [SerializeField] private TurretTechTreeUIController techTreeController;

    [Header("동작")]
    [SerializeField] private bool toggleWhenClicked = true;

    private bool hasRequiredReferences;

    // 컴포넌트 추가 시 버튼 참조를 자동 연결한다
    private void Reset()
    {
        button = GetComponent<Button>();
    }

    // 활성화 전에 버튼 이벤트를 연결한다
    private void Awake()
    {
        hasRequiredReferences = ValidateRequiredReferences();
        BindButton();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButton();
    }

    // 버튼 클릭 시 터렛 트리 팝업을 열거나 토글한다
    public void OpenTechTree()
    {
        if (!hasRequiredReferences)
        {
            Debug.LogWarning("[터렛 트리 UI] 필수 인스펙터 참조가 누락되어 창을 열 수 없습니다.", this);
            return;
        }

        if (toggleWhenClicked)
        {
            techTreeController.Toggle();
            return;
        }

        techTreeController.Show();
    }

    // 런타임에 필요한 인스펙터 참조가 모두 연결됐는지 확인한다
    private bool ValidateRequiredReferences()
    {
        bool isValid = true;
        isValid &= LogMissingReference(button, nameof(button));
        isValid &= LogMissingReference(techTreeController, nameof(techTreeController));
        return isValid;
    }

    // 단일 인스펙터 참조 누락 여부를 로그로 알린다
    private bool LogMissingReference(Object reference, string fieldName)
    {
        if (reference != null)
        {
            return true;
        }

        Debug.LogWarning("[터렛 트리 UI] " + fieldName + " 참조가 비어 있습니다. 인스펙터에서 직접 연결해야 합니다.", this);
        return false;
    }

    // 버튼 클릭 이벤트를 등록한다
    private void BindButton()
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(OpenTechTree);
        button.onClick.AddListener(OpenTechTree);
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButton()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OpenTechTree);
        }
    }
}
