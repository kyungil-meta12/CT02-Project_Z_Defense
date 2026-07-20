using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 장애물/게이트 배치 버튼의 표시 정보와 클릭/드래그 배치 입력을 처리한다.
/// </summary>
[DisallowMultipleComponent]
public class ObstaclePlacementSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("참조")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private TMP_Text tmpNameText;
    [SerializeField] private Text costText;
    [SerializeField] private TMP_Text tmpCostText;

    [Header("수동 배치")]
    [SerializeField] private ObstaclePlacementController placementController;
    [SerializeField] private ObstacleBuildEntrySO buildEntry;

    private GameManager subscribedGameManager;

    // 자동 생성된 버튼에 빌드 항목과 컨트롤러를 주입한다
    public void Initialize(ObstacleBuildEntrySO buildEntry_, ObstaclePlacementController placementController_)
    {
        UnsubscribePlacementController();
        buildEntry = buildEntry_;
        placementController = placementController_;
        SubscribePlacementController();
        Refresh();
    }

    // 활성화 시 배치 컨트롤러 이벤트를 다시 구독한다
    private void OnEnable()
    {
        SubscribePlacementController();
        SubscribeGameManager();
    }

    // 비활성화 시 배치 컨트롤러 이벤트 구독을 해제한다
    private void OnDisable()
    {
        UnsubscribePlacementController();
        UnsubscribeGameManager();
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

    // 시작 시 늦게 생성된 GameManager 구독을 보완하고 현재 비용을 다시 표시한다
    private void Start()
    {
        SubscribeGameManager();
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

    // 수동 배치 버튼에 컨트롤러가 비어 있으면 씬에서 한 번 찾아 연결한다
    private void ResolveController()
    {
        if (placementController != null)
        {
            return;
        }

        placementController = FindFirstObjectByType<ObstaclePlacementController>();
    }

    // 현재 설치 횟수 기준 배치 비용을 조회한다
    private ResourceCost[] GetCurrentPlacementCosts()
    {
        if (placementController == null)
        {
            return buildEntry.BuildCosts;
        }

        return placementController.GetCurrentPlacementCosts(buildEntry);
    }

    // 배치 성공 횟수 변경 이벤트를 구독한다
    private void SubscribePlacementController()
    {
        if (placementController == null)
        {
            return;
        }

        placementController.OnPlacementCountChanged -= OnPlacementCountChanged;
        placementController.OnPlacementCountChanged += OnPlacementCountChanged;
        placementController.OnPlacementCostPreviewChanged -= OnPlacementCostPreviewChanged;
        placementController.OnPlacementCostPreviewChanged += OnPlacementCostPreviewChanged;
    }

    // 배치 성공 횟수 변경 이벤트 구독을 해제한다
    private void UnsubscribePlacementController()
    {
        if (placementController == null)
        {
            return;
        }

        placementController.OnPlacementCountChanged -= OnPlacementCountChanged;
        placementController.OnPlacementCostPreviewChanged -= OnPlacementCostPreviewChanged;
    }

    // 저장 장애물 복원 완료 이벤트를 중복 없이 구독한다
    private void SubscribeGameManager()
    {
        GameManager gameManager = GameManager.Inst;
        if (gameManager == null || subscribedGameManager == gameManager)
        {
            return;
        }

        UnsubscribeGameManager();
        subscribedGameManager = gameManager;
        subscribedGameManager.OnObstaclePlacementsRestored -= OnObstaclePlacementsRestored;
        subscribedGameManager.OnObstaclePlacementsRestored += OnObstaclePlacementsRestored;
    }

    // 현재 구독 중인 저장 장애물 복원 이벤트를 해제한다
    private void UnsubscribeGameManager()
    {
        if (subscribedGameManager == null)
        {
            return;
        }

        subscribedGameManager.OnObstaclePlacementsRestored -= OnObstaclePlacementsRestored;
        subscribedGameManager = null;
    }

    // 저장 장애물이 복원되면 현재 슬롯 진행도 수 기준으로 일반 설치비용을 다시 표시한다
    private void OnObstaclePlacementsRestored()
    {
        Refresh();
    }

    // 같은 빌드 항목의 설치 횟수가 바뀌면 비용 표시를 갱신한다
    private void OnPlacementCountChanged(ObstacleBuildEntrySO changedBuildEntry)
    {
        if (changedBuildEntry != buildEntry)
        {
            return;
        }

        Refresh();
    }

    // 현재 버튼 엔트리의 드래그 비용과 재설치 할인율을 비용 텍스트에 반영한다
    private void OnPlacementCostPreviewChanged(ObstacleBuildEntrySO changedBuildEntry, ResourceCost[] costs, bool isRebuild, float discountRate)
    {
        if (changedBuildEntry != buildEntry)
        {
            return;
        }

        string costLabel = FormatCosts(costs);
        if (isRebuild)
        {
            costLabel = $"{costLabel} ({Mathf.RoundToInt(discountRate * 100.0f)}% 할인)";
        }

        SetCostLabel(costLabel);
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

        // 설치 횟수에 따라 오르는 실효 비용(GetPlacementCosts)을 표시해야 실제 결제 비용과 갈라지지 않는다.
        string costLabel = FormatCosts(GetCurrentPlacementCosts());
        SetCostLabel(costLabel);
    }

    // 일반 Text와 TMP 비용 라벨에 같은 문자열을 적용한다
    private void SetCostLabel(string costLabel)
    {
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
            if (cost == null || cost.RuntimeAmount <= 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(" / ");
            }

            builder.Append(InventorySystem.FormatIncremental(cost.RuntimeAmount));
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
            default:
                return currencyType.ToString();
        }
    }
}
