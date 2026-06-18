using TMPro;
using UnityEngine;
using IncrementalLib;
using Unity.VisualScripting;

public class ItemIndicator : MonoBehaviour
{
    public UIAnimationValue animValue;
    public TextMeshProUGUI coinText;
    private Vector2 originCoinTextScale;
    private Vector2 coinTextScale;
    private RectTransform coinTextRt;

    //public TextMeshProUGUI firePartText;
    //private Vector2 originFirePartTextScale;
    //private Vector2 firePartTextScale;
    //private RectTransform firePartTextRt;
    
    //public TextMeshProUGUI specialPartText;
    //private Vector2 originSpecialPartTextScale;
    //private Vector2 specialPartTextScale;
    //private RectTransform specialPartTextRt;

    void Awake()
    {
        coinTextRt = coinText.GetComponent<RectTransform>();
        originCoinTextScale = coinTextRt.localScale;
        coinTextScale = originCoinTextScale;

    //    firePartTextRt = firePartText.GetComponent<RectTransform>();
    //    originFirePartTextScale = firePartTextRt.localScale;
    //    firePartTextScale = originFirePartTextScale;

    //    specialPartTextRt = specialPartText.GetComponent<RectTransform>();
    //    originSpecialPartTextScale = specialPartTextRt.localScale;
    //    specialPartTextScale = originSpecialPartTextScale;
    }

    void Start()
    {
        // 인벤토리 시스템 이벤트 구독
        InventorySystem.Inst.OnItemCountChange += OnValueChanged;

        coinText.text = InventorySystem.Inst.GetCountString(RewardCurrencyType.Coin);
        //firePartText.text = InventorySystem.Inst.GetCountString(RewardCurrencyType.FirePart);
        //specialPartText.text = InventorySystem.Inst.GetCountString(RewardCurrencyType.SpecialPart);
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
        //firePartTextScale = Vector2.Lerp(firePartTextScale, originFirePartTextScale, Time.deltaTime * animValue.ScaleReturnLerpSpeed);
        //specialPartTextScale = Vector2.Lerp(specialPartTextScale, originSpecialPartTextScale, Time.deltaTime * animValue.ScaleReturnLerpSpeed);
        coinTextRt.localScale = coinTextScale;
        //firePartTextRt.localScale = firePartTextScale;
        //specialPartTextRt.localScale = specialPartTextScale;
    }

    // ItemManager의 이벤트를 구독하여 string 가비지 발생을 줄인다.
    public void OnCoinValueChanged(string str)
    {
        coinText.text = str;
        coinTextScale = originCoinTextScale * animValue.OnValueChangeScale;
    }

    //public void OnFirePartValueChanged(string str)
    //{
    //    firePartText.text = str;
    //    firePartTextScale = originFirePartTextScale * animValue.OnValueChangeScale;
        
    //}

    //public void OnSpecialPartValueChanged(string str)
    //{
    //    specialPartText.text = str;
    //    specialPartTextScale = originSpecialPartTextScale * animValue.OnValueChangeScale;
    //}

    public void OnValueChanged(ItemData data)
    {
       // if(data.Type == RewardCurrencyType.Coin)
      //  {
            OnCoinValueChanged(data.String);
       // } 
        //else if(data.Type == RewardCurrencyType.FirePart)
        //{
        //    OnFirePartValueChanged(data.String);
        //}
        //else if(data.Type == RewardCurrencyType.SpecialPart)
        //{
        //    OnSpecialPartValueChanged(data.String);
        //}
    }
}
