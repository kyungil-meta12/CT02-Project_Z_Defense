using UnityEngine;

/// <summary>
/// 일반 좀비의 근접 공격 범위 안에 있는 유효한 피해 대상을 감지하고 공격 상태를 전환한다.
/// </summary>
public class NormalZombieAttackCollider : MonoBehaviour
{
    private const int MAX_HIT_COLLIDER_COUNT = 8;

    public NormalZombie zombie;
    public LayerMask targetLayer;
    private CapsuleCollider attackCollider;
    private float checkTime;
    private readonly Collider[] hitColliders = new Collider[MAX_HIT_COLLIDER_COUNT];

    // 공격 범위 콜라이더를 캐시한다
    private void Awake()
    {
        attackCollider = GetComponent<CapsuleCollider>();
    }

    // 일정 주기로 공격 범위 안의 유효한 피해 대상을 확인한다
    private void Update()
    {
        // 0.2초 마다 Obstacle 충돌 확인
        checkTime += Time.deltaTime;
        if(checkTime >= 0.2f)
        {
            checkTime -= 0.2f;

            GetCapsulePoints(attackCollider, out var pointA, out var pointB);
            int collidedCount = Physics.OverlapCapsuleNonAlloc(pointA, pointB, attackCollider.radius, hitColliders, targetLayer, QueryTriggerInteraction.Ignore);
            bool hasValidAttackTarget = TryFindAttackTarget(collidedCount, out GameObject nextAttackTarget, out Vector3 nextContactPoint);

            // 공격 상태에서 유효한 피해 대상이 없을 경우 공격 상태 비활성화
            if(zombie.attackState)
            {
                if(!hasValidAttackTarget)
                {
                    zombie.attackState = false;
                    zombie.attackTarget = null;
                    zombie.anim.SetBool("IsAttackState", false);
                    if(zombie.agent.enabled)zombie.agent.isStopped = false;
                }
            }

            // 1개 이상의 Obstacle이 감지되면 그 오브젝트를 공격 타겟으로 설정하고 공격 상태로 전환한다.
            else
            {
                if(!hasValidAttackTarget)
                {
                    return;
                }

                zombie.attackState = true;
                zombie.attackTarget = nextAttackTarget;
                zombie.attackTargetContactPoint = nextContactPoint;
                zombie.anim.SetBool("IsAttackState", true);
                if(zombie.agent.enabled)zombie.agent.isStopped = true;
            }
        }
    }

    // 감지된 콜라이더 중 실제 피해를 받을 수 있는 공격 대상을 찾는다
    private bool TryFindAttackTarget(int collidedCount, out GameObject nextAttackTarget, out Vector3 nextContactPoint)
    {
        nextAttackTarget = null;
        nextContactPoint = Vector3.zero;

        if (zombie == null || zombie.spec == null)
        {
            return false;
        }

        for (int i = 0; i < collidedCount; i++)
        {
            Collider hit = hitColliders[i];
            if (hit == null)
            {
                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
            {
                continue;
            }

            bool isOverlapping = Physics.ComputePenetration(
                attackCollider, attackCollider.transform.position, attackCollider.transform.rotation,
                hit, hit.transform.position, hit.transform.rotation,
                out var direction, out var distance
            );

            if (!isOverlapping || distance < zombie.spec.AttackDistance)
            {
                continue;
            }

            nextAttackTarget = hit.gameObject;
            nextContactPoint = transform.position + (-direction * distance); // 장애물과 충돌한 위치를 바라본다
            return true;
        }

        return false;
    }

    // 캡슐 콜라이더의 월드 기준 양 끝점을 계산한다
    private void GetCapsulePoints(CapsuleCollider capsule, out Vector3 pointA, out Vector3 pointB)
    {
        // 캡슐의 방향에 따른 축 벡터 설정
        Vector3 direction = Vector3.up; // 기본값 Y축

        if (capsule.direction == 0) 
        {
            direction = Vector3.right;
        }
        else if (capsule.direction == 2) 
        {
            direction = Vector3.forward;
        }

        // 캡슐 중심에서 양 끝 원의 중심까지의 거리 계산
        float halfHeight = (capsule.height * 0.5f) - capsule.radius;
        
        if (halfHeight < 0)
        { 
            halfHeight = 0; // 높이가 반지름보다 작을 경우 예외 처리
        }

        // 로컬 좌표 기준의 두 점
        Vector3 localPointA = capsule.center + direction * halfHeight;
        Vector3 localPointB = capsule.center - direction * halfHeight;

        // 로컬 좌표를 오브젝트의 위치/회전/스케일이 반영된 월드 좌표로 변환
        pointA = transform.TransformPoint(localPointA);
        pointB = transform.TransformPoint(localPointB);
    }
}
