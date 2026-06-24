using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엔지니어에게 버프를 받을 터렛 대상을 UI 버튼 목록으로 선택하게 한다.
/// </summary>
[DisallowMultipleComponent]
public class EngineerBuffTargetPanelUI : MonoBehaviour
{
    private const int TARGET_BUTTON_COUNT = 8;

    [Header("패널")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button closeButton;

    [Header("버튼 목록")]
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
        BindCloseButton();
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

    // 배경 클릭 입력으로 엔지니어 버프 대상 선택을 취소한다
    public void OnBackgroundButtonClicked()
    {
        OnCloseButtonClicked();
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

        int availableButtonCount = RefreshTargetButtons();

        if (statusText != null)
        {
            statusText.text = availableButtonCount > 0 ? "Choose a turret to buff." : "No turret available.";
        }
    }

    // 지정 슬롯이 엔지니어 버프 대상으로 유효한지 확인한다
    private bool IsValidTargetSlot(TurretBaseSlot slot)
    {
        if (slot == null || !slot.isActiveAndEnabled || pendingEngineer == null)
        {
            return false;
        }

        TurretDefinitionRuntimeController currentTurret = slot.RefreshAndGetCurrentTurret();
        if (currentTurret == null)
        {
            return false;
        }

        TurretEngineerBuffReceiver buffReceiver = currentTurret.GetComponent<TurretEngineerBuffReceiver>();
        if (buffReceiver != null)
        {
            return buffReceiver.CanRegisterEngineer(pendingEngineer);
        }

        TurretDefinitionSO definition = currentTurret.CurrentTurretDefinition;
        return definition != null && definition.maxEngineerSeatCount > 0;
    }

    // 버튼에 표시할 터렛 대상 이름을 만든다
    private static string CreateTargetLabel(TurretBaseSlot slot, int displayIndex)
    {
        TurretDefinitionRuntimeController currentTurret = slot == null ? null : slot.RefreshAndGetCurrentTurret();
        if (currentTurret == null)
        {
            return "Target " + displayIndex + " - Empty";
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
        int availableButtonCount = 0;
        int count = Mathf.Min(TARGET_BUTTON_COUNT, targetButtons.Length);
        for (int i = 0; i < count; i++)
        {
            EngineerBuffTargetButton targetButton = targetButtons[i];
            if (targetButton == null)
            {
                continue;
            }

            TurretBaseSlot slot = i < targetSlots.Length ? targetSlots[i] : null;
            bool isInteractable = IsValidTargetSlot(slot);
            targetButton.Configure(this, slot, CreateTargetLabel(slot, i + 1), isInteractable);
            targetButton.gameObject.SetActive(true);

            if (isInteractable)
            {
                availableButtonCount++;
            }
        }

        for (int i = count; i < targetButtons.Length; i++)
        {
            if (targetButtons[i] != null)
            {
                targetButtons[i].Clear();
                targetButtons[i].gameObject.SetActive(false);
            }
        }

        return availableButtonCount;
    }

    // 필요한 패널 참조를 비어 있는 경우 자동으로 보완한다
    private void AutoBindReferences()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }
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
