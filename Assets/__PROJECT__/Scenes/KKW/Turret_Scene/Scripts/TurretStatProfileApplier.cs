using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;

[DisallowMultipleComponent]
public class TurretStatProfileApplier : MonoBehaviour
{
    [SerializeField] private TurretStatProfileSO statProfile;
    [SerializeField] private Turret targetTurret;
    [SerializeField] private TargetFinder targetFinder;
    [SerializeField] private Gun[] targetGuns;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool applyOnInspectorChange = true;

    public TurretStatProfileSO StatProfile => statProfile;
    public bool HasStatProfile => statProfile != null;

    private bool autoFireEnabled = true;

    private void Reset()
    {
        RefreshReferences();
    }

    private void OnEnable()
    {
        TurretStatProfileSO.ProfileChanged += OnStatProfileChanged;
    }

    private void OnDisable()
    {
        TurretStatProfileSO.ProfileChanged -= OnStatProfileChanged;
    }

    private void OnValidate()
    {
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

    [ContextMenu("Apply Stat Profile")]
    public void Apply()
    {
        RefreshReferences();

        if (statProfile == null)
        {
            return;
        }

        if (targetFinder != null)
        {
            targetFinder.radius = statProfile.range;
        }

        if (targetTurret != null)
        {
            targetTurret.fireTick = statProfile.fireInterval;

            if (targetTurret.projectilePrefab != null)
            {
                targetTurret.SetProjectilePrefab(targetTurret.projectilePrefab, statProfile.projectileSpeed);
            }

            targetTurret.SetAutoFireEnabled(autoFireEnabled);
        }

        int projectileCount = Mathf.Max(1, statProfile.projectileCount);
        for (int i = 0; i < targetGuns.Length; i++)
        {
            Gun gun = targetGuns[i];
            if (gun == null)
            {
                continue;
            }

            gun.burstFireCount = projectileCount;
        }
    }

    public void Apply(TurretRuntimeStat runtimeStat)
    {
        RefreshReferences();

        if (targetFinder != null)
        {
            targetFinder.radius = runtimeStat.range;
        }

        if (targetTurret != null)
        {
            targetTurret.fireTick = runtimeStat.fireInterval;

            if (targetTurret.projectilePrefab != null)
            {
                targetTurret.SetProjectilePrefab(targetTurret.projectilePrefab, runtimeStat.projectileSpeed);
            }

            targetTurret.SetAutoFireEnabled(autoFireEnabled);
        }

        int projectileCount = Mathf.Max(1, runtimeStat.projectileCount);
        for (int i = 0; i < targetGuns.Length; i++)
        {
            Gun gun = targetGuns[i];
            if (gun == null)
            {
                continue;
            }

            gun.burstFireCount = projectileCount;
        }
    }

    public void SetStatProfile(TurretStatProfileSO statProfile_)
    {
        statProfile = statProfile_;

        if (Application.isPlaying)
        {
            Apply();
        }
    }

    public void SetAutoFireEnabled(bool enabled)
    {
        autoFireEnabled = enabled;
        RefreshReferences();

        if (targetTurret != null)
        {
            targetTurret.SetAutoFireEnabled(autoFireEnabled);
        }
    }

    private void OnStatProfileChanged(TurretStatProfileSO changedProfile)
    {
        if (!Application.isPlaying || !applyOnInspectorChange || changedProfile != statProfile)
        {
            return;
        }

        Apply();
    }

    private void RefreshReferences()
    {
        if (targetTurret == null)
        {
            targetTurret = GetComponent<Turret>();
        }

        if (targetFinder == null)
        {
            targetFinder = GetComponent<TargetFinder>();
        }

        if (targetGuns == null || targetGuns.Length == 0)
        {
            targetGuns = GetComponentsInChildren<Gun>(true);
        }
    }
}
