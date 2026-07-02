using UnityEngine;

/// <summary>
/// 터렛 클릭 선택, 사거리 표시, 선택 허브 팝업과 하위 팝업 전환을 조율한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretSelectionUIController : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] private TurretPlacementController placementController;
    [SerializeField] private bool requireSecondClickToOpenSelectPopup = true;
    [SerializeField, Min(0.05f)] private float secondClickInterval = 1.0f;

    [Header("팝업 참조")]
    [SerializeField] private TurretSelectPopupUI selectPopup;
    [SerializeField] private TurretUpgradePopupUI upgradePopup;
    [SerializeField] private TurretDetailPopupUI detailPopup;
    [SerializeField] private TurretEvolutionPopupUI evolutionPopup;
    [SerializeField] private TurretSkillPopupUI skillPopup;

    [Header("사거리 표시")]
    [SerializeField] private bool showRangeIndicatorOnSelection = true;
    [SerializeField] private TurretRangeIndicator rangeIndicator;
    [SerializeField] private GameObject rangeIndicatorPrefab;
    [SerializeField, Min(0.001f)] private float rangeIndicatorPrefabRadiusAtScaleOne = 1.0f;
    [SerializeField] private bool forceRangeIndicatorPrefabParticleLoop = true;
    [SerializeField] private bool restartRangeIndicatorPrefabParticlesOnShow = true;
    [SerializeField] private bool useLineRangeIndicatorFallback = true;
    [SerializeField, Min(12)] private int rangeIndicatorSegments = 96;
    [SerializeField, Min(0.001f)] private float rangeIndicatorLineWidth = 0.08f;
    [SerializeField] private float rangeIndicatorYOffset = 0.05f;
    [SerializeField] private Color rangeIndicatorColor = new Color(0.2f, 0.85f, 1.0f, 0.65f);

    private TurretSelectionContext currentContext;
    private TurretDefinitionRuntimeController lastClickedTurret;
    private float lastTurretClickTime = -1.0f;
    private bool hasSubscribedCameraTouch;
    private bool hasLoggedMissingCameraTouch;

    // 컴포넌트 추가 시 현재 UI 하위 팝업을 자동으로 찾는다
    private void Reset()
    {
        BindChildReferences();
    }

    // 시작 전에 팝업 참조와 이벤트를 준비한다
    private void Awake()
    {
        BindChildReferences();
        InitializePopups();
        BindPopupEvents();
        HideAllPopups();
        HideRangeIndicator();
    }

    // 활성화될 때 카메라 터치 이벤트 구독을 시도한다
    private void OnEnable()
    {
        SubscribeCameraTouchEvent();
    }

    // 비활성화될 때 카메라 터치 이벤트 구독을 해제한다
    private void OnDisable()
    {
        UnsubscribeCameraTouchEvent();
    }

    // 파괴 시 이벤트 구독과 팝업 이벤트를 정리한다
    private void OnDestroy()
    {
        UnsubscribeCameraTouchEvent();
        UnbindPopupEvents();
    }

    // 늦게 생성되는 카메라 터치 핸들러 구독을 보완한다
    private void Update()
    {
        SubscribeCameraTouchEvent();

        if (placementController != null && placementController.IsPlacing)
        {
            ClearSelection();
        }
    }

    [ContextMenu("참조 다시 연결")]
    // 컨텍스트 메뉴에서 터렛 UI 하위 팝업 참조를 다시 찾는다
    public void BindChildReferences()
    {
        selectPopup = selectPopup != null ? selectPopup : GetComponentInChildren<TurretSelectPopupUI>(true);
        upgradePopup = upgradePopup != null ? upgradePopup : GetComponentInChildren<TurretUpgradePopupUI>(true);
        detailPopup = detailPopup != null ? detailPopup : GetComponentInChildren<TurretDetailPopupUI>(true);
        evolutionPopup = evolutionPopup != null ? evolutionPopup : GetComponentInChildren<TurretEvolutionPopupUI>(true);
        skillPopup = skillPopup != null ? skillPopup : GetComponentInChildren<TurretSkillPopupUI>(true);
    }

    // 현재 선택을 유지한 채 선택 허브 팝업으로 돌아간다
    public void ShowSelectPopupFromChild()
    {
        if (!currentContext.IsValid)
        {
            ClearSelection();
            return;
        }

        HideChildPopups();
        selectPopup?.Show(currentContext);
        RefreshRangeIndicator();
    }

    // 선택, 팝업, 사거리 표시를 모두 해제한다
    public void ClearSelection()
    {
        currentContext = new TurretSelectionContext(null, null);
        ClearPendingTurretClick();
        HideAllPopups();
        HideRangeIndicator();
    }

    // 하위 팝업에서 변경된 선택 터렛 컨텍스트를 현재 선택에 반영한다
    public void UpdateSelectionFromChild(TurretSelectionContext context)
    {
        if (!context.IsValid)
        {
            ClearSelection();
            return;
        }

        currentContext = context;
        ClearPendingTurretClick();
        RefreshRangeIndicator();
    }

    // 카메라 터치 이벤트로 전달된 월드 히트에서 터렛 선택을 처리한다
    private void OnCameraTargetTouched(RaycastHit hit)
    {
        if (placementController != null && placementController.IsPlacing)
        {
            ClearSelection();
            return;
        }

        if (TryGetTurretSelectionFromHit(hit, out TurretDefinitionRuntimeController turret, out TurretBaseSlot slot))
        {
            HandleTurretClick(turret, slot);
        }
    }

    // 터렛 클릭 횟수와 시간 간격에 따라 사거리 또는 선택 팝업을 표시한다
    private void HandleTurretClick(TurretDefinitionRuntimeController turret, TurretBaseSlot slot)
    {
        if (turret == null)
        {
            ClearSelection();
            return;
        }

        TurretSelectionContext nextContext = new TurretSelectionContext(turret, slot);
        if (!requireSecondClickToOpenSelectPopup)
        {
            OpenSelectPopup(nextContext);
            return;
        }

        float currentTime = Time.unscaledTime;
        bool isSameTurret = lastClickedTurret == turret;
        bool isWithinInterval = currentTime - lastTurretClickTime <= secondClickInterval;
        if (isSameTurret && isWithinInterval)
        {
            OpenSelectPopup(nextContext);
            return;
        }

        lastClickedTurret = turret;
        lastTurretClickTime = currentTime;
        SelectRangeOnly(nextContext);
    }

    // 터렛은 선택하되 팝업 없이 사거리만 표시한다
    private void SelectRangeOnly(TurretSelectionContext context)
    {
        currentContext = context;
        HideAllPopups();
        RefreshRangeIndicator();
    }

    // 선택 허브 팝업을 열고 사거리 표시를 유지한다
    private void OpenSelectPopup(TurretSelectionContext context)
    {
        currentContext = context;
        ClearPendingTurretClick();
        HideChildPopups();
        selectPopup?.Show(currentContext);
        RefreshRangeIndicator();
    }

    // 업그레이드 팝업 열기 요청을 처리한다
    private void OpenUpgradePopup()
    {
        if (!currentContext.IsValid)
        {
            ClearSelection();
            return;
        }

        HideAllPopups();
        upgradePopup?.Show(currentContext);
        RefreshRangeIndicator();
    }

    // 상세정보 팝업 열기 요청을 처리한다
    private void OpenDetailPopup()
    {
        if (!currentContext.IsValid)
        {
            ClearSelection();
            return;
        }

        HideAllPopups();
        detailPopup?.Show(currentContext);
        RefreshRangeIndicator();
    }

    // 스킬 팝업 열기 요청을 처리한다
    private void OpenSkillPopup()
    {
        if (!currentContext.IsValid)
        {
            ClearSelection();
            return;
        }

        HideAllPopups();
        skillPopup?.Show(currentContext);
        RefreshRangeIndicator();
    }

    // 진화 팝업 열기 요청을 처리한다
    private void OpenEvolutionPopup()
    {
        if (!currentContext.IsValid)
        {
            ClearSelection();
            return;
        }

        HideAllPopups();
        evolutionPopup?.Show(currentContext);
        RefreshRangeIndicator();
    }

    // 모든 팝업을 숨긴다
    private void HideAllPopups()
    {
        selectPopup?.Hide();
        HideChildPopups();
    }

    // 선택 허브를 제외한 하위 팝업을 숨긴다
    private void HideChildPopups()
    {
        upgradePopup?.Hide();
        detailPopup?.Hide();
        evolutionPopup?.Hide();
        skillPopup?.Hide();
    }

    // 현재 선택된 터렛의 사거리 표시를 갱신한다
    private void RefreshRangeIndicator()
    {
        if (!showRangeIndicatorOnSelection || !currentContext.IsValid)
        {
            HideRangeIndicator();
            return;
        }

        EnsureRangeIndicator();
        if (rangeIndicator == null)
        {
            return;
        }

        TurretRuntimeStat stat = currentContext.CalculateCurrentStat();
        rangeIndicator.ConfigurePrefab(
            rangeIndicatorPrefab,
            rangeIndicatorPrefabRadiusAtScaleOne,
            forceRangeIndicatorPrefabParticleLoop,
            restartRangeIndicatorPrefabParticlesOnShow,
            useLineRangeIndicatorFallback);
        rangeIndicator.Show(currentContext.GetRangeCenter(), stat.range, rangeIndicatorSegments, rangeIndicatorLineWidth, rangeIndicatorYOffset, rangeIndicatorColor);
    }

    // 사거리 표시 컴포넌트를 준비한다
    private void EnsureRangeIndicator()
    {
        if (rangeIndicator != null)
        {
            return;
        }

        GameObject indicatorObject = new GameObject("TurretRangeIndicator_Runtime");
        rangeIndicator = indicatorObject.AddComponent<TurretRangeIndicator>();
    }

    // 사거리 표시를 숨긴다
    private void HideRangeIndicator()
    {
        if (rangeIndicator != null)
        {
            rangeIndicator.Hide();
        }
    }

    // 마지막 클릭 대기 상태를 초기화한다
    private void ClearPendingTurretClick()
    {
        lastClickedTurret = null;
        lastTurretClickTime = -1.0f;
    }

    // 팝업 컴포넌트에 상위 컨트롤러 참조를 전달한다
    private void InitializePopups()
    {
        upgradePopup?.Initialize(this);
        detailPopup?.Initialize(this);
        evolutionPopup?.Initialize(this);
        skillPopup?.Initialize(this);
    }

    // 선택 팝업 이벤트를 등록한다
    private void BindPopupEvents()
    {
        if (selectPopup == null)
        {
            return;
        }

        selectPopup.UpgradeRequested -= OpenUpgradePopup;
        selectPopup.DetailRequested -= OpenDetailPopup;
        selectPopup.SkillRequested -= OpenSkillPopup;
        selectPopup.CloseRequested -= ClearSelection;
        selectPopup.UpgradeRequested += OpenUpgradePopup;
        selectPopup.DetailRequested += OpenDetailPopup;
        selectPopup.SkillRequested += OpenSkillPopup;
        selectPopup.CloseRequested += ClearSelection;

        if (detailPopup != null)
        {
            detailPopup.UpgradeRequested -= OpenUpgradePopup;
            detailPopup.UpgradeRequested += OpenUpgradePopup;
        }

        if (upgradePopup != null)
        {
            upgradePopup.EvolutionPopupRequested -= OpenEvolutionPopup;
            upgradePopup.EvolutionPopupRequested += OpenEvolutionPopup;
        }
    }

    // 선택 팝업 이벤트를 해제한다
    private void UnbindPopupEvents()
    {
        if (selectPopup == null)
        {
            return;
        }

        selectPopup.UpgradeRequested -= OpenUpgradePopup;
        selectPopup.DetailRequested -= OpenDetailPopup;
        selectPopup.SkillRequested -= OpenSkillPopup;
        selectPopup.CloseRequested -= ClearSelection;

        if (detailPopup != null)
        {
            detailPopup.UpgradeRequested -= OpenUpgradePopup;
        }

        if (upgradePopup != null)
        {
            upgradePopup.EvolutionPopupRequested -= OpenEvolutionPopup;
        }
    }

    // 카메라 터치 핸들러 이벤트를 구독한다
    private void SubscribeCameraTouchEvent()
    {
        if (hasSubscribedCameraTouch)
        {
            return;
        }

        if (CameraTouchHandler.Inst == null)
        {
            LogMissingCameraTouchOnce();
            return;
        }

        CameraTouchHandler.Inst.OnCameraTargetTouchEvent += OnCameraTargetTouched;
        hasSubscribedCameraTouch = true;
    }

    // 카메라 터치 핸들러 이벤트 구독을 해제한다
    private void UnsubscribeCameraTouchEvent()
    {
        if (!hasSubscribedCameraTouch)
        {
            return;
        }

        if (CameraTouchHandler.Inst != null)
        {
            CameraTouchHandler.Inst.OnCameraTargetTouchEvent -= OnCameraTargetTouched;
        }

        hasSubscribedCameraTouch = false;
    }

    // 카메라 터치 핸들러 누락을 한 번만 경고한다
    private void LogMissingCameraTouchOnce()
    {
        if (hasLoggedMissingCameraTouch)
        {
            return;
        }

        Debug.LogWarning("[TurretSelectionUIController] CameraTouchHandler가 없어 터렛 선택 UI 입력을 받을 수 없습니다.", this);
        hasLoggedMissingCameraTouch = true;
    }

    // 레이캐스트 히트에서 터렛 컨트롤러와 설치 슬롯을 찾는다
    private static bool TryGetTurretSelectionFromHit(RaycastHit hit, out TurretDefinitionRuntimeController turret, out TurretBaseSlot slot)
    {
        turret = null;
        slot = null;

        if (hit.collider == null)
        {
            return false;
        }

        turret = hit.collider.GetComponentInParent<TurretDefinitionRuntimeController>();
        if (turret != null)
        {
            slot = turret.GetComponentInParent<TurretBaseSlot>();
            return true;
        }

        slot = hit.collider.GetComponentInParent<TurretBaseSlot>();
        if (slot == null)
        {
            return false;
        }

        turret = slot.CurrentTurret != null ? slot.CurrentTurret : slot.RefreshAndGetCurrentTurret();
        return turret != null;
    }
}
