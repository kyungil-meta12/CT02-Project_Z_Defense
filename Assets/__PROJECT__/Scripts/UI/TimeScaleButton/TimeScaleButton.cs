using UnityEngine;
using UnityEngine.UI;

public class TimeScaleButton : MonoBehaviour
{
    public Image image1x;
    public Image image2x;

    void Start()
    {
        // 기본 1배속 모드로 시작
        image1x.gameObject.SetActive(true);
        image2x.gameObject.SetActive(false);
    }

    /// <summary>
    /// 시간 배속 토글
    /// </summary>
    public void OnTimeSpeedButtonClick()
    {
        GameManager.Inst.SetTimeSpeedMode(!GameManager.Inst.isTimeFastMode);
        image1x.gameObject.SetActive(!GameManager.Inst.isTimeFastMode);
        image2x.gameObject.SetActive(GameManager.Inst.isTimeFastMode);
    }
}
