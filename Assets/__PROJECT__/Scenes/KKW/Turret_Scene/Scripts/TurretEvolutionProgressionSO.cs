using UnityEngine;

/// <summary>
/// 현재 터렛 티어 레벨에서 선택 가능한 진화 후보 목록을 정의하는 ScriptableObject.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Turret Evolution Progression")]
public class TurretEvolutionProgressionSO : ScriptableObject
{
    public TurretEvolutionEntry[] evolutionEntries;

    // 현재 레벨에서 선택 가능한 진화 후보 수를 반환한다
    public int GetAvailableEvolutionCount(int level)
    {
        int count = 0;

        if (evolutionEntries == null)
        {
            return count;
        }

        for (int i = 0; i < evolutionEntries.Length; i++)
        {
            TurretEvolutionEntry entry = evolutionEntries[i];
            if (IsEntryAvailable(entry, level))
            {
                count++;
            }
        }

        return count;
    }

    // 현재 레벨에서 지정한 표시 인덱스의 진화 후보를 반환한다
    public TurretEvolutionEntry GetAvailableEvolution(int level, int availableIndex)
    {
        if (availableIndex < 0 || evolutionEntries == null)
        {
            return null;
        }

        int currentIndex = 0;
        for (int i = 0; i < evolutionEntries.Length; i++)
        {
            TurretEvolutionEntry entry = evolutionEntries[i];
            if (!IsEntryAvailable(entry, level))
            {
                continue;
            }

            if (currentIndex == availableIndex)
            {
                return entry;
            }

            currentIndex++;
        }

        return null;
    }

    // 현재 레벨에서 하나 이상의 진화 후보가 있는지 확인한다
    public bool CanEvolve(int level)
    {
        return GetAvailableEvolutionCount(level) > 0;
    }

    // 현재 레벨 이후 가장 가까운 진화 요구 레벨을 반환한다
    public int GetNextRequiredEvolutionLevel(int level)
    {
        int nextRequiredLevel = 0;

        if (evolutionEntries == null)
        {
            return nextRequiredLevel;
        }

        for (int i = 0; i < evolutionEntries.Length; i++)
        {
            TurretEvolutionEntry entry = evolutionEntries[i];
            if (entry == null || entry.targetDefinition == null)
            {
                continue;
            }

            int requiredLevel = Mathf.Max(1, entry.requiredLevel);
            if (level >= requiredLevel)
            {
                continue;
            }

            if (nextRequiredLevel == 0 || requiredLevel < nextRequiredLevel)
            {
                nextRequiredLevel = requiredLevel;
            }
        }

        return nextRequiredLevel;
    }

    // 단일 진화 엔트리가 현재 레벨에서 선택 가능한지 확인한다
    private bool IsEntryAvailable(TurretEvolutionEntry entry, int level)
    {
        if (entry == null || entry.targetDefinition == null)
        {
            return false;
        }

        return level >= Mathf.Max(1, entry.requiredLevel);
    }
}

/// <summary>
/// 터렛 진화 후보의 요구 레벨, 목표 정의, 비용, 표시 정보, 연출 정보를 정의한다.
/// </summary>
[System.Serializable]
public class TurretEvolutionEntry
{
    public int requiredLevel = 100;
    public TurretDefinitionSO targetDefinition;
    public string displayName;
    public ResourceCost[] evolutionCosts;
    public Sprite evolutionIcon;
    public GameObject evolutionEffectPrefab;
    public Vector3 evolutionEffectLocalOffset;
    public float evolutionEffectDuration = 2.0f;
}
