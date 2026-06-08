using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Inst;

    public event Action<int> OnWaveIncrease; // 웨이브 증가 이벤트
    private readonly List<Obstacle> obstacles = new List<Obstacle>(32);

    public int Wave{ get; private set; } = 1;
    public int KillCount{ get; private set; }= 0; // 현재 킬 카운트
    public int DestKillCount{ get; private set; } = 0; // 목표 킬 카운트

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

            float sqrDistance = (current.transform.position - requesterPosition).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                obstacle = current;
            }
        }

        return obstacle != null && obstacle.TryReserveRepair(survivor);
    }
}
