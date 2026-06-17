using ProjectZDefense.StatusEffects;
using UnityEngine;

/// <summary>
/// Poison 틱데미지로 이미 처치가 확정된 대상을 터렛 타겟 후보에서 제외한다.
/// </summary>
public sealed class PoisonLethalTargetCandidateFilter : MonoBehaviour, ITargetCandidateFilter
{
    public string DebugExcludeReason => "Poison 틱데미지로 처치가 확정된 타겟은 탐색에서 제외합니다.";

    // Poison 처형 예고 상태인 타겟 후보를 제외한다
    public bool ShouldExcludeTarget(Transform targetTransform, IDamageable damageable)
    {
        if (targetTransform == null)
        {
            return false;
        }

        IPoisonStatusEffectReceiver poisonReceiver = targetTransform.GetComponentInParent<IPoisonStatusEffectReceiver>();
        return poisonReceiver != null && poisonReceiver.IsPoisonLethalPending;
    }
}
