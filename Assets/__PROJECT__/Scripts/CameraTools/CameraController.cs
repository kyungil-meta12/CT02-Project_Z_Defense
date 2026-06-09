using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    public float minOrthographicSize;
    public float maxOrthographicSize;

    [Header("줌 감도")] public float zoomSensitivity;
    [Header("드래그 감도")] public float dragSensitivity;

    [Header("카메라 이동 범위 제한(+-)")] public float distanceX;
    [Header("카메라 이동 범위 제한(+-)")] public float distanceZ;

    private Camera cam;
    private float originSize;
    private float currSize;
    private float currSizeDest;
    private Vector3 currPos;
    private Vector3 currPosDest;
    private Vector3 originPos;

    #if UNITY_EDITOR
    private Vector3 lastMousePosition;
    private bool isDragging = false; // 에디터용 드래그 상태 추적
    #else
    private int activeDragFingerId = -1; // 모바일용 드래그 손가락 ID 추적
    #endif

    void Start()
    {
        cam = Camera.main;
        originPos = cam.transform.position;
        originSize = cam.orthographicSize;
        currSize = originSize;
        currSizeDest = originSize;
        currPos = cam.transform.position;
        currPosDest = cam.transform.position;
    }

    void Update()
    {
        UpdateDrag();
        UpdateZoom();
    }

    void UpdateDrag()
    {
        Vector2 dragDelta = Vector2.zero;

    #if UNITY_EDITOR
        // 1. 마우스를 처음 누른 순간
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                isDragging = false;
            }
            else
            {
                isDragging = true;
                lastMousePosition = Input.mousePosition;
            }
        }
        // 2. 마우스를 누르고 있는 동안
        else if (Input.GetMouseButton(0) && isDragging)
        {
            Vector3 currentMousePosition = Input.mousePosition;
            dragDelta = currentMousePosition - lastMousePosition;
            lastMousePosition = currentMousePosition;
        }
        
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    #else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    activeDragFingerId = -1;
                }
                else
                {
                    activeDragFingerId = touch.fingerId;
                }
            }

            if (touch.fingerId == activeDragFingerId && touch.phase == TouchPhase.Moved)
            {
                dragDelta = touch.deltaPosition;
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (touch.fingerId == activeDragFingerId) activeDragFingerId = -1;
            }
        }
        else
        {
            activeDragFingerId = -1;
        }
    #endif

        // 카메라 위치 조정
        var zoomVal = currSizeDest / originSize;
        var currSensitivity = dragSensitivity * zoomVal;
        currPosDest.z -= dragDelta.x * currSensitivity;
        currPosDest.x += dragDelta.y * currSensitivity;
        currPosDest.x = Mathf.Clamp(currPosDest.x, originPos.x - distanceX / zoomVal, originPos.x + distanceX / zoomVal);
        currPosDest.z = Mathf.Clamp(currPosDest.z, originPos.z - distanceZ / zoomVal, originPos.z + distanceZ / zoomVal);
        currPos = Vector3.Lerp(currPos, currPosDest, Time.deltaTime * 10f);
        cam.transform.position = currPos;
    }

    void UpdateZoom()
    {
        float zoomDelta = 0f;

    #if UNITY_EDITOR
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            zoomDelta = scroll;
        }
    #else
        if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            bool zeroStartedOnUI = touchZero.phase == TouchPhase.Began && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchZero.fingerId);
            bool oneStartedOnUI = touchOne.phase == TouchPhase.Began && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchOne.fingerId);

            if (!zeroStartedOnUI && !oneStartedOnUI)
            {
                Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

                float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

                zoomDelta = touchDeltaMag - prevTouchDeltaMag;
            }
        }
    #endif

        // 카메라 줌 반영
        currSizeDest -= zoomDelta * zoomSensitivity;
        currSizeDest = Mathf.Clamp(currSizeDest, minOrthographicSize, maxOrthographicSize);
        currSize = Mathf.Lerp(currSize, currSizeDest, Time.deltaTime * 10f);
        cam.orthographicSize = currSize;
    }
}