using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float minOrthographicSize;
    public float maxOrthographicSize;

    [Header("줌 감도")] public float zoomSensitivity;
    [Header("드래그 감도")] public float dragSensitivity;

    #if UNITY_EDITOR
    private Vector3 lastMousePosition;
    #endif

    private Camera cam;
    private float originSize;
    private float currSize;
    private float currSizeDest;

    void Start()
    {
        cam = Camera.main;
        originSize = cam.orthographicSize;
        currSize = originSize;
        currSizeDest = originSize;
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
        if (Input.GetMouseButtonDown(0))
        {
            lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0))
        {
            Vector3 currentMousePosition = Input.mousePosition;
            dragDelta = currentMousePosition - lastMousePosition;
            lastMousePosition = currentMousePosition;
        }
    #else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // 손가락이 화면 위에서 움직이고 있을 때만 델타값 추출
            if (touch.phase == TouchPhase.Moved)
            {
                dragDelta = touch.deltaPosition;
            }
        }
    #endif

        // 드래그로 카메라 위치 조정
        var camPos = cam.transform.position;
        // 드래그 감도를 줌에 따라 가변 조정
        var currSensitivity = dragSensitivity * (currSizeDest / originSize);
        camPos.z -= dragDelta.x * currSensitivity;
        camPos.x += dragDelta.y * currSensitivity;
        cam.transform.position = camPos;
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
        // 모바일 기기에서는 실제 두 손가락 터치 감지
        if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            // 두 손가락 사이의 거리 변화량 계산
            zoomDelta = touchDeltaMag - prevTouchDeltaMag;
        }
    #endif

        // 줌인 줌 아웃으로 카메라 줌 조정
        currSizeDest -= zoomDelta * zoomSensitivity;
        currSizeDest = Mathf.Clamp(currSizeDest, minOrthographicSize, maxOrthographicSize);
        currSize = Mathf.Lerp(currSize, currSizeDest, Time.deltaTime * 5f);
        cam.orthographicSize = currSize;
    }
}
