using UnityEngine;

[CreateAssetMenu(menuName = "Project Z Defense/Turret VFX Progression")]
public class TurretVFXProgressionSO : ScriptableObject
{
    public TurretVFXProfileSO defaultProfile;
    public TurretVFXProgressionEntry[] levelEntries;

    public TurretVFXProfileSO GetProfileForLevel(int level)
    {
        TurretVFXProfileSO result = defaultProfile;

        if (levelEntries == null)
        {
            return result;
        }

        for (int i = 0; i < levelEntries.Length; i++)
        {
            TurretVFXProgressionEntry entry = levelEntries[i];
            if (entry == null || entry.vfxProfile == null || level < entry.requiredLevel)
            {
                continue;
            }

            result = entry.vfxProfile;
        }

        return result;
    }
}

[System.Serializable]
public class TurretVFXProgressionEntry
{
    public int requiredLevel = 1;
    public TurretVFXProfileSO vfxProfile;
}
