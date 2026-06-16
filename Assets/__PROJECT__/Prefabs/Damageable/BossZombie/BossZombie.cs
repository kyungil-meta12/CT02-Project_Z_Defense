using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

/// <summary>
/// 보스 좀비의 웨이브 스탯 초기화, 스킬, 피격, 사망, 처치 보상 지급을 담당한다.
/// </summary>
public class BossZombie : PoolObject, IDamageable, IFrostStatusEffectReceiver, IPoisonStatusEffectReceiver
{
    private static readonly int SpeedHash = Animator.StringToHash("speed");

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
    private BlackboardVariable<float> speedBV;
    private float baseMoveSpeed;
    private float baseAttackSpeed;
    private float frostSlowRatio;
    private float frostExposureTimer;
    private float frostHoldTimer;
    private bool frostStatusDirty;
    private bool frostStatusActive;
    private PoisonStatusPayload poisonStatusPayload;
    private float poisonRemainingDuration;
    private float poisonTickTimer;
    private int poisonStackCount;
    private bool poisonStatusActive;
    
    public float TotalHp { get; private set; }
    public float CurrHp { get; private set; }
    public bool IsAlive { get; private set; }

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
        GetComponentsInChildren(false, colliders);

        ConfigureBehaviorNavigation();
        
        behaviorAgent.GetVariable("Enum", out bossZombieEnum);
        behaviorAgent.GetVariable("AttackTarget", out attackTargetBV);
        behaviorAgent.GetVariable("AttackDistance", out attackDistanceBV);
        behaviorAgent.GetVariable("isDie", out isDieBV);
        behaviorAgent.GetVariable("hitCount", out hitCountBV);
        behaviorAgent.GetVariable("curAttackCount", out curAttackCountBV);
        behaviorAgent.GetVariable("speed", out speedBV);
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
        speedBV.Value = baseMoveSpeed;
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

        // 체력 UI 슬라이더 값 지정
        hpUI.gameObject.SetActive(true);
        hpUI.InputTotalHp(TotalHp);
        hpUI.InputCurrHp(TotalHp);
        hpUI.gameObject.SetActive(false);
        
        SetCollidersEnabled(true);
        agent.enabled = true;
        agent.isStopped = false;
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

        if (speedBV != null)
        {
            speedBV.Value *= safeModifiers.moveAttackSpeedMultiplier;
            baseMoveSpeed = speedBV.Value;
        }

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
    }

    // 매 프레임 사망 처리와 루트모션 이동 방향을 갱신한다
    // 매 프레임 사망 처리와 루트모션 이동 방향을 갱신한다
    void Update()
    {
        UpdateFrostStatus(Time.deltaTime);
        UpdatePoisonStatus(Time.deltaTime);
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
        if (!IsAlive || !agent || !agent.enabled || !agent.isOnNavMesh)
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

    // NavMeshAgent의 설정 속도를 애니메이터 이동 속도 파라미터에 반영한다
    private void UpdateMoveAnimatorSpeed()
    {
        if (!anim)
        {
            return;
        }

        if (!IsAlive || !agent || !agent.enabled || !agent.isOnNavMesh || agent.isStopped || attackTargetBV.Value == null)
        {
            anim.SetFloat(SpeedHash, 0f);
            return;
        }

        anim.SetFloat(SpeedHash, agent.speed);
    }

    // 애니메이터 루트모션 이동량을 최상위 트랜스폼과 NavMeshAgent에 적용한다
    private void OnAnimatorMove()
    {
        if (!IsAlive || !anim || !agent || !agent.enabled || !agent.isOnNavMesh)
        {
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

        if (CurrHp <= 0f)
        {
            Die();
        }
    }

    // Frost 빔으로 전달된 누적 슬로우 데이터를 갱신한다
    public void ApplyFrostStatus(FrostStatusPayload payload)
    {
        if (!IsAlive)
        {
            return;
        }

        float safeMaxSlowRatio = Mathf.Clamp01(payload.maxSlowRatio);
        float safeBuildUpDuration = Mathf.Max(0.0f, payload.slowBuildUpDuration);
        float safeTickInterval = Mathf.Max(0.0f, payload.tickInterval);

        if (safeMaxSlowRatio > 0.0f)
        {
            frostExposureTimer += safeTickInterval > 0.0f ? safeTickInterval : Time.deltaTime;
            float buildUpRatio = safeBuildUpDuration > 0.0f ? Mathf.Clamp01(frostExposureTimer / safeBuildUpDuration) : 1.0f;
            frostSlowRatio = Mathf.Max(frostSlowRatio, safeMaxSlowRatio * buildUpRatio);
            frostHoldTimer = Mathf.Max(frostHoldTimer, payload.slowHoldDuration);
            frostStatusDirty = true;
        }

        // 보스는 Frost 슬로우만 받고 빙결 폭발은 적용하지 않는다.
    }

    // Poison 투사체로 전달된 중독 틱데미지 데이터를 갱신한다
    public void ApplyPoisonStatus(PoisonStatusPayload payload)
    {
        if (!IsAlive || !payload.hasPoisonStatus)
        {
            return;
        }

        poisonStatusPayload = payload;
        int safeMaxStackCount = Mathf.Max(1, payload.maxStackCount);

        if (poisonStackCount <= 0)
        {
            poisonStackCount = 1;
        }
        else if (payload.stackRefreshMode == PoisonStackRefreshMode.AddStackAndRefreshDuration)
        {
            poisonStackCount = Mathf.Min(safeMaxStackCount, poisonStackCount + 1);
        }

        poisonRemainingDuration = Mathf.Max(poisonRemainingDuration, payload.duration);
        if (poisonTickTimer <= 0.0f)
        {
            poisonTickTimer = Mathf.Max(0.01f, payload.tickInterval);
        }

        poisonStatusActive = true;
        SetPoisonVisualActive(true);
    }

    // Frost 상태 타이머를 감소시키고 이동/공격 속도를 갱신한다
    private void UpdateFrostStatus(float deltaTime)
    {
        if (!frostStatusActive && !frostStatusDirty)
        {
            return;
        }

        if (frostHoldTimer > 0.0f)
        {
            frostHoldTimer = Mathf.Max(0.0f, frostHoldTimer - deltaTime);
        }

        if (frostHoldTimer <= 0.0f)
        {
            frostSlowRatio = 0.0f;
            frostExposureTimer = 0.0f;
        }

        ApplyFrostSpeedModifier();
    }

    // Poison 상태 타이머를 감소시키고 틱마다 보스 보정 체력비례 데미지를 적용한다
    private void UpdatePoisonStatus(float deltaTime)
    {
        if (!poisonStatusActive)
        {
            return;
        }

        if (!IsAlive)
        {
            ResetPoisonStatus();
            return;
        }

        poisonRemainingDuration = Mathf.Max(0.0f, poisonRemainingDuration - deltaTime);
        poisonTickTimer -= deltaTime;

        if (poisonTickTimer <= 0.0f && poisonRemainingDuration > 0.0f)
        {
            ApplyPoisonTickDamage();
            poisonTickTimer = Mathf.Max(0.01f, poisonStatusPayload.tickInterval);
        }

        if (poisonRemainingDuration <= 0.0f)
        {
            ResetPoisonStatus();
        }
    }

    // 현재 중독 중첩 수와 보스 보정 배율에 맞는 최대체력 비례 틱데미지를 적용한다
    private void ApplyPoisonTickDamage()
    {
        if (!IsAlive || poisonStackCount <= 0 || poisonStatusPayload.maxHpDamageRatioPerTick <= 0.0f)
        {
            return;
        }

        float damage = TotalHp * Mathf.Clamp01(poisonStatusPayload.maxHpDamageRatioPerTick) * poisonStackCount * Mathf.Max(0.0f, poisonStatusPayload.bossDamageMultiplier);
        TakeDamage(damage);
    }

    // 현재 Frost 상태에 맞춰 비헤이비어 이동 속도와 공격 속도를 반영한다
    private void ApplyFrostSpeedModifier()
    {
        float speedMultiplier = 1.0f;
        if (frostHoldTimer > 0.0f)
        {
            speedMultiplier = Mathf.Clamp01(1.0f - frostSlowRatio);
        }

        if (speedBV != null)
        {
            speedBV.Value = baseMoveSpeed * speedMultiplier;
        }

        if (anim != null)
        {
            anim.SetFloat("AttackSpeed", baseAttackSpeed * speedMultiplier);
        }

        frostStatusActive = speedMultiplier < 1.0f;
        SetFrostVisualActive(frostStatusActive);
        frostStatusDirty = false;
    }

    // 풀 재사용이나 사망 시 Frost 상태를 초기화하고 원래 속도를 복구한다
    private void ResetFrostStatus()
    {
        frostSlowRatio = 0.0f;
        frostExposureTimer = 0.0f;
        frostHoldTimer = 0.0f;
        frostStatusDirty = false;
        frostStatusActive = false;

        if (speedBV != null)
        {
            speedBV.Value = baseMoveSpeed;
        }

        if (anim != null)
        {
            anim.SetFloat("AttackSpeed", baseAttackSpeed);
        }

        SetFrostVisualActive(false);
    }

    // 풀 재사용이나 사망 시 Poison 상태를 초기화하고 비주얼을 끈다
    private void ResetPoisonStatus()
    {
        poisonStatusPayload = default;
        poisonRemainingDuration = 0.0f;
        poisonTickTimer = 0.0f;
        poisonStackCount = 0;
        poisonStatusActive = false;
        SetPoisonVisualActive(false);
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

    // 프로스트 상태 활성 여부에 맞춰 비주얼 컨트롤러를 갱신한다
    private void SetFrostVisualActive(bool isActive)
    {
        if (statusEffectVisualController == null)
        {
            return;
        }

        statusEffectVisualController.SetFrostSlowActive(isActive);
    }

    // 포이즌 상태 활성 여부에 맞춰 비주얼 컨트롤러를 갱신한다
    private void SetPoisonVisualActive(bool isActive)
    {
        if (statusEffectVisualController == null)
        {
            return;
        }

        statusEffectVisualController.SetPoisonActive(isActive);
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
        ResetFrostStatus();
        ResetPoisonStatus();

        if (agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            agent.enabled = false;
        }

        SetCollidersEnabled(false); // 사망 후 터렛 타겟/발사체 충돌 대상에서 제외

        isDieBV.Value = true;
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
        SetCollidersEnabled(true);
        transform.position = t.position;
        agent.enabled = true;
        agent.isStopped = false;
        ConfigureBehaviorNavigation();
        agent.Warp(t.position);
    }
    
    // 스포너 호출 호환성만 유지하고 실제 이동 제어는 비헤이비어 트리에 맡긴다
    public void SetDestination(Transform t)
    {
        agent.enabled = true;
        agent.isStopped = false;
        ConfigureBehaviorNavigation();
    }
}
