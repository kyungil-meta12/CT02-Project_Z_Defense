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
    [Header("일괄 판매 토글박스")] public Toggle batchCellToggle;
    [Header("아이템 정보 이미지")] public Image infoImage;
    [Header("아이템 정보 이름")] public TextMeshProUGUI infoName;
    [Header("아이템 정보 설명")] public TextMeshProUGUI infoDiscription;
    [Header("아이템 정보 보유량")] public TextMeshProUGUI infoCount;
    [Header("시간 표시 이미지 ")] public Image timeCircle;
    [Header("선택된 셀 색상")] public Color selectedCellColor;
    [Header("비활성화 버튼 색상")] public Color disableButtonColor;
    [Header("자동 판매 상태 진입 시간")] public float autoSellEnterTime;
    [Header("자동 판매 실행 간격")] public float autoSellInterval;

    private Dictionary<Button, TransactionCellData> buttonDict = new();
    private Dictionary<RewardCurrencyType, TransactionCellData> typeDict = new();
    private Button latestSelectedCell;
    private EventTrigger sellEvent;
    private TextMeshProUGUI sellButtonText;
    private Color originCellColor;
    private ButtonAutoExecute autoSell = new();
    public bool BatchMode{ get; set; } = false;
    private bool openState = false;

    void Start()
    {
        sellEvent = sellButton.GetComponent<EventTrigger>();
        sellButtonText = sellButton.GetComponentInChildren<TextMeshProUGUI>();
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
            coinTextComp.text = metaData.SellCoinCount.ToString();

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

        // 자동 판매 실행기 설정
        autoSell.SetExecuteEnterTime(autoSellEnterTime);
        autoSell.SetExecuteInterval(autoSellInterval);
        autoSell.RegisterAction(SellItem);
    }

    void OnDestroy()
    {
        if(InventorySystem.Inst)
        {
            InventorySystem.Inst.OnItemCountChange -= OnItemCountChange;
        }
    }

    void Update()
    {
        if(!openState)
        {
            return;
        }
        
        // 자동 판매 실행 업데이트
        autoSell.Update();
        if(autoSell.GetAutoExecuteState() && !sellButton.interactable)
        {
            autoSell.SetPressState(false);
        }

        // 대기 시간이 마감되면 UI를 강제로 닫는다.
        timeCircle.fillAmount = truckObject.GetNormalizedRemainTime();
        if(truckObject.GetLeaveState())
        {
            OnCloseTransactionUI();
        }
    }

    /// <summary>
    /// 아이템 수량이 변경되면 실시간으로 업데이트 한다.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="prev"></param>
    public void OnItemCountChange(ItemData data, Incremental prev)
    {
        if(!openState || data.Type == RewardCurrencyType.Coin) // 코인은 받지 않음
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
            if(type == RewardCurrencyType.Coin)
            {
                continue;
            }
            UpdateItemCountText(type);
        }

        // 일단은 판매 버튼과 일괄 판매 토글을 숨김
        sellButton.gameObject.SetActive(false);
        batchCellToggle.gameObject.SetActive(false);
        ResetScroll();
        if(latestSelectedCell)
        {
            SetButtonColor(latestSelectedCell, originCellColor);
        }
        latestSelectedCell = null;

        // 아이템 정보란 초기화
        SetImageVisibility(infoImage, false);
        infoName.text = "";
        infoDiscription.text = "";
        infoCount.text = "";

        UIManager.Inst.HideGameUI();

        openState = true;
    }

    /// <summary>
    /// UI 닫기
    /// </summary>
    public void OnCloseTransactionUI()
    {
        mainContent.SetActive(false);
        UIManager.Inst.RevertGameUI();
        openState = false;
    }

    /// <summary>
    /// 아이템을 1 소모하여 코인을 얻는다.
    /// </summary>
    public void SellItem()
    {
        if(!latestSelectedCell)
        {
            return;
        }
        var type = buttonDict[latestSelectedCell].Type;
        var metaData = InventorySystem.Inst.GetMetaData(type);

        if(BatchMode) // 일괄 판매
        {
            var itemCount = InventorySystem.Inst.GetCount(type);
            var coinToGet = new Incremental(metaData.SellCoinCount * itemCount);
            InventorySystem.Inst.AddItem(RewardCurrencyType.Coin, coinToGet);
            InventorySystem.Inst.UseItem(type, itemCount);
        }
        else
        {
            var coinToGet = metaData.SellCoinCount;
            InventorySystem.Inst.AddItem(RewardCurrencyType.Coin, coinToGet);
            InventorySystem.Inst.UseItem(type, 1);
        }

        infoCount.text = "보유량: " + InventorySystem.Inst.GetCountString(type);
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

        if(latestSelectedCell && latestSelectedCell != button)
        {
            SetButtonColor(latestSelectedCell, originCellColor);
        }
        SetButtonColor(button, selectedCellColor);
        latestSelectedCell = button;

        var metaData = InventorySystem.Inst.GetMetaData(type);
        infoImage.sprite = metaData.ItemImage;
        SetImageVisibility(infoImage, true);
        infoName.text = metaData.Name;
        infoDiscription.text = metaData.InfoText;
        infoCount.text = "보유량: " + InventorySystem.Inst.GetCountString(type);
        infoCount.color = hasItem ? Color.white : Color.softRed;

        sellButton.gameObject.SetActive(true);
        batchCellToggle.gameObject.SetActive(true);
        SetTextButtonEnable(sellButton, sellEvent, sellButtonText, hasItem);
    }

    public void OnSellButtonDown()
    {
        autoSell.SetPressState(true);
    }

    public void OnSellButtonUp()
    {
        if(!autoSell.GetAutoExecuteState())
        {
            SellItem();
        }
        autoSell.SetPressState(false);
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

    /// <summary>
    /// 이미지가 보이는 여부를 설정한다.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="flag"></param>
    private void SetImageVisibility(Image image, bool flag)
    {
        var color = image.color;
        color.a = flag ? 1f : 0f;
        image.color = color;
    }
}
