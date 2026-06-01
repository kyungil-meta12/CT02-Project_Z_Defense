using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;
using UnityEngine.Serialization;

public class TurretVFXProfileTester : MonoBehaviour
{
    [SerializeField] private Turret targetTurret;
    [SerializeField] private FiringEvent targetFiringEvent;
    [SerializeField] private TurretVFXProfile[] profiles;
    [FormerlySerializedAs("currentIndex")]
    [SerializeField, Min(0)] private int profileIndex = 0;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool applyOnInspectorChange = true;
    [SerializeField] private string currentProfileName;

    public TurretVFXProfile CurrentProfile
    {
        get
        {
            if (profiles == null || profiles.Length == 0)
            {
                return null;
            }

            return profiles[Mathf.Clamp(profileIndex, 0, profiles.Length - 1)];
        }
    }

    private void Reset()
    {
        targetTurret = GetComponent<Turret>();
        targetFiringEvent = GetComponent<FiringEvent>();
    }

    private void OnValidate()
    {
        ClampProfileIndex();
        RefreshCurrentProfileName();

        if (Application.isPlaying && applyOnInspectorChange)
        {
            ApplyCurrentProfile();
        }
    }

    private void Start()
    {
        ClampProfileIndex();

        if (applyOnStart)
        {
            ApplyCurrentProfile();
        }
    }

    [ContextMenu("Apply Current Profile")]
    public void ApplyCurrentProfile()
    {
        TurretVFXProfile profile = CurrentProfile;
        if (profile == null)
        {
            Debug.LogWarning("[TurretVFXProfileTester] No VFX profile assigned.", this);
            return;
        }

        if (targetTurret != null)
        {
            targetTurret.SetProjectilePrefab(profile.projectilePrefab, profile.projectileSpeed);
        }

        if (targetFiringEvent != null)
        {
            targetFiringEvent.muzzleVFX = profile.muzzleVFX;
            targetFiringEvent.muzzleVFXDuration = profile.muzzleVFXDuration;
            targetFiringEvent.firingSound = profile.fireSound;
        }

        RefreshCurrentProfileName();
        Debug.Log($"[TurretVFXProfileTester] Applied profile: {profile.displayName}", this);
    }

    public void SetProfileIndex(int index)
    {
        profileIndex = index;
        ClampProfileIndex();
        ApplyCurrentProfile();
    }

    [ContextMenu("Next Profile")]
    public void NextProfile()
    {
        if (profiles == null || profiles.Length == 0)
        {
            return;
        }

        profileIndex = (profileIndex + 1) % profiles.Length;
        ApplyCurrentProfile();
    }

    [ContextMenu("Previous Profile")]
    public void PreviousProfile()
    {
        if (profiles == null || profiles.Length == 0)
        {
            return;
        }

        profileIndex--;
        if (profileIndex < 0)
        {
            profileIndex = profiles.Length - 1;
        }

        ApplyCurrentProfile();
    }

    private void ClampProfileIndex()
    {
        if (profiles == null || profiles.Length == 0)
        {
            profileIndex = 0;
            return;
        }

        profileIndex = Mathf.Clamp(profileIndex, 0, profiles.Length - 1);
    }

    private void RefreshCurrentProfileName()
    {
        TurretVFXProfile profile = CurrentProfile;
        currentProfileName = profile != null ? profile.name : string.Empty;
    }
}
