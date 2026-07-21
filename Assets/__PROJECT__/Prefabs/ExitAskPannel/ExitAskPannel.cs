using UnityEngine;

public class ExitAskPannel : TouchBackHandler
{
    private CanvasGroup latestCanvasGroup;

    void Awake()
    {
        OnTouchBackAction += DisableAskPannel;
    }

    void Update()
    {
        UpdateTouchBackHandler();
    }
    
    public void SetLatestCanvasGroup(CanvasGroup canvasGroup)
    {
        latestCanvasGroup = canvasGroup;
    }

    public void DisableAskPannel()
    {
        latestCanvasGroup.alpha = 1f;
        latestCanvasGroup.interactable = true;
        gameObject.SetActive(false);
    }
}
