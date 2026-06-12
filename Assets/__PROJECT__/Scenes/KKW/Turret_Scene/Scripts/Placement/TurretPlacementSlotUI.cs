using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 터렛 배치 슬롯 UI의 입력과 설치 횟수별 비용 표시를 관리한다.
/// </summary>
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

    // 상점 엔트리와 배치 컨트롤러를 연결하고 표시를 갱신한다
    public void Initialize(TurretShopEntrySO shopEntry_, TurretPlacementController placementController_)
    {
        UnsubscribePlacementController();
        shopEntry = shopEntry_;
        placementController = placementController_;
        SubscribePlacementController();
        Refresh();
    }

    // 활성화 시 배치 컨트롤러 이벤트를 다시 구독한다
    private void OnEnable()
    {
        SubscribePlacementController();
    }

    // 비활성화 시 배치 컨트롤러 이벤트 구독을 해제한다
    private void OnDisable()
    {
        UnsubscribePlacementController();
    }

    // 컴포넌트 추가 시 기본 UI 참조를 자동으로 연결한다
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

    // 드래그 시작 시 터렛 배치 프리뷰를 시작한다
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (placementController == null || shopEntry == null)
        {
            return;
        }

        placementController.BeginPlacement(shopEntry, eventData.position);
    }

    // 드래그 중 포인터 위치에 맞춰 배치 프리뷰를 갱신한다
    public void OnDrag(PointerEventData eventData)
    {
        if (placementController == null)
        {
            return;
        }

        placementController.UpdatePlacement(eventData.position);
    }

    // 드래그 종료 시 현재 포인터 위치에 터렛 배치를 시도한다
    public void OnEndDrag(PointerEventData eventData)
    {
        if (placementController == null)
        {
            return;
        }

        placementController.EndPlacement(eventData.position);
    }

    // 슬롯 클릭 시 배치 모드를 시작하거나 기존 배치 모드를 취소한다
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

    // 현재 상점 엔트리의 이름, 아이콘, 비용 텍스트를 UI에 반영한다
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

        string costLabel = FormatCosts(GetCurrentPlacementCosts());
        if (costText != null)
        {
            costText.text = costLabel;
        }

        if (tmpCostText != null)
        {
            tmpCostText.text = costLabel;
        }
    }

    // 현재 설치 횟수 기준 배치 비용을 조회한다
    private ResourceCost[] GetCurrentPlacementCosts()
    {
        if (placementController == null)
        {
            return shopEntry.PlacementCosts;
        }

        return placementController.GetCurrentPlacementCosts(shopEntry);
    }

    // 배치 횟수 변경 이벤트를 구독한다
    private void SubscribePlacementController()
    {
        if (placementController == null)
        {
            return;
        }

        placementController.OnPlacementCountChanged -= OnPlacementCountChanged;
        placementController.OnPlacementCountChanged += OnPlacementCountChanged;
    }

    // 배치 횟수 변경 이벤트 구독을 해제한다
    private void UnsubscribePlacementController()
    {
        if (placementController == null)
        {
            return;
        }

        placementController.OnPlacementCountChanged -= OnPlacementCountChanged;
    }

    // 같은 배치 엔트리의 설치 횟수가 바뀌면 비용 표시를 갱신한다
    private void OnPlacementCountChanged(TurretShopEntrySO changedShopEntry)
    {
        if (changedShopEntry != shopEntry)
        {
            return;
        }

        Refresh();
    }

    // ResourceCost 배열을 상점 슬롯에 표시할 짧은 문자열로 변환한다
    private static string FormatCosts(ResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(32);
        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(" / ");
            }

            builder.Append(GetCurrencyLabel(cost.currencyType));
            builder.Append(' ');
            builder.Append(cost.amount);
        }

        return builder.ToString();
    }

    // 재화 타입을 UI에 표시할 짧은 라벨로 변환한다
    private static string GetCurrencyLabel(RewardCurrencyType currencyType)
    {
        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                return "Coin";
            case RewardCurrencyType.FirePart:
                return "Fire";
            case RewardCurrencyType.SpecialPart:
                return "Special";
            default:
                return currencyType.ToString();
        }
    }
}
