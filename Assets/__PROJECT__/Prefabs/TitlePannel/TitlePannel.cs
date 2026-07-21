using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TitlePannel : TouchBackHandler
{
    public ZombieSpawner spawner;
    public Image logoImage;
    public ExitAskPannel pannel;

    private float sinValue = 0f;
    private float sizeSinValue = 0f;
    private TextMeshProUGUI text;
    private CanvasGroup canvasGroup;
    private RectTransform rt;
    private Vector2 originImageSize;
    private bool disappearState = false;
    private bool firstFrame = false;

    void Awake() {
        text = GetComponentInChildren<TextMeshProUGUI>();
        canvasGroup = GetComponent<CanvasGroup>();
        rt = logoImage.rectTransform;
        originImageSize = rt.localScale;

        OnTouchBackAction += OnTouchBack;
    }

    void OnTouchBack()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        pannel.gameObject.SetActive(true);
        pannel.SetLatestCanvasGroup(canvasGroup);
    }

    void Start()
    {
        spawner.gameObject.SetActive(GameManager.Inst.Wave > 1); // 웨이브가 1인 상태로 최초 실행할 경우 스포너를 잠시 중단한다.
    }

    void Update()
    {
        if(!firstFrame)
        {
            UIManager.Inst.HideAll();
            firstFrame = true;
        }
        // 텍스트 색상 부드럽게 점멸
        sinValue += Time.deltaTime * 2f;
        var opacity = Mathf.Abs(Mathf.Sin(sinValue));
        var textColor = text.color;
        textColor.a = opacity;
        text.color = textColor;

        // 이미지가 커졌다 작아졌다를 반복한다.
        sizeSinValue += Time.deltaTime;
        var sizeOffset = Mathf.Sin(sizeSinValue) * 0.1f;
        rt.localScale = originImageSize + new Vector2(sizeOffset, sizeOffset);

        
        // 부드럽게 사라진 후 완전히 사라지면 비활성화 된다.
        if(disappearState)
        {
            canvasGroup.alpha -= Time.deltaTime * 2f;
            if(canvasGroup.alpha <= 0f)
            {
                UIManager.Inst.RevertAll();
                spawner.gameObject.SetActive(true);
                Destroy(gameObject);
            }
        }
        else
        {
            if(canvasGroup.interactable)
            {
                UpdateTouchBackHandler();
            }
        }
    }

    public void OnTouchScreen()
    {
        disappearState = true;
        UISoundPlayer.Inst.PlayDefaultClick();
    }
}
