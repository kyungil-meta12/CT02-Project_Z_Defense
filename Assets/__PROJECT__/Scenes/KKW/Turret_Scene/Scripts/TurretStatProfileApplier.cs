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

    private void Reset()
    {
        RefreshReferences();
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

            targetTurret.SetAutoFireEnabled(true);
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
