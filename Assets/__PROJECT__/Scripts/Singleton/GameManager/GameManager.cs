using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Inst;

    [HideInInspector] public int prevWave = 1; // 이전 웨이브
    public int Wave{ get; private set; } = 1;
    public int KillCount{ get; private set; }= 0; // 현재 킬 카운트
    public int DestKillCount{ get; private set; } = 0; // 목표 킬 카운트

    void Awake()
    {
        if(Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
            return;
        }    
        Inst = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // 킬 카운트가 목표 킬 카운트에 도달할 시 웨이브 증가
        // 다음 목표 킬 카운트는 ZombieSpawner에서 전달한다.
        if(KillCount == DestKillCount)
        {
            KillCount = 0;
            Wave++;
        }
    }

    void LateUpdate()
    {
        prevWave = Wave; // 웨이브 수치 갱신
    }

    /// <summary>
    /// 현재 웨이브가 증가했는지 확인한다. LateUpdate()에서 확인하지 않도록 한다.
    /// </summary>
    /// <returns></returns>
    public bool WasWaveIncreased()
    {
        return prevWave < Wave;
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
}
