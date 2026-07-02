using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 딜 미터기 매니저의 정렬 결과를 Row UI 풀에 반영한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TurretDamageMeterUI : MonoBehaviour
{
    [Header("행 참조")]
    [SerializeField] private TurretDamageMeterRowUI[] rowItems;
    [SerializeField, Min(1.0f)] private float rowSpacing = 68.0f;

    [Header("색상")]
    [SerializeField] private TurretDamageMeterColorProfileSO colorProfile;
    [SerializeField] private Color fallbackBarColor = new Color(0.35f, 0.65f, 1.0f, 1.0f);

    // 매니저의 정렬 결과를 현재 Row UI에 반영한다
    public void Refresh(TurretDamageMeterManager manager)
    {
        if (manager == null || rowItems == null)
        {
            return;
        }

        IReadOnlyList<TurretDamageMeterEntry> entries = manager.GetSortedEntries();
        int visibleCount = Mathf.Min(rowItems.Length, manager.VisibleRowLimit, entries.Count);
        float totalDamage = CalculateTotalDamage(entries);
        float topDamage = visibleCount > 0 ? Mathf.Max(0.0f, entries[0].TotalDamage) : 0.0f;

        for (int i = 0; i < rowItems.Length; i++)
        {
            TurretDamageMeterRowUI row = rowItems[i];
            if (row == null)
            {
                continue;
            }

            if (i >= visibleCount)
            {
                row.SetVisible(false);
                continue;
            }

            TurretDamageMeterEntry entry = entries[i];
            float totalPercent = totalDamage > 0.0f ? entry.TotalDamage / totalDamage : 0.0f;
            float barRatio = topDamage > 0.0f ? entry.TotalDamage / topDamage : 0.0f;
            Color barColor = ResolveBarColor(entry);
            row.SetVisible(true);
            row.SetTargetY(-i * rowSpacing);
            row.Refresh(i + 1, entry.DisplayName, entry.TotalDamage, totalPercent, barRatio, entry.Icon, barColor);
        }
    }

    // 터렛 정의에 맞는 그래프 색상을 반환한다
    private Color ResolveBarColor(TurretDamageMeterEntry entry)
    {
        if (colorProfile == null || entry == null)
        {
            return fallbackBarColor;
        }

        return colorProfile.ResolveColor(entry.TurretDefinition);
    }

    // 표시 대상 항목들의 총 데미지를 계산한다
    private static float CalculateTotalDamage(IReadOnlyList<TurretDamageMeterEntry> entries)
    {
        float totalDamage = 0.0f;
        for (int i = 0; i < entries.Count; i++)
        {
            TurretDamageMeterEntry entry = entries[i];
            if (entry != null)
            {
                totalDamage += Mathf.Max(0.0f, entry.TotalDamage);
            }
        }

        return totalDamage;
    }
}
