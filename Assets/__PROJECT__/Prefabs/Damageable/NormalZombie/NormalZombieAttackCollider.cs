using UnityEngine;

public class NormalZombieAttackCollider : MonoBehaviour
{
    public NormalZombie zombie;
    private Collider attackCollider;
    private float checkTime;
    private bool checkEnabled = false;

    void Awake()
    {
        attackCollider = GetComponent<Collider>();
    }

    void Update()
    {
        // 0.25초 마다 Obstacle 충돌 확인
        checkTime += Time.deltaTime;
        if(checkTime >= 0.25f)
        {
            checkTime -= 0.25f;
            checkEnabled = true;
        }
    }

    void OnTriggerStay(Collider c)
    {
        if(!checkEnabled)
        {
            return;
        }
        // attackCollider가 Obstacle 오브젝트에 충돌 후 distance 일정 미만이 되면 공격 시작
        // collision jitter를 방지하기 위함
        if(!zombie.attackState) {
            bool isOverlapping = Physics.ComputePenetration(
                attackCollider, attackCollider.transform.position, attackCollider.transform.rotation,
                c, c.transform.position, c.transform.rotation,
                out var direction, out var distance
            );

            if (isOverlapping && distance >= 0.25f)
            {
                zombie.attackState = true;
                zombie.attackTarget = c.gameObject;
                zombie.attackTargetContactPoint = transform.position + (-direction * distance); // 장애물과 충돌한 위치를 바라본다
                zombie.anim.SetBool("IsAttackState", true);
                zombie.agent.isStopped = true;
            }
        }
    }

    void OnTriggerExit(Collider c)
    {
        zombie.attackState = false;
        zombie.attackTarget = null;
        zombie.anim.SetBool("IsAttackState", false);
        zombie.agent.isStopped = false;
    }
}
