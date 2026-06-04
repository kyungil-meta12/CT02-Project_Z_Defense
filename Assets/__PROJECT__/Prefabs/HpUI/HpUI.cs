using UnityEngine;
using UnityEngine.UI;

public class HpUI : MonoBehaviour
{
    [Header("HpUI 캔버스 스케일")] public Vector2 canvasScale;
    [Header("HpUI 슬라이더 후면 색상")] public Color sliderBackColor;
    [Header("HpUI 슬라이더 전면 색상")] public Color sliderFrontColor;

    [Space(10)]
    public Slider slider;
    public Image sliderBack;
    public Image sliderFront;
    private Canvas worldCanvas;
    private RectTransform rt;
    private Transform camTransform;

    void Awake()
    {
        camTransform = Camera.main.transform;

        worldCanvas = GetComponent<Canvas>();
        rt = worldCanvas.GetComponent<RectTransform>();

        var originScale = rt.localScale;
        rt.localScale = new Vector3(originScale.x * canvasScale.x, originScale.y * canvasScale.y, 1f);
        
        sliderBack.color = sliderBackColor;
        sliderFront.color = sliderFrontColor;
    }

    // 항상 카메라 정면을 바라보도록 한다
    void LateUpdate()
    {
        rt.rotation = camTransform.rotation;
    }

    /// <summary>
    /// 최대 체력 입력
    /// </summary>
    /// <param name="val"></param>
    public void InputTotalHp(float val)
    {
        slider.maxValue = val;
    }

    /// <summary>
    /// 현재 체력 입력
    /// </summary>
    /// <param name="val"></param>
    public void InputCurrHp(float val)
    {
        slider.value = val;
    }
}
