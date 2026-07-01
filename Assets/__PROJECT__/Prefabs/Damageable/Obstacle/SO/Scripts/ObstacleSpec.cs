using UnityEngine;

/// <summary>
/// 장애물의 기본 체력과 레벨별 체력 증가 값을 정의한다.
/// </summary>
[CreateAssetMenu(fileName = "ObstacleSpec", menuName = "Scriptable Objects/ObstacleSpec")]
public class ObstacleSpec : ScriptableObject
{
    [Header("레벨 기준값 - 기본 생성 또는 기존 호환 경로에서 사용할 시작 레벨")]
    public int level;

    [Header("레벨 체력 증가 - 레벨 1당 기본 체력 대비 증가 비율 (%)")]
    public float levelHpPercentPerLevel;
    
    [Header("기본 체력 - 레벨 보정 전 장애물 최대 체력")]
    public float Hp;
}
