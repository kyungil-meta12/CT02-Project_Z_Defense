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

/// <summary>
/// 일반 타겟 후보가 없을 때 제외된 후보를 fallback 타겟으로 다시 사용할 수 있는지 판단하는 선택 계약이다.
/// </summary>
public interface ITargetCandidateFallbackFilter
{
    // 지정한 제외 후보를 일반 후보가 없을 때 fallback 타겟으로 사용할 수 있는지 확인한다
    bool ShouldAllowFallbackTarget(Transform targetTransform, IDamageable damageable);
}
