using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class TurretPlacementSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private TMP_Text tmpNameText;
    [SerializeField] private Text costText;
    [SerializeField] private TMP_Text tmpCostText;

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

        TMP_Text[] tmpTexts = GetComponentsInChildren<TMP_Text>(true);
        if (tmpTexts.Length > 0)
        {
            tmpNameText = tmpTexts[0];
        }

        if (tmpTexts.Length > 1)
        {
            tmpCostText = tmpTexts[1];
        }
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
            if (shopEntry.Icon != null)
            {
                iconImage.sprite = shopEntry.Icon;
            }

            iconImage.enabled = iconImage.sprite != null;
        }

        if (nameText != null)
        {
            nameText.text = shopEntry.DisplayName;
        }

        if (tmpNameText != null)
        {
            tmpNameText.text = shopEntry.DisplayName;
        }

        string costLabel = shopEntry.Cost <= 0 ? string.Empty : shopEntry.Cost.ToString();
        if (costText != null)
        {
            costText.text = costLabel;
        }

        if (tmpCostText != null)
        {
            tmpCostText.text = costLabel;
        }
    }
}
