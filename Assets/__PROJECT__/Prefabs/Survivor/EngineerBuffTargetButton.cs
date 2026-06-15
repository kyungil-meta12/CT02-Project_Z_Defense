using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엔지니어 버프 대상 선택 UI의 터렛 슬롯 버튼 하나를 관리한다.
/// </summary>
[DisallowMultipleComponent]
public class EngineerBuffTargetButton : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;

    private EngineerBuffTargetPanelUI owner;
    private TurretBaseSlot targetSlot;
    private bool isBound;

    // 컴포넌트 추가 시 하위 UI 참조를 자동으로 연결한다
    private void Reset()
    {
        AutoBindReferences();
    }

    // 활성화 전에 참조를 보완하고 클릭 이벤트를 연결한다
    private void Awake()
    {
        AutoBindReferences();
        BindButton();
        SetInteractable(false);
    }

    // 파괴될 때 클릭 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButton();
    }

    // 버튼이 가리킬 터렛 슬롯과 표시 상태를 갱신한다
    public void Configure(EngineerBuffTargetPanelUI owner_, TurretBaseSlot targetSlot_, string label, bool interactable)
    {
        owner = owner_;
        targetSlot = targetSlot_;
        AutoBindReferences();
        BindButton();

        if (labelText != null)
        {
            labelText.text = label;
        }

        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    // 버튼 대상과 표시 상태를 초기화해 클릭할 수 없게 한다
    public void Clear()
    {
        AutoBindReferences();
        owner = null;
        targetSlot = null;
        SetInteractable(false);

        if (labelText != null)
        {
            labelText.text = string.Empty;
        }
    }

    // 클릭 시 선택한 터렛 슬롯을 패널에 전달한다
    private void OnButtonClicked()
    {
        if (owner == null || targetSlot == null)
        {
            return;
        }

        owner.OnTargetButtonClicked(targetSlot);
    }

    // 필요한 Button과 TMP_Text 참조를 자동으로 찾는다
    private void AutoBindReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (labelText == null)
        {
            labelText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    // 버튼 클릭 이벤트를 중복 없이 연결한다
    private void BindButton()
    {
        if (isBound || button == null)
        {
            return;
        }

        button.onClick.AddListener(OnButtonClicked);
        isBound = true;
    }

    // 버튼 상호작용 가능 여부를 변경한다
    private void SetInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButton()
    {
        if (!isBound || button == null)
        {
            return;
        }

        button.onClick.RemoveListener(OnButtonClicked);
        isBound = false;
    }
}
