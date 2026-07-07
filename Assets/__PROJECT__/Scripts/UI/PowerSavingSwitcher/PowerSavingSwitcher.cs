using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerSavingSwitcher : MonoBehaviour
{
    private Image img;
    private TextMeshProUGUI text;
    private Color imgColor;
    private Color textColor;
    private bool darkState = false;

    void Awake()
    {
        img = GetComponent<Image>();
        text = img.GetComponentInChildren<TextMeshProUGUI>();
    }

    void Start()
    {
        darkState = DisplayManager.Inst.PowerSavingState;
        imgColor = img.color;
        textColor = text.color;
        imgColor.a = darkState ? 1f : 0f;
        textColor.a = darkState ? 1f : 0f;
        img.color = imgColor;
        text.color = textColor;
    }

    //  화면이 완전히 어두워지면 절전 모드로 전환하고, 다시 밝아질 때는 밝아지기 전에 바로 절전 모드를 해제한다.
    void Update()
    {
        if(darkState)
        {
            imgColor.a += Time.deltaTime * 2f;
            textColor.a += Time.deltaTime * 2f;
            if(imgColor.a >= 1f)
            {
                imgColor.a = 1f;
                textColor.a = 1f;
                if(!DisplayManager.Inst.PowerSavingState)
                {
                    img.raycastTarget = true;
                    DisplayManager.Inst.SetPowerSavingMode(true);
                }
            }
        }
        else
        {
            imgColor.a -= Time.deltaTime * 2f;
            textColor.a -= Time.deltaTime * 2f;
            if (imgColor.a <= 0f)
            {
                imgColor.a = 0f;
                textColor.a = 0f;
                img.raycastTarget = false;
            }
        }

        text.color = textColor;
        img.color = imgColor;
    }

    public void EnablePowerSavingMode()
    {
        darkState = true;
    }

    public void DisablePowerSavingMode()
    {
        darkState = false;
        DisplayManager.Inst.SetPowerSavingMode(false);
    }
}
