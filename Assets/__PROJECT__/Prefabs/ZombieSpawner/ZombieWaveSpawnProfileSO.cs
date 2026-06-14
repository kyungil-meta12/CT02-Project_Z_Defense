using System;
using UnityEngine;

/// <summary>
/// 웨이브 구간별 좀비 스폰 구성, 스탯 배율, 보상 배율을 정의한다.
/// </summary>
[CreateAssetMenu(fileName = "ZombieWaveSpawnProfile", menuName = "Project Z Defense/Zombie/Zombie Wave Spawn Profile")]
public class ZombieWaveSpawnProfileSO : ScriptableObject
{
    [SerializeField] private ZombieWaveSpawnStage[] stages;

    // 현재 웨이브에 해당하는 스테이지 설정을 반환한다
    public ZombieWaveSpawnStage GetStageForWave(int wave)
    {
        int safeWave = Mathf.Max(1, wave);
        if (stages == null)
        {
            return null;
        }

        ZombieWaveSpawnStage fallbackStage = null;
        for (int i = 0; i < stages.Length; i++)
        {
            ZombieWaveSpawnStage stage = stages[i];
            if (stage == null)
            {
                continue;
            }

            if (stage.IsWaveMatch(safeWave))
            {
                return stage;
            }

            if (stage.MinWave <= safeWave)
            {
                fallbackStage = stage;
            }
        }

        return fallbackStage;
    }

    // 현재 웨이브의 일반 좀비 프리팹을 가중치 기반으로 선택한다
    public bool TryGetNormalZombiePrefab(int wave, out PoolObject prefab)
    {
        ZombieWaveSpawnStage stage = GetStageForWave(wave);
        if (stage == null)
        {
            prefab = null;
            return false;
        }

        return stage.TryGetNormalZombiePrefab(wave, out prefab);
    }

    // 현재 웨이브의 보스 좀비 프리팹을 가중치 기반으로 선택한다
    public bool TryGetBossZombiePrefab(int wave, out PoolObject prefab)
    {
        ZombieWaveSpawnStage stage = GetStageForWave(wave);
        if (stage == null)
        {
            prefab = null;
            return false;
        }

        return stage.TryGetBossZombiePrefab(wave, out prefab);
    }

    // 현재 웨이브의 스폰 간격을 반환한다
    public float GetSpawnInterval(int wave, float fallbackSpawnInterval)
    {
        ZombieWaveSpawnStage stage = GetStageForWave(wave);
        if (stage == null)
        {
            return Mathf.Max(0.01f, fallbackSpawnInterval);
        }

        return Mathf.Max(0.01f, stage.SpawnInterval);
    }

    // 현재 웨이브의 목표 스폰 수를 반환한다
    public int GetSpawnCount(int wave, int fallbackSpawnCount)
    {
        ZombieWaveSpawnStage stage = GetStageForWave(wave);
        if (stage == null)
        {
            return Mathf.Max(0, fallbackSpawnCount);
        }

        return Mathf.Max(0, stage.SpawnCount);
    }

    // 현재 웨이브에서 마지막 스폰을 보스로 대체할지 반환한다
    public bool ShouldSpawnBossAsLastEnemy(int wave, bool fallbackValue)
    {
        ZombieWaveSpawnStage stage = GetStageForWave(wave);
        return stage == null ? fallbackValue : stage.SpawnBossAsLastEnemy;
    }

    // 현재 웨이브의 좀비 스폰 배율을 반환한다
    public ZombieSpawnRuntimeModifiers GetRuntimeModifiers(int wave)
    {
        ZombieWaveSpawnStage stage = GetStageForWave(wave);
        return stage == null ? ZombieSpawnRuntimeModifiers.Default : stage.GetRuntimeModifiers();
    }

    // 인스펙터 입력값을 유효한 범위로 보정한다
    private void OnValidate()
    {
        if (stages == null)
        {
            return;
        }

        for (int i = 0; i < stages.Length; i++)
        {
            if (stages[i] != null)
            {
                stages[i].Validate();
            }
        }
    }
}

/// <summary>
/// 특정 웨이브 구간에서 사용할 스폰 수치와 좀비 후보 목록을 정의한다.
/// </summary>
[Serializable]
public class ZombieWaveSpawnStage
{
    [Header("Wave Range")]
    [SerializeField, Min(1)] private int minWave = 1;
    [SerializeField, Min(0)] private int maxWave;

    [Header("Spawn")]
    [SerializeField, Min(0.01f)] private float spawnInterval = 1.0f;
    [SerializeField, Min(0)] private int spawnCount = 10;
    [SerializeField] private bool spawnBossAsLastEnemy = true;

    [Header("Normal Zombies")]
    [SerializeField] private ZombieSpawnPrefabEntry[] normalZombieEntries;

    [Header("Boss Zombies")]
    [SerializeField] private ZombieSpawnPrefabEntry[] bossZombieEntries;

    [Header("Runtime Multipliers")]
    [SerializeField, Min(0.0f)] private float hpMultiplier = 1.0f;
    [SerializeField, Min(0.0f)] private float attackDamageMultiplier = 1.0f;
    [SerializeField, Min(0.0f)] private float moveAttackSpeedMultiplier = 1.0f;
    [SerializeField, Min(0.0f)] private float rewardMultiplier = 1.0f;

    public int MinWave
    {
        get
        {
            return minWave;
        }
    }

    public float SpawnInterval
    {
        get
        {
            return spawnInterval;
        }
    }

    public int SpawnCount
    {
        get
        {
            return spawnCount;
        }
    }

    public bool SpawnBossAsLastEnemy
    {
        get
        {
            return spawnBossAsLastEnemy;
        }
    }

    // 지정한 웨이브가 이 스테이지 범위에 포함되는지 확인한다
    public bool IsWaveMatch(int wave)
    {
        int safeWave = Mathf.Max(1, wave);
        return safeWave >= minWave && (maxWave <= 0 || safeWave <= maxWave);
    }

    // 현재 웨이브에 맞는 일반 좀비 프리팹을 선택한다
    public bool TryGetNormalZombiePrefab(int wave, out PoolObject prefab)
    {
        return TrySelectPrefab(normalZombieEntries, wave, out prefab);
    }

    // 현재 웨이브에 맞는 보스 좀비 프리팹을 선택한다
    public bool TryGetBossZombiePrefab(int wave, out PoolObject prefab)
    {
        return TrySelectPrefab(bossZombieEntries, wave, out prefab);
    }

    // 이 스테이지의 런타임 배율을 반환한다
    public ZombieSpawnRuntimeModifiers GetRuntimeModifiers()
    {
        return new ZombieSpawnRuntimeModifiers(
            Mathf.Max(0.0f, hpMultiplier),
            Mathf.Max(0.0f, attackDamageMultiplier),
            Mathf.Max(0.0f, moveAttackSpeedMultiplier),
            Mathf.Max(0.0f, rewardMultiplier));
    }

    // 인스펙터 입력값을 유효한 범위로 보정한다
    public void Validate()
    {
        minWave = Mathf.Max(1, minWave);
        maxWave = Mathf.Max(0, maxWave);
        spawnInterval = Mathf.Max(0.01f, spawnInterval);
        spawnCount = Mathf.Max(0, spawnCount);
        hpMultiplier = Mathf.Max(0.0f, hpMultiplier);
        attackDamageMultiplier = Mathf.Max(0.0f, attackDamageMultiplier);
        moveAttackSpeedMultiplier = Mathf.Max(0.0f, moveAttackSpeedMultiplier);
        rewardMultiplier = Mathf.Max(0.0f, rewardMultiplier);
        ValidateEntries(normalZombieEntries);
        ValidateEntries(bossZombieEntries);
    }

    // 후보 목록에서 현재 웨이브에 맞는 프리팹을 가중치 기반으로 선택한다
    private static bool TrySelectPrefab(ZombieSpawnPrefabEntry[] entries, int wave, out PoolObject prefab)
    {
        prefab = null;
        if (entries == null || entries.Length == 0)
        {
            return false;
        }

        int totalWeight = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            ZombieSpawnPrefabEntry entry = entries[i];
            if (entry != null && entry.IsAvailable(wave))
            {
                totalWeight += entry.Weight;
            }
        }

        if (totalWeight <= 0)
        {
            return false;
        }

        int selectedWeight = UnityEngine.Random.Range(0, totalWeight);
        int accumulatedWeight = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            ZombieSpawnPrefabEntry entry = entries[i];
            if (entry == null || !entry.IsAvailable(wave))
            {
                continue;
            }

            accumulatedWeight += entry.Weight;
            if (selectedWeight < accumulatedWeight)
            {
                prefab = entry.Prefab;
                return prefab != null;
            }
        }

        return false;
    }

    // 후보 목록의 입력값을 보정한다
    private static void ValidateEntries(ZombieSpawnPrefabEntry[] entries)
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null)
            {
                entries[i].Validate();
            }
        }
    }
}

/// <summary>
/// 스폰 후보 프리팹의 가중치와 웨이브 사용 범위를 정의한다.
/// </summary>
[Serializable]
public class ZombieSpawnPrefabEntry
{
    [SerializeField] private PoolObject prefab;
    [SerializeField, Min(0)] private int weight = 1;
    [SerializeField, Min(1)] private int minWave = 1;
    [SerializeField, Min(0)] private int maxWave;

    public PoolObject Prefab
    {
        get
        {
            return prefab;
        }
    }

    public int Weight
    {
        get
        {
            return Mathf.Max(0, weight);
        }
    }

    // 지정한 웨이브에서 이 후보를 사용할 수 있는지 확인한다
    public bool IsAvailable(int wave)
    {
        int safeWave = Mathf.Max(1, wave);
        return prefab != null &&
               Weight > 0 &&
               safeWave >= minWave &&
               (maxWave <= 0 || safeWave <= maxWave);
    }

    // 인스펙터 입력값을 유효한 범위로 보정한다
    public void Validate()
    {
        weight = Mathf.Max(0, weight);
        minWave = Mathf.Max(1, minWave);
        maxWave = Mathf.Max(0, maxWave);
    }
}

/// <summary>
/// 웨이브 스폰 프로필이 좀비 인스턴스에 적용할 런타임 배율 값이다.
/// </summary>
[Serializable]
public struct ZombieSpawnRuntimeModifiers
{
    public static readonly ZombieSpawnRuntimeModifiers Default = new ZombieSpawnRuntimeModifiers(1.0f, 1.0f, 1.0f, 1.0f);

    public float hpMultiplier;
    public float attackDamageMultiplier;
    public float moveAttackSpeedMultiplier;
    public float rewardMultiplier;

    // 런타임 배율 값을 초기화한다
    public ZombieSpawnRuntimeModifiers(float hpMultiplier_, float attackDamageMultiplier_, float moveAttackSpeedMultiplier_, float rewardMultiplier_)
    {
        hpMultiplier = hpMultiplier_;
        attackDamageMultiplier = attackDamageMultiplier_;
        moveAttackSpeedMultiplier = moveAttackSpeedMultiplier_;
        rewardMultiplier = rewardMultiplier_;
    }

    // 0 이하 값으로 들어온 배율을 안전한 기본값으로 보정한다
    public ZombieSpawnRuntimeModifiers Sanitized()
    {
        return new ZombieSpawnRuntimeModifiers(
            hpMultiplier > 0.0f ? hpMultiplier : 1.0f,
            attackDamageMultiplier > 0.0f ? attackDamageMultiplier : 1.0f,
            moveAttackSpeedMultiplier > 0.0f ? moveAttackSpeedMultiplier : 1.0f,
            rewardMultiplier > 0.0f ? rewardMultiplier : 1.0f);
    }
}
