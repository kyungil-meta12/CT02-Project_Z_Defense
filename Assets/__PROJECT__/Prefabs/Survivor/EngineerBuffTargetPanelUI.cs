using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엔지니어에게 버프를 받을 터렛 대상을 UI 버튼 목록으로 선택하게 한다.
/// </summary>
[DisallowMultipleComponent]
public class EngineerBuffTargetPanelUI : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button closeButton;

    [Header("버튼 목록")]
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private EngineerBuffTargetButton targetButtonPrefab;
    [SerializeField] private EngineerBuffTargetButton[] targetButtons = System.Array.Empty<EngineerBuffTargetButton>();

    [Header("터렛 베이스")]
    [SerializeField] private TurretBaseSlot[] targetSlots = System.Array.Empty<TurretBaseSlot>();

    private SurvivorInteractionController owner;
    private Survivor pendingEngineer;
    private bool isCloseButtonBound;

    // 컴포넌트 추가 시 기본 UI 참조를 자동으로 연결한다
    private void Reset()
    {
        AutoBindReferences();
    }

    // 시작 전에 참조를 보완하고 버튼 이벤트를 연결한다
    private void Awake()
    {
        AutoBindReferences();
        AutoBindTargetButtons();
        BindCloseButton();
        DisableTemplateButton();
    }

    // 파괴될 때 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindCloseButton();
    }

    // 지정한 엔지니어의 버프 대상 선택 패널을 표시한다
    public void Show(Survivor engineer, SurvivorInteractionController owner_)
    {
        pendingEngineer = engineer;
        owner = owner_;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        RefreshTargets();
    }

    // 버프 대상 선택 패널을 숨긴다
    public void Hide()
    {
        pendingEngineer = null;
        owner = null;

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    // 버튼에서 선택한 터렛 슬롯을 컨트롤러로 전달한다
    public void OnTargetButtonClicked(TurretBaseSlot targetSlot)
    {
        if (owner == null)
        {
            Hide();
            return;
        }

        if (owner.TryAssignPendingEngineerToTurret(targetSlot))
        {
            Hide();
        }
        else
        {
            RefreshTargets();
        }
    }

    // 닫기 버튼 입력으로 선택을 취소한다
    private void OnCloseButtonClicked()
    {
        if (owner != null)
        {
            owner.CancelPendingEngineerAssignment();
        }

        Hide();
    }

    // 현재 설치된 터렛 슬롯을 기준으로 버튼 목록을 갱신한다
    private void RefreshTargets()
    {
        if (titleText != null)
        {
            titleText.text = "Select Buff Target";
        }

        int visibleButtonCount = RefreshTargetButtons();
        HideUnmappedButtons();
        DisableTemplateButton();

        if (statusText != null)
        {
            statusText.text = visibleButtonCount > 0 ? "Choose a turret to buff." : "No turret available.";
        }
    }

    // 지정 슬롯이 엔지니어 버프 대상으로 유효한지 확인한다
    private static bool IsValidTargetSlot(TurretBaseSlot slot)
    {
        return slot != null && slot.isActiveAndEnabled && slot.RefreshAndGetCurrentTurret() != null;
    }

    // 버튼에 표시할 터렛 대상 이름을 만든다
    private static string CreateTargetLabel(TurretBaseSlot slot, int displayIndex)
    {
        TurretDefinitionRuntimeController currentTurret = slot == null ? null : slot.RefreshAndGetCurrentTurret();
        if (currentTurret == null)
        {
            return "Empty";
        }

        string turretName = currentTurret.CurrentTurretName;
        if (string.IsNullOrEmpty(turretName))
        {
            turretName = currentTurret.name;
        }

        return "Target " + displayIndex + " - " + turretName;
    }

    // 등록된 터렛 베이스와 버튼을 1:1로 갱신한다
    private int RefreshTargetButtons()
    {
        int visibleButtonCount = 0;
        int count = Mathf.Min(targetSlots.Length, targetButtons.Length);
        for (int i = 0; i < count; i++)
        {
            EngineerBuffTargetButton targetButton = targetButtons[i];
            if (targetButton == null)
            {
                continue;
            }

            TurretBaseSlot slot = targetSlots[i];
            if (!IsValidTargetSlot(slot))
            {
                targetButton.Clear();
                targetButton.gameObject.SetActive(false);
                continue;
            }

            targetButton.Configure(this, slot, CreateTargetLabel(slot, i + 1), true);
            targetButton.gameObject.SetActive(true);
            visibleButtonCount++;
        }

        return visibleButtonCount;
    }

    // 슬롯 배열에 매핑되지 않은 버튼을 숨긴다
    private void HideUnmappedButtons()
    {
        for (int i = targetSlots.Length; i < targetButtons.Length; i++)
        {
            if (targetButtons[i] != null)
            {
                targetButtons[i].Clear();
                targetButtons[i].gameObject.SetActive(false);
            }
        }
    }

    // 템플릿 버튼이 실제 대상 버튼처럼 클릭되지 않도록 비활성화한다
    private void DisableTemplateButton()
    {
        if (targetButtonPrefab == null)
        {
            return;
        }

        targetButtonPrefab.Clear();
        targetButtonPrefab.gameObject.SetActive(false);
    }

    // 필요한 패널 참조를 비어 있는 경우 자동으로 보완한다
    private void AutoBindReferences()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (buttonContainer == null)
        {
            buttonContainer = transform;
        }
    }

    // 직렬화된 버튼 배열이 없으면 컨테이너 하위 버튼을 수집한다
    private void AutoBindTargetButtons()
    {
        if ((targetButtons != null && targetButtons.Length > 0) || buttonContainer == null)
        {
            return;
        }

        targetButtons = buttonContainer.GetComponentsInChildren<EngineerBuffTargetButton>(true);
    }

    // 닫기 버튼 이벤트를 중복 없이 연결한다
    private void BindCloseButton()
    {
        if (isCloseButtonBound || closeButton == null)
        {
            return;
        }

        closeButton.onClick.AddListener(OnCloseButtonClicked);
        isCloseButtonBound = true;
    }

    // 닫기 버튼 이벤트를 해제한다
    private void UnbindCloseButton()
    {
        if (!isCloseButtonBound || closeButton == null)
        {
            return;
        }

        closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        isCloseButtonBound = false;
    }
}
