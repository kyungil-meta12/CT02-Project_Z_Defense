using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// IPointerDownHandler 인터페이스가 반드시 함께 추가되어야 합니다!
public class PassEventToScrollRect : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ScrollRect scrollRect;

    void Start()
    {
        var backPannel = transform.root.Find("InventoryUI/MainController/InventoryScrollRect");
        scrollRect = backPannel.GetComponent<ScrollRect>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        scrollRect.OnInitializePotentialDrag(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        scrollRect.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        scrollRect.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        scrollRect.OnEndDrag(eventData);
    }
}