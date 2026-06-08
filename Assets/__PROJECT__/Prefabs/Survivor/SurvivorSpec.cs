using UnityEngine;

[CreateAssetMenu(fileName = "SurvivorSpec", menuName = "Scriptable Objects/SurvivorSpec")]
public class SurvivorSpec : ScriptableObject
{
    [Header("레벨")]
    public int level = 1;

    [Header("이동 속도")]
    public float moveSpeed = 3.5f;
    public float moveSpeedLevelWeight = 0.05f;

    [Header("초당 수리량")]
    public float repairHpPerSecond = 5f;
    public float repairHpPerSecondLevelWeight = 0.5f;

    [Header("수리 행동")]
    public float repairRange = 1.5f;
    public float targetSearchInterval = 0.5f;
    public float rotationSpeed = 8f;

    // 레벨이 반영된 이동 속도를 계산한다
    public float GetMoveSpeed()
    {
        return moveSpeed + Mathf.Max(0, level - 1) * moveSpeedLevelWeight;
    }

    // 레벨이 반영된 초당 수리량을 계산한다
    public float GetRepairHpPerSecond()
    {
        return repairHpPerSecond + Mathf.Max(0, level - 1) * repairHpPerSecondLevelWeight;
    }
}
