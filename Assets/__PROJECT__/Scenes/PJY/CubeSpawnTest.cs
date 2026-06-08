using UnityEngine;
using UnityEngine.Pool;

public class CubeSpawnTest : MonoBehaviour
{
    public PoolObject cube;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        MemoryPool.Inst.CreateInstance(cube);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
