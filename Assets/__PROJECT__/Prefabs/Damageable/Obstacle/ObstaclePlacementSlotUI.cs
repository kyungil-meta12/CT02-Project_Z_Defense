using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 장애물/게이트 배치 버튼의 표시 정보와 클릭/드래그 배치 입력을 처리한다.
/// </summary>
[DisallowMultipleComponent]
public class ObstaclePlacementSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private TMP_Text tmpNameText;
    [SerializeField] private Text costText;
    [SerializeField] private TMP_Text tmpCostText;

    [Header("Manual Placement")]
    [SerializeField] private ObstaclePlacementController placementController;
    [SerializeField] private ObstacleBuildEntrySO buildEntry;

    // 자동 생성된 버튼에 빌드 항목과 컨트롤러를 주입한다
    public void Initialize(ObstacleBuildEntrySO buildEntry_, ObstaclePlacementController placementController_)
    {
        buildEntry = buildEntry_;
        placementController = placementController_;
        Refresh();
    }

    // 인스펙터에서 기본 UI 참조와 컨트롤러를 자동으로 찾는다
    private void Reset()
    {
        iconImage = GetComponentInChildren<Image>();
        placementController = FindFirstObjectByType<ObstaclePlacementController>();

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

    // 버튼 활성화 전에 수동 참조와 표시 정보를 초기화한다
    private void Awake()
    {
        ResolveController();
        Refresh();
    }

    // 인스펙터 값이 바뀌면 버튼 표시를 갱신한다
    private void OnValidate()
    {
        Refresh();
    }

    // 드래그가 시작될 때 해당 빌드 항목의 배치를 시작한다
    public void OnBeginDrag(PointerEventData eventData)
    {
        ResolveController();

        if (placementController == null || buildEntry == null)
        {
            return;
        }

        placementController.BeginPlacement(buildEntry, eventData.position);
    }

    // 드래그 중 포인터 위치로 배치 프리뷰를 갱신한다
    public void OnDrag(PointerEventData eventData)
    {
        if (placementController == null)
        {
            return;
        }

        placementController.UpdatePlacement(eventData.position);
    }

    // 드래그가 끝날 때 현재 위치에 배치를 확정한다
    public void OnEndDrag(PointerEventData eventData)
    {
        if (placementController == null)
        {
            return;
        }

        placementController.EndPlacement(eventData.position);
    }

    // 버튼 클릭으로 배치를 시작하거나 진행 중인 배치를 취소한다
    public void OnPointerClick(PointerEventData eventData)
    {
        ResolveController();

        if (placementController == null || buildEntry == null)
        {
            return;
        }

        if (placementController.IsPlacing)
        {
            placementController.CancelPlacement();
            return;
        }

        placementController.BeginPlacement(buildEntry, eventData.position);
    }

    // 수동 배치 버튼에 컨트롤러가 비어 있으면 씬에서 한 번 찾아 연결한다
    private void ResolveController()
    {
        if (placementController != null)
        {
            return;
        }

        placementController = FindFirstObjectByType<ObstaclePlacementController>();
    }

    // 빌드 항목 정보를 버튼의 아이콘과 텍스트에 반영한다
    private void Refresh()
    {
        if (buildEntry == null)
        {
            return;
        }

        if (iconImage != null)
        {
            if (buildEntry.Icon != null)
            {
                iconImage.sprite = buildEntry.Icon;
            }

            iconImage.enabled = iconImage.sprite != null;
        }

        if (nameText != null)
        {
            nameText.text = buildEntry.DisplayName;
        }

        if (tmpNameText != null)
        {
            tmpNameText.text = buildEntry.DisplayName;
        }

        // UI 표시는 실제 소비에 쓰이는 BuildCosts를 그대로 사용해 표시 비용과 결제 비용이 갈라지지 않게 한다.
        string costLabel = FormatCosts(buildEntry.BuildCosts);
        if (costText != null)
        {
            costText.text = costLabel;
        }

        if (tmpCostText != null)
        {
            tmpCostText.text = costLabel;
        }
    }

    // ResourceCost 배열을 배치 버튼에 표시할 짧은 문자열로 변환한다
    private static string FormatCosts(ResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
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
            builder.Append(" ");
            builder.Append(cost.amount);
        }

        return builder.ToString();
    }

    // 재화 종류를 배치 UI 표시용 짧은 이름으로 변환한다
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
