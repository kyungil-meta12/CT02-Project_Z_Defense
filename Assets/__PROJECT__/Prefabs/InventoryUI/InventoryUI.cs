using IncrementalLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OwnItemCell
{
    public Button ButtonCell;
    public TextMeshProUGUI CountText;
}

public class InventoryUI : MonoBehaviour
{
    [Header("인벤토리 UI의 메인 조작부")] public GameObject mainController;
    [Header("인벤토리 UI의 스크롤 조작부")] public ScrollRect scrollRect;
    [Header("인벤토리 컨텐츠 객체")] public GameObject inventoryContent;
    [Header("크래프팅 컨텐츠 객체")] public GameObject craftContent;
    [Header("인벤토리 아이템 버튼 프리펩")] public GameObject inventoryCellPrefab;
    [Header("크래프트 아이템 버튼 프리펩")] public GameObject craftCellPrefab;
    [Header("크래프트 필요 아이템 표시기 프리펩")] public GameObject craftCellNeedInfoPrefab;
    [Header("인벤토리 탭 버튼")] public Button invenTabButton;
    [Header("크래프트 탭 버튼")] public Button craftTabButton;
    [Header("아이템 보유량 텍스트 객체")] public TextMeshProUGUI itemCountText;
    [Header("아이템 이름 텍스트 객체")] public TextMeshProUGUI itemNameText;
    [Header("아이템 정보 텍스트 객체")] public TextMeshProUGUI itemInfoText;
    [Header("아이템 정보 이미지")] public Image itemInfoImage;
    [Header("조합 아이템 표시기 객체 목록")] public GameObject[] needItemViewerList;
    [Header("패널 텍스트 객체")] public TextMeshProUGUI pannelTitletext;
    [Header("제작 버튼")] public Button makeButton;
    [Header("배경 객체")] public Image background;
    [Header("선택된 탭 색상")] public Color selectedTabColor;
    [Header("선택된 셀 색상")] public Color selectedCellColor;
    [Header("절전 전환 버튼")] public GameObject powerSavingSwitchButton;

    // 현재 보유하고 있는 아이템 타입
    private Dictionary<RewardCurrencyType, OwnItemCell> ownTypes = new();

    // 인벤토리 버튼 데이터
    // invenButtonList의 참조를 invenButtonDict에 저장
    // 나머지는 리스트를 통해 직접 참조
    private Dictionary<Button, RewardCurrencyType> invenButtonDict = new();
    private List<Button> invenButtonList = new();
    private List<Image> invenImageList = new();
    private List<TextMeshProUGUI> invenCountTextList = new();

    // 크래프트 버튼 데이터
    private Dictionary<Button, RewardCurrencyType> craftButtonDict = new();
    
    // 현재 선택된 크래프트 아이템을 제작하는데에 필요한 아이템 관련 데이터 딕셔너리
    private Dictionary<RewardCurrencyType, ItemMaterialData> needItemData = new();
    private Dictionary<RewardCurrencyType, TextMeshProUGUI> needItemText = new();

    // 마지막으로 선택된 크래프트 아이템 타입
    private RewardCurrencyType latestSelectedCraftType = 0;
    private Button latestSelectedCraftCell;

    // 마지막으로 선택된 인벤토리 아이템 타입
    private Button latestSelectedInvenCell;

    // 원본 셀 버튼 색상
    private Color originCellColor;

    // 열려있는가?
    private bool openState = false;


    // 제작 버튼을 누르고 있는 상태
    private bool makeButtonPressState = false;

    // 제작 버튼을 오래 눌러 자동 제작되는 상태
    private bool autoMakeState = false;

    // 제작 버튼을 누르고 있을 떄 누적되는 시간 -> 자동 제작 진입
    private float autoMakeEnterAccumTime = 0f;

    // 제작 버튼을 누르고 있을 때 누적되는 시간 -> 자동 제작 실행
    private float autoMakeAccumTime = 0f;

    // 제작 버튼을 얼마나 누르고 있어야 자동 제작으로 넘어가는지?
    [Header("자동 제작 진입 시간")] public float autoMakeEnterTime;

    // 제작 버튼을 누르고 있을 떄 자동으로 제작되는 간격
    [Header("자동 제작 간격")] public float autoMakeInterval;


    // 인벤토리를 닫은 상태로 시작
    void Awake()
    {
        // 아이템 타입 종류 만큼 인벤토리 셀을 생성한다.
        int typeCount = InventorySystem.Inst.Types.Length;
        for (int i = 0; i < typeCount; i++)
        {
            var button = Instantiate(inventoryCellPrefab, inventoryContent.transform, false).GetComponent<Button>();
            var imageComp = button.transform.Find("ItemImage").GetComponent<Image>();
            var textComp = button.transform.Find("ItemCount").GetComponent<TextMeshProUGUI>();

            textComp.text = "";
            SetImageVisibility(imageComp, false);
            invenImageList.Add(imageComp);
            invenCountTextList.Add(textComp);

            // 상호작용을 비활성화 한다.
            button.interactable = false;

            // 클릭 이벤트 추가
            button.GetComponent<PassEventToScrollRect>().onButtonClick.AddListener(() => OnInventoryCellClick(button));

            // 버튼 목록에 추가
            invenButtonDict.Add(button, 0);
            invenButtonList.Add(button);
        }

        // 아이템 중에서 제작 가능한 아이템 종류 개수 만큼 크래프트 셀을 생성한다.
        foreach (RewardCurrencyType type in InventorySystem.Inst.Types)
        {
            var itemData = InventorySystem.Inst.GetMetaData(type);
            if (itemData.Craftable)
            {
                var button = Instantiate(craftCellPrefab, craftContent.transform, false).GetComponent<Button>();
                var imageComp = button.transform.Find("ItemImage").GetComponent<Image>();
                var textComp = button.transform.Find("ItemName").GetComponent<TextMeshProUGUI>();
            
                imageComp.sprite = itemData.ItemImage;
                textComp.text = itemData.Name;
                SetImageVisibility(imageComp, true);

                // 클릭 이벤트 추가
                button.GetComponent<PassEventToScrollRect>().onButtonClick.AddListener(() => OnCraftCellClick(button));

                // 크래프트 버튼 목록에 추가
                craftButtonDict.Add(button, type);
            }
        }

        // 제작 아이템 표시기 초기화
        ResetNeedItemViewer();

        // 기본 셀 버튼 컬러 얻기
        originCellColor = inventoryCellPrefab.GetComponent<Button>().colors.normalColor;

        OnCloseInventory();
    }

    void Start()
    {
        InventorySystem.Inst.OnItemCountChange += OnItemValueChanged;
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
        // 제작 버튼을 꾹 누르고 있으면 자동으로 제작이 된다.
        if(makeButtonPressState)
        {
            autoMakeEnterAccumTime += Time.deltaTime;
            if(autoMakeEnterAccumTime >= autoMakeEnterTime - autoMakeInterval)
            {
                autoMakeState = true;
            }
            if(autoMakeState)
            {
                autoMakeAccumTime += Time.deltaTime;
                if (autoMakeAccumTime >= autoMakeInterval)
                {
                    autoMakeAccumTime -= autoMakeInterval;
                    MakeItem();
                }
            }
        }
    }

    // 아이템 개수가 변경 될 때마다 아이템에 해당하는 인덱스의 정보를 업데이트 한다.
    public void OnItemValueChanged(ItemData data, Incremental prev)
    {
        if (!openState)
        {
            return;
        }

        if (ownTypes.ContainsKey(data.Type) && data.Count > 0)
        {
            var cellData = ownTypes[data.Type];
            cellData.CountText.text = InventorySystem.Inst.GetCountString(data.Type);
        }
        else
        {
            RefreshInventoryCell();
        }

        UpdateNeedItemData(data.Type);
    }

    /// <summary>
    /// 인벤토리를 열 때 호출하는 메서드
    /// </summary>
    public void OnOpenInventory()
    {
        mainController.SetActive(true);
        background.gameObject.SetActive(true);
        RefreshInventoryCell();
        SetToInventory();
        powerSavingSwitchButton.SetActive(false);
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
    /// 아이템 클릭 이벤트 // 클릭 시 버튼에 해당하는 타입에 해당하는 메타데이터에 있는 이름과 정보를 불러온다.
    /// </summary>
    /// <param name="button"></param>
    public void OnInventoryCellClick(Button button)
    {
        SetInfoText(invenButtonDict, button);
        SetInfoImage(invenButtonDict, button);
        if (latestSelectedInvenCell && latestSelectedInvenCell != button)
        {
            SetButtonColor(latestSelectedInvenCell, originCellColor);
        }
        SetButtonColor(button, selectedCellColor);
        latestSelectedInvenCell = button;
    }

    /// <summary>
    /// 크래프트 아이템 클릭 이벤트 // 클릭 시 버튼에 해당하는 타입에 해당하는 메타데이터에 있는 이름과 정보를 불러온다.
    /// </summary>
    /// <param name="button"></param>
    public void OnCraftCellClick(Button button)
    {
        SetInfoText(craftButtonDict, button);
        SetInfoImage(craftButtonDict, button);
        latestSelectedCraftType = craftButtonDict[button];
        if (latestSelectedCraftCell && latestSelectedCraftCell != button)
        {
            SetButtonColor(latestSelectedCraftCell, originCellColor);
        }
        SetButtonColor(button, selectedCellColor);
        latestSelectedCraftCell = button;

        ResetNeedItemViewer();
        
        // 필요한 아이템들에 대해서만 뷰어 활성화
        var metaData = InventorySystem.Inst.GetMetaData(latestSelectedCraftType);
        var needItems = metaData.ItemsToCreate;
        for(int i = 0; i < needItems.Count; i ++)
        {
            needItemViewerList[i].SetActive(true);
            var image = needItemViewerList[i].GetComponentInChildren<Image>();
            var text = needItemViewerList[i].GetComponentInChildren<TextMeshProUGUI>();
            image.sprite = InventorySystem.Inst.GetMetaData(needItems[i].Type).ItemImage;
            SetImageVisibility(image, true);

            // 각 필요 아이템에 대해서도 실시간으로 보유량을 표시하기 위해 딕셔너리에 데이터 추가 후 반영
            needItemData.Add(needItems[i].Type, needItems[i]);
            needItemText.Add(needItems[i].Type, text);
            UpdateNeedItemData(needItems[i].Type);
        }

        // 한 번 크래프트 아이템 셀을 터치하면 작업 버튼이 다시 활성화 된다.
        makeButton.gameObject.SetActive(true);
    }

    /// <summary>
    /// 인벤토리 창을 활성화 한다.
    /// </summary>
    public void OnInventoryTabClick()
    {
        if (!inventoryContent.activeInHierarchy)
        {
            SetToInventory();
        }
    }

    /// <summary>
    /// 작업 창을 활성화 한다.
    /// </summary>
    public void OnCraftTabClick()
    {
        if (!craftContent.activeInHierarchy)
        {
            SetToCraft();
        }
    }

    public void OnMakeButtonDown()
    {
        makeButtonPressState = true;
    }

    public void OnMakeButtonUp()
    {
        makeButtonPressState = false;

        if(autoMakeState) // 자동 제작 상태였다면 그냥 리턴
        {
            autoMakeState = false; // 자동 제작 상태 초기화
            autoMakeEnterAccumTime = 0f;
            autoMakeAccumTime = 0f;
            return;
        }

        MakeItem();
    }

    /// <summary>
    /// 아이템을 제작한다.
    /// </summary>
    private void MakeItem()
    {
        if (!latestSelectedCraftCell)
        {
            Debug.LogWarning("[InventoryUI] 아이템이 선택되지 않음");
            WarningPopupManager.ShowWarningForDuration("선택된 아이템이 없습니다.", 1f);
            return;
        }
        var needItems = InventorySystem.Inst.GetMetaData(latestSelectedCraftType).ItemsToCreate;
        foreach (var item in needItems)
        {
            if (InventorySystem.Inst.CanUseItem(item.Type, item.Count))
            {
                InventorySystem.Inst.UseItem(item.Type, item.Count);
            }
            else
            {
                Debug.LogWarning($"[InvectoryUI] 아이템이 부족하여 제작할 수 없음 | 부족한 아이템: {item.Type} | 필요 개수: {item.Count} | 현재 개수: {InventorySystem.Inst.GetCount(item.Type)}");
                WarningPopupManager.ShowWarningForDuration("아이템 보유량이 부족합니다.", 1f);
                return;
            }
        }

        Debug.Log($"[InventoryUI] 아이템 제작 완료 |  아이템: {latestSelectedCraftType}");
    }

    /// <summary>
    /// 필요 아이템 표시기를 초기화 한다.
    /// </summary>
    private void ResetNeedItemViewer()
    {
        needItemData.Clear();
        needItemText.Clear();
        foreach(var viwer in needItemViewerList)
        {
            viwer.SetActive(false);
        }
    }

    /// <summary>
    /// 현재 활성화 된 필요 아이템 표시기를 초기화 한다.
    /// </summary>
    /// <param name="type"></param>
    private void UpdateNeedItemData(RewardCurrencyType type)
    {
        if(needItemData.Count == 0)
        {
            return;
        }

        if(needItemData.ContainsKey(type))
        {
            var needData = needItemData[type];
            needItemText[type].text = needData.Count.ToString() + "/" + InventorySystem.Inst.GetCountString(type);
        }
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
    /// 아이템 정보 텍스트를 설정한다. // null 전달 시 텍스트를 비운다.
    /// </summary>
    /// <param name="button"></param>
    private void SetInfoText(Dictionary<Button, RewardCurrencyType> dict, Button button)
    {
        if (button == null || dict == null)
        {
            itemNameText.text = "";
            itemInfoText.text = "";
            itemCountText.text = "";
        }
        else
        {
            var type = dict[button];
            var metaData = InventorySystem.Inst.GetMetaData(type);
            itemNameText.text = metaData.Name;
            itemInfoText.text = metaData.InfoText;
            itemCountText.text = "보유량: " + InventorySystem.Inst.GetCountString(type);
        }
    }

    /// <summary>
    /// 아이템 정보 이미지를 설정한다. // null 전달 시 이미지를 숨긴다.
    /// </summary>
    /// <param name="dict"></param>
    /// <param name="button"></param>
    private void SetInfoImage(Dictionary<Button, RewardCurrencyType> dict, Button button)
    {
        SetImageVisibility(itemInfoImage, dict != null && button != null);
        if (dict != null && button != null)
        {
            itemInfoImage.sprite = InventorySystem.Inst.GetMetaData(dict[button]).ItemImage;
        }
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
        if (latestSelectedCraftCell)
        {
            SetButtonColor(latestSelectedCraftCell, originCellColor);
        }
        latestSelectedCraftCell = null;

        if (latestSelectedInvenCell)
        {
            SetButtonColor(latestSelectedInvenCell, originCellColor);
        }
        latestSelectedInvenCell = null;
    }

    /// <summary>
    /// 인벤토리 상태로 설정한다.
    /// </summary>
    private void SetToInventory()
    {
        inventoryContent.SetActive(true);
        craftContent.SetActive(false);
        SetInfoText(null, null);
        SetInfoImage(null, null);
        SetButtonColor(invenTabButton, selectedTabColor);
        SetButtonColor(craftTabButton, Color.black);
        pannelTitletext.text = "창고";
        ResetScroll(inventoryContent);
        ResetCellSelectionAll();
        ResetNeedItemViewer();
        makeButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// 크래프트 상태로 전환한다.
    /// </summary>
    private void SetToCraft()
    {
        inventoryContent.SetActive(false);
        craftContent.SetActive(true);
        SetInfoText(null, null);
        SetInfoImage(null, null);
        SetButtonColor(invenTabButton, Color.black);
        SetButtonColor(craftTabButton, selectedTabColor);
        pannelTitletext.text = "아이템 제작";
        ResetScroll(craftContent);
        ResetCellSelectionAll();
        ResetNeedItemViewer();
    }

    // 인벤토리 셀을 새로고침 한다.
    private void RefreshInventoryCell()
    {
        ownTypes.Clear();

        foreach (RewardCurrencyType type in InventorySystem.Inst.Types)
        {
            if (InventorySystem.Inst.HasItem(type)) // 가지고 있는 아이템 타입별로 빈 값을 딕셔너리에 추가
            {
                ownTypes.Add(type, new OwnItemCell { ButtonCell = null, CountText = null });
            }
        }

        // 모든 인벤토리 버튼 및 이미지 비활성화
        for (int i = 0; i < InventorySystem.Inst.Types.Length; i++)
        {
            invenButtonList[i].interactable = false;
            invenCountTextList[i].text = "";
            SetImageVisibility(invenImageList[i], false);
        }

        int btIndex = 0;

        // 존재하는 아이템들을 왼쪽 상단부터 순서대로 버튼에 배치한다.
        foreach (var ownType in ownTypes)
        {
            var type = ownType.Key;
            var cell = ownType.Value;

            // 버튼 세팅
            invenButtonList[btIndex].interactable = true;
            invenCountTextList[btIndex].text = InventorySystem.Inst.GetCountString(type);
            invenImageList[btIndex].sprite = InventorySystem.Inst.GetMetaData(type).ItemImage;
            SetImageVisibility(invenImageList[btIndex], true);

            // 보유 타입 딕셔너리 업데이트
            cell.ButtonCell = invenButtonList[btIndex];
            cell.CountText = invenCountTextList[btIndex];

            // 인벤토리 버튼 딕셔너리 업데이트
            invenButtonDict[invenButtonList[btIndex]] = type;

            btIndex++;
        }
    }
}
