using UnityEngine;

/// <summary>
/// Ignition 터렛이 이미 연소 중인 대상을 일반 후보에서 제외하고 비연소 대상을 우선 공격하게 한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Project Z Defense/Targeting/Ignition Burning Target Candidate Filter")]
public sealed class IgnitionBurningTargetCandidateFilter : MonoBehaviour, ITargetCandidateFilter, ITargetCandidateFallbackFilter
{
    [Header("Ignition 타겟 제외 조건")]
    [SerializeField, Tooltip("켜면 이미 Ignition 연소 중인 대상을 일반 타겟 후보에서 제외합니다.")]
    private bool excludeBurningTargets = true;

    [Header("Ignition fallback 조건")]
    [SerializeField, Tooltip("켜면 비연소 대상이 없을 때 연소 중인 대상을 fallback 타겟으로 허용합니다.")]
    private bool allowBurningFallbackTarget = true;

    public string DebugExcludeReason => "Ignition 연소 중인 타겟은 비연소 타겟보다 낮은 우선순위로 탐색합니다.";

    // 이미 연소 중인 타겟 후보를 일반 후보에서 제외한다
    public bool ShouldExcludeTarget(Transform targetTransform, IDamageable damageable)
    {
        if (!excludeBurningTargets)
        {
            return false;
        }

        IgnitionStatusRuntime ignitionStatusRuntime = ResolveIgnitionStatusRuntime(targetTransform);
        return ignitionStatusRuntime != null && ignitionStatusRuntime.IsActive;
    }

    // 일반 후보가 없을 때 연소 중인 대상을 fallback 타겟으로 허용한다
    public bool ShouldAllowFallbackTarget(Transform targetTransform, IDamageable damageable)
    {
        if (!allowBurningFallbackTarget)
        {
            return false;
        }

        IgnitionStatusRuntime ignitionStatusRuntime = ResolveIgnitionStatusRuntime(targetTransform);
        return ignitionStatusRuntime != null && ignitionStatusRuntime.IsActive;
    }

    // 타겟 루트에서 Ignition 상태 런타임을 찾는다
    private static IgnitionStatusRuntime ResolveIgnitionStatusRuntime(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            return null;
        }

        return targetTransform.GetComponentInParent<IgnitionStatusRuntime>();
    }
}
