using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;
using ProjectZDefense.StatusEffects;

/// <summary>
/// 일반 좀비의 웨이브 스탯 초기화, 이동/공격, 피격, 사망, 처치 보상 지급을 담당한다.
/// </summary>
public class NormalZombie : PoolObject, IDamageable, IFrostStatusEffectReceiver, IPoisonStatusEffectReceiver, IElectroStatusEffectReceiver, IElectroOverloadTriggerReceiver, IFrostStatusRuntimeOwner, IElectroStunRuntimeOwner
{
    [Header("일반 좀비 기본 스펙")] public NormalZombieSpec spec;
    [Header("프리팹별 처치 보상 Override")] [SerializeField] private ZombieRewardProfileSO rewardProfileOverride;
    [Header("애니메이터 컨트롤러 목록")] public RuntimeAnimatorController[] animControllers;
    [Header("보상 파티클 스케일")] public float rewardParticleScale;
    [Space(10)]
    [SerializeField] private bool logReceivedDamage = true;
    
    public HpUI hpUI;
    public Collider hitCollider;
    public ItemDropper itemDropper;
    [Header("상태이상 비주얼")] [SerializeField] private StatusEffectVisualController statusEffectVisualController;

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
    public bool IsPoisonLethalPending => poisonStatusRuntime != null && poisonStatusRuntime.IsLethalPending;

    private bool returnInstanceCoroutineRunning = false;

    private Rigidbody rb;
    private Collider[] cachedColliders;
    private bool originalUseGravity;
    private bool originalIsKinematic;
    private float baseMoveSpeed;
    private float baseAttackSpeed;
    private FrostStatusRuntime frostStatusRuntime;
    private PoisonStatusRuntime poisonStatusRuntime;
    private ElectroStatusRuntime electroStatusRuntime;
    private float electroStunRemainingDuration;
    private bool electroStunActive;

    // 사망 시 최종 보상값을 저장하는 구조체
    private RewardResult rewardResult = new();

    // 필요한 컴포넌트를 캐시하고 NavMeshAgent 루트모션 동작 방식을 설정한다
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        CacheStatusEffectVisualController();
        CacheFrostStatusRuntime();
        CachePoisonStatusRuntime();
        CacheElectroStatusRuntime();
        CacheColliders();
        CacheRigidbodyDefaults();
        agent.updatePosition = false;
        agent.updateRotation = false;
    }

    // 풀에서 꺼낼 때 스펙 기반 스탯과 런타임 상태를 초기화한다
    public override void OnSpawn()
    {
        float randomMoveAttackSpeed = UnityEngine.Random.Range(spec.MinMoveAttackSpeed, spec.MaxMoveAttackSpeed);
        float randomAttackDamage = UnityEngine.Random.Range(spec.MinAttackDamage, spec.MaxAttackDamage);
        float randomHp = UnityEngine.Random.Range(spec.MinHp, spec.MaxHp);

        // 애니메이터 랜덤 선택
        anim.runtimeAnimatorController = animControllers[UnityEngine.Random.Range(0, animControllers.Length)];
        anim.SetBool("IsAttackState", false);

        // 이동/공격 속도
        baseMoveSpeed = spec.MoveSpeed * randomMoveAttackSpeed;
        baseAttackSpeed = spec.AttackSpeed * randomMoveAttackSpeed;
        anim.SetFloat("MoveSpeed", baseMoveSpeed);
        anim.SetFloat("AttackSpeed", baseAttackSpeed);

        // 공격 대미지
        attackDamage = spec.AttackDamage * randomAttackDamage;

        // 체력
        TotalHp = spec.Hp * randomHp;
        CurrHp = TotalHp;
        rewardMultiplier = 1.0f;
        IsAlive = true;
        attackState = false;
        attackTarget = null;
        ResetFrostStatus();
        ResetPoisonStatus();
        ResetElectroStatus();

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

    // 풀로 반환될 때 남아있는 상태이상과 빙결 이펙트를 정리한다
    public override void OnDespawn()
    {
        ResetFrostStatus();
        ResetPoisonStatus();
        ResetElectroStatus();
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
            baseMoveSpeed = anim.GetFloat("MoveSpeed");
            baseAttackSpeed = anim.GetFloat("AttackSpeed");
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
        if (frostStatusRuntime != null)
        {
            frostStatusRuntime.Tick(Time.deltaTime);
        }
        if (poisonStatusRuntime != null)
        {
            poisonStatusRuntime.Tick(Time.deltaTime);
        }
        if (electroStatusRuntime != null)
        {
            electroStatusRuntime.Tick(Time.deltaTime);
        }
        UpdateElectroStun(Time.deltaTime);
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

        if (electroStunActive)
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

        if (electroStunActive)
        {
            agent.nextPosition = transform.position;
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
                Debug.LogWarning($"[NormalZombie] {attackTarget.gameObject.name}은 피해 대상이 아니어서 공격 상태를 해제합니다.", this);
                attackState = false;
                attackTarget = null;
                anim.SetBool("IsAttackState", false);
                if (agent != null && agent.enabled)
                {
                    agent.isStopped = false;
                }
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

        if (CurrHp > 0f && poisonStatusRuntime != null && poisonStatusRuntime.IsActive)
        {
            poisonStatusRuntime.RefreshLethalPrediction();
        }

        // 체력이 완전히 떨어지면
        if (CurrHp <= 0f)
        {
            Die();
        }
    }

    // Frost 빔으로 전달된 누적 슬로우와 빙결 폭발 데이터를 갱신한다
    public void ApplyFrostStatus(FrostStatusPayload payload)
    {
        if (!IsAlive || frostStatusRuntime == null)
        {
            return;
        }

        frostStatusRuntime.ApplyFrostStatus(payload);
    }

    // Poison 투사체로 전달된 중독 틱데미지 데이터를 갱신한다
    public void ApplyPoisonStatus(PoisonStatusPayload payload)
    {
        if (!IsAlive || poisonStatusRuntime == null)
        {
            return;
        }

        poisonStatusRuntime.ApplyPoisonStatus(payload);
    }

    // Electro 투사체와 체인 라이트닝으로 전달된 Shock 스택 데이터를 갱신한다
    public void ApplyElectroStatus(ElectroStatusPayload payload, int chainIndex, float sourceDamage)
    {
        if (!IsAlive || electroStatusRuntime == null)
        {
            return;
        }

        electroStatusRuntime.ApplyElectroStatus(payload, chainIndex, sourceDamage);
    }

    // 비-Electro 피해가 적용되는 시점에 Electro Overload 발동 여부를 갱신한다
    public void NotifyNonElectroDamageReceived(float damage)
    {
        if (!IsAlive || electroStatusRuntime == null)
        {
            return;
        }

        electroStatusRuntime.NotifyNonElectroDamageReceived(damage);
    }

    // Frost 상태를 제외한 현재 이동/공격 기준 속도를 반환한다
    public Vector2 GetRuntimeBaseSpeeds()
    {
        return new Vector2(baseMoveSpeed, baseAttackSpeed);
    }

    // 외부 버프가 변경한 이동/공격 기준 속도를 Frost 상태와 함께 다시 반영한다
    public void SetRuntimeBaseSpeeds(float moveSpeed, float attackSpeed)
    {
        baseMoveSpeed = Mathf.Max(0.0f, moveSpeed);
        baseAttackSpeed = Mathf.Max(0.0f, attackSpeed);
        if (frostStatusRuntime != null)
        {
            frostStatusRuntime.RefreshSpeedModifier();
        }
    }

    // Frost 상태가 계산한 속도 배율을 애니메이터 이동/공격 속도에 반영한다
    public void ApplyFrostSpeedMultiplier(float speedMultiplier)
    {
        if (anim == null)
        {
            return;
        }

        float safeSpeedMultiplier = Mathf.Clamp01(speedMultiplier);
        anim.SetFloat("MoveSpeed", baseMoveSpeed * safeSpeedMultiplier);
        anim.SetFloat("AttackSpeed", baseAttackSpeed * safeSpeedMultiplier);
    }

    // Electro 경직을 갱신하고 필요 시 짧은 전기 경직 비주얼을 켠다
    public void ApplyElectroStun(float duration, bool playHitStunVisual)
    {
        if (!IsAlive || duration <= 0.0f)
        {
            return;
        }

        electroStunRemainingDuration = Mathf.Max(electroStunRemainingDuration, duration);
        electroStunActive = true;
        ApplyElectroStunSpeedStop();
        if (playHitStunVisual)
        {
            SetElectroStunVisualActive(true);
        }
    }

    // Electro 경직 상태와 비주얼을 초기화한다
    public void ResetElectroStun()
    {
        electroStunRemainingDuration = 0.0f;
        electroStunActive = false;
        SetElectroStunVisualActive(false);
        RefreshFrostSpeedAfterElectroStun();
    }

    // Electro 경직 타이머를 감소시키고 종료 시 속도를 복구한다
    private void UpdateElectroStun(float deltaTime)
    {
        if (!electroStunActive)
        {
            return;
        }

        electroStunRemainingDuration = Mathf.Max(0.0f, electroStunRemainingDuration - deltaTime);
        ApplyElectroStunSpeedStop();
        if (electroStunRemainingDuration > 0.0f)
        {
            return;
        }

        electroStunActive = false;
        SetElectroStunVisualActive(false);
        RefreshFrostSpeedAfterElectroStun();
    }

    // Electro 경직 중 이동과 공격 애니메이션 속도를 0으로 고정한다
    private void ApplyElectroStunSpeedStop()
    {
        if (anim == null)
        {
            return;
        }

        anim.SetFloat("MoveSpeed", 0.0f);
        anim.SetFloat("AttackSpeed", 0.0f);
    }

    // Electro 경직 종료 후 Frost 상태를 기준으로 속도를 다시 반영한다
    private void RefreshFrostSpeedAfterElectroStun()
    {
        if (frostStatusRuntime != null)
        {
            frostStatusRuntime.RefreshSpeedModifier();
            return;
        }

        ApplyFrostSpeedMultiplier(1.0f);
    }

    // Electro 경직 비주얼 슬롯을 활성화하거나 비활성화한다
    private void SetElectroStunVisualActive(bool isActive)
    {
        if (statusEffectVisualController == null)
        {
            return;
        }

        statusEffectVisualController.SetElectroStunActive(isActive);
    }

    // 풀 재사용이나 사망 시 Frost 상태를 초기화하고 원래 속도를 복구한다
    private void ResetFrostStatus()
    {
        if (frostStatusRuntime == null)
        {
            return;
        }

        frostStatusRuntime.ResetStatus();
    }

    // 풀 재사용이나 사망 시 Poison 상태를 초기화하고 비주얼을 끈다
    private void ResetPoisonStatus()
    {
        if (poisonStatusRuntime == null)
        {
            return;
        }

        poisonStatusRuntime.ResetStatus();
    }

    // 풀 재사용이나 사망 시 Electro Shock 스택과 비주얼을 끈다
    private void ResetElectroStatus()
    {
        if (electroStatusRuntime == null)
        {
            return;
        }

        electroStatusRuntime.ResetStatus();
    }

    // 상태이상 비주얼 컨트롤러를 자식까지 포함해 캐시한다
    private void CacheStatusEffectVisualController()
    {
        if (statusEffectVisualController != null)
        {
            return;
        }

        statusEffectVisualController = GetComponentInChildren<StatusEffectVisualController>(true);
    }

    // Frost 상태 런타임 컴포넌트를 캐시하고 일반 좀비 정책으로 초기화한다
    private void CacheFrostStatusRuntime()
    {
        frostStatusRuntime = GetComponent<FrostStatusRuntime>();
        if (frostStatusRuntime == null)
        {
            frostStatusRuntime = gameObject.AddComponent<FrostStatusRuntime>();
        }

        frostStatusRuntime.Initialize(this, this, statusEffectVisualController, true);
    }

    // Poison 상태 런타임 컴포넌트를 캐시하고 일반 좀비 정책으로 초기화한다
    private void CachePoisonStatusRuntime()
    {
        poisonStatusRuntime = GetComponent<PoisonStatusRuntime>();
        if (poisonStatusRuntime == null)
        {
            poisonStatusRuntime = gameObject.AddComponent<PoisonStatusRuntime>();
        }

        poisonStatusRuntime.Initialize(this, statusEffectVisualController, false, true);
    }

    // Electro 상태 런타임 컴포넌트를 캐시하고 일반 좀비 정책으로 초기화한다
    private void CacheElectroStatusRuntime()
    {
        electroStatusRuntime = GetComponent<ElectroStatusRuntime>();
        if (electroStatusRuntime == null)
        {
            electroStatusRuntime = gameObject.AddComponent<ElectroStatusRuntime>();
        }

        electroStatusRuntime.Initialize(this, false);
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
        GrantKillReward();  // rewardResult를 이 메서드 내부에서 얻는다.

        // 이외의 보상은 필드에 아이템 형식으로 드랍한다.
        // 드랍하지 않을 수도 있다.
        foreach (RewardCurrencyType type in Enum.GetValues(typeof(RewardCurrencyType)))
        {
            itemDropper.CreateDropItem(rewardResult, transform.position, type);
        }

        // 코인 획득량에 따라 다른 코인 파티클을 생성한다.
        if (CoinParticleCreator.Inst)
        {
            CoinParticleCreator.Inst.Create(rewardResult, transform.position, transform.localScale * rewardParticleScale);
        }

        hpUI.gameObject.SetActive(false); // hp UI 비활성화
        TriggerFrostDeathEffectIfNeeded();
        TriggerPoisonDeathBurstIfNeeded();
        ResetFrostStatus();
        ResetPoisonStatus();
        ResetElectroStatus();
        attackState = false; // 공격 상태 초기화
        attackTarget = null; // 공격 대상 초기화
        agent.enabled = false; // 에이전트 비활성화
        anim.SetBool("IsAttackState", false);
        anim.SetTrigger("DeadTrigger"); // 죽는 애니메이션으로 변경
        SetCollidersEnabled(false); // 히트 콜라이더 비활성화
        StopRigidbodySimulation();
        rb.constraints = RigidbodyConstraints.FreezeRotation; // 모든 방향 회전 방지
    }

    // 빙결 상태로 사망한 경우 Frost 사망 전용 이펙트를 실행한다
    private void TriggerFrostDeathEffectIfNeeded()
    {
        if (frostStatusRuntime == null)
        {
            return;
        }

        frostStatusRuntime.TriggerFreezeDeathEffectIfNeeded();
    }

    // Poison 처형 확정 상태로 사망한 경우 사망 폭발과 약한 범위 중독을 실행한다
    private void TriggerPoisonDeathBurstIfNeeded()
    {
        if (poisonStatusRuntime == null)
        {
            return;
        }

        poisonStatusRuntime.TriggerDeathBurstIfNeeded(transform.position);
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

        // ref rewardResult를 통해 최종 보상값을 얻는다.
        RewardGrantUtility.GrantZombieReward(rewardProfileOverride, rewardContext, this, ref rewardResult);
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
