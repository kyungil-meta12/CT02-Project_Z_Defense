using IncrementalLib;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public GameObject mainController;
    public Image background;
    public List<Button> buttons;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemInfoText;

    private List<Image> images = new();
    private List<TextMeshProUGUI> texts = new();
    private ItemMetaDataListSo metaDataListSo;
    private List<ItemMetaDataSo> metaDataList = new();
    private int activatedImageCount;
    private bool openState = false;

    // 인벤토리를 닫은 상태로 시작
    void Start()
    {
        InventorySystem.Inst.OnItemCountChange += OnItemValueChanged;

        // 각 버튼이 가지는 아이템 이미지 컴포넌트를 가져와 일단은 빈 이미지로 채운다.
        foreach (var bt in buttons)
        {
            var imgComp = bt.transform.Find("ItemImage").GetComponent<Image>();
            var txtComp = bt.transform.Find("CountText").GetComponent<TextMeshProUGUI>();
            txtComp.text = "";
            SetImageVisibility(imgComp, false);
            images.Add(imgComp);
            texts.Add(txtComp);
            // 일단은 상호작용을 비활성화 한다.
            bt.interactable = false;
        }

        // 인벤토리로부터 메타데이터 목록을 가져온다.
        metaDataListSo = InventorySystem.Inst.itemMetaDataListSo;
        // 아이템 메타데이터 리스트를 불러온다.
        metaDataList = metaDataListSo.MetaDataList.ToList();

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
            images[index].sprite = metaDataList.Find(meta => meta.Data.Type == data.Type).Data.ItemImage;
        }
        texts[index].text = data.CountString;
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
                images[currentIndex].sprite = metaDataList.Find(meta => meta.Data.Type == type).Data.ItemImage;
                SetImageVisibility(images[currentIndex], true);
                texts[currentIndex].text = InventorySystem.Inst.GetCountString(type);
                currentIndex++;
            }
        }

        // 활성화된 이미지 개수 캐싱
        activatedImageCount = currentIndex + 1;

        itemNameText.text = "";
        itemInfoText.text = "";

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
    public void OnButtonClick(Button button)
    {
        var index = buttons.IndexOf(button);
        var type = (RewardCurrencyType)index;
        var metaData = metaDataList.Find(meta => meta.Data.Type == type);
        itemNameText.text = metaData.Data.Name;
        itemInfoText.text = metaData.Data.InfoText;
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
