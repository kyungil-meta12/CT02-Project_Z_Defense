using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraTouchHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public static CameraTouchHandler Inst;

    [Header("화면 터치 시 레이캐스팅 할 대상 레이어 목록")] 
    public LayerMask[] raycastTargetLayers;

    [Header("더블탭 간격")] public float doubleTapInterval;

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

    // 모든 타겟 레이어를 합친 최종 레이어 비트 마스크
    private int mixedBitMask = 0;

    // 하나만 선택하면 되므로 하나의 인덱스만 생성
    private RaycastHit[] hitResult = new RaycastHit[1];

    // 메인 카메라
    private Camera cam;
    

    void Awake()
    {
        if(Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
            return;
        }

        Inst = this;

        // 레이어 목록의 모든 레이어들을 하나로 합쳐 레이캐스팅 시 사용
        if (raycastTargetLayers != null)
        {
            foreach (LayerMask mask in raycastTargetLayers)
            {
                mixedBitMask |= mask.value;
            }
        }

        cam = Camera.main;
    }

    void OnDestroy()
    {
        Inst = null;
    }

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

        Ray ray = cam.ScreenPointToRay(eventData.position);
        int hitCount = Physics.RaycastNonAlloc(ray, hitResult, Mathf.Infinity, mixedBitMask);
        if (hitCount > 0)
        {
            RaycastHit hit = hitResult[0];
            OnCameraTargetTouchEvent?.Invoke(hit);
        }
        else
        {
            OnCameraOtherTouchEvent?.Invoke();
            tapTime = doubleTapInterval;
            tapCount++;
        }
    }

    // 더블탭 시 더블탭 이벤트 발생과 카메라 리셋
    void UpdateDoubleTap()
    {
        tapTime -= Time.deltaTime;
        tapTime = Mathf.Clamp(tapTime, 0f, doubleTapInterval);
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
}
