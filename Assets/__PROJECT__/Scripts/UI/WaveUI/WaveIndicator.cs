using TMPro;
using UnityEngine;

public class WaveIndicator : MonoBehaviour
{
    public UIAnimationValue animValue;
    public TextMeshProUGUI text;
    private Vector2 originScale;
    private Vector2 currScale;
    private RectTransform rt;

    void Awake()
    {
        text.text = "1";
        rt = text.GetComponent<RectTransform>();
        originScale = rt.localScale;
        currScale = originScale;
    }

    void Start()
    {
        // 게임 매니저 웨이브 증가 이벤트 구독
        GameManager.Inst.OnWaveIncrease += OnWaveChange;
    }

    void OnDestroy()
    {
        if(GameManager.Inst)
        {
            GameManager.Inst.OnWaveIncrease -= OnWaveChange;
        }
    }

    void Update()
    {
        currScale = Vector2.Lerp(currScale, originScale, Time.deltaTime * animValue.ScaleReturnLerpSpeed);
        rt.localScale = currScale;
    }

    /// <summary>
    /// 웨이브 증가 시 웨이브 인디케이터에 반영한다.
    /// </summary>
    /// <param name="val"></param>
    public void OnWaveChange(int val)
    {
        text.text = val.ToString();
        currScale = originScale * animValue.OnValueChangeScale;
    }
}
