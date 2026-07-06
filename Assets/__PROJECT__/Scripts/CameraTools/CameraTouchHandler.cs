using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 전체 화면 터치 입력을 받아 카메라 조작과 월드 오브젝트 터치 이벤트를 중계한다.
/// </summary>
public class CameraTouchHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public static CameraTouchHandler Inst;

    [Header("화면 터치 시 레이캐스팅 할 대상 레이어 목록")] 
    public LayerMask raycastTargetLayer;

    [Header("더블탭 간격")] public float tapInterval;
    [Header("진단 로그")] [SerializeField] private bool logTouchHitDiagnostics = false;

    // 터치 이벤트 감지 시 발생시키는 이벤트
    // 대상을 터치했을 때
    public Action<RaycastHit> OnCameraTargetTouchEvent;

    // 대상이 아닌 것을 선택했을 때
    public Action OnCameraOtherTouchEvent;

    /// <summary>
    /// 더블탭 이벤트
    /// </summary>
    public Action OnDoubleTapEvent;
    private float tapTime = 0f;
    private int tapCount = 0;

    // 현재 드래그 상태. true일 시 터치 이벤트가 발생하지 않는다.
    private bool isDragging = false;

    // 하나만 선택하면 되므로 하나의 인덱스만 생성
    private readonly RaycastHit[] hitResult = new RaycastHit[1];
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>(16);

    // 메인 카메라
    private Camera cam;
    

    // 싱글톤 참조와 메인 카메라를 초기화한다
    void Awake()
    {
        if(Inst && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;
    }

    void Start()
    {
        cam = Camera.main;
    }

    // 파괴 시 싱글톤 참조를 정리한다
    void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }
    }

    // 더블탭 입력 상태를 갱신한다
    void Update()
    {
        UpdateDoubleTap();
    }

    // 터치 시작
    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = false;
    }

    // 드래그 시작
    public void OnDrag(PointerEventData eventData)
    {
        isDragging = true;
    }

    // 드래그가 감지된 이후에 터치를 때면 이벤트가 발생하지 않음
    // 드래그를 하지 않고 떼어야 터치를 한 것으로 인식하고 이벤트를 발생시킴
    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDragging)
        {
            return;
        }

        if (float.IsInfinity(eventData.position.x) || float.IsInfinity(eventData.position.y) ||
            float.IsNaN(eventData.position.x) || float.IsNaN(eventData.position.y))
        {
            return;
        }

        Ray ray = cam.ScreenPointToRay(eventData.position);
        int hitCount = Physics.RaycastNonAlloc(ray, hitResult, Mathf.Infinity, raycastTargetLayer);
        if (hitCount > 0)
        {
            RaycastHit hit = hitResult[0];
            LogTouchDiagnostic($"월드 레이캐스트 히트 - 오브젝트: {hit.collider.gameObject.name}, 레이어: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
            OnCameraTargetTouchEvent?.Invoke(hit);
        }
        else
        {
            LogTouchDiagnostic("월드 레이캐스트 히트 없음");
            OnCameraOtherTouchEvent?.Invoke();
            tapTime = tapInterval;
            tapCount++;
        }
    }

    // 더블탭 시 더블탭 이벤트 발생과 카메라 리셋
    void UpdateDoubleTap()
    {
        tapTime -= Time.deltaTime;
        tapTime = Mathf.Clamp(tapTime, 0f, tapInterval);
        if (tapTime > 0f)
        {
            if (tapCount == 2)
            {
                OnDoubleTapEvent?.Invoke();
                CameraController.Inst.Reset();
                tapCount = 0;
            }
        }
        else
        {
            tapCount = 0;
        }
    }

    // 터치 패드 자신이 아닌 다른 UI가 포인터를 받고 있는지 확인한다
    private bool IsBlockedByOtherUI(PointerEventData eventData)
    {
        if (EventSystem.current == null || eventData == null)
        {
            LogTouchDiagnostic("EventSystem 또는 PointerEventData가 없어 UI 차단 검사를 건너뜁니다.");
            return false;
        }

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, uiRaycastResults);
        LogUIRaycastResults();

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObject = uiRaycastResults[i].gameObject;
            if (hitObject == null || hitObject == gameObject)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // UI 레이캐스트 결과를 진단 로그로 출력한다
    private void LogUIRaycastResults()
    {
        if (!logTouchHitDiagnostics)
        {
            return;
        }

        if (uiRaycastResults.Count == 0)
        {
            Debug.Log("[CameraTouchHandler] UI 레이캐스트 히트 없음", this);
            return;
        }

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            RaycastResult result = uiRaycastResults[i];
            GameObject hitObject = result.gameObject;
            string objectName = hitObject == null ? "null" : hitObject.name;
            string layerName = hitObject == null ? "Unknown" : LayerMask.LayerToName(hitObject.layer);
            bool isSelf = hitObject == gameObject;
            Debug.Log($"[CameraTouchHandler] UI 히트 {i} - 오브젝트: {objectName}, 레이어: {layerName}, 자기 터치패드: {isSelf}", this);
        }
    }

    // 터치 처리 흐름의 진단 로그를 출력한다
    private void LogTouchDiagnostic(string message)
    {
        if (!logTouchHitDiagnostics)
        {
            return;
        }

        Debug.Log($"[CameraTouchHandler] {message}", this);
    }
}
