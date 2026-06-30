using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 향후 터렛 스킬 화면을 위한 준비 중 팝업 표시를 담당한다.
/// </summary>
public class TurretSkillPopupUI : TurretPopupPageUI
{
    [Header("스킬 준비 상태")]
    [SerializeField] private Button skillActionButton;
    [SerializeField] private TMP_Text readyText;

    // 스킬 기능이 아직 준비 중임을 표시한다
    public override void Show(TurretSelectionContext context)
    {
        base.Show(context);

        if (skillActionButton != null)
        {
            skillActionButton.interactable = false;
        }

        if (readyText != null)
        {
            readyText.text = "스킬 기능 준비 중";
        }
    }
}
