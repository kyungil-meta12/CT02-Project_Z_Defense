using System;
using UnityEngine;

/// <summary>
/// 터렛 총구 pivot들을 기준으로 원뿔 범위 안의 데미지 대상을 감지하고 이벤트로 전달한다.
/// </summary>
public sealed class IgnitionConeDetector : MonoBehaviour
{
    [Header("리그 참조")]
    [SerializeField] private TurretRigBinding rigBinding;

    [Header("감지 범위")]
    [SerializeField, Min(0.1f)] private float range = 5.0f;
    [SerializeField, Range(1.0f, 179.0f)] private float coneAngle = 24.0f;
    [SerializeField, Min(0.01f)] private float scanInterval = 0.1f;
    [SerializeField, Min(1)] private int candidateBufferSize = 32;
    [SerializeField, Min(1)] private int targetBufferSize = 16;
    [SerializeField] private LayerMask targetLayerMask = Physics.DefaultRaycastLayers;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("기즈모")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawOnlyWhenSelected = true;
    [SerializeField] private int gizmoSegments = 16;
    [SerializeField] private Color gizmoColor = new Color(1.0f, 0.35f, 0.0f, 0.2f);
    [SerializeField] private Color gizmoLineColor = new Color(1.0f, 0.15f, 0.0f, 0.85f);

    private Collider[] candidateBuffer;
    private IDamageable[] detectedTargets;
    private Collider[] detectedColliders;
    private Transform[] detectedMuzzles;
    private float scanTimer;
    private float halfAngleCos;

    public event Action<IDamageable, Collider, Transform> TargetDetected;

    // 컴포넌트를 초기화하고 감지 버퍼와 원뿔 각도 캐시를 준비한다
    private void Awake()
    {
        CacheReferences();
        EnsureBuffers();
        CacheConeAngle();
    }

#if UNITY_EDITOR
    // 인스펙터 변경 시 감지 설정과 버퍼 크기를 안전한 값으로 보정한다
    private void OnValidate()
    {
        range = Mathf.Max(0.1f, range);
        coneAngle = Mathf.Clamp(coneAngle, 1.0f, 179.0f);
        scanInterval = Mathf.Max(0.01f, scanInterval);
        candidateBufferSize = Mathf.Max(1, candidateBufferSize);
        targetBufferSize = Mathf.Max(1, targetBufferSize);
        gizmoSegments = Mathf.Max(4, gizmoSegments);
        CacheConeAngle();
    }
#endif

    // 정해진 주기마다 원뿔 범위 안의 대상을 감지한다
    private void Update()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer > 0.0f)
        {
            return;
        }

        scanTimer += scanInterval;
        ScanCones();
    }

    // Scene View에서 선택 여부와 무관하게 표시할 기즈모를 그린다
    private void OnDrawGizmos()
    {
        if (!drawGizmos || drawOnlyWhenSelected)
        {
            return;
        }

        DrawConeGizmos();
    }

    // Scene View에서 선택된 동안 원뿔 범위 기즈모를 그린다
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        DrawConeGizmos();
    }

    // 필요한 런타임 참조를 캐시한다
    private void CacheReferences()
    {
        if (rigBinding == null)
        {
            rigBinding = GetComponent<TurretRigBinding>();
        }
    }

    // NonAlloc 감지와 중복 제거에 사용할 버퍼를 준비한다
    private void EnsureBuffers()
    {
        if (candidateBuffer == null || candidateBuffer.Length != candidateBufferSize)
        {
            candidateBuffer = new Collider[candidateBufferSize];
        }

        if (detectedTargets == null || detectedTargets.Length != targetBufferSize)
        {
            detectedTargets = new IDamageable[targetBufferSize];
            detectedColliders = new Collider[targetBufferSize];
            detectedMuzzles = new Transform[targetBufferSize];
        }
    }

    // 원뿔 각도 판정에 사용할 cos 값을 캐시한다
    private void CacheConeAngle()
    {
        halfAngleCos = Mathf.Cos(coneAngle * 0.5f * Mathf.Deg2Rad);
    }

    // 모든 총구 원뿔을 검사하고 감지 이벤트를 발행한다
    private void ScanCones()
    {
        CacheReferences();
        EnsureBuffers();
        ClearDetectedTargets();

        if (rigBinding == null)
        {
            return;
        }

        int muzzleCount = rigBinding.GetMuzzlePivotCount();
        for (int i = 0; i < muzzleCount; i++)
        {
            if (!rigBinding.TryGetMuzzlePivot(i, out Transform muzzle) || muzzle == null)
            {
                continue;
            }

            ScanSingleCone(muzzle);
        }

        DispatchDetectedTargets();
    }

    // 단일 총구의 원뿔 범위 안에 있는 대상을 버퍼에 추가한다
    private void ScanSingleCone(Transform muzzle)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            muzzle.position,
            range,
            candidateBuffer,
            targetLayerMask,
            triggerInteraction);

        Vector3 origin = muzzle.position;
        Vector3 forward = muzzle.forward;

        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = candidateBuffer[i];
            if (candidate == null)
            {
                continue;
            }

            Vector3 targetPoint = candidate.bounds.ClosestPoint(origin);
            if (!IsInsideCone(origin, forward, targetPoint))
            {
                continue;
            }

            IDamageable damageable = ResolveDamageable(candidate);
            if (damageable == null || !damageable.IsAlive)
            {
                continue;
            }

            AddDetectedTarget(damageable, candidate, muzzle);
        }
    }

    // 지정한 위치가 수평 부채꼴 내부인지 검사한다
    private bool IsInsideCone(Vector3 origin, Vector3 forward, Vector3 targetPoint)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        Vector3 flatTargetPoint = new Vector3(targetPoint.x, origin.y, targetPoint.z);
        Vector3 toTarget = flatTargetPoint - origin;
        float distanceSqr = toTarget.sqrMagnitude;
        if (distanceSqr <= 0.0001f || distanceSqr > range * range)
        {
            return false;
        }

        if (flatForward.sqrMagnitude <= 0.0001f)
        {
            flatForward = Vector3.forward;
        }

        float dot = Vector3.Dot(flatForward.normalized, toTarget.normalized);
        return dot >= halfAngleCos;
    }

    // 콜라이더에서 데미지 대상 인터페이스를 찾는다
    private static IDamageable ResolveDamageable(Collider candidate)
    {
        return candidate.GetComponentInParent<IDamageable>();
    }

    // 이번 스캔에서 감지된 대상 버퍼를 초기화한다
    private void ClearDetectedTargets()
    {
        for (int i = 0; i < detectedTargets.Length; i++)
        {
            detectedTargets[i] = null;
            detectedColliders[i] = null;
            detectedMuzzles[i] = null;
        }
    }

    // 중복 대상을 제외하고 감지 대상 버퍼에 추가한다
    private void AddDetectedTarget(IDamageable damageable, Collider detectedCollider, Transform muzzle)
    {
        for (int i = 0; i < detectedTargets.Length; i++)
        {
            if (detectedTargets[i] == damageable)
            {
                return;
            }

            if (detectedTargets[i] == null)
            {
                detectedTargets[i] = damageable;
                detectedColliders[i] = detectedCollider;
                detectedMuzzles[i] = muzzle;
                return;
            }
        }
    }

    // 이번 스캔에서 감지된 대상 이벤트를 발행한다
    private void DispatchDetectedTargets()
    {
        if (TargetDetected == null)
        {
            return;
        }

        for (int i = 0; i < detectedTargets.Length; i++)
        {
            IDamageable target = detectedTargets[i];
            if (target == null)
            {
                continue;
            }

            TargetDetected.Invoke(target, detectedColliders[i], detectedMuzzles[i]);
        }
    }

    // 모든 총구에 대해 원뿔 범위 기즈모를 그린다
    private void DrawConeGizmos()
    {
        CacheReferences();
        CacheConeAngle();

        if (rigBinding == null)
        {
            DrawFallbackConeGizmo(transform);
            return;
        }

        int muzzleCount = rigBinding.GetMuzzlePivotCount();
        for (int i = 0; i < muzzleCount; i++)
        {
            if (rigBinding.TryGetMuzzlePivot(i, out Transform muzzle) && muzzle != null)
            {
                DrawSingleConeGizmo(muzzle);
            }
        }
    }

    // 리그 참조가 없을 때 현재 Transform 기준의 임시 원뿔 기즈모를 그린다
    private void DrawFallbackConeGizmo(Transform fallbackTransform)
    {
        if (fallbackTransform == null)
        {
            return;
        }

        DrawSingleConeGizmo(fallbackTransform);
    }

    // 단일 총구의 수평 부채꼴 기즈모를 중심선과 좌우 경계, 거리 원호로 그린다
    private void DrawSingleConeGizmo(Transform muzzle)
    {
        Vector3 origin = muzzle.position;
        Vector3 forward = Vector3.ProjectOnPlane(muzzle.forward, Vector3.up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        float halfAngle = coneAngle * 0.5f;
        Quaternion leftRotation = Quaternion.AngleAxis(-halfAngle, Vector3.up);
        Quaternion rightRotation = Quaternion.AngleAxis(halfAngle, Vector3.up);
        Vector3 leftDirection = leftRotation * forward;
        Vector3 rightDirection = rightRotation * forward;
        Vector3 centerEnd = origin + forward * range;
        Vector3 leftEnd = origin + leftDirection * range;
        Vector3 rightEnd = origin + rightDirection * range;

        Gizmos.color = gizmoLineColor;
        Gizmos.DrawLine(origin, centerEnd);
        Gizmos.DrawLine(origin, leftEnd);
        Gizmos.DrawLine(origin, rightEnd);

        Vector3 previousPoint = leftEnd;
        for (int i = 1; i <= gizmoSegments; i++)
        {
            float t = i / (float)gizmoSegments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 arcDirection = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            Vector3 edgePoint = origin + arcDirection * range;

            Gizmos.DrawLine(previousPoint, edgePoint);
            previousPoint = edgePoint;
        }

        Gizmos.color = gizmoColor;
        Gizmos.DrawLine(leftEnd, rightEnd);
    }
}

