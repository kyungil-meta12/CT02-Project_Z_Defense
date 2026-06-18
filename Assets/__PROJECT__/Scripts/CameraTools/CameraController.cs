#define USING_TOUCH_CONTROL // 이 전처리기를 주석처리하면 유니티 에디터용 코드로 전환됨

using UnityEngine;
using UnityEngine.EventSystems;


public class CameraController : MonoBehaviour
{

    public static CameraController Inst;

    [Header("줌 감도")] public float zoomSensitivity;
    [Header("드래그 감도")] public float dragSensitivity;
    [Header("최대 줌")] public float maxZoom;
    [Header("최소 줌")] public float minZoom;
    [Header("최대 앞 오프셋")] public float maxForwardOffset;
    [Header("최대 뒤 오프셋")] public float maxBackwardOffset;
    [Header("최대 우측 오프셋")] public float maxRightOffset;
    [Header("최대 좌측 오프셋")] public float maxLeftOffset;

    private Camera cam;
    private float originSize;
    private float currSize;
    private float currSizeDest;
    private Vector3 currPos;
    private Vector3 currPosDest;
    private Vector3 originPos;

    private float shakeForce; // 현재 흔들림 힘
    private float shakeTime; // 흔들림 간격 시간
    private float shakeTimeDest = 0.016f; // 목표 흔들림 간격 시간
    private Vector3 shakeOffset = new(); // 흔들림 오프셋

#if USING_TOUCH_CONTROL // 이 전처리기를 주석처리하면 유니티 에디터용 코드로 전환됨
    private int activeDragFingerId = -1; // 모바일용 드래그 손가락 ID 추적
#else
    private Vector3 lastMousePosition;
    private bool isDragging = false; // 에디터용 드래그 상태 추적
#endif


    void Awake()
    {
        if(Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
        }
        Inst = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        Inst = null;
    }

    void Start()
    {
        cam = Camera.main;
        originPos = cam.transform.position;
        originSize = cam.orthographicSize;
        currSize = originSize;
        currSizeDest = originSize;
        currPos = originPos;
        currPosDest = originPos;
    }

    void Update()
    {
        UpdateShake();
        UpdateDrag();
        UpdateZoom();

        // 업데이트 후에 최종 적용
        cam.transform.position = currPos + shakeOffset;
        cam.orthographicSize = currSize;
    }

    void UpdateShake()
    {
        shakeTime += Time.deltaTime;

        if(shakeTime >= shakeTimeDest)
        {
            shakeTime -= shakeTimeDest;

            float randomX = 0f;
            float randomZ = 0f;

            if(shakeForce > 0.0001f)
            {
                randomX = Random.Range(-shakeForce, shakeForce);
                randomZ = Random.Range(-shakeForce, shakeForce);
            }
           
            shakeOffset.x = randomX;
            shakeOffset.z = randomZ;
        }

        shakeForce = Mathf.Lerp(shakeForce, 0f, Time.deltaTime * 5f);
    }

    void UpdateDrag()
    {
        Vector2 dragDelta = Vector2.zero;

#if USING_TOUCH_CONTROL // 이 전처리기를 주석처리하면 유니티 에디터용 코드로 전환됨
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
                if (touch.fingerId == activeDragFingerId)
                    activeDragFingerId = -1;
            }
        }
        else
        {
            activeDragFingerId = -1;
        }
#else
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
#endif

        // 카메라 위치 조정
        float camHalfHeight = currSizeDest; 
        float camHalfWidth = currSizeDest * cam.aspect;

        float pitchAngle = cam.transform.eulerAngles.x;
        float pitchRad = pitchAngle * Mathf.Deg2Rad;

        // 1. Sin 값의 절댓값을 구합니다.
        float sinPitch = Mathf.Abs(Mathf.Sin(pitchRad));
        float adjustedHalfHeight = camHalfHeight;

        // 2. Sin 값이 0에 가까워 무한대가 되는 것을 미리 방지합니다.
        if (sinPitch > 0.001f) 
        {
            adjustedHalfHeight = camHalfHeight / sinPitch;
        }

        var minX = originPos.x - maxForwardOffset;
        var maxX = originPos.x + maxBackwardOffset;
        var minZ = originPos.z - maxLeftOffset;
        var maxZ = originPos.z + maxRightOffset;

        var zoomVal = currSizeDest / originSize;
        var currSensitivity = dragSensitivity * zoomVal;

        currPosDest.x += dragDelta.y * currSensitivity;
        currPosDest.z -= dragDelta.x * currSensitivity;
        currPosDest.x = Mathf.Clamp(currPosDest.x, minX + adjustedHalfHeight, maxX - adjustedHalfHeight);
        currPosDest.z = Mathf.Clamp(currPosDest.z, minZ + camHalfWidth, maxZ - camHalfWidth);
        
        if (minX + adjustedHalfHeight > maxX - adjustedHalfHeight) 
        {
            currPosDest.x = (minX + maxX) * 0.5f;
        }
        if (minZ + camHalfWidth > maxZ - camHalfWidth) 
        {
            currPosDest.z = (minZ + maxZ) * 0.5f;
        }

        currPos = Vector3.Lerp(currPos, currPosDest, Time.deltaTime * 10f);
    }

    void UpdateZoom()
    {
        float zoomDelta = 0f;

#if USING_TOUCH_CONTROL // 이 전처리기를 주석처리하면 유니티 에디터용 코드로 전환됨
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
#else
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            zoomDelta = scroll;
        }
#endif

        // 카메라 줌 반영
        currSizeDest -= zoomDelta * zoomSensitivity;
        currSizeDest = Mathf.Clamp(currSizeDest, minZoom, maxZoom);
        currSize = Mathf.Lerp(currSize, currSizeDest, Time.deltaTime * 10f);
    }

    /// <summary>
    /// 카메라에 흔들림을 추가한다.
    /// </summary>
    /// <param name="shakeStrength"></param>
    public void AddShake(float shakeStrength)
    {
        shakeForce += shakeStrength;
    }
}