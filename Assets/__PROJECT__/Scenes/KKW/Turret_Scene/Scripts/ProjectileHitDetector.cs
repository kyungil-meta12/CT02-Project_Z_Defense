using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 빠르게 이동하는 발사체의 누락을 줄이고 데미지 적용과 피격 이펙트 처리를 보강한다.
/// </summary>
[DisallowMultipleComponent]
public class ProjectileHitDetector : MonoBehaviour
{
    private const int GROUND_LAYER_MASK = 1 << 3;

    [SerializeField] private LayerMask environmentImpactLayerMask = GROUND_LAYER_MASK;

    private readonly List<Collider> projectileColliders = new List<Collider>(4);
    private readonly RaycastHit[] movementHits = new RaycastHit[16];

    private ProjectileDamageDealer damageDealer;
    private Hovl.HS_ProjectileMover projectileMover;
    private Collider targetCollider;
    private Vector3 previousPosition;

    // 발사체 콜라이더와 이동 컴포넌트를 캐시하고 트리거 판정을 준비한다
    private void Awake()
    {
        CacheProjectileColliders();
        CacheProjectileMover();
        ConfigureProjectileColliders();
    }

    // 발사체 재사용 시 데미지 처리기와 추적 대상을 초기화한다
    public void Init(ProjectileDamageDealer damageDealer_, GameObject target)
    {
        damageDealer = damageDealer_;
        previousPosition = transform.position;
        enabled = true;

        CacheProjectileMover();
        SetTarget(target);
        ConfigureProjectileColliders();
    }

    // 현재 발사체가 추적할 타겟 콜라이더를 갱신한다
    public void SetTarget(GameObject target)
    {
        targetCollider = null;

        if (target == null)
        {
            return;
        }

        targetCollider = target.GetComponentInChildren<Collider>();
    }

    // 물리 틱마다 추적 대상 또는 이동 경로상의 충돌을 검사한다
    private void FixedUpdate()
    {
        if (!TryApplyDamageToTrackedTarget())
        {
            TryApplyDamageAlongMovement();
        }

        previousPosition = transform.position;
    }

    // 트리거 진입 시 데미지와 피격 이펙트를 처리한다
    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamageAndReturn(other);
    }

    // 물리 충돌 시 데미지와 피격 이펙트를 처리한다
    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        TryApplyDamageAndReturn(collision.collider);
    }

    // 추적 대상의 바운드에 도달했는지 확인하고 데미지를 적용한다
    private bool TryApplyDamageToTrackedTarget()
    {
        if (targetCollider == null || !CanApplyMoreDamage())
        {
            return false;
        }

        if (!HasReachedTargetCollider())
        {
            return false;
        }

        if (TryApplyDamage(targetCollider))
        {
            bool impactHandled = TryHandleDamageImpact(targetCollider, transform.position);
            return HandleDamageApplied(impactHandled);
        }

        return false;
    }

    // 현재 이동 구간이 추적 대상 바운드와 교차했는지 검사한다
    private bool HasReachedTargetCollider()
    {
        Bounds targetBounds = targetCollider.bounds;
        Vector3 currentPosition = transform.position;

        if (targetBounds.SqrDistance(currentPosition) <= 0.0001f)
        {
            return true;
        }

        Vector3 movement = currentPosition - previousPosition;
        float movementDistance = movement.magnitude;
        if (movementDistance <= 0.0001f)
        {
            return false;
        }

        Ray movementRay = new Ray(previousPosition, movement / movementDistance);
        return targetBounds.IntersectRay(movementRay, out float hitDistance) && hitDistance <= movementDistance + 0.1f;
    }

    // 이전 위치부터 현재 위치까지 레이캐스트로 누락된 충돌을 보강한다
    private void TryApplyDamageAlongMovement()
    {
        Vector3 currentPosition = transform.position;
        Vector3 movement = currentPosition - previousPosition;
        float movementDistance = movement.magnitude;
        if (movementDistance <= 0.0001f)
        {
            return;
        }

        int hitCount = Physics.RaycastNonAlloc(
            previousPosition,
            movement / movementDistance,
            movementHits,
            movementDistance + 0.1f,
            GetHitDetectionLayerMask(),
            QueryTriggerInteraction.Collide);

        bool hasEnvironmentHit = false;
        RaycastHit nearestEnvironmentHit = default;
        float nearestEnvironmentDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = movementHits[i].collider;

            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (IsEnvironmentImpactLayer(hitCollider.gameObject.layer) && movementHits[i].distance < nearestEnvironmentDistance)
            {
                hasEnvironmentHit = true;
                nearestEnvironmentHit = movementHits[i];
                nearestEnvironmentDistance = movementHits[i].distance;
            }
        }

        if (CanApplyMoreDamage())
        {
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = movementHits[i].collider;

                if (hitCollider == null ||
                    hitCollider.transform.IsChildOf(transform) ||
                    movementHits[i].distance > nearestEnvironmentDistance + 0.0001f)
                {
                    continue;
                }

                if (TryApplyDamage(hitCollider))
                {
                    bool impactHandled = TryHandleDamageImpact(movementHits[i]);
                    if (HandleDamageApplied(impactHandled))
                    {
                        ClearMovementHits(hitCount);
                        return;
                    }
                }
            }
        }

        if (hasEnvironmentHit)
        {
            TryHandleEnvironmentImpact(nearestEnvironmentHit);
        }

        ClearMovementHits(hitCount);
    }

    // 콜라이더 기반 충돌에서 데미지와 피격 이펙트를 처리한다
    private void TryApplyDamageAndReturn(Collider hitCollider)
    {
        if (!TryApplyDamage(hitCollider))
        {
            TryHandleEnvironmentImpact(hitCollider);
            return;
        }

        bool impactHandled = TryHandleDamageImpact(hitCollider, transform.position);
        HandleDamageApplied(impactHandled);
    }

    // 충돌 콜라이더에 데미지를 적용한다
    private bool TryApplyDamage(Collider hitCollider)
    {
        return damageDealer != null && damageDealer.TryApplyDamage(hitCollider);
    }

    // 현재 발사체가 추가 데미지를 적용할 수 있는지 확인한다
    private bool CanApplyMoreDamage()
    {
        return damageDealer != null && !damageDealer.HasReachedPierceLimit;
    }

    // 관통 한계에 도달한 발사체를 비활성화하거나 풀로 반환한다
    private bool HandleDamageApplied(bool impactHandled)
    {
        if (damageDealer == null || !damageDealer.HasReachedPierceLimit)
        {
            return false;
        }

        enabled = false;
        if (!impactHandled)
        {
            PooledProjectileReturner.ReturnOrDestroy(gameObject);
        }

        return true;
    }

    // 발사체 콜라이더를 트리거로 설정해 빠른 충돌 누락을 줄인다
    private void ConfigureProjectileColliders()
    {
        for (int i = 0; i < projectileColliders.Count; i++)
        {
            Collider projectileCollider = projectileColliders[i];
            if (projectileCollider == null)
            {
                continue;
            }

            projectileCollider.isTrigger = true;
        }
    }

    // 발사체와 하위 오브젝트의 콜라이더를 캐시한다
    private void CacheProjectileColliders()
    {
        projectileColliders.Clear();
        GetComponentsInChildren(true, projectileColliders);
    }

    // HOVL 발사체 이동 컴포넌트를 캐시한다
    private void CacheProjectileMover()
    {
        if (projectileMover == null)
        {
            projectileMover = GetComponent<Hovl.HS_ProjectileMover>();
        }
    }

    // 지정한 레이어가 환경 피격 이펙트 대상인지 확인한다
    private bool IsEnvironmentImpactLayer(int layer)
    {
        return (environmentImpactLayerMask.value & (1 << layer)) != 0;
    }

    // 데미지 대상과 환경 대상을 포함한 충돌 레이어 마스크를 반환한다
    private int GetHitDetectionLayerMask()
    {
        if (damageDealer == null)
        {
            return environmentImpactLayerMask.value;
        }

        return damageDealer.DamageLayerMask.value | environmentImpactLayerMask.value;
    }

    // 레이캐스트 환경 충돌에서 기존 발사체 피격 이펙트를 실행한다
    private bool TryHandleEnvironmentImpact(RaycastHit hit)
    {
        if (hit.collider == null || !IsEnvironmentImpactLayer(hit.collider.gameObject.layer))
        {
            return false;
        }

        CacheProjectileMover();
        if (projectileMover == null)
        {
            PooledProjectileReturner.ReturnOrDestroy(gameObject);
            return true;
        }

        projectileMover.HandleExternalHit(hit.point, hit.normal);
        return true;
    }

    // 환경 충돌 처리를 시도하고 충돌 VFX를 재생한다
    private bool TryHandleEnvironmentImpact(Collider hitCollider)
    {
        if (hitCollider == null || !IsEnvironmentImpactLayer(hitCollider.gameObject.layer))
        {
            return false;
        }

        Vector3 hitPoint = GetClosestPointOnCollider(hitCollider, transform.position);
        Vector3 hitNormal = transform.position - hitPoint;
        if (hitNormal.sqrMagnitude <= 0.0001f)
        {
            hitNormal = -transform.forward;
        }

        CacheProjectileMover();
        if (projectileMover == null)
        {
            PooledProjectileReturner.ReturnOrDestroy(gameObject);
            return true;
        }

        if (!gameObject.activeInHierarchy)
        {
            return true;
        }

        projectileMover.HandleExternalHit(hitPoint, hitNormal.normalized);
        return true;
    }

    // 레이캐스트 데미지 충돌에서 기존 발사체 피격 이펙트를 실행한다
    private bool TryHandleDamageImpact(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        Vector3 hitNormal = hit.normal;
        if (hitNormal.sqrMagnitude <= 0.0001f)
        {
            hitNormal = transform.position - hit.point;
        }

        return TryHandleDamageImpact(hit.point, hitNormal);
    }

    // 콜라이더 데미지 충돌에서 기존 발사체 피격 이펙트를 실행한다
    private bool TryHandleDamageImpact(Collider hitCollider, Vector3 fallbackPosition)
    {
        if (hitCollider == null)
        {
            return false;
        }

        Vector3 hitPoint = GetClosestPointOnCollider(hitCollider, fallbackPosition);
        Vector3 hitNormal = fallbackPosition - hitPoint;
        return TryHandleDamageImpact(hitPoint, hitNormal);
    }

    // 데미지 충돌 지점과 법선을 기준으로 기존 발사체 피격 이펙트를 실행한다
    private bool TryHandleDamageImpact(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (damageDealer == null || !damageDealer.HasReachedPierceLimit)
        {
            return false;
        }

        CacheProjectileMover();
        if (projectileMover == null || !gameObject.activeInHierarchy)
        {
            return false;
        }

        if (hitNormal.sqrMagnitude <= 0.0001f)
        {
            hitNormal = -transform.forward;
        }

        projectileMover.HandleExternalHit(hitPoint, hitNormal.normalized);
        return true;
    }

    // 콜라이더 타입에 따라 안전하게 가장 가까운 점을 반환한다
    private static Vector3 GetClosestPointOnCollider(Collider collider, Vector3 position)
    {
        if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
        {
            return collider.ClosestPoint(position);
        }

        MeshCollider meshCollider = collider as MeshCollider;
        if (meshCollider != null && meshCollider.convex)
        {
            return collider.ClosestPoint(position);
        }

        return collider.bounds.ClosestPoint(position);
    }

    // 재사용 버퍼에 남은 레이캐스트 결과를 초기화한다
    private void ClearMovementHits(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            movementHits[i] = default;
        }
    }
}
