using UnityEngine;
using System.Linq;
using UnityEditor.Animations;

public class NormalZombie : PoolObject, IDamageable
{
    [Header("일반 좀비 기본 스펙")] public NormalZombieSpec spec;
    [Header("애니메이터 컨트롤러 목록")] public RuntimeAnimatorController[] animControllers;
    [Header("테스트 모드")] public bool testMode;
    [Header("추적할 타겟(테스트용)")]  public Transform destination;

    [HideInInspector] public Animator anim;
    [HideInInspector] public bool attackState;
    [HideInInspector] public GameObject attackTarget; // 현재 공격 중인 타겟
    [HideInInspector] public Vector3 attackTargetContactPoint; // 공격 콜라이더가 마지막으로 접촉한 지점

    private Transform target; // 현재 추적하는 타겟
    private float attackDamage; // 타워에 가할 대미지

    // IDamageable value
    public float CurrHp{ get; set; } // 현재 체력
    public float TotalHp{ get; set; } // 최대 체력

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
        var randomMoveSpeed = Random.Range(spec.MinMoveSpeed, spec.MaxMoveSpeed);
        var randomAttackSpeed = Random.Range(spec.MinAttackSpeed, spec.MaxAttackSpeed);
        var randomAttackDamage = Random.Range(spec.MinAttackDamage, spec.MaxAttackDamage);
        var randomHp = Random.Range(spec.MinHp, spec.MaxHp);
        float wave = GameManager.Inst.wave;
        bool isFirstWave = GameManager.Inst.wave == 1;

        // 기본 수치 * 랜덤 수치 * 웨이브 반영 수치를 곱하여 결정
        // 웨이브 1때는 웨이브 가중치를 적용하지 않는다.

        // 애니메이터 랜덤 선택
        anim.runtimeAnimatorController = animControllers[Random.Range(0, animControllers.Length)];
        anim.SetBool("IsAttackState", false);

        // 이동 속도
        var moveSpeedMul = isFirstWave ? randomMoveSpeed : randomMoveSpeed * (wave * spec.MoveSpeedWaveMultiply);
        anim.SetFloat("MoveSpeed", spec.MoveSpeed * moveSpeedMul);

        // 공격 속도
        var attackSpeedMul = isFirstWave ? randomAttackSpeed : randomAttackSpeed * (wave * spec.AttackSpeedWaveMultiply);
        anim.SetFloat("AttackSpeed", spec.AttackSpeed * attackSpeedMul);

        // 공격 대미지
        var attackDamageMul = isFirstWave ? randomAttackDamage : randomAttackDamage * (wave * spec.AttackDamageWaveMultiply);
        attackDamage = spec.AttackDamage * attackDamageMul;

        // 체력
        var hpMul = isFirstWave ? randomHp : randomHp * (wave * spec.HpWaveMultiply);
        TotalHp = spec.Hp * hpMul;
        CurrHp = TotalHp;
    }

    void Update()
    {
        // 추적할 대상이 있다면 그 대상을 향하여 이동
        if (!attackState && target)
        {
            Vector3 destDir = target.position - transform.position;
            destDir.y = 0;
            destDir.Normalize();

            if (destDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(destDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2f);
            }
        }

        // 공격 대상이 존재한다면 공격 대상을 향한다
        else if(attackState && attackTarget)
        {
            Vector3 destDir = target.position - attackTargetContactPoint;
            destDir.y = 0;
            destDir.Normalize();

            if (destDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(destDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2f);
            }
        }
    }

    /// <summary>
    /// 대미지를 가한다<para/>
    /// IDamageable method
    /// </summary>
    /// <param name="damage"></param>
    public void TakeDamage(float damage)
    {
        CurrHp -= damage;
        CurrHp = Mathf.Clamp(CurrHp, 0f, TotalHp);
    }

    /// <summary>
    /// 추적할 대상을 지정한다
    /// </summary>
    /// <param name="t"></param>
    public void SetTarget(Transform t)
    {
        target = t;
    }
}