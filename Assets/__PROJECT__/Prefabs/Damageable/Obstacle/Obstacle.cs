using System;
using System.Collections;
using DinoFracture;
using UnityEngine;
using Random = UnityEngine.Random;

public class Obstacle : MonoBehaviour, IDamageable
{
    public ObstacleSpec spec;
    
    public HpUI hpUI;
    
    public float TotalHp { get; private set; }
    public float CurrHp { get; private set; }
    public bool IsAlive { get; private set; }
    
    private RuntimeFracturedGeometry fractureGeometry;

    private void Awake()
    {
        fractureGeometry = GetComponent<RuntimeFracturedGeometry>();
    }

    private void Start()
    {
        //레벨에 맞춰서 hp 설정
        TotalHp = spec.Hp + spec.level * spec.levelWeight;
        CurrHp = TotalHp;
        IsAlive = true;
        
        hpUI.InputTotalHp(TotalHp);
        hpUI.InputCurrHp(TotalHp);
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

    //피격
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

    public void Destroy()
    {
        Destroy(gameObject);
    }
    
    public void SetPosition(Transform t)
    {
        transform.position = t.position;
    }
}
