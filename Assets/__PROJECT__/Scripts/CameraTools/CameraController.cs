using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// UI 이벤트를 받기 위해 IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler 인터페이스를 추가합니다.
public class CameraController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler
{
    public static CameraController Inst;

    [Header("터치 컨트롤 모드 사용")] public bool UsingTouchControl;
    [Space(5f)]

    [Header("줌 감도")] public float zoomSensitivity;
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

    // UI 터치/드래그 컨트롤용 변수
    private Vector2 dragDelta = Vector2.zero; // 현재 프레임의 드래그 이동량
    private List<int> activePointers = new List<int>(); // 현재 패널을 누르고 있는 포인터(손가락/마우스) ID 목록

    void Awake()
    {
        if (Inst && Inst != this)
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
        UpdateMove();

        // 업데이트 후에 최종 적용
        cam.transform.position = currPos + shakeOffset;
        cam.orthographicSize = currSize;

        // 처리가 끝난 후 이번 프레임의 드래그 델타 초기화
        dragDelta = Vector2.zero;
    }

    // 패널을 터치/클릭하기 시작했을 때
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!activePointers.Contains(eventData.pointerId))
        {
            activePointers.Add(eventData.pointerId);
        }
    }

    // 패널에서 손을 떼거나 마우스 클릭을 해제했을 때
    public void OnPointerUp(PointerEventData eventData)
    {
        if (activePointers.Contains(eventData.pointerId))
        {
            activePointers.Remove(eventData.pointerId);
        }
    }

    // 패널 위에서 드래그 중일 때
    public void OnDrag(PointerEventData eventData)
    {
        // 손가락이 하나일 때만 (또는 마우스일 때만) 드래그 이동 처리
        if (activePointers.Count <= 1)
        {
            dragDelta += eventData.delta;
        }
    }

    // PC 환경에서 패널 위에 마우스를 올리고 휠을 굴렸을 때
    public void OnScroll(PointerEventData eventData)
    {
        // 스크롤 방향에 따라 줌 적용 (기존 마우스 휠 로직 대체)
        currSizeDest -= eventData.scrollDelta.y * zoomSensitivity;
    }

    void UpdateShake()
    {
        shakeTime += Time.deltaTime;

        if (shakeTime >= shakeTimeDest)
        {
            shakeTime -= shakeTimeDest;

            float randomX = 0f;
            float randomZ = 0f;

            if (shakeForce > 0.0001f)
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
        if (dragDelta == Vector2.zero)
            return;

        float pixelToWorldScale = (currSizeDest * 2f) / Screen.height;

        float worldDeltaX = dragDelta.x * pixelToWorldScale;
        float worldDeltaY = dragDelta.y * pixelToWorldScale;

        float pitchAngle = cam.transform.eulerAngles.x;
        float pitchRad = pitchAngle * Mathf.Deg2Rad;
        float sinPitch = Mathf.Abs(Mathf.Sin(pitchRad));

        if (sinPitch > 0.001f)
        {
            worldDeltaY /= sinPitch;
        }

        currPosDest.x += worldDeltaY;
        currPosDest.z -= worldDeltaX;

        float camHalfHeight = currSizeDest;
        float camHalfWidth = currSizeDest * cam.aspect;
        float adjustedHalfHeight = camHalfHeight;

        if (sinPitch > 0.001f)
        {
            adjustedHalfHeight = camHalfHeight / sinPitch;
        }

        var minX = originPos.x - maxForwardOffset;
        var maxX = originPos.x + maxBackwardOffset;
        var minZ = originPos.z - maxLeftOffset;
        var maxZ = originPos.z + maxRightOffset;

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

    }

    void UpdateMove()
    {
        currPos = Vector3.Lerp(currPos, currPosDest, Time.deltaTime * 10f);
    }

    void UpdateZoom()
    {
        float zoomDelta = 0f;

        // 모바일 멀티 터치 (핀치 줌) 로직
       if (activePointers.Count >= 2)
        {
            Touch touchZero;
            Touch touchOne;

            if (TryGetTouch(activePointers[0], out touchZero) && TryGetTouch(activePointers[1], out touchOne))
            {
                Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;
                float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;
                zoomDelta = (touchDeltaMag - prevTouchDeltaMag) / Screen.height * 1000f;
            }
        }

        // 카메라 줌 반영 (OnScroll 이벤트에서 처리된 데스크톱 마우스 휠 결과와 함께 갱신)
        currSizeDest -= zoomDelta * zoomSensitivity;
        currSizeDest = Mathf.Clamp(currSizeDest, minZoom, maxZoom);
        currSize = Mathf.Lerp(currSize, currSizeDest, Time.deltaTime * 10f);
    }

    // FingerID를 기반으로 현재 터치 상태를 가져오는 헬퍼 메서드
    private bool TryGetTouch(int fingerId, out Touch result)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.fingerId == fingerId)
            {
                result = t;
                return true;
            }
        }
        result = new Touch();
        return false;
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