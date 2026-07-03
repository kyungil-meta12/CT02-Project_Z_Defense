using IncrementalLib;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum ContentType
{
    Inventory,
    Craft,
    Decompose
}

/// <summary>
/// 패널 컨텐츠 데이터
/// </summary>
[Serializable]
public struct ContentData
{
    public ContentType Type;
    public GameObject Content;
    public Button TabButton;
    public Button FunctionButton;
}

public class CellData {
    public RewardCurrencyType Type;
    public Image CellImage;
    public TextMeshProUGUI CellCountText;
}

// 각 셀 버튼이 가지는 정보를 다루는 클래스
public class CellDictionary
{
    public Dictionary<Button, CellData> Cell = new();
}

/// <summary>
/// 버튼을 길게 누르고 있으면 자동으로 일정 간격마다 콜벡을 호출하는 클래스
/// </summary>
public class ButtonAutoExecute
{
    private bool PressState = false;

    private float AutoEnterTime = 0f;
    private float ExecuteInterval = 0f;
    private Action ExecuteAction;

    private bool AutoExecuteState = false;
    private float AutoEnterAccumTime = 0f;
    private float AutoExecuteAccumTime = 0f;

    public void SetExecuteEnterTime(float sec)
    {
        AutoEnterTime = sec;
    }

    public void SetExecuteInterval(float sec)
    {
        ExecuteInterval = sec;
    }

    public void RegisterAction(Action action)
    {
        ExecuteAction = action;
    }

    public void SetPressState(bool Flag)
    {
        PressState = Flag;
    }

    public bool GetAutoExecuteState()
    {
        return AutoExecuteState;
    }

    public void Update()
    {
        if(PressState)
        {
            AutoEnterAccumTime += Time.deltaTime;
            if(AutoEnterAccumTime >= AutoEnterTime)
            {
                if(!AutoExecuteState)
                {
                    AutoExecuteState = true;
                    AutoExecuteAccumTime = ExecuteInterval;
                }
            }
            if(AutoExecuteState)
            {
                AutoExecuteAccumTime += Time.deltaTime;
                if(AutoExecuteAccumTime >= ExecuteInterval)
                {
                    AutoExecuteAccumTime -= ExecuteInterval;
                    ExecuteAction?.Invoke();
                }
            }
        }
        else
        {
            AutoEnterAccumTime = 0f;
            AutoExecuteAccumTime = 0f;
            AutoExecuteState = false;
        }
    }
}

public class InventoryUI : MonoBehaviour
{
    const float NO_ITEM_BRIGHTNESS = 0.4f;
    const float HAS_ITEM_BRIGHTNESS = 1f;

    [Header("배경 객체")] public Image background;
    [Header("인벤토리 UI의 메인 조작부")] public GameObject mainController;
    [Header("인벤토리 UI의 스크롤 조작부")] public ScrollRect scrollRect;

    [Header("패널 컨텐츠 객체 목록")] public List<ContentData> contentList;

    [Header("인벤토리 아이템 셀 프리펩")] public GameObject inventoryCellPrefab;
    [Header("크래프트 아이템 셀 프리펩")] public GameObject craftCellPrefab;
    [Header("분해 아이템 셀 프리펩")] public GameObject decomposeCellPrefab;
    [Header("크래프트 필요 아이템 표시기 프리펩")] public GameObject craftCellNeedInfoPrefab;

    [Header("제작 버튼 객체")] public Button makeButton;
    [Header("분해 버튼 객체")] public Button decomposeButton;

    [Header("아이템 보유량 텍스트 객체")] public TextMeshProUGUI itemCountText;
    [Header("아이템 이름 텍스트 객체")] public TextMeshProUGUI itemNameText;
    [Header("아이템 정보 텍스트 객체")] public TextMeshProUGUI itemInfoText;
    [Header("아이템 정보 이미지")] public Image itemInfoImage;

    [Header("아이템 표시기 객체 목록")] public GameObject[] itemViewerList;

    [Header("패널 텍스트 객체")] public TextMeshProUGUI pannelTitletext;

    [Header("선택된 탭 색상")] public Color selectedTabColor;
    [Header("선택된 셀 색상")] public Color selectedCellColor;
    [Header("비활성화 버튼 색상")] public Color disableButtonColor;

    [Header("절전 전환 버튼")] public GameObject powerSavingSwitchButton;

    [Header("자동 실행 진입 시간")] public float autoExecuteEnterTime;
    [Header("자동 실행 간격")] public float autoExecuteInterval;

    private EventTrigger makeButtonEvent;
    private TextMeshProUGUI makeButtonText;
    private EventTrigger decomposeButtonEvent;
    private TextMeshProUGUI decomposeButtonText;


    // 현재 패널 콘텐츠
    private ContentType currentContent;

    // 패널 컨텐츠 딕셔너리
    private Dictionary<ContentType, ContentData> contentDict = new();

    // 각 컨텐츠 타입별로 구분하여 관리하는 셀 딕셔너리
    private Dictionary<ContentType, CellDictionary> cellDict = new();

    // 현재 선택된 크래프트 아이템을 제작하는데에 필요한 아이템 관련 데이터 딕셔너리
    private Dictionary<RewardCurrencyType, ItemCraftData> needItemData = new();
    private Dictionary<RewardCurrencyType, TextMeshProUGUI> needItemText = new();

    // 분해하면 얻는 아이템 관련 데이터 딕셔너리
    private Dictionary<RewardCurrencyType, ItemDecomposeData> decomposeItemData = new();
    private Dictionary<RewardCurrencyType, TextMeshProUGUI> decomposeItemText = new();

    // 마지막으로 선택된 아이템 타입 및 셀
    private RewardCurrencyType selectedType = 0;
    private Button selectedCell;

    // 원본 셀 버튼 색상
    private Color originCellColor;

    // 열려있는가?
    private bool openState = false;

    ButtonAutoExecute makeAutoExecute = new();
    ButtonAutoExecute decompAutoExecute = new();

    // 인벤토리를 닫은 상태로 시작
    void Awake()
    {
        makeAutoExecute.SetExecuteEnterTime(autoExecuteEnterTime);
        makeAutoExecute.SetExecuteInterval(autoExecuteInterval);
        makeAutoExecute.RegisterAction(MakeItem);

        decompAutoExecute.SetExecuteEnterTime(autoExecuteEnterTime);
        decompAutoExecute.SetExecuteInterval(autoExecuteInterval);
        decompAutoExecute.RegisterAction(DecomposeItem);

        var buttonColor = makeButton.colors;
        buttonColor.disabledColor = disableButtonColor;

        makeButton.colors = buttonColor;
        makeButtonEvent = makeButton.GetComponent<EventTrigger>();
        makeButtonText = makeButton.GetComponentInChildren<TextMeshProUGUI>();

        decomposeButton.colors = buttonColor;
        decomposeButtonEvent = decomposeButton.GetComponent<EventTrigger>();
        decomposeButtonText = decomposeButton.GetComponentInChildren<TextMeshProUGUI>();

        // 딕셔너리에 패널 컨텐츠 정보 저장
        foreach (var c in contentList)
        {
            contentDict.Add(c.Type, c);
        }
    }

    void Start()
    {
        InventorySystem.Inst.OnItemCountChange += OnItemValueChanged;

        // 인벤토리 셀 생성
        var invenContent = GetContentObject(ContentType.Inventory);
        cellDict.Add(ContentType.Inventory, new CellDictionary());
        var invenDict = cellDict[ContentType.Inventory];

        // 아이템 타입 종류 만큼 셀을 생성한다.
        foreach (RewardCurrencyType type in InventorySystem.Inst.Types)
        {
            var buttonComp = Instantiate(inventoryCellPrefab, invenContent.transform, false).GetComponent<Button>();
            buttonComp.GetComponent<PassEventToScrollRect>().onButtonClick.AddListener(() => OnInventoryCellClick(buttonComp));

            var imageComp = buttonComp.transform.Find("ItemImage").GetComponent<Image>();
            imageComp.sprite = InventorySystem.Inst.GetMetaData(type).ItemImage;
            SetImageBrightness(imageComp, NO_ITEM_BRIGHTNESS);

            var textComp = buttonComp.transform.Find("ItemCount").GetComponent<TextMeshProUGUI>();
            textComp.text = "";

            invenDict.Cell.Add(buttonComp, new CellData());
            var cellData = invenDict.Cell[buttonComp];
            cellData.Type = type;
            cellData.CellImage = imageComp;
            cellData.CellCountText = textComp;
        }

        // 분해 셀 생성
        // 분해 가능한 아이템에 대해서만 셀을 생성한다.
        var decomposeContent = GetContentObject(ContentType.Decompose);
        cellDict.Add(ContentType.Decompose, new CellDictionary());
        var decompDict = cellDict[ContentType.Decompose];

        foreach (RewardCurrencyType type in InventorySystem.Inst.Types)
        {
            var metaData = InventorySystem.Inst.GetMetaData(type);
            if (!metaData.Decomposable)
            {
                continue;
            }
            var buttonComp = Instantiate(decomposeCellPrefab, decomposeContent.transform, false).GetComponent<Button>();
            buttonComp.GetComponent<PassEventToScrollRect>().onButtonClick.AddListener(() => OnDecomposeCellClick(buttonComp));

            var imageComp = buttonComp.transform.Find("ItemImage").GetComponent<Image>();
            imageComp.sprite = metaData.ItemImage;
            SetImageBrightness(imageComp, NO_ITEM_BRIGHTNESS);

            var textComp = buttonComp.transform.Find("ItemCount").GetComponent<TextMeshProUGUI>();
            textComp.text = "";

            decompDict.Cell.Add(buttonComp, new CellData());
            var cellData = decompDict.Cell[buttonComp];
            cellData.Type = type;
            cellData.CellImage = imageComp;
            cellData.CellCountText = textComp;
        }

        // 아이템 중에서 제작 가능한 아이템 종류 개수 만큼 크래프트 셀을 생성한다.
        var craftContent = GetContentObject(ContentType.Craft);
        cellDict.Add(ContentType.Craft, new CellDictionary());
        var craftDict = cellDict[ContentType.Craft];

        foreach (RewardCurrencyType type in InventorySystem.Inst.Types)
        {
            var metaData = InventorySystem.Inst.GetMetaData(type);
            if (!metaData.Createable)
            {
                continue;
            }

            var buttonComp = Instantiate(craftCellPrefab, craftContent.transform, false).GetComponent<Button>();
            buttonComp.GetComponent<PassEventToScrollRect>().onButtonClick.AddListener(() => OnCraftCellClick(buttonComp));

            var imageComp = buttonComp.transform.Find("ItemImage").GetComponent<Image>();
            imageComp.sprite = metaData.ItemImage;
            SetImageVisibility(imageComp, true);

            var textComp = buttonComp.transform.Find("ItemName").GetComponent<TextMeshProUGUI>();
            textComp.text = metaData.Name;

            var countTextComp = buttonComp.transform.Find("ItemCount").GetComponent<TextMeshProUGUI>();
            countTextComp.text = "+" + metaData.CountPerCraft.ToString();

            craftDict.Cell.Add(buttonComp, new CellData());
            var cellData = craftDict.Cell[buttonComp];
            cellData.Type = type;
            // 크래프트 셀은 수량에 상관없이 항상 같은 이미지 상태를 유지하고 보유량을 표시하지 않기 때문에 타입을 제외한 나머지 데이터는 저장하지 않는다.
        }

        // 제작 아이템 표시기 초기화
        ResetItemViewer();

        // 기본 셀 버튼 컬러 얻기
        originCellColor = inventoryCellPrefab.GetComponent<Button>().colors.normalColor;

        // 인벤토리 UI 숨기기
        OnCloseInventory();
    }

    void OnDestroy()
    {
        if (InventorySystem.Inst)
        {
            InventorySystem.Inst.OnItemCountChange -= OnItemValueChanged;
        }
    }

    void Update()
    {
        makeAutoExecute.Update();
        decompAutoExecute.Update();
    }

    // 아이템 개수가 변경 될 때마다 아이템에 해당하는 인덱스의 정보를 업데이트 한다.
    public void OnItemValueChanged(ItemData data, Incremental prev)
    {
        if (!openState)
        {
            return;
        }

        if(selectedCell && selectedType == data.Type)
        {
            SetItemInfoCountText(data.Type);
        }
        UpdateNeedItemData(data.Type);
        UpdateCells();
    }

    /// <summary>
    /// 인벤토리를 열 때 호출하는 메서드
    /// </summary>
    public void OnOpenInventory()
    {
        mainController.SetActive(true);
        background.gameObject.SetActive(true);
        powerSavingSwitchButton.SetActive(false);
        SetToInventoryTab();
        openState = true;
    }

    /// <summary>
    /// 인벤토리를 닫을 때 호출하는 메서드
    /// </summary>
    public void OnCloseInventory()
    {
        mainController.SetActive(false);
        background.gameObject.SetActive(false);
        powerSavingSwitchButton.SetActive(true);
        openState = false;
    }

    /// <summary>
    /// 인벤토리 셀 클릭 이벤트 // 클릭 시 버튼에 해당하는 타입에 해당하는 메타데이터에 있는 이름과 정보를 불러온다.
    /// </summary>
    /// <param name="button"></param>
    public void OnInventoryCellClick(Button button)
    {
        if (selectedCell && selectedCell != button)
        {
            SetButtonColor(selectedCell, originCellColor);
        }
        SetButtonColor(button, selectedCellColor);
        selectedCell = button;

        var cell = cellDict[ContentType.Inventory].Cell;
        selectedType = cell[button].Type;
        var metaData = InventorySystem.Inst.GetMetaData(selectedType);
        SetInfoText(metaData);
        SetInfoImage(metaData);
    }

    /// <summary>
    /// 인벤토리 셀 클릭 이벤트 // 클릭 시 버튼에 해당하는 타입에 해당하는 메타데이터에 있는 이름과 정보를 불러온다.
    /// </summary>
    /// <param name="button"></param>
    public void OnDecomposeCellClick(Button button)
    {
        if (selectedCell && selectedCell != button)
        {
            SetButtonColor(selectedCell, originCellColor);
        }
        SetButtonColor(button, selectedCellColor);
        selectedCell = button;

        var cell = cellDict[ContentType.Decompose].Cell;
        selectedType = cell[button].Type;
        var metaData = InventorySystem.Inst.GetMetaData(selectedType);
        SetInfoText(metaData);
        SetInfoImage(metaData);

        ResetItemViewer();

        // 필요한 아이템들에 대해서만 뷰어 활성화
        var decomposeItems = metaData.ItemsFromDecompose;
        for (int i = 0; i < decomposeItems.Count; i++)
        {
            itemViewerList[i].SetActive(true);
            var image = itemViewerList[i].GetComponentInChildren<Image>();
            var text = itemViewerList[i].GetComponentInChildren<TextMeshProUGUI>();
            image.sprite = InventorySystem.Inst.GetMetaData(decomposeItems[i].Type).ItemImage;
            SetImageVisibility(image, true);
            decomposeItemData.Add(decomposeItems[i].Type, decomposeItems[i]);
            decomposeItemText.Add(decomposeItems[i].Type, text);
            UpdateDecomposeItemData(decomposeItems[i].Type);
        }

        // 한 번 크래프트 아이템 셀을 터치하면 분해 버튼이 다시 활성화 된다.
        contentDict[ContentType.Decompose].FunctionButton.gameObject.SetActive(true);
    }

    /// <summary>
    /// 크래프트 아이템 클릭 이벤트 // 클릭 시 버튼에 해당하는 타입에 해당하는 메타데이터에 있는 이름과 정보를 불러온다.
    /// </summary>
    /// <param name="button"></param>
    public void OnCraftCellClick(Button button)
    {
        if (selectedCell && selectedCell != button)
        {
            SetButtonColor(selectedCell, originCellColor);
        }
        SetButtonColor(button, selectedCellColor);
        selectedCell = button;

        var cell = cellDict[ContentType.Craft].Cell;
        selectedType = cell[button].Type;
        var metaData = InventorySystem.Inst.GetMetaData(selectedType);
        SetInfoText(metaData);
        SetInfoImage(metaData);

        ResetItemViewer();
        
        // 필요한 아이템들에 대해서만 뷰어 활성화
        var needItems = metaData.ItemsToCreate;
        for(int i = 0; i < needItems.Count; i ++)
        {
            itemViewerList[i].SetActive(true);
            var image = itemViewerList[i].GetComponentInChildren<Image>();
            var text = itemViewerList[i].GetComponentInChildren<TextMeshProUGUI>();
            image.sprite = InventorySystem.Inst.GetMetaData(needItems[i].Type).ItemImage;
            SetImageVisibility(image, true);

            // 각 필요 아이템에 대해서도 실시간으로 보유량을 표시하기 위해 딕셔너리에 데이터 추가 후 반영
            // 하나라도 아이템 개수가 모자라면 제작 버튼 비활성화
            needItemData.Add(needItems[i].Type, needItems[i]);
            needItemText.Add(needItems[i].Type, text);
            UpdateNeedItemData(needItems[i].Type);
        }

        // 한 번 크래프트 아이템 셀을 터치하면 작업 버튼이 다시 활성화 된다.
        contentDict[ContentType.Craft].FunctionButton.gameObject.SetActive(true);
    }

    public void OnInventoryTabClick()
    {
        if (!GetContentObject(ContentType.Inventory).activeInHierarchy)
        {
            SetToInventoryTab();
        }
    }

    public void OnCraftTabClick()
    {
        if (!GetContentObject(ContentType.Craft).activeInHierarchy)
        {
            SetToCraftTab();
        }
    }

    public void OnDecomposeTabClick()
    {
        if(!GetContentObject(ContentType.Decompose).activeInHierarchy)
        {
            SetToDecomposeTab();
        }
    }

    public void OnMakeButtonDown()
    {
        makeAutoExecute.SetPressState(true);
    }

    public void OnMakeButtonUp()
    {
        if(!makeAutoExecute.GetAutoExecuteState())
        {
            MakeItem();
        }
        makeAutoExecute.SetPressState(false);
    }

    public void OnDecomposeButtonDown()
    {
        decompAutoExecute.SetPressState(true);
    }

    public void OnDecomposeButtonUP()
    {
        if(!decompAutoExecute.GetAutoExecuteState())
        {
            DecomposeItem();
        }
        decompAutoExecute.SetPressState(false);
    }

    /// <summary>
    /// 아이템을 분해한다.
    /// </summary>
    public void DecomposeItem()
    {
        var metaData = InventorySystem.Inst.GetMetaData(selectedType);
        var decompItems = metaData.ItemsFromDecompose;
        foreach(var item in decompItems)
        {
            var randomCount = UnityEngine.Random.Range(item.Min, item.Max + 1);
            InventorySystem.Inst.AddItem(item.Type, randomCount);
        }
        
        // 분해된 아이템 획득
        InventorySystem.Inst.UseItem(selectedType, 1);
        print("[InventoryUI] 아이템 분해 완료");
    }

    /// <summary>
    /// 아이템을 제작한다.
    /// </summary>
    private void MakeItem()
    {
        var metaData = InventorySystem.Inst.GetMetaData(selectedType);
        var needItems = metaData.ItemsToCreate;
        foreach(var item in needItems)
        {
            InventorySystem.Inst.UseItem(item.Type, item.Count);
        }

        // 아이템 제작
        InventorySystem.Inst.AddItem(selectedType, metaData.CountPerCraft);
        Debug.Log($"[InventoryUI] 아이템 제작 완료 |  아이템: {selectedType}");
    }

    /// <summary>
    /// 필요 아이템 표시기를 초기화 한다.
    /// </summary>
    private void ResetItemViewer()
    {
        decomposeItemData.Clear();
        decomposeItemText.Clear();
        needItemData.Clear();
        needItemText.Clear();
        foreach(var viwer in itemViewerList)
        {
            viwer.SetActive(false);
        }
    }

    /// <summary>
    /// 현재 활성화 된 아이템 표시기를 초기화 한다.
    /// </summary>
    /// <param name="type"></param>
    private void UpdateNeedItemData(RewardCurrencyType type)
    {
        if(needItemData.Count == 0 || currentContent != ContentType.Craft)
        {
            return;
        }
        if(needItemData.ContainsKey(type))
        {
            var needData = needItemData[type];
            bool hadItemEnough = InventorySystem.Inst.GetCount(type) > needData.Count;
            needItemText[type].text = needData.Count.ToString() + "/" + InventorySystem.Inst.GetCountString(type);

            // 개수가 부족하면 빨간색으로 표시한다.
            needItemText[type].color = hadItemEnough ? Color.white : Color.softRed;

            bool itemEnough = true;
            foreach (var need in needItemData)
            {
                if (need.Value.Count > InventorySystem.Inst.GetCount(need.Key))
                {
                    itemEnough = false;
                }
            }

            // 필요 아이템 중에 하나라도 부족하면 제작 버튼 비활성화
            SetTextButtonEnable(makeButton, makeButtonEvent, makeButtonText, itemEnough);
        }
    }

    /// <summary>
    /// 현재 활성화 된 아이템 표시기를 초기화 한다
    /// </summary>
    /// <param name="type"></param>
    private void UpdateDecomposeItemData(RewardCurrencyType type)
    {
        if (decomposeItemData.Count == 0)
        {
            return;
        }
        if (decomposeItemData.ContainsKey(type))
        {
            var decompData = decomposeItemData[type];
            if(decompData.Min == decompData.Max)
            {
                decomposeItemText[type].text = " + " + decompData.Min.ToString();
            }
            else
            {
                decomposeItemText[type].text = " + " + decompData.Min.ToString() + " ~ " + decompData.Max.ToString();
            }
            decomposeItemText[type].color = Color.white;
        }

        // 아이템을 소지하고 있지 않다면 분해 버튼을 비활성화 한다.
        SetTextButtonEnable(decomposeButton, decomposeButtonEvent, decomposeButtonText, InventorySystem.Inst.HasItem(type));
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

    /// <summary>
    /// 이미지의 밝기를 조정한다. // brightness가 높을수록 밝아진다.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="opacity"></param>
    private void SetImageBrightness(Image image, float brightness)
    {
        var color = image.color;
        color.r = brightness;
        color.g = brightness;
        color.b = brightness;
        image.color = color;
    }

    /// <summary>
    /// 아이템 정보 텍스트를 설정한다.
    /// </summary>
    private void SetInfoText(ItemMetaDataSo metaData)
    {
        itemNameText.text = metaData.Name;
        itemInfoText.text = metaData.InfoText;
        SetItemInfoCountText(metaData.Type);
    }

    /// <summary>
    /// 아이템 정보 이미지를 설정한다.
    /// </summary>
    private void SetInfoImage(ItemMetaDataSo metaData)
    {
        itemInfoImage.sprite = metaData.ItemImage;
        SetImageVisibility(itemInfoImage, true);
    }

    /// <summary>
    /// 아이템 정보란을 초기화 한다.
    /// </summary>
    private void ResetInfoArea()
    {
        itemNameText.text = "";
        itemInfoText.text = "";
        SetItemInfoCountText(0, true);
        SetImageVisibility(itemInfoImage, false);
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
    /// 스크롤을 초기화 한다.
    /// </summary>
    private void ResetScroll(GameObject content)
    {
        scrollRect.content = content.GetComponent<RectTransform>();
        scrollRect.verticalNormalizedPosition = 1f;
        scrollRect.velocity = Vector2.zero;
    }

    /// <summary>
    /// 모든 셀 선택을 초기화 한다.
    /// </summary>
    private void ResetCellSelectionAll()
    {
        if (selectedCell)
        {
            SetButtonColor(selectedCell, originCellColor);
        }
        selectedCell = null;
    }

    /// <summary>
    /// 아이템 정보란의 보유량 텍스트를 설정한다.
    /// </summary>
    /// <param name="type"></param>
    private void SetItemInfoCountText(RewardCurrencyType type, bool resetFlag=false)
    {
        if(resetFlag)
        {
            itemCountText.text = "";
            return;
        }
        var count = InventorySystem.Inst.GetCount(type);
        bool hasItem = count > 0;
        itemCountText.text = "보유량: " + InventorySystem.Inst.GetCountString(type);

        // 보유하지 않으면 빨간색으로 표시한다.
        itemCountText.color = hasItem ? Color.white : Color.softRed;
    }

    /// <summary>
    /// 인벤토리 상태로 설정한다.
    /// </summary>
    private void SetToInventoryTab()
    {
        SelectContent(ContentType.Inventory, "창고");
    }

    /// <summary>
    /// 크래프트 상태로 전환한다.
    /// </summary>
    private void SetToCraftTab()
    {
        SelectContent(ContentType.Craft, "제작");
    }
    
    /// <summary>
    /// 분해 상태로 전환한다.
    /// </summary>
    private void SetToDecomposeTab()
    {
        SelectContent(ContentType.Decompose, "분해");
    }

    /// <summary>
    /// 컨텐츠 객체를 얻는다.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private GameObject GetContentObject(ContentType type)
    {
        return contentDict[type].Content;
    }

    /// <summary>
    /// 메인 컨텐츠 객체를 설정한다.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="pannelTitle"></param>
    private void SelectContent(ContentType type, string pannelTitle)
    {
        foreach (var c in contentList) // 이전의 탭 컬러를 복원하고 선택한 탭 컬러를 변경한다.
        {
            c.Content.SetActive(false);
            SetButtonColor(c.TabButton, Color.black);
            if(c.FunctionButton != null)
            {
                c.FunctionButton.gameObject.SetActive(false);
            }
        }
        var selected = contentDict[type];
        var selectedContent = selected.Content;
        var selectedTabButton = selected.TabButton;
        var selectedFunctionButton = selected.FunctionButton;

        selectedContent.SetActive(true);
        ResetScroll(selectedContent);
        SetButtonColor(selectedTabButton, selectedTabColor);
        if(selectedFunctionButton != null)
        {
            selectedFunctionButton.gameObject.SetActive(true);
        }

        ResetInfoArea();
        ResetCellSelectionAll();
        ResetItemViewer();

        pannelTitletext.text = pannelTitle;
        currentContent = type;

        SetTextButtonEnable(makeButton, makeButtonEvent, makeButtonText, false);
        SetTextButtonEnable(decomposeButton, decomposeButtonEvent, decomposeButtonText, false);

        UpdateCells();
    }

    private void UpdateCells()
    {
        if(currentContent == ContentType.Craft) //크래프트 셀은 보유량을 표시하지 않기 때문에 생략한다.
        {
            return;
        }

        // 보유하고 있지 않은 아이템은 어둡게 처리한다.
        // 보유하고 있지 않은 아이템은 보유량 텍스트를 빨강색으로 표시한다.
        // 보유량 텍스트를 업데이트 한다.
        var cellData = cellDict[currentContent].Cell;
        foreach (var cell in cellData)
        {
            bool hasItem = InventorySystem.Inst.HasItem(cell.Value.Type);
            SetImageBrightness(cell.Value.CellImage, hasItem ? HAS_ITEM_BRIGHTNESS : NO_ITEM_BRIGHTNESS);
            cell.Value.CellCountText.text = InventorySystem.Inst.GetCountString(cell.Value.Type);
            cell.Value.CellCountText.color = hasItem ? Color.white : Color.softRed;
        }
    }
}
