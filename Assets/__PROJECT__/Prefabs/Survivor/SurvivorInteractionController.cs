using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 생존자 클릭 상호작용, 치료/역할 선택 UI, 엔지니어 터렛 드래그 배치를 관리한다.
/// </summary>
public class SurvivorInteractionController : MonoBehaviour
{
    private const int RAYCAST_HIT_BUFFER_SIZE = 8;

    [Header("선택")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask survivorLayerMask = ~0;
    [SerializeField] private LayerMask turretSlotLayerMask = ~0;
    [SerializeField, Min(1f)] private float maxRayDistance = 500f;

    [Header("팝업 UI")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button treatmentButton;
    [SerializeField] private Button constructionWorkerButton;
    [SerializeField] private Button engineerButton;

    private readonly RaycastHit[] raycastHits = new RaycastHit[RAYCAST_HIT_BUFFER_SIZE];
    private Survivor selectedSurvivor;
    private Survivor draggingEngineer;
    private TurretBaseSlot hoveredTurretSlot;

    // 필요한 참조를 확인하고 버튼 이벤트를 연결한다
    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        BindButtons();
        HidePopup();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButtons();
    }

    // 매 프레임 클릭 선택 또는 엔지니어 드래그를 처리한다
    private void Update()
    {
        if (draggingEngineer != null)
        {
            UpdateEngineerDrag();
            return;
        }

        if (!WasPrimaryPointerPressed() || IsPointerOverUI())
        {
            return;
        }

        if (TryGetPrimaryPointerPosition(out Vector2 pointerPosition))
        {
            HandleWorldPointerDown(pointerPosition);
        }
    }

    // 치료 버튼 입력을 처리한다
    public void OnTreatmentButtonClicked()
    {
        if (selectedSurvivor == null || !selectedSurvivor.TryStartTreatment())
        {
            RefreshPopup();
            return;
        }

        HidePopup();
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

    // 클릭한 월드 대상에 따라 생존자를 선택하거나 엔지니어 드래그를 시작한다
    private void HandleWorldPointerDown(Vector2 pointerPosition)
    {
        if (!TrySelectSurvivor(pointerPosition, out Survivor survivor))
        {
            HidePopup();
            return;
        }

        selectedSurvivor = survivor;

        if (survivor.CanBeginEngineerAssignment && survivor.TryBeginEngineerAssignment())
        {
            draggingEngineer = survivor;
            HidePopup();
            return;
        }

        ShowPopup(survivor);
    }

    // 현재 포인터 위치의 생존자를 레이캐스트로 찾는다
    private bool TrySelectSurvivor(Vector2 pointerPosition, out Survivor survivor)
    {
        survivor = null;
        if (targetCamera == null)
        {
            return false;
        }

        Ray ray = targetCamera.ScreenPointToRay(pointerPosition);
        int hitCount = Physics.RaycastNonAlloc(ray, raycastHits, maxRayDistance, survivorLayerMask, QueryTriggerInteraction.Collide);
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            if (hit.collider == null || hit.distance >= closestDistance)
            {
                continue;
            }

            Survivor hitSurvivor = hit.collider.GetComponentInParent<Survivor>();
            if (hitSurvivor == null)
            {
                continue;
            }

            closestDistance = hit.distance;
            survivor = hitSurvivor;
        }

        return survivor != null;
    }

    // 엔지니어 드래그 중 터렛 슬롯 탐색과 드롭을 처리한다
    private void UpdateEngineerDrag()
    {
        if (!TryGetPrimaryPointerPosition(out Vector2 pointerPosition))
        {
            CancelEngineerDrag();
            return;
        }

        hoveredTurretSlot = FindTurretSlot(pointerPosition);

        if (WasPrimaryPointerReleased())
        {
            CompleteEngineerDrag();
        }
    }

    // 포인터 위치의 터렛 슬롯을 반환한다
    private TurretBaseSlot FindTurretSlot(Vector2 pointerPosition)
    {
        if (targetCamera == null)
        {
            return null;
        }

        Ray ray = targetCamera.ScreenPointToRay(pointerPosition);
        int hitCount = Physics.RaycastNonAlloc(ray, raycastHits, maxRayDistance, turretSlotLayerMask, QueryTriggerInteraction.Collide);
        float closestDistance = float.MaxValue;
        TurretBaseSlot closestSlot = null;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            if (hit.collider == null || hit.distance >= closestDistance)
            {
                continue;
            }

            TurretBaseSlot slot = hit.collider.GetComponentInParent<TurretBaseSlot>();
            if (slot == null)
            {
                continue;
            }

            closestDistance = hit.distance;
            closestSlot = slot;
        }

        return closestSlot;
    }

    // 엔지니어 드롭 위치가 유효하면 터렛에 버프를 등록한다
    private void CompleteEngineerDrag()
    {
        Survivor engineer = draggingEngineer;
        TurretBaseSlot targetSlot = hoveredTurretSlot;
        draggingEngineer = null;
        hoveredTurretSlot = null;

        if (engineer == null || targetSlot == null || targetSlot.CurrentTurret == null)
        {
            return;
        }

        TurretEngineerBuffReceiver buffReceiver = targetSlot.CurrentTurret.GetComponent<TurretEngineerBuffReceiver>();
        if (buffReceiver == null)
        {
            buffReceiver = targetSlot.CurrentTurret.gameObject.AddComponent<TurretEngineerBuffReceiver>();
        }

        engineer.TryAssignEngineerToTurret(buffReceiver, targetSlot, targetSlot.BuildPoint);
    }

    // 엔지니어 드래그 상태를 취소한다
    private void CancelEngineerDrag()
    {
        draggingEngineer = null;
        hoveredTurretSlot = null;
    }

    // 선택된 생존자에게 역할을 부여하고 UI를 갱신한다
    private void AssignSelectedRole(SurvivorRole role)
    {
        if (selectedSurvivor == null || !selectedSurvivor.TryAssignRole(role))
        {
            RefreshPopup();
            return;
        }

        HidePopup();
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

    // 현재 주 포인터 위치를 반환한다
    private static bool TryGetPrimaryPointerPosition(out Vector2 pointerPosition)
    {
        if (Input.touchCount > 0)
        {
            pointerPosition = Input.GetTouch(0).position;
            return true;
        }

        pointerPosition = Input.mousePosition;
        return true;
    }

    // 주 포인터가 이번 프레임 눌렸는지 확인한다
    private static bool WasPrimaryPointerPressed()
    {
        if (Input.touchCount > 0)
        {
            return Input.GetTouch(0).phase == TouchPhase.Began;
        }

        return Input.GetMouseButtonDown(0);
    }

    // 주 포인터가 이번 프레임 해제됐는지 확인한다
    private static bool WasPrimaryPointerReleased()
    {
        if (Input.touchCount > 0)
        {
            TouchPhase phase = Input.GetTouch(0).phase;
            return phase == TouchPhase.Ended || phase == TouchPhase.Canceled;
        }

        return Input.GetMouseButtonUp(0);
    }

    // 현재 포인터가 UI 위에 있는지 확인한다
    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            return EventSystem.current.IsPointerOverGameObject(touch.fingerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }
}
