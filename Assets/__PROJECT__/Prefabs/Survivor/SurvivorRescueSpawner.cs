using UnityEngine;

/// <summary>
/// 웨이브 시작 시 확률에 따라 좀비 스폰 지점에서 구출 대상 생존자를 생성한다.
/// </summary>
public class SurvivorRescueSpawner : MonoBehaviour
{
    [Header("생존자 스폰")]
    [SerializeField] private Survivor survivorPrefab;
    [SerializeField, Range(0f, 1f)] private float spawnChancePerWave = 0.2f;
    [SerializeField] private bool spawnOnStartWave;

    [Header("이동 지점")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform finalRearPoint;
    [SerializeField] private Transform hospitalPoint;
    [SerializeField, Min(0f)] private float treatmentDuration = 8f;

    // 시작 시 웨이브 이벤트를 구독하고 필요하면 현재 웨이브 스폰을 시도한다
    private void Start()
    {
        if (GameManager.Inst != null)
        {
            GameManager.Inst.OnWaveIncrease += OnWaveIncrease;
        }

        if (spawnOnStartWave)
        {
            TrySpawnSurvivor();
        }
    }

    // 파괴 시 웨이브 이벤트 구독을 해제한다
    private void OnDestroy()
    {
        if (GameManager.Inst != null)
        {
            GameManager.Inst.OnWaveIncrease -= OnWaveIncrease;
        }
    }

    // 웨이브 증가 시 생존자 스폰을 확률적으로 시도한다
    private void OnWaveIncrease(int wave)
    {
        TrySpawnSurvivor();
    }

    // 스폰 확률과 참조 유효성을 확인한 뒤 생존자를 생성한다
    private void TrySpawnSurvivor()
    {
        if (survivorPrefab == null || finalRearPoint == null || Random.value > spawnChancePerWave)
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
