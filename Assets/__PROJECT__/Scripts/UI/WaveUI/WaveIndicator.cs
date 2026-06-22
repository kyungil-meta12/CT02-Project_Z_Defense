using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 현재 웨이브 숫자와 웨이브 변경 팝업 표시를 관리한다.
/// </summary>
public class WaveIndicator : MonoBehaviour
{
    public UIAnimationValue animValue;
    public TextMeshProUGUI text;
    public WavePopup wavePopup;
    private Vector2 originScale;
    private Vector2 currScale;
    private RectTransform rt;

    // 웨이브 텍스트의 초기 스케일과 기본 표시값을 준비한다
    void Awake()
    {
        text.text = "1";
        rt = text.GetComponent<RectTransform>();
        originScale = rt.localScale;
        currScale = originScale;
    }

    // 게임 매니저 이벤트를 구독하고 시작 웨이브를 표시한다
    void Start()
    {
        GameManager.Inst.OnWaveIncrease += OnWaveChange;
        GameManager.Inst.OnWaveDecrease += OnWaveChange;

        text.text = GameManager.Inst.Wave.ToString();

        EnablePopup();
    }

    // 게임 매니저 이벤트 구독을 해제한다
    void OnDestroy()
    {
        if(GameManager.Inst)
        {
            GameManager.Inst.OnWaveIncrease -= OnWaveChange;
            GameManager.Inst.OnWaveDecrease -= OnWaveChange;
        }
    }

    // 웨이브 텍스트 변경 애니메이션 스케일을 원래 크기로 되돌린다
    void Update()
    {
        currScale = Vector2.Lerp(currScale, originScale, Time.deltaTime * animValue.ScaleReturnLerpSpeed);
        rt.localScale = currScale;
    }

    /// <summary>
    /// 웨이브 증가 시 웨이브 인디케이터에 반영한다.
    /// </summary>
    /// <param name="val"></param>
    // 웨이브 변경 값을 텍스트와 팝업에 반영한다
    public void OnWaveChange(int val)
    {
        text.text = val.ToString();
        currScale = originScale * animValue.OnValueChangeScale;
        EnablePopup();
    }

    // 웨이브 변경 팝업 표시 코루틴을 시작한다
    void EnablePopup()
    {
        StartCoroutine(PopupCoroutine());
    }

    // 0.5초 후에 현재 웨이브 팝업을 활성화한다
    IEnumerator PopupCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        wavePopup.gameObject.SetActive(true);
        wavePopup.Init(GameManager.Inst.Wave);
    }
}
