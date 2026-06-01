using UnityEngine;

[CreateAssetMenu(fileName = "NormalZombieSpec", menuName = "Scriptable Objects/NormalZombieSpec")]
public class NormalZombieSpec : ScriptableObject
{
    [Header("일반 좀비 스펙(웨이브 1 기준)")]
    [Header("기본 이동 속도(float)")] public float MoveSpeed;
    [Header("이동 속도 랜덤 범위(Multiply)")][Range(0f, 2f)] public float MoveSpeedRandomRange;
    [Header("기본 공격 속도(float)")] public float AttackSpeed;
    [Header("공격 속도 랜덤 범위(Multiply)")][Range(0f, 2f)] public float AttackSpeedRandomRange;
    [Header("기본 공격 대미지(float)")] public float AttackDamage;
    [Header("공격 대미지 랜덤 범위(Multiply)")][Range(0f, 2f)] public float AttackDamageRandomRange;

    [Header("기본 아이템 드랍률(int)")] public float NoramlItemDropPercentage;
    [Header("레어 아이템 드랍률(int)")] public float RareItemDropPercentage;
}
