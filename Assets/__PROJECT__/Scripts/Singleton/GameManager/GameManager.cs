using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Inst;
    public int prevWave = 1; // 이전 웨이브
    public int wave = 1; // 현재 웨이브

    public int killCount = 0; // 현재 킬 카운트
    public int destKillCount = 0; // 목표 킬 카운트

    void Awake()
    {
        if(Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
            return;
        }    
        Inst = this;
    }

    void Update()
    {
        // 킬 카운트가 목표 킬 카운트에 도달할 시 웨이브 증가
        // 다음 목표 킬 카운트는 ZombieSpawner에서 전달한다.
        if(killCount == destKillCount)
        {
            killCount = 0;
            wave++;
        }
    }

    void LateUpdate()
    {
        prevWave = wave; // 웨이브 수치 갱신
    }

    /// <summary>
    /// 현재 웨이브가 증가했는지 확인한다. LateUpdate()에서 확인하지 않도록 한다.
    /// </summary>
    /// <returns></returns>
    public bool WasWaveIncreased()
    {
        return prevWave < wave;
    }
    
    /// <summary>
    /// 목표 킬 카운드틑 입력한다.
    /// ZombieSpawner에서 호출
    /// </summary>
    /// <param name="val"></param>
    public void InputDestKillCount(int val)
    {
        destKillCount = val;
    }

    /// <summary>
    /// 현재 킬 카운트를 1 증가시킨다.
    /// </summary>
    public void IncreaseKillCount()
    {
        killCount++;
    }
}
