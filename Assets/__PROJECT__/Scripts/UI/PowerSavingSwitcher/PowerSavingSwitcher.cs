using NUnit.Framework.Internal;
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
            imgColor.a = Mathf.Lerp(imgColor.a, 1f, Time.deltaTime * 10f);
            textColor.a = Mathf.Lerp(textColor.a, 1f, Time.deltaTime * 10f);
            if(imgColor.a >= 0.998f && !DisplayManager.Inst.PowerSavingState)
            {
                imgColor.a = 1f;
                textColor.a = 1f;
                DisplayManager.Inst.SetPowerSavingMode(true);
            }
        }
        else
        {
              textColor.a = Mathf.Lerp(textColor.a, 0f, Time.deltaTime * 10f);
            imgColor.a = Mathf.Lerp(imgColor.a, 0f, Time.deltaTime * 10f);
            if(imgColor.a <= 0.002f)
            {
                imgColor.a = 0f;
                textColor.a = 0f;
            }
        }

        text.color = textColor;
        img.color = imgColor;
    }

    public void TogglePowerSavingMode()
    {
        darkState = !darkState;
        if(!darkState && DisplayManager.Inst.PowerSavingState)
        {
            DisplayManager.Inst.SetPowerSavingMode(false);
        }
    }
}
