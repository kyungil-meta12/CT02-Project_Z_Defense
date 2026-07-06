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

    // 컴포넌트 추가 시 버튼 참조를 자동 연결한다
    private void Reset()
    {
        button = GetComponent<Button>();
    }

    // 활성화 전에 버튼 이벤트를 연결한다
    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

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
        if (techTreeController == null)
        {
            Debug.LogWarning("[터렛 트리 UI] 터렛 트리 컨트롤러 참조가 없어 창을 열 수 없습니다.", this);
            return;
        }

        if (toggleWhenClicked)
        {
            techTreeController.Toggle();
            return;
        }

        techTreeController.Show();
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
