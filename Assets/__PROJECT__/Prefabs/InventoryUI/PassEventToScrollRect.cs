using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// IPointerDownHandler 인터페이스가 반드시 함께 추가되어야 합니다!
public class PassEventToScrollRect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ScrollRect scrollRect;
    private bool isDragging = false;
    public UnityEvent onButtonClick;

    [Header("ScrollRect를 참조할 트랜스폼 경로")] 
    [TextArea(3, 5)]
    public string scrollRectTransformpath;

    void Start()
    {
        var backPannel = transform.root.Find(scrollRectTransformpath);
        scrollRect = backPannel.GetComponent<ScrollRect>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = false;
        if (scrollRect)
        {
            scrollRect.OnInitializePotentialDrag(eventData);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        if (scrollRect)
        {
            scrollRect.OnBeginDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (scrollRect)
        {
            scrollRect.OnDrag(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (scrollRect)
        {
            scrollRect.OnEndDrag(eventData);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging && RectTransformUtility.RectangleContainsScreenPoint(GetComponent<RectTransform>(), eventData.position, eventData.pressEventCamera))
        {
            onButtonClick?.Invoke();
        }
    }
}