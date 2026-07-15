using System.Reflection;
using Hovl;
using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;

/// <summary>
/// 투사체의 레거시 개별 수명 타이머를 긴 failsafe 반환 정책으로 통일한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Project Z/Turret/Projectile Lifetime Policy (투사체 수명 정책)")]
public class ProjectileLifetimePolicy : MonoBehaviour
{
    private const float MIN_FAILSAFE_LIFETIME = 1.0f;
    private const float DEFAULT_FAILSAFE_LIFETIME = 30.0f;
    private const string PROJECTILE_MOVEMENT_LIFE_END_METHOD = "OnLifeSpanTimeEnd";
    private const string ROCKET_LIFE_END_METHOD = "OnLifeSpanTimeEnd";

    private static readonly FieldInfo HovlLifeTimeField = typeof(HS_ProjectileMover).GetField("lifeTime", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo HovlStopRunningCoroutinesMethod = typeof(HS_ProjectileMover).GetMethod("StopRunningCoroutines", BindingFlags.Instance | BindingFlags.NonPublic);

    [Header("투사체 수명")]
    [Tooltip("빗나간 투사체가 경계에 닿지 못했을 때 풀 누수를 막기 위한 최종 반환 시간입니다. 사거리 조정값으로 사용하지 않습니다.")]
    [SerializeField, Min(MIN_FAILSAFE_LIFETIME)] private float failsafeLifetime = DEFAULT_FAILSAFE_LIFETIME;

    private PooledProjectileReturner returner;
    private ProjectileMovement projectileMovement;
    private RocketProjectileMovement rocketProjectileMovement;
    private HS_ProjectileMover hovlProjectileMover;

    // 투사체가 활성화될 때 긴 failsafe 반환 타이머를 적용한다
    private void OnEnable()
    {
        ApplyPolicy();
    }

    // 인스펙터 또는 런타임에서 failsafe 수명을 설정한다
    public void SetFailsafeLifetime(float lifetime)
    {
        failsafeLifetime = Mathf.Max(MIN_FAILSAFE_LIFETIME, lifetime);
        ApplyPolicy();
    }

    // 현재 투사체에 연결된 레거시 수명 타이머를 정리하고 공통 failsafe 반환을 예약한다
    public void ApplyPolicy()
    {
        CacheComponents();
        DisableLegacyLifetimeTimers();
        ScheduleFailsafeReturn();
    }

    // 반복 사용되는 투사체 컴포넌트를 캐시한다
    private void CacheComponents()
    {
        if (returner == null)
        {
            returner = GetComponent<PooledProjectileReturner>();
        }

        if (projectileMovement == null)
        {
            projectileMovement = GetComponent<ProjectileMovement>();
        }

        if (rocketProjectileMovement == null)
        {
            rocketProjectileMovement = GetComponent<RocketProjectileMovement>();
        }

        if (hovlProjectileMover == null)
        {
            hovlProjectileMover = GetComponent<HS_ProjectileMover>();
        }
    }

    // 기존 mover들이 개별로 예약한 짧은 수명 타이머를 취소한다
    private void DisableLegacyLifetimeTimers()
    {
        float safeLifetime = Mathf.Max(MIN_FAILSAFE_LIFETIME, failsafeLifetime);

        if (projectileMovement != null)
        {
            projectileMovement.lifeSpan = safeLifetime;
            projectileMovement.CancelInvoke(PROJECTILE_MOVEMENT_LIFE_END_METHOD);
        }

        if (rocketProjectileMovement != null)
        {
            rocketProjectileMovement.lifeSpan = safeLifetime;
            rocketProjectileMovement.CancelInvoke(ROCKET_LIFE_END_METHOD);
        }

        if (hovlProjectileMover != null)
        {
            SetHovlFailsafeLifetime(safeLifetime);
            StopHovlLifetimeRoutine();
        }
    }

    // HOVL 투사체의 내부 lifeTime 값을 긴 failsafe 값으로 갱신한다
    private void SetHovlFailsafeLifetime(float safeLifetime)
    {
        if (HovlLifeTimeField == null || hovlProjectileMover == null)
        {
            return;
        }

        HovlLifeTimeField.SetValue(hovlProjectileMover, safeLifetime);
    }

    // HOVL 투사체가 OnEnable에서 시작한 기존 수명 코루틴을 정리한다
    private void StopHovlLifetimeRoutine()
    {
        if (HovlStopRunningCoroutinesMethod == null || hovlProjectileMover == null)
        {
            return;
        }

        HovlStopRunningCoroutinesMethod.Invoke(hovlProjectileMover, null);
    }

    // 공통 풀 반환 컴포넌트에 긴 failsafe 반환을 예약한다
    private void ScheduleFailsafeReturn()
    {
        if (returner == null)
        {
            return;
        }

        returner.ReturnAfter(Mathf.Max(MIN_FAILSAFE_LIFETIME, failsafeLifetime));
    }
}
