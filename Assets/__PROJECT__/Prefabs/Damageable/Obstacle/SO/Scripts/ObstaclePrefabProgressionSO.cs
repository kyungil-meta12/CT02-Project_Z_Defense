using System;
using UnityEngine;

/// <summary>
/// 장애물 레벨에 따라 사용할 프리팹과 배치 회전을 선택한다.
/// </summary>
[CreateAssetMenu(fileName = "ObstaclePrefabProgression", menuName = "Project Z Defense/Obstacle Prefab Progression")]
public class ObstaclePrefabProgressionSO : ScriptableObject
{
    [Header("레벨별 프리팹 교체 규칙 - 요구 레벨 이상일 때 사용할 프리팹 목록")]
    [SerializeField] private ObstaclePrefabProgressionEntry[] entries;

    // 지정 레벨에 맞는 프리팹 엔트리를 반환한다
    public ObstaclePrefabProgressionEntry GetEntryForLevel(int level)
    {
        if (entries == null || entries.Length == 0)
        {
            return null;
        }

        int safeLevel = Mathf.Max(1, level);
        ObstaclePrefabProgressionEntry selected = null;
        int selectedRequiredLevel = 0;

        for (int i = 0; i < entries.Length; i++)
        {
            ObstaclePrefabProgressionEntry entry = entries[i];
            if (entry == null || entry.Prefab == null)
            {
                continue;
            }

            int requiredLevel = entry.RequiredLevel;
            if (requiredLevel > safeLevel || requiredLevel < selectedRequiredLevel)
            {
                continue;
            }

            selected = entry;
            selectedRequiredLevel = requiredLevel;
        }

        return selected;
    }

    // 인스펙터 입력값을 유효한 레벨 범위로 보정한다
    private void OnValidate()
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            ObstaclePrefabProgressionEntry entry = entries[i];
            if (entry == null)
            {
                continue;
            }

            entry.ClampValues();
        }
    }
}

/// <summary>
/// 특정 장애물 레벨부터 사용할 프리팹과 프리뷰 정보를 정의한다.
/// </summary>
[Serializable]
public class ObstaclePrefabProgressionEntry
{
    [Header("교체 조건 - 이 레벨부터 사용할 프리팹과 프리뷰")]
    [SerializeField, Min(1)] private int requiredLevel = 1;
    [SerializeField] private GameObject prefab;
    [SerializeField] private GameObject previewPrefab;
    [SerializeField] private Vector3 placementLocalEulerAngles;

    public int RequiredLevel
    {
        get
        {
            return Mathf.Max(1, requiredLevel);
        }
    }

    public GameObject Prefab
    {
        get
        {
            return prefab;
        }
    }

    public GameObject PreviewPrefab
    {
        get
        {
            return previewPrefab != null ? previewPrefab : prefab;
        }
    }

    public Quaternion PlacementLocalRotation
    {
        get
        {
            return Quaternion.Euler(placementLocalEulerAngles);
        }
    }

    // 인스펙터 입력값을 유효한 레벨 범위로 보정한다
    public void ClampValues()
    {
        requiredLevel = Mathf.Max(1, requiredLevel);
    }
}
