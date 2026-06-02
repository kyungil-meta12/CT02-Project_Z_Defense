using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Inst;
    public int prevWave = 1; // 이전 웨이브
    public int wave = 1; // 현재 웨이브

    void Awake()
    {
        if(Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
            return;
        }    
        Inst = this;
    }

    void LateUpdate()
    {
        prevWave = wave; // 웨이브 수치 갱신
    }

    /// <summary>
    ///  현재 웨이브를 1 증가시킨다.
    /// </summary>
    public void IncreaseWave()
    {
        wave++;
    }

    /// <summary>
    /// 현재 웨이브가 증가했는지 확인한다. LateUpdate()에서 확인하지 않도록 한다.
    /// </summary>
    /// <returns></returns>
    public bool WasWaveIncreased()
    {
        return prevWave < wave;
    }
}
