using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 웨이브 시작 시 확률에 따라 좀비 스폰 지점에서 구출 대상 생존자를 생성한다.
/// </summary>
public class SurvivorRescueSpawner : MonoBehaviour
{
    [Header("생존자 스폰")]
    [SerializeField, InspectorName("구출 생존자 스폰 활성화")] private bool enableRescueSpawn = true;
    [SerializeField] private Survivor survivorPrefab;
    [SerializeField] private SurvivorRescueSpawnProfileSO spawnProfile;
    [SerializeField, Range(0f, 1f)] private float spawnChancePerWave = 0.2f;
    [SerializeField] private bool spawnOnStartWave;

    [Header("이동 지점")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform finalRearPoint;
    [SerializeField] private Transform hospitalPoint;
    [SerializeField, Min(0f)] private float treatmentDuration = 8f;

    [Header("불러오기 배치")]
    [SerializeField, Min(1)] private int restoreGridColumns = 4;
    [SerializeField, Min(0.1f)] private float restoreGridSpacing = 1.5f;
    [SerializeField, Min(0.1f)] private float restoreNavMeshSampleDistance = 2f;

    public bool IsRescueSpawnEnabled => enableRescueSpawn;

    // 구출 생존자 웨이브 스폰 활성 상태를 변경한다
    public void SetRescueSpawnEnabled(bool isEnabled)
    {
        enableRescueSpawn = isEnabled;
    }

    // 구출 생존자 웨이브 스폰을 활성화한다
    public void EnableRescueSpawn()
    {
        SetRescueSpawnEnabled(true);
    }

    // 구출 생존자 웨이브 스폰을 비활성화한다
    public void DisableRescueSpawn()
    {
        SetRescueSpawnEnabled(false);
    }

    // 시작 시 웨이브 이벤트를 구독하고 필요하면 현재 웨이브 스폰을 시도한다
    private void Start()
    {
        if (GameManager.Inst != null)
        {
            GameManager.Inst.OnFirstWaveReached += OnFirstWaveReached;
            RestoreSavedSurvivors();
        }

        int wave = GameManager.Inst == null ? 1 : GameManager.Inst.Wave;
        bool isFirstWaveReached = GameManager.Inst == null || GameManager.Inst.TryMarkWaveAsReached(wave);
        if (spawnOnStartWave && isFirstWaveReached)
        {
            TrySpawnSurvivor(wave);
        }
    }

    // 파괴 시 웨이브 이벤트 구독을 해제한다
    private void OnDestroy()
    {
        if (GameManager.Inst != null)
        {
            GameManager.Inst.OnFirstWaveReached -= OnFirstWaveReached;
        }
    }

    // 웨이브를 최초로 진행할 때만 생존자 스폰을 확률적으로 시도한다
    private void OnFirstWaveReached(int wave)
    {
        TrySpawnSurvivor(wave);
    }

    // 활성 상태와 스폰 확률 및 참조 유효성을 확인한 뒤 생존자를 생성한다
    private void TrySpawnSurvivor(int wave)
    {
        if (!enableRescueSpawn || survivorPrefab == null || finalRearPoint == null || !ShouldSpawnSurvivor(wave))
        {
            return;
        }

        Transform spawnPoint = GetRandomSpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogWarning("[SurvivorRescueSpawner] 생존자 스폰 위치가 없어 스폰을 건너뜁니다.", this);
            return;
        }

        Survivor survivor = Instantiate(survivorPrefab, spawnPoint.position, spawnPoint.rotation);
        if (survivor == null)
        {
            return;
        }

        survivor.ConfigureRescueFlow(hospitalPoint, finalRearPoint, treatmentDuration);
        survivor.SetPosition(spawnPoint);
        survivor.StartRescueRun(finalRearPoint);
    }

    // 저장된 생존자 명단을 집결지 주변 격자에 순서대로 복원한다
    private void RestoreSavedSurvivors()
    {
        int restoreCount = GameManager.Inst.GetPendingSurvivorRestoreCount();
        if (restoreCount <= 0)
        {
            return;
        }

        if (survivorPrefab == null || finalRearPoint == null)
        {
            Debug.LogWarning("[SurvivorRescueSpawner] 생존자 복원에 필요한 프리팹 또는 집결지가 없어 복원을 보류합니다.", this);
            return;
        }

        GameManager.Inst.BeginSurvivorRestore();
        int safeColumns = Mathf.Max(1, restoreGridColumns);
        float safeSpacing = Mathf.Max(0.1f, restoreGridSpacing);

        for (int i = 0; i < restoreCount; i++)
        {
            if (!GameManager.Inst.TryGetPendingSurvivorRestoreEntry(i, out SurvivorSaveEntry saveEntry))
            {
                continue;
            }

            Vector3 restorePosition = CalculateRestorePosition(i, safeColumns, safeSpacing);
            Survivor survivor = Instantiate(survivorPrefab, restorePosition, finalRearPoint.rotation);
            if (survivor == null)
            {
                continue;
            }

            survivor.RestoreSaveEntry(saveEntry, hospitalPoint, finalRearPoint, treatmentDuration);
        }

        GameManager.Inst.CompleteSurvivorRestore();
    }

    // 집결지 기준 격자 위치를 계산하고 가까운 NavMesh 위치로 보정한다
    private Vector3 CalculateRestorePosition(int index, int columns, float spacing)
    {
        int row = index / columns;
        int column = index % columns;
        float centeredColumn = column - (columns - 1) * 0.5f;
        Vector3 candidate = finalRearPoint.position + finalRearPoint.right * (centeredColumn * spacing) - finalRearPoint.forward * (row * spacing);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, Mathf.Max(0.1f, restoreNavMeshSampleDistance), NavMesh.AllAreas))
        {
            return hit.position;
        }

        Debug.LogWarning($"[SurvivorRescueSpawner] {index + 1}번째 생존자의 NavMesh 복원 위치를 찾지 못해 집결지 위치를 사용합니다.", this);
        return finalRearPoint.position;
    }

    // 현재 웨이브의 프로필 확률을 기준으로 스폰 여부를 결정한다
    private bool ShouldSpawnSurvivor(int wave)
    {
        float spawnChance;
        if (spawnProfile != null)
        {
            if (!spawnProfile.TryGetSpawnChance(wave, out spawnChance))
            {
                return false;
            }
        }
        else
        {
            spawnChance = spawnChancePerWave;
        }

        return Random.value <= Mathf.Clamp01(spawnChance);
    }

    // 등록된 스폰 위치 중 하나를 반환한다
    private Transform GetRandomSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        int selectedIndex = Random.Range(0, spawnPoints.Length);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            int index = (selectedIndex + i) % spawnPoints.Length;
            if (spawnPoints[index] != null)
            {
                return spawnPoints[index];
            }
        }

        return null;
    }
}
