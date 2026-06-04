using System;
using System.Collections;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class BossZombieController : PoolObject, IDamageable
{
    public BossZombieSpec spec;
    public HpUI hpUI;
    
    public BehaviorGraphAgent behaviorAgent;
    public Animator anim;
    public NavMeshAgent agent;
    public Collider col;
    
    private Transform destination;
    private float attackDamage;
    private bool returnInstanceCoroutineRunning = false;

    public BlackboardVariable<GameObject> attackTargetBV;
    private BlackboardVariable<bool> isDieBV;
    private BlackboardVariable<int> hitCountBV;
    private BlackboardVariable<int> curAttackCountBV;
    
    public float TotalHp { get; set; }
    public float CurrHp { get; set; }
    public bool IsAlive{ get; set; }

    public void Awake()
    {
        behaviorAgent = GetComponent<BehaviorGraphAgent>();
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        col = GetComponent<Collider>();
    }

    public void Start()
    {
        behaviorAgent.GetVariable("AttackTarget", out attackTargetBV);
        behaviorAgent.GetVariable("isDie", out isDieBV);
        behaviorAgent.GetVariable("hitCount", out hitCountBV);
        behaviorAgent.GetVariable("curAttackCount", out curAttackCountBV);
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        var randomMoveAttackSpeed = Random.Range(spec.MinMoveAttackSpeed, spec.MaxMoveAttackSpeed);
        var randomAttackDamage = Random.Range(spec.MinAttackDamage, spec.MaxAttackDamage);
        var randomHp = Random.Range(spec.MinHp, spec.MaxHp);
        float wave = GameManager.Inst.wave;
        bool isFirstWave = GameManager.Inst.wave == 1;
        
        // 이동/공격 속도
        var moveAttackSpeedMul = isFirstWave ? randomMoveAttackSpeed : randomMoveAttackSpeed * Mathf.Pow(1f + spec.MoveAttackSpeedWeight, wave - 1f);
        anim.SetFloat("AttackSpeed", spec.AttackSpeed * moveAttackSpeedMul);

        // 공격 대미지
        var attackDamageMul = isFirstWave ? randomAttackDamage : randomAttackDamage * Mathf.Pow(1f + spec.AttackDamageWeight, wave - 1f);
        attackDamage = spec.AttackDamage * attackDamageMul;

        // 체력
        var hpMul = isFirstWave ? randomHp : randomHp * Mathf.Pow(1f + spec.HpWeight, wave - 1f);
        TotalHp = spec.Hp * hpMul;
        CurrHp = TotalHp;
        IsAlive = true;

        // 체력 UI 슬라이더 값 지정
        hpUI.gameObject.SetActive(true);
        hpUI.InputTotalHp(TotalHp);
        hpUI.InputCurrHp(TotalHp);
        
        col.enabled = true;
        agent.enabled = true;
        isDieBV.Value = false;

        // 코루틴 동작 상태 초기화
        returnInstanceCoroutineRunning = false;
    }

    public override void OnDespawn()
    {
        GameManager.Inst.IncreaseKillCount();
    }

    void Update()
    {
        UpdateDeath();
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
        if(attackTargetBV.Value)
        {
            if(attackTargetBV.Value.TryGetComponent<IDamageable>(out var iDmg))
            {
                iDmg.TakeDamage(attackDamage);
                curAttackCountBV.Value++;
            }
            else
            {
                Debug.LogError("[NormalZombie] 공격 대상이 IDamageable을 상속하지 않음");
            }
        }
    }
    
    public void TakeDamage(float damage)
    {
        if(!IsAlive) 
        {
            return;
        }
        CurrHp -= damage;
        //todo 데미지 축적, 넉백 로직 필요 hitCountBV
        CurrHp = Mathf.Clamp(CurrHp, 0f, TotalHp);
        hpUI.InputCurrHp(CurrHp);

        if (CurrHp <= 0f)
        {
            Die();
        }
    }
    
    private void Die()
    {
        IsAlive = false;

        hpUI.gameObject.SetActive(false);

        if (agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            agent.enabled = false;
        }

        col.enabled = false;

        isDieBV.Value = true;
    }
    
    public void SetPosition(Transform t)
    {
        transform.position = t.position;
        agent.enabled = true;
        agent.Warp(t.position);
    }
    
    public void SetDestination(Transform t)
    {
        destination = t;
        agent.enabled = true;
        agent.SetDestination(t.position);
    }
}
