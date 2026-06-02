using UnityEngine;

[CreateAssetMenu(fileName = "NormalZombieSpec", menuName = "Scriptable Objects/NormalZombieSpec")]
public class NormalZombieSpec : ScriptableObject
{
    [Header("일반 좀비 스펙(웨이브 1 기준)")]

    [Header("기본 이동 속도")] public float MoveSpeed;
    [Header("이동 속도 웨이브 반영 수치")] public float MoveSpeedWaveMultiply;
    [Header("이동 속도 랜덤 범위")] 
    public float MinMoveSpeed;
    public float MaxMoveSpeed;

    [Header("기본 공격 속도")] public float AttackSpeed;
    [Header("공격 속도 웨이브 반영 수치")] public float AttackSpeedWaveMultiply;
    [Header("공격 속도 랜덤 범위")]
    public float MinAttackSpeed;
    public float MaxAttackSpeed;
    
    [Header("기본 공격 대미지")] public float AttackDamage;
    [Header("공격 속도 웨이브 반영 수치")] public float AttackDamageWaveMultiply;
    [Header("공격 대미지 랜덤 범위")] 
    public float MinAttackDamage;
    public float MaxAttackDamage;

    [Header("공격 가능 사거리")] public float AttackDistance;

    [Header("기본 HP")] public float Hp;
    [Header("Hp 웨이브 반영 수치")] public float HpWaveMultiply;
    [Header("Hp 랜덤 범위")]
    public float MinHp;
    public float MaxHp;

    [Header("기본 아이템 드랍률(int)")] public float NoramlItemDropPercentage;
    [Header("레어 아이템 드랍률(int)")] public float RareItemDropPercentage;
}
