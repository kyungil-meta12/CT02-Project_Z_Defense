using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class NormalZombie : PoolObject, IDamageable
{
    [Header("일반 좀비 기본 스펙")] public NormalZombieSpec spec;
    [Header("애니메이터 컨트롤러 목록")] public RuntimeAnimatorController[] animControllers;
    [SerializeField] private bool logReceivedDamage = true;
    
    public HpUI hpUI;
    public Collider hitCollider;

    [HideInInspector] public Animator anim;
    [HideInInspector] public bool attackState;
    [HideInInspector] public NavMeshAgent agent;
    [HideInInspector] public GameObject attackTarget; // 현재 공격 중인 타겟
    [HideInInspector] public Vector3 attackTargetContactPoint; // 공격 콜라이더가 마지막으로 접촉한 지점

    private Transform destination; // 현재 추적하는 타겟
    private float attackDamage; // 타워에 가할 대미지

    // IDamageable value
    public float CurrHp { get; private set; } // 현재 체력
    public float TotalHp { get; private set; } // 최대 체력
    public bool IsAlive { get; private set; } // 살아있는 상태

    private bool returnInstanceCoroutineRunning = false;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        agent.updatePosition = false;
        agent.updateRotation = false;
    }

    public override void OnSpawn()
    {
        float randomMoveAttackSpeed = Random.Range(spec.MinMoveAttackSpeed, spec.MaxMoveAttackSpeed);
        float randomAttackDamage = Random.Range(spec.MinAttackDamage, spec.MaxAttackDamage);
        float randomHp = Random.Range(spec.MinHp, spec.MaxHp);
        float wave = GameManager.Inst.Wave;
        bool isFirstWave = GameManager.Inst.Wave == 1;

        // 기본 수치 * 랜덤 수치 * 웨이브 반영 수치를 곱하여 결정
        // 웨이브 1때는 웨이브 가중치를 적용하지 않는다.

        // 애니메이터 랜덤 선택
        anim.runtimeAnimatorController = animControllers[Random.Range(0, animControllers.Length)];
        anim.SetBool("IsAttackState", false);

        // 이동/공격 속도
        float moveAttackSpeedMul = isFirstWave ? randomMoveAttackSpeed : randomMoveAttackSpeed * Mathf.Pow(1f + spec.MoveAttackSpeedWeight, wave - 1f);
        anim.SetFloat("MoveSpeed", spec.MoveSpeed * moveAttackSpeedMul);
        anim.SetFloat("AttackSpeed", spec.AttackSpeed * moveAttackSpeedMul);

        // 공격 대미지
        float attackDamageMul = isFirstWave ? randomAttackDamage : randomAttackDamage * Mathf.Pow(1f + spec.AttackDamageWeight, wave - 1f);
        attackDamage = spec.AttackDamage * attackDamageMul;

        // 체력
        float hpMul = isFirstWave ? randomHp : randomHp * Mathf.Pow(1f + spec.HpWeight, wave - 1f);
        TotalHp = spec.Hp * hpMul;
        CurrHp = TotalHp;
        IsAlive = true;
        attackState = false;
        attackTarget = null;

        // 체력 UI 슬라이더 값 지정
        hpUI.gameObject.SetActive(true);
        hpUI.InputTotalHp(TotalHp);
        hpUI.InputCurrHp(TotalHp);
        hpUI.gameObject.SetActive(false);

        SetCollidersEnabled(true); // 히트 콜라이더 활성화

        rb.constraints = RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezeRotationX; // 회전 제약 재설정

        // 코루틴 동작 상태 초기화
        returnInstanceCoroutineRunning = false;

        // 테스트용 코루틴
       // StartCoroutine(AutoDeathCoroutine());
    }
    
    IEnumerator AutoDeathCoroutine()
    {
        yield return new WaitForSeconds(10f);
        TakeDamage(500f);
    }

    /// <summary>
    /// 일반 좀비 사망 시 코인을 드랍하고, ItemManager에 코인이 추가된다.
    /// </summary>
    public override void OnDespawn()
    {
        ItemManager.Inst.AddCoinCount(spec.DropCoin);
    }

    private void Update()
    {
        UpdateDeath();
        UpdateMoveAndAttack();
    }

    // 죽었을 때 인스턴스를 지연 리턴 한다
    private void UpdateDeath()
    {
        if (IsAlive)
        {
            return;
        }

        AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
        if (info.normalizedTime >= 1f)
        {
            if (!returnInstanceCoroutineRunning)
            {
                StartCoroutine(ReturnInstanceCoroutine());
            }
        }
        return;
    }

    // 이동 및 공격 업데이트
    // attackState, attackTarget 및 agent의 동작 여부는 NormalZombieAttackCollider에서 변경한다
    private void UpdateMoveAndAttack()
    {
        if (!IsAlive)
        {
            return;
        }

        // 목적지가 있다면 그 대상을 향하여 이동, agent가 향하는 방향만 얻어 이동
        // 위치는 애니메이션의 루트 모션 위치를 따른다
        if (!attackState && destination)
        {
            agent.nextPosition = transform.position;

            Vector3 destDir = agent.desiredVelocity;
            destDir.y = 0f;

            // 미세한 떨림을 방지하기 위해 일정 크기 이상의 벡터일 때만 회전 처리
            if (destDir.sqrMagnitude > 0.1f)
            {
                destDir.Normalize();
                Quaternion targetRotation = Quaternion.LookRotation(destDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2.5f);
            }
        }

        // 공격 대상이 존재한다면 공격 대상을 향해 회전한다.
        if (attackState && attackTarget)
        {
            LookAt(attackTargetContactPoint);
        }
    }

    private void OnAnimatorMove()
    {
        if (!IsAlive)
        {
            return;
        }

        // agent 위치 수동 업데이트
        if (!attackState && destination)
        {
            transform.position = anim.rootPosition;
            agent.nextPosition = transform.position;
        }
    }

    // 특정 대상을 향한다
    private void LookAt(Vector3 point)
    {
        Vector3 destDir = point - transform.position;
        destDir.y = 0;
        destDir.Normalize();

        if (destDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(destDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2f);
        }
    }

    // 인스턴스 반환 코루틴
    private IEnumerator ReturnInstanceCoroutine()
    {
        returnInstanceCoroutineRunning = true;
        yield return new WaitForSeconds(3f);
        ReturnInstance();
    }

    /// <summary>
    /// 애니메이션 이벤트, 직접 호출하지 않음
    /// 공격 대상이 IDamageable 인터페이스를 상속해야 실제로 대미지가 적용됨
    /// </summary>
    public void OnZombieAttack()
    {
        if (attackState && attackTarget)
        {
            IDamageable iDmg = attackTarget.GetComponentInParent<IDamageable>();
            if (iDmg != null)
            {
                if (iDmg.IsAlive)
                {
                    iDmg.TakeDamage(attackDamage);
                }
                else
                {
                    print($"[NormalZombie] {attackTarget.gameObject.name}이 살아있지 않은 오브젝트임");
                }
            }
            else
            {
                Debug.LogError($"[NormalZombie] {attackTarget.gameObject.name}이 IDamageable을 상속하지 않음");
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
        if (!IsAlive) // 한 번 체력이 0이 되면 더 이상 TakeDamage를 받지 않음
        {
            return;
        }

        float appliedDamage = Mathf.Max(0f, damage);
        CurrHp -= appliedDamage;
        CurrHp = Mathf.Clamp(CurrHp, 0f, TotalHp);
        hpUI.gameObject.SetActive(true);
        hpUI.InputCurrHp(CurrHp);

        if (logReceivedDamage)
        {
            //Debug.Log($"[NormalZombie] Damage:{appliedDamage:0.###}, HP:{CurrHp:0.###}/{TotalHp:0.###}", this);
        }

        DamagePopupSpawner.SpawnDamage(transform, appliedDamage);

        // 체력이 완전히 떨어지면
        if (CurrHp <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// 사망 상태로 전환한다<para/>
    /// 죽은 좀비가 타겟/충돌 대상으로 남지 않도록 콜라이더를 비활성화한다
    /// </summary>
    private void Die()
    {
        IsAlive = false; // 생존 상태 비활성화
        GameManager.Inst.IncreaseKillCount();

        hpUI.gameObject.SetActive(false); // hp UI 비활성화
        attackState = false; // 공격 상태 초기화
        attackTarget = null; // 공격 대상 초기화
        agent.enabled = false; // 에이전트 비활성화
        anim.SetBool("IsAttackState", false);
        anim.SetTrigger("DeadTrigger"); // 죽는 애니메이션으로 변경
        SetCollidersEnabled(false); // 히트 콜라이더 비활성화
        rb.constraints = RigidbodyConstraints.FreezeRotation; // 모든 방향 회전 방지
    }

    /// <summary>
    /// 풀링 재사용과 사망 상태에 맞춰 전체 콜라이더와 리지드바디 활성 상태를 변경한다
    /// </summary>
    /// <param name="isEnabled"></param>
    private void SetCollidersEnabled(bool isEnabled)
    {
        hitCollider.enabled = isEnabled;
    }

    /// <summary>
    /// 위치를 지정한다
    /// </summary>
    /// <param name="t"></param>
    public void SetPosition(Transform t)
    {
        transform.position = t.position;
        agent.enabled = true;
        agent.Warp(t.position);
    }

    /// <summary>
    /// 추적할 대상을 지정한다
    /// </summary>
    /// <param name="t"></param>
    public void SetDestination(Transform t)
    {
        destination = t;
        agent.enabled = true;
        agent.SetDestination(t.position);
    }
}
