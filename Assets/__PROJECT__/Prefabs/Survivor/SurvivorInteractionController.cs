using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 생존자 클릭 상호작용, 치료/역할 선택 UI, 엔지니어 터렛 UI 배치를 관리한다.
/// </summary>
public class SurvivorInteractionController : MonoBehaviour
{
    [Header("팝업 UI")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button treatmentButton;
    [SerializeField] private Button constructionWorkerButton;
    [SerializeField] private Button engineerButton;

    [Header("엔지니어 버프 대상 UI")]
    [SerializeField] private EngineerBuffTargetPanelUI engineerBuffTargetPanel;

    private Survivor selectedSurvivor;
    private Survivor pendingEngineer;
    private bool hasSubscribedCameraTouch;

    // 필요한 참조를 확인하고 버튼 이벤트를 연결한다
    private void Awake()
    {
        BindButtons();
        HidePopup();
        HideEngineerBuffTargetPanel();
    }

    // 활성화될 때 카메라 터치 이벤트를 구독한다
    private void OnEnable()
    {
        SubscribeCameraTouchEvent();
    }

    // 비활성화될 때 카메라 터치 이벤트를 해제한다
    private void OnDisable()
    {
        UnsubscribeCameraTouchEvent();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnsubscribeCameraTouchEvent();
        UnbindButtons();
    }

    // 카메라 터치 이벤트 구독 상태를 보강한다
    private void Update()
    {
        SubscribeCameraTouchEvent();
    }

    // 치료 버튼 입력을 처리한다
    public void OnTreatmentButtonClicked()
    {
        Survivor survivor = selectedSurvivor;
        if (survivor == null)
        {
            Debug.LogWarning("[SurvivorInteractionController] 선택된 생존자가 없어 치료 명령을 처리할 수 없습니다.", this);
            HidePopup();
            return;
        }

        if (!survivor.TryStartTreatment())
        {
            RefreshPopup();
            return;
        }

        Debug.Log("[SurvivorInteractionController] 생존자 치료 이동 명령을 전달했습니다.", survivor);
        StartCoroutine(HidePopupAfterCommandFrame());
    }

    // 건축노동자 역할 버튼 입력을 처리한다
    public void OnConstructionWorkerButtonClicked()
    {
        AssignSelectedRole(SurvivorRole.constructionWorker);
    }

    // 엔지니어 역할 버튼 입력을 처리한다
    public void OnEngineerButtonClicked()
    {
        AssignSelectedRole(SurvivorRole.engineer);
    }

    // 대기 중인 엔지니어를 지정한 터렛 슬롯에 배치한다
    public bool TryAssignPendingEngineerToTurret(TurretBaseSlot targetSlot)
    {
        Survivor engineer = pendingEngineer;
        TurretDefinitionRuntimeController currentTurret = targetSlot == null ? null : targetSlot.RefreshAndGetCurrentTurret();
        if (engineer == null || currentTurret == null)
        {
            return false;
        }

        TurretEngineerBuffReceiver buffReceiver = currentTurret.GetComponent<TurretEngineerBuffReceiver>();
        if (buffReceiver == null)
        {
            buffReceiver = currentTurret.gameObject.AddComponent<TurretEngineerBuffReceiver>();
        }

        if (!engineer.TryAssignEngineerToTurret(buffReceiver, targetSlot, targetSlot.BuildPoint))
        {
            return false;
        }

        ClearPendingEngineerPlacement();
        HidePopup();
        HideEngineerBuffTargetPanel();
        return true;
    }

    // 엔지니어 버프 대상 선택을 취소한다
    public void CancelPendingEngineerAssignment()
    {
        ClearPendingEngineerPlacement();
    }

    // 배경 클릭 입력으로 생존자 관련 UI를 닫는다
    public void OnBackgroundButtonClicked()
    {
        ClearPendingEngineerPlacement();
        HidePopup();
        HideEngineerBuffTargetPanel();
    }

    // 카메라 터치 이벤트로 전달된 월드 히트에서 생존자를 선택한다
    private void OnCameraTargetTouched(RaycastHit hit)
    {
        if (!TrySelectSurvivorFromHit(hit, out Survivor survivor))
        {
            return;
        }

        selectedSurvivor = survivor;

        if (survivor.CanBeginEngineerAssignment && survivor.TryBeginEngineerAssignment())
        {
            pendingEngineer = survivor;
            HidePopup();
            ShowEngineerBuffTargetPanel(survivor);
            return;
        }

        ClearPendingEngineerPlacement();
        HideEngineerBuffTargetPanel();
        ShowPopup(survivor);
    }

    // 월드 히트 결과에서 선택 가능한 생존자를 찾는다
    private static bool TrySelectSurvivorFromHit(RaycastHit hit, out Survivor survivor)
    {
        survivor = null;

        if (hit.collider == null)
        {
            return false;
        }

        survivor = hit.collider.GetComponentInParent<Survivor>();
        return survivor != null;
    }

    // 엔지니어 버프 대상 선택 UI를 표시한다
    private void ShowEngineerBuffTargetPanel(Survivor engineer)
    {
        if (engineerBuffTargetPanel == null)
        {
            Debug.LogWarning("[SurvivorInteractionController] 엔지니어 버프 대상 UI가 연결되지 않았습니다.", this);
            ClearPendingEngineerPlacement();
            return;
        }

        engineerBuffTargetPanel.Show(engineer, this);
    }

    // 엔지니어 버프 대상 선택 UI를 숨긴다
    private void HideEngineerBuffTargetPanel()
    {
        if (engineerBuffTargetPanel != null)
        {
            engineerBuffTargetPanel.Hide();
        }
    }

    // 엔지니어 터렛 배치 대기 상태를 해제한다
    private void ClearPendingEngineerPlacement()
    {
        pendingEngineer = null;
    }

    // 선택된 생존자에게 역할을 부여하고 UI를 갱신한다
    private void AssignSelectedRole(SurvivorRole role)
    {
        Survivor survivor = selectedSurvivor;
        Debug.Log("[SurvivorInteractionController] 생존자 역할 부여 버튼 입력을 받았습니다.", this);

        if (survivor == null)
        {
            Debug.LogWarning("[SurvivorInteractionController] 선택된 생존자가 없어 역할 부여 명령을 처리할 수 없습니다.", this);
            HidePopup();
            return;
        }

        if (!survivor.TryAssignRole(role))
        {
            Debug.LogWarning("[SurvivorInteractionController] 생존자 역할 부여 조건을 만족하지 못했습니다.", survivor);
            RefreshPopup();
            return;
        }

        Debug.Log("[SurvivorInteractionController] 생존자 역할 부여 명령을 처리했습니다.", survivor);
        StartCoroutine(HidePopupAfterCommandFrame());
    }

    // 선택 생존자 상태에 맞춰 팝업을 표시한다
    private void ShowPopup(Survivor survivor)
    {
        selectedSurvivor = survivor;
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }

        RefreshPopup();
    }

    // 팝업을 숨기고 선택을 해제한다
    private void HidePopup()
    {
        selectedSurvivor = null;
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    // UI 클릭 프레임이 끝난 뒤 팝업을 숨겨 입력 처리 순서를 안정화한다
    private IEnumerator HidePopupAfterCommandFrame()
    {
        yield return new WaitForEndOfFrame();
        HidePopup();
    }

    // 선택된 생존자 상태에 맞춰 텍스트와 버튼을 갱신한다
    private void RefreshPopup()
    {
        if (selectedSurvivor == null)
        {
            HidePopup();
            return;
        }

        if (titleText != null)
        {
            titleText.text = "Survivor";
        }

        if (statusText != null)
        {
            statusText.text = GetStatusText(selectedSurvivor);
        }

        SetButtonVisible(treatmentButton, selectedSurvivor.CanRequestTreatment);
        SetButtonVisible(constructionWorkerButton, selectedSurvivor.CanAssignRole);
        SetButtonVisible(engineerButton, selectedSurvivor.CanAssignRole);
    }

    // 생존자 상태 표시 문자열을 반환한다
    private static string GetStatusText(Survivor survivor)
    {
        if (survivor == null)
        {
            return string.Empty;
        }

        if (survivor.CanRequestTreatment)
        {
            return "Treatment Required";
        }

        if (survivor.CanAssignRole)
        {
            return "Choose Role";
        }

        if (survivor.Role == SurvivorRole.constructionWorker)
        {
            return "Construction Worker";
        }

        if (survivor.Role == SurvivorRole.engineer)
        {
            return "Engineer";
        }

        return "Moving";
    }

    // 버튼 표시 상태를 변경한다
    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button != null)
        {
            button.gameObject.SetActive(visible);
        }
    }

    // 버튼 클릭 이벤트를 연결한다
    private void BindButtons()
    {
        if (treatmentButton != null)
        {
            treatmentButton.onClick.AddListener(OnTreatmentButtonClicked);
        }

        if (constructionWorkerButton != null)
        {
            constructionWorkerButton.onClick.AddListener(OnConstructionWorkerButtonClicked);
        }

        if (engineerButton != null)
        {
            engineerButton.onClick.AddListener(OnEngineerButtonClicked);
        }
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButtons()
    {
        if (treatmentButton != null)
        {
            treatmentButton.onClick.RemoveListener(OnTreatmentButtonClicked);
        }

        if (constructionWorkerButton != null)
        {
            constructionWorkerButton.onClick.RemoveListener(OnConstructionWorkerButtonClicked);
        }

        if (engineerButton != null)
        {
            engineerButton.onClick.RemoveListener(OnEngineerButtonClicked);
        }
    }

    // 카메라 터치 이벤트를 중복 없이 구독한다
    private void SubscribeCameraTouchEvent()
    {
        if (hasSubscribedCameraTouch || CameraTouchHandler.Inst == null)
        {
            return;
        }

        CameraTouchHandler.Inst.OnCameraTargetTouchEvent += OnCameraTargetTouched;
        hasSubscribedCameraTouch = true;
    }

    // 카메라 터치 이벤트 구독을 해제한다
    private void UnsubscribeCameraTouchEvent()
    {
        if (!hasSubscribedCameraTouch || CameraTouchHandler.Inst == null)
        {
            hasSubscribedCameraTouch = false;
            return;
        }

        CameraTouchHandler.Inst.OnCameraTargetTouchEvent -= OnCameraTargetTouched;
        hasSubscribedCameraTouch = false;
    }
}
