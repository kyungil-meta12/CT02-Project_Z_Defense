using UnityEngine;

[CreateAssetMenu(fileName = "SurvivorSpec", menuName = "Scriptable Objects/SurvivorSpec")]
public class SurvivorSpec : ScriptableObject
{
    [Header("레벨")]
    public int level = 1;

    [Header("이동 속도")]
    public float moveSpeed = 3.5f;
    public float moveSpeedLevelWeight = 0.05f;

    [Header("초당 최대 체력 비례 수리량")]
    public float repairMaxHpPercentPerSecond = 10f;
    public float repairMaxHpPercentPerSecondLevelWeight = 0.05f;

    [Header("수리 행동")]
    public float repairRange = 2f;
    public float targetSearchInterval = 0.5f;
    public float rotationSpeed = 8f;

    // 레벨이 반영된 이동 속도를 계산한다
    public float GetMoveSpeed()
    {
        return moveSpeed + Mathf.Max(0, level - 1) * moveSpeedLevelWeight;
    }

    // 레벨이 반영된 초당 최대 체력 비례 수리율을 계산한다
    public float GetRepairMaxHpRatioPerSecond()
    {
        float percentPerSecond = repairMaxHpPercentPerSecond + Mathf.Max(0, level - 1) * repairMaxHpPercentPerSecondLevelWeight;
        return Mathf.Max(0.0f, percentPerSecond) * 0.01f;
    }
}
