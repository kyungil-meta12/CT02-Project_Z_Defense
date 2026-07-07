using UnityEngine;
using UnityEngine.UI;

public class MainMenuButton : TouchBackHandler
{
    public GameObject mainMenu;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponentInParent<CanvasGroup>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // 뒤로가기 누를 시 메인메뉴 활성화
        OnTouchBackAction += () =>
        {
            if(canvasGroup.interactable)
            {
                mainMenu.SetActive(true);
                UIManager.Inst.HideGameUI();
            }
        };
    }

    // Update is called once per frame
    void Update()
    {
        UpdateTouchBackHandler();
    }
}
