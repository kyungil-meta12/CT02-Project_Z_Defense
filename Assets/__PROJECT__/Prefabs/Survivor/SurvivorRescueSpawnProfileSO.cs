using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 웨이브별 생존자 구출 스폰 시도 여부와 확률을 정의한다.
/// </summary>
[CreateAssetMenu(fileName = "SurvivorRescueSpawnProfile", menuName = "Project Z Defense/Survivor Rescue Spawn Profile")]
public class SurvivorRescueSpawnProfileSO : ScriptableObject
{
    [Header("웨이브별 스폰 규칙")]
    [SerializeField] private SurvivorRescueSpawnStage[] stages;

    // 지정 웨이브에서 생존자 스폰을 시도할 확률을 반환한다
    public bool TryGetSpawnChance(int wave, out float spawnChance)
    {
        spawnChance = 0.0f;
        SurvivorRescueSpawnStage stage = GetStageForWave(wave);
        if (stage == null)
        {
            return false;
        }

        spawnChance = stage.SpawnChance;
        return spawnChance > 0.0f;
    }

    // 지정 웨이브와 정확히 일치하는 스폰 규칙을 반환한다
    private SurvivorRescueSpawnStage GetStageForWave(int wave)
    {
        int safeWave = Mathf.Max(1, wave);
        if (stages == null)
        {
            return null;
        }

        for (int i = 0; i < stages.Length; i++)
        {
            SurvivorRescueSpawnStage stage = stages[i];
            if (stage == null)
            {
                continue;
            }

            if (stage.Wave == safeWave)
            {
                return stage;
            }
        }

        return null;
    }

    // 인스펙터 값이 유효 범위를 벗어나지 않도록 보정한다
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
/// 특정 단일 웨이브에서 생존자 구출 스폰을 시도할 확률을 정의한다.
/// </summary>
[Serializable]
public class SurvivorRescueSpawnStage
{
    [Header("웨이브")]
    [FormerlySerializedAs("minWave")]
    [SerializeField, Min(1)] private int wave = 1;

    [Header("스폰 확률")]
    [SerializeField, Range(0.0f, 1.0f)] private float spawnChance = 0.2f;

    public int Wave
    {
        get
        {
            return Mathf.Max(1, wave);
        }
    }

    public float SpawnChance
    {
        get
        {
            return Mathf.Clamp01(spawnChance);
        }
    }

    // 인스펙터 입력값을 유효한 범위로 보정한다
    public void Validate()
    {
        wave = Mathf.Max(1, wave);
        spawnChance = Mathf.Clamp01(spawnChance);
    }
}
