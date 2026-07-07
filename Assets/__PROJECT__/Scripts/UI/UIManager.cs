using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Inst;
    public CanvasGroup[] GamePlayUI;

    void Awake()
    {
        if(Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
            return;
        }

        Inst = this;
    }

    void OnDestroy()
    {
        Inst = null;
    }

    /// <summary>
    /// 인게임 UI를 숨긴다.
    /// </summary>
    public void HideGameUI()
    {
        foreach(var renderer in GamePlayUI)
        {
            renderer.alpha = 0f;
            renderer.interactable = false;
        }
    }

    /// <summary>
    /// 인게임 UI를 복원한다.
    /// </summary>
    public void RevertGameUI()
    {
        foreach (var renderer in GamePlayUI)
        {
            renderer.alpha = 1f;
            renderer.interactable = true;
        }
    }
}
