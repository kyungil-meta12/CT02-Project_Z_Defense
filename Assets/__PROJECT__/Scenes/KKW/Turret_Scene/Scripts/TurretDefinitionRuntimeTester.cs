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
        }

        if (targetFiringEvent != null)
        {
            targetFiringEvent.muzzleVFX = vfxProfile.muzzleVFX;
            targetFiringEvent.muzzleVFXDuration = vfxProfile.muzzleVFXDuration;
            targetFiringEvent.firingSound = vfxProfile.fireSound;
        }
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
}
