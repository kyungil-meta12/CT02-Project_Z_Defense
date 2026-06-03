using UnityEngine;

[CreateAssetMenu(menuName = "Project Z Defense/Turret Projectile Scale Progression")]
public class TurretProjectileScaleProgressionSO : ScriptableObject
{
    public float defaultScale = 1.0f;
    public TurretProjectileScaleProgressionEntry[] levelEntries;

    public float GetScaleForLevel(int level)
    {
        float result = Mathf.Max(0.01f, defaultScale);

        if (levelEntries == null)
        {
            return result;
        }

        for (int i = 0; i < levelEntries.Length; i++)
        {
            TurretProjectileScaleProgressionEntry entry = levelEntries[i];
            if (entry == null || level < entry.requiredLevel)
            {
                continue;
            }

            result = Mathf.Max(0.01f, entry.projectileScale);
        }

        return result;
    }
}

[System.Serializable]
public class TurretProjectileScaleProgressionEntry
{
    public int requiredLevel = 1;
    public float projectileScale = 1.0f;
}
