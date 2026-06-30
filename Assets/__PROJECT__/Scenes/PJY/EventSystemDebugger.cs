using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// EventSystem UI 레이캐스트와 포인터 이벤트 전달 상태를 진단하고, 필요 시 구식 입력 브릿지로 UI 이벤트를 보조한다.
/// </summary>
public class EventSystemDebugger : MonoBehaviour
{
    [Header("입력 브릿지")]
    [SerializeField] private bool enableLegacyPointerBridge = true;
    [SerializeField] private bool logUiRaycastDiagnostics = false;
    [SerializeField] private bool logLegacyPointerBridge = false;
    [SerializeField, Min(1.0f)] private float dragThresholdPixels = 10.0f;

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);
    private PointerEventData activePointerData;
    private GameObject pointerPress;
    private GameObject pointerDrag;
    private GameObject rawPointerPress;
    private Vector2 previousPointerPosition;
    private bool isPointerDown;
    private bool isDragging;

    // 포인터 입력 시 현재 UI 레이캐스트 결과를 출력한다
    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || IsTouchBegan())
        {
            if (EventSystem.current == null)
            {
                Debug.LogWarning("[UI 레이캐스트 진단] EventSystem.current가 없습니다.", this);
                return;
            }

            Vector2 pointerPosition = GetPointerPosition();
            PointerEventData pointerData = CreatePointerData(pointerPosition, Vector2.zero);
            Raycast(pointerData);
            if (logUiRaycastDiagnostics)
            {
                LogRaycastResults(pointerData);
            }

            if (enableLegacyPointerBridge)
            {
                ProcessLegacyPointerDown(pointerData);
            }
        }

        if (!enableLegacyPointerBridge || !isPointerDown)
        {
            return;
        }

        if (Input.GetMouseButton(0) || IsTouchMovedOrStationary())
        {
            ProcessLegacyPointerDrag(GetPointerPosition());
        }

        if (Input.GetMouseButtonUp(0) || IsTouchEnded())
        {
            ProcessLegacyPointerUp(GetPointerPosition());
        }
    }

    // 터치 시작 여부를 확인한다
    private static bool IsTouchBegan()
    {
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
    }

    // 터치 유지 또는 이동 여부를 확인한다
    private static bool IsTouchMovedOrStationary()
    {
        if (Input.touchCount <= 0)
        {
            return false;
        }

        TouchPhase phase = Input.GetTouch(0).phase;
        return phase == TouchPhase.Moved || phase == TouchPhase.Stationary;
    }

    // 터치 종료 여부를 확인한다
    private static bool IsTouchEnded()
    {
        if (Input.touchCount <= 0)
        {
            return false;
        }

        TouchPhase phase = Input.GetTouch(0).phase;
        return phase == TouchPhase.Ended || phase == TouchPhase.Canceled;
    }

    // 현재 주 포인터 위치를 반환한다
    private static Vector2 GetPointerPosition()
    {
        if (Input.touchCount > 0)
        {
            return Input.GetTouch(0).position;
        }

        return Input.mousePosition;
    }

    // 포인터 이벤트 데이터를 생성한다
    private static PointerEventData CreatePointerData(Vector2 pointerPosition, Vector2 pointerDelta)
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.button = PointerEventData.InputButton.Left;
        pointerData.pointerId = -1;
        pointerData.position = pointerPosition;
        pointerData.delta = pointerDelta;
        pointerData.pressPosition = pointerPosition;
        pointerData.clickTime = Time.unscaledTime;
        pointerData.clickCount = 1;
        pointerData.useDragThreshold = true;
        return pointerData;
    }

    // 현재 포인터 위치로 UI 레이캐스트를 수행한다
    private void Raycast(PointerEventData pointerData)
    {
        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, raycastResults);
        pointerData.pointerCurrentRaycast = raycastResults.Count > 0 ? raycastResults[0] : new RaycastResult();
    }

    // 포인터 다운 이벤트를 구식 입력 브릿지로 전달한다
    private void ProcessLegacyPointerDown(PointerEventData pointerData)
    {
        isPointerDown = true;
        isDragging = false;
        activePointerData = pointerData;
        previousPointerPosition = pointerData.position;

        GameObject currentOverObject = pointerData.pointerCurrentRaycast.gameObject;
        rawPointerPress = currentOverObject;
        pointerPress = currentOverObject == null ? null : ExecuteEvents.ExecuteHierarchy(currentOverObject, pointerData, ExecuteEvents.pointerDownHandler);
        GameObject clickHandler = currentOverObject == null ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverObject);
        if (pointerPress == null)
        {
            pointerPress = clickHandler;
        }

        pointerDrag = currentOverObject == null ? null : ExecuteEvents.GetEventHandler<IDragHandler>(currentOverObject);
        if (pointerDrag != null)
        {
            ExecuteEvents.Execute(pointerDrag, pointerData, ExecuteEvents.initializePotentialDrag);
        }

        pointerData.pointerPress = pointerPress;
        pointerData.rawPointerPress = rawPointerPress;
        pointerData.pointerDrag = pointerDrag;
        pointerData.pointerPressRaycast = pointerData.pointerCurrentRaycast;
        pointerData.eligibleForClick = true;

        LogBridge($"포인터 다운 전달 / 히트: {GetObjectName(currentOverObject)} / Press: {GetObjectName(pointerPress)} / Drag: {GetObjectName(pointerDrag)}");
    }

    // 포인터 드래그 이벤트를 구식 입력 브릿지로 전달한다
    private void ProcessLegacyPointerDrag(Vector2 pointerPosition)
    {
        if (activePointerData == null)
        {
            return;
        }

        Vector2 pointerDelta = pointerPosition - previousPointerPosition;
        previousPointerPosition = pointerPosition;
        activePointerData.position = pointerPosition;
        activePointerData.delta = pointerDelta;
        Raycast(activePointerData);

        if (pointerDrag == null)
        {
            return;
        }

        if (!isDragging && (pointerPosition - activePointerData.pressPosition).sqrMagnitude >= dragThresholdPixels * dragThresholdPixels)
        {
            isDragging = true;
            activePointerData.dragging = true;
            activePointerData.eligibleForClick = false;
            ExecuteEvents.Execute(pointerDrag, activePointerData, ExecuteEvents.beginDragHandler);
            LogBridge($"드래그 시작 전달 / Drag: {GetObjectName(pointerDrag)} / 위치: {pointerPosition}");
        }

        if (!isDragging)
        {
            return;
        }

        ExecuteEvents.Execute(pointerDrag, activePointerData, ExecuteEvents.dragHandler);
    }

    // 포인터 업 이벤트를 구식 입력 브릿지로 전달한다
    private void ProcessLegacyPointerUp(Vector2 pointerPosition)
    {
        if (activePointerData == null)
        {
            ClearLegacyPointerState();
            return;
        }

        activePointerData.position = pointerPosition;
        activePointerData.delta = pointerPosition - previousPointerPosition;
        Raycast(activePointerData);

        if (pointerPress != null)
        {
            ExecuteEvents.Execute(pointerPress, activePointerData, ExecuteEvents.pointerUpHandler);
        }

        GameObject currentOverObject = activePointerData.pointerCurrentRaycast.gameObject;
        GameObject clickHandler = currentOverObject == null ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverObject);
        if (!isDragging && pointerPress != null && pointerPress == clickHandler)
        {
            ExecuteEvents.Execute(pointerPress, activePointerData, ExecuteEvents.pointerClickHandler);
        }

        if (isDragging && pointerDrag != null)
        {
            ExecuteEvents.Execute(pointerDrag, activePointerData, ExecuteEvents.endDragHandler);
        }

        LogBridge($"포인터 업 전달 / 히트: {GetObjectName(currentOverObject)} / Press: {GetObjectName(pointerPress)} / Drag: {GetObjectName(pointerDrag)} / 드래그: {isDragging}");
        ClearLegacyPointerState();
    }

    // 구식 입력 브릿지의 현재 포인터 상태를 초기화한다
    private void ClearLegacyPointerState()
    {
        isPointerDown = false;
        isDragging = false;
        activePointerData = null;
        pointerPress = null;
        pointerDrag = null;
        rawPointerPress = null;
    }

    // UI 레이캐스트 결과를 로그로 출력한다
    private void LogRaycastResults(PointerEventData pointerData)
    {
        if (raycastResults.Count > 0)
        {
            Debug.Log($"[UI 레이캐스트 진단] 포인터: {pointerData.position} / 히트 수: {raycastResults.Count}", this);
            for (int i = 0; i < raycastResults.Count; i++)
            {
                RaycastResult result = raycastResults[i];
                GameObject hitObject = result.gameObject;
                Debug.Log($"[UI 레이캐스트 진단] {i}번 / 오브젝트: {hitObject.name} / 레이어: {LayerMask.LayerToName(hitObject.layer)} / 루트: {GetRootName(hitObject)} / 모듈: {result.module}", hitObject);
            }

            return;
        }

        Debug.Log("[UI 레이캐스트 진단] 포인터 아래 UI 히트가 없습니다.", this);
    }

    // 입력 브릿지의 이벤트 전달 상태를 로그로 출력한다
    private void LogBridge(string message)
    {
        if (!logLegacyPointerBridge)
        {
            return;
        }

        Debug.Log($"[UI 입력 브릿지] {message}", this);
    }

    // 로그용 오브젝트 이름을 반환한다
    private static string GetObjectName(GameObject target)
    {
        return target == null ? "없음" : target.name;
    }

    // 로그에서 UI 루트 계층을 빠르게 구분할 수 있도록 루트 이름을 반환한다
    private static string GetRootName(GameObject hitObject)
    {
        if (hitObject == null)
        {
            return "없음";
        }

        Transform root = hitObject.transform;
        while (root.parent != null)
        {
            root = root.parent;
        }

        return root.name;
    }
}
