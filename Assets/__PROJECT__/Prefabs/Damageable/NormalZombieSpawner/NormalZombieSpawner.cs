using UnityEngine;

public class NormalZombieSpawner : MonoBehaviour
{
    [Header("기본 스폰 간격(웨이브 1 기준)")] public float defaultSpawnInterval;
    [Header("웨이브 반영 간격 감소 수치")] public float waveIntervalMultiply;
    [Header("스폰될 위치 목록")]  public Transform[] spwanPoints;
    [Header("스폰된 좀비에게 지정할 위치 목록")] public Transform[] destinations;
    [Header("일반 좀비 프리펩 목록")] public PoolObject[] normalZombiePrefabs;

    private float currTime; // 현재 누적 시간
    private float currSpawnInterval; // 현재 스폰 간격

    void Start()
    {
        currSpawnInterval = defaultSpawnInterval;
    }
    
    void Update()
    {
        // 웨이브 변화가 감지되면 스폰 간격을 웨이브에 맞추어 갱신한다.
        if(GameManager.Inst.wave > 1 && GameManager.Inst.WasWaveIncreased())
        {
            currSpawnInterval = defaultSpawnInterval * GameManager.Inst.wave * waveIntervalMultiply; 
        }

        // currSpawnInterval 간격으로 일반 좀비들을 스폰한다.
        currTime += Time.deltaTime;
        if(currTime >= currSpawnInterval)
        {
            currTime -= currSpawnInterval;

            // 좀비, 스폰 위치, 목적지를 랜덤으로 선택한다
            var zombie = MemoryPool.Inst.GetInstance<NormalZombie>(normalZombiePrefabs[Random.Range(0, normalZombiePrefabs.Length)]);
            zombie.SetPosition(spwanPoints[Random.Range(0, spwanPoints.Length)]);
            zombie.SetDestination(destinations[Random.Range(0, destinations.Length)]);
        }
    }
}
