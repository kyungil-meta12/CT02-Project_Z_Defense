using UnityEngine;
using System.Linq;

public class NormalZombieController : MonoBehaviour
{
    public float attackDistance;
    public float defaultMoveSpeed;

    private Animator anim;
    private SphereCollider sightCollider; // 시야 콜라이더
    private float checkTime = 0f; // 확인 시간
    private Transform targetTransform; // 추적 대상 트랜스폼
    private int humanLayerMask; // 레이어마스크 저장용

    void Awake()
    {
        anim = GetComponent<Animator>();
        anim.SetFloat("MoveSpeed", defaultMoveSpeed);
        sightCollider = GetComponent<SphereCollider>();
        humanLayerMask = LayerMask.GetMask("Human");
    }

    void Update()
    {
        // 매 프레임 시야 콜라이더 충돌을 확인하면 오버헤드가 발생하므로 0.5초에 한 번씩 확인
        // 가장 가까운 거리에 있는 대상을 우선 지정
        checkTime += Time.deltaTime;
        if (checkTime >= 0.5f)
        {
            checkTime -= 0.5f;
            var hits = Physics.OverlapSphere(transform.position, sightCollider.radius, humanLayerMask);

            var sortedHits = hits
            .OrderBy(c => Vector3.Distance(transform.position, c.transform.position))
            .ToArray();
            
            foreach (var collider in sortedHits)
            {
                targetTransform = collider.transform;
                break;
            }
        }

        // 추적할 대상이 있다면 그 대상을 향하여 이동
        if (targetTransform)
        {
            Vector3 destDir = targetTransform.position - transform.position;
            destDir.y = 0;
            destDir.Normalize();

            if (destDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(destDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2f);
            }

            //  attackDistance보다 가까워지면 그 대상 공격
            float targetDist = Vector3.Distance(targetTransform.position, transform.position);
            anim.SetBool("IsAttackState", targetDist <= attackDistance);
        }
    }
}