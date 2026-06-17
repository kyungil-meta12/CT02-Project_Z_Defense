using UnityEngine;

/// <summary>
/// 터렛 타겟 탐색 중 후보 대상 제외 정책을 외부 컴포넌트로 분리하는 계약이다.
/// </summary>
public interface ITargetCandidateFilter
{
    string DebugExcludeReason { get; }

    // 지정한 타겟 후보를 현재 필터 정책으로 제외해야 하는지 확인한다
    bool ShouldExcludeTarget(Transform targetTransform, IDamageable damageable);
}
