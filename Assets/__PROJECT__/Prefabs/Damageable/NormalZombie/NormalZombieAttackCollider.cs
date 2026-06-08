using UnityEngine;

public class NormalZombieAttackCollider : MonoBehaviour
{
    public NormalZombie zombie;
    public LayerMask targetLayer;
    private CapsuleCollider attackCollider;
    private float checkTime;
    void Awake()
    {
        attackCollider = GetComponent<CapsuleCollider>();
    }

    void Update()
    {
        // 0.2초 마다 Obstacle 충돌 확인
        checkTime += Time.deltaTime;
        if(checkTime >= 0.2f)
        {
            checkTime -= 0.2f;

            GetCapsulePoints(attackCollider, out var pointA, out var pointB);
            var hitColliders = Physics.OverlapCapsule(pointA, pointB, attackCollider.radius, targetLayer);

            // 공격 상태에서 충돌한 Obstacle이 없을 경우 공격 상태 비활성화
            if(zombie.attackState)
            {
                if(hitColliders.Length == 0)
                {
                    zombie.attackState = false;
                    zombie.attackTarget = null;
                    zombie.anim.SetBool("IsAttackState", false);
                    zombie.agent.isStopped = false;
                }
            }

            // 1개 이상의 Obstacle이 감지되면 그 오브젝트를 공격 타겟으로 설정하고 공격 상태로 전환한다.
            else
            {
                foreach(var hit in hitColliders)
                {
                    bool isOverlapping = Physics.ComputePenetration(
                        attackCollider, attackCollider.transform.position, attackCollider.transform.rotation,
                        hit, hit.transform.position, hit.transform.rotation,
                        out var direction, out var distance
                    );

                    if (isOverlapping && distance >= zombie.spec.AttackDistance)
                    {
                        zombie.attackState = true;
                        zombie.attackTarget = hit.gameObject;
                        zombie.attackTargetContactPoint = transform.position + (-direction * distance); // 장애물과 충돌한 위치를 바라본다
                        zombie.anim.SetBool("IsAttackState", true);
                        zombie.agent.isStopped = true;
                        break;
                    }
                }
            }
        }
    }

    void GetCapsulePoints(CapsuleCollider capsule, out Vector3 pointA, out Vector3 pointB)
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
