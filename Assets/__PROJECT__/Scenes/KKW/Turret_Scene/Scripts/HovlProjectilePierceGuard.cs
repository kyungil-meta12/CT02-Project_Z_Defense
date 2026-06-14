using UnityEngine;

/// <summary>
/// HOVL 투사체의 자체 타겟 히트 종료가 프로젝트 관통 처리보다 먼저 실행되지 않도록 제어한다.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public class HovlProjectilePierceGuard : MonoBehaviour
{
    private Hovl.HS_ProjectileMover projectileMover;
    private ProjectileDamageDealer damageDealer;

    // 필요한 런타임 컴포넌트를 캐시한다
    private void Awake()
    {
        CacheComponents();
    }

    // 풀에서 다시 활성화될 때 HOVL 자체 타겟 판정을 초기화한다
    private void OnEnable()
    {
        CacheComponents();
        ClearHovlTrackedTarget();
    }

    // HOVL 이동 틱보다 먼저 자체 타겟 판정을 비활성화한다
    private void FixedUpdate()
    {
        CacheComponents();
        ClearHovlTrackedTarget();
    }

    // 현재 투사체의 HOVL 이동 컴포넌트와 데미지 처리기를 찾는다
    private void CacheComponents()
    {
        if (projectileMover == null)
        {
            projectileMover = GetComponent<Hovl.HS_ProjectileMover>();
        }

        if (damageDealer == null)
        {
            damageDealer = GetComponent<ProjectileDamageDealer>();
        }
    }

    // 관통 처리가 필요한 투사체는 HOVL의 첫 타겟 도달 종료를 사용하지 않는다
    private void ClearHovlTrackedTarget()
    {
        if (projectileMover == null || damageDealer == null)
        {
            return;
        }

        if (!damageDealer.HasReachedPierceLimit)
        {
            projectileMover.SetTarget(null);
        }
    }
}
