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
    [SerializeField] private MonoBehaviour[] runtimeStatReceiverBehaviours;
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

        float modifiedDamage = GetModifiedDamage(statProfile.damage);
        if (targetTurret != null)
        {
            targetTurret.fireTick = statProfile.fireInterval;
            targetTurret.SetProjectileCombatStats(modifiedDamage, statProfile.pierceCount);

            if (targetTurret.projectilePrefab != null)
            {
                targetTurret.SetProjectilePrefab(targetTurret.projectilePrefab, statProfile.projectileSpeed);
            }

            if (isStarted)
            {
                targetTurret.SetAutoFireEnabled(autoFireEnabled);
            }
        }

        ApplyRuntimeStatReceivers(modifiedDamage);

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

        float modifiedDamage = GetModifiedDamage(runtimeStat.damage);
        if (targetTurret != null)
        {
            targetTurret.fireTick = runtimeStat.fireInterval;
            targetTurret.SetProjectileCombatStats(modifiedDamage, runtimeStat.pierceCount, logProjectileDamage);

            if (targetTurret.projectilePrefab != null)
            {
                targetTurret.SetProjectilePrefab(targetTurret.projectilePrefab, runtimeStat.projectileSpeed);
            }

            if (isStarted)
            {
                targetTurret.SetAutoFireEnabled(autoFireEnabled);
            }
        }

        ApplyRuntimeStatReceivers(modifiedDamage);

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

    // 외부 터렛 정의에서 전달한 상태이상 프로필과 레벨을 호환 컴포넌트에 적용한다
    public void SetStatusProfile(ScriptableObject statusProfile, int level)
    {
        RefreshReferences();

        if (runtimeStatReceiverBehaviours == null)
        {
            return;
        }

        int safeLevel = Mathf.Max(1, level);
        for (int i = 0; i < runtimeStatReceiverBehaviours.Length; i++)
        {
            ITurretStatusProfileReceiver statusProfileReceiver = runtimeStatReceiverBehaviours[i] as ITurretStatusProfileReceiver;
            if (statusProfileReceiver == null)
            {
                continue;
            }

            statusProfileReceiver.SetStatusProfile(statusProfile, safeLevel);
        }
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

        if (runtimeStatReceiverBehaviours == null || runtimeStatReceiverBehaviours.Length == 0)
        {
            runtimeStatReceiverBehaviours = CollectRuntimeReceiverBehaviours();
        }
    }

    // 기본 데미지에 런타임 배율을 적용한다
    private float GetModifiedDamage(float baseDamage)
    {
        return Mathf.Max(0.0f, baseDamage) * Mathf.Max(0.0f, damageMultiplier);
    }

    // 자식 컴포넌트 중 런타임 스탯 또는 상태 프로필을 받을 수 있는 컴포넌트만 수집한다
    private MonoBehaviour[] CollectRuntimeReceiverBehaviours()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        int receiverCount = 0;
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (CanReceiveRuntimeData(behaviours[i]))
            {
                receiverCount++;
            }
        }

        if (receiverCount == 0)
        {
            return System.Array.Empty<MonoBehaviour>();
        }

        MonoBehaviour[] receivers = new MonoBehaviour[receiverCount];
        int receiverIndex = 0;
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (!CanReceiveRuntimeData(behaviour))
            {
                continue;
            }

            receivers[receiverIndex] = behaviour;
            receiverIndex++;
        }

        return receivers;
    }

    // 컴포넌트가 터렛 런타임 데이터를 받을 수 있는지 확인한다
    private static bool CanReceiveRuntimeData(MonoBehaviour behaviour)
    {
        return behaviour is ITurretRuntimeStatReceiver || behaviour is ITurretStatusProfileReceiver;
    }

    // 런타임 스탯 수신 컴포넌트에 현재 초당 데미지를 전달한다
    private void ApplyRuntimeStatReceivers(float damagePerSecond)
    {
        if (runtimeStatReceiverBehaviours == null)
        {
            return;
        }

        for (int i = 0; i < runtimeStatReceiverBehaviours.Length; i++)
        {
            ITurretRuntimeStatReceiver statReceiver = runtimeStatReceiverBehaviours[i] as ITurretRuntimeStatReceiver;
            if (statReceiver == null)
            {
                continue;
            }

            statReceiver.SetDamagePerSecond(damagePerSecond);
        }
    }
}
