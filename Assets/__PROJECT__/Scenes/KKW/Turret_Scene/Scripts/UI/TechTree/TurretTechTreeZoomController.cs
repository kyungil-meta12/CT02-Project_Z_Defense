using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 터렛 트리 Scroll View의 마우스 휠 확대/축소와 모바일 핀치 확대/축소를 제어한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretTechTreeZoomController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private GameObject inputBlockerRoot;

    [Header("확대 축소")]
    [SerializeField, Min(0.1f)] private float minZoom = 0.55f;
    [SerializeField, Min(0.1f)] private float maxZoom = 1.6f;
    [SerializeField, Min(0.01f)] private float defaultZoom = 1.0f;
    [SerializeField, Min(0.01f)] private float mouseWheelStep = 0.12f;
    [SerializeField, Min(0.01f)] private float pinchSensitivity = 1.0f;
    [SerializeField] private bool resetZoomOnEnable = true;

    private float currentZoom = 1.0f;
    private float previousPinchDistance;
    private bool isPinching;
    private bool didDisableScrollRectForPinch;
    private bool hasRequiredReferences;

    // 컴포넌트 추가 시 같은 오브젝트의 ScrollRect 참조를 자동 연결한다
    private void Reset()
    {
        scrollRect = GetComponent<ScrollRect>();
        BindScrollRectReferences();
    }

    // 시작 시 필수 참조와 초기 줌 값을 준비한다
    private void Awake()
    {
        BindScrollRectReferences();
        hasRequiredReferences = ValidateRequiredReferences();
        currentZoom = content == null ? defaultZoom : Mathf.Max(0.01f, content.localScale.x);
        DisableScrollWheelPan();
    }

    // 터렛 트리 창이 켜질 때 줌 상태를 초기화한다
    private void OnEnable()
    {
        isPinching = false;
        previousPinchDistance = 0.0f;
        DisableScrollWheelPan();
        RestoreScrollRectAfterPinch();

        if (resetZoomOnEnable)
        {
            ResetZoom();
        }
    }

    // 비활성화 시 핀치 입력 상태를 초기화한다
    private void OnDisable()
    {
        isPinching = false;
        previousPinchDistance = 0.0f;
        RestoreScrollRectAfterPinch();
    }

    // 매 프레임 PC 휠과 모바일 핀치 줌 입력을 처리한다
    private void Update()
    {
        if (!hasRequiredReferences || IsInputBlocked())
        {
            isPinching = false;
            RestoreScrollRectAfterPinch();
            return;
        }

        HandleMouseWheelZoom();
        HandlePinchZoom();
    }

    // ScrollRect 기준 필수 하위 참조를 보강한다
    private void BindScrollRectReferences()
    {
        if (scrollRect == null)
        {
            return;
        }

        viewport = viewport != null ? viewport : scrollRect.viewport;
        content = content != null ? content : scrollRect.content;
    }

    // ScrollRect의 기본 마우스 휠 이동을 끄고 휠 입력을 줌 전용으로 사용한다
    private void DisableScrollWheelPan()
    {
        if (scrollRect != null)
        {
            scrollRect.scrollSensitivity = 0.0f;
        }
    }

    // 터렛 트리 줌을 기본 배율로 되돌린다
    public void ResetZoom()
    {
        if (content == null)
        {
            return;
        }

        currentZoom = Mathf.Clamp(defaultZoom, minZoom, maxZoom);
        content.localScale = new Vector3(currentZoom, currentZoom, 1.0f);
    }

    // PC 마우스 휠 입력으로 줌 배율을 변경한다
    private void HandleMouseWheelZoom()
    {
        if (Input.touchCount > 0)
        {
            return;
        }

        float wheelDelta = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(wheelDelta, 0.0f))
        {
            return;
        }

        Vector2 screenPosition = ResolveZoomFocusPosition(Input.mousePosition);
        ApplyZoom(currentZoom * (1.0f + NormalizeWheelDelta(wheelDelta) * mouseWheelStep), screenPosition);
    }

    // 모바일 두 손가락 핀치 입력으로 줌 배율을 변경한다
    private void HandlePinchZoom()
    {
        if (Input.touchCount < 2)
        {
            isPinching = false;
            previousPinchDistance = 0.0f;
            RestoreScrollRectAfterPinch();
            return;
        }

        Touch firstTouch = Input.GetTouch(0);
        Touch secondTouch = Input.GetTouch(1);
        Vector2 firstPosition = firstTouch.position;
        Vector2 secondPosition = secondTouch.position;
        Vector2 centerPosition = (firstPosition + secondPosition) * 0.5f;

        if (!IsScreenPointInsideViewport(centerPosition))
        {
            isPinching = false;
            previousPinchDistance = 0.0f;
            RestoreScrollRectAfterPinch();
            return;
        }

        float currentPinchDistance = Vector2.Distance(firstPosition, secondPosition);
        if (!isPinching || previousPinchDistance <= 0.0f)
        {
            isPinching = true;
            previousPinchDistance = currentPinchDistance;
            DisableScrollRectDuringPinch();
            return;
        }

        if (Mathf.Approximately(currentPinchDistance, previousPinchDistance))
        {
            return;
        }

        float distanceRatio = currentPinchDistance / previousPinchDistance;
        float zoomMultiplier = Mathf.Lerp(1.0f, distanceRatio, pinchSensitivity);
        previousPinchDistance = currentPinchDistance;
        DisableScrollRectDuringPinch();
        ApplyZoom(currentZoom * zoomMultiplier, centerPosition);
    }

    // 지정 화면 좌표를 중심으로 Content 배율과 위치를 함께 보정한다
    private void ApplyZoom(float targetZoom, Vector2 screenPosition)
    {
        if (content == null)
        {
            return;
        }

        float clampedZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        if (Mathf.Approximately(clampedZoom, currentZoom))
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenPosition, null, out Vector2 localPoint))
        {
            return;
        }

        float previousZoom = currentZoom;
        currentZoom = clampedZoom;
        content.localScale = new Vector3(currentZoom, currentZoom, 1.0f);
        content.anchoredPosition += localPoint * (previousZoom - currentZoom);

        if (scrollRect != null)
        {
            scrollRect.StopMovement();
        }
    }

    // 포인터 좌표가 Viewport 밖이면 Viewport 중심을 줌 기준점으로 사용한다
    private Vector2 ResolveZoomFocusPosition(Vector2 screenPosition)
    {
        if (IsScreenPointInsideViewport(screenPosition))
        {
            return screenPosition;
        }

        return GetViewportCenterScreenPosition();
    }

    // Viewport 중심의 화면 좌표를 반환한다
    private Vector2 GetViewportCenterScreenPosition()
    {
        if (viewport == null)
        {
            return Vector2.zero;
        }

        Vector3[] corners = new Vector3[4];
        viewport.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) * 0.5f;
        return RectTransformUtility.WorldToScreenPoint(null, center);
    }

    // 상세 팝업처럼 상위 입력을 막아야 하는 루트가 활성화됐는지 확인한다
    private bool IsInputBlocked()
    {
        return inputBlockerRoot != null && inputBlockerRoot.activeInHierarchy;
    }

    // 화면 좌표가 Viewport 안에 있는지 확인한다
    private bool IsScreenPointInsideViewport(Vector2 screenPosition)
    {
        return viewport != null && RectTransformUtility.RectangleContainsScreenPoint(viewport, screenPosition, null);
    }

    // 환경마다 다른 휠 입력 크기를 한 단계 줌 입력으로 정규화한다
    private static float NormalizeWheelDelta(float wheelDelta)
    {
        if (Mathf.Approximately(wheelDelta, 0.0f))
        {
            return 0.0f;
        }

        return wheelDelta > 0.0f ? 1.0f : -1.0f;
    }

    // 핀치 중 ScrollRect 드래그 입력이 동시에 처리되지 않도록 잠시 비활성화한다
    private void DisableScrollRectDuringPinch()
    {
        if (scrollRect == null || didDisableScrollRectForPinch)
        {
            return;
        }

        scrollRect.StopMovement();
        scrollRect.enabled = false;
        didDisableScrollRectForPinch = true;
    }

    // 핀치가 끝나면 ScrollRect 드래그 입력을 복구한다
    private void RestoreScrollRectAfterPinch()
    {
        if (scrollRect == null || !didDisableScrollRectForPinch)
        {
            return;
        }

        scrollRect.enabled = true;
        scrollRect.scrollSensitivity = 0.0f;
        didDisableScrollRectForPinch = false;
    }

    // 런타임에 필요한 인스펙터 참조가 모두 연결됐는지 확인한다
    private bool ValidateRequiredReferences()
    {
        bool isValid = true;
        isValid &= LogMissingReference(scrollRect, nameof(scrollRect));
        isValid &= LogMissingReference(viewport, nameof(viewport));
        isValid &= LogMissingReference(content, nameof(content));

        if (maxZoom < minZoom)
        {
            Debug.LogWarning("[터렛 트리 줌 UI] Max Zoom이 Min Zoom보다 작아 값을 교정합니다.", this);
            maxZoom = minZoom;
        }

        return isValid;
    }

    // 단일 인스펙터 참조 누락 여부를 로그로 알린다
    private bool LogMissingReference(Object reference, string fieldName)
    {
        if (reference != null)
        {
            return true;
        }

        Debug.LogWarning("[터렛 트리 줌 UI] " + fieldName + " 참조가 비어 있습니다. 인스펙터에서 직접 연결해야 합니다.", this);
        return false;
    }
}
