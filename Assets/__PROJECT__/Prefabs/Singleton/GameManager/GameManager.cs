using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Inst;
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

    /// <summary>
    ///  현재 웨이브를 1 증가시킨다.
    /// </summary>
    public void IncreaseWave()
    {
        wave++;
    }
}
