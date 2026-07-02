using UnityEngine;

/// <summary>
/// Frost 빙결 또는 빙결 재발동 쿨타임 중인 대상을 터렛 타겟 후보에서 제외한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Project Z Defense/Targeting/Frost Freeze Suppressed Target Candidate Filter")]
public sealed class FrostFreezeSuppressedTargetCandidateFilter : MonoBehaviour, ITargetCandidateFilter, ITargetCandidateRetentionFilter
{
    [Header("Frost 타겟 제외 조건")]
    [SerializeField, Tooltip("켜면 현재 빙결 중인 일반 좀비를 타겟 후보에서 제외합니다.")]
    private bool excludeFrozenTargets = true;
    [SerializeField, Tooltip("켜면 빙결이 끝났지만 개별 재빙결 쿨타임 중인 일반 좀비를 타겟 후보에서 제외합니다.")]
    private bool excludeFreezeCooldownTargets = true;

    [Header("Frost 타겟 유지 조건")]
    [SerializeField, Tooltip("켜면 Frost 누적 중인 현재 타겟을 빙결 전까지 유지합니다.")]
    private bool retainActiveFrostTargetUntilFreeze = true;

    public string DebugExcludeReason => "Frost 빙결 또는 빙결 쿨타임 중인 타겟은 탐색에서 제외합니다.";

    // Frost 빙결 재타겟 억제 상태인 타겟 후보를 제외한다
    public bool ShouldExcludeTarget(Transform targetTransform, IDamageable damageable)
    {
        if (targetTransform == null)
        {
            return false;
        }

        FrostStatusRuntime frostStatusRuntime = targetTransform.GetComponentInParent<FrostStatusRuntime>();
        if (frostStatusRuntime == null)
        {
            return false;
        }

        return (excludeFrozenTargets && frostStatusRuntime.IsFrozen)
            || (excludeFreezeCooldownTargets && frostStatusRuntime.IsFreezeCooldownActive);
    }

    // Frost 누적 중이며 아직 빙결 억제 상태가 아닌 현재 타겟을 유지한다
    public bool ShouldRetainCurrentTarget(Transform targetTransform, IDamageable damageable)
    {
        if (!retainActiveFrostTargetUntilFreeze || targetTransform == null)
        {
            return false;
        }

        FrostStatusRuntime frostStatusRuntime = targetTransform.GetComponentInParent<FrostStatusRuntime>();
        if (frostStatusRuntime == null)
        {
            return false;
        }

        return frostStatusRuntime.IsActive && !frostStatusRuntime.IsFreezeRetargetSuppressed;
    }
}
