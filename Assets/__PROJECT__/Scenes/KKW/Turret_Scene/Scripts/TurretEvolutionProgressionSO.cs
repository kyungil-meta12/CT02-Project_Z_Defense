using UnityEngine;

[CreateAssetMenu(menuName = "Project Z Defense/Turret Evolution Progression")]
public class TurretEvolutionProgressionSO : ScriptableObject
{
    public TurretEvolutionEntry[] evolutionEntries;

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

    public bool CanEvolve(int level)
    {
        return GetAvailableEvolutionCount(level) > 0;
    }

    private bool IsEntryAvailable(TurretEvolutionEntry entry, int level)
    {
        if (entry == null || entry.targetDefinition == null)
        {
            return false;
        }

        return level >= Mathf.Max(1, entry.requiredLevel);
    }
}

[System.Serializable]
public class TurretEvolutionEntry
{
    public int requiredLevel = 100;
    public TurretDefinitionSO targetDefinition;
    public string displayName;
}
