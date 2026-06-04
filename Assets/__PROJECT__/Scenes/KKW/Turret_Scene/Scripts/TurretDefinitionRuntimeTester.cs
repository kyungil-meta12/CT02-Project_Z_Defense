using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TurretStatProfileApplier))]
public class TurretDefinitionRuntimeTester : MonoBehaviour
{
    [SerializeField] private TurretDefinitionSO turretDefinition;
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool applyOnInspectorChange = true;
    [SerializeField] private bool applyStatsToTurret = true;
    [SerializeField] private bool applyVFXToTurret = true;
    [SerializeField] private bool logRuntimeStat = true;
    [SerializeField] private bool logProjectileDamage = true;
    [SerializeField] private TurretStatProfileApplier statProfileApplier;
    [SerializeField] private Turret targetTurret;
    [SerializeField] private FiringEvent targetFiringEvent;
    [SerializeField] private string currentTurretName;
    [SerializeField] private string availableEvolutionNames;

    public TurretDefinitionSO CurrentTurretDefinition
    {
        get
        {
            return turretDefinition;
        }
    }

    public int CurrentLevel
    {
        get
        {
            return level;
        }
    }

    public string CurrentTurretName
    {
        get
        {
            return currentTurretName;
        }
    }

    public string AvailableEvolutionNames
    {
        get
        {
            return availableEvolutionNames;
        }
    }

    private void Reset()
    {
        RefreshReferences();
    }

    private void OnValidate()
    {
        level = Mathf.Max(1, level);

        if (Application.isPlaying && applyOnInspectorChange)
        {
            Apply();
        }
    }

    private void Start()
    {
        if (applyOnStart)
        {
            Apply();
        }
    }

    [ContextMenu("Apply Turret Definition")]
    public void Apply()
    {
        RefreshReferences();

        if (turretDefinition == null)
        {
            Debug.LogWarning("[TurretDefinitionRuntimeTester] Turret definition is missing.", this);
            return;
        }

        TurretRuntimeStat runtimeStat = TurretStatCalculator.Calculate(turretDefinition.baseStatProfile, turretDefinition.statGrowthProfile, level);
        RefreshRuntimeNames();

        if (applyStatsToTurret && statProfileApplier != null)
        {
            statProfileApplier.Apply(runtimeStat, logProjectileDamage);
        }

        if (applyVFXToTurret)
        {
            ApplyVFX(runtimeStat);
        }

        if (logRuntimeStat)
        {
            Debug.Log(
                $"[TurretDefinitionRuntimeTester] {turretDefinition.displayName} Lv.{level} " +
                $"Damage:{runtimeStat.damage:0.###}, Range:{runtimeStat.range:0.###}, FireInterval:{runtimeStat.fireInterval:0.###}, " +
                $"ProjectileSpeed:{runtimeStat.projectileSpeed:0.###}, ProjectileCount:{runtimeStat.projectileCount}, PierceCount:{runtimeStat.pierceCount}",
                this);
        }
    }

    public bool CanEvolve()
    {
        return turretDefinition != null &&
               turretDefinition.evolutionProgressionProfile != null &&
               turretDefinition.evolutionProgressionProfile.CanEvolve(level);
    }

    public int GetAvailableEvolutionCount()
    {
        if (turretDefinition == null || turretDefinition.evolutionProgressionProfile == null)
        {
            return 0;
        }

        return turretDefinition.evolutionProgressionProfile.GetAvailableEvolutionCount(level);
    }

    public TurretEvolutionEntry GetAvailableEvolution(int availableIndex)
    {
        if (turretDefinition == null || turretDefinition.evolutionProgressionProfile == null)
        {
            return null;
        }

        return turretDefinition.evolutionProgressionProfile.GetAvailableEvolution(level, availableIndex);
    }

    public bool Evolve(int availableIndex)
    {
        TurretEvolutionEntry evolutionEntry = GetAvailableEvolution(availableIndex);
        if (evolutionEntry == null || evolutionEntry.targetDefinition == null)
        {
            return false;
        }

        PlayEvolutionEffect(evolutionEntry);
        turretDefinition = evolutionEntry.targetDefinition;
        Apply();
        return true;
    }

    public TurretDefinitionRuntimeTester CreateEvolvedInstance(int availableIndex)
    {
        TurretEvolutionEntry evolutionEntry = GetAvailableEvolution(availableIndex);
        if (evolutionEntry == null || evolutionEntry.targetDefinition == null)
        {
            return null;
        }

        if (evolutionEntry.targetDefinition.basePrefab == null)
        {
            return Evolve(availableIndex) ? this : null;
        }

        PlayEvolutionEffect(evolutionEntry);
        GameObject evolvedObject = Instantiate(evolutionEntry.targetDefinition.basePrefab, transform.position, transform.rotation, transform.parent);
        evolvedObject.transform.localScale = transform.localScale;

        TurretDefinitionRuntimeTester evolvedRuntimeTester = evolvedObject.GetComponent<TurretDefinitionRuntimeTester>();
        if (evolvedRuntimeTester == null)
        {
            evolvedRuntimeTester = evolvedObject.AddComponent<TurretDefinitionRuntimeTester>();
        }

        evolvedRuntimeTester.SetDefinition(evolutionEntry.targetDefinition, level);
        Destroy(gameObject);
        return evolvedRuntimeTester;
    }

    public void SetLevel(int level_)
    {
        int nextLevel = GetClampedLevelForEvolution(level_);
        if (nextLevel == level)
        {
            return;
        }

        level = nextLevel;
        Apply();
    }

    public void AddLevel(int levelAmount)
    {
        if (levelAmount <= 0)
        {
            return;
        }

        SetLevel(level + levelAmount);
    }

    public void SetDefinition(TurretDefinitionSO turretDefinition_, int level_)
    {
        turretDefinition = turretDefinition_;
        level = GetClampedLevelForEvolution(level_);
        Apply();
    }

    [ContextMenu("Evolve To First Available")]
    private void EvolveToFirstAvailable()
    {
        if (!Evolve(0))
        {
            Debug.LogWarning("[TurretDefinitionRuntimeTester] No available evolution for the current level.", this);
        }
    }

    private void ApplyVFX(TurretRuntimeStat runtimeStat)
    {
        if (turretDefinition.vfxProgressionProfile == null)
        {
            return;
        }

        TurretVFXProfileSO vfxProfile = turretDefinition.vfxProgressionProfile.GetProfileForLevel(level);
        if (vfxProfile == null)
        {
            return;
        }

        if (targetTurret != null && vfxProfile.projectilePrefab != null)
        {
            targetTurret.SetProjectilePrefab(vfxProfile.projectilePrefab, runtimeStat.projectileSpeed);
            targetTurret.SetProjectileScale(GetProjectileScale());
        }

        if (targetFiringEvent != null)
        {
            targetFiringEvent.muzzleVFX = vfxProfile.muzzleVFX;
            targetFiringEvent.muzzleVFXDuration = vfxProfile.muzzleVFXDuration;
            targetFiringEvent.firingSound = vfxProfile.fireSound;
        }
    }

    private float GetProjectileScale()
    {
        if (turretDefinition.projectileScaleProgressionProfile == null)
        {
            return 1.0f;
        }

        return turretDefinition.projectileScaleProgressionProfile.GetScaleForLevel(level);
    }

    private void PlayEvolutionEffect(TurretEvolutionEntry evolutionEntry)
    {
        if (evolutionEntry == null || evolutionEntry.evolutionEffectPrefab == null)
        {
            return;
        }

        Vector3 effectPosition = transform.TransformPoint(evolutionEntry.evolutionEffectLocalOffset);
        float effectDuration = Mathf.Max(0.0f, evolutionEntry.evolutionEffectDuration);
        PooledObjectUtility.SpawnEffect(evolutionEntry.evolutionEffectPrefab, effectPosition, transform.rotation, effectDuration);
    }

    private int GetClampedLevelForEvolution(int requestedLevel)
    {
        int clampedLevel = Mathf.Max(1, requestedLevel);

        if (turretDefinition == null || turretDefinition.evolutionProgressionProfile == null)
        {
            return clampedLevel;
        }

        if (turretDefinition.evolutionProgressionProfile.CanEvolve(level))
        {
            return Mathf.Max(1, level);
        }

        int nextRequiredLevel = turretDefinition.evolutionProgressionProfile.GetNextRequiredEvolutionLevel(level);
        if (nextRequiredLevel <= 0)
        {
            return clampedLevel;
        }

        return Mathf.Min(clampedLevel, nextRequiredLevel);
    }

    private void RefreshReferences()
    {
        if (statProfileApplier == null)
        {
            statProfileApplier = GetComponent<TurretStatProfileApplier>();
        }

        if (targetTurret == null)
        {
            targetTurret = GetComponent<Turret>();
        }

        if (targetFiringEvent == null)
        {
            targetFiringEvent = GetComponent<FiringEvent>();
        }
    }

    private void RefreshRuntimeNames()
    {
        currentTurretName = turretDefinition == null ? string.Empty : GetDefinitionName(turretDefinition);

        if (turretDefinition == null || turretDefinition.evolutionProgressionProfile == null)
        {
            availableEvolutionNames = string.Empty;
            return;
        }

        int availableEvolutionCount = turretDefinition.evolutionProgressionProfile.GetAvailableEvolutionCount(level);
        if (availableEvolutionCount == 0)
        {
            availableEvolutionNames = string.Empty;
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < availableEvolutionCount; i++)
        {
            TurretEvolutionEntry entry = turretDefinition.evolutionProgressionProfile.GetAvailableEvolution(level, i);
            if (entry == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(GetEvolutionName(entry));
        }

        availableEvolutionNames = builder.ToString();
    }

    private string GetEvolutionName(TurretEvolutionEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(entry.displayName))
        {
            return entry.displayName;
        }

        return GetDefinitionName(entry.targetDefinition);
    }

    private string GetDefinitionName(TurretDefinitionSO definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(definition.displayName))
        {
            return definition.displayName;
        }

        return definition.name;
    }
}
