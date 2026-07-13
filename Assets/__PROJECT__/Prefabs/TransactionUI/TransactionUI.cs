using System.Collections.Generic;
using IncrementalLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public struct TransactionCellData
{
    public RewardCurrencyType Type;
    public Button ItemButton;
    public Image ItemImage;
    public TextMeshProUGUI NameText;
    public TextMeshProUGUI CountText;
}

public class TransactionUI : MonoBehaviour
{
    const float NO_ITEM_BRIGHTNESS = 0.4f;
    const float HAS_ITEM_BRIGHTNESS = 1f;
    [Header("테스트 모드")] public bool testMode;
    [Header("메인 컨텐츠")] public GameObject mainContent;
    [Header("셀 프리펩")] public GameObject cellPrefab;
    [Header("트럭 오브젝트")] public SellerTruckMovement truckObject;
    [Header("스크롤 렉트")] public ScrollRect scrollRect;
    [Header("스크롤 컨텐츠")] public GameObject scrollContent;
    [Header("판매 버튼")] public Button sellButton;
    [Header("선택된 셀 색상")] public Color selectedCellColor;
    [Header("비활성화 버튼 색상")] public Color disableButtonColor;

    private Dictionary<Button, TransactionCellData> buttonDict = new();
    private Dictionary<RewardCurrencyType, TransactionCellData> typeDict = new();
    private Button latestSelectedButton;
    private EventTrigger sellEvent;
    private TextMeshProUGUI sellButtonText;
    private Color originCellColor;
    private Color originSellButtonColor;

    void Start()
    {
        sellEvent = sellButton.GetComponent<EventTrigger>();
        sellButtonText = sellButton.GetComponentInChildren<TextMeshProUGUI>();
        originSellButtonColor = sellButton.colors.normalColor;
        originCellColor = cellPrefab.GetComponent<Button>().colors.normalColor;
        var sellButtonColors = sellButton.colors;
        sellButtonColors.disabledColor = disableButtonColor;
        sellButton.colors = sellButtonColors;

        // 종재하는 아이템에 대한 모든 셀을 생성한다.
        foreach(RewardCurrencyType type in InventorySystem.Inst.Types)
        {
            // 코인은 건너뜀
            if(type == RewardCurrencyType.Coin)
            {
                continue;
            }
            var newData = new TransactionCellData();
            var metaData = InventorySystem.Inst.GetMetaData(type);
            var newCell = Instantiate(cellPrefab, scrollContent.transform, false);
            var buttonComp = newCell.GetComponent<Button>();
            var imageComp = buttonComp.transform.Find("ItemImage").GetComponent<Image>();
            var countTextComp = buttonComp.transform.Find("CountText").GetComponent<TextMeshProUGUI>();
            var coinTextComp = buttonComp.transform.Find("CoinText").GetComponent<TextMeshProUGUI>();
            var nameTextComp = buttonComp.transform.Find("NameText").GetComponent<TextMeshProUGUI>();

            imageComp.sprite = metaData.ItemImage;
            nameTextComp.text = metaData.Name;
            countTextComp.text = InventorySystem.Inst.GetCountString(type);
            coinTextComp.text = "+" + metaData.SellCoinCount.ToString();

            newData.Type = type;
            newData.ItemButton = buttonComp;
            newData.ItemImage = imageComp;
            newData.CountText = countTextComp;
            newData.NameText = nameTextComp;

            buttonComp.GetComponent<PassEventToScrollRect>().onButtonClick.AddListener(() => OnSelectCell(buttonComp));
            SetImageBrightness(imageComp, NO_ITEM_BRIGHTNESS);

            buttonDict.Add(buttonComp, newData);
            typeDict.Add(type, newData);
        }

        InventorySystem.Inst.OnItemCountChange += OnItemCountChange;

        if(testMode)
        {
            OnOpenTransactionUI();
        }
        else
        {
            mainContent.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if(InventorySystem.Inst)
        {
            InventorySystem.Inst.OnItemCountChange -= OnItemCountChange;
        }
    }

    /// <summary>
    /// 아이템 수량이 변경되면 실시간으로 업데이트 한다.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="prev"></param>
    public void OnItemCountChange(ItemData data, Incremental prev)
    {
        if(data.Type == RewardCurrencyType.Coin) // 코인은 받지 않음
        {
            return;
        }
        UpdateItemCountText(data.Type);
    }

    /// <summary>
    /// 아이템 보유량 텍스트 업데이트
    /// 보유하고 있지 않을 경우 빨간색으로 표시한다.
    /// </summary>
    /// <param name="type"></param>
    private void UpdateItemCountText(RewardCurrencyType type)
    {
        var data = typeDict[type];
        var countText = data.CountText;
        var nameText = data.NameText;
        bool hasItem = InventorySystem.Inst.HasItem(type);
        countText.text = InventorySystem.Inst.GetCountString(type);
        countText.color = hasItem ? Color.white : Color.softRed;
        nameText.color = countText.color;
        SetImageBrightness(data.ItemImage, hasItem ? HAS_ITEM_BRIGHTNESS : NO_ITEM_BRIGHTNESS);
    }

    /// <summary>
    /// UI 닫기
    /// </summary>
    public void OnOpenTransactionUI()
    {
        mainContent.SetActive(true);

        // UI를 열 때 각 아이템의 현재 개수를 업데이트 한다.
        foreach(RewardCurrencyType type in InventorySystem.Inst.Types)
        {
            UpdateItemCountText(type);
        }

        // 일단은 판매 버튼을 숨김
        sellButton.gameObject.SetActive(false);
        latestSelectedButton = null;
        ResetScroll();
    }

    /// <summary>
    /// UI 닫기
    /// </summary>
    public void OnCloseTransactionUI()
    {
        mainContent.SetActive(false);
    }

    /// <summary>
    /// 아이템을 1 소모하여 코인을 얻는다.
    /// </summary>
    public void SellItem()
    {
        if(!latestSelectedButton)
        {
            return;
        }
        var type = buttonDict[latestSelectedButton].Type;
        var metaData = InventorySystem.Inst.GetMetaData(type);
        var coinToGet = metaData.SellCoinCount;
        InventorySystem.Inst.AddItem(RewardCurrencyType.Coin, coinToGet);
        InventorySystem.Inst.UseItem(type, 1);
        UpdateItemCountText(type);
    }

    /// <summary>
    /// 셀 선택 이벤트
    /// 선택한 아이템 타입을 가지고 있지 않다면 판매 버튼을 비활성화 한다.
    /// </summary>
    /// <param name="button"></param>
    public void OnSelectCell(Button button)
    {
        var type = buttonDict[button].Type;
        bool hasItem = InventorySystem.Inst.HasItem(type);

        if(latestSelectedButton && latestSelectedButton != button)
        {
            SetButtonColor(latestSelectedButton, originCellColor);
        }
        SetButtonColor(button, selectedCellColor);
        latestSelectedButton = button;

        if(!sellButton.gameObject.activeInHierarchy)
        {
            sellButton.gameObject.SetActive(true);
        }

        SetTextButtonEnable(sellButton, sellEvent, sellButtonText, hasItem);
    }

    public void OnSellButtonDown()
    {
        
    }

    public void OnSellButtonUp()
    {
        SellItem();
    }

    /// <summary>
    /// 스크롤 초기화
    /// </summary>
    private void ResetScroll()
    {
        scrollRect.verticalNormalizedPosition = 1f;
        scrollRect.velocity = Vector2.zero;
    }

    /// <summary>
    /// 버튼의 색상을 변경한다. 투명도는 유지된다.
    /// </summary>
    /// <param name="button"></param>
    /// <param name="color"></param>
    private void SetButtonColor(Button button, Color color)
    {
        var colorBlock = button.colors;
        var opacity = colorBlock.normalColor.a;
        colorBlock.normalColor = new Color(color.r, color.g, color.b, opacity);
        colorBlock.pressedColor = colorBlock.normalColor;
        colorBlock.selectedColor = colorBlock.normalColor;
        colorBlock.highlightedColor = colorBlock.normalColor;
        button.colors = colorBlock;
    }

    /// <summary>
    /// 이미지의 밝기를 조정한다. // brightness가 높을수록 밝아진다.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="brightness"></param>
    private void SetImageBrightness(Image image, float brightness)
    {
        var color = image.color;
        color.r = brightness;
        color.g = brightness;
        color.b = brightness;
        image.color = color;
    }

    /// <summary>
    /// 텍스트가 있는 버튼의 상호작용 상태를 변경한다.
    /// </summary>
    /// <param name="flag"></param>
    private void SetTextButtonEnable(Button button, EventTrigger event_, TextMeshProUGUI text, bool flag)
    {
        button.interactable = flag;
        event_.enabled = button.interactable;
        text.color = button.interactable ? Color.white : button.colors.disabledColor;
    }
}
