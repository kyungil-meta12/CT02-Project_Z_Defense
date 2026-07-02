using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 딜 미터기 매니저의 정렬 결과를 Row UI 풀에 반영한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TurretDamageMeterUI : MonoBehaviour
{
    [Header("행 참조")]
    [SerializeField] private TurretDamageMeterRowUI[] rowItems;
    [Tooltip("Row 참조가 비어 있거나 슬롯 위치 캐시에 실패했을 때만 사용하는 예비 간격입니다.")]
    [FormerlySerializedAs("rowSpacing")]
    [SerializeField, Min(1.0f)] private float fallbackRowSpacing = 68.0f;

    [Header("색상")]
    [SerializeField] private TurretDamageMeterColorProfileSO colorProfile;
    [SerializeField] private Color fallbackBarColor = new Color(0.35f, 0.65f, 1.0f, 1.0f);

    private float[] rowTargetYByRank;

    // 시작 시 에디터에서 배치한 Row 슬롯 위치를 캐시한다
    private void Awake()
    {
        CacheRowTargetPositions();
    }

    // 매니저의 정렬 결과를 현재 Row UI에 반영한다
    public void Refresh(TurretDamageMeterManager manager)
    {
        if (manager == null || rowItems == null)
        {
            return;
        }

        EnsureRowTargetPositions();

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
            row.SetTargetY(GetRowTargetY(i));
            row.Refresh(i + 1, entry.DisplayName, entry.TotalDamage, totalPercent, barRatio, barColor);
        }
    }

    // Row 슬롯 위치 캐시가 없으면 다시 만든다
    private void EnsureRowTargetPositions()
    {
        if (rowTargetYByRank != null && rowTargetYByRank.Length == rowItems.Length)
        {
            return;
        }

        CacheRowTargetPositions();
    }

    // 에디터에서 배치된 Row들의 Y 위치를 순위별 목표 위치로 저장한다
    private void CacheRowTargetPositions()
    {
        if (rowItems == null)
        {
            rowTargetYByRank = null;
            return;
        }

        rowTargetYByRank = new float[rowItems.Length];
        for (int i = 0; i < rowItems.Length; i++)
        {
            TurretDamageMeterRowUI row = rowItems[i];
            RectTransform rowRect = row != null ? row.transform as RectTransform : null;
            rowTargetYByRank[i] = rowRect != null ? rowRect.anchoredPosition.y : -i * fallbackRowSpacing;
        }
    }

    // 지정 순위가 이동할 Y 위치를 반환한다
    private float GetRowTargetY(int rankIndex)
    {
        if (rowTargetYByRank == null || rankIndex < 0 || rankIndex >= rowTargetYByRank.Length)
        {
            return -rankIndex * fallbackRowSpacing;
        }

        return rowTargetYByRank[rankIndex];
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
