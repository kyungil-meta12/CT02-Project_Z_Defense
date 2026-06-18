using TMPro;
using UnityEngine;
using IncrementalLib;

/// <summary>
/// 아이템 주울 시 화면에 아이템 획득 표시를 띄우는 인디케이터
/// </summary>
public class ItemGrabIndicator : MonoBehaviour
{
    public TextMeshProUGUI text;
    private RectTransform rt;
    private Vector2 originPos;
    private Vector2 currentPos;
    private Color textColor;
    private float disappearTime = 0f;
     
    void Awake()
    {
        rt = text.GetComponent<RectTransform>();
        originPos = rt.localPosition;
        currentPos = originPos;
        textColor = text.color;
        textColor.a = 0f;
        text.color = textColor;
    }

    void Start()
    {
        InventorySystem.Inst.OnItemCountChange += OnGrabItem;
    }

    void Update()
    {
        disappearTime -= Time.deltaTime;
        disappearTime = Mathf.Clamp(disappearTime, 0f, 1f);
        currentPos = Vector2.Lerp(currentPos, originPos, Time.deltaTime * 5f);
        rt.localPosition = currentPos;

        // 인디케이터 활성화 후 1초가 지나면 사라지기 시작
        if (disappearTime <= 0f)
        {
            textColor.a -= Time.deltaTime;
            textColor.a = Mathf.Clamp(textColor.a, 0f, 1f);
        }
        else
        {
            textColor.a = Mathf.Lerp(textColor.a, 1f, Time.deltaTime * 5f);
        }
        text.color = textColor;
    }

    //아이템을 주우면 인디케이터 활성화
    public void OnGrabItem(ItemData item, Incremental prev)
    {
        // 코인은 표시하지 않는다.
        if(item.Type == RewardCurrencyType.Coin)
        {
            return;
        }
        currentPos.x = originPos.x - 50f;
        disappearTime = 1f;
        textColor.a = 0f;
        text.color = textColor;
        text.text = $"+ {item.Name} x " + prev.ToString();
    }
}
