using System.Collections.Generic;
using IncrementalLib;
using TMPro;
using Unity.Android.Gradle.Manifest;
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
    public TextMeshProUGUI SellCostText;
    public TextMeshProUGUI BuyCostText;
    public int SellCost;
    public int BuyCost;
}

public class TransactionUI : TouchBackHandler
{
    const float NO_ITEM_BRIGHTNESS = 0.4f;
    const float HAS_ITEM_BRIGHTNESS = 1f;
    [Header("테스트 모드")] public bool testMode;
    [Header("아이템 가격 데이터")] public ItemSellBuyCostSo costData;
    [Header("메인 컨텐츠")] public GameObject mainContent;
    [Header("셀 프리펩")] public GameObject cellPrefab;
    [Header("트럭 오브젝트")] public SellerTruckMovement truckObject;
    [Header("스크롤 렉트")] public ScrollRect scrollRect;
    [Header("스크롤 컨텐츠")] public GameObject scrollContent;
    [Header("판매 버튼")] public Button sellButton;
    [Header("구매 버튼")] public Button buyButton;
    [Header("일괄 판매 토글박스")] public Toggle batchToggleBox;
    [Header("아이템 정보 이미지")] public Image infoImage;
    [Header("아이템 정보 이름")] public TextMeshProUGUI infoName;
    [Header("아이템 정보 설명")] public TextMeshProUGUI infoDiscription;
    [Header("아이템 정보 보유량")] public TextMeshProUGUI infoCount;
    [Header("시간 표시 이미지 ")] public Image timeCircle;
    [Header("선택된 셀 색상")] public Color selectedCellColor;
    [Header("비활성화 버튼 색상")] public Color disableButtonColor;
    [Header("자동 판매 상태 진입 시간")] public float autoExecuteEnterTime;
    [Header("자동 판매 실행 간격")] public float autoExecuteInterval;

    [Header("열기/닫기 사운드")] public AudioClip openCloseSound;
    [Header("셀 클릭 사운드")] public AudioClip cellClickSound;
    [Header("거래 사운드")] public AudioClip dealSound;
    [Header("자동 실행 사운드")] public AudioClip autoExecuteSound;

    private Dictionary<Button, TransactionCellData> buttonDict = new();
    private Dictionary<RewardCurrencyType, TransactionCellData> typeDict = new();
    private Button latestSelectedCell;
    private EventTrigger sellEvent;
    private TextMeshProUGUI sellButtonText;
    private EventTrigger buyEvent;
    private TextMeshProUGUI buyButtonText;
    private Color originCellColor;
    private ButtonAutoExecute autoSell = new();
    private ButtonAutoExecute autoBuy = new();
    private List<ItemSellBuyCost> costList;
    public bool BatchMode{ get; set; } = false;
    private bool openState = false;

    private AudioSource aSource;

    void Start()
    {
        costList = costData.CostWithGrade;

        sellEvent = sellButton.GetComponent<EventTrigger>();
        buyEvent = buyButton.GetComponent<EventTrigger>();
        sellButtonText = sellButton.GetComponentInChildren<TextMeshProUGUI>();
        buyButtonText = buyButton.GetComponentInChildren<TextMeshProUGUI>();
        originCellColor = cellPrefab.GetComponent<Button>().colors.normalColor;
        var sellButtonColors = sellButton.colors;
        sellButtonColors.disabledColor = disableButtonColor;
        sellButton.colors = sellButtonColors;
        buyButton.colors = sellButtonColors;

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
            var nameTextComp = buttonComp.transform.Find("NameText").GetComponent<TextMeshProUGUI>();
            var sellTextComp = buttonComp.transform.Find("SellCostText").GetComponent<TextMeshProUGUI>();
            var buyTextComp = buttonComp.transform.Find("BuyCostText").GetComponent<TextMeshProUGUI>();

            imageComp.sprite = metaData.ItemImage;
            nameTextComp.text = metaData.Name;
            countTextComp.text = InventorySystem.Inst.GetCountString(type);
            var sellCost = costList.Find(Data => Data.Grade == metaData.Grade).CostToSell;
            var buyCost = costList.Find(data => data.Grade == metaData.Grade).CostToBuy;
            sellTextComp.text = "판매: " + sellCost.ToString();
            buyTextComp.text = "구매: " + buyCost.ToString();

            newData.Type = type;
            newData.ItemButton = buttonComp;
            newData.ItemImage = imageComp;
            newData.CountText = countTextComp;
            newData.NameText = nameTextComp;
            newData.SellCostText = sellTextComp;
            newData.BuyCostText = buyTextComp;
            newData.SellCost = sellCost;
            newData.BuyCost = buyCost;

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
            OnCloseTransactionUI();
        }

        // 자동 판매 실행기 설정
        autoSell.SetExecuteEnterTime(autoExecuteEnterTime);
        autoSell.SetExecuteInterval(autoExecuteInterval);
        autoSell.RegisterAction(AutoSellItem);

        // 자동 구매 실행기 설정
        autoBuy.SetExecuteEnterTime(autoExecuteEnterTime);
        autoBuy.SetExecuteInterval (autoExecuteInterval);
        autoBuy.RegisterAction(AutoBuyItem);

        OnTouchBackAction += OnCloseTransactionUI;

        aSource = GetComponent<AudioSource>();
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

        // 뒤로가기 버튼 인식 업데이트
        UpdateTouchBackHandler();
        
        // 자동 판매 실행 업데이트
        autoSell.Update();
        if(autoSell.GetAutoExecuteState() && !sellButton.interactable)
        {
            autoSell.SetPressState(false);
        }

        // 자동 구매 실행 업데이트
        autoBuy.Update();
        if(autoBuy.GetAutoExecuteState() && !buyButton.interactable)
        {
            autoBuy.SetPressState(false);
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
        if(!openState) // 코인은 받지 않음
        {
            return;
        }

        // 각 아이템에 대한 판매 및 구매 가능 여부 업데이트
        foreach(RewardCurrencyType type in InventorySystem.Inst.Types)
        {
            if(type == RewardCurrencyType.Coin)
            {
                continue;
            }
            UpdateCellContent(type);
        }

        // 아이템 정보란 업데이트
        if(latestSelectedCell && buttonDict[latestSelectedCell].Type == data.Type)
        {
            infoCount.text = "보유량: " + InventorySystem.Inst.GetCountString(data.Type);
            infoCount.color = InventorySystem.Inst.HasItem(data.Type) ? Color.white : Color.softRed;
        }
    }

    /// <summary>
    /// 아이템 셀 업데이트
    /// 아이템을 보유하고 있지 않을 경우 판매 가격 텍스트를 빨간색으로 표시한다.
    /// 코인이 부족할 경우 구매 가격 텍스트를 빨간색으로 표시한다.
    /// </summary>
    /// <param name="type"></param>
    private void UpdateCellContent(RewardCurrencyType type)
    {
        var data = typeDict[type];
        bool hasItem = InventorySystem.Inst.HasItem(type);
        bool hasCoinEnough = InventorySystem.Inst.CanUseItem(RewardCurrencyType.Coin, data.BuyCost);
        data.CountText.text = InventorySystem.Inst.GetCountString(type);
        data.CountText.color = hasItem ? Color.white : Color.softRed;
        data.SellCostText.color = hasItem ? Color.white : Color.softRed;
        data.BuyCostText.color = hasCoinEnough ? Color.white : Color.softRed;

        if(latestSelectedCell && buttonDict[latestSelectedCell].Type == type)
        {
            SetImageBrightness(data.ItemImage, hasItem ? HAS_ITEM_BRIGHTNESS : NO_ITEM_BRIGHTNESS);
            SetTextButtonEnable(sellButton, sellEvent, sellButtonText, hasItem);
            SetTextButtonEnable(buyButton, buyEvent, buyButtonText, hasCoinEnough);
        }
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
            UpdateCellContent(type);
        }

        // 일단은 판매 버튼과 일괄 판매 토글, 구매 버튼을 숨김
        sellButton.gameObject.SetActive(false);
        buyButton.gameObject.SetActive(false);
        batchToggleBox.gameObject.SetActive(false);

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

        PlayOpenCloseSound();
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

    public void OnCloseButtonClick()
    {
        OnCloseTransactionUI();   
        PlayOpenCloseSound();
    }

    void AutoSellItem()
    {
        PlayAutoExeSound();
        SellItem();
    }

    void AutoBuyItem()
    {
        PlayAutoExeSound();
        BuyItem();
    }

    /// <summary>
    /// 아이템을 1 소모하여 코인을 얻는다.
    /// </summary>
    void SellItem()
    {
        if(!latestSelectedCell)
        {
            return;
        }

        var data = buttonDict[latestSelectedCell];
        var type = data.Type;
        var sellCost = data.SellCost;

        if(BatchMode) // 일괄 판매
        {
            var itemCount = InventorySystem.Inst.GetCount(type);
            var coinToGet = new Incremental(sellCost * itemCount);
            InventorySystem.Inst.AddItem(RewardCurrencyType.Coin, coinToGet);
            InventorySystem.Inst.UseItem(type, itemCount);
        }
        else
        {
            InventorySystem.Inst.AddItem(RewardCurrencyType.Coin, sellCost);
            InventorySystem.Inst.UseItem(type, 1);
        }
    }

    /// <summary>
    /// 코인을 소모하여 아이템을 1 얻는다.
    /// </summary>
    void BuyItem()
    {
        if(!latestSelectedCell)
        {
            return;
        }

        var data = buttonDict[latestSelectedCell];
        var type = data.Type;
        var buyCost = data.BuyCost;

        if(BatchMode) // 일괄 판매
        {
            var coinCount = InventorySystem.Inst.GetCount(RewardCurrencyType.Coin);
            var itemCanGet = new Incremental(coinCount / buyCost);
            var coinToUse = new Incremental(buyCost * itemCanGet);
            InventorySystem.Inst.AddItem(type, itemCanGet);
            InventorySystem.Inst.UseItem(RewardCurrencyType.Coin, coinToUse);
        }
        else
        {
            InventorySystem.Inst.AddItem(type, 1);
            InventorySystem.Inst.UseItem(RewardCurrencyType.Coin, buyCost);
        }
    }

    /// <summary>
    /// 셀 선택 이벤트
    /// 선택한 아이템 타입을 가지고 있지 않다면 판매 버튼을 비활성화 한다.
    /// </summary>
    /// <param name="button"></param>
    public void OnSelectCell(Button button)
    {
        var data = buttonDict[button];
        var type = data.Type;
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
        buyButton.gameObject.SetActive(true);
        batchToggleBox.gameObject.SetActive(true);

        // 아이템을 보유하고 있다면 판매 버튼을 활성화 한다.
        SetTextButtonEnable(sellButton, sellEvent, sellButtonText, hasItem);

        // 코인을 충분히 가지고 있다면 구매 버튼을 활성화 한다.
        bool hasCoinEnough = InventorySystem.Inst.CanUseItem(RewardCurrencyType.Coin, data.BuyCost);
        SetTextButtonEnable(buyButton, buyEvent, buyButtonText, hasCoinEnough);

        PlayCellClickSound();
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
        PlayDealSound();
    }

    public void OnBuyButtonDown()
    {
        autoBuy.SetPressState(true);
    }

    public void OnBuyButtonUp()
    {
        if(!autoBuy.GetAutoExecuteState())
        {
            BuyItem();
        }
        autoBuy.SetPressState(false);
        PlayDealSound();
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

    private void PlayOpenCloseSound()
    {
        aSource.PlayOneShot(openCloseSound);
    }

    private void PlayCellClickSound()
    {
        aSource.PlayOneShot(cellClickSound);
    }

    private void PlayDealSound()
    {
        aSource.PlayOneShot(dealSound);
    }

    private void PlayAutoExeSound()
    {
        aSource.PlayOneShot(autoExecuteSound);
    }
}
