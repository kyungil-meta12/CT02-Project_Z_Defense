using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 일반 좀비의 웨이브 스탯 초기화, 이동/공격, 피격, 사망, 처치 보상 지급을 담당한다.
/// </summary>
public class NormalZombie : PoolObject, IDamageable
{
    [Header("일반 좀비 기본 스펙")] public NormalZombieSpec spec;
    [Header("프리팹별 처치 보상 Override")] [SerializeField] private ZombieRewardProfileSO rewardProfileOverride;
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
    private float rewardMultiplier = 1.0f; // 스폰 프로필에서 적용한 처치 보상 배율

    // IDamageable value
    public float CurrHp { get; private set; } // 현재 체력
    public float TotalHp { get; private set; } // 최대 체력
    public bool IsAlive { get; private set; } // 살아있는 상태

    private bool returnInstanceCoroutineRunning = false;

    private Rigidbody rb;
    private Collider[] cachedColliders;
    private bool originalUseGravity;
    private bool originalIsKinematic;

    // 필요한 컴포넌트를 캐시하고 NavMeshAgent 루트모션 동작 방식을 설정한다
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        CacheColliders();
        CacheRigidbodyDefaults();
        agent.updatePosition = false;
        agent.updateRotation = false;
    }

    // 풀에서 꺼낼 때 스펙 기반 스탯과 런타임 상태를 초기화한다
    public override void OnSpawn()
    {
        float randomMoveAttackSpeed = Random.Range(spec.MinMoveAttackSpeed, spec.MaxMoveAttackSpeed);
        float randomAttackDamage = Random.Range(spec.MinAttackDamage, spec.MaxAttackDamage);
        float randomHp = Random.Range(spec.MinHp, spec.MaxHp);

        // 애니메이터 랜덤 선택
        anim.runtimeAnimatorController = animControllers[Random.Range(0, animControllers.Length)];
        anim.SetBool("IsAttackState", false);

        // 이동/공격 속도
        anim.SetFloat("MoveSpeed", spec.MoveSpeed * randomMoveAttackSpeed);
        anim.SetFloat("AttackSpeed", spec.AttackSpeed * randomMoveAttackSpeed);

        // 공격 대미지
        attackDamage = spec.AttackDamage * randomAttackDamage;

        // 체력
        TotalHp = spec.Hp * randomHp;
        CurrHp = TotalHp;
        rewardMultiplier = 1.0f;
        IsAlive = true;
        attackState = false;
        attackTarget = null;

        // 체력 UI 슬라이더 값 지정
        hpUI.gameObject.SetActive(true);
        hpUI.InputTotalHp(TotalHp);
        hpUI.InputCurrHp(TotalHp);
        hpUI.gameObject.SetActive(false);

        SetCollidersEnabled(true); // 히트 콜라이더 활성화
        RestoreRigidbodySimulation();

        rb.constraints = RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezeRotationX; // 회전 제약 재설정

        // 코루틴 동작 상태 초기화
        returnInstanceCoroutineRunning = false;

        // 테스트용 코루틴
       // StartCoroutine(AutoDeathCoroutine());
    }

    // 스폰 프로필에서 전달한 HP, 공격력, 이동/공격 속도, 보상 배율을 적용한다
    public void ApplySpawnRuntimeModifiers(ZombieSpawnRuntimeModifiers modifiers)
    {
        ZombieSpawnRuntimeModifiers safeModifiers = modifiers.Sanitized();

        TotalHp *= safeModifiers.hpMultiplier;
        CurrHp = TotalHp;
        attackDamage *= safeModifiers.attackDamageMultiplier;
        rewardMultiplier = safeModifiers.rewardMultiplier;

        if (anim != null)
        {
            anim.SetFloat("MoveSpeed", anim.GetFloat("MoveSpeed") * safeModifiers.moveAttackSpeedMultiplier);
            anim.SetFloat("AttackSpeed", anim.GetFloat("AttackSpeed") * safeModifiers.moveAttackSpeedMultiplier);
        }

        if (hpUI != null)
        {
            hpUI.InputTotalHp(TotalHp);
            hpUI.InputCurrHp(CurrHp);
        }
    }

    // 테스트용으로 일정 시간 뒤 좀비를 사망 처리한다
    IEnumerator AutoDeathCoroutine()
    {
        yield return new WaitForSeconds(10f);
        TakeDamage(500f);
    }

    // 매 프레임 사망 반환과 이동/공격 상태를 갱신한다
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

    // 애니메이터 루트모션 위치를 NavMeshAgent와 동기화한다
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
    // 애니메이션 공격 타이밍에 현재 공격 대상에게 데미지를 적용한다
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
    // 외부 공격으로 받은 데미지를 체력에 반영하고 사망 여부를 확인한다
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
    // 사망 상태 전환과 처치 보상 지급을 한 번만 처리한다
    private void Die()
    {
        IsAlive = false; // 생존 상태 비활성화
        GameManager.Inst.IncreaseKillCount();
        GrantKillReward();

        hpUI.gameObject.SetActive(false); // hp UI 비활성화
        attackState = false; // 공격 상태 초기화
        attackTarget = null; // 공격 대상 초기화
        agent.enabled = false; // 에이전트 비활성화
        anim.SetBool("IsAttackState", false);
        anim.SetTrigger("DeadTrigger"); // 죽는 애니메이션으로 변경
        SetCollidersEnabled(false); // 히트 콜라이더 비활성화
        StopRigidbodySimulation();
        rb.constraints = RigidbodyConstraints.FreezeRotation; // 모든 방향 회전 방지
    }

    // 일반 좀비 프리팹 Override 보상 프로필을 기준으로 처치 보상을 지급한다
    private void GrantKillReward()
    {
        if (spec == null)
        {
            Debug.LogWarning("[NormalZombie] 스펙이 없어 처치 보상을 지급할 수 없습니다.", this);
            return;
        }

        int wave = GameManager.Inst == null ? 1 : GameManager.Inst.Wave;
        ZombieRewardContext rewardContext = ZombieRewardContext.CreateNormalZombie(wave, spec, transform.position).WithRewardMultiplier(rewardMultiplier);
        RewardGrantUtility.GrantZombieReward(rewardProfileOverride, rewardContext, this);
    }

    /// <summary>
    /// 풀링 재사용과 사망 상태에 맞춰 전체 콜라이더와 리지드바디 활성 상태를 변경한다
    /// </summary>
    /// <param name="isEnabled"></param>
    // 풀 재사용과 사망 상태에 맞춰 히트 콜라이더 활성 상태를 변경한다
    private void SetCollidersEnabled(bool isEnabled)
    {
        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            CacheColliders();
        }

        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            if (hitCollider != null)
            {
                hitCollider.enabled = isEnabled;
            }

            return;
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider cachedCollider = cachedColliders[i];
            if (cachedCollider == null)
            {
                continue;
            }

            cachedCollider.enabled = isEnabled;
        }
    }

    // 사망 후 투사체 판정에 남지 않도록 자식 콜라이더를 캐시한다
    private void CacheColliders()
    {
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    // 재사용 시 원래 리지드바디 물리 설정을 복구하기 위해 기본값을 저장한다
    private void CacheRigidbodyDefaults()
    {
        if (rb == null)
        {
            return;
        }

        originalUseGravity = rb.useGravity;
        originalIsKinematic = rb.isKinematic;
    }

    // 사망 중 바닥 콜라이더 없이 중력으로 가라앉지 않도록 리지드바디 물리를 멈춘다
    private void StopRigidbodySimulation()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    // 풀에서 다시 사용할 때 리지드바디 물리 설정을 원래 상태로 복구한다
    private void RestoreRigidbodySimulation()
    {
        if (rb == null)
        {
            return;
        }

        rb.isKinematic = originalIsKinematic;
        rb.useGravity = originalUseGravity;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// 위치를 지정한다
    /// </summary>
    /// <param name="t"></param>
    // 스폰 위치로 좀비를 이동시키고 NavMeshAgent를 재활성화한다
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
    // 목적지를 저장하고 NavMeshAgent 경로를 설정한다
    public void SetDestination(Transform t)
    {
        destination = t;
        agent.enabled = true;
        agent.SetDestination(t.position);
    }
}
