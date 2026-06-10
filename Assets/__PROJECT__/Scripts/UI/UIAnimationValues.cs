using UnityEngine;

[CreateAssetMenu(fileName = "UIAnimationValue", menuName = "Scriptable Objects/UIAnimationValue")]
public class UIAnimationValue : ScriptableObject
{
    [Header("값 변경이 된 순간에 곱하는 스케일 오프셋")] public float OnValueChangeScale;
    [Header("변화된 UI 스케일이 복귀되는 Lerp 속도")] public float ScaleReturnLerpSpeed;
    [Header("팝업 애니메이션 팝 아웃 속도")] public float PopOutSpeed;
    [Header("팝 아웃 후 팝 인 되기까지의 딜레이")] public float PopInDelay;
}