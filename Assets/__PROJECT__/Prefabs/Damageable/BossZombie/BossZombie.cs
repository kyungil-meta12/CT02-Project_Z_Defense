using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;
using ProjectZDefense.StatusEffects;

/// <summary>
/// 보스 좀비의 웨이브 스탯 초기화, 스킬, 피격, 사망, 처치 보상 지급을 담당한다.
/// </summary>
public class BossZombie : PoolObject, IDamageable, IAimPointProvider, IFrostStatusEffectReceiver, IPoisonStatusEffectReceiver, IElectroStatusEffectReceiver, IIgnitionStatusEffectReceiver, IIgnitionReactionReceiver, IElectroOverloadTriggerReceiver, IFrostStatusRuntimeOwner, IElectroStunRuntimeOwner
{
    private static readonly int SpeedHash = Animator.StringToHash("speed");
    private const float MinimumFrostSpeedMultiplier = 0.5f;

    public BossZombieSpec spec;
    [Header("프리팹별 처치 보상 Override")] [SerializeField] private ZombieRewardProfileSO rewardProfileOverride;
    public HpUI hpUI;
    
    public BehaviorGraphAgent behaviorAgent;
    public Animator anim;
    public NavMeshAgent agent;
    public Collider col;
    [Header("보상 파티클 스케일")] public float rewardParticleScale;
    [Header("상태이상 비주얼")] [SerializeField] private StatusEffectVisualController statusEffectVisualController;

    private Rigidbody rb;
    
    private float attackDamage;
    private float rewardMultiplier = 1.0f;
    private bool returnInstanceCoroutineRunning = false;
    private readonly List<Collider> colliders = new List<Collider>(4);
    [SerializeField] private float screamerSkillRadius = 10f;
    [SerializeField] private float screamerSkillSpeedMultiplier = 1.5f;
    [Header("루트모션 회전 보정")]
    [SerializeField, Min(0.0f)] private float navigationRotationSpeed = 2.5f;
    [SerializeField, Range(0.0f, 1.0f)] private float movingRootMotionRotationWeight = 0.25f;
    [SerializeField] private bool logReceivedDamage = true;
    private Coroutine screamerSkillCoroutine;
    private readonly Dictionary<NormalZombie, Vector2> screamerOriginalSpeeds = new Dictionary<NormalZombie, Vector2>();

    public BlackboardVariable<BossZombieEnum> bossZombieEnum;
    public BlackboardVariable<GameObject> attackTargetBV;
    private BlackboardVariable<float> attackDistanceBV;
    private BlackboardVariable<bool> isDieBV;
    private BlackboardVariable<int> hitCountBV;
    private BlackboardVariable<int> curAttackCountBV;
    private float baseMoveSpeed;
    private float baseAttackSpeed;
    private float frostSpeedMultiplier = 1.0f;
    private FrostStatusRuntime frostStatusRuntime;
    private PoisonStatusRuntime poisonStatusRuntime;
    private ElectroStatusRuntime electroStatusRuntime;
    private IgnitionStatusRuntime ignitionStatusRuntime;
    private float electroStunRemainingDuration;
    private bool electroStunActive;
    
    public float TotalHp { get; private set; }
    public float CurrHp { get; private set; }
    public bool IsAlive { get; private set; }
    public bool IsPoisonLethalPending => poisonStatusRuntime != null && poisonStatusRuntime.IsLethalPending;

    // 최종 보상값을 저장하는 구조체
    private RewardResult rewardResult = new();

    // 필요한 컴포넌트와 비헤이비어 블랙보드 변수를 초기화한다
    public void Awake()
    {
        behaviorAgent = GetComponent<BehaviorGraphAgent>();
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        CacheStatusEffectVisualController();
        CacheFrostStatusRuntime();
        CachePoisonStatusRuntime();
        CacheElectroStatusRuntime();
        CacheIgnitionStatusRuntime();
        GetComponentsInChildren(false, colliders);

        ConfigureBehaviorNavigation();
        
        behaviorAgent.GetVariable("Enum", out bossZombieEnum);
        behaviorAgent.GetVariable("AttackTarget", out attackTargetBV);
        behaviorAgent.GetVariable("AttackDistance", out attackDistanceBV);
        behaviorAgent.GetVariable("isDie", out isDieBV);
        behaviorAgent.GetVariable("hitCount", out hitCountBV);
        behaviorAgent.GetVariable("curAttackCount", out curAttackCountBV);
    }

    // 풀에서 꺼낼 때 스펙 기반 보스 스탯과 런타임 상태를 초기화한다
    public override void OnSpawn()
    {
        base.OnSpawn();
        ConfigureBehaviorNavigation();

        var randomMoveAttackSpeed = Random.Range(spec.MinMoveAttackSpeed, spec.MaxMoveAttackSpeed);
        var randomAttackDamage = Random.Range(spec.MinAttackDamage, spec.MaxAttackDamage);
        var randomHp = Random.Range(spec.MinHp, spec.MaxHp);
        
        // 이동/공격 속도
        baseMoveSpeed = spec.MoveSpeed * randomMoveAttackSpeed;
        baseAttackSpeed = spec.AttackSpeed * randomMoveAttackSpeed;
        frostSpeedMultiplier = 1.0f;
        anim.SetFloat(SpeedHash, 0.0f);
        anim.SetFloat("AttackSpeed", baseAttackSpeed);

        // 공격 대미지
        attackDamage = spec.AttackDamage * randomAttackDamage;
        attackDistanceBV.Value = spec.AttackDistance;

        // 체력
        TotalHp = spec.Hp * randomHp;
        CurrHp = TotalHp;
        rewardMultiplier = 1.0f;
        IsAlive = true;
        storeDamage = 0f;
        hitCountBV.Value = 0;
        curAttackCountBV.Value = 0;
        attackTargetBV.Value = null;
        ResetFrostStatus();
        ResetPoisonStatus();
        ResetElectroStatus();
        ResetIgnitionStatus();

        // 체력 UI 슬라이더 값 지정
        hpUI.gameObject.SetActive(true);
        hpUI.InputTotalHp(TotalHp);
        hpUI.InputCurrHp(TotalHp);
        hpUI.gameObject.SetActive(false);
        
        SetCollidersEnabled(true);
        isDieBV.Value = false;

        // 코루틴 동작 상태 초기화
        returnInstanceCoroutineRunning = false;
    }

    // 스폰 프로필에서 전달한 HP, 공격력, 이동/공격 속도, 보상 배율을 적용한다
    public void ApplySpawnRuntimeModifiers(ZombieSpawnRuntimeModifiers modifiers)
    {
        ZombieSpawnRuntimeModifiers safeModifiers = modifiers.Sanitized();

        TotalHp *= safeModifiers.hpMultiplier;
        CurrHp = TotalHp;
        attackDamage *= safeModifiers.attackDamageMultiplier;
        rewardMultiplier = safeModifiers.rewardMultiplier;

        baseMoveSpeed *= safeModifiers.moveAttackSpeedMultiplier;

        if (anim != null)
        {
            anim.SetFloat("AttackSpeed", anim.GetFloat("AttackSpeed") * safeModifiers.moveAttackSpeedMultiplier);
            baseAttackSpeed = anim.GetFloat("AttackSpeed");
        }

        if (hpUI != null)
        {
            hpUI.InputTotalHp(TotalHp);
            hpUI.InputCurrHp(CurrHp);
        }
    }

    // 풀에 반환될 때 보스 전용 지속 효과와 버프 상태를 정리한다
    public override void OnDespawn()
    {
        if (screamerSkillCoroutine != null)
        {
            StopCoroutine(screamerSkillCoroutine);
            screamerSkillCoroutine = null;
        }

        RestoreAllScreamerSpeedBuffs();
        ResetFrostStatus();
        ResetPoisonStatus();
        ResetElectroStatus();
        ResetIgnitionStatus();
    }

    // 매 프레임 사망 처리와 루트모션 이동 방향을 갱신한다
    void Update()
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
        if (ignitionStatusRuntime != null)
        {
            ignitionStatusRuntime.Tick(Time.deltaTime);
        }
        UpdateElectroStun(Time.deltaTime);
        UpdateDeath();
        UpdateRootMotionNavigation();
        UpdateMoveAnimatorSpeed();
    }

    // 비헤이비어 트리가 경로를 제어하고 애니메이터 루트모션이 실제 위치를 갱신하도록 설정한다
    private void ConfigureBehaviorNavigation()
    {
        if (anim)
        {
            anim.applyRootMotion = true;
        }

        if (!agent)
        {
            return;
        }

        agent.updatePosition = false;
        agent.updateRotation = false;
    }

    // NavMeshAgent가 계산한 진행 방향으로 보스의 회전만 갱신한다
    private void UpdateRootMotionNavigation()
    {
        if (!IsAlive || electroStunActive || !agent || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.nextPosition = transform.position;

        Vector3 destDir = agent.desiredVelocity;
        destDir.y = 0f;

        if (destDir.sqrMagnitude <= 0.1f)
        {
            return;
        }

        destDir.Normalize();
        Quaternion targetRotation = Quaternion.LookRotation(destDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * navigationRotationSpeed);
    }

    // 현재 보스 이동 속도를 애니메이터 이동 속도 파라미터에 반영한다
    private void UpdateMoveAnimatorSpeed()
    {
        if (!anim)
        {
            return;
        }

        if (!IsAlive || electroStunActive || !agent || !agent.enabled || !agent.isOnNavMesh || agent.isStopped || attackTargetBV.Value == null)
        {
            anim.SetFloat(SpeedHash, 0f);
            return;
        }

        anim.SetFloat(SpeedHash, GetCurrentMoveAnimatorSpeed());
    }

    // 애니메이터 루트모션 이동량을 최상위 트랜스폼과 NavMeshAgent에 적용한다
    private void OnAnimatorMove()
    {
        if (!anim)
        {
            return;
        }

        if (!IsAlive)
        {
            // 사망 애니메이션 루트모션을 트랜스폼에 반영해 공중 부유 현상을 방지한다
            transform.position += anim.deltaPosition;
            transform.rotation *= anim.deltaRotation;
            return;
        }

        if (!agent || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        if (electroStunActive)
        {
            agent.nextPosition = transform.position;
            return;
        }

        if (HasAgentMoveIntent())
        {
            if (movingRootMotionRotationWeight > 0.0f)
            {
                transform.rotation *= Quaternion.Slerp(Quaternion.identity, anim.deltaRotation, movingRootMotionRotationWeight);
            }
        }
        else
        {
            transform.rotation *= anim.deltaRotation;
        }

        Vector3 deltaPosition = anim.deltaPosition;
        agent.Move(deltaPosition);
        transform.position = agent.nextPosition;
        agent.nextPosition = transform.position;
    }

    // NavMeshAgent가 실제 이동 의도를 가지고 있는지 확인한다
    private bool HasAgentMoveIntent()
    {
        return agent != null && !agent.isStopped && agent.desiredVelocity.sqrMagnitude > 0.1f;
    }
    
    // 사망 애니메이션이 충분히 진행되면 풀 반환 코루틴을 시작한다
    void UpdateDeath()
    {
        if(IsAlive || returnInstanceCoroutineRunning)
        {
            return;
        }
        var info = anim.GetCurrentAnimatorStateInfo(0);
        // 죽음 애니메이션 state/tag 확인, 3초 후 코루틴
        if (info.IsTag("Death") && info.normalizedTime >= 3f)
        {
            StartCoroutine(ReturnInstanceCoroutine());
        }
    }
    
    // 사망 연출 대기 후 보스 인스턴스를 풀에 반환한다
    IEnumerator ReturnInstanceCoroutine()
    {
        returnInstanceCoroutineRunning = true;
        yield return new WaitForSeconds(3f);
        ReturnInstance();
    }
    
    // 일반 공격 애니메이션 타이밍에 현재 공격 대상에게 피해를 준다
    public void OnAttack()
    {
        if (TryDamageAttackTarget(attackDamage))
        {
            curAttackCountBV.Value++;
        }
    }

    // 보스 타입에 맞는 스킬을 실행한다
    public void OnSkill()
    {
        switch (bossZombieEnum.Value)
        {
            case BossZombieEnum.Tank:
                TankSkill();
                break;
            case BossZombieEnum.Screamer:
                screamerSkillCoroutine = StartCoroutine(ScreamerSkill());
                break;
            case BossZombieEnum.Boomer:
                StartCoroutine(BoomerSkill());
                break;
        }
    }

    //탱크 스킬 : 5배 데미지로 1회 타격
    private void TankSkill()
    {
        //todo 탱크 스킬타격 이펙트
        TryDamageAttackTarget(attackDamage * 5f);
    }

    readonly WaitForSeconds screamerSkillWait = new WaitForSeconds(10f);
    //스크리머 스킬 : 주변 좀비 속도 버프
    private IEnumerator ScreamerSkill()
    {
        //todo 스크림 이펙트
        var colliders = Physics.OverlapSphere(transform.position, screamerSkillRadius);
        //List대신 HashSet으로 중복방지
        var zombies = new HashSet<NormalZombie>();

        foreach (var c in colliders)
        {
            var zombie = c.GetComponentInParent<NormalZombie>();

            if (!zombie || !zombie.IsAlive)
            {
                continue;
            }

            zombies.Add(zombie);
        }

        foreach (var zombie in zombies)
        {
            ApplyScreamerSpeedBuff(zombie);
        }

        yield return screamerSkillWait;

        foreach (var zombie in zombies)
        {
            RestoreScreamerSpeedBuff(zombie);
        }

        screamerSkillCoroutine = null;
    }

    //노말좀비 스피드 버프
    private void ApplyScreamerSpeedBuff(NormalZombie zombie)
    {
        if (!zombie)
        {
            return;
        }

        if (!screamerOriginalSpeeds.ContainsKey(zombie))
        {
            screamerOriginalSpeeds[zombie] = zombie.GetRuntimeBaseSpeeds();
        }

        var originalSpeeds = screamerOriginalSpeeds[zombie];
        zombie.SetRuntimeBaseSpeeds(
            originalSpeeds.x * screamerSkillSpeedMultiplier,
            originalSpeeds.y * screamerSkillSpeedMultiplier);
    }

    //노말좀비 버프 해제
    private void RestoreScreamerSpeedBuff(NormalZombie zombie)
    {
        if (zombie && screamerOriginalSpeeds.TryGetValue(zombie, out var originalSpeeds))
        {
            zombie.SetRuntimeBaseSpeeds(originalSpeeds.x, originalSpeeds.y);
        }

        screamerOriginalSpeeds.Remove(zombie);
    }

    //스크리머 사망시 전체 좀비 버프해제
    private void RestoreAllScreamerSpeedBuffs()
    {
        var zombies = new List<NormalZombie>(screamerOriginalSpeeds.Keys);

        foreach (var zombie in zombies)
        {
            RestoreScreamerSpeedBuff(zombie);
        }
    }

    //부머 스킬 : 1/2데미지로 1초마다 타격 10회 반복
    readonly WaitForSeconds boomerSkillWait = new WaitForSeconds(1f);
    // 부머 스킬을 1초 간격으로 반복 실행한다
    private IEnumerator BoomerSkill()
    {
        int t = 0;
        //todo 토하는 이펙트
        while (t < 10)
        {
            if (attackTargetBV.Value)
            {
                TryDamageAttackTarget(attackDamage / 2f);
            }

            t++;
            yield return boomerSkillWait;
        }
    }
    
    // 외부 공격으로 받은 데미지를 체력에 반영하고 사망 여부를 확인한다
    public void TakeDamage(float damage)
    {
        if (!IsAlive)
        {
            return;
        }

        float appliedDamage = Mathf.Max(0f, damage);
        CurrHp -= appliedDamage;
        StoreDamage(appliedDamage);
        CurrHp = Mathf.Clamp(CurrHp, 0f, TotalHp);
        hpUI.gameObject.SetActive(true);
        hpUI.InputCurrHp(CurrHp);

        if (logReceivedDamage)
        {
            //Debug.Log($"[BossZombie] Damage:{appliedDamage:0.###}, HP:{CurrHp:0.###}/{TotalHp:0.###}", this);
        }

        DamagePopupSpawner.SpawnDamage(transform, appliedDamage);

        if (CurrHp > 0f && poisonStatusRuntime != null && poisonStatusRuntime.IsActive)
        {
            poisonStatusRuntime.RefreshLethalPrediction();
        }

        if (CurrHp <= 0f)
        {
            Die();
        }
    }

    // Frost 빔으로 전달된 누적 슬로우 데이터를 갱신한다
    public void ApplyFrostStatus(FrostStatusPayload payload)
    {
        if (!IsAlive || frostStatusRuntime == null)
        {
            return;
        }

        frostStatusRuntime.ApplyFrostStatus(payload);
        NotifyIgnitionReaction(IgnitionReactionType.Frost);
    }

    // Poison 투사체로 전달된 중독 틱데미지 데이터를 갱신한다
    public void ApplyPoisonStatus(PoisonStatusPayload payload)
    {
        if (!IsAlive || poisonStatusRuntime == null)
        {
            return;
        }

        poisonStatusRuntime.ApplyPoisonStatus(payload);
        NotifyIgnitionReaction(IgnitionReactionType.Poison);
    }

    // Electro 투사체와 체인 라이트닝으로 전달된 Shock 스택 데이터를 갱신한다
    public void ApplyElectroStatus(ElectroStatusPayload payload, int chainIndex, float sourceDamage)
    {
        if (!IsAlive || electroStatusRuntime == null)
        {
            return;
        }

        electroStatusRuntime.ApplyElectroStatus(payload, chainIndex, sourceDamage);
        if (electroStatusRuntime.IsIgnitionReactionEligible)
        {
            NotifyIgnitionReaction(IgnitionReactionType.Electro);
        }
    }

    // 화염 공격으로 전달된 연소 상태 데이터를 갱신한다
    public void ApplyIgnitionStatus(IgnitionStatusPayload payload)
    {
        if (!IsAlive || ignitionStatusRuntime == null)
        {
            return;
        }

        ignitionStatusRuntime.ApplyIgnitionStatus(payload);
    }

    // Ignition 연소 중 다른 3세대 속성 공격 반응을 런타임에 전달한다
    public void NotifyIgnitionReaction(IgnitionReactionType reactionType)
    {
        if (!IsAlive || ignitionStatusRuntime == null)
        {
            return;
        }

        ignitionStatusRuntime.NotifyIgnitionReaction(reactionType);
    }

    // 비-Electro 피해가 적용되는 시점에 Electro Overload 발동 여부를 갱신한다
    public void NotifyNonElectroDamageReceived(float damage)
    {
        if (!IsAlive || electroStatusRuntime == null)
        {
            return;
        }

        electroStatusRuntime.NotifyNonElectroDamageReceived(damage);
        if (electroStatusRuntime.IsIgnitionReactionEligible)
        {
            NotifyIgnitionReaction(IgnitionReactionType.Electro);
        }
    }

    // 터렛 조준에 사용할 보스 루트 콜라이더 기준점을 반환한다
    public Vector3 GetAimPosition(float aimHeightRatio)
    {
        if (col != null)
        {
            return TurretAimPointUtility.GetAimPosition(col, aimHeightRatio);
        }

        return transform.position;
    }

    // Frost 상태가 계산한 속도 배율을 애니메이터 이동/공격 속도에 반영한다
    public void ApplyFrostSpeedMultiplier(float speedMultiplier)
    {
        float safeSpeedMultiplier = Mathf.Max(MinimumFrostSpeedMultiplier, Mathf.Clamp01(speedMultiplier));
        frostSpeedMultiplier = safeSpeedMultiplier;

        if (anim != null)
        {
            anim.SetFloat("AttackSpeed", baseAttackSpeed * safeSpeedMultiplier);
        }
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

    // Electro 경직 중 애니메이터 이동과 공격 속도를 0으로 고정한다
    private void ApplyElectroStunSpeedStop()
    {
        if (anim != null)
        {
            anim.SetFloat("AttackSpeed", 0.0f);
            anim.SetFloat(SpeedHash, 0.0f);
        }
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

    // 현재 보스 루트모션 재생에 사용할 이동 속도 값을 반환한다
    private float GetCurrentMoveAnimatorSpeed()
    {
        return Mathf.Max(0.0f, baseMoveSpeed * frostSpeedMultiplier);
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

    // 풀 재사용이나 사망 시 Ignition 연소 상태를 초기화한다
    private void ResetIgnitionStatus()
    {
        if (ignitionStatusRuntime == null)
        {
            return;
        }

        ignitionStatusRuntime.ResetStatus();
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

    // Frost 상태 런타임 컴포넌트를 캐시하고 보스 좀비 정책으로 초기화한다
    private void CacheFrostStatusRuntime()
    {
        frostStatusRuntime = GetComponent<FrostStatusRuntime>();
        if (frostStatusRuntime == null)
        {
            frostStatusRuntime = gameObject.AddComponent<FrostStatusRuntime>();
        }

        frostStatusRuntime.Initialize(this, this, statusEffectVisualController, false);
    }

    // Poison 상태 런타임 컴포넌트를 캐시하고 보스 좀비 정책으로 초기화한다
    private void CachePoisonStatusRuntime()
    {
        poisonStatusRuntime = GetComponent<PoisonStatusRuntime>();
        if (poisonStatusRuntime == null)
        {
            poisonStatusRuntime = gameObject.AddComponent<PoisonStatusRuntime>();
        }

        poisonStatusRuntime.Initialize(this, statusEffectVisualController, true, false);
    }

    // Electro 상태 런타임 컴포넌트를 캐시하고 보스 좀비 정책으로 초기화한다
    private void CacheElectroStatusRuntime()
    {
        electroStatusRuntime = GetComponent<ElectroStatusRuntime>();
        if (electroStatusRuntime == null)
        {
            electroStatusRuntime = gameObject.AddComponent<ElectroStatusRuntime>();
        }

        electroStatusRuntime.Initialize(this, true);
    }

    // Ignition 상태 런타임 컴포넌트를 캐시하고 보스 좀비 정책으로 초기화한다
    private void CacheIgnitionStatusRuntime()
    {
        ignitionStatusRuntime = GetComponent<IgnitionStatusRuntime>();
        if (ignitionStatusRuntime == null)
        {
            ignitionStatusRuntime = gameObject.AddComponent<IgnitionStatusRuntime>();
        }

        ignitionStatusRuntime.Initialize(this, statusEffectVisualController, true);
    }

    float storeDamage = 0;
    //데미지 축적 및 넉백 호출 메서드
    public void StoreDamage(float damage)
    {
        storeDamage += damage;
        float hitCountDamageThreshold = TotalHp / 2f;
        if (storeDamage >= hitCountDamageThreshold)
        {
            hitCountBV.Value++;
            storeDamage = 0f;
        }
    }

    // 현재 공격 대상이 유효하면 지정한 데미지를 적용한다
    private bool TryDamageAttackTarget(float damage)
    {
        if (!attackTargetBV.Value)
        {
            return false;
        }

        IDamageable iDmg = attackTargetBV.Value.GetComponentInParent<IDamageable>();
        if (iDmg == null)
        {
            Debug.LogError($"[BossZombie] {attackTargetBV.Value.gameObject.name}이 IDamageable을 상속하지 않음");
            return false;
        }

        if (!iDmg.IsAlive)
        {
            print($"[BossZombie] {attackTargetBV.Value.gameObject.name}이 살아있지 않은 오브젝트임");
            return false;
        }

        iDmg.TakeDamage(damage);
        return true;
    }
    
    /// <summary>
    /// 사망 상태로 전환한다<para/>
    /// 죽은 보스가 타겟/충돌 대상으로 남지 않도록 콜라이더를 비활성화한다
    /// </summary>
    // 사망 상태 전환과 처치 보상 지급을 한 번만 처리한다
    private void Die()
    {
        IsAlive = false; // 생존 상태 비활성화
        GameManager.Inst.IncreaseKillCount();
        GrantKillReward(); // 이 메서드 내부에서 rewardResult를 얻는다.

        // 코인 획득량에 따라 다른 코인 파티클을 생성한다.
        if (CoinParticleCreator.Inst)
        {
            CoinParticleCreator.Inst.Create(rewardResult, transform.position, transform.localScale * rewardParticleScale);
        }

        hpUI.gameObject.SetActive(false); // hp UI 비활성화
        TriggerFrostDeathEffectIfNeeded();
        ResetFrostStatus();
        ResetPoisonStatus();
        ResetElectroStatus();
        ResetIgnitionStatus();

        if (agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                // 사망 시작 위치를 NavMesh 표면 Y로 보정해 초기 부유를 방지한다
                Vector3 snapPos = transform.position;
                snapPos.y = agent.nextPosition.y;
                transform.position = snapPos;

                agent.ResetPath();
            }

            agent.enabled = false;
        }

        SetCollidersEnabled(false); // 사망 후 터렛 타겟/발사체 충돌 대상에서 제외

        isDieBV.Value = true;
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

    // 보스 프리팹 Override 또는 스펙의 보상 프로필을 기준으로 처치 보상을 지급한다
    private void GrantKillReward()
    {
        if (spec == null)
        {
            Debug.LogWarning("[BossZombie] 스펙이 없어 처치 보상을 지급할 수 없습니다.", this);
            return;
        }

        int wave = GameManager.Inst == null ? 1 : GameManager.Inst.Wave;
        ZombieRewardContext rewardContext = ZombieRewardContext.CreateBossZombie(wave, spec, transform.position).WithRewardMultiplier(rewardMultiplier);

        // ref rewardResult를 통해 최종 보상값을 얻는다.
        RewardGrantUtility.GrantZombieReward(GetRewardProfile(), rewardContext, this, ref rewardResult);
    }

    // 프리팹별 Override가 있으면 우선 사용하고 없으면 스펙 기본 보상 프로필을 반환한다
    private ZombieRewardProfileSO GetRewardProfile()
    {
        return rewardProfileOverride != null ? rewardProfileOverride : spec.RewardProfile;
    }

    /// <summary>
    /// 풀링 재사용과 사망 상태에 맞춰 전체 콜라이더와 리지드바디 활성 상태를 변경한다
    /// </summary>
    /// <param name="isEnabled"></param>
    // 풀 재사용과 사망 상태에 맞춰 전체 콜라이더 활성 상태를 변경한다
    private void SetCollidersEnabled(bool isEnabled)
    {
        for (int i = 0; i < colliders.Count; i++)
        {
            Collider colliderComp = colliders[i];
            if (colliderComp == null)
            {
                continue;
            }

            colliderComp.enabled = isEnabled;
        }
        if (rb)
        {
            rb.isKinematic = !isEnabled;
            rb.detectCollisions = isEnabled;
        }
    }
    
    // 스폰 위치로 보스를 이동시키고 NavMeshAgent를 재활성화한다
    public void SetPosition(Transform t)
    {
        if (t == null)
        {
            return;
        }

        SetPosition(t.position);
    }

    // 스폰 위치 좌표로 보스를 이동시키고 NavMeshAgent를 재활성화한다
    public void SetPosition(Vector3 position)
    {
        SetCollidersEnabled(true);
        transform.position = position;
        agent.enabled = true;
        agent.isStopped = false;
        ConfigureBehaviorNavigation();
        agent.Warp(position);
    }
    
    // 스포너 호출 호환성만 유지하고 실제 이동 제어는 비헤이비어 트리에 맡긴다
    public void SetDestination(Transform t)
    {
        agent.enabled = true;
        agent.isStopped = false;
        ConfigureBehaviorNavigation();
    }
}
