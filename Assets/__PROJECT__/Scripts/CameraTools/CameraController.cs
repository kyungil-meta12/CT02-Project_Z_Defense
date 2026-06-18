using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler
{
    public static CameraController Inst;

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

    // 포인터 ID와 해당 포인터의 '현재/이전 위치'를 저장하기 위한 딕셔너리
    private Dictionary<int, PointerEventData> activePointers = new Dictionary<int, PointerEventData>();
    private float lastTouchDistance = 0f;

    // UI 터치/드래그 컨트롤용 변수
    private Vector2 dragDelta = Vector2.zero; // 현재 프레임의 드래그 이동량

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

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!activePointers.ContainsKey(eventData.pointerId))
        {
            activePointers.Add(eventData.pointerId, eventData);
        }

        // 손가락이 2개가 되는 순간 초기 거리 계산
        if (activePointers.Count == 2)
        {
            var keys = new List<int>(activePointers.Keys);
            Vector2 pos0 = activePointers[keys[0]].position;
            Vector2 pos1 = activePointers[keys[1]].position;
            lastTouchDistance = Vector2.Distance(pos0, pos1);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (activePointers.ContainsKey(eventData.pointerId))
        {
            activePointers.Remove(eventData.pointerId);
        }

        // 손가락을 떼면 거리 초기화
        if (activePointers.Count < 2)
        {
            lastTouchDistance = 0f;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 실시간 위치 갱신
        if (activePointers.ContainsKey(eventData.pointerId))
        {
            activePointers[eventData.pointerId] = eventData;
        }

        // 1개 이하일 때만 드래그 이동 처리 (멀티 터치 줌 할 때는 화면이 이동하지 않도록 방지)
        if (activePointers.Count <= 1)
        {
            dragDelta += eventData.delta;
        }
        else if (activePointers.Count == 2)
        {
            // 두 손가락 드래그 중일 때 실시간으로 핀치 줌 계산
            var keys = new List<int>(activePointers.Keys);
            Vector2 pos0 = activePointers[keys[0]].position;
            Vector2 pos1 = activePointers[keys[1]].position;

            float currentDistance = Vector2.Distance(pos0, pos1);

            if (lastTouchDistance > 0f)
            {
                // 이전 거리와 현재 거리의 차이를 구함
                float deltaDist = currentDistance - lastTouchDistance;

                // 스크린 높이 비율로 환산하여 줌 타겟 변경
                float zoomDelta = (deltaDist / Screen.height) * 1000f;
                currSizeDest -= zoomDelta * zoomSensitivity;
            }

            lastTouchDistance = currentDistance;
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
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