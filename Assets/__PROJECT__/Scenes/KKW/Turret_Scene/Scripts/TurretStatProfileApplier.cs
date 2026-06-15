using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;

/// <summary>
/// 터렛 스탯 프로필과 런타임 보정값을 실제 터렛 컴포넌트에 적용한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretStatProfileApplier : MonoBehaviour
{
    [Header("스탯 프로필")]
    [SerializeField] private TurretStatProfileSO statProfile;
    [SerializeField] private Turret targetTurret;
    [SerializeField] private TargetFinder targetFinder;
    [SerializeField] private Gun[] targetGuns;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool applyOnInspectorChange = true;

    [Header("런타임 보정")]
    [SerializeField, Min(0f)] private float damageMultiplier = 1.0f;

    public TurretStatProfileSO StatProfile
    {
        get
        {
            return statProfile;
        }
    }

    public bool HasStatProfile
    {
        get
        {
            return statProfile != null;
        }
    }

    private bool autoFireEnabled = true;
    private bool isStarted;

    // 컴포넌트 추가 시 필요한 참조를 자동으로 수집한다
    private void Reset()
    {
        RefreshReferences();
    }

    // 활성화될 때 프로필 변경 이벤트를 구독한다
    private void OnEnable()
    {
        TurretStatProfileSO.ProfileChanged += OnStatProfileChanged;
    }

    // 비활성화될 때 프로필 변경 이벤트 구독을 해제한다
    private void OnDisable()
    {
        TurretStatProfileSO.ProfileChanged -= OnStatProfileChanged;
        isStarted = false;
    }

    // 인스펙터 변경 시 현재 스탯을 다시 적용한다
    private void OnValidate()
    {
        damageMultiplier = Mathf.Max(0.0f, damageMultiplier);

        if (Application.isPlaying && applyOnInspectorChange)
        {
            Apply();
        }
    }

    // 시작 시 자동 적용 옵션에 따라 스탯을 적용한다
    private void Start()
    {
        isStarted = true;

        if (applyOnStart)
        {
            Apply();
        }
    }

    [ContextMenu("Apply Stat Profile")]
    // 설정된 스탯 프로필을 터렛에 적용한다
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
            targetTurret.SetProjectileCombatStats(GetModifiedDamage(statProfile.damage), statProfile.pierceCount);

            if (targetTurret.projectilePrefab != null)
            {
                targetTurret.SetProjectilePrefab(targetTurret.projectilePrefab, statProfile.projectileSpeed);
            }

            if (isStarted)
            {
                targetTurret.SetAutoFireEnabled(autoFireEnabled);
            }
        }

        int projectileCount = Mathf.Max(1, statProfile.projectileCount);
        if (targetGuns == null)
        {
            return;
        }

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

    // 런타임 스탯을 로그 없이 터렛에 적용한다
    public void Apply(TurretRuntimeStat runtimeStat)
    {
        Apply(runtimeStat, false);
    }

    // 런타임 스탯과 투사체 데미지 로그 옵션을 터렛에 적용한다
    public void Apply(TurretRuntimeStat runtimeStat, bool logProjectileDamage)
    {
        RefreshReferences();

        if (targetFinder != null)
        {
            targetFinder.radius = runtimeStat.range;
        }

        if (targetTurret != null)
        {
            targetTurret.fireTick = runtimeStat.fireInterval;
            targetTurret.SetProjectileCombatStats(GetModifiedDamage(runtimeStat.damage), runtimeStat.pierceCount, logProjectileDamage);

            if (targetTurret.projectilePrefab != null)
            {
                targetTurret.SetProjectilePrefab(targetTurret.projectilePrefab, runtimeStat.projectileSpeed);
            }

            if (isStarted)
            {
                targetTurret.SetAutoFireEnabled(autoFireEnabled);
            }
        }

        int projectileCount = Mathf.Max(1, runtimeStat.projectileCount);
        if (targetGuns == null)
        {
            return;
        }

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

    // 적용할 스탯 프로필을 교체하고 런타임이면 즉시 반영한다
    public void SetStatProfile(TurretStatProfileSO statProfile_)
    {
        statProfile = statProfile_;

        if (Application.isPlaying)
        {
            Apply();
        }
    }

    // 터렛 자동 발사 활성 상태를 변경한다
    public void SetAutoFireEnabled(bool enabled)
    {
        autoFireEnabled = enabled;
        RefreshReferences();

        if (targetTurret != null)
        {
            targetTurret.SetAutoFireEnabled(autoFireEnabled);
        }
    }

    // 외부 버프가 사용할 데미지 배율을 설정하고 즉시 반영한다
    public void SetDamageMultiplier(float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.0f, multiplier);
        if (Mathf.Approximately(damageMultiplier, safeMultiplier))
        {
            return;
        }

        damageMultiplier = safeMultiplier;
        Apply();
    }

    // 프로필 변경 이벤트가 현재 프로필에 해당하면 스탯을 다시 적용한다
    private void OnStatProfileChanged(TurretStatProfileSO changedProfile)
    {
        if (!Application.isPlaying || !applyOnInspectorChange || changedProfile != statProfile)
        {
            return;
        }

        Apply();
    }

    // 필요한 터렛 관련 컴포넌트 참조를 수집한다
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

    // 기본 데미지에 런타임 배율을 적용한다
    private float GetModifiedDamage(float baseDamage)
    {
        return Mathf.Max(0.0f, baseDamage) * Mathf.Max(0.0f, damageMultiplier);
    }
}
