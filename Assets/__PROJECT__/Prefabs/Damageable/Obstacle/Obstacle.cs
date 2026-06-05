using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class Obstacle : PoolObject, IDamageable
{
    public ObstacleSpec spec;
    
    public HpUI hpUI;
    
    public float TotalHp { get; set; }
    public float CurrHp { get; set; }
    public bool IsAlive { get; set; }
    
    private bool returnInstanceCoroutineRunning = false;

    public override void OnSpawn()
    {
        base.OnSpawn();
        
        var randomHp = Random.Range(spec.MinHp, spec.MaxHp);
        
        //var hpMul = isFirstWave ? randomHp : randomHp * Mathf.Pow(1f + spec.HpWeight, wave - 1f);
        //todo 레벨디자인에 맞춰서 hpMul 설정 필요
        TotalHp = spec.Hp;// *hpMul;
        CurrHp = TotalHp;
        IsAlive = true;
        
        hpUI.InputTotalHp(TotalHp);
        hpUI.InputCurrHp(TotalHp);

        returnInstanceCoroutineRunning = false;
    }
    
    public override void OnDespawn()
    {}
    
    void Update()
    {
        UpdateDeath();
    }
    
    void UpdateDeath()
    {
        if(IsAlive)
        {
            return;
        }
        if (!returnInstanceCoroutineRunning)
        {
            StartCoroutine(ReturnInstanceCoroutine());
        }
        IEnumerator ReturnInstanceCoroutine()
        {
            returnInstanceCoroutineRunning = true;
            yield return new WaitForSeconds(3f);
            ReturnInstance();
        }
    }

    public void TakeDamage(float damage)
    {
        if(!IsAlive)
        {
            return;
        }
        CurrHp -= damage;
        CurrHp = Mathf.Clamp(CurrHp, 0f, TotalHp);
        hpUI.InputCurrHp(CurrHp);

        if (CurrHp <= 0f)
        {
            hpUI.gameObject.SetActive(false); // hp UI 비활성화
            IsAlive = false; // 생존 상태 비활성화
            //todo DinoFracture 파편 효과
        }
    }
    
    public void SetPosition(Transform t)
    {
        transform.position = t.position;
    }
}
