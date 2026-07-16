using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Inst;
    public CanvasGroup[] GamePlayUI;
    public GameObject[] WorldUI;

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

    /// <summary>
    /// 월드 UI를 숨긴다.
    /// </summary>
    public void HideWorldUI()
    {
        foreach(var o in WorldUI)
        {
            var colliders = o.GetComponentsInChildren<Collider>();
            if(colliders.Length > 0)
            {
                foreach(var c in colliders)
                {
                    c.enabled = false;
                }
            }
            var renderers = o.GetComponentsInChildren<Renderer>();
            if(renderers.Length > 0)
            {
                foreach(var r in renderers)
                {
                    r.enabled = false;
                }
            }
        }
    }

    /// <summary>
    /// 월드 UI를 복원한다.
    /// </summary>
    public void RevertWorldUI()
    {
        foreach(var o in WorldUI)
        {
            var colliders = o.GetComponentsInChildren<Collider>();
            if(colliders.Length > 0)
            {
                foreach(var c in colliders)
                {
                    c.enabled = true;
                }
            }
            var renderers = o.GetComponentsInChildren<Renderer>();
            if(renderers.Length > 0)
            {
                foreach(var r in renderers)
                {
                    r.enabled = true;
                }
            }
        }
    }
    
    /// <summary>
    /// 인게임 UI와 월드 UI를 모두 숨긴다.
    /// </summary>
    public void HideAll()
    {
        HideGameUI();
        HideWorldUI();
    }

    /// <summary>
    /// 인게임 UI와 월드 UI를 모드 복원한다.
    /// </summary>
    public void RevertAll()
    {
        RevertGameUI();
        RevertWorldUI();
    }
}
