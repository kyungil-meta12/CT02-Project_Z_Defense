using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 웨이브 구간별 좀비 스폰 구성, 스탯 배율, 보상 배율을 정의한다.
/// </summary>
[CreateAssetMenu(fileName = "ZombieWaveSpawnProfile", menuName = "Project Z Defense/Zombie/Zombie Wave Spawn Profile")]
public class ZombieWaveSpawnProfileSO : ScriptableObject
{
    [Header("Prefab Map")]
    [SerializeField] private NormalZombiePrefabBinding[] normalZombiePrefabMap;
    [SerializeField] private BossZombiePrefabBinding[] bossZombiePrefabMap;

    [Header("보스 스폰 스케줄")]
    [SerializeField] private BossZombieSpawnSchedule[] bossSpawnSchedules;

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
        if (stage == null || !stage.TrySelectNormalZombieType(wave, out NormalZombieType type))
        {
            prefab = null;
            return false;
        }

        return TryGetNormalPrefabForType(type, out prefab);
    }

    // 현재 웨이브의 보스 좀비 프리팹을 가중치 기반으로 선택한다
    public bool TryGetBossZombiePrefab(int wave, out PoolObject prefab)
    {
        ZombieWaveSpawnStage stage = GetStageForWave(wave);
        if (stage == null || !stage.TrySelectBossZombieType(wave, out BossZombieType type))
        {
            prefab = null;
            return false;
        }

        return TryGetBossPrefabForType(type, out prefab);
    }

    // 보스 스폰 스케줄이 별도로 설정되어 있는지 확인한다
    public bool HasBossSpawnSchedules()
    {
        return bossSpawnSchedules != null && bossSpawnSchedules.Length > 0;
    }

    // 현재 웨이브에 출현할 보스 타입을 스케줄 순서대로 채운다
    public int FillScheduledBossZombieTypes(int wave, List<BossZombieType> results)
    {
        if (results == null)
        {
            return 0;
        }

        results.Clear();
        if (bossSpawnSchedules == null || bossSpawnSchedules.Length == 0)
        {
            return 0;
        }

        int safeWave = Mathf.Max(1, wave);
        for (int i = 0; i < bossSpawnSchedules.Length; i++)
        {
            BossZombieSpawnSchedule schedule = bossSpawnSchedules[i];
            if (schedule != null && schedule.IsScheduledForWave(safeWave))
            {
                results.Add(schedule.BossType);
            }
        }

        return results.Count;
    }

    // enum 타입으로 일반 좀비 프리팹을 조회한다
    public bool TryGetNormalPrefabForType(NormalZombieType type, out PoolObject prefab)
    {
        prefab = null;
        if (normalZombiePrefabMap == null)
        {
            return false;
        }

        for (int i = 0; i < normalZombiePrefabMap.Length; i++)
        {
            NormalZombiePrefabBinding binding = normalZombiePrefabMap[i];
            if (binding != null && binding.ZombieType == type && binding.Prefab != null)
            {
                prefab = binding.Prefab;
                return true;
            }
        }

        return false;
    }

    // enum 타입으로 보스 좀비 프리팹을 조회한다
    public bool TryGetBossPrefabForType(BossZombieType type, out PoolObject prefab)
    {
        prefab = null;
        if (bossZombiePrefabMap == null)
        {
            return false;
        }

        for (int i = 0; i < bossZombiePrefabMap.Length; i++)
        {
            BossZombiePrefabBinding binding = bossZombiePrefabMap[i];
            if (binding != null && binding.BossType == type && binding.Prefab != null)
            {
                prefab = binding.Prefab;
                return true;
            }
        }

        return false;
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

    // 현재 웨이브에서 첫 스폰을 보스로 대체할지 반환한다
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
        if (bossSpawnSchedules != null)
        {
            for (int i = 0; i < bossSpawnSchedules.Length; i++)
            {
                if (bossSpawnSchedules[i] != null)
                {
                    bossSpawnSchedules[i].Validate();
                }
            }
        }

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
/// 보스 좀비 타입별 최초 출현 웨이브와 반복 간격을 정의한다.
/// </summary>
[Serializable]
public class BossZombieSpawnSchedule
{
    [SerializeField] private BossZombieType bossType;
    [SerializeField, Min(1)] private int firstWave = 1;
    [SerializeField, Min(1)] private int waveInterval = 1;

    public BossZombieType BossType => bossType;

    public int FirstWave => Mathf.Max(1, firstWave);

    public int WaveInterval => Mathf.Max(1, waveInterval);

    // 지정한 웨이브가 이 보스 스케줄에 해당하는지 확인한다
    public bool IsScheduledForWave(int wave)
    {
        int safeWave = Mathf.Max(1, wave);
        int safeFirstWave = FirstWave;
        int safeInterval = WaveInterval;
        return safeWave >= safeFirstWave && (safeWave - safeFirstWave) % safeInterval == 0;
    }

    // 인스펙터 입력값을 유효한 범위로 보정한다
    public void Validate()
    {
        firstWave = Mathf.Max(1, firstWave);
        waveInterval = Mathf.Max(1, waveInterval);
    }
}

/// <summary>
/// NormalZombieType과 스폰할 PoolObject 프리팹을 연결한다.
/// </summary>
[Serializable]
public class NormalZombiePrefabBinding
{
    [SerializeField] private NormalZombieType zombieType;
    [SerializeField] private PoolObject prefab;

    public NormalZombieType ZombieType => zombieType;
    public PoolObject Prefab => prefab;
}

/// <summary>
/// BossZombieType과 스폰할 PoolObject 프리팹을 연결한다.
/// </summary>
[Serializable]
public class BossZombiePrefabBinding
{
    [SerializeField] private BossZombieType bossType;
    [SerializeField] private PoolObject prefab;

    public BossZombieType BossType => bossType;
    public PoolObject Prefab => prefab;
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
    [SerializeField, InspectorName("Spawn Boss As First Enemy"), Tooltip("현재 웨이브의 첫 스폰을 보스로 대체할지 여부입니다. 기존 직렬화 이름은 에셋 호환을 위해 유지합니다.")]
    private bool spawnBossAsLastEnemy = true;

    [Header("Normal Zombies")]
    [SerializeField] private NormalZombieSpawnEntry[] normalZombieEntries;

    [Header("Boss Zombies")]
    [SerializeField] private BossZombieSpawnEntry[] bossZombieEntries;

    [Header("Runtime Multipliers")]
    [SerializeField, Min(0.0f)] private float hpMultiplier = 1.0f;
    [SerializeField, Min(0.0f)] private float attackDamageMultiplier = 1.0f;
    [SerializeField, Min(0.0f)] private float moveAttackSpeedMultiplier = 1.0f;
    [SerializeField, Min(0.0f)] private float rewardMultiplier = 1.0f;

    public int MinWave => minWave;
    public float SpawnInterval => spawnInterval;
    public int SpawnCount => spawnCount;
    public bool SpawnBossAsLastEnemy => spawnBossAsLastEnemy;
    public NormalZombieSpawnEntry[] NormalZombieEntries => normalZombieEntries;
    public BossZombieSpawnEntry[] BossZombieEntries => bossZombieEntries;

    // 지정한 웨이브가 이 스테이지 범위에 포함되는지 확인한다
    public bool IsWaveMatch(int wave)
    {
        int safeWave = Mathf.Max(1, wave);
        return safeWave >= minWave && (maxWave <= 0 || safeWave <= maxWave);
    }

    // 가중치 기반으로 일반 좀비 타입을 선택한다
    public bool TrySelectNormalZombieType(int wave, out NormalZombieType type)
    {
        type = default;
        if (normalZombieEntries == null || normalZombieEntries.Length == 0)
        {
            return false;
        }

        int totalWeight = 0;
        for (int i = 0; i < normalZombieEntries.Length; i++)
        {
            NormalZombieSpawnEntry entry = normalZombieEntries[i];
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
        int accumulated = 0;
        for (int i = 0; i < normalZombieEntries.Length; i++)
        {
            NormalZombieSpawnEntry entry = normalZombieEntries[i];
            if (entry == null || !entry.IsAvailable(wave))
            {
                continue;
            }

            accumulated += entry.Weight;
            if (selectedWeight < accumulated)
            {
                type = entry.ZombieType;
                return true;
            }
        }

        return false;
    }

    // 가중치 기반으로 보스 좀비 타입을 선택한다
    public bool TrySelectBossZombieType(int wave, out BossZombieType type)
    {
        type = default;
        if (bossZombieEntries == null || bossZombieEntries.Length == 0)
        {
            return false;
        }

        int totalWeight = 0;
        for (int i = 0; i < bossZombieEntries.Length; i++)
        {
            BossZombieSpawnEntry entry = bossZombieEntries[i];
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
        int accumulated = 0;
        for (int i = 0; i < bossZombieEntries.Length; i++)
        {
            BossZombieSpawnEntry entry = bossZombieEntries[i];
            if (entry == null || !entry.IsAvailable(wave))
            {
                continue;
            }

            accumulated += entry.Weight;
            if (selectedWeight < accumulated)
            {
                type = entry.BossType;
                return true;
            }
        }

        return false;
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

    // 일반 좀비 후보 목록의 인스펙터 입력값을 보정한다
    private static void ValidateEntries(NormalZombieSpawnEntry[] entries)
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

    // 보스 좀비 후보 목록의 인스펙터 입력값을 보정한다
    private static void ValidateEntries(BossZombieSpawnEntry[] entries)
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
/// 일반 좀비 스폰 후보의 타입, 가중치, 웨이브 사용 범위를 정의한다.
/// </summary>
[Serializable]
public class NormalZombieSpawnEntry
{
    [SerializeField] private NormalZombieType zombieType;
    [SerializeField, Min(0)] private int weight = 1;
    [SerializeField, Min(1)] private int minWave = 1;
    [SerializeField, Min(0)] private int maxWave;

    public NormalZombieType ZombieType => zombieType;

    public int Weight => Mathf.Max(0, weight);

    // 지정한 웨이브에서 이 후보를 사용할 수 있는지 확인한다
    public bool IsAvailable(int wave)
    {
        int safeWave = Mathf.Max(1, wave);
        return Weight > 0 &&
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
/// 보스 좀비 스폰 후보의 타입, 가중치, 웨이브 사용 범위를 정의한다.
/// </summary>
[Serializable]
public class BossZombieSpawnEntry
{
    [SerializeField] private BossZombieType bossType;
    [SerializeField, Min(0)] private int weight = 1;
    [SerializeField, Min(1)] private int minWave = 1;
    [SerializeField, Min(0)] private int maxWave;

    public BossZombieType BossType => bossType;

    public int Weight => Mathf.Max(0, weight);

    // 지정한 웨이브에서 이 후보를 사용할 수 있는지 확인한다
    public bool IsAvailable(int wave)
    {
        int safeWave = Mathf.Max(1, wave);
        return Weight > 0 &&
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
