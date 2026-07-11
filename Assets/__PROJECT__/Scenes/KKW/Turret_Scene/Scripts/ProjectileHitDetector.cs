using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 빠르게 이동하는 투사체의 충돌 누락을 보정하고 데미지 적용 후 HOVL 피격 처리 경로로 연결한다.
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

    // 투사체 콜라이더와 HOVL 이동 컴포넌트를 캐싱하고 보정 충돌 설정을 적용한다
    private void Awake()
    {
        CacheProjectileColliders();
        CacheProjectileMover();
        ConfigureProjectileColliders();
    }

    // 투사체가 발사될 때 데미지 처리기와 추적 대상을 초기화한다
    public void Init(ProjectileDamageDealer damageDealer_, GameObject target)
    {
        damageDealer = damageDealer_;
        previousPosition = transform.position;
        enabled = true;

        CacheProjectileMover();
        SetTarget(target);
        ConfigureProjectileColliders();
    }

    // 현재 투사체가 보정 판정할 추적 대상 콜라이더를 갱신한다
    public void SetTarget(GameObject target)
    {
        targetCollider = null;

        if (target == null)
        {
            return;
        }

        targetCollider = target.GetComponentInChildren<Collider>();
    }

    // 물리 틱마다 추적 대상과 이동 경로를 보정 판정한다
    private void FixedUpdate()
    {
        if (!TryApplyDamageToTrackedTarget())
        {
            TryApplyDamageAlongMovement();
        }

        previousPosition = transform.position;
    }

    // 트리거 충돌 시 데미지 또는 환경 충돌 처리를 시도한다
    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamageAndReturn(other);
    }

    // 물리 충돌 시 데미지 또는 환경 충돌 처리를 시도한다
    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        TryApplyDamageAndReturn(collision.collider);
    }

    // 추적 대상 콜라이더에 도달했는지 확인하고 데미지를 적용한다
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
            return HandleDamageApplied(targetCollider);
        }

        return false;
    }

    // 이전 위치와 현재 위치 사이에서 대상 콜라이더에 도달했는지 계산한다
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

    // 이전 위치에서 현재 위치까지 레이캐스트해 빠른 투사체의 충돌 누락을 보정한다
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

                if (TryApplyDamage(hitCollider) && HandleDamageApplied(movementHits[i]))
                {
                    ClearMovementHits(hitCount);
                    return;
                }
            }
        }

        if (hasEnvironmentHit)
        {
            TryHandleEnvironmentImpact(nearestEnvironmentHit);
        }

        ClearMovementHits(hitCount);
    }

    // 단일 콜라이더 충돌에서 데미지 적용 후 필요한 경우 투사체를 종료한다
    private void TryApplyDamageAndReturn(Collider hitCollider)
    {
        if (!TryApplyDamage(hitCollider))
        {
            TryHandleEnvironmentImpact(hitCollider);
            return;
        }

        HandleDamageApplied(hitCollider);
    }

    // 데미지 처리기를 통해 대상 콜라이더에 데미지를 적용한다
    private bool TryApplyDamage(Collider hitCollider)
    {
        return damageDealer != null && damageDealer.TryApplyDamage(hitCollider);
    }

    // 투사체가 추가 대상에게 데미지를 줄 수 있는지 확인한다
    private bool CanApplyMoreDamage()
    {
        return damageDealer != null && !damageDealer.HasReachedPierceLimit;
    }

    // 데미지 적용 후 관통 한계에 도달하면 HOVL 피격 처리 루틴으로 종료한다
    private bool HandleDamageApplied(Collider hitCollider)
    {
        if (damageDealer == null || !damageDealer.HasReachedPierceLimit)
        {
            PlayProjectilePierceHitEffect(hitCollider);
            return false;
        }

        enabled = false;
        HandleProjectileImpact(hitCollider);
        return true;
    }

    // 데미지 적용 후 관통 한계에 도달하면 RaycastHit 기준으로 HOVL 피격 처리 루틴을 호출한다
    private bool HandleDamageApplied(RaycastHit hit)
    {
        if (damageDealer == null || !damageDealer.HasReachedPierceLimit)
        {
            PlayProjectilePierceHitEffect(hit);
            return false;
        }

        enabled = false;
        HandleProjectileImpact(hit.point, hit.normal);
        return true;
    }

    // 관통 중간 타격에서 투사체를 종료하지 않고 피격 이펙트만 재생한다
    private void PlayProjectilePierceHitEffect(Collider hitCollider)
    {
        Vector3 hitPoint = hitCollider == null ? transform.position : GetClosestPointOnCollider(hitCollider, transform.position);
        Vector3 hitNormal = transform.position - hitPoint;
        if (hitNormal.sqrMagnitude <= 0.0001f)
        {
            hitNormal = -transform.forward;
        }

        PlayProjectilePierceHitEffect(hitPoint, hitNormal.normalized);
    }

    // 관통 중간 타격에서 RaycastHit 기준으로 피격 이펙트만 재생한다
    private void PlayProjectilePierceHitEffect(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return;
        }

        PlayProjectilePierceHitEffect(hit.point, hit.normal);
    }

    // HOVL 투사체에 관통 중간 피격 이펙트 재생을 요청한다
    private void PlayProjectilePierceHitEffect(Vector3 hitPoint, Vector3 hitNormal)
    {
        CacheProjectileMover();
        if (projectileMover == null || !gameObject.activeInHierarchy)
        {
            return;
        }

        HovlProjectileHitEffectUtility.Play(projectileMover, hitPoint, hitNormal);
    }

    // 데미지 피격 콜라이더에서 충돌 위치와 방향을 계산해 HOVL 피격 처리로 넘긴다
    private void HandleProjectileImpact(Collider hitCollider)
    {
        Vector3 hitPoint = hitCollider == null ? transform.position : GetClosestPointOnCollider(hitCollider, transform.position);
        Vector3 hitNormal = transform.position - hitPoint;
        if (hitNormal.sqrMagnitude <= 0.0001f)
        {
            hitNormal = -transform.forward;
        }

        HandleProjectileImpact(hitPoint, hitNormal.normalized);
    }

    // HOVL 투사체의 공통 피격 루틴을 호출하고 없으면 풀 반환으로 대체한다
    private void HandleProjectileImpact(Vector3 hitPoint, Vector3 hitNormal)
    {
        PlayImpactAudio();
        CacheProjectileMover();
        if (projectileMover == null)
        {
            PooledProjectileReturner.ReturnOrDestroy(gameObject);
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        projectileMover.HandleExternalHit(hitPoint, hitNormal);
    }

    // 최종 투사체 충돌 사운드를 재생한다
    private void PlayImpactAudio()
    {
        if (damageDealer == null)
        {
            return;
        }

        damageDealer.PlayImpactAudio(transform);
    }

    // 투사체 하위 콜라이더를 트리거로 설정해 보정 판정과 충돌 판정을 함께 사용한다
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

    // 투사체 자신과 하위 콜라이더를 캐싱한다
    private void CacheProjectileColliders()
    {
        projectileColliders.Clear();
        GetComponentsInChildren(true, projectileColliders);
    }

    // HOVL 투사체 이동 컴포넌트를 캐싱한다
    private void CacheProjectileMover()
    {
        if (projectileMover == null)
        {
            projectileMover = GetComponent<Hovl.HS_ProjectileMover>();
        }
    }

    // 지정 레이어가 환경 피격 레이어에 포함되는지 확인한다
    private bool IsEnvironmentImpactLayer(int layer)
    {
        return (environmentImpactLayerMask.value & (1 << layer)) != 0;
    }

    // 데미지 레이어와 환경 피격 레이어를 합친 보정 판정 마스크를 반환한다
    private int GetHitDetectionLayerMask()
    {
        if (damageDealer == null)
        {
            return environmentImpactLayerMask.value;
        }

        return damageDealer.DamageLayerMask.value | environmentImpactLayerMask.value;
    }

    // RaycastHit 기반 환경 충돌을 HOVL 피격 처리로 넘긴다
    private bool TryHandleEnvironmentImpact(RaycastHit hit)
    {
        if (hit.collider == null || !IsEnvironmentImpactLayer(hit.collider.gameObject.layer))
        {
            return false;
        }

        CacheProjectileMover();
        if (projectileMover == null)
        {
            PlayImpactAudio();
            PooledProjectileReturner.ReturnOrDestroy(gameObject);
            return true;
        }

        PlayImpactAudio();
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
            PlayImpactAudio();
            PooledProjectileReturner.ReturnOrDestroy(gameObject);
            return true;
        }

        if (!gameObject.activeInHierarchy)
        {
            return true;
        }

        PlayImpactAudio();
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

    // 재사용되는 RaycastHit 버퍼의 사용 구간을 비운다
    private void ClearMovementHits(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            movementHits[i] = default;
        }
    }
}
