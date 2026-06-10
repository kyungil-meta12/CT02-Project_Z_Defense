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

    public TextMeshProUGUI firePartText;
    private Vector2 originFirePartTextScale;
    private Vector2 firePartTextScale;
    private RectTransform firePartTextRt;
    
    public TextMeshProUGUI specialPartText;
    private Vector2 originSpecialPartTextScale;
    private Vector2 specialPartTextScale;
    private RectTransform specialPartTextRt;

    void Awake()
    {
        coinTextRt = coinText.GetComponent<RectTransform>();
        originCoinTextScale = coinTextRt.localScale;
        coinTextScale = originCoinTextScale;

        firePartTextRt = firePartText.GetComponent<RectTransform>();
        originFirePartTextScale = firePartTextRt.localScale;
        firePartTextScale = originFirePartTextScale;

        specialPartTextRt = specialPartText.GetComponent<RectTransform>();
        originSpecialPartTextScale = specialPartTextRt.localScale;
        specialPartTextScale = originSpecialPartTextScale;
    }
    void Start()
    {
        // ItemManager 값 변경 이벤트 구독
        // string 가비지 발생을 줄이기 위해 값이 변경되었을 때만 인디케이터의 텍스트에 반영한다.
        ItemManager.Inst.OnCoinValueChange += OnCoinValueChanged;
        ItemManager.Inst.OnFirePartValueChange += OnFirePartValueChanged;
        ItemManager.Inst.OnSpecialPartValueChange += OnSpecialPartValueChanged;

        coinText.text = ItemManager.Inst.CoinCountString;
        firePartText.text = ItemManager.Inst.FirePartCountString;
        specialPartText.text = ItemManager.Inst.SpecialPartCountString;
    }

    void OnDestroy()
    {
        if(ItemManager.Inst)
        {
            ItemManager.Inst.OnCoinValueChange -= OnCoinValueChanged;
            ItemManager.Inst.OnFirePartValueChange -= OnFirePartValueChanged;
            ItemManager.Inst.OnSpecialPartValueChange -= OnSpecialPartValueChanged;
        }
    }

    // 소유한 개수가 변경되면 피드백 애니메이션을 재생한다.
    void Update()
    {
        coinTextScale = Vector2.Lerp(coinTextScale, originCoinTextScale, Time.deltaTime * animValue.ScaleReturnLerpSpeed);
        firePartTextScale = Vector2.Lerp(firePartTextScale, originFirePartTextScale, Time.deltaTime * animValue.ScaleReturnLerpSpeed);
        specialPartTextScale = Vector2.Lerp(specialPartTextScale, originSpecialPartTextScale, Time.deltaTime * animValue.ScaleReturnLerpSpeed);
        coinTextRt.localScale = coinTextScale;
        firePartTextRt.localScale = firePartTextScale;
        specialPartTextRt.localScale = specialPartTextScale;
    }

    // ItemManager의 이벤트를 구독하여 string 가비지 발생을 줄인다.
    public void OnCoinValueChanged(string str)
    {
        coinText.text = str;
        coinTextScale = originCoinTextScale * animValue.OnValueChangeScale;
    }

    public void OnFirePartValueChanged(string str)
    {
        firePartText.text = str;
        firePartTextScale = originFirePartTextScale * animValue.OnValueChangeScale;
        
    }

    public void OnSpecialPartValueChanged(string str)
    {
        specialPartText.text = str;
        specialPartTextScale = originSpecialPartTextScale * animValue.OnValueChangeScale;
    }
}
