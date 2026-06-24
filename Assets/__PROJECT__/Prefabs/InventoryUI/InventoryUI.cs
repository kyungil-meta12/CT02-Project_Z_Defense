using IncrementalLib;
using System.Collections.Generic;
using TMPro;
using Unity.Android.Gradle.Manifest;
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
    [Header("아이템 이름 텍스트 객체")] public TextMeshProUGUI itemNameText;
    [Header("아이템 정보 텍스트 객체")] public TextMeshProUGUI itemInfoText;
    [Header("아이템 정보 이미지")] public Image itemInfoImage;
    [Header("패널 텍스트 객체")] public TextMeshProUGUI pannelTitletext;
    [Header("제작 버튼")] public Button makeButton;
    [Header("배경 객체")] public Image background;

    // 메타데이터 목록
    private List<ItemMetaDataSo> metaDataList = new();

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
    private List<Image> craftImageList = new();
    private List<TextMeshProUGUI> craftTextList = new();
    private Dictionary<RewardCurrencyType, TextMeshProUGUI> craftCountTextList = new();



    // 열려있는가?
    private bool openState = false;


    // 인벤토리를 닫은 상태로 시작
    void Awake()
    {

        // 아이템 메타데이터 리스트를 불러온다.
        metaDataList = InventorySystem.Inst.itemMetaDataListSo.MetaDataList;

        // 아이템 타입 종류 만큼 인벤토리 셀을 생성한다.
        int typeCount = InventorySystem.Inst.Types.Length;
        for(int i = 0; i < typeCount; i ++)
        {
            var button = Instantiate(inventoryCellPrefab, inventoryContent.transform, false).GetComponent<Button>();
            var imageComp = button.transform.Find("ItemImage").GetComponent<Image>();
            var textComp = button.transform.Find("CountText").GetComponent<TextMeshProUGUI>();

            textComp.text = "";
            SetImageVisibility(imageComp, false);
            invenImageList.Add(imageComp);
            invenCountTextList.Add(textComp);

            // 상호작용을 비활성화 한다.
            button.interactable = false;

            // 클릭 이벤트 추가
            button.onClick.AddListener(() => OnInventoryCellClick(button));

            // 버튼 목록에 추가
            invenButtonDict.Add(button, 0);
            invenButtonList.Add(button);
        }

        // 아이템 중에서 제작 가능한 아이템 종류 개수 만큼 크래프트 셀을 생성한다.
        foreach(RewardCurrencyType type in InventorySystem.Inst.Types)
        {
            var itemData = metaDataList.Find(meta => meta.Type == type);
            if(itemData.Craftable)
            {
                var button = Instantiate(craftCellPrefab, craftContent.transform, false).GetComponent<Button>();
                var imageComp = button.transform.Find("ItemImage").GetComponent<Image>();
                var textComp = button.transform.Find("CountText").GetComponent<TextMeshProUGUI>();
                var countTextComp = button.transform.Find("OwnCountText").GetComponent<TextMeshProUGUI>();

                // 한 번 제작할 때 만들어지는 개수 표시
                textComp.text = "+" + itemData.CreateCount.ToString();
                imageComp.sprite = itemData.ItemImage;
                SetImageVisibility(imageComp, true);
                craftImageList.Add(imageComp);
                craftTextList.Add(textComp);

                // 제작에 필요한 아이템 정보를 가져온다.
                var needItemList = itemData.ItemsToCreate;

                // 현재 버튼에 필요한 아이템 표시기를 추가한다.
                foreach(var item in needItemList)
                {
                    var addLocation = button.transform.Find("NeedItems");
                    var needIndicator = Instantiate(craftCellNeedInfoPrefab, addLocation, false);
                    var needImage = needIndicator.transform.Find("ItemImage").GetComponent<Image>();
                    var needText = needIndicator.transform.Find("CountText").GetComponent<TextMeshProUGUI>();
                    needText.text = "0";
                    var needItemData = metaDataList.Find(meta => meta.Type == item.Type);
                    needImage.sprite = needItemData.ItemImage;
                    needText.text = item.Count.ToString();
                    SetImageVisibility(needImage, true);
                }

                // 클릭 이벤트 추가
                button.onClick.AddListener(() => OnCraftCellClick(button));

                // 크래프트 버튼 목록에 추가
                craftButtonDict.Add(button, type);

                // 크래프트 아이템 보유량 텍스트 목록에 추가
                craftCountTextList.Add(type, countTextComp);
            }
        }

        OnCloseInventory();
    }
 
    void Start()
    {
        InventorySystem.Inst.OnItemCountChange += OnItemValueChanged;
    }

    void OnDestroy()
    {
        if(InventorySystem.Inst)
        {
            InventorySystem.Inst.OnItemCountChange -= OnItemValueChanged;
        }
    }

    // 아이템 개수가 변경 될 때마다 아이템에 해당하는 인덱스의 정보를 업데이트 한다.
    public void OnItemValueChanged(ItemData data, Incremental prev)
    {
        if(!openState)
        {
            return;
        }

        if(ownTypes.ContainsKey(data.Type) && data.Count > 0)
        {
            var cellData = ownTypes[data.Type];
            cellData.CountText.text = InventorySystem.Inst.GetCountString(data.Type);
            // 작업 아이템 보유 텍스트도 같이 업데이트
            SetCraftItemOwnCountText(data.Type);
        }
        else
        {
            RefreshInventoryCell();
        }
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
        openState = true;
    }

    /// <summary>
    /// 인벤토리를 닫을 때 호출하는 메서드
    /// </summary>
    public void OnCloseInventory()
    {
        mainController.SetActive(false);
        background.gameObject.SetActive(false);
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
    }

    /// <summary>
    /// 크래프트 아이템 클릭 이벤트 // 클릭 시 버튼에 해당하는 타입에 해당하는 메타데이터에 있는 이름과 정보를 불러온다.
    /// </summary>
    /// <param name="button"></param>
    public void OnCraftCellClick(Button button)
    {
        SetInfoText(craftButtonDict, button);
        SetInfoImage(craftButtonDict, button);
    }

    /// <summary>
    /// 인벤토리 창을 활성화 한다.
    /// </summary>
    public void OnInventoryTabClick()
    {
        if(!inventoryContent.activeInHierarchy)
        {
            SetToInventory();
        }
    }

    /// <summary>
    /// 작업 창을 활성화 한다.
    /// </summary>
    public void OnCraftTabClick()
    {
        if(!craftContent.activeInHierarchy)
        {
            SetToCraft();
        }
    }

    public void OnMakeButtonClick()
    {
        print("[InventoryUI] 제작 버튼 눌림");
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
        if(button == null || dict == null)
        {
            itemNameText.text = "";
            itemInfoText.text = "";
        }
        else
        {
            var type = dict[button];
            var metaData = metaDataList.Find(meta => meta.Type == type);
            itemNameText.text = metaData.Name;
            itemInfoText.text = metaData.InfoText;
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
        if(dict != null && button != null)
        {
            itemInfoImage.sprite = metaDataList.Find(meta => meta.Type == dict[button]).ItemImage;
        }
    }

    private void SetCraftItemOwnCountText(RewardCurrencyType type)
    {
        if(craftCountTextList.ContainsKey(type))
        {
            craftCountTextList[type].text = InventorySystem.Inst.GetCountString(type);
        }
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
    /// 인벤토리 상태로 설정한다.
    /// </summary>
    private void SetToInventory()
    {
        inventoryContent.SetActive(true);
        craftContent.SetActive(false);
        SetInfoText(null, null);
        SetInfoImage(null, null);
        pannelTitletext.text = "Inventory";
        ResetScroll(inventoryContent);
        makeButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// 크래프트 상태로 전환한다.
    /// </summary>
    private void SetToCraft() {
        inventoryContent.SetActive(false);
        craftContent.SetActive(true);
        SetInfoText(null, null);
        SetInfoImage(null, null);
        pannelTitletext.text = "Craft";
        ResetScroll(craftContent);
        makeButton.gameObject.SetActive(true);
    }

    // 인벤토리 셀을 새로고침 한다.
    private void RefreshInventoryCell()
    {
        ownTypes.Clear();

        foreach(RewardCurrencyType type in InventorySystem.Inst.Types)
        {
            if(InventorySystem.Inst.HasItem(type)) // 가지고 있는 아이템 타입별로 빈 값을 딕셔너리에 추가
            {
                ownTypes.Add(type, new OwnItemCell{ ButtonCell = null, CountText = null });
            }

            // 작업 아이템 보유량 텍스트 업데이트
            SetCraftItemOwnCountText(type);
        }

        // 모든 인벤토리 버튼 및 이미지 비활성화
        for (int i = 0; i < InventorySystem.Inst.Types.Length; i ++)
        {
            invenButtonList[i].interactable = false;
            invenCountTextList[i].text = "";
            SetImageVisibility(invenImageList[i], false);
        }
        
        int btIndex = 0;

        // 존재하는 아이템들을 왼쪽 상단부터 순서대로 버튼에 배치한다.
        foreach(var ownType in ownTypes)
        {
            var type = ownType.Key;
            var cell = ownType.Value;

            // 버튼 세팅
            invenButtonList[btIndex].interactable = true;
            invenCountTextList[btIndex].text = InventorySystem.Inst.GetCountString(type);
            invenImageList[btIndex].sprite = metaDataList.Find(meta => meta.Type == type).ItemImage;
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
