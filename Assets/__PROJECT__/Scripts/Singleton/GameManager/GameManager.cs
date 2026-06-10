using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private const int DEFAULT_DEFENSE_LINE_COUNT = 3;

    [Serializable]
    private class DefenseLineEntry
    {
        [Header("방어선 설정")]
        public string lineName;
        public List<Obstacle> obstacles = new List<Obstacle>(8);
        [Tooltip("도망 포인트")]
        public Transform retreatPoint;
        [Tooltip("복귀 포인트")]
        public Transform restoredPoint;

        [NonSerialized] public bool isBreached;
    }

    public static GameManager Inst;

    [Header("방어선")]
    [SerializeField] private List<DefenseLineEntry> defenseLines = new List<DefenseLineEntry>(3);

    public event Action<int> OnWaveIncrease; // 웨이브 증가 이벤트
    private readonly List<Obstacle> obstacles = new List<Obstacle>(32);
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
        // 킬 카운트가 목표 킬 카운트에 도달할 시 웨이브 증가
        // 다음 목표 킬 카운트는 ZombieSpawner에서 전달한다.
        if(KillCount == DestKillCount)
        {
            KillCount = 0;
            Wave++;
            OnWaveIncrease?.Invoke(Wave);
        }
    }
    
    /// <summary>
    /// 목표 킬 카운드틑 입력한다.
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

    // 장애물 목록에 등록한다
    public void RegisterObstacle(Obstacle obstacle)
    {
        if (obstacle == null || obstacles.Contains(obstacle))
        {
            return;
        }

        obstacles.Add(obstacle);
    }

    // 장애물 목록에서 해제한다
    public void UnregisterObstacle(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        obstacles.Remove(obstacle);
    }

    // 생존자 목록에 등록한다
    public void RegisterSurvivor(Survivor survivor)
    {
        if (survivor == null || survivors.Contains(survivor))
        {
            return;
        }

        survivors.Add(survivor);
    }

    // 생존자 목록에서 해제한다
    public void UnregisterSurvivor(Survivor survivor)
    {
        if (survivor == null)
        {
            return;
        }

        survivors.Remove(survivor);
    }

    // 파괴된 장애물이 속한 방어선을 붕괴 상태로 전환한다
    public void NotifyObstacleFractured(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        int defenseLineIndex = FindDefenseLineIndex(obstacle);
        if (defenseLineIndex < 0)
        {
            return;
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

        defenseLine.isBreached = false;
        CommandSurvivorsToRestoredPoint(defenseLineIndex, defenseLine.restoredPoint);
    }

    // 수리가 필요한 가장 가까운 장애물을 예약한다
    public bool TryGetRepairTarget(Vector3 requesterPosition, Survivor survivor, out Obstacle obstacle)
    {
        obstacle = null;
        if (survivor == null || obstacles.Count == 0)
        {
            return false;
        }

        float closestSqrDistance = float.MaxValue;

        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            Obstacle current = obstacles[i];
            if (current == null)
            {
                obstacles.RemoveAt(i);
                continue;
            }

            if (!current.CanBeRepairedBy(survivor))
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

        return obstacle != null && obstacle.TryReserveRepair(survivor);
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
    }

    // 장애물이 속한 방어선 인덱스를 찾는다
    private int FindDefenseLineIndex(Obstacle obstacle)
    {
        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || defenseLine.obstacles == null)
            {
                continue;
            }

            for (int j = defenseLine.obstacles.Count - 1; j >= 0; j--)
            {
                Obstacle current = defenseLine.obstacles[j];
                if (current == null)
                {
                    defenseLine.obstacles.RemoveAt(j);
                    continue;
                }

                if (current == obstacle)
                {
                    return i;
                }
            }
        }

        return -1;
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
