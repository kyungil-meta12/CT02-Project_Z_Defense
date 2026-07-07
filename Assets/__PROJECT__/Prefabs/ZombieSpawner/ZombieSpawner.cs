using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 웨이브별 좀비 스폰, 스폰 정지, 활성 좀비 디스폰, 웨이브 재시작 준비를 관리한다.
/// </summary>
public class ZombieSpawner : MonoBehaviour
{
    [Header("신규 웨이브 스폰 프로필")] public ZombieWaveSpawnProfileSO waveSpawnProfile;
    [Header("스폰될 위치 목록")]  public Transform[] spwanPoints;
    [Header("스폰된 좀비에게 지정할 위치 목록")] public Transform[] destinations;
    [Header("일반 좀비 프리펩 목록")] public PoolObject[] normalZombiePrefabs;
    [Header("보스 좀비 프리펩 목록")] public PoolObject[] bossZombiePrefabs;

    private float currTime; // 현재 누적 시간
    private float currSpawnInterval; // 현재 스폰 간격
    private int currSpawnCount; // 현재 누적 스폰 횟수
    private int currMaxSpawnCount; // 현재 최대 스폰 횟수
    private bool spawnEnabled = true; // 스폰 활성화 상태
    private bool currentSpawnBossAsFirstEnemy = true; // 현재 웨이브 첫 스폰 보스 여부
    private ZombieSpawnRuntimeModifiers currentRuntimeModifiers = ZombieSpawnRuntimeModifiers.Default; // 현재 웨이브 좀비 배율
    private Coroutine waveWaitCoroutine;
    private readonly List<PoolObject> spawnedZombies = new List<PoolObject>(64);

    // 시작 시 현재 웨이브의 스폰 설정을 초기화하고 웨이브 변경 이벤트를 구독한다
    void Start()
    {
        int wave = GameManager.Inst == null ? 1 : GameManager.Inst.Wave;
        ApplyWaveSpawnSettings(wave);

        if (GameManager.Inst != null)
        {
            GameManager.Inst.RegisterZombieSpawner(this);
            GameManager.Inst.InputDestKillCount(currMaxSpawnCount);
            GameManager.Inst.OnWaveIncrease += OnWaveIncrease;
        }
    }

    // 파괴 시 웨이브 변경 이벤트 구독을 해제한다
    void OnDestroy()
    {
        if(GameManager.Inst)
        {
            GameManager.Inst.OnWaveIncrease -= OnWaveIncrease;
            GameManager.Inst.UnregisterZombieSpawner(this);
        }
    }

    // 웨이브 증가 시 스폰 설정을 갱신하고 대기 시간을 시작한다
    void OnWaveIncrease(int wave)
    {
        ApplyWaveSpawnSettings(wave);
        GameManager.Inst.InputDestKillCount(currMaxSpawnCount);
        currSpawnCount = 0;
        waveWaitCoroutine = StartCoroutine(WaveWaitCoroutine());
    }

    // 웨이브가 증가할 때 5초간 스폰하지 않는다.
    IEnumerator WaveWaitCoroutine() 
    {
        currTime = 0f;
        spawnEnabled = false;
        yield return new WaitForSeconds(5f);
        spawnEnabled = true;
        waveWaitCoroutine = null;
    }

    // 게임오버 또는 일시 정지 중 추가 스폰을 멈춘다
    public void PauseSpawning()
    {
        spawnEnabled = false;
        StopWaveWaitCoroutine();
    }

    // 준비된 웨이브의 스폰을 다시 시작한다
    public void ResumeSpawning()
    {
        currTime = 0f;
        spawnEnabled = true;
    }

    // 지정 웨이브를 처음부터 다시 스폰할 수 있도록 준비하고 목표 스폰 수를 반환한다
    public int PrepareWaveForRestart(int wave)
    {
        StopWaveWaitCoroutine();
        ApplyWaveSpawnSettings(wave);
        currSpawnCount = 0;
        currTime = 0f;
        spawnEnabled = false;
        return currMaxSpawnCount;
    }

    // 이 스포너가 생성한 활성 좀비를 모두 풀로 반환한다
    public void DespawnAllSpawnedZombies()
    {
        for (int i = spawnedZombies.Count - 1; i >= 0; i--)
        {
            PoolObject zombie = spawnedZombies[i];
            spawnedZombies.RemoveAt(i);

            if (zombie == null || !zombie.gameObject.activeInHierarchy)
            {
                continue;
            }

            zombie.ReturnToPool();
        }
    }

    // 현재 웨이브에 맞는 스폰 프로필 설정을 적용한다
    private void ApplyWaveSpawnSettings(int wave)
    {
        int safeWave = Mathf.Max(1, wave);

        if (waveSpawnProfile == null)
        {
            Debug.LogWarning("[ZombieSpawner] 웨이브 스폰 프로필이 없어 좀비 스폰을 비활성화합니다.", this);
            currSpawnInterval = 1.0f;
            currMaxSpawnCount = 1;
            spawnEnabled = false;
            currentSpawnBossAsFirstEnemy = false;
            currentRuntimeModifiers = ZombieSpawnRuntimeModifiers.Default;
            return;
        }

        spawnEnabled = true;
        currSpawnInterval = waveSpawnProfile.GetSpawnInterval(safeWave, 1.0f);
        currMaxSpawnCount = waveSpawnProfile.GetSpawnCount(safeWave, 0);
        currentSpawnBossAsFirstEnemy = waveSpawnProfile.ShouldSpawnBossAsLastEnemy(safeWave, false);
        currentRuntimeModifiers = waveSpawnProfile.GetRuntimeModifiers(safeWave).Sanitized();
    }

    // 매 프레임 스폰 타이머를 갱신하고 필요 시 다음 좀비를 스폰한다
    void Update()
    {
        if(spawnEnabled)
        {
            currTime += Time.deltaTime;
            if (currSpawnCount < currMaxSpawnCount && currTime >= currSpawnInterval)
            {
                SpawnNextZombie();
                currTime -= currSpawnInterval;
                currSpawnCount++;
            }
        }
    }

    // 현재 스폰 순서에 맞춰 일반 좀비 또는 보스를 스폰한다
    private void SpawnNextZombie()
    {
        bool shouldSpawnBoss = currentSpawnBossAsFirstEnemy && currSpawnCount == 0;
        if (shouldSpawnBoss && TryGetBossZombiePrefab(out PoolObject bossPrefab))
        {
            SpawnBossZombie(bossPrefab);
            return;
        }

        if (TryGetNormalZombiePrefab(out PoolObject normalPrefab))
        {
            SpawnNormalZombie(normalPrefab);
        }
    }

    // 현재 웨이브에 맞는 일반 좀비 프리팹을 반환한다
    private bool TryGetNormalZombiePrefab(out PoolObject prefab)
    {
        int wave = GameManager.Inst == null ? 1 : GameManager.Inst.Wave;
        if (waveSpawnProfile != null && waveSpawnProfile.TryGetNormalZombiePrefab(wave, out prefab))
        {
            return true;
        }

        return TryGetRandomPrefab(normalZombiePrefabs, out prefab);
    }

    // 현재 웨이브에 맞는 보스 좀비 프리팹을 반환한다
    private bool TryGetBossZombiePrefab(out PoolObject prefab)
    {
        int wave = GameManager.Inst == null ? 1 : GameManager.Inst.Wave;
        if (waveSpawnProfile != null && waveSpawnProfile.TryGetBossZombiePrefab(wave, out prefab))
        {
            return true;
        }

        return TryGetRandomPrefab(bossZombiePrefabs, out prefab);
    }

    // 배열에서 null이 아닌 프리팹 하나를 균등 랜덤으로 반환한다
    private static bool TryGetRandomPrefab(PoolObject[] prefabs, out PoolObject prefab)
    {
        prefab = null;
        if (prefabs == null || prefabs.Length == 0)
        {
            return false;
        }

        int validCount = 0;
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
            {
                validCount++;
            }
        }

        if (validCount <= 0)
        {
            return false;
        }

        int selectedIndex = Random.Range(0, validCount);
        int currentIndex = 0;
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null)
            {
                continue;
            }

            if (currentIndex == selectedIndex)
            {
                prefab = prefabs[i];
                return true;
            }

            currentIndex++;
        }

        return false;
    }

    // 지정한 프리팹으로 일반 좀비를 스폰하고 웨이브 배율을 적용한다
    private void SpawnNormalZombie(PoolObject prefab)
    {
        if (MemoryPool.Inst == null)
        {
            Debug.LogWarning("[ZombieSpawner] MemoryPool이 없어 일반 좀비를 스폰할 수 없습니다.", this);
            return;
        }

        Transform spawnPoint = GetRandomSpawnPoint();
        Transform destination = GetRandomDestination();
        if (spawnPoint == null || destination == null)
        {
            Debug.LogWarning("[ZombieSpawner] 스폰 위치 또는 목적지가 없어 일반 좀비 위치를 설정할 수 없습니다.", this);
            return;
        }

        Vector3 spawnPosition = ResolveSpawnPosition(spawnPoint.position);
        NormalZombie zombie = MemoryPool.Inst.GetInstance<NormalZombie>(prefab, spawnPosition, spawnPoint.rotation);
        if (zombie == null)
        {
            return;
        }

        zombie.ApplySpawnRuntimeModifiers(currentRuntimeModifiers);
        zombie.SetPosition(spawnPosition);
        zombie.SetDestination(destination);
        RegisterSpawnedZombie(zombie);
    }

    // 지정한 프리팹으로 보스 좀비를 스폰하고 웨이브 배율을 적용한다
    private void SpawnBossZombie(PoolObject prefab)
    {
        if (MemoryPool.Inst == null)
        {
            Debug.LogWarning("[ZombieSpawner] MemoryPool이 없어 보스 좀비를 스폰할 수 없습니다.", this);
            return;
        }

        Transform spawnPoint = GetRandomSpawnPoint();
        Transform destination = GetRandomDestination();
        if (spawnPoint == null || destination == null)
        {
            Debug.LogWarning("[ZombieSpawner] 스폰 위치 또는 목적지가 없어 보스 좀비 위치를 설정할 수 없습니다.", this);
            return;
        }

        Vector3 spawnPosition = ResolveSpawnPosition(spawnPoint.position);
        BossZombie bossZombie = MemoryPool.Inst.GetInstance<BossZombie>(prefab, spawnPosition, spawnPoint.rotation);
        if (bossZombie == null)
        {
            return;
        }

        bossZombie.ApplySpawnRuntimeModifiers(currentRuntimeModifiers);
        bossZombie.SetPosition(spawnPosition);
        bossZombie.SetDestination(destination);
        RegisterSpawnedZombie(bossZombie);
    }

    // 스폰 기준 위치에서 가장 가까운 NavMesh 위치를 반환한다
    private static Vector3 ResolveSpawnPosition(Vector3 requestedPosition)
    {
        if (NavMesh.SamplePosition(requestedPosition, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return requestedPosition;
    }

    // 웨이브 대기 코루틴이 실행 중이면 중단한다
    private void StopWaveWaitCoroutine()
    {
        if (waveWaitCoroutine == null)
        {
            return;
        }

        StopCoroutine(waveWaitCoroutine);
        waveWaitCoroutine = null;
    }

    // 스폰된 좀비를 디스폰 추적 목록에 등록한다
    private void RegisterSpawnedZombie(PoolObject zombie)
    {
        if (zombie == null || spawnedZombies.Contains(zombie))
        {
            return;
        }

        spawnedZombies.Add(zombie);
    }

    // 등록된 스폰 위치 중 하나를 반환한다
    private Transform GetRandomSpawnPoint()
    {
        return GetRandomTransform(spwanPoints);
    }

    // 등록된 목적지 중 하나를 반환한다
    private Transform GetRandomDestination()
    {
        return GetRandomTransform(destinations);
    }

    // Transform 배열에서 null이 아닌 항목 하나를 균등 랜덤으로 반환한다
    private static Transform GetRandomTransform(Transform[] transforms)
    {
        if (transforms == null || transforms.Length == 0)
        {
            return null;
        }

        int selectedIndex = Random.Range(0, transforms.Length);
        for (int i = 0; i < transforms.Length; i++)
        {
            int index = (selectedIndex + i) % transforms.Length;
            if (transforms[index] != null)
            {
                return transforms[index];
            }
        }

        return null;
    }
}
