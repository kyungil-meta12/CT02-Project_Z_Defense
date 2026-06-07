using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TurretPlacementSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Text costText;

    private TurretPlacementController placementController;
    private TurretShopEntrySO shopEntry;

    public void Initialize(TurretShopEntrySO shopEntry_, TurretPlacementController placementController_)
    {
        shopEntry = shopEntry_;
        placementController = placementController_;
        Refresh();
    }

    private void Reset()
    {
        iconImage = GetComponentInChildren<Image>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (placementController == null || shopEntry == null)
        {
            return;
        }

        placementController.BeginPlacement(shopEntry, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (placementController == null)
        {
            return;
        }

        placementController.UpdatePlacement(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (placementController == null)
        {
            return;
        }

        placementController.EndPlacement(eventData.position);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (placementController == null || shopEntry == null)
        {
            return;
        }

        if (placementController.IsPlacing)
        {
            placementController.CancelPlacement();
            return;
        }

        placementController.BeginPlacement(shopEntry, eventData.position);
    }

    private void Refresh()
    {
        if (shopEntry == null)
        {
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = shopEntry.Icon;
            iconImage.enabled = shopEntry.Icon != null;
        }

        if (nameText != null)
        {
            nameText.text = shopEntry.DisplayName;
        }

        if (costText != null)
        {
            costText.text = shopEntry.Cost <= 0 ? string.Empty : shopEntry.Cost.ToString();
        }
    }
}
