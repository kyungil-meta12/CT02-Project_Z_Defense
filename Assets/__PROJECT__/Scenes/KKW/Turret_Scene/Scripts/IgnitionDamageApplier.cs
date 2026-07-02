using ProjectZDefense.StatusEffects;
using UnityEngine;

/// <summary>
/// IgnitionConeDetector가 감지한 대상에게 일정 주기마다 화염 데미지를 적용한다.
/// </summary>
public sealed class IgnitionDamageApplier : MonoBehaviour, ITurretRuntimeStatReceiver, ITurretStatusProfileReceiver
{
    [Header("감지 참조")]
    [SerializeField] private IgnitionConeDetector detector;

    [Header("데미지")]
    [SerializeField, Min(0.0f)] private float damagePerSecond = 10.0f;
    [SerializeField, Min(0.01f)] private float damageTickInterval = 0.2f;
    [SerializeField, Min(1)] private int targetBufferSize = 16;
    [Tooltip("테스트 대상처럼 Ignition 수신자가 없는 대상에게만 직접 데미지를 적용합니다. 실전 좀비는 꺼둡니다.")]
    [SerializeField] private bool useDirectDamageFallback;
    [SerializeField] private bool logDamage;

    [Header("런타임 주입 상태이상")]
    [Tooltip("TurretDefinitionRuntimeController가 Definition의 Ignition Status Profile을 런타임에 주입합니다. 프리팹에서는 비워둬도 됩니다.")]
    [SerializeField] private IgnitionStatusProfileSO ignitionStatusProfile;
    [Tooltip("런타임에 현재 터렛 레벨로 갱신됩니다.")]
    [SerializeField, Min(1)] private int ignitionStatusLevel = 1;

    private IDamageable[] pendingTargets;
    private float damageTickTimer;
    private IgnitionStatusPayload ignitionStatusPayload;
    private TurretStatGrowthProfileSO statGrowthProfile;
    private TurretDamageMeterSource damageMeterSource;
    private bool hasLoggedMissingDamagePath;

    // 시작 시 감지 이벤트를 구독하고 첫 데미지 틱을 준비한다
    private void Awake()
    {
        CacheReferences();
        EnsureBuffers();
        RefreshIgnitionStatusPayload();
    }

    // 활성화될 때 감지 이벤트를 구독한다
    private void OnEnable()
    {
        CacheReferences();
        if (detector != null)
        {
            detector.TargetDetected += HandleTargetDetected;
        }
    }

    // 비활성화될 때 감지 이벤트 구독을 해제한다
    private void OnDisable()
    {
        if (detector != null)
        {
            detector.TargetDetected -= HandleTargetDetected;
        }
    }

#if UNITY_EDITOR
    // 인스펙터 변경 시 데미지 설정을 안전한 값으로 보정한다
    private void OnValidate()
    {
        damagePerSecond = Mathf.Max(0.0f, damagePerSecond);
        damageTickInterval = Mathf.Max(0.01f, damageTickInterval);
        targetBufferSize = Mathf.Max(1, targetBufferSize);
        ignitionStatusLevel = Mathf.Max(1, ignitionStatusLevel);
        RefreshIgnitionStatusPayload();
    }
#endif

    // 데미지 틱 주기에 맞춰 감지된 대상들에게 일괄 데미지를 적용한다
    private void Update()
    {
        damageTickTimer -= Time.deltaTime;
        if (damageTickTimer > 0.0f)
        {
            return;
        }

        damageTickTimer += damageTickInterval;
        ApplyPendingDamage();
        ClearPendingTargets();
    }

    // 필요한 감지 컴포넌트 참조를 캐시한다
    private void CacheReferences()
    {
        if (detector == null)
        {
            detector = GetComponent<IgnitionConeDetector>();
        }
    }

    // 데미지 적용에 사용할 대상 버퍼를 준비한다
    private void EnsureBuffers()
    {
        if (pendingTargets == null || pendingTargets.Length != targetBufferSize)
        {
            pendingTargets = new IDamageable[targetBufferSize];
        }
    }

    // 감지된 대상을 이번 데미지 틱 후보에 추가한다
    private void HandleTargetDetected(IDamageable target, Collider detectedCollider, Transform muzzle)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        EnsureBuffers();
        if (TryApplyIgnitionStatus(target, detectedCollider))
        {
            return;
        }

        if (!useDirectDamageFallback)
        {
            LogMissingDamagePathOnce();
            return;
        }

        AddPendingTarget(target);
    }

    // 외부 스탯 시스템에서 전달한 초당 화염 데미지를 적용한다
    public void SetDamagePerSecond(float damagePerSecond_)
    {
        damagePerSecond = Mathf.Max(0.0f, damagePerSecond_);
        RefreshIgnitionStatusPayload();
    }

    // 외부 터렛 정의에서 사용할 Ignition 상태 프로필과 현재 레벨을 설정한다
    public void SetIgnitionStatusProfile(IgnitionStatusProfileSO ignitionStatusProfile_, int level, TurretStatGrowthProfileSO growthProfile)
    {
        ignitionStatusProfile = ignitionStatusProfile_;
        ignitionStatusLevel = Mathf.Max(1, level);
        statGrowthProfile = growthProfile;
        RefreshIgnitionStatusPayload();
    }

    // 외부 터렛 정의에서 딜 미터기 출처를 설정한다
    public void SetDamageMeterSource(TurretDamageMeterSource damageMeterSource_)
    {
        damageMeterSource = damageMeterSource_;
        RefreshIgnitionStatusPayload();
    }

    // 외부 터렛 정의에서 전달한 상태 프로필이 Ignition이면 현재 레벨과 함께 적용한다
    public void SetStatusProfile(ScriptableObject statusProfile, int level, TurretStatGrowthProfileSO growthProfile)
    {
        SetIgnitionStatusProfile(statusProfile as IgnitionStatusProfileSO, level, growthProfile);
    }

    // 감지된 대상이 Ignition 상태를 받을 수 있으면 연소 payload를 전달한다
    private bool TryApplyIgnitionStatus(IDamageable target, Collider detectedCollider)
    {
        if (!ignitionStatusPayload.hasIgnitionStatus)
        {
            return false;
        }

        IIgnitionStatusEffectReceiver ignitionReceiver = target as IIgnitionStatusEffectReceiver;
        if (ignitionReceiver == null && detectedCollider != null)
        {
            ignitionReceiver = detectedCollider.GetComponentInParent<IIgnitionStatusEffectReceiver>();
        }

        if (ignitionReceiver == null)
        {
            return false;
        }

        ignitionReceiver.ApplyIgnitionStatus(ignitionStatusPayload);
        return true;
    }

    // 현재 데미지와 프로필 기준으로 Ignition 상태 payload를 갱신한다
    private void RefreshIgnitionStatusPayload()
    {
        if (ignitionStatusProfile == null)
        {
            ignitionStatusPayload = default;
            return;
        }

        ignitionStatusPayload = ignitionStatusProfile.CreatePayload(ignitionStatusLevel, damagePerSecond, statGrowthProfile);
        ignitionStatusPayload.damageSource = damageMeterSource;
        hasLoggedMissingDamagePath = false;
    }

    // 상태 프로필과 직접 데미지 fallback이 모두 없을 때 설정 문제를 한 번만 알린다
    private void LogMissingDamagePathOnce()
    {
        if (hasLoggedMissingDamagePath)
        {
            return;
        }

        hasLoggedMissingDamagePath = true;
        Debug.LogWarning("Ignition 데미지 경로가 없습니다. Ignition Status Profile을 연결하거나 테스트용 Direct Damage Fallback을 켜주세요.", this);
    }

    // 중복 없이 데미지 대상을 버퍼에 추가한다
    private void AddPendingTarget(IDamageable target)
    {
        for (int i = 0; i < pendingTargets.Length; i++)
        {
            if (pendingTargets[i] == target)
            {
                return;
            }

            if (pendingTargets[i] == null)
            {
                pendingTargets[i] = target;
                return;
            }
        }
    }

    // 버퍼에 모인 모든 대상에게 데미지를 적용한다
    private void ApplyPendingDamage()
    {
        if (!useDirectDamageFallback || pendingTargets == null)
        {
            return;
        }

        float damage = damagePerSecond * damageTickInterval;
        for (int i = 0; i < pendingTargets.Length; i++)
        {
            IDamageable target = pendingTargets[i];
            if (target == null || !target.IsAlive)
            {
                continue;
            }

            target.TakeDamage(new DamageInfo(damage, DamagePopupType.Normal, DamagePopupPolicyResolver.ResolveAreaOfEffect(), damageMeterSource));

            if (logDamage)
            {
                Debug.Log($"화염 원뿔 데미지 적용: {damage:0.###}", this);
            }
        }
    }

    // 다음 감지 주기를 위해 대상 버퍼를 초기화한다
    private void ClearPendingTargets()
    {
        if (pendingTargets == null)
        {
            return;
        }

        for (int i = 0; i < pendingTargets.Length; i++)
        {
            pendingTargets[i] = null;
        }
    }
}


