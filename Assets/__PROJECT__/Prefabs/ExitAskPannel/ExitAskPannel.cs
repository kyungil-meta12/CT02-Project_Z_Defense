using UnityEngine;

public class ExitAskPannel : TouchBackHandler
{
    public CanvasGroup group;

    void Awake()
    {
        OnTouchBackAction += DisableAskPannel;
    }

    void Update()
    {
        UpdateTouchBackHandler();
    }

    public void DisableAskPannel()
    {
        group.alpha = 1f;
        group.interactable = true;
        gameObject.SetActive(false);
    }
}
