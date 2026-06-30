using TMPro;
using UnityEngine;
using IncrementalLib;

public class ItemIndicator : MonoBehaviour
{
    public UIAnimationValue animValue;
    public TextMeshProUGUI coinText;
    private Vector2 originCoinTextScale;
    private Vector2 coinTextScale;
    private RectTransform coinTextRt;

    void Awake()
    {
        coinTextRt = coinText.GetComponent<RectTransform>();
        originCoinTextScale = coinTextRt.localScale;
        coinTextScale = originCoinTextScale;
    }

    void Start()
    {
        // 인벤토리 시스템 이벤트 구독
        InventorySystem.Inst.OnItemCountChange += OnValueChanged;

        coinText.text = InventorySystem.Inst.GetCountString(RewardCurrencyType.Coin);
    }

    void OnDestroy()
    {
        if(InventorySystem.Inst)
        {
            InventorySystem.Inst.OnItemCountChange -= OnValueChanged;
        }
    }

    // 소유한 개수가 변경되면 피드백 애니메이션을 재생한다.
    void Update()
    {
        coinTextScale = Vector2.Lerp(coinTextScale, originCoinTextScale, Time.deltaTime * animValue.ScaleReturnLerpSpeed);
        coinTextRt.localScale = coinTextScale;
    }

    public void OnValueChanged(ItemData data, Incremental prev)
    {
        if(data.Type == RewardCurrencyType.Coin)
        {
            coinText.text = data.CountString;
            coinTextScale = originCoinTextScale * animValue.OnValueChangeScale;
        }
    }
}
