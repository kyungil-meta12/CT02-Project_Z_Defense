using UnityEngine;
using System.Linq;

public class NormalZombieController : PoolObject
{
    [Header("테스트 모드")] public bool testMode;
    [Header("일반 좀비 기본 스펙")] public NormalZombieSpec spec; // 일반 좀비 스펙 스크립터블 오브젝트
    [Header("공격 가능 거리")] public float attackDistance;
    [Header("추적할 타겟")]  public Transform target;

    private Animator anim;
    private float attackDamage; // 타워에 가할 대미지

    void Awake()
    {
        anim = GetComponent<Animator>();

        if(testMode)
        {
            OnSpawn();
        }
    }

    public override void OnSpawn()
    {
        var randomMoveSpeed = Random.Range(-spec.MoveSpeedRandomRange, spec.MoveSpeedRandomRange);
        var randomAttackSpeed = Random.Range(-spec.AttackSpeedRandomRange, spec.AttackSpeedRandomRange);
        var randomAttackDamage = Random.Range(-spec.AttackDamageRandomRange, spec.AttackDamageRandomRange);

        // 기본 수치 * 랜덤 수치 * 웨이브 수를 곱하여 결정
        anim.SetFloat("MoveSpeed", spec.MoveSpeed * randomMoveSpeed);
        anim.SetFloat("AttackSpeed", spec.AttackSpeed * randomAttackSpeed);
        attackDamage = spec.AttackDamage + randomAttackDamage;
    }

    void Update()
    {
        // 추적할 대상이 있다면 그 대상을 향하여 이동
        if (target)
        {
            Vector3 destDir = target.position - transform.position;
            destDir.y = 0;
            destDir.Normalize();

            if (destDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(destDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2f);
            }

            //  attackDistance보다 가까워지면 그 대상 공격
            float targetDist = Vector3.Distance(target.position, transform.position);
            anim.SetBool("IsAttackState", targetDist <= attackDistance);
        }
    }
}