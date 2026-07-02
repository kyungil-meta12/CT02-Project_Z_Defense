using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

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

    [Header("접기 버튼")]
    [SerializeField] private GameObject closeButtonFrame;
    [SerializeField] private GameObject openButtonFrame;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button openButton;

    [Header("접기 연출")]
    [SerializeField, Min(0.05f)] private float foldDuration = 0.5f;
    [SerializeField, Min(0.0f)] private float rowCascadeInterval = 0.035f;
    [Tooltip("접힐 때 Row들이 모이는 기준 위치에 더할 Y 오프셋입니다.")]
    [SerializeField] private float foldedYOffset;

    private float[] rowTargetYByRank;
    private Coroutine foldCoroutine;
    private int lastVisibleCount;
    private bool isFolded;
    private bool isFoldAnimationRunning;

    // 시작 시 에디터에서 배치한 Row 슬롯 위치를 캐시한다
    private void Awake()
    {
        CacheRowTargetPositions();
        ResolveFoldButtonReferences();
        BindFoldButtons();
        RefreshFoldButtonFrames();
    }

    // 제거 시 버튼 이벤트 구독을 해제한다
    private void OnDestroy()
    {
        UnbindFoldButtons();
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
        lastVisibleCount = visibleCount;
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
            row.Refresh(i + 1, entry.DisplayName, entry.TotalDamage, totalPercent, barRatio, barColor);

            if (isFoldAnimationRunning)
            {
                continue;
            }

            if (isFolded)
            {
                row.SetTargetY(GetFoldedTargetY());
                row.SetAlpha(0.0f);
                row.SetVisible(false);
                continue;
            }

            row.SetVisible(true);
            row.SetAlpha(1.0f);
            row.SetTargetY(GetRowTargetY(i));
        }
    }

    // 딜 미터기 Row를 위쪽으로 순차 접는다
    public void CloseDamageMeterRows()
    {
        if (isFolded && !isFoldAnimationRunning)
        {
            return;
        }

        StartFoldAnimation(FoldRowsClosed());
    }

    // 딜 미터기 Row를 아래쪽으로 순차 펼친다
    public void OpenDamageMeterRows()
    {
        if (!isFolded && !isFoldAnimationRunning)
        {
            return;
        }

        StartFoldAnimation(FoldRowsOpened());
    }

    // 기존 접기 연출을 중단하고 새 연출을 시작한다
    private void StartFoldAnimation(IEnumerator routine)
    {
        if (foldCoroutine != null)
        {
            StopCoroutine(foldCoroutine);
        }

        foldCoroutine = StartCoroutine(routine);
    }

    // 활성 Row들을 접힘 위치로 이동시킨 뒤 숨긴다
    private IEnumerator FoldRowsClosed()
    {
        isFoldAnimationRunning = true;
        SetFoldButtonFrames(false, true);

        int visibleCount = GetSafeVisibleCount();
        float foldedY = GetFoldedTargetY();
        if (visibleCount <= 0)
        {
            isFolded = true;
            isFoldAnimationRunning = false;
            foldCoroutine = null;
            RefreshFoldButtonFrames();
            yield break;
        }

        PrepareRowsForClose(visibleCount);

        float elapsedTime = 0.0f;
        while (elapsedTime < foldDuration)
        {
            elapsedTime += Time.deltaTime;
            ApplyCloseTargets(visibleCount, foldedY, elapsedTime);
            SetVisibleRowsAlpha(visibleCount, 1.0f - elapsedTime / foldDuration);
            yield return null;
        }

        HideRowsAfterFold(visibleCount, foldedY);
        isFolded = true;
        isFoldAnimationRunning = false;
        foldCoroutine = null;
        RefreshFoldButtonFrames();
    }

    // 활성 Row들을 접힘 위치에서 순위 슬롯 위치로 펼친다
    private IEnumerator FoldRowsOpened()
    {
        isFoldAnimationRunning = true;
        SetFoldButtonFrames(true, false);

        int visibleCount = GetSafeVisibleCount();
        float foldedY = GetFoldedTargetY();
        float elapsedDelay = 0.0f;
        if (visibleCount <= 0)
        {
            isFolded = false;
            isFoldAnimationRunning = false;
            foldCoroutine = null;
            RefreshFoldButtonFrames();
            yield break;
        }

        for (int i = 0; i < visibleCount; i++)
        {
            TurretDamageMeterRowUI row = rowItems[i];
            if (row != null)
            {
                row.SetVisible(true);
                row.SetAlpha(1.0f);
                row.SetCurrentY(foldedY);
            }
        }

        for (int i = 0; i < visibleCount; i++)
        {
            TurretDamageMeterRowUI row = rowItems[i];
            if (row != null)
            {
                row.SetAlpha(1.0f);
                row.SetTargetY(GetRowTargetY(i));
            }

            if (rowCascadeInterval > 0.0f)
            {
                elapsedDelay += rowCascadeInterval;
                yield return new WaitForSeconds(rowCascadeInterval);
            }
        }

        float remainTime = Mathf.Max(0.0f, foldDuration - elapsedDelay);
        if (remainTime > 0.0f)
        {
            yield return new WaitForSeconds(remainTime);
        }

        SnapRowsToRankSlots(visibleCount);
        isFolded = false;
        isFoldAnimationRunning = false;
        foldCoroutine = null;
        RefreshFoldButtonFrames();
    }

    // 접기 시작 전 표시 Row의 활성 상태와 투명도를 초기화한다
    private void PrepareRowsForClose(int visibleCount)
    {
        for (int i = 0; i < visibleCount; i++)
        {
            TurretDamageMeterRowUI row = rowItems[i];
            if (row == null)
            {
                continue;
            }

            row.SetVisible(true);
            row.SetAlpha(1.0f);
        }
    }

    // 접기 경과 시간에 맞춰 아래 Row부터 접힘 위치로 이동시킨다
    private void ApplyCloseTargets(int visibleCount, float foldedY, float elapsedTime)
    {
        for (int i = visibleCount - 1; i >= 0; i--)
        {
            float rowDelay = (visibleCount - 1 - i) * rowCascadeInterval;
            if (elapsedTime < Mathf.Min(rowDelay, foldDuration))
            {
                continue;
            }

            TurretDamageMeterRowUI row = rowItems[i];
            if (row != null)
            {
                row.SetTargetY(foldedY);
            }
        }
    }

    // 표시 Row들의 전체 투명도를 같은 값으로 맞춘다
    private void SetVisibleRowsAlpha(int visibleCount, float alpha)
    {
        for (int i = 0; i < visibleCount; i++)
        {
            TurretDamageMeterRowUI row = rowItems[i];
            if (row != null)
            {
                row.SetAlpha(alpha);
            }
        }
    }

    // 접기 버튼 참조를 이름 기준으로 보강한다
    private void ResolveFoldButtonReferences()
    {
        if (closeButtonFrame == null)
        {
            Transform closeFrame = FindChildByName(transform, "CloseButtonFrame");
            closeButtonFrame = closeFrame != null ? closeFrame.gameObject : null;
        }

        if (openButtonFrame == null)
        {
            Transform openFrame = FindChildByName(transform, "OpenButtonFrame");
            openButtonFrame = openFrame != null ? openFrame.gameObject : null;
        }

        if (closeButton == null && closeButtonFrame != null)
        {
            closeButton = closeButtonFrame.GetComponentInChildren<Button>(true);
        }

        if (openButton == null && openButtonFrame != null)
        {
            openButton = openButtonFrame.GetComponentInChildren<Button>(true);
        }
    }

    // 접기와 펼치기 버튼 이벤트를 연결한다
    private void BindFoldButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseDamageMeterRows);
            closeButton.onClick.AddListener(CloseDamageMeterRows);
        }

        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenDamageMeterRows);
            openButton.onClick.AddListener(OpenDamageMeterRows);
        }
    }

    // 접기와 펼치기 버튼 이벤트 연결을 해제한다
    private void UnbindFoldButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseDamageMeterRows);
        }

        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenDamageMeterRows);
        }
    }

    // 현재 접힘 상태에 맞춰 버튼 프레임을 표시한다
    private void RefreshFoldButtonFrames()
    {
        SetFoldButtonFrames(!isFolded, isFolded);
    }

    // 닫기와 열기 버튼 프레임 활성 상태를 변경한다
    private void SetFoldButtonFrames(bool showCloseButton, bool showOpenButton)
    {
        if (closeButtonFrame != null)
        {
            closeButtonFrame.SetActive(showCloseButton);
        }

        if (openButtonFrame != null)
        {
            openButtonFrame.SetActive(showOpenButton);
        }
    }

    // 접기 연출 뒤 Row를 숨기고 접힘 위치로 맞춘다
    private void HideRowsAfterFold(int visibleCount, float foldedY)
    {
        for (int i = 0; i < visibleCount; i++)
        {
            TurretDamageMeterRowUI row = rowItems[i];
            if (row == null)
            {
                continue;
            }

            row.SetCurrentY(foldedY);
            row.SetAlpha(0.0f);
            row.SetVisible(false);
        }
    }

    // 펼치기 연출 뒤 Row를 순위 슬롯 위치로 정확히 맞춘다
    private void SnapRowsToRankSlots(int visibleCount)
    {
        for (int i = 0; i < visibleCount; i++)
        {
            TurretDamageMeterRowUI row = rowItems[i];
            if (row == null)
            {
                continue;
            }

            row.SetVisible(true);
            row.SetAlpha(1.0f);
            row.SetCurrentY(GetRowTargetY(i));
        }
    }

    // 현재 Row 배열과 표시 개수를 기준으로 안전한 표시 개수를 반환한다
    private int GetSafeVisibleCount()
    {
        if (rowItems == null)
        {
            return 0;
        }

        return Mathf.Clamp(lastVisibleCount, 0, rowItems.Length);
    }

    // Row들이 접힐 목표 Y 위치를 반환한다
    private float GetFoldedTargetY()
    {
        return GetRowTargetY(0) + foldedYOffset;
    }

    // 하위 계층에서 이름이 일치하는 Transform을 찾는다
    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform found = FindChildByName(child, childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
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
