using IncrementalLib;
using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("인벤토리 UI의 메인 조작부")] public GameObject mainController;
    [Header("인벤토리 UI의 스크롤 조작부")] public ScrollRect scrollRect;
    [Header("인벤토리 컨턴츠 객체")] public GameObject inventoryContent;
    [Header("크래프팅 컨텐츠 객체")] public GameObject craftContent;
    [Header("인벤토리 아이템 버튼 프리펩")] public GameObject inventoryCellPrefab;
    [Header("배경 객체")] public Image background;
    [Header("아이템 이름 텍스트 객체")] public TextMeshProUGUI itemNameText;
    [Header("아이템 정보 텍스트 객체")] public TextMeshProUGUI itemInfoText;
    [Header("패널 텍스트 객체")] public TextMeshProUGUI pannelTitletext;


    private ItemMetaDataListSo metaDataListSo;
    private List<ItemMetaDataSo> metaDataList = new();
    
    private List<Button> buttons = new();
    private List<Image> images = new();
    private List<TextMeshProUGUI> texts = new();
    private int activatedImageCount;
    private bool openState = false;

    // 인벤토리를 닫은 상태로 시작
    void Start()
    {
        InventorySystem.Inst.OnItemCountChange += OnItemValueChanged;

        // 인벤토리로부터 메타데이터 목록을 가져온다.
        metaDataListSo = InventorySystem.Inst.itemMetaDataListSo;
        // 아이템 메타데이터 리스트를 불러온다.
        metaDataList = metaDataListSo.MetaDataList;

        // 아이템 타입 종류 만큼 인벤토리 셀을 생성한다.
        int typeCount = InventorySystem.Inst.Types.Length;
        for(int i = 0; i < typeCount; i ++)
        {
            var obj = Instantiate(inventoryCellPrefab, inventoryContent.transform, false);
            var button = obj.GetComponent<Button>();

            var imgComp = button.transform.Find("ItemImage").GetComponent<Image>();
            var txtComp = button.transform.Find("CountText").GetComponent<TextMeshProUGUI>();

            txtComp.text = "";
            SetImageVisibility(imgComp, false);
            images.Add(imgComp);
            texts.Add(txtComp);
            // 일단은 상호작용을 비활성화 한다.
            button.interactable = false;

            // 클릭 이벤트 추가
            button.onClick.AddListener(() => OnItemCellClick(button));

            // 버튼 목록에 추가
            buttons.Add(button);
        }

        OnCloseInventory();
    }

    void OnDestory()
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

        var index = (int)data.Type;
        if (!buttons[index].interactable)
        {
            buttons[index].interactable = true;
            SetImageVisibility(images[index], true);
            images[index].sprite = metaDataList.Find(meta => meta.Type == data.Type).ItemImage;
        }
        
        else if(data.Count == 0)
        {
            buttons[index].interactable = false;
            SetImageVisibility(images[index], false);
            images[index].sprite = null;
        }
        texts[index].text = data.CountString;

        // 상호작용이 활성화 된 버튼 개수가 곧 활성화 된 이미지의 개수이다.
        int enabledCount = 0;
        foreach(var bt in buttons)
        {
            if(bt.interactable)
            {
                enabledCount++;
            }
        }
        activatedImageCount = enabledCount;
    }

    /// <summary>
    /// 인벤토리를 열 때 호출하는 메서드
    /// </summary>
    public void OnOpenInventory()
    {
        mainController.SetActive(true);
        background.gameObject.SetActive(true);

        // 이전에 활성화 되었던 이미지 및 버튼만 비활성화
        for (int i = 0; i < activatedImageCount; i ++)
        {
            buttons[i].interactable = false;
            SetImageVisibility(images[i], false);
            texts[i].text = "";
        }

        // 아이템 품목별로 존재하는지 확인한다. 존재하는 아이템은 좌측 상단부터 순서대로 아이템 이미지를 배치한다.
        // 아이템 이미지가 배치된 버튼은 상호작용이 활성화 된다.
        // 메타데이터 목록에서 타입에 해당하는 이미지를 찾아 해당 이미지로 교체한다.
        int currentIndex = 0;
        foreach(RewardCurrencyType type in InventorySystem.Inst.Types)
        {
            if(InventorySystem.Inst.HasItem(type))
            {
                buttons[currentIndex].interactable = true;
                images[currentIndex].sprite = metaDataList.Find(meta => meta.Type == type).ItemImage;
                SetImageVisibility(images[currentIndex], true);
                texts[currentIndex].text = InventorySystem.Inst.GetCountString(type);
                currentIndex++;
            }
        }

        // 활성화된 이미지 개수 캐싱
        activatedImageCount = currentIndex + 1;

        itemNameText.text = "";
        itemInfoText.text = "";

        inventoryContent.SetActive(true);
        craftContent.SetActive(false);
        scrollRect.verticalNormalizedPosition = 1f;
        scrollRect.velocity = Vector2.zero;
        pannelTitletext.text = "Inventory";

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
    public void OnItemCellClick(Button button)
    {
        var index = buttons.IndexOf(button);
        var type = (RewardCurrencyType)index;
        var metaData = metaDataList.Find(meta => meta.Type == type);
        itemNameText.text = metaData.Name;
        itemInfoText.text = metaData.InfoText;
    }

    /// <summary>
    /// 인벤토리 창을 활성화 한다.
    /// </summary>
    public void OnInventoryTabClick()
    {
        if(!inventoryContent.activeInHierarchy)
        {
            inventoryContent.SetActive(true);
            craftContent.SetActive(false);
            scrollRect.verticalNormalizedPosition = 1f;
            scrollRect.velocity = Vector2.zero;
            pannelTitletext.text = "Inventory";
            itemNameText.text = "";
            itemInfoText.text = "";
        }
    }

    /// <summary>
    /// 작업 창을 활성화 한다.
    /// </summary>
    public void OnCraftTabClick()
    {
        if(!craftContent.activeInHierarchy)
        {
            inventoryContent.SetActive(false);
            craftContent.SetActive(true);
            scrollRect.verticalNormalizedPosition = 1f;
            scrollRect.velocity = Vector2.zero;
            pannelTitletext.text = "Craft";
            itemNameText.text = "";
            itemInfoText.text = "";
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
}
