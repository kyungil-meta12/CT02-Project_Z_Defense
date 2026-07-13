using UnityEngine;

/// <summary>
/// 현재 타겟이 유효한 동안 더 가까운 후보로 타겟을 전환하지 않도록 유지한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Project Z Defense/Targeting/Sticky Current Target Retention Filter")]
public sealed class StickyCurrentTargetRetentionFilter : MonoBehaviour, ITargetCandidateRetentionFilter
{
    [Header("타겟 유지 조건")]
    [SerializeField, Tooltip("켜면 현재 타겟이 활성 상태이고 살아있는 동안 새 후보보다 우선 유지합니다.")]
    private bool retainAliveTarget = true;

    // 현재 타겟이 활성 상태이고 살아있으면 타겟 전환을 미룬다
    public bool ShouldRetainCurrentTarget(Transform targetTransform, IDamageable damageable)
    {
        if (!retainAliveTarget || targetTransform == null || !targetTransform.gameObject.activeInHierarchy)
        {
            return false;
        }

        return damageable == null || damageable.IsAlive;
    }
}
