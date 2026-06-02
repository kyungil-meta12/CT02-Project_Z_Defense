using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Project Z Defense/Turret Parts Progression")]
public class TurretPartsProgressionSO : ScriptableObject
{
    public TurretPartProgressionEntry[] levelEntries;

    public void GetActivePartNames(int level, List<string> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();

        if (levelEntries == null)
        {
            return;
        }

        for (int i = 0; i < levelEntries.Length; i++)
        {
            TurretPartProgressionEntry entry = levelEntries[i];
            if (entry == null || level < entry.requiredLevel || entry.activePartNames == null)
            {
                continue;
            }

            for (int j = 0; j < entry.activePartNames.Length; j++)
            {
                string partName = entry.activePartNames[j];
                if (!string.IsNullOrEmpty(partName) && !results.Contains(partName))
                {
                    results.Add(partName);
                }
            }
        }
    }
}

[System.Serializable]
public class TurretPartProgressionEntry
{
    public int requiredLevel = 1;
    public string[] activePartNames;
}
