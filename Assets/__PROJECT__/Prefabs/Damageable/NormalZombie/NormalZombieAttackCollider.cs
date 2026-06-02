using UnityEngine;

public class NormalZombieAttackCollider : MonoBehaviour
{
    public NormalZombie zombie;
    private Collider attackCollider;

    void Awake()
    {
        attackCollider = GetComponent<Collider>();
    }

    void OnTriggerEnter(Collider c)
    {
        // 목표 추적 도중 attackCollider가 obstacle 레이어 오브젝트에 닿으면 공격 시작
        zombie.attackState = true;
        zombie.attackTarget = c.gameObject;
        zombie.attackTargetContactPoint = attackCollider.ClosestPoint(c.gameObject.transform.position);
        zombie.anim.SetBool("IsAttackState", true);
    }

    void OnTriggerExit(Collider c)
    {
        // attackCollider가 obstacle 레이어 오브젝트와 떨어지면 다시 목표 추적 시작
        zombie.attackState = false;
        zombie.attackTarget = null;
        zombie.anim.SetBool("IsAttackState", false);
    }
}
