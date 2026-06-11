using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class BossZombie : PoolObject, IDamageable
{
    private const string RootMotionScaleRootName = "Root";

    public BossZombieSpec spec;
    public HpUI hpUI;
    
    public BehaviorGraphAgent behaviorAgent;
    public Animator anim;
    public NavMeshAgent agent;
    public Collider col;
    private Rigidbody rb;
    [SerializeField] private Transform boneRoot;
    
    private float attackDamage;
    private bool returnInstanceCoroutineRunning = false;
    private readonly List<Collider> colliders = new List<Collider>(4);
    [SerializeField] private float screamerSkillRadius = 10f;
    [SerializeField] private float screamerSkillSpeedMultiplier = 1.5f;
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
    
    public float TotalHp { get; private set; }
    public float CurrHp { get; private set; }
    public bool IsAlive { get; private set; }

    // 필요한 컴포넌트와 비헤이비어 블랙보드 변수를 초기화한다
    public void Awake()
    {
        behaviorAgent = GetComponent<BehaviorGraphAgent>();
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
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

    public override void OnSpawn()
    {
        base.OnSpawn();
        ConfigureBehaviorNavigation();

        var randomMoveAttackSpeed = Random.Range(spec.MinMoveAttackSpeed, spec.MaxMoveAttackSpeed);
        var randomAttackDamage = Random.Range(spec.MinAttackDamage, spec.MaxAttackDamage);
        var randomHp = Random.Range(spec.MinHp, spec.MaxHp);
        float wave = GameManager.Inst.Wave;
        bool isFirstWave = GameManager.Inst.Wave == 1;
        
        // 이동/공격 속도
        var moveAttackSpeedMul = isFirstWave ? randomMoveAttackSpeed : randomMoveAttackSpeed * Mathf.Pow(1f + spec.MoveAttackSpeedWeight, wave - 1f);
        speedBV.Value = spec.MoveSpeed * moveAttackSpeedMul;
        anim.SetFloat("AttackSpeed", spec.AttackSpeed * moveAttackSpeedMul);

        // 공격 대미지
        var attackDamageMul = isFirstWave ? randomAttackDamage : randomAttackDamage * Mathf.Pow(1f + spec.AttackDamageWeight, wave - 1f);
        attackDamage = spec.AttackDamage * attackDamageMul;
        attackDistanceBV.Value = spec.AttackDistance;

        // 체력
        var hpMul = isFirstWave ? randomHp : randomHp * Mathf.Pow(1f + spec.HpWeight, wave - 1f);
        TotalHp = spec.Hp * hpMul;
        CurrHp = TotalHp;
        IsAlive = true;
        storeDamage = 0f;
        hitCountBV.Value = 0;
        curAttackCountBV.Value = 0;
        attackTargetBV.Value = null;

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

    public override void OnDespawn()
    {
        if (screamerSkillCoroutine != null)
        {
            StopCoroutine(screamerSkillCoroutine);
            screamerSkillCoroutine = null;
        }

        RestoreAllScreamerSpeedBuffs();
    }

    // 매 프레임 사망 처리와 루트모션 이동 방향을 갱신한다
    void Update()
    {
        UpdateDeath();
        UpdateRootMotionNavigation();
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
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2.5f);
    }

    // 애니메이터 루트모션 이동량에 본 루트 스케일을 반영해 최상위 트랜스폼과 NavMeshAgent에 적용한다
    private void OnAnimatorMove()
    {
        if (!IsAlive || !anim || !agent || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 deltaPosition = anim.deltaPosition;
        if (boneRoot)
        {
            Vector3 rootScale = boneRoot.localScale;
            Vector3 localDelta = transform.InverseTransformVector(deltaPosition);
            localDelta.x *= rootScale.x;
            localDelta.z *= rootScale.z;
            deltaPosition = transform.TransformVector(localDelta);
        }

        transform.position += deltaPosition;
        agent.nextPosition = transform.position;
    }
    
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
    
    IEnumerator ReturnInstanceCoroutine()
    {
        returnInstanceCoroutineRunning = true;
        yield return new WaitForSeconds(3f);
        ReturnInstance();
    }
    
    public void OnAttack()
    {
        if (TryDamageAttackTarget(attackDamage))
        {
            curAttackCountBV.Value++;
        }
    }

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
        if (!zombie || !zombie.anim)
        {
            return;
        }

        if (!screamerOriginalSpeeds.ContainsKey(zombie))
        {
            screamerOriginalSpeeds[zombie] = new Vector2(
                zombie.anim.GetFloat("MoveSpeed"),
                zombie.anim.GetFloat("AttackSpeed")
            );
        }

        var originalSpeeds = screamerOriginalSpeeds[zombie];
        zombie.anim.SetFloat("MoveSpeed", originalSpeeds.x * screamerSkillSpeedMultiplier);
        zombie.anim.SetFloat("AttackSpeed", originalSpeeds.y * screamerSkillSpeedMultiplier);
    }

    //노말좀비 버프 해제
    private void RestoreScreamerSpeedBuff(NormalZombie zombie)
    {
        if (zombie && zombie.anim && screamerOriginalSpeeds.TryGetValue(zombie, out var originalSpeeds))
        {
            zombie.anim.SetFloat("MoveSpeed", originalSpeeds.x);
            zombie.anim.SetFloat("AttackSpeed", originalSpeeds.y);
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

    float storeDamage = 0;
    //데미지 축적 및 넉백 호출 메서드
    public void StoreDamage(float damage)
    {
        storeDamage += damage;
        float hitCountDamageThreshold = TotalHp / 3f;
        if (storeDamage >= hitCountDamageThreshold)
        {
            hitCountBV.Value++;
            storeDamage = 0f;
        }
    }

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
    private void Die()
    {
        IsAlive = false; // 생존 상태 비활성화
        GameManager.Inst.IncreaseKillCount();

        hpUI.gameObject.SetActive(false); // hp UI 비활성화

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

    /// <summary>
    /// 풀링 재사용과 사망 상태에 맞춰 전체 콜라이더와 리지드바디 활성 상태를 변경한다
    /// </summary>
    /// <param name="isEnabled"></param>
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
