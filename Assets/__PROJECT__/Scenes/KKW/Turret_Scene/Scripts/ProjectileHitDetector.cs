using System.Collections.Generic;
using UnityEngine;

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

    private void Awake()
    {
        CacheProjectileColliders();
        CacheProjectileMover();
        ConfigureProjectileColliders();
    }

    public void Init(ProjectileDamageDealer damageDealer_, GameObject target)
    {
        damageDealer = damageDealer_;
        previousPosition = transform.position;
        enabled = true;

        CacheProjectileMover();
        SetTarget(target);
        ConfigureProjectileColliders();
    }

    public void SetTarget(GameObject target)
    {
        targetCollider = null;

        if (target == null)
        {
            return;
        }

        targetCollider = target.GetComponentInChildren<Collider>();
    }

    private void FixedUpdate()
    {
        if (!TryApplyDamageToTrackedTarget())
        {
            TryApplyDamageAlongMovement();
        }

        previousPosition = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamageAndReturn(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        TryApplyDamageAndReturn(collision.collider);
    }

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
            return HandleDamageApplied();
        }

        return false;
    }

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
            Physics.DefaultRaycastLayers,
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

                if (TryApplyDamage(hitCollider) && HandleDamageApplied())
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

    private void TryApplyDamageAndReturn(Collider hitCollider)
    {
        if (!TryApplyDamage(hitCollider))
        {
            TryHandleEnvironmentImpact(hitCollider);
            return;
        }

        HandleDamageApplied();
    }

    private bool TryApplyDamage(Collider hitCollider)
    {
        return damageDealer != null && damageDealer.TryApplyDamage(hitCollider);
    }

    private bool CanApplyMoreDamage()
    {
        return damageDealer != null && !damageDealer.HasReachedPierceLimit;
    }

    private bool HandleDamageApplied()
    {
        if (damageDealer == null || !damageDealer.HasReachedPierceLimit)
        {
            return false;
        }

        enabled = false;
        PooledProjectileReturner.ReturnOrDestroy(gameObject);
        return true;
    }

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

    private void CacheProjectileColliders()
    {
        projectileColliders.Clear();
        GetComponentsInChildren(true, projectileColliders);
    }

    private void CacheProjectileMover()
    {
        if (projectileMover == null)
        {
            projectileMover = GetComponent<Hovl.HS_ProjectileMover>();
        }
    }

    private bool IsEnvironmentImpactLayer(int layer)
    {
        return (environmentImpactLayerMask.value & (1 << layer)) != 0;
    }

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

    private bool TryHandleEnvironmentImpact(Collider hitCollider)
    {
        if (hitCollider == null || !IsEnvironmentImpactLayer(hitCollider.gameObject.layer))
        {
            return false;
        }

        Vector3 hitPoint = hitCollider.ClosestPoint(transform.position);
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

        projectileMover.HandleExternalHit(hitPoint, hitNormal.normalized);
        return true;
    }

    private void ClearMovementHits(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            movementHits[i] = default;
        }
    }
}
