using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerSavingSwitcher : MonoBehaviour
{
    public Image panel;
    public TextMeshProUGUI text;
    private CanvasGroup canvasGroup;
    private bool powerSavingEnabled = false;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Start()
    {
        powerSavingEnabled = DisplayManager.Inst.PowerSavingState;
        canvasGroup.alpha = powerSavingEnabled ? 1f : 0f;
        panel.raycastTarget = powerSavingEnabled;
        text.gameObject.SetActive(powerSavingEnabled);
    }

    //  화면이 완전히 어두워지면 절전 모드로 전환하고, 다시 밝아질 때는 밝아지기 전에 바로 절전 모드를 해제한다.
    void Update()
    {
        if(powerSavingEnabled)
        {
            canvasGroup.alpha += Time.deltaTime * 2f;
            if (canvasGroup.alpha >= 1f)
            {
                canvasGroup.alpha = 1f;
                if (!DisplayManager.Inst.PowerSavingState)
                {
                    panel.raycastTarget = true;
                    text.gameObject.SetActive(true);
                    DisplayManager.Inst.SetPowerSavingMode(true);
                }
            }
        }
        else
        {
            canvasGroup.alpha -= 1f * Time.deltaTime * 2f;
            if (canvasGroup.alpha <= 0f)
            {
                canvasGroup.alpha = 0f;
                panel.raycastTarget = false;
            }
        }
    }

    public void EnablePowerSavingMode()
    {
        powerSavingEnabled = true;
    }

    public void DisablePowerSavingMode()
    {
        powerSavingEnabled = false;
        text.gameObject.SetActive(false);
        DisplayManager.Inst.SetPowerSavingMode(false);
    }
}
