using UnityEngine;

[CreateAssetMenu(fileName = "ObstacleSpec", menuName = "Scriptable Objects/ObstacleSpec")]
public class ObstacleSpec : ScriptableObject
{
    [Header("장애물 스펙")]
    
    [Header("기본 HP")] public float Hp;
    [Header("Hp 가중치")] public float HpWeight;
    [Header("Hp 랜덤 범위")]
    public float MinHp;
    public float MaxHp;
}