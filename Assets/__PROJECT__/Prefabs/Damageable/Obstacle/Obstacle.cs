using System;
using System.Collections;
using DinoFracture;
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
    
    private RuntimeFracturedGeometry fractureGeometry;

    private void Awake()
    {
        fractureGeometry = GetComponent<RuntimeFracturedGeometry>();
    }

    //todo 레벨업시 호출, 레벨 가중치를 hp최대치에 추가
    public void LevelUp()
    {
        TotalHp = spec.Hp + spec.level * spec.levelWeight;
        hpUI.InputTotalHp(TotalHp);
    }
    
    //파편화 
    public void Fracture()
    {
        if (fractureGeometry == null || fractureGeometry.IsProcessingFracture)
        {
            return;
        }

        fractureGeometry.Fracture();
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        
        var randomHp = Random.Range(spec.MinHp, spec.MaxHp);
        
        //레벨에 맞춰서 hp 설정
        TotalHp = spec.Hp + spec.level * spec.levelWeight;
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
            
            Fracture(); //DinoFracture 파편 효과
        }
    }
    
    public void SetPosition(Transform t)
    {
        transform.position = t.position;
    }
}
