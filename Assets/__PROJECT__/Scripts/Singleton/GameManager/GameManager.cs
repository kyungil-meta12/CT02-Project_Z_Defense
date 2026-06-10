using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private const int DEFAULT_DEFENSE_LINE_COUNT = 3;
    private const int FIRST_DEFENSE_LINE_SLOT_COUNT = 3;
    private const int SECOND_DEFENSE_LINE_SLOT_COUNT = 3;
    private const int THIRD_DEFENSE_LINE_SLOT_COUNT = 1;

    [Serializable]
    private class DefenseLineEntry
    {
        [Header("방어선 설정")]
        public string lineName;
        public List<ObstacleBuildSlot> obstacleSlots = new List<ObstacleBuildSlot>(3);
        [Tooltip("도망 포인트")]
        public Transform retreatPoint;
        [Tooltip("복귀 포인트")]
        public Transform restoredPoint;

        [NonSerialized] public bool isBreached;
    }

    public static GameManager Inst;

    [Header("방어선")]
    [SerializeField] private List<DefenseLineEntry> defenseLines = new List<DefenseLineEntry>(DEFAULT_DEFENSE_LINE_COUNT);

    public event Action<int> OnWaveIncrease; // 웨이브 증가 이벤트
    private readonly List<Survivor> survivors = new List<Survivor>(16);

    public int Wave{ get; private set; } = 1;
    public int KillCount{ get; private set; }= 0; // 현재 킬 카운트
    public int DestKillCount{ get; private set; } = 0; // 목표 킬 카운트

    private void OnValidate()
    {
        EnsureDefaultDefenseLineEntries();
    }

    private void Awake()
    {
        if(Inst && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;
        DontDestroyOnLoad(gameObject);
        EnsureDefaultDefenseLineEntries();
    }

    private void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }
    }

    private void Update()
    {
        // 킬 카운트가 목표 킬 카운트에 도달할 시 웨이브를 증가시킨다
        if(KillCount == DestKillCount)
        {
            KillCount = 0;
            Wave++;
            OnWaveIncrease?.Invoke(Wave);
        }
    }

    /// <summary>
    /// 목표 킬 카운트를 입력한다.
    /// ZombieSpawner에서 호출
    /// </summary>
    /// <param name="val"></param>
    public void InputDestKillCount(int val)
    {
        DestKillCount = val;
    }

    /// <summary>
    /// 현재 킬 카운트를 1 증가시킨다.
    /// </summary>
    public void IncreaseKillCount()
    {
        KillCount++;
    }

    // 생존자를 방어선 이벤트 수신 대상으로 등록한다
    public void RegisterSurvivor(Survivor survivor)
    {
        if (survivor == null || survivors.Contains(survivor))
        {
            return;
        }

        survivors.Add(survivor);
    }

    // 생존자를 방어선 이벤트 수신 대상에서 해제한다
    public void UnregisterSurvivor(Survivor survivor)
    {
        if (survivor == null)
        {
            return;
        }

        survivors.Remove(survivor);
    }

    // 장애물이 속한 설치 슬롯을 슬롯 기준 방어선 목록과 동기화한다
    public void RegisterObstacle(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        ObstacleBuildSlot slot = obstacle.GetComponentInParent<ObstacleBuildSlot>();
        if (slot == null)
        {
            return;
        }

        slot.SetCurrentObstacle(obstacle);
        RegisterDefenseLineSlot(slot);
    }

    // 장애물이 제거될 때 설치 슬롯 점유 상태를 해제한다
    public void UnregisterObstacle(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        ObstacleBuildSlot slot = FindDefenseLineSlot(obstacle);
        if (slot == null)
        {
            slot = obstacle.GetComponentInParent<ObstacleBuildSlot>();
        }

        if (slot != null)
        {
            slot.ClearCurrentObstacle(obstacle);
        }
    }

    // 방어선 설치 슬롯을 등록한다
    public void RegisterDefenseLineSlot(ObstacleBuildSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        EnsureDefaultDefenseLineEntries();

        int defenseLineIndex = slot.DefenseLineIndex;
        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            Debug.LogWarning("[GameManager] 방어선 슬롯 인덱스가 유효하지 않아 등록하지 않았습니다.", slot);
            return;
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        if (defenseLine == null)
        {
            Debug.LogWarning("[GameManager] 방어선 설정이 비어 있어 슬롯을 등록하지 않았습니다.", this);
            return;
        }

        if (defenseLine.obstacleSlots == null)
        {
            defenseLine.obstacleSlots = new List<ObstacleBuildSlot>(GetExpectedDefenseLineSlotCount(defenseLineIndex));
        }

        if (!defenseLine.obstacleSlots.Contains(slot))
        {
            defenseLine.obstacleSlots.Add(slot);
            defenseLine.obstacleSlots.Sort(CompareObstacleBuildSlotIndex);
        }

        slot.RefreshCurrentObstacleReference();
    }

    // 방어선 설치 슬롯 등록을 해제한다
    public void UnregisterDefenseLineSlot(ObstacleBuildSlot slot)
    {
        if (slot == null || defenseLines == null)
        {
            return;
        }

        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || defenseLine.obstacleSlots == null)
            {
                continue;
            }

            defenseLine.obstacleSlots.Remove(slot);
        }
    }

    // 슬롯에 장애물이 설치되었을 때 방어선 복구 가능 여부를 확인한다
    public void NotifyObstaclePlaced(ObstacleBuildSlot slot, Obstacle obstacle)
    {
        if (slot == null || obstacle == null)
        {
            return;
        }

        RegisterDefenseLineSlot(slot);
        slot.SetCurrentObstacle(obstacle);

        int defenseLineIndex = slot.DefenseLineIndex;
        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            return;
        }

        if (defenseLines[defenseLineIndex].isBreached && IsDefenseLineFullyBuilt(defenseLineIndex))
        {
            NotifyDefenseLineRestored(defenseLineIndex);
        }
    }

    // 파괴된 장애물이 속한 방어선을 붕괴 상태로 전환한다
    public void NotifyObstacleFractured(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        ObstacleBuildSlot slot = FindDefenseLineSlot(obstacle);
        int defenseLineIndex = slot != null ? slot.DefenseLineIndex : FindDefenseLineIndex(obstacle);
        if (defenseLineIndex < 0)
        {
            return;
        }

        if (slot != null)
        {
            slot.ClearCurrentObstacle(obstacle);
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        if (defenseLine.isBreached)
        {
            return;
        }

        defenseLine.isBreached = true;
        CommandSurvivorsToRetreat(defenseLineIndex, defenseLine.retreatPoint);
    }

    // 외부 재건축 시스템이 방어선 복구 완료 시 생존자 복귀를 요청한다
    public void NotifyDefenseLineRestored(int defenseLineIndex)
    {
        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            Debug.LogWarning("[GameManager] 복구 처리할 방어선 인덱스가 유효하지 않습니다.", this);
            return;
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        if (defenseLine == null)
        {
            Debug.LogWarning("[GameManager] 복구 처리할 방어선 설정이 비어 있습니다.", this);
            return;
        }

        if (!defenseLine.isBreached)
        {
            return;
        }

        if (!IsDefenseLineFullyBuilt(defenseLineIndex))
        {
            return;
        }

        defenseLine.isBreached = false;
        CommandSurvivorsToRestoredPoint(defenseLineIndex, defenseLine.restoredPoint);
    }

    // 수리가 필요한 가장 가까운 슬롯 점유 장애물을 예약한다
    public bool TryGetRepairTarget(Vector3 requesterPosition, Survivor survivor, out Obstacle obstacle)
    {
        obstacle = null;
        if (survivor == null || defenseLines == null)
        {
            return false;
        }

        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || defenseLine.obstacleSlots == null)
            {
                continue;
            }

            for (int j = defenseLine.obstacleSlots.Count - 1; j >= 0; j--)
            {
                ObstacleBuildSlot slot = defenseLine.obstacleSlots[j];
                if (slot == null)
                {
                    defenseLine.obstacleSlots.RemoveAt(j);
                    continue;
                }

                Obstacle current = slot.CurrentObstacle;
                if (current == null || !current.CanBeRepairedBy(survivor))
                {
                    continue;
                }

                if (IsRepairBlockedByRetreatedLine(survivor, current))
                {
                    continue;
                }

                float sqrDistance = (current.transform.position - requesterPosition).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    obstacle = current;
                }
            }
        }

        return obstacle != null && obstacle.TryReserveRepair(survivor);
    }

    // 지정 방어선의 모든 필수 슬롯이 점유되었는지 확인한다
    public bool IsDefenseLineFullyBuilt(int defenseLineIndex)
    {
        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            return false;
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        if (defenseLine == null || defenseLine.obstacleSlots == null)
        {
            return false;
        }

        int expectedSlotCount = GetExpectedDefenseLineSlotCount(defenseLineIndex);
        int occupiedSlotCount = 0;

        for (int i = defenseLine.obstacleSlots.Count - 1; i >= 0; i--)
        {
            ObstacleBuildSlot slot = defenseLine.obstacleSlots[i];
            if (slot == null)
            {
                defenseLine.obstacleSlots.RemoveAt(i);
                continue;
            }

            if (slot.CurrentObstacle != null)
            {
                occupiedSlotCount++;
            }
        }

        return occupiedSlotCount >= expectedSlotCount;
    }

    // 모든 방어선 필수 슬롯이 점유되었는지 확인한다
    public bool AreAllDefenseLineSlotsBuilt()
    {
        for (int i = 0; i < DEFAULT_DEFENSE_LINE_COUNT; i++)
        {
            if (!IsDefenseLineFullyBuilt(i))
            {
                return false;
            }
        }

        return true;
    }

    // 지정 방어선의 빈 슬롯 수를 계산한다
    public int GetEmptyDefenseLineSlotCount(int defenseLineIndex)
    {
        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            return 0;
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        int expectedSlotCount = GetExpectedDefenseLineSlotCount(defenseLineIndex);
        if (defenseLine == null || defenseLine.obstacleSlots == null)
        {
            return expectedSlotCount;
        }

        int occupiedSlotCount = 0;
        for (int i = 0; i < defenseLine.obstacleSlots.Count; i++)
        {
            ObstacleBuildSlot slot = defenseLine.obstacleSlots[i];
            if (slot != null && slot.CurrentObstacle != null)
            {
                occupiedSlotCount++;
            }
        }

        return Mathf.Max(0, expectedSlotCount - occupiedSlotCount);
    }

    // 후퇴한 생존자가 이전 방어선을 수리 대상으로 잡지 않도록 확인한다
    private bool IsRepairBlockedByRetreatedLine(Survivor survivor, Obstacle obstacle)
    {
        if (survivor == null || obstacle == null || survivor.ActiveDefenseLineIndex < 0)
        {
            return false;
        }

        int obstacleDefenseLineIndex = FindDefenseLineIndex(obstacle);
        return obstacleDefenseLineIndex >= 0 && obstacleDefenseLineIndex <= survivor.ActiveDefenseLineIndex;
    }

    // 기본 방어선 항목이 부족하면 1차부터 3차까지 채운다
    private void EnsureDefaultDefenseLineEntries()
    {
        if (defenseLines == null)
        {
            defenseLines = new List<DefenseLineEntry>(DEFAULT_DEFENSE_LINE_COUNT);
        }

        while (defenseLines.Count < DEFAULT_DEFENSE_LINE_COUNT)
        {
            int lineNumber = defenseLines.Count + 1;
            defenseLines.Add(new DefenseLineEntry
            {
                lineName = $"{lineNumber}차 방어선"
            });
        }

        for (int i = 0; i < DEFAULT_DEFENSE_LINE_COUNT; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null)
            {
                defenseLine = new DefenseLineEntry();
                defenseLines[i] = defenseLine;
            }

            if (string.IsNullOrWhiteSpace(defenseLine.lineName))
            {
                defenseLine.lineName = $"{i + 1}차 방어선";
            }

            if (defenseLine.obstacleSlots == null)
            {
                defenseLine.obstacleSlots = new List<ObstacleBuildSlot>(GetExpectedDefenseLineSlotCount(i));
            }
        }
    }

    // 장애물이 속한 방어선 인덱스를 찾는다
    private int FindDefenseLineIndex(Obstacle obstacle)
    {
        ObstacleBuildSlot slot = FindDefenseLineSlot(obstacle);
        return slot == null ? -1 : slot.DefenseLineIndex;
    }

    // 장애물이 점유한 방어선 슬롯을 찾는다
    private ObstacleBuildSlot FindDefenseLineSlot(Obstacle obstacle)
    {
        if (obstacle == null || defenseLines == null)
        {
            return null;
        }

        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || defenseLine.obstacleSlots == null)
            {
                continue;
            }

            for (int j = defenseLine.obstacleSlots.Count - 1; j >= 0; j--)
            {
                ObstacleBuildSlot slot = defenseLine.obstacleSlots[j];
                if (slot == null)
                {
                    defenseLine.obstacleSlots.RemoveAt(j);
                    continue;
                }

                if (slot.CurrentObstacle == obstacle)
                {
                    return slot;
                }
            }
        }

        return null;
    }

    // 방어선별 필수 슬롯 수를 반환한다
    private int GetExpectedDefenseLineSlotCount(int defenseLineIndex)
    {
        switch (defenseLineIndex)
        {
            case 0:
                return FIRST_DEFENSE_LINE_SLOT_COUNT;
            case 1:
                return SECOND_DEFENSE_LINE_SLOT_COUNT;
            case 2:
                return THIRD_DEFENSE_LINE_SLOT_COUNT;
            default:
                return 0;
        }
    }

    private static int CompareObstacleBuildSlotIndex(ObstacleBuildSlot left, ObstacleBuildSlot right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        return left.SlotIndex.CompareTo(right.SlotIndex);
    }

    // 등록된 생존자에게 대피 명령을 전달한다
    private void CommandSurvivorsToRetreat(int defenseLineIndex, Transform retreatPoint)
    {
        if (retreatPoint == null)
        {
            Debug.LogWarning("[GameManager] 방어선 대피 포인트가 할당되지 않았습니다.", this);
            return;
        }

        for (int i = survivors.Count - 1; i >= 0; i--)
        {
            Survivor survivor = survivors[i];
            if (survivor == null)
            {
                survivors.RemoveAt(i);
                continue;
            }

            survivor.StartDefenseLineRetreat(defenseLineIndex, retreatPoint);
        }
    }

    // 등록된 생존자에게 방어선 복귀 명령을 전달한다
    private void CommandSurvivorsToRestoredPoint(int defenseLineIndex, Transform restoredPoint)
    {
        if (restoredPoint == null)
        {
            Debug.LogWarning("[GameManager] 방어선 복귀 포인트가 할당되지 않았습니다.", this);
            return;
        }

        for (int i = survivors.Count - 1; i >= 0; i--)
        {
            Survivor survivor = survivors[i];
            if (survivor == null)
            {
                survivors.RemoveAt(i);
                continue;
            }

            survivor.StartDefenseLineReturn(defenseLineIndex, restoredPoint);
        }
    }
}
