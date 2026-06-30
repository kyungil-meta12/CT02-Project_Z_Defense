using TMPro;
using UnityEngine;

/// <summary>
/// 선택된 터렛의 읽기 전용 상세 정보를 표시할 팝업의 1차 표시 연결을 담당한다.
/// </summary>
public class TurretDetailPopupUI : TurretPopupPageUI
{
    [Header("상세 정보")]
    [SerializeField] private TMP_Text statText;

    // 선택된 터렛의 현재 기본 스탯을 상세 팝업에 표시한다
    public override void Show(TurretSelectionContext context)
    {
        base.Show(context);
        RefreshStatText();
    }

    // 현재 터렛 스탯을 상세 정보 문자열로 갱신한다
    private void RefreshStatText()
    {
        if (statText == null)
        {
            return;
        }

        if (!CurrentContext.IsValid)
        {
            statText.text = "선택된 터렛 없음";
            return;
        }

        TurretRuntimeStat stat = CurrentContext.CalculateCurrentStat();
        statText.text =
            $"공격력: {stat.damage:0.##}\n" +
            $"사거리: {stat.range:0.##}\n" +
            $"발사간격: {stat.fireInterval:0.###}\n" +
            $"탄속: {stat.projectileSpeed:0.##}\n" +
            $"투사체 수: {stat.projectileCount}\n" +
            $"관통 횟수: {stat.pierceCount}";
    }
}
