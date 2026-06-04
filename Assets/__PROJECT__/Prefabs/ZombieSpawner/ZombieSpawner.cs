using System.Collections;
using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    [Header("스폰 데이터")] public ZombieSpawnData spawnData;
    [Header("스폰될 위치 목록")]  public Transform[] spwanPoints;
    [Header("스폰된 좀비에게 지정할 위치 목록")] public Transform[] destinations;
    [Header("일반 좀비 프리펩 목록")] public PoolObject[] normalZombiePrefabs;
    [Header("보스 좀비 프리펩 목록")] public PoolObject[] bossZombiePrefabs;

    private float currTime; // 현재 누적 시간
    private float currSpawnInterval; // 현재 스폰 간격
    private int currSpawnCount; // 현재 누적 스폰 횟수
    private int currMaxSpawnCount; // 현재 최대 스폰 횟수
    private bool spawnEnabled = true; // 스폰 활성화 상태

    void Start()
    {
        currSpawnInterval = spawnData.DefaultSpawnInterval;
        currMaxSpawnCount = spawnData.DefaultSpawnCount;
        GameManager.Inst.InputDestKillCount(currMaxSpawnCount); // 게임 매니저로 목표 킬 수 전달
    }

    // 웨이브가 증가할 때 5초간 스폰하지 않는다.
    IEnumerator WaveWaitCoroutine() 
    {
        currTime = 0f;
        spawnEnabled = false;
        yield return new WaitForSeconds(5f);
        spawnEnabled = true;
    }
    
    void Update()
    {
        // 웨이브 증가 시 스폰 간격과 최대 스폰 횟수를 갱신한다.
        if(GameManager.Inst.WasWaveIncreased())
        {
            currSpawnInterval = spawnData.DefaultSpawnInterval / Mathf.Pow(1f + spawnData.SpawnIntervalWeight, GameManager.Inst.Wave - 1f);

            // 최대 스폰 횟수 증가는 지수가 아닌 선형 계산식 사용
            currMaxSpawnCount = spawnData.DefaultSpawnCount + GameManager.Inst.Wave * spawnData.SpawnCountWeight;

            // 게임 매니저로 목표 킬 카운트 전달
            GameManager.Inst.InputDestKillCount(currMaxSpawnCount);

            // 웨이브 대기 코루틴 시작 // 웨이브가 증가한 순간부터 5초간 스폰하지 않음
            StartCoroutine(WaveWaitCoroutine());
        }

        // currSpawnInterval 간격으로 일반 좀비들을 스폰한다.
        // 최대 스폰 횟수 - 현재 스폰 횟수 == 1일 경우 보스 좀비를 스폰한다.
        // 최대 스폰 횟수를 넘기면 좀비는 더 이상 스폰되지 않는다.
        if(spawnEnabled)
        {
            currTime += Time.deltaTime;
            if (currSpawnCount < currMaxSpawnCount && currTime >= currSpawnInterval)
            {
                // 마지막 좀비로 보스 좀비를 스폰한다.
                if (currMaxSpawnCount - currSpawnCount == 1)
                {
                    print("보스 좀비 스폰됨");
                    // 보스 좀비 스폰 로직 추가 필요
                }
                // 아니라면 일반 좀비 스폰
                else
                {
                    // 일반 좀비, 스폰 위치, 목적지를 랜덤으로 선택한다
                    var zombie = MemoryPool.Inst.GetInstance<NormalZombie>(normalZombiePrefabs[Random.Range(0, normalZombiePrefabs.Length)]);
                    zombie.SetPosition(spwanPoints[Random.Range(0, spwanPoints.Length)]);
                    zombie.SetDestination(destinations[Random.Range(0, destinations.Length)]);
                    print("일반 좀비 스폰됨");
                }

                currTime -= currSpawnInterval;
                currSpawnCount++;
            }
        }
    }
}
